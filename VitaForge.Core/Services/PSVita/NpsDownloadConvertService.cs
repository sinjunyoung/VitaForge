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

    public static async Task<List<string>> RunAsync(IReadOnlyList<NpsCatalogItem> items, string outputRoot, PkgConvertOutput output, Action<string, LogLevel> log, IProgress<ProgressInfo>? progress, CancellationToken ct)
    {
        var plans = BuildPlans(items, log);

        if (plans.Count == 0)
            throw new InvalidOperationException(Loc.S.ErrNpsNoDownloadable);

        Directory.CreateDirectory(outputRoot);

        string workDir = Path.Combine(outputRoot, ".download_" + Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(workDir);

        try
        {
            long totalBytes = Math.Max(1, plans.Sum(p => p.Item.Size));
            var reporter = new ProgressReporter(Loc.S.StatusDownloading, string.Empty, totalBytes, progress);
            var entries = new List<BatchSourceEntry>();

            for (int i = 0; i < plans.Count; i++)
            {
                ct.ThrowIfCancellationRequested();

                var plan = plans[i];
                string pkgPath = Path.Combine(workDir, $"{plan.Item.TitleId}_{plan.Item.Kind}_{i}.pkg");

                reporter.Label = $"{Loc.S.StatusDownloading} ({i + 1}/{plans.Count}): {plan.Item.Name}";

                log(Loc.S.LogNpsDownloadingItem(i + 1, plans.Count, plan.Item.Name), LogLevel.Info);

                string hash = await NpsDownloadService.DownloadAsync(plan.Item.Url, pkgPath, reporter.AddProgress, ct);

                if (!string.IsNullOrEmpty(plan.Item.Sha256) && !hash.Equals(plan.Item.Sha256, StringComparison.OrdinalIgnoreCase))
                    log(Loc.S.LogNpsHashMismatch(plan.Item.Name), LogLevel.Error);

                var probe = await Task.Run(() => PkgProbe.Probe(pkgPath), ct);

                entries.Add(new BatchSourceEntry
                {
                    Kind = SourceKind.Pkg,
                    Path = pkgPath,
                    License = plan.License,
                    Probe = probe
                });
            }

            reporter.ForceReport();

            return await NoNpDrmConvertService.ConvertAsync(entries, outputRoot, output, log, progress, ct);
        }
        finally
        {
            DeleteQuietly(workDir);
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

    private static void DeleteQuietly(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, true);
        }
        catch { }
    }
}
