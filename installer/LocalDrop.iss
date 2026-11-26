#define MyAppName "HADOC LocalDrop"
#define MyAppVersion "1.0.4"
#define MyAppPublisher "KyZer_HADOC"
#define MyAppExeName "HADOC.LocalDrop.exe"
#define PublishDir "..\src\HADOC.LocalDrop\bin\Release\net8.0-windows\win-x64\publish"

[Setup]
AppId={{B761D5D9-70CA-4B81-9D35-4B5B3E0A4D27}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\HADOC LocalDrop
DefaultGroupName={#MyAppName}
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64
MinVersion=10.0.17763
OutputDir=..\artifacts
OutputBaseFilename=HADOC-LocalDrop-Setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
UninstallDisplayIcon={app}\{#MyAppExeName}
SetupIconFile=..\src\HADOC.LocalDrop\Assets\HADOC-LocalDrop.ico

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: unchecked

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\HADOC LocalDrop"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\HADOC LocalDrop"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch HADOC LocalDrop"; Flags: postinstall nowait skipifsilent
