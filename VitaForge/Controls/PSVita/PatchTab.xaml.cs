using System.IO;
using System.Windows;
using System.Windows.Controls;
using VitaForge.Core;
using VitaForge.Core.ViewModels.PSVita;
using VitaForge.ViewModels.PSVita;

namespace VitaForge.Controls.PSVita;

public partial class PatchTab : UserControl
{
    private readonly ListViewColumnSorter _sorter = new();

    private PatchMainViewModel ViewModel => (PatchMainViewModel)DataContext;

    public PatchTab() => InitializeComponent();

    private void TxtTargetPath_PreviewDragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop) && e.Data.GetData(DataFormats.FileDrop) is string[] paths && paths.Length > 0 && Directory.Exists(paths[0]))
            e.Effects = DragDropEffects.Copy;
        else
            e.Effects = DragDropEffects.None;

        e.Handled = true;
    }

    private void TxtTargetPath_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop) && e.Data.GetData(DataFormats.FileDrop) is string[] paths && paths.Length > 0 && Directory.Exists(paths[0]) && ViewModel != null)
            ViewModel.TargetPath = paths[0];

        e.Handled = true;
    }

    private async void BtnStart_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.IsLocked)
            ViewModel.Cancel();
        else
            await ViewModel.StartAsync();
    }

    private void LvPatches_HeaderClick(object sender, RoutedEventArgs e)
    {
        if (sender is ListView listView)
            _sorter.HandleHeaderClick(e, listView);
    }

    private void BtnReleaseLink_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: PatchRowViewModel row })
            PatchMainViewModel.OpenReleaseLink(row);

        e.Handled = true;
    }
}