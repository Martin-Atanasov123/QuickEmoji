# Testing

## Environment of record

Recorded 2026-10-14 from `dotnet --info`:

| Item | Value |
|---|---|
| OS | Windows 11 Pro, 10.0.26300, x64 (`win-x64`) |
| .NET SDK | 10.0.204 (MSBuild 18.3.3) |
| .NET runtime | Microsoft.NETCore.App 10.0.8, Microsoft.WindowsDesktop.App 10.0.8 |
| Workloads | none |
| Visual Studio | not installed; everything builds with the .NET CLI |

`global.json` pins SDK 10.0.204 with `rollForward: latestFeature`.

## Automated tests

Tests use xUnit v3 on Microsoft Testing Platform (see DECISIONS.md).

| Command | What it runs |
|---|---|
| `dotnet build QuickEmoji.sln -c Debug` | Builds everything; warnings are errors. |
| `dotnet test --filter "Category!=E2E"` | Core and Platform unit tests. This is what CI runs. |
| `dotnet test --project tests/QuickEmoji.E2E` | End-to-end tests on a real desktop. Build first; never run in CI. |

### Projects
- `tests/QuickEmoji.Core.Tests`: pure logic, including acceptance tests against the committed `emoji.json`.
- `tests/QuickEmoji.Platform.Tests`: the pure parts of the Win32 layer, behind interfaces. `Category=Win32` tests call real Win32/WinRT APIs (DirectWrite, the message-only window, RegisterHotKey, monitor queries) — they need no visible desktop and run in CI, but they are not hermetic unit tests in the usual sense (see the Stage 3 table below).
- `tests/QuickEmoji.DataBuilder.Tests`: DataBuilder parsers, skin-tone grouping, merging and the JSON writer, with small fixtures.
- `tests/QuickEmoji.E2E`: drives the built `QuickEmoji.exe` (and, from Stage 4, Notepad through FlaUI). Every test is `[Trait("Category","E2E")]`.

### Test data
- `tests/data/unicode/emoji-test-18.0.txt`: a byte-identical copy of the pinned Unicode file. Core tests parse it independently of DataBuilder and use it as the oracle for "every fully-qualified emoji loads".

### What the emoji data tests prove (Stage 1)
| Test | Proves |
|---|---|
| `EmojiCatalogDataTests.Every_fully_qualified_emoji_of_the_pinned_version_loads_exactly_once` | All 3,963 fully-qualified Emoji 18.0 sequences are in the catalog, as a base or a variant, with no duplicates. |
| `...Skin_tone_variants_group_under_their_base` | 👍🏽 → 👍, ☝🏿 → ☝️, 👩🏻‍❤️‍👨🏼 → 👩‍❤️‍👨, 🧑🏻‍🤝‍🧑🏿 → 🧑‍🤝‍🧑, 🧑🏻‍❤️‍💋‍🧑🏼 → 💏, 🫱🏻‍🫲🏿 → 🤝, 👨🏽‍🦰 → 👨‍🦰. |
| `...Sequences_round_trip_byte_for_byte`, `...Every_zwj_and_tag_sequence_round_trips_byte_for_byte` | ZWJ, VS16, skin-tone, keycap and tag sequences keep identical UTF-8 bytes. |
| `...Index_loads_within_150_ms` | Warm load median ≤ 150 ms. Cold first load in a fresh process measured 50–56 ms on the reference machine. |
| `EmojiCatalogFilterTests` | `MaxEmojiVersion` and per-sequence exclusion, including variants newer than their base. |
| `EmojiFontProbeTests` (Win32) | DirectWrite reports ❤️, 👨‍👩‍👧‍👦 and 1️⃣ as renderable and unassigned code points as not; the filtered catalog contains nothing the font cannot draw. |

### What the search engine tests prove (Stage 2)
`tests/data/ranking-golden.tsv` holds 184 cases across the six SPEC §3.6 categories (brief, Bulgarian, slang, typos, multi-word, ambiguous), plus a `common` category of everyday single-word queries. `GoldenRankingTests` asserts **100% top-1** and **≥95% top-3-set** pass rates — both currently pass at 100%. On failure, each test prints every failing case with the query, the expectation, and what the engine actually returned (emoji, score, match kind, matched term), so a regression is diagnosable from the test output alone.

| Test | Proves |
|---|---|
| `GoldenRankingTests` | 100% top-1, 100% top-3-set (≥95% required) across 184 golden cases. |
| `QueryNormalizerTests` | Trim, whitespace collapse, lowercase, Latin diacritic stripping (café→cafe), Cyrillic left as typed, apostrophes dropped not split. |
| `DamerauLevenshteinTests` | Edit-distance bound, including transpositions, at both the 1-edit and 2-edit thresholds. |
| `LruCacheTests` | Capacity eviction, recency promotion on get, no growth past capacity. |
| `FrecencyStoreTests` | Per-query and global counts, exponential decay at exactly one and two half-lives, `MostUsed` ordering. |
| `EmojiSearchEngineTests` | Deduplication by base emoji, the minimum score, multi-word AND (not OR), empty-query defaults and frecency-driven recents, skin-tone variant picks crediting the base emoji, per-language filtering, cache correctness after a `RecordUse`. |
| `SearchLatencyBenchmarkTests` | p95 per-query latency ≤ 5 ms, both warm (cached) and cold (never-seen) queries. Measured on the reference machine: warm p95 ≈ 0.10 ms, cold p95 ≈ 0.004 ms — two orders of magnitude under budget. |

### What the hotkey/target/placement/clipboard tests prove (Stage 3)
| Test | Proves |
|---|---|
| `HotkeyValidatorTests`, `HotkeyGestureTests` | SPEC §5: a gesture needs Ctrl, Alt or Win; Ctrl+Alt without Win warns about AltGr; display formatting. |
| `HotkeyServiceTests` (Win32) | Real `RegisterHotKey`/`UnregisterHotKey`: registration, conflict detection between two services, `TestRegister` probing without taking over the combination, `Paused` releasing and re-acquiring it, replacing a registration, `Dispose` releasing it, and `WM_HOTKEY` actually reaching `HotkeyPressed`. |
| `IntegrityLevelReaderTests` (Win32) | Reads the current process's own integrity level without failing, agrees with `WindowsPrincipal.IsInRole(Administrator)` on whether it's elevated (not a fixed expected level — CI runners are commonly elevated where a dev desktop is not), is consistent across two reads, and returns null rather than throwing for an invalid or nonexistent PID. |
| `TargetCaptureTests` (Win32) | A real capture is internally consistent; `IsElevatedOrUnknown` treats a null level the same as High/System; `IsValid` reflects whether a foreground window was found. |
| `PopupPlacementTests` | Near-caret placement, flipping above when there's no room below, falling back to the cursor when there's no caret, centering, clamping to all four work-area edges, and DPI scaling at 100/150/200% — all pure, no Win32. |
| `MonitorQueryTests` (Win32) | Real monitor/DPI/cursor queries return sane values, including for an off-screen point. |
| `ClipboardGuardTests` | `Copy` against a fake clipboard: the text, the history/roaming exclusion flags, and the Unicode (ZWJ) sequence passed through. The real clipboard needs the running WinUI app's own thread (see docs/DECISIONS.md), so it is not exercised here. |

### What the insertion-engine tests prove (Stage 4)
| Test | Proves |
|---|---|
| `InputBuilderTests` | SPEC §4.2 step 5's `INPUT[]` arrays for Unicode text and the 5 named paste-shortcut sequences: correct key-down/up pairing, `KEYEVENTF_UNICODE`, and modifier ordering. |
| `KeyReleaseWaiterTests` | Returns immediately once no modifier/Enter key is held (the fast path legitimately skips a cancellation check already satisfied); polls until release otherwise. |
| `ForegroundActivatorTests` | `SetForegroundWindow` + bounded poll: succeeds once the target becomes foreground, times out (returns false, never throws) if it never does, treats a null/zero handle as failure. |
| `MethodSelectorTests` | SPEC §4.2 step 4's pure method choice: elevated/unknown target → Copy; a per-app override wins when present; otherwise Unicode injection. |
| `ClipboardGuardTests` | `Copy` (from Stage 3) plus Stage 4's `PrepareForPasteAsync`/`RestoreIfUnchangedAsync`: snapshot captured before overwriting, restore only when the sequence number is unchanged (i.e. nothing else touched the clipboard meanwhile), skip-restore for an unrestorable (too-large or empty) snapshot. |

### Current E2E tests
| Test | Checks |
|---|---|
| `AppLaunchTests.App_starts_and_exits_cleanly_in_startup_check_mode` | `QuickEmoji.exe --startup-check` exits with code 0 within 30 s, with the full composition (catalog, hotkey, popup, toast, tray) wired up and torn down. |
| `NotepadTests.Hotkey_then_Enter_inserts_the_emoji_after_the_typed_text` | Win+Shift+E opens the popup with real keyboard focus (see DECISIONS.md — this caught a real focus bug), typing a query and pressing Enter inserts the emoji via Unicode injection into Notepad, after the previously-typed text. |
| `NotepadTests.Esc_closes_the_popup_and_changes_nothing` | Esc closes the popup without inserting anything; Notepad's text is unchanged. |
| `NotepadTests.Inserting_an_emoji_does_not_change_the_clipboard` | SPEC §4.2 step 5: Unicode injection never touches the clipboard. A marker written to the clipboard before the hotkey is still there, byte-for-byte, after the emoji is inserted. |

These three are real, live FlaUI tests against a built `QuickEmoji.exe` and a real Notepad window — not stubs. They take over keyboard focus for several seconds each and must never run in CI (`[Trait("Category","E2E")]` already excludes them from `dotnet test --filter "Category!=E2E"`). See DECISIONS.md's Stage 4 section for the several real bugs they caught (a missing window-activation call in production code) and the environment quirks that had to be worked around to make them reliable (Windows 11 Notepad's stub-process launch and tab-reuse behavior, throttled synthetic typing, raw-Win32 clipboard writes, and a stuck-key cleanup lesson).

## CI

`.github/workflows/ci.yml` runs on `windows-latest` for pushes to `main` and for pull requests: build, then the non-E2E tests.

## Manual checks

### Stage 0 (superseded by Stage 3)
Stage 0's temporary status window no longer exists; its checks are folded into Stage 3's matrix below.

### Stage 1
1. `dotnet run --project tools/QuickEmoji.DataBuilder -- --offline` (or without `--offline` on a fresh clone) prints "Wrote 1923 base emoji with 2040 skin-tone variants".
2. `git status` shows no change to `src/QuickEmoji.Core/Data/emoji.json`: the build is deterministic.
3. Open `emoji.json` in an editor. Emoji appear as real characters (no `\u` escapes), one emoji per line.

### Stage 2
1. `dotnet test --project tests/QuickEmoji.Core.Tests` passes, including `GoldenRankingTests` and `SearchLatencyBenchmarkTests`.
2. Open `tests/data/ranking-golden.tsv` — it is a plain tab-separated file, readable without special tooling.

### Stage 3 (human gate)
Everything below needs a real desktop — the hotkey, focus changes, and other apps — so it cannot run in CI. `dotnet run --project src/QuickEmoji.App` first.

**Startup and tray**
1. Launch the app. No window appears; a tray icon appears (bottom-right, "show hidden icons" if collapsed). Hover it: the tooltip reads "QuickEmoji — Win+Shift+E".
2. Right-click the tray icon: a menu with Open, Settings, Pause hotkey, Quit.
3. Left-click the tray icon: the popup opens, centered on the screen.
4. Press Esc. The popup closes with no visible animation.
5. Click "Settings" or "Open" in the tray menu: the popup opens centered (Settings is a stand-in until Stage 5 — see docs/DECISIONS.md).
6. Switch Windows between light and dark taskbar mode (Settings → Personalization → Colors). The tray glyph switches between dark-on-light and light-on-dark within a few seconds, without restarting the app.

**Hotkey**
7. Click into Notepad (or any text field) and type a sentence. Press **Win+Shift+E**. The popup appears near the text caret, not centered, and the search field has focus already.
8. Press **Win+Shift+E** again while the popup is open. It closes (toggle).
9. Click somewhere outside the popup (e.g. the Notepad window). The popup closes, as if Esc were pressed.

**Search and keymap**
10. With the popup open and empty, it shows ❤️ 😂 👍 (first run) or your 3 most-used emoji after some use.
11. Type `love`. Results update on every keystroke; row 1 shows a "Best match" chip and the accent-colored ↵ glyph.
12. Type `lol`. The row with the match explains itself with a small "matched: lol" line under the name.
13. Type something with no matches, e.g. `zzzxxxqqq`. The popup shows "No match. Try another word." and Enter does nothing.
14. Press **Down** then **Up** a few times: selection moves and wraps at both ends; the ↵ glyph and accent background follow the selection, not row 1.
15. Press **Tab** / **Shift+Tab**: same as Down/Up.
16. Type `heart` (multiple results). Press **Alt+2**: the second row is copied regardless of which row is selected.
17. Press **Ctrl+Enter** on any row: copies that row and closes, without changing what Enter would otherwise have inserted later (Stage 4 behavior — for now both just copy).
18. Press **Enter**: the emoji is copied to the clipboard, the popup closes, and a small toast appears near the popup's last position reading "Copied X — press Ctrl+V", then disappears on its own after about 2.5 s without taking focus (your typing cursor stays where it was).
19. Paste (Ctrl+V) into the text field: the emoji you picked appears.
20. Click a result row directly with the mouse: same effect as Enter on that row.

**Visuals**
21. The popup is a plain 400px-wide rounded rectangle with no title bar or border, always on top, and does **not** appear in Alt+Tab or the taskbar.
22. On Windows 11, the popup's corners are visibly rounded. (Flat corners are correct on Windows 10.)
23. The background looks translucent/blurred (Acrylic) over whatever is behind it, unless you have transparency effects or battery saver on, in which case it is a plain solid color.
24. Opening the popup shows a brief (~120 ms) fade/slide-up; closing has no animation.
25. Turn off "Show animations in Windows" (Settings → Accessibility → Visual effects) and reopen the popup: it should appear instantly with no fade.
26. Switch the system theme between Light and Dark (Settings → Personalization → Colors): the popup's colors and the system accent follow, next time it's opened.
27. With a screen reader (Narrator, Win+Ctrl+Enter) running, open the popup and arrow through results: each row is announced, e.g. "Red heart, best match, 1 of 3".

**Multi-monitor and DPI**
28. On a two-monitor setup, trigger the hotkey while focused on each monitor in turn: the popup appears on the correct monitor, clamped to its work area.
29. Set a monitor to 150% or 200% scaling and repeat: the popup is still legible and correctly sized/positioned, not clipped or overlapping the screen edge oddly.

**Single instance**
30. With the app already running, launch `QuickEmoji.exe` again (e.g. double-click the built exe). It should exit almost immediately; the already-running instance's popup opens (centered) as a sign it received the signal.
31. Check `%LOCALAPPDATA%\QuickEmoji\logs\quickemoji*.log` (the newest-numbered file): both the running instance's own startup line and the second launch's "signalling it and exiting" line are present.

**Shutdown**
32. Right-click the tray icon → Quit. The tray icon disappears and the process exits (Task Manager shows no `QuickEmoji.exe`).

### Stage 4 (human gate) — SPEC §11's full manual matrix
Everything below needs a real desktop, several different target apps, and (for the elevated case) an admin prompt — none of it runs in CI. Build and run `dotnet run --project src/QuickEmoji.App` first; the E2E tests above already automate the Notepad case continuously, so this matrix focuses on the apps and conditions they cannot reach.

**Insertion across apps** — for each of Notepad, Microsoft Word, a Chromium browser's address bar and a text area (e.g. Edge or Chrome), a messaging app (Discord, Telegram, or WhatsApp desktop), VS Code (both the editor and the integrated terminal), and Windows Terminal:
1. Click into the app, type a short sentence, press **Win+Shift+E**, search for `heart`, press **Enter**. The emoji is inserted right after your typed text, with nothing else changed.
2. Repeat searching for something that yields a skin-tone variant (e.g. `thumbs up`) and a ZWJ family emoji (e.g. `family`) if the app's font supports them — confirm the inserted sequence is the exact one shown in the popup, not split or re-normalized.
3. Repeat for a flag emoji (e.g. `germany`) — confirm it inserts as the regional-indicator pair (SPEC's documented Segoe UI Emoji limitation: it will render as two letters, not artwork, except in apps with their own emoji font).

**Elevated target**
4. Run Notepad **as administrator** (right-click → Run as administrator) while QuickEmoji itself is not elevated. Type into it, press the hotkey, pick an emoji, press Enter. SPEC §4.6: insertion must fall back to copy, with a toast reading "Copied X — press Ctrl+V" that states the cause. Confirm Ctrl+V into the elevated Notepad then produces the emoji.

**Esc / cancel**
5. Repeat the basic insertion case in two different apps, but press **Esc** instead of Enter. Confirm no text changed in either app.

**Keymap**
6. In an app with multiple plausible matches (e.g. search `heart`), use **arrow keys** and **Alt+1/2/3** to insert a row other than the first. Confirm the correct emoji lands.

**Clipboard preserved**
7. Copy some distinctive text to the clipboard first (e.g. select a sentence in Notepad and Ctrl+C). Trigger the hotkey, insert an emoji via Enter (not Ctrl+Enter). Paste (Ctrl+V) afterward — the original copied text comes back, not the emoji: insertion must restore whatever was on the clipboard before it ran, in every app from step 1, including one where the clipboard held something non-text (e.g. copy an image in Paint first, then repeat the insertion + paste check).

**Focus-change abort**
8. Trigger the hotkey, then — while the popup is open — click into an entirely different window (not Esc, not the popup itself) before pressing Enter. SPEC §4.6: the popup should close (as documented in Stage 3) and nothing should be inserted into either window.

**Per-app override** (if implemented by Stage 4 — otherwise mark as not yet applicable and note it in the Stage report)
9. If a per-app override exists for a specific process (SPEC §4.2 step 4), confirm that app always uses its configured method (e.g. always Copy) regardless of elevation.

**Hotkey conflict**
10. Start another application that holds Win+Shift+E first (or simulate by registering it with a throwaway script), then launch QuickEmoji. Confirm the warning is logged (SPEC §5 — Settings-based UI arrives in Stage 5, see docs/DECISIONS.md) and the app does not crash.

**Timing budget**
11. With a stopwatch or screen recording, confirm the popup appears within roughly 50 ms of pressing the hotkey (CLAUDE.md's gotcha about not recreating the popup), and that insertion completes without a visible stall after pressing Enter.

Launch-at-startup, Explorer restart, two-monitor and DPI cases are already covered by Stage 3's matrix above and are not re-tested here; nothing about insertion changes them.
