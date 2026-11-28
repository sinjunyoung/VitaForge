using Common.Models;
using VitaForge.Core.ViewModels;
using System.ComponentModel;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace VitaForge.Core;

public class CommonConfig : ViewModelBase
{
    public double LogBoxHeight { get; set { SetProperty(ref field, value); } } = 130;

    public bool Topmost { get; set { SetProperty(ref field, value); } }

    public bool AutoCheckUpdate { get; set { SetProperty(ref field, value); } } = true;

    public string? Language { get; set { SetProperty(ref field, value); } } = null;

    public bool IsMuted { get; set { SetProperty(ref field, value); } }

    public double SoundVolume { get; set { SetProperty(ref field, value); } } = 50;

    public string CompleteSoundPath { get; set { SetProperty(ref field, value); } } = "complete.mp3";

    public string CancelSoundPath { get; set { SetProperty(ref field, value); } } = "cancel.mp3";

    public string ErrorSoundPath { get; set { SetProperty(ref field, value); } } = "error.mp3";
}

public class WindowConfig : ViewModelBase
{
    public double Left { get; set { SetProperty(ref field, value); } } = 100;

    public double Top { get; set { SetProperty(ref field, value); } } = 100;

    public double Width { get; set { SetProperty(ref field, value); } } = 900;

    public double Height { get; set { SetProperty(ref field, value); } } = 715;

    public bool IsMaximized { get; set { SetProperty(ref field, value); } } = false;
}

public class OutputFoldersConfig : ViewModelBase
{
    public string? VitaOutputPath { get; set { SetProperty(ref field, value); } }

    public string? VitaConvertOutputPath { get; set { SetProperty(ref field, value); } }

    public string? VitaDecryptOutputPath { get; set { SetProperty(ref field, value); } }

    public string? VitaDownloadOutputPath { get; set { SetProperty(ref field, value); } }

    public string? VitaPatchEmuRootPath { get; set { SetProperty(ref field, value); } }

    public string? VitaPatchDeviceRootPath { get; set { SetProperty(ref field, value); } }

    public string? VitaPatchOutputPath { get; set { SetProperty(ref field, value); } }
}

public class VitaPatchConfig : ViewModelBase
{
    public string EnterButton { get; set { SetProperty(ref field, value); } } = "Circle";
}

public class AppConfig : ViewModelBase
{
    private static readonly string DefaultFilePath = Path.ChangeExtension(Environment.ProcessPath!, "config.json");
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private static readonly Lazy<AppConfig> _instance = new(() => new AppConfig().LoadInternal());
    public static AppConfig Instance => _instance.Value;

    public CommonConfig Common { get; set { SetProperty(ref field, value); } } = new();

    public WindowConfig Window { get; set { SetProperty(ref field, value); } } = new();

    public OutputFoldersConfig OutputFolders { get; set { SetProperty(ref field, value); } } = new();

    public VitaPatchConfig VitaPatch { get; set { SetProperty(ref field, value); } } = new();

    [JsonConstructor]
    private AppConfig() { }

    private AppConfig LoadInternal()
    {
        if (!File.Exists(DefaultFilePath))
        {
            Save();
            SubscribeToChanges();
            return this;
        }

        try
        {
            var json = File.ReadAllText(DefaultFilePath);
            var loaded = JsonSerializer.Deserialize<AppConfig>(json);

            if (loaded != null)
            {
                Common = loaded.Common ?? new();
                Window = loaded.Window ?? new();
                OutputFolders = loaded.OutputFolders ?? new();
                VitaPatch = loaded.VitaPatch ?? new();
            }
        }
        catch
        {
            Save();
        }

        SubscribeToChanges();
        return this;
    }

    private void SubscribeToChanges()
    {
        void AutoSave(object? s, PropertyChangedEventArgs e) => Save();

        Common.PropertyChanged += AutoSave;
        Window.PropertyChanged += AutoSave;
        OutputFolders.PropertyChanged += AutoSave;
        VitaPatch.PropertyChanged += AutoSave;
    }

    public void Save() => File.WriteAllText(DefaultFilePath, JsonSerializer.Serialize(this, JsonOptions));
}
