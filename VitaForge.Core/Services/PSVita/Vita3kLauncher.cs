using Common.Localization;
using Common.Models;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace VitaForge.Core.Services.PSVita;

public static class Vita3kLauncher
{
    private const string SdlFrontendOption = "--frontend";

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

        string arguments = BuildArguments(info.ExePath, titleId);

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

    private static string BuildArguments(string exePath, string? titleId)
    {
        if (string.IsNullOrEmpty(titleId))
            return string.Empty;

        return SupportsSdlFrontend(exePath) ? $"-r {titleId} {SdlFrontendOption} sdl" : $"-r {titleId}";
    }

    private static bool SupportsSdlFrontend(string exePath)
    {
        try
        {
            return File.ReadAllBytes(exePath).AsSpan().IndexOf(Encoding.ASCII.GetBytes(SdlFrontendOption)) >= 0;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
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