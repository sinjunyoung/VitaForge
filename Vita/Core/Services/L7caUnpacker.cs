using System.Buffers.Binary;
using System.Text;

namespace Vita.Core.Services;

internal static class L7caUnpacker
{
    private const uint Magic = 0x4143374C;
    private const int FsEntrySize = 24;
    private const int FileEntrySize = 24;
    private const int ChunkEntrySize = 8;

    private static readonly uint[] CrcTable = BuildCrcTable();

    public static IEnumerable<(string Path, byte[] Data)> Unpack(byte[] archive, string prefix, List<string> failures)
    {
        if (archive.Length < 48 || U32(archive, 0) != Magic)
            throw new InvalidDataException("L7CA 아카이브가 아닙니다.");

        int metadataOffset = I32(archive, 12);
        int fsEntries = I32(archive, 24);
        int files = I32(archive, 32);
        int stringTableSize = I32(archive, 40);

        var strings = ReadStrings(archive, stringTableSize);
        var nameToId = new Dictionary<string, int>(StringComparer.Ordinal);

        for (int i = 0; i < fsEntries; i++)
        {
            int p = metadataOffset + i * FsEntrySize;
            int id = I32(archive, p);

            if (id == -1)
                continue;

            string folder = strings.GetValueOrDefault(I32(archive, p + 8), "?");
            string file = strings.GetValueOrDefault(I32(archive, p + 12), "?");

            nameToId[$"{folder}/{file}"] = id;
        }

        int fileEntriesStart = metadataOffset + fsEntries * FsEntrySize;
        int chunkTableStart = fileEntriesStart + files * FileEntrySize;
        var targets = nameToId.Keys.Where(n => n.StartsWith(prefix, StringComparison.Ordinal) && !n.Contains('?')).Order(StringComparer.Ordinal).ToList();

        foreach (var name in targets)
        {
            byte[]? data = TryExtract(archive, fileEntriesStart + nameToId[name] * FileEntrySize, chunkTableStart, out string? error);

            if (data is null)
            {
                failures.Add($"{name}: {error}");
                continue;
            }

            yield return (name, data);
        }
    }

    private static byte[]? TryExtract(byte[] archive, int entryPos, int chunkTableStart, out string? error)
    {
        error = null;

        try
        {
            int compressedSize = I32(archive, entryPos);
            int rawSize = I32(archive, entryPos + 4);
            int chunkIndex = I32(archive, entryPos + 8);
            int chunkCount = I32(archive, entryPos + 12);
            int offset = I32(archive, entryPos + 16);
            uint crc = U32(archive, entryPos + 20);

            var output = new GrowBuffer();
            int blobPos = offset;
            int blobEnd = Math.Min(archive.Length, offset + compressedSize);

            for (int c = 0; c < chunkCount; c++)
            {
                uint sizeField = U32(archive, chunkTableStart + (chunkIndex + c) * ChunkEntrySize);
                bool compressed = (sizeField & 0x80000000) != 0;
                int length = (int)(sizeField & 0x00FFFFFF);
                int take = Math.Max(0, Math.Min(length, blobEnd - blobPos));
                var chunk = archive.AsSpan(blobPos, take);

                blobPos += length;

                if (compressed)
                    Decompress(chunk, output);
                else
                    output.Append(chunk);
            }

            if (output.Length != rawSize)
            {
                error = $"decompressed size {output.Length} != expected {rawSize}";
                return null;
            }

            var data = output.ToArray();
            uint actual = Crc32(data);

            if (actual != crc)
            {
                error = $"crc mismatch: got {actual:x8} expected {crc:x8}";
                return null;
            }

            return data;
        }
        catch (Exception ex) when (ex is IndexOutOfRangeException or ArgumentException or InvalidDataException)
        {
            error = ex.Message;
            return null;
        }
    }

    private static void Decompress(ReadOnlySpan<byte> data, GrowBuffer output)
    {
        int pos = 0;
        int n = data.Length;

        while (pos < n)
        {
            int c = data[pos++];

            if (c > 0xBF)
            {
                int length = (c - 0xBE) * 2;
                int flag = data[pos++];
                int back = ((flag & 0x7F) << 8) + data[pos++] + 1;

                if ((flag & 0x80) != 0)
                    length++;

                output.CopyBack(back, length);
            }
            else if (c > 0x7F)
            {
                int length = (c >> 2) & 0x1F;
                int back = ((c & 0x3) << 8) + data[pos++] + 1;

                if ((c & 0x80) != 0)
                    length += 3;

                output.CopyBack(back, length);
            }
            else if (c > 0x3F)
            {
                output.CopyBack((c & 0x0F) + 1, (c >> 4) - 2);
            }
            else if (c == 0)
            {
                int flag = data[pos++];
                int length = 0x40;

                if ((flag & 0x80) == 0)
                {
                    int flag2 = data[pos++];

                    length = 0xBF + flag2 + (flag << 8);

                    if (flag == 0 && flag2 == 0 && pos < n && data[pos] == 0)
                        break;
                }
                else
                    length += flag & 0x7F;

                output.Append(data.Slice(pos, Math.Max(0, Math.Min(length, n - pos))));
                pos += length;
            }
            else
            {
                output.Append(data.Slice(pos, Math.Max(0, Math.Min(c, n - pos))));
                pos += c;
            }
        }
    }

    private static Dictionary<int, string> ReadStrings(byte[] archive, int stringTableSize)
    {
        var strings = new Dictionary<int, string>();
        int baseOffset = archive.Length - stringTableSize;
        int pos = 0;

        while (pos < stringTableSize)
        {
            int length = archive[baseOffset + pos];
            int take = Math.Min(length, stringTableSize - pos - 1);

            strings[pos] = Encoding.UTF8.GetString(archive, baseOffset + pos + 1, Math.Max(0, take));
            pos += 1 + length;
        }

        return strings;
    }

    private static uint Crc32(byte[] data)
    {
        uint crc = 0xFFFFFFFF;

        foreach (byte b in data)
            crc = CrcTable[(crc ^ b) & 0xFF] ^ (crc >> 8);

        return ~crc;
    }

    private static uint[] BuildCrcTable()
    {
        var table = new uint[256];

        for (uint i = 0; i < 256; i++)
        {
            uint value = i;

            for (int k = 0; k < 8; k++)
                value = (value & 1) != 0 ? 0xEDB88320 ^ (value >> 1) : value >> 1;

            table[i] = value;
        }

        return table;
    }

    private static int I32(byte[] d, int o) => BinaryPrimitives.ReadInt32LittleEndian(d.AsSpan(o));

    private static uint U32(byte[] d, int o) => BinaryPrimitives.ReadUInt32LittleEndian(d.AsSpan(o));

    private sealed class GrowBuffer
    {
        private byte[] _data = new byte[4096];

        public int Length { get; private set; }

        public void Append(ReadOnlySpan<byte> source)
        {
            Ensure(Length + source.Length);

            source.CopyTo(_data.AsSpan(Length));

            Length += source.Length;
        }

        public void CopyBack(int back, int count)
        {
            int start = Length - back;

            if (start < 0)
                throw new InvalidDataException("invalid back reference");

            Ensure(Length + count);

            for (int i = 0; i < count; i++)
                _data[Length++] = _data[start + i];
        }

        public byte[] ToArray() => _data.AsSpan(0, Length).ToArray();

        private void Ensure(int capacity)
        {
            if (capacity <= _data.Length)
                return;

            Array.Resize(ref _data, Math.Max(capacity, _data.Length * 2));
        }
    }
}