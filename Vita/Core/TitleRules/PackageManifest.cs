using System.Text.Json.Serialization;

namespace Vita.Core.TitleRules;

internal sealed class PackageManifest
{
    [JsonPropertyName("format")]
    public string Format { get; set; } = string.Empty;

    [JsonPropertyName("title_id")]
    public string TitleId { get; set; } = string.Empty;

    [JsonPropertyName("files")]
    public List<PackageManifestEntry> Files { get; set; } = [];

    [JsonIgnore]
    public string Root { get; set; } = string.Empty;

    public string PatchPath(PackageManifestEntry entry) => $"{Root}patches/{entry.Patch}";
}
