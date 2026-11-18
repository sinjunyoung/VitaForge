using Common.Services;
using VitaForge.Core;
using VitaForge.Core.Models;
using VitaForge.Core.UI.Helpers;
using VitaForge.Core.ViewModels;
using VitaForge.ViewModels.PSVita;
using VitaForge.ViewModels.Settings;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Drawing;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shell;

namespace VitaForge.ViewModels;

public class MainViewModel : ToolTabViewModel
{
    public static ImageSource? AppIcon
    {
        get
        {
            try
            {
                var exePath = Process.GetCurrentProcess().MainModule?.FileName;

                if (string.IsNullOrEmpty(exePath))
                    return null;

                using var icon = Icon.ExtractAssociatedIcon(exePath);

                if (icon == null)
                    return null;

                return Imaging.CreateBitmapSourceFromHIcon(icon.Handle, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            }
            catch
            {
                return null;
            }
        }
    }

    public static double LogBoxHeight { get => AppConfig.Instance.Common.LogBoxHeight; set { AppConfig.Instance.Common.LogBoxHeight = value; } }

    public PSVitaMainViewModel PSVitaMainVM { get; } = new();

    public SettingsMainViewModel Settings { get; } = new();

    public int SelectedTabIndex
    {
        get;
        set
        {
            SetProperty(ref field, value);
            OnPropertyChanged(nameof(ActiveLogEntries));
            OnPropertyChanged(nameof(HasHelpKey));
        }
    }

    public ToolTabViewModel SelectedViewModel
    {
        get => Tools[SelectedTabIndex];
        set
        {
            var index = Tools.IndexOf(value);

            if (index != -1)
                SelectedTabIndex = index;
        }
    }

    public ObservableCollection<LogEntry> ActiveLogEntries => SelectedTabIndex switch
    {
        0 => PSVitaMainVM.LogEntries,
        _ => PSVitaMainVM.LogEntries
    };

    public static string AppVersion => $"{AppDomain.CurrentDomain.FriendlyName} - Ver {Utils.ToAppVersionString()}";

    public double TaskbarProgressValue { get; set => SetProperty(ref field, value); }

    public TaskbarItemProgressState TaskbarProgressState { get; set => SetProperty(ref field, value); } = TaskbarItemProgressState.None;

    public override bool HasHelpKey => SelectedViewModel?.HasHelpKey ?? false;

    public override string? HelpPageKey => SelectedViewModel?.HelpPageKey;

    internal static MainViewModel? Current { get; set; }

    public MainViewModel()
    {
        Current = this;

        Tools.Add(PSVitaMainVM);
        Tools.Add(Settings);

        foreach (var tool in Tools)
        {
            RegisterChild(tool);

            tool.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(HasHelpKey))
                    OnPropertyChanged(nameof(HasHelpKey));
            };
        }
    }

    public static void SaveConfig() => AppConfig.Instance.Save();

    public bool IsAnyChildLocked()
    {
        if (Tools.Any(vm => vm.IsLocked))
            return true;

        foreach (var child in Tools)
        {
            if (child.Tools != null && child.Tools.Any(child => child.IsLocked))
                return true;
        }

        return false;
    }

    public static void SetTaskbarProgress(double value, TaskbarItemProgressState state = TaskbarItemProgressState.Normal)
    {
        if (Current == null) return;

        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher != null && !dispatcher.CheckAccess())
        {
            dispatcher.Invoke(() => SetTaskbarProgress(value, state));
            return;
        }

        if (value <= 0 || state == TaskbarItemProgressState.None)
        {
            Current.TaskbarProgressState = TaskbarItemProgressState.None;
            Current.TaskbarProgressValue = 0;
        }
        else
        {
            Current.TaskbarProgressState = state;
            Current.TaskbarProgressValue = value / 100.0;

            if (state == TaskbarItemProgressState.Error)
                FlashTaskbar.FlashTaskbarIcon(Application.Current?.MainWindow!);
        }
    }

    public override void ClearLog() => ActiveLogEntries.Clear();
}
