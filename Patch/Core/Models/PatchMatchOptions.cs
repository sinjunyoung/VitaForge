using System.Text.RegularExpressions;

namespace Patch.Core.Models;

public sealed class PatchMatchOptions
{
    public static readonly PatchMatchOptions Empty = new([], []);

    private readonly Regex[] _nameStrips;

    public PatchMatchOptions(IReadOnlyList<PatchTargetRule> targetRules, IReadOnlyList<string> nameStrips)
    {
        TargetRules = targetRules;
        _nameStrips = [.. nameStrips.Select(TryCreate).OfType<Regex>()];
    }

    public IReadOnlyList<PatchTargetRule> TargetRules { get; }

    public string StripName(string baseName)
    {
        foreach (var strip in _nameStrips)
        {
            string stripped = strip.Replace(baseName, string.Empty);

            if (stripped.Length > 0 && stripped != baseName)
                return stripped;
        }

        return baseName;
    }

    private static Regex? TryCreate(string pattern)
    {
        try
        {
            return new Regex($"(?:{pattern})$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
        }
        catch (ArgumentException)
        {
            return null;
        }
    }
}
