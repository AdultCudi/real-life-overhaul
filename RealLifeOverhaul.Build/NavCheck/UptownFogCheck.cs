using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using RealLifeOverhaul.Uptown;

// The city map's fog over Uptown (Uptown/LayoutFog.cs, GroundGame.Fog.cs and GroundDirector.Fog.cs): the cut's data held
// against the game's own fog of Build 3682 (UptownFogGame.cs, its triangles; its vertices are LayoutFog.Game's) and against
// the band as the mod lays it (LayoutRules.BandGround, LayoutPlans.MapBounds). Indices and attributes valid, every triangle
// the game's way round, none over the band's ground or the map's widened bounds over it, the game's outline and its one hole
// kept but where the cut meets them, every point of the game's fog outside the cut still fog, and the field the game's or
// the cut's ramp at the game's rate. Over the sources: the cut stood down on a fog that is not Build 3682's, the game's mesh
// back on every way out, and nothing per frame. Geometry in doubles, from the floats as they ship.
internal static partial class Program
{
    // The fog material's _Threshold and _EdgeSmooth (Build 3682, navlab facts): its pass makes the alpha
    // smoothstep(saturate(uv0.x * _Threshold * (1 + noise) / _EdgeSmooth)), so the fog is whole from uv0.x 0.12.
    private const double FogThreshold = 500.0;
    private const double FogSmooth = 60.0;
    private const double FogWhole = FogSmooth / FogThreshold;
    private static readonly double[] FogNoise = { 0.0, 0.346, 0.863 };   // its noise texture's red, linear: least, mean, most
    private const double FogGameBoundsNorth = 467.0;   // m: the game's own city map bounds (CityMapCam, level3, Build 3682), north and east,
    private const double FogGameBoundsEast = 506.0;    // past which its sheet reaches 687 and 671 m, more than the map's widest view

    private sealed class FogMesh
    {
        public double[] X, Z, U;   // world metres, uv0.x
        public int[] T;            // three a triangle
        private Dictionary<(int, int), List<int>> _grid;

        public int Triangles => T.Length / 3;

        // uv0.x where a triangle covers the point (its edges included), or null.
        public double? At(double x, double z, double eps = 1e-9)
        {
            if (_grid == null)
            {
                _grid = new Dictionary<(int, int), List<int>>();
                for (int t = 0; t < Triangles; t++)
                {
                    int a = T[t * 3], b = T[t * 3 + 1], c = T[t * 3 + 2];
                    int gx0 = (int)Math.Floor(Math.Min(X[a], Math.Min(X[b], X[c])) / 50.0), gx1 = (int)Math.Floor(Math.Max(X[a], Math.Max(X[b], X[c])) / 50.0);
                    int gz0 = (int)Math.Floor(Math.Min(Z[a], Math.Min(Z[b], Z[c])) / 50.0), gz1 = (int)Math.Floor(Math.Max(Z[a], Math.Max(Z[b], Z[c])) / 50.0);
                    for (int gx = gx0; gx <= gx1; gx++)
                    {
                        for (int gz = gz0; gz <= gz1; gz++)
                        {
                            if (!_grid.TryGetValue((gx, gz), out List<int> cell))
                            {
                                _grid[(gx, gz)] = cell = new List<int>();
                            }
                            cell.Add(t);
                        }
                    }
                }
            }
            if (!_grid.TryGetValue(((int)Math.Floor(x / 50.0), (int)Math.Floor(z / 50.0)), out List<int> near))
            {
                return null;
            }
            foreach (int t in near)
            {
                int a = T[t * 3], b = T[t * 3 + 1], c = T[t * 3 + 2];
                double d = (Z[b] - Z[c]) * (X[a] - X[c]) + (X[c] - X[b]) * (Z[a] - Z[c]);
                double l1 = ((Z[b] - Z[c]) * (x - X[c]) + (X[c] - X[b]) * (z - Z[c])) / d;
                double l2 = ((Z[c] - Z[a]) * (x - X[c]) + (X[a] - X[c]) * (z - Z[c])) / d;
                double l3 = 1.0 - l1 - l2;
                if (l1 >= -eps && l2 >= -eps && l3 >= -eps)
                {
                    return l1 * U[a] + l2 * U[b] + l3 * U[c];
                }
            }
            return null;
        }
    }

    private static FogMesh FogMade()
    {
        int n = LayoutFog.VertexCount, g = LayoutFog.GameVertexCount;
        var m = new FogMesh { X = new double[n], Z = new double[n], U = new double[n], T = LayoutFog.Triangles.Select(i => (int)i).ToArray() };
        for (int i = 0; i < n; i++)
        {
            bool game = i < g;
            int k = game ? i * LayoutFog.GameStride : (i - g) * LayoutFog.MadeStride;
            float[] data = game ? LayoutFog.Game : LayoutFog.Made;
            m.X[i] = (double)data[k] + LayoutFog.PlaceX;
            m.Z[i] = (double)data[k + (game ? 2 : 1)] + LayoutFog.PlaceZ;
            m.U[i] = data[k + (game ? 3 : 2)];
        }
        return m;
    }

    private static FogMesh FogOfTheGame()
    {
        FogMesh made = FogMade();
        int g = LayoutFog.GameVertexCount;
        return new FogMesh { X = made.X.Take(g).ToArray(), Z = made.Z.Take(g).ToArray(), U = made.U.Take(g).ToArray(), T = UptownFogGame.Triangles.Select(i => (int)i).ToArray() };
    }

    private static double FogAlpha(double u, double noise)
    {
        double t = Math.Max(0.0, Math.Min(1.0, u * FogThreshold * (1.0 + noise) / FogSmooth));
        return Math.Min(1.0, t * t * (3.0 - 2.0 * t));
    }

    // The cut in the world, and how far a point is from it.
    private static double FogCutDistance(double x, double z)
    {
        double dx = Math.Max(Math.Max(LayoutFog.CutWest - x, 0.0), x - LayoutFog.CutEast);
        double dz = Math.Max(Math.Max(LayoutFog.CutSouth - z, 0.0), z - LayoutFog.CutNorth);
        return Math.Sqrt(dx * dx + dz * dz);
    }

    private static bool FogInCut(double x, double z)
    {
        return x >= LayoutFog.CutWest && x <= LayoutFog.CutEast && z >= LayoutFog.CutSouth && z <= LayoutFog.CutNorth;
    }

    // True when a triangle and the cut share some area: separating axes, exact for two convex shapes.
    private static bool FogOverlapsCut(FogMesh m, int t)
    {
        double[] px = { m.X[m.T[t * 3]], m.X[m.T[t * 3 + 1]], m.X[m.T[t * 3 + 2]] };
        double[] pz = { m.Z[m.T[t * 3]], m.Z[m.T[t * 3 + 1]], m.Z[m.T[t * 3 + 2]] };
        double[] rx = { LayoutFog.CutWest, LayoutFog.CutEast, LayoutFog.CutEast, LayoutFog.CutWest };
        double[] rz = { LayoutFog.CutSouth, LayoutFog.CutSouth, LayoutFog.CutNorth, LayoutFog.CutNorth };
        var axes = new List<(double, double)> { (1.0, 0.0), (0.0, 1.0) };
        for (int i = 0; i < 3; i++)
        {
            axes.Add((-(pz[(i + 1) % 3] - pz[i]), px[(i + 1) % 3] - px[i]));
        }
        foreach ((double ax, double az) in axes)
        {
            double t0 = double.MaxValue, t1 = double.MinValue, r0 = double.MaxValue, r1 = double.MinValue;
            for (int i = 0; i < 3; i++)
            {
                double v = ax * px[i] + az * pz[i];
                t0 = Math.Min(t0, v);
                t1 = Math.Max(t1, v);
            }
            for (int i = 0; i < 4; i++)
            {
                double v = ax * rx[i] + az * rz[i];
                r0 = Math.Min(r0, v);
                r1 = Math.Max(r1, v);
            }
            if (t1 <= r0 || r1 <= t0)
            {
                return false;
            }
        }
        return true;
    }

    // The boundary loops of a mesh, by position: each a list of points round it.
    private static List<List<(double, double)>> FogLoops(FogMesh m)
    {
        var count = new Dictionary<((double, double), (double, double)), int>();
        for (int t = 0; t < m.Triangles; t++)
        {
            for (int e = 0; e < 3; e++)
            {
                int a = m.T[t * 3 + e], b = m.T[t * 3 + (e + 1) % 3];
                var key = ((m.X[a], m.Z[a]), (m.X[b], m.Z[b]));
                count[key] = count.TryGetValue(key, out int c) ? c + 1 : 1;
            }
        }
        var next = new Dictionary<(double, double), (double, double)>();
        foreach (var edge in count.Keys)
        {
            if (!count.ContainsKey((edge.Item2, edge.Item1)))
            {
                next[edge.Item1] = edge.Item2;
            }
        }
        var loops = new List<List<(double, double)>>();
        var seen = new HashSet<(double, double)>();
        foreach ((double, double) start in next.Keys)
        {
            if (seen.Contains(start))
            {
                continue;
            }
            var loop = new List<(double, double)>();
            (double, double) at = start;
            while (seen.Add(at))
            {
                loop.Add(at);
                if (!next.TryGetValue(at, out at))
                {
                    break;
                }
            }
            loops.Add(loop);
        }
        return loops;
    }

    private static double FogArea(List<(double, double)> loop)
    {
        double sum = 0.0;
        for (int i = 0; i < loop.Count; i++)
        {
            (double x1, double z1) = loop[i];
            (double x2, double z2) = loop[(i + 1) % loop.Count];
            sum += x1 * z2 - x2 * z1;
        }
        return sum / 2.0;
    }

    private static void UptownFogChecks(string scripts)
    {
        UptownFogDataChecks();
        UptownFogCutChecks();
        UptownFogShapeChecks();
        UptownFogFieldChecks();
        UptownFogSourceChecks(scripts);
    }

    // ---------------------------------------------------------------- the data

    private static void UptownFogDataChecks()
    {
        int n = LayoutFog.VertexCount, g = LayoutFog.GameVertexCount;
        ushort[] tris = LayoutFog.Triangles, game = UptownFogGame.Triangles;
        Check(LayoutFog.Game.Length == g * LayoutFog.GameStride && LayoutFog.Made.Length % LayoutFog.MadeStride == 0 && n == g + LayoutFog.Made.Length / LayoutFog.MadeStride
              && tris.Length % 3 == 0 && tris.All(i => i < n) && n < 65536,
            "uptown fog: " + n + " vertices (the game's " + g + ", " + (n - g) + " made), " + tris.Length / 3 + " triangles, every index one of them, 16 bits enough");
        Check(game.Length == LayoutFog.GameIndexCount && game.All(i => i < g), "uptown fog: the game's own " + game.Length / 3 + " triangles over its " + g + " vertices, as its mesh reports them");
        // The kept triangles are the game's own, index for index, in its order.
        int at = 0;
        bool kept = LayoutFog.Kept > 0;
        for (int k = 0; k < LayoutFog.Kept && kept; k++)
        {
            while (at < game.Length / 3 && !(game[at * 3] == tris[k * 3] && game[at * 3 + 1] == tris[k * 3 + 1] && game[at * 3 + 2] == tris[k * 3 + 2]))
            {
                at++;
            }
            kept &= at < game.Length / 3;
            at++;
        }
        Check(kept, "uptown fog: its first " + LayoutFog.Kept + " triangles are the game's own, index for index, in the game's order");
        // The figures the mod checks the game's fog against are those of the vertices it holds.
        double x0 = double.MaxValue, x1 = double.MinValue, y0 = double.MaxValue, y1 = double.MinValue, z0 = double.MaxValue, z1 = double.MinValue;
        for (int i = 0; i < g; i++)
        {
            int k = i * LayoutFog.GameStride;
            x0 = Math.Min(x0, LayoutFog.Game[k]);
            x1 = Math.Max(x1, LayoutFog.Game[k]);
            y0 = Math.Min(y0, LayoutFog.Game[k + 1]);
            y1 = Math.Max(y1, LayoutFog.Game[k + 1]);
            z0 = Math.Min(z0, LayoutFog.Game[k + 2]);
            z1 = Math.Max(z1, LayoutFog.Game[k + 2]);
        }
        Check(Math.Abs((x0 + x1) / 2 - LayoutFog.GameCentreX) < 0.001 && Math.Abs((y0 + y1) / 2 - LayoutFog.GameCentreY) < 0.001 && Math.Abs((z0 + z1) / 2 - LayoutFog.GameCentreZ) < 0.001
              && Math.Abs((x1 - x0) / 2 - LayoutFog.GameExtentX) < 0.001 && Math.Abs((y1 - y0) / 2 - LayoutFog.GameExtentY) < 0.001 && Math.Abs((z1 - z0) / 2 - LayoutFog.GameExtentZ) < 0.001,
            "uptown fog: the game's mesh's bounds the mod checks are those of the vertices it holds");
        FogMesh m = FogMade();
        double least = double.MaxValue;
        bool turn = true;
        for (int t = 0; t < m.Triangles; t++)
        {
            int a = m.T[t * 3], b = m.T[t * 3 + 1], c = m.T[t * 3 + 2];
            double cross = (m.X[b] - m.X[a]) * (m.Z[c] - m.Z[a]) - (m.X[c] - m.X[a]) * (m.Z[b] - m.Z[a]);
            turn &= cross < 0.0;
            least = Math.Min(least, Math.Abs(cross) / 2.0);
        }
        Check(turn && least > 0.001, "uptown fog: every triangle clockwise seen from above, as the game's (its normals up, its pass culling back faces), none without area (the least "
                                     + least.ToString("0.000") + " m2)");
        bool uv = true;
        for (int i = 0; i < g; i++)
        {
            int k = i * LayoutFog.GameStride;
            float u = LayoutFog.Game[k + 3], v = LayoutFog.Game[k + 4];
            uv &= (u == 0f && v == LayoutFog.EdgeV) || (u == LayoutFog.RingU && v == LayoutFog.RingV) || (u == LayoutFog.FarU && v == LayoutFog.FarV);
            uv &= Math.Abs(LayoutFog.PlanarU(LayoutFog.Game[k]) - LayoutFog.Game[k + 5]) < 3e-5 && Math.Abs(LayoutFog.PlanarV(LayoutFog.Game[k + 2]) - LayoutFog.Game[k + 6]) < 3e-5;
            uv &= Math.Abs(LayoutFog.Game[k + 1] - LayoutFog.Height) < 0.01f && Math.Abs(LayoutFog.Game[k + 10]) == 1f;
        }
        Check(uv && LayoutFog.UvY(0f) == LayoutFog.EdgeV && Math.Abs(LayoutFog.UvY(LayoutFog.RingU) - LayoutFog.RingV) < 1e-6 && Math.Abs(LayoutFog.UvY(LayoutFog.FarU) - LayoutFog.FarV) < 1e-6,
            "uptown fog: the game's vertices' uv0 one of its three (edge, ring, far), uv1 its planar projection, 92 m up in the mesh; a made vertex's take the same path");
        Check(m.U.All(u => u >= 0.0 && u <= 1.0), "uptown fog: uv0.x within 0 and 1 at every vertex");
    }

    // ---------------------------------------------------------------- the cut over the band

    private static void UptownFogCutChecks()
    {
        List<GroundRect> band = LayoutRules.BandGround;
        LayoutPlans.MapBounds(out float west, out float east, out float north);
        double south = band.Min(r => (double)r.Z1);
        Check(band.Count == 23 && band.All(r => r.X1 >= LayoutFog.CutWest && r.X2 <= LayoutFog.CutEast && r.Z1 >= LayoutFog.CutSouth && r.Z2 <= LayoutFog.CutNorth)
              && LayoutFog.CutWest <= west && LayoutFog.CutEast >= east && LayoutFog.CutNorth >= north && LayoutFog.CutSouth <= south,
            "uptown fog: the cut holds the band's 23 pieces of ground and the map's widened bounds over them (x " + west + " to " + east + ", to z " + north + ")");
        Check(Math.Abs(LayoutFog.CutSouth - (south - LayoutFog.Clearance)) < 0.001 && Math.Abs(LayoutFog.CutWest - west) < 0.001 && Math.Abs(LayoutFog.CutEast - east) < 0.001
              && Math.Abs(LayoutFog.CutNorth - north) < 0.001,
            "uptown fog: the cut is the map's bounds over the band, and south of 1st Avenue's continuations the " + LayoutFog.Clearance.ToString("0.0") + " m the game keeps past 1st Avenue's north pavement");
        FogMesh m = FogMade();
        int over = 0;
        for (int t = 0; t < m.Triangles; t++)
        {
            over += FogOverlapsCut(m, t) ? 1 : 0;
        }
        Check(over == 0, "uptown fog: no triangle shares any area with the cut (" + over + " do)");
        int points = 0, under = 0;
        foreach (GroundRect r in band)
        {
            for (double x = r.X1; x <= r.X2 + 1e-9; x += 2.0)
            {
                for (double z = r.Z1; z <= r.Z2 + 1e-9; z += 2.0)
                {
                    points++;
                    under += m.At(x, z) != null ? 1 : 0;
                }
            }
        }
        Check(points > 150000 && under == 0, "uptown fog: no triangle over the band's ground at " + points + " points 2 m apart, its edges included (" + under + " under one)");
        points = under = 0;
        for (double x = west; x <= east + 1e-9; x += 5.0)
        {
            for (double z = south; z <= north + 1e-9; z += 5.0)
            {
                points++;
                double? u = m.At(x, z);
                under += u != null && u.Value > 1e-9 ? 1 : 0;   // the bounds' edge is the hole's, where uv0.x is 0
            }
        }
        Check(under == 0, "uptown fog: no fog of any strength over the map's widened bounds over the band at " + points + " points 5 m apart, their edge included");
    }

    // ---------------------------------------------------------------- the shape: the game's outline and hole as they were but where the cut meets them

    private static void UptownFogShapeChecks()
    {
        FogMesh m = FogMade(), game = FogOfTheGame();
        var directed = new HashSet<((double, double), (double, double))>();
        bool once = true;
        for (int t = 0; t < m.Triangles; t++)
        {
            for (int e = 0; e < 3; e++)
            {
                int a = m.T[t * 3 + e], b = m.T[t * 3 + (e + 1) % 3];
                once &= directed.Add(((m.X[a], m.Z[a]), (m.X[b], m.Z[b])));
            }
        }
        Check(once, "uptown fog: no edge twice the same way round, so no triangle lies over another");
        List<List<(double, double)>> loops = FogLoops(m), gameLoops = FogLoops(game);
        Check(gameLoops.Count == 2, "uptown fog: the game's fog is its outline and one hole (" + gameLoops.Count + " loops), so the cut joins the only hole there is and no other is touched");
        Check(loops.Count == 2, "uptown fog: the made fog is an outline and one hole (" + loops.Count + " loops)");
        if (loops.Count != 2 || gameLoops.Count != 2)
        {
            return;
        }
        List<(double, double)> outline = loops.OrderByDescending(l => Math.Abs(FogArea(l))).First(), hole = loops.OrderBy(l => Math.Abs(FogArea(l))).First();
        List<(double, double)> gameOutline = gameLoops.OrderByDescending(l => Math.Abs(FogArea(l))).First(), gameHole = gameLoops.OrderBy(l => Math.Abs(FogArea(l))).First();
        double gameNorth = gameOutline.Max(p => p.Item2), gameEast = gameOutline.Max(p => p.Item1);
        // The game's outline is kept on its west and south sides, point for point and in its order; its north-east corner
        // goes, as its north and east edges move out with the map's bounds.
        List<(double, double)> westSouth = gameOutline.Where(p => !(p.Item1 == gameEast && p.Item2 == gameNorth)).ToList();
        var outlineSet = new HashSet<(double, double)>(outline);
        Check(westSouth.All(outlineSet.Contains) && outline.Count == westSouth.Count + 3 && !outlineSet.Contains((gameEast, gameNorth)),
            "uptown fog: the game's outline kept on the west and south, its " + westSouth.Count + " points as they were, three corners more for the north and east moved out");
        Check(Math.Abs(outline.Max(p => p.Item2) - LayoutFog.SheetNorth) < 0.01 && Math.Abs(outline.Max(p => p.Item1) - LayoutFog.SheetEast) < 0.01
              && outline.Min(p => p.Item1) == gameOutline.Min(p => p.Item1) && outline.Min(p => p.Item2) == gameOutline.Min(p => p.Item2),
            "uptown fog: the sheet reaches north to " + LayoutFog.SheetNorth.ToString("0") + " and east to " + LayoutFog.SheetEast.ToString("0") + ", west and south where the game's does");
        LayoutPlans.MapBounds(out float west, out float east, out float north);
        Check(LayoutFog.SheetNorth - north >= gameNorth - FogGameBoundsNorth - 0.01 && LayoutFog.SheetEast - east >= gameEast - FogGameBoundsEast - 0.01,
            "uptown fog: past the map's widened bounds the sheet keeps the margin the game's keeps past its own (north " + (gameNorth - FogGameBoundsNorth).ToString("0") + " m, east "
            + (gameEast - FogGameBoundsEast).ToString("0") + " m), more than the map's widest view reaches past its pivot");
        var holeSet = new HashSet<(double, double)>(hole);
        List<(double, double)> outside = gameHole.Where(p => !FogInCut(p.Item1, p.Item2)).ToList();
        Check(outside.All(holeSet.Contains) && hole.All(p => outside.Contains(p) || FogCutDistance(p.Item1, p.Item2) < 0.001),
            "uptown fog: the hole is the game's, its " + outside.Count + " points outside the cut as they were, with the cut's edge and nothing else");
        double tris = 0.0;
        for (int t = 0; t < m.Triangles; t++)
        {
            int a = m.T[t * 3], b = m.T[t * 3 + 1], c = m.T[t * 3 + 2];
            tris += Math.Abs((m.X[b] - m.X[a]) * (m.Z[c] - m.Z[a]) - (m.X[c] - m.X[a]) * (m.Z[b] - m.Z[a])) / 2.0;
        }
        Check(Math.Abs(tris - (Math.Abs(FogArea(outline)) - Math.Abs(FogArea(hole)))) < 1.0, "uptown fog: the triangles cover the sheet less its hole, once over (" + tris.ToString("0") + " m2)");
    }

    // ---------------------------------------------------------------- the field: the game's fog kept, the cut's edge the game's

    private static void UptownFogFieldChecks()
    {
        FogMesh m = FogMade(), game = FogOfTheGame();
        int samples = 0, bare = 0, differ = 0;
        double worstAlpha = 0.0;
        for (int t = 0; t < game.Triangles; t++)
        {
            int a = game.T[t * 3], b = game.T[t * 3 + 1], c = game.T[t * 3 + 2];
            const int S = 30;
            for (int i = 0; i <= S; i++)
            {
                for (int j = 0; j <= S - i; j++)
                {
                    double l1 = i / (double)S, l2 = j / (double)S, l3 = 1.0 - l1 - l2;
                    double x = l1 * game.X[a] + l2 * game.X[b] + l3 * game.X[c], z = l1 * game.Z[a] + l2 * game.Z[b] + l3 * game.Z[c];
                    if (FogInCut(x, z))
                    {
                        continue;
                    }
                    samples++;
                    double? u = m.At(x, z, 1e-7);
                    if (u == null)
                    {
                        bare++;
                        continue;
                    }
                    double was = l1 * game.U[a] + l2 * game.U[b] + l3 * game.U[c];
                    double ramp = LayoutFog.Rise * FogCutDistance(x, z);
                    double want = ramp < LayoutFog.RingU ? Math.Min(was, ramp) : was;
                    differ += Math.Abs(Math.Min(u.Value, FogWhole) - Math.Min(want, FogWhole)) > 0.0011 ? 1 : 0;
                    foreach (double noise in FogNoise)
                    {
                        worstAlpha = Math.Max(worstAlpha, Math.Abs(FogAlpha(u.Value, noise) - FogAlpha(want, noise)));
                    }
                }
            }
        }
        Check(bare == 0, "uptown fog: every point of the game's fog outside the cut is still fog (" + bare + " of " + samples + " samples, 31 a side over each of its triangles, bare)");
        Check(differ == 0 && worstAlpha <= 0.02, "uptown fog: the field there is the game's, or the cut's ramp where that is lower, within 0.0011 of uv0.x below whole fog ("
                                                 + differ + " differ), the fog's alpha within " + worstAlpha.ToString("0.000") + " of it at the noise's least, mean and most");
        // Off each of the cut's edges, away from the game's hole, uv0.x rises at the game's rate: the game's own fade.
        bool ramps = true;
        (double x, double z, double dx, double dz)[] edges =
        {
            (LayoutFog.CutWest, 700.0, -1.0, 0.0), (0.0, LayoutFog.CutNorth, 0.0, 1.0), (LayoutFog.CutEast, 700.0, 1.0, 0.0), (-650.0, LayoutFog.CutSouth, 0.0, -1.0),
            (700.0, LayoutFog.CutSouth, 0.0, -1.0)
        };
        foreach (var e in edges)
        {
            foreach (double d in new[] { 5.0, 25.0, 50.0, 100.0, 130.0 })
            {
                double? u = m.At(e.x + e.dx * d, e.z + e.dz * d);
                ramps &= u != null && Math.Abs(u.Value - LayoutFog.Rise * d) < 0.0011;
            }
        }
        Check(ramps, "uptown fog: off the cut's west, north, east and two south edges uv0.x rises " + LayoutFog.Rise.ToString("0.000000") + " a metre, the game's own fade, 5 to 130 m out");
        // Where the sheet grew, it is fog, whole past the fade.
        int grown = 0, thin = 0;
        double gameNorth = game.Z.Max(), gameEast = game.X.Max();
        for (double x = game.X.Min(); x <= LayoutFog.SheetEast; x += 50.0)
        {
            for (double z = game.Z.Min(); z <= LayoutFog.SheetNorth; z += 50.0)
            {
                if ((z <= gameNorth && x <= gameEast) || FogInCut(x, z))
                {
                    continue;
                }
                double? u = m.At(x, z);
                grown += u == null ? 1 : 0;
                thin += u != null && FogCutDistance(x, z) > FogWhole / LayoutFog.Rise + 1.0 && u.Value < FogWhole ? 1 : 0;
            }
        }
        Check(grown == 0 && thin == 0, "uptown fog: the sheet's new north and east parts are fog everywhere, whole past the cut's fade");
    }

    // ---------------------------------------------------------------- the sources

    private static void UptownFogSourceChecks(string scripts)
    {
        string folder = Path.Combine(scripts, "Uptown");
        string fog = Code(File.ReadAllText(Path.Combine(folder, "GroundGame.Fog.cs")));
        string dirFog = Code(File.ReadAllText(Path.Combine(folder, "GroundDirector.Fog.cs")));
        string game = Code(File.ReadAllText(Path.Combine(folder, "GroundGame.cs")));
        string director = Code(File.ReadAllText(Path.Combine(folder, "GroundDirector.cs")));
        string layout = Code(File.ReadAllText(Path.Combine(folder, "LayoutFog.cs")));
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
        string cut = Body(fog, "public static string CutFog()"), differs = Body(fog, "private static string FogDiffers("), uncut = Body(fog, "public static void UncutFog()");
        Check(Order(cut, "FogDiffers(filter)") >= 0 && Order(cut, "if (differs != null)") > Order(cut, "FogDiffers(filter)")
              && Order(cut, "filter.sharedMesh = _fogMade;") > Order(cut, "if (differs != null)") && Regex.Matches(fog, @"\.sharedMesh = ").Count == 2,
            "uptown fog sources: the cut goes on the fog only after the fog is matched, and a fog that differs is left as it was");
        Check(new[] { "mesh.name == LayoutFog.GameName", "mesh.vertexCount == LayoutFog.GameVertexCount", "mesh.subMeshCount == 1", "mesh.GetTopology(0) == MeshTopology.Triangles",
                  "indices == LayoutFog.GameIndexCount", "Near(bounds.center, LayoutFog.GameCentreX", "Near(bounds.extents, LayoutFog.GameExtentX", "Near(place, LayoutFog.PlaceX", "turn <= LayoutFog.Near",
                  "Mathf.Abs(scale.x - 1f)" }.All(differs.Contains)
              && !Regex.IsMatch(differs, @"\.(?:vertices|triangles|uv|normals|tangents)\b|GetVertices|GetIndices|GetTriangles"),
            "uptown fog sources: the game's fog is matched on what its mesh tells without read access (its name, vertices, indices, parts, bounds) and where it stands, never on what it cannot be read for");
        Check(Order(uncut, "_fog.sharedMesh = _fogGame;") >= 0 && Order(uncut, "_fog.sharedMesh == _fogMade") >= 0 && Order(uncut, "Object.Destroy(_fogMade);") >= 0
              && Order(uncut, "_fogMade = null;") > Order(uncut, "Object.Destroy(_fogMade);"),
            "uptown fog sources: giving back puts the game's mesh back where the cut still is and destroys the cut");
        Check(Regex.IsMatch(game, @"public static void Forget\(\)\s*\{\s*DropArt\(\);\s*UncutFog\(\);") && Regex.IsMatch(game, @"public static void GiveBack\(\)\s*\{[^}]*Forget\(\);"),
            "uptown fog sources: the fog given back when the district is given back the same load and when it is forgotten (the city going, a new load)");
        Check(Order(Body(director, "public void Close(string why)"), "GroundGame.UncutFog();") >= 0, "uptown fog sources: the fog given back at the feature's clean-up (the options page, faults, the save tools, another city)");
        string destroy = Body(director, "private void OnDestroy()");
        Check(Order(destroy, "GroundGame.UncutFog();") >= 0 && Order(destroy, "GroundGame.UncutFog();") < Order(destroy, "if (!stays)"),
            "uptown fog sources: the fog given back when the director goes, the save tools' dormant way out among them");
        string lay = Body(director, "private void Lay()");
        Check(Order(lay, "LayFog();") > Order(lay, "GroundGame.WidenMap(") && Order(lay, "LayFog();") < Order(lay, "TransitMap.AddTerminus(") && Order(dirFog, "GroundGame.CutFog()") >= 0
              && Regex.Matches(director + dirFog, @"\bLayFog\(\);").Count == 1,
            "uptown fog sources: the cut laid once a load, behind the loading screen, once the map's bounds reach over the band");
        Check(!Regex.IsMatch(fog + dirFog + layout, @"\bvoid\s+(?:Update|LateUpdate|FixedUpdate|OnGUI)\s*\(|HarmonyLib|\[Harmony|\bGetField\(|\bGetProperty\(|\bGetMethod\(|BindingFlags|FindObjects|GameObject\.Find\(")
              && !Regex.IsMatch(layout, @"\busing UnityEngine|\bVector3\b|\bMathf\b"),
            "uptown fog sources: nothing per frame, no patch of the game's, nothing read by name, no search of the scene; the cut's data game-free");
        Check(guide.Contains(".Check(\"reallife_uptown_ground_test_fog\", GroundDirector.TestFog, \"Uptown check (map fog)\")") && dirFog.Contains("\"Uptown check (map fog): \"")
              && Regex.IsMatch(dirFog, @"if \(Meta\.Dev\.Verbose\)\s*\{[^}]*""Uptown check \(map fog, the load\): ")
              && Regex.IsMatch(fog, @"MadeFogName = ""RealLifeOverhaul "),
            "uptown fog sources: its test in the feature's list and the city expansion's run, its line written by its test, the load's line with Developer diagnostics on, the made mesh named for the mod");
    }
}
