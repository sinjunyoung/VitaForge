using System.IO;
using VitaForge.Core.Models.PSVita;

namespace VitaForge.Core.Services.PSVita;

public static class Vita3kLocator
{
    private const string ConfigFileName = "config.yml";
    private const string ExeFileName = "Vita3K.exe";
    private const string PrefKey = "pref-path:";

    public static Vita3kInfo Resolve(string rootPath)
    {
        string root = Path.GetFullPath(rootPath);

        return new Vita3kInfo(root, ReadPrefPath(root) ?? root, Path.Combine(root, ExeFileName));
    }

    private static string? ReadPrefPath(string root)
    {
        string configPath = Path.Combine(root, ConfigFileName);

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