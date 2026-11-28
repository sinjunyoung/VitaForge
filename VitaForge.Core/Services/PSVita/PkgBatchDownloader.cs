using Common.Localization;
using Common.Models;
using Common.Services;
using System.IO;
using VitaForge.Core.Models.PSVita;
using Vita.Core.Models;
using Vita.Core.Services;

namespace VitaForge.Core.Services.PSVita;

public static class PkgBatchDownloader
{
    public static async Task<List<BatchSourceEntry>> DownloadAsync(IReadOnlyList<(NpsCatalogItem Item, string? License)> downloads, string? patchPath, ProgressReporter reporter, Action<string, LogLevel> log, CancellationToken ct)
    {
        var entries = new List<BatchSourceEntry>();

        for (int i = 0; i < downloads.Count; i++)
        {
            ct.ThrowIfCancellationRequested();

            var (item, license) = downloads[i];
            string pkgPath = DownloadCache.PkgPath(item);

            reporter.Label = $"{Loc.S.StatusDownloading} ({i + 1}/{downloads.Count}): {item.Name}";

            log(Loc.S.LogNpsDownloadingItem(i + 1, downloads.Count, item.Name), LogLevel.Info);

            await FetchAsync(item, pkgPath, reporter, log, ct);

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

        return entries;
    }

    private static async Task FetchAsync(NpsCatalogItem item, string pkgPath, ProgressReporter reporter, Action<string, LogLevel> log, CancellationToken ct)
    {
        if (DownloadCache.IsComplete(pkgPath, item))
        {
            log(Loc.S.LogPsvDownloadReused(Path.GetFileName(pkgPath)), LogLevel.Info);

            reporter.AddProgress(item.Size);

            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(pkgPath)!);

        string partPath = pkgPath + ".part";

        try
        {
            await NpsDownloadService.DownloadVerifiedAsync(item, partPath, reporter.AddProgress, log, true, ct);

            File.Move(partPath, pkgPath, true);
        }
        catch
        {
            PkgOutputPaths.DeleteQuietly(partPath);

            throw;
        }
    }
}