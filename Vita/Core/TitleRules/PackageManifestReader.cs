using System.Text.Json;
using Vita.Core.Models;
using Vita.Core.Services;

namespace Vita.Core.TitleRules;

internal static class PackageManifestReader
{
    private const string ManifestFileName = "manifest.json";

    private static readonly string[] SupportedFormats = ["nlks-korean-release-manifest-v1"];

    private static readonly JsonSerializerOptions Options = new()
    {
        AllowTrailingCommas = true,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip
    };

    public static PackageManifest? TryRead(string titleId, IReadOnlyList<PatchContainerFile> files, ISourceAccessor accessor)
    {
        var candidate = files
            .Where(f => Path.GetFileName(f.Path).Equals(ManifestFileName, StringComparison.OrdinalIgnoreCase))
            .OrderBy(f => f.Path.Count(c => c == '/'))
            .FirstOrDefault();

        if (candidate is null)
            return null;

        PackageManifest? manifest;

        try
        {
            using var stream = new MemoryStream(accessor.ReadAllBytes(candidate.Raw), writable: false);

            manifest = JsonSerializer.Deserialize<PackageManifest>(stream, Options);
        }
        catch (Exception ex) when (ex is JsonException or IOException or InvalidDataException)
        {
            return null;
        }

        if (manifest is null || !SupportedFormats.Contains(manifest.Format, StringComparer.Ordinal) || !manifest.TitleId.Equals(titleId, StringComparison.OrdinalIgnoreCase))
            return null;

        int slash = candidate.Path.LastIndexOf('/');

        manifest.Root = slash < 0 ? string.Empty : candidate.Path[..(slash + 1)];

        return manifest;
    }
}
