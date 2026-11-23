using Common.Localization;
using Common.Models;
using System.Diagnostics;
using System.IO;

namespace VitaForge.Core.Services.PSVita;

public static class Vita3kLauncher
{
    public static bool Launch(string rootPath, Action<string, LogLevel> log)
    {
        var info = Vita3kLocator.Resolve(rootPath);

        if (!File.Exists(info.ExePath))
        {
            log(Loc.S.LogPsvPatchVita3kExeMissing(info.ExePath), LogLevel.Error);

            return false;
        }

        if (IsRunning(info.ExePath))
            return true;

        try
        {
            Process.Start(new ProcessStartInfo(info.ExePath)
            {
                WorkingDirectory = info.RootPath,
                UseShellExecute = true
            });

            log(Loc.S.LogPsvPatchVita3kLaunch(info.ExePath), LogLevel.Ok);

            return true;
        }
        catch (Exception ex)
        {
            log(Loc.S.LogError(ex.Message), LogLevel.Error);

            return false;
        }
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