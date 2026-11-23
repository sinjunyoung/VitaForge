using Common.Localization;
using System.Buffers.Binary;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using VitaForge.Core.Models.PSVita;

namespace VitaForge.Core.Services.PSVita;

public static class MegaDownloader
{
    private const string ApiUrl = "https://g.api.mega.co.nz/cs";
    private const int BufferSize = 81920;
    private const int ApiAttempts = 3;
    private const int TemporaryErrorCode = -3;

    private static readonly HttpClient Client = new() { Timeout = Timeout.InfiniteTimeSpan };

    public static async Task DownloadAsync(MegaFileLink link, string destinationPath, Action<long> onBytes, Action<long>? onTotal, CancellationToken ct)
    {
        string downloadUrl = await GetDownloadUrlAsync(link.NodeId, ct);

        byte[] aesKey = new byte[16];

        for (int i = 0; i < aesKey.Length; i++)
            aesKey[i] = (byte)(link.Key[i] ^ link.Key[16 + i]);

        byte[] nonce = link.Key[16..24];

        using var aes = Aes.Create();

        aes.Key = aesKey;

        using var response = await StreamDownloader.GetAsync(Client, downloadUrl, ct);

        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException(Loc.S.ErrPsvPatchMegaFailed((int)response.StatusCode));

        if (response.Content.Headers.ContentLength is { } total)
            onTotal?.Invoke(total);

        await using var source = await response.Content.ReadAsStreamAsync(ct);

        var counterBlocks = new byte[BufferSize];
        var keystream = new byte[BufferSize];
        long blockIndex = 0;

        await StreamDownloader.CopyAsync(source, destinationPath, response.Content.Headers.ContentLength, onBytes, (buffer, read) =>
        {
            int blocks = (read + 15) / 16;

            FillCounterBlocks(counterBlocks, nonce, blockIndex, blocks);

            aes.EncryptEcb(counterBlocks.AsSpan(0, blocks * 16), keystream.AsSpan(0, blocks * 16), PaddingMode.None);

            for (int i = 0; i < read; i++)
                buffer[i] ^= keystream[i];

            blockIndex += blocks;
        }, ct);
    }

    private static void FillCounterBlocks(byte[] counterBlocks, byte[] nonce, long firstBlockIndex, int blocks)
    {
        for (int b = 0; b < blocks; b++)
        {
            int offset = b * 16;

            nonce.CopyTo(counterBlocks, offset);

            BinaryPrimitives.WriteUInt64BigEndian(counterBlocks.AsSpan(offset + 8, 8), (ulong)(firstBlockIndex + b));
        }
    }

    private static async Task<string> GetDownloadUrlAsync(string nodeId, CancellationToken ct)
    {
        int code = 0;

        for (int attempt = 0; attempt < ApiAttempts; attempt++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, $"{ApiUrl}?id={attempt + 1}")
            {
                Content = new StringContent($"[{{\"a\":\"g\",\"g\":1,\"p\":\"{nodeId}\"}}]", Encoding.UTF8, "application/json")
            };

            using var response = await Client.SendAsync(request, ct);

            response.EnsureSuccessStatusCode();

            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));

            var root = doc.RootElement;

            if (root.ValueKind == JsonValueKind.Array && root.GetArrayLength() > 0)
                root = root[0];

            if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("g", out var url) && url.ValueKind == JsonValueKind.String)
                return url.GetString()!;

            code = root.ValueKind == JsonValueKind.Number ? root.GetInt32() : root.ValueKind == JsonValueKind.Object && root.TryGetProperty("e", out var error) && error.ValueKind == JsonValueKind.Number ? error.GetInt32() : 0;

            if (code != TemporaryErrorCode)
                break;

            await Task.Delay(TimeSpan.FromSeconds(attempt + 1), ct);
        }

        throw new HttpRequestException(Loc.S.ErrPsvPatchMegaFailed(code));
    }
}