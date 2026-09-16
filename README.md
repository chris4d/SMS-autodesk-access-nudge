# SMS-autodesk-access-nudge

A small tray app (C#/.NET 8, WinForms) that posts a Windows toast notification
when Autodesk Access has product updates available on the local workstation.

## How it detects updates

Autodesk Access (the update manager installed at
`C:\Program Files\Autodesk\AdODIS\V1\`) keeps its install state in a SQLite
database at `%ProgramData%\Autodesk\ODIS\Install.db`. The `Bundle` table has one
row per installed product (`type = 'PRD'`) and one per installed product update
(`type = 'UPD'`). Each `PRD` row carries an `updateversion` column that reflects
the latest version Autodesk Access has seen for that product line (`constantid`
groups a product with its update bundles).

An update counts as available when:

```
PRD.updateversion > max(installed versions for the same constantid)
```

The app copies `Install.db` to a temp file (the live one is locked by the
Access service while running) and reads the copy, then toasts and remembers the
set of `(product, availableVersion)` pairs it already notified about, so the
same state doesn't re-toast every poll.

## Usage

```
SmsNudge.exe            starts the tray app
SmsNudge.exe --once     prints the current update state as JSON and exits
SmsNudge.exe --test-toast  shows a test notification
```

Tray menu: status line, Open Autodesk Access, Check now, Show test toast,
Start with Windows (HKCU Run key — no admin needed), Exit.
Left-click or double-left-click also triggers an immediate check.

## Configuration

`%LOCALAPPDATA%\SMS-autodesk-access-nudge\config.json`
(created with defaults on first run):

```json
{
  "PollIntervalMinutes": 15,
  "EnableAutoStart": true,
  "PlcFilter": []
}
```

- `PollIntervalMinutes` — how often to re-read Access's state
- `PlcFilter` — restrict notifications to certain product lines, e.g. `["RVT", "ACD"]`.
  Empty means all products Access covers.
- Product line codes seen in practice: `RVT` (Revit), `ACD` (AutoCAD),
  `DSKCON` (Desktop Connector), `ASC` (Shared Components), `CIV3D` (Civil 3D), etc.

Log and dedupe state live in the same folder (`nudge.log`, `notified-state.json`).

## Building

```
dotnet build
dotnet test
dotnet run --project src\SmsNudge -- --once
```

Requires .NET 8 SDK and Windows 10+ (toast notifications and the SQLite
bundles are Windows-and-Win32 specific); admin rights are never required.

## Packaging (suite contract)

The app is published per contract for the utility suite:

- **Conventional installer**: `Setup-SmsNudge-vX.Y.Z.exe` attached to every GitHub
  Release, built via Inno Setup 6 from `installer/Setup.iss`.
- **Silent install**: Inno flags are supported natively — `/VERYSILENT /NORESTART /SUPPRESSMSGBOXES`.
- **ARP entry**: Inno Setup registers `Autodesk Access Nudge` under
  `HKCU\Software\Microsoft\Windows\CurrentVersion\Uninstall\{AppId}`
  (per-user; **no elevation required**).
- **Idempotent**: rerunning the installer upgrades in place (same `AppId`) and
  relaunches the tray. `--shutdown` stops a running instance during upgrades/uninstalls.
- **Digest**: each release also ships `Setup-SmsNudge-vX.Y.Z.sha256`; the
  release SHA-256 is meant to be checked by the suite's staged-payload
  verification (same pattern as AnythingLLM/Revit payloads).
- **Login autostart** is owned by this utility's installer, not the suite:
  the installer registers a per-user HKCU Run key (`SMS-autodesk-access-nudge`)
  that launches the tray app at login, and the uninstaller removes it. Default
  is enabled; a silently-installed suite gets autostart automatically. To
  disable for a given install, pass Inno's standard
  `/MERGETASKS=!autostart` flag. The tray itself only launches immediately
  after interactive installs — suite-deployed machines show the tray at their
  next login.

Build locally:

```
powershell -File scripts\build-installer.ps1 -Version 0.1.0
```

Requires Inno Setup 6 (`winget install --id JRSoftware.InnoSetup`) and .NET 8 SDK.
The GitHub Actions workflow (`.github/workflows/release.yml`) builds and
publishes on `v*` tags or manual dispatch.

## Files

- `src/SmsNudge/AccessStateReader.cs` — SQLite read + `ComputeAvailableUpdates` logic
- `src/SmsNudge/UpdateMonitor.cs`      — polling, filtering, dedupe, toast trigger
- `src/SmsNudge/AccessTrayIcon.cs`     — tray UI
- `src/SmsNudge/ToastNotifier.cs`      — Windows toast plumbing
- `installer/Setup.iss`                — per-user Inno Setup script
- `scripts/build-installer.ps1`        — publish + Inno build + SHA-256
- `tests/SmsNudge.Tests/`              — detection-rule unit tests

## Notes / limitations

- Relies on the undocumented `Bundle.updateversion` column. If an Autodesk
  Access update changes the schema, detection may stop working — check
  `%LOCALAPPDATA%\SMS-autodesk-access-nudge\nudge.log` for read errors.
- Only products whose manifests arrive through ODIS (the installer pipeline
  behind Access) are covered — the same set Access advertises updates for.
