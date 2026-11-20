using Common.Localization;
using Common.Models;
using Common.Services;
using VitaForge.Core.Models.PSVita;
using System.IO;

namespace VitaForge.Core.Services.PSVita;

public static class NpsPkgDownloadService
{
    public static async Task<List<string>> RunAsync(IReadOnlyList<NpsCatalogItem> items, string outputRoot, Action<string, LogLevel> log, IProgress<ProgressInfo>? progress, CancellationToken ct)
    {
        var targets = items
            .Where(i => !string.IsNullOrWhiteSpace(i.Url))
            .OrderBy(i => i.TitleId, StringComparer.OrdinalIgnoreCase)
            .ThenBy(i => i.Kind)
            .ToList();

        if (targets.Count == 0)
            throw new InvalidOperationException(Loc.S.ErrNpsNoDownloadable);

        Directory.CreateDirectory(outputRoot);

        var reporter = new ProgressReporter(Loc.S.StatusDownloading, string.Empty, Math.Max(1, targets.Sum(i => i.Size)), progress);
        var outputs = new List<string>();

        for (int i = 0; i < targets.Count; i++)
        {
            ct.ThrowIfCancellationRequested();

            var item = targets[i];
            string finalPath = Utils.GetUniqueFilePath(Path.Combine(outputRoot, BuildFileName(item)));
            string partPath = finalPath + ".part";

            reporter.Label = $"{Loc.S.StatusDownloading} ({i + 1}/{targets.Count}): {item.Name}";

            log(Loc.S.LogNpsDownloadingItem(i + 1, targets.Count, item.Name), LogLevel.Info);

            try
            {
                await NpsDownloadService.DownloadVerifiedAsync(item, partPath, reporter.AddProgress, log, ct);

                File.Move(partPath, finalPath);
            }
            catch
            {
                DeleteQuietly(partPath);
                throw;
            }

            outputs.Add(finalPath);
        }

        reporter.ForceReport();

        return outputs;
    }

    private static string BuildFileName(NpsCatalogItem item)
    {
        string name = string.Empty;

        if (Uri.TryCreate(item.Url, UriKind.Absolute, out var uri))
            name = Path.GetFileName(Uri.UnescapeDataString(uri.AbsolutePath));

        if (string.IsNullOrWhiteSpace(name) || !name.EndsWith(".pkg", StringComparison.OrdinalIgnoreCase))
            name = $"{(string.IsNullOrWhiteSpace(item.ContentId) ? item.TitleId : item.ContentId)}_{item.Kind}.pkg";

        foreach (char c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');

        return name;
    }

    private static void DeleteQuietly(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch { }
    }
}
