# VOID Overlay

VOID Overlay is a transparent, always-on-top Windows desktop overlay for live CPU and GPU telemetry, with compact temperature history graphs and system-tray controls.

> There is no application screenshot in this source tree. Hardware behavior varies by system; read [known limitations](#hardware-and-sensor-limitations) before relying on a particular reading.

## Features

- CPU and GPU temperature readings and compact history graphs.
- CPU/GPU load and clock reporting when the hardware provider exposes those values.
- RAM/VRAM usage where available.
- Transparent, topmost overlay with lock and click-through options.
- Four corner presets, independent X/Y offsets, and full-monitor or work-area reference.
- Configurable sampling interval (250–5000 ms).
- Settings persisted under `%LOCALAPPDATA%\VOIDOverlay\settings.json`.
- Diagnostics mode for sensor discovery, selection, readings, and positioning.
- Application, window, and tray icons based on `VOIDOverlay/assets/void-overlay-logo.png`.

## Supported platform and requirements

- Windows 10 or Windows 11, x64.
- .NET 8 Windows Desktop Runtime for the framework-dependent published build.
- Administrator approval at launch. The application manifest requests elevation because the hardware-monitor driver may require it to read CPU sensors.
- Internet access during the first restore/build to retrieve NuGet dependencies.

The project targets `net8.0-windows`, uses WPF and Windows Forms, and has been built with the .NET 10 SDK against the .NET 8 target. Runtime compatibility has not been verified on every Windows version or hardware configuration.

## Install and launch

There is not yet a public installer or release download. To run from source, clone or download the project, open PowerShell in the repository root, and run:

```powershell
dotnet run --project .\VOIDOverlay\VOIDOverlay.csproj
```

Approve the Windows elevation prompt. The overlay runs in the notification area; closing or hiding the small overlay does not exit the process.

### Build from source

Install the .NET 8 SDK or a compatible newer SDK, then run:

```powershell
dotnet restore .\VOIDOverlay\VOIDOverlay.csproj
dotnet test .\VOIDOverlay.Tests\VOIDOverlay.Tests.csproj --configuration Release
dotnet build .\VOIDOverlay\VOIDOverlay.csproj --configuration Release --no-restore
```

The release helper script runs the same restore, test, and build sequence:

```powershell
.\scripts\build-release.ps1
```

### Publish for Windows x64

To create a framework-dependent Windows x64 publish folder:

```powershell
.\scripts\publish-windows-x64.ps1
```

The output is written to `artifacts\publish\win-x64\`. Users of this output need the .NET 8 Windows Desktop Runtime installed. The executable requests administrator approval when launched. This is not an installer and does not create a signed package.

## Use and configure

The tray icon menu provides:

- **Show/Hide Overlay** (double-clicking the tray icon also toggles visibility).
- **Overlay Position** with TopLeft, TopRight, BottomLeft, and BottomRight presets.
- **Lock/Unlock**, **Settings**, **Start with Windows**, and **Exit**.

Settings provides sampling interval, lock/click-through/startup preferences, and position fine-tuning. Horizontal X and vertical Y offsets are specified in WPF device-independent units (DIPs), default to zero, and accept values from -200 to 1000. Negative offsets intentionally move beyond the selected edge; large positive offsets may move the overlay beyond the opposite edge. Edge reference can be the full monitor bounds or the taskbar-excluding work area. Existing settings using the former shared edge margin are migrated to matching initial X and Y offsets.

Position is calculated relative to the monitor currently containing the overlay, with the primary monitor used if no window monitor is available. Per-window DPI is used for conversion between physical monitor bounds and WPF DIPs.

## Hardware monitoring

LibreHardwareMonitor is used to discover sensors and refresh readings in the background. CPU selection prefers a valid package sensor, then a die/Tctl/Tdie reading, then a core aggregate, then a core sensor. GPU selection targets the GPU core temperature. The application does not invent values; unavailable or invalid measurements display as `N/A`, and graph history records gaps as missing samples.

Sensor availability depends on CPU/GPU model, firmware, driver support, and elevation. Some systems do not expose CPU temperatures to the provider, and the presence of a GPU temperature does not imply that CPU temperature is supported. Load, clock, memory, and VRAM values may also be unavailable on some hardware.

### Diagnostics

Launch with `--diagnostics` to write detailed telemetry and position logs to:

```text
%LOCALAPPDATA%\VOIDOverlay\diagnostics.log
```

Logs can disclose hardware names, sensor identifiers/readings, and local timestamps. Review and redact diagnostic output before sharing it publicly.

## Troubleshooting

- **CPU/GPU shows N/A:** confirm the device and driver are supported, approve the elevation prompt, and review diagnostics for discovered hardware, sensor candidates, and update failures.
- **Overlay is hidden:** double-click the tray icon or use its Show/Hide menu.
- **Position is unexpected:** check the selected corner, X/Y offsets, and Full Screen/Work Area edge reference. Negative offsets and large positive values may place part of the overlay outside the screen by design.
- **Settings do not persist:** confirm the current Windows user can write to `%LOCALAPPDATA%\VOIDOverlay`.
- **Build fails:** verify a compatible .NET SDK is installed and rerun `dotnet restore`.

## Tests

Run all automated tests with:

```powershell
dotnet test .\VOIDOverlay.Tests\VOIDOverlay.Tests.csproj --configuration Release
```

The test project covers CPU sensor selection, telemetry/view-model updates, position calculations, settings persistence/migration, and settings-window sizing. Automated tests do not replace physical validation on different CPUs, GPUs, monitors, and DPI configurations.
The release-preparation test/build run is automated; this audit did not perform a new physical CPU/GPU telemetry or multi-monitor DPI test.

## Dependencies and licensing

Direct NuGet dependencies include CommunityToolkit.Mvvm and LibreHardwareMonitorLib. See [DEPENDENCIES.md](./DEPENDENCIES.md) for the resolved dependency list and license review notes.

The project code is licensed under the MIT License; see [LICENSE](./LICENSE). Third-party packages retain their own terms. Review [THIRD-PARTY-NOTICES.md](./THIRD-PARTY-NOTICES.md) and [DEPENDENCIES.md](./DEPENDENCIES.md) before distributing a release.

## Contributing

See [CONTRIBUTING.md](./CONTRIBUTING.md) for local build, test, and contribution guidance.

## Security

Do not post unredacted diagnostics publicly. A private vulnerability-reporting contact has not yet been configured; establish one before enabling public vulnerability reports for a GitHub repository.
