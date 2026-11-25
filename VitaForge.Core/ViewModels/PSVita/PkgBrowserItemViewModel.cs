using Common.Services;
using Vita.Core.Models;

namespace VitaForge.Core.ViewModels.PSVita;

public sealed class PkgBrowserItemViewModel
{
    public required string Name { get; init; }

    public required string FullPath { get; init; }

    public bool IsDirectory { get; init; }

    public PkgBrowserFile? File { get; init; }

    public string IconSource => IsDirectory ? "/Assets/Images/AddFolder.png" : "/Assets/Images/AddFile.png";

    public string SizeText => File is null ? string.Empty : FileSize.FormatSize(File.Size);

    public string EncryptedMark => File?.IsEncrypted == true ? "🔒" : string.Empty;

    public static PkgBrowserItemViewModel ForFolder(string name, string fullPath) => new() { Name = name, FullPath = fullPath, IsDirectory = true };

    public static PkgBrowserItemViewModel ForFile(PkgBrowserFile file) => new() { Name = file.Path[(file.Path.LastIndexOf('/') + 1)..], FullPath = file.Path, File = file };
}