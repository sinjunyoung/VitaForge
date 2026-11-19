using Common.Localization;

namespace Vita.Core.Services;

internal sealed class FolderDecryptSink : IDecryptSink
{
    private const int BufferSize = 81920;

    private readonly string _root;

    public FolderDecryptSink(string folderPath)
    {
        Directory.CreateDirectory(folderPath);

        _root = Path.GetFullPath(folderPath) + Path.DirectorySeparatorChar;
    }

    public Stream Open(string entryPath)
    {
        string fullPath = Path.GetFullPath(Path.Combine(_root, entryPath.Replace('/', Path.DirectorySeparatorChar)));

        if (!fullPath.StartsWith(_root, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException(Loc.S.ErrPathNotAllowed(entryPath));

        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

        return new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None, BufferSize, useAsync: true);
    }

    public void Dispose() { }
}
