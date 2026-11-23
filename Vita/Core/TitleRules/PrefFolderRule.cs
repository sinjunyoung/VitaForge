using Vita.Core.Models;

namespace Vita.Core.TitleRules;

internal sealed class PrefFolderRule(string titleId, string prefFolder) : ITitleRule
{
    private static readonly string[] AppContentNames = ["Media", "sce_sys", "sce_module", "eboot.bin"];

    private static readonly string[] LayoutDirs = ["app", "rePatch", "addcont", "reAddcont"];    

    public string TitleId { get; } = titleId;

    public IReadOnlyList<PatchContainerFile> RewritePatchFiles(ContentCategory category, IReadOnlyList<PatchContainerFile> files)
    {
        if (category == ContentCategory.Addcont)
            return files;

        return [.. files.Where(f => FindRoot(f.Path.Split('/')) < 0)];
    }

    public IEnumerable<ExtraPatchFile> SelectExtraFiles(IReadOnlyList<PatchContainerFile> files)
    {
        foreach (var file in files)
        {
            var segments = file.Path.Split('/');
            int at = FindRoot(segments);

            if (at < 0 || at + 1 >= segments.Length)
                continue;

            yield return new ExtraPatchFile(file.Raw, $"{prefFolder}/{TitleId}/{string.Join('/', segments[(at + 1)..])}");
        }
    }

    private int FindRoot(string[] segments)
    {
        for (int i = 0; i + 1 < segments.Length; i++)
        {
            if (!segments[i].Equals(TitleId, StringComparison.OrdinalIgnoreCase))
                continue;

            if (i > 0 && LayoutDirs.Contains(segments[i - 1], StringComparer.OrdinalIgnoreCase))
                continue;

            if (AppContentNames.Contains(segments[i + 1], StringComparer.OrdinalIgnoreCase))
                continue;

            return i;
        }

        return -1;
    }
}