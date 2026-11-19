using System.IO;
using System.Net.Http;
using System.Security.Cryptography;

namespace VitaForge.Core.Services.PSVita;

public static class NpsDownloadService
{
    private const int BufferSize = 81920;

    private static readonly HttpClient Client = new() { Timeout = Timeout.InfiniteTimeSpan };

    public static async Task<string> DownloadAsync(string url, string destinationPath, Action<long> onBytes, CancellationToken ct)
    {
        using var response = await Client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);

        response.EnsureSuccessStatusCode();

        await using var source = await response.Content.ReadAsStreamAsync(ct);
        await using var target = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, BufferSize, true);

        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

        var buffer = new byte[BufferSize];
        int read;

        while ((read = await source.ReadAsync(buffer, ct)) > 0)
        {
            await target.WriteAsync(buffer.AsMemory(0, read), ct);

            hash.AppendData(buffer, 0, read);
            onBytes(read);
        }

        return Convert.ToHexString(hash.GetHashAndReset());
    }
}
