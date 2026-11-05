#ifndef PayloadDir
  #error Specify PayloadDir using scripts/build-setup.ps1
#endif
#ifndef AppVersion
  #error AppVersion must come from the published FromZero executable
#endif

[Setup]
AppId={{26FF7663-126B-412D-B2F3-91C2787845F6}
AppName=FromZero
AppVersion={#AppVersion}
AppPublisher=FromZero
AppPublisherURL=https://github.com/Gercekkriptocu/fromzero
AppUpdatesURL=https://github.com/Gercekkriptocu/fromzero/releases/latest
DefaultDirName={autopf}\FromZero
DefaultGroupName=FromZero
DisableProgramGroupPage=yes
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.19044
ShowLanguageDialog=yes
SetupIconFile=..\src\FromZero\Assets\FromZero.ico
WizardSmallImageFile=..\src\FromZero\Assets\FromZero.png
WizardStyle=modern
UninstallDisplayIcon={app}\App\FromZero.exe
OutputDir={#SetupOutput}
OutputBaseFilename=FromZero-Setup-win-x64
Compression=lzma2
SolidCompression=yes
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "en"; MessagesFile: "compiler:Default.isl"
Name: "tr"; MessagesFile: "compiler:Languages\Turkish.isl"

[CustomMessages]
en.LaunchFromZero=Launch FromZero
tr.LaunchFromZero=FromZero uygulamasını aç

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; Flags: unchecked

[Files]
Source: "{#PayloadDir}\*"; DestDir: "{app}\App"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "language-en.txt"; DestDir: "{app}"; DestName: "install-language.txt"; Languages: en; Flags: ignoreversion
Source: "language-tr.txt"; DestDir: "{app}"; DestName: "install-language.txt"; Languages: tr; Flags: ignoreversion

[Icons]
Name: "{group}\FromZero"; Filename: "{app}\App\FromZero.exe"
Name: "{autodesktop}\FromZero"; Filename: "{app}\App\FromZero.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\App\FromZero.exe"; Description: "{cm:LaunchFromZero}"; Flags: nowait postinstall runascurrentuser skipifsilent unchecked
