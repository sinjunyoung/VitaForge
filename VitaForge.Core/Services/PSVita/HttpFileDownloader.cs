using System.IO;
using System.Net.Http;

namespace VitaForge.Core.Services.PSVita;

public static class HttpFileDownloader
{
    private const int BufferSize = 81920;

    public static async Task DownloadAsync(HttpClient client, string url, string destinationPath, CancellationToken ct)
    {
        using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);

        response.EnsureSuccessStatusCode();

        await using var source = await response.Content.ReadAsStreamAsync(ct);
        await using var target = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, BufferSize, true);

        await source.CopyToAsync(target, BufferSize, ct);
    }
}