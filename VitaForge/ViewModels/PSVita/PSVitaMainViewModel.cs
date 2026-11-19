using VitaForge.Core.ViewModels;

namespace VitaForge.ViewModels.PSVita;

public class PSVitaMainViewModel : MultiToolTabViewModel
{
    public RepackMainViewModel RepackVM { get; } = new();

    public ConverterMainViewModel ConverterVM { get; } = new();

    public DownloadMainViewModel DownloadVM { get; } = new();

    public PSVitaMainViewModel()
    {
        Tools.Add(RepackVM);
        Tools.Add(ConverterVM);
        Tools.Add(DownloadVM);

        InitializeMultiTools();
    }
}
