using Common.Localization;
using Common.Models;
using Common.Services;
using VitaForge.Core;
using VitaForge.Core.Models.PSVita;
using VitaForge.Core.Services;
using VitaForge.Core.Services.PSVita;
using VitaForge.Core.UI.Command;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows.Input;
using Vita.Core.Models;
using VitaForge.Core.ViewModels.PSVita;

namespace VitaForge.ViewModels.PSVita;

public class DownloadMainViewModel : ProgressToolTabViewModel
{
    private readonly Stopwatch _totalSw = new();
    private readonly HashSet<NpsCatalogItem> _checkedItems = [];
    private List<NpsCatalogGame> _searchResults = [];
    private int _resultCount;

    public ObservableCollection<NpsRowViewModel> Rows { get; private set { SetProperty(ref field, value); } } = [];

    public IReadOnlyList<NpsRegionOption> RegionOptions { get; private set { SetProperty(ref field, value); } } = [new NpsRegionOption(null)];

    public NpsRegionOption? SelectedRegion
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
                Search();
        }
    }

    public string? SearchTitleId { get; set { SetProperty(ref field, value); } }

    public string? SearchName { get; set { SetProperty(ref field, value); } }

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

    public string ResultText => Loc.S.LabelNpsResultSelected(_resultCount, _checkedItems.Count);

    public string? OutputPath
    {
        get;
        set
        {
            SetProperty(ref field, value);

            AppConfig.Instance.OutputFolders.VitaDownloadOutputPath = value;

            NotifyTargetChanged();
        }
    }

    public string? TargetPath
    {
        get => ModeInstall ? InstallEmu ? AppConfig.Instance.OutputFolders.VitaPatchEmuRootPath : AppConfig.Instance.OutputFolders.VitaPatchDeviceRootPath : OutputPath;
        set
        {
            if (!ModeInstall)
                OutputPath = value;
            else if (InstallEmu)
                AppConfig.Instance.OutputFolders.VitaPatchEmuRootPath = value;
            else
                AppConfig.Instance.OutputFolders.VitaPatchDeviceRootPath = value;
        }
    }

    public bool ModeInstall
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
                NotifyTargetChanged();
        }
    }

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

    public bool IsInstall => ModeInstall;

    public System.Windows.Visibility ArchiveOptionsVisibility => ModeInstall ? System.Windows.Visibility.Collapsed : System.Windows.Visibility.Visible;

    public System.Windows.Visibility InstallOptionsVisibility => ModeInstall ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;

    public System.Windows.Visibility OutputHintVisibility => !ModeInstall && string.IsNullOrWhiteSpace(TargetPath) ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;

    public System.Windows.Visibility EmuHintVisibility => ModeInstall && InstallEmu && string.IsNullOrWhiteSpace(TargetPath) ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;

    public System.Windows.Visibility DeviceHintVisibility => ModeInstall && !InstallEmu && string.IsNullOrWhiteSpace(TargetPath) ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;

    public bool OutputZip { get; set { SetProperty(ref field, value); } } = true;

    public bool OutputFolder { get; set { SetProperty(ref field, value); } }

    public bool OutputPkg { get; set { SetProperty(ref field, value); } }

    public bool ShowCheckedOnly
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
                RebuildRows();
        }
    }

    public bool CanShowCheckedOnly => ShowCheckedOnly || _checkedItems.Count > 0;

    public ICommand SearchCommand { get; }

    public ICommand ClearCheckedCommand { get; }

    public ICommand ToggleAllCheckedCommand { get; }

    public ICommand BrowseOutputCommand { get; }

    public DownloadMainViewModel()
    {
        OutputPath = string.IsNullOrWhiteSpace(AppConfig.Instance.OutputFolders.VitaDownloadOutputPath) ? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "output") : AppConfig.Instance.OutputFolders.VitaDownloadOutputPath;

        AppConfig.Instance.OutputFolders.PropertyChanged += OnOutputFoldersChanged;

        SearchCommand = new RelayCommand(_ => Search(), _ => NpsCatalogService.Instance.IsLoaded);
        ClearCheckedCommand = new RelayCommand(_ => ClearChecked(), _ => _checkedItems.Count > 0);
        ToggleAllCheckedCommand = new RelayCommand(_ => ToggleAllChecked(), _ => Rows.Count > 0);
        BrowseOutputCommand = new RelayCommand(_ => BrowseOutput());
        CancelCommand = new RelayCommand(_ => Cancel());

        _ = LoadCatalogAsync();
    }

    private void OnOutputFoldersChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(OutputFoldersConfig.VitaPatchEmuRootPath) or nameof(OutputFoldersConfig.VitaPatchDeviceRootPath))
            NotifyTargetChanged();
    }

    private void NotifyTargetChanged()
    {
        OnPropertyChanged(nameof(ModeArchive));
        OnPropertyChanged(nameof(InstallDevice));
        OnPropertyChanged(nameof(TargetPath));
        OnPropertyChanged(nameof(IsInstall));
        OnPropertyChanged(nameof(ArchiveOptionsVisibility));
        OnPropertyChanged(nameof(InstallOptionsVisibility));
        OnPropertyChanged(nameof(OutputHintVisibility));
        OnPropertyChanged(nameof(EmuHintVisibility));
        OnPropertyChanged(nameof(DeviceHintVisibility));
    }

    private async Task LoadCatalogAsync()
    {
        try
        {
            await NpsCatalogService.Instance.LoadAsync();

            RegionOptions = [new NpsRegionOption(null), .. NpsCatalogService.Instance.Regions.Select(r => new NpsRegionOption(r))];
            SelectedRegion = RegionOptions[0];

            CommandManager.InvalidateRequerySuggested();
        }
        catch (Exception ex)
        {
            AppendLog(Loc.S.LogError(ex.Message), LogLevel.Error);
        }
    }

    private void Search()
    {
        if (!NpsCatalogService.Instance.IsLoaded)
            return;

        _searchResults = NpsCatalogService.Instance.Search(SearchTitleId, SelectedRegion?.Value, SearchName);
        _resultCount = _searchResults.Count;

        if (ShowCheckedOnly)
            ShowCheckedOnly = false;
        else
            RebuildRows();
    }

    private void RebuildRows()
    {
        Rows = ShowCheckedOnly ? [.. BuildCheckedRows()] : [.. _searchResults.Select(g => CreateRow(g.Game, g, isChild: false))];

        NotifyCheckedChanged();
    }

    private IEnumerable<NpsRowViewModel> BuildCheckedRows() => _checkedItems
        .OrderBy(i => i.TitleId, StringComparer.OrdinalIgnoreCase)
        .ThenBy(i => i.Kind)
        .Select(i => CreateRow(i, null, isChild: i.Kind != NpsEntryKind.Game));

    private void NotifyCheckedChanged()
    {
        OnPropertyChanged(nameof(ResultText));
        OnPropertyChanged(nameof(AllChecked));
        OnPropertyChanged(nameof(CanShowCheckedOnly));

        CommandManager.InvalidateRequerySuggested();
    }

    private NpsRowViewModel CreateRow(NpsCatalogItem item, NpsCatalogGame? game, bool isChild) => new(item, game, isChild, _checkedItems.Contains, SetChecked);

    private void SetChecked(NpsCatalogItem item, bool isChecked)
    {
        if (isChecked)
            _checkedItems.Add(item);
        else
            _checkedItems.Remove(item);

        NotifyCheckedChanged();
    }

    private void ToggleAllChecked()
    {
        bool target = AllChecked != true;

        foreach (var row in Rows)
        {
            if (target)
                _checkedItems.Add(row.Item);
            else
                _checkedItems.Remove(row.Item);

            row.RefreshChecked();
        }

        NotifyCheckedChanged();
    }

    private void ClearChecked()
    {
        _checkedItems.Clear();

        if (ShowCheckedOnly)
        {
            ShowCheckedOnly = false;
            return;
        }

        foreach (var row in Rows)
            row.RefreshChecked();

        NotifyCheckedChanged();
    }

    public bool SortRows(string column, ListSortDirection direction)
    {
        if (Rows.Count == 0)
            return false;

        int sign = direction == ListSortDirection.Ascending ? 1 : -1;
        var segments = new List<List<NpsRowViewModel>>();

        foreach (var row in Rows)
        {
            if (row.IsChild && !ShowCheckedOnly && segments.Count > 0)
                segments[^1].Add(row);
            else
                segments.Add([row]);
        }

        var sorted = segments.OrderBy(s => GetSortKey(s[0], column), Comparer<object?>.Create((a, b) => sign * CompareSortKeys(a, b)));

        Rows = [.. sorted.SelectMany(s => s)];

        return true;
    }

    private static object? GetSortKey(NpsRowViewModel row, string column) => column switch
    {
        "Category" => (int)row.Item.Kind,
        "TitleId" => row.TitleId,
        "Region" => row.Region,
        "Name" => row.HasKoreanName ? row.KoreanName : row.Name,
        "Version" => row.Version,
        "Size" => row.Item.Size,
        _ => null
    };

    private static int CompareSortKeys(object? a, object? b) => a is string sa && b is string sb ? string.Compare(sa, sb, StringComparison.CurrentCultureIgnoreCase) : Comparer<object?>.Default.Compare(a, b);

    public void ToggleExpand(NpsRowViewModel row)
    {
        if (row.IsChild || !row.HasChildren || row.Game == null)
            return;

        int index = Rows.IndexOf(row);

        if (index < 0)
            return;

        if (row.IsExpanded)
        {
            while (index + 1 < Rows.Count && Rows[index + 1].IsChild)
                Rows.RemoveAt(index + 1);

            row.IsExpanded = false;

            OnPropertyChanged(nameof(AllChecked));

            return;
        }

        int insertAt = index + 1;

        foreach (var child in row.Game.Updates.Concat(row.Game.Dlcs))
            Rows.Insert(insertAt++, CreateRow(child, null, isChild: true));

        row.IsExpanded = true;

        OnPropertyChanged(nameof(AllChecked));
    }

    private void BrowseOutput()
    {
        var dlg = new Ookii.Dialogs.Wpf.VistaFolderBrowserDialog
        {
            Description = !ModeInstall ? Loc.S.ToolTipBrowseOutput : InstallEmu ? Loc.S.HintVita3kFolder : Loc.S.HintDeviceRoot,
            UseDescriptionForTitle = true
        };

        if (dlg.ShowDialog() == true)
            TargetPath = dlg.SelectedPath;
    }

    public async Task StartAsync()
    {
        if (_checkedItems.Count == 0)
        {
            AppendLog(Loc.S.ErrNpsSelectItems, LogLevel.Error);
            await SoundPlayer.PlayErrorAsync();
            return;
        }

        if (IsInstall && string.IsNullOrWhiteSpace(TargetPath))
            BrowseOutput();

        if (string.IsNullOrWhiteSpace(TargetPath))
        {
            AppendLog(Loc.S.ErrSelectOutputFolder, LogLevel.Error);
            await SoundPlayer.PlayErrorAsync();
            return;
        }

        if (IsInstall && !Directory.Exists(TargetPath))
        {
            AppendLog(Loc.S.ErrPsvPatchInstallPathNotFound(TargetPath), LogLevel.Error);
            await SoundPlayer.PlayErrorAsync();
            return;
        }

        _totalSw.Restart();

        using (BeginWork())
        {
            try
            {
                CancellationTokenSource = new CancellationTokenSource();

                await ExecuteAsync(CancellationTokenSource.Token);
            }
            finally
            {
                CleanupTask();
            }
        }
    }

    private async Task ExecuteAsync(CancellationToken ct)
    {
        try
        {
            int succeeded = IsInstall ? await InstallAsync(ct) : await ExportAsync(ct);

            AppendLog(Loc.S.LogVitaAllDoneTitles(succeeded, _totalSw.Elapsed.ToString("mm\\:ss")), LogLevel.Ok);

            ClearChecked();

            if (succeeded > 0 && ModeInstall && InstallEmu)
                Vita3kLauncher.Launch(TargetPath!, AppendLog);

            await SoundPlayer.PlayCompleteAsync();

            if (!IsInstall)
                OutputPath?.OpenFolder();
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

    private async Task<int> InstallAsync(CancellationToken ct)
    {
        var target = InstallEmu ? OutputTarget.Emu : OutputTarget.Retail;

        AppendLog(Loc.S.StatusDownloading, LogLevel.Highlight);

        return await NpsDownloadInstallService.RunAsync([.. _checkedItems], target, TargetPath!, AppendLog, BuildProgressReporter(), ct);
    }

    private async Task<int> ExportAsync(CancellationToken ct)
    {
        if (OutputPkg)
        {
            AppendLog(Loc.S.StatusDownloading, LogLevel.Highlight);

            return (await NpsPkgDownloadService.RunAsync([.. _checkedItems], OutputPath!, AppendLog, BuildProgressReporter(), ct)).Count;
        }

        var output = OutputZip ? PkgConvertOutput.Zip : PkgConvertOutput.Folder;

        AppendLog(Loc.S.LogVitaConvertingNoNpDrm(OutputZip ? "zip" : Loc.S.LabelFolder), LogLevel.Highlight);

        return (await NpsDownloadConvertService.RunAsync([.. _checkedItems], OutputPath!, output, OutputTarget.Retail, AppendLog, BuildProgressReporter(), ct)).Count;
    }
}