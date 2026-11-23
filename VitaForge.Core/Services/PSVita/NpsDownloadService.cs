using Common.Localization;
using Common.Models;
using VitaForge.Core.Models.PSVita;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;

namespace VitaForge.Core.Services.PSVita;

public static class NpsDownloadService
{
    private static readonly HttpClient Client = new() { Timeout = Timeout.InfiniteTimeSpan };

    public static async Task DownloadVerifiedAsync(NpsCatalogItem item, string destinationPath, Action<long> onBytes, Action<string, LogLevel> log, bool failOnMismatch, CancellationToken ct)
    {
        string hash = await DownloadAsync(item.Url, destinationPath, onBytes, null, ct);

        if (!string.IsNullOrEmpty(item.Sha256) && !hash.Equals(item.Sha256, StringComparison.OrdinalIgnoreCase))
        {
            if (failOnMismatch)
                throw new InvalidDataException(Loc.S.LogNpsHashMismatch(item.Name));

            log(Loc.S.LogNpsHashMismatch(item.Name), LogLevel.Error);
        }
    }

    public static async Task<string> DownloadAsync(string url, string destinationPath, Action<long> onBytes, Action<long>? onTotal, CancellationToken ct)
    {
        using var response = await StreamDownloader.GetAsync(Client, url, ct);

        response.EnsureSuccessStatusCode();

        if (response.Content.Headers.ContentLength is { } total)
            onTotal?.Invoke(total);

        await using var source = await response.Content.ReadAsStreamAsync(ct);

        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

        await StreamDownloader.CopyAsync(source, destinationPath, response.Content.Headers.ContentLength, onBytes, (buffer, count) => hash.AppendData(buffer, 0, count), ct);

        return Convert.ToHexString(hash.GetHashAndReset());
    }
}
