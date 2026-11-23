using Patch.Core.Models;
using Patch.Core.Services;

namespace Vita.Core.Services;

internal static class PatchWrapperStripper
{
    private const int MaxDepth = 3;
    private const int MaxAlignRounds = 3;
    private const int MaxAlignSkip = 4;

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

        var targetsByName = targetSet.ToLookup(FileNameOf, StringComparer.OrdinalIgnoreCase);

        for (int round = 0; round < MaxAlignRounds; round++)
        {
            var alignment = FindAlignment(entries, targetSet, targetsByName);

            if (alignment is null)
                break;

            entries = [.. entries.Select(e => Align(e, alignment.Value, targetSet))];
        }

        var result = new PatchFileIndex();

        result.Entries.AddRange(entries);

        return result;
    }

    private readonly record struct Alignment(int Skip, string Prefix);

    private static Alignment? FindAlignment(List<PatchFileEntry> entries, HashSet<string> targetSet, ILookup<string, string> targetsByName)
    {
        var votes = new Dictionary<(int Skip, string Prefix), int>();

        foreach (var entry in entries)
        {
            if (entry.RelativeDir == PatchFileIndex.LooseOnlyDir || entry.BaseName.Length == 0)
                continue;

            string key = BuildKey(NormalizeDir(entry), entry.BaseName);

            if (targetSet.Contains(key))
                continue;

            string[] keySegments = key.Split('/');

            foreach (string target in targetsByName[entry.BaseName])
            {
                string[] targetSegments = target.Split('/');
                int common = 0;

                while (common < keySegments.Length && common < targetSegments.Length
                    && string.Equals(keySegments[^(common + 1)], targetSegments[^(common + 1)], StringComparison.OrdinalIgnoreCase))
                    common++;

                if (common == 0)
                    continue;

                int skip = keySegments.Length - common;

                if (skip > MaxAlignSkip)
                    continue;

                string prefix = string.Join('/', targetSegments[..(targetSegments.Length - common)]).ToLowerInvariant();
                var vote = (skip, prefix);

                votes[vote] = votes.GetValueOrDefault(vote) + common;
            }
        }

        if (votes.Count == 0)
            return null;

        var best = votes.OrderByDescending(v => v.Value).ThenBy(v => v.Key.Skip).ThenBy(v => v.Key.Prefix, StringComparer.Ordinal).First().Key;

        return new Alignment(best.Skip, best.Prefix);
    }

    private static PatchFileEntry Align(PatchFileEntry entry, Alignment alignment, HashSet<string> targetSet)
    {
        if (entry.RelativeDir == PatchFileIndex.LooseOnlyDir || entry.BaseName.Length == 0)
            return entry;

        string key = BuildKey(NormalizeDir(entry), entry.BaseName);

        if (targetSet.Contains(key))
            return entry;

        string[] segments = key.Split('/');

        if (segments.Length <= alignment.Skip)
            return entry;

        string remainder = string.Join('/', segments[alignment.Skip..]);
        string alignedKey = alignment.Prefix.Length == 0 ? remainder : $"{alignment.Prefix}/{remainder}";

        if (!targetSet.Contains(alignedKey))
            return entry;

        int slash = alignedKey.LastIndexOf('/');

        return entry with { RelativeDir = slash < 0 ? string.Empty : alignedKey[..slash] };
    }

    private static string FileNameOf(string path)
    {
        int slash = path.LastIndexOf('/');

        return slash < 0 ? path : path[(slash + 1)..];
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