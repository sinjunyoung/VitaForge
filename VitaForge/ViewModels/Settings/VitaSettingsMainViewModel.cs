using Common.Localization;
using System.IO;
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

    public static string DownloadHint => Loc.S.HintDefaultDownloadFolder(DownloadFolderDefaults.Resolve(null));

    public Visibility DownloadHintVisibility => string.IsNullOrWhiteSpace(DownloadPath) ? Visibility.Visible : Visibility.Collapsed;

    public ICommand BrowseDownloadPathCommand { get; }
    
    public ICommand ClearCacheCommand { get; }

    public VitaSettingsMainViewModel()
    {
        Loc.Instance.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(Loc.Instance.Current))
                OnPropertyChanged(nameof(DownloadHint));
        };

        BrowseDownloadPathCommand = new RelayCommand(_ => BrowseDownloadPath());
        ClearCacheCommand = new RelayCommand(_ => ClearCache());
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

    private void ClearCache()
    {
        var path = DownloadFolderDefaults.Resolve(DownloadPath);
        if (!Directory.Exists(path)) return;

        if (MessageBox.Show($"Are you sure you want to delete all files and subfolders in the download folder?\n\n{path}", "Clear Cache",
            MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;

        try
        {
            foreach (var file in Directory.GetFiles(path)) File.Delete(file);
            foreach (var dir in Directory.GetDirectories(path)) Directory.Delete(dir, true);
            MessageBox.Show("Cache cleared successfully.", "VitaForge", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to clear cache: {ex.Message}", "VitaForge", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}