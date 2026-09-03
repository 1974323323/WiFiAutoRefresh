#define MyAppName "WiFi Auto Refresh"
#define MyAppShortName "WiFiAutoRefresh"
#define MyAppExeName "WiFiAutoRefresh.exe"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "1974323323"
#define MyAppURL "https://github.com/1974323323/WiFiAutoRefresh"
#define MyAppCopyright "MIT License"

[Setup]
AppId={{A8F4D6E1-7C3B-4F2E-9A1D-5B6C7D8E9F0A}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}/issues
AppUpdatesURL={#MyAppURL}/releases
DefaultDirName={autopf}\{#MyAppShortName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
LicenseFile=
InfoBeforeFile=
InfoAfterFile=
OutputDir=C:\Users\Administrator\.qclaw\workspace-agent-9c607f74\dist
OutputBaseFilename=WiFiAutoRefresh_Setup_{#MyAppVersion}
SetupIconFile=C:\Scripts\WiFiRefresh\WiFiAutoRefresh.ico
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
PrivilegesRequiredOverridesAllowed=dialog
ArchitecturesInstallIn64BitMode=x64compatible
UninstallDisplayIcon={app}\{#MyAppExeName}
UninstallDisplayName={#MyAppName}
VersionInfoVersion={#MyAppVersion}.0
VersionInfoCompany={#MyAppPublisher}
VersionInfoCopyright={#MyAppCopyright}
VersionInfoDescription=WiFi Auto Refresh Installer
MinVersion=10.0

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[CustomMessages]
CreateDesktopIcon=Create a &desktop shortcut
LaunchAfterInstall=Launch {#MyAppName} after installation

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "Additional shortcuts:"

[Files]
Source: "C:\Scripts\WiFiRefresh\WiFiAutoRefresh.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "C:\Scripts\WiFiRefresh\WiFiAutoRefresh.ico"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\WiFiAutoRefresh.ico"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchAfterInstall}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: filesandordirs; Name: "{app}\wifi_bg.log"

[Code]
// Ask user at end of install which autostart mode to use.
function InitializeSetup(): Boolean;
begin
  Result := True;
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  ResultCode: Integer;
begin
  if CurStep = ssPostInstall then
  begin
    // No-op; user selects inside the app itself.
  end;
end;
