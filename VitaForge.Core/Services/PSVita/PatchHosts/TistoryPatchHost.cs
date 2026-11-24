using Common.Localization;
using Common.Models;
using VitaForge.Core.Models.PSVita;

namespace VitaForge.Core.Services.PSVita.PatchHosts;

public sealed class TistoryPatchHost : IPatchHostDownloader
{
    public bool CanHandle(string url) => Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Host.EndsWith(".tistory.com", StringComparison.OrdinalIgnoreCase);

    public async Task DownloadAsync(PatchLink patchLink, string destinationPath, PatchDownloadProgress progress, Action<string, LogLevel> log, CancellationToken ct)
    {
        var attachments = await TistoryAttachmentFinder.FindAllAsync(patchLink.Url, ct);

        if (attachments.Count == 0)
            throw new InvalidOperationException(Loc.S.ErrPsvPatchTistoryNoAttachment(patchLink.Url));

        var attachment = patchLink.HasSelector
            ? PatchFileSelector.Select(attachments, a => [a.Name, a.UrlName], patchLink)
            : attachments.OrderByDescending(a => a.Priority).First();

        log(Loc.S.LogPsvPatchTistoryAttachment(attachment.Name.Length > 0 ? attachment.Name : attachment.Url), LogLevel.Info);

        await HttpFileDownloader.DownloadAsync(TistoryAttachmentFinder.Client, attachment.Url, destinationPath, progress.AddBytes, progress.SetTotal, ct);
    }
}
