using System.Globalization;
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
            if (SetProperty(ref field, value && IsSelectable))
                onCheckedChanged();
        }
    }

    public bool IsSelectable => Item.IsSelectable;

    public string TitleId => Item.TitleId;

    public string Region => Item.Region;

    public string Name => Item.Name;

    public string Version => Item.Version;

    public string RequiredUpdate => Item.RequiredUpdateVersion;

    public string PatchType => Item.PatchType;

    public DateTime Date { get; } = ParseDate(item.LastModified);

    public string DateText => Item.LastModified;

    public string PatchTypeBadgeColor => Item.IsDirect ? "#2E7D32" : "#8A5A00";

    public bool HasReleaseLink => !string.IsNullOrWhiteSpace(Item.ReleaseLink);

    private static DateTime ParseDate(string value) => DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) ? date : DateTime.MinValue;
}