namespace Vita.Core.Cpk;

internal static class CriCipher
{
    public static byte[] Decrypt(ReadOnlySpan<byte> data)
    {
        byte[] result = data.ToArray();

        Apply(result);

        return result;
    }

    public static void Apply(Span<byte> data)
    {
        byte key = 0x5F;

        for (int i = 0; i < data.Length; i++)
        {
            data[i] ^= key;

            key = (byte)(key * 0x15);
        }
    }
}