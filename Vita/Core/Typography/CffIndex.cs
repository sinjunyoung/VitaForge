namespace Vita.Core.Typography;

internal sealed class CffIndex
{
    private readonly int[] offsets;
    private readonly int dataStart;

    public CffIndex(byte[] data, int position)
    {
        Count = BigEndian.U16(data, position);

        if (Count == 0)
        {
            offsets = [];
            End = position + 2;

            return;
        }

        int offsetSize = BigEndian.U8(data, position + 2);

        offsets = new int[Count + 1];

        for (int i = 0; i <= Count; i++)
            offsets[i] = ReadOffset(data, position + 3 + i * offsetSize, offsetSize);

        dataStart = position + 3 + (Count + 1) * offsetSize - 1;
        End = dataStart + offsets[Count];
    }

    public int Count { get; }

    public int End { get; }

    public (int Start, int End) Get(int index) => (dataStart + offsets[index], dataStart + offsets[index + 1]);

    private static int ReadOffset(byte[] data, int position, int size)
    {
        int value = 0;

        for (int i = 0; i < size; i++)
            value = (value << 8) | data[position + i];

        return value;
    }
}