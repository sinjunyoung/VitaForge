using Common.Localization;
using Common.Models;
using System.IO;
using Vita.Core.Models;
using VitaForge.Core.Models.PSVita;

namespace VitaForge.Core.Services.PSVita;

public static class NpsDownloadInstallService
{
    public static async Task<int> RunAsync(IReadOnlyList<NpsCatalogItem> items, OutputTarget target, string targetPath, Action<string, LogLevel> log, IProgress<ProgressInfo>? progress, CancellationToken ct)
    {
        string destination = InstallDestination.Resolve(target, targetPath, log);
        string workDir = InstallWorkspace.Create();

        try
        {
            var outputs = await NpsDownloadConvertService.RunAsync(items, workDir, PkgConvertOutput.Folder, target, log, progress, ct);

            log(Loc.S.StatusInstalling, LogLevel.Highlight);

            foreach (string output in outputs)
            {
                ct.ThrowIfCancellationRequested();

                await FolderInstaller.MergeAsync(output, destination, progress, ct);

                log(Loc.S.LogPsvPatchInstalled(Path.GetFileName(output), destination), LogLevel.Ok);
            }

            return outputs.Count;
        }
        finally
        {
            InstallWorkspace.Delete(workDir);
            InstallWorkspace.DeleteRootIfEmpty();
        }
    }
}