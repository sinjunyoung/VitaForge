namespace Vita.Core.Typography;

internal static class DistanceTransform
{
    private const double Infinity = 1e20;

    public static double[] SquaredDistanceToSet(bool[] inside, int width, int height)
    {
        double[] distance = new double[width * height];

        for (int i = 0; i < distance.Length; i++)
            distance[i] = inside[i] ? 0 : Infinity;

        int longest = Math.Max(width, height);
        double[] line = new double[longest];
        double[] output = new double[longest];
        int[] vertices = new int[longest];
        double[] boundaries = new double[longest + 1];

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
                line[y] = distance[y * width + x];

            Transform(line, output, height, vertices, boundaries);

            for (int y = 0; y < height; y++)
                distance[y * width + x] = output[y];
        }

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
                line[x] = distance[y * width + x];

            Transform(line, output, width, vertices, boundaries);

            for (int x = 0; x < width; x++)
                distance[y * width + x] = output[x];
        }

        return distance;
    }

    private static void Transform(double[] f, double[] d, int n, int[] v, double[] z)
    {
        int k = 0;

        v[0] = 0;
        z[0] = -Infinity;
        z[1] = Infinity;

        for (int q = 1; q < n; q++)
        {
            double s = Intersection(f, q, v[k]);

            while (s <= z[k])
            {
                k--;

                s = Intersection(f, q, v[k]);
            }

            k++;
            v[k] = q;
            z[k] = s;
            z[k + 1] = Infinity;
        }

        k = 0;

        for (int q = 0; q < n; q++)
        {
            while (z[k + 1] < q)
                k++;

            d[q] = (q - v[k]) * (double)(q - v[k]) + f[v[k]];
        }
    }

    private static double Intersection(double[] f, int q, int p) => (f[q] + (double)q * q - (f[p] + (double)p * p)) / (2.0 * q - 2.0 * p);
}