using Microsoft.Extensions.Time.Testing;
using QuickEmoji.Core.Emoji;
using QuickEmoji.Core.Insertion;
using QuickEmoji.Core.Search;
using QuickEmoji.Core.Settings;
using QuickEmoji.Core.Tests.IO;

namespace QuickEmoji.Core.Tests.Settings;

public sealed class SettingsStoreTests
{
    private static readonly DateTimeOffset FixedNow = new(2026, 10, 16, 12, 0, 0, TimeSpan.Zero);

    private static (SettingsStore Store, FakeFileSystem FileSystem, AppPaths Paths) Make()
    {
        var fs = new FakeFileSystem();
        var paths = new AppPaths(@"C:\fake");
        return (new SettingsStore(fs, paths, new FakeTimeProvider(FixedNow)), fs, paths);
    }

    [Fact]
    public void No_file_yet_returns_defaults_and_is_not_reported_as_corrupt()
    {
        var (store, _, _) = Make();

        var result = store.Load();

        Assert.Equal(AppSettings.Default, result.Settings);
        Assert.False(result.FileWasCorrupt);
    }

    [Fact]
    public void Saving_then_loading_round_trips_every_field()
    {
        var (store, _, _) = Make();
        var settings = new AppSettings
        {
            Hotkey = new HotkeySetting(Ctrl: true, Alt: false, Shift: false, Win: false, VirtualKey: 0x4C),
            LaunchAtStartup = true,
            PopupPosition = PopupPosition.Centered,
            Theme = AppTheme.Dark,
            Languages = SearchLanguages.Bulgarian,
            PreferredSkinTone = SkinTone.MediumDark,
            MaxEmojiVersionOverride = new EmojiVersion(15, 1),
            DefaultInsertionMethod = InsertionMethod.ClipboardPaste,
            PerAppOverrides = [new AppOverride("mstsc", InsertionMethod.ClipboardPaste, PasteKeys.ShiftInsert)],
            ClipboardRestoreDelay = TimeSpan.FromMilliseconds(400),
        };

        store.Save(settings);
        var loaded = store.Load();

        Assert.Equal(settings, loaded.Settings);
        Assert.False(loaded.FileWasCorrupt);
    }

    [Fact]
    public void Unparseable_json_is_quarantined_with_a_timestamped_name_and_defaults_are_returned()
    {
        var (store, fs, paths) = Make();
        fs.Set(paths.SettingsFile, "{ not valid json");

        var result = store.Load();

        Assert.Equal(AppSettings.Default, result.Settings);
        Assert.True(result.FileWasCorrupt);
        Assert.Null(fs.Get(paths.SettingsFile));
        Assert.Equal("{ not valid json", fs.Get($"{paths.SettingsFile}.bad-{FixedNow:yyyyMMddHHmmss}"));
    }

    [Fact]
    public void A_json_array_instead_of_an_object_is_also_quarantined_as_corrupt()
    {
        var (store, fs, paths) = Make();
        fs.Set(paths.SettingsFile, "[1, 2, 3]");

        var result = store.Load();

        Assert.Equal(AppSettings.Default, result.Settings);
        Assert.True(result.FileWasCorrupt);
    }

    [Fact]
    public void An_unknown_field_is_ignored_and_the_rest_of_the_file_still_loads()
    {
        var (store, fs, paths) = Make();
        fs.Set(paths.SettingsFile, """
            { "launchAtStartup": true, "somethingFromAFutureVersion": 42 }
            """);

        var result = store.Load();

        Assert.False(result.FileWasCorrupt);
        Assert.True(result.Settings.LaunchAtStartup);
    }

    [Fact]
    public void A_field_with_the_wrong_type_falls_back_to_the_default_for_that_field_only()
    {
        var (store, fs, paths) = Make();
        fs.Set(paths.SettingsFile, """
            { "launchAtStartup": "yes please", "theme": "Dark" }
            """);

        var result = store.Load();

        Assert.False(result.FileWasCorrupt);
        Assert.Equal(AppSettings.Default.LaunchAtStartup, result.Settings.LaunchAtStartup);
        Assert.Equal(AppTheme.Dark, result.Settings.Theme);
    }

    [Fact]
    public void An_unrecognized_enum_value_falls_back_to_the_default()
    {
        var (store, fs, paths) = Make();
        fs.Set(paths.SettingsFile, """
            { "theme": "Rainbow" }
            """);

        var result = store.Load();

        Assert.Equal(AppSettings.Default.Theme, result.Settings.Theme);
    }

    [Fact]
    public void A_hotkey_missing_its_required_modifier_falls_back_to_the_default_hotkey()
    {
        var (store, fs, paths) = Make();
        fs.Set(paths.SettingsFile, """
            { "hotkey": { "ctrl": false, "alt": false, "shift": true, "win": false, "virtualKey": 69 } }
            """);

        var result = store.Load();

        Assert.Equal(AppSettings.Default.Hotkey, result.Settings.Hotkey);
    }

    [Fact]
    public void A_stored_default_method_of_Copy_is_rejected_in_favor_of_the_real_default()
    {
        var (store, fs, paths) = Make();
        fs.Set(paths.SettingsFile, """
            { "defaultInsertionMethod": "Copy" }
            """);

        var result = store.Load();

        Assert.Equal(AppSettings.Default.DefaultInsertionMethod, result.Settings.DefaultInsertionMethod);
    }

    [Fact]
    public void A_malformed_override_entry_is_skipped_but_valid_ones_still_load()
    {
        var (store, fs, paths) = Make();
        fs.Set(paths.SettingsFile, """
            { "perAppOverrides": [ { "method": "ClipboardPaste" }, { "processName": "mstsc", "method": "ClipboardPaste", "pasteKeys": "ShiftInsert" } ] }
            """);

        var result = store.Load();

        Assert.Single(result.Settings.PerAppOverrides);
        Assert.Equal("mstsc", result.Settings.PerAppOverrides[0].ProcessName);
    }

    [Fact]
    public void A_negative_clipboard_delay_falls_back_to_the_default()
    {
        var (store, fs, paths) = Make();
        fs.Set(paths.SettingsFile, """
            { "clipboardRestoreDelayMs": -5 }
            """);

        var result = store.Load();

        Assert.Equal(AppSettings.Default.ClipboardRestoreDelay, result.Settings.ClipboardRestoreDelay);
    }
}
