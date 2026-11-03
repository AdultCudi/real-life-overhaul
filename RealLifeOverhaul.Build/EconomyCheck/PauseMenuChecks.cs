using System;
using System.Diagnostics;
using System.IO;
using RealLifeOverhaul.Ui;

// The pause menu's own words and its one save line (Scripts/Ui/Pause, Scripts/Ui/UiPauseMenu): the time played kept in the
// save under reallife:playtime and read back, an older save without it, a later version's or a damaged line refused; how
// long ago a save was written and the time played in words; the game's save list read only as the menu opens, its loading
// through the game's own question and calls, and saving over one through the game's Save.
internal static partial class Program
{
    private static void PauseMenuChecks()
    {
        int before = _run, failedBefore = _failed;
        var watch = Stopwatch.StartNew();
        UiWords en = (key, english) => english;

        Check(PlayTimeStore.Write(4560L) == "v=1^4560", "pause menu: the time played is written as v=1^seconds");
        Check(PlayTimeStore.Read(PlayTimeStore.Write(4560L), out long back) && back == 4560L, "pause menu: the time played reads back as written");
        Check(PlayTimeStore.Read(null, out long none) && none == 0L, "pause menu: an older save without the line starts at nothing");
        Check(!PlayTimeStore.Read("v=2^10", out long later) && later == 0L, "pause menu: a later version's line is refused, not read");
        Check(!PlayTimeStore.Read("v=1^ten", out _) && !PlayTimeStore.Read("v=1^-5", out _) && !PlayTimeStore.Read("v=1", out _), "pause menu: a damaged line is refused");
        Check(PlayTimeStore.Write(-3L) == "v=1^0" && PlayTimeStore.Write(long.MaxValue) == "v=1^" + PlayTimeStore.Most, "pause menu: the time played is kept between nothing and a century");
        Check(PlayTimeStore.Key == "reallife:playtime", "pause menu: the time played is under its own reallife: key");

        Check(UiPauseMenu.Ago(en, -1) == "" && UiPauseMenu.Ago(en, 0) == "just now" && UiPauseMenu.Ago(en, 1) == "a minute ago"
              && UiPauseMenu.Ago(en, 14) == "14 minutes ago" && UiPauseMenu.Ago(en, 90) == "an hour ago" && UiPauseMenu.Ago(en, 5 * 60) == "5 hours ago"
              && UiPauseMenu.Ago(en, 30 * 60) == "yesterday" && UiPauseMenu.Ago(en, 6 * 24 * 60) == "6 days ago", "pause menu: a save's age in its largest whole unit");
        Check(UiPauseMenu.SaveLine(en, 412, 14) == "Day 412, 14 minutes ago" && UiPauseMenu.SaveLine(en, 0, 14) == "14 minutes ago" && UiPauseMenu.SaveLine(en, 9, -1) == "Day 9",
            "pause menu: a save's line leaves out what is not known");
        Check(UiPauseMenu.Played(en, 40) == "Under a minute" && UiPauseMenu.Played(en, 45 * 60) == "45 min" && UiPauseMenu.Played(en, 31 * 3600 + 20 * 60) == "31 h 20 min",
            "pause menu: the time played in hours and minutes");

        string scripts = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Scripts"));
        string saves = File.ReadAllText(System.IO.Path.Combine(scripts, @"Ui\Pause\PauseSaves.cs"));
        string screen = File.ReadAllText(System.IO.Path.Combine(scripts, @"Ui\Pause\PauseScreen.cs"));
        string play = File.ReadAllText(System.IO.Path.Combine(scripts, @"Ui\Pause\PlayTime.cs"));
        string guide = File.ReadAllText(System.IO.Path.Combine(scripts, @"Ui\UiGuide.cs"));
        Check(saves.Contains("SaveGamePathHelper.GetAllSaveGamesFromVersion()") && saves.Contains("save.characterId"), "pause menu: the saves are the game's own list, kept to this city's character");
        Check(saves.Contains("GameConfirm.Show(") && saves.Contains("SaveGameManager.JoinSaveGameThreads();") && saves.Contains("TransitionToSave.saveToLoadData = s;")
              && saves.Contains("LoadScene.LoadTransitionToSave();"), "pause menu: a save loads after the game's question, as the game's funeral loads one");
        Check(saves.Contains("!GoingPublic.InSession"), "pause menu: no save is loaded in a shared session");
        Check(screen.Contains("UiWidgets.Type(_name, s.name, false);") && screen.Contains("UiWidgets.Press(_buttons[0])") && screen.Contains("s.isRecoverSave"),
            "pause menu: saving over types the name into the game's field and presses its Save, never over an autosave");
        Check(screen.Contains("if (open && !_open)"), "pause menu: the city and the saves are read as the menu opens, not each frame");
        Check(play.Contains("SaveLines.IsKept(") && play.Contains("SaveLines.Unreadable(") && play.Contains("GoingPublic.IsGuest") && play.Contains("Meta.SaveTools.Dormant"),
            "pause menu: the time played keeps an unread line, a guest's save and a dormant save as they were");
        Check(guide.Contains(".Data(PlayTimeStore.Key)") && guide.Contains(".ResetData(PlayTime.Forget)"), "pause menu: the time played's key is declared by the interface's feature");
        Console.WriteLine("  pause menu: " + (_run - before) + " checks, " + (_failed - failedBefore) + " failed, " + watch.ElapsedMilliseconds + " ms");
    }
}
