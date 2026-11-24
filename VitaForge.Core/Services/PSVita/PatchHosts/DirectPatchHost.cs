using Common.Models;
using VitaForge.Core.Models.PSVita;

namespace VitaForge.Core.Services.PSVita.PatchHosts;

public sealed class DirectPatchHost : IPatchHostDownloader
{
    public bool CanHandle(string url) => true;

    public async Task DownloadAsync(PatchLink patchLink, string destinationPath, PatchDownloadProgress progress, Action<string, LogLevel> log, CancellationToken ct)
        => await NpsDownloadService.DownloadAsync(patchLink.Url, destinationPath, progress.AddBytes, progress.SetTotal, ct);
}