[Setup]
AppId=app.braviaremote.controller
AppName=Universal TV Controller
AppVersion={#AppVersion}
AppPublisher=Universal TV Controller
AppPublisherURL=https://github.com/vhnatovskyi/universal-tv-controller
AppMutex=Local\app.braviaremote.controller
DefaultDirName={localappdata}\Programs\BraviaRemote
DefaultGroupName=Universal TV Controller
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763
UninstallDisplayIcon={app}\UniversalTvController.exe
SetupIconFile={#PayloadDirectory}\remote.ico
OutputDir={#OutputDirectory}
OutputBaseFilename=UniversalTvController-Setup-{#AppVersion}
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
Name: "{group}\Universal TV Controller"; Filename: "{app}\UniversalTvController.exe"; WorkingDir: "{app}"
Name: "{autodesktop}\Universal TV Controller"; Filename: "{app}\UniversalTvController.exe"; WorkingDir: "{app}"; Tasks: desktopicon

[InstallDelete]
Type: files; Name: "{userprograms}\BRAVIA Remote\BRAVIA Remote.lnk"
Type: files; Name: "{autodesktop}\BRAVIA Remote.lnk"
Type: files; Name: "{app}\BraviaRemote.exe"

[Run]
Filename: "{app}\UniversalTvController.exe"; Description: "{cm:LaunchProgram,Universal TV Controller}"; Flags: nowait postinstall skipifsilent
