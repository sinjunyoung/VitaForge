using Common.Models;
using System.IO;
using VitaForge.Core.Services.PSVita.PatchHosts;

namespace VitaForge.Core.Services.PSVita;

public static class PatchFileDownloader
{
    public static async Task<string> DownloadAsync(string url, string workDir, PatchDownloadProgress progress, Action<string, LogLevel> log, CancellationToken ct)
    {
        string rawPath = Path.Combine(workDir, "patch.download");

        await PatchHostDownloaderRegistry.Resolve(url).DownloadAsync(url, rawPath, progress, log, ct);

        return await PatchArchivePreparer.PrepareAsync(rawPath, workDir, url, ct);
    }
}