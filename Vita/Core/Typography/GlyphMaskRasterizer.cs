namespace Vita.Core.Typography;

internal static class GlyphMaskRasterizer
{
    public static float[] Fill(IReadOnlyList<List<(double X, double Y)>> polygons, int width, int height, int subScanlines)
    {
        float[] coverage = new float[width * height];
        var edges = new List<(double X0, double Y0, double X1, double Y1)>();

        foreach (var polygon in polygons)
        {
            for (int i = 0; i < polygon.Count; i++)
            {
                var a = polygon[i];
                var b = polygon[(i + 1) % polygon.Count];

                if (a.Y != b.Y)
                    edges.Add((a.X, a.Y, b.X, b.Y));
            }
        }

        var crossings = new List<(double X, int Direction)>();
        float weight = 1f / subScanlines;

        for (int row = 0; row < height; row++)
        {
            for (int sub = 0; sub < subScanlines; sub++)
            {
                double y = row + (sub + 0.5) / subScanlines;

                crossings.Clear();

                foreach (var (x0, y0, x1, y1) in edges)
                {
                    if ((y0 <= y) == (y1 <= y))
                        continue;

                    crossings.Add((x0 + (y - y0) * (x1 - x0) / (y1 - y0), y1 > y0 ? 1 : -1));
                }

                crossings.Sort((l, r) => l.X.CompareTo(r.X));

                int winding = 0;

                for (int i = 0; i < crossings.Count - 1; i++)
                {
                    winding += crossings[i].Direction;

                    if (winding != 0)
                        AddSpan(coverage, row * width, width, crossings[i].X, crossings[i + 1].X, weight);
                }
            }
        }

        return coverage;
    }

    private static void AddSpan(float[] coverage, int rowStart, int width, double left, double right, float weight)
    {
        left = Math.Max(left, 0);
        right = Math.Min(right, width);

        if (right <= left)
            return;

        int first = (int)Math.Floor(left);
        int last = Math.Min((int)Math.Floor(right), width - 1);

        if (first == last)
        {
            coverage[rowStart + first] += (float)(right - left) * weight;

            return;
        }

        coverage[rowStart + first] += (float)(first + 1 - left) * weight;

        for (int x = first + 1; x < last; x++)
            coverage[rowStart + x] += weight;

        coverage[rowStart + last] += (float)(right - last) * weight;
    }
}