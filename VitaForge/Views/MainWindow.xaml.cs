using Common.Localization;
using Common.Services;
using VitaForge.Core;
using VitaForge.Core.Models;
using VitaForge.Core.Services;
using VitaForge.ViewModels;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace VitaForge.Views;

public partial class MainWindow : Window
{
    private MainViewModel ViewModel { get; } = new();

    public MainWindow()
    {
        DataContext = ViewModel;
        InitializeComponent();
        Closing += MainWindow_Closing;

        RestoreWindowState();

        Topmost = AppConfig.Instance.Common.Topmost;
        AppConfig.Instance.Common.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(CommonConfig.Topmost))
                Topmost = AppConfig.Instance.Common.Topmost;
        };
    }

    private void RestoreWindowState()
    {
        var cfg = AppConfig.Instance.Window;

        Left = cfg.Left;
        Top = cfg.Top;
        Width = cfg.Width;
        Height = cfg.Height;

        if (cfg.IsMaximized)
            WindowState = WindowState.Maximized;
    }

    private void SaveWindowState()
    {
        var cfg = AppConfig.Instance.Window;

        cfg.IsMaximized = WindowState == WindowState.Maximized;
        var bounds = WindowState == WindowState.Maximized ? RestoreBounds : new Rect(Left, Top, Width, Height);
        cfg.Left = bounds.Left;
        cfg.Top = bounds.Top;
        cfg.Width = bounds.Width;
        cfg.Height = bounds.Height;
    }

    private void IconBox_Click(object sender, RoutedEventArgs e)
    {
        var element = (FrameworkElement)sender;
        var point = element.TransformToAncestor(this).Transform(new Point(0, element.ActualHeight));

        point.Offset(Left, Top);
        SystemCommands.ShowSystemMenu(this, point);
    }

    private void IconBox_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        var element = (FrameworkElement)sender;
        var pos = e.GetPosition(element);
        var screenPoint = element.PointToScreen(pos);

        SystemCommands.ShowSystemMenu(this, screenPoint);
    }

    private void GridSplitter_DragCompleted(object sender, DragCompletedEventArgs e) => MainViewModel.LogBoxHeight = LogRow.Height.Value;

    private void MainWindow_Closing(object? sender, CancelEventArgs e)
    {
        SaveWindowState();
        MainViewModel.SaveConfig();

        bool busy = ViewModel.IsAnyChildLocked();

        if (!busy)
            return;

        var result = MessageBoxHelper.ShowQuestion(Loc.S.MsgConfirmCancelAndExit);

        if (result)
            ViewModel.CancelAll();
        else
            e.Cancel = true;
    }

    private void LogBox_ContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        if (logBox.SelectedItems.Count == 0 || logBox.Items.Count == 0)
            e.Handled = true;
    }

    private void ClearLog_Click(object sender, RoutedEventArgs e) => ViewModel.ClearLog();

    private void CopyLog_Click(object sender, RoutedEventArgs e)
    {
        if (logBox.SelectedItems.Count > 0)
        {
            var lines = new List<string>();

            foreach (LogEntry entry in logBox.SelectedItems)
            {
                if (entry != null)
                    lines.Add(entry.Message);
            }

            if (lines.Count > 0)
                ClipboardHelper.CopyText(string.Join(Environment.NewLine, lines));
        }
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e) => SystemCommands.MinimizeWindow(this);

    private void MaximizeButton_Click(object sender, RoutedEventArgs e)
    {
        if (WindowState == WindowState.Maximized)
            SystemCommands.RestoreWindow(this);
        else
            SystemCommands.MaximizeWindow(this);
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => SystemCommands.CloseWindow(this);

    private void UrlButton_Click(object sender, RoutedEventArgs e) => "https://github.com/sinjunyoung/VitaForge/releases/latest".OpenUrl();

    private void HelpButton_Click(object sender, RoutedEventArgs e) => (ViewModel.HelpPageKey != null ? "https://sinjunyoung.github.io/VitaForge/" + ViewModel.HelpPageKey : null)?.OpenUrl();
}
