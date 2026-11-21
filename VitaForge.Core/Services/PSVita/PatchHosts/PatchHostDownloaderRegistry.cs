namespace VitaForge.Core.Services.PSVita.PatchHosts;

public static class PatchHostDownloaderRegistry
{
    private static readonly IPatchHostDownloader[] Hosts =
    [
        new GoogleDrivePatchHost(),
        new MegaPatchHost(),
        new TistoryPatchHost()
    ];

    private static readonly IPatchHostDownloader Fallback = new DirectPatchHost();

    public static IPatchHostDownloader Resolve(string url) => Hosts.FirstOrDefault(h => h.CanHandle(url)) ?? Fallback;
}