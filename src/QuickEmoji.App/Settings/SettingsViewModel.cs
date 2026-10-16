using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using QuickEmoji.Core.Emoji;
using QuickEmoji.Core.Insertion;
using QuickEmoji.Core.Search;
using QuickEmoji.Core.Settings;
using QuickEmoji.Platform.Hotkeys;
using Windows.System;

namespace QuickEmoji.App.Settings;

/// <summary>
/// Settings window state (SPEC §6.2). Holds a working copy of <see cref="AppSettings"/>; nothing is persisted
/// or applied to the running app until <see cref="Save"/> (so closing without saving discards edits, including
/// an in-progress hotkey recording).
/// </summary>
public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly Func<HotkeyGesture, HotkeyRegistrationResult> _testRegisterHotkey;
    private readonly Action<AppSettings> _onSave;
    private readonly Action _onClearUsageHistory;
    private readonly Func<string> _buildDiagnostics;
    private readonly Action<string> _copyToClipboard;

    private HotkeyGesture _hotkeyGesture;

    public SettingsViewModel(
        AppSettings initial,
        string appVersion,
        Func<HotkeyGesture, HotkeyRegistrationResult> testRegisterHotkey,
        Action<AppSettings> onSave,
        Action onClearUsageHistory,
        Func<string> buildDiagnostics,
        Action<string> copyToClipboard)
    {
        ArgumentNullException.ThrowIfNull(initial);
        _testRegisterHotkey = testRegisterHotkey;
        _onSave = onSave;
        _onClearUsageHistory = onClearUsageHistory;
        _buildDiagnostics = buildDiagnostics;
        _copyToClipboard = copyToClipboard;

        AppVersion = appVersion;
        _hotkeyGesture = HotkeyGestureMapping.ToGesture(initial.Hotkey);
        HotkeyDisplay = _hotkeyGesture.ToString();
        LaunchAtStartup = initial.LaunchAtStartup;
        PopupPosition = initial.PopupPosition;
        Theme = initial.Theme;
        SearchEnglish = (initial.Languages & SearchLanguages.English) != 0;
        SearchBulgarian = (initial.Languages & SearchLanguages.Bulgarian) != 0;
        PreferredSkinTone = initial.PreferredSkinTone;
        MaxEmojiVersionOverrideText = initial.MaxEmojiVersionOverride?.ToString() ?? "";
        DefaultInsertionMethod = initial.DefaultInsertionMethod;
        ClipboardRestoreDelayMs = initial.ClipboardRestoreDelay.TotalMilliseconds;

        foreach (var o in initial.PerAppOverrides)
        {
            PerAppOverrides.Add(new AppOverrideRowViewModel(o));
        }
    }

    public string AppVersion { get; }

    [ObservableProperty]
    public partial string HotkeyDisplay { get; set; }

    [ObservableProperty]
    public partial bool IsRecordingHotkey { get; set; }

    /// <summary>AltGr warning or a live conflict error from test-registering the candidate (SPEC §5). Null when there is nothing to show.</summary>
    [ObservableProperty]
    public partial string? HotkeyMessage { get; set; }

    [ObservableProperty]
    public partial bool LaunchAtStartup { get; set; }

    [ObservableProperty]
    public partial PopupPosition PopupPosition { get; set; }

    [ObservableProperty]
    public partial AppTheme Theme { get; set; }

    [ObservableProperty]
    public partial bool SearchEnglish { get; set; }

    [ObservableProperty]
    public partial bool SearchBulgarian { get; set; }

    [ObservableProperty]
    public partial SkinTone PreferredSkinTone { get; set; }

    /// <summary>Advanced (SPEC §6.2): empty means "auto" — measured from the font, not a fixed override.</summary>
    [ObservableProperty]
    public partial string MaxEmojiVersionOverrideText { get; set; }

    [ObservableProperty]
    public partial InsertionMethod DefaultInsertionMethod { get; set; }

    /// <summary><c>double</c> to match <c>NumberBox.Value</c> directly — this app avoids XAML value converters (see docs/DECISIONS.md).</summary>
    [ObservableProperty]
    public partial double ClipboardRestoreDelayMs { get; set; }

    public ObservableCollection<AppOverrideRowViewModel> PerAppOverrides { get; } = [];

    public void AddOverrideRow() => PerAppOverrides.Add(new AppOverrideRowViewModel());

    public void RemoveOverrideRow(AppOverrideRowViewModel row) => PerAppOverrides.Remove(row);

    /// <summary>Starts recording: the next non-modifier key press (fed via <see cref="OnRecorderKeyDown"/>) becomes the candidate.</summary>
    public void BeginRecordingHotkey()
    {
        IsRecordingHotkey = true;
        HotkeyMessage = null;
    }

    /// <summary>
    /// Feeds one key-down from the recorder field. While only modifiers are held this returns without changing
    /// anything; once a complete combination arrives, it is validated and test-registered live (SPEC §5).
    /// </summary>
    public void OnRecorderKeyDown(VirtualKey key, bool ctrl, bool alt, bool shift, bool win)
    {
        if (!IsRecordingHotkey)
        {
            return;
        }

        var candidate = HotkeyRecorder.OnKeyDown(key, ctrl, alt, shift, win);
        if (candidate is not { } gesture)
        {
            return;
        }

        IsRecordingHotkey = false;

        switch (HotkeyValidator.Validate(gesture))
        {
            case HotkeyValidation.MissingModifier:
                HotkeyMessage = "A hotkey must include Ctrl, Alt, or Win.";
                return;
            case HotkeyValidation.ValidWithAltGrWarning:
                HotkeyMessage = "Ctrl+Alt combinations act as AltGr on many keyboard layouts.";
                break;
        }

        if (_testRegisterHotkey(gesture) == HotkeyRegistrationResult.Conflict)
        {
            HotkeyMessage = $"{gesture} is already used by another application.";
            return;
        }

        _hotkeyGesture = gesture;
        HotkeyDisplay = gesture.ToString();
    }

    public void ClearUsageHistory() => _onClearUsageHistory();

    public void CopyDiagnostics() => _copyToClipboard(_buildDiagnostics());

    /// <summary>Applies every field as one new <see cref="AppSettings"/> (SPEC §7: persisted and used atomically together).</summary>
    public void Save()
    {
        var languages = SearchLanguages.None;
        if (SearchEnglish)
        {
            languages |= SearchLanguages.English;
        }

        if (SearchBulgarian)
        {
            languages |= SearchLanguages.Bulgarian;
        }

        var settings = new AppSettings
        {
            Hotkey = HotkeyGestureMapping.ToSetting(_hotkeyGesture),
            LaunchAtStartup = LaunchAtStartup,
            PopupPosition = PopupPosition,
            Theme = Theme,
            Languages = languages == SearchLanguages.None ? SearchLanguages.All : languages,
            PreferredSkinTone = PreferredSkinTone,
            MaxEmojiVersionOverride = EmojiVersion.TryParse(MaxEmojiVersionOverrideText.Trim(), out var version) ? version : null,
            DefaultInsertionMethod = DefaultInsertionMethod,
            PerAppOverrides = PerAppOverrides.Where(r => r.IsValid).Select(r => r.ToOverride()).ToList(),
            ClipboardRestoreDelay = TimeSpan.FromMilliseconds(Math.Max(0, (int)ClipboardRestoreDelayMs)),
        };

        _onSave(settings);
    }
}
