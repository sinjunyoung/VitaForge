using System.Numerics;

namespace Vita.Core.Ftx;

internal static class MortonSwizzle
{
    public static byte[] Swizzle(ReadOnlySpan<byte> linear, int blocksWide, int blocksHigh, int blockSize)
    {
        byte[] output = new byte[blocksWide * blocksHigh * blockSize];
        int total = blocksWide * blocksHigh;
        int minDimension = Math.Min(blocksWide, blocksHigh);
        int shift = BitOperations.Log2((uint)minDimension);

        for (int morton = 0; morton < total; morton++)
        {
            int linearIndex = LinearIndex(morton, blocksWide, blocksHigh, minDimension, shift);

            if (linearIndex >= 0)
                linear.Slice(linearIndex * blockSize, blockSize).CopyTo(output.AsSpan(morton * blockSize, blockSize));
        }

        return output;
    }

    public static byte[] Unswizzle(ReadOnlySpan<byte> swizzled, int blocksWide, int blocksHigh, int blockSize)
    {
        byte[] output = new byte[blocksWide * blocksHigh * blockSize];
        int total = blocksWide * blocksHigh;
        int minDimension = Math.Min(blocksWide, blocksHigh);
        int shift = BitOperations.Log2((uint)minDimension);

        for (int morton = 0; morton < total; morton++)
        {
            int linearIndex = LinearIndex(morton, blocksWide, blocksHigh, minDimension, shift);

            if (linearIndex >= 0)
                swizzled.Slice(morton * blockSize, blockSize).CopyTo(output.AsSpan(linearIndex * blockSize, blockSize));
        }

        return output;
    }

    private static int LinearIndex(int morton, int blocksWide, int blocksHigh, int minDimension, int shift)
    {
        int x = Compact((uint)(morton >> 1)) & (minDimension - 1);
        int y = Compact((uint)morton) & (minDimension - 1);
        int upper = (morton >> (2 * shift)) << shift;

        if (blocksWide >= blocksHigh)
            x |= upper;
        else
            y |= upper;

        return x >= blocksWide || y >= blocksHigh ? -1 : y * blocksWide + x;
    }

    private static int Compact(uint value)
    {
        value &= 0x55555555;
        value = (value ^ (value >> 1)) & 0x33333333;
        value = (value ^ (value >> 2)) & 0x0F0F0F0F;
        value = (value ^ (value >> 4)) & 0x00FF00FF;
        value = (value ^ (value >> 8)) & 0x0000FFFF;

        return (int)value;
    }
}