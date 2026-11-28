using System.IO;
using System.Security.Cryptography;
using System.Text;
using Vita.Core.Services;
using VitaForge.Core.Models.PSVita;

namespace VitaForge.Core.Services.PSVita;

public static class DownloadCache
{
    private const string PkgFolderName = "PKG";
    private const string PatchFolderName = "Patch";

    public static string Root => DownloadFolderDefaults.Resolve(AppConfig.Instance.VitaDownload.DownloadPath);

    public static bool DeleteAfterInstall => AppConfig.Instance.VitaDownload.DeleteAfterInstall;

    public static string PkgPath(NpsCatalogItem item) => Path.Combine(Root, PkgFolderName, NpsPkgDownloadService.BuildFileName(item));

    public static string PatchDirectory(PatchCatalogItem patch)
    {
        string identity = string.Join('\n', patch.Links.Select(l => $"{l.Url}|{l.FileName}|{l.Index}").Append(patch.Version).Append(patch.LastModified));
        string key = Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes(identity)))[..8];

        return Path.Combine(Root, PatchFolderName, $"{Sanitize(patch.TitleId)}_{key}");
    }

    public static bool IsComplete(string path, NpsCatalogItem item)
    {
        var info = new FileInfo(path);

        return info.Exists && info.Length > 0 && (item.Size <= 0 || info.Length == item.Size);
    }

    public static void Delete(PatchInstallPlan plan)
    {
        foreach (var (item, _) in plan.Downloads)
            PkgOutputPaths.DeleteQuietly(PkgPath(item));

        PkgOutputPaths.DeleteQuietly(PatchDirectory(plan.Patch));

        RemoveEmptyFolders();
    }

    public static void DeletePkgs(IEnumerable<NpsCatalogItem> items)
    {
        foreach (var item in items)
            PkgOutputPaths.DeleteQuietly(PkgPath(item));

        RemoveEmptyFolders();
    }

    private static void RemoveEmptyFolders()
    {
        try
        {
            foreach (string name in new[] { PkgFolderName, PatchFolderName })
            {
                string dir = Path.Combine(Root, name);

                if (Directory.Exists(dir) && !Directory.EnumerateFileSystemEntries(dir).Any())
                    Directory.Delete(dir);
            }

            if (string.IsNullOrWhiteSpace(AppConfig.Instance.VitaDownload.DownloadPath) && Directory.Exists(Root) && !Directory.EnumerateFileSystemEntries(Root).Any())
                Directory.Delete(Root);
        }
        catch { }
    }

    private static string Sanitize(string name)
    {
        foreach (char c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');

        return name;
    }
}