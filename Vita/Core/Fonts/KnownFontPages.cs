namespace Vita.Core.Fonts;

internal static class KnownFontPages
{
    private static readonly Dictionary<string, KnownFontPage> ByHigh32 = new[]
    {
        new KnownFontPage("8665CE082D339B33", 1644),
        new KnownFontPage("A8E6FDD162258699", 1644),
        new KnownFontPage("18747565A804E292", 1644),
        new KnownFontPage("6706A53E1D94C16E", 1644),
        new KnownFontPage("E690E190AA5C798F", 2668),
        new KnownFontPage("5F01AD869403D330", 3692)
    }.ToDictionary(p => p.FullHash[..8], StringComparer.OrdinalIgnoreCase);

    public static readonly string[] FontFtxPaths = ["_US/other/font.ftx", "_US/other/font2a.ftx", "_US/other/font2b.ftx"];

    public static KnownFontPage? Find(string high32) => ByHigh32.GetValueOrDefault(high32);
}