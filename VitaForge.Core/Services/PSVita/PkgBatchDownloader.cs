using Common.Localization;
using Common.Models;
using Common.Services;
using VitaForge.Core.Models.PSVita;
using Vita.Core.Models;
using Vita.Core.Services;

namespace VitaForge.Core.Services.PSVita;

public static class PkgBatchDownloader
{
    public static async Task<List<BatchSourceEntry>> DownloadAsync(IReadOnlyList<(NpsCatalogItem Item, string? License)> downloads, Func<NpsCatalogItem, int, string> pathFor, string? patchPath, ProgressReporter reporter, Action<string, LogLevel> log, CancellationToken ct)
    {
        var entries = new List<BatchSourceEntry>();

        for (int i = 0; i < downloads.Count; i++)
        {
            ct.ThrowIfCancellationRequested();

            var (item, license) = downloads[i];
            string pkgPath = pathFor(item, i);

            reporter.Label = $"{Loc.S.StatusDownloading} ({i + 1}/{downloads.Count}): {item.Name}";

            log(Loc.S.LogNpsDownloadingItem(i + 1, downloads.Count, item.Name), LogLevel.Info);

            await NpsDownloadService.DownloadVerifiedAsync(item, pkgPath, reporter.AddProgress, log, true, ct);

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
}
