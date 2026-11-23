namespace VitaForge.Core.Services.PSVita.PatchHosts;

public sealed class PatchDownloadProgress(Action<long> onTotal, Action<long> onBytes)
{
    public static PatchDownloadProgress None { get; } = new(_ => { }, _ => { });

    public void SetTotal(long total) => onTotal(total);

    public void AddBytes(long bytes) => onBytes(bytes);
}
