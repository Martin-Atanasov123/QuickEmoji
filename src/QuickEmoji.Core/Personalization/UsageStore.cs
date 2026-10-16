using System.Text.Json;
using System.Text.Json.Nodes;
using QuickEmoji.Core.IO;
using QuickEmoji.Core.Settings;

namespace QuickEmoji.Core.Personalization;

/// <summary>
/// Reads and writes <c>usage.json</c> (SPEC §7: frecency data), so picks survive a restart. Same corruption
/// handling as <see cref="SettingsStore"/>: an unparseable file is quarantined and loading falls back to an
/// empty, fresh store; one malformed entry is skipped rather than failing the whole load.
/// </summary>
public sealed class UsageStore
{
    private readonly IFileSystem _fileSystem;
    private readonly AppPaths _paths;
    private readonly TimeProvider _time;

    public UsageStore(IFileSystem fileSystem, AppPaths paths, TimeProvider? time = null)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentNullException.ThrowIfNull(paths);
        _fileSystem = fileSystem;
        _paths = paths;
        _time = time ?? TimeProvider.System;
    }

    /// <summary>True when the file existed but was corrupt and had to be quarantined (caller may show a notification).</summary>
    public bool Load(out FrecencyStore store)
    {
        if (!_fileSystem.FileExists(_paths.UsageFile))
        {
            store = new FrecencyStore(_time);
            return false;
        }

        string text;
        try
        {
            text = _fileSystem.ReadAllText(_paths.UsageFile);
        }
        catch (IOException)
        {
            store = new FrecencyStore(_time);
            return false;
        }

        JsonNode? node;
        try
        {
            node = JsonNode.Parse(text);
        }
        catch (JsonException)
        {
            Quarantine();
            store = new FrecencyStore(_time);
            return true;
        }

        if (node is not JsonObject root)
        {
            Quarantine();
            store = new FrecencyStore(_time);
            return true;
        }

        var byQuery = ReadQueryEntries(root["byQuery"] as JsonArray);
        var global = ReadGlobalEntries(root["global"] as JsonArray);
        store = FrecencyStore.FromSnapshot(new FrecencySnapshot(byQuery, global), _time);
        return false;
    }

    public void Save(FrecencyStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        var snapshot = store.Snapshot();

        var root = new JsonObject
        {
            ["byQuery"] = new JsonArray(snapshot.ByQuery.Select(e => (JsonNode)new JsonObject
            {
                ["query"] = e.Query,
                ["emoji"] = e.Emoji,
                ["value"] = e.Value,
                ["updated"] = e.Updated,
            }).ToArray()),
            ["global"] = new JsonArray(snapshot.Global.Select(e => (JsonNode)new JsonObject
            {
                ["emoji"] = e.Emoji,
                ["value"] = e.Value,
                ["updated"] = e.Updated,
            }).ToArray()),
        };

        AtomicFileWriter.Write(_fileSystem, _paths.UsageFile, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }

    private void Quarantine()
    {
        try
        {
            _fileSystem.Move(_paths.UsageFile, $"{_paths.UsageFile}.bad-{_time.GetUtcNow():yyyyMMddHHmmss}", overwrite: true);
        }
        catch (IOException)
        {
            // Best effort — if even the rename fails, an empty store is still returned to the caller.
        }
    }

    private static List<FrecencyQueryEntry> ReadQueryEntries(JsonArray? array)
    {
        var result = new List<FrecencyQueryEntry>();
        if (array is null)
        {
            return result;
        }

        foreach (var item in array)
        {
            if (item is JsonObject o
                && o["query"] is JsonValue q && q.TryGetValue(out string? query)
                && o["emoji"] is JsonValue em && em.TryGetValue(out string? emoji)
                && o["value"] is JsonValue val && val.TryGetValue(out double value)
                && o["updated"] is JsonValue upd && upd.TryGetValue(out DateTimeOffset updated))
            {
                result.Add(new FrecencyQueryEntry(query, emoji, value, updated));
            }
        }

        return result;
    }

    private static List<FrecencyGlobalEntry> ReadGlobalEntries(JsonArray? array)
    {
        var result = new List<FrecencyGlobalEntry>();
        if (array is null)
        {
            return result;
        }

        foreach (var item in array)
        {
            if (item is JsonObject o
                && o["emoji"] is JsonValue em && em.TryGetValue(out string? emoji)
                && o["value"] is JsonValue val && val.TryGetValue(out double value)
                && o["updated"] is JsonValue upd && upd.TryGetValue(out DateTimeOffset updated))
            {
                result.Add(new FrecencyGlobalEntry(emoji, value, updated));
            }
        }

        return result;
    }
}
