using System.IO;
using Vita.Core.Models;

namespace VitaForge.Core.Services.PSVita;

public static class LicenseResolver
{
    private static readonly string[] SidecarExtensions = [".rif", ".zrif", ".txt"];

    public static string? Resolve(string? current, string pkgPath, ContentCategory category, string? titleId, string? contentIdSuffix)
    {
        string? sidecar = ReadSidecar(pkgPath);

        if (!string.IsNullOrEmpty(sidecar))
            return sidecar;

        if (!string.IsNullOrEmpty(current))
            return current;

        var group = NPSService.Instance.GetGroup(titleId);

        return category switch
        {
            ContentCategory.App => group?.App?.ZRif,
            ContentCategory.Addcont => group?.GetDlcLicense(contentIdSuffix),
            _ => null
        };
    }

    private static string? ReadSidecar(string pkgPath)
    {
        string? dir = Path.GetDirectoryName(pkgPath);

        if (string.IsNullOrEmpty(dir))
            return null;

        string baseName = Path.GetFileNameWithoutExtension(pkgPath);
        string? licensePath = SidecarExtensions.Select(ext => Path.Combine(dir, baseName + ext)).FirstOrDefault(File.Exists);

        if (licensePath == null)
            return null;

        try
        {
            string text = File.ReadAllText(licensePath).Trim();

            return string.IsNullOrEmpty(text) ? null : text;
        }
        catch
        {
            return null;
        }
    }
}
