using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using RealLifeOverhaul;
using RealLifeOverhaul.Uptown;

// Uptown's streets (Uptown/StreetsRules.cs, Transport/TransitPlan.Routes.cs and the files that lay them): the district's two
// bus routes there and back from beside the two Manhattan subway entrances nearest it, every point of their ways on the
// band's streets and avenues, their stops on the way in order and each avenue stop between its crossings' plain lanes;
// every block's far corners within 250 m of a stop, a stop missed by its window's width either way included; the bus
// network's own routes as the mod lays them (after the pairing, local lines, a pole judged where it stands); the street
// furniture of every pavement at Manhattan's own spacing per 100 m (measured from Build 3682's scene), on the kerb's strip,
// clear of the crossings, the stops, the copied modules' lamps and one another, the benches' backs to the road; the same
// twice; and over the sources the scene touched in StreetsGame alone, laid with the art before the ground grows, and the
// texts in three languages with What's new in all nine.
internal static partial class Program
{
    private static void UptownStreets(bool print)
    {
        if (print)
        {
            PrintUptownStreets();
        }
        string scripts = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Scripts"));
        UptownRouteChecks();
        UptownReachChecks();
        UptownWantedChecks();
        UptownFurnitureChecks();
        UptownStreetsTimingChecks();
        UptownStreetsSourceChecks(scripts);
        UptownStreetsTextChecks(scripts);
    }

    // With --uptown: the planned reach and the furniture, in figures.
    private static void PrintUptownStreets()
    {
        List<TransitPlan.Route> routes = UptownRoutes();
        var px = new List<float>();
        var pz = new List<float>();
        StreetsRules.WantedPlaces(routes, px, pz);
        var poles = new List<float>();
        var polesZ = new List<float>();
        StreetsRules.Poles(px, pz, poles, polesZ);
        var blocks = new List<StreetsRules.BlockReach>();
        float worst = StreetsRules.Reach(poles, polesZ, blocks);
        Console.WriteLine("uptown streets: " + px.Count + " places wanted, " + routes.Sum(r => r.Stops) + " stops of " + routes.Count + " directions; the farthest corner "
                          + worst.ToString("F0") + " m; fewest stops within reach of a block's middle " + blocks.Min(b => b.Stops));
        for (int row = LayoutRules.Rows; row >= 1; row--)
        {
            Console.WriteLine("  row " + row + ": " + string.Join(" ", blocks.Where(b => b.Row == row).Select(b => b.Farthest.ToString("F0").PadLeft(3))));
        }
        var all = new List<StreetPlace>();
        int kept = StreetsRules.Furniture(routes, all);
        Console.WriteLine("uptown streets: furniture " + all.Count + " pieces (" + kept + " kept off the stops): " + string.Join(", ", Enum.GetValues(typeof(StreetPiece)).Cast<StreetPiece>()
                          .Select(k => k + " " + all.Count(p => p.Kind == k))));
    }

    // ---------------------------------------------------------------- the routes

    private static List<TransitPlan.Route> UptownRoutes()
    {
        var routes = new List<TransitPlan.Route>(4);
        StreetsRules.Routes(routes);
        return routes;
    }

    private static bool OnBandStreet(float x, float z, out bool avenue)
    {
        avenue = false;
        for (int s = LayoutStreets.WestMost; s <= LayoutStreets.EastMost; s++)
        {
            if (Math.Abs(x - LayoutRules.StreetX(s)) < 0.01f)
            {
                return z >= StreetsRules.RouteFromZ - 0.01f && z <= LayoutRules.AvenueZ(LayoutRules.Rows) + 0.01f;
            }
        }
        for (int row = 1; row <= LayoutRules.Rows; row++)
        {
            if (Math.Abs(z - LayoutRules.AvenueZ(row)) < 0.01f)
            {
                avenue = true;
                return x >= LayoutRules.StreetX(LayoutStreets.WestMost) && x <= LayoutRules.StreetX(LayoutStreets.EastMost);
            }
        }
        return false;
    }

    // A route's way on the grid: its points with each turn's junction between a street's point and an avenue's.
    private static void GridWay(TransitPlan.Route route, List<float> x, List<float> z, List<float> metres)
    {
        x.Clear();
        z.Clear();
        metres.Clear();
        for (int i = 0; i < route.Points; i++)
        {
            if (i > 0)
            {
                OnBandStreet(route.ViaX[i - 1], route.ViaZ[i - 1], out bool wasAvenue);
                OnBandStreet(route.ViaX[i], route.ViaZ[i], out bool isAvenue);
                if (wasAvenue != isAvenue)
                {
                    // the junction: the street's x and the avenue's z
                    x.Add(wasAvenue ? route.ViaX[i] : route.ViaX[i - 1]);
                    z.Add(wasAvenue ? route.ViaZ[i - 1] : route.ViaZ[i]);
                }
            }
            x.Add(route.ViaX[i]);
            z.Add(route.ViaZ[i]);
        }
        // in steps of a metre, as a lane's points are laid along it
        var fx = new List<float>();
        var fz = new List<float>();
        float along = 0f;
        for (int i = 0; i + 1 < x.Count; i++)
        {
            float dx = x[i + 1] - x[i], dz = z[i + 1] - z[i], length = (float)Math.Sqrt(dx * dx + dz * dz);
            int steps = Math.Max(1, (int)Math.Ceiling(length));
            for (int k = 0; k < steps; k++)
            {
                fx.Add(x[i] + dx * k / steps);
                fz.Add(z[i] + dz * k / steps);
                metres.Add(along + length * k / steps);
            }
            along += length;
        }
        fx.Add(x[x.Count - 1]);
        fz.Add(z[z.Count - 1]);
        metres.Add(along);
        x.Clear();
        x.AddRange(fx);
        z.Clear();
        z.AddRange(fz);
    }

    private static void UptownRouteChecks()
    {
        List<TransitPlan.Route> routes = UptownRoutes();
        Check(routes.Count == 4, "uptown buses: two routes, each there and back (" + routes.Count + ")");
        Check(routes.Count <= TransitPlanOwnMost * 2, "uptown buses: within the bus network's allowance of a district's own routes");
        Check(routes.Select(r => r.Name).Distinct().Count() == routes.Count, "uptown buses: every direction named apart, as the network keeps one a name");
        for (int r = 0; r + 1 < routes.Count; r += 2)
        {
            TransitPlan.Route there = routes[r], back = routes[r + 1];
            Check(there.Fault() == null && back.Fault() == null, "uptown buses: " + there.Name + " can be laid both ways (" + (there.Fault() ?? back.Fault()) + ")");
            Check(there.ViaX.Reverse().SequenceEqual(back.ViaX) && there.ViaZ.Reverse().SequenceEqual(back.ViaZ) && there.StopX.Reverse().SequenceEqual(back.StopX)
                  && there.StopZ.Reverse().SequenceEqual(back.StopZ), "uptown buses: " + there.Name + " back is its way and its stops the other way round");
            bool onGrid = true, joined = true;
            for (int i = 0; i < there.Points; i++)
            {
                onGrid &= OnBandStreet(there.ViaX[i], there.ViaZ[i], out _);
                if (i > 0)
                {
                    bool sameStreet = Math.Abs(there.ViaX[i] - there.ViaX[i - 1]) < 0.01f, sameAvenue = Math.Abs(there.ViaZ[i] - there.ViaZ[i - 1]) < 0.01f;
                    OnBandStreet(there.ViaX[i - 1], there.ViaZ[i - 1], out bool a0);
                    OnBandStreet(there.ViaX[i], there.ViaZ[i], out bool a1);
                    bool turn = a0 != a1 && Math.Abs(there.ViaX[i] - there.ViaX[i - 1]) <= LayoutRules.StreetPitch && Math.Abs(there.ViaZ[i] - there.ViaZ[i - 1]) <= LayoutRules.AvenuePitch;
                    joined &= (sameStreet && !a0 && !a1) || (sameAvenue && a0 && a1) || turn;
                }
            }
            Check(onGrid, "uptown buses: every point of " + there.Name + "'s way on a street or an avenue of the band, or 9th and 2nd Street's stretch in Manhattan");
            Check(joined, "uptown buses: " + there.Name + "'s way goes straight along a street or an avenue, or turns at the junction next to it, from point to point");
            Check(Math.Abs(there.ViaX[0] - LayoutRules.StreetX(0)) < 0.01f && Math.Abs(there.ViaX[there.Points - 1] - LayoutRules.StreetX(7)) < 0.01f
                  && there.ViaZ[0] < LayoutRules.FirstAvenueZ - 100f && there.ViaZ[there.Points - 1] < LayoutRules.FirstAvenueZ - 100f,
                "uptown buses: " + there.Name + " from 9th Street to 2nd Street in Manhattan, south of 1st Avenue, joined to the city's network");
            // Beside the two entrances: the first stop's and the last stop's poles near them.
            float first = Dist(there.StopX[0], there.StopZ[0], StreetsRules.NorthWestStationX, StreetsRules.NorthWestStationZ);
            float last = Dist(there.StopX[there.Stops - 1], there.StopZ[there.Stops - 1], StreetsRules.EastStationX, StreetsRules.EastStationZ);
            Check(first <= 45f && last <= 45f, "uptown buses: " + there.Name + " stops beside Midtown North West Station (" + first.ToString("F0") + " m) and the station by 2nd Street ("
                                               + last.ToString("F0") + " m), its first and last stops");
            // The stops on the way, in order, and each avenue stop between its crossings' plain lanes.
            var x = new List<float>();
            var z = new List<float>();
            var m = new List<float>();
            GridWay(there, x, z, m);
            var along = new float[there.Stops];
            int found = TransitPlan.WantedAlong(x, z, m, x.Count, there.StopX, there.StopZ, along);
            bool rising = true;
            for (int s = 1; s < along.Length; s++)
            {
                rising &= along[s] > along[s - 1] + 100f && along[s] - along[s - 1] < 520f;
            }
            Check(found == there.Stops && rising, "uptown buses: every stop of " + there.Name + " on its way, in order, 100 to 520 m apart (" + found + " of " + there.Stops + ")");
            bool between = true;
            for (int s = 0; s < there.Stops; s++)
            {
                OnBandStreet(there.StopX[s], there.StopZ[s], out bool onAvenue);
                if (onAvenue)
                {
                    float toStreet = float.MaxValue;
                    for (int k = LayoutStreets.WestMost; k <= LayoutStreets.EastMost; k++)
                    {
                        toStreet = Math.Min(toStreet, Math.Abs(there.StopX[s] - LayoutRules.StreetX(k)));
                    }
                    between &= toStreet - LayoutPlans.AvenueStop >= TransitMapJunctionClear;   // the avenue's lanes stop 14.85 m short of a crossing
                }
                else if (there.StopZ[s] > LayoutRules.FirstAvenueZ)
                {
                    float toAvenue = float.MaxValue;
                    for (int row = 0; row <= LayoutRules.Rows; row++)
                    {
                        toAvenue = Math.Min(toAvenue, Math.Abs(there.StopZ[s] - LayoutRules.AvenueZ(row)));
                    }
                    between &= toAvenue - LayoutPlans.StreetStop >= TransitMapJunctionClear;
                }
            }
            Check(between, "uptown buses: every stop of " + there.Name + " a full clear stretch of plain lane from the crossings either side");
        }
        List<TransitPlan.Route> again = UptownRoutes();
        Check(routes.Count == again.Count && routes.Zip(again, (a, b) => a.ViaX.SequenceEqual(b.ViaX) && a.StopZ.SequenceEqual(b.StopZ) && a.Name == b.Name).All(t => t),
            "uptown buses: the same routes every time, on every machine");
    }

    private const int TransitPlanOwnMost = 4;          // TransitMap.MostOwnRoutes (a Game file, not linked here)
    private const float TransitMapJunctionClear = 26f;  // TransitMap.JunctionClear: the paired lines' clear stretch, kept by the district's stops too

    private static float Dist(float x0, float z0, float x1, float z1)
    {
        return (float)Math.Sqrt((x0 - x1) * (x0 - x1) + (z0 - z1) * (z0 - z1));
    }

    // ---------------------------------------------------------------- the reach

    private static void UptownReachChecks()
    {
        List<TransitPlan.Route> routes = UptownRoutes();
        var px = new List<float>();
        var pz = new List<float>();
        StreetsRules.WantedPlaces(routes, px, pz);
        var poles = new List<float>();
        var polesZ = new List<float>();
        StreetsRules.Poles(px, pz, poles, polesZ);
        Check(poles.Count == px.Count * 2, "uptown reach: a pole each side of every place wanted (" + poles.Count + ")");
        var blocks = new List<StreetsRules.BlockReach>();
        float worst = StreetsRules.Reach(poles, polesZ, blocks);
        Check(blocks.Count == LayoutRules.Rows * LayoutRules.Columns, "uptown reach: every block of the band measured, its squares among them (" + blocks.Count + ")");
        Check(worst <= StreetsRules.ReachWanted - 50f, "uptown reach: every block's every corner within " + (StreetsRules.ReachWanted - 50f) + " m of a stop as planned (" + worst.ToString("F0") + " m)");
        Check(blocks.All(b => b.Stops >= 1), "uptown reach: a stop within reach of every block's middle (fewest " + blocks.Min(b => b.Stops) + ")");
        // A stop laid a window's width along its road either way, every one at once, still reaches every corner.
        foreach (float shift in new[] { -24f, 24f })
        {
            var sx = new List<float>();
            var sz = new List<float>();
            for (int i = 0; i < poles.Count; i++)
            {
                OnBandStreet(px[i / 2], pz[i / 2], out bool avenue);
                sx.Add(poles[i] + (avenue ? shift : 0f));
                sz.Add(polesZ[i] + (avenue ? 0f : shift));
            }
            float shifted = StreetsRules.Reach(sx, sz, null);
            Check(shifted <= StreetsRules.ReachWanted, "uptown reach: every corner within " + StreetsRules.ReachWanted + " m with every stop " + shift + " m along its road (" + shifted.ToString("F0") + " m)");
        }
        // The far corners: the band's four corner blocks.
        foreach (var corner in new[] { (1, 1), (1, LayoutRules.Columns), (LayoutRules.Rows, 1), (LayoutRules.Rows, LayoutRules.Columns) })
        {
            StreetsRules.BlockReach b = blocks.First(k => k.Row == corner.Item1 && k.Column == corner.Item2);
            Check(b.Farthest <= 180f, "uptown reach: the band's corner block row " + corner.Item1 + " column " + corner.Item2 + " within 180 m of a stop (" + b.Farthest.ToString("F0") + " m)");
        }
        // The rule itself on made-up stops: none, one at a corner, the order of the blocks.
        Check(StreetsRules.Reach(new List<float>(), new List<float>(), null) == float.MaxValue, "uptown reach: no stops, nothing within reach");
        var one = new List<StreetsRules.BlockReach>();
        StreetsRules.Reach(new List<float> { LayoutRules.StreetX(LayoutStreets.WestMost) + LayoutRules.StreetHalf }, new List<float> { LayoutRules.AvenueZ(0) + LayoutRules.AvenueHalf }, one);
        float wide = LayoutRules.StreetPitch - 2f * LayoutRules.StreetHalf, deep = LayoutRules.AvenuePitch - 2f * LayoutRules.AvenueHalf;
        Check(Near(one[0].Farthest, (float)Math.Sqrt(wide * wide + deep * deep), 0.05f) && one[0].Row == 1 && one[0].Column == 1 && one.Last().Row == LayoutRules.Rows
              && one.Last().Column == LayoutRules.Columns, "uptown reach: a stop at a block's corner reaches its far corner at the block's diagonal; blocks west to east, row by row");
        // Without the round route the band's edges are out of reach: the routes are what reach them.
        var middle = new List<TransitPlan.Route> { routes[2], routes[3] };
        StreetsRules.WantedPlaces(middle, px, pz);
        StreetsRules.Poles(px, pz, poles, polesZ);
        Check(StreetsRules.Reach(poles, polesZ, null) > StreetsRules.ReachWanted, "uptown reach: Uptown Avenue's route alone leaves the band's corners out of reach (the round route is needed)");
    }

    // ---------------------------------------------------------------- the stops along a way

    private static void UptownWantedChecks()
    {
        // A straight way of a metre a point, 0 to 500 m along x.
        var x = Enumerable.Range(0, 501).Select(i => (float)i).ToList();
        var z = Enumerable.Repeat(0f, 501).ToList();
        var m = Enumerable.Range(0, 501).Select(i => (float)i).ToList();
        var along = new float[3];
        int found = TransitPlan.WantedAlong(x, z, m, x.Count, new[] { 100f, 260f, 900f }, new[] { 5f, -5f, 0f }, along);
        Check(found == 2 && Near(along[0], 100f) && Near(along[1], 260f) && along[2] < 0f, "uptown stops: a stop beside the way found at its nearest point; one off it is -1");
        found = TransitPlan.WantedAlong(x, z, m, x.Count, new[] { 100f, 100f }, new[] { 0f, TransitPlan.WantedReach + 1f }, new float[2]);
        Check(found == 1, "uptown stops: a stop further than the reach from the way is not on it");
        // A way that passes a place twice (out and back on itself): the second stop at the second pass.
        var lx = new List<float>();
        var lz = new List<float>();
        var lm = new List<float>();
        for (int i = 0; i <= 300; i++)
        {
            lx.Add(i);
            lz.Add(0f);
            lm.Add(i);
        }
        for (int i = 1; i <= 300; i++)
        {
            lx.Add(300 - i);
            lz.Add(10f);
            lm.Add(300 + 10 + i);
        }
        var twice = new float[2];
        TransitPlan.WantedAlong(lx, lz, lm, lx.Count, new[] { 50f, 50f }, new[] { 4f, 6f }, twice);
        Check(twice[0] < 100f && twice[1] > 500f, "uptown stops: a way past a place twice stops there in the order wanted (" + twice[0] + ", " + twice[1] + ")");
        // The order lane points are tried round a wanted place: nearest first, alternately, never before the last stop.
        var metres = new List<float> { 0f, 5f, 10f, 15f, 20f, 25f, 30f };
        var order = new int[8];
        int n = TransitPlan.Nearest(metres, metres.Count, 16f, 6f, -1, order);
        Check(n == 3 && order[0] == 3 && order[1] == 4 && order[2] == 2, "uptown stops: tried nearest first, then the next on and back, within the reach");
        n = TransitPlan.Nearest(metres, metres.Count, 16f, 6f, 3, order);
        Check(n == 1 && order[0] == 4, "uptown stops: never at or before the last stop's point");
        n = TransitPlan.Nearest(metres, metres.Count, 100f, 6f, -1, order);
        Check(n == 0, "uptown stops: none past the way's end");
        Check(new TransitPlan.Route("x", new[] { 0f }, new[] { 0f }, new[] { 0f, 1f }, new[] { 0f, 1f }).Fault() != null
              && new TransitPlan.Route("x", new[] { 0f, 1f }, new[] { 0f, 1f }, new[] { 0f }, new[] { 0f }).Fault() != null
              && new TransitPlan.Route("x", new[] { 0f, 1f }, new[] { 0f }, new[] { 0f, 1f }, new[] { 0f, 1f }).Fault() != null,
            "uptown stops: a route of one point, of one stop or of unpaired figures is refused");
    }

    // ---------------------------------------------------------------- the furniture

    // Manhattan's own, per 100 m of pavement, measured over 72 street and 72 avenue faces of Build 3682 (scene scan of 25 Oct 2026).
    private static readonly Dictionary<StreetPiece, (float street, float avenue)> Manhattan = new Dictionary<StreetPiece, (float, float)>
    {
        { StreetPiece.Bench, (0.28f, 0.53f) },
        { StreetPiece.Bin, (1.75f, 2.75f) },
        { StreetPiece.Tree, (1.52f, 1.56f) },
        { StreetPiece.BikeRack, (1.17f, 0.81f) }
    };

    // Metres in from the kerb Manhattan's own pieces of each kind stand, the medians of the same faces.
    private static readonly Dictionary<StreetPiece, float> ManhattanIn = new Dictionary<StreetPiece, float>
    {
        { StreetPiece.Bench, 0.95f }, { StreetPiece.Bin, 0.8f }, { StreetPiece.Tree, 1.0f }, { StreetPiece.BikeRack, 0.4f }
    };

    // Metres along the pavement a stop needs clear either side: its pole up to 7.5 m from the bus's middle, its shelter
    // 3.2 m ahead of it, the bus's doors, and the 12 m either way the stop may be laid from where it is wanted.
    private const float StopRoom = 28f;

    private static void UptownFurnitureChecks()
    {
        List<TransitPlan.Route> routes = UptownRoutes();
        var all = new List<StreetPlace>();
        int kept = StreetsRules.Furniture(routes, all);
        var again = new List<StreetPlace>();
        StreetsRules.Furniture(routes, again);
        Check(all.Count > 600 && all.SequenceEqual(again), "uptown furniture: the same pieces in the same order every time (" + all.Count + ")");
        Check(kept > 0, "uptown furniture: pieces kept off the routes' stops (" + kept + ")");
        // The rates, from the plan with no stop kept clear, against Manhattan's.
        var plain = new List<StreetPlace>();
        StreetsRules.Furniture(new List<TransitPlan.Route>(), plain);
        int streetFaces = (LayoutStreets.EastMost - LayoutStreets.WestMost + 1) * LayoutRules.Rows * 2;
        int avenueFaces = LayoutRules.Columns * LayoutRules.Rows * 2 + (LayoutRules.Columns - LayoutRules.FirstColumns);
        float streetMetres = streetFaces * StreetsRules.StreetFaceLength, avenueMetres = avenueFaces * StreetsRules.AvenueFaceLength;
        Check(Near(StreetsRules.StreetFaceLength, 94f, 0.05f) && Near(StreetsRules.AvenueFaceLength, 44f, 0.05f), "uptown furniture: a street's face 94 m and an avenue's 44 m between their crossings' clearances");
        foreach (var kind in Manhattan)
        {
            int onStreets = plain.Count(p => p.Kind == kind.Key && OnAStreet(p)), onAvenues = plain.Count(p => p.Kind == kind.Key && !OnAStreet(p));
            float streetRate = 100f * onStreets / streetMetres, avenueRate = 100f * onAvenues / avenueMetres;
            Check(Math.Abs(streetRate - kind.Value.street) <= kind.Value.street * 0.3f && Math.Abs(avenueRate - kind.Value.avenue) <= kind.Value.avenue * 0.3f,
                "uptown furniture: " + kind.Key + " at Manhattan's spacing: streets " + streetRate.ToString("F2") + " per 100 m (Manhattan's " + kind.Value.street + "), avenues "
                + avenueRate.ToString("F2") + " (" + kind.Value.avenue + ")");
        }
        Check(all.Count(p => p.Kind == StreetPiece.Bench) >= 40, "uptown furniture: benches on the pavements (" + all.Count(p => p.Kind == StreetPiece.Bench) + ")");
        // Each on its kerb's strip, its own distance in from the kerb.
        bool strip = true, crossings = true, stops = true, yaw = true, index = true, firstAvenue = true;
        var wx = new List<float>();
        var wz = new List<float>();
        StreetsRules.WantedPlaces(routes, wx, wz);
        foreach (StreetPlace p in all)
        {
            float wanted = ManhattanIn[p.Kind];
            if (OnAStreet(p))
            {
                int s = NearestStreet(p.X);
                float mid = LayoutRules.StreetX(s), west = mid - LayoutRules.StreetKerbWest - wanted, east = mid + LayoutRules.StreetKerbEast + wanted;
                strip &= Near(p.X, west, 0.2f) || Near(p.X, east, 0.2f);
                for (int row = 0; row <= LayoutRules.Rows; row++)
                {
                    crossings &= Math.Abs(p.Z - LayoutRules.AvenueZ(row)) >= LayoutRules.CrossingHalfZ + StreetsRules.CrossingClear - 0.01f;
                }
                stops &= !wx.Where((x, k) => Math.Abs(x - mid) < 1f && Math.Abs(wz[k] - p.Z) < StopRoom).Any();
                yaw &= p.Kind != StreetPiece.Bench || p.Yaw == (p.X < mid ? 270 : 90);
                yaw &= p.Kind != StreetPiece.BikeRack || p.Yaw == 0;
                index &= p.Street == s - LayoutStreets.WestMost;
            }
            else
            {
                int row = NearestAvenue(p.Z);
                float mid = LayoutRules.AvenueZ(row);
                strip &= Near(p.Z, mid - LayoutRules.AvenueKerb - wanted, 0.2f) || Near(p.Z, mid + LayoutRules.AvenueKerb + wanted, 0.2f);
                for (int s = LayoutStreets.WestMost; s <= LayoutStreets.EastMost; s++)
                {
                    crossings &= Math.Abs(p.X - LayoutRules.StreetX(s)) >= LayoutRules.CrossingHalfX + StreetsRules.CrossingClear - 0.01f;
                }
                stops &= !wx.Where((x, k) => Math.Abs(wz[k] - mid) < 1f && Math.Abs(x - p.X) < StopRoom).Any();
                yaw &= p.Kind != StreetPiece.Bench || p.Yaw == (p.Z < mid ? 180 : 0);
                yaw &= p.Kind != StreetPiece.BikeRack || p.Yaw == 90;
                index &= row == 0 ? p.Street == StreetsRules.FirstAvenue : p.Street == LayoutStreets.EastMost - LayoutStreets.WestMost + row;
                firstAvenue &= row > 0 || (p.Z > mid && (p.X < LayoutRules.StreetX(0) || p.X > LayoutRules.StreetX(LayoutRules.FirstColumns)));
            }
        }
        Check(strip, "uptown furniture: every piece on its kerb's strip, as far in as Manhattan's of its kind stand (within 0.2 m of their median), the pavement behind it clear to the doors");
        Check(crossings, "uptown furniture: none on a crossing's module or within " + StreetsRules.CrossingClear + " m of it: the corners and crosswalks clear");
        Check(stops, "uptown furniture: none within " + StopRoom + " m of a stop the routes want, on either pavement of its road");
        Check(yaw, "uptown furniture: the benches' backs to the road and the docks along the kerb, as Manhattan's stand");
        Check(index, "uptown furniture: every piece counted with its own street, 1st Avenue's continuations apart");
        Check(firstAvenue, "uptown furniture: on 1st Avenue only its continuations' north pavements, the game's own avenue between 9th and 1st Street left as it is");
        // Clear of one another and of the copied modules' lamps.
        bool apart = true;
        var byFace = all.GroupBy(p => OnAStreet(p) ? "s" + NearestStreet(p.X) + (p.X < LayoutRules.StreetX(NearestStreet(p.X)) ? "w" : "e")
                                                   : "a" + NearestAvenue(p.Z) + (p.Z < LayoutRules.AvenueZ(NearestAvenue(p.Z)) ? "s" : "n"));
        foreach (var face in byFace)
        {
            var sorted = face.OrderBy(p => OnAStreet(p) ? p.Z : p.X).ToList();
            for (int i = 1; i < sorted.Count; i++)
            {
                float gap = OnAStreet(sorted[i]) ? sorted[i].Z - sorted[i - 1].Z : sorted[i].X - sorted[i - 1].X;
                bool docks = sorted[i].Kind == StreetPiece.BikeRack && sorted[i - 1].Kind == StreetPiece.BikeRack;
                apart &= gap >= (docks ? 1.0f : 3.5f);
            }
        }
        Check(apart, "uptown furniture: every piece 3.5 m from the next along its pavement, a row of docks 1.05 m apart");
        bool clearOfLamps = true;
        foreach (StreetPlace p in all)
        {
            if (OnAStreet(p))
            {
                int row = 1;
                while (row < LayoutRules.Rows && p.Z > LayoutRules.AvenueZ(row))
                {
                    row++;
                }
                float centre = (LayoutRules.AvenueZ(row - 1) + LayoutRules.AvenueZ(row)) * 0.5f;
                clearOfLamps &= new[] { -33.75f, -9.84f, 11.0f, 34.9f }.All(o => Math.Abs(p.Z - (centre + o)) >= 3.0f);
            }
            else
            {
                int west = (int)Math.Floor((p.X - LayoutRules.StreetFirstX) / LayoutRules.StreetPitch);
                float centre = LayoutRules.StreetX(west) + LayoutRules.StreetPitch * 0.5f;
                clearOfLamps &= new[] { -20.0f, -2.5f, 16.84f }.All(o => Math.Abs(p.X - (centre + o)) >= 2.4f);
            }
        }
        Check(clearOfLamps, "uptown furniture: clear of the copied modules' own lamps (a street's four a side every 100 m, an avenue's three every 50 m, Build 3682)");
        int[,] counts = StreetsRules.Counts(all);
        int sum = 0;
        for (int s = 0; s < counts.GetLength(0); s++)
        {
            for (int k = 0; k < counts.GetLength(1); k++)
            {
                sum += counts[s, k];
            }
        }
        Check(counts.GetLength(0) == LayoutStreets.All.Length + 1 && counts.GetLength(1) == 4 && sum == all.Count, "uptown furniture: counted by street and kind, every piece once");
        Check(all.Where(p => p.Kind == StreetPiece.Tree).Select(p => p.Variant).Distinct().Count() == StreetsRules.TreeKinds && all.Where(p => p.Kind != StreetPiece.Tree).All(p => p.Variant == 0),
            "uptown furniture: the game's three kinds of street tree among the trees");
        Check(LayoutStreets.All.Take(LayoutStreets.EastMost - LayoutStreets.WestMost + 1).Select((s, i) => i).All(i => Enumerable.Range(0, 4).Sum(k => counts[i, k]) > 0),
            "uptown furniture: something on every street's pavements");
    }

    private static bool OnAStreet(StreetPlace p)
    {
        float s = LayoutRules.StreetX(NearestStreet(p.X));
        return Math.Abs(p.X - s) < LayoutRules.StreetHalf;
    }

    private static int NearestStreet(float x)
    {
        return (int)Math.Round((x - LayoutRules.StreetFirstX) / LayoutRules.StreetPitch);
    }

    private static int NearestAvenue(float z)
    {
        return (int)Math.Round((z - LayoutRules.FirstAvenueZ) / LayoutRules.AvenuePitch);
    }

    private static void UptownStreetsTimingChecks()
    {
        List<TransitPlan.Route> routes = UptownRoutes();
        var places = new List<StreetPlace>();
        StreetsRules.Furniture(routes, places);
        var watch = Stopwatch.StartNew();
        for (int i = 0; i < 20; i++)
        {
            StreetsRules.Furniture(routes, places);
        }
        double furnish = watch.Elapsed.TotalMilliseconds / 20.0;
        var px = new List<float>();
        var pz = new List<float>();
        StreetsRules.WantedPlaces(routes, px, pz);
        watch.Restart();
        for (int i = 0; i < 20; i++)
        {
            StreetsRules.Reach(px, pz, null);
        }
        double reach = watch.Elapsed.TotalMilliseconds / 20.0;
        Check(furnish < 20.0 && reach < 5.0, "uptown streets: the furniture planned in " + furnish.ToString("F2") + " ms and the reach measured in " + reach.ToString("F2")
                                             + " ms, once a load and once a test (wanted under 20 and 5)");
    }

    // ---------------------------------------------------------------- the sources

    private static void UptownStreetsSourceChecks(string scripts)
    {
        string uptown = Path.Combine(scripts, "Uptown"), transport = Path.Combine(scripts, "Transport");
        string Read(string folder, string name) => File.Exists(Path.Combine(folder, name)) ? Code(File.ReadAllText(Path.Combine(folder, name))) : string.Empty;
        string rules = Read(uptown, "StreetsRules.cs"), game = Read(uptown, "StreetsGame.cs"), director = Read(uptown, "GroundDirector.cs"), streets = Read(uptown, "GroundDirector.Streets.cs");
        string guide = Read(uptown, "StreetsGuide.cs"), art = Read(uptown, "GroundGame.Art.cs");
        string plan = Read(transport, "TransitPlan.Routes.cs"), map = Read(transport, "TransitMap.cs"), own = Read(transport, "TransitMap.Routes.cs"), stops = Read(transport, "TransitMap.Stops.cs");
        Check(rules.Length > 0 && game.Length > 0 && streets.Length > 0 && guide.Length > 0 && plan.Length > 0 && own.Length > 0, "uptown streets sources: every file is there");
        foreach (var file in new[] { ("StreetsRules.cs", rules), ("TransitPlan.Routes.cs", plan) })
        {
            Check(!Regex.IsMatch(file.Item2, @"\busing UnityEngine|\busing Gley|\bVector3\b|\bMathf\b|\bGameObject\b|\bWaypoint\b"), "uptown streets sources: " + file.Item1 + " is game-free");
        }
        foreach (var file in new[] { ("StreetsRules.cs", rules), ("StreetsGame.cs", game), ("GroundDirector.Streets.cs", streets), ("TransitPlan.Routes.cs", plan), ("TransitMap.Routes.cs", own) })
        {
            Check(!Regex.IsMatch(file.Item2, @"\bSystem\.Linq\b|using System\.Linq"), "uptown streets sources: no LINQ (" + file.Item1 + ")");
            Check(!Regex.IsMatch(file.Item2, @"new System\.Random\(\)|\bUnityEngine\.Random\b|\bRandom\.Range\b|DateTime\.Now"), "uptown streets sources: nothing left to chance or the clock (" + file.Item1 + ")");
            Check(!Regex.IsMatch(file.Item2, @"\bmodData\b|\bSaveHook\b|\bSend\("), "uptown streets sources: nothing saved or sent (" + file.Item1 + ")");
            Check(!Regex.IsMatch(file.Item2, @"\bvoid\s+(?:Update|LateUpdate|FixedUpdate)\s*\("), "uptown streets sources: no frame method (" + file.Item1 + ")");
        }
        var scene = new Regex(@"\b(?:Object\.Instantiate|Instantiate\(|SetActive\(|AddComponent<|GetComponentsInChildren<|SceneManager\.|DestroyImmediate|Physics\.)");
        Check(scene.IsMatch(game) && !scene.IsMatch(rules) && !scene.IsMatch(streets), "uptown streets sources: the scene touched in StreetsGame alone");
        Check(!Regex.IsMatch(game, @"FindObjects|GameObject\.Find\(|SceneFinds\."), "uptown streets sources: the templates found among their scene roots' pieces, no search of the whole scene");
        Check(Regex.IsMatch(game, @"HolderName = ""RealLifeOverhaul ") && Regex.IsMatch(game, @"holder\.SetActive\(false\);[\s\S]*holder\.transform\.SetParent\(art, false\);[\s\S]*holder\.SetActive\(true\);"),
            "uptown streets sources: the furniture made asleep under the art's holder, named for the mod, and woken together");
        Check(art.Contains("public static Transform ArtRoot") && streets.Contains("StreetsGame.Furnish(GroundGame.ArtRoot)"), "uptown streets sources: the furniture goes with the art when the city goes or the district stands down");
        int copyArt = director.IndexOf("GroundGame.CopyArt(", StringComparison.Ordinal), furnish = director.IndexOf("Furnish();", StringComparison.Ordinal),
            ground = director.IndexOf("GroundWork.Ground(LayoutPlans.PlayerGround())", StringComparison.Ordinal), termini = director.IndexOf("TransitMap.AddTerminus(", StringComparison.Ordinal),
            routes = director.IndexOf("AddRoutes();", StringComparison.Ordinal);
        Check(copyArt >= 0 && furnish > copyArt && ground > furnish && routes > termini && termini >= 0,
            "uptown streets load: the furniture laid after the art and before the ground grows (walked round), the routes handed over after the termini, behind the loading screen");
        Check(game.Contains("GroundGame.InMouth(") && game.Contains("Physics.CheckBox("), "uptown streets sources: nothing laid in a big place's mouth or where something of the game's stands");
        Check(map.Contains("!RoutesLaid") && map.Contains("LayOwn();") && Regex.IsMatch(map, @"if \(IsOwn\(line\)\)\s*\{\s*continue;") && map.Contains("IsOwn(line) ? -1"),
            "uptown streets transit: a district's routes laid behind the loading screen after the pairing, as local lines, the main lines the paired routes they were");
        Check(stops.Contains("return PlaceWanted(line);") && Regex.IsMatch(stops, @"float found = Vector3\.Dot\(Flat\(stop\.Kerb - stop\.Lane\), right\);")
              && stops.Contains("BusInside.SignClearAlways(Vector3.Dot(toPole, stop.Along), Vector3.Dot(toPole, right), found, waitAlong, waitAside)"),
            "uptown streets transit: a district's stops where wanted, and every pole judged where it stands against the kerb as found, as the poles' check reads them");
        Check(own.Contains("\"M\" + (_routes + 1)") && own.Contains("Share(line, hostIndex, Hosts[hostId], WantedClear)") && own.Contains("TryRound(line, wanted, WantedNear, JunctionClear) ?? TryRound(line, wanted, WantedFar, WantedClear)"),
            "uptown streets transit: numbered on after the paired lines, a place shared where a line stops already, the paired lines' clear stretch first");
        Check(Regex.IsMatch(map, @"public static void ClearTermini\(\)\s*\{\s*Termini\.Clear\(\);\s*OwnRoutes\.Clear\(\);"), "uptown streets transit: the district's routes go with the city, as its termini do");
        Check(guide.Contains("\"reallife_uptown_streets_test_reach\"") && guide.Contains("\"reallife_uptown_streets_test_subway\"") && guide.Contains("\"reallife_uptown_streets_test_furniture\"")
              && streets.Contains("\"Uptown check (bus reach): \"") && streets.Contains("\"Uptown check (subway): ") && streets.Contains("\"Uptown check (street furniture): \"")
              && Regex.Matches(streets, @"TestRefusals\.(?:Tell|TellKey)\(").Count >= 5, "uptown streets tests: three tests, each says why it did not start and writes one check line");
        Check(streets.Contains("GameMembers.NameKey(") && !streets.Contains("GetNameKey("), "uptown streets sources: a station's name through GameMembers, as older builds lack the game's own");
    }

    private static void UptownStreetsTextChecks(string scripts)
    {
        string locales = Path.GetFullPath(Path.Combine(scripts, @"..\Locales"));
        var named = Directory.GetFiles(Path.Combine(scripts, "Uptown"), "Streets*.cs").SelectMany(f => Regex.Matches(File.ReadAllText(f), "\"(reallife_[a-z_]+)\"").Select(m => m.Groups[1].Value))
                             .Distinct().ToList();
        Check(named.Count >= 6, "uptown streets texts: its texts are found: " + named.Count);
        foreach (string language in new[] { "en", "pt", "zh-cn" })
        {
            using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(locales, language + ".json")));
            foreach (string key in named)
            {
                Check(doc.RootElement.TryGetProperty(key, out JsonElement value) && value.GetString().Length > 0, "uptown streets texts: " + key + " is in " + language + ".json");
                if (language == "en" && doc.RootElement.TryGetProperty(key, out JsonElement en))
                {
                    Check(en.GetString().Length <= 240 && !Regex.IsMatch(en.GetString(), "[–—!]| - "), "uptown streets texts: " + key + " at most 240 characters, no dash, no exclamation mark");
                }
            }
        }
        var line = new Dictionary<string, (string, string)>
        {
            { "en", ("Buses now reach every corner of Uptown", "Fixed: ") }, { "pt", ("Os ônibus agora chegam a todos os cantos do Uptown", "Corrigido: ") },
            { "zh-cn", ("公交现在通到上城的每个角落", "已修复：") }, { "de", ("Busse fahren jetzt in jede Ecke von Uptown", "Behoben: ") },
            { "fr", ("Les bus desservent désormais chaque coin d'Uptown", "Corrigé : ") }, { "es", ("Los autobuses llegan ahora a cada rincón de Uptown", "Corregido: ") },
            { "ru", ("Автобусы теперь доходят до каждого уголка Uptown", "Исправлено: ") }, { "nl", ("Bussen komen nu in elke hoek van Uptown", "Opgelost: ") },
            { "tr", ("Otobüsler artık Uptown'ın her köşesine ulaşıyor", "Düzeltildi: ") }
        };
        foreach (var language in line)
        {
            using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(locales, language.Key + ".json")));
            string next = doc.RootElement.TryGetProperty("reallife_changes_next", out JsonElement row) ? row.GetString() : string.Empty;
            int at = next.IndexOf(language.Value.Item1, StringComparison.Ordinal), fix = next.IndexOf(language.Value.Item2, StringComparison.Ordinal);
            Check(at >= 0 && (fix < 0 || at < fix), "uptown streets texts: What's new's next row tells of Uptown's buses and pavements in " + language.Key + ", before its fixes");
        }
    }
}
