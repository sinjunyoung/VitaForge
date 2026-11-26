using System.Text.Json.Serialization;

namespace Vita.Core.TitleRules;

internal sealed class PackageManifestEntry
{
    [JsonPropertyName("destination")]
    public string Destination { get; set; } = string.Empty;

    [JsonPropertyName("patch")]
    public string Patch { get; set; } = string.Empty;

    [JsonPropertyName("patch_sha256")]
    public string PatchSha256 { get; set; } = string.Empty;

    [JsonPropertyName("source_kind")]
    public string SourceKind { get; set; } = string.Empty;

    [JsonPropertyName("source")]
    public PackageManifestSource? Source { get; set; }

    [JsonPropertyName("source_sha256")]
    public string SourceSha256 { get; set; } = string.Empty;

    [JsonPropertyName("source_size")]
    public long SourceSize { get; set; }

    [JsonPropertyName("target_sha256")]
    public string TargetSha256 { get; set; } = string.Empty;

    [JsonPropertyName("target_size")]
    public long TargetSize { get; set; }

    [JsonIgnore]
    public bool IsDirect => SourceKind is PackageManifestSourceKinds.BaseFile or PackageManifestSourceKinds.UpdateFile;
}
