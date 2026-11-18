using VitaForge.Core.ViewModels;

namespace VitaForge.ViewModels.Settings;

public class SettingsMainViewModel : MultiToolTabViewModel
{
    public GeneralSettingsMainViewModel General { get; } = new();

    public SettingsMainViewModel()
    {
        Tools.Add(General);

        InitializeMultiTools();
    }
}
