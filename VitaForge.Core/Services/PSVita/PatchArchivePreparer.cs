using Common.Localization;
using Patch.Core.Services;
using SevenZip;
using System.IO;
using VitaForge.Core.Models.PSVita;

namespace VitaForge.Core.Services.PSVita;

public static class PatchArchivePreparer
{
    private enum ArchiveKind
    {
        Unknown,

        Zip,

        SevenZip
    }

    public static async Task<string> PrepareAsync(string rawPath, string workDir, PatchLink link, CancellationToken ct)
    {
        string folderPath = Path.Combine(workDir, "patch");

        switch (Detect(rawPath))
        {
            case ArchiveKind.Zip:
                string zipPath = Path.Combine(workDir, "patch.zip");

                File.Move(rawPath, zipPath);

                return zipPath;

            case ArchiveKind.SevenZip:
                await Task.Run(() => ExtractSevenZip(rawPath, folderPath, true, ct), ct);
                File.Delete(rawPath);

                return folderPath;

            default:
                MoveRawFile(rawPath, folderPath, link);

                return folderPath;
        }
    }

    public static async Task MergeIntoFolderAsync(string rawPath, string folderPath, PatchLink link, CancellationToken ct)
    {
        switch (Detect(rawPath))
        {
            case ArchiveKind.Zip:
            case ArchiveKind.SevenZip:
                await Task.Run(() => ExtractSevenZip(rawPath, folderPath, false, ct), ct);
                File.Delete(rawPath);
                break;

            default:
                MoveRawFile(rawPath, folderPath, link);
                break;
        }
    }

    public static async Task ExtractSplitAsync(IReadOnlyList<string> volumePaths, string folderPath, CancellationToken ct)
    {
        string mainVolumePath = PickMainVolume(volumePaths);

        await Task.Run(() => ExtractSevenZip(mainVolumePath, folderPath, true, ct), ct);

        foreach (string volumePath in volumePaths)
            DeleteFileQuietly(volumePath);
    }

    private static string PickMainVolume(IReadOnlyList<string> volumePaths)
        => volumePaths.FirstOrDefault(p => p.EndsWith(".001", StringComparison.OrdinalIgnoreCase))
            ?? volumePaths.FirstOrDefault(p => p.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) || p.EndsWith(".7z", StringComparison.OrdinalIgnoreCase))
            ?? volumePaths[0];

    private static void MoveRawFile(string rawPath, string folderPath, PatchLink link)
    {
        if (!link.HasValidFileName)
            throw new NotSupportedException(Loc.S.ErrVitaUnsupportedSourceType(link.Url));

        Directory.CreateDirectory(folderPath);

        File.Move(rawPath, Path.Combine(folderPath, link.LocalFileName), true);
    }

    private static void ExtractSevenZip(string archivePath, string folderPath, bool deleteOnFailure, CancellationToken ct)
    {
        NativeSevenZip.EnsureInitialized();

        Directory.CreateDirectory(folderPath);

        try
        {
            using (var extractor = new SevenZipExtractor(archivePath))
            {
                extractor.FileExtractionStarted += (_, e) =>
                {
                    if (ct.IsCancellationRequested)
                        e.Cancel = true;
                };

                extractor.ExtractArchive(folderPath);
            }

            ct.ThrowIfCancellationRequested();
        }
        catch
        {
            if (deleteOnFailure)
                DeleteFolderQuietly(folderPath);

            throw;
        }
    }

    private static void DeleteFileQuietly(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch { }
    }

    private static void DeleteFolderQuietly(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, true);
        }
        catch { }
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
