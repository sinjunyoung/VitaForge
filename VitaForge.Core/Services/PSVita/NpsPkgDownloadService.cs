using Common.Localization;
using Common.Models;
using Common.Services;
using VitaForge.Core.Models.PSVita;
using System.IO;
using System.Text.RegularExpressions;

namespace VitaForge.Core.Services.PSVita;

public static partial class NpsPkgDownloadService
{
    [GeneratedRegex(@"(?<cid>[A-Z]{2}\d{4}-[A-Z]{4}\d{5}_\d{2}-[A-Z0-9]{16})(?:-A(?<a>\d{4})-V(?<v>\d{4}))?", RegexOptions.IgnoreCase)]
    private static partial Regex StandardNameRegex();

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

                if (item.HasKey)
                    File.WriteAllText(Path.ChangeExtension(finalPath, ".zrif"), item.ZRif!.Trim());
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

    public static string BuildFileName(NpsCatalogItem item)
    {
        string urlName = GetUrlFileName(item.Url);
        var urlMatch = StandardNameRegex().Match(urlName);
        string contentId = ResolveContentId(item, urlMatch);

        string name;

        if (string.IsNullOrWhiteSpace(contentId))
        {
            name = urlName.EndsWith(".pkg", StringComparison.OrdinalIgnoreCase) ? urlName : $"{item.TitleId}_{item.Kind}.pkg";
        }
        else if (item.Kind == NpsEntryKind.Update)
        {
            string appVersion = urlMatch.Groups["a"].Success ? urlMatch.Groups["a"].Value : FormatVersion(NpsCatalogService.Instance.FindGame(item.TitleId)?.Game.Version);
            string patchVersion = urlMatch.Groups["v"].Success ? urlMatch.Groups["v"].Value : FormatVersion(item.Version);

            name = $"{contentId}-A{appVersion}-V{patchVersion}.pkg";
        }
        else
            name = $"{contentId}.pkg";

        foreach (char c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');

        return name;
    }

    private static string ResolveContentId(NpsCatalogItem item, Match urlMatch)
    {
        var itemMatch = StandardNameRegex().Match(item.ContentId ?? string.Empty);

        if (itemMatch.Success)
            return itemMatch.Groups["cid"].Value.ToUpperInvariant();

        if (urlMatch.Success)
            return urlMatch.Groups["cid"].Value.ToUpperInvariant();

        var gameMatch = StandardNameRegex().Match(NpsCatalogService.Instance.FindGame(item.TitleId)?.Game.ContentId ?? string.Empty);

        return gameMatch.Success ? gameMatch.Groups["cid"].Value.ToUpperInvariant() : string.Empty;
    }

    private static string GetUrlFileName(string url)
    {
        if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return Path.GetFileName(Uri.UnescapeDataString(uri.AbsolutePath));

        return string.Empty;
    }

    private static string FormatVersion(string? version)
    {
        if (string.IsNullOrWhiteSpace(version))
            return "0000";

        var parts = version.Trim().Split('.');
        string major = new([.. parts[0].Where(char.IsDigit)]);
        string minor = parts.Length > 1 ? new string([.. parts[1].Where(char.IsDigit)]) : string.Empty;

        return major.PadLeft(2, '0') + minor.PadLeft(2, '0');
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