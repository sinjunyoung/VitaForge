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
        int installed = 0;

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
                if (await InstallOneAsync(plan, options, workDir, resolver, log, progress, ct))
                    installed++;
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

        return installed;
    }

    private static async Task<bool> InstallOneAsync(PatchInstallPlan plan, PatchInstallOptions options, string workDir, PatchTargetResolver? resolver, Action<string, LogLevel> log, IProgress<ProgressInfo>? progress, CancellationToken ct)
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
        string zipPath = Path.Combine(workDir, "build.zip");

        log(Loc.S.LogVitaMergingGamePatch(label), LogLevel.Highlight);

        var result = await Task.Run(() => PatchOutputBuilder.BuildMergedFromEntriesAsync(entries, patchPath, zipPath, options.Target, log, progress, resolver, ct), ct);

        foreach (var entry in entries)
            DeleteFileQuietly(entry.Path);

        if (result.PatchCandidates > 0 && result.PatchedSuccessfully == 0)
        {
            log(Loc.S.LogPsvPatchNothingPatched(plan.Patch.Name), LogLevel.Error);

            return false;
        }

        string destination = options.Target == OutputTarget.Emu ? Path.Combine(options.InstallRoot, "ux0") : options.InstallRoot;

        log(Loc.S.StatusInstalling, LogLevel.Highlight);

        await ZipInstaller.ExtractAsync(zipPath, destination, progress, ct);

        log(Loc.S.LogPsvPatchInstalled(plan.Patch.Name, destination), LogLevel.Ok);

        return true;
    }

    private static void DeleteFileQuietly(string path)
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