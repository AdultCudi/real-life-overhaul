; Real Life Overhaul: the Windows installer (setup.exe) for playing without the Steam Workshop.
; It puts the mod in the game's ModsLocal folder, where the game loads it exactly as it loads a Workshop item.
;
; Build (see build-installer.ps1, which also stages the files):
;   ISCC.exe /DAppVersion=0.57.2 /DSourceDir=<folder holding the RealLifeOverhaul folder> /DOutputDir=<folder> RealLifeOverhaul.iss

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
Name: "de"; MessagesFile: "compiler:Languages\German.isl"
Name: "fr"; MessagesFile: "compiler:Languages\French.isl"
Name: "es"; MessagesFile: "compiler:Languages\Spanish.isl"
Name: "ru"; MessagesFile: "compiler:Languages\Russian.isl"
Name: "nl"; MessagesFile: "compiler:Languages\Dutch.isl"
Name: "tr"; MessagesFile: "compiler:Languages\Turkish.isl"
Name: "pl"; MessagesFile: "compiler:Languages\Polish.isl"
Name: "ko"; MessagesFile: "compiler:Languages\Korean.isl"

[CustomMessages]
en.GameRunning=Big Ambitions is running. Close the game, then press Retry.
pt.GameRunning=Big Ambitions está aberto. Feche o jogo e pressione Repetir.
de.GameRunning=Big Ambitions läuft. Schließe das Spiel und klicke dann auf Wiederholen.
fr.GameRunning=Big Ambitions est ouvert. Fermez le jeu, puis cliquez sur Réessayer.
es.GameRunning=Big Ambitions está abierto. Cierra el juego y pulsa Reintentar.
ru.GameRunning=Big Ambitions запущена. Закройте игру и нажмите «Повторить».
nl.GameRunning=Big Ambitions draait. Sluit het spel en druk dan op Opnieuw proberen.
tr.GameRunning=Big Ambitions çalışıyor. Oyunu kapatın, ardından Yeniden Dene düğmesine basın.
pl.GameRunning=Big Ambitions jest uruchomione. Zamknij grę, a potem naciśnij Ponów.
ko.GameRunning=Big Ambitions이(가) 실행 중입니다. 게임을 닫은 다음 다시 시도를 누르세요.
en.WorkshopToo=You are also subscribed to Real Life Overhaul on the Steam Workshop. With both, the game loads neither copy.%n%nAfter installing, unsubscribe from the Workshop item or untick it under Mods in the game's main menu. Press No to stop here.%n%nInstall anyway?
pt.WorkshopToo=Você também está inscrito no Real Life Overhaul na Oficina Steam. Com os dois, o jogo não carrega nenhuma das cópias.%n%nDepois de instalar, cancele a inscrição no item da Oficina ou desmarque-o em Mods no menu principal do jogo. Pressione Não para parar aqui.%n%nInstalar mesmo assim?
de.WorkshopToo=Du hast Real Life Overhaul auch im Steam Workshop abonniert. Mit beiden lädt das Spiel keine der beiden Kopien.%n%nKündige nach der Installation das Abo des Workshop-Objekts oder entferne den Haken unter Mods im Hauptmenü des Spiels. Klicke auf Nein, um hier aufzuhören.%n%nTrotzdem installieren?
fr.WorkshopToo=Vous êtes aussi abonné à Real Life Overhaul sur le Workshop Steam. Avec les deux, le jeu ne charge aucune des deux copies.%n%nAprès l'installation, désabonnez-vous de l'objet du Workshop ou décochez-le sous Mods dans le menu principal du jeu. Cliquez sur Non pour vous arrêter ici.%n%nInstaller quand même ?
es.WorkshopToo=También estás suscrito a Real Life Overhaul en el Workshop de Steam. Con los dos, el juego no carga ninguna de las dos copias.%n%nDespués de instalar, cancela la suscripción al objeto del Workshop o desmárcalo en Mods, en el menú principal del juego. Pulsa No para parar aquí.%n%n¿Instalar de todos modos?
ru.WorkshopToo=Вы также подписаны на Real Life Overhaul в Мастерской Steam. С обеими копиями игра не загрузит ни одну из них.%n%nПосле установки отпишитесь от предмета в Мастерской или снимите с него отметку в разделе «Моды» главного меню игры. Нажмите «Нет», чтобы остановиться.%n%nВсё равно установить?
nl.WorkshopToo=Je bent ook geabonneerd op Real Life Overhaul in de Steam Workshop. Met beide versies laadt het spel geen van beide kopieën.%n%nSchrijf je na de installatie uit voor het Workshop-item of vink het uit onder "Mods" in het hoofdmenu van het spel. Druk op Nee om hier te stoppen.%n%nToch installeren?
tr.WorkshopToo=Steam Workshop üzerinden de Real Life Overhaul aboneliğiniz var. Her ikisi de yüklü olduğunda oyun iki kopyayı da yüklemez.%n%nKurulumdan sonra Workshop öğesinin aboneliğinden çıkın veya oyunun ana menüsündeki "Modlar" kısmından işaretini kaldırın. Burada durmak için Hayır düğmesine basın.%n%nYine de kurulsun mu?
pl.WorkshopToo=Subskrybujesz też Real Life Overhaul w Warsztacie Steam. Gdy są oba, gra nie wczyta żadnej z kopii.%n%nPo instalacji anuluj subskrypcję elementu z Warsztatu albo odznacz go w Modach w menu głównym gry. Naciśnij Nie, aby tu przerwać.%n%nZainstalować mimo to?
ko.WorkshopToo=Steam 워크숍에서 Real Life Overhaul도 구독 중입니다. 둘 다 있으면 게임은 어느 쪽 사본도 불러오지 않습니다.%n%n설치한 뒤에는 워크숍 항목의 구독을 취소하거나 게임 메인 메뉴의 모드에서 체크를 해제하세요. 여기서 멈추려면 아니요를 누르세요.%n%n그래도 설치할까요?
en.ReadyText=Real Life Overhaul {#AppVersion} will be put in the game's mod folder:%n%n{#ModFolder}%n%nAn older copy there is replaced. Your saves and the mod's options are kept.
pt.ReadyText=O Real Life Overhaul {#AppVersion} será colocado na pasta de mods do jogo:%n%n{#ModFolder}%n%nUma cópia mais antiga ali é substituída. Seus saves e as opções do mod são mantidos.
de.ReadyText=Real Life Overhaul {#AppVersion} wird in den Mod-Ordner des Spiels gelegt:%n%n{#ModFolder}%n%nEine ältere Kopie dort wird ersetzt. Deine Spielstände und die Optionen der Mod bleiben erhalten.
fr.ReadyText=Real Life Overhaul {#AppVersion} va être placé dans le dossier des mods du jeu :%n%n{#ModFolder}%n%nUne copie plus ancienne qui s'y trouve est remplacée. Vos sauvegardes et les options du mod sont conservées.
es.ReadyText=Real Life Overhaul {#AppVersion} se pondrá en la carpeta de mods del juego:%n%n{#ModFolder}%n%nSe sustituye una copia anterior que haya allí. Tus partidas y las opciones del mod se conservan.
ru.ReadyText=Real Life Overhaul {#AppVersion} будет помещён в папку модов игры:%n%n{#ModFolder}%n%nСтарая копия в ней заменяется. Ваши сохранения и настройки мода остаются.
nl.ReadyText=Real Life Overhaul {#AppVersion} wordt in de modmap van het spel geplaatst:%n%n{#ModFolder}%n%nEen oudere kopie daar wordt vervangen. Je savegames en de opties van de mod blijven bewaard.
tr.ReadyText=Real Life Overhaul {#AppVersion} oyunun mod klasörüne yerleştirilecek:%n%n{#ModFolder}%n%nOradaki eski bir kopya değiştirilecek. Kayıtlarınız ve mod seçenekleri korunacak.
pl.ReadyText=Real Life Overhaul {#AppVersion} zostanie umieszczony w folderze modów gry:%n%n{#ModFolder}%n%nStarsza kopia w tym miejscu zostanie zastąpiona. Twoje zapisy i opcje moda zostaną zachowane.
ko.ReadyText=Real Life Overhaul {#AppVersion}을(를) 게임의 모드 폴더에 설치합니다:%n%n{#ModFolder}%n%n그곳에 있는 이전 사본은 교체됩니다. 저장 파일과 모드 옵션은 그대로 유지됩니다.
en.DoneText=Real Life Overhaul {#AppVersion} is installed.%n%nStart Big Ambitions: the mod's page is on the Mods tab of Options, and a notice opens its Guide. The first time you load a save that has never had mods, the game offers to keep a backup.
pt.DoneText=O Real Life Overhaul {#AppVersion} está instalado.%n%nInicie o Big Ambitions: a página do mod fica na aba Mods das Opções, e um aviso abre o Guia. Na primeira vez que você carregar um save que nunca teve mods, o jogo oferece guardar uma cópia de segurança.
de.DoneText=Real Life Overhaul {#AppVersion} ist installiert.%n%nStarte Big Ambitions: Die Seite der Mod ist im Reiter Mods der Optionen, und eine Mitteilung öffnet ihren Leitfaden. Wenn du zum ersten Mal einen Spielstand lädst, der nie Mods hatte, bietet das Spiel an, eine Sicherung anzulegen.
fr.DoneText=Real Life Overhaul {#AppVersion} est installé.%n%nLancez Big Ambitions : la page du mod se trouve dans l'onglet Mods des Options, et une notification ouvre son Guide. La première fois que vous chargez une sauvegarde qui n'a jamais eu de mods, le jeu propose d'en garder une copie.
es.DoneText=Real Life Overhaul {#AppVersion} está instalado.%n%nInicia Big Ambitions: la página del mod está en la pestaña Mods de Opciones, y un aviso abre su Guía. La primera vez que cargues una partida que nunca ha tenido mods, el juego ofrece guardar una copia de seguridad.
ru.DoneText=Real Life Overhaul {#AppVersion} установлен.%n%nЗапустите Big Ambitions: страница мода находится на вкладке «Моды» в настройках, а уведомление открывает его руководство. Когда вы впервые загрузите сохранение, в котором никогда не было модов, игра предложит сделать резервную копию.
nl.DoneText=Real Life Overhaul {#AppVersion} is geïnstalleerd.%n%nStart Big Ambitions: de pagina van de mod staat op het tabblad "Mods" van "Opties", en een melding opent de "Gids". De eerste keer dat je een savegame laadt die nog nooit mods had, biedt het spel aan om een reservekopie te maken.
tr.DoneText=Real Life Overhaul {#AppVersion} kuruldu.%n%nBig Ambitions'ı başlatın: modun sayfası "Ayarlar" içindeki "Modlar" sekmesindedir ve bir bildirim "Kılavuz"u açar. Daha önce hiç mod kullanılmamış bir kaydı ilk kez yüklediğinizde, oyun bir yedek tutmayı teklif eder.
pl.DoneText=Real Life Overhaul {#AppVersion} został zainstalowany.%n%nUruchom Big Ambitions: strona moda jest na karcie Mody w Opcjach, a powiadomienie otwiera jego Przewodnik. Gdy po raz pierwszy wczytasz zapis, w którym nigdy nie było modów, gra zaproponuje zachowanie kopii zapasowej.
ko.DoneText=Real Life Overhaul {#AppVersion} 설치가 끝났습니다.%n%nBig Ambitions를 실행하세요. 모드 페이지는 옵션의 모드 탭에 있고, 알림을 누르면 가이드가 열립니다. 모드를 쓴 적이 없는 저장 파일을 처음 불러오면 게임이 백업을 남길지 물어봅니다.

[Messages]
en.ConfirmUninstall=Before you remove the mod, load each save you played with it and press "Prepare this save for removing the mod" under Troubleshooting, Save tools, then save.%n%nRemove Real Life Overhaul now?
pt.ConfirmUninstall=Antes de remover o mod, carregue cada save em que você jogou com ele e pressione "Preparar este save para a remoção do mod" em Solução de problemas, Ferramentas de salvamento, e salve.%n%nRemover o Real Life Overhaul agora?
de.ConfirmUninstall=Bevor du die Mod entfernst, lade jeden Spielstand, den du mit ihr gespielt hast, klicke unter Fehlerbehebung, Spielstand-Werkzeuge auf "Diesen Spielstand für das Entfernen der Mod vorbereiten" und speichere.%n%nReal Life Overhaul jetzt entfernen?
fr.ConfirmUninstall=Avant de retirer le mod, chargez chaque sauvegarde où vous avez joué avec lui, appuyez sur « Préparer cette sauvegarde au retrait du mod » sous Dépannage, Outils de sauvegarde, puis sauvegardez.%n%nRetirer Real Life Overhaul maintenant ?
es.ConfirmUninstall=Antes de quitar el mod, carga cada partida en la que hayas jugado con él, pulsa "Preparar esta partida para quitar el mod" en Solución de problemas, Herramientas de partida, y guarda.%n%n¿Quitar Real Life Overhaul ahora?
ru.ConfirmUninstall=Перед удалением мода загрузите каждое сохранение, в котором вы играли с ним, нажмите «Подготовить это сохранение к удалению мода» в разделе «Устранение неполадок», «Инструменты сохранений» и сохраните игру.%n%nУдалить Real Life Overhaul сейчас?
nl.ConfirmUninstall=Voordat je de mod verwijdert, laad je elke savegame waarmee je de mod hebt gespeeld en druk je onder "Problemen oplossen", "Savegame-tools" op "Deze savegame voorbereiden op het verwijderen van de mod" en sla je op.%n%nReal Life Overhaul nu verwijderen?
tr.ConfirmUninstall=Modu kaldırmadan önce, modla oynadığınız her kaydı yükleyin, "Sorun Giderme" altındaki "Kayıt Araçları"ndan "Bu kaydı modu kaldırmaya hazırla" seçeneğine basın ve kaydedin.%n%nReal Life Overhaul şimdi kaldırılsın mı?
pl.ConfirmUninstall=Zanim usuniesz moda, wczytaj każdy zapis, w który z nim grałeś, naciśnij „Przygotuj ten zapis do usunięcia moda” w sekcji Rozwiązywanie problemów, Narzędzia zapisu, a potem zapisz grę.%n%nUsunąć teraz Real Life Overhaul?
ko.ConfirmUninstall=모드를 제거하기 전에, 모드와 함께 플레이한 저장 파일을 각각 불러와서 문제 해결, 저장 도구의 "모드 제거를 위해 이 저장 파일 준비"를 누른 다음 저장하세요.%n%n지금 Real Life Overhaul을 제거할까요?

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
