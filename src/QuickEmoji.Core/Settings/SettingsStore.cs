using System.Text.Json;
using System.Text.Json.Nodes;
using QuickEmoji.Core.Emoji;
using QuickEmoji.Core.Insertion;
using QuickEmoji.Core.IO;
using QuickEmoji.Core.Search;

namespace QuickEmoji.Core.Settings;

/// <summary>Whether loading found the file corrupt (SPEC §7: "show one notification" in that case, never silently).</summary>
public sealed record SettingsLoadResult(AppSettings Settings, bool FileWasCorrupt);

/// <summary>
/// Reads and writes <c>settings.json</c> (SPEC §7). Corrupt JSON (unparseable, or not an object) is quarantined
/// to <c>settings.json.bad-&lt;timestamp&gt;</c> and defaults are used instead. A valid JSON object with an
/// unknown or invalid individual field falls back to that one field's default rather than rejecting the whole
/// file — every field below is read defensively, one at a time, for exactly that reason.
/// </summary>
public sealed class SettingsStore
{
    private readonly IFileSystem _fileSystem;
    private readonly AppPaths _paths;
    private readonly TimeProvider _time;

    public SettingsStore(IFileSystem fileSystem, AppPaths paths, TimeProvider? time = null)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentNullException.ThrowIfNull(paths);
        _fileSystem = fileSystem;
        _paths = paths;
        _time = time ?? TimeProvider.System;
    }

    public SettingsLoadResult Load()
    {
        if (!_fileSystem.FileExists(_paths.SettingsFile))
        {
            return new SettingsLoadResult(AppSettings.Default, FileWasCorrupt: false);
        }

        string text;
        try
        {
            text = _fileSystem.ReadAllText(_paths.SettingsFile);
        }
        catch (IOException)
        {
            return new SettingsLoadResult(AppSettings.Default, FileWasCorrupt: false);
        }

        JsonNode? node;
        try
        {
            node = JsonNode.Parse(text);
        }
        catch (JsonException)
        {
            Quarantine();
            return new SettingsLoadResult(AppSettings.Default, FileWasCorrupt: true);
        }

        if (node is not JsonObject root)
        {
            Quarantine();
            return new SettingsLoadResult(AppSettings.Default, FileWasCorrupt: true);
        }

        var d = AppSettings.Default;
        var settings = new AppSettings
        {
            Hotkey = ReadHotkey(root, d.Hotkey),
            LaunchAtStartup = ReadBool(root, "launchAtStartup", d.LaunchAtStartup),
            PopupPosition = ReadEnum<PopupPosition>(root, "popupPosition", d.PopupPosition),
            Theme = ReadEnum<AppTheme>(root, "theme", d.Theme),
            Languages = ReadEnum<SearchLanguages>(root, "languages", d.Languages),
            PreferredSkinTone = ReadEnum<SkinTone>(root, "preferredSkinTone", d.PreferredSkinTone),
            MaxEmojiVersionOverride = ReadVersion(root, "maxEmojiVersionOverride"),
            DefaultInsertionMethod = ReadDefaultInsertionMethod(root, d.DefaultInsertionMethod),
            PerAppOverrides = ReadOverrides(root, d.PerAppOverrides),
            ClipboardRestoreDelay = ReadDelay(root, d.ClipboardRestoreDelay),
        };

        return new SettingsLoadResult(settings, FileWasCorrupt: false);
    }

    public void Save(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var root = new JsonObject
        {
            ["schemaVersion"] = AppSettings.CurrentSchemaVersion,
            ["hotkey"] = new JsonObject
            {
                ["ctrl"] = settings.Hotkey.Ctrl,
                ["alt"] = settings.Hotkey.Alt,
                ["shift"] = settings.Hotkey.Shift,
                ["win"] = settings.Hotkey.Win,
                ["virtualKey"] = settings.Hotkey.VirtualKey,
            },
            ["launchAtStartup"] = settings.LaunchAtStartup,
            ["popupPosition"] = settings.PopupPosition.ToString(),
            ["theme"] = settings.Theme.ToString(),
            ["languages"] = settings.Languages.ToString(),
            ["preferredSkinTone"] = settings.PreferredSkinTone.ToString(),
            ["maxEmojiVersionOverride"] = settings.MaxEmojiVersionOverride?.ToString(),
            ["defaultInsertionMethod"] = settings.DefaultInsertionMethod.ToString(),
            ["perAppOverrides"] = new JsonArray(settings.PerAppOverrides.Select(o => (JsonNode)new JsonObject
            {
                ["processName"] = o.ProcessName,
                ["method"] = o.Method.ToString(),
                ["pasteKeys"] = o.PasteKeys.ToString(),
            }).ToArray()),
            ["clipboardRestoreDelayMs"] = (int)settings.ClipboardRestoreDelay.TotalMilliseconds,
        };

        AtomicFileWriter.Write(_fileSystem, _paths.SettingsFile, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }

    private void Quarantine()
    {
        try
        {
            _fileSystem.Move(_paths.SettingsFile, $"{_paths.SettingsFile}.bad-{_time.GetUtcNow():yyyyMMddHHmmss}", overwrite: true);
        }
        catch (IOException)
        {
            // Best effort — if even the rename fails, defaults are still returned to the caller.
        }
    }

    private static HotkeySetting ReadHotkey(JsonObject root, HotkeySetting fallback)
    {
        if (root["hotkey"] is not JsonObject h)
        {
            return fallback;
        }

        var ctrl = ReadBool(h, "ctrl", fallback.Ctrl);
        var alt = ReadBool(h, "alt", fallback.Alt);
        var shift = ReadBool(h, "shift", fallback.Shift);
        var win = ReadBool(h, "win", fallback.Win);
        var virtualKey = h["virtualKey"] is JsonValue v && v.TryGetValue(out uint vk) ? vk : fallback.VirtualKey;
        var candidate = new HotkeySetting(ctrl, alt, shift, win, virtualKey);
        return candidate.HasRequiredModifier ? candidate : fallback;
    }

    private static bool ReadBool(JsonObject root, string field, bool fallback) =>
        root[field] is JsonValue v && v.TryGetValue(out bool b) ? b : fallback;

    private static TEnum ReadEnum<TEnum>(JsonObject root, string field, TEnum fallback) where TEnum : struct, Enum =>
        root[field] is JsonValue v && v.TryGetValue(out string? s) && Enum.TryParse<TEnum>(s, ignoreCase: true, out var parsed)
            ? parsed
            : fallback;

    private static EmojiVersion? ReadVersion(JsonObject root, string field) =>
        root[field] is JsonValue v && v.TryGetValue(out string? s) && EmojiVersion.TryParse(s, out var version)
            ? version
            : null;

    private static InsertionMethod ReadDefaultInsertionMethod(JsonObject root, InsertionMethod fallback)
    {
        var method = ReadEnum(root, "defaultInsertionMethod", fallback);
        // SPEC §6.2: the default is only ever Auto (UnicodeInjection) or Clipboard (ClipboardPaste) — Copy is
        // never a chosen default, only ever elevation-forced or per-app-override-chosen (see MethodSelector).
        return method == InsertionMethod.Copy ? fallback : method;
    }

    private static IReadOnlyList<AppOverride> ReadOverrides(JsonObject root, IReadOnlyList<AppOverride> fallback)
    {
        if (root["perAppOverrides"] is not JsonArray array)
        {
            return fallback;
        }

        var result = new List<AppOverride>(array.Count);
        foreach (var item in array)
        {
            if (item is not JsonObject o
                || o["processName"] is not JsonValue pn || !pn.TryGetValue(out string? processName)
                || string.IsNullOrWhiteSpace(processName))
            {
                continue;
            }

            var method = ReadEnum(o, "method", InsertionMethod.UnicodeInjection);
            var pasteKeys = ReadEnum(o, "pasteKeys", PasteKeys.CtrlV);
            result.Add(new AppOverride(processName, method, pasteKeys));
        }

        return result;
    }

    private static TimeSpan ReadDelay(JsonObject root, TimeSpan fallback) =>
        root["clipboardRestoreDelayMs"] is JsonValue v && v.TryGetValue(out int ms) && ms >= 0
            ? TimeSpan.FromMilliseconds(ms)
            : fallback;
}
