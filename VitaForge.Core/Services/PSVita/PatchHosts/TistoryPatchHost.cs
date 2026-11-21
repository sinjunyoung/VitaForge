using Common.Localization;
using Common.Models;
using System.IO;

namespace VitaForge.Core.Services.PSVita.PatchHosts;

public sealed class TistoryPatchHost : IPatchHostDownloader
{
    public bool CanHandle(string url) => Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Host.EndsWith(".tistory.com", StringComparison.OrdinalIgnoreCase);

    public async Task DownloadAsync(string url, string destinationPath, Action<string, LogLevel> log, CancellationToken ct)
    {
        string attachmentUrl = await TistoryAttachmentFinder.FindFirstAsync(url, ct) ?? throw new InvalidOperationException(Loc.S.ErrPsvPatchTistoryNoAttachment(url));

        log(Loc.S.LogPsvPatchTistoryAttachment(Path.GetFileName(new Uri(attachmentUrl).LocalPath)), LogLevel.Info);

        await HttpFileDownloader.DownloadAsync(TistoryAttachmentFinder.Client, attachmentUrl, destinationPath, ct);
    }
}