using System.Text.Json.Serialization;

namespace QuickEmoji.Core.Emoji.Serialization;

/// <summary>
/// The on-disk shape of <c>Data/emoji.json</c>. DataBuilder writes it; <see cref="EmojiCatalogLoader"/> reads it.
/// </summary>
internal sealed class EmojiDataFile
{
    public const int CurrentSchemaVersion = 1;

    [JsonPropertyName("schemaVersion")]
    public int SchemaVersion { get; set; }

    [JsonPropertyName("sources")]
    public EmojiDataSources Sources { get; set; } = new();

    [JsonPropertyName("emoji")]
    public List<EmojiRecord> Emoji { get; set; } = [];
}

internal sealed class EmojiDataSources
{
    [JsonPropertyName("emojiTest")]
    public string EmojiTest { get; set; } = "";

    [JsonPropertyName("cldr")]
    public string Cldr { get; set; } = "";

    [JsonPropertyName("gemoji")]
    public string Gemoji { get; set; } = "";
}

internal sealed class EmojiRecord
{
    [JsonPropertyName("emoji")]
    public string Emoji { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("nameBg")]
    public string? NameBg { get; set; }

    [JsonPropertyName("version")]
    public string Version { get; set; } = "";

    [JsonPropertyName("group")]
    public string Group { get; set; } = "";

    [JsonPropertyName("subgroup")]
    public string Subgroup { get; set; } = "";

    [JsonPropertyName("shortcodes")]
    public List<string>? Shortcodes { get; set; }

    [JsonPropertyName("keywords")]
    public List<string>? Keywords { get; set; }

    [JsonPropertyName("keywordsBg")]
    public List<string>? KeywordsBg { get; set; }

    [JsonPropertyName("synonyms")]
    public List<string>? Synonyms { get; set; }

    [JsonPropertyName("popularity")]
    public int? Popularity { get; set; }

    [JsonPropertyName("variants")]
    public List<VariantRecord>? Variants { get; set; }
}

internal sealed class VariantRecord
{
    [JsonPropertyName("emoji")]
    public string Emoji { get; set; } = "";

    [JsonPropertyName("tones")]
    public List<int> Tones { get; set; } = [];

    /// <summary>Only written when it differs from the base emoji's version (e.g. toned 🤝 is 14.0, 🤝 is 3.0).</summary>
    [JsonPropertyName("version")]
    public string? Version { get; set; }
}

[JsonSourceGenerationOptions(DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(EmojiDataFile))]
internal sealed partial class EmojiDataJsonContext : JsonSerializerContext;
