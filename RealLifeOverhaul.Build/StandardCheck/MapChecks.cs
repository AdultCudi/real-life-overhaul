using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

// docs/standards/performance.md, RLO-PERF-022: what the mod changes for the city map is put back as the map shuts, and the
// map check can see that it was. The game's map draws the whole city from above and pauses the game, so the mod's parts give
// things back for it (the far districts' squares, the mod's far things, the draw distances, a copied building's detail) and
// must take them out again at once after it: the far districts left some 250,000 renderers drawn for 7 to 9 seconds after
// every map (0.61.0 beta 4), a cost no record saw, as the session record leaves paused frames out. Every handler on the
// map's events (onCityMapToggle, onCityMapClosed) is listed with the name of its part's look (Core/MapParts), which the map
// check of the session record asks after the map shut (Diagnostics/SessionWatch.Map), or with no part and the reason it
// leaves nothing to put back. A handler on the map's events that is not listed fails the check: list it, and if it changes
// anything for the map, give its part a look. The bodies of the listed handlers hold no search of the scene, no rebuilt
// layout and no disk read.
internal static partial class Program
{
    private const string MapRule = "RLO-PERF-022";

    // The file that puts the handler on the map's events, the file and method that is the handler, the name its part's look
    // is added under (MapParts.Add), or null where it changes nothing that must be put back, and why. The number of
    // subscriptions in a file must be the number of entries naming it.
    private static readonly (string SubscribedIn, string File, string Method, string Part, string Why)[] MapHandlers =
    {
        ("Core/MapTreeGuard.cs", "Core/MapTreeGuard.cs", "OnMapToggled", null,
            "takes trees the mod destroyed out of the game's list before the map reads it, and tells the session record; nothing is changed for the map"),
        ("Core/MapPlaces.Driver.cs", "Core/MapPlaces.cs", "OnMap", "map places",
            "the mod's markers and lit buildings as for the open map, and as in the street again once it shuts"),
        ("Core/SpecialNames.cs", "Core/SpecialNames.cs", "Search", null,
            "the mod's buildings' names in the open map's search list, which the map takes away with it; nothing is left to put back"),
        ("Districts/DistrictView.cs", "Districts/DistrictView.cs", "OnMap", "district view",
            "a district layer shown on the open map, taken off with its frame work once it shuts"),
        ("Navigation/NavDirector.cs", "Navigation/NavDirector.Map.cs", "OnMap", "route on the map",
            "the route drawn on the open map, its object off once it shuts"),
        ("NewBuildings/CopyLook.cs", "NewBuildings/CopyLook.cs", "OnMap", "copied buildings",
            "a copied building drawn as its box on the map, its detail drawn again once it shuts"),
        ("Performance/ModCulling.cs", "Performance/ModCulling.cs", "MapToggled", "the mod's far things",
            "the mod's far things drawn on the open map and hidden again after it, 64 a frame until none waits"),
        ("Performance/ModCulling.cs", "Performance/ModCulling.cs", "MapClosed", "the mod's far things",
            "the same once the map's camera is back down, should the shutting have been missed"),
        ("Performance/Tweaks/FarDistricts.cs", "Performance/Tweaks/FarDistricts.Map.cs", "OnMapToggled", "far squares",
            "notes what is out as the map opens; a square comes back, lent, in the frame the map's camera first sees it"),
        ("Performance/Tweaks/FarDistricts.cs", "Performance/Tweaks/FarDistricts.Map.cs", "Back", "far squares",
            "the map's camera is back down (onCityMapClosed): the very renderers lent to it are taken out again, 2 ms a frame"),
        ("Performance/Tweaks/FarPropCull.cs", "Performance/Tweaks/FarPropCull.cs", "Reconcile", "draw distances",
            "the camera's own draw distances while the map is open, the options' again as it shuts"),
        ("Performance/Tweaks/FarPropCull.cs", "Performance/Tweaks/FarPropCull.cs", "Reconcile", "draw distances",
            "the same once the map's camera is back down (onCityMapClosed)"),
        ("Performance/Tweaks/SkyThrottle.cs", "Performance/Tweaks/SkyThrottle.cs", "Refresh", null,
            "asks the pipeline for one fresh sky as the map opens and as it shuts; nothing is left behind"),
        ("Transport/Taxi/TaxiDirector.cs", "Transport/Taxi/TaxiDirector.cs", "OnMap", null,
            "notes the taxi the map was opened for, if a ride is under way; nothing is changed for the map"),
        ("Uptown/SideWalls.cs", "Uptown/SideWalls.cs", "OnMap", "side walls",
            "the windows stand aside while the map shows each building's box, drawn again once it shuts")
    };

    private static readonly Regex MapSubscription = new Regex(
        @"EventHold\.(?:Keep|First)\s*\(\s*ref\s+GlobalEvents\.onCityMap(?:Toggle|Closed)\b|GlobalEvents\.onCityMap(?:Toggle|Closed)\s*\+="
        + @"|Delegate\.Combine\s*\(\s*(?:[\w.]+\s*,\s*)?GlobalEvents\.onCityMap(?:Toggle|Closed)\b", RegexOptions.Compiled);

    private static readonly Regex PartAdded = new Regex(@"\bMapParts\.Add\s*\(\s*""([^""]+)""", RegexOptions.Compiled);

    private static void MapChecks()
    {
        var files = new Dictionary<string, SourceFile>(StringComparer.Ordinal);
        foreach (SourceFile f in Scripts)
        {
            files[f.Rel] = f;
        }

        // Every subscription to the map's events is in a file the list names, as many as it lists.
        var listed = new Dictionary<string, int>(StringComparer.Ordinal);
        var parts = new HashSet<string>(StringComparer.Ordinal);
        foreach ((string subscribed, string file, string method, string part, string why) in MapHandlers)
        {
            listed[subscribed] = listed.TryGetValue(subscribed, out int n) ? n + 1 : 1;
            Check(why.Length >= 25, MapRule + ": " + file + " " + method + " says what it changes for the map and how that is put back");
            if (part != null)
            {
                parts.Add(part);
            }
        }
        var added = new HashSet<string>(StringComparer.Ordinal);
        foreach (SourceFile f in Scripts)
        {
            string name = f.Rel.Substring(ScriptsDir.Length + 1);
            for (int i = 0; i < f.Bare.Length; i++)
            {
                foreach (Match m in PartAdded.Matches(f.Bare[i]))
                {
                    string part = m.Groups[1].Value;
                    added.Add(part);
                    Examine(MapRule, f.Rel);
                    if (!parts.Contains(part))
                    {
                        Hit(MapRule, f.Rel, i + 1, "the look '" + part + "' is added to the map check, but no handler on the map's events names it: "
                                                    + "list its handler with it in StandardCheck/MapChecks.cs");
                    }
                }
            }
            int found = MapSubscription.Matches(f.CodeText).Count;
            listed.TryGetValue(name, out int expected);
            if (found == 0 && expected == 0)
            {
                continue;
            }
            Examine(MapRule, f.Rel);
            if (found > expected)
            {
                Hit(MapRule, f.Rel, 0, found + " handler(s) on the city map's events, " + expected + " listed: what a handler changes for the map must be put back as it "
                                       + "shuts and be seen to be (a look added with MapParts.Add, asked by the map check); list it in StandardCheck/MapChecks.cs");
            }
            else if (found < expected)
            {
                Hit(MapRule, f.Rel, 0, found + " handler(s) on the city map's events, " + expected + " listed: put the list of the map's handlers right (StandardCheck/MapChecks.cs)");
            }
        }

        // Each listed part has its look in the map check.
        foreach (string part in parts)
        {
            if (!added.Contains(part))
            {
                Hit(MapRule, ScriptsDir + "/Core/MapParts.cs", 0, "the part '" + part + "' changes something for the map but adds no look (MapParts.Add(\"" + part
                                                                  + "\", ...)): the map check cannot see that it was put back");
            }
        }

        // Each listed handler is there, and holds nothing that does not belong in the frame the map opens or shuts in.
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach ((string subscribed, string file, string method, string part, string why) in MapHandlers)
        {
            string rel = ScriptsDir + "/" + file;
            if (!seen.Add(rel + "#" + method))
            {
                continue;
            }
            Examine(MapRule, rel);
            if (!files.TryGetValue(rel, out SourceFile f))
            {
                Hit(MapRule, rel, 0, "the file is gone: put the list of the map's handlers right (StandardCheck/MapChecks.cs)");
                continue;
            }
            if (!HandlerBody(f, method, out int bodyLine, out string body))
            {
                Hit(MapRule, rel, 0, method + " is gone: put the list of the map's handlers right (StandardCheck/MapChecks.cs)");
                continue;
            }
            string[] lines = body.Split('\n');
            for (int k = 0; k < lines.Length; k++)
            {
                foreach ((string what, Regex pattern) in DayBanned)
                {
                    if (pattern.IsMatch(lines[k]))
                    {
                        Hit(MapRule, rel, bodyLine + k, method + " (on the city map's events): " + what + ": " + lines[k]);
                    }
                }
            }
        }

        // The map check itself: the session record is told first, and asks the parts.
        string guard = ScriptsDir + "/Core/MapTreeGuard.cs";
        string watch = ScriptsDir + "/Diagnostics/SessionWatch.Map.cs";
        Examine(MapRule, guard);
        Examine(MapRule, watch);
        if (files.TryGetValue(guard, out SourceFile g) && HandlerBody(g, "OnMapToggled", out _, out string first))
        {
            Check(first.Contains("SessionWatch.MapToggled(open)"), MapRule + ": the first handler on the map's event (Core/MapTreeGuard) tells the session record");
        }
        else
        {
            Hit(MapRule, guard, 0, "the first handler on the map's event is gone: the map check needs the camera and the frame as the map opens");
        }
        Check(files.TryGetValue(watch, out SourceFile w) && w.CodeText.Contains("MapParts.Lingering("), MapRule + ": the map check asks the parts (Diagnostics/SessionWatch.Map)");
    }

    // A method's body, by its name: a block, or an expression after "=>". Its first line (1-based) and its text.
    private static bool HandlerBody(SourceFile f, string method, out int bodyLine, out string body)
    {
        bodyLine = 0;
        body = string.Empty;
        Match m = Regex.Match(f.CodeText, @"[\w>\]]\s+" + method + @"\s*\([^;{}]*\)\s*(\{|=>)");
        if (!m.Success)
        {
            return false;
        }
        if (m.Groups[1].Value == "=>")
        {
            int start = m.Index + m.Length;
            int end = f.CodeText.IndexOf(';', start);
            if (end < 0)
            {
                return false;
            }
            bodyLine = f.LineAt(start) + 1;
            body = f.CodeText.Substring(start, end - start);
            return true;
        }
        if (!f.Block(m.Index, out int open, out int close))
        {
            return false;
        }
        bodyLine = f.LineAt(open) + 1;   // the brace's line: the body's first piece is on it
        body = f.CodeText.Substring(open + 1, close - open - 1);
        return true;
    }
}
