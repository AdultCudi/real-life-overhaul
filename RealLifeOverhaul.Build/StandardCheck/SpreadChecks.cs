using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

// docs/standards/performance.md, RLO-PERF-024: one-off work that a moment sets off (a black screen, getting into a car, a
// save) is spread a step a frame or done once, never all of it in the one frame. The owner's beta 6 log (28 Oct 2026) put
// the mod's own worst frames there: the car's handling and power worked out whole as the player got in (15.2 ms), every
// piece waiting for a black screen run in its first frame (13.3 ms; 45.3 ms in beta 4), and the district save's section
// built a formatter for every type of its records again at every save (53 ms of a save frame). Each piece of that work is
// listed with the words that show it is spread or done once, and the words that would show it went back.
internal static partial class Program
{
    // File under Scripts, the method (null: the whole file, its texts read too), words it must hold (all), words it must not hold (any), and why.
    private static readonly (string File, string Method, string[] Must, string[] Not, string Why)[] SpreadWork =
    {
        ("Core/SceneFinds.cs", "OnUpdate", new[] { "RunOne()", "Spread.Frame(" }, new[] { "Run()" },
            "behind a black screen the waiting pieces go one a frame, never all of them in its first frame"),
        ("Core/SceneFinds.cs", "RunOne", new[] { "TriedIn[i] = _blacks", "Call(Work[i]" }, new[] { "for (" },
            "one piece a call: the next not yet tried behind this black screen"),
        ("Seasons/SeasonGame.Coats.cs", "AtDoor", new[] { "DressSome(1)", "PutBack(1)", "ScenePrewarm.NextFrame()" }, new[] { "DressSome(DoorMost)", "PutBack(DoorMost)" },
            "a coat a frame behind the black screen: each is a mesh merge of 1 to 2 ms"),
        ("CityLife/CityPeople.Shelf.cs", "AtDoor", new[] { "ScenePrewarm.NextFrame()", "--_doorLeft" }, new[] { "made < WornAtDoor" },
            "a worn look a frame behind the black screen: each is a mesh merge"),
        ("Core/RealLifeOverhaulMod.cs", null, new[] { "ScenePrewarm.AddLate(\"roads: the lane points put in a grid" }, new[] { "ScenePrewarm.Add(\"roads: the lane points put in a grid" },
            "the lane grid (3.8 ms) is made behind the loading screen, in the game's late call, not at the first black screen"),
        ("Driving/CarTuning.cs", "OnUpdate", new[] { "WorkSlice.Of(SliceMs", "Carry(ref slice" }, new[] { "WorkSlice.Whole", "_handling.Apply(" },
            "the car's sums go a slice of half a millisecond a frame from the frame the player gets in"),
        ("Driving/CarTuning.cs", "Carry", new[] { "slice.More(Heavy(_sums))", "_handling.Step()", "_solve.Step()" }, new string[0],
            "the handling's design and the search for the power a step at a time"),
        ("Driving/CarTuning.cs", null, new string[0], new[] { "CarModel.Solve(" },
            "the whole search for a car's power (24 drives of the model) is for the offline checks, never a frame of play"),
        ("Driving/CarHandling.cs", null, new[] { "_design.Begin(", "_design.Step()" }, new[] { "HandlingModel.Design(" },
            "the whole design of the handling is for the offline checks, never a frame of play"),
        ("DistrictSave/DistrictSaveCodec.cs", "Write", new[] { "SerializationPolicy = Policy;" }, new[] { "new SaveGameSerializationPolicy" },
            "one serialization policy for the session, as the game keeps: a new one at every save makes Odin build every formatter again"),
        // The city side's own spikes (the own-spike scan, 28 Oct 2026; perf/city-spikes).
        ("Construction/ConstructionDirector.BareGround.cs", "BareGroundFrame", new[] { "WorkSlice.Of(BareSliceMs", "_bare.Step()" }, new[] { "BuildGroundRules.Audit(" },
            "the load's bare ground check goes a slice of a millisecond a frame (BareAudit): whole in one frame it was 143 and 181 ms"),
        ("Construction/ConstructionDirector.BareGround.cs", null, new string[0], new[] { "BuildGroundRules.Audit(", "while (_bare.Step())" },
            "the whole bare ground check in one go is for the offline checks, never a frame of play"),
        ("Construction/ConstructionDirector.Spaces.cs", "Space", new[] { "LotsFound().First(id)" }, new[] { "for (" },
            "a lot found by its id in one lookup (LotFinder), never a walk over every lot of the district for each column"),
        ("Construction/ConstructionDirector.Spaces.cs", "Before", new[] { "LotsFound().Ending(" }, new[] { "for (" },
            "the lot before one on its strip found in one lookup, never a walk over every lot"),
        ("Construction/ConstructionDirector.Spaces.cs", "After", new[] { "LotsFound().Starting(" }, new[] { "for (" },
            "the lot after one on its strip found in one lookup, never a walk over every lot"),
        ("Traffic/Solid/SolidStreets.cs", "Awake", new[] { "PrewarmStandIns" }, new string[0],
            "the sixteen stand-ins for people in the road are made behind the loading screen: made in the look that first needed one they were 37 ms"),
        ("Traffic/Solid/SolidGame.cs", "MakeNext", new[] { "break;" }, new string[0],
            "in play a stand-in not yet made is made one a look, never all of them at once"),
        ("Traffic/Drivers/CityDrivers.cs", "Awake", new[] { "PrewarmLines" }, new string[0],
            "the two hold lines for the player's car are made behind the loading screen: made in the look that first wanted one they were 9 ms"),
        ("Transport/BusZones.Pieces.cs", "Probe", new[] { "ProbeOne(hid)", "return;" }, new[] { "OnMesh(" },
            "the bus stops' pieces are asked about the walkable ground one a tick: a stop's pieces all fall due together"),
        ("Transport/TransitDirector.cs", "PrewarmDressings", new[] { "while (_spare.Count < AwayMost" }, new string[0],
            "a spare bus body for each bus that may drive off at once is built behind the loading screen, so none is built in a tick of play"),
        ("Tech/Sites/TechSites.cs", "Slow", new[] { "if (pickedBefore || !_hq.Picked)" }, new string[0],
            "the office count never runs in the tick the tech towers are first picked: the two whole-city passes were 12.1 ms in one frame"),
        ("Performance/Tweaks/FarDistricts.cs", "Survey", new[] { "WorkSlice.Of(SliceMs, SurveyLookEvery)" }, new string[0],
            "the far squares' survey looks at the clock every few renderers, so its slice holds"),
        ("Performance/Tweaks/FarDistricts.cs", "Refresh", new[] { "_moves.EnsureCapacity(" }, new string[0],
            "the survey's map of parents is given its room behind the screen, never rebuilt at each doubling in a tick of play")
    };

    private static void SpreadChecks()
    {
        const string rule = "RLO-PERF-024";
        var files = new Dictionary<string, SourceFile>(StringComparer.Ordinal);
        foreach (SourceFile f in Scripts)
        {
            files[f.Rel] = f;
        }
        foreach ((string file, string method, string[] must, string[] not, string why) in SpreadWork)
        {
            string rel = ScriptsDir + "/" + file;
            Examine(rule, rel);
            if (!files.TryGetValue(rel, out SourceFile f))
            {
                Hit(rule, rel, 0, "the file is gone: put the list of spread work right (StandardCheck/SpreadChecks.cs)");
                continue;
            }
            string body = string.Join('\n', f.Bare);   // the whole file: comments blanked, the texts kept
            int line = 1;
            string where = "the file";
            if (method != null)
            {
                Match m = Regex.Match(f.CodeText, @"[\w>\]]\s+" + method + @"\s*\([^;{}]*\)\s*\{");
                if (!m.Success || !f.Block(m.Index, out int open, out int close))
                {
                    Hit(rule, rel, 0, method + " is gone: put the list of spread work right (StandardCheck/SpreadChecks.cs)");
                    continue;
                }
                line = f.LineAt(open);
                body = f.CodeText.Substring(open + 1, close - open - 1);
                where = method;
            }
            foreach (string word in must)
            {
                if (!body.Contains(word))
                {
                    Hit(rule, rel, line, where + " no longer holds \"" + word + "\": " + why);
                }
            }
            foreach (string word in not)
            {
                if (body.Contains(word))
                {
                    Hit(rule, rel, line, where + " holds \"" + word + "\", which does the work whole in one frame: " + why);
                }
            }
        }
    }
}
