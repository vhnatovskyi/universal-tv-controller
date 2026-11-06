[Setup]
AppId=app.braviaremote.controller
AppName=BRAVIA Remote
AppVersion={#AppVersion}
AppPublisher=BRAVIA Remote
AppPublisherURL=https://github.com/vhnatovskyi/tv-controller
AppMutex=Local\app.braviaremote.controller
DefaultDirName={localappdata}\Programs\BraviaRemote
DefaultGroupName=BRAVIA Remote
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763
UninstallDisplayIcon={app}\BraviaRemote.exe
OutputDir={#OutputDirectory}
OutputBaseFilename=BraviaRemote-Setup-{#AppVersion}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
LanguageDetectionMethod=none

[Languages]
Name: "en"; MessagesFile: "compiler:Default.isl"
Name: "uk"; MessagesFile: "compiler:Languages\Ukrainian.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; Flags: unchecked

[Files]
Source: "{#PayloadDirectory}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\BRAVIA Remote"; Filename: "{app}\BraviaRemote.exe"; WorkingDir: "{app}"
Name: "{autodesktop}\BRAVIA Remote"; Filename: "{app}\BraviaRemote.exe"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\BraviaRemote.exe"; Description: "{cm:LaunchProgram,BRAVIA Remote}"; Flags: nowait postinstall skipifsilent
