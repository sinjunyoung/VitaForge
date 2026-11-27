using System.Buffers.Binary;

namespace Vita.Core.Cpk;

internal sealed class CpkArchive
{
    private readonly Dictionary<string, CpkEntry> entriesByPath = new(StringComparer.OrdinalIgnoreCase);

    private CpkArchive(byte[] data, int length)
    {
        Data = data;
        Length = length;
    }

    public byte[] Data { get; }

    public int Length { get; }

    public bool IsEncrypted { get; private init; }

    public CriUtfTable Header { get; private init; } = null!;

    public CriUtfTable Toc { get; private init; } = null!;

    public int HeaderTableSize { get; private init; }

    public int TocPosition { get; private init; }

    public int TocTableSize { get; private init; }

    public long ContentOffset { get; private init; }

    public long AddOffset { get; private init; }

    public long Align { get; private init; }

    public long EtocOffset { get; private init; }

    public IReadOnlyList<CpkEntry> Entries { get; private init; } = [];

    public static CpkArchive Open(byte[] data, int length)
    {
        if (length < 0x20 || length > data.Length || !data.AsSpan(0, 4).SequenceEqual("CPK "u8))
            throw new InvalidDataException("not a CPK archive");

        bool encrypted = !data.AsSpan(0x10, 4).SequenceEqual("@UTF"u8);
        int headerSize = ReadTableSize(data, length, 0);
        var header = ParseTable(data, 0, headerSize, encrypted);

        long tocOffset = header.GetInteger(0, "TocOffset");
        long contentOffset = header.GetInteger(0, "ContentOffset");
        long align = header.GetInteger(0, "Align");

        if (tocOffset <= 0 || tocOffset + 0x10 > length)
            throw new InvalidDataException("CPK has no TOC");

        if (!data.AsSpan((int)tocOffset, 4).SequenceEqual("TOC "u8))
            throw new InvalidDataException("invalid CPK TOC signature");

        int tocSize = ReadTableSize(data, length, (int)tocOffset);
        var toc = ParseTable(data, (int)tocOffset, tocSize, encrypted);
        long addOffset = Math.Min(contentOffset, tocOffset);
        var entries = new List<CpkEntry>(toc.RowCount);

        for (int row = 0; row < toc.RowCount; row++)
        {
            string directory = toc.GetString(row, "DirName");
            string name = toc.GetString(row, "FileName");

            entries.Add(new CpkEntry(row, directory.Length > 0 ? $"{directory}/{name}" : name, toc.GetInteger(row, "FileOffset") + addOffset, toc.GetInteger(row, "FileSize"), toc.GetInteger(row, "ExtractSize")));
        }

        var archive = new CpkArchive(data, length)
        {
            IsEncrypted = encrypted,
            Header = header,
            Toc = toc,
            HeaderTableSize = headerSize,
            TocPosition = (int)tocOffset,
            TocTableSize = tocSize,
            ContentOffset = contentOffset,
            AddOffset = addOffset,
            Align = align > 0 ? align : 2048,
            EtocOffset = header.GetInteger(0, "EtocOffset"),
            Entries = entries
        };

        foreach (var entry in entries)
            archive.entriesByPath.TryAdd(entry.Path, entry);

        return archive;
    }

    public CpkEntry? Find(string path) => entriesByPath.GetValueOrDefault(path);

    public byte[] Extract(CpkEntry entry)
    {
        if (entry.Offset < 0 || entry.FileSize < 0 || entry.Offset + entry.FileSize > Length)
            throw new InvalidDataException($"CPK entry is out of range: {entry.Path}");

        var block = Data.AsSpan((int)entry.Offset, (int)entry.FileSize);

        return CriLayla.IsCompressed(block) ? CriLayla.Decompress(block) : block.ToArray();
    }

    private static int ReadTableSize(byte[] data, int length, int position)
    {
        if (position + 0x10 > length)
            throw new InvalidDataException("truncated CPK chunk");

        int size = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(position + 8)));

        if (size < 0 || (long)position + 0x10 + size > length)
            throw new InvalidDataException("truncated CPK table");

        return size;
    }

    private static CriUtfTable ParseTable(byte[] data, int position, int size, bool encrypted)
    {
        var table = data.AsSpan(position + 0x10, size);

        return CriUtfTable.Parse(encrypted ? CriCipher.Decrypt(table) : table.ToArray());
    }
}