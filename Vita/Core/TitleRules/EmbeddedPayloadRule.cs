using Common.Models;
using Vita.Core.Models;
using Vita.Core.Services;

namespace Vita.Core.TitleRules;

internal sealed class EmbeddedPayloadRule(string titleId, EmbeddedPayloadRecipe recipe) : ITitleRule
{
    public string TitleId { get; } = titleId;

    public IReadOnlyList<PatchContainerFile> RewritePatchFiles(ContentCategory category, IReadOnlyList<PatchContainerFile> files, ISourceAccessor accessor)
    {
        if (category == ContentCategory.Addcont || FindExecutable(files) is null)
            return files;

        return [];
    }

    public long EstimateGeneratedSize(MergeGroup group)
    {
        if (group.Category != ContentCategory.App || FindExecutable(group.PatchCtx.Files) is null)
            return 0;

        return recipe.Entries.Sum(e => e.EstimateSize(group));
    }

    public Task<GenerationResult> GenerateFilesAsync(MergeGroup group, string tempDirectory, Func<ExpandedFile, Task> emit, Action<long> onBytes, Action<string, LogLevel> log, CancellationToken ct)
    {
        if (group.Category != ContentCategory.App || FindExecutable(group.PatchCtx.Files) is not { } executable)
            return Task.FromResult(default(GenerationResult));

        return new EmbeddedPayloadGenerator(TitleId, group, recipe, executable, emit, onBytes, log).GenerateAsync(tempDirectory, ct);
    }

    private static PatchContainerFile? FindExecutable(IReadOnlyList<PatchContainerFile> files)
        => files
            .Where(f => Path.GetExtension(f.Path).Equals(".exe", StringComparison.OrdinalIgnoreCase))
            .OrderBy(f => f.Path.Count(c => c == '/'))
            .FirstOrDefault();
}
