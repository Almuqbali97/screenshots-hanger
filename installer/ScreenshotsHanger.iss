#define AppVersion "1.0.2"
[Setup]
AppId={{7ED5E904-61A8-4AE1-9574-DF9787F6318B}
AppName=Screenshots Hanger
AppVersion={#AppVersion}
AppPublisher=Screenshots Hanger
DefaultDirName={localappdata}\Programs\Screenshots Hanger
DefaultGroupName=Screenshots Hanger
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.19041
OutputDir=..\artifacts
OutputBaseFilename=ScreenshotsHanger-Setup-{#AppVersion}-x64
SetupIconFile=..\assets\hanger.ico
UninstallDisplayIcon={app}\ScreenshotsHanger.exe
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes
RestartApplications=no
UninstallDisplayName=Screenshots Hanger

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Shortcuts:"; Flags: unchecked
Name: "startup"; Description: "Start Screenshots Hanger when I sign in"; GroupDescription: "Preferences:"; Flags: unchecked

[Files]
Source: "..\artifacts\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\Screenshots Hanger"; Filename: "{app}\ScreenshotsHanger.exe"
Name: "{autodesktop}\Screenshots Hanger"; Filename: "{app}\ScreenshotsHanger.exe"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "ScreenshotsHanger"; ValueData: """{app}\ScreenshotsHanger.exe"" --background"; Flags: uninsdeletevalue; Tasks: startup
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueName: "ScreenshotsHanger"; Flags: uninsdeletevalue

[Run]
Filename: "{app}\ScreenshotsHanger.exe"; Description: "Open Screenshots Hanger"; Flags: nowait postinstall skipifsilent
