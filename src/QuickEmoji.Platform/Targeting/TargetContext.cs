using QuickEmoji.Platform.Security;

namespace QuickEmoji.Platform.Targeting;

/// <summary>
/// A caret rectangle in screen coordinates. Width/Height are 0 when the target gave no caret (SPEC §4.1).
/// </summary>
public readonly record struct CaretRect(int Left, int Top, int Right, int Bottom)
{
    public static CaretRect Empty { get; } = default;

    public bool IsEmpty => Right <= Left || Bottom <= Top;
}

/// <summary>
/// Everything captured about the focused app at hotkey time (SPEC §4.1), before the popup takes the foreground.
/// Immutable: a fresh capture is taken on every hotkey press, never mutated or reused across invocations.
/// </summary>
public sealed record TargetContext(
    nint ForegroundWindowHandle,
    int ProcessId,
    string? ProcessName,
    nint FocusedChildWindowHandle,
    CaretRect CaretRectangle,
    IntegrityLevel? IntegrityLevel,
    DateTimeOffset CapturedAt)
{
    /// <summary>
    /// True when the target is elevated or its elevation could not be determined — SPEC §4.2 treats "unknown"
    /// the same as elevated, because a failed read is itself evidence of an access boundary.
    /// </summary>
    public bool IsElevatedOrUnknown => IntegrityLevel is null or Security.IntegrityLevel.High or Security.IntegrityLevel.System;

    /// <summary>No foreground window was found at capture time.</summary>
    public bool IsValid => ForegroundWindowHandle != 0;
}
