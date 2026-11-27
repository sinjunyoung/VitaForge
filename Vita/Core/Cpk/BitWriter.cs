namespace Vita.Core.Cpk;

internal sealed class BitWriter(int initialCapacity)
{
    private byte[] buffer = new byte[Math.Max(16, initialCapacity)];
    private long bitCount;

    public void Write(int value, int count)
    {
        for (int shift = count - 1; shift >= 0; shift--)
        {
            int index = (int)(bitCount >> 3);

            if (index >= buffer.Length)
                Array.Resize(ref buffer, buffer.Length * 2);

            if (((value >> shift) & 1) != 0)
                buffer[index] |= (byte)(0x80 >> (int)(bitCount & 7));

            bitCount++;
        }
    }

    public byte[] ToReversedBytes()
    {
        int length = (int)((bitCount + 7) >> 3);
        byte[] result = new byte[length];

        for (int i = 0; i < length; i++)
            result[i] = buffer[length - 1 - i];

        return result;
    }
}