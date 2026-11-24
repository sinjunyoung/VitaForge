using Common.Localization;
using Common.Models;
using System.IO;
using VitaForge.Core.Models.PSVita;
using VitaForge.Core.Services.PSVita.PatchHosts;

namespace VitaForge.Core.Services.PSVita;

public static class PatchFileDownloader
{
    public static async Task<string> DownloadAsync(PatchCatalogItem patch, string workDir, PatchDownloadProgress progress, Action<string, LogLevel> log, CancellationToken ct)
    {
        var links = patch.Links;

        if (links.Count == 1)
        {
            string rawPath = Path.Combine(workDir, "patch.download");

            await DownloadLinkAsync(links[0], rawPath, progress, log, ct);

            return await PatchArchivePreparer.PrepareAsync(rawPath, workDir, links[0], ct);
        }

        if (patch.IsSplit)
            return await DownloadSplitAsync(links, workDir, progress, log, ct);

        return await DownloadIndividualAsync(links, workDir, progress, log, ct);
    }

    private static async Task<string> DownloadIndividualAsync(IReadOnlyList<PatchLink> links, string workDir, PatchDownloadProgress progress, Action<string, LogLevel> log, CancellationToken ct)
    {
        string folderPath = Path.Combine(workDir, "patch");

        Directory.CreateDirectory(folderPath);

        for (int i = 0; i < links.Count; i++)
        {
            string rawPath = Path.Combine(workDir, $"patch_{i}.download");

            log(Loc.S.LogPsvPatchDownloadingPart(i + 1, links.Count, DisplayName(links[i])), LogLevel.Info);

            await DownloadLinkAsync(links[i], rawPath, progress, log, ct);
            await PatchArchivePreparer.MergeIntoFolderAsync(rawPath, folderPath, links[i], ct);
        }

        return folderPath;
    }

    private static async Task<string> DownloadSplitAsync(IReadOnlyList<PatchLink> links, string workDir, PatchDownloadProgress progress, Action<string, LogLevel> log, CancellationToken ct)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var link in links)
        {
            if (!link.HasValidFileName || !names.Add(link.FileName))
                throw new InvalidOperationException(Loc.S.ErrPsvPatchLinkFileName(link.Url));
        }

        string partsPath = Path.Combine(workDir, "patch_parts");

        Directory.CreateDirectory(partsPath);

        var volumePaths = new List<string>();

        for (int i = 0; i < links.Count; i++)
        {
            string volumePath = Path.Combine(partsPath, links[i].FileName);

            log(Loc.S.LogPsvPatchDownloadingPart(i + 1, links.Count, DisplayName(links[i])), LogLevel.Info);

            await DownloadLinkAsync(links[i], volumePath, progress, log, ct);

            volumePaths.Add(volumePath);
        }

        string folderPath = Path.Combine(workDir, "patch");

        await PatchArchivePreparer.ExtractSplitAsync(volumePaths, folderPath, ct);

        return folderPath;
    }

    private static Task DownloadLinkAsync(PatchLink link, string destinationPath, PatchDownloadProgress progress, Action<string, LogLevel> log, CancellationToken ct)
        => PatchHostDownloaderRegistry.Resolve(link.Url).DownloadAsync(link.Url, destinationPath, progress, log, ct);

    private static string DisplayName(PatchLink link) => link.FileName.Length > 0 ? link.FileName : link.Url;
}
