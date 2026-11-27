using Common.Localization;
using Common.Models;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace VitaForge.Core.Services.PSVita;

public static class Vita3kLauncher
{
    public static string? SingleTitleId(IEnumerable<string> titleIds)
    {
        var distinct = titleIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim().ToUpperInvariant())
            .Distinct()
            .Take(2)
            .ToList();

        return distinct.Count == 1 ? distinct[0] : null;
    }

    public static bool Launch(string rootPath, string? titleId, Action<string, LogLevel> log)
    {
        var info = Vita3kLocator.Resolve(rootPath);

        if (!File.Exists(info.ExePath))
        {
            log(Loc.S.LogPsvPatchVita3kExeMissing(info.ExePath), LogLevel.Error);

            return false;
        }

        if (IsRunning(info.ExePath))
            return true;

        string arguments = BuildArguments(titleId);

        try
        {
            Process.Start(new ProcessStartInfo(info.ExePath)
            {
                Arguments = arguments,
                WorkingDirectory = info.RootPath,
                UseShellExecute = true
            });

            log(Loc.S.LogPsvPatchVita3kLaunch(arguments.Length == 0 ? info.ExePath : $"{info.ExePath} {arguments}"), LogLevel.Ok);

            return true;
        }
        catch (Exception ex)
        {
            log(Loc.S.LogError(ex.Message), LogLevel.Error);

            return false;
        }
    }

    private static string BuildArguments(string? titleId)
    {
        if (string.IsNullOrEmpty(titleId))
            return string.Empty;

        return $"-r {titleId}";
    }

    private static bool IsRunning(string exePath)
    {
        Process[] processes = [];

        try
        {
            processes = Process.GetProcessesByName(Path.GetFileNameWithoutExtension(exePath));

            foreach (var process in processes)
            {
                try
                {
                    if (string.Equals(process.MainModule?.FileName, exePath, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
                catch { }
            }
        }
        catch { }
        finally
        {
            foreach (var process in processes)
                process.Dispose();
        }

        return false;
    }
}