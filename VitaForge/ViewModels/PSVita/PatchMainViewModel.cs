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
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using Vita.Core.Models;
using VitaForge.Core.ViewModels.PSVita;

namespace VitaForge.ViewModels.PSVita;

public class PatchMainViewModel : ProgressToolTabViewModel
{
    private static readonly string CatalogPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "PSV_PATCH.json");

    private readonly Stopwatch _totalSw = new();
    private string? _outputPath;
    private string _appliedTitleId = string.Empty;
    private string _appliedName = string.Empty;

    public ObservableCollection<PatchRowViewModel> Rows { get; } = [];

    public bool? AllChecked
    {
        get
        {
            var selectable = VisibleSelectableRows.ToList();

            if (selectable.Count == 0)
                return false;

            int checkedCount = selectable.Count(r => r.IsChecked);

            return checkedCount == 0 ? false : checkedCount == selectable.Count ? true : null;
        }
    }

    public string ResultText => Loc.S.LabelNpsResultSelected(Rows.Count(r => r.IsSelectable), Rows.Count(r => r.IsChecked));

    public bool ModeInstall
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
                NotifyTargetChanged();
        }
    } = true;

    public bool ModeArchive
    {
        get => !ModeInstall;
        set => ModeInstall = !value;
    }

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

    public bool IncludeDlc { get; set { SetProperty(ref field, value); } } = false;

    public string? TargetPath
    {
        get => !ModeInstall ? _outputPath : InstallEmu ? AppConfig.Instance.OutputFolders.VitaPatchEmuRootPath : AppConfig.Instance.OutputFolders.VitaPatchDeviceRootPath;
        set
        {
            if (!ModeInstall)
            {
                _outputPath = value;
                AppConfig.Instance.OutputFolders.VitaPatchOutputPath = value;

                NotifyPathChanged();
            }
            else if (InstallEmu)
                AppConfig.Instance.OutputFolders.VitaPatchEmuRootPath = value;
            else
                AppConfig.Instance.OutputFolders.VitaPatchDeviceRootPath = value;
        }
    }

    public Visibility EmuHintVisibility => ModeInstall && InstallEmu && string.IsNullOrWhiteSpace(TargetPath) ? Visibility.Visible : Visibility.Collapsed;

    public Visibility DeviceHintVisibility => ModeInstall && !InstallEmu && string.IsNullOrWhiteSpace(TargetPath) ? Visibility.Visible : Visibility.Collapsed;

    public Visibility OutputHintVisibility => !ModeInstall && string.IsNullOrWhiteSpace(TargetPath) ? Visibility.Visible : Visibility.Collapsed;

    public string? SearchTitleId { get; set { SetProperty(ref field, value); } }

    public string? SearchName { get; set { SetProperty(ref field, value); } }

    public ICommand SearchCommand { get; }

    public ICommand ClearCheckedCommand { get; }

    public ICommand ToggleAllCheckedCommand { get; }

    public ICommand BrowseTargetCommand { get; }

    private IEnumerable<PatchRowViewModel> VisibleSelectableRows => Rows.Where(r => r.IsSelectable && Matches(r));

    public PatchMainViewModel()
    {
        _outputPath = OutputFolderDefaults.Resolve(AppConfig.Instance.OutputFolders.VitaPatchOutputPath);

        SearchCommand = new RelayCommand(_ => Search());
        ClearCheckedCommand = new RelayCommand(_ => ClearChecked(), _ => Rows.Any(r => r.IsChecked));
        ToggleAllCheckedCommand = new RelayCommand(_ => ToggleAllChecked(), _ => Rows.Any(r => r.IsSelectable));
        BrowseTargetCommand = new RelayCommand(_ => BrowseTarget());
        CancelCommand = new RelayCommand(_ => Cancel());

        AppConfig.Instance.OutputFolders.PropertyChanged += OnOutputFoldersChanged;

        var view = CollectionViewSource.GetDefaultView(Rows);

        view.Filter = item => item is PatchRowViewModel row && Matches(row);
        view.SortDescriptions.Add(new SortDescription(nameof(PatchRowViewModel.Date), ListSortDirection.Descending));
        view.SortDescriptions.Add(new SortDescription(nameof(PatchRowViewModel.Name), ListSortDirection.Ascending));

        LoadCatalog();
    }

    public static void OpenReleaseLink(PatchRowViewModel row)
    {
        if (!Uri.TryCreate(row.Item.ReleaseLink, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
            return;

        Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
    }

    private void OnOutputFoldersChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(OutputFoldersConfig.VitaPatchEmuRootPath) or nameof(OutputFoldersConfig.VitaPatchDeviceRootPath))
            NotifyPathChanged();
    }

    private void NotifyTargetChanged()
    {
        OnPropertyChanged(nameof(ModeArchive));
        OnPropertyChanged(nameof(InstallDevice));

        NotifyPathChanged();
    }

    private void NotifyPathChanged()
    {
        OnPropertyChanged(nameof(TargetPath));
        OnPropertyChanged(nameof(EmuHintVisibility));
        OnPropertyChanged(nameof(DeviceHintVisibility));
        OnPropertyChanged(nameof(OutputHintVisibility));
    }

    private void NotifyCheckedChanged()
    {
        OnPropertyChanged(nameof(ResultText));
        OnPropertyChanged(nameof(AllChecked));
        CommandManager.InvalidateRequerySuggested();
    }

    private void LoadCatalog()
    {
        if (!File.Exists(CatalogPath))
        {
            AppendLog(Loc.S.LogPsvPatchCatalogNotFound(CatalogPath), LogLevel.Error);
            return;
        }

        try
        {
            var items = PatchCatalogReader.Read(CatalogPath);

            foreach (var item in items)
                Rows.Add(new PatchRowViewModel(item, NotifyCheckedChanged));

            AppendLog(Loc.S.LogPsvPatchCatalogLoaded(items.Count, CatalogPath), LogLevel.Info);

            NotifyCheckedChanged();
        }
        catch (Exception ex)
        {
            AppendLog(Loc.S.LogError(ex.Message), LogLevel.Error);
        }
    }

    private bool Matches(PatchRowViewModel row) =>
        (_appliedTitleId.Length == 0 || row.TitleId.Contains(_appliedTitleId, StringComparison.OrdinalIgnoreCase)) &&
        (_appliedName.Length == 0 || row.Name.Contains(_appliedName, StringComparison.OrdinalIgnoreCase));

    private void Search()
    {
        _appliedTitleId = SearchTitleId?.Trim() ?? string.Empty;
        _appliedName = SearchName?.Trim() ?? string.Empty;

        CollectionViewSource.GetDefaultView(Rows).Refresh();

        NotifyCheckedChanged();
    }

    private void ToggleAllChecked()
    {
        bool target = AllChecked != true;

        foreach (var row in VisibleSelectableRows.ToList())
            row.IsChecked = target;
    }

    private void ClearChecked()
    {
        foreach (var row in Rows)
            row.IsChecked = false;
    }

    private void BrowseTarget()
    {
        var dlg = new Ookii.Dialogs.Wpf.VistaFolderBrowserDialog
        {
            Description = !ModeInstall ? Loc.S.HintPatchOutputFolder : InstallEmu ? Loc.S.HintVita3kFolder : Loc.S.HintDeviceRoot,
            UseDescriptionForTitle = true
        };

        if (dlg.ShowDialog() == true)
            TargetPath = dlg.SelectedPath;
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

        if (string.IsNullOrWhiteSpace(TargetPath))
            BrowseTarget();

        if (string.IsNullOrWhiteSpace(TargetPath))
            return;

        if (ModeInstall && !Directory.Exists(TargetPath))
        {
            AppendLog(Loc.S.ErrPsvPatchInstallPathNotFound(TargetPath), LogLevel.Error);
            await SoundPlayer.PlayErrorAsync();
            return;
        }

        var options = new PatchInstallOptions(InstallEmu ? OutputTarget.Emu : OutputTarget.Retail, ModeInstall ? PatchOutputMode.Install : PatchOutputMode.Archive, TargetPath, IncludeDlc);

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

            var succeeded = await PatchInstallService.RunAsync(items, options, ResolvePatchTarget, AppendLog, BuildProgressReporter(), ct);

            AppendLog(Loc.S.LogVitaAllDoneTitles(succeeded.Count, _totalSw.Elapsed.ToString("mm\\:ss")), succeeded.Count > 0 ? LogLevel.Ok : LogLevel.Error);

            foreach (var row in Rows.Where(r => succeeded.Contains(r.Item)))
                row.IsChecked = false;

            if (succeeded.Count > 0 && options.Mode == PatchOutputMode.Archive)
                options.TargetPath.OpenFolder();
            else if (succeeded.Count > 0 && options.Target == OutputTarget.Emu)
                Vita3kLauncher.Launch(options.TargetPath, Vita3kLauncher.SingleTitleId(succeeded.Select(i => i.TitleId)), AppendLog);

            if (succeeded.Count > 0)
                await SoundPlayer.PlayCompleteAsync();
            else
                await SoundPlayer.PlayErrorAsync();
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

    private IReadOnlyList<string?> ResolvePatchTarget(PatchTargetRequest request)
    {
        var selections = Application.Current.Dispatcher.Invoke(() => PatchTargetPickerWindow.Pick(Application.Current.MainWindow, request));

        PatchTargetLogger.Log(request, selections, AppendLog);

        return selections;
    }
}