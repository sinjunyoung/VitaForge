using System.IO.Compression;

namespace Vita.Core.Services;

internal sealed class ZipDecryptSink : IDecryptSink
{
    private readonly FileStream _fileStream;
    private readonly ZipArchive _zip;

    public ZipDecryptSink(string zipPath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(zipPath))!);

        _fileStream = new FileStream(zipPath, FileMode.Create, FileAccess.Write);
        _zip = new ZipArchive(_fileStream, ZipArchiveMode.Create);
    }

    public Stream Open(string entryPath) => _zip.CreateEntry(entryPath, CompressionLevel.NoCompression).Open();

    public void Dispose()
    {
        _zip.Dispose();
        _fileStream.Dispose();
    }
}
