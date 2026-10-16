# QuickEmoji

A Windows tray utility: press a global hotkey, type a word, press Enter, and the best emoji for that word appears where you were typing.

> **Status:** Stage 5 (settings, startup, first run, personalization) — the hotkey, popup, real insertion (Unicode injection or clipboard paste, with an elevated-target fallback to copy), and the Settings/first-run windows are all implemented; the full manual test matrix for Stages 3-5 is still awaiting verification on a real desktop. Press **Win+Shift+E** anywhere, type a word, and the popup shows the top 3 matches; Enter inserts the pick directly where you were typing. See [docs/PLAN.md](docs/PLAN.md) and [docs/TESTING.md](docs/TESTING.md) for the full manual test matrices.

## Build from source

Requires Windows 10 1809 or later (Windows 11 recommended) and the .NET 10 SDK (`global.json` pins the version). Visual Studio is not needed.

```powershell
dotnet build QuickEmoji.sln -c Debug
dotnet test --filter "Category!=E2E"
dotnet run --project src/QuickEmoji.App
```

## Usage

| Key | Action |
|---|---|
| **Win+Shift+E** | Open the popup near your text caret (press again to close) |
| Type | Search updates on every keystroke |
| **Enter** | Insert the selected (or best) match where you were typing, close |
| **Alt+1 / Alt+2 / Alt+3** | Insert that row directly |
| **Ctrl+Enter** | Copy the selected row to the clipboard instead of inserting it |
| **↑ / ↓**, Tab / Shift+Tab | Move the selection (wraps) |
| **Esc** | Close, no side effects |

A tray icon (right-click for Open / Settings / Pause hotkey / Quit) is always available; left-click opens the popup centered (with no insertion target, Enter copies instead — SPEC §6.3).

The first time you run QuickEmoji, a short welcome window shows your hotkey, lets you try it immediately, and asks whether to launch at Windows startup (off by default). Settings (tray menu → Settings) covers the hotkey, popup position, theme, search languages, preferred skin tone, per-app insertion overrides, and a "Copy diagnostics" button for bug reports.

## Emoji data

`src/QuickEmoji.Core/Data/emoji.json` is generated and committed, so normal builds need no network access. To regenerate it after editing `synonyms.json` or `popularity.json`, or after changing a pinned source:

```powershell
dotnet run --project tools/QuickEmoji.DataBuilder
```

Sources, versions and licences are listed in [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).

Emoji that the installed Segoe UI Emoji font cannot draw are hidden automatically. On Windows 11 build 26300 that means Emoji 18.0 and the England, Scotland and Wales flags. Windows draws country flags as two letters (for example "DE"); they stay searchable, and apps with their own emoji fonts show the real flag.

## Performance (SPEC §9)

Measured on the reference machine (Windows 11 build 26300, Debug build). Run `QuickEmoji.exe --perf` to log cold-start time and idle working set to `%LOCALAPPDATA%\QuickEmoji\logs`.

| Metric | Budget | Measured |
|---|---|---|
| Search core per query | ≤ 5 ms (p95) | warm ≈ 0.10 ms, cold ≈ 0.004 ms (`SearchLatencyBenchmarkTests`) |
| Catalog load | ≤ 150 ms | ~26-56 ms warm; CI asserts the fastest of 11 loads stays under budget |
| Enter → emoji in target app | ≤ 150 ms (Unicode path) | ~2 ms (`InsertAsync`'s own timing, e.g. "4 of 4 events accepted in 2.1 ms") |
| Cold start → tray ready | ≤ 1.5 s | ~0.69 s (`--perf`) |
| Idle CPU | 0% (no polling, no timers while hidden) | No remaining polling loop: `SingleInstanceGuard`'s watcher blocks indefinitely on `WaitHandle.WaitAny` (fixed in Stage 6 — it previously woke every 1 s to re-check a flag); `ToastWindow`'s auto-hide timer stops on `Hide()`; the popup's show animation is one-shot. |
| Idle working set | Target ≤ 100 MB | ~164 MB (`--perf`) — **exceeds the target.** This is the self-contained Windows App SDK's own baseline footprint, not a QuickEmoji-specific leak (see docs/DECISIONS.md); trimming it further (ReadyToRun tuning, trimming unused WinAppSDK components) is out of scope for this stage. |
| Hotkey → popup visible and focused | ≤ 50 ms | Not independently measured — needs a human with a stopwatch or screen recording (docs/TESTING.md's Stage 4 manual matrix, item 11). |
| Keystroke → results rendered | ≤ 16 ms (p95) | Not independently measured — bounded by the ≤5 ms search core plus rendering at most 3 result rows, but no frame-timing tool has verified the XAML re-render itself. |

## Documentation
- [SPEC.md](SPEC.md): product and technical specification
- [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md): layers, dependencies, data pipeline
- [docs/TESTING.md](docs/TESTING.md): how to test, CI, manual checks
- [docs/DECISIONS.md](docs/DECISIONS.md): deviations from the spec and why
