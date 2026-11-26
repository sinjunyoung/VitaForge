using System.IO.Compression;
using Vita.Core.Models;
using Vita.Core.Services;

namespace Vita.Core.TitleRules;

internal sealed class DecryptedArchiveFile(FileStream stream) : IDisposable
{
    public static async Task<DecryptedArchiveFile> ExtractAsync(MergeGroup group, string archivePath, string tempDirectory, Action<long> onBytes, CancellationToken ct)
    {
        if (!group.Index.TryGetValue(archivePath, out var appEntry))
            throw new FileNotFoundException(archivePath);

        var owner = appEntry.Owner;

        Directory.CreateDirectory(tempDirectory);

        var temp = new FileStream(Path.Combine(tempDirectory, $"vf_{Guid.NewGuid():N}.pk"), FileMode.Create, FileAccess.ReadWrite, FileShare.None, 1 << 20, FileOptions.DeleteOnClose);

        try
        {
            await Task.Run(() => NoNpDrmDecryptor.DecryptEntryTo(owner.Item.Accessor!, owner.Item.SourcePath, owner.License.Klicensee, appEntry.FileEntry, owner.Table.UnicvEntries[appEntry.EntryIndex], owner.Table.FilesSalt, temp, delta =>
            {
                ct.ThrowIfCancellationRequested();

                onBytes(delta);
            }), ct);

            temp.Flush();

            return new DecryptedArchiveFile(temp);
        }
        catch
        {
            temp.Dispose();

            throw;
        }
    }

    public byte[] ReadMember(PackageManifestSource source)
    {
        stream.Seek(source.Offset, SeekOrigin.Begin);

        byte[] stored = new byte[source.StoredBytes];

        stream.ReadExactly(stored);

        return source.Compression switch
        {
            "gzip" => Gunzip(stored),
            "raw" => stored,
            _ => throw new NotSupportedException($"unsupported package compression: {source.Compression}")
        };
    }

    public void Dispose() => stream.Dispose();

    private static byte[] Gunzip(byte[] stored)
    {
        using var gzip = new GZipStream(new MemoryStream(stored, writable: false), CompressionMode.Decompress);
        using var output = new MemoryStream();

        gzip.CopyTo(output);

        return output.ToArray();
    }
}
