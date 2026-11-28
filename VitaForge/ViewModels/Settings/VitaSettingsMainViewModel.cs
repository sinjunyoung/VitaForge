using Common.Localization;
using System.Windows;
using System.Windows.Input;
using VitaForge.Core;
using VitaForge.Core.Services.PSVita;
using VitaForge.Core.UI.Command;
using VitaForge.Core.ViewModels;

namespace VitaForge.ViewModels.Settings;

public class VitaSettingsMainViewModel : ToolTabViewModel
{
    public string? DownloadPath
    {
        get => AppConfig.Instance.VitaDownload.DownloadPath;
        set
        {
            AppConfig.Instance.VitaDownload.DownloadPath = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(DownloadHintVisibility));
        }
    }

    public bool DeleteAfterInstall
    {
        get => AppConfig.Instance.VitaDownload.DeleteAfterInstall;
        set
        {
            AppConfig.Instance.VitaDownload.DeleteAfterInstall = value;
            OnPropertyChanged();
        }
    }

    public string DownloadHint => Loc.S.HintDefaultDownloadFolder(DownloadFolderDefaults.Resolve(null));

    public Visibility DownloadHintVisibility => string.IsNullOrWhiteSpace(DownloadPath) ? Visibility.Visible : Visibility.Collapsed;

    public ICommand BrowseDownloadPathCommand { get; }

    public VitaSettingsMainViewModel()
    {
        Loc.Instance.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(Loc.Instance.Current))
                OnPropertyChanged(nameof(DownloadHint));
        };

        BrowseDownloadPathCommand = new RelayCommand(_ => BrowseDownloadPath());
    }

    private void BrowseDownloadPath()
    {
        var dlg = new Ookii.Dialogs.Wpf.VistaFolderBrowserDialog
        {
            Description = Loc.S.LabelDownloadLocation,
            UseDescriptionForTitle = true
        };

        if (dlg.ShowDialog() == true)
            DownloadPath = dlg.SelectedPath;
    }
}