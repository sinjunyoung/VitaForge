using System.Buffers.Binary;

namespace Vita.Core.Ftx;

internal static class DxtDecoder
{
    public static byte[] Decode(ReadOnlySpan<byte> linear, int width, int height, uint format)
    {
        if (format != DxtFormat.Dxt5 && format != DxtFormat.Dxt3)
            throw new NotSupportedException($"unsupported texture format 0x{format:X8}");

        int blocksWide = width / 4;
        int blocksHigh = height / 4;
        byte[] output = new byte[width * height * 4];
        Span<int> palette = stackalloc int[12];
        Span<int> alpha = stackalloc int[8];

        for (int by = 0; by < blocksHigh; by++)
        {
            for (int bx = 0; bx < blocksWide; bx++)
            {
                var block = linear.Slice((by * blocksWide + bx) * 16, 16);

                Color565.Unpack(BinaryPrimitives.ReadUInt16LittleEndian(block[8..]), palette[..3]);
                Color565.Unpack(BinaryPrimitives.ReadUInt16LittleEndian(block[10..]), palette.Slice(3, 3));

                for (int channel = 0; channel < 3; channel++)
                {
                    palette[6 + channel] = (2 * palette[channel] + palette[3 + channel]) / 3;
                    palette[9 + channel] = (palette[channel] + 2 * palette[3 + channel]) / 3;
                }

                uint colorBits = BinaryPrimitives.ReadUInt32LittleEndian(block[12..]);
                ulong alphaBits = 0;

                if (format == DxtFormat.Dxt5)
                {
                    BuildAlphaTable(block[0], block[1], alpha);

                    for (int k = 0; k < 6; k++)
                        alphaBits |= (ulong)block[2 + k] << (8 * k);
                }

                for (int i = 0; i < 16; i++)
                {
                    int colorIndex = (int)((colorBits >> (2 * i)) & 3);
                    int target = ((by * 4 + i / 4) * width + bx * 4 + i % 4) * 4;

                    output[target] = (byte)palette[colorIndex * 3];
                    output[target + 1] = (byte)palette[colorIndex * 3 + 1];
                    output[target + 2] = (byte)palette[colorIndex * 3 + 2];
                    output[target + 3] = format == DxtFormat.Dxt5
                        ? (byte)alpha[(int)((alphaBits >> (3 * i)) & 7)]
                        : (byte)(((block[i / 2] >> (i % 2 == 0 ? 0 : 4)) & 0x0F) * 17);
                }
            }
        }

        return output;
    }

    private static void BuildAlphaTable(int alpha0, int alpha1, Span<int> table)
    {
        table[0] = alpha0;
        table[1] = alpha1;

        if (alpha0 > alpha1)
        {
            for (int j = 2; j < 8; j++)
                table[j] = ((8 - j) * alpha0 + (j - 1) * alpha1) / 7;

            return;
        }

        for (int j = 2; j < 6; j++)
            table[j] = ((6 - j) * alpha0 + (j - 1) * alpha1) / 5;

        table[6] = 0;
        table[7] = 255;
    }
}