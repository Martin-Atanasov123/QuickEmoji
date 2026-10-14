# QuickEmoji

A Windows tray utility: press a global hotkey, type a word, press Enter, and the best emoji for that word appears where you were typing.

> **Status:** Stage 0 (scaffold). The app starts and exits; search, the hotkey and insertion arrive in later stages. See [docs/PLAN.md](docs/PLAN.md).

## Build from source

Requires Windows 10 1809 or later (Windows 11 recommended) and the .NET 10 SDK (`global.json` pins the version). Visual Studio is not needed.

```powershell
dotnet build QuickEmoji.sln -c Debug
dotnet test --filter "Category!=E2E"
dotnet run --project src/QuickEmoji.App
```

## Documentation
- [SPEC.md](SPEC.md): product and technical specification
- [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md): layers and dependencies
- [docs/TESTING.md](docs/TESTING.md): how to test, CI, manual checks
- [docs/DECISIONS.md](docs/DECISIONS.md): deviations from the spec and why
