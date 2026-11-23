using System.IO;
using System.Net.Http;

namespace VitaForge.Core.Services.PSVita;

public static class HttpFileDownloader
{
    public static async Task DownloadAsync(HttpClient client, string url, string destinationPath, Action<long>? onBytes, Action<long>? onTotal, CancellationToken ct)
    {
        using var response = await StreamDownloader.GetAsync(client, url, ct);

        response.EnsureSuccessStatusCode();

        if (response.Content.Headers.ContentLength is { } total)
            onTotal?.Invoke(total);

        await using var source = await response.Content.ReadAsStreamAsync(ct);

        await StreamDownloader.CopyAsync(source, destinationPath, null, onBytes, null, ct);
    }
}