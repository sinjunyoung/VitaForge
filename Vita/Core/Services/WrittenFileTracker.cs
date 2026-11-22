namespace Vita.Core.Services;

public sealed class WrittenFileTracker
{
    private readonly Lock _lock = new();
    private readonly List<string> _files = [];
    private readonly List<string> _directories = [];

    public void EnsureDirectory(string path)
    {
        lock (_lock)
        {
            var missing = new Stack<string>();
            string? current = Path.GetFullPath(path);

            while (!string.IsNullOrEmpty(current) && !Directory.Exists(current))
            {
                missing.Push(current);
                current = Path.GetDirectoryName(current);
            }

            Directory.CreateDirectory(path);

            _directories.AddRange(missing);
        }
    }

    public void RecordFile(string path)
    {
        lock (_lock)
            _files.Add(Path.GetFullPath(path));
    }

    public void Rollback()
    {
        lock (_lock)
        {
            foreach (string file in _files)
            {
                try
                {
                    if (File.Exists(file))
                        File.Delete(file);
                }
                catch { }
            }

            for (int i = _directories.Count - 1; i >= 0; i--)
            {
                try
                {
                    if (Directory.Exists(_directories[i]) && !Directory.EnumerateFileSystemEntries(_directories[i]).Any())
                        Directory.Delete(_directories[i]);
                }
                catch { }
            }

            _files.Clear();
            _directories.Clear();
        }
    }
}