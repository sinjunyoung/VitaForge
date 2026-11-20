namespace VitaForge.ViewModels.PSVita;

public sealed class PatchTargetNode(string name, string? fullPath)
{
    public string Name { get; } = name;

    public string? FullPath { get; } = fullPath;

    public bool IsFile => FullPath != null;

    public string Glyph => IsFile ? "📄" : "📁";

    public List<PatchTargetNode> Children { get; } = [];

    public static List<PatchTargetNode> Build(IEnumerable<string> paths)
    {
        var root = new PatchTargetNode(string.Empty, null);
        var folders = new Dictionary<string, PatchTargetNode>(StringComparer.OrdinalIgnoreCase);

        foreach (string path in paths)
        {
            string[] segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);

            if (segments.Length == 0)
                continue;

            var current = root;
            string folderPath = string.Empty;

            for (int i = 0; i < segments.Length - 1; i++)
            {
                folderPath = folderPath.Length == 0 ? segments[i] : $"{folderPath}/{segments[i]}";

                if (!folders.TryGetValue(folderPath, out var folder))
                {
                    folder = new PatchTargetNode(segments[i], null);
                    folders[folderPath] = folder;
                    current.Children.Add(folder);
                }

                current = folder;
            }

            current.Children.Add(new PatchTargetNode(segments[^1], path));
        }

        Sort(root);

        return root.Children;
    }

    private static void Sort(PatchTargetNode node)
    {
        node.Children.Sort((a, b) => a.IsFile != b.IsFile ? a.IsFile.CompareTo(b.IsFile) : StringComparer.OrdinalIgnoreCase.Compare(a.Name, b.Name));

        foreach (var child in node.Children)
            Sort(child);
    }
}
