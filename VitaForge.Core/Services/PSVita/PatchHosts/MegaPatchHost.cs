using Common.Localization;
using Common.Models;

namespace VitaForge.Core.Services.PSVita.PatchHosts;

public sealed class MegaPatchHost : IPatchHostDownloader
{
    public bool CanHandle(string url) => MegaLink.TryParse(url, out _);

    public async Task DownloadAsync(string url, string destinationPath, Action<string, LogLevel> log, CancellationToken ct)
    {
        if (!MegaLink.TryParse(url, out var link))
            throw new NotSupportedException(Loc.S.ErrVitaUnsupportedSourceType(url));

        await MegaDownloader.DownloadAsync(link, destinationPath, _ => { }, ct);
    }
}