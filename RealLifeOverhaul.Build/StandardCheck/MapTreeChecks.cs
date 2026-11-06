using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

// docs/standards/performance.md, RLO-PERF-018: the game's list of trees only holds trees that exist. The game adds a tree to
// TreeController.AllTrees when it wakes and never takes it out, and its city map asks every tree in the list for its game
// object, so one tree the mod destroyed made the map throw in its open coroutine and stay shut (0.60.0: the garage doors'
// mouths on Uptown's pavements destroyed the trees standing in them). Two things are held here:
//   1. a file that copies the game's scenery into the city destroys a game object only through SceneryTrees (Destroy,
//      DestroyNow, or Forget before a plain Destroy); a line that holds no tree by construction says so with the words
//      "RLO-PERF-018 no trees" in its comment;
//   2. the guard exists: MapTreeGuard puts its handler first in the map's event and prunes the list there, and the mod starts it.
internal static partial class Program
{
    private const string TreeRule = "RLO-PERF-018";

    // Where the mod copies the game's scenery (or lays copies of its streets, buildings and sites) and takes copies away.
    private static readonly string[] TreeFolders =
    {
        ScriptsDir + "/Uptown/", ScriptsDir + "/NewGround/", ScriptsDir + "/NewBuildings/", ScriptsDir + "/Civic/Site",
        ScriptsDir + "/Construction/CarPark", ScriptsDir + "/Landmarks/", ScriptsDir + "/Corners/CornerLook.cs",
        ScriptsDir + "/Schools/SchoolsGame.cs", ScriptsDir + "/Schools/CampusLook.cs", ScriptsDir + "/Law/PrecinctFrontage.cs",
        ScriptsDir + "/Travel/Hotels.cs", ScriptsDir + "/Floors/FloorsGame.Layout.cs", ScriptsDir + "/Core/GameFurniture.cs"
    };

    private static readonly Regex RawDestroy = new Regex(@"(?<![\w.])(?:(?:UnityEngine\.)?Object\.)?(Destroy|DestroyImmediate)\s*\(\s*([^,;]+?)\s*(?:,[^;]*)?\)\s*;", RegexOptions.Compiled);
    private static readonly Regex GameObjectName = new Regex(@"\b(?:List<GameObject>|GameObject\[\]|GameObject)\s+(\w+)", RegexOptions.Compiled);

    private static void MapTreeChecks()
    {
        foreach (SourceFile f in Scripts)
        {
            bool scenery = false;
            foreach (string folder in TreeFolders)
            {
                scenery |= f.Rel.StartsWith(folder, StringComparison.Ordinal);
            }
            if (!scenery)
            {
                continue;
            }
            Examine(TreeRule, f.Rel);
            foreach (int i in PlainDestroys(f, "RLO-PERF-018 no trees", "SceneryTrees.Forget"))
            {
                Hit(TreeRule, f.Rel, i + 1, "a game object destroyed without SceneryTrees: " + f.Code[i].Trim());
            }
        }
        // The guard itself.
        const string guardFile = ScriptsDir + "/Core/MapTreeGuard.cs";
        const string modFile = ScriptsDir + "/Core/RealLifeOverhaulMod.cs";
        Examine(TreeRule, guardFile);
        SourceFile guard = null, mod = null;
        foreach (SourceFile f in Scripts)
        {
            if (f.Rel == guardFile)
            {
                guard = f;
            }
            else if (f.Rel == modFile)
            {
                mod = f;
            }
        }
        if (guard == null)
        {
            Hit(TreeRule, guardFile, 0, "the map's tree guard is gone: the city map cannot open once a tree is destroyed");
            return;
        }
        if (!Regex.IsMatch(guard.CodeText, @"EventHold\s*\.\s*First\s*\(\s*ref\s+GlobalEvents\s*\.\s*onCityMapToggle"))
        {
            Hit(TreeRule, guardFile, 0, "the guard no longer puts its handler first in GlobalEvents.onCityMapToggle (EventHold.First)");
        }
        Match toggled = Regex.Match(guard.CodeText, @"void\s+OnMapToggled\s*\([^)]*\)\s*\{");
        if (!toggled.Success || !guard.Block(toggled.Index, out int open, out int close)
            || guard.CodeText.IndexOf("SceneryTrees.Prune()", open, close - open, StringComparison.Ordinal) < 0)
        {
            Hit(TreeRule, guardFile, 0, "the map handler no longer calls SceneryTrees.Prune() before the game's own");
        }
        if (mod == null || !Regex.IsMatch(mod.CodeText, @"Guard\s*\.\s*Add\s*<\s*MapTreeGuard\s*>"))
        {
            Hit(TreeRule, modFile, 0, "the mod no longer starts MapTreeGuard");
        }
    }

    // The lines (0-based) of a file that destroy a game object the plain way (Object.Destroy or DestroyImmediate), not
    // through SceneryTrees, with none of the given forgets on the line before and not marked with the given words in their
    // comment. Shared by RLO-PERF-018 (trees) and RLO-PERF-023 (CullingChecks.cs, the game's culling lists).
    private static List<int> PlainDestroys(SourceFile f, string exempt, params string[] forgets)
    {
        var found = new List<int>();
        var objects = new HashSet<string>(StringComparer.Ordinal);
        foreach (string line in f.Code)
        {
            foreach (Match m in GameObjectName.Matches(line))
            {
                objects.Add(m.Groups[1].Value);
            }
        }
        for (int i = 0; i < f.Code.Length; i++)
        {
            Match m = RawDestroy.Match(f.Code[i]);
            if (!m.Success || f.Code[i].IndexOf("SceneryTrees", StringComparison.Ordinal) >= 0)
            {
                continue;
            }
            string arg = m.Groups[2].Value.Trim();
            string head = Regex.Match(arg, @"^[\w.]+").Value;
            int dot = head.LastIndexOf('.');
            string last = dot >= 0 ? head.Substring(dot + 1) : head;
            bool gameObject = arg.EndsWith(".gameObject", StringComparison.Ordinal) || objects.Contains(arg) || objects.Contains(head)
                || objects.Contains(Regex.Replace(arg, @"\[.*$", string.Empty)) || (dot >= 0 && objects.Contains(last) && !arg.Contains("("));
            if (!gameObject || f.Lines[i].IndexOf(exempt, StringComparison.Ordinal) >= 0)
            {
                continue;
            }
            bool forgotten = false;
            foreach (string forget in forgets)
            {
                forgotten |= i > 0 && f.Code[i - 1].IndexOf(forget, StringComparison.Ordinal) >= 0;
            }
            if (!forgotten)
            {
                found.Add(i);
            }
        }
        return found;
    }
}
