using System.Buffers.Binary;

namespace Vita.Core.Typography;

internal static class BigEndian
{
    public static int U8(byte[] data, int offset) => data[offset];

    public static int U16(byte[] data, int offset) => BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(offset));

    public static int I16(byte[] data, int offset) => BinaryPrimitives.ReadInt16BigEndian(data.AsSpan(offset));

    public static uint U32(byte[] data, int offset) => BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(offset));

    public static int I32(byte[] data, int offset) => BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(offset));
}