namespace Vita.Core.Typography;

public static class GlyphRasterizerProvider
{
    private static IGlyphRasterizer? current;

    public static IGlyphRasterizer Current
    {
        get => current ??= new OutlineGlyphRasterizer();
        set => current = value;
    }
}