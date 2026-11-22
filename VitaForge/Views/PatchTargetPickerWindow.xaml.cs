using Common;
using Common.Localization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using Vita.Core.Models;
using VitaForge.Core.Models.PSVita;
using VitaForge.Core.ViewModels.PSVita;

namespace VitaForge.Views;

public partial class PatchTargetPickerWindow : Window
{
    private readonly List<PatchTargetLinkItem> _items;
    private string? _selectedPath;

    public IReadOnlyList<string?> Selections => [.. _items.Select(i => i.TargetPath)];

    public PatchTargetPickerWindow(PatchTargetRequest request)
    {
        InitializeComponent();

        HeaderText.Text = Loc.S.MsgPatchTargetsNotFound(request.PatchFileNames.Count);
        SubText.Text = request.ContentIdSuffix is { Length: > 0 } suffix ? $"{request.TitleId} · {request.Category} · {suffix}" : $"{request.TitleId} · {request.Category}";
        HintText.Text = Loc.S.HintPatchTargetLink;

        _items = [.. request.PatchFileNames.Select(n => new PatchTargetLinkItem(n))];

        TargetTree.ItemsSource = PatchTargetNode.Build(request.CandidatePaths);
        PatchList.ItemsSource = _items;
        PatchList.SelectedIndex = 0;

        UpdateButtons();
    }

    public static IReadOnlyList<string?> Pick(Window? owner, PatchTargetRequest request)
    {
        var window = new PatchTargetPickerWindow(request);

        if (owner != null)
            window.Owner = owner;
        else
            window.WindowStartupLocation = WindowStartupLocation.CenterScreen;

        return window.ShowDialog() == true ? window.Selections : [.. request.PatchFileNames.Select(_ => (string?)null)];
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        IntPtr hWnd = new WindowInteropHelper(this).Handle;
        int value = 1;

        _ = Win32API.DwmSetWindowAttribute(hWnd, 20, ref value, sizeof(int));
    }

    private void TargetTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        _selectedPath = e.NewValue is PatchTargetNode { IsFile: true } node ? node.FullPath : null;

        SelectedText.Text = _selectedPath ?? string.Empty;

        UpdateButtons();
    }

    private void TargetTree_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is FrameworkElement { DataContext: PatchTargetNode { IsFile: true } })
            Link();
    }

    private void PatchList_SelectionChanged(object sender, SelectionChangedEventArgs e) => UpdateButtons();

    private void PatchList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is FrameworkElement { DataContext: PatchTargetLinkItem })
            Link();
    }

    private void LinkButton_Click(object sender, RoutedEventArgs e) => Link();

    private void UnlinkButton_Click(object sender, RoutedEventArgs e)
    {
        if (PatchList.SelectedItem is PatchTargetLinkItem item)
            item.TargetPath = null;

        UpdateButtons();
    }

    private void ApplyButton_Click(object sender, RoutedEventArgs e) => DialogResult = true;

    private void Link()
    {
        if (PatchList.SelectedItem is not PatchTargetLinkItem item || _selectedPath == null)
            return;

        foreach (var other in _items.Where(i => i != item && string.Equals(i.TargetPath, _selectedPath, StringComparison.OrdinalIgnoreCase)))
            other.TargetPath = null;

        item.TargetPath = _selectedPath;

        int index = _items.IndexOf(item);
        var next = _items.Skip(index + 1).Concat(_items.Take(index)).FirstOrDefault(i => !i.IsLinked);

        if (next != null)
        {
            PatchList.SelectedItem = next;
            PatchList.ScrollIntoView(next);
        }

        UpdateButtons();
    }

    private void UpdateButtons()
    {
        var selected = PatchList.SelectedItem as PatchTargetLinkItem;

        LinkButton.IsEnabled = selected != null && _selectedPath != null;
        UnlinkButton.IsEnabled = selected is { IsLinked: true };
        ApplyButton.IsEnabled = _items.Any(i => i.IsLinked);
    }
}