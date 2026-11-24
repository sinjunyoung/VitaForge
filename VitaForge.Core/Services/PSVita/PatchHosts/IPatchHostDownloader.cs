using Common.Models;
using VitaForge.Core.Models.PSVita;

namespace VitaForge.Core.Services.PSVita.PatchHosts;

public interface IPatchHostDownloader
{
    bool CanHandle(string url);

    Task DownloadAsync(PatchLink patchLink, string destinationPath, PatchDownloadProgress progress, Action<string, LogLevel> log, CancellationToken ct);
}