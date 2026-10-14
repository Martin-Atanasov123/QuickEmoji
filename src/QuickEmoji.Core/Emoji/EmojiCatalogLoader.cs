using System.Text.Json;
using QuickEmoji.Core.Emoji.Serialization;

namespace QuickEmoji.Core.Emoji;

/// <summary>Deserialises the pre-built <c>Data/emoji.json</c> (SPEC §3.5: no work beyond deserialising).</summary>
public static class EmojiCatalogLoader
{
    public const string ResourceName = "QuickEmoji.Core.Data.emoji.json";

    public static EmojiCatalog LoadEmbedded()
    {
        using var stream = typeof(EmojiCatalogLoader).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Embedded resource '{ResourceName}' is missing.");
        return Load(stream);
    }

    public static EmojiCatalog Load(Stream json)
    {
        ArgumentNullException.ThrowIfNull(json);
        var file = JsonSerializer.Deserialize(json, EmojiDataJsonContext.Default.EmojiDataFile)
            ?? throw new InvalidDataException("emoji.json is empty.");
        if (file.SchemaVersion != EmojiDataFile.CurrentSchemaVersion)
        {
            throw new InvalidDataException(
                $"emoji.json has schema version {file.SchemaVersion}; expected {EmojiDataFile.CurrentSchemaVersion}.");
        }

        var entries = new EmojiEntry[file.Emoji.Count];
        for (var i = 0; i < entries.Length; i++)
        {
            entries[i] = ToEntry(file.Emoji[i]);
        }

        return new EmojiCatalog(entries, new EmojiSourceVersions(file.Sources.EmojiTest, file.Sources.Cldr, file.Sources.Gemoji));
    }

    private static EmojiEntry ToEntry(EmojiRecord record)
    {
        var version = EmojiVersion.Parse(record.Version);
        return new EmojiEntry
        {
            Emoji = record.Emoji,
            Name = record.Name,
            NameBg = record.NameBg,
            Version = version,
            Group = record.Group,
            Subgroup = record.Subgroup,
            Shortcodes = record.Shortcodes ?? [],
            Keywords = record.Keywords ?? [],
            KeywordsBg = record.KeywordsBg ?? [],
            Synonyms = record.Synonyms ?? [],
            PopularityRank = record.Popularity,
            Variants = record.Variants?.Select(v => ToVariant(v, version)).ToArray() ?? [],
        };
    }

    private static EmojiVariant ToVariant(VariantRecord record, EmojiVersion baseVersion)
    {
        var tones = new SkinTone[record.Tones.Count];
        for (var i = 0; i < tones.Length; i++)
        {
            var tone = record.Tones[i];
            if (tone is < (int)SkinTone.Light or > (int)SkinTone.Dark)
            {
                throw new InvalidDataException($"Variant {record.Emoji} has an invalid skin tone value {tone}.");
            }

            tones[i] = (SkinTone)tone;
        }

        var version = record.Version is null ? baseVersion : EmojiVersion.Parse(record.Version);
        return new EmojiVariant(record.Emoji, tones, version);
    }
}
