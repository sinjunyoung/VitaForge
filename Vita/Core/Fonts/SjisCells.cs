namespace Vita.Core.Fonts;

internal static class SjisCells
{
    public const int PageZeroBase = 1644;

    public static readonly (int First, int Second) Space = (0x8C, 0x6D);

    public static int ToCell(int first, int second) => (first - 0x81) * 188 + (second < 0x80 ? second - 0x40 : second - 0x41);

    public static (int First, int Second) FromCell(int cell)
    {
        int offset = cell % 188;

        return (0x81 + cell / 188, offset < 63 ? 0x40 + offset : 0x41 + offset);
    }
}