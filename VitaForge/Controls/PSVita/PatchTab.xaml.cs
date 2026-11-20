using System.IO;
using System.Windows;
using System.Windows.Controls;
using VitaForge.Core.ViewModels.PSVita;
using VitaForge.ViewModels.PSVita;

namespace VitaForge.Controls.PSVita;

public partial class PatchTab : UserControl
{
    private PatchMainViewModel ViewModel => (PatchMainViewModel)DataContext;

    public PatchTab() => InitializeComponent();

    private void TxtInstallPath_PreviewDragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop) && e.Data.GetData(DataFormats.FileDrop) is string[] paths && paths.Length > 0 && Directory.Exists(paths[0]))
            e.Effects = DragDropEffects.Copy;
        else
            e.Effects = DragDropEffects.None;

        e.Handled = true;
    }

    private void TxtInstallPath_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop) && e.Data.GetData(DataFormats.FileDrop) is string[] paths && paths.Length > 0 && Directory.Exists(paths[0]) && ViewModel != null)
            ViewModel.InstallPath = paths[0];

        e.Handled = true;
    }

    private async void BtnStart_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.IsLocked)
            ViewModel.Cancel();
        else
            await ViewModel.StartAsync();
    }

    private void BtnReleaseLink_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: PatchRowViewModel row })
            PatchMainViewModel.OpenReleaseLink(row);

        e.Handled = true;
    }
}