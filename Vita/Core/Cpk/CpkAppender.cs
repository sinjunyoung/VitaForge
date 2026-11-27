namespace Vita.Core.Cpk;

internal static class CpkAppender
{
    public static byte[] AppendWithoutEtoc(CpkArchive source, IReadOnlyDictionary<string, byte[]> replacements, CancellationToken ct)
    {
        long align = source.Align;
        int insertPoint = source.EtocOffset > 0 ? checked((int)source.EtocOffset) : source.Length;

        if (insertPoint > source.Length)
            throw new InvalidDataException("CPK ETOC offset is out of range");

        var placements = new List<(CpkEntry Entry, byte[] Raw, byte[] Block, long Offset)>();
        long cursor = AlignUp(insertPoint, align);

        foreach (var entry in source.Entries)
        {
            ct.ThrowIfCancellationRequested();

            if (!replacements.TryGetValue(entry.Path, out byte[]? raw))
                continue;

            byte[] block = CriLayla.Compress(raw);

            placements.Add((entry, raw, block, cursor));

            cursor = AlignUp(cursor + block.Length, align);
        }

        byte[] output = new byte[checked((int)cursor)];

        Buffer.BlockCopy(source.Data, 0, output, 0, insertPoint);

        var toc = source.Toc.Clone();

        foreach (var (entry, raw, block, offset) in placements)
        {
            block.CopyTo(output, offset);

            toc.SetInteger(entry.RowIndex, "FileOffset", offset - source.AddOffset);
            toc.SetInteger(entry.RowIndex, "FileSize", block.Length);
            toc.SetInteger(entry.RowIndex, "ExtractSize", raw.Length);
        }

        WriteTable(output, source.TocPosition, toc.Data, source.IsEncrypted);

        var header = source.Header.Clone();

        header.SetInteger(0, "ContentSize", cursor - source.ContentOffset);
        header.SetInteger(0, "EtocOffset", 0);
        header.SetInteger(0, "EtocSize", 0);

        WriteTable(output, 0, header.Data, source.IsEncrypted);

        return output;
    }

    private static void WriteTable(byte[] output, int position, byte[] table, bool encrypted)
    {
        byte[] bytes = encrypted ? CriCipher.Decrypt(table) : table;

        bytes.CopyTo(output, position + 0x10);
    }

    private static long AlignUp(long value, long align) => (value + align - 1) / align * align;
}