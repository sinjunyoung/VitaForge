using Common.Localization;

namespace VitaForge.ViewModels.PSVita;

public sealed class NpsRegionOption(string? value)
{
    public string? Value { get; } = value;

    public string Label => Value ?? Loc.S.ButtonFilterAll;
}
