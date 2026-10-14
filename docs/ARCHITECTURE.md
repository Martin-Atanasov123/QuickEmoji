# Architecture

QuickEmoji is three layers with one-way dependencies. This document grows with each stage; Stage 7 adds the insertion sequence diagram.

```mermaid
flowchart LR
    App["QuickEmoji.App<br/>WinUI 3 composition root:<br/>windows, view models, tray, toast"]
    Platform["QuickEmoji.Platform<br/>Win32 only: hotkey, target capture,<br/>SendInput, ClipboardGuard, startup"]
    Core["QuickEmoji.Core<br/>pure logic: emoji model, search,<br/>ranking, frecency, settings"]
    DataBuilder["tools/QuickEmoji.DataBuilder<br/>build-time only"]
    Data[("Core/Data/emoji.json<br/>(committed)")]

    App --> Platform
    App --> Core
    Platform --> Core
    DataBuilder --> Data
    Core -. loads .-> Data
```

## Rules
- Core references nothing project-local and nothing Windows-specific. File access goes through an abstraction.
- Platform holds Win32 interop (CsWin32) and no UI types.
- App is thin: it wires Core and Platform together and owns all UI.
- DataBuilder is the only component allowed to use the network (the opt-in semantic-model download in SPEC §3.7 is the single runtime exception).

## Emoji data pipeline (Stage 1)

```mermaid
flowchart LR
    ET["emoji-test.txt<br/>Emoji 18.0"] --> P[DataBuilder parsers]
    CLDR["CLDR annotations<br/>+ derived, en/bg"] --> P
    GE["gemoji v4.1.0"] --> P
    SY["Core/Data/synonyms.json"] --> P
    PO["Core/Data/popularity.json"] --> P
    P --> G["SkinToneGrouper<br/>variants under base"] --> A["EmojiDataAssembler<br/>merge + validate"] --> W["EmojiJsonWriter"] --> J[("Core/Data/emoji.json<br/>embedded")]
    J --> L["EmojiCatalogLoader"] --> C["EmojiCatalog"]
    F["Platform: EmojiFontProbe<br/>(DirectWrite)"] --> FC["FontCoverage<br/>MaxEmojiVersion + unrenderable"]
    C --> X["catalog.Apply(filter)"]
    FC --> X
```

- Sources are joined on a VS16-free key, but the stored string is always the exact fully-qualified sequence from emoji-test.txt. Nothing in the pipeline rewrites an emoji string.
- DataBuilder re-loads its output through `EmojiCatalogLoader` and refuses to write unless every fully-qualified sequence survives exactly once.
- At startup the app loads the catalog (about 50 ms cold), measures font coverage (about 25 ms), and applies the filter. Stage 3 wires this in, off the UI thread.

## Search engine (Stage 2)

```mermaid
flowchart LR
    Q["query string"] --> N["QueryNormalizer<br/>trim, lowercase, fold, tokenize"]
    N --> M["SearchIndex.Match<br/>per-token exact / prefix / fuzzy<br/>AND across tokens, phrase bonus"]
    C["EmojiCatalog"] -. built once .-> IDX["SearchIndex<br/>term → postings, per language"]
    IDX --> M
    M --> LRU["LruCache&lt;query, candidates&gt;<br/>256 entries"]
    LRU --> E["EmojiSearchEngine.Search"]
    POP["Popularity prior<br/>(0–60, from emoji.json)"] --> E
    FRE["FrecencyStore<br/>decayed per-query + global counts"] --> E
    E --> R["top 3 ≥ minimum score"]
```

- `SearchIndex` is immutable and built once from the catalog; `EmojiSearchEngine` owns one per process (via `App`/`Core` composition, wired in Stage 3).
- The LRU cache holds lexical candidates only (not popularity or frecency), so a `RecordUse` changes the next search immediately without invalidating any cache entry.
- `RankingWeights` is the single source of every scoring constant (SPEC's own rule); `docs/DECISIONS.md` records where Stage 2's tuning departs from SPEC's literal numbers and why.
- `tests/data/ranking-golden.tsv` is the contract: 184 cases, 100% top-1 and 100% top-3-set.

## Startup (Stage 0)
`Program.Main` parses the command line, configures Serilog (`%LOCALAPPDATA%\QuickEmoji\logs`, 7 × 1 MB), then starts WinUI with a `DispatcherQueueSynchronizationContext`. `QuickEmojiApplication.OnLaunched` creates the window. `--startup-check` exits as soon as initialisation finishes.
