using System.Collections.Concurrent;

namespace Vita.Core.Typography;

internal sealed class OutlineGlyphRasterizer : IGlyphRasterizer
{
    private const int Oversample = 4;
    private const int SubScanlines = 4;

    private readonly ConcurrentDictionary<string, SfntFont> fonts = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<(string FontKey, string Character, double Em, double Stroke, GlyphColor Fill, GlyphColor StrokeFill), GlyphInk?> cache = new();

    public GlyphInk? Render(GlyphInkRequest request)
        => cache.GetOrAdd((request.FontKey, request.Character, request.EmSizePixels, request.StrokePixels, request.Fill, request.StrokeFill), _ => RenderGlyph(request));

    private GlyphInk? RenderGlyph(GlyphInkRequest request)
    {
        var font = fonts.GetOrAdd(request.FontKey, _ => SfntFont.Parse(request.FontData));
        var path = font.GetOutline(font.GetGlyphIndex(char.ConvertToUtf32(request.Character, 0)));
        var polygons = OutlineFlattener.Flatten(path, request.EmSizePixels / font.UnitsPerEm);

        if (polygons.Count == 0)
            return null;

        double radius = Math.Max(0, request.StrokePixels);
        int pad = (int)Math.Ceiling(radius) + 2;
        double minX = polygons.Min(p => p.Min(q => q.X));
        double maxX = polygons.Max(p => p.Max(q => q.X));
        double minY = polygons.Min(p => p.Min(q => q.Y));
        double maxY = polygons.Max(p => p.Max(q => q.Y));
        int left = (int)Math.Floor(minX) - pad;
        int top = (int)Math.Floor(minY) - pad;
        int width = (int)Math.Ceiling(maxX) + pad - left;
        int height = (int)Math.Ceiling(maxY) + pad - top;
        int gridWidth = width * Oversample;
        int gridHeight = height * Oversample;
        var scaled = polygons.Select(p => p.Select(q => ((q.X - left) * Oversample, (q.Y - top) * Oversample)).ToList()).ToList();
        float[] fine = GlyphMaskRasterizer.Fill(scaled, gridWidth, gridHeight, SubScanlines);
        float[] fill = Downsample(fine, gridWidth, width, height);
        float[] stroke = radius > 0 ? Downsample(Dilate(fine, gridWidth, gridHeight, radius * Oversample), gridWidth, width, height) : fill;

        return Compose(request, fill, stroke, radius > 0, width, height);
    }

    private static float[] Dilate(float[] coverage, int width, int height, double radius)
    {
        bool[] inside = new bool[coverage.Length];
        bool any = false;

        for (int i = 0; i < coverage.Length; i++)
        {
            inside[i] = coverage[i] >= 0.5f;
            any |= inside[i];
        }

        float[] result = new float[coverage.Length];

        if (!any)
            return result;

        double[] squared = DistanceTransform.SquaredDistanceToSet(inside, width, height);

        for (int i = 0; i < result.Length; i++)
            result[i] = Math.Max(coverage[i], (float)Math.Clamp(radius + 0.5 - Math.Sqrt(squared[i]), 0, 1));

        return result;
    }

    private static float[] Downsample(float[] fine, int fineWidth, int width, int height)
    {
        float[] result = new float[width * height];
        float scale = 1f / (Oversample * Oversample);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float sum = 0;

                for (int sy = 0; sy < Oversample; sy++)
                {
                    for (int sx = 0; sx < Oversample; sx++)
                        sum += fine[(y * Oversample + sy) * fineWidth + x * Oversample + sx];
                }

                result[y * width + x] = Math.Min(1f, sum * scale);
            }
        }

        return result;
    }

    private static GlyphInk? Compose(GlyphInkRequest request, float[] fill, float[] stroke, bool hasStroke, int width, int height)
    {
        byte[] pixels = new byte[width * height * 4];
        int minX = width;
        int minY = height;
        int maxX = -1;
        int maxY = -1;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int index = y * width + x;
                double r = 0;
                double g = 0;
                double b = 0;
                double a = 0;

                if (hasStroke)
                {
                    double m = stroke[index];

                    r = request.StrokeFill.R * m;
                    g = request.StrokeFill.G * m;
                    b = request.StrokeFill.B * m;
                    a = request.StrokeFill.A * m;
                }

                double f = fill[index];

                r = request.Fill.R * f + r * (1 - f);
                g = request.Fill.G * f + g * (1 - f);
                b = request.Fill.B * f + b * (1 - f);
                a = request.Fill.A * f + a * (1 - f);

                byte alpha = (byte)Math.Clamp((int)Math.Round(a), 0, 255);

                if (alpha == 0)
                    continue;

                pixels[index * 4] = (byte)Math.Clamp((int)Math.Round(r), 0, 255);
                pixels[index * 4 + 1] = (byte)Math.Clamp((int)Math.Round(g), 0, 255);
                pixels[index * 4 + 2] = (byte)Math.Clamp((int)Math.Round(b), 0, 255);
                pixels[index * 4 + 3] = alpha;

                minX = Math.Min(minX, x);
                minY = Math.Min(minY, y);
                maxX = Math.Max(maxX, x);
                maxY = Math.Max(maxY, y);
            }
        }

        if (maxX < 0)
            return null;

        int croppedWidth = maxX - minX + 1;
        int croppedHeight = maxY - minY + 1;
        byte[] cropped = new byte[croppedWidth * croppedHeight * 4];

        for (int y = 0; y < croppedHeight; y++)
            Buffer.BlockCopy(pixels, ((minY + y) * width + minX) * 4, cropped, y * croppedWidth * 4, croppedWidth * 4);

        return new GlyphInk(croppedWidth, croppedHeight, cropped);
    }
}