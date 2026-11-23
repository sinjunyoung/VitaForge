using Common.Models;

namespace VitaForge.Core.Services.PSVita.PatchHosts;

public sealed class DirectPatchHost : IPatchHostDownloader
{
    public bool CanHandle(string url) => true;

    public async Task DownloadAsync(string url, string destinationPath, PatchDownloadProgress progress, Action<string, LogLevel> log, CancellationToken ct)
        => await NpsDownloadService.DownloadAsync(url, destinationPath, progress.AddBytes, progress.SetTotal, ct);
}