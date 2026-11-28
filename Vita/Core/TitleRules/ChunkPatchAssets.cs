using System.Text.Json;
using Vita.Core.Ftx;
using Vita.Core.Imaging;
using Vita.Core.Models;
using Vita.Core.Services;

namespace Vita.Core.TitleRules;

internal sealed class ChunkPatchAssets
{
    private const string ManifestSuffix = "translations/ftx_texture_map.json";
    private const string ImageFolder = "/textures/kr/ui/";
    private const string VariantImageFolder = "/textures/kr/ui_xbutton/";

    private readonly IReadOnlyList<PatchContainerFile> files;
    private readonly ISourceAccessor accessor;
    private readonly PatchContainerFile? manifestFile;
    private readonly Dictionary<string, PatchContainerFile> images;
    private readonly Dictionary<string, PatchContainerFile> variantImages;
    private Dictionary<string, FtxTextureInfo>? manifest;

    public ChunkPatchAssets(IReadOnlyList<PatchContainerFile> files, ISourceAccessor accessor)
    {
        this.files = files;
        this.accessor = accessor;

        manifestFile = files
            .Where(f => f.Path.EndsWith(ManifestSuffix, StringComparison.OrdinalIgnoreCase))
            .OrderBy(f => f.Path.Count(c => c == '/'))
            .FirstOrDefault();

        images = IndexImages(files, ImageFolder);
        variantImages = IndexImages(files, VariantImageFolder);
    }

    public bool HasTextures => manifestFile is not null && images.Count > 0;

    public IReadOnlyDictionary<string, FtxTextureInfo> Manifest => manifest ??= LoadManifest();

    public RgbaImage? LoadImage(string hash)
    {
        var source = TitleRuleOptions.EnterButton == EnterButton.Cross && variantImages.TryGetValue(hash, out var variant) ? variant : images.GetValueOrDefault(hash);

        return source is null ? null : PngDecoder.Decode(accessor.ReadAllBytes(source.Raw));
    }

    public byte[]? ReadBySuffix(string suffix)
    {
        var file = files
            .Where(f => $"/{f.Path}".EndsWith($"/{suffix}", StringComparison.OrdinalIgnoreCase))
            .OrderBy(f => f.Path.Count(c => c == '/'))
            .FirstOrDefault();

        return file is null ? null : accessor.ReadAllBytes(file.Raw);
    }

    private static Dictionary<string, PatchContainerFile> IndexImages(IReadOnlyList<PatchContainerFile> files, string folder)
        => files
            .Where(f => f.Path.EndsWith(".png", StringComparison.OrdinalIgnoreCase) && $"/{f.Path}".Contains(folder, StringComparison.OrdinalIgnoreCase))
            .GroupBy(f => Path.GetFileNameWithoutExtension(f.Path), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.OrderBy(f => f.Path.Count(c => c == '/')).First(), StringComparer.OrdinalIgnoreCase);

    private Dictionary<string, FtxTextureInfo> LoadManifest()
    {
        byte[] bytes = accessor.ReadAllBytes(manifestFile!.Raw);

        return JsonSerializer.Deserialize<Dictionary<string, FtxTextureInfo>>(bytes, new JsonSerializerOptions { AllowTrailingCommas = true }) ?? [];
    }
}