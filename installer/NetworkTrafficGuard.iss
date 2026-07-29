#define MyAppName "Network Traffic Guard"
#define MyAppVersion "0.1.0-preview.1"
#define MyAppPublisher "MengChiang"
#define MyAppExeName "NetworkTrafficGuard.Tray.exe"
#define MyServiceName "NetworkTrafficGuard"

[Setup]
AppId={{9C2D9E37-0D2D-4B04-95EF-3B8D89D1E5F3}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\NetworkTrafficGuard
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
OutputBaseFilename=NetworkTrafficGuardSetup
Compression=lzma
SolidCompression=yes
PrivilegesRequired=admin
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64

[Files]
Source: "..\artifacts\tray\*"; DestDir: "{app}\tray"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\artifacts\service\*"; DestDir: "{app}\service"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\tray\{#MyAppExeName}"
Name: "{autostartup}\{#MyAppName}"; Filename: "{app}\tray\{#MyAppExeName}"

[Run]
Filename: "{sys}\sc.exe"; Parameters: "create {#MyServiceName} binPath= ""{app}\service\NetworkTrafficGuard.Service.exe"" start= auto DisplayName= ""{#MyAppName}"""; Flags: runhidden waituntilterminated
Filename: "{sys}\sc.exe"; Parameters: "description {#MyServiceName} ""Monitors Windows default routes and monthly network usage."""; Flags: runhidden waituntilterminated
Filename: "{sys}\sc.exe"; Parameters: "start {#MyServiceName}"; Flags: runhidden waituntilterminated
Filename: "{app}\tray\{#MyAppExeName}"; Description: "Launch {#MyAppName}"; Flags: nowait postinstall skipifsilent

[UninstallRun]
Filename: "{sys}\sc.exe"; Parameters: "stop {#MyServiceName}"; Flags: runhidden waituntilterminated
Filename: "{sys}\sc.exe"; Parameters: "delete {#MyServiceName}"; Flags: runhidden waituntilterminated
