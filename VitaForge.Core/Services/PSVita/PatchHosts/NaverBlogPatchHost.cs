using Common.Localization;
using Common.Models;
using VitaForge.Core.Models.PSVita;

namespace VitaForge.Core.Services.PSVita.PatchHosts;

public sealed class NaverBlogPatchHost : IPatchHostDownloader
{
    public bool CanHandle(string url) => Uri.TryCreate(url, UriKind.Absolute, out var uri) && (uri.Host.Equals("blog.naver.com", StringComparison.OrdinalIgnoreCase) || uri.Host.Equals("m.blog.naver.com", StringComparison.OrdinalIgnoreCase));

    public async Task DownloadAsync(PatchLink patchLink, string destinationPath, PatchDownloadProgress progress, Action<string, LogLevel> log, CancellationToken ct)
    {
        var attachments = await NaverBlogAttachmentFinder.FindAllAsync(patchLink.Url, ct);

        if (attachments.Count == 0)
            throw new InvalidOperationException(Loc.S.ErrPsvPatchNaverBlogNoAttachment(patchLink.Url));

        var attachment = patchLink.HasSelector ? PatchFileSelector.Select(attachments, a => [a.Name], patchLink) : attachments[0];

        log(Loc.S.LogPsvPatchNaverBlogAttachment(attachment.Name), LogLevel.Info);

        await HttpFileDownloader.DownloadAsync(NaverBlogAttachmentFinder.Client, attachment.Url, destinationPath, progress.AddBytes, progress.SetTotal, ct);
    }
}