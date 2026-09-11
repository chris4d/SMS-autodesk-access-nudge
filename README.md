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

## Files

- `src/SmsNudge/AccessStateReader.cs` — SQLite read + `ComputeAvailableUpdates` logic
- `src/SmsNudge/UpdateMonitor.cs`      — polling, filtering, dedupe, toast trigger
- `src/SmsNudge/AccessTrayIcon.cs`     — tray UI
- `src/SmsNudge/ToastNotifier.cs`      — Windows toast plumbing
- `tests/SmsNudge.Tests/`              — detection-rule unit tests

## Notes / limitations

- Relies on the undocumented `Bundle.updateversion` column. If an Autodesk
  Access update changes the schema, detection may stop working — check
  `%LOCALAPPDATA%\SMS-autodesk-access-nudge\nudge.log` for read errors.
- Only products whose manifests arrive through ODIS (the installer pipeline
  behind Access) are covered — the same set Access advertises updates for.
