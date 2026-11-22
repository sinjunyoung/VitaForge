using Common.Localization;
using Common.Models;
using Vita.Core.Models;

namespace VitaForge.Core.Services.PSVita;

public static class PatchTargetLogger
{
    public static void Log(PatchTargetRequest request, IReadOnlyList<string?> selections, Action<string, LogLevel> log)
    {
        for (int i = 0; i < request.PatchFileNames.Count; i++)
        {
            string name = request.PatchFileNames[i];
            string? selected = i < selections.Count ? selections[i] : null;

            if (selected == null)
                log(Loc.S.LogVitaPatchTargetSkipped(name), LogLevel.Info);
            else
                log(Loc.S.LogVitaPatchTargetSelected(name, selected), LogLevel.Ok);
        }
    }
}