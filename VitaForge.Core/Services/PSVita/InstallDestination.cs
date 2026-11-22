using Common.Localization;
using Common.Models;
using System.IO;
using Vita.Core.Models;

namespace VitaForge.Core.Services.PSVita;

public static class InstallDestination
{
    public static string Resolve(OutputTarget target, string targetPath, Action<string, LogLevel> log)
    {
        if (target != OutputTarget.Emu)
            return targetPath;

        var info = Vita3kLocator.Resolve(targetPath);

        log(Loc.S.LogPsvPatchVita3kPrefPath(info.PrefPath), LogLevel.Info);

        return Path.Combine(info.PrefPath, "ux0");
    }
}