using System.Text.Json.Serialization;
using Vita.Core.Services;

namespace Vita.Core.TitleRules;

internal sealed class ChunkPatchDefinition
{
    [JsonPropertyName("format")]
    public string Format { get; set; } = string.Empty;

    [JsonPropertyName("source_name")]
    public string SourceName { get; set; } = string.Empty;

    [JsonPropertyName("source_size")]
    public long SourceSize { get; set; }

    [JsonPropertyName("source_sha256")]
    public string SourceSha256 { get; set; } = string.Empty;

    [JsonPropertyName("target_size")]
    public long TargetSize { get; set; }

    [JsonPropertyName("target_sha256")]
    public string TargetSha256 { get; set; } = string.Empty;

    [JsonPropertyName("blob_file")]
    public string BlobFile { get; set; } = string.Empty;

    [JsonPropertyName("blob_size")]
    public long BlobSize { get; set; }

    [JsonPropertyName("blob_sha256")]
    public string BlobSha256 { get; set; } = string.Empty;

    [JsonPropertyName("chunks")]
    public List<ChunkPatchChunk> Chunks { get; set; } = [];

    [JsonIgnore]
    public string Root { get; set; } = string.Empty;

    [JsonIgnore]
    public string SourcePath => PatchShared.NormalizeZipPath(SourceName);

    [JsonIgnore]
    public string BlobPath => $"{Root}{BlobFile}";
}