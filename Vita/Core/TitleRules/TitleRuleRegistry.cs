using Common.Models;
using Vita.Core.Models;

namespace Vita.Core.TitleRules;

internal static class TitleRuleRegistry
{
    private static readonly ILookup<string, ITitleRule> Rules = new ITitleRule[]
    {
        new FlattenSubfolderRule("PCSG01216", "Data", "Patch"),
        new SelfDecryptSourceRule("PCSG00009", FselfLayout.Standard, "eboot.bin"),
        new SelfDecryptSourceRule("PCSA00068", FselfLayout.Vita3K, "eboot.bin"),
        new L7caUnpackRule("PCSG00009", "toidata_release.l7c", "_Data/"),
        new PrefFolderRule("PCSG00684", "textures/import")
    }.ToLookup(r => r.TitleId, StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyList<PatchContainerFile> RewritePatchFiles(string titleId, ContentCategory category, IReadOnlyList<PatchContainerFile> files)
    {
        foreach (var rule in Rules[titleId])
            files = rule.RewritePatchFiles(category, files);

        return files;
    }

    public static IReadOnlyList<ExtraPatchFile> SelectExtraFiles(string titleId, IReadOnlyList<PatchContainerFile> files) => [.. Rules[titleId].SelectMany(r => r.SelectExtraFiles(files))];

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
}