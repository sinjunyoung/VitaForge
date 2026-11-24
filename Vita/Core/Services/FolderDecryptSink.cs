using Common.Localization;

namespace Vita.Core.Services;

internal sealed class FolderDecryptSink : IDecryptSink
{
    private const int BufferSize = 81920;

    private readonly string _root;
    private readonly WrittenFileTracker? _tracker;

    public FolderDecryptSink(string folderPath, WrittenFileTracker? tracker = null)
    {
        _tracker = tracker;

        if (tracker != null)
            tracker.EnsureDirectory(folderPath);
        else
            Directory.CreateDirectory(folderPath);

        _root = Path.GetFullPath(folderPath);
    }

    public Stream Open(string entryPath)
    {
        string fullPath = Path.GetFullPath(Path.Combine(_root, entryPath.Replace('/', Path.DirectorySeparatorChar)));
        string relativePath = Path.GetRelativePath(_root, fullPath);

        if (Path.IsPathRooted(relativePath) || relativePath == ".." || relativePath.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            throw new InvalidDataException(Loc.S.ErrPathNotAllowed(entryPath));

        if (_tracker != null)
        {
            _tracker.EnsureDirectory(Path.GetDirectoryName(fullPath)!);
            _tracker.RecordFile(fullPath);
        }
        else
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

        return new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None, BufferSize, useAsync: true);
    }

    public void Dispose() { }
}