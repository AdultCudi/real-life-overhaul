using System;
using System.Collections.Generic;
using System.IO;
using RealLifeOverhaul;

// A bus never stuck for good (a player's report, 20 Oct 2026: "I see the bus but it is stuck in place and not moving"). The
// way round a lane closed ahead or something standing still in its lane (Transport/BusDetour) on made-up roads: two lanes
// one way, one lane, both closed, a short block, the bus's own position; the red light that is never waited at for ever,
// seen or not (RideRules.LightHolds, the bus and the taxi); when the bus goes round (RideRules.GoRound); and over the
// sources that each is asked where it must be, and that a bus let go in view drives on rather than standing in the road.
internal static partial class Program
{
    // A made-up road: lanes of points 5 m apart along +z, side by side 3.5 m apart, each point knowing the next and the one beside it.
    private sealed class PretendLanes : IBusLanes
    {
        public readonly List<float> Xs = new List<float>(), Zs = new List<float>();
        public readonly List<List<int>> Nexts = new List<List<int>>(), Sides = new List<List<int>>();
        public readonly HashSet<int> Closed = new HashSet<int>();

        public int Lane(float x, int points)
        {
            int first = Xs.Count;
            for (int i = 0; i < points; i++)
            {
                Xs.Add(x);
                Zs.Add(i * 5f);
                Nexts.Add(new List<int>());
                Sides.Add(new List<int>());
                if (i > 0)
                {
                    Nexts[first + i - 1].Add(first + i);
                }
            }
            return first;
        }

        public void Beside(int a, int b, int points)
        {
            for (int i = 0; i < points; i++)
            {
                Sides[a + i].Add(b + i);
                Sides[b + i].Add(a + i);
            }
        }

        public bool Open(int point) => point >= 0 && point < Xs.Count && !Closed.Contains(point);
        public float X(int point) => Xs[point];
        public float Z(int point) => Zs[point];
        public int NextCount(int point) => Nexts[point].Count;
        public int Next(int point, int k) => Nexts[point][k];
        public int SideCount(int point) => Sides[point].Count;
        public int Side(int point, int k) => Sides[point][k];
    }

    private static void BusRound()
    {
        // ---- two lanes one way, the bus's lane closed from 100 m to 120 m ahead (the works), the bus at 50 m.
        var road = new PretendLanes();
        int a = road.Lane(0f, 60), b = road.Lane(3.5f, 60);
        road.Beside(a, b, 60);
        for (int i = 20; i <= 24; i++)
        {
            road.Closed.Add(a + i);
        }
        var route = new List<int>();
        for (int i = 10; i < 60; i++)
        {
            route.Add(a + i);
        }
        var way = new List<int>();
        bool found = BusDetour.Find(road, route, 0, 10, 14, 0f, 50f, way, out int leave, out int rejoin);
        bool allBeside = way.Count > 0, ahead = true;
        foreach (int p in way)
        {
            allBeside &= p >= b && p < b + 60 && road.Open(p);
            ahead &= road.Z(p) - 50f >= BusDetour.Lead - 1e-3f;
        }
        Check(found && leave >= 7 && leave < 10 && rejoin > 14 && allBeside && ahead && road.Open(route[rejoin]),
            "bus round: a lane closed ahead is gone round by the lane beside it, from just before it (place " + leave + ") to beyond it (place " + rejoin + "), "
            + way.Count + " points, every one open and at least " + BusDetour.Lead + " m ahead of the bus");
        float lastZ = -1f;
        bool forwards = true;
        foreach (int p in way)
        {
            forwards &= road.Z(p) > lastZ;
            lastZ = road.Z(p);
        }
        Check(forwards && road.Z(route[rejoin]) > road.Z(way[way.Count - 1]) - BusDetour.RejoinWithin,
            "bus round: the way round runs forwards along the road and rejoins beside or ahead of its last point, never back");
        bool again = BusDetour.Find(road, route, 0, 10, 14, 0f, 50f, way, out int leave2, out int rejoin2);
        Check(again && leave2 == leave && rejoin2 == rejoin, "bus round: the same way round every time (Going Public: the host's bus alone, but nothing left to chance)");

        // ---- one lane only: no way round, nothing made up.
        var single = new PretendLanes();
        int s = single.Lane(0f, 40);
        for (int i = 20; i <= 22; i++)
        {
            single.Closed.Add(s + i);
        }
        var oneRoute = new List<int>();
        for (int i = 5; i < 40; i++)
        {
            oneRoute.Add(s + i);
        }
        Check(!BusDetour.Find(single, oneRoute, 0, 15, 17, 0f, 25f, way, out _, out _) && way.Count == 0,
            "bus round: a road of one lane has no way round; it waits as the traffic there does");

        // ---- both lanes closed: no way round.
        var both = new PretendLanes();
        int c = both.Lane(0f, 50), d = both.Lane(3.5f, 50);
        both.Beside(c, d, 50);
        for (int i = 20; i <= 24; i++)
        {
            both.Closed.Add(c + i);
            both.Closed.Add(d + i);
        }
        var bothRoute = new List<int>();
        for (int i = 10; i < 50; i++)
        {
            bothRoute.Add(c + i);
        }
        Check(!BusDetour.Find(both, bothRoute, 0, 10, 14, 0f, 50f, way, out _, out _), "bus round: with the lane beside it closed too, there is no way round");

        // ---- the places before the bus are never left from; a route of its own points (-1) is passed over.
        Check(!BusDetour.Find(road, route, 10, 10, 14, 0f, 50f, way, out _, out _), "bus round: never from a place of its route behind the bus");
        var mixed = new List<int>(route);
        mixed[9] = -1;
        Check(BusDetour.Find(road, mixed, 0, 10, 14, 0f, 50f, way, out int leave3, out _) && leave3 != 9, "bus round: a place of its own on the route (a bay's curve) is never left from");
        Check(!BusDetour.Find(road, route, 0, 14, 10, 0f, 50f, way, out _, out _) && !BusDetour.Find(null, route, 0, 10, 14, 0f, 50f, way, out _, out _)
              && !BusDetour.Find(road, route, 0, 10, route.Count, 0f, 50f, way, out _, out _),
            "bus round: a blocked stretch the wrong way round, no road or past the route's end is refused");

        // ---- a car standing in its lane at 100 m: the stretch it takes, gone round the same way, back beyond it.
        road.Closed.Clear();
        Check(BusDetour.Find(road, route, 0, 10, 12, 0f, 50f, way, out _, out int rejoin4) && rejoin4 > 12, "bus round: something standing in its lane is gone round and the route rejoined beyond it");

        // ---- when it goes round, and the lights.
        Check(!RideRules.GoRound(true, RideRules.RoundClosedAfter - 0.5f, false, false, false) && RideRules.GoRound(true, RideRules.RoundClosedAfter, false, false, false),
            "bus round: before a closed lane it goes round after " + RideRules.RoundClosedAfter + " s");
        Check(!RideRules.GoRound(false, RideRules.RoundStillAfter - 1f, true, false, false) && RideRules.GoRound(false, RideRules.RoundStillAfter, true, false, false),
            "bus round: behind something standing still in its lane it goes round after " + RideRules.RoundStillAfter + " s");
        Check(!RideRules.GoRound(false, 200f, false, false, false) && !RideRules.GoRound(false, 200f, true, true, false) && !RideRules.GoRound(true, 200f, false, false, true),
            "bus round: never round a car that moves (a queue), nor one that only stands for the bus (it is told to go on), and once a hold");
        Check(RideRules.RoundStillAfter > RideRules.LightPatience * 0.5f && RideRules.RoundStillAfter < 60f,
            "bus round: a car in a queue at a red light moves before the bus would go round it, and the bus goes round within a minute");
        Check(RideRules.LightHolds(RideRules.LightPatience - 1f, false) && !RideRules.LightHolds(RideRules.LightPatience, false)
              && RideRules.LightHolds(RideRules.LightPatienceSeen - 1f, true) && !RideRules.LightHolds(RideRules.LightPatienceSeen, true),
            "bus round: a red light is waited at " + RideRules.LightPatience + " s unseen and " + RideRules.LightPatienceSeen + " s in view, then taken for one the game does not run");
        Check(RideRules.StuckDriving > RideRules.LightPatience && RideRules.LightPatienceSeen < 150f,
            "bus round: a light in view lets the bus go before the director would give it up (150 s)");

        // ---- over the sources.
        string scripts = System.IO.Path.Combine(LocalesFolder(), "..", "Scripts");
        string bus = System.IO.File.ReadAllText(System.IO.Path.Combine(scripts, "Transport", "TransitBus.cs"));
        string held = System.IO.File.ReadAllText(System.IO.Path.Combine(scripts, "Transport", "TransitBus.Held.cs"));
        string round = System.IO.File.ReadAllText(System.IO.Path.Combine(scripts, "Transport", "TransitBus.Round.cs"));
        string gates = System.IO.File.ReadAllText(System.IO.Path.Combine(scripts, "Traffic", "RouteGates.cs"));
        string driver = System.IO.File.ReadAllText(System.IO.Path.Combine(scripts, "Transport", "BusDriver.cs"));
        Check(bus.Contains("RideRules.LightHolds(WaitingFor, CitySpots.InView(point.position))") && gates.Contains("RideRules.LightHolds(waitedFor, CitySpots.InView(point.position))")
              && !bus.Contains("|| CitySpots.InView(point.position))") && !gates.Contains("|| CitySpots.InView(point.position))"),
            "bus round source: the bus and the taxi wait at a light by the one rule, never for ever while it is in view");
        Check(held.Contains("if (!_hold.Rounded && Round(now, still, by))") && round.Contains("RideRules.GoRound(closed, still, holderStill, yields, _hold.Rounded, unitBehind)")
              && round.Contains("BusDetour.Find(Lanes, _routePoints, next, blockFrom, blockTo,") && round.Contains("_driver.Pass(by,") && round.Contains("Begin();"),
            "bus round source: a hold looks for a way round by the rules, once, and drives it past what held it");
        Check(driver.Contains("hit == _passing") && driver.Contains("_passing = null;"), "bus round source: what it goes round is passed over only on that drive");
        int goingOut = bus.IndexOf("if (_goingOut)", StringComparison.Ordinal);
        string afterGoingOut = goingOut >= 0 ? bus.Substring(goingOut, Math.Min(1200, bus.Length - goingOut)) : string.Empty;
        Check(afterGoingOut.Contains("else if (_driver.Arrived && !_driver.Active && _gate < 0)\r\n") || afterGoingOut.Contains("else if (_driver.Arrived && !_driver.Active && _gate < 0)\n"),
            "bus round source: a bus let go in view drives on along the road for as long as it is seen, never left standing at the end of its route");
    }
}
