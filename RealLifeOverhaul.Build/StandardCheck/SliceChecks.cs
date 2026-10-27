using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

// docs/standards/performance.md, RLO-PERF-019: a job that is heavy the first time, or once a city, is spread over frames.
// A player's log (27 Oct 2026, a 10 minute session) put the mod's worst single frames in work that was done whole in one frame:
// the law's choosing of six precincts and the dressing of the first front (83 ms and 16 ms), the garage's search for bike places
// (37 ms), a bus built in a tick (about 20 ms), and the bus stops' look along a window of lane points (16 ms). Each is listed here with
// the words that show it is spread over frames (a WorkSlice a call, a step a frame, built behind the loading screen) and the words that would
// show it went back to being done whole. A job added to the list is checked the same way.
internal static partial class Program
{
    // File under Scripts, the method, words its body must hold (all of them), words it must not hold (any of them), and why.
    private static readonly (string File, string Method, string[] Must, string[] Not, string Why)[] SlicedJobs =
    {
        ("Law/Precincts.cs", "Choose", new[] { "WorkSlice.Of(", "Choose(ref slice)" }, new[] { "WorkSlice.Whole", "Best(" },
            "the precincts are chosen a slice of about 2 ms a frame: the walk over the city's buildings and the measuring of each district's candidates are the items"),
        ("Law/Precincts.cs", "Search", new[] { "slice.More()", "probe.Scan", "probe.Measured" }, new string[0],
            "one district's search goes on from the building or candidate it stopped at"),
        ("Law/LawDirector.cs", "OnUpdate", new[] { "_precincts.Choosing", "ChooseOn()", "_precincts.Placing", "_precincts.Frame()" }, new string[0],
            "every frame out of doors carries on the choosing and the dressing of a front, which the one-second tick only begins"),
        ("Law/PrecinctFrontage.Wall.cs", "PlaceStatics", new[] { "_step", "while (whole" }, new string[0],
            "the front's pieces go up one a frame unless a dark screen covers them"),
        ("Law/PrecinctFrontage.Wall.cs", "PlaceStep", new[] { "switch (_step)", "_step++" }, new[] { "PlaceStatics(" },
            "one piece of the front, then the next frame's"),
        ("Law/PrecinctFrontage.cs", "Dress", new[] { "_step >= 0", "PlaceStatics(force)" }, new[] { "PlaceStatics()" },
            "a front under way is left to the frames; only a dark screen (force) puts the rest up at once"),
        ("Garage/BikePlaces.cs", "Find", new[] { "_search = " }, new[] { "Beside(", "Outside(", "Physics." },
            "finding the places only sets the search up (the sorting of the stands and buildings is on keys read once); the casts are in Step"),
        ("Garage/BikePlaces.cs", "Step", new[] { "WorkSlice.Of(", "slice.More()", "s.Next" }, new string[0],
            "the docks, racks and stands are found from where the last slice stopped"),
        ("Garage/TwoWheelers.cs", "Frame", new[] { "_places.Finding", "_places.Step()" }, new string[0],
            "a slice of the search every frame while it is under way"),
        ("Transport/TransitMap.Stops.cs", "PlaceStop", new[] { "ref slice", "slice.More()", "_tryStage", "_tryAt" }, new[] { "WorkSlice.Whole" },
            "a stop's window of lane points is tried one point an item, from where the last slice stopped (Try is some 2 ms of the engine's queries)"),
        ("Transport/TransitMap.Stops.cs", "Terminus", new[] { "slice.More()", "_tryAt" }, new[] { "WorkSlice.Whole" },
            "the last stop near a station is looked for the same way"),
        ("Transport/TransitMap.cs", "Next", new[] { "PlaceStop(Lines[_lineCursor], ref slice)", "WorkSlice.Of(StopsSliceMs" }, new string[0],
            "the stops of the lines are laid in slices of about a millisecond, the slice handed down to the lane points"),
        ("Transport/TransitDirector.cs", "PrewarmDressings", new[] { "_bus.Prepare()", "_next.Prepare()", "_seenOff.Prepare()", "_spare.Add(" }, new string[0],
            "the buses' bodies are built behind the loading screen, so that the first bus sent in play builds nothing"),
        ("Social/Flare/FlareDirector.cs", "OnDay", new[] { "FinishDay();", "_dayStep = 0", "DaySteps()" }, new[] { "PlayDay(", "DayStep()" },
            "the day's round only queues its steps at midnight (32 to 46 ms in one frame in a player's log); a coroutine plays them a step a frame"),
        ("Social/Flare/FlareDirector.cs", "OnSave", new[] { "FinishDay();" }, new string[0],
            "a save holds the day that has turned, played"),
        ("Social/Flare/FlareDirector.cs", "StandDown", new[] { "FinishDay();" }, new string[0],
            "standing down plays the day owed before the account is kept"),
        ("Core/CompanionGame.cs", "Write", new[] { "CompanionSnapshot.Render(job.Got)" }, new[] { "Logger", "SaveGameManager", "Failed(" },
            "the report's text is written from plain values on a worker: no game read, no log line"),
        ("Core/SaveHook.cs", "Run", new[] { "CompanionGame.Begin(" }, new[] { "CompanionGame.Save(" },
            "the report's readers run before the district save takes the mod's places out"),
        ("DistrictSave/DistrictSaveDirector.cs", "BeforeWrite", new[] { "CompanionGame.End(save);" }, new string[0],
            "the district save joins the report's text, written on a worker beside its section"),
        ("World/MapLinks.cs", "Feed", new[] { "apply: false", "Category.Show()" }, new string[0],
            "a batch of markers is shown once, not once a marker (the row's markers were gone over for each)")
    };

    private static void SliceChecks()
    {
        const string rule = "RLO-PERF-019";
        var files = new Dictionary<string, SourceFile>(StringComparer.Ordinal);
        foreach (SourceFile f in Scripts)
        {
            files[f.Rel] = f;
        }
        foreach ((string file, string method, string[] must, string[] not, string why) in SlicedJobs)
        {
            string rel = ScriptsDir + "/" + file;
            Examine(rule, rel);
            if (!files.TryGetValue(rel, out SourceFile f))
            {
                Hit(rule, rel, 0, "the file is gone: put the list of sliced jobs right (StandardCheck/SliceChecks.cs)");
                continue;
            }
            Match m = Regex.Match(f.CodeText, @"[\w>\]]\s+" + method + @"\s*\([^;{}]*\)\s*\{");
            if (!m.Success || !f.Block(m.Index, out int open, out int close))
            {
                Hit(rule, rel, 0, method + " is gone: put the list of sliced jobs right (StandardCheck/SliceChecks.cs)");
                continue;
            }
            int line = f.LineAt(open);
            string body = f.CodeText.Substring(open + 1, close - open - 1);
            foreach (string word in must)
            {
                if (!body.Contains(word))
                {
                    Hit(rule, rel, line, method + " no longer holds \"" + word + "\": " + why);
                }
            }
            foreach (string word in not)
            {
                if (body.Contains(word))
                {
                    Hit(rule, rel, line, method + " holds \"" + word + "\", which does the job whole in one frame: " + why);
                }
            }
        }
    }
}
