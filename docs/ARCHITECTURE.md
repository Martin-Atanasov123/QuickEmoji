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

## Startup (Stage 0)
`Program.Main` parses the command line, configures Serilog (`%LOCALAPPDATA%\QuickEmoji\logs`, 7 × 1 MB), then starts WinUI with a `DispatcherQueueSynchronizationContext`. `QuickEmojiApplication.OnLaunched` creates the window. `--startup-check` exits as soon as initialisation finishes.
