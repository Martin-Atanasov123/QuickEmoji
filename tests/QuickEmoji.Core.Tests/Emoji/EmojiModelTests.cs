using System.Text;
using QuickEmoji.Core.Emoji;

namespace QuickEmoji.Core.Tests.Emoji;

public sealed class EmojiModelTests
{
    [Theory]
    [InlineData("0.6", 0, 6)]
    [InlineData("15.1", 15, 1)]
    [InlineData("18.0", 18, 0)]
    public void EmojiVersion_parses_major_and_minor(string text, int major, int minor)
    {
        Assert.Equal(new EmojiVersion(major, minor), EmojiVersion.Parse(text));
        Assert.Equal(text, EmojiVersion.Parse(text).ToString());
    }

    [Theory]
    [InlineData("")]
    [InlineData("15")]
    [InlineData(".1")]
    [InlineData("15.")]
    [InlineData("E15.0")]
    [InlineData("15.-1")]
    public void EmojiVersion_rejects_malformed_text(string text)
    {
        Assert.False(EmojiVersion.TryParse(text, out _));
        Assert.Throws<FormatException>(() => EmojiVersion.Parse(text));
    }

    [Fact]
    public void EmojiVersion_orders_numerically_not_lexically()
    {
        Assert.True(new EmojiVersion(2, 0) < new EmojiVersion(11, 0));
        Assert.True(new EmojiVersion(15, 1) > new EmojiVersion(15, 0));
        Assert.True(new EmojiVersion(0, 6) <= new EmojiVersion(0, 6));
    }

    [Fact]
    public void SkinTones_maps_each_fitzpatrick_modifier()
    {
        Assert.Equal(SkinTone.Light, SkinTones.FromModifier(0x1F3FB));
        Assert.Equal(SkinTone.Dark, SkinTones.FromModifier(0x1F3FF));
        Assert.False(SkinTones.IsModifier(0x1F3FA));
        Assert.Throws<ArgumentOutOfRangeException>(() => SkinTones.FromModifier(0x1F44D));
    }

    [Fact]
    public void SkinTones_In_lists_tones_in_order()
    {
        Assert.Equal([SkinTone.Light, SkinTone.Dark], SkinTones.In("🧑🏻‍🤝‍🧑🏿"));
        Assert.Empty(SkinTones.In("❤️"));
    }

    [Fact]
    public void Variant_is_uniform_only_when_every_person_has_the_tone()
    {
        var mixed = new EmojiVariant("🧑🏻‍🤝‍🧑🏿", [SkinTone.Light, SkinTone.Dark], new EmojiVersion(12, 1));
        var same = new EmojiVariant("🧑🏻‍🤝‍🧑🏻", [SkinTone.Light, SkinTone.Light], new EmojiVersion(12, 0));

        Assert.False(mixed.IsUniform(SkinTone.Light));
        Assert.True(same.IsUniform(SkinTone.Light));
        Assert.False(same.IsUniform(SkinTone.Dark));
    }

    [Fact]
    public void Loader_rejects_an_unknown_schema_version()
    {
        using var json = new MemoryStream(Encoding.UTF8.GetBytes("""{"schemaVersion":99,"sources":{},"emoji":[]}"""));

        Assert.Throws<InvalidDataException>(() => EmojiCatalogLoader.Load(json));
    }

    [Fact]
    public void Loader_rejects_an_invalid_skin_tone()
    {
        using var json = new MemoryStream(Encoding.UTF8.GetBytes(
            """{"schemaVersion":1,"sources":{},"emoji":[{"emoji":"👍","name":"thumbs up","version":"0.6","group":"g","subgroup":"s","variants":[{"emoji":"👍🏽","tones":[7]}]}]}"""));

        Assert.Throws<InvalidDataException>(() => EmojiCatalogLoader.Load(json));
    }

    [Fact]
    public void Loader_gives_variants_the_base_version_unless_they_state_their_own()
    {
        using var json = new MemoryStream(Encoding.UTF8.GetBytes(
            """{"schemaVersion":1,"sources":{"emojiTest":"18.0","cldr":"48","gemoji":"4.1.0"},"emoji":[{"emoji":"🤝","name":"handshake","version":"3.0","group":"g","subgroup":"s","variants":[{"emoji":"🤝🏻","tones":[1],"version":"14.0"},{"emoji":"🤝🏼","tones":[2]}]}]}"""));

        var catalog = EmojiCatalogLoader.Load(json);

        var variants = catalog.Entries.Single().Variants;
        Assert.Equal(new EmojiVersion(14, 0), variants[0].Version);
        Assert.Equal(new EmojiVersion(3, 0), variants[1].Version);
        Assert.Equal("18.0", catalog.Sources.EmojiTest);
    }
}
