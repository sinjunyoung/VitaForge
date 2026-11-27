using Common.Models;
using Vita.Core.Models;
using Vita.Core.Services;

namespace Vita.Core.TitleRules;

internal static class GroupSourceReader
{
    private static readonly FselfLayout[] SelfLayouts = [FselfLayout.Standard, FselfLayout.Vita3K];

    public static async Task<byte[]> ReadFileAsync(PatchAppEntry appEntry, Action<long> onBytes, CancellationToken ct)
    {
        var entry = appEntry.FileEntry;

        if (entry.Size > int.MaxValue)
            throw new NotSupportedException($"file is too large to load in memory: {entry.RelativePath ?? entry.Name}");

        var buffer = new MemoryStream((int)entry.Size);

        await DecryptToAsync(appEntry, buffer, onBytes, ct);

        return buffer.Length == buffer.Capacity ? buffer.GetBuffer() : buffer.ToArray();
    }

    public static async Task<byte[]> ReadFileWithCapacityAsync(PatchAppEntry appEntry, long capacity, Action<long> onBytes, CancellationToken ct)
    {
        var entry = appEntry.FileEntry;

        if (capacity > int.MaxValue || capacity < entry.Size)
            throw new NotSupportedException($"file is too large to load in memory: {entry.RelativePath ?? entry.Name}");

        var buffer = new MemoryStream((int)capacity);

        await DecryptToAsync(appEntry, buffer, onBytes, ct);

        if (buffer.Length != entry.Size)
            throw new InvalidDataException($"unexpected decrypted size of {entry.RelativePath ?? entry.Name} (expected {entry.Size:N0} bytes, got {buffer.Length:N0} bytes)");

        return buffer.GetBuffer();
    }

    public static async Task<byte[]> ReadDecryptedSelfAsync(PatchAppEntry appEntry, Func<byte[], bool> accepts, string expected, string label, Action<string, LogLevel> log, Action<long> onBytes, CancellationToken ct)
    {
        byte[] raw = await ReadFileAsync(appEntry, onBytes, ct);

        if (accepts(raw))
            return raw;

        foreach (var layout in SelfLayouts)
        {
            byte[] decrypted = VitaSelfDecryptor.DecryptToFself(raw, appEntry.Owner.License.Klicensee, layout);

            if (!accepts(decrypted))
                continue;

            log($"{label}: decrypted with {layout} layout", LogLevel.Info);

            return decrypted;
        }

        throw new InvalidDataException($"no decrypted layout matches the expected source ({expected})");
    }

    private static Task DecryptToAsync(PatchAppEntry appEntry, Stream destination, Action<long> onBytes, CancellationToken ct)
    {
        var owner = appEntry.Owner;

        return Task.Run(() => NoNpDrmDecryptor.DecryptEntryTo(owner.Item.Accessor!, owner.Item.SourcePath, owner.License.Klicensee, appEntry.FileEntry, owner.Table.UnicvEntries[appEntry.EntryIndex], owner.Table.FilesSalt, destination, delta =>
        {
            ct.ThrowIfCancellationRequested();

            onBytes(delta);
        }), ct);
    }
}