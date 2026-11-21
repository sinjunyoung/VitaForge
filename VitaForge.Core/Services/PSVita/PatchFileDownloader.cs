using Common.Localization;
using Common.Models;
using Patch.Core.Services;
using SevenZip;
using System.IO;
using VitaForge.Core.Models.PSVita;

namespace VitaForge.Core.Services.PSVita;

public static class PatchFileDownloader
{
    private enum ArchiveKind
    {
        Unknown,

        Zip,

        SevenZip
    }

    public static async Task<string> DownloadAsync(string url, string workDir, Action<string, LogLevel> log, CancellationToken ct)
    {
        string rawPath = Path.Combine(workDir, "patch.download");

        if (GoogleDriveLink.TryGetFolderId(url, out string folderId))
        {
            var file = await PickFromFolderAsync(folderId, log, ct);

            await GoogleDriveDownloader.DownloadAsync(file.Id, rawPath, _ => { }, ct);
        }
        else if (GoogleDriveLink.TryGetFileId(url, out string fileId))
        {
            await GoogleDriveDownloader.DownloadAsync(fileId, rawPath, _ => { }, ct);
        }
        else if (MegaLink.TryParse(url, out var megaLink))
        {
            await MegaDownloader.DownloadAsync(megaLink, rawPath, _ => { }, ct);
        }
        else
        {
            await NpsDownloadService.DownloadAsync(url, rawPath, _ => { }, ct);
        }

        switch (Detect(rawPath))
        {
            case ArchiveKind.Zip:
                string zipPath = Path.Combine(workDir, "patch.zip");

                File.Move(rawPath, zipPath);

                return zipPath;

            case ArchiveKind.SevenZip:
                string folderPath = Path.Combine(workDir, "patch");

                await Task.Run(() => ExtractSevenZip(rawPath, folderPath), ct);

                File.Delete(rawPath);

                return folderPath;

            default:
                throw new NotSupportedException(Loc.S.ErrVitaUnsupportedSourceType(url));
        }
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

    private static void ExtractSevenZip(string archivePath, string folderPath)
    {
        NativeSevenZip.EnsureInitialized();

        Directory.CreateDirectory(folderPath);

        using var extractor = new SevenZipExtractor(archivePath);

        extractor.ExtractArchive(folderPath);
    }

    private static ArchiveKind Detect(string path)
    {
        Span<byte> head = stackalloc byte[6];

        using var stream = File.OpenRead(path);

        int read = stream.Read(head);

        if (read >= 4 && head[0] == 0x50 && head[1] == 0x4B)
            return ArchiveKind.Zip;

        if (read >= 6 && head[0] == 0x37 && head[1] == 0x7A && head[2] == 0xBC && head[3] == 0xAF && head[4] == 0x27 && head[5] == 0x1C)
            return ArchiveKind.SevenZip;

        return ArchiveKind.Unknown;
    }
}