using Common.Localization;
using Common.Models;
using Common.Services;
using VitaForge.Core.Models.PSVita;
using System.IO;
using Vita.Core.Models;
using Vita.Core.Services;

namespace VitaForge.Core.Services.PSVita;

public static class NpsDownloadConvertService
{
    private sealed record DownloadPlan(NpsCatalogItem Item, string? License);

    public static async Task<List<string>> RunAsync(IReadOnlyList<NpsCatalogItem> items, string outputRoot, PkgConvertOutput output, OutputTarget target, Action<string, LogLevel> log, IProgress<ProgressInfo>? progress, CancellationToken ct, WrittenFileTracker? tracker = null)
    {
        var plans = BuildPlans(items, log);

        if (plans.Count == 0)
            throw new InvalidOperationException(Loc.S.ErrNpsNoDownloadable);

        Directory.CreateDirectory(outputRoot);

        try
        {
            long totalBytes = Math.Max(1, plans.Sum(p => p.Item.Size));
            var reporter = new ProgressReporter(Loc.S.StatusDownloading, string.Empty, totalBytes, progress);
            var downloads = plans.Select(p => (p.Item, p.License)).ToList();
            var entries = await PkgBatchDownloader.DownloadAsync(downloads, null, reporter, log, ct);

            return target == OutputTarget.Emu
                ? await DecryptService.DecryptAsync(entries, outputRoot, output, log, progress, tracker, ct)
                : await NoNpDrmConvertService.ConvertAsync(entries, outputRoot, output, log, progress, tracker, ct);
        }
        finally
        {
            if (DownloadCache.DeleteAfterInstall)
                DownloadCache.DeletePkgs(plans.Select(p => p.Item));
        }
    }

    private static List<DownloadPlan> BuildPlans(IReadOnlyList<NpsCatalogItem> items, Action<string, LogLevel> log)
    {
        var plans = new List<DownloadPlan>();
        var selectedAppKeys = items.Where(i => i.Kind == NpsEntryKind.Game && i.HasKey).Select(i => i.TitleId).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var latestUpdates = items.Where(i => i.Kind == NpsEntryKind.Update).GroupBy(i => i.TitleId, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.OrderByDescending(i => i.Version, Comparer<string>.Create(NpsCatalogService.CompareVersion)).First(), StringComparer.OrdinalIgnoreCase);

        foreach (var item in items.OrderBy(i => i.TitleId, StringComparer.OrdinalIgnoreCase).ThenBy(i => i.Kind))
        {
            if (item.Kind == NpsEntryKind.Update && !ReferenceEquals(latestUpdates[item.TitleId], item))
            {
                log(Loc.S.LogNpsOlderUpdateSkipped(item.TitleId, item.Name), LogLevel.Info);
                continue;
            }

            string? license = item.ZRif;
            bool convertible = item.HasKey;

            if (item.Kind == NpsEntryKind.Update)
            {
                string? gameKey = NpsCatalogService.Instance.FindGame(item.TitleId)?.Game.ZRif;

                convertible = selectedAppKeys.Contains(item.TitleId) || !string.IsNullOrWhiteSpace(gameKey);
                license = selectedAppKeys.Contains(item.TitleId) ? null : gameKey;
            }

            if (!convertible)
            {
                log(Loc.S.LogNpsKeyMissingSkipped(item.TitleId, item.Name), LogLevel.Error);
                continue;
            }

            plans.Add(new DownloadPlan(item, license));
        }

        return plans;
    }
}