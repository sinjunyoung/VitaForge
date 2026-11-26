using System.Security.Cryptography;

namespace Vita.Core.TitleRules;

internal static class Sha256Hex
{
    public static string Of(byte[] data) => Convert.ToHexString(SHA256.HashData(data));

    public static bool Matches(byte[] data, string expected) => Of(data).Equals(expected, StringComparison.OrdinalIgnoreCase);
}
