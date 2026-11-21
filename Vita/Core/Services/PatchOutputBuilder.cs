using Common.Localization;
using Common.Models;
using Common.Services;
using Vita.Core.Models;

namespace Vita.Core.Services;

public static class PatchOutputBuilder
{
    public static Task<MergeResult> BuildMergedFromEntriesAsync(List<BatchSourceEntry> entries, string defaultPatchPath, string outputZipPath, OutputTarget target, Action<string, LogLevel> log, IProgress<ProgressInfo>? progress = null, PatchTargetResolver? targetResolver = null, CancellationToken ct = default)
        => PatchShared.RunWithItemsAsync(entries, log, items => BuildCoreAsync(items, defaultPatchPath, () => new ZipDecryptSink(outputZipPath), target, log, progress, targetResolver, ct));

    public static Task<MergeResult> BuildMergedToFolderAsync(List<BatchSourceEntry> entries, string defaultPatchPath, string outputFolderPath, OutputTarget target, Action<string, LogLevel> log, IProgress<ProgressInfo>? progress = null, PatchTargetResolver? targetResolver = null, CancellationToken ct = default)
        => PatchShared.RunWithItemsAsync(entries, log, items => BuildCoreAsync(items, defaultPatchPath, () => new FolderDecryptSink(outputFolderPath), target, log, progress, targetResolver, ct));

    private static async Task<MergeResult> BuildCoreAsync(List<SourceItem> items, string defaultPatchPath, Func<IDecryptSink> createSink, OutputTarget target, Action<string, LogLevel> log, IProgress<ProgressInfo>? progress, PatchTargetResolver? targetResolver, CancellationToken ct)
    {
        var patchContexts = new Dictionary<string, PatchContext>(StringComparer.OrdinalIgnoreCase);

        try
        {
            var groups = PatchShared.BuildGroups(items, defaultPatchPath, patchContexts, target, log, ct, targetResolver);
            return await WriteAsync(groups, createSink, target, log, progress, ct);
        }
        finally
        {
            foreach (var patchCtx in patchContexts.Values)
                patchCtx.Accessor.Dispose();
        }
    }

    internal static async Task<MergeResult> WriteAsync(List<MergeGroup> groups, Func<IDecryptSink> createSink, OutputTarget target, Action<string, LogLevel> log, IProgress<ProgressInfo>? progress, CancellationToken ct)
    {
        long totalBytes = 0;

        foreach (var g in groups)
            totalBytes += g.Targets.Sum(t => t.EstimatedSize) + GetWriteTotal(g, target);

        var reporter = new ProgressReporter(Loc.S.StatusApplyingPatchAndCompressing, string.Empty, totalBytes, progress);
        var writtenEntries = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int patchCandidates = 0;
        int patchedSuccess = 0;

        async Task<byte[]?> PatchAsync(MergeGroup group, PatchTarget patchTarget)
        {
            try
            {
                byte[]? bytes = await PatchShared.ResolveTargetBytesAsync(patchTarget, log, ct);

                if (bytes is not null)
                {
                    patchCandidates++;
                    patchedSuccess++;
                }

                return bytes;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                patchCandidates++;

                log(Loc.S.LogVitaPatchFailedKeptOriginal(group.Category, patchTarget.RelativePath, ex.Message), LogLevel.Error);

                return null;
            }
            finally
            {
                reporter.AddProgress(patchTarget.EstimatedSize);
            }
        }

        using (var sink = createSink())
        {
            foreach (var group in groups)
            {
                ct.ThrowIfCancellationRequested();

                var targetByPath = group.Targets.ToDictionary(t => t.RelativePath, StringComparer.OrdinalIgnoreCase);
                var handledTargets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                if (target == OutputTarget.Emu)
                {
                    foreach (var (relativePath, appEntry) in group.Index)
                    {
                        ct.ThrowIfCancellationRequested();

                        byte[]? patchedBytes = targetByPath.TryGetValue(relativePath, out var patchTarget) ? await PatchAsync(group, patchTarget) : null;
                        string entryPath = PatchShared.BuildEntryPath(PatchShared.GetBasePrefix(group.Category), group, relativePath);

                        if (patchedBytes is not null)
                        {
                            await WriteEntryAsync(sink, writtenEntries, entryPath, patchedBytes, appEntry.FileEntry.Size, reporter, ct);
                            continue;
                        }

                        WriteDecryptedEntry(sink, writtenEntries, entryPath, group, relativePath, appEntry, reporter, log, ct);
                    }
                }
                else
                {
                    foreach (var owner in group.Owners)
                    {
                        foreach (var (relativePath, size) in PatchShared.GetAllOwnedFiles(owner))
                        {
                            ct.ThrowIfCancellationRequested();

                            if (!WriteOwnedFile(sink, writtenEntries, owner, group, relativePath, size, reporter, log, ct))
                                continue;

                            if (!targetByPath.TryGetValue(relativePath, out var patchTarget))
                                continue;

                            if (!handledTargets.Add(relativePath))
                                continue;

                            byte[]? patchedBytes = await PatchAsync(group, patchTarget);

                            if (patchedBytes != null)
                            {
                                string patchedEntryPath = PatchShared.BuildEntryPath(PatchShared.GetPatchedPrefix(group.Category, target), group, relativePath);

                                await WriteEntryAsync(sink, writtenEntries, patchedEntryPath, patchedBytes, patchTarget.EstimatedSize, reporter, ct);
                            }
                        }
                    }
                }

                if (target == OutputTarget.Emu)
                {
                    foreach (var owner in group.Owners)
                        PatchShared.WriteLicenseEntry(sink, writtenEntries, owner);
                }
            }

            reporter.ForceReport();
        }

        return new MergeResult { TotalFiles = writtenEntries.Count, PatchCandidates = patchCandidates, PatchedSuccessfully = patchedSuccess };
    }

    private static long GetWriteTotal(MergeGroup group, OutputTarget target)
    {
        if (target == OutputTarget.Emu)
            return group.Index.Values.Sum(e => (long)e.FileEntry.Size);

        long ownedTotal = 0;

        foreach (var owner in group.Owners)
            ownedTotal += PatchShared.GetAllOwnedFiles(owner).Sum(f => f.Size);

        return ownedTotal + group.Targets.Sum(t => t.EstimatedSize);
    }

    private static void WriteDecryptedEntry(IDecryptSink sink, HashSet<string> writtenEntries, string entryPath, MergeGroup group, string relativePath, PatchAppEntry appEntry, ProgressReporter reporter, Action<string, LogLevel> log, CancellationToken ct)
    {
        if (!writtenEntries.Add(entryPath))
            return;

        var owner = appEntry.Owner;
        long reported = 0;

        try
        {
            using var entryStream = sink.Open(entryPath);

            NoNpDrmDecryptor.DecryptEntryTo(owner.Item.Accessor!, owner.Item.SourcePath, owner.License.Klicensee, appEntry.FileEntry, owner.Table.UnicvEntries[appEntry.EntryIndex], owner.Table.FilesSalt, entryStream, delta =>
            {
                ct.ThrowIfCancellationRequested();

                reported += delta;

                reporter.AddProgress(delta);
            });
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            writtenEntries.Remove(entryPath);

            log(Loc.S.LogVitaDecryptFailed(group.Category, relativePath, ex.Message), LogLevel.Error);
            reporter.AddProgress(appEntry.FileEntry.Size - reported);
        }
    }

    private static bool WriteOwnedFile(IDecryptSink sink, HashSet<string> writtenEntries, OwnerContext owner, MergeGroup group, string relativePath, long size, ProgressReporter reporter, Action<string, LogLevel> log, CancellationToken ct)
    {
        string baseEntryPath = PatchShared.BuildEntryPath(PatchShared.GetRetailBaseFolder(owner.Item.Category), group, relativePath);

        if (!writtenEntries.Add(baseEntryPath))
            return true;

        string srcRel = $"{owner.Item.SourcePath}/{relativePath}";
        long reported = 0;

        try
        {
            using var entryStream = sink.Open(baseEntryPath);

            SourceStreamCopier.CopyScaled(owner.Item.Accessor!, srcRel, entryStream, size, delta =>
            {
                ct.ThrowIfCancellationRequested();

                reported += delta;

                reporter.AddProgress(delta);
            });
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            writtenEntries.Remove(baseEntryPath);

            log(Loc.S.LogVitaSourceReadFailed(owner.Item.Category, relativePath, ex.Message), LogLevel.Error);
            reporter.AddProgress(size - reported);

            return false;
        }

        return true;
    }

    private static async Task WriteEntryAsync(IDecryptSink sink, HashSet<string> writtenEntries, string entryPath, byte[] data, long estimatedSize, ProgressReporter reporter, CancellationToken ct)
    {
        if (!writtenEntries.Add(entryPath))
            return;

        using var entryStream = sink.Open(entryPath);

        await PatchShared.WriteStreamWithProgressAsync(entryStream, data, estimatedSize, reporter, ct);
    }
}