; Inno Setup script for ShelfDrop on Windows.
;
; Builds a per-user installer: it needs no administrator rights, installs under %LocalAppData%\Programs,
; registers itself in Settings -> Apps (so it can be uninstalled there) and leaves nothing behind when removed.
;
; Build:  iscc /DAppVersion=0.1.0 /DSourceDir=..\publish ShelfDrop.iss
; The app's own "Uninstall ShelfDrop..." menu item runs the uninstaller this script produces (unins000.exe).

#ifndef AppVersion
  #define AppVersion "0.1.0"
#endif
#ifndef SourceDir
  #define SourceDir "..\publish"
#endif

#define AppName "ShelfDrop"
#define AppExe "ShelfDrop.exe"

[Setup]
; Identifies this program to Windows across versions. Never change it.
AppId={{6B2D6E52-1D0C-4F2A-9B53-3C1F0A7D8E64}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher=Dat98it
AppPublisherURL=https://github.com/Dat98it/ShelfDrop
AppSupportURL=https://github.com/Dat98it/ShelfDrop/issues
AppUpdatesURL=https://github.com/Dat98it/ShelfDrop/releases
VersionInfoVersion={#AppVersion}
DefaultDirName={autopf}\{#AppName}
DisableProgramGroupPage=yes
DisableDirPage=auto
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763
CloseApplications=yes
RestartApplications=no
OutputDir=..\dist
OutputBaseFilename=ShelfDrop-Setup-{#AppVersion}
SetupIconFile=..\src\ShelfDrop.App\Assets\ShelfDrop.ico
UninstallDisplayIcon={app}\{#AppExe}
UninstallDisplayName={#AppName}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
LicenseFile=..\..\LICENSE

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "startup"; Description: "Start ShelfDrop when I sign in to Windows"; Flags: unchecked

[Files]
Source: "{#SourceDir}\{#AppExe}"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\..\LICENSE"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExe}"

[Registry]
; "Launch at Login" as the app itself writes it (see RegistryLoginItemService).
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "{#AppName}"; \
  ValueData: """{app}\{#AppExe}"""; Flags: uninsdeletevalue; Tasks: startup

[Run]
Filename: "{app}\{#AppExe}"; Description: "Start {#AppName}"; Flags: nowait postinstall skipifsilent

[UninstallRun]
; The app lives in the tray with no window, so nothing asks it to close: stop it before its file is removed.
Filename: "{sys}\taskkill.exe"; Parameters: "/F /IM {#AppExe}"; Flags: runhidden skipifdoesntexist; RunOnceId: "StopShelfDrop"

[UninstallDelete]
; What the app keeps outside its own folder: its settings, its log and the temporary copies it makes.
Type: filesandordirs; Name: "{userappdata}\{#AppName}"
Type: filesandordirs; Name: "{localappdata}\{#AppName}"
Type: filesandordirs; Name: "{%TEMP}\{#AppName}"

[Code]
function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  ResultCode: Integer;
begin
  { A copy that is still running keeps its file open and would block an update. It holds nothing unsaved. }
  Exec(ExpandConstant('{sys}\taskkill.exe'), '/F /IM {#AppExe}', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Result := '';
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usPostUninstall then
  begin
    { Launch at Login may have been switched on from the app's menu rather than by this installer, so remove it either way,
      together with the on/off switch Windows keeps for it. }
    RegDeleteValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', '{#AppName}');
    RegDeleteValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run', '{#AppName}');
  end;
end;
