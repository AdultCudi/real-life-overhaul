using System;
using System.IO;

// Checks, over the mod's sources as they ship, that the one-off work of the first seconds after a load stays where it
// costs nothing to see: behind the loading screen (Core/SceneFinds.cs, ScenePrewarm), or read from the game's own lists.
// The owner's logs of 14 Oct 2026 had a frame of 1381 ms just after a save loaded, in which the landmarks (six
// searches of the whole scene), the scooter stands, the skies (two) and the street lamps were each looked for, a frame
// of 196 ms after it in which the far districts gathered every renderer of the city, and a frame of some 135 ms on the
// first drive of each session, in which Empire Motors' view was looked for by a search of the scene. Each is now handed
// to ScenePrewarm by the feature that needs it, or read from a list the game keeps; these checks fail if one comes back.
//
// What is not here: how long anything takes in the game, which the "Session: a frame of ..." and "Scene search check
// (...)" lines of Player.log show ("behind the loading screen ...: PASS").
internal static partial class Program
{
    private static void Prewarms()
    {
        string scripts = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Scripts"));
        if (!Directory.Exists(scripts))
        {
            Check(false, "prewarm: the mod's sources are at " + scripts);
            return;
        }

        Handed(scripts, @"Leisure\LeisureDirector.cs", "LeisureVenues.PrewarmLandmarks", "the city's landmarks");
        Handed(scripts, @"Leisure\Sound\LeisureSound.cs", "PrewarmLoops", "the ambient sound's loops");
        Handed(scripts, @"Garage\GarageDirector.cs", "_bikes.Places.PrewarmStands", "the scooter stands of the bike-share docks");
        Handed(scripts, @"Performance\PerfDirector.cs", "PerfTweaks.Prewarm", "the performance tweaks' looks at the scene");
        Handed(scripts, @"Camera\ChaseCamera.cs", "EmpireView.Prewarm", "Empire Motors' view from the driver's seat");

        string venues = Source(scripts, @"Leisure\LeisureVenues.cs");
        string find = Body(venues, "public static void FindLandmarks()");
        Check(find.Length > 0 && !find.Contains("SceneFinds.") && find.Contains("SceneLandmarks()"), "prewarm: the landmarks found on the street make no search of the scene of their own (the searches are SceneLandmarks')");
        Check(Body(venues, "public static bool PrewarmLandmarks()").Contains("SceneLandmarks()"), "prewarm: behind the loading screen the landmarks' six searches are made");

        string lamps = Source(scripts, @"Performance\Tweaks\StaleLampLights.cs");
        Check(lamps.Length > 0 && !lamps.Contains("SceneFinds.") && lamps.Contains("PerfGame.Lamps("), "prewarm: the street lamps come from the game's own culling list, never a search of the scene");
        Check(Source(scripts, @"Performance\PerfGame.cs").Contains("GetField(\"_cullables\""), "prewarm: the game's culling list is read through its field, looked up once");

        string sky = Source(scripts, @"Performance\Tweaks\SkyThrottle.cs");
        Check(Body(sky, "protected override void Prewarm()").Contains("SceneFinds.All<Volume>") && Body(sky, "private void Sweep()").Contains("kept ?"), "prewarm: the skies are searched for behind the loading screen and the switch-on uses what was found");

        string far = Source(scripts, @"Performance\Tweaks\FarDistricts.cs");
        Check(Body(far, "protected override void Prewarm()").Contains("Refresh()"), "prewarm: the far districts gather the scenery behind the loading screen");

        string tweak = Source(scripts, @"Performance\PerfTweak.cs");
        Check(Body(tweak, "internal void RunPrewarm(").Contains("ShouldBeActive"), "prewarm: only a tweak that is wanted looks at the scene behind the loading screen");

        string bikes = Source(scripts, @"Garage\BikePlaces.cs");
        Check(Body(bikes, "public void Find()").Contains("_stands ??"), "prewarm: the bike-share docks are placed from the stands found behind the loading screen");

        string empire = Source(scripts, @"Camera\EmpireView.cs");
        string look = Body(empire, "private static Behaviour Find()");
        int listened = look.IndexOf("Listening()", StringComparison.Ordinal);
        int searched = look.IndexOf("SceneFinds.Any(", StringComparison.Ordinal);
        Check(!empire.Contains("FindObjectOfType") && listened >= 0 && searched > listened, "prewarm: Empire Motors' view is found in the game's own list of listeners first, and searched for only through SceneFinds after that");
    }

    // The feature's file hands the named piece of work to ScenePrewarm.
    private static void Handed(string scripts, string file, string work, string what)
    {
        bool handed = false;
        foreach (string line in Source(scripts, file).Split('\n'))
        {
            handed |= line.Contains("ScenePrewarm.Add(") && line.Contains(", " + work + ")");
        }
        Check(handed, "prewarm: " + what + " handed to ScenePrewarm (" + file + ")");
    }

    // A file of the mod with its comments taken out (BuildCheck's Code), or nothing if it is not there.
    private static string Source(string scripts, string file)
    {
        string path = Path.Combine(scripts, file);
        return File.Exists(path) ? Code(File.ReadAllText(path)) : string.Empty;
    }

    // The body of the member that starts with this text, braces matched; nothing if it is not there.
    private static string Body(string code, string start)
    {
        int at = code.IndexOf(start, StringComparison.Ordinal);
        if (at < 0)
        {
            return string.Empty;
        }
        int open = code.IndexOf('{', at);
        if (open < 0)
        {
            return string.Empty;
        }
        int depth = 0;
        for (int i = open; i < code.Length; i++)
        {
            if (code[i] == '{')
            {
                depth++;
            }
            else if (code[i] == '}' && --depth == 0)
            {
                return code.Substring(open, i - open + 1);
            }
        }
        return string.Empty;
    }
}
