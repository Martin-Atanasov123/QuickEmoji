namespace QuickEmoji.Core.Emoji;

/// <summary>
/// A hand-maintained synonym. <see cref="Rank"/> is this emoji's position in the word's list in synonyms.json
/// (1 = the preferred answer for that word).
/// </summary>
public sealed record EmojiSynonym(string Word, int Rank);
