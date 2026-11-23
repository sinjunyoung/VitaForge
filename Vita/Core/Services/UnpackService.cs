using Common.Localization;
using Common.Models;
using Common.Services;
using Vita.Core.Models;

namespace Vita.Core.Services;

public static class UnpackService
{
    public static Task<int> UnpackAsync(List<BatchSourceEntry> entries, string unpackedRoot, Action<string, LogLevel> log, IProgress<ProgressInfo>? progress = null, CancellationToken ct = default)
        => Task.Run(() => PatchShared.RunWithItemsAsync(entries, log, items => UnpackCoreAsync(items, unpackedRoot, log, progress, ct)), ct);

    private static async Task<int> UnpackCoreAsync(List<SourceItem> items, string unpackedRoot, Action<string, LogLevel> log, IProgress<ProgressInfo>? progress, CancellationToken ct)
    {
        var groups = PatchShared.BuildBaseGroups(items, log, ct);

        long totalBytes = 0;

        foreach (var g in groups)
            foreach (var o in g.Owners)
                totalBytes += PatchShared.GetAllOwnedFiles(o).Sum(f => f.Size);

        var reporter = new ProgressReporter(Loc.S.StatusUnpacking, string.Empty, totalBytes, progress);
        var writtenEntries = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int written = 0;

        foreach (var group in groups)
        {
            ct.ThrowIfCancellationRequested();

            int groupWritten = await Task.Run(() => UnpackGroup(group, unpackedRoot, writtenEntries, reporter, log, ct), ct);

            written += groupWritten;

            string name = group.Category == ContentCategory.Addcont ? $"{group.TitleId}/{group.ContentIdSuffix}" : group.TitleId;

            log(Loc.S.LogVitaUnpackGroupDone(group.Category, name, groupWritten), LogLevel.Ok);
        }

        reporter.ForceReport();

        return written;
    }

    private static int UnpackGroup(BaseGroup group, string unpackedRoot, HashSet<string> writtenEntries, ProgressReporter reporter, Action<string, LogLevel> log, CancellationToken ct)
    {
        int count = 0;

        foreach (var owner in group.Owners)
        {
            string prefix = PatchShared.GetRetailBaseFolder(owner.Item.Category);

            foreach (var (relativePath, size) in PatchShared.GetAllOwnedFiles(owner))
            {
                ct.ThrowIfCancellationRequested();

                string entryPath = PatchShared.BuildEntryPath(prefix, group.Category, group.TitleId, group.ContentIdSuffix, relativePath);

                if (!writtenEntries.Add(entryPath))
                    continue;

                string fullPath = ResolvePath(unpackedRoot, entryPath);
                long reported = 0;

                Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

                try
                {
                    using var stream = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920);

                    SourceStreamCopier.CopyScaled(owner.Item.Accessor!, $"{owner.Item.SourcePath}/{relativePath}", stream, size, delta =>
                    {
                        ct.ThrowIfCancellationRequested();

                        reported += delta;

                        reporter.AddProgress(delta);
                    });
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    DeleteQuietly(fullPath);

                    writtenEntries.Remove(entryPath);

                    log(Loc.S.LogVitaSourceReadFailed(owner.Item.Category, relativePath, ex.Message), LogLevel.Error);

                    if (size > reported)
                        reporter.AddProgress(size - reported);

                    continue;
                }
                catch
                {
                    DeleteQuietly(fullPath);

                    throw;
                }

                count++;
            }
        }

        return count;
    }

    private static string ResolvePath(string unpackedRoot, string entryPath)
    {
        string root = Path.GetFullPath(unpackedRoot) + Path.DirectorySeparatorChar;
        string fullPath = Path.GetFullPath(Path.Combine(unpackedRoot, entryPath.Replace('/', Path.DirectorySeparatorChar)));

        if (!fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException(Loc.S.ErrPathNotAllowed(entryPath));

        return fullPath;
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
