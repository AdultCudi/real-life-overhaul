using System;
using System.Collections.Generic;
using System.Linq;
using RealLifeOverhaul;
using RealLifeOverhaul.NewGround;
using RealLifeOverhaul.Subway;
using RealLifeOverhaul.Uptown;

// The subway stations of Uptown and Cedar Heights on the ground (Subway/StationsRules.cs, the rules the director calls before it stands one): three
// stations west to east, each held whole by its block's lawn or pavement and never by a carriageway; off every lot of the plan (its buildings, the
// open runs the player builds on, the big special places); clear of its park's paths and pieces, of the pavements' furniture, of the avenue's lamp
// posts, of the bus stops the routes want and of the bus termini; its rider coming up on ground both walking plans hold (the player's and the
// pedestrians'), with room round it and a kerb a car stops at near; an entrance on a pavement leaving a way past; and each of these refusing a
// station put where it must not be. With --subway the stations are printed in figures.
internal static partial class Program
{
    private static void UptownSubway(bool print)
    {
        IReadOnlyList<StationSite> sites = StationsRules.Sites;
        GroundPlan player = LayoutPlans.PlayerGround(), npc = LayoutPlans.NpcGround();
        var routes = new List<TransitPlan.Route>();
        StreetsRules.Routes(routes);
        var furniture = new List<StreetPlace>();
        StreetsRules.Furniture(routes, furniture);
        List<float> fx = furniture.Select(p => p.X).ToList(), fz = furniture.Select(p => p.Z).ToList();
        var poleX = new List<float>();
        var poleZ = new List<float>();
        StreetsRules.StopPoles(poleX, poleZ);
        var termini = new List<PlanTerminus>();
        LayoutPlans.Termini(termini);
        List<float> tx = termini.Select(t => t.X).ToList(), tz = termini.Select(t => t.Z).ToList();

        Check(sites.Count == 3 && sites[0].Id == "birchwood" && sites[1].Id == "summit" && sites[2].Id == "east" && sites[0].X < sites[1].X && sites[1].X < sites[2].X,
            "subway: three stations, west to east: birchwood, summit and east");
        Check(sites[0].District == DistrictSplit.CedarHeights && sites[1].District == DistrictSplit.CedarHeights && sites[2].District == DistrictSplit.Uptown,
            "subway: Birchwood's and Summit Park's are in Cedar Heights, the east park's in Uptown, each by its block's column");
        Check(sites[0].Block == LayoutRules.CampusBlock && ParksRules.IsPark(sites[1].Block) && ParksRules.IsPark(sites[2].Block) && sites[1].Block != sites[2].Block
              && LayoutRules.Squares.Count == 3,
            "subway: one station by each of the plan's three squares: the campus and the two parks");
        Check(ReferenceEquals(StationsRules.Sites, sites) && StationsRules.Get("summit") == sites[1] && StationsRules.Get("nowhere") == null, "subway: the plan is made once and found by id");
        Check(Math.Abs(StationsRules.ParksSpineHalf - ParksRules.SpineHalf) < 0.0001f, "subway: the spine's half width the stations are placed by is the parks' own");
        Check(Math.Abs(StationsRules.LawnTop - (0.05518f - SurfaceRules.GroundDrop)) < 0.0002f && Math.Abs(StationsRules.PavementTop - (0.055f + SurfaceRules.AvenueLift)) < 0.0002f,
            "subway: the heights an entrance is put at are the district's own surfaces' (a block's ground lowered, an avenue's pavement raised)");
        Check(Math.Abs(StationsRules.PavementWidth - 3.7f) < 0.001f && Math.Abs(StationsRules.StopClear - StreetsRules.StopClear) < 0.001f,
            "subway: an avenue's pavement is 3.7 m, and a station keeps the stretch from a wanted stop that the furniture keeps");

        for (int i = 0; i < sites.Count; i++)
        {
            StationSite s = sites[i];
            string name = "subway: " + s.Id + ": ";
            StationsRules.Footprint(s, out float x0, out float z0, out float x1, out float z1);
            StationsRules.Arrival(s, out float ax, out float az);
            Check(Math.Abs((x1 - x0) * (z1 - z0) - StationsRules.HalfWidth * 2f * (StationsRules.Front + StationsRules.Back)) < 0.01f && ax > x0 - 1f && ax < x1 + 1f && az > z0 - 1f && az < z1 + 1f,
                name + "its ground is the entrance's own size, its rider up just ahead of its mouth");
            int block = StationsRules.Holds(s, out bool pavement);
            Check(block == s.Block && pavement == (s.Ground == StationGround.Pavement),
                name + "held whole by its block's " + (s.Ground == StationGround.Pavement ? "pavement" : "lawn") + ", off every carriageway (block " + block + ")");
            Check(StationsRules.MeetsLot(s, UptownLots.All) == null && StationsRules.MeetsLot(s, UptownLots.Places) == null,
                name + "off every lot of the plan: its buildings, its open runs and the big special places (" + (StationsRules.MeetsLot(s, UptownLots.All) ?? StationsRules.MeetsLot(s, UptownLots.Places)) + ")");
            float lotNear = UptownLots.All.Concat(UptownLots.Places).Min(l => SubwayLotGap(s, l)), openNear = UptownLots.All.Where(l => l.Open).Min(l => SubwayLotGap(s, l));
            Check(lotNear >= StationsRules.LotClear && openNear >= 10f,
                name + "its nearest lot is " + lotNear.ToString("F1") + " m off and the nearest land the player can build on " + openNear.ToString("F0") + " m off");
            // Where its rider comes up: on the player's walking plan and the pedestrians', with room round it.
            bool onPlayer = true, onPeople = true;
            foreach ((float dx, float dz) in new[] { (0f, 0f), (StationsRules.ArriveRoom, 0f), (-StationsRules.ArriveRoom, 0f), (0f, StationsRules.ArriveRoom), (0f, -StationsRules.ArriveRoom) })
            {
                onPlayer &= UptownCovered(player.Boxes, ax + dx, az + dz);
                onPeople &= UptownCovered(npc.Boxes, ax + dx, az + dz);
            }
            Check(onPlayer && onPeople, name + "its rider comes up on ground the player's walking plan and the pedestrians' both hold, with " + StationsRules.ArriveRoom + " m round it ("
                + (onPlayer ? "player yes" : "player NO") + ", " + (onPeople ? "pedestrians yes" : "pedestrians NO") + ")");
            float kerb = StationsRules.KerbDistance(s);
            Check(kerb <= StationsRules.RoadNear && kerb > 0.5f, name + "a kerb a car stops at is " + kerb.ToString("F1") + " m from where its rider comes up (within " + StationsRules.RoadNear + ")");
            Check(StationsRules.MeetsPoint(s, fx, fz, StationsRules.PieceClear, "furniture") == null,
                name + "clear of the pavements' furniture (" + StationsRules.MeetsPoint(s, fx, fz, StationsRules.PieceClear, "furniture") + ")");
            Check(StationsRules.MeetsPoint(s, poleX, poleZ, StationsRules.StopClear, "a stop") == null && StationsRules.MeetsPoint(s, tx, tz, StationsRules.StopClear, "a terminus") == null,
                name + "no bus stop the routes want and no terminus within " + StationsRules.StopClear + " m ("
                + (StationsRules.MeetsPoint(s, poleX, poleZ, StationsRules.StopClear, "a stop") ?? StationsRules.MeetsPoint(s, tx, tz, StationsRules.StopClear, "a terminus")) + ")");
            var lampX = new List<float>();
            var lampZ = new List<float>();
            StationsRules.Lamps(s, lampX, lampZ);
            StationsRules.PavementRoom(s, out float behind, out float toKerb);
            if (s.Ground == StationGround.Pavement)
            {
                Check(lampX.Count == 3 && StationsRules.MeetsPoint(s, lampX, lampZ, StationsRules.LampClear, "a lamp") == null,
                    name + "clear of its avenue's three lamp posts (" + StationsRules.MeetsPoint(s, lampX, lampZ, StationsRules.LampClear, "a lamp") + ")");
                Check(lampZ.All(z => z > z0 - 0.01f && z < z1 + 0.01f) && lampX.All(x => x < x0 - StationsRules.LampClear || x > x1 + StationsRules.LampClear),
                    name + "the lamps stand in its own line along the kerb, before and after it");
                Check(behind >= StationsRules.WayPast && toKerb >= 0.15f && Math.Abs(behind + toKerb + StationsRules.HalfWidth * 2f - StationsRules.PavementWidth) < 0.01f,
                    name + "it leaves " + behind.ToString("F2") + " m of pavement behind it as the way past and " + toKerb.ToString("F2") + " m to the kerb");
                Check(UptownCovered(npc.Boxes, s.X, s.Z) && UptownCovered(player.Boxes, s.X, s.Z), name + "it stands on the pedestrians' pavement, which its cut then leaves a way past on");
            }
            else
            {
                Check(lampX.Count == 0 && behind < 0f && toKerb < 0f, name + "on a park's lawn it has no lamp post of the avenue's in its way and leaves the whole pavement");
                var pieces = new List<ParkPiece>();
                ParksRules.Pieces(s.Block, pieces);
                List<float> px = pieces.Select(p => p.X).ToList(), pz = pieces.Select(p => p.Z).ToList();
                string met = StationsRules.MeetsPoint(s, px, pz, StationsRules.PieceClear, "a piece of the park");
                Check(pieces.Count > 20 && met == null, name + "clear of its park's " + pieces.Count + " trees, bushes, benches, lamps and bins (" + met + ")");
                var paths = new List<PlanBox>();
                ParksRules.Paths(s.Block, paths);
                bool offPaths = paths.All(p => StationsRules.MeetsBox(s, p.X - p.HalfX, p.Z - p.HalfZ, p.X + p.HalfX, p.Z + p.HalfZ, 0.25f, "a path") == null);
                ParksRules.Centre(s.Block, out float cx, out _);
                Check(offPaths && Math.Abs(ax - cx) <= ParksRules.SpineHalf - StationsRules.ArriveRoom + 0.03f,
                    name + "off every path of its park, its mouth a step from the spine and its rider up on the spine itself (" + Math.Abs(ax - cx).ToString("F2") + " m from its middle)");
                ParksRules.Ground(s.Block, out _, out float gz0, out _, out float gz1);
                float inFrom = Math.Min(Math.Abs(z0 - gz0), Math.Abs(gz1 - z1));
                Check(inFrom > 0.3f && Math.Max(Math.Abs(z1 - gz0), Math.Abs(gz1 - z0)) > 0f && Math.Min(z1 - gz0, gz1 - z0) < ParksRules.EdgeClear,
                    name + "it stands in the strip of lawn along the park's edge the trees keep off (" + inFrom.ToString("F2") + " m in from the pavement)");
                Check(!UptownCovered(npc.Boxes, s.X, s.Z), name + "the pedestrians' ground is not under it: only the player's lawn is cut");
            }
            if (print)
            {
                Console.WriteLine("subway: " + s.Id + " (" + s.NameKey + ", block " + s.Block + ", " + s.Ground + ", district " + s.District + "): origin (" + s.X.ToString("F2") + ", "
                                  + s.Z.ToString("F2") + ") yaw " + s.Yaw + ", ground x " + x0.ToString("F2") + " to " + x1.ToString("F2") + ", z " + z0.ToString("F2") + " to " + z1.ToString("F2")
                                  + ", rider up at (" + ax.ToString("F2") + ", " + az.ToString("F2") + "), kerb " + kerb.ToString("F1") + " m, nearest lot " + lotNear.ToString("F1")
                                  + " m, nearest open land " + openNear.ToString("F0") + " m, height " + StationsRules.GroundOf(s).ToString("F4"));
            }
        }

        // Each rule refuses a station put where it must not be.
        Lot lot = UptownLots.All[0];
        var onLot = new StationSite("x", "k", lot.Block, StationGround.Park, lot.X, lot.Z, 0);
        Check(StationsRules.MeetsLot(onLot, UptownLots.All) != null, "subway: a station on a lot is refused, and the lot named");
        Lot open = UptownLots.All.First(l => l.Open);
        Check(StationsRules.MeetsLot(new StationSite("x", "k", open.Block, StationGround.Park, open.X, open.Z, 90), UptownLots.All) == open.Id,
            "subway: a station on land the player can build on is refused, and that land named");
        var onRoad = new StationSite("x", "k", 309, StationGround.Park, LayoutRules.StreetX(4), sites[1].Z + 40f, 0);
        Check(StationsRules.Holds(onRoad, out _) == 0 && StationsRules.KerbDistance(onRoad) == float.MaxValue, "subway: a station on a carriageway is held by no block");
        var onAvenue = new StationSite("x", "k", 309, StationGround.Park, sites[1].X, LayoutRules.AvenueZ(2), 90);
        Check(StationsRules.Holds(onAvenue, out _) == 0, "subway: a station in the middle of an avenue is held by no block");
        var outside = new StationSite("x", "k", 309, StationGround.Park, 5000f, 5000f, 0);
        Check(StationsRules.Holds(outside, out _) == 0, "subway: a station off the band is held by no block");
        ParksRules.Centre(309, out float parkX, out float parkZ);
        var deepInPark = new StationSite("x", "k", 309, StationGround.Park, parkX + 20f, parkZ + 15f, 0);
        StationsRules.Arrival(deepInPark, out float dax, out float daz);
        Check(StationsRules.Holds(deepInPark, out bool deepPavement) == 309 && !deepPavement && UptownCovered(player.Boxes, dax, daz) && !UptownCovered(npc.Boxes, dax, daz),
            "subway: a station on a park's lawn away from its paths would bring its rider up where the pedestrians' plan holds nothing: the plan's two open on the spine");
        var wide = new StationSite("x", "k", sites[0].Block, StationGround.Pavement, sites[0].X, sites[0].Z + 0.5f, 90);
        StationsRules.PavementRoom(wide, out float wideBehind, out _);
        Check(wideBehind < StationsRules.WayPast, "subway: an entrance half a metre further from the kerb leaves less than the way past wanted (" + wideBehind.ToString("F2") + " m)");
        var lampsHere = new List<float>();
        var lampsHereZ = new List<float>();
        StationsRules.Lamps(sites[0], lampsHere, lampsHereZ);
        var onLamp = new StationSite("x", "k", sites[0].Block, StationGround.Pavement, lampsHere[0], sites[0].Z, 90);
        Check(StationsRules.MeetsPoint(onLamp, lampsHere, lampsHereZ, StationsRules.LampClear, "a lamp") != null, "subway: an entrance on a lamp post's place is refused");
        var onStop = new StationSite("x", "k", 101, StationGround.Pavement, poleX[0], poleZ[0], 90);
        Check(StationsRules.MeetsPoint(onStop, poleX, poleZ, StationsRules.StopClear, "a stop") != null, "subway: an entrance at a bus stop the routes want is refused");
        Check(StationsRules.MeetsBox(sites[1], sites[1].X - 1f, sites[1].Z - 1f, sites[1].X + 1f, sites[1].Z + 1f, 0f, "a box") == "a box"
              && StationsRules.MeetsBox(sites[1], sites[1].X + 40f, sites[1].Z, sites[1].X + 50f, sites[1].Z + 5f, 0.5f, "a box") == null,
            "subway: a rectangle on an entrance's ground is met, one far off is not");

        // The four turns: the mouth looks the way the turn says, and the ground follows it.
        bool turns = true;
        foreach (int yaw in new[] { 0, 90, 180, 270, 360, -90 })
        {
            var t = new StationSite("x", "k", 309, StationGround.Park, 10f, 20f, yaw);
            StationsRules.Forward(yaw, out float fwx, out float fwz);
            StationsRules.Footprint(t, out float tx0, out float tz0, out float tx1, out float tz1);
            StationsRules.Arrival(t, out float tax, out float taz);
            turns &= Math.Abs(fwx * fwx + fwz * fwz - 1f) < 0.0001f && Math.Abs(tax - (10f + fwx * StationsRules.ArriveAhead)) < 0.001f && Math.Abs(taz - (20f + fwz * StationsRules.ArriveAhead)) < 0.001f;
            turns &= Math.Abs((tx1 - tx0) + (tz1 - tz0) - (StationsRules.HalfWidth * 2f + StationsRules.Front + StationsRules.Back)) < 0.001f;
            turns &= tx0 <= 10f && tx1 >= 10f && tz0 <= 20f && tz1 >= 20f;
        }
        Check(turns, "subway: at each of the four turns the mouth looks its way, the rider comes up ahead of it and the ground holds the origin");
        StationsRules.Carve(out float ahead, out float across, out float along);
        Check(across > 1f && across < StationsRules.HalfWidth * 2f && along > 3f && along < StationsRules.Front + StationsRules.Back && Math.Abs(ahead - (StationsRules.Front - StationsRules.Back) * 0.5f) < 0.001f
              && StationsRules.ArriveAhead - (ahead + along * 0.5f) > StationsRules.ArriveRoom,
            "subway: the walking meshes' cut is inside the entrance's ground and ends " + (StationsRules.ArriveAhead - (ahead + along * 0.5f)).ToString("F2") + " m short of where its rider comes up");
    }

    // The metres between a station's ground (with where its rider comes up) and a lot's.
    private static float SubwayLotGap(StationSite s, Lot lot)
    {
        StationsRules.Footprint(s, out float x0, out float z0, out float x1, out float z1);
        StationsRules.Arrival(s, out float ax, out float az);
        x0 = Math.Min(x0, ax);
        x1 = Math.Max(x1, ax);
        z0 = Math.Min(z0, az);
        z1 = Math.Max(z1, az);
        StationsRules.LotGround(lot, out float lx0, out float lz0, out float lx1, out float lz1);
        float dx = Math.Max(0f, Math.Max(lx0 - x1, x0 - lx1)), dz = Math.Max(0f, Math.Max(lz0 - z1, z0 - lz1));
        return (float)Math.Sqrt(dx * dx + dz * dz);
    }
}
