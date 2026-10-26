; Instalador do ZYX EXPLODE (Inno Setup 6). Compilado pelo workflow .github/workflows/publicar.yml:
;   ISCC.exe /DAppVersion=0.16.0 /DSourceDir=<pasta com o ZyxExplode.exe publicado> installer\ZyxExplode.iss
; Saída: installer\Output\ZyxExplode-Setup.exe
#ifndef AppVersion
  #define AppVersion "0.0.0"
#endif
#ifndef SourceDir
  #define SourceDir "..\dist"
#endif
#define AppName "Zyx Explode"
#define AppExe "ZyxExplode.exe"

[Setup]
; AppId identifica o programa para atualizar/desinstalar: NÃO mude depois de publicar.
AppId={{D6B8F376-0A16-49B4-82B0-A65FF0D08079}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher=Zyx Solutions
AppPublisherURL=https://github.com/zyxsolutions/Zyx-explode
AppSupportURL=https://github.com/zyxsolutions/Zyx-explode
VersionInfoVersion={#AppVersion}
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
; O programa exige administrador (app.manifest); o instalador também, para gravar em Arquivos de Programas.
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
SetupIconFile=..\Assets\app.ico
UninstallDisplayIcon={app}\{#AppExe}
UninstallDisplayName={#AppName}
LicenseFile=..\LICENSE
OutputDir=Output
OutputBaseFilename=ZyxExplode-Setup
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "ptbr"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#SourceDir}\{#AppExe}"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\LEIA-ME.txt"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\LICENSE"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\THIRD-PARTY-NOTICES.md"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{group}\Desinstalar {#AppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExe}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent
; Atualização feita de dentro do programa (modo silencioso): abre a versão nova sozinho.
Filename: "{app}\{#AppExe}"; Flags: nowait skipifnotsilent

[Code]
// As configurações e o histórico ficam em %AppData%\ZyxExplode, fora da pasta do programa.
// Por padrão são mantidos (reinstalar não perde nada); o usuário pode apagar tudo ao desinstalar.
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  Dados: String;
begin
  if CurUninstallStep = usPostUninstall then
  begin
    Dados := ExpandConstant('{userappdata}\ZyxExplode');
    if (not UninstallSilent) and DirExists(Dados) then
      if MsgBox('Remover também as configurações e o histórico do Zyx Explode?' + #13#10 + Dados,
                mbConfirmation, MB_YESNO or MB_DEFBUTTON2) = IDYES then
        DelTree(Dados, True, True, True);
  end;
end;
