namespace Vita.Core.Fonts;

internal sealed record KnownFontPage(string FullHash, int PageBase)
{
    public const string MenuHash = "A8E6FDD162258699";

    public bool IsMenu => FullHash.Equals(MenuHash, StringComparison.OrdinalIgnoreCase);
}