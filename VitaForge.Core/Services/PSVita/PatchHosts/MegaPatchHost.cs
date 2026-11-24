using Common.Localization;
using Common.Models;
using VitaForge.Core.Models.PSVita;

namespace VitaForge.Core.Services.PSVita.PatchHosts;

public sealed class MegaPatchHost : IPatchHostDownloader
{
    public bool CanHandle(string url) => MegaLink.TryParse(url, out _);

    public async Task DownloadAsync(PatchLink patchLink, string destinationPath, PatchDownloadProgress progress, Action<string, LogLevel> log, CancellationToken ct)
    {
        if (!MegaLink.TryParse(patchLink.Url, out var link))
            throw new NotSupportedException(Loc.S.ErrVitaUnsupportedSourceType(patchLink.Url));

        await MegaDownloader.DownloadAsync(link, destinationPath, progress.AddBytes, progress.SetTotal, ct);
    }
}