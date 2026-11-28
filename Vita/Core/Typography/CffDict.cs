namespace Vita.Core.Typography;

internal static class CffDict
{
    public static Dictionary<int, double[]> Parse(byte[] data, int start, int end)
    {
        var result = new Dictionary<int, double[]>();
        var operands = new List<double>();
        int position = start;

        while (position < end)
        {
            int b0 = data[position++];

            if (b0 <= 21)
            {
                int key = b0;

                if (b0 == 12)
                    key = 1200 + data[position++];

                result[key] = [.. operands];
                operands.Clear();

                continue;
            }

            if (b0 == 28)
            {
                operands.Add(BigEndian.I16(data, position));
                position += 2;
            }
            else if (b0 == 29)
            {
                operands.Add(BigEndian.I32(data, position));
                position += 4;
            }
            else if (b0 == 30)
            {
                position = ReadReal(data, position, operands);
            }
            else if (b0 >= 32 && b0 <= 246)
            {
                operands.Add(b0 - 139);
            }
            else if (b0 >= 247 && b0 <= 250)
            {
                operands.Add((b0 - 247) * 256 + data[position++] + 108);
            }
            else if (b0 >= 251 && b0 <= 254)
            {
                operands.Add(-(b0 - 251) * 256 - data[position++] - 108);
            }
        }

        return result;
    }

    private static int ReadReal(byte[] data, int position, List<double> operands)
    {
        var text = new System.Text.StringBuilder();
        bool done = false;

        while (!done && position < data.Length)
        {
            int b = data[position++];

            foreach (int nibble in new[] { b >> 4, b & 0x0F })
            {
                if (nibble == 0x0F)
                {
                    done = true;

                    break;
                }

                text.Append(nibble switch
                {
                    <= 9 => ((char)('0' + nibble)).ToString(),
                    0x0A => ".",
                    0x0B => "E",
                    0x0C => "E-",
                    0x0E => "-",
                    _ => string.Empty
                });
            }
        }

        operands.Add(double.TryParse(text.ToString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double value) ? value : 0);

        return position;
    }
}