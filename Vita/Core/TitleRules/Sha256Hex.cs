using System.Security.Cryptography;

namespace Vita.Core.TitleRules;

internal static class Sha256Hex
{
    public static string Of(byte[] data) => Of(data.AsSpan());

    public static string Of(ReadOnlySpan<byte> data) => Convert.ToHexString(SHA256.HashData(data));

    public static bool Matches(byte[] data, string expected) => Matches(data.AsSpan(), expected);

    public static bool Matches(ReadOnlySpan<byte> data, string expected) => Of(data).Equals(expected, StringComparison.OrdinalIgnoreCase);
}