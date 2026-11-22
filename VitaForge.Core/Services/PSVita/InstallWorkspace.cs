using System.IO;

namespace VitaForge.Core.Services.PSVita;

public static class InstallWorkspace
{
    private static readonly string Root = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "temp");

    public static string Create()
    {
        string path = Path.Combine(Root, Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(path);

        return path;
    }

    public static void Delete(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, true);
        }
        catch { }
    }

    public static void DeleteRootIfEmpty()
    {
        try
        {
            if (Directory.Exists(Root) && !Directory.EnumerateFileSystemEntries(Root).Any())
                Directory.Delete(Root);
        }
        catch { }
    }
}