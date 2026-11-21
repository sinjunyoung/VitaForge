using Common.Localization;
using Common.Models;
using Common.Services;
using System.IO;

namespace VitaForge.Core.Services.PSVita;

public static class FolderInstaller
{
    private const int BufferSize = 81920;

    public static async Task<int> MergeAsync(string sourceRoot, string destinationRoot, IProgress<ProgressInfo>? progress, CancellationToken ct)
    {
        string source = Path.GetFullPath(sourceRoot);
        string destination = Path.GetFullPath(destinationRoot);

        Directory.CreateDirectory(destination);

        var files = Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories).Select(p => new FileInfo(p)).ToList();
        bool sameVolume = string.Equals(Path.GetPathRoot(source), Path.GetPathRoot(destination), StringComparison.OrdinalIgnoreCase);
        var reporter = new ProgressReporter(Loc.S.StatusInstalling, string.Empty, Math.Max(1, files.Sum(f => f.Length)), progress);

        foreach (var file in files)
        {
            ct.ThrowIfCancellationRequested();

            string target = Path.Combine(destination, Path.GetRelativePath(source, file.FullName));

            Directory.CreateDirectory(Path.GetDirectoryName(target)!);

            if (sameVolume)
            {
                File.Move(file.FullName, target, true);

                reporter.AddProgress(file.Length);

                continue;
            }

            await CopyAsync(file.FullName, target, reporter, ct);

            File.Delete(file.FullName);
        }

        reporter.ForceReport();

        return files.Count;
    }

    private static async Task CopyAsync(string sourcePath, string targetPath, ProgressReporter reporter, CancellationToken ct)
    {
        await using var source = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize, true);
        await using var target = new FileStream(targetPath, FileMode.Create, FileAccess.Write, FileShare.None, BufferSize, true);

        var buffer = new byte[BufferSize];
        int read;

        while ((read = await source.ReadAsync(buffer, ct)) > 0)
        {
            await target.WriteAsync(buffer.AsMemory(0, read), ct);

            reporter.AddProgress(read);
        }
    }
}