namespace Vita.Core.Typography;

public sealed record GlyphInkRequest(string FontKey, byte[] FontData, string Character, double EmSizePixels, double StrokePixels, GlyphColor Fill, GlyphColor StrokeFill);