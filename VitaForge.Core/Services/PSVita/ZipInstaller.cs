using Common.Localization;
using Common.Models;
using Common.Services;
using System.IO;
using System.IO.Compression;

namespace VitaForge.Core.Services.PSVita;

public static class ZipInstaller
{
    private const int BufferSize = 81920;

    public static async Task<int> ExtractAsync(string zipPath, string destinationRoot, IProgress<ProgressInfo>? progress, CancellationToken ct)
    {
        string root = Path.GetFullPath(destinationRoot);

        Directory.CreateDirectory(root);

        using var zip = ZipFile.OpenRead(zipPath);
        var reporter = new ProgressReporter(Loc.S.StatusInstalling, string.Empty, Math.Max(1, zip.Entries.Sum(e => e.Length)), progress);
        var buffer = new byte[BufferSize];
        int count = 0;

        foreach (var entry in zip.Entries)
        {
            ct.ThrowIfCancellationRequested();

            if (string.IsNullOrEmpty(entry.Name))
                continue;

            string target = Path.GetFullPath(Path.Combine(root, entry.FullName));
            string relative = Path.GetRelativePath(root, target);

            if (relative.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(relative))
                throw new InvalidDataException(Loc.S.ErrPathNotAllowed(entry.FullName));

            Directory.CreateDirectory(Path.GetDirectoryName(target)!);

            await using var source = entry.Open();
            await using var destination = new FileStream(target, FileMode.Create, FileAccess.Write, FileShare.None, BufferSize, true);
            int read;

            while ((read = await source.ReadAsync(buffer, ct)) > 0)
            {
                await destination.WriteAsync(buffer.AsMemory(0, read), ct);

                reporter.AddProgress(read);
            }

            count++;
        }

        reporter.ForceReport();

        return count;
    }
}