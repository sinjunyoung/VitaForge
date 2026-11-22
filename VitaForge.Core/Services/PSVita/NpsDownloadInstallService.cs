using Common.Models;
using Vita.Core.Models;
using Vita.Core.Services;
using VitaForge.Core.Models.PSVita;

namespace VitaForge.Core.Services.PSVita;

public static class NpsDownloadInstallService
{
    public static async Task<int> RunAsync(IReadOnlyList<NpsCatalogItem> items, OutputTarget target, string targetPath, Action<string, LogLevel> log, IProgress<ProgressInfo>? progress, CancellationToken ct)
    {
        string destination = InstallDestination.Resolve(target, targetPath, log);
        var tracker = new WrittenFileTracker();

        try
        {
            var outputs = await NpsDownloadConvertService.RunAsync(items, destination, PkgConvertOutput.Direct, target, log, progress, ct, tracker);
            return outputs.Count;
        }
        catch
        {
            tracker.Rollback();

            throw;
        }
        finally
        {
            InstallWorkspace.DeleteRootIfEmpty();
        }
    }
}