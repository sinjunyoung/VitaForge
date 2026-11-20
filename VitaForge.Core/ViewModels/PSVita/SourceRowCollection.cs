using Common.Localization;
using Common.Models;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using VitaForge.Core.Services.PSVita;
using Vita.Core.Models;

namespace VitaForge.Core.ViewModels.PSVita;

public sealed class SourceRowCollection
{
    private readonly Action<string, LogLevel> _log;
    private bool _isSyncing;

    public ObservableCollection<SourceRowViewModel> Rows { get; } = [];

    public event Action<string?>? PatchPathChanged;

    public SourceRowCollection(Action<string, LogLevel> log)
    {
        _log = log;

        Rows.CollectionChanged += Rows_CollectionChanged;
    }

    public string GetPatchPath() => Rows.Select(r => r.PatchPath).FirstOrDefault(p => !string.IsNullOrWhiteSpace(p)) ?? string.Empty;

    public void Remove(SourceRowViewModel? row)
    {
        if (row != null)
            Rows.Remove(row);
    }

    public void AddSourceFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return;

        bool isDirectory = Directory.Exists(path);
        bool isPkg = path.EndsWith(".pkg", StringComparison.OrdinalIgnoreCase);
        bool isZip = path.EndsWith(".zip", StringComparison.OrdinalIgnoreCase);

        if (!isDirectory && !isPkg && !isZip)
            return;

        if (isPkg)
        {
            SourceRowViewModel row;

            try
            {
                row = SourceRowViewModel.FromPkg(path);
            }
            catch (Exception ex)
            {
                _log(Loc.S.LogFileAnalyzeFailed(System.IO.Path.GetFileName(path), ex.Message), LogLevel.Error);
                return;
            }

            ApplyDefaults(row, path);
            AddOrReplaceRow(row);

            return;
        }

        List<SourceRowViewModel> discoveredRows;

        try
        {
            discoveredRows = SourceRowViewModel.DiscoverFromContainer(path);
        }
        catch (Exception ex)
        {
            _log(Loc.S.LogFileAnalyzeFailed(System.IO.Path.GetFileName(path), ex.Message), LogLevel.Error);
            return;
        }

        foreach (var row in discoveredRows)
        {
            ApplyDefaults(row, null);
            AddOrReplaceRow(row);
        }
    }

    private void Rows_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems != null)
        {
            foreach (SourceRowViewModel row in e.NewItems)
                row.PropertyChanged += Row_PropertyChanged;
        }

        if (e.OldItems != null)
        {
            foreach (SourceRowViewModel row in e.OldItems)
                row.PropertyChanged -= Row_PropertyChanged;
        }

        RecomputeLicenseEditability();
    }

    private void Row_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_isSyncing)
            return;

        if (sender is not SourceRowViewModel changedRow)
            return;

        _isSyncing = true;

        try
        {
            if (e.PropertyName == nameof(SourceRowViewModel.PatchPath))
            {
                foreach (var row in Rows)
                {
                    if (row != changedRow && !string.Equals(row.PatchPath, changedRow.PatchPath))
                        row.PatchPath = changedRow.PatchPath;
                }

                PatchPathChanged?.Invoke(changedRow.PatchPath);
            }
            else if (e.PropertyName == nameof(SourceRowViewModel.License))
            {
                if (changedRow.Category != ContentCategory.Addcont)
                {
                    foreach (var row in Rows)
                    {
                        if (row.IsPkg && row.Category != ContentCategory.Addcont && row != changedRow && !string.Equals(row.License, changedRow.License))
                            row.License = changedRow.License;
                    }
                }
            }
        }
        finally
        {
            _isSyncing = false;
        }
    }

    private void ApplyDefaults(SourceRowViewModel row, string? pkgPath)
    {
        if (row.IsPkg && pkgPath != null)
        {
            string? dir = System.IO.Path.GetDirectoryName(pkgPath);

            if (!string.IsNullOrEmpty(dir))
            {
                string baseName = System.IO.Path.GetFileNameWithoutExtension(pkgPath);
                string? licensePath = new[] { ".zrif", ".txt" }.Select(ext => System.IO.Path.Combine(dir, baseName + ext)).FirstOrDefault(File.Exists);

                if (licensePath != null)
                {
                    try
                    {
                        string licenseText = File.ReadAllText(licensePath).Trim();

                        if (!string.IsNullOrEmpty(licenseText))
                            row.License = licenseText;
                    }
                    catch { }
                }
            }

            if (string.IsNullOrEmpty(row.License))
            {
                var group = NPSService.Instance.GetGroup(row.TitleId);

                row.License = row.Category switch
                {
                    ContentCategory.App => group?.App?.ZRif,
                    ContentCategory.Addcont => group?.GetDlcLicense(row.ContentIdSuffix),
                    _ => row.License
                };
            }

            if (row.Category != ContentCategory.Addcont)
            {
                var existingAppOrPatch = Rows.FirstOrDefault(r => r.IsPkg && r.Category != ContentCategory.Addcont && !string.IsNullOrEmpty(r.License));

                if (existingAppOrPatch != null && string.IsNullOrEmpty(row.License))
                    row.License = existingAppOrPatch.License;
            }
        }

        var existingAny = Rows.FirstOrDefault();

        if (existingAny != null && !string.IsNullOrEmpty(existingAny.PatchPath))
            row.PatchPath = existingAny.PatchPath;

        _ = row.LoadMetadataAsync();
    }

    private void AddOrReplaceRow(SourceRowViewModel newRow)
    {
        if (newRow.Category is ContentCategory.App or ContentCategory.Patch)
        {
            var existing = Rows.FirstOrDefault(r => r.Category == newRow.Category);

            if (existing != null)
            {
                _log(Loc.S.LogVitaReplacedExisting(existing.FileName, newRow.FileName, newRow.Category), LogLevel.Info);
                Rows.Remove(existing);
            }
        }
        else
        {
            var duplicate = Rows.FirstOrDefault(r => r.Category == ContentCategory.Addcont && string.Equals(r.TitleId, newRow.TitleId, StringComparison.OrdinalIgnoreCase) && string.Equals(r.ContentIdSuffix, newRow.ContentIdSuffix, StringComparison.OrdinalIgnoreCase));

            if (duplicate != null)
            {
                _log(Loc.S.LogVitaReplacedSameDlc(duplicate.FileName, newRow.FileName), LogLevel.Info);
                Rows.Remove(duplicate);
            }
        }

        Rows.Insert(ComputeInsertIndex(newRow.Category, newRow.ContentIdSuffix), newRow);
    }

    private int ComputeInsertIndex(ContentCategory? category, string? contentIdSuffix)
    {
        if (category == ContentCategory.App)
            return 0;

        if (category == ContentCategory.Patch)
            return Rows.Any(r => r.Category == ContentCategory.App) ? 1 : 0;

        int index = Rows.Count(r => r.Category is ContentCategory.App or ContentCategory.Patch);

        foreach (var row in Rows)
        {
            if (row.Category != ContentCategory.Addcont)
                continue;

            if (string.Compare(contentIdSuffix, row.ContentIdSuffix, StringComparison.OrdinalIgnoreCase) < 0)
                break;

            index++;
        }

        return index;
    }

    private void RecomputeLicenseEditability()
    {
        foreach (var row in Rows)
        {
            if (!row.IsPkg)
            {
                row.IsLicenseEditable = false;
                continue;
            }

            if (row.Category == ContentCategory.Addcont)
            {
                row.IsLicenseEditable = true;
                continue;
            }

            var appRow = Rows.FirstOrDefault(r => r.Category == ContentCategory.App && string.Equals(r.TitleId, row.TitleId, StringComparison.OrdinalIgnoreCase));

            row.IsLicenseEditable = appRow == null || appRow.IsPkg;

            if (!row.IsLicenseEditable && !string.IsNullOrEmpty(row.License))
                row.License = string.Empty;
        }
    }
}
