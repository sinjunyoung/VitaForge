using Common.Models;
using Vita.Core.Models;

namespace Vita.Core.TitleRules;

internal static class TitleRuleRegistry
{
    private static readonly ILookup<string, ITitleRule> Rules = new ITitleRule[]
    {
        new FlattenSubfolderRule("PCSG01216", "Data", "Patch"),
        new SelfDecryptSourceRule("PCSG00009", "eboot.bin"),
        new L7caUnpackRule("PCSG00009", "toidata_release.l7c", "_Data/")
    }.ToLookup(r => r.TitleId, StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyList<PatchContainerFile> RewritePatchFiles(string titleId, ContentCategory category, IReadOnlyList<PatchContainerFile> files)
    {
        foreach (var rule in Rules[titleId])
            files = rule.RewritePatchFiles(category, files);

        return files;
    }

    public static byte[] RewriteSource(string titleId, string relativePath, byte[] sourceBytes, OwnerContext owner)
    {
        foreach (var rule in Rules[titleId])
            sourceBytes = rule.RewriteSource(relativePath, sourceBytes, owner);

        return sourceBytes;
    }

    public static IEnumerable<ExpandedFile> ExpandOutput(string titleId, string relativePath, byte[] outputBytes, Action<string, LogLevel> log)
    {
        foreach (var rule in Rules[titleId])
        {
            foreach (var file in rule.ExpandOutput(relativePath, outputBytes, log))
                yield return file;
        }
    }
}