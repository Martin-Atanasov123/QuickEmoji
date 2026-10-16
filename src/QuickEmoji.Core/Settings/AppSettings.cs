using QuickEmoji.Core.Emoji;
using QuickEmoji.Core.Insertion;
using QuickEmoji.Core.Search;

namespace QuickEmoji.Core.Settings;

/// <summary>
/// A hotkey combination, independent of how it is registered (SPEC §5). This is Core's own representation —
/// Platform's <c>HotkeyGesture</c> is a separate type with the same shape, because Core may not reference
/// Platform (CLAUDE.md: "Core references nothing project-local"); the composition root converts between them.
/// </summary>
public readonly record struct HotkeySetting(bool Ctrl, bool Alt, bool Shift, bool Win, uint VirtualKey)
{
    /// <summary>SPEC §5: a hotkey must include Ctrl, Alt, or Win — it cannot be a bare key or Shift alone.</summary>
    public bool HasRequiredModifier => Ctrl || Alt || Win;

    /// <summary>SPEC §5: Ctrl+Alt (without Win) acts as AltGr on many European layouts — the recorder warns about this.</summary>
    public bool IsAltGrLike => Ctrl && Alt && !Win;
}

/// <summary>Where the popup appears (SPEC §6.1).</summary>
public enum PopupPosition
{
    NearCaret,
    NearCursor,
    Centered,
}

/// <summary>SPEC §6.1: "System (default), Light, or Dark, using the system accent color."</summary>
public enum AppTheme
{
    System,
    Light,
    Dark,
}

/// <summary>
/// Everything SPEC §5-§7 persists to <c>settings.json</c>. Immutable — a change produces a new instance via
/// <c>with</c>, which is also what makes partial, per-field recovery from a corrupt file straightforward
/// (<see cref="SettingsStore"/> starts from <see cref="Default"/> and only overrides the fields that validate).
/// </summary>
public sealed record AppSettings
{
    public const int CurrentSchemaVersion = 1;

    public static AppSettings Default { get; } = new();

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    public HotkeySetting Hotkey { get; init; } = SettingsDefaults.Hotkey;

    public bool LaunchAtStartup { get; init; } = SettingsDefaults.LaunchAtStartup;

    public PopupPosition PopupPosition { get; init; } = SettingsDefaults.PopupPosition;

    public AppTheme Theme { get; init; } = SettingsDefaults.Theme;

    public SearchLanguages Languages { get; init; } = SettingsDefaults.Languages;

    public SkinTone PreferredSkinTone { get; init; } = SettingsDefaults.PreferredSkinTone;

    /// <summary>Null means "auto" (SPEC §3.2: measured from the installed font, not a fixed value).</summary>
    public EmojiVersion? MaxEmojiVersionOverride { get; init; } = SettingsDefaults.MaxEmojiVersionOverride;

    /// <summary>SPEC §6.2: "default method (Auto or Clipboard)." Auto is <see cref="InsertionMethod.UnicodeInjection"/>; Copy is never a default, only ever elevation-forced or override-chosen.</summary>
    public InsertionMethod DefaultInsertionMethod { get; init; } = SettingsDefaults.DefaultInsertionMethod;

    public IReadOnlyList<AppOverride> PerAppOverrides { get; init; } = [];

    public TimeSpan ClipboardRestoreDelay { get; init; } = TimeSpan.FromMilliseconds(SettingsDefaults.ClipboardRestoreDelayMs);

    // A record's compiler-generated equality compares IReadOnlyList<T> by reference, not content, so two
    // settings loaded independently with an identical override table would never compare equal. Overriding both
    // members (required together) switches PerAppOverrides to a structural SequenceEqual; every other field
    // still compares exactly as the generated version would.
    public bool Equals(AppSettings? other) =>
        other is not null
        && SchemaVersion == other.SchemaVersion
        && Hotkey == other.Hotkey
        && LaunchAtStartup == other.LaunchAtStartup
        && PopupPosition == other.PopupPosition
        && Theme == other.Theme
        && Languages == other.Languages
        && PreferredSkinTone == other.PreferredSkinTone
        && MaxEmojiVersionOverride == other.MaxEmojiVersionOverride
        && DefaultInsertionMethod == other.DefaultInsertionMethod
        && PerAppOverrides.SequenceEqual(other.PerAppOverrides)
        && ClipboardRestoreDelay == other.ClipboardRestoreDelay;

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(SchemaVersion);
        hash.Add(Hotkey);
        hash.Add(LaunchAtStartup);
        hash.Add(PopupPosition);
        hash.Add(Theme);
        hash.Add(Languages);
        hash.Add(PreferredSkinTone);
        hash.Add(MaxEmojiVersionOverride);
        hash.Add(DefaultInsertionMethod);
        foreach (var o in PerAppOverrides)
        {
            hash.Add(o);
        }

        hash.Add(ClipboardRestoreDelay);
        return hash.ToHashCode();
    }
}
