; Inno Setup Script para o CrankoShot
; Gerador de Instalador Oficial Windows (.exe)

#define MyAppName "CrankoShot"
#define MyAppVersion "2.0.0"
#define MyAppPublisher "CrankoShot Project"
#define MyAppExeName "ShotMind.exe"

[Setup]
AppId={{E1B3A920-56E8-4B02-86A8-A63E02CF091B}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
OutputDir=installer_output
OutputBaseFilename=CrankoShot-Setup
SetupIconFile=app.ico
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
ArchitecturesInstallIn64BitMode=x64compatible
ArchitecturesAllowed=x64compatible
MinVersion=10.0.10240
PrivilegesRequired=admin
PrivilegesRequiredOverridesAllowed=dialog
UsePreviousAppDir=yes
DirExistsWarning=no
CloseApplications=yes
CloseApplicationsFilter={#MyAppExeName}
UninstallDisplayIcon={app}\{#MyAppExeName}
UninstallDisplayName={#MyAppName} {#MyAppVersion}

[Languages]
Name: "brazilianportuguese"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "startupicon"; Description: "Iniciar o CrankoShot automaticamente ao ligar o computador"; GroupDescription: "Configurações de Inicialização:"

[Dirs]
Name: "{userappdata}\ShotMind"
Name: "{userappdata}\ShotMind\logs"

[Files]
Source: "publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"
Name: "{group}\Desinstalar {#MyAppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"; Tasks: desktopicon
Name: "{userstartup}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Parameters: "--background"; WorkingDir: "{app}"; Tasks: startupicon

[Run]
Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent runasoriginaluser

[Messages]
brazilianportuguese.BeveledLabel=CrankoShot — Captura & Inteligência Visual

[Code]
// Fecha qualquer instância anterior em execução antes de atualizar arquivos
function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  ResultCode: Integer;
begin
  Result := '';
  Exec('taskkill.exe', '/IM {#MyAppExeName} /F', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Sleep(400);
end;

// Confirmação e informações ao desinstalar
function InitializeUninstall(): Boolean;
begin
  Result := MsgBox('Deseja realmente desinstalar o CrankoShot do seu computador?' + #13#10#13#10 +
                   'Nota: Suas capturas salvas na pasta Imagens não serão apagadas.',
                   mbConfirmation, MB_YESNO) = IDYES;
end;
