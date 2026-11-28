namespace Vita.Core.Ftx;

internal static class FtxTextureWriter
{
    public static void Replace(byte[] ftx, FtxTexture texture, int baseSize, byte[] rgba)
    {
        int textureSize = texture.TextureSize != 0 ? texture.TextureSize : baseSize;
        int mipLevels = textureSize > baseSize ? CountMipLevels(texture.Width, texture.Height, textureSize) : 1;
        byte[] payload = FtxTextureEncoder.EncodePayload(rgba, texture.Width, texture.Height, texture.Format, mipLevels);

        if (texture.AbsoluteOffset + textureSize > ftx.Length)
            throw new InvalidDataException("texture payload is out of range");

        Buffer.BlockCopy(payload, 0, ftx, texture.AbsoluteOffset, Math.Min(payload.Length, textureSize));
    }

    private static int CountMipLevels(int width, int height, int textureSize)
    {
        int levels = 0;
        int accumulated = 0;

        while (width >= 4 && height >= 4 && accumulated < textureSize)
        {
            accumulated += width / 4 * (height / 4) * 16;
            levels++;
            width /= 2;
            height /= 2;
        }

        return levels;
    }
}