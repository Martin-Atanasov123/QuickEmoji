# QuickEmoji — Product & Technical Specification (v1.0)

Status: authoritative. If code and this spec disagree, the spec wins unless a deviation is recorded in `docs/DECISIONS.md` with a reason.

---

## 1. Positioning

Windows already ships an emoji panel (Win + .). QuickEmoji exists only if it is faster and more accurate at one job: **turning a word into the right emoji and putting it where the user was typing**, in as few keystrokes as possible.

What makes it better than the built-in panel:

| Built-in panel | QuickEmoji |
|---|---|
| Unranked grid of keyword hits | Ranked top 3 with one confident best match |
| Exact keyword matching | Synonyms, slang, typo tolerance |
| No learning | Learns which emoji you pick for which words |
| One UI language | English and Bulgarian keywords searched together |
| Mouse-oriented | Keyboard-only: hotkey → word → Enter |

### Non-goals for v1
- GIFs, stickers, kaomoji, custom emoji
- Cloud AI or any network call during normal use
- An inline trigger such as typing `:fire` inside other apps (requires a low-level keyboard hook; antivirus tools flag it and it adds a large failure surface)
- macOS or Linux

---

## 2. Core interaction

```
[any app, typing] --Win+Shift+E--> [popup: search focused]
     type "love" --> results update on every keystroke
     Enter        --> popup hides, focus returns, ❤️ appears at the caret
     Esc          --> popup hides, nothing changes anywhere
```

### Keymap (popup open)

| Key | Action |
|---|---|
| Enter | Insert the selected emoji (row 1 by default) |
| Alt+1 / Alt+2 / Alt+3 | Insert that row directly |
| Ctrl+Enter | Copy the selected emoji to the clipboard and close; do not insert |
| Up / Down, Shift+Tab / Tab | Move selection (wraps) |
| Esc | Close with no side effects |
| Hotkey again | Close (toggle) |
| Click a row | Insert that row |
| Click outside / popup loses activation | Close as Esc |

### States
- **Empty query:** show the user's 3 most frequently picked emoji (frecency). First run: ❤️ 😂 👍.
- **Results:** exactly 3 distinct emoji when at least 3 candidates clear the minimum score. Fewer if not; never pad with weak matches.
- **No results:** "No match. Try another word." and Enter does nothing.
- **Error after commit:** a small transient toast near the popup's last position (see §4.6).

Row 1 is always selected when results change. Selection never animates; speed matters more than motion.

---

## 3. Search engine

### 3.1 Data sources (pinned versions, processed at build time)

A console tool (`tools/QuickEmoji.DataBuilder`) downloads pinned versions of the sources below, merges them, and writes `src/QuickEmoji.Core/Data/emoji.json`. **Commit the generated file**; normal builds must not need network access.

- **Unicode `emoji-test.txt`**: the list of emoji, fully-qualified forms only, plus group, subgroup, and emoji version.
- **CLDR annotations and annotationsDerived** (cldr-json, `en` and `bg`): official names and keywords, including derived entries for skin-tone variants.
- **GitHub gemoji aliases** (MIT licence): shortcodes such as `thumbsup`.
- **`synonyms.json`** (hand-maintained, committed): slang and synonyms in English and Bulgarian, for example `lol→😂`, `gg→🤝 🏆`, `ок→👌`, `obsessed→😍`.
- **`popularity.json`**: a prior ordering of roughly the 500 most used emoji, used to break ties so that `heart` returns ❤️ rather than 🫀. Use Unicode's published emoji frequency data if its licence permits; otherwise curate the list by hand. Record the choice in DECISIONS.md.

Record the source URLs, versions, and licences in `THIRD_PARTY_NOTICES.md`.

### 3.2 Data model
- **Base emoji with variants.** Skin-tone variants are grouped under their base emoji. Search ranks and returns base emoji only. The user's preferred skin tone (a setting) is applied at insertion time when the emoji supports it.
- **ZWJ sequences, flags, and keycaps are atomic strings.** They are never split, re-normalized, or stripped of VS16 anywhere in the pipeline.
- **Font coverage cap.** Emoji above `MaxEmojiVersion` are excluded from the index. Set the default from the OS build, using the newest emoji version that Segoe UI Emoji renders on that build. Determine and verify that mapping; do not guess it. An emoji that renders as tofu (an empty box) must never appear in results.

### 3.3 Query normalization
Apply in order: trim; collapse internal whitespace; lowercase with invariant culture; strip Latin diacritics (NFKD). Leave Cyrillic as typed. Treat the query as a sequence of tokens.

### 3.4 Scoring

Each candidate gets the score of the best tier it matches. All constants live in a single `RankingWeights` class.

| Tier | Score |
|---|---|
| Exact CLDR name or shortcode | 1000 |
| Exact keyword | 800 |
| Prefix of a name token | 600 |
| Prefix of a keyword | 500 |
| Exact synonym | 450 |
| Fuzzy match (Damerau-Levenshtein ≤1 for token length 4–6, ≤2 for length ≥7; no fuzzy matching under 4 characters) | 300 |
| Group or subgroup word | 200 |

- **Multi-word queries:** every token must match something (AND). The score is the mean of the token scores, plus 150 if the full phrase is a prefix of the name (`thumbs up`, `red heart`).
- **Adjustments:** add popularity prior (0–60) and frecency (0–150). Frecency is tracked per (normalized query, emoji) and globally, with exponential decay and a 14-day half-life.
- **Minimum score to show:** 150.
- **Deduplicate** by base emoji before taking the top 3.
- **Cache:** an LRU cache of 256 queries. Invalidate a query's entry when its frecency changes.

### 3.5 Performance
- Per-query latency at p95: 5 ms or less, measured in a benchmark test.
- Index load at startup: 150 ms or less (pre-indexed data; no work at runtime beyond deserializing).

### 3.6 Golden ranking tests
`tests/data/ranking-golden.tsv` holds at least 120 cases in the form `query  expected_top1  expected_top3_set`. Cover:
- the examples from the original brief (love, fire, happy, dog, coffee, money, sad, party)
- Bulgarian queries (сърце→❤️, кафе→☕, куче→🐶, огън→🔥, тъжен→😢)
- slang (lol, gg, sus, goat)
- typos (hapy, cofee, firre)
- multi-word queries (thumbs up, red heart, face with tears)
- ambiguous queries (bank, fire, star, cool)

The golden set is the definition of ranking quality. Fix failures by improving ranking or data, never by weakening the expectations. If an expectation is genuinely wrong, change it and record why in DECISIONS.md.

### 3.7 Semantic mode (optional, Stage 8, off by default)
- **Model:** a local ONNX sentence-embedding model (candidate: all-MiniLM-L6-v2, int8-quantized, Apache-2.0) run with Microsoft.ML.OnnxRuntime.
- **Vectors:** precompute emoji embeddings from name, keywords, and synonyms in DataBuilder.
- **Download:** fetch the model on demand when the user enables the mode, with a checksum verified against a pinned SHA-256. Do not bundle it.
- **Fusion:** semantic results only fill slots that lexical search left empty or weak (lexical score below 450). They never displace a strong lexical hit, so the top result does not flicker. Semantic matching runs off the UI thread with a 120 ms debounce.
- **No cloud LLM mode.** It would send every keystroke off the machine and could not meet the latency budget.

---

## 4. Insertion engine (the critical path)

### 4.1 Capture at hotkey time: `TargetContext`
Record before showing the popup:
- foreground HWND, process ID, and process name
- focused child HWND and caret rectangle (from GetGUIThreadInfo)
- the target process's integrity level
- a timestamp

### 4.2 Commit pipeline
1. Hide the popup immediately, with no exit animation.
2. Call `SetForegroundWindow(target)`. Poll `GetForegroundWindow()` until it returns the target, for at most 250 ms. If the target window no longer exists or something else has taken the foreground, **do not inject**. Go to Copy fallback (4.6).
3. Wait until Ctrl, Shift, Alt, Win, and Enter are all released (GetAsyncKeyState), for at most 300 ms. Otherwise the injected characters combine with held modifiers.
4. Choose the method:
   - **Target runs elevated and QuickEmoji does not:** use Copy fallback. UIPI blocks SendInput into higher-integrity processes **silently**: SendInput still reports success. The clipboard paste path also uses SendInput (to press Ctrl+V), so it is blocked in the same way.
   - **A per-app override matches the process name:** use the method it names.
   - **Otherwise:** Unicode injection.
5. **Unicode injection:** send everything in one SendInput call. For each UTF-16 code unit, send a key-down and key-up pair with `KEYEVENTF_UNICODE`. Surrogate pairs, ZWJ, VS16, skin-tone modifiers, and tag sequences all go through unchanged. The pure function that builds the INPUT array from a string is unit-tested against 👨‍👩‍👧‍👦, 👍🏽, 🏳️‍🌈, 🏴󠁧󠁢󠁳󠁣󠁴󠁿, and 1️⃣.
6. **Clipboard paste:** used only through a per-app override.
   1. `ClipboardGuard` takes a snapshot of the clipboard.
   2. Put the emoji on the clipboard together with the formats `ExcludeClipboardContentFromMonitorProcessing`, `CanIncludeInClipboardHistory` = 0, and `CanUploadToCloudClipboard` = 0, so that it stays out of Win+V history and cloud sync.
   3. Inject Ctrl+V (or Shift+Insert, if the override says so).
   4. Wait for the restore delay (setting, default 150 ms).
   5. Restore the snapshot only if `GetClipboardSequenceNumber` shows that nothing else changed the clipboard in the meantime.
7. Record the frecency update. Log the method, process name, outcome, and timings.

### 4.3 Clipboard snapshot fidelity
- **Restored exactly:** CF_UNICODETEXT, CF_TEXT, HTML Format, Rich Text Format, CF_DIB/CF_DIBV5, PNG, and CF_HDROP.
- **Other formats:** best effort.
- **Snapshots over 32 MB:** skip the clipboard method and use Copy fallback.

Document these limits in the README.

### 4.4 Why there is no automatic "did it work?" retry
Injection cannot be verified reliably across apps. A retry after a slow app finally processes the input would insert the emoji twice. Failures are handled up front instead: the foreground check, the elevation check, and per-app overrides.

### 4.5 Known limits (state them in the README)
- Games that use raw input
- Some RDP/VM client windows
- UAC and secure-desktop prompts
- Some terminals and legacy apps

For any of these, the user adds a per-app override or uses Ctrl+Enter.

### 4.6 Copy fallback
1. Put the emoji on the clipboard. This deliberately overwrites the clipboard, because the user needs to paste it.
2. Show a toast: "Copied ❤️ — press Ctrl+V". Where the cause is known, say it, for example "the target app runs as administrator".
3. The toast is a small custom window. It does not take focus and auto-hides after 2.5 s.

---

## 5. Global hotkey

- **Registration:** RegisterHotKey on a dedicated message-only window owned by the Platform layer, with `MOD_NOREPEAT`. Marshal WM_HOTKEY to the UI thread via DispatcherQueue.
- **Default: Win+Shift+E.** Applications almost never bind Win-modifier combinations, so the default does not collide with apps. Ctrl+Shift+E, from the original brief, collides with VS Code's Explorer view; offer it as a preset only.
- **Hotkey recorder (Settings):**
  - The combination must include Ctrl, Alt, or Win.
  - Warn on Ctrl+Alt combinations: they act as AltGr on many European layouts and produce characters.
  - Test-register the combination live and show a conflict error inline when it fails.
- **Registration failure at startup:** show a tray notification and open Settings at the hotkey field. Never fail silently.
- **Pause:** the tray menu can pause and resume the hotkey.
- **Foreground rights:** receiving WM_HOTKEY grants the process the right to take the foreground. Activate the popup inside the hotkey handler and nowhere else.

---

## 6. User interface

### 6.1 Popup
- **Window:**
  - Borderless, always on top.
  - Hidden from the taskbar and Alt+Tab (AppWindow `IsShownInSwitchers = false`).
  - **Created once at startup and shown or hidden thereafter.** It is never re-created per invocation; this is the single biggest latency factor.
- **Size:** 400 effective px wide; the height fits its content.
- **Corners:** Windows 11 DWM rounded corners. Flat on Windows 10.
- **Backdrop:** Desktop Acrylic, which is the Windows convention for transient surfaces. Fall back to a solid theme color on Windows 10, when transparency effects are off, and in battery saver.
- **Search field:**
  - 16 px text, no field chrome, focused on show.
  - Placeholder: "Search emoji… (English or Български)".
- **Result rows:**
  - 52 px tall; emoji at 32 px in Segoe UI Emoji (color).
  - Title-case CLDR name.
  - When the match came from a keyword or synonym rather than the name, a muted line explaining it, such as "matched: affection".
- **Best match:** row 1 carries a "Best match" chip. The selected row shows the system accent and an ↵ glyph.
- **Footer:** one muted line of key hints: `↵ insert   ↑↓ select   Ctrl+↵ copy   Esc close`.
- **Motion:** on show, a 120 ms fade plus 6 px upward translate. No exit animation. Respect the system "Animation effects" setting.
- **Position** (setting):
  - **Near text caret (default):** use the caret rectangle from GetGUIThreadInfo; fall back to near cursor.
  - **Near cursor.**
  - **Centered on the active monitor.**

  Clamp the popup to the monitor's work area, and flip it above the anchor when there is no room below. Use per-monitor DPI awareness v2.
- **Theme:** System (default), Light, or Dark, using the system accent color.

### 6.2 Settings window
- Mica backdrop, standard WinUI controls, about 720×560.
- **General:** hotkey recorder, launch at startup, popup position.
- **Appearance:** theme.
- **Search:**
  - keyword languages (EN, BG)
  - preferred skin tone, shown as six visual swatches
  - max emoji version (advanced)
  - semantic mode toggle (once Stage 8 exists)
  - clear usage history
- **Insertion:**
  - default method (Auto or Clipboard)
  - per-app overrides table: process name → method → paste keys
  - clipboard restore delay
- **About:**
  - version
  - open log folder
  - "Copy diagnostics": OS build, app version, hotkey state, last 20 insertion outcomes, no user text
  - licences

### 6.3 Tray
- **Icon:** variants for light and dark taskbars. Tooltip: `QuickEmoji — Win+Shift+E`.
- **Left-click:** opens the popup centered. Insertion is disabled in this mode, because there is no target; Enter copies instead.
- **Right-click menu:** Open, Settings, Pause hotkey, Quit.
- **Explorer restarts:** recreate the icon on the `TaskbarCreated` message.

### 6.4 First run
A small welcome window:
- shows the hotkey
- has a built-in test field so the user can try the hotkey immediately
- has a launch-at-startup toggle, off by default and visible, so the user gives explicit consent

### 6.5 Accessibility
- AutomationProperties on every element. Narrator reads, for example, "Red heart, best match, 1 of 3".
- Respect high-contrast themes.
- Every function is reachable from the keyboard.

### 6.6 App icon
An original, simple icon at all sizes from 16 to 256 px, produced as SVG then ICO. No third-party emoji artwork.

---

## 7. Settings and storage

- **Locations** (under `%LOCALAPPDATA%\QuickEmoji\`):
  - `settings.json`: includes `schemaVersion`; written atomically (temp file, then File.Replace).
  - `usage.json`: frecency data.
  - `logs\`: rolling logs, 7 files of up to 1 MB each.
- **Corrupt or invalid file:** rename it to `*.bad-<timestamp>`, load defaults, and show one notification.
- **Unknown or invalid individual fields:** use the default for that field; do not reject the whole file.
- **Launch at startup:** HKCU `...\CurrentVersion\Run` value pointing to the exe with `--background`. Remove the value on uninstall.

---

## 8. Reliability

- **Single instance:** enforced with a named mutex. A second launch signals the first to open Settings, then exits.
- **Exceptions:** global handlers (AppDomain, TaskScheduler, and the WinUI UnhandledException event) log the exception and keep the tray process alive. A failure inside the popup must never take down the app.
- **Display changes:** handle DPI changes and monitors being added or removed (recompute position on every show).
- **Network:** zero network calls in default mode. No telemetry.

---

## 9. Performance budgets (measured, reported in README)

| Metric | Budget |
|---|---|
| Hotkey → popup visible and focused | ≤ 50 ms |
| Keystroke → results rendered | ≤ 16 ms (p95) |
| Search core per query | ≤ 5 ms (p95) |
| Enter → emoji in target app | ≤ 150 ms (Unicode path) |
| Cold start → tray ready | ≤ 1.5 s |
| Idle CPU | 0% (no polling, no timers while hidden) |
| Idle working set | Target ≤ 100 MB. Measure and report; optimize if exceeded. |

---

## 10. Packaging

- **Builds:** `win-x64` and `win-arm64`. Self-contained .NET and Windows App SDK, unpackaged.
- **Deliverables:**
  - per-user Inno Setup installer (no admin rights required)
  - portable zip
- **Versioning:** SemVer, set in `Directory.Build.props`.
- **Signing:** builds are unsigned, so SmartScreen will warn. Document this and how to sign later.

---

## 11. Testing

### Automated (CI-safe)
- **Core:** golden ranking, normalization, deduplication, ZWJ atomicity, skin-tone application, frecency decay, LRU cache, settings corruption recovery.
- **Platform (pure parts):** the INPUT[] builder, hotkey validation rules, ClipboardGuard against a fake clipboard, method-selection logic (elevation and overrides).
- **Benchmark:** a search latency test that asserts the p95 budget.

### Opt-in end-to-end (FlaUI, real desktop, `[Trait("Category","E2E")]`)
Excluded from the default test run. Steps:
1. Launch Notepad and type a sentence.
2. Trigger the hotkey through SendInput.
3. Type `love`, press Enter.
4. Assert through UI Automation that the text now ends in ❤️ and the original text is unchanged.
5. Assert the clipboard is unchanged.
6. Repeat for Esc and assert no change.

### Manual matrix (`docs/TESTING.md`)
- **Apps:** Notepad, Word, Chrome or Edge text area, a messaging app (Discord, Telegram, or WhatsApp desktop), VS Code, Windows Terminal, an elevated Notepad.
- **Cases:**
  - ❤️, 👍🏽, 👨‍👩‍👧‍👦, and a flag
  - Esc
  - arrows and Alt+1–3
  - clipboard preserved
  - focus-change abort (switch window during the popup)
  - hotkey conflict
  - launch at startup after a reboot
  - Explorer restart
  - 150% and 200% DPI
  - two monitors
