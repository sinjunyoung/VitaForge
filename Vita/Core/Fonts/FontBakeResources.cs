using Vita.Core.Typography;

namespace Vita.Core.Fonts;

internal sealed record FontBakeResources(FontRenderProfile Body, FontRenderProfile Menu, KoreanSjisMapping Mapping, IReadOnlySet<int> UsedAsciiCodes, IGlyphRasterizer Rasterizer);