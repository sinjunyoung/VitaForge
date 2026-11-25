using Common.Services;
using System.Windows;
using Vita.Core.Models;

namespace VitaForge.Core.ViewModels.PSVita;

public sealed class PkgBrowserItemViewModel(Action<PkgBrowserItemViewModel, bool> onChecked) : ViewModelBase
{
    private bool _suppress;

    public required string Name { get; init; }

    public required string FullPath { get; init; }

    public bool IsDirectory { get; init; }

    public PkgBrowserFile? File { get; init; }

    public string IconSource => IsDirectory ? "/Assets/Images/Folder.png" : "/Assets/Images/AddFile.png";

    public string SizeText => File is null ? string.Empty : FileSize.FormatSize(File.Size);

    public bool IsEncrypted => File?.IsEncrypted == true;

    public Visibility LockIconVisibility => File is null ? Visibility.Collapsed : Visibility.Visible;

    public bool IsChecked
    {
        get;
        set
        {
            if (!SetProperty(ref field, value))
                return;

            if (!_suppress)
                onChecked(this, value);
        }
    }

    public bool IsCheckEnabled { get; private set { SetProperty(ref field, value); } } = true;

    public void SetCheckState(bool isChecked, bool isEnabled)
    {
        _suppress = true;

        IsChecked = isChecked;
        IsCheckEnabled = isEnabled;

        _suppress = false;
    }

    public static PkgBrowserItemViewModel ForFolder(string name, string fullPath, Action<PkgBrowserItemViewModel, bool> onChecked)
        => new(onChecked) { Name = name, FullPath = fullPath, IsDirectory = true };

    public static PkgBrowserItemViewModel ForFile(PkgBrowserFile file, Action<PkgBrowserItemViewModel, bool> onChecked)
        => new(onChecked) { Name = file.Path[(file.Path.LastIndexOf('/') + 1)..], FullPath = file.Path, File = file };
}