using Common.Services;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using VitaForge.ViewModels.PSVita;

namespace VitaForge.Controls.PSVita;

public partial class DownloadTab : UserControl
{
    private DownloadMainViewModel ViewModel => (DownloadMainViewModel)DataContext;

    public DownloadTab() => InitializeComponent();

    private void LvCatalog_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (ViewModel == null || e.OriginalSource is not DependencyObject source)
            return;

        for (DependencyObject? current = source; current != null && current != LvCatalog; current = VisualTreeHelper.GetParent(current))
        {
            if (current is ToggleButton or Button or ScrollBar or GridViewColumnHeader)
                return;

            if (current is ListViewItem { DataContext: NpsRowViewModel row })
            {
                ViewModel.ToggleExpand(row);
                e.Handled = true;
                return;
            }
        }
    }

    private void TxtOutputPath_PreviewDragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop) && e.Data.GetData(DataFormats.FileDrop) is string[] paths && paths.Length > 0 && Directory.Exists(paths[0]))
            e.Effects = DragDropEffects.Copy;
        else
            e.Effects = DragDropEffects.None;

        e.Handled = true;
    }

    private void TxtOutputPath_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop) && e.Data.GetData(DataFormats.FileDrop) is string[] paths && paths.Length > 0)
        {
            string droppedPath = paths[0];
            string? folderPath = Directory.Exists(droppedPath) ? droppedPath : Path.GetDirectoryName(droppedPath);

            if (!string.IsNullOrEmpty(folderPath) && Directory.Exists(folderPath) && ViewModel != null)
                ViewModel.OutputPath = folderPath;
        }

        e.Handled = true;
    }

    private async void BtnStart_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.IsLocked)
            ViewModel.Cancel();
        else
            await ViewModel.StartAsync();
    }

    private void BtnExpand_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: NpsRowViewModel row })
            ViewModel?.ToggleExpand(row);

        e.Handled = true;
    }
}