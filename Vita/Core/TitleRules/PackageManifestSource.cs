using System.Text.Json.Serialization;

namespace Vita.Core.TitleRules;

internal sealed class PackageManifestSource
{
    [JsonPropertyName("archive")]
    public string Archive { get; set; } = string.Empty;

    [JsonPropertyName("compression")]
    public string Compression { get; set; } = string.Empty;

    [JsonPropertyName("offset")]
    public long Offset { get; set; }

    [JsonPropertyName("stored_bytes")]
    public int StoredBytes { get; set; }

    [JsonPropertyName("path")]
    public string Path { get; set; } = string.Empty;
}
