using Common.Localization;
using Common.Models;
using Common.Services;
using Vita.Core.Models;

namespace Vita.Core.Services;

public static class DecryptService
{
    public static Task<List<string>> DecryptAsync(List<BatchSourceEntry> entries, string outputRoot, PkgConvertOutput output, Action<string, LogLevel> log, IProgress<ProgressInfo>? progress = null, WrittenFileTracker? tracker = null, CancellationToken ct = default)
        => Task.Run(() => PatchShared.RunWithItemsAsync(entries, log, items => DecryptCoreAsync(items, outputRoot, output, log, progress, tracker, ct)), ct);

    private static async Task<List<string>> DecryptCoreAsync(List<SourceItem> items, string outputRoot, PkgConvertOutput output, Action<string, LogLevel> log, IProgress<ProgressInfo>? progress, WrittenFileTracker? tracker, CancellationToken ct)
    {
        var groups = PatchShared.BuildBaseGroups(items, log, ct);
        long totalBytes = groups.Sum(g =>
        {
            static long selector(PatchAppEntry e) => (long)e.FileEntry.Size;
            return g.Index.Values.Sum(selector);
        });
        var reporter = new ProgressReporter(Loc.S.StatusDecrypting, string.Empty, totalBytes, progress);
        var outputs = new List<string>();

        Directory.CreateDirectory(outputRoot);

        foreach (var titleGroup in groups.GroupBy(g => g.TitleId, StringComparer.OrdinalIgnoreCase).OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase))
        {
            ct.ThrowIfCancellationRequested();

            string outputPath = PkgOutputPaths.Resolve(outputRoot, titleGroup.Key, output);
            (int Count, int Failed) written;

            try
            {
                using IDecryptSink sink = output == PkgConvertOutput.Zip ? new ZipDecryptSink(outputPath) : new FolderDecryptSink(outputPath, tracker);

                written = await WriteTitleAsync([.. titleGroup], sink, reporter, log, ct);

                if (written.Failed > 0 && output == PkgConvertOutput.Direct)
                    throw new InvalidDataException(Loc.S.LogVitaFilesFailedDiscarded(written.Failed));
            }
            catch
            {
                PkgOutputPaths.Discard(outputPath, output);
                throw;
            }

            outputs.Add(outputPath);

            log(Loc.S.LogVitaDecryptGroupOutput(titleGroup.Key, written.Count, outputPath), LogLevel.Ok);
        }

        reporter.ForceReport();

        return outputs;
    }

    private static async Task<(int Count, int Failed)> WriteTitleAsync(List<BaseGroup> groups, IDecryptSink sink, ProgressReporter reporter, Action<string, LogLevel> log, CancellationToken ct)
    {
        var writtenLicenses = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int count = 0;
        int failed = 0;

        foreach (var group in groups)
        {
            foreach (var (relativePath, appEntry) in group.Index)
            {
                ct.ThrowIfCancellationRequested();

                var fileEntry = appEntry.FileEntry;
                var owner = appEntry.Owner;
                string entryPath = PatchShared.BuildEntryPath(PatchShared.GetBasePrefix(group.Category), group.Category, group.TitleId, group.ContentIdSuffix, relativePath);
                long reported = 0;

                try
                {
                    await using var stream = sink.Open(entryPath);

                    NoNpDrmDecryptor.DecryptEntryTo(owner.Item.Accessor!, owner.Item.SourcePath, owner.License.Klicensee, fileEntry, owner.Table.UnicvEntries[appEntry.EntryIndex], owner.Table.FilesSalt, stream, delta =>
                    {
                        ct.ThrowIfCancellationRequested();

                        reported += delta;

                        reporter.AddProgress(delta);
                    });
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    log(Loc.S.LogVitaDecryptFailed(group.Category, relativePath, ex.Message), LogLevel.Error);
                    reporter.AddProgress(fileEntry.Size - reported);
                    failed++;
                    continue;
                }

                count++;
            }

            foreach (var owner in group.Owners)
            {
                if (PatchShared.BuildLicenseEntry(owner) is not { } license || !writtenLicenses.Add(license.EntryPath))
                    continue;

                await using var licenseStream = sink.Open(license.EntryPath);

                await licenseStream.WriteAsync(license.Data, ct);
            }
        }

        return (count, failed);
    }
}