using Common.Localization;
using Common.Models;
using Common.Services;
using Vita.Core.Models;

namespace Vita.Core.Services;

public static class DecryptService
{
    public static Task<List<string>> DecryptAsync(List<BatchSourceEntry> entries, string outputRoot, PkgConvertOutput output, Action<string, LogLevel> log, IProgress<ProgressInfo>? progress = null, CancellationToken ct = default)
        => Task.Run(() => PatchShared.RunWithItemsAsync(entries, log, items => DecryptCoreAsync(items, outputRoot, output, log, progress, ct)), ct);

    private static async Task<List<string>> DecryptCoreAsync(List<SourceItem> items, string outputRoot, PkgConvertOutput output, Action<string, LogLevel> log, IProgress<ProgressInfo>? progress, CancellationToken ct)
    {
        var groups = PatchShared.BuildBaseGroups(items, log, ct);
        long totalBytes = groups.Sum(g => g.Index.Values.Sum(e => (long)e.FileEntry.Size));
        var reporter = new ProgressReporter(Loc.S.StatusDecrypting, string.Empty, totalBytes, progress);
        var outputs = new List<string>();

        Directory.CreateDirectory(outputRoot);

        foreach (var titleGroup in groups.GroupBy(g => g.TitleId, StringComparer.OrdinalIgnoreCase).OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase))
        {
            ct.ThrowIfCancellationRequested();

            string outputPath = PkgOutputPaths.Resolve(outputRoot, titleGroup.Key, output);
            int count;

            try
            {
                using IDecryptSink sink = output == PkgConvertOutput.Zip ? new ZipDecryptSink(outputPath) : new FolderDecryptSink(outputPath);

                count = await WriteTitleAsync([.. titleGroup], sink, reporter, log, ct);
            }
            catch
            {
                PkgOutputPaths.DeleteQuietly(outputPath);
                throw;
            }

            outputs.Add(outputPath);

            log(Loc.S.LogVitaDecryptGroupOutput(titleGroup.Key, count, outputPath), LogLevel.Ok);
        }

        reporter.ForceReport();

        return outputs;
    }

    private static async Task<int> WriteTitleAsync(List<BaseGroup> groups, IDecryptSink sink, ProgressReporter reporter, Action<string, LogLevel> log, CancellationToken ct)
    {
        var writtenLicenses = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int count = 0;

        foreach (var group in groups)
        {
            foreach (var (relativePath, appEntry) in group.Index)
            {
                ct.ThrowIfCancellationRequested();

                var fileEntry = appEntry.FileEntry;
                var owner = appEntry.Owner;
                byte[] data;

                try
                {
                    data = NoNpDrmDecryptor.DecryptEntry(owner.Item.Accessor!, owner.Item.SourcePath, owner.License.Klicensee, fileEntry, owner.Table.UnicvEntries[appEntry.EntryIndex], owner.Table.FilesSalt);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    log(Loc.S.LogVitaDecryptFailed(group.Category, relativePath, ex.Message), LogLevel.Error);
                    reporter.AddProgress(fileEntry.Size);
                    continue;
                }

                string entryPath = PatchShared.BuildEntryPath(PatchShared.GetBasePrefix(group.Category), group.Category, group.TitleId, group.ContentIdSuffix, relativePath);

                await using (var stream = sink.Open(entryPath))
                    await PatchShared.WriteStreamWithProgressAsync(stream, data, fileEntry.Size, reporter, ct);

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

        return count;
    }
}
