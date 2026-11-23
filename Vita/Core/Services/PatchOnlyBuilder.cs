using Common;
using Common.Localization;
using Common.Models;
using Common.Services;
using System.IO.Compression;
using Vita.Core.Models;
using Vita.Core.TitleRules;

namespace Vita.Core.Services;

public static class PatchOnlyBuilder
{
    private static readonly string DefaultTempDirectory = Path.Combine(Path.GetTempPath(), "VitaForge");

    public static Task<PatchOnlyResult> BuildFromEntriesAsync(List<BatchSourceEntry> entries, string defaultPatchPath, string outputZipPath, OutputTarget target, Action<string, LogLevel> log, IProgress<ProgressInfo>? progress = null, PatchTargetResolver? targetResolver = null, string? tempDirectory = null, CancellationToken ct = default)
        => PatchShared.RunWithItemsAsync(entries, log, items => BuildCoreAsync(items, defaultPatchPath, outputZipPath, target, log, progress, targetResolver, tempDirectory, ct));

    private static async Task<PatchOnlyResult> BuildCoreAsync(List<SourceItem> items, string defaultPatchPath, string outputZipPath, OutputTarget target, Action<string, LogLevel> log, IProgress<ProgressInfo>? progress, PatchTargetResolver? targetResolver, string? tempDirectory, CancellationToken ct)
    {
        var patchContexts = new Dictionary<string, PatchContext>(StringComparer.OrdinalIgnoreCase);

        try
        {
            var groups = PatchShared.BuildGroups(items, defaultPatchPath, patchContexts, target, log, ct, targetResolver);
            return await WriteAsync(groups, outputZipPath, target, log, progress, tempDirectory, ct);
        }
        finally
        {
            foreach (var patchCtx in patchContexts.Values)
                patchCtx.Accessor.Dispose();
        }
    }

    internal static async Task<PatchOnlyResult> WriteAsync(List<MergeGroup> groups, string outputZipPath, OutputTarget target, Action<string, LogLevel> log, IProgress<ProgressInfo>? progress, string? tempDirectory, CancellationToken ct)
    {
        long totalBytes = 0;

        foreach (var g in groups)
            totalBytes += g.Targets.Sum(t => t.EstimatedSize);

        var reporter = new ProgressReporter(Loc.S.StatusApplyingPatch, string.Empty, totalBytes, progress);
        var writtenEntries = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int matched = 0;
        int success = 0;

        Directory.CreateDirectory(Path.GetDirectoryName(outputZipPath)!);

        using var zipStream = new FileStream(outputZipPath, FileMode.Create, FileAccess.Write);
        using var zip = new ZipArchive(zipStream, ZipArchiveMode.Create);

        foreach (var group in groups)
        {
            ct.ThrowIfCancellationRequested();

            int groupMatched = 0;
            int groupSuccess = 0;

            foreach (var t in group.Targets)
            {
                ct.ThrowIfCancellationRequested();

                bool strict = t.IsStrict;

                if (strict)
                    groupMatched++;

                string prefix = PatchShared.GetPatchedPrefix(group.Category, target);
                string entryPath = PatchShared.BuildEntryPath(prefix, group, t.RelativePath);

                if (!writtenEntries.Add(entryPath))
                {
                    log(Loc.S.LogVitaPatchOnlyDuplicateSkipped(group.Category, t.RelativePath), LogLevel.Highlight);
                    continue;
                }

                bool streamed = PatchShared.RequiresStreaming(t);

                try
                {
                    using var streamedResult = streamed ? await PatchShared.ResolveTargetStreamAsync(t, tempDirectory ?? DefaultTempDirectory, reporter.AddProgress, log, ct) : null;
                    byte[]? outputBytes = streamed ? null : await PatchShared.ResolveTargetBytesAsync(t, log, ct);

                    if (outputBytes is null && streamedResult is null)
                    {
                        writtenEntries.Remove(entryPath);

                        if (!streamed)
                            reporter.AddProgress(t.EstimatedSize);

                        continue;
                    }

                    if (!strict)
                        groupMatched++;

                    var zipEntry = zip.CreateEntry(entryPath, CompressionLevel.NoCompression);

                    if (outputBytes is not null)
                    {
                        await PatchShared.WriteEntryWithProgressAsync(zipEntry, outputBytes, t.EstimatedSize, reporter, ct);

                        foreach (var extra in TitleRuleRegistry.ExpandOutput(group.TitleId, t.RelativePath, outputBytes, log))
                        {
                            string extraPath = PatchShared.BuildEntryPath(prefix, group, extra.RelativePath);

                            if (!writtenEntries.Add(extraPath))
                                continue;

                            var extraEntry = zip.CreateEntry(extraPath, CompressionLevel.NoCompression);

                            await PatchShared.WriteEntryWithProgressAsync(extraEntry, extra.Data, 0, reporter, ct);
                        }
                    }
                    else
                    {
                        var content = streamedResult!.Content;

                        using var entryStream = zipEntry.Open();
                        await using var source = content.Open();

                        await PatchShared.CopyStreamWithProgressAsync(source, entryStream, content.Length, 0, reporter, ct);
                    }

                    groupSuccess++;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    if (!strict)
                        groupMatched++;

                    log(Loc.S.LogVitaItemProcessFailed(group.Category, t.RelativePath, ex.Message), LogLevel.Error);

                    if (!streamed)
                        reporter.AddProgress(t.EstimatedSize);
                }
            }

            matched += groupMatched;
            success += groupSuccess;

            LogGroupResult(log, group, groupMatched, groupSuccess);

            if (target == OutputTarget.Emu)
            {
                foreach (var owner in group.Owners)
                    PatchShared.WriteLicenseEntry(zip, writtenEntries, owner);
            }
        }

        reporter.ForceReport();

        return new PatchOnlyResult { MatchedCandidates = matched, PatchedSuccessfully = success };
    }

    private static void LogGroupResult(Action<string, LogLevel> log, MergeGroup group, int matched, int success)
    {
        if (matched == 0)
            return;

        string name = group.Category == ContentCategory.Addcont ? $"{group.TitleId}/{group.ContentIdSuffix}" : group.TitleId;

        if (success == matched)
            log(Loc.S.LogVitaPatchAllDone(group.Category, name, matched, success), LogLevel.Ok);
        else
            log(Loc.S.LogVitaPatchPartial(group.Category, name, matched, success, matched - success), LogLevel.Error);
    }
}