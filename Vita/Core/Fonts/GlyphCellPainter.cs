using Vita.Core.Imaging;
using Vita.Core.Typography;

namespace Vita.Core.Fonts;

internal sealed class GlyphCellPainter(IGlyphRasterizer rasterizer)
{
    private static readonly GlyphColor StrokeFill = new(0, 0, 0, 255);

    public void Draw(RgbaImage page, int x, int y, int cellSize, string character, FontRenderProfile profile, double points, GlyphColor fill, GlyphAlign align)
    {
        int supersample = profile.Stroke == Math.Floor(profile.Stroke) ? 1 : 2;
        int scaledCell = cellSize * supersample;
        int scaledPoints = (int)Math.Round(points * supersample, MidpointRounding.ToEven);
        int scaledStroke = (int)Math.Round(profile.Stroke * supersample, MidpointRounding.ToEven);
        var ink = rasterizer.Render(new GlyphInkRequest(profile.FontKey, profile.FontData, character, scaledPoints, scaledStroke, fill, StrokeFill));

        if (ink is null || ink.Width == 0 || ink.Height == 0)
            return;

        var (left, top) = Place(align, cellSize, supersample, scaledCell, ink);
        byte[] glyph = new byte[scaledCell * scaledCell * 4];

        Blit(glyph, scaledCell, ink, left, top);

        byte[] cell = supersample > 1 ? LanczosResizer.Resize(new RgbaImage(scaledCell, scaledCell, glyph), cellSize, cellSize).Pixels : glyph;

        Composite(page, x, y, cellSize, cell);
    }

    private static (int Left, int Top) Place(GlyphAlign align, int cellSize, int supersample, int scaledCell, GlyphInk ink) => align switch
    {
        GlyphAlign.BottomLeft => (cellSize / 16 * supersample, scaledCell - ink.Height - cellSize / 8 * supersample),
        GlyphAlign.ColonLeft => (cellSize / 4 * supersample, FloorHalf(scaledCell - ink.Height)),
        _ => (FloorHalf(scaledCell - ink.Width), FloorHalf(scaledCell - ink.Height))
    };

    private static int FloorHalf(int value) => (int)Math.Floor(value / 2.0);

    private static void Blit(byte[] glyph, int size, GlyphInk ink, int left, int top)
    {
        for (int iy = 0; iy < ink.Height; iy++)
        {
            int dy = top + iy;

            if (dy < 0 || dy >= size)
                continue;

            for (int ix = 0; ix < ink.Width; ix++)
            {
                int dx = left + ix;

                if (dx < 0 || dx >= size)
                    continue;

                Buffer.BlockCopy(ink.PremultipliedPixels, (iy * ink.Width + ix) * 4, glyph, (dy * size + dx) * 4, 4);
            }
        }
    }

    private static void Composite(RgbaImage page, int x, int y, int cellSize, byte[] cell)
    {
        for (int cy = 0; cy < cellSize; cy++)
        {
            for (int cx = 0; cx < cellSize; cx++)
            {
                int source = (cy * cellSize + cx) * 4;
                int alpha = cell[source + 3];

                if (alpha == 0)
                    continue;

                int target = ((y + cy) * page.Width + x + cx) * 4;
                int destinationAlpha = page.Pixels[target + 3];

                if (alpha == 255 || destinationAlpha == 0)
                {
                    Buffer.BlockCopy(cell, source, page.Pixels, target, 4);

                    continue;
                }

                double outAlpha = alpha + destinationAlpha * (255 - alpha) / 255.0;

                for (int channel = 0; channel < 3; channel++)
                    page.Pixels[target + channel] = (byte)Math.Clamp((int)Math.Round((cell[source + channel] * alpha + page.Pixels[target + channel] * destinationAlpha * (255 - alpha) / 255.0) / outAlpha), 0, 255);

                page.Pixels[target + 3] = (byte)Math.Clamp((int)Math.Round(outAlpha), 0, 255);
            }
        }
    }
}