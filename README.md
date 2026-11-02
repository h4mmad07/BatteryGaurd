# Battery Guard

Battery Guard is a small Windows tray app that reminds you when to unplug or plug in your laptop. It monitors battery percentage and power state; it does not control charging.

## Features

- Tray icon shows the current battery level and whether the device is charging.
- Alerts when the battery reaches the upper threshold while plugged in, or the lower threshold while on battery.
- Alerts clear when the power condition changes. Press **Esc** or **Space** to snooze an alert.
- Configure charge thresholds, snooze duration, alert sounds, and Windows startup.
- Pause alerts for one hour from the tray menu or battery dashboard.
- Battery dashboard shows health and hardware details when Windows and the device provide them.
- Handles desktop PCs without a system battery without showing battery alerts.

Default thresholds are 80% to unplug and 20% to plug in.

## Important limitation

Battery Guard only provides reminders. Windows does not give this app a standard way to stop charging. If your laptop supports a charge limit, use its manufacturer utility or firmware setting for automatic charge control.

## Install

Download the latest **BatteryGuard-Setup** file from the [Releases](../../releases) page and run it. The installer is for 64-bit Windows and includes the .NET runtime, so the .NET SDK or runtime does not need to be installed separately.

The installer installs for the current Windows user. Its startup option is selected by default, so Battery Guard starts when that user signs in. The installer creates a Start Menu shortcut, offers a desktop shortcut, and registers an uninstaller. Uninstalling removes the startup entry and installed app files; settings under `%AppData%\BatteryGuard` are retained.

## Build locally

Requirements: .NET 8 SDK, Inno Setup 6 or 7, and access to NuGet.org to restore packages.

From the repository root, run:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\installer\build-installer.ps1
```

The script accepts an optional stable version (for example `-Version 1.2.0`) and writes the installer to `artifacts\BatteryGuard-Setup-1.2.0-win-x64.exe`.

To run the app from source:

```powershell
dotnet run --project .\BatteryGuard.csproj
```

## GitHub Releases

Pushing a version tag such as `v1.0.0` starts the workflow in `.github/workflows/release.yml`. It builds the self-contained app and publishes the setup executable as an asset on that GitHub Release. Installer binaries are intentionally not committed to the repository.

## License

No license has been added yet. The repository is public for viewing, but reuse and redistribution are not granted unless a license is added.
