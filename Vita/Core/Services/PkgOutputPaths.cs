using Common.Services;
using Vita.Core.Models;

namespace Vita.Core.Services;

public static class PkgOutputPaths
{
    public static string Resolve(string outputRoot, string? titleId, PkgConvertOutput output)
    {
        if (output == PkgConvertOutput.Direct)
            return outputRoot;

        string name = SanitizeFileName(titleId);

        return output == PkgConvertOutput.Zip
            ? Utils.GetUniqueFilePath(Path.Combine(outputRoot, name + ".zip"))
            : GetUniqueDirectoryPath(Path.Combine(outputRoot, name));
    }

    public static void Discard(string path, PkgConvertOutput output)
    {
        if (output != PkgConvertOutput.Direct)
            DeleteQuietly(path);
    }

    public static void DeleteQuietly(string? path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
            else if (Directory.Exists(path))
                Directory.Delete(path, true);
        }
        catch { }
    }

    private static string SanitizeFileName(string? name)
    {
        if (string.IsNullOrEmpty(name))
            return string.Empty;

        foreach (char c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');

        return name;
    }

    private static string GetUniqueDirectoryPath(string path)
    {
        while (Directory.Exists(path) || File.Exists(path))
            path += "_";

        return path;
    }
}