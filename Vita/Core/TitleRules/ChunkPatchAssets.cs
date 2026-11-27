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

    private readonly ISourceAccessor accessor;
    private readonly PatchContainerFile? manifestFile;
    private readonly Dictionary<string, PatchContainerFile> images;
    private Dictionary<string, FtxTextureInfo>? manifest;

    public ChunkPatchAssets(IReadOnlyList<PatchContainerFile> files, ISourceAccessor accessor)
    {
        this.accessor = accessor;

        manifestFile = files
            .Where(f => f.Path.EndsWith(ManifestSuffix, StringComparison.OrdinalIgnoreCase))
            .OrderBy(f => f.Path.Count(c => c == '/'))
            .FirstOrDefault();

        images = files
            .Where(f => f.Path.EndsWith(".png", StringComparison.OrdinalIgnoreCase) && $"/{f.Path}".Contains(ImageFolder, StringComparison.OrdinalIgnoreCase))
            .GroupBy(f => Path.GetFileNameWithoutExtension(f.Path), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.OrderBy(f => f.Path.Count(c => c == '/')).First(), StringComparer.OrdinalIgnoreCase);
    }

    public bool HasTextures => manifestFile is not null && images.Count > 0;

    public IReadOnlyDictionary<string, FtxTextureInfo> Manifest => manifest ??= LoadManifest();

    public RgbaImage? LoadImage(string hash)
        => images.TryGetValue(hash, out var file) ? PngDecoder.Decode(accessor.ReadAllBytes(file.Raw)) : null;

    private Dictionary<string, FtxTextureInfo> LoadManifest()
    {
        byte[] bytes = accessor.ReadAllBytes(manifestFile!.Raw);

        return JsonSerializer.Deserialize<Dictionary<string, FtxTextureInfo>>(bytes, new JsonSerializerOptions { AllowTrailingCommas = true }) ?? [];
    }
}