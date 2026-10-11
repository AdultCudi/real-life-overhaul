using System;
using System.Collections.Generic;
using RealLifeOverhaul;

// Checks for the taxi called from the phone (Transport/Taxi/TaxiRules.cs, compiled as it ships): the call-out by the
// hour and the weather, what a cancel and a taxi nobody came to cost, what can be taken from a caller with little
// money, the bands a taxi starts in, the wait the dispatcher quotes, which refusal is said first, and when a taxi that
// gets nowhere is planned again or given up. The search that finds the taxi's way is the shared one (Core/RouteSearch),
// checked above on its own; here it is run the way the taxi runs it: from every place of a band at once to the
// caller's lane, on a made-up grid of streets, to show that what comes back is the nearest of those places by road.
//
// What is not here: anything of the game. Which vehicle is taken, how it drives, where it stands and what the game
// does when it is clicked are for the game to show, in the "Taxi check (...)" lines of Player.log.
internal static partial class Program
{
    private static void Taxi()
    {
        TaxiMoney();
        TaxiBands();
        TaxiMinutes();
        TaxiRefusals();
        TaxiWatch();
        TaxiSearch();
    }

    private static void TaxiMoney()
    {
        // The hours of the night rate: 22:00 to 05:59.
        for (int hour = 0; hour < 24; hour++)
        {
            bool night = hour >= 22 || hour <= 5;
            Check(TaxiRules.Night(hour) == night, "taxi: " + hour + ":00 is " + (night ? "night" : "day"));
            Check(Near(TaxiRules.Fee(hour, false), night ? 18f : 12f), "taxi: the call-out at " + hour + ":00 is " + (night ? 18 : 12));
        }
        Check(Near(TaxiRules.Fee(12, true), 18f), "taxi: the call-out by day in the wet is half as much again (18)");
        Check(Near(TaxiRules.Fee(23, true), 27f), "taxi: the call-out by night in the wet is half as much again (27)");
        Check(Near(TaxiRules.Fee(21, false), TaxiRules.CallOut) && Near(TaxiRules.Fee(22, false), TaxiRules.NightCallOut), "taxi: the night rate begins at 22:00");
        Check(Near(TaxiRules.Fee(5, false), TaxiRules.NightCallOut) && Near(TaxiRules.Fee(6, false), TaxiRules.CallOut), "taxi: the night rate ends at 06:00");

        // A cancel: free with far to come, half once it is close; the edge itself is close.
        Check(Near(TaxiRules.CancelFee(12f, 400f), 0f), "taxi: a cancel with 400 m to come is free");
        Check(Near(TaxiRules.CancelFee(12f, 100.5f), 0f), "taxi: a cancel with just over 100 m to come is free");
        Check(Near(TaxiRules.CancelFee(12f, 100f), 6f), "taxi: a cancel with 100 m to come costs half");
        Check(Near(TaxiRules.CancelFee(12f, 0f), 6f), "taxi: a cancel of a standing taxi costs half");
        Check(Near(TaxiRules.CancelFee(27f, 20f), 14f), "taxi: half of 27 is charged as whole money (14)");
        Check(Near(TaxiRules.CancelFee(18f, 50f), 9f), "taxi: a cancel at night, close, costs 9");

        // Nobody came.
        Check(Near(TaxiRules.NoShowFee(12f), 6f) && Near(TaxiRules.NoShowFee(18f), 9f), "taxi: a taxi nobody came to costs half the call-out");
        Check(TaxiRules.NoShowFee(27f) <= 27f * 0.5f + 0.5f, "taxi: half of an odd call-out is within a half of it");

        // Never below nothing, never more than is there, whole money.
        Check(Near(TaxiRules.Payable(12f, 1000f), 12f), "taxi: a caller with money pays the whole charge");
        Check(Near(TaxiRules.Payable(12f, 12f), 12f), "taxi: a caller with exactly the charge pays it");
        Check(Near(TaxiRules.Payable(12f, 7.8f), 7f), "taxi: a caller with 7.80 pays 7");
        Check(Near(TaxiRules.Payable(12f, 0.6f), 0f), "taxi: a caller with 0.60 pays nothing");
        Check(Near(TaxiRules.Payable(12f, 0f), 0f), "taxi: a caller with nothing pays nothing");
        Check(Near(TaxiRules.Payable(12f, -250f), 0f), "taxi: a caller in debt pays nothing");
        Check(Near(TaxiRules.Payable(0f, 500f), 0f), "taxi: no charge takes nothing");
        for (float money = -5f; money <= 30f; money += 0.7f)
        {
            float take = TaxiRules.Payable(18f, money);
            Check(take >= 0f && take <= 18f && money - take >= Math.Min(0f, money) && (money <= 0f || money - take >= 0f), "taxi: paying 18 from " + money.ToString("F1") + " never goes below nothing");
        }
    }

    private static void TaxiBands()
    {
        TaxiRules.StartBand(false, false, out float least, out float most);
        Check(Near(least, 250f) && Near(most, 450f), "taxi: in the dry a taxi starts 250 to 450 m off");
        TaxiRules.StartBand(true, false, out float wetLeast, out float wetMost);
        Check(wetLeast > least && wetMost > most && wetLeast < wetMost, "taxi: in the wet it starts further off");
        Check(wetLeast >= most - 60f, "taxi: the wet band begins about where the dry one ends");
        TaxiRules.StartBand(false, true, out float testLeast, out float testMost);
        TaxiRules.StartBand(true, true, out float testWetLeast, out float testWetMost);
        Check(testLeast >= 60f && testMost < least, "taxi: the test's band is nearer than any real one and never beside the caller");
        Check(Near(testLeast, testWetLeast) && Near(testMost, testWetMost), "taxi: the test's band does not change with the weather");
        Check(TaxiRules.InBand(250f, least, most) && TaxiRules.InBand(450f, least, most) && !TaxiRules.InBand(249f, least, most) && !TaxiRules.InBand(451f, least, most),
            "taxi: a band includes its ends and nothing beyond them");
        Check(least > 115f && wetLeast > 115f, "taxi: every real band begins beyond what counts as in view (110 m)");

        // A second plan starts nearer than the first had got, and never close.
        foreach (float remaining in new[] { 111f, 150f, 200f, 300f, 450f, 640f })
        {
            TaxiRules.ReplanBand(remaining, out float againLeast, out float againMost);
            Check(againLeast < againMost && againLeast >= 115f - 0.01f, "taxi: a second plan from " + remaining + " m has a band, out of view");
            Check(againMost <= Math.Max(remaining - TaxiRules.ReplanNearer, TaxiRules.ReplanLeast + 30f) + 0.01f, "taxi: a second plan from " + remaining + " m starts nearer");
        }

        // A way round that is far too long is no way.
        Check(TaxiRules.RouteAccepted(300f, 450f) && TaxiRules.RouteAccepted(450f * TaxiRules.RouteMost, 450f), "taxi: a route up to the limit is taken");
        Check(!TaxiRules.RouteAccepted(450f * TaxiRules.RouteMost + 1f, 450f) && !TaxiRules.RouteAccepted(0f, 450f), "taxi: a route beyond the limit, or none, is not");
        Check(!TaxiRules.FarAtArrival(80f) && TaxiRules.FarAtArrival(80.5f), "taxi: the caller is far from it beyond 80 m");
        Check(TaxiRules.Grace(15f, false) && !TaxiRules.Grace(15.5f, false) && !TaxiRules.Grace(3f, true), "taxi: it stays a little longer once, for a caller within 15 m");
    }

    private static void TaxiMinutes()
    {
        Check(TaxiRules.Minutes(0f, 1f) == 1, "taxi: the wait is never said to be less than a minute");
        Check(TaxiRules.Minutes(250f, 1f) == 32, "taxi: 250 m take 31.25 s, which is 32 minutes of the game's clock");
        Check(TaxiRules.Minutes(480f, 1f) == 60 && TaxiRules.Minutes(481f, 1f) == 61, "taxi: the wait is rounded up (480 m is 60 minutes, 481 m is 61)");
        Check(TaxiRules.Minutes(650f, 1f) == 82, "taxi: 650 m is 82 minutes");
        Check(TaxiRules.Minutes(450f, 2f) == 2 * TaxiRules.Minutes(450f, 1f) - 1, "taxi: with the game's clock twice as fast the same road is twice the minutes (113 for 57)");
        Check(TaxiRules.Minutes(4f, 1f) == 1 && TaxiRules.Minutes(8f, 0.5f) == 1, "taxi: a few metres are a minute");
        Check(TaxiRules.Minutes(450f, 0f) == TaxiRules.Minutes(450f, 1f), "taxi: a clock speed that cannot be read counts as the game's own");
        Check(Near(TaxiRules.Seconds(800f), 100f), "taxi: 800 m take 100 s at a taxi's pace");
        int last = 0;
        bool rising = true;
        for (float metres = 0f; metres <= 1500f; metres += 25f)
        {
            int minutes = TaxiRules.Minutes(metres, 1f);
            rising &= minutes >= last;
            last = minutes;
        }
        Check(rising, "taxi: more road is never fewer minutes");
        TaxiRules.StartBand(true, false, out _, out float wetMost);
        TaxiRules.StartBand(false, false, out _, out float most);
        Check(TaxiRules.Minutes(wetMost, 1f) >= TaxiRules.Minutes(most, 1f), "taxi: the wait in the wet is no shorter");
    }

    private static void TaxiRefusals()
    {
        Check(TaxiRules.Refuse(true, false, false, false, false, false) == TaxiRules.Refusal.None, "taxi: out of doors, on foot, with none out: a call is taken");
        Check(TaxiRules.Refuse(false, true, true, true, true, true) == TaxiRules.Refusal.Off, "taxi: switched off is said before anything else");
        Check(TaxiRules.Refuse(true, true, true, true, true, true) == TaxiRules.Refusal.OnItsWay, "taxi: a taxi already out is said next");
        Check(TaxiRules.Refuse(true, true, true, false, true, true) == TaxiRules.Refusal.Riding, "taxi: then a ride under way");
        Check(TaxiRules.Refuse(true, false, true, false, true, true) == TaxiRules.Refusal.Held, "taxi: then a scene that holds the caller");
        Check(TaxiRules.Refuse(true, false, false, false, true, true) == TaxiRules.Refusal.InVehicle, "taxi: then a vehicle they are in");
        Check(TaxiRules.Refuse(true, false, false, false, false, true) == TaxiRules.Refusal.Indoors, "taxi: then being indoors");
        // Every one of the five alone refuses.
        for (int bit = 0; bit < 5; bit++)
        {
            Check(TaxiRules.Refuse(true, bit == 0, bit == 1, bit == 2, bit == 3, bit == 4) != TaxiRules.Refusal.None, "taxi: reason " + bit + " alone refuses a call");
        }
    }

    private static void TaxiWatch()
    {
        Check(TaxiRules.Judge(0f, 30f, 300f, false, false) == TaxiRules.Watch.CarryOn, "taxi: a taxi that gains ground is left alone");
        Check(TaxiRules.Judge(39f, 60f, 300f, false, false) == TaxiRules.Watch.CarryOn, "taxi: 39 s without a metre is not long yet");
        Check(TaxiRules.Judge(41f, 60f, 300f, false, false) == TaxiRules.Watch.Replan, "taxi: 41 s without a metre, out of view, far off: planned again");
        Check(TaxiRules.Judge(41f, 60f, 300f, true, false) == TaxiRules.Watch.CarryOn, "taxi: never planned again in view");
        Check(TaxiRules.Judge(41f, 60f, 110f, false, false) == TaxiRules.Watch.CarryOn, "taxi: not planned again with 110 m or less to come");
        Check(TaxiRules.Judge(41f, 60f, 300f, false, true) == TaxiRules.Watch.CarryOn, "taxi: planned again once only");
        Check(TaxiRules.Judge(151f, 200f, 300f, true, true) == TaxiRules.Watch.GiveUp, "taxi: 151 s without a metre: given up");
        Check(TaxiRules.Judge(151f, 200f, 300f, false, false) == TaxiRules.Watch.GiveUp, "taxi: giving up comes before another plan");
        Check(TaxiRules.Judge(5f, 301f, 20f, true, false) == TaxiRules.Watch.GiveUp, "taxi: out 301 s without arriving: given up");
        Check(TaxiRules.Judge(150f, 300f, 300f, true, true) == TaxiRules.Watch.CarryOn, "taxi: at the limits themselves it is still left");
        Check(TaxiRules.ReplanAfter < TaxiRules.GiveUpAfter && TaxiRules.GiveUpAfter < TaxiRules.GiveUpOut, "taxi: a new plan comes before giving up, and that before the longest outing");
        Check(TaxiRules.Seconds(TaxiRules.WetStartMost * TaxiRules.RouteMost) < TaxiRules.GiveUpOut, "taxi: the longest route taken can be driven before it is given up");
    }

    // A grid of two-way streets 50 m apart, a lane point every 10 m, each street two lanes (one each way) 3 m apart.
    // The caller's lane is in the middle; the band's places are every lane point between two distances from it.
    private static void TaxiSearch()
    {
        const int blocks = 12;          // streets each way
        const int per = 5;              // lane points a block
        const float step = 10f;
        int side = blocks * per + 1;
        // Node (x, z, lane): lane 0 runs east or north, lane 1 west or south. Only points on a street exist.
        var id = new Dictionary<long, int>();
        var xs = new List<float>();
        var zs = new List<float>();
        var horizontal = new List<bool>();
        var forward = new List<bool>();
        Func<int, int, bool, bool, long> key = (i, k, h, f) => (((long)i * 1000 + k) * 2 + (h ? 1 : 0)) * 2 + (f ? 1 : 0);
        for (int i = 0; i < side; i++)
        {
            for (int k = 0; k < side; k++)
            {
                foreach (bool h in new[] { true, false })
                {
                    if (h ? k % per != 0 : i % per != 0)
                    {
                        continue;
                    }
                    foreach (bool f in new[] { true, false })
                    {
                        id[key(i, k, h, f)] = xs.Count;
                        float aside = f ? -1.5f : 1.5f;
                        xs.Add(i * step + (h ? 0f : -aside));
                        zs.Add(k * step + (h ? aside : 0f));
                        horizontal.Add(h);
                        forward.Add(f);
                    }
                }
            }
        }
        int count = xs.Count;
        var edges = new List<int>[count];
        for (int n = 0; n < count; n++)
        {
            edges[n] = new List<int>(3);
        }
        for (int i = 0; i < side; i++)
        {
            for (int k = 0; k < side; k++)
            {
                foreach (bool h in new[] { true, false })
                {
                    foreach (bool f in new[] { true, false })
                    {
                        if (!id.TryGetValue(key(i, k, h, f), out int from))
                        {
                            continue;
                        }
                        int ni = i + (h ? (f ? 1 : -1) : 0), nk = k + (h ? 0 : (f ? 1 : -1));
                        if (id.TryGetValue(key(ni, nk, h, f), out int on))
                        {
                            edges[from].Add(on);   // along the lane
                        }
                        if (i % per == 0 && k % per == 0)
                        {
                            foreach (bool turn in new[] { true, false })
                            {
                                if (id.TryGetValue(key(i, k, !h, turn), out int across))
                                {
                                    edges[from].Add(across);   // a turn at the crossing
                                }
                            }
                        }
                    }
                }
            }
        }
        var graph = new RouteSearch(count);
        var start = new int[count + 1];
        var to = new List<int>();
        for (int n = 0; n < count; n++)
        {
            graph.X[n] = xs[n];
            graph.Z[n] = zs[n];
            start[n] = to.Count;
            to.AddRange(edges[n]);
        }
        start[count] = to.Count;
        graph.SetEdges(start, to.ToArray(), new float[to.Count]);

        // The caller stands beside the eastbound lane of the middle street, between two crossings.
        float cx = (blocks / 2) * per * step + 20f, cz = (blocks / 2) * per * step - 4f;
        var goals = new List<int>();
        float nearest = float.MaxValue;
        for (int n = 0; n < count; n++)
        {
            float d = (xs[n] - cx) * (xs[n] - cx) + (zs[n] - cz) * (zs[n] - cz);
            nearest = Math.Min(nearest, d);
        }
        float reach = (float)Math.Sqrt(nearest) + 5f;
        for (int n = 0; n < count; n++)
        {
            float d = (xs[n] - cx) * (xs[n] - cx) + (zs[n] - cz) * (zs[n] - cz);
            if (d <= reach * reach)
            {
                goals.Add(n);
            }
        }
        Check(goals.Count >= 1 && goals.Count <= 12, "taxi: the caller's lane is a handful of lane points (" + goals.Count + ")");

        foreach (bool wet in new[] { false, true })
        {
            TaxiRules.StartBand(wet, false, out float least, out float most);
            // The grid is 600 m across: the bands are scaled to it (a third), which changes nothing about the search.
            least /= 3f;
            most /= 3f;
            var starts = new List<int>();
            for (int n = 0; n < count; n++)
            {
                float d = (float)Math.Sqrt((xs[n] - cx) * (xs[n] - cx) + (zs[n] - cz) * (zs[n] - cz));
                if (TaxiRules.InBand(d, least, most) && (n & 3) == 0)
                {
                    starts.Add(n);
                }
            }
            var path = new List<int>();
            bool found = graph.Find(starts, goals, cx, 0f, cz, reach + 1f, 60000, path);
            Check(found && path.Count >= 3, "taxi: a way is found from the " + (wet ? "wet" : "dry") + " band (" + starts.Count + " places to start from)");
            if (!found)
            {
                continue;
            }
            float metres = 0f;
            for (int p = 1; p < path.Count; p++)
            {
                metres += (float)Math.Sqrt((xs[path[p]] - xs[path[p - 1]]) * (xs[path[p]] - xs[path[p - 1]]) + (zs[path[p]] - zs[path[p - 1]]) * (zs[path[p]] - zs[path[p - 1]]));
            }
            float crow = (float)Math.Sqrt((xs[path[0]] - cx) * (xs[path[0]] - cx) + (zs[path[0]] - cz) * (zs[path[0]] - cz));
            Check(starts.Contains(path[0]) && goals.Contains(path[path.Count - 1]), "taxi: the way begins at a place of the band and ends on the caller's lane");
            Check(TaxiRules.InBand(crow, least, most), "taxi: where it starts is in the band (" + crow.ToString("F0") + " m off)");
            Check(metres >= crow - reach - 1f, "taxi: the road is no shorter than the straight line (" + metres.ToString("F0") + " m)");
            Check(TaxiRules.RouteAccepted(metres, most), "taxi: the way found is one a taxi takes");
            // No other place of the band is nearer by road: each on its own gives a way at least as long.
            bool nearestByRoad = true;
            var one = new List<int>(1) { 0 };
            var other = new List<int>();
            for (int s = 0; s < starts.Count; s += 7)
            {
                one[0] = starts[s];
                if (graph.Find(one, goals, cx, 0f, cz, reach + 1f, 60000, other))
                {
                    float length = 0f;
                    for (int p = 1; p < other.Count; p++)
                    {
                        length += (float)Math.Sqrt((xs[other[p]] - xs[other[p - 1]]) * (xs[other[p]] - xs[other[p - 1]]) + (zs[other[p]] - zs[other[p - 1]]) * (zs[other[p]] - zs[other[p - 1]]));
                    }
                    nearestByRoad &= length >= metres - 0.05f;
                }
            }
            Check(nearestByRoad, "taxi: no other place of the " + (wet ? "wet" : "dry") + " band is nearer by road");
            // A place traffic stands on is left out, and the next way begins somewhere else.
            int taken = path[0];
            starts.Remove(taken);
            Check(graph.Find(starts, goals, cx, 0f, cz, reach + 1f, 60000, other) && other[0] != taken, "taxi: with that place left out another is found");
        }
        Check(!graph.Find(new List<int>(), goals, cx, 0f, cz, reach + 1f, 60000, new List<int>()), "taxi: with no place to start from there is no way");
        Check(!graph.Find(new List<int> { 0 }, new List<int>(), cx, 0f, cz, reach + 1f, 60000, new List<int>()), "taxi: with no lane to end at there is no way");
    }
}
