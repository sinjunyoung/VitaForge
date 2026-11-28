using Common;
using System.Windows;
using System.Windows.Interop;
using Vita.Core.Models;

namespace VitaForge.Views;

public partial class EnterButtonWindow : Window
{
    public EnterButtonWindow(EnterButton current)
    {
        InitializeComponent();

        (current == EnterButton.Cross ? CrossRadio : CircleRadio).IsChecked = true;
    }

    public EnterButton Selected => CrossRadio.IsChecked == true ? EnterButton.Cross : EnterButton.Circle;

    public static EnterButton? Pick(Window? owner, EnterButton current)
    {
        var window = new EnterButtonWindow(current);

        if (owner != null)
            window.Owner = owner;
        else
            window.WindowStartupLocation = WindowStartupLocation.CenterScreen;

        return window.ShowDialog() == true ? window.Selected : null;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        IntPtr hWnd = new WindowInteropHelper(this).Handle;
        int value = 1;

        _ = Win32API.DwmSetWindowAttribute(hWnd, 20, ref value, sizeof(int));
    }

    private void Radio_Checked(object sender, RoutedEventArgs e)
    {
        if (HintText != null)
            HintText.Visibility = CrossRadio.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OkButton_Click(object sender, RoutedEventArgs e) => DialogResult = true;
}