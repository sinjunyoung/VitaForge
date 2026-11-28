namespace Vita.Core.Typography;

internal sealed class CffCharStringInterpreter(byte[] data, CffIndex globalSubrs, CffIndex localSubrs)
{
    private const int MaxStack = 96;
    private const int MaxDepth = 10;

    private readonly double[] stack = new double[MaxStack];
    private readonly GlyphPath path = new();
    private int stackCount;
    private int stems;
    private bool widthParsed;
    private bool open;
    private double x;
    private double y;

    public GlyphPath Run(int start, int end)
    {
        Execute(start, end, 0);

        CloseContour();

        return path;
    }

    private static int Bias(int count) => count < 1240 ? 107 : count < 33900 ? 1131 : 32768;

    private bool Execute(int start, int end, int depth)
    {
        if (depth > MaxDepth)
            return true;

        int position = start;

        while (position < end)
        {
            int b0 = data[position++];

            if (b0 >= 32 || b0 == 28)
            {
                position = ReadNumber(b0, position);

                continue;
            }

            switch (b0)
            {
                case 1:
                case 3:
                case 18:
                case 23:
                    ParseWidth(stackCount % 2 == 1);

                    stems += stackCount / 2;
                    stackCount = 0;

                    break;
                case 19:
                case 20:
                    ParseWidth(stackCount % 2 == 1);

                    stems += stackCount / 2;
                    stackCount = 0;
                    position += (stems + 7) / 8;

                    break;
                case 21:
                    ParseWidth(stackCount > 2);
                    MoveTo(stack[0], stack[1]);

                    break;
                case 22:
                    ParseWidth(stackCount > 1);
                    MoveTo(stack[0], 0);

                    break;
                case 4:
                    ParseWidth(stackCount > 1);
                    MoveTo(0, stack[0]);

                    break;
                case 5:
                    for (int i = 0; i + 1 < stackCount; i += 2)
                        LineBy(stack[i], stack[i + 1]);

                    stackCount = 0;

                    break;
                case 6:
                case 7:
                    AlternatingLines(b0 == 6);

                    break;
                case 8:
                    for (int i = 0; i + 5 < stackCount; i += 6)
                        CurveBy(stack[i], stack[i + 1], stack[i + 2], stack[i + 3], stack[i + 4], stack[i + 5]);

                    stackCount = 0;

                    break;
                case 24:
                    CurveLine();

                    break;
                case 25:
                    LineCurve();

                    break;
                case 26:
                    VerticalVerticalCurve();

                    break;
                case 27:
                    HorizontalHorizontalCurve();

                    break;
                case 30:
                case 31:
                    AlternatingCurves(b0 == 31);

                    break;
                case 10:
                case 29:
                    if (stackCount == 0)
                        return true;

                    CffIndex subrs = b0 == 10 ? localSubrs : globalSubrs;
                    int index = (int)stack[--stackCount] + Bias(subrs.Count);

                    if (index < 0 || index >= subrs.Count)
                        return true;

                    var (subrStart, subrEnd) = subrs.Get(index);

                    if (Execute(subrStart, subrEnd, depth + 1))
                        return true;

                    break;
                case 11:
                    return false;
                case 14:
                    ParseWidth(stackCount == 1 || stackCount == 5);

                    return true;
                case 12:
                    Escape(data[position++]);

                    break;
                default:
                    stackCount = 0;

                    break;
            }
        }

        return false;
    }

    private int ReadNumber(int b0, int position)
    {
        double value;

        if (b0 == 28)
        {
            value = BigEndian.I16(data, position);
            position += 2;
        }
        else if (b0 <= 246)
        {
            value = b0 - 139;
        }
        else if (b0 <= 250)
        {
            value = (b0 - 247) * 256 + data[position++] + 108;
        }
        else if (b0 <= 254)
        {
            value = -(b0 - 251) * 256 - data[position++] - 108;
        }
        else
        {
            value = BigEndian.I32(data, position) / 65536.0;
            position += 4;
        }

        if (stackCount < MaxStack)
            stack[stackCount++] = value;

        return position;
    }

    private void ParseWidth(bool hasWidth)
    {
        if (widthParsed)
            return;

        widthParsed = true;

        if (!hasWidth || stackCount == 0)
            return;

        for (int i = 1; i < stackCount; i++)
            stack[i - 1] = stack[i];

        stackCount--;
    }

    private void CloseContour()
    {
        if (!open)
            return;

        path.Close();

        open = false;
    }

    private void MoveTo(double dx, double dy)
    {
        CloseContour();

        x += dx;
        y += dy;

        path.MoveTo(x, y);

        open = true;
        stackCount = 0;
    }

    private void LineBy(double dx, double dy)
    {
        x += dx;
        y += dy;

        path.LineTo(x, y);
    }

    private void CurveBy(double dx1, double dy1, double dx2, double dy2, double dx3, double dy3)
    {
        double x1 = x + dx1;
        double y1 = y + dy1;
        double x2 = x1 + dx2;
        double y2 = y1 + dy2;

        x = x2 + dx3;
        y = y2 + dy3;

        path.CubicTo(x1, y1, x2, y2, x, y);
    }

    private void AlternatingLines(bool horizontal)
    {
        for (int i = 0; i < stackCount; i++)
        {
            if (horizontal)
                LineBy(stack[i], 0);
            else
                LineBy(0, stack[i]);

            horizontal = !horizontal;
        }

        stackCount = 0;
    }

    private void CurveLine()
    {
        int i = 0;

        for (; i + 6 <= stackCount - 2; i += 6)
            CurveBy(stack[i], stack[i + 1], stack[i + 2], stack[i + 3], stack[i + 4], stack[i + 5]);

        if (i + 1 < stackCount)
            LineBy(stack[i], stack[i + 1]);

        stackCount = 0;
    }

    private void LineCurve()
    {
        int i = 0;

        for (; i + 2 <= stackCount - 6; i += 2)
            LineBy(stack[i], stack[i + 1]);

        if (i + 5 < stackCount)
            CurveBy(stack[i], stack[i + 1], stack[i + 2], stack[i + 3], stack[i + 4], stack[i + 5]);

        stackCount = 0;
    }

    private void VerticalVerticalCurve()
    {
        int i = 0;
        double dx1 = 0;

        if (stackCount % 4 == 1)
            dx1 = stack[i++];

        for (; i + 3 < stackCount; i += 4)
        {
            CurveBy(dx1, stack[i], stack[i + 1], stack[i + 2], 0, stack[i + 3]);

            dx1 = 0;
        }

        stackCount = 0;
    }

    private void HorizontalHorizontalCurve()
    {
        int i = 0;
        double dy1 = 0;

        if (stackCount % 4 == 1)
            dy1 = stack[i++];

        for (; i + 3 < stackCount; i += 4)
        {
            CurveBy(stack[i], dy1, stack[i + 1], stack[i + 2], stack[i + 3], 0);

            dy1 = 0;
        }

        stackCount = 0;
    }

    private void AlternatingCurves(bool horizontal)
    {
        int i = 0;

        while (i + 3 < stackCount)
        {
            bool last = stackCount - i == 5;
            double final = last ? stack[i + 4] : 0;

            if (horizontal)
                CurveBy(stack[i], 0, stack[i + 1], stack[i + 2], final, stack[i + 3]);
            else
                CurveBy(0, stack[i], stack[i + 1], stack[i + 2], stack[i + 3], final);

            horizontal = !horizontal;
            i += last ? 5 : 4;
        }

        stackCount = 0;
    }

    private void Escape(int code)
    {
        switch (code)
        {
            case 34:
                if (stackCount >= 7)
                {
                    CurveBy(stack[0], 0, stack[1], stack[2], stack[3], 0);
                    CurveBy(stack[4], 0, stack[5], -stack[2], stack[6], 0);
                }

                break;
            case 35:
                if (stackCount >= 12)
                {
                    CurveBy(stack[0], stack[1], stack[2], stack[3], stack[4], stack[5]);
                    CurveBy(stack[6], stack[7], stack[8], stack[9], stack[10], stack[11]);
                }

                break;
            case 36:
                if (stackCount >= 9)
                {
                    CurveBy(stack[0], stack[1], stack[2], stack[3], stack[4], 0);
                    CurveBy(stack[5], 0, stack[6], stack[7], stack[8], -(stack[1] + stack[3] + stack[7]));
                }

                break;
            case 37:
                if (stackCount >= 11)
                {
                    double dx = stack[0] + stack[2] + stack[4] + stack[6] + stack[8];
                    double dy = stack[1] + stack[3] + stack[5] + stack[7] + stack[9];
                    bool horizontal = Math.Abs(dx) > Math.Abs(dy);

                    CurveBy(stack[0], stack[1], stack[2], stack[3], stack[4], stack[5]);
                    CurveBy(stack[6], stack[7], stack[8], stack[9], horizontal ? stack[10] : -dx, horizontal ? -dy : stack[10]);
                }

                break;
        }

        stackCount = 0;
    }
}