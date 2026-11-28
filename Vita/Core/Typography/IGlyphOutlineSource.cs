namespace Vita.Core.Typography;

internal interface IGlyphOutlineSource
{
    GlyphPath GetOutline(int glyphIndex);
}