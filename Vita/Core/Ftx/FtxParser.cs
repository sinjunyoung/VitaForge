using System.Buffers.Binary;

namespace Vita.Core.Ftx;

internal static class FtxParser
{
    public static List<FtxTexture> ParseAll(ReadOnlySpan<byte> data)
    {
        var result = new List<FtxTexture>();
        int position = 0;
        int blockOrdinal = -1;

        while (position <= data.Length)
        {
            int found = data[position..].IndexOf("GXT\0"u8);

            if (found < 0)
                break;

            int gxt = position + found;

            if (gxt + 12 > data.Length)
                break;

            uint textureCount = BinaryPrimitives.ReadUInt32LittleEndian(data[(gxt + 8)..]);

            if (textureCount == 0 || textureCount > 256)
            {
                position = gxt + 4;

                continue;
            }

            blockOrdinal++;

            for (int i = 0; i < textureCount; i++)
            {
                int entry = gxt + 0x20 + i * 32;

                if (entry + 32 > data.Length)
                    break;

                int width = BinaryPrimitives.ReadUInt16LittleEndian(data[(entry + 24)..]);
                int height = BinaryPrimitives.ReadUInt16LittleEndian(data[(entry + 26)..]);

                if (width == 0 || height == 0)
                    continue;

                result.Add(new FtxTexture(gxt, blockOrdinal, i, (int)BinaryPrimitives.ReadUInt32LittleEndian(data[entry..]), (int)BinaryPrimitives.ReadUInt32LittleEndian(data[(entry + 4)..]), BinaryPrimitives.ReadUInt32LittleEndian(data[(entry + 20)..]), width, height, BinaryPrimitives.ReadUInt16LittleEndian(data[(entry + 28)..])));
            }

            position = gxt + 4;
        }

        return result;
    }
}