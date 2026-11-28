namespace Vita.Core.Typography;

internal sealed class SfntCmap
{
    private readonly byte[] data;
    private readonly int subtable;
    private readonly int format;

    public SfntCmap(byte[] data, int offset)
    {
        this.data = data;

        int count = BigEndian.U16(data, offset + 2);
        int bestScore = -1;

        for (int i = 0; i < count; i++)
        {
            int record = offset + 4 + i * 8;
            int platform = BigEndian.U16(data, record);
            int encoding = BigEndian.U16(data, record + 2);
            int candidate = offset + (int)BigEndian.U32(data, record + 4);
            int candidateFormat = BigEndian.U16(data, candidate);
            int score = Score(platform, encoding, candidateFormat);

            if (score <= bestScore)
                continue;

            bestScore = score;
            subtable = candidate;
            format = candidateFormat;
        }

        if (bestScore < 0)
            throw new NotSupportedException("font has no supported cmap subtable");
    }

    public int Lookup(int codePoint) => format == 12 ? LookupFormat12(codePoint) : LookupFormat4(codePoint);

    private static int Score(int platform, int encoding, int format)
    {
        if (format == 12 && ((platform == 3 && encoding == 10) || platform == 0))
            return 4;

        if (format == 4 && ((platform == 3 && encoding == 1) || platform == 0))
            return 3;

        return format == 12 ? 2 : format == 4 ? 1 : -1;
    }

    private int LookupFormat12(int codePoint)
    {
        int groups = (int)BigEndian.U32(data, subtable + 12);

        for (int i = 0; i < groups; i++)
        {
            int group = subtable + 16 + i * 12;
            uint start = BigEndian.U32(data, group);
            uint end = BigEndian.U32(data, group + 4);

            if (codePoint >= start && codePoint <= end)
                return (int)(BigEndian.U32(data, group + 8) + (uint)codePoint - start);
        }

        return 0;
    }

    private int LookupFormat4(int codePoint)
    {
        if (codePoint > 0xFFFF)
            return 0;

        int segments = BigEndian.U16(data, subtable + 6) / 2;
        int endCodes = subtable + 14;
        int startCodes = endCodes + segments * 2 + 2;
        int deltas = startCodes + segments * 2;
        int rangeOffsets = deltas + segments * 2;

        for (int i = 0; i < segments; i++)
        {
            if (codePoint > BigEndian.U16(data, endCodes + i * 2))
                continue;

            if (codePoint < BigEndian.U16(data, startCodes + i * 2))
                return 0;

            int delta = BigEndian.U16(data, deltas + i * 2);
            int rangeOffset = BigEndian.U16(data, rangeOffsets + i * 2);

            if (rangeOffset == 0)
                return (codePoint + delta) & 0xFFFF;

            int glyphAddress = rangeOffsets + i * 2 + rangeOffset + (codePoint - BigEndian.U16(data, startCodes + i * 2)) * 2;
            int glyph = BigEndian.U16(data, glyphAddress);

            return glyph == 0 ? 0 : (glyph + delta) & 0xFFFF;
        }

        return 0;
    }
}