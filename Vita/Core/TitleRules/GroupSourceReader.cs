using Common.Models;
using Vita.Core.Models;
using Vita.Core.Services;

namespace Vita.Core.TitleRules;

internal static class GroupSourceReader
{
    private static readonly FselfLayout[] SelfLayouts = [FselfLayout.Standard, FselfLayout.Vita3K];

    public static async Task<byte[]> ReadFileAsync(PatchAppEntry appEntry, Action<long> onBytes, CancellationToken ct)
    {
        var owner = appEntry.Owner;
        var entry = appEntry.FileEntry;

        if (entry.Size > int.MaxValue)
            throw new NotSupportedException($"file is too large to load in memory: {entry.RelativePath ?? entry.Name}");

        var buffer = new MemoryStream((int)entry.Size);

        await Task.Run(() => NoNpDrmDecryptor.DecryptEntryTo(owner.Item.Accessor!, owner.Item.SourcePath, owner.License.Klicensee, entry, owner.Table.UnicvEntries[appEntry.EntryIndex], owner.Table.FilesSalt, buffer, delta =>
        {
            ct.ThrowIfCancellationRequested();

            onBytes(delta);
        }), ct);

        return buffer.Length == buffer.Capacity ? buffer.GetBuffer() : buffer.ToArray();
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
}
