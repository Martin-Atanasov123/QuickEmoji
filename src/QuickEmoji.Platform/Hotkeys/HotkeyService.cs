using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Input.KeyboardAndMouse;

namespace QuickEmoji.Platform.Hotkeys;

public enum HotkeyRegistrationResult
{
    Registered,

    /// <summary>Another application already owns this combination.</summary>
    Conflict,
}

/// <summary>
/// Owns a message-only window (SPEC §5) and the one hotkey registered on it. Every Win32 call goes through
/// <see cref="MessageOnlyWindow.Invoke"/>: RegisterHotKey and UnregisterHotKey fail with
/// ERROR_WINDOW_OF_OTHER_THREAD unless called from the thread that owns the window, while this service's own
/// public methods may be called from anywhere.
/// <see cref="HotkeyPressed"/> fires on the message-only window's own background thread, <b>not</b> the UI
/// thread — Platform has no dependency on WinUI or its dispatcher. SPEC §5 asks for WM_HOTKEY to be marshalled
/// to the UI thread; the subscriber (App) does that marshaling itself, since only App may reference WinUI
/// (CLAUDE.md: "Platform... NO UI types"). "Receiving WM_HOTKEY grants the right to take the foreground, so the
/// popup must be activated inside the handler" still holds: App's handler, scheduled via its own
/// <c>DispatcherQueue.TryEnqueue</c>, runs promptly enough to keep that right in practice.
/// </summary>
public sealed class HotkeyService : IDisposable
{
    private const int HotkeyId = 1;
    private static int _instanceCount;

    private readonly MessageOnlyWindow _window;
    private bool _registered;
    private bool _paused;
    private bool _disposed;

    public HotkeyService()
    {
        var className = $"QuickEmojiHotkeyWindow_{Interlocked.Increment(ref _instanceCount)}_{Environment.ProcessId}";
        _window = new MessageOnlyWindow(className, OnMessage);
    }

    /// <summary>
    /// Raised on the message-only window's background thread when the hotkey fires. Never raised while
    /// <see cref="Paused"/> is true. Subscribers that touch UI must marshal to their own dispatcher.
    /// </summary>
    public event EventHandler? HotkeyPressed;

    /// <summary>Pausing unregisters the hotkey so other applications can use the combination meanwhile (SPEC §5 tray menu).</summary>
    public bool Paused
    {
        get => _paused;
        set
        {
            if (value == _paused || !_registered)
            {
                _paused = value;
                return;
            }

            if (value)
            {
                _window.Invoke(() => UnregisterHotKey(HotkeyId));
            }
            else
            {
                _window.Invoke(() => RegisterHotKey(HotkeyId, Current!.Value));
            }

            _paused = value;
        }
    }

    public HotkeyGesture? Current { get; private set; }

    /// <summary>The message-only window's handle, for tests that need to post it a message directly.</summary>
    internal nint WindowHandle => _window.Handle;

    /// <summary>
    /// Registers <paramref name="gesture"/>, replacing any hotkey this service previously registered.
    /// Does not validate the gesture; call <see cref="HotkeyValidator"/> first.
    /// </summary>
    public HotkeyRegistrationResult Register(HotkeyGesture gesture)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _window.Invoke(() =>
        {
            if (_registered)
            {
                UnregisterHotKey(HotkeyId);
                _registered = false;
            }

            if (_paused)
            {
                Current = gesture;
                return HotkeyRegistrationResult.Registered;
            }

            if (!RegisterHotKey(HotkeyId, gesture))
            {
                Current = null;
                return HotkeyRegistrationResult.Conflict;
            }

            _registered = true;
            Current = gesture;
            return HotkeyRegistrationResult.Registered;
        });
    }

    /// <summary>
    /// Registers <paramref name="gesture"/> purely to test whether it is free, then immediately releases it,
    /// restoring whatever was registered before (SPEC §5: "test-register the combination live"). Does not affect
    /// <see cref="Current"/> or <see cref="Paused"/>.
    /// </summary>
    public HotkeyRegistrationResult TestRegister(HotkeyGesture gesture)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        const int probeId = HotkeyId + 1;
        return _window.Invoke(() =>
        {
            if (!RegisterHotKey(probeId, gesture))
            {
                return HotkeyRegistrationResult.Conflict;
            }

            UnregisterHotKey(probeId);
            return HotkeyRegistrationResult.Registered;
        });
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_registered)
        {
            _window.Invoke(() => UnregisterHotKey(HotkeyId));
        }

        _window.Dispose();
    }

    private void OnMessage(uint message, WPARAM wParam, LPARAM lParam)
    {
        if (message == PInvoke.WM_HOTKEY && (int)wParam.Value == HotkeyId)
        {
            HotkeyPressed?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Must run on the message-only window's own thread (see <see cref="MessageOnlyWindow.Invoke"/>).</summary>
    private bool RegisterHotKey(int id, HotkeyGesture gesture)
    {
        unsafe
        {
            return PInvoke.RegisterHotKey(new HWND((void*)_window.Handle), id, ToWin32(gesture), gesture.VirtualKey);
        }
    }

    /// <summary>Must run on the message-only window's own thread (see <see cref="MessageOnlyWindow.Invoke"/>).</summary>
    private void UnregisterHotKey(int id)
    {
        unsafe
        {
            PInvoke.UnregisterHotKey(new HWND((void*)_window.Handle), id);
        }
    }

    private static HOT_KEY_MODIFIERS ToWin32(HotkeyGesture gesture)
    {
        var modifiers = HOT_KEY_MODIFIERS.MOD_NOREPEAT;
        if ((gesture.Modifiers & HotkeyModifiers.Alt) != 0)
        {
            modifiers |= HOT_KEY_MODIFIERS.MOD_ALT;
        }

        if ((gesture.Modifiers & HotkeyModifiers.Ctrl) != 0)
        {
            modifiers |= HOT_KEY_MODIFIERS.MOD_CONTROL;
        }

        if ((gesture.Modifiers & HotkeyModifiers.Shift) != 0)
        {
            modifiers |= HOT_KEY_MODIFIERS.MOD_SHIFT;
        }

        if ((gesture.Modifiers & HotkeyModifiers.Win) != 0)
        {
            modifiers |= HOT_KEY_MODIFIERS.MOD_WIN;
        }

        return modifiers;
    }
}
