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
    {
        if (group.Category == ContentCategory.Addcont)
            return FindDlcFiles(group, target).Sum(f => f.Entry.FileEntry.Size * 2);

        return ReadDefinitions(group).Sum(d => group.Index.TryGetValue(d.SourcePath, out var appEntry) ? appEntry.FileEntry.Size + d.TargetSize : d.TargetSize);
    }

    public Task<GenerationResult> GenerateFilesAsync(MergeGroup group, OutputTarget target, string tempDirectory, Func<ExpandedFile, Task> emit, Action<long> onBytes, Action<string, LogLevel> log, CancellationToken ct)
    {
        if (group.Category == ContentCategory.Addcont)
            return GenerateDlcAsync(group, target, emit, onBytes, log, ct);

        var definitions = ReadDefinitions(group);

        if (definitions.Count == 0)
            return Task.FromResult(default(GenerationResult));

        var assets = target == OutputTarget.Retail ? CreateAssets(group) : null;

        return new ChunkPatchGenerator(TitleId, group, target, assets, definitions, emit, onBytes, log).GenerateAsync(ct);
    }

    private Task<GenerationResult> GenerateDlcAsync(MergeGroup group, OutputTarget target, Func<ExpandedFile, Task> emit, Action<long> onBytes, Action<string, LogLevel> log, CancellationToken ct)
    {
        if (target != OutputTarget.Retail)
            return Task.FromResult(default(GenerationResult));

        var assets = CreateAssets(group);
        var files = ChunkPatchDlcFinder.Find(group, assets);

        if (files.Count == 0)
            return Task.FromResult(default(GenerationResult));

        return new ChunkPatchDlcGenerator(TitleId, assets, files, emit, onBytes, log).GenerateAsync(ct);
    }

    private static IReadOnlyList<ChunkPatchDlcFile> FindDlcFiles(MergeGroup group, OutputTarget target)
        => target == OutputTarget.Retail ? ChunkPatchDlcFinder.Find(group, CreateAssets(group)) : [];

    private static ChunkPatchAssets CreateAssets(MergeGroup group) => new(group.PatchCtx.Files, group.PatchCtx.Accessor);

    private static IReadOnlyList<ChunkPatchDefinition> ReadDefinitions(MergeGroup group)
        => group.Category == ContentCategory.App ? ChunkPatchReader.Read(group.PatchCtx.Files, group.PatchCtx.Accessor) : [];
}