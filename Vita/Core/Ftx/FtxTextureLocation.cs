using System.Text.Json.Serialization;

namespace Vita.Core.Ftx;

internal sealed class FtxTextureLocation
{
    [JsonPropertyName("cpk")]
    public string Cpk { get; set; } = string.Empty;

    [JsonPropertyName("ftx_path")]
    public string FtxPath { get; set; } = string.Empty;

    [JsonPropertyName("block_ord")]
    public int BlockOrdinal { get; set; }

    [JsonPropertyName("w")]
    public int Width { get; set; }

    [JsonPropertyName("h")]
    public int Height { get; set; }
}