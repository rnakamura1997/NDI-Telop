[Setup]
AppName=NdiTelop
AppVersion={#MyAppVersion}
AppPublisher=NakamuraService
AppPublisherURL=https://github.com/rnakamura1997/NDI-Telop
DefaultDirName={autopf}\NdiTelop
DefaultGroupName=NdiTelop
OutputBaseFilename=NdiTelop-Setup-v{#MyAppVersion}
Compression=lzma
SolidCompression=yes
ArchitecturesInstallIn64BitMode=x64
SetupIconFile=src\NdiTelop\Assets\icon.ico

[Languages]
Name: "japanese"; MessagesFile: "compiler:Languages\Japanese.isl"

[Tasks]
Name: "desktopicon"; Description: "デスクトップにアイコンを作成"; GroupDescription: "追加アイコン:"; Flags: unchecked

[Files]
Source: "..\publish\win-x64\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\NdiTelop"; Filename: "{app}\NdiTelop.exe"
Name: "{group}\NdiTelop のアンインストール"; Filename: "{uninstallexe}"
Name: "{commondesktop}\NdiTelop"; Filename: "{app}\NdiTelop.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\NdiTelop.exe"; Description: "{cm:LaunchProgram,NdiTelop}"; Flags: nowait postinstall skipifsilent
