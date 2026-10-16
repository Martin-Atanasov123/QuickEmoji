namespace QuickEmoji.Core.Insertion;

/// <summary>How a commit reaches the target app (SPEC §4.2 step 4).</summary>
public enum InsertionMethod
{
    /// <summary>SendInput with KEYEVENTF_UNICODE, one call for the whole sequence (SPEC §4.2 step 5). The default.</summary>
    UnicodeInjection,

    /// <summary>Clipboard set, then Ctrl+V or Shift+Insert injected, then restore (SPEC §4.2 step 6). Only via a per-app override.</summary>
    ClipboardPaste,

    /// <summary>Clipboard set, toast shown, nothing injected (SPEC §4.6). Elevated targets, or an override that asks for it.</summary>
    Copy,
}

/// <summary>Which keys a clipboard paste sends (SPEC §4.2 step 6.3).</summary>
public enum PasteKeys
{
    CtrlV,
    ShiftInsert,
}

/// <summary>A process name mapped to a specific insertion method (SPEC §6.2 "per-app overrides table").</summary>
public sealed record AppOverride(string ProcessName, InsertionMethod Method, PasteKeys PasteKeys = PasteKeys.CtrlV);
