using System.Buffers.Binary;
using System.IO.Compression;
using System.Security.Cryptography;
using Vita.Core.Cryptography;
using Vita.Core.Models;

namespace Vita.Core.Services;

internal static class VitaSelfDecryptor
{
    private const uint SceMagic = 0x00454353;
    private const int HeaderLength = 0x1000;
    private const int ElfHeaderSize = 52;
    private const int PhdrSize = 32;
    private const int SegmentInfoSize = 32;
    private const int MetadataHeaderSize = 32;
    private const int MetadataSectionSize = 48;
    private const int MetadataInfoSize = 64;
    private const uint SelfTypeApp = 8;
    private const uint EncryptionAes128Ctr = 3;
    private const uint SecureNo = 1;
    private const uint SecureYes = 2;
    private const ulong MaxVersion = ulong.MaxValue;

    private sealed record KeyEntry(ulong MinVersion, ulong MaxVersion, int KeyRevision, byte[] Key, byte[] Iv);

    private sealed record SceSegment(int Index, byte[] Key, byte[] Iv);

    private readonly record struct Phdr(uint Type, uint Offset, uint VAddr, uint PAddr, uint FileSize, uint MemSize, uint Flags, uint Align);

    private readonly record struct SegmentInfo(ulong Offset, ulong Size, uint Compressed, uint Plaintext);

    private static readonly KeyEntry[] NpdrmKeys =
    [
        Entry(0, 0, MaxVersion, "C10368BF3D2943BC6E5BD05E46A9A7B6", "00000000000000000000000000000000"),
        Entry(1, 0, MaxVersion, "16419DD3BFBE8BDC596929B72CE237CD", "00000000000000000000000000000000")
    ];

    private static readonly KeyEntry[] MetadataKeys =
    [
        Entry(0, 0, 0x16920000000, "AAA508FA5E85EAEE597ED2B27804D22287CFADF1DF32EDC7A7C58E8C9AA8BB36", "CD1BD3A59200CC67A3B804808DC2AE73"),
        Entry(0, 0x18000000000, MaxVersion, "5661E5FB20CFD1D1DFF50C1E59A6EA977D0AA5C5770F53B9CDD4E9451FFF55CB", "23D02FF79BF430E2D123869BF0CACAA0"),
        Entry(1, 0, MaxVersion, "4181B2DF5F5D94D3C80B7D86EACF1928533A49BA58EDE2B43CDEE7E572568BD4", "B1678C0543B6C1997B63A6F4F3C8FD33"),
        Entry(2, 0, MaxVersion, "5282582F17F068F89A260AAFB71C58928F45A8D08C681376B07FF9EAB1114226", "29672DF43E426F41AF46D42E8437D449"),
        Entry(3, 0, MaxVersion, "270CBA370061B87077672ADB5142D18844AAED352A9CCEE63602B0D740594334", "1CF2454FBF47D76221B91AFC3B608C28"),
        Entry(4, 0, MaxVersion, "A782BC5A9EDDFC49A513FF3E592C4677A8C8920F23C9F11F2558FB9D99A43868", "559B5E658559EB65EBF892C274E098A9"),
        Entry(5, 0, MaxVersion, "12D64D0172495226010A687DE245A73DE028B3561E25E69BABC325636F3CAE0A", "F149EED1757E5A915B24309795BFC380")
    ];

    private static KeyEntry Entry(int revision, ulong min, ulong max, string key, string iv)
        => new(min, max, revision, Convert.FromHexString(key), Convert.FromHexString(iv));

    public static bool IsSelf(ReadOnlySpan<byte> data) => data.Length >= 0x80 && BinaryPrimitives.ReadUInt32LittleEndian(data) == SceMagic;

    public static byte[] DecryptToFself(byte[] self, byte[] klicensee, FselfLayout layout)
    {
        if (!IsSelf(self))
            throw new InvalidDataException("SELF 형식이 아닙니다.");

        ulong appInfoOffset = U64(self, 0x38);
        ulong elfOffset = U64(self, 0x40);
        ulong phdrOffset = U64(self, 0x48);
        ulong segmentInfoOffset = U64(self, 0x58);
        ulong authId = U64(self, (int)appInfoOffset);
        uint selfType = U32(self, (int)appInfoOffset + 12);
        ulong sysVersion = U64(self, (int)appInfoOffset + 16);
        int phnum = U16(self, (int)elfOffset + 44);

        var phdrs = new List<Phdr>(phnum);
        var segmentInfos = new List<SegmentInfo>(phnum);
        bool encrypted = false;

        for (int i = 0; i < phnum; i++)
        {
            int p = (int)phdrOffset + i * PhdrSize;

            phdrs.Add(ReadPhdr(self, p));

            int s = (int)segmentInfoOffset + i * SegmentInfoSize;
            var info = new SegmentInfo(U64(self, s), U64(self, s + 8), U32(self, s + 16), U32(self, s + 24));

            segmentInfos.Add(info);

            if (info.Plaintext == SecureNo)
                encrypted = true;
        }

        if (!encrypted)
            return self;

        if (selfType != SelfTypeApp)
            throw new NotSupportedException($"APP 타입이 아닌 SELF는 지원하지 않습니다. (type=0x{selfType:X})");

        if (klicensee.Length != 16 || klicensee.All(b => b == 0))
            throw new InvalidDataException("SELF 복호화에 필요한 klicensee가 없습니다.");

        var segments = GetSegments(self, sysVersion, self[9], klicensee);
        var elf = new MemoryStream();

        elf.Write(self, (int)elfOffset, ElfHeaderSize);

        long at = ElfHeaderSize;

        for (int i = 0; i < phnum; i++)
        {
            elf.Write(self, (int)phdrOffset + i * PhdrSize, PhdrSize);
            at += PhdrSize;
        }

        for (int i = 0; i < phnum; i++)
        {
            if (segments.Count > 0 && i >= segments.Count)
                break;

            int idx = segments.Count > 0 ? segments[i].Index : i;

            if (phdrs[idx].FileSize == 0)
                continue;

            long pad = phdrs[idx].Offset - at;

            if (pad > 0)
                elf.Write(new byte[pad]);

            at += pad;

            var info = segmentInfos[idx];
            var decrypted = new byte[info.Size];

            if (info.Plaintext == SecureNo)
            {
                Array.Copy(self, (long)info.Offset, decrypted, 0, (long)info.Size);

                using var ctr = new Aes128Ctr(segments[i].Key, segments[i].Iv);

                ctr.XorAt(0, decrypted);
            }

            if (info.Compressed == SecureYes)
            {
                var inflated = Inflate(decrypted);

                elf.Write(inflated);
                at += inflated.Length;
            }
            else
            {
                elf.Write(decrypted);
                at += decrypted.Length;
            }
        }

        return MakeFself(elf.ToArray(), authId, layout);
    }

    private static List<SceSegment> GetSegments(byte[] self, ulong sysVersion, int keyRevision, byte[] klicensee)
    {
        uint metadataOffset = U32(self, 12);
        int headerLength = (int)U64(self, 16);
        int datLength = headerLength - (int)metadataOffset - 48;
        var dat = self.AsSpan((int)metadataOffset + 48, datLength).ToArray();

        var metadataKey = Select(MetadataKeys, sysVersion, keyRevision);
        var npdrmKey = Select(NpdrmKeys, sysVersion, keyRevision >= 2 ? 1 : 0);

        byte[] predec = CbcDecrypt(npdrmKey.Key, npdrmKey.Iv, klicensee);
        byte[] decIn = CbcDecrypt(predec, npdrmKey.Iv, dat.AsSpan(0, MetadataInfoSize).ToArray());
        byte[] dec = CbcDecrypt(metadataKey.Key, metadataKey.Iv, decIn);

        byte[] segKey = dec.AsSpan(0, 16).ToArray();
        byte[] segIv = dec.AsSpan(32, 16).ToArray();

        int restLength = (datLength - MetadataInfoSize) & ~15;
        byte[] dec1 = CbcDecrypt(segKey, segIv, dat.AsSpan(MetadataInfoSize, restLength).ToArray());

        uint sectionCount = U32(dec1, 12);
        uint keyCount = U32(dec1, 16);
        int start = MetadataHeaderSize + (int)sectionCount * MetadataSectionSize;
        var vault = new List<byte[]>();

        for (int i = 0; i < keyCount; i++)
            vault.Add(dec1.AsSpan(start + 16 * i, 16).ToArray());

        var result = new List<SceSegment>();

        for (int i = 0; i < sectionCount; i++)
        {
            int b = MetadataHeaderSize + i * MetadataSectionSize;
            int segIdx = (int)U32(dec1, b + 20);
            uint encryption = U32(dec1, b + 32);
            int keyIdx = (int)U32(dec1, b + 36);
            int ivIdx = (int)U32(dec1, b + 40);

            if (encryption == EncryptionAes128Ctr)
                result.Add(new SceSegment(segIdx, vault[keyIdx], vault[ivIdx]));
        }

        return result;
    }

    private static byte[] MakeFself(byte[] elf, ulong authId, FselfLayout layout)
    {
        int fileSize = elf.Length;
        int phnum = U16(elf, 44);
        int phoff = (int)U32(elf, 28);
        int phentsize = U16(elf, 42);

        var output = new byte[HeaderLength + fileSize];

        const int appInfoOffset = 0x80;
        const int elfOffset = 0x80 + 0x20;
        int phdrOffset = (elfOffset + ElfHeaderSize + 0xF) & ~0xF;
        int sectionInfoOffset = layout == FselfLayout.Vita3K ? (phdrOffset + PhdrSize) * phnum : phdrOffset + PhdrSize * phnum;
        int versionOffset = sectionInfoOffset + SegmentInfoSize * phnum;
        int controlOffset = versionOffset + 16;
        const int control5Size = 0x110;
        const int control6Size = 0x110;
        const int control7Size = 0x50;

        W32(output, 0, SceMagic);
        W32(output, 4, 3);
        W16(output, 8, 0xC0);
        W16(output, 10, 1);
        W32(output, 12, 0x600);
        W64(output, 0x10, HeaderLength);
        W64(output, 0x18, (ulong)fileSize);
        W64(output, 0x20, (ulong)(HeaderLength + fileSize));
        W64(output, 0x28, 0);
        W64(output, 0x30, 4);
        W64(output, 0x38, appInfoOffset);
        W64(output, 0x40, elfOffset);
        W64(output, 0x48, (ulong)phdrOffset);
        W64(output, 0x50, 0);
        W64(output, 0x58, (ulong)sectionInfoOffset);
        W64(output, 0x60, (ulong)versionOffset);
        W64(output, 0x68, (ulong)controlOffset);
        W64(output, 0x70, control5Size + control6Size + control7Size);
        W64(output, 0x78, 0);

        W64(output, appInfoOffset, authId);
        W32(output, appInfoOffset + 8, 0);
        W32(output, appInfoOffset + 12, 8);
        W64(output, appInfoOffset + 16, 0x1000000000000);
        W64(output, appInfoOffset + 24, 0);

        output[elfOffset + 0] = 0x7F;
        output[elfOffset + 1] = (byte)'E';
        output[elfOffset + 2] = (byte)'L';
        output[elfOffset + 3] = (byte)'F';
        output[elfOffset + 4] = 1;
        output[elfOffset + 5] = 1;
        output[elfOffset + 6] = 1;
        W16(output, elfOffset + 16, U16(elf, 16));
        W16(output, elfOffset + 18, 0x28);
        W32(output, elfOffset + 20, 1);
        W32(output, elfOffset + 24, U32(elf, 24));
        W32(output, elfOffset + 28, 0x34);
        W32(output, elfOffset + 36, 0x05000000);
        W16(output, elfOffset + 40, 0x34);
        W16(output, elfOffset + 42, 0x20);
        W16(output, elfOffset + 44, (ushort)phnum);

        for (int i = 0; i < phnum; i++)
        {
            var ph = ReadPhdr(elf, phoff + phentsize * i);
            uint align = ph.Align > 0x1000 ? 0x1000 : ph.Align;
            int p = phdrOffset + PhdrSize * i;

            W32(output, p, ph.Type);
            W32(output, p + 4, ph.Offset);
            W32(output, p + 8, ph.VAddr);
            W32(output, p + 12, ph.PAddr);
            W32(output, p + 16, ph.FileSize);
            W32(output, p + 20, ph.MemSize);
            W32(output, p + 24, ph.Flags);
            W32(output, p + 28, align);

            int s = sectionInfoOffset + SegmentInfoSize * i;

            W64(output, s, (ulong)HeaderLength + ph.Offset);
            W64(output, s + 8, ph.FileSize);
            W64(output, s + 16, 1);
            W64(output, s + 24, 2);
        }

        W32(output, versionOffset, 1);
        W32(output, versionOffset + 4, 0);
        W32(output, versionOffset + 8, 16);
        W32(output, versionOffset + 12, 0);

        W32(output, controlOffset, 5);
        W32(output, controlOffset + 4, control5Size);
        W32(output, controlOffset + 8, 1);

        int c6 = controlOffset + control5Size;

        W32(output, c6, 6);
        W32(output, c6 + 4, control6Size);
        W32(output, c6 + 8, 1);
        W32(output, c6 + 16, 1);

        int c7 = c6 + control6Size;

        W32(output, c7, 7);
        W32(output, c7 + 4, control7Size);

        Buffer.BlockCopy(elf, 0, output, HeaderLength, fileSize);

        return output;
    }

    private static KeyEntry Select(KeyEntry[] entries, ulong sysVersion, int revision)
    {
        foreach (var e in entries)
        {
            if (sysVersion >= e.MinVersion && sysVersion <= e.MaxVersion && e.KeyRevision == revision)
                return e;
        }

        throw new NotSupportedException($"SELF 키를 찾을 수 없습니다. (sysver=0x{sysVersion:X}, rev={revision})");
    }

    private static byte[] CbcDecrypt(byte[] key, byte[] iv, byte[] data)
    {
        using var aes = Aes.Create();

        aes.Key = key;

        return aes.DecryptCbc(data, iv, PaddingMode.None);
    }

    private static byte[] Inflate(byte[] data)
    {
        var output = new MemoryStream();

        try
        {
            using var z = new ZLibStream(new MemoryStream(data), CompressionMode.Decompress);

            z.CopyTo(output);
        }
        catch (InvalidDataException)
        {
        }

        return output.ToArray();
    }

    private static Phdr ReadPhdr(byte[] d, int p) => new(U32(d, p), U32(d, p + 4), U32(d, p + 8), U32(d, p + 12), U32(d, p + 16), U32(d, p + 20), U32(d, p + 24), U32(d, p + 28));

    private static ushort U16(byte[] d, int o) => BinaryPrimitives.ReadUInt16LittleEndian(d.AsSpan(o));

    private static uint U32(byte[] d, int o) => BinaryPrimitives.ReadUInt32LittleEndian(d.AsSpan(o));

    private static ulong U64(byte[] d, int o) => BinaryPrimitives.ReadUInt64LittleEndian(d.AsSpan(o));

    private static void W16(byte[] d, int o, ushort v) => BinaryPrimitives.WriteUInt16LittleEndian(d.AsSpan(o), v);

    private static void W32(byte[] d, int o, uint v) => BinaryPrimitives.WriteUInt32LittleEndian(d.AsSpan(o), v);

    private static void W64(byte[] d, int o, ulong v) => BinaryPrimitives.WriteUInt64LittleEndian(d.AsSpan(o), v);
}