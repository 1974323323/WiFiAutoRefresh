#define MyAppName "WiFi \u81ea\u52a8\u5237\u65b0\u5668"
#define MyAppShortName "WiFiAutoRefresh"
#define MyAppExeName "WiFiAutoRefresh.exe"
#define MyAppVersion "1.1.0"
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
Name: "chinesesimplified"; MessagesFile: "compiler:Languages\ChineseSimplified.isl"

; 兜底：若 ISCC 自带目录里没 ChineseSimplified.isl，Windows 编译环境也没有互联网下载时，
; 可将同名 isl 文件与本 iss 脚本放在同一目录并改成下面的相对路径：
; Name: "chinesesimplified"; MessagesFile: "ChineseSimplified.isl"

[CustomMessages]
chinesesimplified.WelcomeLabel2=\u5b89\u88c5\u5668\u5c06\u4f1a\u628a [%1] \u5b89\u88c5\u5230\u4e0b\u5217\u6587\u4ef6\u5939\u3002\r\n\u4f60\u53ef\u4ee5\u4fee\u6539\u8fd9\u4e2a\u6587\u4ef6\u5934\uff0c\u6216\u8005\u70b9\u201c\u4e0a\u4e00\u6b65\u201d\u9009\u62e9\u5176\u4ed6\u6587\u4ef6\u5934\u3002\r\n\r\n\u5e94\u7528\u7279\u70b9\uff1a\r\n\u2022 \u6bcf 5 \u79d2\u626b\u63cf\u53ef\u89c1 WiFi\uff0c\u4ee5\u4fe1\u53f7\u5f3a\u5ea6\u771f\u5b9e\u503c\u6392\u5e8f\r\n\u2022 \u5df2\u8fde\u63a5\u7f51\u7edc\u9ad8\u4eae\uff08\u7eff\u8272\uff09\u5e76\u7f6e\u9876\r\n\u2022 \u4e09\u79cd\u81ea\u52a8\u8fd0\u884c\u65b9\u5f0f\uff08\u5728\u7a0b\u5e8f\u5185\u9009\u62e9\uff09\uff1a\u6ce8\u518c\u8868 Run / \u7cfb\u7edf\u7ea7\u8ba1\u5212\u4efb\u52a1 / Windows \u670d\u52a1\r\n\u2022 \u53f3\u4fa7\u201c\u8fde\u63a5 / \u65ad\u5f00\u201d\u6309\u94ae\u5feb\u901f\u5207\u6362\u7f51\u7edc
chinesesimplified.FinishedLabel=\u5b89\u88c5\u5b8c\u6210\u3002\u70b9\u201c\u5b8c\u6210\u201d\u9000\u51fa\u5b89\u88c5\u5411\u5bfc\u3002
chinesesimplified.FinishedLabel2=\u5df2\u5c06 [%1] \u5b89\u88c5\u5230\u4f60\u7684\u7535\u8111\u3002\r\n\r\n\u70b9\u201c\u5b8c\u6210\u201d\u540e\uff0c\u4f60\u53ef\u4ee5\u542f\u52a8\u8be5\u7a0b\u5e8f\u3002\r\n\r\n\u6ce8\u610f\uff1a\u201c\u81ea\u52a8\u8fd0\u884c\u201d\u9700\u5728\u7a0b\u5e8f UI \u91cc\u624b\u52a8\u9009\u62e9\u6a21\u5f0f\u5e76\u70b9\u201c\u4e00\u952e\u542f\u7528\u201d\uff0c\u672c\u5b89\u88c5\u5668\u4e0d\u4f1a\u81ea\u52a8\u6ce8\u518c\u81ea\u542f\u3002
chinesesimplified.CreateDesktopIcon=\u521b\u5efa\u684c\u9762\u5feb\u6377\u65b9\u5f0f(&D)
chinesesimplified.LaunchAfterInstall=\u5b89\u88c5\u5b8c\u6210\u540e\u542f\u52a8 {#MyAppName}(&L)

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "\u9644\u52a0\u4efb\u52a1:"

[Files]
Source: "C:\Scripts\WiFiRefresh\WiFiAutoRefresh.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "C:\Scripts\WiFiRefresh\WiFiAutoRefresh.ico"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\WiFiAutoRefresh.ico"; Tasks: desktopicon
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\WiFiAutoRefresh.ico"

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchAfterInstall}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: filesandordirs; Name: "{app}\wifi_bg.log"

[Code]
function InitializeSetup(): Boolean;
begin
  Result := True;
end;
