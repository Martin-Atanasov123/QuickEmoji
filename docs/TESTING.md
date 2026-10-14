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
- `tests/QuickEmoji.Platform.Tests`: the pure parts of the Win32 layer, behind interfaces. `Category=Win32` tests call DirectWrite for real (they need no desktop and run in CI).
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

### Current E2E tests
| Test | Checks |
|---|---|
| `AppLaunchTests.App_starts_and_exits_cleanly_in_startup_check_mode` | `QuickEmoji.exe --startup-check` exits with code 0 within 30 s. |

## CI

`.github/workflows/ci.yml` runs on `windows-latest` for pushes to `main` and for pull requests: build, then the non-E2E tests.

## Manual checks

### Stage 0
1. `dotnet run --project src/QuickEmoji.App` opens a small "QuickEmoji is running" window.
2. Close it. The process exits (Task Manager shows no `QuickEmoji.exe`).
3. `%LOCALAPPDATA%\QuickEmoji\logs\quickemoji.log` contains "starting" and "exited normally" lines for that run.

### Stage 1
1. `dotnet run --project tools/QuickEmoji.DataBuilder -- --offline` (or without `--offline` on a fresh clone) prints "Wrote 1923 base emoji with 2040 skin-tone variants".
2. `git status` shows no change to `src/QuickEmoji.Core/Data/emoji.json`: the build is deterministic.
3. Open `emoji.json` in an editor. Emoji appear as real characters (no `\u` escapes), one emoji per line.

### Stage 2
1. `dotnet test --project tests/QuickEmoji.Core.Tests` passes, including `GoldenRankingTests` and `SearchLatencyBenchmarkTests`.
2. Open `tests/data/ranking-golden.tsv` — it is a plain tab-separated file, readable without special tooling.

The full manual matrix from SPEC §11 is added in Stage 3 and Stage 4.
