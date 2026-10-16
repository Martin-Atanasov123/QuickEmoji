using QuickEmoji.Core.Settings;
using QuickEmoji.Platform.Hotkeys;

namespace QuickEmoji.App.Settings;

/// <summary>
/// Converts between Core's <see cref="HotkeySetting"/> (the persisted, Windows-free settings field) and
/// Platform's <see cref="HotkeyGesture"/> (what <c>HotkeyService</c>/<c>HotkeyValidator</c> actually operate
/// on). Core may not reference Platform (CLAUDE.md), so this boundary conversion lives in App, the only layer
/// that references both.
/// </summary>
public static class HotkeyGestureMapping
{
    public static HotkeyGesture ToGesture(HotkeySetting setting)
    {
        var modifiers = HotkeyModifiers.None;
        if (setting.Ctrl)
        {
            modifiers |= HotkeyModifiers.Ctrl;
        }

        if (setting.Alt)
        {
            modifiers |= HotkeyModifiers.Alt;
        }

        if (setting.Shift)
        {
            modifiers |= HotkeyModifiers.Shift;
        }

        if (setting.Win)
        {
            modifiers |= HotkeyModifiers.Win;
        }

        return new HotkeyGesture(modifiers, setting.VirtualKey);
    }

    public static HotkeySetting ToSetting(HotkeyGesture gesture) => new(
        Ctrl: (gesture.Modifiers & HotkeyModifiers.Ctrl) != 0,
        Alt: (gesture.Modifiers & HotkeyModifiers.Alt) != 0,
        Shift: (gesture.Modifiers & HotkeyModifiers.Shift) != 0,
        Win: (gesture.Modifiers & HotkeyModifiers.Win) != 0,
        VirtualKey: gesture.VirtualKey);
}
