using QuickEmoji.Platform.Hotkeys;
using Windows.System;

namespace QuickEmoji.App.Settings;

/// <summary>
/// Turns key-down events from the Settings window's hotkey recorder field into a candidate
/// <see cref="HotkeyGesture"/> (SPEC §5). Modifier-only presses (Ctrl/Alt/Shift/Win alone) are not a complete
/// combination by themselves — recording waits for the next non-modifier key while they are held.
/// </summary>
public sealed class HotkeyRecorder
{
    /// <summary>
    /// One key-down, with the modifier keys currently held. Returns the candidate gesture once
    /// <paramref name="key"/> is itself a non-modifier key; null while only modifiers have been pressed so far.
    /// </summary>
    public static HotkeyGesture? OnKeyDown(VirtualKey key, bool ctrl, bool alt, bool shift, bool win)
    {
        if (IsModifierKey(key))
        {
            return null;
        }

        var modifiers = HotkeyModifiers.None;
        if (ctrl)
        {
            modifiers |= HotkeyModifiers.Ctrl;
        }

        if (alt)
        {
            modifiers |= HotkeyModifiers.Alt;
        }

        if (shift)
        {
            modifiers |= HotkeyModifiers.Shift;
        }

        if (win)
        {
            modifiers |= HotkeyModifiers.Win;
        }

        return new HotkeyGesture(modifiers, (uint)key);
    }

    private static bool IsModifierKey(VirtualKey key) => key is
        VirtualKey.Control or VirtualKey.LeftControl or VirtualKey.RightControl or
        VirtualKey.Menu or VirtualKey.LeftMenu or VirtualKey.RightMenu or
        VirtualKey.Shift or VirtualKey.LeftShift or VirtualKey.RightShift or
        VirtualKey.LeftWindows or VirtualKey.RightWindows;
}
