using System.Reflection;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using QuickEmoji.App.Diagnostics;
using QuickEmoji.App.FirstRun;
using QuickEmoji.App.Insertion;
using QuickEmoji.App.Perf;
using QuickEmoji.App.Popup;
using QuickEmoji.App.Reliability;
using QuickEmoji.App.Settings;
using QuickEmoji.App.Toast;
using QuickEmoji.App.Tray;
using QuickEmoji.Core;
using QuickEmoji.Core.Emoji;
using QuickEmoji.Core.IO;
using QuickEmoji.Core.Personalization;
using QuickEmoji.Core.Search;
using QuickEmoji.Core.Settings;
using QuickEmoji.Platform.Clipboard;
using QuickEmoji.Platform.Display;
using QuickEmoji.Platform.Fonts;
using QuickEmoji.Platform.Hotkeys;
using QuickEmoji.Platform.Startup;
using QuickEmoji.Platform.Targeting;
using Serilog;

namespace QuickEmoji.App;

/// <summary>
/// The composition root (CLAUDE.md: "App... thin: it calls Core and Platform"). Wires the catalog, search
/// engine, hotkey, popup, toast, tray, insertion engine, and (Stage 5) settings/first-run/startup together.
/// </summary>
public partial class QuickEmojiApplication : Application
{
    private readonly LaunchOptions _options;
    private readonly SingleInstanceGuard _singleInstance;
    private readonly AppPaths _paths = AppPaths.ForCurrentUser();
    private readonly IFileSystem _fileSystem = new PhysicalFileSystem();
    private readonly InsertionOutcomeLog _outcomeLog = new();

    private SettingsStore? _settingsStore;
    private UsageStore? _usageStore;
    private AppSettings _settings = AppSettings.Default;
    private FrecencyStore? _frecency;

    private HotkeyService? _hotkeyService;
    private HotkeyGesture _currentHotkey;
    private PopupWindow? _popup;
    private ToastWindow? _toast;
    private TrayIconController? _tray;
    private InsertionCoordinator? _insertion;
    private ClipboardGuard? _clipboard;

    internal QuickEmojiApplication(LaunchOptions options, SingleInstanceGuard singleInstance)
    {
        _options = options;
        _singleInstance = singleInstance;
        InitializeComponent();
        GlobalExceptionHandlers.InstallFor(this);
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        var dispatcher = DispatcherQueue.GetForCurrentThread();

        var isFirstRun = !_fileSystem.FileExists(_paths.SettingsFile);
        _settingsStore = new SettingsStore(_fileSystem, _paths);
        _usageStore = new UsageStore(_fileSystem, _paths);

        var settingsLoad = _settingsStore.Load();
        _settings = settingsLoad.Settings;
        var usageWasCorrupt = _usageStore.Load(out var frecency);
        _frecency = frecency;

        var rawCatalog = EmojiCatalogLoader.LoadEmbedded();
        var filteredCatalog = ApplyFontFilter(rawCatalog, _settings.MaxEmojiVersionOverride);
        var engine = new EmojiSearchEngine(filteredCatalog, _frecency);

        _clipboard = new ClipboardGuard();
        _insertion = new InsertionCoordinator(_clipboard, ShowToast, _outcomeLog.Record)
        {
            Overrides = _settings.PerAppOverrides,
            ClipboardRestoreDelay = _settings.ClipboardRestoreDelay,
            DefaultMethod = _settings.DefaultInsertionMethod,
        };

        _toast = new ToastWindow();
        _popup = new PopupWindow(engine, _insertion, _clipboard, ShowToast);
        _popup.ViewModel.Settings = _settings;

        if (settingsLoad.FileWasCorrupt || usageWasCorrupt)
        {
            // SPEC §7: "show one notification" when a corrupt file had to be quarantined and defaults used.
            ShowToast("Your settings or usage history was reset because the saved file was unreadable.");
        }

        _hotkeyService = new HotkeyService();
        _hotkeyService.HotkeyPressed += (_, _) => dispatcher.TryEnqueue(OnHotkeyPressed);
        _currentHotkey = HotkeyGestureMapping.ToGesture(_settings.Hotkey);
        if (_hotkeyService.Register(_currentHotkey) == HotkeyRegistrationResult.Conflict)
        {
            // SPEC §5: "show a tray notification and open Settings at the hotkey field." No real Settings
            // window existed before Stage 5 (see docs/DECISIONS.md); now there is one, so this opens it.
            Log.Warning("Hotkey {Gesture} could not be registered: another application already owns it", _currentHotkey);
            dispatcher.TryEnqueue(() => OpenSettings());
        }

        var assetsDirectory = Path.Combine(AppContext.BaseDirectory, "Assets");
        _tray = new TrayIconController(assetsDirectory, startPaused: false);
        _tray.OpenRequested += (_, _) => dispatcher.TryEnqueue(ShowPopupFromTray);
        _tray.SettingsRequested += (_, _) => dispatcher.TryEnqueue(() => OpenSettings());
        _tray.PauseToggleRequested += (_, _) => dispatcher.TryEnqueue(TogglePause);
        _tray.QuitRequested += (_, _) => dispatcher.TryEnqueue(ShutdownAndExit);

        _singleInstance.AnotherInstanceStarted += (_, _) => dispatcher.TryEnqueue(() => OpenSettings()); // SPEC §8: a second launch opens Settings.

        SyncStartupRegistration();

        if (_options.StartupCheck)
        {
            // Proves the full composition — catalog, hotkey, popup, toast, tray, settings/usage persistence, and
            // (Stage 5) the Settings and first-run windows — wires up and tears down cleanly. Unit tests cannot
            // catch a XAML parse or binding error (they never construct a real WinUI window); this does.
            OpenSettings().Close();
            ShowFirstRun().Close();
            Log.Information("Startup check passed; exiting");
            dispatcher.TryEnqueue(DispatcherQueuePriority.Low, ShutdownAndExit);
            return;
        }

        Log.Information("QuickEmoji ready; hotkey {Gesture}", _currentHotkey);

        if (_options.PerfTrace)
        {
            PerfTrace.LogColdStart(PerfTrace.ElapsedSinceProcessStart());
            // A few seconds after "ready" rather than immediately: SPEC §9's "idle working set" means settled
            // idle state, not the transient peak of initial JIT/GC activity right as startup finishes. Logging
            // needs no UI thread, so this runs on a plain background task rather than the dispatcher.
            _ = Task.Run(async () =>
            {
                await Task.Delay(TimeSpan.FromSeconds(3));
                PerfTrace.LogWorkingSet();
            });
        }

        if (isFirstRun)
        {
            dispatcher.TryEnqueue(() => ShowFirstRun());
        }
    }

    private void OnHotkeyPressed()
    {
        // Capture happens inside the hotkey handler, the only moment the right window is guaranteed foreground (SPEC §4.1, §5).
        var target = TargetCapture.Capture();
        Log.Debug("Hotkey pressed; captured target {Process}", target.ProcessName);
        _popup!.Show(MapPosition(_settings.PopupPosition), target);
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

    private SettingsWindow OpenSettings()
    {
        var viewModel = new SettingsViewModel(
            _settings,
            AppVersion(),
            testRegisterHotkey: gesture => _hotkeyService!.TestRegister(gesture),
            onSave: ApplySettings,
            onClearUsageHistory: ClearUsageHistory,
            buildDiagnostics: () => DiagnosticsReport.Build(AppVersion(), _currentHotkey.ToString(), _hotkeyService!.Paused, _outcomeLog.Recent()),
            copyToClipboard: text => _clipboard!.Copy(text));

        var window = new SettingsWindow(viewModel);
        window.Activate();
        return window;
    }

    private FirstRunWindow ShowFirstRun()
    {
        var window = new FirstRunWindow(_currentHotkey.ToString(), launchAtStartup => ApplySettings(_settings with { LaunchAtStartup = launchAtStartup }));
        window.Activate();
        return window;
    }

    /// <summary>Applies every Settings field to the running app and persists it (SPEC §7) — called on Settings "Save" and after first run.</summary>
    private void ApplySettings(AppSettings newSettings)
    {
        _settings = newSettings;
        _settingsStore!.Save(newSettings);

        _popup!.ViewModel.Settings = newSettings;
        _insertion!.Overrides = newSettings.PerAppOverrides;
        _insertion!.ClipboardRestoreDelay = newSettings.ClipboardRestoreDelay;
        _insertion!.DefaultMethod = newSettings.DefaultInsertionMethod;

        var gesture = HotkeyGestureMapping.ToGesture(newSettings.Hotkey);
        if (!gesture.Equals(_currentHotkey))
        {
            if (_hotkeyService!.Register(gesture) == HotkeyRegistrationResult.Conflict)
            {
                Log.Warning("Hotkey {Gesture} could not be registered: another application already owns it", gesture);
            }
            else
            {
                _currentHotkey = gesture;
            }
        }

        var rawCatalog = EmojiCatalogLoader.LoadEmbedded();
        var filteredCatalog = ApplyFontFilter(rawCatalog, newSettings.MaxEmojiVersionOverride);
        _popup.ViewModel.UpdateEngine(new EmojiSearchEngine(filteredCatalog, _frecency!));

        SyncStartupRegistration();
    }

    private void ClearUsageHistory()
    {
        _frecency?.Clear();
        if (_frecency is not null)
        {
            _usageStore?.Save(_frecency);
        }
    }

    private void SyncStartupRegistration()
    {
        if (Environment.ProcessPath is not { } exePath)
        {
            return;
        }

        if (_settings.LaunchAtStartup)
        {
            RunKeyStartup.Enable(exePath);
        }
        else
        {
            RunKeyStartup.Disable();
        }
    }

    /// <summary>
    /// SPEC §3.2: Segoe UI Emoji cannot draw every sequence the catalog ships, and which version it caps out at
    /// is a property of the installed font, not the OS build — measured here, not looked up from a table (see
    /// docs/DECISIONS.md, Stage 1). <paramref name="overrideVersion"/> is Settings → Search → "max emoji version
    /// (advanced)"; it replaces the measured cap but never the individually-unrenderable exclusions.
    /// </summary>
    private static EmojiCatalog ApplyFontFilter(EmojiCatalog catalog, EmojiVersion? overrideVersion)
    {
        using var probe = EmojiFontProbe.TryCreate();
        if (probe is null)
        {
            return catalog;
        }

        var coverage = probe.Measure(catalog.AllSequences());
        var effectiveMax = overrideVersion ?? coverage.MaxEmojiVersion;
        return catalog.Apply(new EmojiCatalogFilter(effectiveMax, coverage.Unrenderable));
    }

    private static PopupPositionMode MapPosition(PopupPosition position) => position switch
    {
        PopupPosition.NearCursor => PopupPositionMode.NearCursor,
        PopupPosition.Centered => PopupPositionMode.Centered,
        _ => PopupPositionMode.NearCaret,
    };

    private static string AppVersion() =>
        Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "unknown";

    private void ShutdownAndExit()
    {
        if (_frecency is not null)
        {
            _usageStore?.Save(_frecency);
        }

        _hotkeyService?.Dispose();
        _tray?.Dispose();
        _singleInstance.Dispose();
        Exit();
    }
}
