# QuickEmoji

A Windows tray utility: press a global hotkey, type a word, press Enter, and the best emoji for that word appears where you were typing.

> **Status:** Stage 3 (hotkey, popup, tray) — awaiting manual verification on a real desktop. Press **Win+Shift+E** anywhere, type a word, and the popup shows the top 3 matches. Enter currently **copies** the pick to the clipboard and shows a toast (real insertion arrives in Stage 4). See [docs/PLAN.md](docs/PLAN.md) and [docs/TESTING.md](docs/TESTING.md#stage-3-human-gate) for the full manual test matrix.

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
| **Enter** | Copy the selected (or best) match to the clipboard, close |
| **Alt+1 / Alt+2 / Alt+3** | Copy that row directly |
| **Ctrl+Enter** | Copy the selected row without changing the selection |
| **↑ / ↓**, Tab / Shift+Tab | Move the selection (wraps) |
| **Esc** | Close, no side effects |

A tray icon (right-click for Open / Settings / Pause hotkey / Quit) is always available; left-click opens the popup centered.

## Emoji data

`src/QuickEmoji.Core/Data/emoji.json` is generated and committed, so normal builds need no network access. To regenerate it after editing `synonyms.json` or `popularity.json`, or after changing a pinned source:

```powershell
dotnet run --project tools/QuickEmoji.DataBuilder
```

Sources, versions and licences are listed in [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).

Emoji that the installed Segoe UI Emoji font cannot draw are hidden automatically. On Windows 11 build 26300 that means Emoji 18.0 and the England, Scotland and Wales flags. Windows draws country flags as two letters (for example "DE"); they stay searchable, and apps with their own emoji fonts show the real flag.

## Documentation
- [SPEC.md](SPEC.md): product and technical specification
- [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md): layers, dependencies, data pipeline
- [docs/TESTING.md](docs/TESTING.md): how to test, CI, manual checks
- [docs/DECISIONS.md](docs/DECISIONS.md): deviations from the spec and why
