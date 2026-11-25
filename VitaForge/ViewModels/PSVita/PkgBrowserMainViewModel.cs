using Common.Localization;
using Common.Models;
using Common.Services;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using Vita.Core.Models;
using Vita.Core.Services;
using VitaForge.Core;
using VitaForge.Core.Services;
using VitaForge.Core.UI.Command;
using VitaForge.Core.ViewModels.PSVita;

namespace VitaForge.ViewModels.PSVita;

public class PkgBrowserMainViewModel : ProgressToolTabViewModel
{
    private readonly Stopwatch _sw = new();
    private PkgBrowserSession? _session;

    public ObservableCollection<PkgBrowserItemViewModel> Items { get; } = [];

    public string? PkgPath
    {
        get;
        private set
        {
            SetProperty(ref field, value);
            OnPropertyChanged(nameof(EntriesHintVisibility));
        }
    }

    public string CurrentFolder
    {
        get;
        private set
        {
            SetProperty(ref field, value);
            OnPropertyChanged(nameof(DisplayPath));
        }
    } = string.Empty;

    public string DisplayPath => "/" + CurrentFolder;

    public string? License
    {
        get;
        set
        {
            SetProperty(ref field, value);
            OnPropertyChanged(nameof(LicenseHintVisibility));
        }
    }

    public Visibility LicenseHintVisibility => string.IsNullOrWhiteSpace(License) ? Visibility.Visible : Visibility.Collapsed;

    public Visibility EntriesHintVisibility => PkgPath is null ? Visibility.Visible : Visibility.Collapsed;

    public ICommand AddCommand { get; }

    public ICommand ClearCommand { get; }

    public ICommand UpCommand { get; }

    public PkgBrowserMainViewModel()
    {
        AddCommand = new RelayCommand(async _ => await AddAsync());
        ClearCommand = new RelayCommand(_ => Clear(), _ => PkgPath is not null);
        UpCommand = new RelayCommand(_ => GoUp(), _ => CurrentFolder.Length > 0);
        CancelCommand = new RelayCommand(_ => Cancel());
    }

    public async Task OpenDroppedAsync(string[] files)
    {
        if (files.Length != 1)
        {
            AppendLog(Loc.S.ErrPkgBrowserSingleFileOnly, LogLevel.Error);

            return;
        }

        await OpenPkgAsync(files[0]);
    }

    public void OpenItem(PkgBrowserItemViewModel item)
    {
        if (item.IsDirectory)
            Navigate(item.FullPath);
    }

    public void GoUp()
    {
        int slash = CurrentFolder.LastIndexOf('/');

        Navigate(slash < 0 ? string.Empty : CurrentFolder[..slash]);
    }

    public async Task SaveSelectedAsync(IReadOnlyList<PkgBrowserItemViewModel> selected, bool decrypted)
    {
        if (_session is null || selected.Count == 0 || IsLocked)
            return;

        var files = selected.SelectMany(CollectFiles).Distinct().ToList();

        if (files.Count == 0)
            return;

        if (selected.Count == 1 && !selected[0].IsDirectory)
        {
            var dialog = new SaveFileDialog { FileName = selected[0].Name };

            if (dialog.ShowDialog() != true)
                return;

            await SaveAsync([(files[0], dialog.FileName)], decrypted, Path.GetDirectoryName(dialog.FileName)!);

            return;
        }

        string? folder = BrowseFolder();

        if (folder is null)
            return;

        string basePrefix = CurrentFolder.Length == 0 ? string.Empty : CurrentFolder + "/";

        await SaveAsync([.. files.Select(f => (f, Path.Combine(folder, ToLocalPath(f.Path[basePrefix.Length..]))))], decrypted, folder);
    }

    public async Task SaveAllAsync(bool decrypted)
    {
        if (_session is null || IsLocked)
            return;

        string? folder = BrowseFolder();

        if (folder is null)
            return;

        await SaveAsync([.. _session.Files.Select(f => (f, Path.Combine(folder, ToLocalPath(f.Path))))], decrypted, folder);
    }

    private async Task AddAsync()
    {
        var dialog = new OpenFileDialog { Filter = "PKG (*.pkg)|*.pkg" };

        if (dialog.ShowDialog() == true)
            await OpenPkgAsync(dialog.FileName);
    }

    private async Task OpenPkgAsync(string path)
    {
        if (IsLocked)
            return;

        if (!string.Equals(Path.GetExtension(path), ".pkg", StringComparison.OrdinalIgnoreCase))
        {
            AppendLog(Loc.S.ErrNotFormatFile("PKG"), LogLevel.Error);

            return;
        }

        using (BeginWork())
        {
            try
            {
                var session = await Task.Run(() => PkgBrowserSession.Open(path));

                _session?.Dispose();
                _session = session;

                PkgPath = path;

                Navigate(string.Empty);

                AppendLog(Loc.S.LogPkgBrowserOpened(Path.GetFileName(path), session.Files.Count), LogLevel.Ok);
            }
            catch (Exception ex)
            {
                AppendLog(Loc.S.LogError(ex.Message), LogLevel.Error);
                await SoundPlayer.PlayErrorAsync();
            }
        }
    }

    private void Clear()
    {
        _session?.Dispose();
        _session = null;

        PkgPath = null;

        Navigate(string.Empty);
    }

    private void Navigate(string folder)
    {
        CurrentFolder = folder;

        Items.Clear();

        if (_session is null)
            return;

        string prefix = folder.Length == 0 ? string.Empty : folder + "/";
        var folders = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        var files = new List<PkgBrowserFile>();

        foreach (var file in _session.Files)
        {
            if (!file.Path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                continue;

            string rest = file.Path[prefix.Length..];
            int slash = rest.IndexOf('/');

            if (slash >= 0)
                folders.Add(rest[..slash]);
            else
                files.Add(file);
        }

        foreach (string name in folders)
            Items.Add(PkgBrowserItemViewModel.ForFolder(name, prefix + name));

        foreach (var file in files)
            Items.Add(PkgBrowserItemViewModel.ForFile(file));
    }

    private IEnumerable<PkgBrowserFile> CollectFiles(PkgBrowserItemViewModel item)
    {
        if (!item.IsDirectory)
            return [item.File!];

        string prefix = item.FullPath + "/";

        return _session!.Files.Where(f => f.Path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }

    private static string? BrowseFolder()
    {
        var dialog = new Ookii.Dialogs.Wpf.VistaFolderBrowserDialog
        {
            Description = Loc.S.ToolTipBrowseOutput,
            UseDescriptionForTitle = true
        };

        return dialog.ShowDialog() == true ? dialog.SelectedPath : null;
    }

    private static string ToLocalPath(string path) => path.Replace('/', Path.DirectorySeparatorChar);

    private async Task SaveAsync(List<(PkgBrowserFile File, string Destination)> jobs, bool decrypted, string openFolderPath)
    {
        var session = _session!;
        string? license = License;
        bool needKey = decrypted && jobs.Any(j => j.File.IsEncrypted);

        if (needKey && string.IsNullOrWhiteSpace(license))
        {
            AppendLog(Loc.S.ErrPkgBrowserLicenseRequired, LogLevel.Error);
            await SoundPlayer.PlayErrorAsync();

            return;
        }

        _sw.Restart();

        using (BeginWork())
        {
            CancellationTokenSource = new CancellationTokenSource();

            try
            {
                var ct = CancellationTokenSource.Token;
                var progress = BuildProgressReporter();

                await Task.Run(() =>
                {
                    byte[]? klicensee = needKey ? session.ResolveKlicensee(license!) : null;

                    WriteFiles(session, jobs, klicensee, decrypted, progress, ct);
                }, ct);

                AppendLog(Loc.S.LogPkgBrowserSaved(jobs.Count, openFolderPath), LogLevel.Ok);
                await SoundPlayer.PlayCompleteAsync();
                openFolderPath.OpenFolder();
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
            finally
            {
                CancellationTokenSource?.Dispose();

                CancellationTokenSource = null;

                CleanupTask();
            }
        }
    }

    private static void WriteFiles(PkgBrowserSession session, List<(PkgBrowserFile File, string Destination)> jobs, byte[]? klicensee, bool decrypted, IProgress<ProgressInfo> progress, CancellationToken ct)
    {
        long total = jobs.Sum(j => decrypted ? j.File.Size : j.File.RawSize);
        var reporter = new ProgressReporter(decrypted ? Loc.S.StatusDecrypting : Loc.S.StatusExtracting, string.Empty, total, progress);

        foreach (var (file, destination) in jobs)
        {
            ct.ThrowIfCancellationRequested();

            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);

            bool completed = false;

            try
            {
                using (var stream = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None, 1024 * 1024))
                {
                    session.Save(file.Path, stream, file.IsEncrypted ? klicensee : null, delta =>
                    {
                        ct.ThrowIfCancellationRequested();

                        reporter.AddProgress(delta);
                    });
                }

                completed = true;
            }
            finally
            {
                if (!completed)
                    TryDelete(destination);
            }
        }

        reporter.ForceReport();
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch { }
    }
}