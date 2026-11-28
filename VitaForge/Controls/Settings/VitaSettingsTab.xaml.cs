using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace VitaForge.Controls.Settings;

public partial class VitaSettingsTab : UserControl
{
    public VitaSettingsTab() => InitializeComponent();

    private void TxtDownloadPath_PreviewDragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void TxtDownloadPath_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is string[] { Length: > 0 } paths && DataContext is ViewModels.Settings.VitaSettingsMainViewModel vm)
            vm.DownloadPath = Directory.Exists(paths[0]) ? paths[0] : Path.GetDirectoryName(paths[0]);
    }
}