using System.Buffers.Binary;

namespace Vita.Core.Ftx;

internal static class DxtEncoder
{
    public static byte[] Encode(ReadOnlySpan<byte> rgba, int width, int height, uint format)
    {
        if (width % 4 != 0 || height % 4 != 0)
            throw new NotSupportedException($"texture size {width}x{height} is not a multiple of 4");

        if (format != DxtFormat.Dxt5 && format != DxtFormat.Dxt3)
            throw new NotSupportedException($"unsupported texture format 0x{format:X8}");

        int blocksWide = width / 4;
        int blocksHigh = height / 4;
        byte[] output = new byte[blocksWide * blocksHigh * 16];
        Span<int> red = stackalloc int[16];
        Span<int> green = stackalloc int[16];
        Span<int> blue = stackalloc int[16];
        Span<int> alpha = stackalloc int[16];

        for (int by = 0; by < blocksHigh; by++)
        {
            for (int bx = 0; bx < blocksWide; bx++)
            {
                for (int py = 0; py < 4; py++)
                {
                    for (int px = 0; px < 4; px++)
                    {
                        int source = ((by * 4 + py) * width + bx * 4 + px) * 4;
                        int index = py * 4 + px;

                        red[index] = rgba[source];
                        green[index] = rgba[source + 1];
                        blue[index] = rgba[source + 2];
                        alpha[index] = rgba[source + 3];
                    }
                }

                var block = output.AsSpan((by * blocksWide + bx) * 16, 16);

                if (format == DxtFormat.Dxt5)
                    EncodeAlphaDxt5(alpha, block[..8]);
                else
                    EncodeAlphaDxt3(alpha, block[..8]);

                EncodeColor(red, green, blue, block[8..]);
            }
        }

        return output;
    }

    private static void EncodeColor(ReadOnlySpan<int> red, ReadOnlySpan<int> green, ReadOnlySpan<int> blue, Span<byte> destination)
    {
        int minRed = 255;
        int minGreen = 255;
        int minBlue = 255;
        int maxRed = 0;
        int maxGreen = 0;
        int maxBlue = 0;

        for (int i = 0; i < 16; i++)
        {
            minRed = Math.Min(minRed, red[i]);
            minGreen = Math.Min(minGreen, green[i]);
            minBlue = Math.Min(minBlue, blue[i]);
            maxRed = Math.Max(maxRed, red[i]);
            maxGreen = Math.Max(maxGreen, green[i]);
            maxBlue = Math.Max(maxBlue, blue[i]);
        }

        int color0 = Pack565(maxRed, maxGreen, maxBlue);
        int color1 = Pack565(minRed, minGreen, minBlue);

        if (color0 < color1)
            (color0, color1) = (color1, color0);

        uint bits = 0;

        if (color0 != color1)
        {
            Span<int> palette = stackalloc int[12];

            Unpack565(color0, palette[..3]);
            Unpack565(color1, palette.Slice(3, 3));

            for (int channel = 0; channel < 3; channel++)
            {
                palette[6 + channel] = (2 * palette[channel] + palette[3 + channel]) / 3;
                palette[9 + channel] = (palette[channel] + 2 * palette[3 + channel]) / 3;
            }

            for (int i = 0; i < 16; i++)
            {
                int best = 0;
                int bestDistance = int.MaxValue;

                for (int p = 0; p < 4; p++)
                {
                    int dr = red[i] - palette[p * 3];
                    int dg = green[i] - palette[p * 3 + 1];
                    int db = blue[i] - palette[p * 3 + 2];
                    int distance = dr * dr + dg * dg + db * db;

                    if (distance >= bestDistance)
                        continue;

                    bestDistance = distance;
                    best = p;
                }

                bits |= (uint)best << (2 * i);
            }
        }

        destination[0] = (byte)color0;
        destination[1] = (byte)(color0 >> 8);
        destination[2] = (byte)color1;
        destination[3] = (byte)(color1 >> 8);

        BinaryPrimitives.WriteUInt32LittleEndian(destination[4..], bits);
    }

    private static void EncodeAlphaDxt5(ReadOnlySpan<int> alpha, Span<byte> destination)
    {
        int alpha0 = 0;
        int alpha1 = 255;

        for (int i = 0; i < 16; i++)
        {
            alpha0 = Math.Max(alpha0, alpha[i]);
            alpha1 = Math.Min(alpha1, alpha[i]);
        }

        ulong bits = 0;

        if (alpha0 != alpha1)
        {
            Span<int> lookup = stackalloc int[8];

            lookup[0] = alpha0;
            lookup[1] = alpha1;

            for (int i = 2; i < 8; i++)
                lookup[i] = ((8 - i) * alpha0 + (i - 1) * alpha1) / 7;

            for (int i = 0; i < 16; i++)
            {
                int best = 0;
                int bestDistance = int.MaxValue;

                for (int j = 0; j < 8; j++)
                {
                    int distance = Math.Abs(alpha[i] - lookup[j]);

                    if (distance >= bestDistance)
                        continue;

                    bestDistance = distance;
                    best = j;
                }

                bits |= (ulong)best << (3 * i);
            }
        }

        destination[0] = (byte)alpha0;
        destination[1] = (byte)alpha1;

        for (int k = 0; k < 6; k++)
            destination[2 + k] = (byte)(bits >> (8 * k));
    }

    private static void EncodeAlphaDxt3(ReadOnlySpan<int> alpha, Span<byte> destination)
    {
        for (int b = 0; b < 8; b++)
            destination[b] = (byte)(((alpha[2 * b] >> 4) & 0x0F) | (((alpha[2 * b + 1] >> 4) & 0x0F) << 4));
    }

    private static int Pack565(int red, int green, int blue) => ((red >> 3) << 11) | ((green >> 2) << 5) | (blue >> 3);

    private static void Unpack565(int value, Span<int> destination)
    {
        destination[0] = ((value >> 11) & 0x1F) * 255 / 31;
        destination[1] = ((value >> 5) & 0x3F) * 255 / 63;
        destination[2] = (value & 0x1F) * 255 / 31;
    }
}