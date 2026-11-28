namespace Vita.Core.Typography;

internal static class OutlineFlattener
{
    public static List<List<(double X, double Y)>> Flatten(GlyphPath path, double scale)
    {
        var polygons = new List<List<(double X, double Y)>>();
        List<(double X, double Y)>? current = null;
        double cx = 0;
        double cy = 0;

        foreach (var op in path.Ops)
        {
            switch (op.Kind)
            {
                case PathOpKind.Move:
                    Finish(polygons, current);

                    current = [(op.X1 * scale, -op.Y1 * scale)];
                    cx = op.X1;
                    cy = op.Y1;

                    break;
                case PathOpKind.Line:
                    current?.Add((op.X1 * scale, -op.Y1 * scale));

                    cx = op.X1;
                    cy = op.Y1;

                    break;
                case PathOpKind.Quad:
                    if (current is not null)
                        AddCurve(current, scale, cx, cy, cx + 2.0 / 3.0 * (op.X1 - cx), cy + 2.0 / 3.0 * (op.Y1 - cy), op.X2 + 2.0 / 3.0 * (op.X1 - op.X2), op.Y2 + 2.0 / 3.0 * (op.Y1 - op.Y2), op.X2, op.Y2);

                    cx = op.X2;
                    cy = op.Y2;

                    break;
                case PathOpKind.Cubic:
                    if (current is not null)
                        AddCurve(current, scale, cx, cy, op.X1, op.Y1, op.X2, op.Y2, op.X3, op.Y3);

                    cx = op.X3;
                    cy = op.Y3;

                    break;
                case PathOpKind.Close:
                    Finish(polygons, current);

                    current = null;

                    break;
            }
        }

        Finish(polygons, current);

        return polygons;
    }

    private static void Finish(List<List<(double X, double Y)>> polygons, List<(double X, double Y)>? current)
    {
        if (current is { Count: >= 3 })
            polygons.Add(current);
    }

    private static void AddCurve(List<(double X, double Y)> points, double scale, double x0, double y0, double x1, double y1, double x2, double y2, double x3, double y3)
    {
        double length = (Math.Abs(x1 - x0) + Math.Abs(y1 - y0) + Math.Abs(x2 - x1) + Math.Abs(y2 - y1) + Math.Abs(x3 - x2) + Math.Abs(y3 - y2)) * scale;
        int steps = Math.Clamp((int)Math.Ceiling(Math.Sqrt(length * 6)), 4, 64);

        for (int i = 1; i <= steps; i++)
        {
            double t = (double)i / steps;
            double u = 1 - t;
            double a = u * u * u;
            double b = 3 * u * u * t;
            double c = 3 * u * t * t;
            double d = t * t * t;

            points.Add(((a * x0 + b * x1 + c * x2 + d * x3) * scale, -(a * y0 + b * y1 + c * y2 + d * y3) * scale));
        }
    }
}