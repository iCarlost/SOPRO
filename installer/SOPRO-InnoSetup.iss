#define MyAppName "SOPRO"
#define MyAppVersion "1.5.0"
#define MyAppPublisher "CEPM"
#define MyAppExeName "SOPRO.WinForms.exe"
#define MyAppId "SOPRO.WinForms"
#define MyPublishDir "..\publish\SOPRO"

[Setup]
AppId={#MyAppId}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={localappdata}\Programs\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir=.
OutputBaseFilename=SOPRO-Setup-{#MyAppVersion}
UninstallDisplayIcon={app}\{#MyAppExeName}
; Si el icono no está junto al .iss, deja esta línea comentada
;SetupIconFile=***REMOVED***
ChangesAssociations=no
CloseApplications=yes
CloseApplicationsFilter={#MyAppExeName}
RestartApplications=no
UsePreviousAppDir=yes
DisableDirPage=yes

[Languages]
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"

[Tasks]
Name: "desktopicon"; Description: "Crear acceso directo en el escritorio"; GroupDescription: "Accesos directos:"; Flags: unchecked

[Files]
Source: "{#MyPublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Ejecutar {#MyAppName}"; Flags: nowait postinstall skipifsilent unchecked

[Dirs]
Name: "{userdocs}\SOPRO"
Name: "{userdocs}\SOPRO\Proyectos"
Name: "{localappdata}\SOPRO"

[UninstallDelete]
; No borrar bases, reportes ni configuración del usuario.
; El desinstalador NO toca Documentos\SOPRO ni LocalAppData\SOPRO.
