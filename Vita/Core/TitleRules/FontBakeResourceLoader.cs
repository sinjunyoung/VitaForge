using Vita.Core.Fonts;
using Vita.Core.Typography;

namespace Vita.Core.TitleRules;

internal static class FontBakeResourceLoader
{
    private const string BodyFontName = "fonts/RIDIBatang.otf";
    private const string MenuFontName = "fonts/Griun_PolSensibility-Rg.ttf";
    private const string MappingName = "translations/kr_sjis_mapping.json";
    private const string MessagesName = "translations/jp_messages.json";

    public static bool TryLoad(ChunkPatchAssets assets, IGlyphRasterizer rasterizer, out FontBakeResources? resources, out string? error)
    {
        resources = null;
        error = null;

        byte[]? body = assets.ReadBySuffix(BodyFontName);
        byte[]? menu = assets.ReadBySuffix(MenuFontName);
        byte[]? mapping = assets.ReadBySuffix(MappingName);
        byte[]? messages = assets.ReadBySuffix(MessagesName);

        string? missing = body is null ? BodyFontName : menu is null ? MenuFontName : mapping is null ? MappingName : messages is null ? MessagesName : null;

        if (missing is not null)
        {
            error = $"font bake data is missing from the patch package: {missing}";

            return false;
        }

        resources = new FontBakeResources(
            new FontRenderProfile("RIDIBatang.otf", body!, 20, 16, 1.5),
            new FontRenderProfile("Griun_PolSensibility-Rg.ttf", menu!, 20, 18, 0),
            KoreanSjisMapping.Parse(mapping!),
            AsciiOverflowMap.ReadUsedCodes(messages!),
            rasterizer);

        return true;
    }
}