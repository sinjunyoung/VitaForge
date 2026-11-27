namespace Vita.Core.Ftx;

internal sealed record FtxTexture(int GxtPosition, int BlockOrdinal, int Index, int TextureOffset, int TextureSize, uint Format, int Width, int Height, int MipMaps)
{
    public int AbsoluteOffset => GxtPosition + TextureOffset;
}