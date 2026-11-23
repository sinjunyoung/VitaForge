using Common.Localization;
using System.IO.Compression;

namespace Vita.Core.Services;

public sealed class ZipSourceAccessor : ISourceAccessor
{
    private readonly FileStream _stream;
    private readonly ZipArchive _zip;
    private readonly Dictionary<string, ZipArchiveEntry> _entries = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, HashSet<string>> _children = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _directories = new(StringComparer.OrdinalIgnoreCase);

    public ZipSourceAccessor(string zipPath)
    {
        _stream = new FileStream(zipPath, FileMode.Open, FileAccess.Read, FileShare.Read);

        try
        {
            _zip = new ZipArchive(_stream, ZipArchiveMode.Read);

            foreach (var entry in _zip.Entries)
            {
                string key = Normalize(entry.FullName);

                if (_entries.TryAdd(key, entry))
                    IndexDirectories(key);
            }
        }
        catch
        {
            _zip?.Dispose();
            _stream.Dispose();

            throw;
        }
    }

    private static string Normalize(string path) => PatchShared.NormalizeZipPath(path);

    private void IndexDirectories(string key)
    {
        int start = 0;
        string parent = string.Empty;

        while (true)
        {
            int slash = key.IndexOf('/', start);

            if (slash < 0)
                break;

            if (slash > start)
            {
                string name = key[start..slash];

                if (!_children.TryGetValue(parent, out var names))
                    _children[parent] = names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                names.Add(name);

                parent = parent.Length == 0 ? name : $"{parent}/{name}";

                _directories.Add(parent);
            }

            start = slash + 1;
        }
    }

    public bool DirectoryExists(string relativePath)
    {
        string prefix = Normalize(relativePath);

        if (prefix.Length == 0)
            return _entries.Count > 0;

        return _directories.Contains(prefix);
    }

    public IEnumerable<string> EnumerateDirectoryNames(string relativePath)
        => _children.TryGetValue(Normalize(relativePath), out var names) ? [.. names] : [];

    public IEnumerable<string> EnumerateAllFiles()
    {
        return _entries.Values
            .Where(e => !e.FullName.EndsWith('/') && !e.FullName.EndsWith('\\'))
            .Select(e => Normalize(e.FullName));
    }

    public bool FileExists(string relativePath) => _entries.ContainsKey(Normalize(relativePath));

    public byte[] ReadAllBytes(string relativePath)
    {
        if (!_entries.TryGetValue(Normalize(relativePath), out var entry))
            throw new FileNotFoundException(relativePath);

        if (entry.Length > Array.MaxLength)
            throw new NotSupportedException(Loc.S.ErrFileTooLargeForMemory(relativePath, entry.Length));

        var data = new byte[entry.Length];
        using var s = entry.Open();

        s.ReadExactly(data);

        return data;
    }

    public Stream OpenRead(string relativePath)
    {
        if (!_entries.TryGetValue(Normalize(relativePath), out var entry))
            throw new FileNotFoundException(relativePath);

        return entry.Open();
    }

    public void CopyTo(string relativePath, Stream destination, Action<long>? onProgress = null)
    {
        if (!_entries.TryGetValue(Normalize(relativePath), out var entry))
            throw new FileNotFoundException(relativePath);

        using var source = entry.Open();

        SourceStreamCopier.Copy(source, destination, onProgress);
    }

    public long GetFileSize(string relativePath)
    {
        if (!_entries.TryGetValue(Normalize(relativePath), out var entry))
            throw new FileNotFoundException(relativePath);

        return entry.Length;
    }

    public void Dispose()
    {
        _zip.Dispose();
        _stream.Dispose();
    }
}
