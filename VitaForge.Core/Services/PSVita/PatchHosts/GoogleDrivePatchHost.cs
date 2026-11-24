using Common.Localization;
using Common.Models;
using VitaForge.Core.Models.PSVita;

namespace VitaForge.Core.Services.PSVita.PatchHosts;

public sealed class GoogleDrivePatchHost : IPatchHostDownloader
{
    public bool CanHandle(string url) => GoogleDriveLink.TryGetFolderId(url, out _) || GoogleDriveLink.TryGetFileId(url, out _);

    public async Task DownloadAsync(PatchLink patchLink, string destinationPath, PatchDownloadProgress progress, Action<string, LogLevel> log, CancellationToken ct)
    {
        string url = patchLink.Url;
        string fileId;

        if (GoogleDriveLink.TryGetFolderId(url, out string folderId))
            fileId = (await PickFromFolderAsync(folderId, patchLink, log, ct)).Id;
        else if (GoogleDriveLink.TryGetFileId(url, out string parsedId))
            fileId = parsedId;
        else
            throw new NotSupportedException(Loc.S.ErrVitaUnsupportedSourceType(url));

        await GoogleDriveDownloader.DownloadAsync(fileId, destinationPath, progress.AddBytes, progress.SetTotal, ct);
    }

    private static async Task<GoogleDriveFile> PickFromFolderAsync(string folderId, PatchLink patchLink, Action<string, LogLevel> log, CancellationToken ct)
    {
        var files = await GoogleDriveFolderLister.ListAsync(folderId, ct);

        if (files.Count == 0)
            throw new InvalidOperationException(Loc.S.ErrPsvPatchDriveEmptyFolder);

        var sorted = files.OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase).ToList();

        if (patchLink.HasSelector)
        {
            var selected = PatchFileSelector.Select(sorted, f => [f.Name], patchLink);

            log(Loc.S.LogPsvPatchDriveFilePicked(selected.Name, sorted.Count), LogLevel.Info);

            return selected;
        }

        var archives = sorted.Where(f => f.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) || f.Name.EndsWith(".7z", StringComparison.OrdinalIgnoreCase)).ToList();
        var candidates = archives.Count > 0 ? archives : sorted;
        var picked = candidates[^1];

        log(Loc.S.LogPsvPatchDriveFilePicked(picked.Name, candidates.Count), LogLevel.Info);

        return picked;
    }
}