namespace VitaForge.Core.Models.PSVita;

public sealed class PatchCatalogItem
{
    public string TitleId { get; init; } = string.Empty;

    public string Region { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string Version { get; init; } = string.Empty;

    public string RequiredUpdateVersion { get; init; } = string.Empty;

    public string User { get; init; } = string.Empty;

    public string ReleaseLink { get; init; } = string.Empty;

    public string DirectLink { get; init; } = string.Empty;

    public string LastModified { get; init; } = string.Empty;
}