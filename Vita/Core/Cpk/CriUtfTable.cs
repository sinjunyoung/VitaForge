using System.Buffers.Binary;
using System.Text;

namespace Vita.Core.Cpk;

internal sealed class CriUtfTable
{
    private const int StorageZero = 0x10;
    private const int StorageConstant = 0x30;
    private const int StoragePerRow = 0x50;

    private static readonly int[] TypeSizes = [1, 1, 2, 2, 4, 4, 8, 8, 4, 8, 4, 8];

    private readonly Dictionary<string, CriUtfColumn> columns = new(StringComparer.Ordinal);
    private readonly int rowsOffset;
    private readonly int stringOffset;
    private readonly int rowLength;

    private CriUtfTable(byte[] data)
    {
        if (data.Length < 32 || !data.AsSpan(0, 4).SequenceEqual("@UTF"u8))
            throw new InvalidDataException("invalid UTF table signature");

        Data = data;

        rowsOffset = BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(8)) + 8;
        stringOffset = BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(12)) + 8;

        int columnCount = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(24));

        rowLength = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(26));
        RowCount = BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(28));

        int offset = 32;
        int rowCursor = 0;

        for (int i = 0; i < columnCount; i++)
        {
            int flags = data[offset++];

            if (flags == 0)
                flags = data[offset++];

            string name = ReadString(BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(offset)));

            offset += 4;

            int type = flags & 0x0F;
            int storage = flags & 0xF0;

            if (type >= TypeSizes.Length)
                throw new InvalidDataException($"unknown UTF column type {type}");

            int constantOffset = -1;
            int rowOffset = -1;

            if (storage == StorageConstant)
            {
                constantOffset = offset;
                offset += TypeSizes[type];
            }
            else if (storage == StoragePerRow)
            {
                rowOffset = rowCursor;
                rowCursor += TypeSizes[type];
            }

            columns[name] = new CriUtfColumn(name, type, storage, constantOffset, rowOffset);
        }
    }

    public byte[] Data { get; }

    public int RowCount { get; }

    public static CriUtfTable Parse(byte[] data) => new(data);

    public CriUtfTable Clone() => new(Data.ToArray());

    public bool HasColumn(string name) => columns.ContainsKey(name);

    public string GetString(int row, string name)
    {
        if (!columns.TryGetValue(name, out var column) || column.Storage == StorageZero || column.Type != 0x0A)
            return string.Empty;

        return ReadString(BinaryPrimitives.ReadInt32BigEndian(Data.AsSpan(ValueOffset(row, column))));
    }

    public long GetInteger(int row, string name)
    {
        if (!columns.TryGetValue(name, out var column) || column.Storage == StorageZero)
            return 0;

        var span = Data.AsSpan(ValueOffset(row, column));

        return column.Type switch
        {
            0 => span[0],
            1 => (sbyte)span[0],
            2 => BinaryPrimitives.ReadUInt16BigEndian(span),
            3 => BinaryPrimitives.ReadInt16BigEndian(span),
            4 => BinaryPrimitives.ReadUInt32BigEndian(span),
            5 => BinaryPrimitives.ReadInt32BigEndian(span),
            6 => (long)BinaryPrimitives.ReadUInt64BigEndian(span),
            7 => BinaryPrimitives.ReadInt64BigEndian(span),
            _ => throw new NotSupportedException($"UTF column {name} is not an integer")
        };
    }

    public void SetInteger(int row, string name, long value)
    {
        if (!columns.TryGetValue(name, out var column) || column.Storage == StorageZero)
            return;

        var span = Data.AsSpan(ValueOffset(row, column));

        switch (column.Type)
        {
            case 4:
            case 5:
                BinaryPrimitives.WriteUInt32BigEndian(span, checked((uint)value));

                break;
            case 6:
            case 7:
                BinaryPrimitives.WriteUInt64BigEndian(span, checked((ulong)value));

                break;
            default:
                throw new NotSupportedException($"UTF column {name} has unsupported type {column.Type}");
        }
    }

    private int ValueOffset(int row, CriUtfColumn column)
    {
        int offset = column.Storage == StorageConstant ? column.ConstantOffset : rowsOffset + row * rowLength + column.RowOffset;

        if (offset < 0 || offset >= Data.Length)
            throw new InvalidDataException($"UTF field {column.Name} is out of range");

        return offset;
    }

    private string ReadString(int offset)
    {
        int start = stringOffset + offset;
        int end = Array.IndexOf(Data, (byte)0, start);

        if (start < 0 || end < 0)
            throw new InvalidDataException("invalid UTF string offset");

        return Encoding.UTF8.GetString(Data, start, end - start);
    }
}