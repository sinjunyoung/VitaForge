namespace VitaForge.Core.Models.PSVita;

public sealed class NpsCatalogGame(NpsCatalogItem game)
{
    public NpsCatalogItem Game { get; } = game;

    public List<NpsCatalogItem> Updates { get; } = [];

    public List<NpsCatalogItem> Dlcs { get; } = [];

    public bool HasChildren => Updates.Count > 0 || Dlcs.Count > 0;
}
