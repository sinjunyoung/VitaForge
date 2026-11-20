using Common;
using Common.Localization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using Vita.Core.Models;
using VitaForge.Core.Models.PSVita;

namespace VitaForge.Views;

public partial class PatchTargetPickerWindow : Window
{
    private string? _selectedPath;

    public string? SelectedPath { get; private set; }

    public PatchTargetPickerWindow(PatchTargetRequest request)
    {
        InitializeComponent();

        HeaderText.Text = Loc.S.MsgPatchTargetNotFound(request.PatchFileName);
        SubText.Text = request.ContentIdSuffix is { Length: > 0 } suffix ? $"{request.TitleId} · {request.Category} · {suffix}" : $"{request.TitleId} · {request.Category}";
        TargetTree.ItemsSource = PatchTargetNode.Build(request.CandidatePaths);
    }

    public static string? Pick(Window? owner, PatchTargetRequest request)
    {
        var window = new PatchTargetPickerWindow(request);

        if (owner != null)
            window.Owner = owner;
        else
            window.WindowStartupLocation = WindowStartupLocation.CenterScreen;

        return window.ShowDialog() == true ? window.SelectedPath : null;
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

        SelectButton.IsEnabled = _selectedPath != null;
        SelectedText.Text = _selectedPath ?? string.Empty;
    }

    private void TargetTree_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (_selectedPath != null && e.OriginalSource is FrameworkElement { DataContext: PatchTargetNode { IsFile: true } })
            Accept();
    }

    private void SelectButton_Click(object sender, RoutedEventArgs e) => Accept();

    private void Accept()
    {
        SelectedPath = _selectedPath;
        DialogResult = true;
    }
}
