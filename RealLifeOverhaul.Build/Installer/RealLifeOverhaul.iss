; Real Life Overhaul: the Windows installer (setup.exe) for playing without the Steam Workshop.
; It puts the mod in the game's ModsLocal folder, where the game loads it exactly as it loads a Workshop item.
;
; Build (see build-installer.ps1, which also stages the files):
;   ISCC.exe /DAppVersion=0.4.7 /DSourceDir=<folder holding the RealLifeOverhaul folder> /DOutputDir=<folder> RealLifeOverhaul.iss

#ifndef AppVersion
  #error Pass /DAppVersion=<version>
#endif
#ifndef SourceDir
  #error Pass /DSourceDir=<folder holding the built RealLifeOverhaul folder>
#endif
#ifndef OutputDir
  #define OutputDir "."
#endif

#define ModFolder "{%USERPROFILE}\AppData\LocalLow\Hovgaard Games\Big Ambitions\ModsLocal\RealLifeOverhaul"
#define WorkshopId "3816536560"
#define GameAppId "1331550"

[Setup]
AppId={{6C1E3F0A-4B7D-4E9A-9C21-5D8F2A7B3E14}
AppName=Real Life Overhaul
AppVersion={#AppVersion}
AppVerName=Real Life Overhaul {#AppVersion}
AppPublisher=Real Life Overhaul
AppPublisherURL=https://github.com/AdultCudi/real-life-overhaul
AppSupportURL=https://discord.gg/tzCh2VXs9E
AppUpdatesURL=https://github.com/AdultCudi/real-life-overhaul/releases/latest
VersionInfoVersion={#AppVersion}
VersionInfoDescription=Real Life Overhaul {#AppVersion} setup, a mod for Big Ambitions
DefaultDirName={#ModFolder}
DisableDirPage=yes
DisableProgramGroupPage=yes
DisableReadyPage=no
PrivilegesRequired=lowest
OutputDir={#OutputDir}
OutputBaseFilename=RealLifeOverhaul-{#AppVersion}-setup
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
UninstallDisplayName=Real Life Overhaul (Big Ambitions mod)
; beside ModsLocal, not in the mod's folder: that folder is emptied on every update and holds only what the game loads
UninstallFilesDir={app}\..\..\RealLifeOverhaul Setup
UsePreviousAppDir=no
CloseApplications=no

[Languages]
Name: "en"; MessagesFile: "compiler:Default.isl"
Name: "pt"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"

[CustomMessages]
en.GameRunning=Big Ambitions is running. Close the game, then press Retry.
pt.GameRunning=Big Ambitions está aberto. Feche o jogo e pressione Repetir.
en.WorkshopToo=You are also subscribed to Real Life Overhaul on the Steam Workshop. With both, the game starts the mod twice and you cannot be sure which version runs.%n%nAfter installing, unsubscribe from the Workshop item or untick it under Mods in the game's main menu. Press No to stop here.%n%nInstall anyway?
pt.WorkshopToo=Você também está inscrito no Real Life Overhaul na Oficina Steam. Com os dois, o jogo inicia o mod duas vezes e não dá para saber qual versão está rodando.%n%nDepois de instalar, cancele a inscrição no item da Oficina ou desmarque-o em Mods no menu principal do jogo. Pressione Não para parar aqui.%n%nInstalar mesmo assim?
en.ReadyText=Real Life Overhaul {#AppVersion} will be put in the game's mod folder:%n%n{#ModFolder}%n%nAn older copy there is replaced. Your saves and the mod's options are kept.
pt.ReadyText=O Real Life Overhaul {#AppVersion} será colocado na pasta de mods do jogo:%n%n{#ModFolder}%n%nUma cópia mais antiga ali é substituída. Seus saves e as opções do mod são mantidos.
en.DoneText=Real Life Overhaul {#AppVersion} is installed.%n%nStart Big Ambitions: the mod's page is on the Mods tab of Options, and a notice opens its Guide. The first time you load a save that has never had mods, the game offers to keep a backup.
pt.DoneText=O Real Life Overhaul {#AppVersion} está instalado.%n%nInicie o Big Ambitions: a página do mod fica na aba Mods das Opções, e um aviso abre o Guia. Na primeira vez que você carregar um save que nunca teve mods, o jogo oferece guardar uma cópia de segurança.

[Messages]
en.ConfirmUninstall=Before you remove the mod, load each save you played with it and press "Prepare this save for removing the mod" under Troubleshooting, Save tools, then save.%n%nRemove Real Life Overhaul now?
pt.ConfirmUninstall=Antes de remover o mod, carregue cada save em que você jogou com ele e pressione "Preparar este save para a remoção do mod" em Solução de problemas, Ferramentas de salvamento, e salve.%n%nRemover o Real Life Overhaul agora?

[InstallDelete]
; whatever an older version left, so that no stale file is loaded with the new one
Type: filesandordirs; Name: "{app}\*"

[Files]
Source: "{#SourceDir}\RealLifeOverhaul\*"; DestDir: "{app}"; Flags: recursesubdirs createallsubdirs ignoreversion

[UninstallDelete]
Type: filesandordirs; Name: "{app}"

[Code]
function GameRunning(): Boolean;
var
  Locator, Service, Found: Variant;
begin
  Result := False;
  try
    Locator := CreateOleObject('WbemScripting.SWbemLocator');
    Service := Locator.ConnectServer('.', 'root\CIMV2');
    Found := Service.ExecQuery('SELECT ProcessId FROM Win32_Process WHERE Name = ''Big Ambitions.exe''');
    Result := Found.Count > 0;
  except
    Result := False;   { no way to tell: go on, as copying over a running game's files is the only risk and Windows refuses it }
  end;
end;

function WaitForGameClosed(): Boolean;
begin
  Result := True;
  while GameRunning() do
  begin
    if SuppressibleMsgBox(CustomMessage('GameRunning'), mbError, MB_RETRYCANCEL, IDCANCEL) = IDCANCEL then   { silent: stop, never wait }
    begin
      Result := False;
      Exit;
    end;
  end;
end;

{ Every Steam library folder: the main one and those listed in libraryfolders.vdf ("path" lines). }
function SubscribedOnWorkshop(): Boolean;
var
  SteamPath, Line, Path: String;
  Lines: TArrayOfString;
  I, A, B: Integer;
begin
  Result := False;
  if not RegQueryStringValue(HKCU, 'Software\Valve\Steam', 'SteamPath', SteamPath) then
    Exit;
  StringChangeEx(SteamPath, '/', '\', True);
  if DirExists(SteamPath + '\steamapps\workshop\content\{#GameAppId}\{#WorkshopId}') then
  begin
    Result := True;
    Exit;
  end;
  if not LoadStringsFromFile(SteamPath + '\steamapps\libraryfolders.vdf', Lines) then
    Exit;
  for I := 0 to GetArrayLength(Lines) - 1 do
  begin
    Line := Trim(Lines[I]);
    if Pos('"path"', Line) = 1 then
    begin
      Line := Trim(Copy(Line, 7, Length(Line)));
      A := Pos('"', Line);
      if A > 0 then
      begin
        Line := Copy(Line, A + 1, Length(Line));
        B := Pos('"', Line);
        if B > 0 then
        begin
          Path := Copy(Line, 1, B - 1);
          StringChangeEx(Path, '\\', '\', True);
          if DirExists(Path + '\steamapps\workshop\content\{#GameAppId}\{#WorkshopId}') then
          begin
            Result := True;
            Exit;
          end;
        end;
      end;
    end;
  end;
end;

function InitializeSetup(): Boolean;
begin
  Result := WaitForGameClosed();
  if Result and SubscribedOnWorkshop() then
    Result := SuppressibleMsgBox(CustomMessage('WorkshopToo'), mbConfirmation, MB_YESNO, IDYES) = IDYES;
end;

procedure CurPageChanged(CurPageID: Integer);
begin
  if CurPageID = wpReady then
    WizardForm.ReadyMemo.Text := CustomMessage('ReadyText')
  else if CurPageID = wpFinished then
    WizardForm.FinishedLabel.Caption := CustomMessage('DoneText');
end;

function NextButtonClick(CurPageID: Integer): Boolean;
begin
  Result := True;
  if CurPageID = wpReady then
    Result := WaitForGameClosed();
end;

function InitializeUninstall(): Boolean;
begin
  Result := WaitForGameClosed();   { Windows' own question, which follows, says what to do in each save first (ConfirmUninstall) }
end;
