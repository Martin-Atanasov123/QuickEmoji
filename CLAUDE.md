# CLAUDE.md — QuickEmoji

Windows tray utility: global hotkey → search a word → Enter inserts the best emoji into the app that was focused.
The full spec is in `SPEC.md`. Read it before any non-trivial change. Record deviations from it in `docs/DECISIONS.md` as date / decision / reason.

## Environment
- Develop on Windows 11 only. The WinUI 3 app cannot build or run anywhere else. If `dotnet --info` reports a non-Windows OS, stop and say so.
- Use .NET (current LTS) and the Windows App SDK (latest stable), unpackaged, with `WindowsAppSDKSelfContained=true`.
- **Do not trust memory for package versions or APIs.** Check the current NuGet versions and official docs for Windows App SDK, H.NotifyIcon.WinUI, Microsoft.Windows.CsWin32, CommunityToolkit.Mvvm, FlaUI, and Microsoft.ML.OnnxRuntime before you use them. Pin exact versions in `Directory.Packages.props` (central package management).

## Solution layout and dependency rules
```
src/QuickEmoji.Core        net*        Pure logic: emoji model, index, search, ranking, frecency, settings model/persistence.
                                       NO Windows, WinUI, or P/Invoke references. File system access goes through an abstraction.
src/QuickEmoji.Platform    net*-windows Win32 only: hotkey, foreground/target capture, SendInput injector, ClipboardGuard,
                                       startup (Run key), caret locator, integrity check. NO UI types.
src/QuickEmoji.App         WinUI 3     Composition root, windows, view models, tray, toast. Thin: it calls Core and Platform.
tools/QuickEmoji.DataBuilder console   Downloads pinned emoji sources and writes src/QuickEmoji.Core/Data/emoji.json (committed).
tests/QuickEmoji.Core.Tests          xUnit
tests/QuickEmoji.Platform.Tests      xUnit (pure parts only, behind interfaces)
tests/QuickEmoji.DataBuilder.Tests   xUnit (parsers, grouping, merge, writer)
tests/QuickEmoji.E2E                 xUnit + FlaUI, [Trait("Category","E2E")], excluded from default runs
installer/                           Inno Setup script
docs/                                TESTING.md, DECISIONS.md, ARCHITECTURE.md
```
Dependencies point one way only: App → Platform → Core, and App → Core. Core references nothing project-local.

## Commands
- Build: `dotnet build QuickEmoji.sln -c Debug`
- Test: `dotnet test --filter "Category!=E2E"`
- E2E (interactive desktop only, when asked): `dotnet test --project tests/QuickEmoji.E2E` (build the solution first)
- Run: `dotnet run --project src/QuickEmoji.App`
- Regenerate data: `dotnet run --project tools/QuickEmoji.DataBuilder` (add `-- --offline` to use only the download cache)
- Publish: `dotnet publish src/QuickEmoji.App -c Release -r win-x64 --self-contained` (and `win-arm64`)

Keep these commands accurate. If they change, update this file in the same change.

## Code rules
- Enable nullable reference types. Treat warnings as errors. Use file-scoped namespaces. Keep classes small, with one responsibility each.
- Use CsWin32 (`NativeMethods.txt`) for Win32 interop. Write a manual `DllImport` only when CsWin32 cannot generate the API, and add a comment saying why.
- Never block the UI thread. Do waits (focus polling, key release, clipboard restore delay) with `await Task.Delay`, never `Thread.Sleep`. Marshal Win32 callbacks to the UI thread with `DispatcherQueue`.
- Only `ClipboardGuard` may read or write the clipboard. Only `TextInjector` may call SendInput, apart from the E2E test driver.
- Keep ranking constants in `RankingWeights` only. Do not hard-code scores elsewhere.
- Use structured logging with Serilog to `%LOCALAPPDATA%\QuickEmoji\logs`. Log insertion method, process name, outcome, and timings. Never log what the user typed in other apps.

## Hard constraints
- No low-level keyboard or mouse hooks (`WH_KEYBOARD_LL`, `WH_MOUSE_LL`).
- No network calls at runtime, except the opt-in semantic-model download (SPEC §3.7). DataBuilder may use the network; the app may not.
- No TODOs, placeholders, stubs, or "left as an exercise" in delivered code. If something cannot be finished, say so explicitly in the stage report and keep it out of the code path.
- Never weaken a golden ranking expectation to make a test pass. Improve ranking or data instead. If an expectation is genuinely wrong, change it and record why in DECISIONS.md.
- Never split, normalize, or strip ZWJ sequences, VS16, skin-tone modifiers, or flag tag sequences.
- Never inject when the foreground check fails. Fall back to copy and show a toast (SPEC §4.6).

## Gotchas
- SendInput into an elevated process from a non-elevated one fails **silently**: the return value and GetLastError both look successful. Detect elevation by checking the target process's integrity level beforehand.
- The clipboard paste fallback also uses SendInput (Ctrl+V), so UIPI blocks it in the same way. Elevated targets get copy-only.
- The process may take the foreground only while handling WM_HOTKEY. Activate the popup inside that handler.
- Create the popup window once and show or hide it. Re-creating it on every invocation breaks the 50 ms budget.
- WinUI 3 has no tray API. Use H.NotifyIcon.WinUI and handle `TaskbarCreated` so the icon comes back after Explorer restarts.
- Emoji newer than the OS font renders as an empty box. Respect `MaxEmojiVersion` (SPEC §3.2).
- Wait for modifier and Enter keys to be released before injecting. Otherwise the target receives Ctrl or Shift combined with the emoji input.
- Ctrl+Alt hotkeys act as AltGr on many keyboard layouts. Warn the user in the hotkey recorder.

## Definition of done (every stage, every change)
1. `dotnet build` is clean, with zero warnings.
2. `dotnet test --filter "Category!=E2E"` passes. Paste the summary line into your report.
3. New logic has tests. A bug fix includes a regression test.
4. Docs are updated: README, TESTING.md, DECISIONS.md, and this file if the commands changed.
5. The report states what changed, how to verify it manually, known limitations, and any deviations from the spec.
