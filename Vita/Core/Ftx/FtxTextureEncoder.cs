namespace Vita.Core.Ftx;

internal static class FtxTextureEncoder
{
    public static byte[] EncodePayload(byte[] rgba, int width, int height, uint format, int mipLevels)
    {
        var chunks = new List<byte[]>();
        byte[] current = rgba;
        int currentWidth = width;
        int currentHeight = height;

        for (int level = 0; level < Math.Max(1, mipLevels); level++)
        {
            if (currentWidth < 4 || currentHeight < 4)
                break;

            byte[] linear = DxtEncoder.Encode(current, currentWidth, currentHeight, format);

            chunks.Add(MortonSwizzle.Swizzle(linear, currentWidth / 4, currentHeight / 4, 16));

            current = DownsampleHalf(current, currentWidth, currentHeight);
            currentWidth /= 2;
            currentHeight /= 2;
        }

        byte[] payload = new byte[chunks.Sum(c => c.Length)];
        int offset = 0;

        foreach (byte[] chunk in chunks)
        {
            chunk.CopyTo(payload, offset);

            offset += chunk.Length;
        }

        return payload;
    }

    private static byte[] DownsampleHalf(byte[] rgba, int width, int height)
    {
        int halfWidth = width / 2;
        int halfHeight = height / 2;
        byte[] output = new byte[halfWidth * halfHeight * 4];

        for (int y = 0; y < halfHeight; y++)
        {
            for (int x = 0; x < halfWidth; x++)
            {
                int topLeft = ((y * 2) * width + x * 2) * 4;
                int topRight = topLeft + 4;
                int bottomLeft = topLeft + width * 4;
                int bottomRight = bottomLeft + 4;

                for (int channel = 0; channel < 4; channel++)
                    output[(y * halfWidth + x) * 4 + channel] = (byte)((rgba[topLeft + channel] + rgba[topRight + channel] + rgba[bottomLeft + channel] + rgba[bottomRight + channel] + 2) / 4);
            }
        }

        return output;
    }
}