using Common.Localization;
using Common.Models;
using System.IO;
using Vita.Core.Services;
using VitaForge.Core.Models.PSVita;
using VitaForge.Core.Services.PSVita.PatchHosts;

namespace VitaForge.Core.Services.PSVita;

public static class PatchFileDownloader
{
    public static async Task<string> DownloadAsync(PatchCatalogItem patch, string workDir, string cacheDir, PatchDownloadProgress progress, Action<string, LogLevel> log, CancellationToken ct)
    {
        var links = patch.Links;

        if (links.Count == 1)
        {
            string rawPath = Path.Combine(workDir, "patch.download");

            await FetchAsync(links[0], Path.Combine(cacheDir, "patch.download"), rawPath, progress, log, ct);

            return await PatchArchivePreparer.PrepareAsync(rawPath, workDir, links[0], ct);
        }

        if (patch.IsSplit)
            return await DownloadSplitAsync(links, workDir, cacheDir, progress, log, ct);

        return await DownloadIndividualAsync(links, workDir, cacheDir, progress, log, ct);
    }

    private static async Task<string> DownloadIndividualAsync(IReadOnlyList<PatchLink> links, string workDir, string cacheDir, PatchDownloadProgress progress, Action<string, LogLevel> log, CancellationToken ct)
    {
        string folderPath = Path.Combine(workDir, "patch");

        Directory.CreateDirectory(folderPath);

        for (int i = 0; i < links.Count; i++)
        {
            string rawPath = Path.Combine(workDir, $"patch_{i}.download");

            log(Loc.S.LogPsvPatchDownloadingPart(i + 1, links.Count, DisplayName(links[i])), LogLevel.Info);

            await FetchAsync(links[i], Path.Combine(cacheDir, $"patch_{i}.download"), rawPath, progress, log, ct);
            await PatchArchivePreparer.MergeIntoFolderAsync(rawPath, folderPath, links[i], ct);
        }

        return folderPath;
    }

    private static async Task<string> DownloadSplitAsync(IReadOnlyList<PatchLink> links, string workDir, string cacheDir, PatchDownloadProgress progress, Action<string, LogLevel> log, CancellationToken ct)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var link in links)
        {
            if (!link.HasValidFileName || !names.Add(link.LocalFileName))
                throw new InvalidOperationException(Loc.S.ErrPsvPatchLinkFileName(link.Url));
        }

        string partsPath = Path.Combine(workDir, "patch_parts");

        Directory.CreateDirectory(partsPath);

        var volumePaths = new List<string>();

        for (int i = 0; i < links.Count; i++)
        {
            string volumePath = Path.Combine(partsPath, links[i].LocalFileName);

            log(Loc.S.LogPsvPatchDownloadingPart(i + 1, links.Count, DisplayName(links[i])), LogLevel.Info);

            await FetchAsync(links[i], Path.Combine(cacheDir, "patch_parts", links[i].LocalFileName), volumePath, progress, log, ct);

            volumePaths.Add(volumePath);
        }

        return await PatchArchivePreparer.PrepareSplitAsync(volumePaths, workDir, ct);
    }

    private static async Task FetchAsync(PatchLink link, string cachePath, string workPath, PatchDownloadProgress progress, Action<string, LogLevel> log, CancellationToken ct)
    {
        if (File.Exists(cachePath))
            log(Loc.S.LogPsvDownloadReused(DisplayName(link)), LogLevel.Info);
        else
        {
            Directory.CreateDirectory(Path.GetDirectoryName(cachePath)!);

            string partPath = cachePath + ".part";

            try
            {
                await DownloadLinkAsync(link, partPath, progress, log, ct);

                File.Move(partPath, cachePath, true);
            }
            catch
            {
                PkgOutputPaths.DeleteQuietly(partPath);

                throw;
            }
        }

        Directory.CreateDirectory(Path.GetDirectoryName(workPath)!);

        File.Copy(cachePath, workPath, true);
    }

    private static Task DownloadLinkAsync(PatchLink link, string destinationPath, PatchDownloadProgress progress, Action<string, LogLevel> log, CancellationToken ct) => PatchHostDownloaderRegistry.Resolve(link.Url).DownloadAsync(link, destinationPath, progress, log, ct);

    private static string DisplayName(PatchLink link) => link.LocalFileName.Length > 0 ? link.LocalFileName : link.Url;
}