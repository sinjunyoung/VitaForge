using Vita.Core.Models;

namespace Vita.Core.TitleRules;

internal sealed record EmbeddedPayloadEntry(string RelativePath, IReadOnlyList<EmbeddedPayloadVariant> Variants, bool IsSelf = false, bool IsOptional = false)
{
    public long EstimateSize(MergeGroup group)
    {
        if (!group.Index.TryGetValue(RelativePath, out var appEntry))
            return IsOptional ? 0 : Variants[0].TargetSize;

        return appEntry.FileEntry.Size + Variants[0].TargetSize;
    }
}
