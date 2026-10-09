; ============================================================
;  LiteBright  —  Inno Setup 6 installer script
;  Build:  Open in Inno Setup IDE or run build-setup.ps1
; ============================================================

#define AppName      "LiteBright"
#define AppVersion   "1.2.1"
#define AppPublisher "LiteBright"
#define AppExeName   "BrightnessController.exe"
#define AppId        "{{A7B3C2D1-E4F5-4A6B-8C9D-0E1F2A3B4C5D}"
#define SrcDir       "..\bin\publish-setup"
#define IconFile     "..\public\icon.ico"

[Setup]
AppId={#AppId}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisherURL=https://github.com/SN7k/LiteBright
AppSupportURL=https://github.com/SN7k/LiteBright
AppUpdatesURL=https://github.com/SN7k/LiteBright
AppPublisher={#AppPublisher}
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
OutputDir=..\bin\installer-output
OutputBaseFilename=LiteBright-Setup-{#AppVersion}
SetupIconFile={#IconFile}
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
; Use the app icon for the installer add/remove icon
UninstallDisplayIcon={app}\{#AppExeName}
UninstallDisplayName={#AppName}

; Minimum OS: Windows 10
MinVersion=10.0

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon";   Description: "Create a &desktop shortcut";       GroupDescription: "Additional icons:"; Flags: unchecked
Name: "startupentry";  Description: "Start LiteBright when Windows starts"; GroupDescription: "Startup:";         Flags: unchecked

[Files]
; All self-contained app files (includes bundled .NET runtime)
Source: "{#SrcDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
; Start Menu shortcut
Name: "{group}\{#AppName}";           Filename: "{app}\{#AppExeName}"; IconFilename: "{app}\{#AppExeName}"
Name: "{group}\Uninstall {#AppName}"; Filename: "{uninstallexe}"

; Desktop shortcut (optional task)
Name: "{autodesktop}\{#AppName}";     Filename: "{app}\{#AppExeName}"; IconFilename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Registry]
; Optional: add to Windows startup (if user chose the task)
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; \
  ValueType: string; ValueName: "BrightnessController"; \
  ValueData: """{app}\{#AppExeName}"""; \
  Flags: uninsdeletevalue; Tasks: startupentry

[Run]
; Launch app after install (skip if silent)
Filename: "{app}\{#AppExeName}"; \
  Description: "Launch {#AppName} now"; \
  Flags: nowait postinstall skipifsilent runasoriginaluser

[UninstallRun]
; Kill the app gracefully before uninstall
Filename: "taskkill.exe"; Parameters: "/F /IM {#AppExeName}"; \
  RunOnceId: "KillApp"; Flags: runhidden skipifdoesntexist

[UninstallDelete]
; Remove settings folder on uninstall
Type: filesandordirs; \
  Name: "{userappdata}\BrightnessController"

[Code]
// Show a "Close LiteBright before continuing" warning if the app is running
function InitializeSetup(): Boolean;
begin
  Result := True;
end;
