using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using RealLifeOverhaul.NewGround;
using RealLifeOverhaul.Uptown;

// What a car can run into where nothing is drawn (Uptown/SolidRules.cs, EdgeRules.cs, GroundGame.Edges.cs and GroundDirector.Edges.cs;
// a beta player, 27 Oct 2026: "i keep crashing into nothing while driving and dying"). Held here: the first solid a car meets driving out of
// the band along any street or avenue is a barricade's box, and its first face is its fencing's drawn face (beta 4 stood the box 2.9 m
// before the fencing, and the fencing at 1st Avenue's two ends inside an invisible wall, so a car stopped dead at a face nothing showed);
// no invisible solid (the walls round the districts, the part of a box behind its fencing) overlaps a road, a lane or the surface a car
// drives on, or touches one except behind the fencing; the rules fail on the geometry beta 4 shipped; and over the sources every place that
// makes a collider is listed with why it is no solid with no mesh on a road, the box laid only with a drawn fencing (the bundle's model or a
// plain panel), and the crash log naming what was hit. Geometry in floats as it ships, tested in doubles.
internal static partial class Program
{
    private static void UptownSolidChecks(string scripts)
    {
        var ends = new List<Barricade>(32);
        var walls = new List<PlanBox>(12);
        var grounds = new List<PlanBox>(4);
        var roads = new List<PlanBox>(80);
        EdgeRules.Ends(ends);
        LayoutPlans.NewWalls(walls);
        LayoutPlans.Colliders(grounds, roads);
        RoadPlan lanes = LayoutPlans.Streets();

        string first = SolidRules.FirstIsFencing(ends, walls);
        Check(first == null, "uptown solids: the first solid a car meets driving out of the band along any of the " + ends.Count + " streets and avenues, across its whole width, is a barricade's box, never an invisible wall" + (first != null ? ": " + first : string.Empty));
        double worst = 0.0;
        string gapped = null;
        foreach (Barricade b in ends)
        {
            double gap = SolidRules.FenceGap(b);
            worst = Math.Max(worst, gap);
            if (gap < -0.02 || gap > SolidRules.FenceMost)
            {
                gapped = gapped ?? b.Name + " by " + gap.ToString("0.00");
            }
        }
        Check(gapped == null, "uptown solids: every barricade's box has its first face at its fencing's drawn face, at most " + SolidRules.FenceMost + " m before it (the worst " + worst.ToString("0.000") + " m)" + (gapped != null ? ": " + gapped : string.Empty));
        string hidden = SolidRules.OnTheRoad(ends, walls, roads, lanes);
        Check(hidden == null, "uptown solids: no invisible solid (" + walls.Count + " walls, the " + ends.Count + " boxes behind their fencing) overlaps or touches a road surface or comes within a lane's width of a lane of the plan except behind fencing" + (hidden != null ? ": " + hidden : string.Empty));
        bool wall = false, onFence = true;
        foreach (Barricade b in ends)
        {
            float face = EdgeRules.WallFace(b.Cx, b.Cz, b.OutX, b.OutZ, b.Half, walls);
            wall |= face < EdgeRules.HoardingAt + EdgeRules.FenceBack;
            onFence &= b.Fence + EdgeRules.FenceBack <= face + 1e-3f;
        }
        Check(wall && onFence && ends.Count(b => b.Fence < 0f) == 2 && ends.Where(b => b.Fence < 0f).All(b => b.Avenue && b.Index == 0), "uptown solids: the fencing stands in front of the invisible wall that stands at the edge itself, at 1st Avenue's two ends and nowhere else (" + ends.Count(b => b.Fence < 0f) + " ends), and never behind one anywhere");

        // The rules fail on beta 4's geometry: every fence 3 m past the edge, every box from 0.1 m past it.
        var beta4 = new List<Barricade>(ends.Count);
        foreach (Barricade b in ends)
        {
            Barricade old = b;
            old.Fence = EdgeRules.HoardingAt;
            float x0 = b.Avenue ? Math.Min(b.Cx + b.OutX * 0.1f, b.Cx + b.OutX * 3.4f) : b.Cx - b.Half, x1 = b.Avenue ? Math.Max(b.Cx + b.OutX * 0.1f, b.Cx + b.OutX * 3.4f) : b.Cx + b.Half;
            float z0 = b.Avenue ? b.Cz - b.Half : b.Cz + 0.1f, z1 = b.Avenue ? b.Cz + b.Half : b.Cz + 3.4f;
            old.Wall = new PlanBox(x0, z0, x1, z1, EdgeRules.WallTop);
            beta4.Add(old);
        }
        bool oldGap = beta4.Any(b => SolidRules.FenceGap(b) > SolidRules.FenceMost), oldWall = SolidRules.FirstIsFencing(beta4, walls) != null;
        Check(oldGap && oldWall && beta4.Count(b => SolidRules.FenceGap(b) > 2.5) == ends.Count, "uptown solids: the same rules fail on beta 4's geometry (a box 0.1 m past the edge with its fencing at 3 m: " + beta4.Count(b => SolidRules.FenceGap(b) > 2.5) + " ends 2.9 m short of it; a car out of 1st Avenue's ends meets the wall first: " + oldWall + ")");
        var overlapping = new List<PlanBox>(walls) { new PlanBox(LayoutPlans.West + 1f, LayoutRules.AvenueZ(1) - 3f, LayoutPlans.West + 4f, LayoutRules.AvenueZ(1) + 3f, EdgeRules.WallTop) };
        Check(SolidRules.OnTheRoad(ends, overlapping, roads, lanes) != null, "uptown solids: and on a wall put on an avenue's road surface");

        // The fencing is the bundle's model or, where the bundle cannot give it, a plain panel as deep and as wide: the same box under either.
        Check(EdgeRules.FenceFace + EdgeRules.FenceBack > 0.099f && EdgeRules.FenceFace + EdgeRules.FenceBack < 0.101f && EdgeRules.PanelWidth > 2.39f && EdgeRules.BoxAhead > 0f && EdgeRules.BoxAhead < 0.02f,
            "uptown solids: the fencing is 0.10 m deep (from " + EdgeRules.FenceBack + " m behind its origin to " + EdgeRules.FenceFace + " m before it, the prop's own, PropAssets/scripts/props_site.py) and the box starts " + EdgeRules.BoxAhead + " m before its face");
        UptownSolidSources(scripts);
    }

    private static void UptownSolidSources(string scripts)
    {
        string folder = Path.Combine(scripts, "Uptown");
        string game = Code(File.ReadAllText(Path.Combine(folder, "GroundGame.Edges.cs")));
        string director = Code(File.ReadAllText(Path.Combine(folder, "GroundDirector.Edges.cs")));
        string rules = Code(File.ReadAllText(Path.Combine(folder, "SolidRules.cs")));
        string contacts = Code(File.ReadAllText(Path.Combine(scripts, "Traffic", "CarContacts.cs")));
        string crash = Code(File.ReadAllText(Path.Combine(scripts, "Traffic", "CrashConsequences.cs")));
        Check(!Regex.IsMatch(rules, @"\busing UnityEngine|\bVector3\b|\bMathf\b|\bGameObject\b|\bCollider\b"), "uptown solids sources: the rules are game-free");
        // Every place that makes a collider is listed, with how many and why none is a solid with no mesh on a road.
        var listed = new Dictionary<string, int>();
        foreach (string site in SolidRules.Sites)
        {
            string[] part = site.Split('|');
            listed[part[0]] = int.Parse(part[1]);
        }
        var found = new Dictionary<string, int>();
        foreach (string file in Directory.GetFiles(scripts, "*.cs", SearchOption.AllDirectories))
        {
            int count = Regex.Matches(Code(File.ReadAllText(file)), @"AddComponent<(?:Box|Mesh|Capsule|Sphere|Wheel|Terrain)?Collider>").Count;
            if (count > 0)
            {
                found[file.Substring(scripts.Length + 1).Replace('\\', '/')] = count;
            }
        }
        string unlisted = found.Where(f => !listed.TryGetValue(f.Key, out int n) || f.Value > n).Select(f => f.Key + " makes " + f.Value + (listed.ContainsKey(f.Key) ? " (listed " + listed[f.Key] + ")" : " (not listed)")).FirstOrDefault();
        Check(unlisted == null, "uptown solids sources: every script that makes a collider is listed in SolidRules.Sites with why it is no solid without a mesh on a road (" + found.Count + " files, " + found.Values.Sum() + " colliders)" + (unlisted != null ? ": " + unlisted : string.Empty));
        Check(listed.Keys.All(k => found.ContainsKey(k)), "uptown solids sources: and every listed script still makes one (a stale line is taken out)");
        Check(Regex.IsMatch(game, @"public static string LayEdges\(EdgeFogData fog\)") && !Regex.IsMatch(SolidBody(game, "public static string LayEdges("), @"StandBox|BoxCollider"),
            "uptown solids sources: the edges' boxes are not laid with the fog but with their fencing");
        string pieces = SolidBody(game, "public static string LayPieces(");
        Check(pieces.IndexOf("StandBox(ends[i])", StringComparison.Ordinal) > pieces.IndexOf("Plain(places[p])", StringComparison.Ordinal) && pieces.IndexOf("if (any)", StringComparison.Ordinal) > 0
              && game.Contains("Shapes.Piece(\"panel\"") && game.Contains("Shapes.Paint("),
            "uptown solids sources: a box is laid only for an end whose fencing is drawn, the bundle's model or a plain panel of the mod's own shapes, so no box stands without a drawn face");
        Check(game.Contains("public static string FenceMeetsBox(") && director.Contains("GroundGame.FenceMeetsBox(_ends,") && director.Contains("SolidRules.FirstIsFencing(") && director.Contains("SolidRules.OnTheRoad("),
            "uptown solids sources: the load's line reads back the fencing against its box and asks the same two rules of what stands");
        Check(contacts.Contains("Describe(") && Regex.IsMatch(crash, @"CarContacts\.Describe\(collision,\s*contact\)") && crash.Contains("\"Crash: impact"),
            "uptown solids sources: a crash's line in the log names what was hit, its layer and the normal (CarContacts.Describe), so a report says which solid it was");
    }

    private static string SolidBody(string text, string head)
    {
        int at = text.IndexOf(head, StringComparison.Ordinal);
        if (at < 0)
        {
            return string.Empty;
        }
        int open = text.IndexOf('{', at), depth = 0;
        for (int i = open; i < text.Length; i++)
        {
            depth += text[i] == '{' ? 1 : text[i] == '}' ? -1 : 0;
            if (depth == 0)
            {
                return text.Substring(open, i - open + 1);
            }
        }
        return string.Empty;
    }
}
