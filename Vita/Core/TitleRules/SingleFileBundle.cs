using System.Text;

namespace Vita.Core.TitleRules;

internal sealed class SingleFileBundle
{
    private const int MinimumVersion = 2;

    private const int CompressionVersion = 6;

    private const int ScanChunkSize = 1 << 20;

    private static readonly byte[] Signature = Convert.FromHexString("8B1202B96A612038727B930214D7A03213F5B9E6EFAE3318EE3B2DCE24B36AAE");

    private SingleFileBundle(IReadOnlyList<SingleFileBundleEntry> entries) => Entries = entries;

    public IReadOnlyList<SingleFileBundleEntry> Entries { get; }

    public static SingleFileBundle? TryRead(Stream stream)
    {
        long signatureAt = FindSignature(stream);

        if (signatureAt < sizeof(long))
            return null;

        using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);

        stream.Position = signatureAt - sizeof(long);

        long headerOffset = reader.ReadInt64();

        if (headerOffset <= 0 || headerOffset >= stream.Length)
            return null;

        stream.Position = headerOffset;

        uint major = reader.ReadUInt32();

        reader.ReadUInt32();

        int count = reader.ReadInt32();

        if (major < MinimumVersion || count <= 0)
            return null;

        reader.ReadString();
        reader.ReadBytes(sizeof(long) * 5);

        var entries = new List<SingleFileBundleEntry>(count);

        for (int i = 0; i < count; i++)
        {
            long offset = reader.ReadInt64();
            long size = reader.ReadInt64();
            long compressedSize = major >= CompressionVersion ? reader.ReadInt64() : 0;
            byte type = reader.ReadByte();
            string path = reader.ReadString();

            entries.Add(new SingleFileBundleEntry(path, type, offset, size, compressedSize));
        }

        return new SingleFileBundle(entries);
    }

    public SingleFileBundleEntry? FindAssembly(string? name)
    {
        var assemblies = Entries.Where(e => e.IsAssembly).ToList();

        return assemblies.FirstOrDefault(e => name is not null && e.Path.Equals(name, StringComparison.OrdinalIgnoreCase))
            ?? assemblies.OrderByDescending(e => e.Size).FirstOrDefault();
    }

    private static long FindSignature(Stream stream)
    {
        byte[] buffer = new byte[ScanChunkSize + Signature.Length];
        long bufferStart = 0;
        int carry = 0;

        stream.Position = 0;

        while (true)
        {
            int read = stream.Read(buffer, carry, ScanChunkSize);

            if (read <= 0)
                return -1;

            int total = carry + read;
            int index = buffer.AsSpan(0, total).IndexOf(Signature);

            if (index >= 0)
                return bufferStart + index;

            carry = Math.Min(Signature.Length - 1, total);

            Array.Copy(buffer, total - carry, buffer, 0, carry);

            bufferStart += total - carry;
        }
    }
}
