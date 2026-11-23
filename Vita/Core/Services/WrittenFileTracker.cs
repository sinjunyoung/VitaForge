namespace Vita.Core.Services;

public sealed class WrittenFileTracker
{
    private const string BackupSuffix = ".vfbak";

    private readonly Lock _lock = new();
    private readonly HashSet<string> _files = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _directories = [];
    private readonly Dictionary<string, string> _backups = new(StringComparer.OrdinalIgnoreCase);

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
        {
            string full = Path.GetFullPath(path);

            if (!_files.Add(full))
                return;

            if (!File.Exists(full))
                return;

            string backup = full + BackupSuffix;

            if (!File.Exists(backup))
                File.Move(full, backup);

            _backups[full] = backup;
        }
    }

    public void Commit()
    {
        lock (_lock)
        {
            foreach (string backup in _backups.Values)
            {
                try
                {
                    if (File.Exists(backup))
                        File.Delete(backup);
                }
                catch { }
            }

            _backups.Clear();
            _files.Clear();
            _directories.Clear();
        }
    }

    public int Rollback()
    {
        lock (_lock)
        {
            int deleted = 0;

            foreach (string file in _files)
            {
                try
                {
                    if (File.Exists(file))
                    {
                        File.Delete(file);
                        deleted++;
                    }
                }
                catch { }
            }

            foreach (var (file, backup) in _backups)
            {
                try
                {
                    if (File.Exists(backup))
                        File.Move(backup, file, true);
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
            _backups.Clear();
            _directories.Clear();

            return deleted;
        }
    }
}
