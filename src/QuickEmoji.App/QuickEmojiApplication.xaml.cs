using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using QuickEmoji.App.Popup;
using QuickEmoji.App.Toast;
using QuickEmoji.App.Tray;
using QuickEmoji.Core.Emoji;
using QuickEmoji.Core.Personalization;
using QuickEmoji.Core.Search;
using QuickEmoji.Platform.Clipboard;
using QuickEmoji.Platform.Display;
using QuickEmoji.Platform.Hotkeys;
using QuickEmoji.Platform.Targeting;
using Serilog;

namespace QuickEmoji.App;

/// <summary>
/// The composition root (CLAUDE.md: "App... thin: it calls Core and Platform"). Wires the catalog, search
/// engine, hotkey, popup, toast and tray together. Stage 3: committing an emoji copies it to the clipboard and
/// shows a toast; Stage 4 replaces that with real insertion.
/// </summary>
public partial class QuickEmojiApplication : Application
{
    private readonly LaunchOptions _options;
    private readonly SingleInstanceGuard _singleInstance;

    private HotkeyService? _hotkeyService;
    private PopupWindow? _popup;
    private ToastWindow? _toast;
    private TrayIconController? _tray;

    internal QuickEmojiApplication(LaunchOptions options, SingleInstanceGuard singleInstance)
    {
        _options = options;
        _singleInstance = singleInstance;
        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        var dispatcher = DispatcherQueue.GetForCurrentThread();

        var catalog = EmojiCatalogLoader.LoadEmbedded();
        var engine = new EmojiSearchEngine(catalog, new FrecencyStore());
        var clipboard = new ClipboardGuard();

        _toast = new ToastWindow();
        _popup = new PopupWindow(engine, clipboard, ShowToast);

        _hotkeyService = new HotkeyService();
        _hotkeyService.HotkeyPressed += (_, _) => dispatcher.TryEnqueue(OnHotkeyPressed);
        if (_hotkeyService.Register(HotkeyGesture.Default) == HotkeyRegistrationResult.Conflict)
        {
            // SPEC §5: "show a tray notification and open Settings at the hotkey field." Settings arrives in
            // Stage 5; until then this is logged loudly rather than silently failing (see docs/DECISIONS.md).
            Log.Warning("Default hotkey {Gesture} could not be registered: another application already owns it", HotkeyGesture.Default);
        }

        var assetsDirectory = Path.Combine(AppContext.BaseDirectory, "Assets");
        _tray = new TrayIconController(assetsDirectory, startPaused: false);
        _tray.OpenRequested += (_, _) => dispatcher.TryEnqueue(ShowPopupFromTray);
        _tray.SettingsRequested += (_, _) => dispatcher.TryEnqueue(ShowPopupFromTray); // Stage 5 replaces with a real Settings window.
        _tray.PauseToggleRequested += (_, _) => dispatcher.TryEnqueue(TogglePause);
        _tray.QuitRequested += (_, _) => dispatcher.TryEnqueue(ShutdownAndExit);

        _singleInstance.AnotherInstanceStarted += (_, _) => dispatcher.TryEnqueue(ShowPopupFromTray); // SPEC §8: a second launch should open Settings; see docs/DECISIONS.md.

        if (_options.StartupCheck)
        {
            // Proves the full composition — catalog, hotkey, popup, toast, tray — wires up and tears down cleanly.
            Log.Information("Startup check passed; exiting");
            dispatcher.TryEnqueue(DispatcherQueuePriority.Low, ShutdownAndExit);
            return;
        }

        Log.Information("QuickEmoji ready; hotkey {Gesture}", HotkeyGesture.Default);
    }

    private void OnHotkeyPressed()
    {
        // Capture happens inside the hotkey handler, the only moment the right window is guaranteed foreground (SPEC §4.1, §5).
        var target = TargetCapture.Capture();
        _popup!.Show(PopupPositionMode.NearCaret, target);
    }

    /// <summary>SPEC §6.3: left-click (or the tray menu's Open) opens the popup centered — there is no caret target.</summary>
    private void ShowPopupFromTray() => _popup!.Show(PopupPositionMode.Centered, target: null);

    private void ShowToast(string message)
    {
        var position = _popup!.AppWindow.Position;
        var size = _popup.AppWindow.Size;
        _toast!.Show(message, new Point(position.X, position.Y + size.Height + 8));
    }

    private void TogglePause()
    {
        if (_hotkeyService is null)
        {
            return;
        }

        _hotkeyService.Paused = !_hotkeyService.Paused;
        _tray?.SetPaused(_hotkeyService.Paused);
        Log.Information("Hotkey {State}", _hotkeyService.Paused ? "paused" : "resumed");
    }

    private void ShutdownAndExit()
    {
        _hotkeyService?.Dispose();
        _tray?.Dispose();
        _singleInstance.Dispose();
        Exit();
    }
}
