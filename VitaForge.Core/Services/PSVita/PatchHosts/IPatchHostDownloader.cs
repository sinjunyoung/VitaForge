using Common.Models;

namespace VitaForge.Core.Services.PSVita.PatchHosts;

public interface IPatchHostDownloader
{
    bool CanHandle(string url);

    Task DownloadAsync(string url, string destinationPath, PatchDownloadProgress progress, Action<string, LogLevel> log, CancellationToken ct);
}