using Common.Services;
using VitaForge.Core.Models.PSVita;
using VitaForge.Core.ViewModels;
using System.Windows.Media;
using Vita.Core.Models;

namespace VitaForge.ViewModels.PSVita;

public class NpsRowViewModel(NpsCatalogItem item, NpsCatalogGame? game, bool isChild, Func<NpsCatalogItem, bool> isChecked, Action<NpsCatalogItem, bool> setChecked) : ViewModelBase
{
    public NpsCatalogItem Item { get; } = item;

    public NpsCatalogGame? Game { get; } = game;

    public bool IsChild { get; } = isChild;

    public bool HasChildren => !IsChild && Game?.HasChildren == true;

    public bool IsExpanded { get; set { if (SetProperty(ref field, value)) OnPropertyChanged(nameof(ExpandGlyph)); } }

    public string ExpandGlyph => !HasChildren ? string.Empty : IsExpanded ? "▼" : "▶";

    public bool IsChecked
    {
        get => isChecked(Item);
        set
        {
            setChecked(Item, value);
            OnPropertyChanged();
        }
    }

    public ContentCategory Category => Item.Kind switch
    {
        NpsEntryKind.Game => ContentCategory.App,
        NpsEntryKind.Update => ContentCategory.Patch,
        _ => ContentCategory.Addcont
    };

    public SolidColorBrush CategoryBadgeColor => Category switch
    {
        ContentCategory.App => new SolidColorBrush(Color.FromRgb(0x3B, 0x82, 0xF6)),
        ContentCategory.Patch => new SolidColorBrush(Color.FromRgb(0x10, 0xB9, 0x81)),
        _ => new SolidColorBrush(Color.FromRgb(0x8B, 0x5C, 0xF6))
    };

    public string TitleId => Item.TitleId;

    public string Region => Item.Region;

    public string Name => Item.Name;

    public string KoreanName => Item.KoreanName;

    public bool HasKoreanName => !string.IsNullOrEmpty(Item.KoreanName);

    public string Version => Item.Version;

    public string Size => Item.Size > 0 ? FileSize.FormatSize(Item.Size) : string.Empty;

    public void RefreshChecked() => OnPropertyChanged(nameof(IsChecked));
}
