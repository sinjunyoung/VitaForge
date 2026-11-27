using System.Text.Json.Serialization;

namespace Vita.Core.Ftx;

internal sealed class FtxTextureInfo
{
    [JsonPropertyName("locations")]
    public List<FtxTextureLocation> Locations { get; set; } = [];
}