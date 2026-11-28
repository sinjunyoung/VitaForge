using Vita.Core.Imaging;
using Vita.Core.Typography;

namespace Vita.Core.Fonts;

internal sealed class FontPageBaker(FontBakeResources resources)
{
    private const int CellSize = 32;
    private const int CellCount = 1024;
    private const int AsciiSpaceCell = 224;
    private const int RuntimeOffset = 192;

    private static readonly int[] RuntimeOverlayCodes = [.. Enumerable.Range(0x30, 10), 0x3A, 0x3F, 0x5B, 0x5D, 0x2D, 0x2E, 0x2F, 0x46, 0x6F, 0x72, 0x6D];

    private readonly GlyphCellPainter painter = new(resources.Rasterizer);

    public int Bake(RgbaImage page, KnownFontPage known)
    {
        bool menu = known.IsMenu;
        var profile = menu ? resources.Menu : resources.Body;
        double asciiPoints = profile.AsciiPoints;
        bool white = MeanRgb(page.Pixels) > 200;
        var clear = white ? new GlyphColor(255, 255, 255, 0) : new GlyphColor(0, 0, 0, 0);
        var ink = white ? new GlyphColor(255, 255, 255, 255) : new GlyphColor(247, 247, 247, 255);
        int columns = page.Width / CellSize;
        int spaceLocal = SjisCells.ToCell(SjisCells.Space.First, SjisCells.Space.Second) - known.PageBase;
        var protectedCells = menu ? Enumerable.Range(0x30, 10).Select(code => RuntimeOffset + code).ToHashSet() : new HashSet<int>();
        var koreanCells = new Dictionary<int, string>();

        foreach (var (character, first, second) in resources.Mapping.Entries)
        {
            int local = SjisCells.ToCell(first, second) - known.PageBase;

            if (local < 0 || local >= CellCount || local == spaceLocal || protectedCells.Contains(local))
                continue;

            koreanCells[local] = character;
        }

        foreach (var (local, character) in koreanCells)
        {
            ClearCell(page, local, columns, clear);

            painter.Draw(page, local % columns * CellSize, local / columns * CellSize, CellSize, character, profile, profile.KoreanPoints, ink, GlyphAlign.Center);
        }

        if (known.PageBase != SjisCells.PageZeroBase)
            return koreanCells.Count;

        var transparent = new GlyphColor(0, 0, 0, 0);

        if (spaceLocal >= 0 && spaceLocal < CellCount)
            ClearCell(page, spaceLocal, columns, transparent);

        ClearCell(page, AsciiSpaceCell, columns, transparent);

        foreach (var (character, local) in AsciiOverflowMap.BuildCells(resources.Mapping, resources.UsedAsciiCodes))
        {
            ClearCell(page, local, columns, clear);

            painter.Draw(page, local % columns * CellSize, local / columns * CellSize, CellSize, character.ToString(), profile, asciiPoints, ink, character is '.' or ',' ? GlyphAlign.BottomLeft : GlyphAlign.Center);
        }

        if (menu)
            return koreanCells.Count;

        foreach (int code in RuntimeOverlayCodes)
        {
            int local = RuntimeOffset + code;
            char character = (char)code;

            ClearCell(page, local, columns, clear);

            painter.Draw(page, local % columns * CellSize, local / columns * CellSize, CellSize, character.ToString(), profile, asciiPoints, ink, character is '.' or ',' ? GlyphAlign.BottomLeft : character == ':' ? GlyphAlign.ColonLeft : GlyphAlign.Center);
        }

        ClearCell(page, 454, columns, clear);

        painter.Draw(page, 454 % columns * CellSize, 454 / columns * CellSize, CellSize, ":", profile, asciiPoints, ink, GlyphAlign.Center);

        return koreanCells.Count;
    }

    private static double MeanRgb(byte[] pixels)
    {
        long sum = 0;

        for (int i = 0; i < pixels.Length; i += 4)
            sum += pixels[i] + pixels[i + 1] + pixels[i + 2];

        return (double)sum / (pixels.Length / 4 * 3);
    }

    private static void ClearCell(RgbaImage page, int local, int columns, GlyphColor color)
    {
        int originX = local % columns * CellSize;
        int originY = local / columns * CellSize;

        for (int y = 0; y < CellSize; y++)
        {
            for (int x = 0; x < CellSize; x++)
            {
                int index = ((originY + y) * page.Width + originX + x) * 4;

                page.Pixels[index] = color.R;
                page.Pixels[index + 1] = color.G;
                page.Pixels[index + 2] = color.B;
                page.Pixels[index + 3] = color.A;
            }
        }
    }
}