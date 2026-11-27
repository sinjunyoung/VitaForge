using Common.Models;
using Vita.Core.Models;
using Vita.Core.Services;

namespace Vita.Core.TitleRules;

internal interface ITitleRule
{
    string TitleId { get; }

    IReadOnlyList<PatchContainerFile> RewritePatchFiles(ContentCategory category, IReadOnlyList<PatchContainerFile> files) => files;

    IReadOnlyList<PatchContainerFile> RewritePatchFiles(ContentCategory category, IReadOnlyList<PatchContainerFile> files, ISourceAccessor accessor) => RewritePatchFiles(category, files);

    byte[] RewriteSource(string relativePath, byte[] sourceBytes, OwnerContext owner) => sourceBytes;

    IEnumerable<ExpandedFile> ExpandOutput(string relativePath, byte[] outputBytes, Action<string, LogLevel> log) => [];

    long EstimateExpandedSize(string relativePath, byte[] outputBytes) => 0;

    IEnumerable<ExtraPatchFile> SelectExtraFiles(IReadOnlyList<PatchContainerFile> files) => [];

    long EstimateGeneratedSize(MergeGroup group, OutputTarget target) => 0;

    Task<GenerationResult> GenerateFilesAsync(MergeGroup group, OutputTarget target, string tempDirectory, Func<ExpandedFile, Task> emit, Action<long> onBytes, Action<string, LogLevel> log, CancellationToken ct) => Task.FromResult(default(GenerationResult));
}