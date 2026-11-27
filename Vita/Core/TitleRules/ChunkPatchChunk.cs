using System.Text.Json.Serialization;

namespace Vita.Core.TitleRules;

internal sealed class ChunkPatchChunk
{
    [JsonPropertyName("offset")]
    public long Offset { get; set; }

    [JsonPropertyName("length")]
    public int Length { get; set; }

    [JsonPropertyName("blob_offset")]
    public long BlobOffset { get; set; }
}