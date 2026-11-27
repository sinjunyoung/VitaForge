namespace Vita.Core.Cpk;

internal sealed record CpkEntry(int RowIndex, string Path, long Offset, long FileSize, long ExtractSize);