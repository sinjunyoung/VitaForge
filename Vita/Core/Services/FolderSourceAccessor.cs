namespace Vita.Core.Services;

public sealed class FolderSourceAccessor(string root) : ISourceAccessor
{
    private string Full(string relativePath) => Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));

    public bool DirectoryExists(string relativePath) => Directory.Exists(Full(relativePath));

    public IEnumerable<string> EnumerateDirectoryNames(string relativePath) => Directory.Exists(Full(relativePath)) ? Directory.EnumerateDirectories(Full(relativePath)).Select(d => Path.GetFileName(d)!) : [];

    public IEnumerable<string> EnumerateAllFiles() => Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories).Select(f => Path.GetRelativePath(root, f));

    public bool FileExists(string relativePath) => File.Exists(Full(relativePath));

    public byte[] ReadAllBytes(string relativePath) => File.ReadAllBytes(Full(relativePath));

    public Stream OpenRead(string relativePath) => new FileStream(Full(relativePath), FileMode.Open, FileAccess.Read, FileShare.Read, 81920, FileOptions.SequentialScan);

    public void CopyTo(string relativePath, Stream destination, Action<long>? onProgress = null)
    {
        using var source = new FileStream(Full(relativePath), FileMode.Open, FileAccess.Read, FileShare.Read);

        SourceStreamCopier.Copy(source, destination, onProgress);
    }

    public long GetFileSize(string relativePath) => new FileInfo(Full(relativePath)).Length;

    public void Dispose() { }
}