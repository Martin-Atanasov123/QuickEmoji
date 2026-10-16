using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using QuickEmoji.Platform.Display;
using WinRT.Interop;

namespace QuickEmoji.App.FirstRun;

/// <summary>
/// The first-run welcome window (SPEC §6.4): shows the hotkey, a field to try it on immediately (the hotkey is
/// global, so simply focusing this window and pressing it works exactly like any other app), and the
/// launch-at-startup toggle — off by default, so enabling it is the user's explicit choice.
/// </summary>
public sealed partial class FirstRunWindow : Window
{
    private readonly Action<bool> _onContinue;

    public FirstRunWindow(string hotkeyDisplay, Action<bool> onContinue)
    {
        _onContinue = onContinue;
        InitializeComponent();

        var hwnd = WindowNative.GetWindowHandle(this);
        AppWindow.Title = "Welcome to QuickEmoji";
        AppWindow.Resize(new Windows.Graphics.SizeInt32(420, 420));
        if (DesktopAcrylicController.IsSupported())
        {
            SystemBackdrop = new MicaBackdrop();
        }

        WindowChrome.ApplyRoundedCorners(hwnd);
        HotkeyText.Text = hotkeyDisplay;
    }

    private void GotIt_Click(object sender, RoutedEventArgs e)
    {
        _onContinue(LaunchAtStartupCheckBox.IsChecked == true);
        Close();
    }
}
