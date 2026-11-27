using System.Buffers.Binary;

namespace Vita.Core.Cpk;

internal static class CriLayla
{
    private const int HeaderSize = 0x10;
    private const int RawSize = 0x100;
    private const int MaxOffset = 8194;
    private const int MaxCandidates = 32;
    private const int MaxChain = 64;
    private const int RegisterLimit = 256;

    private static readonly int[] LengthBits = [2, 3, 5, 8];

    public static bool IsCompressed(ReadOnlySpan<byte> block) => block.Length >= HeaderSize && block[..8].SequenceEqual("CRILAYLA"u8);

    public static byte[] Decompress(ReadOnlySpan<byte> block)
    {
        int uncompressedSize = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(block[8..]));
        int compressedSize = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(block[12..]));

        if ((long)HeaderSize + compressedSize + RawSize > block.Length)
            throw new InvalidDataException("truncated CRILAYLA block");

        byte[] result = new byte[RawSize + uncompressedSize];

        block.Slice(HeaderSize + compressedSize, RawSize).CopyTo(result);

        if (compressedSize > 0)
            DecompressPayload(block.Slice(HeaderSize, compressedSize), result.AsSpan(RawSize));

        return result;
    }

    public static byte[] Compress(ReadOnlySpan<byte> data)
    {
        if (data.Length <= RawSize)
        {
            byte[] small = new byte[HeaderSize + RawSize];

            "CRILAYLA"u8.CopyTo(small);
            data.CopyTo(small.AsSpan(HeaderSize));

            return small;
        }

        byte[] payload = data[RawSize..].ToArray();
        byte[] compressed = CompressPayload(payload);
        byte[] result = new byte[HeaderSize + compressed.Length + RawSize];

        "CRILAYLA"u8.CopyTo(result);
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(8), (uint)payload.Length);
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(12), (uint)compressed.Length);
        compressed.CopyTo(result.AsSpan(HeaderSize));
        data[..RawSize].CopyTo(result.AsSpan(HeaderSize + compressed.Length));

        return result;
    }

    private static void DecompressPayload(ReadOnlySpan<byte> payload, Span<byte> output)
    {
        var reader = new ReverseBitReader(payload);
        int position = output.Length - 1;

        while (position >= 0)
        {
            if (reader.Read(1) == 0)
            {
                output[position--] = (byte)reader.Read(8);

                continue;
            }

            int offset = reader.Read(13) + 3;
            int length = 3;
            bool overflow = true;

            foreach (int bits in LengthBits)
            {
                int level = reader.Read(bits);

                length += level;

                if (level != (1 << bits) - 1)
                {
                    overflow = false;

                    break;
                }
            }

            if (overflow)
            {
                int level;

                do
                {
                    level = reader.Read(8);

                    length += level;
                }
                while (level == 255);
            }

            for (int i = 0; i < length && position >= 0; i++)
            {
                if (position + offset >= output.Length)
                    throw new InvalidDataException("invalid CRILAYLA back reference");

                output[position] = output[position + offset];

                position--;
            }
        }
    }

    private static byte[] CompressPayload(byte[] payload)
    {
        int size = payload.Length;
        var chains = new Dictionary<int, List<int>>();
        var writer = new BitWriter(size / 2 + 64);
        int position = size - 1;

        while (position >= 0)
        {
            int bestOffset = 0;
            int bestLength = 0;

            if (position >= 2 && chains.TryGetValue(KeyAt(payload, position), out var candidates))
            {
                for (int c = Math.Max(0, candidates.Count - MaxCandidates); c < candidates.Count; c++)
                {
                    int offset = candidates[c] - position;

                    if (offset < 3 || offset > MaxOffset)
                        continue;

                    int length = 0;

                    while (position - length >= 0 && position - length + offset < size && payload[position - length] == payload[position - length + offset])
                        length++;

                    if (length <= bestLength)
                        continue;

                    bestLength = length;
                    bestOffset = offset;

                    if (bestLength >= 256)
                        break;
                }
            }

            if (bestLength >= 3)
            {
                writer.Write(1, 1);
                writer.Write(bestOffset - 3, 13);

                WriteLength(writer, bestLength - 3);

                int registered = Math.Min(bestLength, RegisterLimit);

                for (int i = 0; i < registered; i++)
                {
                    if (position - i >= 2)
                        Register(chains, payload, position - i);
                }

                position -= bestLength;

                continue;
            }

            writer.Write(0, 1);
            writer.Write(payload[position], 8);

            if (position >= 2)
                Register(chains, payload, position);

            position--;
        }

        return writer.ToReversedBytes();
    }

    private static void WriteLength(BitWriter writer, int remaining)
    {
        foreach (int bits in LengthBits)
        {
            int max = (1 << bits) - 1;

            if (remaining < max)
            {
                writer.Write(remaining, bits);

                return;
            }

            writer.Write(max, bits);

            remaining -= max;
        }

        while (remaining >= 255)
        {
            writer.Write(255, 8);

            remaining -= 255;
        }

        writer.Write(remaining, 8);
    }

    private static int KeyAt(byte[] payload, int position) => (payload[position] << 16) | (payload[position - 1] << 8) | payload[position - 2];

    private static void Register(Dictionary<int, List<int>> chains, byte[] payload, int position)
    {
        int key = KeyAt(payload, position);

        if (!chains.TryGetValue(key, out var chain))
        {
            chain = [];
            chains[key] = chain;
        }

        chain.Add(position);

        if (chain.Count > MaxChain)
            chain.RemoveRange(0, chain.Count - MaxChain);
    }
}