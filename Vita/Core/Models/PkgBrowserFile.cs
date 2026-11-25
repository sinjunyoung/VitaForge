namespace Vita.Core.Models;

public sealed record PkgBrowserFile(string Path, long Size, long RawSize, bool IsEncrypted);