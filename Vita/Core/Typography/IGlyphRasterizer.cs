namespace Vita.Core.Typography;

public interface IGlyphRasterizer
{
    GlyphInk? Render(GlyphInkRequest request);
}