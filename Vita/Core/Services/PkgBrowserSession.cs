using Vita.Core.Models;

namespace Vita.Core.Services;

public sealed class PkgBrowserSession : IDisposable
{
    private readonly PkgSourceAccessor _accessor;
    private readonly PfsFileTable? _table;
    private readonly Dictionary<string, int> _pfsIndex = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<PkgBrowserFile> Files { get; }

    public PkgProbeResult? Probe { get; }

    public string ContentId => _accessor.Header.ContentId;

    private PkgBrowserSession(PkgSourceAccessor accessor)
    {
        _accessor = accessor;

        try
        {
            Probe = PkgProbe.Probe(_accessor);
        }
        catch
        {
            Probe = null;
        }

        try
        {
            _table = NoNpDrmDecryptor.ParseFileTable(_accessor, string.Empty);
        }
        catch
        {
            _table = null;
        }

        if (_table is not null)
        {
            for (int i = 0; i < _table.Entries.Count; i++)
            {
                var entry = _table.Entries[i];

                if (!entry.Type.IsDirectory())
                    _pfsIndex[PatchShared.NormalizeZipPath(entry.RelativePath ?? entry.Name)] = i;
            }
        }

        Files = [.. _accessor.EnumerateAllFiles().Select(CreateFile).OrderBy(f => f.Path, StringComparer.OrdinalIgnoreCase)];
    }

    public static PkgBrowserSession Open(string pkgPath)
    {
        var accessor = new PkgSourceAccessor(pkgPath);

        try
        {
            return new PkgBrowserSession(accessor);
        }
        catch
        {
            accessor.Dispose();

            throw;
        }
    }

    public byte[] ResolveKlicensee(string license)
        => Probe?.Category == ContentCategory.Patch ? PkgLicenseResolver.ResolveKlicenseeForPatch(license, ContentId) : PkgLicenseResolver.ResolveKlicensee(license, ContentId);

    public void Save(string path, Stream destination, byte[]? klicensee, Action<long>? onProgress)
    {
        if (klicensee is not null && _table is not null && _pfsIndex.TryGetValue(path, out int index))
        {
            NoNpDrmDecryptor.DecryptEntryTo(_accessor, string.Empty, klicensee, _table.Entries[index], _table.UnicvEntries[index], _table.FilesSalt, destination, onProgress);

            return;
        }

        _accessor.CopyTo(path, destination, onProgress);
    }

    public void Dispose() => _accessor.Dispose();

    private PkgBrowserFile CreateFile(string path)
    {
        long rawSize = _accessor.GetFileSize(path);

        if (_table is null || !_pfsIndex.TryGetValue(path, out int index))
            return new PkgBrowserFile(path, rawSize, rawSize, false);

        var entry = _table.Entries[index];
        bool encrypted = entry.Type.IsEncrypted() && _table.UnicvEntries[index].NSectors > 0 && rawSize > 0;

        return new PkgBrowserFile(path, entry.Size, rawSize, encrypted);
    }
}