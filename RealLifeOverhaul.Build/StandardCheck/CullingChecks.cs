using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

// docs/standards/performance.md, RLO-PERF-023: the game's culling lists only hold what exists. The game's lamps, trees,
// benches, building signs, people's zones and the like put themselves in its culling lists (Culling.ICullable) as they
// start, and the game makes its culling group again from a list, asking every entry for its sphere, whenever one leaves.
// While a city unloads their own OnDestroy leaves them in the list, and some kinds never leave it at all; so a copy the
// mod destroyed stayed listed, the mod's own unregistering of a people's zone asked for a rebuild as the city unloaded,
// and the rebuild threw on Uptown's destroyed lamp posts every frame until the city's scene went (0.61.0 beta 6). Held here:
//   1. nothing in the mod calls the game's RegisterCullable or UnregisterCullable but Core/SceneryCulling (the game's
//      unregistering asks for that rebuild, and leaves the group's count wrong until it runs);
//   2. every destroy through SceneryTrees takes the object's cullables out first (its Forget calls SceneryCulling.Forget),
//      and SceneryCulling compacts the list with the group's spheres in step and sets the group's count;
//   3. a file that names a cullable game type destroys a game object only through SceneryTrees (Destroy, DestroyNow, or
//      SceneryTrees.Forget or SceneryCulling.Forget before a plain Destroy), as the files that copy the game's scenery
//      already must (RLO-PERF-018); a line that holds no cullable by construction says so with the words
//      "RLO-PERF-023 no cullables" in its comment;
//   4. the check made as a city unloads ("Culling check (unload)") is started by the mod's unload, before its root goes.
internal static partial class Program
{
    private const string CullRule = "RLO-PERF-023";

    // The game's types that are cullables (Culling.ICullable, Build 3682), and the building that owns two of them.
    private static readonly Regex CullableType = new Regex(
        @"\b(?:ICullable|StreetLamp|TreeController|OutsideBenchController|BuildingSignController|StationaryAiSpawner|PedestrianSpawnerFor\w+|HamptonsHouse"
        + @"|GolfPlatformController|GolfCourse|TennisCourtManager|RandomClipOnSourceLooped|SellerStandController|BuildingOutside(?:Music|HangoutZone)Controller)\b",
        RegexOptions.Compiled);

    private static readonly Regex GameCullCall = new Regex(@"\b(?:Unr|R)egisterCullable\s*\(", RegexOptions.Compiled);

    private static void CullingChecks()
    {
        const string culling = ScriptsDir + "/Core/SceneryCulling.cs";
        const string rules = ScriptsDir + "/Core/SceneryCullRules.cs";
        const string trees = ScriptsDir + "/Core/SceneryTrees.cs";
        const string mod = ScriptsDir + "/Core/RealLifeOverhaulMod.cs";
        var files = new Dictionary<string, SourceFile>(StringComparer.Ordinal);
        foreach (SourceFile f in Scripts)
        {
            files[f.Rel] = f;
        }

        foreach (SourceFile f in Scripts)
        {
            // 1. the game's own registering and unregistering only in SceneryCulling
            if (f.Rel != culling)
            {
                for (int i = 0; i < f.Code.Length; i++)
                {
                    if (GameCullCall.IsMatch(f.Code[i]))
                    {
                        Hit(CullRule, f.Rel, i + 1, "the game's culling list changed outside Core/SceneryCulling (its unregistering asks for a rebuild over every "
                                                    + "entry, which walks destroyed objects while a city unloads): " + f.Code[i].Trim());
                    }
                }
            }

            // 3. a file that names a cullable game type destroys game objects only through SceneryTrees (the scenery folders
            // are held to it by RLO-PERF-018 already)
            bool scenery = false;
            foreach (string folder in TreeFolders)
            {
                scenery |= f.Rel.StartsWith(folder, StringComparison.Ordinal);
            }
            if (scenery || f.Rel.StartsWith(ScriptsDir + "/Core/Scenery", StringComparison.Ordinal) || !CullableType.IsMatch(f.CodeText))
            {
                continue;
            }
            Examine(CullRule, f.Rel);
            foreach (int i in PlainDestroys(f, "RLO-PERF-023 no cullables", "SceneryTrees.Forget", "SceneryCulling.Forget"))
            {
                Hit(CullRule, f.Rel, i + 1, "a game object destroyed without SceneryTrees in a file that names a cullable game type: " + f.Code[i].Trim());
            }
        }

        // 2. SceneryTrees takes the cullables out first; SceneryCulling keeps the group in step.
        Examine(CullRule, trees);
        Examine(CullRule, culling);
        Examine(CullRule, rules);
        if (!files.TryGetValue(trees, out SourceFile t) || !HandlerBody(t, "Forget", out _, out string forget) || !forget.Contains("SceneryCulling.Forget("))
        {
            Hit(CullRule, trees, 0, "SceneryTrees.Forget no longer takes the object's cullables out of the game's culling lists (SceneryCulling.Forget)");
        }
        else
        {
            foreach (string way in new[] { "Destroy", "DestroyNow" })
            {
                if (!HandlerBody(t, way, out _, out string body) || !Regex.IsMatch(body, @"(?<![\w.])Forget\s*\(\s*root\s*\)"))
                {
                    Hit(CullRule, trees, 0, "SceneryTrees." + way + " no longer forgets the object before destroying it");
                }
            }
        }
        if (!files.TryGetValue(culling, out SourceFile c))
        {
            Hit(CullRule, culling, 0, "Core/SceneryCulling is gone: the mod's destroyed scenery would stay in the game's culling lists");
        }
        else
        {
            if (!HandlerBody(c, "Take", out _, out string take) || !take.Contains("SceneryCullRules.Compact(") || !take.Contains("SetBoundingSphereCount("))
            {
                Hit(CullRule, culling, 0, "SceneryCulling no longer compacts the list with the group's spheres in step and sets the group's count at once");
            }
            if (!HandlerBody(c, "TheGamesWay", out _, out string theirs) || !theirs.Contains("GameManager.isCitySceneBeingUnloaded"))
            {
                Hit(CullRule, culling, 0, "SceneryCulling's fall back on the game's own unregistering no longer stands aside while a city unloads");
            }
        }

        // 4. the unload check: started by the city part's unload (the OnUnloadAsync that destroys the root) before its root
        // goes, and its line named as the docs name it
        bool started = false, unloads = false;
        if (files.TryGetValue(mod, out SourceFile m))
        {
            foreach (Match u in Regex.Matches(m.CodeText, @"\bOnUnloadAsync\s*\(\s*\)\s*\{"))
            {
                if (!m.Block(u.Index, out int open, out int close))
                {
                    continue;
                }
                string unload = m.CodeText.Substring(open, close - open);
                int root = unload.IndexOf("Destroy(_root)", StringComparison.Ordinal);
                if (root < 0)
                {
                    continue;
                }
                unloads = true;
                int watch = unload.IndexOf("SceneryCulling.WatchUnload", StringComparison.Ordinal);
                started |= watch >= 0 && watch < root;
            }
        }
        if (!unloads)
        {
            Hit(CullRule, mod, 0, "the city part's unload (the OnUnloadAsync that destroys the mod's root) is gone: the culling check cannot be started");
        }
        else if (!started)
        {
            Hit(CullRule, mod, 0, "the mod's unload no longer starts the culling check (SceneryCulling.WatchUnload) before its root is destroyed");
        }
        if (!files.TryGetValue(rules, out SourceFile r) || Array.FindIndex(r.Lines, l => l.Contains("\"Culling check (unload)\"")) < 0)
        {
            Hit(CullRule, rules, 0, "the culling check's line is no longer named 'Culling check (unload)'");
        }
    }
}
