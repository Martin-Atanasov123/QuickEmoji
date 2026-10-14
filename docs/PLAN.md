# QuickEmoji implementation plan (Stages 0–7)

Written 2026-10-14 after reading `CLAUDE.md` and `SPEC.md`. Stage 8 (semantic mode) is out of scope until requested.

## Verified toolchain (2026-10-14)

| Item | Version | Source |
|---|---|---|
| OS | Windows 11 Pro 10.0.26300, x64 | `dotnet --info` |
| .NET SDK / runtime | 10.0.204 / 10.0.8 (.NET 10 = current LTS) | `dotnet --info` |
| Microsoft.WindowsAppSDK | 2.5.1 (SemVer metapackage; WinUI component 2.3.9) | nuget.org, Learn release notes |
| H.NotifyIcon.WinUI | 2.4.1 (built against WinAppSDK 1.6; TFM net10.0-windows10.0.17763) | nuget.org |
| Microsoft.Windows.CsWin32 | 0.3.363 | nuget.org |
| CommunityToolkit.Mvvm | 8.4.2 | nuget.org |
| FlaUI.Core / FlaUI.UIA3 | 5.0.0 | nuget.org |
| Microsoft.ML.OnnxRuntime | 1.31.0 (Stage 8 only) | nuget.org |
| Serilog / Serilog.Sinks.File | 4.4.0 / 7.0.0 | nuget.org |
| xunit.v3 | 4.0.2 (Microsoft Testing Platform v2; supports VSTest-style `--filter`) | nuget.org, xunit 4.0 release notes |

Not installed locally: Visual Studio, standalone Windows SDK, Inno Setup (installed via winget at Stage 7, approved).

## Cross-cutting decisions
- TFMs: Core and DataBuilder `net10.0`; Platform, App and Windows-bound tests `net10.0-windows10.0.26100.0`, `SupportedOSPlatformVersion` 10.0.17763.0 (WinAppSDK minimum).
- Tests run on Microsoft Testing Platform (`global.json` `test.runner`). `dotnet test --filter "Category!=E2E"` stays the documented command.
- The App references the WinAppSDK 2.5.1 metapackage directly, because H.NotifyIcon.WinUI would otherwise pull in the 1.6 metapackage.
- Git: private GitHub repo, one commit and push per stage; CI on `windows-latest`.

---

## Stage 0: Scaffold
**Files:** `global.json`, `QuickEmoji.sln`, `Directory.Build.props`, `Directory.Packages.props`, `.editorconfig`, `.gitignore`, `.gitattributes`, `.github/workflows/ci.yml`, the 7 projects from CLAUDE.md, `docs/{TESTING,DECISIONS,ARCHITECTURE}.md`, `README.md`, `installer/README.md`.
**Main types:** `Core.IO.IFileSystem`/`PhysicalFileSystem`, `Core.AppPaths` (settings/usage/log locations); `Platform.Startup.StartupCommand` (Run-key value builder and matcher); `App.Program` (custom `Main`), `App.Logging.LogSetup` (Serilog, 7 × 1 MB rolling), `App.App`, `App.LaunchOptions` (`--background`, `--startup-check`).
**Risks:** WinUI build without Visual Studio (XAML compiler and PRI tooling under `dotnet build`); a filtered-out E2E project returning MTP exit code 8; self-contained WinAppSDK under `dotnet run`.
**Order:** props and global.json → Core + tests → Platform + tests → App → DataBuilder and E2E shells → build, test, launch/exit check → CI → docs → commit and push.

## Stage 1: Emoji data
**Files:** `tools/QuickEmoji.DataBuilder/{Program,SourceManifest,EmojiTestParser,CldrAnnotationParser,GemojiParser,EmojiJsonWriter}.cs`; `src/QuickEmoji.Core/Data/{emoji.json,synonyms.json,popularity.json}` (EmbeddedResource); `Core/Emoji/{EmojiEntry,EmojiVariant,SkinTone,EmojiVersion,EmojiCatalog,EmojiCatalogLoader}.cs`; `Platform/Fonts/EmojiFontProbe.cs` (DirectWrite shaping probe for MaxEmojiVersion); `THIRD_PARTY_NOTICES.md`; tests for parsers, loader, grouping, ZWJ round-trip, load time.
**Main types:** `EmojiEntry` (base emoji, name, keywords per language, shortcodes, group/subgroup, version, variants), `EmojiCatalog` (immutable, filtered by `MaxEmojiVersion`).
**Risks:** CLDR derived annotations for skin-tone and ZWJ variants; grouping multi-person skin-tone combos (🧑🏻‍🤝‍🧑🏿) under the right base; the Segoe UI Emoji → emoji version mapping must be measured (probe glyph shaping of sentinel emoji per version) rather than guessed; System.Text.Json source-generated deserialization to meet the 150 ms load budget; licence of Unicode frequency data for popularity.
**Order:** pin sources → parsers with fixture tests → merge and write emoji.json → synonyms/popularity → Core loader and model → load benchmark → font probe → notices.

## Stage 2: Search engine
**Files:** `Core/Search/{QueryNormalizer,Tokenizer,RankingWeights,SearchIndex,SearchIndexBuilder,DamerauLevenshtein,EmojiSearchEngine,SearchResult,MatchReason,LruCache}.cs`, `Core/Personalization/{FrecencyStore,FrecencyEntry,IClock}.cs`; `tests/data/ranking-golden.tsv` (≥120 cases); golden, normalization, fuzzy, frecency decay, LRU and latency tests.
**Main types:** `EmojiSearchEngine.Search(query) → IReadOnlyList<SearchResult>`; precomputed token → posting lists at index build; `RankingWeights` holds every constant.
**Risks:** hitting 100% golden top-1 while staying at p95 ≤ 5 ms with fuzzy matching (bound candidates by token length buckets); Cyrillic vs Latin normalization; mean-of-token scoring interacting with the minimum score.
**Order:** normalizer → index structures → tiers → multi-word AND + phrase bonus → fuzzy → priors → frecency → dedupe/min score → cache → golden set → tune data and ranking → benchmark.

## Stage 3: Hotkey, popup, tray → HUMAN GATE
**Files:** `Platform/NativeMethods.txt`, `Platform/Hotkeys/{HotkeyGesture,HotkeyValidator,HotkeyService,MessageOnlyWindow}.cs`, `Platform/Targeting/{TargetContext,TargetCapture}.cs`, `Platform/Display/{PopupPlacement,MonitorInfo}.cs` (pure math + Win32 query); `App/Popup/{PopupWindow.xaml,PopupViewModel,ResultRowViewModel}`, `App/Tray/TrayIconController.cs`, `App/Toast/ToastWindow.xaml`, `App/SingleInstance.cs`, `App/Assets/QuickEmoji.svg/.ico`; tests for hotkey validation and placement math; TESTING.md matrix.
**Risks:** H.NotifyIcon 2.4.1 compatibility with WinAppSDK 2.5.1 (fallback: Shell_NotifyIcon via CsWin32, recorded in DECISIONS.md); 50 ms show budget (window created once, pre-warmed); Acrylic fallback conditions; foreground activation only inside WM_HOTKEY.

## Stage 4: Insertion engine → HUMAN GATE
**Files:** `Platform/Injection/{InputBuilder,TextInjector,KeyReleaseWaiter,ForegroundRestorer}.cs`, `Platform/Security/IntegrityLevelReader.cs`, `Platform/Clipboard/{IClipboard,Win32Clipboard,ClipboardGuard,ClipboardSnapshot}.cs`, `Core/Insertion/{InsertionMethod,AppOverride,MethodSelector}.cs`, `App/Insertion/InsertionCoordinator.cs`; `tests/QuickEmoji.E2E/NotepadTests.cs`.
**Risks:** silent UIPI failure (integrity check first); clipboard ownership races (sequence-number check); Notepad on Windows 11 is a WinUI/RichEdit app, so the E2E assertions read text through UIA TextPattern.

## Stage 5: Settings, startup, first run, personalization
**Files:** `Core/Settings/{AppSettings,SettingsStore,UsageStore,AtomicFileWriter,SettingsDefaults}.cs`, `Core/Emoji/SkinToneApplier.cs`, `Platform/Startup/RunKeyStartup.cs`, `App/Settings/{SettingsWindow.xaml,SettingsViewModel,HotkeyRecorder}`, `App/FirstRun/FirstRunWindow.xaml`, `App/Diagnostics/DiagnosticsReport.cs`.
**Risks:** File.Replace across volumes; per-field fallback on invalid JSON; live hotkey test-registration racing with the active hotkey.

## Stage 6: Hardening and performance
**Files:** `App/Reliability/GlobalExceptionHandlers.cs`, `App/Perf/PerfTrace.cs` (`--perf`), display-change handling, README performance table, DECISIONS.md entries.
**Risks:** idle timers in WinUI (composition, caret blink) while hidden; idle working set above 100 MB with self-contained WinAppSDK.

## Stage 7: Packaging and docs
**Files:** `build-release.ps1`, `installer/QuickEmoji.iss`, final README, TESTING.md, ARCHITECTURE.md (Mermaid component diagram + insertion sequence).
**Risks:** WinAppSDK 2.5.1 metapackage pulls AI/ML components into self-contained output (measure size; trim unneeded runtime assets if safe); arm64 publish on an x64 host; per-user install without admin.
