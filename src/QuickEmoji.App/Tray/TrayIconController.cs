using CommunityToolkit.Mvvm.Input;
using H.NotifyIcon;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.UI.ViewManagement;

namespace QuickEmoji.App.Tray;

/// <summary>
/// The tray icon and its menu (SPEC §6.3). Light/dark variants switch with the taskbar theme; H.NotifyIcon
/// recreates the icon itself after Explorer restarts (<c>TaskbarCreated</c>), which Stage 6 verifies on a real
/// desktop as part of hardening.
/// </summary>
public sealed class TrayIconController : IDisposable
{
    private readonly TaskbarIcon _icon;
    private readonly UISettings _uiSettings = new();
    private readonly MenuFlyoutItem _pauseItem;
    private bool _disposed;

    public TrayIconController(string assetsDirectory, bool startPaused)
    {
        _icon = new TaskbarIcon
        {
            ToolTipText = "QuickEmoji — Win+Shift+E",
            ContextMenuMode = ContextMenuMode.SecondWindow,
        };

        var openItem = new MenuFlyoutItem { Text = "Open" };
        openItem.Click += (_, _) => OpenRequested?.Invoke(this, EventArgs.Empty);

        var settingsItem = new MenuFlyoutItem { Text = "Settings" };
        settingsItem.Click += (_, _) => SettingsRequested?.Invoke(this, EventArgs.Empty);

        _pauseItem = new MenuFlyoutItem { Text = PauseText(startPaused) };
        _pauseItem.Click += (_, _) => PauseToggleRequested?.Invoke(this, EventArgs.Empty);

        var quitItem = new MenuFlyoutItem { Text = "Quit" };
        quitItem.Click += (_, _) => QuitRequested?.Invoke(this, EventArgs.Empty);

        _icon.ContextFlyout = new MenuFlyout
        {
            Items = { openItem, settingsItem, _pauseItem, new MenuFlyoutSeparator(), quitItem },
        };

        // TaskbarIcon has no left-click event; left-click is command-bound (SPEC §6.3: left-click opens the popup).
        _icon.LeftClickCommand = new RelayCommand(() => OpenRequested?.Invoke(this, EventArgs.Empty));
        _uiSettings.ColorValuesChanged += (_, _) => _icon.DispatcherQueue.TryEnqueue(() => ApplyIcon(assetsDirectory));

        ApplyIcon(assetsDirectory);
        _icon.ForceCreate();
    }

    /// <summary>Left-click, or the tray menu's "Open": SPEC §6.3 opens the popup centered.</summary>
    public event EventHandler? OpenRequested;

    public event EventHandler? SettingsRequested;

    public event EventHandler? PauseToggleRequested;

    public event EventHandler? QuitRequested;

    public void SetPaused(bool paused)
    {
        _pauseItem.Text = PauseText(paused);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _icon.Dispose();
    }

    /// <summary>Dark glyph for a light taskbar, light glyph for a dark taskbar (SPEC §6.3).</summary>
    private void ApplyIcon(string assetsDirectory)
    {
        var fileName = IsDarkTheme() ? "tray-dark.ico" : "tray-light.ico";
        _icon.IconSource = new BitmapImage(new Uri(Path.Combine(assetsDirectory, fileName)));
    }

    private bool IsDarkTheme()
    {
        var background = _uiSettings.GetColorValue(UIColorType.Background);
        // Perceived luminance; the taskbar follows the system background color regardless of the app's own theme.
        var luminance = ((0.299 * background.R) + (0.587 * background.G) + (0.114 * background.B)) / 255.0;
        return luminance < 0.5;
    }

    private static string PauseText(bool paused) => paused ? "Resume hotkey" : "Pause hotkey";
}
