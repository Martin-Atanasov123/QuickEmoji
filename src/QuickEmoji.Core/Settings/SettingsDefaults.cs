using QuickEmoji.Core.Emoji;
using QuickEmoji.Core.Insertion;
using QuickEmoji.Core.Search;

namespace QuickEmoji.Core.Settings;

/// <summary>Named default values for every settings field (SPEC §5-§7), so defaults live in one place.</summary>
public static class SettingsDefaults
{
    /// <summary>SPEC §5: "Win+Shift+E", chosen because apps almost never bind Win-modifier combinations.</summary>
    public static HotkeySetting Hotkey { get; } = new(Ctrl: false, Alt: false, Shift: true, Win: true, VirtualKey: VirtualKeys.E);

    public const bool LaunchAtStartup = false;

    public const PopupPosition PopupPosition = Settings.PopupPosition.NearCaret;

    public const AppTheme Theme = AppTheme.System;

    public const SearchLanguages Languages = SearchLanguages.All;

    public const SkinTone PreferredSkinTone = SkinTone.None;

    /// <summary>Null means "auto" — measured from the installed font (SPEC §3.2), not a fixed override.</summary>
    public static EmojiVersion? MaxEmojiVersionOverride { get; } = null;

    public const InsertionMethod DefaultInsertionMethod = InsertionMethod.UnicodeInjection;

    public const int ClipboardRestoreDelayMs = 150;
}

/// <summary>The handful of virtual-key codes the hotkey recorder and defaults need by name.</summary>
public static class VirtualKeys
{
    public const uint E = 0x45;
}
