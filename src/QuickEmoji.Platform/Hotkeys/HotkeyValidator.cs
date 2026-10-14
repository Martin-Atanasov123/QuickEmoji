namespace QuickEmoji.Platform.Hotkeys;

public enum HotkeyValidation
{
    /// <summary>No modifier, or a bare letter/number/function key alone.</summary>
    MissingModifier,

    /// <summary>Valid, but Ctrl+Alt combinations act as AltGr on many European keyboard layouts (SPEC §5).</summary>
    ValidWithAltGrWarning,

    Valid,
}

/// <summary>
/// Pure validation of a candidate hotkey (SPEC §5): the combination must include Ctrl, Alt or Win, and
/// Ctrl+Alt (without Win) is flagged because it collides with AltGr on many layouts.
/// </summary>
public static class HotkeyValidator
{
    public static HotkeyValidation Validate(HotkeyGesture gesture)
    {
        if (gesture.Modifiers == HotkeyModifiers.None)
        {
            return HotkeyValidation.MissingModifier;
        }

        var isCtrlAlt = (gesture.Modifiers & (HotkeyModifiers.Ctrl | HotkeyModifiers.Alt)) == (HotkeyModifiers.Ctrl | HotkeyModifiers.Alt);
        var hasWin = (gesture.Modifiers & HotkeyModifiers.Win) != 0;
        return isCtrlAlt && !hasWin ? HotkeyValidation.ValidWithAltGrWarning : HotkeyValidation.Valid;
    }
}
