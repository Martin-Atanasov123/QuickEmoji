using QuickEmoji.Core.Insertion;
using QuickEmoji.Platform.Clipboard;
using QuickEmoji.Platform.Injection;
using QuickEmoji.Platform.Targeting;
using Serilog;

namespace QuickEmoji.App.Insertion;

/// <summary>
/// The insertion engine's orchestration (SPEC §4.2): restore foreground, wait for key release, pick a method
/// (Core's pure <see cref="MethodSelector"/>), then inject, paste, or fall back to copy. Thin — it calls Core
/// and Platform (CLAUDE.md), with no Win32 or WinRT calls of its own.
/// </summary>
public sealed class InsertionCoordinator(ClipboardGuard clipboardGuard, Action<string> showToast, Action<string>? recordOutcome = null)
{
    private static readonly TimeSpan ForegroundTimeout = TimeSpan.FromMilliseconds(250);
    private static readonly TimeSpan KeyReleaseTimeout = TimeSpan.FromMilliseconds(300);

    /// <summary>Settings → Insertion → "clipboard restore delay" (SPEC §6.2); Stage 5's default until the user changes it.</summary>
    public TimeSpan ClipboardRestoreDelay { get; set; } = TimeSpan.FromMilliseconds(150);

    /// <summary>Settings → Insertion → per-app overrides table (SPEC §6.2); empty until the user adds one.</summary>
    public IReadOnlyList<AppOverride> Overrides { get; set; } = [];

    /// <summary>Settings → Insertion → "default method (Auto or Clipboard)" (SPEC §6.2).</summary>
    public InsertionMethod DefaultMethod { get; set; } = InsertionMethod.UnicodeInjection;

    /// <summary>
    /// Commits <paramref name="emoji"/> into <paramref name="target"/> (SPEC §4.2). Never throws for an invalid
    /// or gone target — falls back to <see cref="InsertionMethod.Copy"/> instead (SPEC §4.6).
    /// </summary>
    public async Task InsertAsync(TargetContext target, string emoji)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentException.ThrowIfNullOrEmpty(emoji);

        if (!target.IsValid)
        {
            CopyFallback(emoji, "no target window was captured");
            return;
        }

        var activated = await ForegroundActivator.ActivateAsync(target.ForegroundWindowHandle, ForegroundTimeout);
        if (!activated)
        {
            // SPEC §4.2 step 2: "If the target window no longer exists or something else has taken the
            // foreground, do not inject."
            CopyFallback(emoji, "could not restore focus to the target app");
            return;
        }

        await KeyReleaseWaiter.WaitForReleaseAsync(KeyReleaseTimeout);

        var selection = MethodSelector.Select(target.ProcessName, target.IsElevatedOrUnknown, Overrides, DefaultMethod);
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        switch (selection.Method)
        {
            case InsertionMethod.UnicodeInjection:
                var accepted = TextInjector.SendUnicodeText(emoji);
                Log.Information(
                    "Inserted via {Method} into {Process} ({Reason}); {Accepted} of {Expected} events accepted in {ElapsedMs} ms",
                    selection.Method, target.ProcessName, selection.Reason, accepted, emoji.Length * 2, stopwatch.Elapsed.TotalMilliseconds);
                recordOutcome?.Invoke($"{selection.Method} into {target.ProcessName ?? "(unknown)"}: ok");
                break;

            case InsertionMethod.ClipboardPaste:
                await PasteAsync(emoji, selection, target.ProcessName, stopwatch);
                break;

            case InsertionMethod.Copy:
            default:
                Log.Information("Copy fallback for {Process} ({Reason})", target.ProcessName, selection.Reason);
                CopyFallback(emoji, DescribeCause(selection.Reason, target));
                recordOutcome?.Invoke($"Copy fallback for {target.ProcessName ?? "(unknown)"}: {selection.Reason}");
                break;
        }
    }

    private async Task PasteAsync(string emoji, MethodSelection selection, string? processName, System.Diagnostics.Stopwatch stopwatch)
    {
        var prepared = await clipboardGuard.PrepareForPasteAsync(emoji);
        if (prepared.TooLarge)
        {
            // SPEC §4.3: "Snapshots over 32 MB: skip the clipboard method and use Copy fallback."
            Log.Information("Clipboard paste skipped for {Process}: existing clipboard content exceeds 32 MB", processName);
            CopyFallback(emoji, "the current clipboard contents were too large to preserve");
            return;
        }

        var shortcut = selection.PasteKeys == PasteKeys.ShiftInsert ? PasteShortcut.ShiftInsert : PasteShortcut.CtrlV;
        TextInjector.SendPasteShortcut(shortcut);
        await Task.Delay(ClipboardRestoreDelay);
        var restored = await clipboardGuard.RestoreIfUnchangedAsync(prepared.Snapshot!, prepared.SequenceNumberAfterSet);

        Log.Information(
            "Inserted via {Method} ({Shortcut}) into {Process} ({Reason}); clipboard {RestoreOutcome} in {ElapsedMs} ms",
            selection.Method, shortcut, processName, selection.Reason, restored ? "restored" : "left as-is (changed meanwhile)",
            stopwatch.Elapsed.TotalMilliseconds);
        recordOutcome?.Invoke($"{selection.Method} ({shortcut}) into {processName ?? "(unknown)"}: {(restored ? "ok" : "clipboard changed meanwhile")}");
    }

    private void CopyFallback(string emoji, string? cause)
    {
        clipboardGuard.Copy(emoji);
        var message = cause is null ? $"Copied {emoji} — press Ctrl+V" : $"Copied {emoji} — press Ctrl+V ({cause})";
        showToast(message);
    }

    private static string? DescribeCause(string reason, TargetContext target) =>
        target.IsElevatedOrUnknown ? "the target app runs as administrator" : reason;
}
