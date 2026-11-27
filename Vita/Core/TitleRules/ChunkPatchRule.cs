using Common.Models;
using Vita.Core.Models;
using Vita.Core.Services;

namespace Vita.Core.TitleRules;

internal sealed class ChunkPatchRule(string titleId) : ITitleRule
{
    public string TitleId { get; } = titleId;

    public IReadOnlyList<PatchContainerFile> RewritePatchFiles(ContentCategory category, IReadOnlyList<PatchContainerFile> files, ISourceAccessor accessor)
    {
        if (category == ContentCategory.Addcont || ChunkPatchReader.Read(files, accessor).Count == 0)
            return files;

        return [];
    }

    public long EstimateGeneratedSize(MergeGroup group, OutputTarget target)
        => ReadDefinitions(group).Sum(d => group.Index.TryGetValue(d.SourcePath, out var appEntry) ? appEntry.FileEntry.Size + d.TargetSize : d.TargetSize);

    public Task<GenerationResult> GenerateFilesAsync(MergeGroup group, OutputTarget target, string tempDirectory, Func<ExpandedFile, Task> emit, Action<long> onBytes, Action<string, LogLevel> log, CancellationToken ct)
    {
        var definitions = ReadDefinitions(group);

        if (definitions.Count == 0)
            return Task.FromResult(default(GenerationResult));

        var assets = target == OutputTarget.Retail ? new ChunkPatchAssets(group.PatchCtx.Files, group.PatchCtx.Accessor) : null;

        return new ChunkPatchGenerator(TitleId, group, target, assets, definitions, emit, onBytes, log).GenerateAsync(ct);
    }

    private static IReadOnlyList<ChunkPatchDefinition> ReadDefinitions(MergeGroup group)
        => group.Category == ContentCategory.App ? ChunkPatchReader.Read(group.PatchCtx.Files, group.PatchCtx.Accessor) : [];
}