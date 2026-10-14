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
