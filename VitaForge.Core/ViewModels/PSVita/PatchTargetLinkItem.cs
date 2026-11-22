using Common.Localization;

namespace VitaForge.Core.ViewModels.PSVita;

public sealed class PatchTargetLinkItem(string patchFileName) : ViewModelBase
{
    public string PatchFileName { get; } = patchFileName;

    public string? TargetPath
    {
        get;
        set
        {
            if (!SetProperty(ref field, value))
                return;

            OnPropertyChanged(nameof(IsLinked));
            OnPropertyChanged(nameof(TargetText));
            OnPropertyChanged(nameof(StatusGlyph));
        }
    }

    public bool IsLinked => TargetPath != null;

    public string TargetText => TargetPath ?? Loc.S.LabelPatchTargetNone;

    public string StatusGlyph => IsLinked ? "✔" : "○";
}