using VitaForge.Core.ViewModels;

namespace VitaForge.ViewModels.PSVita;

public class PSVitaMainViewModel : MultiToolTabViewModel
{
    public PatchMainViewModel PatchVM { get; } = new();

    public DownloadMainViewModel DownloadVM { get; } = new();

    public RepackMainViewModel RepackVM { get; } = new();

    public DecryptMainViewModel DecryptVM { get; } = new();

    public ConverterMainViewModel ConverterVM { get; } = new();

    public PkgBrowserMainViewModel PkgBrowserVM { get; } = new();

    public PSVitaMainViewModel()
    {
        Tools.Add(PatchVM);
        Tools.Add(DownloadVM);
        Tools.Add(RepackVM);
        Tools.Add(DecryptVM);
        Tools.Add(ConverterVM);
        Tools.Add(PkgBrowserVM);

        InitializeMultiTools();
    }
}