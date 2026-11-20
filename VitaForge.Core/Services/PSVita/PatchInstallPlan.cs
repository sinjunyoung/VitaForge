using VitaForge.Core.Models.PSVita;

namespace VitaForge.Core.Services.PSVita;

public sealed record PatchInstallPlan(PatchCatalogItem Patch, NpsCatalogItem Game, NpsCatalogItem? Update, IReadOnlyList<NpsCatalogItem> Dlcs)
{
    public long TotalSize => Game.Size + (Update?.Size ?? 0) + Dlcs.Sum(d => d.Size);

    public List<(NpsCatalogItem Item, string? License)> Downloads
    {
        get
        {
            var list = new List<(NpsCatalogItem Item, string? License)> { (Game, Game.ZRif) };

            if (Update != null)
                list.Add((Update, null));

            foreach (var dlc in Dlcs)
                list.Add((dlc, dlc.ZRif));

            return list;
        }
    }
}