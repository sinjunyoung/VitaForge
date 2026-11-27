using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;

namespace Vita.Core.Imaging;

internal static class PngDecoder
{
    private static readonly byte[] Signature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    public static RgbaImage Decode(byte[] png)
    {
        if (png.Length < 8 || !png.AsSpan(0, 8).SequenceEqual(Signature))
            throw new InvalidDataException("not a PNG image");

        int width = 0;
        int height = 0;
        int depth = 0;
        int colorType = 0;
        int interlace = 0;
        int position = 8;
        bool ended = false;

        using var compressed = new MemoryStream();

        while (!ended && position + 8 <= png.Length)
        {
            int length = BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(position));
            string type = Encoding.ASCII.GetString(png, position + 4, 4);
            int dataStart = position + 8;

            if (length < 0 || (long)dataStart + length + 4 > png.Length)
                throw new InvalidDataException("truncated PNG chunk");

            switch (type)
            {
                case "IHDR":
                    width = BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(dataStart));
                    height = BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(dataStart + 4));
                    depth = png[dataStart + 8];
                    colorType = png[dataStart + 9];
                    interlace = png[dataStart + 12];

                    break;
                case "IDAT":
                    compressed.Write(png, dataStart, length);

                    break;
                case "IEND":
                    ended = true;

                    break;
            }

            position = dataStart + length + 4;
        }

        if (width <= 0 || height <= 0 || depth != 8 || interlace != 0 || (colorType != 6 && colorType != 2))
            throw new NotSupportedException($"unsupported PNG format (depth {depth}, color type {colorType}, interlace {interlace})");

        int channels = colorType == 6 ? 4 : 3;
        int stride = width * channels;
        byte[] raw = new byte[(stride + 1) * height];

        compressed.Position = 0;

        using (var zlib = new ZLibStream(compressed, CompressionMode.Decompress))
            zlib.ReadExactly(raw);

        Unfilter(raw, stride, height, channels);

        byte[] pixels = new byte[width * height * 4];

        for (int y = 0; y < height; y++)
        {
            int source = y * (stride + 1) + 1;
            int target = y * width * 4;

            for (int x = 0; x < width; x++)
            {
                pixels[target + x * 4] = raw[source + x * channels];
                pixels[target + x * 4 + 1] = raw[source + x * channels + 1];
                pixels[target + x * 4 + 2] = raw[source + x * channels + 2];
                pixels[target + x * 4 + 3] = channels == 4 ? raw[source + x * channels + 3] : (byte)255;
            }
        }

        return new RgbaImage(width, height, pixels);
    }

    private static void Unfilter(byte[] raw, int stride, int height, int bytesPerPixel)
    {
        for (int y = 0; y < height; y++)
        {
            int rowStart = y * (stride + 1);
            int filter = raw[rowStart];
            int line = rowStart + 1;
            int prior = y == 0 ? -1 : line - (stride + 1);

            for (int i = 0; i < stride; i++)
            {
                int left = i >= bytesPerPixel ? raw[line + i - bytesPerPixel] : 0;
                int up = prior >= 0 ? raw[prior + i] : 0;
                int upLeft = prior >= 0 && i >= bytesPerPixel ? raw[prior + i - bytesPerPixel] : 0;

                raw[line + i] = (byte)(raw[line + i] + filter switch
                {
                    0 => 0,
                    1 => left,
                    2 => up,
                    3 => (left + up) / 2,
                    4 => Paeth(left, up, upLeft),
                    _ => throw new InvalidDataException($"invalid PNG filter {filter}")
                });
            }
        }
    }

    private static int Paeth(int left, int up, int upLeft)
    {
        int estimate = left + up - upLeft;
        int distanceLeft = Math.Abs(estimate - left);
        int distanceUp = Math.Abs(estimate - up);
        int distanceUpLeft = Math.Abs(estimate - upLeft);

        if (distanceLeft <= distanceUp && distanceLeft <= distanceUpLeft)
            return left;

        return distanceUp <= distanceUpLeft ? up : upLeft;
    }
}