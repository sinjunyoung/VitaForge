namespace Vita.Core.Typography;

internal sealed class GlyphPath
{
    public List<PathOp> Ops { get; } = [];

    public void MoveTo(double x, double y) => Ops.Add(new PathOp(PathOpKind.Move, x, y, 0, 0, 0, 0));

    public void LineTo(double x, double y) => Ops.Add(new PathOp(PathOpKind.Line, x, y, 0, 0, 0, 0));

    public void QuadTo(double cx, double cy, double x, double y) => Ops.Add(new PathOp(PathOpKind.Quad, cx, cy, x, y, 0, 0));

    public void CubicTo(double c1x, double c1y, double c2x, double c2y, double x, double y) => Ops.Add(new PathOp(PathOpKind.Cubic, c1x, c1y, c2x, c2y, x, y));

    public void Close() => Ops.Add(new PathOp(PathOpKind.Close, 0, 0, 0, 0, 0, 0));

    public void AppendTransformed(GlyphPath other, double xx, double xy, double yx, double yy, double dx, double dy)
    {
        foreach (var op in other.Ops)
        {
            (double x1, double y1) = Transform(op.X1, op.Y1, xx, xy, yx, yy, dx, dy);
            (double x2, double y2) = Transform(op.X2, op.Y2, xx, xy, yx, yy, dx, dy);
            (double x3, double y3) = Transform(op.X3, op.Y3, xx, xy, yx, yy, dx, dy);

            Ops.Add(new PathOp(op.Kind, x1, y1, x2, y2, x3, y3));
        }
    }

    private static (double X, double Y) Transform(double x, double y, double xx, double xy, double yx, double yy, double dx, double dy)
        => (xx * x + yx * y + dx, xy * x + yy * y + dy);
}