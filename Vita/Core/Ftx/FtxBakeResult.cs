namespace Vita.Core.Ftx;

internal sealed record FtxBakeResult(int Baked, IReadOnlyList<string> Skipped);