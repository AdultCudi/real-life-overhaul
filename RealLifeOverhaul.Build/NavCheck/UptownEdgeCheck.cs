using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using RealLifeOverhaul.NewGround;
using RealLifeOverhaul.Uptown;

// The districts' edges (Uptown/EdgeRules.cs, GroundGame.Edges.cs and GroundDirector.Edges.cs): fog behind every stretch of the
// outline the invisible walls make, and a barricade across every street end that runs into it. Held here: the outline is the walls'
// inner faces and its corners are what they say; the fog is four layers that thicken to a whole one over 12 m and a lid at the height
// of the game's own sheet, wound to face into the districts and up, over nothing in play, covering the ground beyond the edge to where
// the game's own fog is whole, in a mesh of the size the data says; the closed ends are exactly the ways out of the plan's junctions
// that no lane takes and that run into an edge (found from the lanes alone), each a box the width of its street or avenue between the
// ring's strips, with fencing across it, signs and cones before it, all of the mod's own props already in its bundle; and no lane, walking
// ground, zone, terminus or road collider crosses a barricade's box, which lies right at the end of the ground the player and the people
// walk. Over the sources: one mesh, one material copy matched on the game's shader, the depth test changed, both destroyed with the
// district, nothing per frame, the load's line and the test in the feature's list. Geometry in floats as they ship, tested in doubles.
internal static partial class Program
{
    private static void UptownEdgeChecks(string scripts)
    {
        var edges = new List<EdgeSeg>(8);
        var ends = new List<Barricade>(32);
        EdgeRules.Edges(edges);
        EdgeRules.Ends(ends);
        EdgeFogData fog = EdgeRules.Fog(edges);
        UptownEdgeOutline(edges);
        UptownEdgeFog(edges, fog);
        UptownEdgeEnds(edges, ends);
        UptownEdgeCrossing(ends);
        UptownEdgeTiming();
        UptownEdgeSources(scripts);
    }

    // ---------------------------------------------------------------- the outline

    // True strictly inside the districts' playable ground: the ring and the band above 1st Avenue's north pavement, and 1st Avenue's strip.
    private static bool EdgePlay(double x, double z, double within = 0.0)
    {
        double west = LayoutPlans.West, east = LayoutPlans.East, ringWest = LayoutPlans.RingWest, ringEast = LayoutPlans.RingEast;
        double south = LayoutPlans.SouthWall, first = LayoutPlans.RowOneFrom, north = LayoutPlans.RingNorth;
        return (x > ringWest + within && x < ringEast - within && z > first + within && z < north - within)
               || (x > west + within && x < east - within && z > south + within && z <= first + 1e-9);
    }

    private static bool EdgeWall(List<PlanBox> walls, double x, double z)
    {
        return walls.Any(w => Math.Abs(x - w.X) <= w.HalfX && Math.Abs(z - w.Z) <= w.HalfZ);
    }

    private static void UptownEdgeOutline(List<EdgeSeg> edges)
    {
        var walls = new List<PlanBox>(12);
        LayoutPlans.NewWalls(walls);
        Check(edges.Count == 7 && edges.All(e => e.Length > 20f), "uptown edges: the outline has seven stretches with fog behind them (" + edges.Count + "), none of them short");
        bool joined = true, turns = true, normals = true;
        for (int i = 0; i < edges.Count; i++)
        {
            EdgeSeg e = edges[i];
            double len = e.Length, dx = (e.X1 - e.X0) / len, dz = (e.Z1 - e.Z0) / len;
            normals &= Math.Abs(e.OutX * e.OutX + e.OutZ * e.OutZ - 1.0) < 1e-6 && Math.Abs(e.OutX - -dz) < 1e-6 && Math.Abs(e.OutZ - dx) < 1e-6;   // the left of the way round: the districts lie on its right
            if (i + 1 < edges.Count)
            {
                EdgeSeg n = edges[i + 1];
                double ndx = (n.X1 - n.X0) / n.Length, ndz = (n.Z1 - n.Z0) / n.Length;
                double turn = dx * ndz - dz * ndx;   // below zero: a turn to the right, round a corner that points out
                joined &= Math.Abs(e.X1 - n.X0) < 1e-3 && Math.Abs(e.Z1 - n.Z0) < 1e-3;
                EdgeCorner want = turn < -0.5 ? EdgeCorner.Convex : turn > 0.5 ? EdgeCorner.Reflex : EdgeCorner.Free;
                turns &= e.End == want && n.Start == want;
            }
        }
        turns &= edges[0].Start == EdgeCorner.Free && edges[edges.Count - 1].End == EdgeCorner.Free;
        Check(joined, "uptown edges: each stretch ends where the next begins");
        Check(turns, "uptown edges: a corner that points out is convex and one that points in is reflex, the two ends of the outline free");
        Check(normals, "uptown edges: every stretch's way out is a unit vector on the left of the way round, the districts on its right");
        bool outside = true, inside = true, onWall = true;
        foreach (EdgeSeg e in edges)
        {
            double len = e.Length;
            for (double s = 0.3; s <= len - 0.3; s += Math.Max(1.0, len / 40.0))
            {
                double t = Math.Min(s, len) / len, x = e.X0 + (e.X1 - e.X0) * t, z = e.Z0 + (e.Z1 - e.Z0) * t;
                outside &= !EdgePlay(x + e.OutX * 0.5, z + e.OutZ * 0.5);
                inside &= EdgePlay(x - e.OutX * 0.5, z - e.OutZ * 0.5, -1e-6);
                onWall &= EdgeWall(walls, x + e.OutX * 0.5, z + e.OutZ * 0.5) && EdgeWall(walls, x + e.OutX * 2.0, z + e.OutZ * 2.0) && !EdgeWall(walls, x - e.OutX * 0.5, z - e.OutZ * 0.5);
            }
        }
        Check(outside && inside, "uptown edges: every point of every stretch has the districts' ground just inside it and none just outside");
        Check(onWall, "uptown edges: every stretch lies on the inner face of an invisible wall: wall 0.5 and 2 m out from it, none 0.5 m in");
        double south = LayoutPlans.SouthWall;
        int behind = 0, tried = 0;
        bool every = true;
        foreach (PlanBox w in walls.Where(b => Math.Abs(b.Z + b.HalfZ - south) > 0.01))   // and the corners below the south line are the city's side
        {
            for (double x = w.X - w.HalfX + 0.5; x <= w.X + w.HalfX - 0.5; x += 5.0)
            {
                for (double z = w.Z - w.HalfZ + 0.5; z <= w.Z + w.HalfZ - 0.5; z += 5.0)
                {
                    if (z < south)
                    {
                        continue;
                    }
                    tried++;
                    double near = edges.Min(e => EdgeDistance(e, x, z));
                    bool ok = near <= LayoutPlans.WallThick * Math.Sqrt(2.0) + 0.01;
                    behind += ok ? 1 : 0;
                    every &= ok;
                }
            }
        }
        Check(every && tried > 400, "uptown edges: every invisible wall round the districts (but the two along 1st Avenue's south side, the city's own) lies within its thickness of a stretch of edge, so none stands without fog behind it (" + behind + " of " + tried + " places)");
        Check(walls.Count(b => Math.Abs(b.Z + b.HalfZ - south) <= 0.01) == 2, "uptown edges: the two walls along 1st Avenue's continuations' south side are the only walls with no fog: the city's own backdrop stands there");
    }

    private static double EdgeDistance(EdgeSeg e, double x, double z)
    {
        double ax = e.X0, az = e.Z0, bx = e.X1, bz = e.Z1, dx = bx - ax, dz = bz - az;
        double t = Math.Max(0.0, Math.Min(1.0, ((x - ax) * dx + (z - az) * dz) / (dx * dx + dz * dz)));
        return Math.Sqrt((ax + dx * t - x) * (ax + dx * t - x) + (az + dz * t - z) * (az + dz * t - z));
    }

    // ---------------------------------------------------------------- the fog

    private static bool EdgeUnderLid(EdgeFogData f, double x, double z)
    {
        for (int t = 0; t + 2 < f.Triangles.Count; t += 3)
        {
            int a = f.Triangles[t], b = f.Triangles[t + 1], c = f.Triangles[t + 2];
            if (f.Y[a] < EdgeRules.Height - 0.01f || f.Y[b] < EdgeRules.Height - 0.01f || f.Y[c] < EdgeRules.Height - 0.01f)
            {
                continue;
            }
            double d1 = (f.X[b] - f.X[a]) * (z - f.Z[a]) - (f.Z[b] - f.Z[a]) * (x - f.X[a]);
            double d2 = (f.X[c] - f.X[b]) * (z - f.Z[b]) - (f.Z[c] - f.Z[b]) * (x - f.X[b]);
            double d3 = (f.X[a] - f.X[c]) * (z - f.Z[c]) - (f.Z[a] - f.Z[c]) * (x - f.X[c]);
            if (!((d1 < -1e-9 || d2 < -1e-9 || d3 < -1e-9) && (d1 > 1e-9 || d2 > 1e-9 || d3 > 1e-9)))
            {
                return true;
            }
        }
        return false;
    }

    private static void UptownEdgeFog(List<EdgeSeg> edges, EdgeFogData fog)
    {
        int layers = EdgeRules.CurtainOffset.Length;
        Check(fog.Segments == edges.Count && fog.Curtains == edges.Count * layers && fog.Lids == edges.Count * 3 + 4 * 9,
            "uptown edges: fog on all seven stretches: " + fog.Curtains + " layers (four each) and " + fog.Lids + " pieces of lid (three along each, nine at each of the four corners that point out)");
        Check(fog.Vertices == layers * 4 * 7 + 7 * 8 + 4 * 16 && fog.Triangles.Count / 3 == layers * 2 * 7 + 7 * 6 + 4 * 18,
            "uptown edges: the fog is " + fog.Vertices + " vertices and " + fog.Triangles.Count / 3 + " triangles, one mesh");
        Check(fog.X.Count == fog.Y.Count && fog.X.Count == fog.Z.Count && fog.X.Count == fog.U.Count && fog.X.Count == fog.NormalX.Count && fog.X.Count == fog.NormalY.Count
              && fog.X.Count == fog.NormalZ.Count && fog.Triangles.Count % 3 == 0 && fog.Triangles.All(i => i >= 0 && i < fog.Vertices && i <= ushort.MaxValue),
            "uptown edges: every fog attribute for every vertex, every index inside the mesh (16-bit)");
        Check(fog.U.All(u => u >= 0f && u <= 1f) && fog.NormalX.Select((n, i) => n * n + fog.NormalY[i] * fog.NormalY[i] + fog.NormalZ[i] * fog.NormalZ[i]).All(n => Math.Abs(n - 1.0) < 1e-6),
            "uptown edges: uv0.x within 0 and 1 and every normal a unit vector");
        // The way round and the way each face looks, as Unity's front face is the side the cross product of a triangle's first two edges points to.
        bool up = true, into = true, vertical = true, area = true;
        for (int t = 0; t + 2 < fog.Triangles.Count; t += 3)
        {
            int a = fog.Triangles[t], b = fog.Triangles[t + 1], c = fog.Triangles[t + 2];
            double ux = fog.X[b] - fog.X[a], uy = fog.Y[b] - fog.Y[a], uz = fog.Z[b] - fog.Z[a];
            double vx = fog.X[c] - fog.X[a], vy = fog.Y[c] - fog.Y[a], vz = fog.Z[c] - fog.Z[a];
            double nx = uy * vz - uz * vy, ny = uz * vx - ux * vz, nz = ux * vy - uy * vx;
            double size = Math.Sqrt(nx * nx + ny * ny + nz * nz) / 2.0;
            area &= size > 1e-3;
            if (fog.NormalY[a] > 0.5f)
            {
                up &= ny > 0.0 && Math.Abs(fog.Y[a] - EdgeRules.Height) < 1e-3 && Math.Abs(fog.Y[b] - EdgeRules.Height) < 1e-3 && Math.Abs(fog.Y[c] - EdgeRules.Height) < 1e-3;
            }
            else
            {
                vertical &= Math.Abs(ny) < 1e-6;
                // the face looks the way the vertices' normals say: into the districts
                into &= nx * fog.NormalX[a] + nz * fog.NormalZ[a] > 0.0;
            }
        }
        Check(area && up, "uptown edges: every fog triangle has area, and every lid triangle faces up at the height of the game's own sheet (" + EdgeRules.Height + " m)");
        Check(vertical && into, "uptown edges: every layer of curtain is upright and faces into the districts, so the game's culling of back faces keeps the street camera's view and hides it from outside");
        double top = double.MinValue, floor = double.MaxValue;
        for (int i = 0; i < fog.Vertices; i++)
        {
            if (fog.NormalY[i] < 0.5f)
            {
                top = Math.Max(top, fog.Y[i]);
                floor = Math.Min(floor, fog.Y[i]);
            }
        }
        Check(Math.Abs(top - EdgeRules.Height) < 1e-3 && floor <= -1.0, "uptown edges: the curtains run from under the ground to the lid's height, so the two meet with no gap");
        // Each stretch's layers, found by where they stand: four, 0, 4, 8 and 12 m out, thickening to a whole one.
        bool offsets = true, rising = true, whole = true, soft = true, depth = true;
        foreach (EdgeSeg e in edges)
        {
            var found = new SortedDictionary<double, double>();
            for (int i = 0; i < fog.Vertices; i++)
            {
                if (fog.NormalY[i] >= 0.5f || fog.Y[i] > 0f)
                {
                    continue;
                }
                double x = fog.X[i], z = fog.Z[i], out0 = (x - e.X0) * e.OutX + (z - e.Z0) * e.OutZ, along = (x - e.X0) * -e.OutZ + (z - e.Z0) * e.OutX;
                double length = e.Length * (e.OutX != 0 ? 1.0 : 1.0);
                double span = (e.X1 - e.X0) * -e.OutZ + (e.Z1 - e.Z0) * e.OutX;
                if (Math.Abs(fog.NormalX[i] + e.OutX) < 1e-6 && Math.Abs(fog.NormalZ[i] + e.OutZ) < 1e-6 && out0 > -1e-6 && out0 < 13.0 && along >= Math.Min(0.0, span) - 13.0 && along <= Math.Max(0.0, span) + 13.0 && length > 0)
                {
                    found[Math.Round(out0, 3)] = fog.U[i];
                }
            }
            offsets &= found.Count == layers && found.Keys.Select((k, i) => Math.Abs(k - EdgeRules.CurtainOffset[i]) < 1e-3).All(b => b);
            double[] us = found.Values.ToArray();
            rising &= us.Length == layers && Enumerable.Range(1, Math.Max(0, us.Length - 1)).All(i => us[i] > us[i - 1]);
            if (us.Length == layers)
            {
                foreach (double noise in new[] { 0.0, 0.346, 0.863 })
                {
                    double left = 1.0;
                    foreach (double u in us)
                    {
                        left *= 1.0 - EdgeRules.Alpha(u, noise);
                    }
                    whole &= left < 1e-6;   // nothing past the last layer shows through, whatever the noise
                }
                double first = EdgeRules.Alpha(us[0], 0.863), second = 1.0 - (1.0 - EdgeRules.Alpha(us[0], 0.863)) * (1.0 - EdgeRules.Alpha(us[1], 0.863));
                soft &= first < 0.35 && second < 0.55;   // the first layers let most through even at the noise's most: fog fading in, not a wall
                depth &= EdgeRules.CurtainOffset[layers - 1] - EdgeRules.CurtainOffset[0] >= 10.0;
            }
        }
        Check(offsets, "uptown edges: every stretch has its four layers 0, 4, 8 and 12 m out from the wall's inner face");
        Check(rising && whole, "uptown edges: each layer is thicker than the one before, and the four together let nothing through whatever the game's noise (alpha = smoothstep(uv0.x * 500 * (1 + noise) / 60))");
        Check(soft && depth, "uptown edges: it fades in over 12 m, not a hard wall: the first layer is under a third even at the noise's most, the first two under a half");
        Check(Math.Abs(EdgeRules.LidAt(0f)) < 1e-9 && Math.Abs(EdgeRules.LidAt(EdgeRules.LidFull) - EdgeRules.WholeU) < 1e-6 && Math.Abs(EdgeRules.LidAt(EdgeRules.LidHold) - EdgeRules.WholeU) < 1e-6
              && Math.Abs(EdgeRules.LidAt(EdgeRules.LidReach)) < 1e-9 && EdgeRules.Alpha(EdgeRules.WholeU, 0.0) > 0.999
              && Enumerable.Range(0, 120).All(i => EdgeRules.LidAt(EdgeRules.LidFull + i * 0.7f) >= EdgeRules.WholeU - 1e-6 || EdgeRules.LidFull + i * 0.7f > EdgeRules.LidHold),
            "uptown edges: the lid is nothing on the edge, whole from 12 m to 100 m out and gone by 170 m, where the game's own fog (whole 136 m from a hole 21 m out or more) has taken over");
        // Nothing in play is covered, and the ground beyond the edge is.
        bool clear = true;
        for (int i = 0; i < fog.Vertices; i++)
        {
            clear &= !EdgePlay(fog.X[i], fog.Z[i], 1e-3);
        }
        for (int t = 0; t + 2 < fog.Triangles.Count; t += 3)
        {
            int a = fog.Triangles[t], b = fog.Triangles[t + 1], c = fog.Triangles[t + 2];
            clear &= !EdgePlay((fog.X[a] + fog.X[b] + fog.X[c]) / 3.0, (fog.Z[a] + fog.Z[b] + fog.Z[c]) / 3.0, 1e-3);
            clear &= !EdgePlay((fog.X[a] + fog.X[b]) / 2.0, (fog.Z[a] + fog.Z[b]) / 2.0, 1e-3) && !EdgePlay((fog.X[b] + fog.X[c]) / 2.0, (fog.Z[b] + fog.Z[c]) / 2.0, 1e-3);
        }
        var grounds = new List<GroundBox>(512);
        grounds.AddRange(LayoutPlans.PlayerGround().Boxes);
        grounds.AddRange(LayoutPlans.NpcGround().Boxes);
        bool ground = true;
        for (int i = 0; i < fog.Vertices; i++)
        {
            foreach (GroundBox g in grounds)
            {
                ground &= !(Math.Abs(fog.X[i] - g.X) < g.HalfX - 1e-3 && Math.Abs(fog.Z[i] - g.Z) < g.HalfZ - 1e-3);
            }
        }
        Check(clear && ground, "uptown edges: no fog vertex, edge or middle of a triangle lies inside the districts' ground, and no fog vertex over any piece of the ground the player or the people walk: the parts in play stay clear");
        bool covered = true, bare = true;
        int sampled = 0, spared = 0;
        foreach (EdgeSeg e in edges)
        {
            double len = e.Length;
            for (double s = 3.0; s < len - 3.0; s += 7.3)
            {
                double t = s / len, x = e.X0 + (e.X1 - e.X0) * t, z = e.Z0 + (e.Z1 - e.Z0) * t;
                for (double d = 1.0; d <= 160.0; d += 13.0)
                {
                    sampled++;
                    covered &= EdgeUnderLid(fog, x + e.OutX * d, z + e.OutZ * d);
                }
            }
        }
        for (double x = LayoutPlans.RingWest + 3.0; x < LayoutPlans.RingEast; x += 23.0)
        {
            for (double z = LayoutPlans.RowOneFrom + 3.0; z < LayoutPlans.RingNorth; z += 17.0)
            {
                spared++;
                bare &= !EdgeUnderLid(fog, x, z);
            }
        }
        for (double x = LayoutPlans.West + 3.0; x < LayoutPlans.East; x += 23.0)
        {
            for (double z = LayoutPlans.SouthWall + 1.0; z < LayoutPlans.RowOneFrom; z += 7.0)
            {
                spared++;
                bare &= !EdgeUnderLid(fog, x, z);
            }
        }
        Check(covered && sampled > 1500, "uptown edges: the lid lies over the ground beyond every stretch of edge, 1 to 160 m out (" + sampled + " points)");
        Check(bare && spared > 800, "uptown edges: and over no part of the districts' ground, the band, the ring and 1st Avenue's strip (" + spared + " points)");
        EdgeFogData again = EdgeRules.Fog(edges);
        Check(again.Vertices == fog.Vertices && again.Triangles.SequenceEqual(fog.Triangles) && again.X.SequenceEqual(fog.X) && again.Z.SequenceEqual(fog.Z) && again.U.SequenceEqual(fog.U),
            "uptown edges: the same fog twice");
    }

    // ---------------------------------------------------------------- the street ends

    private static void UptownEdgeEnds(List<EdgeSeg> edges, List<Barricade> ends)
    {
        Check(ends.Count == 19 + 2 * 4 && ends.Select(b => b.Name).Distinct().Count() == ends.Count && ends.Count(b => !b.Avenue) == 19 && ends.Count(b => b.Avenue) == 8,
            "uptown edges: " + ends.Count + " street ends closed: the nineteen streets' north ends and the four avenues' west and east ends, each with a name of its own");
        // The ways out of the plan's junctions that no lane takes and that run into an edge: found from the lanes, not from the list.
        RoadPlan plan = LayoutPlans.Streets();
        var arms = new List<OpenArm>(32);
        EdgeRules.OpenArms(plan, edges, arms);
        bool matched = arms.Count == ends.Count;
        int paired = 0;
        foreach (OpenArm arm in arms)
        {
            int hits = ends.Count(b => Math.Abs(b.OutX - arm.DirX) < 1e-6 && Math.Abs(b.OutZ - arm.DirZ) < 1e-6
                                        && (arm.DirX != 0f ? Math.Abs(b.Cz - arm.Z) < 0.5 && (b.Cx - arm.X) * arm.DirX > 0 : Math.Abs(b.Cx - arm.X) < 0.5 && (b.Cz - arm.Z) * arm.DirZ > 0)
                                        && Math.Abs((arm.DirX != 0f ? b.Cx - arm.X : b.Cz - arm.Z)) <= arm.Reach + 0.01);
            matched &= hits == 1;
            paired += hits;
        }
        Check(matched && paired == ends.Count, "uptown edges: every way out of a junction that no lane takes and that runs into an edge within " + EdgeRules.Reach + " m has exactly one barricade, and every barricade is such a way out (" + arms.Count + " ways, " + ends.Count + " ends)");
        bool streets = LayoutPlans.Streets().Roads.Count > 100 && Enumerable.Range(LayoutStreets.WestMost, 19).All(k => ends.Count(b => !b.Avenue && b.Index == k) == 1)
                       && Enumerable.Range(0, LayoutRules.Rows + 1).All(r => ends.Count(b => b.Avenue && b.Index == r && b.OutX < 0) == 1 && ends.Count(b => b.Avenue && b.Index == r && b.OutX > 0) == 1);
        Check(streets, "uptown edges: every one of the nineteen streets and each avenue's two ends has its barricade, once");
        // The box: as wide as the street or avenue between the ring's strips, from just past the band's edge, short of the wall behind it.
        bool width = true, between = true, clear = true, shape = true;
        var strips = new List<GroundRect>(32);
        for (int row = 1; row <= LayoutRules.Rows + 1; row++)
        {
            for (int column = 0; column <= LayoutRules.Columns + 1; column++)
            {
                if (LayoutRules.IsRing(row, column))
                {
                    LayoutRules.RingStrip(row, column, out float x0, out float z0, out float x1, out float z1, out _);
                    strips.Add(new GroundRect(x0, z0, x1, z1));
                }
            }
        }
        var blocks = new List<GroundRect>(64);
        for (int row = 1; row <= LayoutRules.Rows; row++)
        {
            for (int column = 1; column <= LayoutRules.Columns; column++)
            {
                LayoutRules.Block(row, column, out float x0, out float z0, out float x1, out float z1);
                blocks.Add(new GroundRect(x0, z0, x1, z1));
            }
        }
        foreach (Barricade b in ends)
        {
            PlanBox w = b.Wall;
            double across = b.Avenue ? w.HalfZ : w.HalfX, deep = b.Avenue ? w.HalfX : w.HalfZ;
            width &= Math.Abs(across - (b.Avenue ? LayoutRules.AvenueHalf : LayoutRules.StreetHalf)) < 1e-3 && Math.Abs(b.Half - across) < 1e-3;
            double inner = b.Avenue ? (b.OutX < 0 ? w.X + w.HalfX : w.X - w.HalfX) : w.Z - w.HalfZ;
            double edge = b.Avenue ? b.Cx : b.Cz;
            shape &= Math.Abs(Math.Abs(inner - edge) - EdgeRules.WallGap) < 1e-3 && Math.Abs(deep * 2.0 - EdgeRules.Depth) < 1e-3 && Math.Abs(w.Top - EdgeRules.WallTop) < 1e-6;
            // short of the invisible wall behind it, and the corridor's sides: the ring's strips and the band's blocks
            double far = b.Avenue ? (b.OutX < 0 ? w.X - w.HalfX : w.X + w.HalfX) : w.Z + w.HalfZ;
            between &= b.Avenue ? (b.OutX < 0 ? far > LayoutPlans.RingWest : far < LayoutPlans.RingEast) : far < LayoutPlans.RingNorth;
            foreach (GroundRect r in strips.Concat(blocks))
            {
                clear &= !(Math.Abs(w.X - (r.X1 + r.X2) / 2) < w.HalfX + (r.X2 - r.X1) / 2 - 1e-3 && Math.Abs(w.Z - (r.Z1 + r.Z2) / 2) < w.HalfZ + (r.Z2 - r.Z1) / 2 - 1e-3);
            }
        }
        Check(width && shape, "uptown edges: each box is as wide as its street or avenue from pavement to pavement, starts " + EdgeRules.WallGap + " m past the band's edge, " + EdgeRules.Depth + " m deep and " + EdgeRules.WallTop + " m high");
        Check(between && clear, "uptown edges: each box lies short of the invisible wall behind it and shares no ground with any ring strip or block of the districts");
        // The pieces.
        var pieces = new List<PiecePlace>(24);
        bool fenced = true, facing = true, inside = true, order = true, counts = true, stretch = true;
        int total = 0;
        foreach (Barricade b in ends)
        {
            EdgeRules.Pieces(b, pieces);
            total += pieces.Count;
            var panels = pieces.Where(p => p.Kind == PieceKind.Hoarding).ToList();
            double span = b.Half * 2.0, pitch = span / Math.Max(1, panels.Count);
            fenced &= panels.Count == (int)Math.Round(span / EdgeRules.PanelWidth) && panels.All(p => Math.Abs(p.Stretch * EdgeRules.PanelWidth - pitch) < 1e-3);
            stretch &= panels.All(p => p.Stretch > 0.95f && p.Stretch < 1.1f);
            counts &= pieces.Count(p => p.Kind == PieceKind.Sign) == (b.Avenue ? 2 : 1) && pieces.Count(p => p.Kind == PieceKind.Cone) == (b.Avenue ? 4 : 2);
            foreach (PiecePlace p in pieces)
            {
                double yaw = p.Yaw * Math.PI / 180.0;
                facing &= Math.Abs(Math.Sin(yaw) + b.OutX) < 1e-4 && Math.Abs(Math.Cos(yaw) + b.OutZ) < 1e-4;
                inside &= Math.Abs(p.X - b.Wall.X) < b.Wall.HalfX && Math.Abs(p.Z - b.Wall.Z) < b.Wall.HalfZ;
            }
            double behind = panels.Count > 0 ? (panels[0].X - b.Cx) * b.OutX + (panels[0].Z - b.Cz) * b.OutZ : 0.0;
            order &= pieces.Where(p => p.Kind != PieceKind.Hoarding).All(p => (p.X - b.Cx) * b.OutX + (p.Z - b.Cz) * b.OutZ < behind);
            if (panels.Count > 1)
            {
                double along0 = panels[0].X * -b.OutZ + panels[0].Z * b.OutX, along1 = panels[1].X * -b.OutZ + panels[1].Z * b.OutX;
                fenced &= Math.Abs(Math.Abs(along1 - along0) - pitch) < 1e-3;
            }
        }
        Check(fenced && stretch, "uptown edges: a row of construction fencing across the whole width of every end, panels edge to edge (stretched at most a tenth)");
        Check(counts && order, "uptown edges: roadworks signs (one on a street, two on an avenue) and cones (two, four) before the fencing at every end");
        Check(facing && inside, "uptown edges: every piece faces into the districts and stands inside its barricade's box");
        Check(total == EdgeRules.PieceCount(ends) && total == 19 * 10 + 8 * 17, "uptown edges: " + total + " pieces in all, and the count the game's side is held to");
        // The props: the mod's own, in the bundle that is closed or open already; none new.
        string contents = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\PropAssets\contents"));
        var known = new HashSet<string>();
        foreach (string file in Directory.GetFiles(contents, "*.json"))
        {
            using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(file));
            foreach (JsonElement prop in doc.RootElement.GetProperty("props").EnumerateArray())
            {
                known.Add(Path.GetFileNameWithoutExtension(file) + ":" + prop.GetProperty("id").GetString());
            }
        }
        Check(EdgeRules.PropIds.All(id => known.Contains("reallife_props:" + id)) && EdgeRules.PropIds.Length == 3,
            "uptown edges: fencing, roadworks sign and cone are props the mod's first bundle already holds (" + string.Join(", ", EdgeRules.PropIds) + "): no new prop, nothing added to the second bundle");
    }

    // ---------------------------------------------------------------- what crosses a barricade

    private static void UptownEdgeCrossing(List<Barricade> ends)
    {
        RoadPlan plan = LayoutPlans.Streets();
        string lane = null;
        foreach (Barricade b in ends)
        {
            for (int r = 0; r < plan.Roads.Count && lane == null; r++)
            {
                if (EdgeRules.LaneTouches(plan.Roads[r], b))
                {
                    lane = b.Name + " by road " + r;
                }
            }
        }
        Check(lane == null, "uptown edges: no traffic lane (car, bus, taxi, a rival's drive) comes within its own width and half a metre of a barricade" + (lane != null ? ": " + lane : string.Empty));
        bool endsAtJunction = true;
        foreach (PlanJunction j in plan.Junctions)
        {
            foreach (Barricade b in ends)
            {
                endsAtJunction &= !(Math.Abs(j.X - b.Wall.X) < b.Wall.HalfX + 0.5 && Math.Abs(j.Z - b.Wall.Z) < b.Wall.HalfZ + 0.5);
            }
        }
        Check(endsAtJunction, "uptown edges: no junction of the plan, with its lights and its turns, lies at a barricade");
        string walk = null;
        foreach (GroundPlan g in new[] { LayoutPlans.PlayerGround(), LayoutPlans.NpcGround() })
        {
            foreach (GroundBox box in g.Boxes)
            {
                foreach (Barricade b in ends)
                {
                    if (walk == null && EdgeRules.Overlap(b.Wall, box.X, box.Z, box.HalfX, box.HalfZ))
                    {
                        walk = g.Name + " at " + b.Name;
                    }
                }
            }
        }
        Check(walk == null, "uptown edges: no walking ground (the player's, the people's) lies across a barricade: nobody's path goes through one" + (walk != null ? ": " + walk : string.Empty));
        var zones = new List<PlanBox>(256);
        LayoutPlans.Zones(zones);
        LayoutPlans.ParkZones(zones);
        var termini = new List<PlanTerminus>(4);
        LayoutPlans.Termini(termini);
        var grounds = new List<PlanBox>(4);
        var roads = new List<PlanBox>(80);
        LayoutPlans.Colliders(grounds, roads);
        bool zonesClear = zones.All(z => ends.All(b => !EdgeRules.Overlap(b.Wall, z.X, z.Z, z.HalfX, z.HalfZ)));
        bool terminiClear = termini.All(t => ends.All(b => !b.Wall.Holds(t.X, t.Z, 1.0f)));
        bool roadsClear = roads.All(r => ends.All(b => !EdgeRules.Overlap(b.Wall, r.X, r.Z, r.HalfX, r.HalfZ)));
        Check(zonesClear && terminiClear && roadsClear, "uptown edges: no pedestrian zone, bus terminus or road collider lies at a barricade");
        // The ground ends right at the barricade: the player stops at it and sees the street closed, not a gap of road.
        bool snug = true;
        GroundPlan player = LayoutPlans.PlayerGround();
        foreach (Barricade b in ends)
        {
            double inner = b.Avenue ? (b.OutX < 0 ? b.Wall.X + b.Wall.HalfX : b.Wall.X - b.Wall.HalfX) : b.Wall.Z - b.Wall.HalfZ;
            double nearest = double.MaxValue;
            foreach (GroundBox box in player.Boxes)
            {
                double across = b.Avenue ? box.Z : box.X, acrossMid = b.Avenue ? b.Cz : b.Cx;
                if (Math.Abs(across - acrossMid) > (b.Avenue ? box.HalfZ : box.HalfX))
                {
                    continue;
                }
                double face = b.Avenue ? (b.OutX < 0 ? box.X - box.HalfX : box.X + box.HalfX) : box.Z + box.HalfZ;
                double gap = Math.Abs(inner - face);
                if (b.Avenue ? (b.OutX < 0 ? face > inner - 1e-9 : face < inner + 1e-9) : face < inner + 1e-9)
                {
                    nearest = Math.Min(nearest, gap);
                }
            }
            snug &= nearest <= 0.7;
        }
        Check(snug, "uptown edges: the player's ground ends within 0.7 m of every barricade's box, so the street reads closed at the point the player stops");
    }

    // ---------------------------------------------------------------- the time it takes

    private static void UptownEdgeTiming()
    {
        var edges = new List<EdgeSeg>(8);
        var ends = new List<Barricade>(32);
        var arms = new List<OpenArm>(32);
        var pieces = new List<PiecePlace>(24);
        RoadPlan plan = LayoutPlans.Streets();
        var watch = Stopwatch.StartNew();
        int sum = 0;
        for (int i = 0; i < 100; i++)
        {
            EdgeRules.Edges(edges);
            EdgeRules.Ends(ends);
            sum += EdgeRules.Fog(edges).Vertices + EdgeRules.PieceCount(ends);
            EdgeRules.OpenArms(plan, edges, arms);
            foreach (Barricade b in ends)
            {
                EdgeRules.Pieces(b, pieces);
            }
        }
        watch.Stop();
        Check(sum > 0 && watch.ElapsedMilliseconds < 1500, "uptown edges: the edges, the ends, the fog, the pieces and the open ways laid a hundred times in " + watch.ElapsedMilliseconds + " ms (well under 15 ms each, once a load)");
    }

    // ---------------------------------------------------------------- the sources

    private static void UptownEdgeSources(string scripts)
    {
        string folder = Path.Combine(scripts, "Uptown");
        string rules = Code(File.ReadAllText(Path.Combine(folder, "EdgeRules.cs")));
        string game = Code(File.ReadAllText(Path.Combine(folder, "GroundGame.Edges.cs")));
        string director = Code(File.ReadAllText(Path.Combine(folder, "GroundDirector.Edges.cs")));
        string main = Code(File.ReadAllText(Path.Combine(folder, "GroundDirector.cs")));
        string ground = Code(File.ReadAllText(Path.Combine(folder, "GroundGame.cs")));
        string guide = Code(File.ReadAllText(Path.Combine(folder, "GroundGuide.cs")));
        int Order(string text, string what) => text.IndexOf(what, StringComparison.Ordinal);
        string Body(string text, string head)
        {
            int at = Order(text, head);
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
        Check(!Regex.IsMatch(rules, @"\busing UnityEngine|\bVector3\b|\bMathf\b|\bMesh\b|\bGameObject\b"), "uptown edges sources: the rules and the data game-free");
        Check(!Regex.IsMatch(rules + game + director, @"\bvoid\s+(?:Update|LateUpdate|FixedUpdate|OnGUI)\s*\(|HarmonyLib|\[Harmony|\bGetField\(|\bGetProperty\(|\bGetMethod\(|BindingFlags|FindObjects|GameObject\.Find\(|\.Find\(""|InvokeRepeating|\bLinq\b"),
            "uptown edges sources: nothing per frame, no patch of the game's, nothing read by name, no search of the scene, no LINQ");
        string stand = Body(game, "private static string StandFog(");
        Check(Order(stand, "source.shader.name") >= 0 && Order(stand, "shader != EdgeFogShader") >= 0 && Order(stand, "HasProperty(EdgeFogProperties[i])") >= 0
              && Order(stand, "new Material(source)") > Order(stand, "HasProperty(EdgeFogProperties[i])") && Regex.Matches(game, @"new Material\(").Count == 1 && game.Contains("\"Shader Graphs/BorderFog\""),
            "uptown edges sources: the game's fog material is copied only after its shader and the properties the copy changes are matched; a fog that differs is left alone with a reason, and the game's own material is never changed");
        Check(Regex.IsMatch(stand, @"SetFloat\(ZTestForward, \(float\)CompareFunction\.LessEqual\)") && Regex.IsMatch(stand, @"SetFloat\(ZTestTransparent, \(float\)CompareFunction\.LessEqual\)")
              && game.Contains("\"_ZTestDepthEqualForOpaque\"") && !stand.Contains("source.Set") && !Regex.IsMatch(stand, @"renderer\.sharedMaterial\s*=[^=]"),
            "uptown edges sources: the copy's depth test is the usual one (the game's is Always, drawn over everything from the map), set on the copy alone");
        Check(Regex.Matches(stand, @"new Mesh\b").Count == 1 && Regex.Matches(stand, @"AddComponent<MeshRenderer>").Count == 1 && stand.Contains("ShadowCastingMode.Off") && stand.Contains("motionVectorGenerationMode"),
            "uptown edges sources: the fog is one mesh and one renderer, no shadows, no motion vectors: one draw call");
        string drop = Body(game, "public static void DropEdges()");
        Check(Order(drop, "Object.Destroy(_edgeFogMesh);") >= 0 && Order(drop, "Object.Destroy(_edgeFogMaterial);") >= 0 && Order(drop, "_edgeFogMesh = null;") > Order(drop, "Object.Destroy(_edgeFogMesh);"),
            "uptown edges sources: the fog's mesh and material destroyed when the district goes");
        Check(Regex.IsMatch(ground, @"public static void Forget\(\)\s*\{\s*DropArt\(\);\s*UncutFog\(\);\s*DropEdges\(\);") && Regex.IsMatch(ground, @"public static void GiveBack\(\)\s*\{[^}]*Forget\(\);"),
            "uptown edges sources: everything of the edges goes when the district is given back the same load and when the city goes");
        Check(!Regex.IsMatch(game, @"(?<![\w.])(?:Object\.)?Destroy\(\s*_(?:edges|pieces)\b") && game.Contains("SetParent(_ground.transform, false)") && game.Contains("SetParent(_edges.transform, false)"),
            "uptown edges sources: the objects hang under the district's own and go with it, none destroyed on its own");
        string lay = Body(main, "private void Lay()");
        Check(Order(lay, "LayEdges();") > Order(lay, "LayFog();") && Order(lay, "LayEdges();") < Order(lay, "TransitMap.AddTerminus(") && Regex.Matches(main + director, @"\bLayEdges\(\);").Count == 1
              && Order(lay, "GroundGame.Build(") < Order(lay, "LayEdges();"),
            "uptown edges sources: the edges laid once a load, behind the loading screen, once the walls stand");
        string layEdges = Body(director, "private void LayEdges()");
        string late = Body(director, "private bool LatePieces()");
        Check(Order(layEdges, "GroundGame.LayEdges(_ends, EdgeRules.Fog(_edges))") >= 0 && Order(layEdges, "ScenePrewarm.AddLate(") > 0 && Order(late, "PropLibrary.Settled") >= 0 && Order(late, "return false;") > Order(late, "PropLibrary.Settled")
              && Order(late, "GroundGame.LayPieces(_ends)") > Order(late, "return false;"),
            "uptown edges sources: the pieces wait for the props bundle (the game's late call, or as soon after as it has loaded)");
        Check(Order(late, "if (Meta.Dev.Verbose)") >= 0 && Order(late, "\"Uptown check (edges): \" + line") >= 0 && Order(director, "\" street ends closed, \"") >= 0 && Order(director, "\" fog segments\"") >= 0
              && Order(director, "\"Uptown check (edges): \" + line + \": PASS\"") >= 0 && Order(director, "\"Uptown check (edges): \" + line + \": FAIL\"") >= 0,
            "uptown edges sources: the load's line with Developer diagnostics on and the test's say \"Uptown check (edges): N street ends closed, N fog segments\" and PASS or FAIL");
        Check(guide.Contains(".Check(\"reallife_uptown_ground_test_edges\", GroundDirector.TestEdges, \"Uptown check (edges)\")"), "uptown edges sources: the test in the feature's list and the city expansion's run");
        string pieces = Body(game, "public static string LayPieces(");
        Check(Order(pieces, "PropLibrary.Ready") >= 0 && Order(pieces, "PropLibrary.Has(EdgeRules.PropIds[k])") >= 0 && Order(pieces, "PropLibrary.TryGet(id, out Mesh mesh, out Material material)") >= 0 && !pieces.Contains("new Mesh") && !pieces.Contains("new Material"),
            "uptown edges sources: the pieces are the bundle's shared meshes and materials, none made here, and each prop is asked for before it is used");
    }
}
