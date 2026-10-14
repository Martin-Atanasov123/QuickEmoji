namespace QuickEmoji.DataBuilder.Sources;

/// <summary>A pinned upstream file, verified by SHA-256 after download.</summary>
internal sealed record SourceFile(string FileName, Uri Url, string Sha256);

/// <summary>
/// Every upstream input, pinned by version and hash. Changing a version means updating the URL and hash here,
/// regenerating emoji.json, and updating THIRD_PARTY_NOTICES.md.
/// </summary>
internal static class SourceManifest
{
    public const string EmojiVersion = "18.0";
    public const string CldrVersion = "48.2.3";
    public const string GemojiVersion = "4.1.0";

    private const string CldrBase = "https://raw.githubusercontent.com/unicode-org/cldr-json/" + CldrVersion + "/cldr-json/";

    public static readonly SourceFile EmojiTest = new(
        "emoji-test.txt",
        new Uri("https://www.unicode.org/Public/18.0.0/emoji/emoji-test.txt"),
        "8f3735cda1f92a779d78af67cf86066bb1f07143dc22f2ac29394d9bc57ab21a");

    public static readonly SourceFile CldrEnglish = new(
        "cldr-annotations-en.json",
        new Uri(CldrBase + "cldr-annotations-full/annotations/en/annotations.json"),
        "f22083cb86dffb63a643d5bacb5d9899f82d2fa5d388ad4f3aed72184acef505");

    public static readonly SourceFile CldrEnglishDerived = new(
        "cldr-annotations-derived-en.json",
        new Uri(CldrBase + "cldr-annotations-derived-full/annotationsDerived/en/annotations.json"),
        "15c9457afa703a32dcc78d54626418a91e820fe0ddd67b7d491c4c4d9f5846ab");

    public static readonly SourceFile CldrBulgarian = new(
        "cldr-annotations-bg.json",
        new Uri(CldrBase + "cldr-annotations-full/annotations/bg/annotations.json"),
        "c268a922ad142d71e7ebfd217f99737eee22cd2ee7d736c79acc122b38deeb42");

    public static readonly SourceFile CldrBulgarianDerived = new(
        "cldr-annotations-derived-bg.json",
        new Uri(CldrBase + "cldr-annotations-derived-full/annotationsDerived/bg/annotations.json"),
        "38fd6fd136cb245f8ee27e18d5459e212edaaf2c1b55e19736d4ec06ba60748d");

    public static readonly SourceFile Gemoji = new(
        "gemoji-emoji.json",
        new Uri("https://raw.githubusercontent.com/github/gemoji/v" + GemojiVersion + "/db/emoji.json"),
        "b174ae2aeb321b52f64adb9ff412f966a7f338839d780784dd15dcad702c2dd6");

    public static IReadOnlyList<SourceFile> All { get; } =
        [EmojiTest, CldrEnglish, CldrEnglishDerived, CldrBulgarian, CldrBulgarianDerived, Gemoji];
}
