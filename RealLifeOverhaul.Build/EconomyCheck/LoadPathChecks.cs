using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using RealLifeOverhaul;
using RealLifeOverhaul.Seasons;

// Load-path parity (Scripts: every hook that runs when a save is loaded), and the season under the date. Two player reports found the Contacts
// app's groups and the season under the date failing on one way of loading. In the game, Continue on the main menu and Load from the list are
// one call (Scenes.MainMenu.LoadGame.Load: TransitionToSave, then LoadScene.LoadGame), so the mod has nothing to tell them apart by and must not
// look for anything. What does differ between loads is what the game has made by then: the first city of a session finds a game static that
// the game sets to null when it starts and makes only when a screen wakes (ContactsApp.onContactAdded, made in the Contacts app's Awake). The
// checks hold: nothing in the mod reads a thing that tells one way of loading from another; every entry point the game runs for the city is
// the mod's city entry (an intro entry holds nothing a city needs); a load-time install never gives up on such a static being null (it makes it,
// or tries again); and the season under the date does not depend on Alcware Seasons being loaded.
internal static partial class Program
{
    // What tells one way of loading from another: the game's own record of which save is to be loaded and how, and the scope it loads in.
    private static readonly string[] PathMarks =
    {
        "TransitionToSave.", "saveToLoadData", "saveGameLoadErrored", "LastSaveGameName", "continueButton", "ModActivationScope", "LoadGame.Load(", "SaveGameStruct", "IntroToGameTransition"
    };

    private static void LoadPaths()
    {
        string scripts = System.IO.Path.GetFullPath(System.IO.Path.Combine(LocalesFolder(), "..", "Scripts"));
        var sources = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (string file in System.IO.Directory.GetFiles(scripts, "*.cs", System.IO.SearchOption.AllDirectories))
        {
            sources[System.IO.Path.GetRelativePath(scripts, file).Replace('\\', '/')] = System.IO.File.ReadAllText(file);
        }

        // 1. Nothing in the mod can tell Continue from Load (or from the game's other ways into a city): none of it names what would.
        string told = null;
        foreach (KeyValuePair<string, string> source in sources)
        {
            if (source.Key == "Core/CityRules.cs")
            {
                continue;   // the names of the scenes that are not the city, as text
            }
            foreach (string mark in PathMarks)
            {
                if (told == null && source.Value.Contains(mark))
                {
                    told = source.Key + " names '" + mark + "'";
                }
            }
        }
        Check(told == null, "load paths: nothing in the mod tells Continue from Load, or any way into a city from another" + (told != null ? " (" + told + ")" : string.Empty));

        // 2. The entry points the game runs, by scope: a city entry runs for every city, an intro entry only for a new character's creation, so
        // an intro entry's setup that a city needs is repeated in the city entry (Continue and Load never go through the intro).
        string quality = sources["Character/CharacterQuality.cs"];
        string cityEntry = Between(quality, "public class CharacterQualityCity", "public class CharacterQualityIntro");
        string introEntry = Between(quality, "public class CharacterQualityIntro", "public class CharacterQuality :");
        var shared = new List<string>();
        foreach (Match m in Regex.Matches(introEntry, @"^\s*([A-Za-z]+\.(?:Logger = context\.Logger|Begin\(context\)))\s*;", RegexOptions.Multiline))
        {
            shared.Add(m.Groups[1].Value);
        }
        string missing = null;
        foreach (string line in shared)
        {
            if (!cityEntry.Contains(line + ";"))
            {
                missing = missing ?? line;
            }
        }
        Check(shared.Count >= 4 && missing == null, "load paths: what the character-creation entry sets up for the game's characters (" + shared.Count + " step(s)) is also set up by the city entry, which Continue and Load both run"
                                                   + (missing != null ? " (missing in the city entry: " + missing + ")" : string.Empty));
        int cityEntries = 0, introEntries = 0, initEntries = 0;
        string introWork = null;
        foreach (KeyValuePair<string, string> source in sources)
        {
            cityEntries += Regex.Matches(source.Value, @"\[ModEntryOnCityLoad\]").Count;
            initEntries += Regex.Matches(source.Value, @"\[ModEntryOnInitializationLoad\]").Count;
            int intro = Regex.Matches(source.Value, @"\[ModEntryOnIntroLoad\]").Count;
            introEntries += intro;
            if (intro > 0 && source.Key != "Character/CharacterQuality.cs")
            {
                introWork = source.Key;   // any other intro entry is new: it must be looked at for what a city needs of it
            }
        }
        Check(cityEntries == 2 && initEntries == 2 && introEntries == 1 && introWork == null,
            "load paths: the mod's entry points are the known ones (" + initEntries + " at the game's start, " + cityEntries + " for each city, " + introEntries + " for character creation"
            + (introWork != null ? "; a new intro entry in " + introWork : string.Empty) + "): a new one is judged for what it leaves out of a loaded city");

        // 3. The once-per-load install in LifeEvents puts the phone's contacts and then their groups in, behind the one gate every load goes through.
        string life = sources["Life/LifeEvents.cs"];
        int gateAt = life.IndexOf("if (!ModState.Loaded)", StringComparison.Ordinal);
        int setupAt = life.IndexOf("PhoneContacts.Setup();", StringComparison.Ordinal), groupsAt = life.IndexOf("ContactGroupsGame.Install();", StringComparison.Ordinal);
        Check(gateAt > 0 && setupAt > gateAt && groupsAt > setupAt,
            "load paths: the phone's contacts and then their groups are put in once at every load, behind the one gate every load goes through (ModState.Loaded)");

        // 4. A game static the game makes when a screen wakes is null at the first city of a session. The Contacts app's two events are made by the
        // groups' install and never waited for; the only other reader that compares them with null tries again every second.
        string groups = sources["Phone/Contacts/ContactGroupsGame.cs"];
        string newBuildings = sources["NewBuildings/NewBuildingsDirector.cs"];
        var nullReaders = new List<string>();
        foreach (KeyValuePair<string, string> source in sources)
        {
            if (Regex.IsMatch(source.Value, @"ContactsApp\.onContact(Added|Removed)\s*(==|!=)\s*null"))
            {
                nullReaders.Add(source.Key);
            }
        }
        nullReaders.Sort(StringComparer.Ordinal);
        Check(string.Join(",", nullReaders) == "NewBuildings/NewBuildingsDirector.cs,Phone/Contacts/ContactGroupsGame.cs",
            "load paths: only the groups' install and the made buildings' contact hook look at whether the Contacts app's events are null yet (" + string.Join(", ", nullReaders) + ")");
        Check(Regex.IsMatch(newBuildings, @"private void Slow\(float now\)[\s\S]*?HookContacts\(\);"), "load paths: the made buildings' contact hook is tried again every second until the Contacts app's events are there");
        Check(groups.Contains("ContactsApp.onContactAdded = new UnityEvent();") && groups.Contains("ContactsApp.onContactRemoved = new UnityEvent();")
              && !Regex.IsMatch(Between(groups, "public static void Install()", "private static int Events()"), @"if\s*\([^)]*onContact(Added|Removed)[^)]*==\s*null"),
            "load paths: the groups' install makes the Contacts app's events when the game has not, and does not give the groups up when they are null");

        // 5. The install reports itself in the log at every load, whichever way the save came in, so a Player.log says what each load did.
        Check(groups.Contains("Contacts check (groups, load "), "load paths: the groups' install says in the log, for each load of a session, what the row and the contacts came to");
    }

    // ---------------------------------------------------------------- the season under the date

    private static void SeasonBarIndependence()
    {
        // The rule: the picture shows when the seasons and its own option are on and no other mod's season is under the date; whether that mod is loaded is not asked.
        bool table = true;
        foreach (bool seasons in new[] { false, true })
        {
            foreach (bool bar in new[] { false, true })
            {
                foreach (bool other in new[] { false, true })
                {
                    table &= SeasonRules.BarShows(seasons, bar, other) == (seasons && bar && !other);
                }
            }
        }
        Check(table && SeasonRules.BarShows(true, true, false) && !SeasonRules.BarShows(true, true, true) && !SeasonRules.BarShows(false, true, false) && !SeasonRules.BarShows(true, false, false),
            "season bar: the season under the date shows when the seasons and its option are on and no other mod's season is under it, in all eight cases");
        // Alcware Seasons loaded and not kept beside: the rest of the seasons stand down, the picture does not (it shows while Alcware has none under the date:
        // its options off, or the mod switched off in a session that had loaded it).
        Check(SeasonRules.StandsDown(true, false) && SeasonRules.BarShows(true, true, false), "season bar: with Alcware Seasons loaded and the seasons standing down, the season under the date still shows while Alcware's own is not there");
        Check(!SeasonRules.BarShows(true, true, true), "season bar: it gives way to Alcware's own season under the date, so there are never two");

        string folder = System.IO.Path.GetFullPath(System.IO.Path.Combine(LocalesFolder(), "..", "Scripts", "Seasons"));
        string bar2 = System.IO.File.ReadAllText(System.IO.Path.Combine(folder, "SeasonBar.cs"));
        string director = System.IO.File.ReadAllText(System.IO.Path.Combine(folder, "SeasonDirector.cs"));
        string look = System.IO.File.ReadAllText(System.IO.Path.Combine(folder, "SeasonDirector.Look.cs"));
        Check(!bar2.Contains("OtherMods.") && !bar2.Contains("StandingDown") && !bar2.Contains("SeasonBesideAlcware") && !bar2.Contains(".Loaded") && bar2.Contains("SeasonRules.BarShows(") && bar2.Contains("\"Alcware season indicator\""),
            "season bar: the picture never asks whether Alcware Seasons is loaded or kept beside; it looks for Alcware's own piece under the date and asks the rule");
        string slow = Between(director, "private void Slow(float now)", "/// <summary>Snow falls or lies on the roads");
        int standAt = slow.IndexOf("StandDown()", StringComparison.Ordinal), aloneAt = slow.IndexOf("BarAlone();", StringComparison.Ordinal), onAt = slow.IndexOf("_on = true;", StringComparison.Ordinal);
        Check(standAt > 0 && aloneAt > standAt && aloneAt < onAt, "season bar: while the seasons stand down for Alcware, the director still shows the season under the date (BarAlone) before it returns");
        string alone = Between(look, "private void BarAlone()", "// The season and its day under the date, written");
        Check(alone.Contains("Bar(day, year,") && !alone.Contains("StandDown") && !alone.Contains("OtherMods") && alone.Contains("Settings.Seasons"),
            "season bar: the stand-down's own path to the picture follows the seasons' master option only, never Alcware");
        Check(Between(look, "private bool StandDown()", "private void Looks(").Contains("reallife_season_alcware") && look.Contains("OtherMods.AlcwareSeasons.Loaded"),
            "season bar: the rest of the seasons keep standing down for Alcware (the notice and the log line are kept)");
    }
}
