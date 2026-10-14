# QuickEmoji

A Windows tray utility: press a global hotkey, type a word, press Enter, and the best emoji for that word appears where you were typing.

> **Status:** Stage 2 (search engine). The app starts and exits. The emoji catalog (Emoji 18.0, English and Bulgarian keywords) and the ranked search engine are built and tested (100% of 184 golden cases, p95 latency ≈ 0.1 ms). The hotkey, popup and insertion arrive in later stages. See [docs/PLAN.md](docs/PLAN.md).

## Build from source

Requires Windows 10 1809 or later (Windows 11 recommended) and the .NET 10 SDK (`global.json` pins the version). Visual Studio is not needed.

```powershell
dotnet build QuickEmoji.sln -c Debug
dotnet test --filter "Category!=E2E"
dotnet run --project src/QuickEmoji.App
```

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
