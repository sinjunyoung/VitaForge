using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using VitaForge.Core.ViewModels.PSVita;
using VitaForge.ViewModels.PSVita;

namespace VitaForge.Controls.PSVita;

public partial class PkgBrowserTab : UserControl
{
    private PkgBrowserMainViewModel ViewModel => (PkgBrowserMainViewModel)DataContext;

    public PkgBrowserTab() => InitializeComponent();

    private void Root_DragEnter(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private async void Root_Drop(object sender, DragEventArgs e)
    {
        e.Handled = true;

        if (e.Data.GetDataPresent(DataFormats.FileDrop) && e.Data.GetData(DataFormats.FileDrop) is string[] files)
            await ViewModel.OpenDroppedAsync(files);
    }

    private void License_PreviewDragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void License_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop) && e.Data.GetData(DataFormats.FileDrop) is string[] { Length: > 0 } files)
            ViewModel.License = files[0];

        e.Handled = true;
    }

    private void LvItems_ContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        if (LvItems.SelectedItems.Count == 0)
            e.Handled = true;
    }

    private void LvItems_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (LvItems.SelectedItem is PkgBrowserItemViewModel item)
            ViewModel.OpenItem(item);
    }

    private void LvItems_KeyUp(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && LvItems.SelectedItem is PkgBrowserItemViewModel item)
            ViewModel.OpenItem(item);
        else if (e.Key == Key.Back)
            ViewModel.GoUp();
    }

    private async void SaveSelected(bool decrypted)
    {
        var selected = LvItems.SelectedItems.Cast<PkgBrowserItemViewModel>().ToList();

        await ViewModel.SaveSelectedAsync(selected, decrypted);
    }

    private void MenuSaveAsRaw_Click(object sender, RoutedEventArgs e) => SaveSelected(false);

    private void MenuSaveAsDecrypted_Click(object sender, RoutedEventArgs e) => SaveSelected(true);

    private void BtnSaveAll_Click(object sender, RoutedEventArgs e)
    {
        var menu = (ContextMenu)Resources["SaveAllMenu"];

        menu.PlacementTarget = BtnSaveAll;
        menu.Placement = PlacementMode.Top;
        menu.IsOpen = true;
    }

    private async void MenuSaveAllRaw_Click(object sender, RoutedEventArgs e) => await ViewModel.SaveAllAsync(false);

    private async void MenuSaveAllDecrypted_Click(object sender, RoutedEventArgs e) => await ViewModel.SaveAllAsync(true);
}