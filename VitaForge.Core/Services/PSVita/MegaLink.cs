using System.Text.RegularExpressions;
using VitaForge.Core.Models.PSVita;

namespace VitaForge.Core.Services.PSVita;

public static partial class MegaLink
{
    [GeneratedRegex(@"mega\.(?:co\.)?nz/(?:file/(?<id>[\w-]{8})#(?<key>[\w-]{43})|#!(?<id>[\w-]{8})!(?<key>[\w-]{43}))", RegexOptions.IgnoreCase)]
    private static partial Regex FileRegex();

    public static bool TryParse(string url, out MegaFileLink link)
    {
        link = null!;

        var match = FileRegex().Match(url ?? string.Empty);

        if (!match.Success)
            return false;

        try
        {
            byte[] key = Convert.FromBase64String(match.Groups["key"].Value.Replace('-', '+').Replace('_', '/') + "=");

            if (key.Length != 32)
                return false;

            link = new MegaFileLink(match.Groups["id"].Value, key);

            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}