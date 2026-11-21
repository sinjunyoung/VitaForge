using Common.Localization;
using Patch.Core.Services;
using SevenZip;
using System.IO;

namespace VitaForge.Core.Services.PSVita;

public static class PatchArchivePreparer
{
    private enum ArchiveKind
    {
        Unknown,
        Zip,
        SevenZip
    }

    public static async Task<string> PrepareAsync(string rawPath, string workDir, string sourceUrl, CancellationToken ct)
    {
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
                throw new NotSupportedException(Loc.S.ErrVitaUnsupportedSourceType(sourceUrl));
        }
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