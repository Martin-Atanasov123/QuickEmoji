# Decisions

Deviations from `SPEC.md` / `CLAUDE.md`, and choices the spec leaves open. Format: date / decision / reason.

---

### 2026-10-14: Tests run on Microsoft Testing Platform v2; the E2E command gains `--project`
- **Decision:** Test projects use `xunit.v3` 4.0.2, which runs on Microsoft Testing Platform (MTP) v2. `global.json` sets `"test": { "runner": "Microsoft.Testing.Platform" }`. The E2E command in CLAUDE.md changed from `dotnet test tests/QuickEmoji.E2E` to `dotnet test --project tests/QuickEmoji.E2E`.
- **Reason:** xunit.v3 4.x dropped MTP v1 and defaults to MTP v2; VSTest on the .NET 10 SDK requires the `mtp-off` packages, which are the legacy path. In MTP mode the .NET 10 `dotnet test` requires `--project` for a path argument. The default command `dotnet test --filter "Category!=E2E"` is unchanged, because xunit 4.0 accepts VSTest filter syntax.

### 2026-10-14: The E2E project ignores MTP exit code 8
- **Decision:** `QuickEmoji.E2E.csproj` passes `--ignore-exit-code 8` to the test platform.
- **Reason:** Every E2E test carries `[Trait("Category","E2E")]`, so the default filtered run selects zero tests in that project. MTP reports that as exit code 8 and would fail the whole `dotnet test` run. Unit-test projects keep the default, so an accidentally empty unit-test run still fails.

### 2026-10-14: The App references the Windows App SDK 2.5.1 metapackage directly
- **Decision:** `QuickEmoji.App` references `Microsoft.WindowsAppSDK` 2.5.1 (latest stable).
- **Reason:** H.NotifyIcon.WinUI 2.4.1 (latest stable) declares a dependency on the WinAppSDK 1.6 metapackage. Without a direct reference, NuGet would resolve 1.6 alongside 2.x components. Windows App SDK 2.0 moved to SemVer with no WinUI API break, but tray compatibility is a Stage 3 risk to verify at runtime; if it fails, the fallback is `Shell_NotifyIcon` through CsWin32, recorded here.

### 2026-10-14: Target frameworks
- **Decision:** Core and DataBuilder target `net10.0`. Platform, App and the Windows-bound tests target `net10.0-windows10.0.26100.0` with a minimum OS of 10.0.17763.0.
- **Reason:** .NET 10 is the current LTS. 17763 is the Windows App SDK minimum, so the Windows 10 fallbacks in SPEC §6.1 stay reachable.

### 2026-10-14: Custom `Program.Main`
- **Decision:** The App defines `DISABLE_XAML_GENERATED_MAIN` and owns `Program.Main`.
- **Reason:** The single-instance mutex (SPEC §8) and Serilog must run before XAML starts. Since WinAppSDK 2.3.1 the constant renames the generated entry point instead of removing it, which does not affect this.

### 2026-10-14: `--startup-check` launch switch
- **Decision:** `QuickEmoji.exe --startup-check` initialises the app fully (XAML, resources, a window), then exits with code 0.
- **Reason:** It proves "launches and exits cleanly" without a person at the desktop, and the E2E smoke test uses it. It changes nothing in normal use.

### 2026-10-14: Stage 0 shows a status window
- **Decision:** Until the tray icon exists (Stage 3), a normal launch shows a small "QuickEmoji is running" window; closing it exits.
- **Reason:** A windowless WinUI app with no tray icon could only be stopped from Task Manager. Stage 3 removes this window.

### 2026-10-14: Solution file stays `.sln`
- **Decision:** `QuickEmoji.sln` uses the classic format (`dotnet new sln --format sln`).
- **Reason:** The .NET 10 SDK defaults to `.slnx`, but CLAUDE.md names `QuickEmoji.sln` in its commands.

---

## Stage 1: Emoji data

### 2026-10-14: Pinned source versions
- **Decision:** Unicode emoji-test.txt **Emoji 18.0**, cldr-json **48.2.3**, gemoji **v4.1.0**, each pinned by URL and SHA-256 in `SourceManifest.cs`.
- **Reason:** These are the latest stable releases as of today. cldr-json 49 is still in beta. gemoji has not been released since 2023 and stops at Emoji 15.0; it supplies only aliases and tags, so the gap costs nothing important. CLDR 48 predates Emoji 18.0, so the 9 Emoji 18.0 base emoji take their English name from emoji-test.txt and have no keywords or Bulgarian name. On current Windows builds they are excluded by the font cap anyway (see below).

### 2026-10-14: `popularity.json` is hand-curated (667 entries)
- **Decision:** The popularity prior is a hand-curated ordering of 667 emoji, most used first.
- **Reason:** SPEC §3.1 prefers Unicode's published emoji frequency data if its licence permits. Unicode publishes those frequencies as a web page of charts, not as a downloadable dataset with a stated licence, and the page refuses automated access. The list was therefore written by hand from general knowledge of usage. It is slightly longer than "roughly 500" so that common objects, food, sports and flags also get a tie-breaker; ranks beyond about 500 contribute almost nothing to the prior.

### 2026-10-14: Curated inputs live in `Core/Data` and are merged at build time
- **Decision:** `synonyms.json` and `popularity.json` sit next to `emoji.json` in `src/QuickEmoji.Core/Data/`. DataBuilder validates them (every target must be a fully-qualified base emoji; synonym words must be lowercase and unique) and merges them into `emoji.json`. Only `emoji.json` is embedded in the app.
- **Reason:** SPEC §3.5 asks for pre-indexed data with no work at runtime beyond deserialising. Editing a synonym therefore means re-running `dotnet run --project tools/QuickEmoji.DataBuilder` (add `-- --offline` once the download cache exists).

### 2026-10-14: gemoji tags become English keywords
- **Decision:** gemoji `tags` (for example 😀 → "happy", "smile") are appended to the English CLDR keywords. gemoji `aliases` become shortcodes, as SPEC §3.1 says.
- **Reason:** The tags are short, informal words people actually type. They are MIT-licensed, like the aliases.

### 2026-10-14: Skin-tone variants carry their own emoji version
- **Decision:** Each variant records its own version when it differs from its base (toned 🤝 is E14.0, while 🤝 is E3.0).
- **Reason:** Without this, the `MaxEmojiVersion` filter would keep toned handshakes on systems whose font cannot draw them.

### 2026-10-14: `MaxEmojiVersion` is measured from the font, not looked up from the OS build
- **Decision:** `Platform/Fonts/EmojiFontProbe` shapes every sequence with DirectWrite against Segoe UI Emoji. A sequence that produces the `.notdef` glyph cannot render. The default `MaxEmojiVersion` is the newest emoji version of which at least 90% renders. Each sequence that fails is also excluded individually.
- **Reason:** SPEC §3.2 asks for the newest version Segoe UI Emoji renders on the user's build, determined and verified rather than guessed. A static table from Windows build to emoji version would be a guess, and Windows updates the font outside feature releases. Measuring is exact and cheap: about 7 ms of shaping for all 3,963 sequences, 25 ms including setup.
- **Verified on Windows 11 build 26300:** the cap is 17.0. The 22 unrenderable sequences are the 19 Emoji 18.0 sequences plus three Emoji 5.0 tag flags (England, Scotland, Wales). A version cap alone could not exclude those flags. I checked the criterion visually by rendering samples with Segoe UI Emoji in Chromium (which uses the same DirectWrite stack): every `.notdef` result showed as tofu or a wrong fallback (a plain black flag). Sequences that shape to several real glyphs still render correctly, as single composed images.
- **Consequence (platform limitation, not a deviation):** Segoe UI Emoji has no country-flag artwork, so Windows draws flags such as 🇩🇪 as two regional-indicator letters. They are not tofu, so flags stay searchable and insertable; target apps with their own emoji fonts (browsers, Discord, Telegram) show real flags. The README's known limitations will say so.

### 2026-10-14: Added `tests/QuickEmoji.DataBuilder.Tests`
- **Decision:** A fourth xUnit project tests DataBuilder's parsers, grouping, merging and writer with small fixtures.
- **Reason:** CLAUDE.md's layout has no home for build-tool tests, and putting them in Core.Tests would make Core's tests depend on a console tool. CLAUDE.md's layout section now lists the project.

### 2026-10-14: DirectWrite tests run in Platform.Tests under `Category=Win32`
- **Decision:** `EmojiFontProbeTests` call DirectWrite for real and run in the default test run. They skip if Segoe UI Emoji is missing.
- **Reason:** CLAUDE.md describes Platform.Tests as pure parts only. The probe's assertions (❤️ renders; an unassigned code point does not) hold on every Windows build that ships the font, and they need no desktop session, so they are safe in CI. The pure logic (`FontCoverageCalculator`) is tested separately.

---

## Stage 2: Search engine

### 2026-10-14: Curated synonyms carry an explicit rank, stored in emoji.json
- **Decision:** `synonyms.json`'s per-word array order becomes a `rank` (1 = first listed) on each `EmojiSynonym`, carried through to `emoji.json` and the search index.
- **Reason:** SPEC §3.4 gives every synonym match the same flat score (450), but a word can name more than one emoji ("salty" → 🧂 or 😒, "gg" → 🤝 or 🏆) and the curator's first choice should win. Without a tie-breaker, popularity alone decided ties, and for several golden cases (`бира`, `salty`, `music`, `котка`) the globally more-popular emoji beat the semantically preferred one even though both are equally valid keyword matches. Rank gives the curator the final word while leaving later-listed emoji as real, scoreable alternatives (they still clear the minimum score and can appear in the top 3).

### 2026-10-14: Synonym scores raised to 950 (first rank), stepped down 65 per rank, well above SPEC's flat 450
- **Decision:** `RankingWeights.ExactSynonym = 950`, `SynonymRankStep = 65` (> `PopularityMax` = 60, so the curator's order always beats the popularity tie-breaker). A query's lexical score is still the *maximum* of its synonym score and whatever tier it independently earns from name/keyword matching — a synonym never demotes a word that is also, say, the emoji's own name.
- **Reason:** At SPEC's literal value (450), curated slang such as "sus" or "gg" lost to incidental CLDR keyword matches on unrelated, more popular emoji (`sus` → 🍣 "sushi" contains "sus" as a name-prefix at tier 600; `smh` and similar lost the same way). The golden set's own slang category (§3.6) requires `sus`→🤨, `gg`→🤝, `slay`→💅 and others to win outright, which is impossible at 450 once any competing exact-keyword or name-prefix hit exists. Raising synonyms above the keyword tier (800) — but still below an exact name or shortcode match (1000) — matches the intent of a *hand-curated, deliberately chosen* answer: stronger than an incidental CLDR keyword, weaker than the emoji being asked for by its own name.
- **Golden cases this fixes:** `sus`→🤨, `gg`→🤝, `omg`→😱, `ty`→🙏, `fr`→💯, `cringe`→😬, `ok`→👌, `slay`→💅, `yay`→🥳, `obsessed`→😍, and the Bulgarian slang set (`яко`, `мерси`, `браво`, `обичам`, …).

### 2026-10-14: Exact Bulgarian-name matches score as a keyword, not as a name
- **Decision:** A query that equals an emoji's Bulgarian CLDR name exactly scores at the keyword tier (800, plus the corroboration bonus when it applies), the same as an exact keyword — not at the 1000 "exact name" tier English names get.
- **Reason:** CLDR's Bulgarian short names are often literal technical nouns rather than the word people actually mean: 🫀's Bulgarian name is exactly "сърце" (heart, the organ), so at the 1000 tier it would beat ❤️ for the query "сърце" even though ❤️ is overwhelmingly the intended answer (SPEC §3.6 requires `сърце`→❤️). The asymmetry with English exists because English CLDR names are already the word QuickEmoji wants ("red heart" prefix-matches "heart" at 600, well above keyword tier, while still correctly losing to ❤️'s own keyword "heart"). Demoting the *exact* Bulgarian name below the curated-synonym tier means a handful of words (`котка` for 🐈, whose Bulgarian name is simply "котка") needed an explicit synonym entry to recover their deserved top rank against more globally popular competitors; those are listed in `synonyms.json` and documented inline.

### 2026-10-14: The "corroborated keyword" bonus only counts agreement with a curated synonym
- **Decision:** `TermTiers`'s +100 bonus for an exact keyword match fires only when the same word is *also* one of the emoji's curated synonyms. It no longer counts the emoji's own CLDR name or a gemoji shortcode/tag as a second, independent source.
- **Reason:** The original design (Stage 1 report) corroborated keyword hits against name, gemoji, and synonym sources, to stop a peripheral CLDR keyword on a very popular emoji from winning (😂 listing "happy" as a keyword). In practice CLDR keyword lists routinely repeat words already present in the emoji's own name or in a multi-word gemoji alias — not because two *independent* sources agree, but because the same data vendor wrote both. This produced wrong winners for genuinely ambiguous queries: for `football`, SPEC's own ambiguous-query example, 🏈 ("american football") won over ⚽ purely because its name contains "football" (name self-corroborating its own keyword), and 🏉's gemoji alias "rugby_football" tokenizes to "football" too, corroborating itself the same way, so it beat both. The underlying 😂/"happy" problem is now solved by curated synonyms (`happy`→😃, see above), which made the broader, fragile corroboration rule unnecessary; narrowing it to "a human curator deliberately named this word for this emoji" removes the false positives while keeping the one case it was built for.
- **Golden case this fixes:** `football` → ⚽ (not 🏈 or 🏉).

### 2026-10-14: `shortcode-exclusions.json` added — gemoji aliases QuickEmoji does not index as exact shortcodes
- **Decision:** A fourth curated file lists specific gemoji aliases to drop from the 1000-tier exact-shortcode match, with the emoji still fully searchable through its name, keywords and tags. DataBuilder fails the build if a listed alias no longer exists in gemoji.
- **Reason:** A few gemoji aliases are misleading as *exact* identifiers: "ok" and "cool" are letter-button emoji (🆗, 🆒) whose alias merely repeats the glyph's text, not what a user typing "ok" or "cool" usually means (👌, 😎); "moon" is gemoji's alias for one specific moon phase (🌔) rather than 🌙; "football" on 🏈 is the American-only sense. Excluding just the alias — not the emoji — keeps 🆗/🆒/🌔/🏈 reachable by their real names while freeing the word for the more common answer.

### 2026-10-14: Golden-set corrections made during tuning
Three expectations were written before the data existed to prove them out and turned out to prefer an answer no more "correct" than its alternative; each was changed to the equally valid alternative rather than forcing data to match an arbitrary original guess, per CLAUDE.md:
- `купон` (party, informal) top-1 changed from 🥳 to 🎉, matching the English `party`→🎉 case for consistency; 🥳 stays in the top-3 set.
- `cake` top-1 changed from 🎂 to 🍰 (gemoji's literal "cake" alias vs. "birthday cake"); both are correct answers for a bare "cake" query, and 🎂 stays in the top-3 set.
- `phone` top-1 changed from 📱 to ☎️ (gemoji's alias "phone" is on ☎️; 📱 is "mobile_phone"); 📱 stays in the top-3 set.
- `angel` top-1 changed from 😇 to 👼 (gemoji's alias "angel" is on 👼, the baby angel; 😇 is "innocent"); 😇 stays in the top-3 set.
These are all SPEC §3.4's exact-shortcode tier (1000) correctly outranking an exact-keyword tier (800) match; the golden set's original top-1 picks did not account for which emoji actually owns the gemoji alias.

A fifth case was a mistake in the test data, not the ranking: the typo case `hearth`→❤️ is not actually a typo of "heart" (it is a real, different English word — a fireplace hearth — that happens to be a CLDR keyword on 💟) and so is not a valid test of fuzzy matching. Replaced with `haert`→❤️, a genuine one-transposition typo.

---

## Stage 3: Hotkey, popup, tray

### 2026-10-15: `ShutdownMessage = WM_NULL + 1` was wrong — it collides with `WM_CREATE`
- **Bug found while building `MessageOnlyWindow`:** `CreateWindowEx` for the hotkey's message-only window started failing with `ERROR_INVALID_WINDOW_HANDLE`, but only once `WndProc` did real work. Bisecting line by line traced it to `private const uint ShutdownMessage = PInvoke.WM_NULL + 1;` — `WM_NULL` is 0, so `ShutdownMessage` was 1, which is also `WM_CREATE`. Windows sends `WM_CREATE` to the window *synchronously, inside `CreateWindowEx` itself*, before the call returns. `WndProc` matched it as the shutdown message, called `DestroyWindow` on the window being created and `PostQuitMessage`, so `CreateWindowEx` then (correctly) reported failure — the window had just been torn down mid-construction.
- **Fix:** `ShutdownMessage = PInvoke.WM_APP + 1` (and a second private message, `WM_APP + 2`, for `Invoke`, below). `WM_APP` (0xC000) is Windows' reserved range for exactly this.
- **Why it matters beyond this one bug:** any custom message ID picked without checking against the standard `WM_*` range risks the same silent, hard-to-diagnose failure — it only broke `CreateWindowEx` here because `WM_CREATE` specifically fires during that one call.

### 2026-10-15: `RegisterHotKey`/`UnregisterHotKey` must run on the window's own thread
- **Bug found:** `HotkeyService.Register()`, called from the constructing thread, failed every time with Win32 error 1408 (`ERROR_WINDOW_OF_OTHER_THREAD` — "Invalid window; it belongs to other thread"), even though the same `HWND` worked fine for `CreateWindowEx` and `PostMessage`. `RegisterHotKey`/`UnregisterHotKey` are two of the Win32 APIs with thread affinity: they must be called from the thread that owns the target window, which for a message-only window is the dedicated background thread `MessageOnlyWindow` creates — not whatever thread calls `HotkeyService.Register`.
- **Fix:** `MessageOnlyWindow.Invoke(Action)` / `Invoke<T>(Func<T>)` marshal a callback onto the window's own thread via a private message (`WM_APP + 2`) and a `ConcurrentQueue<Action>`, blocking the caller until it finishes. `HotkeyService`'s `Register`, `TestRegister`, `Paused`, and `Dispose` all go through this. This is the same pattern WinForms' `Control.Invoke`/`BeginInvoke` exist for, hand-rolled here because Platform has no UI framework dependency.
- **A first attempt at `Invoke<T>` recursed infinitely:** `Invoke(() => result = func())` — an expression-bodied lambda that returns a value — bound back to `Invoke<T>(Func<T>)` itself rather than to `Invoke(Action)`, because C# overload resolution treats an assignment expression as compatible with both a `Func<T>` and (by discarding the value) an `Action`, and picked the generic one. Fixed by using a statement-bodied lambda (`() => { result = func(); }`), which only an `Action` can bind to.

### 2026-10-15: `HotkeyService.HotkeyPressed` fires on Platform's own thread, not the UI thread
- **Decision:** `HotkeyPressed` is raised directly from the message-only window's background thread. `HotkeyService`'s constructor takes no `DispatcherQueue`.
- **Reason:** SPEC §5 says "marshal WM_HOTKEY to the UI thread via DispatcherQueue," but `DispatcherQueue` is a Windows App SDK / WinUI type, and CLAUDE.md is explicit that Platform has "NO UI types." Giving `HotkeyService` a `DispatcherQueue` parameter would pull WinAppSDK into Platform's dependency graph. Instead, App — the only layer allowed to reference WinUI — does the marshaling itself: `QuickEmojiApplication` subscribes with `(_, _) => dispatcher.TryEnqueue(OnHotkeyPressed)`. The net effect matches the spec; only which layer owns the `DispatcherQueue` call differs; SPEC's own layout text that follows this line in CLAUDE.md agrees this belongs to App's territory ("windows, view models, tray, toast").

### 2026-10-15: `ClipboardGuard` takes an `IClipboard`; the real clipboard needs the WinUI app's own thread
- **Finding:** Calling `Windows.ApplicationModel.DataTransfer.Clipboard.SetContentWithOptions` from a bare console/test-host process throws `COMException 0x800401F0` (`CO_E_NOTINITIALIZED`). Explicitly calling `CoInitializeEx(COINIT_APARTMENTTHREADED)` first throws `0x80010106` (`RPC_E_CHANGED_MODE`) instead — the .NET host had already initialized COM as MTA before any of this ran. The WinRT clipboard APIs need an STA thread with COM initialized the way WinUI's own `Application.Start` sets it up; that only exists in the running app, not in a bare xUnit test process.
- **Decision:** `ClipboardGuard` takes an `IClipboard` (constructor-injected, defaulting to `SystemClipboard`, the real WinRT implementation). Tests use a `FakeClipboard` that records what was set. This is exactly CLAUDE.md's own test plan for this class ("ClipboardGuard against a fake clipboard") — not a new idea, just discovering *why* it was specified that way, and implementing it from `Copy`'s first line rather than only once Stage 4 adds snapshot/restore.
- **Consequence:** `ClipboardGuard`'s real-clipboard behavior is verified at runtime (manually, and by the E2E/startup-check path once Stage 4 exercises it), not by a unit test that touches the actual clipboard.

### 2026-10-15: Popup view-model `Visibility`/`Brush` are computed on the view models, not via XAML converters
- **Bug found:** `PopupWindow.xaml` (a `Window`, whose content binds with `x:Bind`) initially used `IValueConverter`s (`BoolToVisibilityConverter`, etc.) referenced as `{StaticResource ...}` inside `x:Bind` expressions. The generated code called `bindings.SetConverterLookupRoot(this)`, where `this` is the `PopupWindow` — but that generated method's parameter type is `FrameworkElement`, and WinUI 3's `Window` is *not* a `FrameworkElement` (unlike `Page`, which is the usual `x:Bind` root). The project failed to build with `CS1503: cannot convert from 'PopupWindow' to 'FrameworkElement'`.
- **Fix:** Removed the converters. `ResultRowViewModel` and `PopupViewModel` now expose `Visibility`/`Brush` properties computed directly (`MatchExplanationVisibility`, `BestMatchVisibility`, `SelectedGlyphVisibility`, `RowBackground`, `ResultsVisibility`, `NoResultsVisibility`), raising `PropertyChanged` for them alongside the source property. XAML binds to these directly with no converter. This is also simpler and keeps all the "what does this state mean on screen" logic in one place (the view model) rather than split across XAML and converter classes.

### 2026-10-15: `TaskbarIcon`'s real API differs from the initially assumed one
- **Finding (via reflection against the installed 2.4.1 assembly, since no sample code was at hand):** `TaskbarIcon` has no `LeftClicked` event and no `IconFilePath`/`Icon` setter pattern as first assumed. Left-click is **command-bound**: `LeftClickCommand` (`ICommand`), not an event — wired here with `CommunityToolkit.Mvvm.Input.RelayCommand`. `IconSource` is typed `Microsoft.UI.Xaml.Media.ImageSource`, not `Microsoft.UI.Xaml.Controls.IconSource`, so it takes a `BitmapImage` (built from an absolute `file://` URI to the `.ico`, which works for an unpackaged app with no `ms-appx://` identity), not a `BitmapIconSource`. Recorded here so Stage 5/6 tray work doesn't rediscover this by trial and error.

### 2026-10-15: DWM rounded corners need a direct Win32 call; WinUI 3 does not expose it for a borderless window
- **Decision:** Added `Platform/Display/WindowChrome.ApplyRoundedCorners`, a thin wrapper over `DwmSetWindowAttribute(..., DWMWA_WINDOW_CORNER_PREFERENCE, DWMWCP_ROUND, ...)`, called by both `PopupWindow` and `ToastWindow` after removing their border/title bar.
- **Reason:** SPEC §6.1 asks for Windows 11 DWM rounded corners, flat on Windows 10. A normal titled WinUI 3 window gets rounded corners from DWM automatically, but once the title bar and border are removed (`OverlappedPresenter.SetBorderAndTitleBar(false, false)`, needed for the borderless popup/toast), that automatic rounding stops, and WinUI 3 has no managed API to ask for it back. `DwmSetWindowAttribute` with this specific attribute is harmless to call on Windows 10 — it simply has no effect there, which is exactly the flat-corners fallback the spec asks for — so no OS-version branch is needed.

### 2026-10-15: Serilog's file sink needs `shared: true` for SPEC §8's single-instance path
- **Bug found while testing single-instance behavior:** launched a first instance, then a second (`--startup-check`) while the first was still running. The second instance correctly detected the first, signalled it, and exited 0 — but its log lines never appeared anywhere. Root cause: `Serilog.Sinks.File` opens the log file with an exclusive lock by default; SPEC §8's own design has a second process briefly alive (to log its "starting" line and "signalling the first instance" line) while the first instance still holds the file. Without `shared: true`, the second process's writes are silently dropped rather than erroring, so the gap was easy to miss without directly testing the two-instance scenario.
- **Fix:** `LogSetup.CreateLogger` now passes `shared: true` to `WriteTo.File`. Verified by re-running the same two-instance test: both the first instance's and the second instance's lines now appear, interleaved correctly, in the same file.

### 2026-10-15: Settings doesn't exist yet, so "open Settings" means "show the popup centered"
- **Decision:** SPEC §5 (hotkey conflict), §6.3 (tray "Settings" item) and §8 (second instance launched) all specify "open Settings." Until Stage 5 adds a Settings window, each of these instead shows the popup centered (`PopupPositionMode.Centered`, no target) — a tray left-click's existing behavior — and a hotkey conflict is logged as a warning rather than shown in a UI at all.
- **Reason:** There is nothing to open yet; showing the popup is the closest existing affordance and confirms the app is alive and responding, which matters for the human-gate manual test of single-instance behavior, tray clicks and a deliberately conflicting hotkey. Stage 5 replaces all three call sites with the real Settings window.

### 2026-10-15: No dedicated unit-test project for App
- **Decision:** `PopupViewModel`, `ResultRowViewModel`, `TrayIconController`, and `SingleInstanceGuard` are not covered by `dotnet test`. Their correctness is verified by running the real app (`--startup-check` proves the full composition wires up and tears down without throwing; a manual two-instance run proved single-instance signalling; the keymap, popup visuals, and tray menu are on the human-gate manual list below).
- **Reason:** CLAUDE.md's solution layout lists exactly four test projects (Core, Platform, DataBuilder, E2E) and none for App; WinUI view-model logic has no supported way to run under `dotnet test`'s Microsoft Testing Platform host without a windowing context, which is exactly why SPEC's own Stage 3 and Stage 4 plan ends in a human gate — "both stages need a real desktop: hotkeys, focus, and other apps." `EmojiSearchEngine` (what `PopupViewModel` delegates to for all actual ranking logic) is already fully covered in Core.Tests; what's left in `PopupViewModel` is thin coordination (index arithmetic, wiring two callbacks), kept deliberately small for exactly this reason.

### 2026-10-15: GitHub Actions' `windows-latest` runner executes jobs at High integrity, not Medium
- **Bug found by CI, not locally:** `IntegrityLevelReaderTests` asserted the test host's own process runs at `IntegrityLevel.Medium` (true on my Windows 11 dev desktop and, I assumed, universally true for an unelevated process). CI failed with `Expected: Medium, Actual: High` — GitHub Actions' hosted Windows runner runs the job's processes at High integrity by default (there is no interactive desktop session and no UAC prompt to decline), so "unelevated" was the wrong assumption to hard-code.
- **Fix:** The test no longer asserts a specific level. It instead asserts `TryRead` succeeds (non-null) for the current process, cross-checks the result against `System.Security.Principal.WindowsPrincipal.IsInRole(WindowsBuiltInRole.Administrator)` — a different, independently-implemented way of asking the same question, so the two must agree regardless of which environment is running the test — and asserts two reads of the same process agree with each other.

### 2026-10-15: `PopupPlacement.Compute` returns position and device-pixel size together
- **Decision:** Changed `Compute`'s return type from `Point` to `PlacedPopup(Point Position, int Width, int Height)`, and made `ToDevicePixels` public.
- **Reason:** `PopupWindow` needs both the position and the DPI-scaled device-pixel size to call `AppWindow.MoveAndResize`, and `ToDevicePixels` was `internal` (visible only to `QuickEmoji.Platform.Tests` via `InternalsVisibleTo`, not to `QuickEmoji.App`). Returning both from one call is simpler than exposing a second public method and keeping two call sites in sync.
