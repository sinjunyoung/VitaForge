using System.Text;

namespace Vita.Core.Typography;

internal sealed class SfntFont
{
    private readonly SfntCmap cmap;
    private readonly IGlyphOutlineSource outlines;

    private SfntFont(byte[] data)
    {
        if (data.Length < 12)
            throw new InvalidDataException("font file is too small");

        var tables = new Dictionary<string, (int Offset, int Length)>(StringComparer.Ordinal);
        int count = BigEndian.U16(data, 4);

        for (int i = 0; i < count; i++)
        {
            int record = 12 + i * 16;

            tables[Encoding.ASCII.GetString(data, record, 4)] = ((int)BigEndian.U32(data, record + 8), (int)BigEndian.U32(data, record + 12));
        }

        if (!tables.TryGetValue("head", out var head) || !tables.TryGetValue("cmap", out var cmapTable))
            throw new InvalidDataException("font is missing required tables");

        UnitsPerEm = BigEndian.U16(data, head.Offset + 18);
        cmap = new SfntCmap(data, cmapTable.Offset);

        if (tables.TryGetValue("CFF ", out var cff))
        {
            outlines = new CffOutlineReader(data, cff.Offset);

            return;
        }

        if (tables.TryGetValue("glyf", out var glyf) && tables.TryGetValue("loca", out var loca) && tables.TryGetValue("maxp", out var maxp))
        {
            outlines = new TrueTypeOutlineReader(data, glyf.Offset, loca.Offset, BigEndian.I16(data, head.Offset + 50) != 0, BigEndian.U16(data, maxp.Offset + 4));

            return;
        }

        throw new NotSupportedException("font has no supported outline table");
    }

    public int UnitsPerEm { get; }

    public static SfntFont Parse(byte[] data) => new(data);

    public int GetGlyphIndex(int codePoint) => cmap.Lookup(codePoint);

    public GlyphPath GetOutline(int glyphIndex) => outlines.GetOutline(glyphIndex);
}