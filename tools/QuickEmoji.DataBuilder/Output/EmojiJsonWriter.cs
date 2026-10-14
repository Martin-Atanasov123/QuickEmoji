using System.Globalization;
using System.Text;
using QuickEmoji.Core.Emoji.Serialization;

namespace QuickEmoji.DataBuilder.Output;

/// <summary>
/// Writes emoji.json as UTF-8 (no BOM, LF) with one emoji per line, so diffs stay reviewable.
/// Only '"', '\' and control characters are escaped. Emoji, ZWJ, VS16 and tag characters are written as-is,
/// so every sequence round-trips byte for byte.
/// </summary>
internal static class EmojiJsonWriter
{
    public static void Write(EmojiDataFile data, TextWriter writer)
    {
        writer.Write("{\n");
        writer.Write($"  \"schemaVersion\": {data.SchemaVersion.ToString(CultureInfo.InvariantCulture)},\n");
        writer.Write($"  \"sources\": {{\"emojiTest\":{Quote(data.Sources.EmojiTest)},\"cldr\":{Quote(data.Sources.Cldr)},\"gemoji\":{Quote(data.Sources.Gemoji)}}},\n");
        writer.Write("  \"emoji\": [\n");
        for (var i = 0; i < data.Emoji.Count; i++)
        {
            writer.Write("    ");
            writer.Write(Record(data.Emoji[i]));
            writer.Write(i < data.Emoji.Count - 1 ? ",\n" : "\n");
        }

        writer.Write("  ]\n}\n");
    }

    public static byte[] ToUtf8(EmojiDataFile data)
    {
        using var stream = new MemoryStream();
        using (var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)) { NewLine = "\n" })
        {
            Write(data, writer);
        }

        return stream.ToArray();
    }

    private static string Record(EmojiRecord r)
    {
        var fields = new List<string>
        {
            Field("emoji", Quote(r.Emoji)),
            Field("name", Quote(r.Name)),
        };
        AddIf(fields, "nameBg", r.NameBg is null ? null : Quote(r.NameBg));
        fields.Add(Field("version", Quote(r.Version)));
        fields.Add(Field("group", Quote(r.Group)));
        fields.Add(Field("subgroup", Quote(r.Subgroup)));
        AddIf(fields, "shortcodes", Array(r.Shortcodes));
        AddIf(fields, "keywords", Array(r.Keywords));
        AddIf(fields, "keywordsBg", Array(r.KeywordsBg));
        AddIf(fields, "tags", Array(r.Tags));
        AddIf(fields, "synonyms", r.Synonyms is null
            ? null
            : "[" + string.Join(",", r.Synonyms.Select(s => "{" + Field("word", Quote(s.Word)) + "," + Field("rank", s.Rank.ToString(CultureInfo.InvariantCulture)) + "}")) + "]");
        AddIf(fields, "popularity", r.Popularity?.ToString(CultureInfo.InvariantCulture));
        AddIf(fields, "variants", r.Variants is null ? null : "[" + string.Join(",", r.Variants.Select(Variant)) + "]");
        return "{" + string.Join(",", fields) + "}";
    }

    private static string Variant(VariantRecord v)
    {
        var tones = "[" + string.Join(",", v.Tones.Select(t => t.ToString(CultureInfo.InvariantCulture))) + "]";
        var version = v.Version is null ? "" : "," + Field("version", Quote(v.Version));
        return "{" + Field("emoji", Quote(v.Emoji)) + "," + Field("tones", tones) + version + "}";
    }

    private static string? Array(List<string>? values) =>
        values is null ? null : "[" + string.Join(",", values.Select(Quote)) + "]";

    private static void AddIf(List<string> fields, string name, string? json)
    {
        if (json is not null)
        {
            fields.Add(Field(name, json));
        }
    }

    private static string Field(string name, string json) => "\"" + name + "\":" + json;

    internal static string Quote(string value)
    {
        var builder = new StringBuilder(value.Length + 2).Append('"');
        foreach (var c in value)
        {
            switch (c)
            {
                case '"': builder.Append("\\\""); break;
                case '\\': builder.Append("\\\\"); break;
                case '\n': builder.Append("\\n"); break;
                case '\r': builder.Append("\\r"); break;
                case '\t': builder.Append("\\t"); break;
                case < ' ': builder.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture)); break;
                default: builder.Append(c); break;
            }
        }

        return builder.Append('"').ToString();
    }
}
