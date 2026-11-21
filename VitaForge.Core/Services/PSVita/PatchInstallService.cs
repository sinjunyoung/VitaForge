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
    private static readonly string WorkRoot = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "temp");

    public static async Task<int> RunAsync(IReadOnlyList<PatchCatalogItem> items, PatchInstallOptions options, PatchTargetResolver? resolver, Action<string, LogLevel> log, IProgress<ProgressInfo>? progress, CancellationToken ct)
    {
        int succeeded = 0;
        string? destination = null;

        if (options.Mode == PatchOutputMode.Install)
            destination = ResolveDestination(options, log);
        else
            Directory.CreateDirectory(options.TargetPath);

        foreach (var item in items)
        {
            ct.ThrowIfCancellationRequested();

            var plan = PatchInstallPlanner.Create(item, options.IncludeDlc, log);

            if (plan == null)
                continue;

            string workDir = Path.Combine(WorkRoot, Guid.NewGuid().ToString("N"));

            Directory.CreateDirectory(workDir);

            try
            {
                if (await InstallOneAsync(plan, options, workDir, destination, resolver, log, progress, ct))
                    succeeded++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                log(Loc.S.LogPsvPatchItemFailed(item.Name, ex.Message), LogLevel.Error);
            }
            finally
            {
                DeleteQuietly(workDir);
            }
        }

        DeleteIfEmpty(WorkRoot);

        return succeeded;
    }

    private static async Task<bool> InstallOneAsync(PatchInstallPlan plan, PatchInstallOptions options, string workDir, string? destination, PatchTargetResolver? resolver, Action<string, LogLevel> log, IProgress<ProgressInfo>? progress, CancellationToken ct)
    {
        var reporter = new ProgressReporter(Loc.S.StatusDownloading, string.Empty, Math.Max(1, plan.TotalSize), progress);

        log(Loc.S.LogPsvPatchDownloadingPatch(plan.Patch.Name), LogLevel.Highlight);

        string patchPath = await PatchFileDownloader.DownloadAsync(plan.Patch.DirectLink, workDir, log, ct);

        var downloads = plan.Downloads;
        var entries = new List<BatchSourceEntry>();

        for (int i = 0; i < downloads.Count; i++)
        {
            ct.ThrowIfCancellationRequested();

            var (item, license) = downloads[i];

            string pkgPath = Utils.GetUniqueFilePath(Path.Combine(workDir, NpsPkgDownloadService.BuildFileName(item)));

            reporter.Label = $"{Loc.S.StatusDownloading} ({i + 1}/{downloads.Count}): {item.Name}";

            log(Loc.S.LogNpsDownloadingItem(i + 1, downloads.Count, item.Name), LogLevel.Info);

            await NpsDownloadService.DownloadVerifiedAsync(item, pkgPath, reporter.AddProgress, log, ct);

            var probe = await Task.Run(() => PkgProbe.Probe(pkgPath), ct);

            entries.Add(new BatchSourceEntry
            {
                Kind = SourceKind.Pkg,
                Path = pkgPath,
                License = license,
                PatchPath = patchPath,
                Probe = probe
            });
        }

        reporter.ForceReport();

        string label = options.Target == OutputTarget.Emu ? Loc.S.LabelOutputEmu : Loc.S.LabelOutputRetail;
        bool archive = options.Mode == PatchOutputMode.Archive;
        string outputPath = archive ? Utils.GetUniqueFilePath(Path.Combine(options.TargetPath, BuildArchiveName(plan, label))) : Path.Combine(workDir, "out");

        log(Loc.S.LogVitaMergingGamePatch(label), LogLevel.Highlight);

        MergeResult result;

        try
        {
            result = archive
                ? await Task.Run(() => PatchOutputBuilder.BuildMergedFromEntriesAsync(entries, patchPath, outputPath, options.Target, log, progress, resolver, ct), ct)
                : await Task.Run(() => PatchOutputBuilder.BuildMergedToFolderAsync(entries, patchPath, outputPath, options.Target, log, progress, resolver, ct), ct);
        }
        catch
        {
            if (archive)
                DeleteFileQuietly(outputPath);

            throw;
        }

        log(Loc.S.LogVitaRepackDone(label, result.TotalFiles, result.PatchedSuccessfully, result.PatchCandidates, outputPath), LogLevel.Info);

        foreach (var entry in entries)
            DeleteFileQuietly(entry.Path);

        if (result.PatchedSuccessfully == 0)
        {
            LogPatchArchiveContents(patchPath, log);

            log(Loc.S.LogPsvPatchNothingPatched(plan.Patch.Name), LogLevel.Error);

            if (archive)
                DeleteFileQuietly(outputPath);

            return false;
        }

        if (archive)
        {
            log(Loc.S.LogPsvPatchArchived(plan.Patch.Name, outputPath), LogLevel.Ok);

            return true;
        }

        log(Loc.S.StatusInstalling, LogLevel.Highlight);

        await FolderInstaller.MergeAsync(outputPath, destination!, progress, ct);

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

    private static string ResolveDestination(PatchInstallOptions options, Action<string, LogLevel> log)
    {
        if (options.Target != OutputTarget.Emu)
            return options.TargetPath;

        var info = Vita3kLocator.Resolve(options.TargetPath);

        log(Loc.S.LogPsvPatchVita3kPrefPath(info.PrefPath), LogLevel.Info);

        return Path.Combine(info.PrefPath, "ux0");
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

    private static void DeleteFileQuietly(string? path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch { }
    }

    private static void DeleteQuietly(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, true);
        }
        catch { }
    }

    private static void DeleteIfEmpty(string path)
    {
        try
        {
            if (Directory.Exists(path) && !Directory.EnumerateFileSystemEntries(path).Any())
                Directory.Delete(path);
        }
        catch { }
    }
}