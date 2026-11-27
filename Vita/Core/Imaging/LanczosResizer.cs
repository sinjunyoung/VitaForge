namespace Vita.Core.Imaging;

internal static class LanczosResizer
{
    private const double Support = 3.0;

    public static RgbaImage Resize(RgbaImage source, int width, int height)
    {
        byte[] premultiplied = Premultiply(source.Pixels);
        byte[] horizontal = ResizeHorizontal(premultiplied, source.Width, source.Height, width);
        byte[] vertical = ResizeVertical(horizontal, width, source.Height, height);

        return new RgbaImage(width, height, Unpremultiply(vertical));
    }

    private static byte[] ResizeHorizontal(byte[] source, int sourceWidth, int rows, int width)
    {
        var (starts, weights) = BuildWeights(sourceWidth, width);
        byte[] output = new byte[width * rows * 4];

        for (int y = 0; y < rows; y++)
        {
            for (int x = 0; x < width; x++)
            {
                double[] taps = weights[x];

                for (int channel = 0; channel < 4; channel++)
                {
                    double sum = 0;

                    for (int k = 0; k < taps.Length; k++)
                        sum += taps[k] * source[(y * sourceWidth + starts[x] + k) * 4 + channel];

                    output[(y * width + x) * 4 + channel] = ToByte(sum);
                }
            }
        }

        return output;
    }

    private static byte[] ResizeVertical(byte[] source, int width, int sourceHeight, int height)
    {
        var (starts, weights) = BuildWeights(sourceHeight, height);
        byte[] output = new byte[width * height * 4];

        for (int y = 0; y < height; y++)
        {
            double[] taps = weights[y];

            for (int x = 0; x < width; x++)
            {
                for (int channel = 0; channel < 4; channel++)
                {
                    double sum = 0;

                    for (int k = 0; k < taps.Length; k++)
                        sum += taps[k] * source[((starts[y] + k) * width + x) * 4 + channel];

                    output[(y * width + x) * 4 + channel] = ToByte(sum);
                }
            }
        }

        return output;
    }

    private static (int[] Starts, double[][] Weights) BuildWeights(int sourceSize, int targetSize)
    {
        double scale = (double)sourceSize / targetSize;
        double filterScale = Math.Max(scale, 1.0);
        double support = Support * filterScale;
        int[] starts = new int[targetSize];
        double[][] weights = new double[targetSize][];

        for (int i = 0; i < targetSize; i++)
        {
            double center = (i + 0.5) * scale;
            int start = Math.Max(0, (int)(center - support + 0.5));
            int end = Math.Min(sourceSize, (int)(center + support + 0.5));
            double[] taps = new double[Math.Max(0, end - start)];
            double total = 0;

            for (int k = 0; k < taps.Length; k++)
            {
                taps[k] = Lanczos((k + start - center + 0.5) / filterScale);
                total += taps[k];
            }

            if (total != 0)
            {
                for (int k = 0; k < taps.Length; k++)
                    taps[k] /= total;
            }

            starts[i] = start;
            weights[i] = taps;
        }

        return (starts, weights);
    }

    private static double Lanczos(double x)
    {
        if (x <= -Support || x >= Support)
            return 0;

        return Sinc(x) * Sinc(x / Support);
    }

    private static double Sinc(double x)
    {
        if (x == 0)
            return 1;

        double value = Math.PI * x;

        return Math.Sin(value) / value;
    }

    private static byte ToByte(double value) => (byte)Math.Clamp((int)Math.Round(value, MidpointRounding.AwayFromZero), 0, 255);

    private static byte[] Premultiply(byte[] pixels)
    {
        byte[] result = new byte[pixels.Length];

        for (int i = 0; i < pixels.Length; i += 4)
        {
            int alpha = pixels[i + 3];

            result[i] = (byte)((pixels[i] * alpha + 127) / 255);
            result[i + 1] = (byte)((pixels[i + 1] * alpha + 127) / 255);
            result[i + 2] = (byte)((pixels[i + 2] * alpha + 127) / 255);
            result[i + 3] = (byte)alpha;
        }

        return result;
    }

    private static byte[] Unpremultiply(byte[] pixels)
    {
        byte[] result = new byte[pixels.Length];

        for (int i = 0; i < pixels.Length; i += 4)
        {
            int alpha = pixels[i + 3];

            result[i + 3] = (byte)alpha;

            if (alpha == 0)
                continue;

            result[i] = (byte)Math.Min(255, pixels[i] * 255 / alpha);
            result[i + 1] = (byte)Math.Min(255, pixels[i + 1] * 255 / alpha);
            result[i + 2] = (byte)Math.Min(255, pixels[i + 2] * 255 / alpha);
        }

        return result;
    }
}