using System.IO;
using System.Net.Http;

namespace VitaForge.Core.Services.PSVita;

public static class HttpFileDownloader
{
    public static async Task DownloadAsync(HttpClient client, string url, string destinationPath, CancellationToken ct)
    {
        using var response = await StreamDownloader.GetAsync(client, url, ct);

        response.EnsureSuccessStatusCode();

        await using var source = await response.Content.ReadAsStreamAsync(ct);

        await StreamDownloader.CopyAsync(source, destinationPath, null, null, null, ct);
    }
}