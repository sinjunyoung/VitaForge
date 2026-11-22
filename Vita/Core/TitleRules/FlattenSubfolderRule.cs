using Vita.Core.Models;

namespace Vita.Core.TitleRules;

internal sealed class FlattenSubfolderRule(string titleId, string parentFolder, string subFolder) : ITitleRule
{
    public string TitleId { get; } = titleId;

    public IReadOnlyList<PatchContainerFile> RewritePatchFiles(ContentCategory category, IReadOnlyList<PatchContainerFile> files)
    {
        if (category == ContentCategory.Addcont)
            return files;

        var moved = new List<PatchContainerFile>();
        var movedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var file in files)
        {
            int at = FindSubFolder(file.Path.Split('/'));

            if (at < 0)
                continue;

            string newPath = string.Join('/', file.Path.Split('/').Where((_, i) => i != at));

            moved.Add(file with { Path = newPath });
            movedPaths.Add(newPath);
        }

        if (moved.Count == 0)
            return files;

        var result = new List<PatchContainerFile>(files.Count);

        foreach (var file in files)
        {
            if (FindSubFolder(file.Path.Split('/')) >= 0 || movedPaths.Contains(file.Path))
                continue;

            result.Add(file);
        }

        result.AddRange(moved);

        return result;
    }

    private int FindSubFolder(string[] segments)
    {
        for (int i = 0; i + 2 < segments.Length; i++)
        {
            if (!segments[i].Equals(parentFolder, StringComparison.OrdinalIgnoreCase) || !segments[i + 1].Equals(subFolder, StringComparison.OrdinalIgnoreCase))
                continue;

            if (i > 0 && !segments[i - 1].Equals(TitleId, StringComparison.OrdinalIgnoreCase))
                continue;

            return i + 1;
        }

        return -1;
    }
}