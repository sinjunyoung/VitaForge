using Common.Localization;
using Common.Models;
using Vita.Core.Services;

namespace VitaForge.Core.Services.PSVita;

public static class InstallRollback
{
    public static void Run(WrittenFileTracker tracker, Action<string, LogLevel> log)
    {
        int deleted = tracker.Rollback();

        if (deleted > 0)
            log(Loc.S.LogVitaRollbackDeleted(deleted), LogLevel.Info);
    }
}