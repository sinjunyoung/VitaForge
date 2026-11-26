namespace Vita.Core.TitleRules;

internal sealed record SingleFileBundleEntry(string Path, byte Type, long Offset, long Size, long CompressedSize)
{
    private const byte AssemblyType = 1;

    public bool IsAssembly => Type == AssemblyType;

    public bool IsCompressed => CompressedSize > 0;
}
