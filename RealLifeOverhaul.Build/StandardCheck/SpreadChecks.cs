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
        // The own-spikes scan's app and people leads (28 Oct 2026, perf/app-spikes).
        ("Social/Flare/FlareCityCamera.cs", "Take", new[] { "if (pass > 0)", "pass < Renders", "RenderOne(target)" }, new[] { "_camera.Render()", "i < Renders" },
            "a city photo's two renders go a frame each: both in one frame were 11 to 38 ms"),
        ("Social/Flare/FlareCityCamera.cs", "RenderOne", new[] { "_camera.Render();" }, new[] { "for (", "while (" },
            "one render a call"),
        ("Phone/Contacts/ContactPainter.cs", "OnUpdate", new[] { "_fetch = Ask(p);" }, new[] { "File.", "FileStream", "FileInfo" },
            "a contact's picture file is looked for and read on the worker, never in a frame (26 ms)"),
        ("Phone/Contacts/ContactPainter.cs", "Ask", new[] { "_io.ContinueWith(_ => Read(ask)" }, new[] { "File.", "FileStream", "FileInfo" },
            "the worker reads the file, behind any write or delete given it before"),
        ("Phone/Contacts/ContactPainter.cs", "Use", new string[0], new[] { "File.", "FileStream", "FileInfo" },
            "the main thread only puts the bytes the worker read into the texture"),
        ("Phone/Contacts/ContactPainter.cs", "PaintLogo", new string[0], new[] { "File.", "LogoHelper.GetLogoShapeTexture(", "ReadLogo(" },
            "a logo painted from the game's shape reads the shape's bytes the worker read"),
        ("Phone/Contacts/ContactPainter.cs", "PaintFace", new string[0], new[] { "File." },
            "a kept face is decoded from the bytes the worker read"),
        ("Phone/Contacts/ContactPainter.cs", "Drawn", new[] { "OnWorker(" }, new[] { "File.WriteAllBytes(file, p.Texture" },
            "a face drawn by the portrait camera is written to the cache on the worker"),
        ("Phone/Contacts/ContactPainter.cs", "KeepLogo", new[] { "OnWorker(" }, new string[0],
            "a painted logo is written to the cache on the worker"),
        ("Markets/MarketDirector.cs", "SaveTape", new[] { "tape.Changes != _tapeSaved", "tape.Lines()" }, new[] { "Whole()" },
            "the market's tape is written at a save only if it changed, from the lines it keeps, never made whole again on the main thread"),
        ("Markets/MarketDirector.cs", null, new string[0], new[] { "Tape.Whole()" },
            "the market's tape is never made whole on the main thread: its kept lines are handed to the writer"),
        ("Markets/MarketTape.cs", "Lines", new[] { "_secretLines.CopyTo(", "_envLines.CopyTo(", "_eventLines.CopyTo(" }, new[] { "AppendEnv(", "AppendSecret(", "AppendEvent(" },
            "the tape's lines as they were made: no day written again at a save"),
        ("Civic/CivicDirector.cs", null, new[] { "ScenePrewarm.Add(\"civic: its five people made\", PrewarmCast)" }, new string[0],
            "civic life's five people are made behind the loading screen, not a person every two seconds in play"),
        ("Events/EventDirector.cs", null, new[] { "ScenePrewarm.Add(\"events: the people of fires and street events made\", PrewarmPeople)" }, new string[0],
            "the eight people of fires and street events are made behind the loading screen, not a person every two seconds in play"),
        ("Travel/TravelDirector.cs", null, new[] { "ScenePrewarm.Add(\"travel: the hotel doorman made\", Hotels.PrewarmDoorman)" }, new string[0],
            "the hotel doorman is made behind the loading screen, not as a hotel's door first comes near"),
        ("People/ActorPool.cs", "Prewarm", new[] { "PropLibrary.Ready", "MakeUniformed()" }, new string[0],
            "the street pool's officers and paramedics are made behind the loading screen when their kit is read, not a look every five seconds in play"),
        ("Leisure/Activities/ChessGame.cs", "PutOut", new[] { "_pending = 1;" }, new[] { "People.Take(", "Watch(city" },
            "the chess table, its opponent and its onlooker go out a frame each: all three in one frame were 3.2 ms"),
        ("Leisure/Activities/ChessGame.cs", "Step", new[] { "People.Take(", "Watch(city", "_pending = 2;" }, new string[0],
            "the opponent a frame after the table, the onlooker a frame after that")
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
