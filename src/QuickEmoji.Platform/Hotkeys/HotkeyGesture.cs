using Windows.Win32.UI.Input.KeyboardAndMouse;

namespace QuickEmoji.Platform.Hotkeys;

/// <summary>A key combination, independent of how it is registered. No <see cref="HOT_KEY_MODIFIERS.MOD_NOREPEAT"/> here; the service adds it.</summary>
public readonly record struct HotkeyGesture(HotkeyModifiers Modifiers, uint VirtualKey)
{
    /// <summary>SPEC §5 default.</summary>
    public static HotkeyGesture Default { get; } = new(HotkeyModifiers.Win | HotkeyModifiers.Shift, VirtualKeyCodes.E);

    public override string ToString() =>
        string.Join('+', Parts());

    private IEnumerable<string> Parts()
    {
        if ((Modifiers & HotkeyModifiers.Win) != 0)
        {
            yield return "Win";
        }

        if ((Modifiers & HotkeyModifiers.Ctrl) != 0)
        {
            yield return "Ctrl";
        }

        if ((Modifiers & HotkeyModifiers.Alt) != 0)
        {
            yield return "Alt";
        }

        if ((Modifiers & HotkeyModifiers.Shift) != 0)
        {
            yield return "Shift";
        }

        yield return VirtualKeyCodes.Name(VirtualKey);
    }
}

/// <summary>Modifier keys, independent of the Win32 <c>MOD_*</c> flags used only at registration time.</summary>
[Flags]
public enum HotkeyModifiers
{
    None = 0,
    Alt = 1,
    Ctrl = 2,
    Shift = 4,
    Win = 8,
}

/// <summary>Display names for the small set of virtual-key codes the hotkey recorder accepts.</summary>
public static class VirtualKeyCodes
{
    public const uint E = 0x45;

    /// <summary>A..Z, 0..9, and F1..F24 — the keys the hotkey recorder offers (SPEC §5 gives no exhaustive list).</summary>
    public static string Name(uint virtualKey) => virtualKey switch
    {
        >= 0x30 and <= 0x39 => ((char)virtualKey).ToString(), // '0'..'9'
        >= 0x41 and <= 0x5A => ((char)virtualKey).ToString(), // 'A'..'Z'
        >= 0x70 and <= 0x87 => $"F{virtualKey - 0x6F}", // VK_F1..VK_F24
        _ => $"0x{virtualKey:X2}",
    };
}
