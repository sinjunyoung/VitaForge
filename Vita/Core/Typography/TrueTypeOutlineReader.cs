namespace Vita.Core.Typography;

internal sealed class TrueTypeOutlineReader(byte[] data, int glyfOffset, int locaOffset, bool longLoca, int glyphCount) : IGlyphOutlineSource
{
    private const int MaxDepth = 8;

    public GlyphPath GetOutline(int glyphIndex)
    {
        var path = new GlyphPath();

        Append(path, glyphIndex, 1, 0, 0, 1, 0, 0, 0);

        return path;
    }

    private void Append(GlyphPath target, int glyphIndex, double xx, double xy, double yx, double yy, double dx, double dy, int depth)
    {
        if (glyphIndex < 0 || glyphIndex >= glyphCount || depth > MaxDepth)
            return;

        int start = glyfOffset + LocaAt(glyphIndex);
        int end = glyfOffset + LocaAt(glyphIndex + 1);

        if (end <= start)
            return;

        int contours = BigEndian.I16(data, start);

        if (contours >= 0)
        {
            var simple = ReadSimple(start, contours);

            target.AppendTransformed(simple, xx, xy, yx, yy, dx, dy);

            return;
        }

        int position = start + 10;
        bool more;

        do
        {
            int flags = BigEndian.U16(data, position);
            int component = BigEndian.U16(data, position + 2);

            position += 4;

            double offsetX;
            double offsetY;

            if ((flags & 0x1) != 0)
            {
                offsetX = BigEndian.I16(data, position);
                offsetY = BigEndian.I16(data, position + 2);
                position += 4;
            }
            else
            {
                offsetX = (sbyte)data[position];
                offsetY = (sbyte)data[position + 1];
                position += 2;
            }

            if ((flags & 0x2) == 0)
            {
                offsetX = 0;
                offsetY = 0;
            }

            double a = 1;
            double b = 0;
            double c = 0;
            double d = 1;

            if ((flags & 0x8) != 0)
            {
                a = d = BigEndian.I16(data, position) / 16384.0;
                position += 2;
            }
            else if ((flags & 0x40) != 0)
            {
                a = BigEndian.I16(data, position) / 16384.0;
                d = BigEndian.I16(data, position + 2) / 16384.0;
                position += 4;
            }
            else if ((flags & 0x80) != 0)
            {
                a = BigEndian.I16(data, position) / 16384.0;
                b = BigEndian.I16(data, position + 2) / 16384.0;
                c = BigEndian.I16(data, position + 4) / 16384.0;
                d = BigEndian.I16(data, position + 6) / 16384.0;
                position += 8;
            }

            double cxx = xx * a + yx * b;
            double cxy = xy * a + yy * b;
            double cyx = xx * c + yx * d;
            double cyy = xy * c + yy * d;

            Append(target, component, cxx, cxy, cyx, cyy, xx * offsetX + yx * offsetY + dx, xy * offsetX + yy * offsetY + dy, depth + 1);

            more = (flags & 0x20) != 0;
        }
        while (more);
    }

    private int LocaAt(int index) => longLoca ? (int)BigEndian.U32(data, locaOffset + index * 4) : BigEndian.U16(data, locaOffset + index * 2) * 2;

    private GlyphPath ReadSimple(int start, int contours)
    {
        var path = new GlyphPath();

        if (contours == 0)
            return path;

        int[] ends = new int[contours];

        for (int i = 0; i < contours; i++)
            ends[i] = BigEndian.U16(data, start + 10 + i * 2);

        int pointCount = ends[^1] + 1;
        int position = start + 10 + contours * 2;

        position += 2 + BigEndian.U16(data, position);

        byte[] flags = new byte[pointCount];

        for (int i = 0; i < pointCount; i++)
        {
            byte flag = data[position++];

            flags[i] = flag;

            if ((flag & 0x08) == 0)
                continue;

            int repeat = data[position++];

            while (repeat-- > 0 && i + 1 < pointCount)
                flags[++i] = flag;
        }

        double[] xs = ReadCoordinates(flags, ref position, 0x02, 0x10);
        double[] ys = ReadCoordinates(flags, ref position, 0x04, 0x20);
        int first = 0;

        foreach (int last in ends)
        {
            AppendContour(path, xs, ys, flags, first, last);

            first = last + 1;
        }

        return path;
    }

    private double[] ReadCoordinates(byte[] flags, ref int position, int shortFlag, int sameFlag)
    {
        double[] values = new double[flags.Length];
        int current = 0;

        for (int i = 0; i < flags.Length; i++)
        {
            if ((flags[i] & shortFlag) != 0)
            {
                int delta = data[position++];

                current += (flags[i] & sameFlag) != 0 ? delta : -delta;
            }
            else if ((flags[i] & sameFlag) == 0)
            {
                current += BigEndian.I16(data, position);
                position += 2;
            }

            values[i] = current;
        }

        return values;
    }

    private static void AppendContour(GlyphPath path, double[] xs, double[] ys, byte[] flags, int first, int last)
    {
        int count = last - first + 1;

        if (count < 2)
            return;

        bool OnCurve(int i) => (flags[first + i % count] & 1) != 0;
        double X(int i) => xs[first + i % count];
        double Y(int i) => ys[first + i % count];

        int startIndex = 0;
        double startX;
        double startY;

        if (OnCurve(0))
        {
            startX = X(0);
            startY = Y(0);
            startIndex = 1;
        }
        else if (OnCurve(count - 1))
        {
            startX = X(count - 1);
            startY = Y(count - 1);
            count--;
        }
        else
        {
            startX = (X(0) + X(count - 1)) / 2;
            startY = (Y(0) + Y(count - 1)) / 2;
        }

        path.MoveTo(startX, startY);

        double controlX = 0;
        double controlY = 0;
        bool hasControl = false;

        for (int i = startIndex; i < count; i++)
        {
            if (OnCurve(i))
            {
                if (hasControl)
                {
                    path.QuadTo(controlX, controlY, X(i), Y(i));

                    hasControl = false;
                }
                else
                {
                    path.LineTo(X(i), Y(i));
                }

                continue;
            }

            if (hasControl)
                path.QuadTo(controlX, controlY, (controlX + X(i)) / 2, (controlY + Y(i)) / 2);

            controlX = X(i);
            controlY = Y(i);
            hasControl = true;
        }

        if (hasControl)
            path.QuadTo(controlX, controlY, startX, startY);

        path.Close();
    }
}