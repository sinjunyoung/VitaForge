using System.IO;
using VitaForge.Core.Models.PSVita;

namespace VitaForge.Core.Services.PSVita;

public static class Vita3kLocator
{
    private const string ConfigFileName = "config.yml";
    private const string ExeFileName = "Vita3K.exe";
    private const string PrefKey = "pref-path:";
    private const string PortableFolderName = "portable";

    public static Vita3kInfo Resolve(string rootPath)
    {
        string root = Path.GetFullPath(rootPath);

        string prefPath = ReadPrefPath(root) ?? FindExistingDataPath(root) ?? root;

        return new Vita3kInfo(root, prefPath, Path.Combine(root, ExeFileName));
    }

    private static string? FindExistingDataPath(string root)
    {
        var candidates = new List<string> { Path.Combine(root, PortableFolderName, "fs"), root };
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

        if (!string.IsNullOrEmpty(appData))
        {
            candidates.Add(Path.Combine(appData, "Vita3K", "Vita3K"));
            candidates.Add(Path.Combine(appData, "Vita3K"));
        }

        return candidates.FirstOrDefault(c => Directory.Exists(Path.Combine(c, "ux0")));
    }

    private static string? ReadPrefPath(string root)
    {
        string configPath = Path.Combine(root, PortableFolderName, ConfigFileName);

        if (!File.Exists(configPath))
            configPath = Path.Combine(root, ConfigFileName);

        if (!File.Exists(configPath))
            return null;

        try
        {
            using var stream = new FileStream(configPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(stream);

            string? line;

            while ((line = reader.ReadLine()) != null)
            {
                if (!line.StartsWith(PrefKey, StringComparison.Ordinal))
                    continue;

                string value = line[PrefKey.Length..].Trim().Trim('"', '\'');

                if (value.Length == 0)
                    return null;

                return Path.GetFullPath(Path.IsPathRooted(value) ? value : Path.Combine(root, value));
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
        }

        return null;
    }
}