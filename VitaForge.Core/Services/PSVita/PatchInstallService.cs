using VitaForge.Core.Services.PSVita.PatchHosts;
using Common.Localization;
using Common.Models;
using Common.Services;
using VitaForge.Core.Models.PSVita;
using System.IO;
using Vita.Core.Models;
using Vita.Core.Services;

namespace VitaForge.Core.Services.PSVita;

public static class PatchInstallService
{
    public static async Task<List<PatchCatalogItem>> RunAsync(IReadOnlyList<PatchCatalogItem> items, PatchInstallOptions options, PatchTargetResolver? resolver, Action<string, LogLevel> log, IProgress<ProgressInfo>? progress, CancellationToken ct)
    {
        var succeeded = new List<PatchCatalogItem>();
        string? destination = null;

        InstallWorkspace.CleanStale();

        if (options.Mode == PatchOutputMode.Install)
            destination = InstallDestination.Resolve(options.Target, options.TargetPath, log);
        else
            Directory.CreateDirectory(options.TargetPath);

        foreach (var item in items)
        {
            ct.ThrowIfCancellationRequested();

            if (!item.Supports(options.Target))
            {
                log(item.IsConsoleOnly ? Loc.S.LogPsvPatchConsoleOnly(item.Name) : Loc.S.LogPsvPatchEmulatorOnly(item.Name), LogLevel.Error);
                continue;
            }

            var plan = PatchInstallPlanner.Create(item, options.IncludeDlc, log);

            if (plan == null)
                continue;

            string workDir = InstallWorkspace.Create();

            try
            {
                if (await InstallOneAsync(plan, options, workDir, destination, resolver, log, progress, ct))
                    succeeded.Add(item);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                log(Loc.S.LogPsvPatchItemFailed(item.Name, ex.Message), LogLevel.Error);
            }
            finally
            {
                InstallWorkspace.Delete(workDir);
            }
        }

        InstallWorkspace.DeleteRootIfEmpty();

        return succeeded;
    }

    private static async Task<bool> InstallOneAsync(PatchInstallPlan plan, PatchInstallOptions options, string workDir, string? destination, PatchTargetResolver? resolver, Action<string, LogLevel> log, IProgress<ProgressInfo>? progress, CancellationToken ct)
    {
        bool archive = options.Mode == PatchOutputMode.Archive;
        var reporter = new ProgressReporter(Loc.S.StatusDownloading, string.Empty, Math.Max(1, plan.TotalSize), progress);

        log(Loc.S.LogPsvPatchDownloadingPatch(plan.Patch.Name), LogLevel.Highlight);

        string patchLabel = $"{Loc.S.StatusDownloading} ({plan.Patch.Name})";
        ProgressReporter? patchReporter = null;

        var patchProgress = new PatchDownloadProgress(
            total => patchReporter = new ProgressReporter(patchLabel, string.Empty, total, progress),
            bytes =>
            {
                patchReporter ??= new ProgressReporter(patchLabel, string.Empty, 0, progress);
                patchReporter.AddProgress(bytes);
            });

        string patchPath = await PatchFileDownloader.DownloadAsync(plan.Patch.DirectLink, workDir, patchProgress, log, ct);

        patchReporter?.ForceReport();

        if (archive && options.Target == OutputTarget.Emu && TitleExtraInstaller.HasExtraFiles(patchPath, plan.Patch.TitleId))
        {
            log($"{plan.Patch.TitleId}: files outside ux0 (Vita3K textures) cannot be archived. Use install mode.", LogLevel.Error);
            return false;
        }

        var entries = await PkgBatchDownloader.DownloadAsync(plan.Downloads, (item, _) => Utils.GetUniqueFilePath(Path.Combine(workDir, NpsPkgDownloadService.BuildFileName(item))), patchPath, reporter, log, ct);

        string label = options.Target == OutputTarget.Emu ? Loc.S.LabelOutputEmu : Loc.S.LabelOutputRetail;
        var tracker = archive ? null : new WrittenFileTracker();
        string outputPath = archive ? Utils.GetUniqueFilePath(Path.Combine(options.TargetPath, BuildArchiveName(plan, label))) : destination!;

        log(Loc.S.LogVitaMergingGamePatch(label), LogLevel.Highlight);

        MergeResult result;

        try
        {
            result = archive
                ? await Task.Run(() => PatchOutputBuilder.BuildMergedFromEntriesAsync(entries, patchPath, outputPath, options.Target, log, progress, resolver, workDir, ct), ct)
                : await Task.Run(() => PatchOutputBuilder.BuildMergedToFolderAsync(entries, patchPath, outputPath, options.Target, log, progress, resolver, tracker, workDir, ct), ct);
        }
        catch
        {
            if (archive)
                PkgOutputPaths.DeleteQuietly(outputPath);
            else
                InstallRollback.Run(tracker!, log);

            throw;
        }

        log(Loc.S.LogVitaRepackDone(label, result.TotalFiles, result.PatchedSuccessfully, result.PatchCandidates, outputPath), LogLevel.Info);

        foreach (var entry in entries)
            PkgOutputPaths.DeleteQuietly(entry.Path);

        if (result.FailedFiles > 0)
        {
            log(Loc.S.LogVitaFilesFailedDiscarded(result.FailedFiles), LogLevel.Error);

            if (archive)
                PkgOutputPaths.DeleteQuietly(outputPath);
            else
                InstallRollback.Run(tracker!, log);

            return false;
        }

        if (result.PatchedSuccessfully == 0)
        {
            LogPatchArchiveContents(patchPath, log);

            log(Loc.S.LogPsvPatchNothingPatched(plan.Patch.Name), LogLevel.Error);

            if (archive)
                PkgOutputPaths.DeleteQuietly(outputPath);
            else
                InstallRollback.Run(tracker!, log);

            return false;
        }

        if (options.Target == OutputTarget.Emu && !archive)
        {
            try
            {
                TitleExtraInstaller.InstallToEmulatorRoot(patchPath, plan.Patch.TitleId, Path.GetFullPath(options.TargetPath), tracker!, log, ct);
            }
            catch
            {
                InstallRollback.Run(tracker!, log);

                throw;
            }
        }

        if (archive)
        {
            log(Loc.S.LogPsvPatchArchived(plan.Patch.Name, outputPath), LogLevel.Ok);

            return true;
        }

        tracker!.Commit();

        log(Loc.S.LogPsvPatchInstalled(plan.Patch.Name, destination), LogLevel.Ok);

        return true;
    }

    private static string BuildArchiveName(PatchInstallPlan plan, string label)
    {
        string version = string.IsNullOrWhiteSpace(plan.Patch.Version) ? string.Empty : $"_{plan.Patch.Version}";
        string name = $"{plan.Patch.TitleId}_{label}{version}.zip";

        foreach (char c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');

        return name;
    }

    private static void LogPatchArchiveContents(string patchPath, Action<string, LogLevel> log)
    {
        try
        {
            using var accessor = SourceAccessorFactory.Open(patchPath);

            var files = accessor.EnumerateAllFiles().ToList();

            log(Loc.S.LogPsvPatchArchiveContents(files.Count, string.Join(", ", files.Take(12))), LogLevel.Info);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            log(Loc.S.LogError(ex.Message), LogLevel.Error);
        }
    }
}