namespace Vita.Core.Typography;

internal sealed class CffOutlineReader : IGlyphOutlineSource
{
    private readonly byte[] data;
    private readonly CffIndex globalSubrs;
    private readonly CffIndex charStrings;
    private readonly CffIndex[] localSubrs;
    private readonly int[]? fontDictOfGlyph;

    public CffOutlineReader(byte[] data, int offset)
    {
        this.data = data;

        int headerSize = BigEndian.U8(data, offset + 2);
        var names = new CffIndex(data, offset + headerSize);
        var topDicts = new CffIndex(data, names.End);
        var strings = new CffIndex(data, topDicts.End);

        globalSubrs = new CffIndex(data, strings.End);

        var (topStart, topEnd) = topDicts.Get(0);
        var top = CffDict.Parse(data, topStart, topEnd);

        if (!top.TryGetValue(17, out double[]? charStringsOffset) || charStringsOffset.Length == 0)
            throw new InvalidDataException("CFF font has no CharStrings");

        charStrings = new CffIndex(data, offset + (int)charStringsOffset[0]);

        if (top.TryGetValue(1236, out double[]? fontDictArray) && fontDictArray.Length > 0)
        {
            var fontDicts = new CffIndex(data, offset + (int)fontDictArray[0]);

            localSubrs = new CffIndex[fontDicts.Count];

            for (int i = 0; i < fontDicts.Count; i++)
            {
                var (start, end) = fontDicts.Get(i);

                localSubrs[i] = ReadLocalSubrs(offset, CffDict.Parse(data, start, end));
            }

            fontDictOfGlyph = top.TryGetValue(1237, out double[]? select) && select.Length > 0 ? ReadFdSelect(offset + (int)select[0], charStrings.Count) : null;

            return;
        }

        localSubrs = [ReadLocalSubrs(offset, top)];
    }

    public GlyphPath GetOutline(int glyphIndex)
    {
        if (glyphIndex < 0 || glyphIndex >= charStrings.Count)
            return new GlyphPath();

        int fontDict = fontDictOfGlyph is null ? 0 : fontDictOfGlyph[glyphIndex];
        var (start, end) = charStrings.Get(glyphIndex);

        return new CffCharStringInterpreter(data, globalSubrs, localSubrs[Math.Min(fontDict, localSubrs.Length - 1)]).Run(start, end);
    }

    private CffIndex ReadLocalSubrs(int cffOffset, Dictionary<int, double[]> dict)
    {
        if (!dict.TryGetValue(18, out double[]? privateInfo) || privateInfo.Length < 2)
            return new CffIndex([0, 0], 0);

        int privateStart = cffOffset + (int)privateInfo[1];
        var privateDict = CffDict.Parse(data, privateStart, privateStart + (int)privateInfo[0]);

        return privateDict.TryGetValue(19, out double[]? subrs) && subrs.Length > 0 ? new CffIndex(data, privateStart + (int)subrs[0]) : new CffIndex([0, 0], 0);
    }

    private int[] ReadFdSelect(int position, int glyphCount)
    {
        int[] result = new int[glyphCount];
        int format = BigEndian.U8(data, position);

        if (format == 0)
        {
            for (int i = 0; i < glyphCount; i++)
                result[i] = data[position + 1 + i];

            return result;
        }

        int ranges = BigEndian.U16(data, position + 1);

        for (int r = 0; r < ranges; r++)
        {
            int first = BigEndian.U16(data, position + 3 + r * 3);
            int fd = data[position + 5 + r * 3];
            int next = BigEndian.U16(data, position + 6 + r * 3);

            for (int g = first; g < next && g < glyphCount; g++)
                result[g] = fd;
        }

        return result;
    }
}