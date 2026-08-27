#ifndef MyAppVersion
  #define MyAppVersion "1.0.0"
#endif
#ifndef SourceDir
  #define SourceDir "..\artifacts\win-x64"
#endif
#ifndef OutputDir
  #define OutputDir "..\artifacts"
#endif

[Setup]
AppId={{B7E4D91A-6C2F-4A18-9E5B-8F3C1A0D7E62}}
AppName=SDM
AppVersion={#MyAppVersion}
AppVerName=SDM {#MyAppVersion}
AppPublisher=jeong-jimin-github
AppPublisherURL=https://github.com/jeong-jimin-github/SDM
AppSupportURL=https://github.com/jeong-jimin-github/SDM/issues
AppUpdatesURL=https://github.com/jeong-jimin-github/SDM/releases
DefaultDirName={autopf}\SDM
DefaultGroupName=SDM
DisableProgramGroupPage=yes
OutputDir={#OutputDir}
OutputBaseFilename=SDM-Setup-{#MyAppVersion}
SetupIconFile=..\src\SDM.App\Assets\sdm.ico
UninstallDisplayIcon={app}\SDM.exe
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "korean"; MessagesFile: "compiler:Languages\Korean.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\SDM"; Filename: "{app}\SDM.exe"
Name: "{autodesktop}\SDM"; Filename: "{app}\SDM.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\SDM.exe"; Description: "SDM 실행"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: filesandordirs; Name: "{localappdata}\SDM"

[Registry]
Root: HKCU; Subkey: "Software\Google\Chrome\NativeMessagingHosts\com.sdm.host"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Chromium\NativeMessagingHosts\com.sdm.host"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Microsoft\Edge\NativeMessagingHosts\com.sdm.host"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\BraveSoftware\Brave-Browser\NativeMessagingHosts\com.sdm.host"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Mozilla\NativeMessagingHosts\com.sdm.host"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\sdm"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: none; ValueName: "SDM"; Flags: uninsdeletevalue

[Code]
function InitializeUninstall(): Boolean;
var
  ResultCode: Integer;
begin
  { Stop both executables before [UninstallDelete] removes their files. }
  Exec(ExpandConstant('{sys}\taskkill.exe'), '/F /T /IM SDM.exe', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Exec(ExpandConstant('{sys}\taskkill.exe'), '/F /T /IM SDM.NativeHost.exe', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Result := True;
end;
