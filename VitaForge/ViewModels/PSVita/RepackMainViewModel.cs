using Common.Localization;
using Common.Models;
using Common.Services;
using VitaForge.Core;
using VitaForge.Core.Services;
using VitaForge.Core.Services.Patch;
using VitaForge.Core.Services.PSVita;
using VitaForge.Core.UI.Command;
using VitaForge.Core.ViewModels.PSVita;
using VitaForge.Views;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using Vita.Core.Models;
using Vita.Core.Services;

namespace VitaForge.ViewModels.PSVita;

public class RepackMainViewModel : ProgressToolTabViewModel
{
    private readonly Stopwatch _totalSw = new();
    private readonly SourceRowCollection _sources;
    private string? _pendingZipPath;
    private readonly PatchTargetChoiceCache _choiceCache = new();
    private int _patchLogVersion;

    public ObservableCollection<SourceRowViewModel> SourceRows => _sources.Rows;

    public string? OutputPath
    {
        get;
        set
        {
            SetProperty(ref field, value);
            OnPropertyChanged(nameof(OutputHintVisibility));

            AppConfig.Instance.OutputFolders.VitaOutputPath = value;
        }
    } = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "output");

    public Visibility OutputHintVisibility => string.IsNullOrWhiteSpace(OutputPath) ? Visibility.Visible : Visibility.Collapsed;

    public Visibility EntriesHintVisibility => SourceRows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

    public bool BuildEmu { get; set { SetProperty(ref field, value); } } = true;

    public bool BuildRetail { get; set { SetProperty(ref field, value); } }

    public bool MergePatchIntoGame { get; set { SetProperty(ref field, value); } } = false;

    public SourceRowViewModel? SelectedRow { get; set { SetProperty(ref field, value); } }

    public ICommand RemoveSelectedCommand { get; }
    public ICommand RemoveAllCommand { get; }
    public ICommand BrowseOutputCommand { get; }

    public RepackMainViewModel()
    {
        _sources = new SourceRowCollection(AppendLog);

        OutputPath = OutputFolderDefaults.Resolve(AppConfig.Instance.OutputFolders.VitaOutputPath);

        RemoveSelectedCommand = new RelayCommand(_ => _sources.Remove(SelectedRow), _ => SelectedRow != null);
        RemoveAllCommand = new RelayCommand(_ => SourceRows.Clear(), _ => SourceRows.Count > 0);
        BrowseOutputCommand = new RelayCommand(_ => BrowseOutput());
        CancelCommand = new RelayCommand(_ => Cancel());

        SourceRows.CollectionChanged += (_, _) =>
        {
            _choiceCache.Clear();
            OnPropertyChanged(nameof(EntriesHintVisibility));
        };
        _sources.PatchPathChanged += path =>
        {
            _choiceCache.Clear();
            _ = LogPatchInfoAsync(path);
        };
    }

    public void AddSourceFile(string path) => _sources.AddSourceFile(path);

    private async Task LogPatchInfoAsync(string? patchPath)
    {
        int version = ++_patchLogVersion;

        if (string.IsNullOrWhiteSpace(patchPath))
            return;

        try
        {
            var (total, patchFiles) = await Task.Run(() => PatchInspector.Inspect(patchPath));

            if (version == _patchLogVersion)
                AppendLog(Loc.S.LogVitaPatchInfoReadDetail(total, patchFiles, total - patchFiles));
        }
        catch (Exception ex)
        {
            if (version == _patchLogVersion)
                AppendLog(Loc.S.LogPatchReadFailed(ex.Message), LogLevel.Error);
        }
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
            await BuildAsync(ct);

            AppendLog(Loc.S.LogAllDoneElapsed(_totalSw.Elapsed.ToString("mm\\:ss")), LogLevel.Ok);
            await SoundPlayer.PlayCompleteAsync();
            OutputPath?.OpenFolder();
        }
        catch (OperationCanceledException)
        {
            AppendLog(Loc.S.LogJobCancelled, LogLevel.Error);
            await SoundPlayer.PlayCancelAsync();
            MainViewModel.SetTaskbarProgress(0);
            SafeDeleteFile(_pendingZipPath);
        }
        catch (Exception ex)
        {
            AppendLog(Loc.S.LogError(ex.Message), LogLevel.Error);
            await SoundPlayer.PlayErrorAsync();
            MainViewModel.SetTaskbarProgress(100, System.Windows.Shell.TaskbarItemProgressState.Error);
            SafeDeleteFile(_pendingZipPath);
        }
        finally
        {
            CleanupTask();
        }
    }

    private async Task BuildAsync(CancellationToken ct)
    {
        var target = BuildEmu ? OutputTarget.Emu : OutputTarget.Retail;
        string label = target == OutputTarget.Emu ? Loc.S.LabelOutputEmu : Loc.S.LabelOutputRetail;
        bool merge = MergePatchIntoGame;
        string? baseName = SourceRows.First(r => r.Category == ContentCategory.App).TitleId;
        string patchPath = _sources.GetPatchPath();
        string suffix = merge ? $"_{label}_merged.zip" : $"_{label}.zip";
        string fileName = VersionInfoExtractor.ApplySuffix($"{baseName}{suffix}", patchPath);
        string zipPath = Utils.GetUniqueFilePath(Path.Combine(OutputPath!, fileName));
        var progress = BuildProgressReporter();
        var entries = SourceRows.Select(r => r.ToBatchEntry()).ToList();

        _pendingZipPath = zipPath;

        _choiceCache.Reset(PatchTargetChoiceCache.BuildFingerprint(entries.SelectMany(e => new[] { e.Path, e.PatchPath }).Append(patchPath)));

        if (merge)
        {
            AppendLog(Loc.S.LogVitaMergingGamePatch(label), LogLevel.Highlight);

            var result = await Task.Run(() => PatchOutputBuilder.BuildMergedFromEntriesAsync(entries, patchPath, zipPath, target, AppendLog, progress, ResolvePatchTarget, null, ct), ct);

            if (result.FailedFiles > 0)
                throw new InvalidDataException(Loc.S.LogVitaFilesFailedDiscarded(result.FailedFiles));

            AppendLog(Loc.S.LogVitaRepackDone(label, result.TotalFiles, result.PatchedSuccessfully, result.PatchCandidates, zipPath), LogLevel.Ok);
        }
        else
        {
            AppendLog(Loc.S.LogVitaCreatingPatch(label), LogLevel.Highlight);

            var result = await Task.Run(() => PatchOnlyBuilder.BuildFromEntriesAsync(entries, patchPath, zipPath, target, AppendLog, progress, ResolvePatchTarget, null, ct), ct);

            AppendLog(Loc.S.LogVitaPatchOnlyDone(label, result.MatchedCandidates, result.PatchedSuccessfully, zipPath), LogLevel.Ok);
        }
    }

    private IReadOnlyList<string?> ResolvePatchTarget(PatchTargetRequest request)
    {
        if (!_choiceCache.TryGet(request, out var selections))
        {
            selections = Application.Current.Dispatcher.Invoke(() => PatchTargetPickerWindow.Pick(Application.Current.MainWindow, request));

            _choiceCache.Set(request, selections);
        }

        PatchTargetLogger.Log(request, selections, AppendLog);

        return selections;
    }

    private bool Validate(out string error)
    {
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(OutputPath))
        {
            error = Loc.S.ErrSelectWorkFolder;
            return false;
        }

        if (SourceRows.Count == 0)
        {
            error = Loc.S.ErrVitaAddSourceFiles;
            return false;
        }

        if (!BuildEmu && !BuildRetail)
        {
            error = Loc.S.ErrSelectOutputFormat;
            return false;
        }

        if (SourceRows.Any(r => string.IsNullOrWhiteSpace(r.PatchPath)))
        {
            error = Loc.S.ErrSpecifyPatch;
            return false;
        }

        bool everyTitleHasApp = SourceRows
            .GroupBy(r => r.TitleId, StringComparer.OrdinalIgnoreCase)
            .All(g => g.Any(r => r.Category == ContentCategory.App));

        if (!everyTitleHasApp)
        {
            error = Loc.S.ErrVitaTitleWithoutApp;
            return false;
        }

        return true;
    }

    private static void SafeDeleteFile(string? path)
    {
        try
        {
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
                File.Delete(path);
        }
        catch { }
    }
}
