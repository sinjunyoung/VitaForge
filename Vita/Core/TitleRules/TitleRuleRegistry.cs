using Common.Models;
using Vita.Core.Models;
using Vita.Core.Services;

namespace Vita.Core.TitleRules;

internal static class TitleRuleRegistry
{
    private static readonly ILookup<string, ITitleRule> Rules = new ITitleRule[]
    {
        new FlattenSubfolderRule("PCSG01216", "Data", "Patch"),
        new SelfDecryptSourceRule("PCSG00009", FselfLayout.Standard, "eboot.bin"),
        new SelfDecryptSourceRule("PCSA00068", FselfLayout.Vita3K, "eboot.bin"),
        new SelfDecryptSourceRule("PCSG00213", FselfLayout.Vita3K, "eboot.bin"),
        new SelfDecryptSourceRule("PCSG00034", FselfLayout.Vita3K, "eboot.bin"),
        new L7caUnpackRule("PCSG00009", "toidata_release.l7c", "_Data/"),
        new PrefFolderRule("PCSG00684", "textures/import"),
        new PackageManifestRule("PCSG00034"),
        new EmbeddedPayloadRule("PCSG01100", DoukokuSoshiteRecipe.Value),
        new PrefFolderRule("PCSE00240", "textures/import"),
        new EnterButtonRule("PCSE00240", "textures/import"),
        new ChunkPatchRule("PCSE00240")
    }.ToLookup(r => r.TitleId, StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyList<PatchContainerFile> RewritePatchFiles(string titleId, ContentCategory category, IReadOnlyList<PatchContainerFile> files, ISourceAccessor accessor)
    {
        foreach (var rule in Rules[titleId])
            files = rule.RewritePatchFiles(category, files, accessor);

        return files;
    }

    public static IReadOnlyList<ExtraPatchFile> SelectExtraFiles(string titleId, IReadOnlyList<PatchContainerFile> files) => [.. Rules[titleId].SelectMany(r => r.SelectExtraFiles(files)).GroupBy(e => e.TargetPath, StringComparer.OrdinalIgnoreCase).Select(g => g.Last())];

    public static bool SupportsEnterButton(string titleId) => Rules[titleId].OfType<EnterButtonRule>().Any();

    public static byte[] RewriteSource(string titleId, string relativePath, byte[] sourceBytes, OwnerContext owner)
    {
        foreach (var rule in Rules[titleId])
            sourceBytes = rule.RewriteSource(relativePath, sourceBytes, owner);

        return sourceBytes;
    }

    public static long EstimateExpandedSize(string titleId, string relativePath, byte[] outputBytes) => Rules[titleId].Sum(r => r.EstimateExpandedSize(relativePath, outputBytes));

    public static IEnumerable<ExpandedFile> ExpandOutput(string titleId, string relativePath, byte[] outputBytes, Action<string, LogLevel> log)
    {
        foreach (var rule in Rules[titleId])
        {
            foreach (var file in rule.ExpandOutput(relativePath, outputBytes, log))
                yield return file;
        }
    }

    public static long EstimateGeneratedSize(string titleId, MergeGroup group, OutputTarget target) => Rules[titleId].Sum(r => r.EstimateGeneratedSize(group, target));

    public static async Task<GenerationResult> GenerateFilesAsync(string titleId, MergeGroup group, OutputTarget target, string tempDirectory, Func<ExpandedFile, Task> emit, Action<long> onBytes, Action<string, LogLevel> log, CancellationToken ct)
    {
        var total = default(GenerationResult);

        foreach (var rule in Rules[titleId])
            total += await rule.GenerateFilesAsync(group, target, tempDirectory, emit, onBytes, log, ct);

        return total;
    }
}