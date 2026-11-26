using Common.Localization;
using Common.Models;
using Common.Services;
using Vita.Core.Models;
using Vita.Core.TitleRules;
using Patch.Core.Models;

namespace Vita.Core.Services;

public static class PatchOutputBuilder
{
    private static readonly string DefaultTempDirectory = Path.Combine(Path.GetTempPath(), "VitaForge");

    public static Task<MergeResult> BuildMergedFromEntriesAsync(List<BatchSourceEntry> entries, string defaultPatchPath, string outputZipPath, OutputTarget target, Action<string, LogLevel> log, IProgress<ProgressInfo>? progress = null, PatchTargetResolver? targetResolver = null, PatchMatchOptions? matchOptions = null, string? tempDirectory = null, CancellationToken ct = default)
        => PatchShared.RunWithItemsAsync(entries, log, items => BuildCoreAsync(items, defaultPatchPath, () => new ZipDecryptSink(outputZipPath), target, log, progress, targetResolver, matchOptions, tempDirectory, ct));

    public static Task<MergeResult> BuildMergedToFolderAsync(List<BatchSourceEntry> entries, string defaultPatchPath, string outputFolderPath, OutputTarget target, Action<string, LogLevel> log, IProgress<ProgressInfo>? progress = null, PatchTargetResolver? targetResolver = null, PatchMatchOptions? matchOptions = null, WrittenFileTracker? tracker = null, string? tempDirectory = null, CancellationToken ct = default)
        => PatchShared.RunWithItemsAsync(entries, log, items => BuildCoreAsync(items, defaultPatchPath, () => new FolderDecryptSink(outputFolderPath, tracker), target, log, progress, targetResolver, matchOptions, tempDirectory, ct));

    private static async Task<MergeResult> BuildCoreAsync(List<SourceItem> items, string defaultPatchPath, Func<IDecryptSink> createSink, OutputTarget target, Action<string, LogLevel> log, IProgress<ProgressInfo>? progress, PatchTargetResolver? targetResolver, PatchMatchOptions? matchOptions, string? tempDirectory, CancellationToken ct)
    {
        var patchContexts = new Dictionary<string, PatchContext>(StringComparer.OrdinalIgnoreCase);

        try
        {
            var groups = PatchShared.BuildGroups(items, defaultPatchPath, patchContexts, target, log, targetResolver, matchOptions, ct);
            return await WriteAsync(groups, createSink, target, log, progress, tempDirectory, ct);
        }
        finally
        {
            foreach (var patchCtx in patchContexts.Values)
                patchCtx.Accessor.Dispose();
        }
    }

    internal static async Task<MergeResult> WriteAsync(List<MergeGroup> groups, Func<IDecryptSink> createSink, OutputTarget target, Action<string, LogLevel> log, IProgress<ProgressInfo>? progress, string? tempDirectory, CancellationToken ct)
    {
        long totalBytes = 0;

        foreach (var g in groups)
            totalBytes += g.Targets.Sum(t => t.EstimatedSize) + GetWriteTotal(g, target) + TitleRuleRegistry.EstimateGeneratedSize(g.TitleId, g);

        var reporter = new ProgressReporter(Loc.S.StatusApplyingPatch, string.Empty, totalBytes, progress);
        var writtenEntries = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int patchCandidates = 0;
        int patchedSuccess = 0;
        int failedFiles = 0;

        async Task<PatchedPayload?> PatchAsync(MergeGroup group, PatchTarget patchTarget)
        {
            bool streamed = PatchShared.RequiresStreaming(patchTarget);

            try
            {
                PatchedPayload? payload;

                if (streamed)
                {
                    var result = await PatchShared.ResolveTargetStreamAsync(patchTarget, tempDirectory ?? DefaultTempDirectory, reporter.AddProgress, log, ct);

                    payload = result is null ? null : new PatchedPayload(null, result);
                }
                else
                {
                    byte[]? bytes = await PatchShared.ResolveTargetBytesAsync(patchTarget, log, ct);

                    payload = bytes is null ? null : new PatchedPayload(bytes, null);
                }

                if (payload is not null)
                {
                    patchCandidates++;
                    patchedSuccess++;
                }

                return payload;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                patchCandidates++;

                log(Loc.S.LogVitaPatchFailedKeptOriginal(group.Category, patchTarget.RelativePath, ex.Message), LogLevel.Error);

                return null;
            }
            finally
            {
                if (!streamed)
                    reporter.AddProgress(patchTarget.EstimatedSize);
            }
        }

        using (var sink = createSink())
        {
            foreach (var group in groups)
            {
                ct.ThrowIfCancellationRequested();

                var generated = await WriteGeneratedAsync(sink, writtenEntries, group, target, tempDirectory ?? DefaultTempDirectory, reporter, log, ct);

                patchCandidates += generated.Candidates;
                patchedSuccess += generated.Succeeded;

                var targetByPath = group.Targets.ToDictionary(t => t.RelativePath, StringComparer.OrdinalIgnoreCase);
                var handledTargets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                if (target == OutputTarget.Emu)
                {
                    foreach (var (relativePath, appEntry) in group.Index)
                    {
                        ct.ThrowIfCancellationRequested();

                        using var payload = targetByPath.TryGetValue(relativePath, out var patchTarget) ? await PatchAsync(group, patchTarget) : null;
                        string entryPath = PatchShared.BuildEntryPath(PatchShared.GetBasePrefix(group.Category), group, relativePath);

                        if (payload is not null)
                        {
                            await WritePayloadAsync(sink, writtenEntries, entryPath, payload, appEntry.FileEntry.Size, reporter, ct);
                            await WritePayloadExpandedAsync(sink, writtenEntries, PatchShared.GetBasePrefix(group.Category), group, relativePath, payload, reporter, log, ct);
                            continue;
                        }

                        if (!WriteDecryptedEntry(sink, writtenEntries, entryPath, group, relativePath, appEntry, reporter, log, ct))
                            failedFiles++;
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
                            {
                                failedFiles++;

                                continue;
                            }

                            if (!targetByPath.TryGetValue(relativePath, out var patchTarget))
                                continue;

                            if (!handledTargets.Add(relativePath))
                                continue;

                            using var payload = await PatchAsync(group, patchTarget);

                            if (payload != null)
                            {
                                string patchedEntryPath = PatchShared.BuildEntryPath(PatchShared.GetPatchedPrefix(group.Category, target), group, relativePath);

                                await WritePayloadAsync(sink, writtenEntries, patchedEntryPath, payload, patchTarget.EstimatedSize, reporter, ct);
                                await WritePayloadExpandedAsync(sink, writtenEntries, PatchShared.GetPatchedPrefix(group.Category, target), group, relativePath, payload, reporter, log, ct);
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

        return new MergeResult { TotalFiles = writtenEntries.Count, PatchCandidates = patchCandidates, PatchedSuccessfully = patchedSuccess, FailedFiles = failedFiles };
    }

    private static Task<GenerationResult> WriteGeneratedAsync(IDecryptSink sink, HashSet<string> writtenEntries, MergeGroup group, OutputTarget target, string tempDirectory, ProgressReporter reporter, Action<string, LogLevel> log, CancellationToken ct)
    {
        string prefix = PatchShared.GetPatchedPrefix(group.Category, target);

        return TitleRuleRegistry.GenerateFilesAsync(group.TitleId, group, tempDirectory, file => WriteEntryAsync(sink, writtenEntries, PatchShared.BuildEntryPath(prefix, group, file.RelativePath), file.Data, file.Data.Length, reporter, ct), reporter.AddProgress, log, ct);
    }

    private sealed class PatchedPayload(byte[]? bytes, StreamedPatchResult? streamed) : IDisposable
    {
        public byte[]? Bytes { get; } = bytes;

        public StreamedPatchResult? Streamed { get; } = streamed;

        public void Dispose() => Streamed?.Dispose();
    }

    private static async Task WritePayloadAsync(IDecryptSink sink, HashSet<string> writtenEntries, string entryPath, PatchedPayload payload, long estimatedSize, ProgressReporter reporter, CancellationToken ct)
    {
        if (payload.Bytes is { } bytes)
        {
            await WriteEntryAsync(sink, writtenEntries, entryPath, bytes, estimatedSize, reporter, ct);

            return;
        }

        var content = payload.Streamed!.Content;

        if (!writtenEntries.Add(entryPath))
            return;

        using var entryStream = sink.Open(entryPath);
        await using var source = content.Open();

        await PatchShared.CopyStreamWithProgressAsync(source, entryStream, content.Length, estimatedSize, reporter, ct);
    }

    private static Task WritePayloadExpandedAsync(IDecryptSink sink, HashSet<string> writtenEntries, string prefix, MergeGroup group, string relativePath, PatchedPayload payload, ProgressReporter reporter, Action<string, LogLevel> log, CancellationToken ct)
        => payload.Bytes is { } bytes ? WriteExpandedAsync(sink, writtenEntries, prefix, group, relativePath, bytes, reporter, log, ct) : Task.CompletedTask;

    private static async Task WriteExpandedAsync(IDecryptSink sink, HashSet<string> writtenEntries, string prefix, MergeGroup group, string relativePath, byte[] patchedBytes, ProgressReporter reporter, Action<string, LogLevel> log, CancellationToken ct)
    {
        var expansion = new ExpansionProgress(reporter, group.TitleId, relativePath, patchedBytes);

        foreach (var extra in TitleRuleRegistry.ExpandOutput(group.TitleId, relativePath, patchedBytes, log))
        {
            ct.ThrowIfCancellationRequested();

            string entryPath = PatchShared.BuildEntryPath(prefix, group, extra.RelativePath);

            await WriteEntryAsync(sink, writtenEntries, entryPath, extra.Data, expansion.SizeOf(extra), expansion.Reporter, ct);
        }

        expansion.Complete();
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

    private static bool WriteDecryptedEntry(IDecryptSink sink, HashSet<string> writtenEntries, string entryPath, MergeGroup group, string relativePath, PatchAppEntry appEntry, ProgressReporter reporter, Action<string, LogLevel> log, CancellationToken ct)
    {
        if (!writtenEntries.Add(entryPath))
            return true;

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

            return false;
        }

        return true;
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