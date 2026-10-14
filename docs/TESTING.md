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
- `tests/QuickEmoji.Core.Tests`: pure logic.
- `tests/QuickEmoji.Platform.Tests`: the pure parts of the Win32 layer, behind interfaces.
- `tests/QuickEmoji.E2E`: drives the built `QuickEmoji.exe` (and, from Stage 4, Notepad through FlaUI). Every test is `[Trait("Category","E2E")]`.

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

The full manual matrix from SPEC §11 is added in Stage 3 and Stage 4.
