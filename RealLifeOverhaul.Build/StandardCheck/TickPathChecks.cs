using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

// docs/standards/performance.md, RLO-PERF-014: a feature whose frame method calls on into other methods on every tick
// names those methods here, and none of them allocates. RLO-PERF-003 reads the frame method's own body and does not
// follow its calls, which is how the seasons' once-a-second step came to grow the heap by 888 KB a second unseen (a
// player's session record, 25 Oct 2026). Work that allocates on purpose and rarely (a log line when the season turns,
// a copy of the sky when its look changes) lives in methods of its own, left off the list.
internal static partial class Program
{
    private const string SeasonsDir = ScriptsDir + "/Seasons/";

    // Each file and the methods of it that run on every tick of the seasons' step.
    private static readonly (string File, string[] Methods)[] TickPaths =
    {
        ("SeasonDirector.cs", new[] { "Update", "OnUpdate", "Slow", "Hush", "Demand", "FollowMarks", "News" }),
        ("SeasonDirector.Look.cs", new[] { "Looks", "Bar" }),
        ("SnowLook.cs", new[] { "Show", "Hide", "Fall", "Follow", "Fit", "Lie", "Layered", "Uncover", "Pose" }),
        ("SeasonLook.cs", new[] { "Leaf", "StopLeaves" }),
        ("SeasonBar.cs", new[] { "Show", "Hide" }),
        ("SeasonGame.cs", new[] { "Loading", "Walkers" }),
        ("SeasonGame.Trees.cs", new[] { "Trees", "WalkTrees", "RestoreTrees" }),
        ("SeasonGame.Sky.cs", new[] { "Sky", "EndRain" }),
        ("SeasonGame.Coats.cs", new[] { "Coats", "Seen", "PutBack" }),
        ("SeasonGame.Snow.cs", new[] { "HushRain", "UnhushRain" }),
        ("SnowHush.cs", new[] { "Hold", "LateUpdate", "OnLateUpdate" })
    };

    // Beyond RLO-PERF-003's patterns: an object of a class made (the value types these methods use are let through).
    private static readonly Regex ObjectMade = new Regex(
        @"\bnew\s+(?!(?:Vector2|Vector3|Vector4|Color|Color32|Quaternion|Keyframe|ParticleSystem\.MinMaxCurve|Worn|Tint|SnowDay|RainDay|SnowField)\b)[A-Z][\w.]*\s*[({]",
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
            string rel = SeasonsDir + file;
            Examine("RLO-PERF-014", rel);
            if (!files.TryGetValue(rel, out SourceFile f))
            {
                Hit("RLO-PERF-014", rel, 0, "the file is gone: put the list of the seasons' tick methods right");
                continue;
            }
            foreach (string method in methods)
            {
                Match m = Regex.Match(f.CodeText, @"[\w>\]]\s+" + method + @"\s*\([^;{}]*\)\s*\{");
                if (!m.Success || !f.Block(m.Index, out int open, out int close))
                {
                    Hit("RLO-PERF-014", rel, 0, method + " is gone: put the list of the seasons' tick methods right");
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
                }
            }
        }
    }
}
