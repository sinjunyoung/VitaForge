using System.IO;

namespace VitaForge.Core.Services.PSVita;

public static class InstallWorkspace
{
    private const string LockFileName = ".lock";

    private static readonly string Root = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "temp");
    private static readonly Lock Gate = new();
    private static readonly Dictionary<string, FileStream> Locks = new(StringComparer.OrdinalIgnoreCase);

    public static string Create()
    {
        string path = Path.Combine(Root, Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(path);

        var handle = new FileStream(Path.Combine(path, LockFileName), FileMode.Create, FileAccess.ReadWrite, FileShare.None);

        lock (Gate)
            Locks[path] = handle;

        return path;
    }

    public static void Delete(string path)
    {
        FileStream? handle;

        lock (Gate)
            Locks.Remove(path, out handle);

        handle?.Dispose();

        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, true);
        }
        catch { }
    }

    public static void CleanStale()
    {
        try
        {
            if (!Directory.Exists(Root))
                return;

            foreach (string dir in Directory.EnumerateDirectories(Root))
            {
                try
                {
                    using (new FileStream(Path.Combine(dir, LockFileName), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
                    {
                    }
                }
                catch
                {
                    continue;
                }

                try
                {
                    Directory.Delete(dir, true);
                }
                catch { }
            }
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
