namespace Vita.Core.Ftx;

internal static class Color565
{
    public static int Pack(int red, int green, int blue) => ((red >> 3) << 11) | ((green >> 2) << 5) | (blue >> 3);

    public static void Unpack(int value, Span<int> destination)
    {
        destination[0] = ((value >> 11) & 0x1F) * 255 / 31;
        destination[1] = ((value >> 5) & 0x3F) * 255 / 63;
        destination[2] = (value & 0x1F) * 255 / 31;
    }
}