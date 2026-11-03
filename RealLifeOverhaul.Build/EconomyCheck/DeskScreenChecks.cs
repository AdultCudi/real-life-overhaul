using System;
using System.Collections.Generic;
using System.IO;
using IoPath = System.IO.Path;
using RealLifeOverhaul;

// A player working at a web development or tech firm desk saw both monitors black, and so did the staff the mod seats there.
// The mod furnishes those floors with copies of the game's furniture stripped of every script, among them the monitor's
// ScreenVideoController that plays the screen's picture, and it seats people by the pose alone: the game lights a desk's
// screens only when it seats its own employee (WorkstationController.AssignEmployee), which a pose never calls. Now the
// copies keep the game's component, switched off, and every place the mod seats somebody at a computer desk switches it on
// (Core/DeskScreens) and every way they get up switches it off. Read here from the sources as the game cannot be run.
internal static partial class Program
{
    private static void DeskScreens()
    {
        DeskScreensComponent();
        DeskScreensPlayer();
        DeskScreensCast();
        DeskScreensEvery();
        DeskScreensTexts();
    }

    // The game's own component, kept on the copies and driven only by an event.
    private static void DeskScreensComponent()
    {
        string furniture = ModSource(@"Core\GameFurniture.cs");
        string unwanted = MethodText(furniture, "private static bool Unwanted(Component c)");
        string strip = MethodText(furniture, "private static void Strip(Transform root)");
        Check(unwanted.Contains("!(c is ScreenVideoController)") && strip.Contains("Components[i] is ScreenVideoController screen") && strip.Contains("screen.enabled = false;"),
            "desk screens: a stripped copy of the game's furniture keeps the game's own screen component (ScreenVideoController), switched off, so a monitor can be lit");

        string screens = ModSource(@"Core\DeskScreens.cs");
        Check(screens.Contains("item.PlayVideoOnScreen(VideoClipData.VideoType.Work);") && screens.Contains("item.StopVideoOnScreen();")
              && screens.Contains("screen.Play(VideoClipData.VideoType.Work);") && screens.Contains("screen.Stop();"),
            "desk screens: the screens are lit and put out by the game's own calls, the Work video of its desk computers (no screen of the mod's own)");
        Check(ComesBefore(MethodText(screens, "public static bool OnScreen(ScreenVideoController screen)"), "screen.enabled = true;", "screen.Play(")
              && ComesBefore(MethodText(screens, "public static bool OffScreen(ScreenVideoController screen)"), "screen.Stop();", "screen.enabled = false;"),
            "desk screens: a copy's component is on only while its screen plays, so nothing of it runs while the desk is idle");
        Check(!screens.Contains("void Update(") && !screens.Contains("LateUpdate") && !screens.Contains("FixedUpdate") && !screens.Contains("MonoBehaviour")
              && !screens.Contains("GoingPublic") && !screens.Contains("Send") && !screens.Contains("Net"),
            "desk screens: nothing runs every frame, and nothing is sent over the network (the screen is local and visual)");
        Check(screens.Contains("Desk check (screens): ") && screens.Contains("Meta.Dev.Verbose") && screens.Contains("PASS"),
            "desk screens: with Developer diagnostics on, every seat and stand writes one Desk check (screens) line");
    }

    // The player: the game's desks through the pose, the mod's fitted desk through its own copy.
    private static void DeskScreensPlayer()
    {
        string game = ModSource(@"Careers\DeskGame.cs");
        string sitAt = MethodText(game, "public static bool SitAt(EmployeeStationController station)");
        string sitOn = MethodText(game, "public static bool SitOn(Transform seat, EmployeeStationController station, out Vector3 stood)");
        string standFrom = MethodText(game, "public static void StandFrom(EmployeeStationController station)");
        string standUp = MethodText(game, "public static void StandUp(Vector3 at, EmployeeStationController station)");
        Check(ComesBefore(sitAt, "if (Holds(station, player))", "ScreenOn(station);") && ComesBefore(sitAt, "ScreenOn(station);", "return true;")
              && ComesBefore(sitOn, "if (player.Character.isSittingOn == seat)", "ScreenOn(station);") && ComesBefore(sitOn, "ScreenOn(station);", "return true;"),
            "desk screens: both poses (SitAt, SitOn) light a desk computer's screens once the player is held, and not before");
        Check(standFrom.Contains("ScreenOff(station);") && standUp.Contains("ScreenOff(station);")
              && ComesBefore(standFrom, "if (station != null && !Taken(station))", "ScreenOff(station);") && ComesBefore(standUp, "if (station != null && !Taken(station))", "ScreenOff(station);"),
            "desk screens: both releases (StandFrom, StandUp) put them out, unless the game has seated somebody of its own there since");
        string on = MethodText(game, "private static void ScreenOn(EmployeeStationController station)");
        string off = MethodText(game, "private static void ScreenOff(EmployeeStationController station)");
        Check(on.Contains("station is DesktopWorkstationController") && on.Contains("DeskScreens.OnItem(station);")
              && off.Contains("station is DesktopWorkstationController") && off.Contains("DeskScreens.OffItem(station);"),
            "desk screens: a desk computer's screens by the game's own item call; a till or a counter, which has none, is left alone");

        string work = ModSource(@"Careers\DeskWork.cs");
        string arrived = MethodText(work, "private void Arrived()");
        string end = MethodText(work, "private void End(bool settle, bool walking)");
        Check(arrived.Contains("DeskScreens.OnDesk(_desk.Desk,")
              && ComesBefore(arrived, "if (!(seated ? DeskGame.SitOn(", "DeskScreens.OnDesk(") && ComesBefore(arrived, "DeskScreens.OnDesk(", "_sitting.SkipStarted();"),
            "desk screens: a job at a fitted desk (the tech and web development firms' floors) lights its monitors once the player sits, before the day begins");
        Check(end.Contains("DeskScreens.OffDesk(_desk.Desk,") && ComesBefore(end, "DeskGame.StandUp(", "DeskScreens.OffDesk(") && MethodText(work, "public void Finish()").Contains("End(true, true);")
              && MethodText(work, "public void Close()").Contains("End(false, true);") && EndsWhenFinished(MethodText(work, "public void ChangeState(PlayerActivityState state)"), "End(true, true);"),
            "desk screens: they go dark on every way out of a fitted desk: Stop working, the time up, skipped, the game's panel, the job closing");
    }

    // The people the mod seats: a fitted floor's staff and a police station's officers.
    private static void DeskScreensCast()
    {
        string cast = ModSource(@"FitOut\FitCast.cs");
        string sit = MethodText(cast, "private void SitDown(Member m, float now)");
        string release = MethodText(cast, "private void Release(Member m)");
        string end = MethodText(cast, "public void End()");
        Check(sit.Contains("DeskScreens.OnDesk(seat.Desk,") && ComesBefore(sit, "if (!m.Role.Sit(seat.At))", "DeskScreens.OnDesk(") && sit.Contains("m.Use == FitUse.Desk"),
            "desk screens: staff the mod seats at a fitted desk have its monitors lit while they work");
        Check(release.Contains("DeskScreens.OffDesk(_seats[m.Seat].Desk,") && end.Contains("Release(_members[i]);"),
            "desk screens: dark when they get up, leave, are taken back or the floor goes (every one of those passes through Release)");
        string view = ModSource(@"FitOut\FitView.cs");
        Check(view.Contains("Stand = _seats[i].Stand, Desk = _seats[i].Desk });"), "desk screens: the floor hands each seat's desk to its cast");

        string station = ModSource(@"Law\StationInterior.Cast.cs");
        string light = MethodText(station, "private void LightDesk(int index, Transform seat)");
        string hold = MethodText(station, "private void Hold(int seat, bool held)");
        string endCast = MethodText(station, "private void EndCast()");
        string settle = MethodText(station, "private void Settle(Member m, float now)");
        Check(light.Contains("DeskScreens.Near(") && light.Contains("DeskScreens.OnScreen(s.Screen)")
              && ComesBefore(settle, "if (!role.Sit(seat))", "LightDesk(m.Seat, seat);") && settle.Contains("m.Use == SeatUse.Desk"),
            "desk screens: the officers the mod seats at a police station's desks have the screen on their desk lit while they work");
        Check(hold.Contains("DeskScreens.OffScreen(s.Screen);") && ComesBefore(hold, "if (!held && s.Screen != null)", "DeskScreens.OffScreen(") && endCast.Contains("DeskScreens.OffScreen(s.Screen);")
              && Occurs(station, "Hold(m.Seat, false);") == 2,
            "desk screens: dark when the officer leaves their chair or is taken away, and when the station's people end");
    }

    // Every place the mod seats somebody at a computer desk: a new one fails here until it lights and darkens the screens too.
    private static void DeskScreensEvery()
    {
        string scripts = IoPath.GetFullPath(IoPath.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Scripts"));
        var found = new List<string>();
        foreach (string path in Directory.GetFiles(scripts, "*.cs", SearchOption.AllDirectories))
        {
            string code = File.ReadAllText(path);
            if (!code.Contains("UsingDesktopComputer"))
            {
                continue;
            }
            string name = IoPath.GetFileName(path);
            found.Add(name);
            bool both;
            switch (name)
            {
                case "FitCast.cs":
                    both = code.Contains("DeskScreens.OnDesk(") && code.Contains("DeskScreens.OffDesk(");
                    break;
                case "StationInterior.Cast.cs":
                    both = code.Contains("DeskScreens.OnScreen(") && code.Contains("DeskScreens.OffScreen(");
                    break;
                case "DeskGame.cs":
                    both = code.Contains("DeskScreens.OnItem(") && code.Contains("DeskScreens.OffItem(");
                    break;
                case "ComputerSitting.cs":
                case "SchoolDesk.cs":
                    both = code.Contains(".PlayVideoOnScreen(VideoClipData.VideoType.Work)") && code.Contains(".StopVideoOnScreen()");   // the game's own call, as its study activity makes it
                    break;
                default:
                    both = false;
                    break;
            }
            Check(both, "desk screens: " + name + " seats somebody at a computer desk and lights and darkens its screens");
        }
        found.Sort(StringComparer.Ordinal);
        Check(string.Join(", ", found) == "ComputerSitting.cs, DeskGame.cs, FitCast.cs, SchoolDesk.cs, StationInterior.Cast.cs",
            "desk screens: the files that seat somebody at a computer desk are the home computer, the job's desks, a fitted floor's staff, the school desk and the police station's officers;"
            + " a new one is checked for its screens here first (" + string.Join(", ", found) + ")");
    }

    private static void DeskScreensTexts()
    {
        Check(FixLineEverywhere("screens at desks in tech and web development firms stayed black", "telas das mesas em empresas de tecnologia e desenvolvimento web",
                                "科技公司和网页开发公司办公桌上的屏幕",
                                "Bildschirme an den Schreibtischen in Tech- und Webentwicklungsfirmen", "écrans des bureaux dans les entreprises de technologie et de développement web",
                                "pantallas de los escritorios en empresas de tecnología y desarrollo web", "экраны за столами в компаниях сферы технологий и веб-разработки",
                                "schermen bij bureaus in tech- en webontwikkelingsbedrijven", "teknoloji ve web geliştirme şirketlerindeki masaların ekranları"),
            "desk screens texts: What's new has the line, with its fix mark, in some row of every language");
        bool later = true;
        var later2 = new Dictionary<string, string> { { "pl", "Naprawiono: ekrany przy biurkach" }, { "ko", "수정: 기술 및 웹 개발 회사 책상의 화면" } };
        foreach (KeyValuePair<string, string> lang in later2)
        {
            Dictionary<string, string> words = LoadWords(IoPath.Combine(LocalesFolder(), lang.Key + ".json"));
            later &= words != null && WhatsNewHas(words, lang.Value);
        }
        Check(later, "desk screens texts: What's new has the line in Polish and Korean too, in some row");
    }
}
