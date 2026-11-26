using Common.Models;
using Vita.Core.Models;
using Vita.Core.Services;

namespace Vita.Core.TitleRules;

internal sealed class PackageManifestRule(string titleId) : ITitleRule
{
    public string TitleId { get; } = titleId;

    public IReadOnlyList<PatchContainerFile> RewritePatchFiles(ContentCategory category, IReadOnlyList<PatchContainerFile> files, ISourceAccessor accessor)
    {
        if (category == ContentCategory.Addcont || PackageManifestReader.TryRead(TitleId, files, accessor) is not { } manifest)
            return files;

        var byPath = new Dictionary<string, PatchContainerFile>(StringComparer.OrdinalIgnoreCase);

        foreach (var file in files)
            byPath.TryAdd(file.Path, file);

        var result = new List<PatchContainerFile>();

        foreach (var entry in manifest.Files.Where(e => e.IsDirect))
        {
            if (!byPath.TryGetValue(manifest.PatchPath(entry), out var patch))
                continue;

            result.Add(patch with { Path = $"{PatchShared.NormalizeZipPath(entry.Destination)}{Path.GetExtension(entry.Patch)}" });
        }

        return result;
    }

    public long EstimateGeneratedSize(MergeGroup group)
    {
        if (ReadManifest(group) is not { } manifest)
            return 0;

        long total = manifest.Files.Where(e => !e.IsDirect).Sum(e => e.TargetSize);

        var archives = manifest.Files
            .Where(e => e.SourceKind == PackageManifestSourceKinds.PackageMember)
            .Select(e => PatchShared.NormalizeZipPath(e.Source?.Archive ?? string.Empty))
            .Distinct(StringComparer.OrdinalIgnoreCase);

        foreach (string archive in archives)
        {
            if (group.Index.TryGetValue(archive, out var appEntry))
                total += appEntry.FileEntry.Size;
        }

        return total;
    }

    public Task<GenerationResult> GenerateFilesAsync(MergeGroup group, string tempDirectory, Func<ExpandedFile, Task> emit, Action<long> onBytes, Action<string, LogLevel> log, CancellationToken ct)
    {
        if (ReadManifest(group) is not { } manifest)
            return Task.FromResult(default(GenerationResult));

        return new PackageManifestGenerator(TitleId, group, manifest, emit, onBytes, log).GenerateAsync(tempDirectory, ct);
    }

    private PackageManifest? ReadManifest(MergeGroup group)
        => group.Category == ContentCategory.App ? PackageManifestReader.TryRead(TitleId, group.PatchCtx.Files, group.PatchCtx.Accessor) : null;
}
