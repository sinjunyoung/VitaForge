using VitaForge.Core.ViewModels;

namespace VitaForge.ViewModels.Settings;

public class SettingsMainViewModel : MultiToolTabViewModel
{
    public GeneralSettingsMainViewModel General { get; } = new();

    public VitaSettingsMainViewModel Vita { get; } = new();

    public SettingsMainViewModel()
    {
        Tools.Add(General);
        Tools.Add(Vita);

        InitializeMultiTools();
    }
}