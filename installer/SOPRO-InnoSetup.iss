#define MyAppName "SOPRO"
#define MyAppVersion "1.8.0"
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
; El icono del instalador es opcional: solo se activa si el asset local
; SOPRO.WinForms\SOPRO.ico existe (nunca se publica en el repo).
#if FileExists(AddBackslash(SourcePath) + "..\SOPRO.WinForms\SOPRO.ico")
SetupIconFile=..\SOPRO.WinForms\SOPRO.ico
#endif
ChangesAssociations=yes
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

[Registry]
; Asociación de archivos .soproproj (instalación per-user, HKCU).
; El icono del tipo de archivo se toma del exe embebido (asset no versionado).
Root: HKCU; Subkey: "Software\Classes\.soproproj"; ValueType: string; ValueName: ""; ValueData: "SOPRO.Project"; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\Classes\.soproproj\OpenWithProgids"; ValueType: string; ValueName: "SOPRO.Project"; ValueData: ""; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\Classes\SOPRO.Project"; ValueType: string; ValueName: ""; ValueData: "Proyecto SOPRO"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\SOPRO.Project\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: """{app}\SOPRO.WinForms.exe"",0"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\SOPRO.Project\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\SOPRO.WinForms.exe"" ""%1"""; Flags: uninsdeletekey

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Ejecutar {#MyAppName}"; Flags: nowait postinstall skipifsilent unchecked

[Dirs]
Name: "{userdocs}\SOPRO"
Name: "{userdocs}\SOPRO\Proyectos"
Name: "{localappdata}\SOPRO"

[UninstallDelete]
; No borrar bases, reportes ni configuración del usuario.
; El desinstalador NO toca Documentos\SOPRO ni LocalAppData\SOPRO.
