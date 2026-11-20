using Common.Localization;
using Common.Models;
using Common.Services;
using VitaForge.Core;
using VitaForge.Core.Models.PSVita;
using VitaForge.Core.Services;
using VitaForge.Core.Services.PSVita;
using VitaForge.Core.UI.Command;
using VitaForge.Views;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using Vita.Core.Models;
using VitaForge.Core.ViewModels.PSVita;

namespace VitaForge.ViewModels.PSVita;

public class PatchMainViewModel : ProgressToolTabViewModel
{
    private static readonly string DefaultCsvPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "PSV_PATCH.csv");

    private readonly Stopwatch _totalSw = new();
    private string? _emuRoot;
    private string? _deviceRoot;

    public ObservableCollection<PatchRowViewModel> Rows { get; } = [];

    public string CsvPath { get; private set { SetProperty(ref field, value); } } = string.Empty;

    public bool? AllChecked
    {
        get
        {
            if (Rows.Count == 0)
                return false;

            int checkedCount = Rows.Count(r => r.IsChecked);

            return checkedCount == 0 ? false : checkedCount == Rows.Count ? true : null;
        }
    }

    public string ResultText => Loc.S.LabelNpsResultSelected(Rows.Count, Rows.Count(r => r.IsChecked));

    public bool InstallEmu
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
                NotifyTargetChanged();
        }
    } = true;

    public bool InstallDevice
    {
        get => !InstallEmu;
        set => InstallEmu = !value;
    }

    public bool IncludeDlc { get; set { SetProperty(ref field, value); } } = true;

    public string? InstallPath
    {
        get => InstallEmu ? _emuRoot : _deviceRoot;
        set
        {
            if (InstallEmu)
            {
                _emuRoot = value;
                AppConfig.Instance.OutputFolders.VitaPatchEmuRootPath = value;
            }
            else
            {
                _deviceRoot = value;
                AppConfig.Instance.OutputFolders.VitaPatchDeviceRootPath = value;
            }

            OnPropertyChanged();
            OnPropertyChanged(nameof(EmuHintVisibility));
            OnPropertyChanged(nameof(DeviceHintVisibility));
        }
    }

    public Visibility EmuHintVisibility => InstallEmu && string.IsNullOrWhiteSpace(InstallPath) ? Visibility.Visible : Visibility.Collapsed;

    public Visibility DeviceHintVisibility => !InstallEmu && string.IsNullOrWhiteSpace(InstallPath) ? Visibility.Visible : Visibility.Collapsed;

    public ICommand BrowseCsvCommand { get; }

    public ICommand ClearCheckedCommand { get; }

    public ICommand ToggleAllCheckedCommand { get; }

    public ICommand BrowseInstallCommand { get; }

    public PatchMainViewModel()
    {
        _emuRoot = AppConfig.Instance.OutputFolders.VitaPatchEmuRootPath;
        _deviceRoot = AppConfig.Instance.OutputFolders.VitaPatchDeviceRootPath;

        BrowseCsvCommand = new RelayCommand(_ => BrowseCsv());
        ClearCheckedCommand = new RelayCommand(_ => ClearChecked(), _ => Rows.Any(r => r.IsChecked));
        ToggleAllCheckedCommand = new RelayCommand(_ => ToggleAllChecked(), _ => Rows.Count > 0);
        BrowseInstallCommand = new RelayCommand(_ => BrowseInstall());
        CancelCommand = new RelayCommand(_ => Cancel());

        if (File.Exists(DefaultCsvPath))
            LoadCsv(DefaultCsvPath);
    }

    public static void OpenReleaseLink(PatchRowViewModel row)
    {
        if (!Uri.TryCreate(row.Item.ReleaseLink, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
            return;

        Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
    }

    private void NotifyTargetChanged()
    {
        OnPropertyChanged(nameof(InstallDevice));
        OnPropertyChanged(nameof(InstallPath));
        OnPropertyChanged(nameof(EmuHintVisibility));
        OnPropertyChanged(nameof(DeviceHintVisibility));
    }

    private void NotifyCheckedChanged()
    {
        OnPropertyChanged(nameof(ResultText));
        OnPropertyChanged(nameof(AllChecked));
        CommandManager.InvalidateRequerySuggested();
    }

    private void BrowseCsv()
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "CSV (*.csv)|*.csv",
            InitialDirectory = AppDomain.CurrentDomain.BaseDirectory
        };

        if (dlg.ShowDialog() == true)
            LoadCsv(dlg.FileName);
    }

    private void LoadCsv(string path)
    {
        try
        {
            var items = PatchCsvReader.Read(path);

            Rows.Clear();

            foreach (var item in items)
                Rows.Add(new PatchRowViewModel(item, NotifyCheckedChanged));

            CsvPath = path;

            AppendLog(Loc.S.LogPsvPatchCsvLoaded(items.Count, path), LogLevel.Info);

            NotifyCheckedChanged();
        }
        catch (Exception ex)
        {
            AppendLog(Loc.S.LogError(ex.Message), LogLevel.Error);
        }
    }

    private void ToggleAllChecked()
    {
        bool target = AllChecked != true;

        foreach (var row in Rows)
            row.IsChecked = target;
    }

    private void ClearChecked()
    {
        foreach (var row in Rows)
            row.IsChecked = false;
    }

    private void BrowseInstall()
    {
        var dlg = new Ookii.Dialogs.Wpf.VistaFolderBrowserDialog
        {
            Description = InstallEmu ? Loc.S.HintVita3kFolder : Loc.S.HintDeviceRoot,
            UseDescriptionForTitle = true
        };

        if (dlg.ShowDialog() == true)
            InstallPath = dlg.SelectedPath;
    }

    public async Task StartAsync()
    {
        var items = Rows.Where(r => r.IsChecked).Select(r => r.Item).ToList();

        if (items.Count == 0)
        {
            AppendLog(Loc.S.ErrNpsSelectItems, LogLevel.Error);
            await SoundPlayer.PlayErrorAsync();
            return;
        }

        if (string.IsNullOrWhiteSpace(InstallPath))
            BrowseInstall();

        if (string.IsNullOrWhiteSpace(InstallPath))
            return;

        if (!Directory.Exists(InstallPath))
        {
            AppendLog(Loc.S.ErrPsvPatchInstallPathNotFound(InstallPath), LogLevel.Error);
            await SoundPlayer.PlayErrorAsync();
            return;
        }

        var options = new PatchInstallOptions(InstallEmu ? OutputTarget.Emu : OutputTarget.Retail, InstallPath, IncludeDlc);

        _totalSw.Restart();

        using (BeginWork())
        {
            try
            {
                CancellationTokenSource = new CancellationTokenSource();

                await ExecuteAsync(items, options, CancellationTokenSource.Token);
            }
            finally
            {
                CleanupTask();
            }
        }
    }

    private async Task ExecuteAsync(List<PatchCatalogItem> items, PatchInstallOptions options, CancellationToken ct)
    {
        try
        {
            await NpsCatalogService.Instance.LoadAsync();

            int installed = await PatchInstallService.RunAsync(items, options, ResolvePatchTarget, AppendLog, BuildProgressReporter(), ct);

            AppendLog(Loc.S.LogVitaAllDoneTitles(installed, _totalSw.Elapsed.ToString("mm\\:ss")), LogLevel.Ok);
            ClearChecked();

            await SoundPlayer.PlayCompleteAsync();
        }
        catch (OperationCanceledException)
        {
            AppendLog(Loc.S.LogJobCancelled, LogLevel.Error);
            await SoundPlayer.PlayCancelAsync();
            MainViewModel.SetTaskbarProgress(0);
        }
        catch (Exception ex)
        {
            AppendLog(Loc.S.LogError(ex.Message), LogLevel.Error);
            await SoundPlayer.PlayErrorAsync();
            MainViewModel.SetTaskbarProgress(100, System.Windows.Shell.TaskbarItemProgressState.Error);
        }
    }

    private string? ResolvePatchTarget(PatchTargetRequest request)
    {
        string? selected = Application.Current.Dispatcher.Invoke(() => PatchTargetPickerWindow.Pick(Application.Current.MainWindow, request));

        if (selected == null)
            AppendLog(Loc.S.LogVitaPatchTargetSkipped(request.PatchFileName), LogLevel.Info);
        else
            AppendLog(Loc.S.LogVitaPatchTargetSelected(request.PatchFileName, selected), LogLevel.Ok);

        return selected;
    }
}