namespace Vita.Core.Cpk;

internal ref struct ReverseBitReader
{
    private readonly ReadOnlySpan<byte> source;
    private int offset;
    private int pool;
    private int bitsLeft;

    public ReverseBitReader(ReadOnlySpan<byte> source)
    {
        this.source = source;

        offset = source.Length - 1;
        pool = 0;
        bitsLeft = 0;
    }

    public int Read(int count)
    {
        int result = 0;
        int produced = 0;

        while (produced < count)
        {
            if (bitsLeft == 0)
            {
                if (offset < 0)
                    return result;

                pool = source[offset--];
                bitsLeft = 8;
            }

            int take = Math.Min(bitsLeft, count - produced);

            result |= ((pool >> (bitsLeft - take)) & ((1 << take) - 1)) << (count - produced - take);

            bitsLeft -= take;
            produced += take;
        }

        return result;
    }
}