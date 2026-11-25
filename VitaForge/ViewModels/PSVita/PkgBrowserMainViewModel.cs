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
using VitaForge.Core.Services;
using VitaForge.Core.Services.PSVita;
using VitaForge.Core.UI.Command;
using VitaForge.Core.ViewModels.PSVita;

namespace VitaForge.ViewModels.PSVita;

public class PkgBrowserMainViewModel : ProgressToolTabViewModel
{
    private readonly Stopwatch _sw = new();
    private readonly HashSet<string> _checked = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, PkgBrowserTreeNode> _nodes = new(StringComparer.OrdinalIgnoreCase);
    private readonly Stack<string> _back = new();
    private readonly Stack<string> _forward = new();
    private PkgBrowserSession? _session;
    private string? _autoLicense;
    private bool _syncingTree;
    private bool _bulk;

    public ObservableCollection<PkgBrowserItemViewModel> Items { get; } = [];

    public ObservableCollection<PkgBrowserTreeNode> TreeRoots { get; } = [];

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

    public int CheckedCount
    {
        get;
        private set
        {
            SetProperty(ref field, value);
            OnPropertyChanged(nameof(CheckedText));
        }
    }

    public string CheckedText => CheckedCount == 0 ? string.Empty : Loc.S.LabelPkgBrowserChecked(CheckedCount);

    public Visibility LicenseHintVisibility => string.IsNullOrWhiteSpace(License) ? Visibility.Visible : Visibility.Collapsed;

    public Visibility EntriesHintVisibility => PkgPath is null ? Visibility.Visible : Visibility.Collapsed;

    public ICommand AddCommand { get; }

    public ICommand ClearCommand { get; }

    public ICommand BackCommand { get; }

    public ICommand ForwardCommand { get; }

    public ICommand UpCommand { get; }

    public PkgBrowserMainViewModel()
    {
        AddCommand = new RelayCommand(async _ => await AddAsync());
        ClearCommand = new RelayCommand(_ => Clear(), _ => PkgPath is not null);
        BackCommand = new RelayCommand(_ => GoBack(), _ => _back.Count > 0);
        ForwardCommand = new RelayCommand(_ => GoForward(), _ => _forward.Count > 0);
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
            NavigateTo(item.FullPath);
    }

    public void GoBack()
    {
        if (_back.Count == 0)
            return;

        _forward.Push(CurrentFolder);

        Navigate(_back.Pop());
    }

    public void GoForward()
    {
        if (_forward.Count == 0)
            return;

        _back.Push(CurrentFolder);

        Navigate(_forward.Pop());
    }

    public void GoUp()
    {
        if (CurrentFolder.Length == 0)
            return;

        int slash = CurrentFolder.LastIndexOf('/');

        NavigateTo(slash < 0 ? string.Empty : CurrentFolder[..slash]);
    }

    public void SetCurrentChecked(bool value)
    {
        _bulk = true;

        foreach (var item in Items.ToList())
        {
            if (item.IsCheckEnabled)
                item.IsChecked = value;
        }

        _bulk = false;

        RefreshChecked();
    }

    public async Task SaveSelectedAsync(IReadOnlyList<PkgBrowserItemViewModel> highlighted, bool decrypted)
    {
        if (_session is null || IsLocked)
            return;

        if (_checked.Count > 0)
        {
            var checkedFiles = _session.Files.Where(f => IsPathChecked(f.Path)).ToList();
            bool single = _checked.Count == 1 && checkedFiles.Count == 1 && !_nodes.ContainsKey(_checked.First());

            await SaveFilesAsync(checkedFiles, string.Empty, decrypted, single);

            return;
        }

        if (highlighted.Count == 0)
            return;

        var files = highlighted.SelectMany(CollectFiles).Distinct().ToList();
        string basePrefix = CurrentFolder.Length == 0 ? string.Empty : CurrentFolder + "/";

        await SaveFilesAsync(files, basePrefix, decrypted, highlighted.Count == 1 && !highlighted[0].IsDirectory);
    }

    public async Task SaveAllAsync(bool decrypted)
    {
        if (_session is null || IsLocked)
            return;

        await SaveFilesAsync([.. _session.Files], string.Empty, decrypted, false);
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
                string? linked = await Task.Run(() => ResolveLicense(path, session.Probe));

                _session?.Dispose();
                _session = session;

                PkgPath = path;

                _checked.Clear();
                _back.Clear();
                _forward.Clear();

                BuildTree();
                Navigate(string.Empty);
                RefreshChecked();
                ApplyAutoLicense(linked, session.Probe?.TitleId);

                AppendLog(Loc.S.LogPkgBrowserOpened(Path.GetFileName(path), session.Files.Count), LogLevel.Ok);
            }
            catch (Exception ex)
            {
                AppendLog(Loc.S.LogError(ex.Message), LogLevel.Error);
                await SoundPlayer.PlayErrorAsync();
            }
        }
    }

    private static string? ResolveLicense(string path, PkgProbeResult? probe)
    {
        if (probe is null)
            return null;

        try
        {
            string? license = LicenseResolver.Resolve(null, path, probe.Category, probe.TitleId, probe.ContentIdSuffix);

            if (!string.IsNullOrEmpty(license))
                return license;

            return probe.Category == ContentCategory.Patch ? NPSService.Instance.GetGroup(probe.TitleId)?.App?.ZRif : null;
        }
        catch
        {
            return null;
        }
    }

    private void ApplyAutoLicense(string? linked, string? titleId)
    {
        bool replaceable = string.IsNullOrWhiteSpace(License) || License == _autoLicense;

        if (!replaceable)
            return;

        License = string.IsNullOrWhiteSpace(linked) ? null : linked;
        _autoLicense = License;

        if (License is not null)
            AppendLog(Loc.S.LogPkgBrowserLicenseLinked(titleId), LogLevel.Info);
    }

    private void Clear()
    {
        _session?.Dispose();
        _session = null;

        PkgPath = null;

        _checked.Clear();
        _back.Clear();
        _forward.Clear();
        _nodes.Clear();
        TreeRoots.Clear();
        Items.Clear();

        CurrentFolder = string.Empty;
        CheckedCount = 0;

        CommandManager.InvalidateRequerySuggested();
    }

    private void BuildTree()
    {
        TreeRoots.Clear();
        _nodes.Clear();

        if (_session is null)
            return;

        var root = new PkgBrowserTreeNode(Path.GetFileName(PkgPath!), string.Empty, OnTreeNodeSelected) { IsExpanded = true };

        _nodes[string.Empty] = root;

        foreach (var file in _session.Files)
        {
            string[] parts = file.Path.Split('/');
            var parent = root;
            string path = string.Empty;

            for (int i = 0; i < parts.Length - 1; i++)
            {
                path = path.Length == 0 ? parts[i] : path + "/" + parts[i];

                if (!_nodes.TryGetValue(path, out var node))
                {
                    node = new PkgBrowserTreeNode(parts[i], path, OnTreeNodeSelected);
                    _nodes[path] = node;

                    parent.Children.Add(node);
                }

                parent = node;
            }
        }

        TreeRoots.Add(root);
    }

    private void OnTreeNodeSelected(PkgBrowserTreeNode node)
    {
        if (!_syncingTree)
            NavigateTo(node.FullPath);
    }

    private void SyncTree(string folder)
    {
        if (!_nodes.TryGetValue(folder, out var node))
            return;

        _syncingTree = true;

        try
        {
            string path = string.Empty;

            foreach (string part in folder.Split('/', StringSplitOptions.RemoveEmptyEntries))
            {
                if (_nodes.TryGetValue(path, out var ancestor))
                    ancestor.IsExpanded = true;

                path = path.Length == 0 ? part : path + "/" + part;
            }

            node.IsSelected = true;
        }
        finally
        {
            _syncingTree = false;
        }
    }

    private void NavigateTo(string folder)
    {
        if (_session is null || string.Equals(folder, CurrentFolder, StringComparison.OrdinalIgnoreCase))
            return;

        _back.Push(CurrentFolder);
        _forward.Clear();

        Navigate(folder);
    }

    private void Navigate(string folder)
    {
        CurrentFolder = folder;

        RebuildItems();
        SyncTree(folder);

        CommandManager.InvalidateRequerySuggested();
    }

    private void RebuildItems()
    {
        Items.Clear();

        if (_session is null)
            return;

        string prefix = CurrentFolder.Length == 0 ? string.Empty : CurrentFolder + "/";
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
            AddItem(PkgBrowserItemViewModel.ForFolder(name, prefix + name, OnItemChecked));

        foreach (var file in files)
            AddItem(PkgBrowserItemViewModel.ForFile(file, OnItemChecked));
    }

    private void AddItem(PkgBrowserItemViewModel item)
    {
        ApplyCheckState(item);

        Items.Add(item);
    }

    private void ApplyCheckState(PkgBrowserItemViewModel item) => item.SetCheckState(IsPathChecked(item.FullPath), !IsAncestorChecked(item.FullPath));

    private void OnItemChecked(PkgBrowserItemViewModel item, bool value)
    {
        if (value)
        {
            if (item.IsDirectory)
            {
                string prefix = item.FullPath + "/";

                _checked.RemoveWhere(p => p.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
            }

            _checked.Add(item.FullPath);
        }
        else
            _checked.Remove(item.FullPath);

        if (!_bulk)
            RefreshChecked();
    }

    private void RefreshChecked()
    {
        foreach (var item in Items)
            ApplyCheckState(item);

        CheckedCount = _session is null ? 0 : _session.Files.Count(f => IsPathChecked(f.Path));
    }

    private bool IsPathChecked(string path) => _checked.Contains(path) || IsAncestorChecked(path);

    private bool IsAncestorChecked(string path)
    {
        int slash;

        while ((slash = path.LastIndexOf('/')) > 0)
        {
            path = path[..slash];

            if (_checked.Contains(path))
                return true;
        }

        return false;
    }

    private IEnumerable<PkgBrowserFile> CollectFiles(PkgBrowserItemViewModel item)
    {
        if (!item.IsDirectory)
            return [item.File!];

        string prefix = item.FullPath + "/";

        return _session!.Files.Where(f => f.Path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }

    private async Task SaveFilesAsync(List<PkgBrowserFile> files, string basePrefix, bool decrypted, bool single)
    {
        if (files.Count == 0)
            return;

        if (single)
        {
            var dialog = new SaveFileDialog { FileName = Path.GetFileName(files[0].Path) };

            if (dialog.ShowDialog() != true)
                return;

            await SaveAsync([(files[0], dialog.FileName)], decrypted, Path.GetDirectoryName(dialog.FileName)!);

            return;
        }

        string? folder = BrowseFolder();

        if (folder is null)
            return;

        await SaveAsync([.. files.Select(f => (f, Path.Combine(folder, ToLocalPath(f.Path[basePrefix.Length..]))))], decrypted, folder);
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