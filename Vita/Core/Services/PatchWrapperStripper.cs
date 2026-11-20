using Patch.Core.Models;
using Patch.Core.Services;

namespace Vita.Core.Services;

internal static class PatchWrapperStripper
{
    private const int MaxDepth = 3;

    public static PatchFileIndex Strip(PatchFileIndex index, IReadOnlyCollection<string> targetPaths)
    {
        var targetSet = new HashSet<string>(targetPaths, StringComparer.OrdinalIgnoreCase);
        var entries = index.Entries.ToList();

        for (int depth = 0; depth < MaxDepth; depth++)
        {
            string? wrapper = FindWrapper(entries, targetSet);

            if (wrapper == null)
                break;

            entries = [.. entries.Select(e => StripEntry(e, wrapper))];
        }

        var result = new PatchFileIndex();

        result.Entries.AddRange(entries);

        return result;
    }

    private static string? FindWrapper(List<PatchFileEntry> entries, HashSet<string> targetSet)
    {
        string? best = null;
        int bestGain = 0;

        var groups = entries
            .Where(e => GetWrapper(e) != null)
            .GroupBy(e => GetWrapper(e)!, StringComparer.OrdinalIgnoreCase);

        foreach (var group in groups)
        {
            int plain = group.Count(e => targetSet.Contains(BuildKey(NormalizeDir(e), e.BaseName)));
            int stripped = group.Count(e => targetSet.Contains(BuildKey(RemoveFirstSegment(NormalizeDir(e)), e.BaseName)));
            int gain = stripped - plain;

            if (gain > bestGain)
            {
                bestGain = gain;
                best = group.Key;
            }
        }

        return best;
    }

    private static PatchFileEntry StripEntry(PatchFileEntry entry, string wrapper)
    {
        string? current = GetWrapper(entry);

        if (current == null || !current.Equals(wrapper, StringComparison.OrdinalIgnoreCase))
            return entry;

        return entry with { RelativeDir = RemoveFirstSegment(NormalizeDir(entry)) };
    }

    private static string? GetWrapper(PatchFileEntry entry)
    {
        if (entry.RelativeDir == PatchFileIndex.LooseOnlyDir)
            return null;

        string dir = NormalizeDir(entry);

        if (dir.Length == 0)
            return null;

        int slash = dir.IndexOf('/');

        return slash < 0 ? dir : dir[..slash];
    }

    private static string NormalizeDir(PatchFileEntry entry) => entry.RelativeDir.Replace(Path.DirectorySeparatorChar, '/');

    private static string RemoveFirstSegment(string dir)
    {
        int slash = dir.IndexOf('/');
        return slash < 0 ? string.Empty : dir[(slash + 1)..];
    }

    private static string BuildKey(string dir, string baseName) => dir.Length == 0 ? baseName : $"{dir}/{baseName}";
}