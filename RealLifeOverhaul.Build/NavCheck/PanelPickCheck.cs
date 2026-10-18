using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using RealLifeOverhaul.GameUi;

// Who has the game's one panel box over a place when the player is near two places that want it
// (Core/GameUi/PanelPickRules.cs): the nearer place the player faces, a person met first within talking range, a
// margin so the box never goes to and fro, the game's own panels left where they are, the key to what is shown, and
// a person waiting to be met stood clear of other panels' places. And over the sources, every panel of the mod's goes
// through the rule.
internal static partial class Program
{
    private static void PanelPicks()
    {
        PanelPickNearer();
        PanelPickSteady();
        PanelPickMeet();
        PanelPickKey();
        PanelPickSpot();
        PanelPickTimed();
        PanelPickSources();
    }

    private static PanelClaim Ahead(float metres, float degrees, bool meet = false)
    {
        return new PanelClaim(metres, (float)Math.Cos(degrees * Math.PI / 180.0), meet, false);
    }

    private static void PanelPickNearer()
    {
        // The case seen in play: a subway entrance's panel 6 m off has the box; a door's panel 2 m ahead asks for it.
        Check(PanelPickRules.Takes(Ahead(6f, 0f), Ahead(2f, 0f)), "panel pick: a place 2 m ahead takes the box from one 6 m ahead");
        Check(!PanelPickRules.Takes(Ahead(2f, 0f), Ahead(6f, 0f)), "panel pick: a place 6 m off does not take the box from one 2 m off");

        // Facing: at the same distance the one ahead wins, and it takes the box from the one behind.
        Check(PanelPickRules.Takes(Ahead(3f, 180f), Ahead(3f, 0f)), "panel pick: of two places 3 m off, the one ahead takes the box from the one behind");
        Check(!PanelPickRules.Takes(Ahead(3f, 0f), Ahead(3f, 180f)), "panel pick: a place behind does not take the box from one ahead at the same distance");
        Check(Near(PanelPickRules.Counted(Ahead(3f, 180f)), 6f) && Near(PanelPickRules.Counted(Ahead(3f, 90f)), 4.5f) && Near(PanelPickRules.Counted(Ahead(3f, 0f)), 3f),
            "panel pick: a place straight behind counts twice its distance, to the side one and a half times");

        // Turning to the other place gives it the box: the player turns from a stop ahead (2.5 m) to a kiosk (2.5 m) behind.
        PanelClaim stop = Ahead(2.5f, 0f), kiosk = Ahead(2.5f, 180f);
        Check(!PanelPickRules.Takes(stop, kiosk), "panel pick: the stop ahead keeps the box over the kiosk behind");
        PanelClaim stopTurned = Ahead(2.5f, 180f), kioskTurned = Ahead(2.5f, 0f);
        Check(PanelPickRules.Takes(stopTurned, kioskTurned), "panel pick: once the player turns round, the kiosk now ahead takes the box");

        // Edges: at the place itself facing is not asked; a nonsense distance or facing is held in range.
        Check(PanelPickRules.Counted(Ahead(0f, 180f)) == 0f && Near(PanelPickRules.Counted(Ahead(0.2f, 180f)), 0.2f),
            "panel pick: standing at the place (0.3 m or less) counts the distance alone, whichever way the player faces");
        Check(PanelPickRules.Counted(new PanelClaim(-2f, 1f, false, false)) == 0f && Near(PanelPickRules.Counted(new PanelClaim(2f, -5f, false, false)), 4f)
              && Near(PanelPickRules.Counted(new PanelClaim(2f, 5f, false, false)), 2f), "panel pick: a negative distance counts 0, a facing beyond -1 or 1 as -1 or 1");

        // The game's own panels (a pump, a lift) are shown while the player stands in them: never taken, even by a person met.
        var game = new PanelClaim(0f, 1f, false, true);
        Check(!PanelPickRules.Takes(game, Ahead(0.5f, 0f)) && !PanelPickRules.Takes(game, Ahead(1f, 0f, true)),
            "panel pick: the game's own panel keeps the box, from a place 0.5 m ahead and from a person met");
        Check(PanelPickRules.Counted(game) == 0f, "panel pick: the game's own panel counts as nearest");
        Check(PanelPickRules.Takes(Ahead(4f, 0f), Ahead(1f, 0f)) == PanelPickRules.Takes(Ahead(4f, 0f), Ahead(1f, 0f)), "panel pick: the same claims give the same answer");
    }

    private static void PanelPickSteady()
    {
        // The margin: 0.75 m, or a fifth of the held count.
        Check(Near(PanelPickRules.Margin(2f), 0.75f) && Near(PanelPickRules.Margin(10f), 2f) && Near(PanelPickRules.Margin(0f), 0.75f),
            "panel pick: the margin is 0.75 m, or a fifth of the held count when that is more");
        Check(!PanelPickRules.Takes(Ahead(3f, 0f), Ahead(2.5f, 0f)) && PanelPickRules.Takes(Ahead(3f, 0f), Ahead(2.2f, 0f)),
            "panel pick: 0.5 m nearer is not enough to take the box, 0.8 m nearer is");

        // Never both ways: over every pair of claims in a grid, at most one of the two takes the box from the other.
        bool both = false;
        for (float a = 0f; a <= 12f; a += 0.25f)
        {
            for (float b = 0f; b <= 12f; b += 0.25f)
            {
                for (int fa = 0; fa <= 180; fa += 30)
                {
                    for (int fb = 0; fb <= 180; fb += 30)
                    {
                        PanelClaim one = Ahead(a, fa), two = Ahead(b, fb);
                        both |= PanelPickRules.Takes(one, two) && PanelPickRules.Takes(two, one);
                    }
                }
            }
        }
        Check(!both, "panel pick: of any two places, at most one can take the box from the other: it never goes to and fro while the player stands still");

        // The player walks from a stop to a subway entrance 8 m on, swaying a little at every step: the box changes hands once.
        int changes = 0;
        bool stopHas = true;
        for (int step = 0; step <= 80; step++)
        {
            float walked = step * 0.1f;
            float sway = (step % 2 == 0 ? 1f : -1f) * 0.15f;
            PanelClaim toStop = Ahead(Math.Abs(walked + sway), 180f - 10f);
            PanelClaim toSubway = Ahead(Math.Abs(8f - walked - sway), 10f);
            if (stopHas && PanelPickRules.Takes(toStop, toSubway))
            {
                stopHas = false;
                changes++;
            }
            else if (!stopHas && PanelPickRules.Takes(toSubway, toStop))
            {
                stopHas = true;
                changes++;
            }
        }
        Check(changes == 1 && !stopHas, "panel pick: walking from a stop to a subway entrance with a sway at each step, the box changes hands once (" + changes + ")");

        // Standing still halfway between the two, side on to both, swaying 15 cm and 4 degrees either way: the box stays where it is.
        changes = 0;
        stopHas = true;
        for (int step = 0; step < 200; step++)
        {
            float sway = step % 2 == 0 ? 1f : -1f;
            PanelClaim toStop = Ahead(4f + sway * 0.15f, 90f + sway * 4f);
            PanelClaim toSubway = Ahead(4f - sway * 0.15f, 90f - sway * 4f);
            if (stopHas ? PanelPickRules.Takes(toStop, toSubway) : PanelPickRules.Takes(toSubway, toStop))
            {
                stopHas = !stopHas;
                changes++;
            }
        }
        Check(changes == 0, "panel pick: halfway between two places, swaying on the spot never moves the box (" + changes + " changes)");
    }

    private static void PanelPickMeet()
    {
        // The report: the friend 3 m off beside a subway entrance whose panel is 2 m from the player has the box.
        Check(PanelPickRules.Takes(Ahead(2f, 0f), Ahead(3f, 120f, true)), "panel pick: a person met 3 m off, even behind, takes the box from a subway entrance 2 m ahead");
        Check(PanelPickRules.Counted(Ahead(PanelPickRules.TalkRange, 180f, true)) == 0f, "panel pick: at talking range's edge (3.5 m) a person met counts as nearest");
        Check(!PanelPickRules.Takes(Ahead(1f, 0f), Ahead(3.6f, 0f, true)), "panel pick: beyond talking range a person met is counted as any place");
        Check(!PanelPickRules.Takes(Ahead(3f, 0f, true), Ahead(0.2f, 0f)), "panel pick: a person met in talking range keeps the box over a place the player stands at");
        Check(PanelPickRules.TalkRange >= 3.4f, "panel pick: talking range reaches as far as an outing's hello is offered (3.4 m)");
    }

    // The game sends the key to the box's own panel alone; a panel's button runs only while it is in the box, and the mod's own key yields to it.
    private static void PanelPickKey()
    {
        Check(PanelPickRules.KeyToPanel(true, true, true), "panel key: the panel in the box runs its button");
        Check(!PanelPickRules.KeyToPanel(false, true, true), "panel key: a panel put out of the box never runs its button");
        Check(!PanelPickRules.KeyToPanel(true, false, true) && !PanelPickRules.KeyToPanel(true, true, false), "panel key: no button, or a dimmed one, runs nothing");
        Check(!PanelPickRules.KeyFree(true, true), "panel key: the mod's own key does nothing while the box shows a button on it");
        Check(PanelPickRules.KeyFree(true, false) && PanelPickRules.KeyFree(false, false) && PanelPickRules.KeyFree(false, true),
            "panel key: the mod's own key is free with nothing in the box, or a box with no button on the key (a subway entrance's line of times)");

        // One press near a subway entrance's panel, a person met and a door: exactly one thing runs, the one in the box.
        PanelClaim subway = Ahead(2f, 0f), friend = Ahead(3f, 60f, true);
        bool friendHas = PanelPickRules.Takes(subway, friend);
        int ran = 0;
        ran += PanelPickRules.KeyToPanel(!friendHas, false, true) ? 1 : 0;   // the subway entrance's panel: no button
        ran += PanelPickRules.KeyToPanel(friendHas, true, true) ? 1 : 0;     // the friend's "Say hello"
        ran += PanelPickRules.KeyFree(true, friendHas) ? 1 : 0;              // the mod's own key at the door nearby
        Check(friendHas && ran == 1, "panel key: beside a subway entrance and a door, one press says hello and does nothing else (" + ran + " ran)");
    }

    private static void PanelPickSpot()
    {
        float[] busyX = { 0f, 20f }, busyZ = { 0f, 0f }, busyReach = { 7.5f, 3f };
        // Places to stand, nearest the door first: inside the subway entrance's reach, at its edge, clear.
        float[] x = { 3f, 7.8f, 9f, 12f }, z = { 0f, 0f, 0f, 0f };
        Check(PanelPickRules.Pick(x, z, 4, busyX, busyZ, busyReach, 2) == 2, "panel spot: the first place 0.6 m or more outside every reach is taken (9 m, not 3 m or 7.8 m)");
        Check(Near(PanelPickRules.Room(9f, 0f, busyX, busyZ, busyReach, 2), 1.5f) && Near(PanelPickRules.Room(3f, 0f, busyX, busyZ, busyReach, 2), -4.5f),
            "panel spot: room is metres outside the nearest reach, negative inside it");
        float[] tightX = { 1f, 5f, 6f }, tightZ = { 0f, 0f, 0f };
        Check(PanelPickRules.Pick(tightX, tightZ, 3, busyX, busyZ, busyReach, 2) == 2, "panel spot: with no clear place, the roomiest is taken (6 m in, 1.5 m inside the reach)");
        Check(PanelPickRules.Pick(x, z, 0, busyX, busyZ, busyReach, 2) == -1, "panel spot: no place to stand gives none");
        Check(PanelPickRules.Pick(x, z, 4, busyX, busyZ, busyReach, 0) == 0, "panel spot: with no other panel near, the place nearest the door is taken");
        Check(PanelPickRules.Room(5f, 5f, busyX, busyZ, busyReach, 0) == float.MaxValue, "panel spot: with no reaches there is all the room there is");
        float[] sameX = { 1f, 1f }, sameZ = { 0f, 0f };
        Check(PanelPickRules.Pick(sameX, sameZ, 2, busyX, busyZ, busyReach, 2) == 0, "panel spot: of two places with the same room the earlier is taken");
    }

    private static void PanelPickTimed()
    {
        long ticks = TimePanelPicks(out int taken);
        double ms = ticks * 1000.0 / Stopwatch.Frequency;
        Check(taken > 0 && ms < 50.0, "panel pick: 200,000 contests in " + ms.ToString("F1") + " ms (wanted under 50 ms; the mod asks a few times a second)");
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.NoInlining)]
    private static long TimePanelPicks(out int taken)
    {
        taken = 0;
        Stopwatch watch = Stopwatch.StartNew();
        for (int i = 0; i < 200000; i++)
        {
            var holder = new PanelClaim((i % 97) * 0.1f, ((i % 13) - 6) / 6f, (i & 7) == 0, false);
            var asking = new PanelClaim((i % 89) * 0.1f, ((i % 11) - 5) / 5f, (i & 5) == 0, false);
            taken += PanelPickRules.Takes(holder, asking) ? 1 : 0;
        }
        watch.Stop();
        return watch.ElapsedTicks;
    }

    // Over the mod's sources: every panel over a place goes through the rule.
    private static void PanelPickSources()
    {
        string root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Scripts"));
        string Read(string relative) => File.Exists(Path.Combine(root, relative)) ? Code(File.ReadAllText(Path.Combine(root, relative))) : string.Empty;   // a file gone fails its check, never the checker

        string panel = Read(@"Core\GameUi\WorldPanel.cs");
        string pick = Read(@"Core\GameUi\WorldPanel.Pick.cs");
        Check(panel.Contains("OverlayUI.IsVisible && !Contest(box)") && pick.Contains("PanelPickRules.Takes(theirs, mine)"),
            "panel sources: WorldPanel.Show asks the rule before it takes a box another panel has");
        int rule = panel.IndexOf("!Contest(box)", StringComparison.Ordinal);
        Check(Regex.Matches(panel, @"OverlayUI\.Show\(this\)").Count == 2 && rule >= 0 && rule < panel.LastIndexOf("OverlayUI.Show(this)", StringComparison.Ordinal),
            "panel sources: WorldPanel puts itself in the box only after the rule, or to rewrite what it already shows");

        string door = Read(@"Core\GameUi\DoorPanel.cs");
        string job = Read(@"Jobs\JobPanel.cs");
        Check(door.Contains("Panel.For(place);"), "panel sources: every DoorPanel is measured from its own place");
        Check(Regex.Matches(job, @"_panel\.For\(").Count == 2, "panel sources: both of JobPanel's Shows give the rule the place itself");

        // Every panel made by a feature has a name for the log and gives the rule its place before every Show.
        var made = new Regex(@"(?:GameUi\.)?WorldPanel\s+(_\w+)\s*=\s*new\s+(?:GameUi\.)?WorldPanel\s*(\{[^}]*\})?");
        var wrong = new List<string>();
        int panels = 0, shows = 0;
        foreach (string file in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
        {
            string relative = file.Substring(root.Length + 1);
            if (relative.StartsWith(@"Core\GameUi\", StringComparison.Ordinal) || relative == @"Jobs\JobPanel.cs")
            {
                continue;
            }
            string code = Code(File.ReadAllText(file));
            if (code.Contains("OverlayUI.Show(") && relative != @"Garage\GarageBay.cs")
            {
                wrong.Add(relative + " puts something in the game's box itself");
            }
            foreach (Match m in made.Matches(code))
            {
                panels++;
                string field = m.Groups[1].Value;
                string named = m.Groups[2].Value;
                if (!named.Contains("Name =") && !code.Contains(field + ".Name ="))
                {
                    wrong.Add(relative + " " + field + " has no name");
                }
                string stem = Path.GetFileNameWithoutExtension(file).Split('.')[0];
                foreach (string part in Directory.GetFiles(Path.GetDirectoryName(file), stem + "*.cs"))
                {
                    string partCode = Code(File.ReadAllText(part));
                    int calls = Regex.Matches(partCode, Regex.Escape(field) + @"\.Show\(\)").Count;
                    shows += calls;
                    if (calls > 0 && !partCode.Contains(field + ".For(") && !partCode.Contains(field + ".Follow ="))
                    {
                        wrong.Add(Path.GetFileName(part) + " shows " + field + " without its place");
                    }
                }
            }
        }
        Check(panels >= 9 && shows >= 9 && wrong.Count == 0, "panel sources: all " + panels + " panels made by features (" + shows + " Shows) are named and measured from their place, and"
                                                       + " only WorldPanel and the filling station's own panel put anything in the game's box"
                                                       + (wrong.Count > 0 ? "; not: " + string.Join(", ", wrong) : string.Empty));

        // The owners named in the report, each through the rule.
        Check(Read(@"Garage\TwoWheelers.cs").Contains("_world.For(place.Position);"), "panel sources: the bike places' panel gives the rule its place");
        Check(Read(@"Home\HomeDirector.Offers.cs").Contains("_doctor.For(_doctorAt);"), "panel sources: the doctor's desk panel gives the rule the desk");
        Check(Read(@"Jobs\Kinds\VendorJob.Screen.cs").Contains("_panel.For(_cartAt);"), "panel sources: the drinks cart's panel gives the rule the cart");
        Check(!Read(@"Transport\TransitDirector.Player.cs").Contains("_subway.Showing"), "panel sources: a stop's panel no longer stands aside for a subway entrance's by its own rule");

        // The outing: the person met first within talking range, a place to wait clear of other panels, and the question box after a few seconds.
        string social = Read(@"Social\SocialDirector.Ui.cs");
        Check(social.Contains("panel.Meet = over != null;"), "panel sources: the panel over the person met is marked so");
        Check(social.Contains("GameConfirm.Show(prompt,") && social.Contains("AskAfter"), "panel sources: an outing's hello is asked in the game's question box when the panel cannot be had");
        Check(Read(@"Social\OutingScene.Meeting.cs").Contains("SocialPlaces.StandingClear(_venue.Front"), "panel sources: whoever waits to be met is stood clear of other panels' places");
        int finders = Regex.Matches(Read(@"Transport\SubwayJourney.cs") + Read(@"Transport\TransitDirector.Player.cs") + Read(@"Garage\TwoWheelers.cs") + Read(@"Ledger\LedgerKiosks.cs"),
            @"PanelPlaces\.Set\(").Count;
        Check(finders == 4, "panel sources: the subway entrances, stops, bike places and kiosks each name their places (" + finders + " of 4)");

        // The key: the mod's own interact key yields to a button in the box.
        Check(Read(@"World\NearbyInteraction.cs").Contains("WorldPanel.InteractFree()"), "panel sources: the mod's own interact key yields to the button the box shows");

        // The text and What's new, in every row (the release moves the next row's lines into a numbered one).
        string locales = Path.GetFullPath(Path.Combine(root, @"..\Locales"));
        foreach (string lang in new[] { "en", "pt", "zh-cn" })
        {
            Check(File.ReadAllText(Path.Combine(locales, lang + ".json")).Contains("\"reallife_social_hello_ask\":"), "panel texts: the hello question in " + lang + ".json");
        }
        bool told = false;
        foreach (string line in File.ReadAllLines(Path.Combine(locales, "en.json")))
        {
            told |= line.TrimStart().StartsWith("\"reallife_changes_", StringComparison.Ordinal) && line.Contains("Fixed: the person you came to meet now always gets the panel to say hello");
        }
        Check(told, "panel texts: What's new says the person met now always gets the panel");
    }
}
