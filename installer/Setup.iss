; Per-user (non-elevated) Inno Setup 6 script for Autodesk Access Nudge.
; Define AppVersion when invoking, e.g.: ISCC /DAppVersion=1.0.0 Setup.iss

#define AppName "Autodesk Access Nudge"
#define AppId "{1F4B02D2-C37C-4A44-8A8A-C64C0E844512}"
#define ExePath "..\src\SmsNudge\publish\SmsNudge.exe"

#ifndef OutputDir
#define OutputDir "output"
#endif

[Setup]
AppId={{1F4B02D2-C37C-4A44-8A8A-C64C0E844512}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher=Scott Mitchell Studio
DefaultDirName={localappdata}\{#AppName}
DisableProgramGroupPage=yes
AllowNoIcons=yes
PrivilegesRequired=lowest
RestartApplications=no
Compression=lzma2/max
SolidCompression=yes
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir={#OutputDir}
OutputBaseFilename=Setup-SmsNudge-v{#AppVersion}
UninstallDisplayName={#AppName}
UninstallDisplayIcon={app}\SmsNudge.exe

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Files]
Source: "{#ExePath}"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\SmsNudge.exe"

[Run]
Filename: "{app}\SmsNudge.exe"; Parameters: "--tray"; Flags: postinstall skipifsilent nowait; Description: "{cm:LaunchProgram,{#AppName}}"
Filename: "{app}\SmsNudge.exe"; Parameters: "--autostart"; Flags: runhidden

[UninstallRun]
Filename: "{app}\SmsNudge.exe"; Parameters: "--shutdown"; Flags: runhidden
Filename: "{app}\SmsNudge.exe"; Parameters: "--remove-autostart"; Flags: runhidden

[UninstallDelete]
Type: filesandordirs; Name: "{localappdata}\SMS-autodesk-access-nudge"
