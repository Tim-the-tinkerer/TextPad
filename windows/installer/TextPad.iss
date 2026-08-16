; TextPad Windows installer (Inno Setup 6) — 64-bit only
#ifndef MyAppVersion
  #define MyAppVersion "1.5.5"
#endif
#ifndef MyAppSource
  #define MyAppSource "..\dist\x64-installer"
#endif
#ifndef MyAppId
  #define MyAppId "8F4E2B91-6C3D-4A7E-9F12-3D5B8C1E0A43"
#endif

#define MyAppName "TextPad"
#define MyAppPublisher "TextPad"
#define MyAppExeName "TextPad.exe"

[Setup]
AppId={{{#MyAppId}}}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
AllowNoIcons=yes
OutputDir=..\dist
OutputBaseFilename=TextPad-{#MyAppVersion}-win-x64-Setup
SetupIconFile=..\TextPad\Assets\AppIcon.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
DisableProgramGroupPage=yes
MinVersion=10.0
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "fileassoc"; Description: "Add TextPad to the ""Open with"" menu for common text and code files"; GroupDescription: "Optional:"; Flags: unchecked

[Files]
Source: "{#MyAppSource}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Excludes: "*.pdb"

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Registry]
Root: HKCR; Subkey: "TextPad.Document"; ValueType: string; ValueName: ""; ValueData: "TextPad Document"; Flags: uninsdeletekey; Tasks: fileassoc
Root: HKCR; Subkey: "TextPad.Document\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: "{app}\{#MyAppExeName},0"; Tasks: fileassoc
Root: HKCR; Subkey: "TextPad.Document\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExeName}"" ""%1"""; Tasks: fileassoc
Root: HKCR; Subkey: ".txt\OpenWithProgids"; ValueType: string; ValueName: "TextPad.Document"; ValueData: ""; Flags: uninsdeletevalue; Tasks: fileassoc
Root: HKCR; Subkey: ".rtf\OpenWithProgids"; ValueType: string; ValueName: "TextPad.Document"; ValueData: ""; Flags: uninsdeletevalue; Tasks: fileassoc
Root: HKCR; Subkey: ".md\OpenWithProgids"; ValueType: string; ValueName: "TextPad.Document"; ValueData: ""; Flags: uninsdeletevalue; Tasks: fileassoc
Root: HKCR; Subkey: ".json\OpenWithProgids"; ValueType: string; ValueName: "TextPad.Document"; ValueData: ""; Flags: uninsdeletevalue; Tasks: fileassoc
Root: HKCR; Subkey: ".xml\OpenWithProgids"; ValueType: string; ValueName: "TextPad.Document"; ValueData: ""; Flags: uninsdeletevalue; Tasks: fileassoc
Root: HKCR; Subkey: ".csv\OpenWithProgids"; ValueType: string; ValueName: "TextPad.Document"; ValueData: ""; Flags: uninsdeletevalue; Tasks: fileassoc
Root: HKCR; Subkey: ".log\OpenWithProgids"; ValueType: string; ValueName: "TextPad.Document"; ValueData: ""; Flags: uninsdeletevalue; Tasks: fileassoc
Root: HKCR; Subkey: ".ini\OpenWithProgids"; ValueType: string; ValueName: "TextPad.Document"; ValueData: ""; Flags: uninsdeletevalue; Tasks: fileassoc
Root: HKCR; Subkey: ".cfg\OpenWithProgids"; ValueType: string; ValueName: "TextPad.Document"; ValueData: ""; Flags: uninsdeletevalue; Tasks: fileassoc
Root: HKCR; Subkey: ".yaml\OpenWithProgids"; ValueType: string; ValueName: "TextPad.Document"; ValueData: ""; Flags: uninsdeletevalue; Tasks: fileassoc
Root: HKCR; Subkey: ".yml\OpenWithProgids"; ValueType: string; ValueName: "TextPad.Document"; ValueData: ""; Flags: uninsdeletevalue; Tasks: fileassoc
Root: HKCR; Subkey: ".cs\OpenWithProgids"; ValueType: string; ValueName: "TextPad.Document"; ValueData: ""; Flags: uninsdeletevalue; Tasks: fileassoc
Root: HKCR; Subkey: ".cpp\OpenWithProgids"; ValueType: string; ValueName: "TextPad.Document"; ValueData: ""; Flags: uninsdeletevalue; Tasks: fileassoc
Root: HKCR; Subkey: ".h\OpenWithProgids"; ValueType: string; ValueName: "TextPad.Document"; ValueData: ""; Flags: uninsdeletevalue; Tasks: fileassoc
Root: HKCR; Subkey: ".py\OpenWithProgids"; ValueType: string; ValueName: "TextPad.Document"; ValueData: ""; Flags: uninsdeletevalue; Tasks: fileassoc
Root: HKCR; Subkey: ".js\OpenWithProgids"; ValueType: string; ValueName: "TextPad.Document"; ValueData: ""; Flags: uninsdeletevalue; Tasks: fileassoc
Root: HKCR; Subkey: ".ts\OpenWithProgids"; ValueType: string; ValueName: "TextPad.Document"; ValueData: ""; Flags: uninsdeletevalue; Tasks: fileassoc
Root: HKCR; Subkey: ".html\OpenWithProgids"; ValueType: string; ValueName: "TextPad.Document"; ValueData: ""; Flags: uninsdeletevalue; Tasks: fileassoc
Root: HKCR; Subkey: ".htm\OpenWithProgids"; ValueType: string; ValueName: "TextPad.Document"; ValueData: ""; Flags: uninsdeletevalue; Tasks: fileassoc
Root: HKCR; Subkey: ".css\OpenWithProgids"; ValueType: string; ValueName: "TextPad.Document"; ValueData: ""; Flags: uninsdeletevalue; Tasks: fileassoc
Root: HKCR; Subkey: ".sql\OpenWithProgids"; ValueType: string; ValueName: "TextPad.Document"; ValueData: ""; Flags: uninsdeletevalue; Tasks: fileassoc
Root: HKCR; Subkey: ".sh\OpenWithProgids"; ValueType: string; ValueName: "TextPad.Document"; ValueData: ""; Flags: uninsdeletevalue; Tasks: fileassoc
Root: HKCR; Subkey: ".bat\OpenWithProgids"; ValueType: string; ValueName: "TextPad.Document"; ValueData: ""; Flags: uninsdeletevalue; Tasks: fileassoc
Root: HKCR; Subkey: ".ps1\OpenWithProgids"; ValueType: string; ValueName: "TextPad.Document"; ValueData: ""; Flags: uninsdeletevalue; Tasks: fileassoc

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent
