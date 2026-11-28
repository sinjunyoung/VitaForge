using System.IO.Hashing;
using Vita.Core.Ftx;
using Vita.Core.Imaging;

namespace Vita.Core.Fonts;

internal sealed class FontFtxBaker(FontBakeResources resources)
{
    private readonly FontPageBaker pageBaker = new(resources);

    public int Bake(byte[] ftx, Action<string> log)
    {
        int baked = 0;

        foreach (var texture in FtxParser.ParseAll(ftx))
        {
            if (texture.Format != DxtFormat.Dxt5 && texture.Format != DxtFormat.Dxt3)
                continue;

            int baseSize = texture.Width / 4 * (texture.Height / 4) * 16;

            if (texture.AbsoluteOffset + baseSize > ftx.Length)
                continue;

            string high = XxHash3.HashToUInt64(ftx.AsSpan(texture.AbsoluteOffset, baseSize)).ToString("X16")[..8];
            var known = KnownFontPages.Find(high);

            if (known is null)
                continue;

            byte[] linear = MortonSwizzle.Unswizzle(ftx.AsSpan(texture.AbsoluteOffset, baseSize), texture.Width / 4, texture.Height / 4, 16);
            var page = new RgbaImage(texture.Width, texture.Height, DxtDecoder.Decode(linear, texture.Width, texture.Height, texture.Format));
            int glyphs = pageBaker.Bake(page, known);

            FtxTextureWriter.Replace(ftx, texture, baseSize, page.Pixels);

            log($"{known.FullHash} page baked ({glyphs} Korean glyphs, {texture.Width}x{texture.Height})");

            baked++;
        }

        return baked;
    }
}