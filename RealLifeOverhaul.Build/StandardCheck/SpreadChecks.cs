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
        ("Core/GameUi/PhoneApps.cs", "Pump", new[] { "WorkSlice.Of(InstallQueue<PhoneApp>.SliceMs", "Waiting.Next(ref slice" }, new[] { "WorkSlice.Whole" },
            "the mod's apps go on the phone one a frame, never all of them in the frame it can first take them (77 ms in the beta 6 log)"),
        ("Core/GameUi/PhoneApps.cs", "Install", new[] { "app.Queue(menu)", "OnPhone(phone, menu, def.Id)" }, new[] { "Put()", "GetComponentsInChildren", "Instantiate(" },
            "an install joins the line; its buttons are made when its turn comes, and the phones are searched for names once"),
        ("DistrictSave/DistrictSaveCodec.cs", "Write", new[] { "SerializationPolicy = Policy;" }, new[] { "new SaveGameSerializationPolicy" },
            "one serialization policy for the session, as the game keeps: a new one at every save makes Odin build every formatter again")
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
