using VitaForge.Core.Models.PSVita;

namespace VitaForge.Core.ViewModels.PSVita;

public class PatchRowViewModel(PatchCatalogItem item, Action onCheckedChanged) : ViewModelBase
{
    public PatchCatalogItem Item { get; } = item;

    public bool IsChecked
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
                onCheckedChanged();
        }
    }

    public string TitleId => Item.TitleId;

    public string Region => Item.Region;

    public string Name => Item.Name;

    public string Version => Item.Version;

    public string RequiredUpdate => Item.RequiredUpdateVersion;

    public bool HasReleaseLink => !string.IsNullOrWhiteSpace(Item.ReleaseLink);
}