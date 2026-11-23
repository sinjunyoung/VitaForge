using Common.Localization;
using System.IO;
using System.Net.Http;

namespace VitaForge.Core.Services.PSVita;

public static class StreamDownloader
{
    private const int BufferSize = 81920;

    private static readonly TimeSpan StallTimeout = TimeSpan.FromSeconds(60);

    public static async Task<HttpResponseMessage> GetAsync(HttpClient client, string url, CancellationToken ct)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);

        timeout.CancelAfter(StallTimeout);

        try
        {
            return await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new TimeoutException(Loc.S.ErrDownloadStalled);
        }
    }

    public static async Task CopyAsync(Stream source, string destinationPath, long? expectedLength, Action<long>? onBytes, Action<byte[], int>? process, CancellationToken ct)
    {
        try
        {
            await using var target = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, BufferSize, true);

            var buffer = new byte[BufferSize];
            long total = 0;

            while (true)
            {
                int read = await ReadFullAsync(source, buffer, ct);

                if (read == 0)
                    break;

                process?.Invoke(buffer, read);

                await target.WriteAsync(buffer.AsMemory(0, read), ct);

                total += read;

                onBytes?.Invoke(read);
            }

            if (expectedLength is { } expected && total != expected)
                throw new IOException(Loc.S.ErrDownloadIncomplete(total, expected));
        }
        catch
        {
            DeleteQuietly(destinationPath);

            throw;
        }
    }

    private static async Task<int> ReadFullAsync(Stream source, byte[] buffer, CancellationToken ct)
    {
        int total = 0;

        while (total < buffer.Length)
        {
            int read = await ReadWithStallAsync(source, buffer.AsMemory(total), ct);

            if (read == 0)
                break;

            total += read;
        }

        return total;
    }

    private static async Task<int> ReadWithStallAsync(Stream source, Memory<byte> memory, CancellationToken ct)
    {
        using var stall = CancellationTokenSource.CreateLinkedTokenSource(ct);

        stall.CancelAfter(StallTimeout);

        try
        {
            return await source.ReadAsync(memory, stall.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new TimeoutException(Loc.S.ErrDownloadStalled);
        }
    }

    private static void DeleteQuietly(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch { }
    }
}
