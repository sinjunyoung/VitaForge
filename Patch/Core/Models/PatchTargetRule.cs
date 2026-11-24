using Patch.Core.Services;

namespace Patch.Core.Models;

public sealed record PatchTargetRule(string Patch, string Target)
{
    public bool MatchesPatch(PatchFileEntry entry)
    {
        string spec = Normalize(Patch);
        int slash = spec.LastIndexOf('/');
        string dir = slash < 0 ? string.Empty : spec[..slash];
        string name = slash < 0 ? spec : spec[(slash + 1)..];
        string baseName = PatchFileIndex.PatchExtensions.Contains(Path.GetExtension(name), StringComparer.OrdinalIgnoreCase) ? Path.GetFileNameWithoutExtension(name) : name;

        if (!entry.BaseName.Equals(baseName, StringComparison.OrdinalIgnoreCase))
            return false;

        return dir.Length == 0 || Normalize(entry.RelativeDir).Equals(dir, StringComparison.OrdinalIgnoreCase);
    }

    public bool MatchesTarget(string targetPath)
    {
        string spec = Normalize(Target);
        string path = Normalize(targetPath);

        if (spec.Contains('/'))
            return path.Equals(spec, StringComparison.OrdinalIgnoreCase);

        int slash = path.LastIndexOf('/');

        return (slash < 0 ? path : path[(slash + 1)..]).Equals(spec, StringComparison.OrdinalIgnoreCase);
    }

    private static string Normalize(string value) => value.Trim().Replace('\\', '/').Trim('/');
}
