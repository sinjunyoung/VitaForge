using Vita.Core.Models;
using Vita.Core.Services;

namespace Vita.Core.TitleRules;

internal sealed class SelfDecryptSourceRule(string titleId, params string[] relativePaths) : ITitleRule
{
    private readonly HashSet<string> _paths = new(relativePaths.Select(PatchShared.NormalizeZipPath), StringComparer.OrdinalIgnoreCase);

    public string TitleId { get; } = titleId;

    public byte[] RewriteSource(string relativePath, byte[] sourceBytes, OwnerContext owner)
    {
        if (!_paths.Contains(PatchShared.NormalizeZipPath(relativePath)))
            return sourceBytes;

        return VitaSelfDecryptor.DecryptToFself(sourceBytes, owner.License.Klicensee);
    }
}