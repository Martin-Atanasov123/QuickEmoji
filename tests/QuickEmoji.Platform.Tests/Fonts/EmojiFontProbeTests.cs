using QuickEmoji.Core.Emoji;
using QuickEmoji.Platform.Fonts;

namespace QuickEmoji.Platform.Tests.Fonts;

/// <summary>
/// Calls DirectWrite against the installed Segoe UI Emoji. Assertions hold on every Windows build that ships the
/// font, so these run in CI; they skip only if the font is absent.
/// </summary>
[Trait("Category", "Win32")]
public sealed class EmojiFontProbeTests
{
    [Theory]
    [InlineData("❤️")]
    [InlineData("😂")]
    [InlineData("👍🏽")]
    [InlineData("👨‍👩‍👧‍👦")]
    [InlineData("1️⃣")]
    public void Long_established_emoji_render(string emoji)
    {
        using var probe = CreateOrSkip();

        Assert.True(probe.CanRender(emoji));
    }

    [Theory]
    [InlineData("\U0001FAFF")] // unassigned code point
    [InlineData("\U000F0000")] // private use, plane 15
    public void Code_points_outside_the_font_do_not_render(string text)
    {
        using var probe = CreateOrSkip();

        Assert.False(probe.CanRender(text));
    }

    [Fact]
    public void Measuring_the_catalog_yields_a_cap_and_keeps_core_emoji()
    {
        using var probe = CreateOrSkip();
        var catalog = EmojiCatalogLoader.LoadEmbedded();

        var coverage = probe.Measure(catalog.AllSequences());
        var filtered = catalog.Apply(coverage.ToFilter());

        Assert.NotNull(coverage.MaxEmojiVersion);
        Assert.True(coverage.MaxEmojiVersion >= new EmojiVersion(11, 0), $"cap {coverage.MaxEmojiVersion}");
        Assert.True(filtered.TryGet("❤️", out _));
        Assert.True(filtered.TryGet("👍", out _));
        Assert.All(filtered.AllSequences(), s => Assert.True(probe.CanRender(s.Emoji), $"{s.Emoji} survived the filter"));
    }

    [Fact]
    public void Disposed_probe_refuses_work()
    {
        var probe = CreateOrSkip();
        probe.Dispose();

        Assert.Throws<ObjectDisposedException>(() => probe.CanRender("❤️"));
    }

    private static EmojiFontProbe CreateOrSkip()
    {
        var probe = EmojiFontProbe.TryCreate();
        Assert.SkipWhen(probe is null, $"{EmojiFontProbe.FontFamilyName} is not installed.");
        return probe!;
    }
}
