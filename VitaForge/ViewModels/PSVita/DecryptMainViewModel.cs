using VitaForge.Core.Services.PSVita;
using Common.Localization;
using Common.Models;
using Common.Services;
using VitaForge.Core;
using VitaForge.Core.Services;
using VitaForge.Core.UI.Command;
using VitaForge.Core.ViewModels.PSVita;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using Vita.Core.Models;
using Vita.Core.Services;

namespace VitaForge.ViewModels.PSVita;

public class DecryptMainViewModel : ProgressToolTabViewModel
{
    private readonly Stopwatch _totalSw = new();
    private readonly SourceRowCollection _sources;

    public ObservableCollection<SourceRowViewModel> SourceRows => _sources.Rows;

    public string? OutputPath
    {
        get;
        set
        {
            SetProperty(ref field, value);
            OnPropertyChanged(nameof(OutputHintVisibility));

            AppConfig.Instance.OutputFolders.VitaDecryptOutputPath = value;
        }
    }

    public Visibility OutputHintVisibility => string.IsNullOrWhiteSpace(OutputPath) ? Visibility.Visible : Visibility.Collapsed;

    public Visibility EntriesHintVisibility => SourceRows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

    public bool OutputZip { get; set { SetProperty(ref field, value); } } = true;

    public bool OutputFolder { get; set { SetProperty(ref field, value); } }

    public SourceRowViewModel? SelectedRow { get; set { SetProperty(ref field, value); } }

    public ICommand RemoveSelectedCommand { get; }

    public ICommand RemoveAllCommand { get; }

    public ICommand BrowseOutputCommand { get; }

    public DecryptMainViewModel()
    {
        _sources = new SourceRowCollection(AppendLog);

        OutputPath = OutputFolderDefaults.Resolve(AppConfig.Instance.OutputFolders.VitaDecryptOutputPath);

        RemoveSelectedCommand = new RelayCommand(_ => _sources.Remove(SelectedRow), _ => SelectedRow != null);
        RemoveAllCommand = new RelayCommand(_ => SourceRows.Clear(), _ => SourceRows.Count > 0);
        BrowseOutputCommand = new RelayCommand(_ => BrowseOutput());
        CancelCommand = new RelayCommand(_ => Cancel());

        SourceRows.CollectionChanged += (_, _) => OnPropertyChanged(nameof(EntriesHintVisibility));
    }

    public void AddSourceFile(string path) => _sources.AddSourceFile(path);

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
        if (!Validate(out string error))
        {
            AppendLog(error, LogLevel.Error);
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
                CancellationTokenSource?.Dispose();

                CancellationTokenSource = null;

                CleanupTask();
            }
        }
    }

    private async Task ExecuteAsync(CancellationToken ct)
    {
        try
        {
            var output = OutputZip ? PkgConvertOutput.Zip : PkgConvertOutput.Folder;
            var entries = SourceRows.Select(r => r.ToBatchEntry()).ToList();

            AppendLog(Loc.S.StatusDecrypting, LogLevel.Highlight);

            var outputs = await DecryptService.DecryptAsync(entries, OutputPath!, output, AppendLog, BuildProgressReporter(), null, ct);

            AppendLog(Loc.S.LogVitaAllDoneTitles(outputs.Count, _totalSw.Elapsed.ToString("mm\\:ss")), LogLevel.Ok);
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

    private bool Validate(out string error)
    {
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(OutputPath))
        {
            error = Loc.S.ErrSelectOutputFolder;
            return false;
        }

        if (SourceRows.Count == 0)
        {
            error = Loc.S.ErrVitaAddSourceFiles;
            return false;
        }

        foreach (var row in SourceRows)
        {
            if (!row.IsPkg || !string.IsNullOrWhiteSpace(row.License))
                continue;

            bool patchUsesAppLicense = row.Category == ContentCategory.Patch && SourceRows.Any(r => r.Category == ContentCategory.App && string.Equals(r.TitleId, row.TitleId, StringComparison.OrdinalIgnoreCase));

            if (!patchUsesAppLicense)
            {
                error = Loc.S.ErrVitaKeyRequired(row.TitleId, row.FileName);
                return false;
            }
        }

        return true;
    }
}
