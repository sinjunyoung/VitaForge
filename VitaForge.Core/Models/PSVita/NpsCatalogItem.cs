namespace VitaForge.Core.Models.PSVita;

public sealed class NpsCatalogItem
{
    public NpsEntryKind Kind { get; init; }

    public string TitleId { get; init; } = string.Empty;

    public string Region { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string OriginalName { get; init; } = string.Empty;

    public IReadOnlyList<string> KoreanNames { get; set; } = [];

    public string Url { get; init; } = string.Empty;

    public string? ZRif { get; init; }

    public string ContentId { get; init; } = string.Empty;

    public string Version { get; init; } = string.Empty;

    public long Size { get; init; }

    public string Sha256 { get; init; } = string.Empty;

    public string KoreanName => KoreanNames.Count > 0 ? KoreanNames[0] : string.Empty;

    public bool HasKey => !string.IsNullOrWhiteSpace(ZRif);
}
