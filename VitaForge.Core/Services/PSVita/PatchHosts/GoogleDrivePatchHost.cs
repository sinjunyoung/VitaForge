using Common.Localization;
using Common.Models;
using VitaForge.Core.Models.PSVita;

namespace VitaForge.Core.Services.PSVita.PatchHosts;

public sealed class GoogleDrivePatchHost : IPatchHostDownloader
{
    public bool CanHandle(string url) => GoogleDriveLink.TryGetFolderId(url, out _) || GoogleDriveLink.TryGetFileId(url, out _);

    public async Task DownloadAsync(string url, string destinationPath, PatchDownloadProgress progress, Action<string, LogLevel> log, CancellationToken ct)
    {
        string fileId;

        if (GoogleDriveLink.TryGetFolderId(url, out string folderId))
            fileId = (await PickFromFolderAsync(folderId, log, ct)).Id;
        else if (GoogleDriveLink.TryGetFileId(url, out string parsedId))
            fileId = parsedId;
        else
            throw new NotSupportedException(Loc.S.ErrVitaUnsupportedSourceType(url));

        await GoogleDriveDownloader.DownloadAsync(fileId, destinationPath, progress.AddBytes, progress.SetTotal, ct);
    }

    private static async Task<GoogleDriveFile> PickFromFolderAsync(string folderId, Action<string, LogLevel> log, CancellationToken ct)
    {
        var files = await GoogleDriveFolderLister.ListAsync(folderId, ct);

        if (files.Count == 0)
            throw new InvalidOperationException(Loc.S.ErrPsvPatchDriveEmptyFolder);

        var archives = files.Where(f => f.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) || f.Name.EndsWith(".7z", StringComparison.OrdinalIgnoreCase)).ToList();
        var candidates = archives.Count > 0 ? archives : files;
        var picked = candidates.OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase).Last();

        log(Loc.S.LogPsvPatchDriveFilePicked(picked.Name, candidates.Count), LogLevel.Info);

        return picked;
    }
}