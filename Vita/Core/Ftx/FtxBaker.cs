using System.IO.Hashing;
using Vita.Core.Imaging;

namespace Vita.Core.Ftx;

internal sealed class FtxBaker(Func<string, RgbaImage?> loadImage)
{
    public FtxBakeResult Bake(byte[] ftx, IReadOnlyList<FtxBakeItem> items)
    {
        var textures = new Dictionary<int, FtxTexture>();

        foreach (var texture in FtxParser.ParseAll(ftx))
            textures[texture.BlockOrdinal] = texture;

        var skipped = new List<string>();
        int baked = 0;

        foreach (var item in items)
        {
            string? reason = TryBake(ftx, textures, item);

            if (reason is null)
                baked++;
            else
                skipped.Add($"{item.Hash}: {reason}");
        }

        return new FtxBakeResult(baked, skipped);
    }

    private string? TryBake(byte[] ftx, Dictionary<int, FtxTexture> textures, FtxBakeItem item)
    {
        if (!textures.TryGetValue(item.BlockOrdinal, out var texture))
            return $"texture block {item.BlockOrdinal} not found";

        if (texture.Width != item.Width || texture.Height != item.Height)
            return $"size mismatch (expected {item.Width}x{item.Height}, got {texture.Width}x{texture.Height})";

        int baseSize = texture.Width / 4 * (texture.Height / 4) * 16;

        if (item.Hash.Length < 8 || texture.AbsoluteOffset + baseSize > ftx.Length)
            return "texture data is out of range";

        string current = XxHash3.HashToUInt64(ftx.AsSpan(texture.AbsoluteOffset, baseSize)).ToString("X16")[..8];

        if (!current.Equals(item.Hash[..8], StringComparison.OrdinalIgnoreCase))
            return $"texture hash mismatch (got {current})";

        var image = loadImage(item.Hash);

        if (image is null)
            return "Korean texture image not found";

        try
        {
            var resized = image.Width == texture.Width && image.Height == texture.Height ? image : LanczosResizer.Resize(image, texture.Width, texture.Height);

            FtxTextureWriter.Replace(ftx, texture, baseSize, resized.Pixels);

            return null;
        }
        catch (Exception ex) when (ex is NotSupportedException or InvalidDataException)
        {
            return ex.Message;
        }
    }
}