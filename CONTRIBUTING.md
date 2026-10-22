# Contributing

Thanks for considering a contribution to VOIDOverlay Termo, a standalone tool in the VOID Overlay family. Preserve Termo's compact transparent UI and avoid unrelated rewrites.

## Development setup

- Windows 10/11 x64.
- .NET 8 SDK or compatible newer SDK with Windows desktop targeting support.
- PowerShell and NuGet access for initial dependency restore.

From the repository root:

```powershell
dotnet restore .\VOIDOverlay\VOIDOverlay.csproj
dotnet test .\VOIDOverlay.Tests\VOIDOverlay.Tests.csproj --configuration Release
dotnet build .\VOIDOverlay\VOIDOverlay.csproj --configuration Release --no-restore
```

Use `.\scripts\build-release.ps1` to run the standard local verification sequence.

## Change guidelines

- Keep changes focused and follow existing C# and XAML patterns.
- Do not add fabricated hardware readings or silently suppress sensor failures.
- Add or update automated tests for behavior changes, especially telemetry selection, settings persistence, and monitor/DPI positioning.
- Do not commit `bin`, `obj`, test results, local settings, diagnostics, credentials, or personal IDE configuration.
- Keep the supplied application artwork and icon resources intact unless a change is explicitly requested.

## Pull requests

Describe the behavior changed, list verification commands and results, and include a screenshot only when it is a real capture of the changed application. Do not include machine-specific diagnostic logs without reviewing and redacting them.

The project is licensed under MIT; see `LICENSE`. Submit only work you have the right to contribute, and do not include third-party code without confirming its license.
