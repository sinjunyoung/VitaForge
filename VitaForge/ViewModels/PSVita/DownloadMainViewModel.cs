using Common.Localization;
using Common.Models;
using Common.Services;
using VitaForge.Core;
using VitaForge.Core.Models.PSVita;
using VitaForge.Core.Services;
using VitaForge.Core.Services.PSVita;
using VitaForge.Core.UI.Command;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows.Input;
using Vita.Core.Models;

namespace VitaForge.ViewModels.PSVita;

public class DownloadMainViewModel : ProgressToolTabViewModel
{
    private readonly Stopwatch _totalSw = new();
    private readonly HashSet<NpsCatalogItem> _checkedItems = [];
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
            OnPropertyChanged(nameof(OutputHintVisibility));

            AppConfig.Instance.OutputFolders.VitaDownloadOutputPath = value;
        }
    }

    public System.Windows.Visibility OutputHintVisibility => string.IsNullOrWhiteSpace(OutputPath) ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;

    public bool OutputZip { get; set { SetProperty(ref field, value); } } = true;

    public bool OutputFolder { get; set { SetProperty(ref field, value); } }

    public ICommand SearchCommand { get; }

    public ICommand ClearCheckedCommand { get; }

    public ICommand ToggleAllCheckedCommand { get; }

    public ICommand BrowseOutputCommand { get; }

    public DownloadMainViewModel()
    {
        OutputPath = string.IsNullOrWhiteSpace(AppConfig.Instance.OutputFolders.VitaDownloadOutputPath) ? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "output") : AppConfig.Instance.OutputFolders.VitaDownloadOutputPath;

        SearchCommand = new RelayCommand(_ => Search(), _ => NpsCatalogService.Instance.IsLoaded);
        ClearCheckedCommand = new RelayCommand(_ => ClearChecked(), _ => _checkedItems.Count > 0);
        ToggleAllCheckedCommand = new RelayCommand(_ => ToggleAllChecked(), _ => Rows.Count > 0);
        BrowseOutputCommand = new RelayCommand(_ => BrowseOutput());
        CancelCommand = new RelayCommand(_ => Cancel());

        _ = LoadCatalogAsync();
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

        var results = NpsCatalogService.Instance.Search(SearchTitleId, SelectedRegion?.Value, SearchName);

        _resultCount = results.Count;

        Rows = [.. results.Select(g => CreateRow(g.Game, g, isChild: false))];

        OnPropertyChanged(nameof(ResultText));
        OnPropertyChanged(nameof(AllChecked));
    }

    private NpsRowViewModel CreateRow(NpsCatalogItem item, NpsCatalogGame? game, bool isChild) => new(item, game, isChild, _checkedItems.Contains, SetChecked);

    private void SetChecked(NpsCatalogItem item, bool isChecked)
    {
        if (isChecked)
            _checkedItems.Add(item);
        else
            _checkedItems.Remove(item);

        OnPropertyChanged(nameof(ResultText));
        OnPropertyChanged(nameof(AllChecked));

        CommandManager.InvalidateRequerySuggested();
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

        OnPropertyChanged(nameof(ResultText));
        OnPropertyChanged(nameof(AllChecked));

        CommandManager.InvalidateRequerySuggested();
    }

    private void ClearChecked()
    {
        _checkedItems.Clear();

        foreach (var row in Rows)
            row.RefreshChecked();

        OnPropertyChanged(nameof(ResultText));
        OnPropertyChanged(nameof(AllChecked));
    }

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
            Description = Loc.S.ToolTipBrowseOutput,
            UseDescriptionForTitle = true
        };

        if (dlg.ShowDialog() == true)
            OutputPath = dlg.SelectedPath;
    }

    public async Task StartAsync()
    {
        if (string.IsNullOrWhiteSpace(OutputPath))
        {
            AppendLog(Loc.S.ErrSelectOutputFolder, LogLevel.Error);
            await SoundPlayer.PlayErrorAsync();
            return;
        }

        if (_checkedItems.Count == 0)
        {
            AppendLog(Loc.S.ErrNpsSelectItems, LogLevel.Error);
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
            var output = OutputZip ? PkgConvertOutput.Zip : PkgConvertOutput.Folder;

            AppendLog(Loc.S.LogVitaConvertingNoNpDrm(OutputZip ? "zip" : Loc.S.LabelFolder), LogLevel.Highlight);

            var outputs = await NpsDownloadConvertService.RunAsync([.. _checkedItems], OutputPath!, output, AppendLog, BuildProgressReporter(), ct);

            AppendLog(Loc.S.LogVitaAllDoneTitles(outputs.Count, _totalSw.Elapsed.ToString("mm\\:ss")), LogLevel.Ok);

            ClearChecked();

            await SoundPlayer.PlayCompleteAsync();
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
}