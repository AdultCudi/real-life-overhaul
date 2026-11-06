using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

// docs/standards/performance.md, RLO-PERF-014: a feature whose frame method calls on into other methods on every tick
// names those methods here, and none of them allocates. RLO-PERF-003 reads the frame method's own body and does not
// follow its calls, which is how the seasons' once-a-second step came to grow the heap by 888 KB a second unseen (a
// player's session record, 25 Oct 2026). Work that allocates on purpose and rarely (a log line when the season turns,
// a copy of the sky when its look changes) lives in methods of its own, left off the list.
// RLO-PERF-015: none of those methods builds a character's look either (the game's mesh merge, some 1.5 MB of arrays a
// person): the coats dressed one person at every look in play and the city's people changed worn looks in play (the
// owner's session of 25 Oct 2026); both now wait for a screen that hides them.
internal static partial class Program
{
    // Each file under Scripts and the methods of it that run on every tick of its feature.
    private static readonly (string File, string[] Methods)[] TickPaths =
    {
        ("Seasons/SeasonDirector.cs", new[] { "Update", "OnUpdate", "Slow", "Hush", "Demand", "FollowMarks", "News" }),
        ("Seasons/SeasonDirector.Look.cs", new[] { "Looks", "Bar" }),
        ("Seasons/SnowLook.cs", new[] { "Show", "Hide", "Fall", "Follow", "Fit", "Lie", "Layered", "Uncover", "Pose", "Keep", "Reach", "Size", "Place" }),
        ("Seasons/SnowCoverFollow.cs", new[] { "Hold", "LateUpdate", "OnLateUpdate" }),
        ("Seasons/SnowCoverRules.cs", new[] { "Reach", "HalfFor", "Covered", "Moves", "Lay", "Step", "Ahead" }),
        ("Seasons/SeasonLook.cs", new[] { "Leaf", "StopLeaves" }),
        ("Seasons/SeasonBar.cs", new[] { "Show", "Hide" }),
        ("Seasons/SeasonGame.cs", new[] { "Loading", "Walkers" }),
        ("Seasons/SeasonGame.Trees.cs", new[] { "Trees", "WalkTrees", "Walk", "RestoreTrees" }),
        ("Seasons/SeasonGame.Sky.cs", new[] { "Sky", "EndRain" }),
        ("Seasons/SeasonGame.Coats.cs", new[] { "Coats", "Undressed", "Walking" }),
        ("Seasons/SeasonGame.Snow.cs", new[] { "HushRain", "UnhushRain" }),
        ("Seasons/SnowHush.cs", new[] { "Hold", "LateUpdate", "OnLateUpdate" }),
        // Flare's faces: the painter's frame while faces wait for their moment, and the moment itself.
        ("Social/Flare/FlareFacePainter.cs", new[] { "Update", "OnUpdate", "Now" }),
        ("Social/Flare/FlareFaceMoment.cs", new[] { "Of" }),
        // The living city's shelf: the frames it waits through and what it asks each one.
        ("CityLife/CityPeople.Shelf.cs", new[] { "Tending", "AnyWorn", "AskDoor" }),
        // The session record and the mod's share of each frame: every frame while the record runs.
        ("Diagnostics/SessionWatch.cs", new[] { "Update", "OnUpdate" }),
        ("Diagnostics/SessionWatch.Timing.cs", new[] { "Split", "Figures", "CaptureTiming" }),
        ("Core/HotSpots.cs", new[] { "Time", "End", "Roll", "Settle", "Keep" }),
        // The performance director: every frame, its tweaks' timers, the culling's frame.
        ("Performance/PerfDirector.cs", new[] { "Update", "OnUpdate", "Tick", "Schedule" })
    };

    // Beyond RLO-PERF-003's patterns: an object of a class made (the value types these methods use are let through).
    private static readonly Regex ObjectMade = new Regex(
        @"\bnew\s+(?!(?:Vector2|Vector3|Vector4|Color|Color32|Quaternion|Keyframe|ParticleSystem\.MinMaxCurve|Worn|Tint|SnowDay|RainDay|SnowField|SnowCoverState|Scope|FrameFigures)\b)[A-Z][\w.]*\s*[({]",
        RegexOptions.Compiled);

    // A character's look built: the game's own calls that merge a person's meshes, and the mod's that make them.
    private static readonly Regex LookBuilt = new Regex(
        @"\.(?:UpdateElements|UpdateVisuals|SetAppearance|SetRandomAppearance|RandomizeElements|RandomizeClothes)\s*\(|(?<![\w.])(?:Dress|DressSome|Redress|PutBack)\s*\(",
        RegexOptions.Compiled);

    private static void TickPathChecks()
    {
        var files = new Dictionary<string, SourceFile>(StringComparer.Ordinal);
        foreach (SourceFile f in Scripts)
        {
            files[f.Rel] = f;
        }
        foreach ((string file, string[] methods) in TickPaths)
        {
            string rel = ScriptsDir + "/" + file;
            Examine("RLO-PERF-014", rel);
            Examine("RLO-PERF-015", rel);
            if (!files.TryGetValue(rel, out SourceFile f))
            {
                Hit("RLO-PERF-014", rel, 0, "the file is gone: put the list of the tick methods right");
                continue;
            }
            foreach (string method in methods)
            {
                Match m = Regex.Match(f.CodeText, @"[\w>\]]\s+" + method + @"\s*\([^;{}]*\)\s*\{");
                if (!m.Success || !f.Block(m.Index, out int open, out int close))
                {
                    Hit("RLO-PERF-014", rel, 0, method + " is gone: put the list of the tick methods right");
                    continue;
                }
                int bodyLine = f.LineAt(open);
                string[] lines = f.CodeText.Substring(open + 1, close - open - 1).Split('\n');
                for (int k = 0; k < lines.Length; k++)
                {
                    foreach ((string what, Regex pattern) in Allocations)
                    {
                        if (pattern.IsMatch(lines[k]))
                        {
                            Hit("RLO-PERF-014", rel, bodyLine + k + 1, method + " (every tick): " + what + ": " + lines[k]);
                        }
                    }
                    if (ObjectMade.IsMatch(lines[k]))
                    {
                        Hit("RLO-PERF-014", rel, bodyLine + k + 1, method + " (every tick): an object made: " + lines[k]);
                    }
                    if (LookBuilt.IsMatch(lines[k]))
                    {
                        Hit("RLO-PERF-015", rel, bodyLine + k + 1, method + " (every tick): a character's look built (a mesh merge): " + lines[k]);
                    }
                }
            }
        }
    }
}
