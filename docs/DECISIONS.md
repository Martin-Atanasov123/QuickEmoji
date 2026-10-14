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
