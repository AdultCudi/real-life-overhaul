using System;
using System.Collections.Generic;
using System.Diagnostics;
using RealLifeOverhaul;

// The bus network (Scripts/Transport/TransitPlan, TransitEta, Core/RouteSearch): the timetable hour by hour
// and the night service, the calls at one stop kept apart, the estimate of a bus's arrival and what it learns from the buses
// that come, the panel's figure and when it says DELAYED, and the layout over a made-up city of avenues and streets: which
// station pairs the routes join, both directions of each, no line doubling back, the stops lines share, how many lines each
// place and station has, and the journeys of one bus or one change between every two places.
internal static partial class Program
{
    private static void BusNetwork()
    {
        var watch = Stopwatch.StartNew();
        int before = _run, failedBefore = _failed;
        BusTimetable();
        BusEstimate();
        BusPanelFigure();
        BusDoublesBack();
        BusLayout();
        BusPlanner();
        BusSession();
        BusStopsLikely();
        Console.WriteLine("  Bus network: " + (_run - before) + " checks, " + (_failed - failedBefore) + " failed, " + watch.ElapsedMilliseconds + " ms");
    }

    // ---------------------------------------------------------------- the dressings built behind the loading screen

    // A line's stops at the spacing wanted: the first at its distance, one more each spacing, none on a line shorter than that.
    private static void BusStopsLikely()
    {
        Check(TransitPlan.StopsLikely(0f, 90f, 260f) == 0 && TransitPlan.StopsLikely(89.9f, 90f, 260f) == 0 && TransitPlan.StopsLikely(90f, 90f, 260f) == 1
              && TransitPlan.StopsLikely(349.9f, 90f, 260f) == 1 && TransitPlan.StopsLikely(350f, 90f, 260f) == 2 && TransitPlan.StopsLikely(2690f, 90f, 260f) == 11,
            "bus network: a line has a stop at 90 m and one each 260 m after it (none short of 90 m, 11 on 2.69 km)");
        Check(TransitPlan.StopsLikely(5000f, 90f, 0f) == 0 && TransitPlan.StopsLikely(-10f, 90f, 260f) == 0 && TransitPlan.StopsLikely(1e6f, 90f, 260f) == 3846,
            "bus network: no spacing or no length gives no stops, and the longest line its share (3846 on 1,000 km)");
    }

    // ---------------------------------------------------------------- the timetable

    private static void BusTimetable()
    {
        bool mainAll = true, localNight = true, localDay = true;
        var table = new System.Text.StringBuilder("  Bus timetable (game minutes between two buses of a line at a stop, weekday; main / other):");
        for (int hour = 0; hour < 24; hour++)
        {
            float main = TransitPlan.Headway(true, hour, false, false, 1f);
            float local = TransitPlan.Headway(false, hour, false, false, 1f);
            mainAll &= main > 0f && main <= 90f;
            bool night = hour >= TransitPlan.NightFrom && hour < TransitPlan.NightUntil;
            localNight &= !night || local == 0f;
            localDay &= night || (local > 0f && local >= main);
            table.Append(' ').Append(hour.ToString("00")).Append(' ').Append(main.ToString("F0")).Append('/').Append(local > 0f ? local.ToString("F0") : "none").Append(';');
        }
        Console.WriteLine(table.ToString());
        Check(mainAll, "bus timetable: the main lines run every hour of the day and night, every 90 game minutes at the least often");
        Check(localNight, "bus timetable: between 01:00 and 05:00 only the main lines run (the night service)");
        Check(localDay, "bus timetable: the other lines run every other hour, never more often than the main lines");
        Check(TransitPlan.Headway(true, 8, false, false, 1f) < TransitPlan.Headway(true, 12, false, false, 1f) && TransitPlan.Headway(true, 17, false, false, 1f) < TransitPlan.Headway(true, 21, false, false, 1f),
              "bus timetable: the rush hours run more often than the middle of the day and the evening");
        Check(TransitPlan.Headway(true, 8, true, false, 1f) == TransitPlan.Headway(true, 12, true, false, 1f), "bus timetable: at the weekend the rush hours run as the rest of the day");
        Check(TransitPlan.Headway(true, 12, false, true, 1f) > TransitPlan.Headway(true, 12, false, false, 1f), "bus timetable: in the rain buses run less often");
        Check(TransitPlan.Headway(true, 12, false, false, 1.3f) < TransitPlan.Headway(true, 12, false, false, 1f) && TransitPlan.Headway(true, 12, false, false, 0.75f) > TransitPlan.Headway(true, 12, false, false, 1f),
              "bus timetable: the city's transit policy runs them more or less often");
        Check(!TransitPlan.Weekend(1) && !TransitPlan.Weekend(4) && TransitPlan.Weekend(5) && TransitPlan.Weekend(7) && !TransitPlan.Weekend(8),
              "bus timetable: the game's weekend is Friday to Sunday, day 1 a Monday");

        // The next call: never in the past, never further off than a headway while the line runs, every headway on.
        bool soon = true, spaced = true;
        for (int day = 1; day <= 8; day++)
        {
            for (double minute = day * 1440.0 + 300.0; minute < day * 1440.0 + 1440.0; minute += 37.0)
            {
                double offset = TransitPlan.Offset(7, 12f);
                double call = TransitPlan.NextCall(minute, false, offset, false, 1f);
                int hour = (int)(minute % 1440.0 / 60.0);
                float headway = TransitPlan.Headway(false, hour, TransitPlan.Weekend(day), false, 1f);
                soon &= call >= minute - 1e-6 && (headway <= 0f || call <= minute + 60.0 + 90.0);
                double after = TransitPlan.NextCall(call + 0.01, false, offset, false, 1f);
                int callHour = (int)(call % 1440.0 / 60.0);
                float callHeadway = TransitPlan.Headway(false, callHour, TransitPlan.Weekend((int)(call / 1440.0)), false, 1f);
                spaced &= after > call && (Math.Abs(after - call - callHeadway) < 1e-6 || (int)(after % 1440.0 / 60.0) != callHour);
            }
        }
        Check(soon, "bus timetable: a line's next call is never in the past and never further off than its headway while it runs");
        Check(spaced, "bus timetable: calls come every headway within an hour");
        double night0 = 3 * 1440.0 + 150.0;   // 02:30
        double local0 = TransitPlan.NextCall(night0, false, TransitPlan.Offset(3, 0f), false, 1f);
        double main0 = TransitPlan.NextCall(night0, true, TransitPlan.Offset(3, 0f), false, 1f);
        Check(local0 >= 3 * 1440.0 + TransitPlan.NightUntil * 60.0 && local0 < 3 * 1440.0 + TransitPlan.NightUntil * 60.0 + 80.0, "bus timetable: at 02:30 the next bus of a line that does not run at night is after 05:00");
        Check(main0 >= night0 && main0 <= night0 + 90.0, "bus timetable: at 02:30 a main line's next bus is within its night headway");
        Check(Math.Abs(TransitPlan.Offset(5, 30f) - TransitPlan.Offset(5, 0f) - 30.0) < 1e-9 && TransitPlan.Offset(5, 0f) != TransitPlan.Offset(12, 0f),
              "bus timetable: a line calls at each of its stops the minutes of the ride later; lines have their own offsets");

        // The calls at one stop kept apart.
        var times = new double[] { 100.0, 104.0, 99.0, 150.0, double.MaxValue };
        var who = new[] { 0, 1, 2, 3, 4 };
        TransitPlan.Space(times, who, 5, 22.0, 90.0);
        bool apart = true;
        for (int i = 1; i < 4; i++)
        {
            apart &= times[i] - times[i - 1] >= 22.0 - 1e-9;
        }
        Check(apart && times[0] >= 112.0 - 1e-9 && who[0] == 2 && times[4] == double.MaxValue, "bus timetable: calls at one stop are in order, kept apart, and after the bus that last stood there");
        Check(Math.Abs(times[3] - 178.0) < 1e-9 || times[3] >= 150.0, "bus timetable: a call already far enough on is not moved");
    }

    // ---------------------------------------------------------------- the estimate

    private static void BusEstimate()
    {
        var pace = new TransitPlan.Pace();
        float a = pace.Seconds(20f, 1, 0f, false, 0f), b = pace.Seconds(30f, 1, 0f, false, 0f), c = pace.Seconds(20f, 3, 0f, false, 0f);
        Check(b > a && c > a && pace.Seconds(20f, 1, 5f, false, 0f) > a + 4.9f, "bus estimate: more road, more lit junctions or a hold at its start all make it later");
        Check(pace.Seconds(20f, 0, 0f, true, 0f) >= pace.Seconds(20f, 0, 0f, false, 0f) + TransitPlan.RedExpect - 1e-3f && pace.Seconds(20f, 0, 0f, true, 40f) >= pace.Seconds(20f, 0, 0f, false, 0f) + 2f,
              "bus estimate: at a red light what is still expected of it is added, a couple of seconds at the least however long it has stood");
        Check(TransitPlan.Slack(10f) >= TransitPlan.SlackLeast && TransitPlan.Slack(200f) > TransitPlan.Slack(20f), "bus estimate: the slack is a few seconds, more for a long wait");

        // Where a bus starts, so that it is there when the timetable says: the estimate of that start is the seconds left.
        bool fits = true;
        for (float seconds = 15f; seconds <= 45f; seconds += 5f)
        {
            float back = TransitPlan.StartBack(seconds, 11f, pace, 1f / 150f, 60f, 330f);
            float drive = back / 11f + 4f;   // (the open road at cruise, and gathering speed and pulling in)
            float estimate = pace.Seconds(drive / pace.Traffic * pace.Traffic, 0, 0f, false, 0f) + back / 150f * pace.Light;
            fits &= back <= 60f || back >= 330f || Math.Abs(estimate - seconds) < 4f;
        }
        Check(fits, "bus estimate: a bus is started as far back as the seconds to its call take to drive");
        Check(TransitPlan.LeadSeconds(11f, pace, 1f / 150f, 330f) >= 330f / 11f * pace.Traffic, "bus estimate: a bus is put on the road no later than its longest start takes to drive");

        // Learning: a city whose traffic makes drives a third longer, with lights of 10 s on average, is learnt within bounds.
        var learn = new TransitPlan.Pace();
        for (int i = 0; i < 30; i++)
        {
            learn.Learn(30f, 2, 30f * 1.33f, 20f);
        }
        Check(Math.Abs(learn.Traffic - 1.33f) < 0.02f && Math.Abs(learn.Light - 10f) < 0.3f, "bus estimate: thirty buses teach it the city's traffic (x" + learn.Traffic.ToString("F2") + ") and its lights (" + learn.Light.ToString("F1") + " s)");
        float was = learn.Traffic;
        learn.Learn(30f, 2, 300f, 20f);
        Check(learn.Traffic <= was + (TransitPlan.TrafficMost - was) * TransitPlan.LearnShare + 1e-3f && learn.Traffic <= TransitPlan.TrafficMost, "bus estimate: one bus stuck for minutes moves it a share, within bounds");
        learn.Learn(2f, 0, 50f, 0f);
        Check(learn.Samples == 31, "bus estimate: a bus that set off at the stop teaches nothing");

        // A day of buses: drives a little longer than reckoned, red lights at random. Promised against arrived, once learnt.
        var rng = new Random(11);
        var day = new TransitPlan.Pace();
        int late = 0, falseDelays = 0, count = 0;
        double offSum = 0.0;
        for (int bus = 0; bus < 120; bus++)
        {
            float drive = 18f + (float)rng.NextDouble() * 22f;
            int lights = rng.Next(0, 3);
            float promise = day.Seconds(drive, lights, 0f, false, 0f);
            float atLights = 0f;
            for (int l = 0; l < lights; l++)
            {
                atLights += rng.NextDouble() < 0.5 ? 6f + (float)rng.NextDouble() * 12f : 0f;
            }
            float moving = drive * (1.2f + (float)(rng.NextDouble() - 0.5) * 0.12f);
            float actual = moving + atLights;
            if (bus >= 40)
            {
                count++;
                offSum += Math.Abs(actual - promise);
                bool lateBus = actual > promise + TransitPlan.Slack(promise);
                late += lateBus ? 1 : 0;
                // The panel, ticked as the bus comes: DELAYED only if it really is late by more than the slack.
                var eta = new TransitEta();
                eta.Timetable(promise);
                eta.Assign(promise);
                bool said = false;
                for (float t = 0f; t < actual; t += 0.25f)
                {
                    eta.Track(0.25f, actual - t, false);   // (the estimate as good as the truth: what the panel then says is the rule's alone)
                    said |= eta.State == TransitEta.Mode.Delayed;
                }
                falseDelays += said && !lateBus ? 1 : 0;
            }
            day.Learn(drive, lights, moving, atLights);
        }
        double meanOff = offSum / Math.Max(1, count);
        Console.WriteLine("  Bus estimate over a day of 80 buses, once learnt: " + meanOff.ToString("F1") + " s off on average, " + late + " later than the slack, " + falseDelays + " said DELAYED on time; traffic x"
                          + day.Traffic.ToString("F2") + ", " + day.Light.ToString("F1") + " s a lit junction");
        Check(meanOff < 6.0, "bus estimate: once learnt, buses come within a few seconds of the promise on average");
        Check(late <= count / 6, "bus estimate: few buses are later than the slack in ordinary traffic (" + late + " of " + count + ")");
        Check(falseDelays == 0, "bus estimate: no bus that is on time is said to be DELAYED");
    }

    // ---------------------------------------------------------------- the panel's figure

    private static void BusPanelFigure()
    {
        // On time: the figure counts down and is never DELAYED.
        var eta = new TransitEta();
        eta.Timetable(40f);
        eta.Wait(10f, false);
        Check(Near(eta.Seconds, 30f, 1e-3f) && eta.State == TransitEta.Mode.Timetable, "bus panel: before the bus sets out the timetable's figure counts down");
        eta.Assign(30f);
        bool delayed = false;
        for (float t = 0f; t < 30f; t += 0.25f)
        {
            eta.Track(0.25f, 30f - t, t > 10f && t < 18f);   // (a red light on the way, allowed for in the estimate)
            delayed |= eta.State == TransitEta.Mode.Delayed;
        }
        Check(!delayed && eta.Seconds < 1f, "bus panel: a bus that keeps its time is never DELAYED, the light it waited at included");

        // Held in traffic: late by more than the slack, held, for some seconds: DELAYED with its own figure.
        eta = new TransitEta();
        eta.Timetable(30f);
        eta.Assign(30f);
        float estimate = 30f;
        for (float t = 0f; t < 30f; t += 0.25f)
        {
            estimate = t < 5f ? 30f - t : 25f;   // standing from 5 s on
            eta.Track(0.25f, estimate, t >= 5f);
            if (eta.State == TransitEta.Mode.Delayed)
            {
                break;
            }
        }
        Check(eta.State == TransitEta.Mode.Delayed && eta.Known && Near(eta.Seconds, estimate, 1e-3f), "bus panel: a bus held up for long is DELAYED, with its new figure");
        Check(eta.Figure, "bus panel: a delayed bus on the road shows its figure");

        // Slow but moving within the slack: not DELAYED.
        eta = new TransitEta();
        eta.Timetable(30f);
        eta.Assign(30f);
        delayed = false;
        for (float t = 0f; t < 34f; t += 0.25f)
        {
            eta.Track(0.25f, Math.Max(0f, 34f - t), false);
            delayed |= eta.State == TransitEta.Mode.Delayed;
        }
        Check(!delayed, "bus panel: four seconds behind in ordinary traffic is not DELAYED");

        // No bus could set out: DELAYED after it was due, with no figure; then the bus's own figure.
        eta = new TransitEta();
        eta.Timetable(2f);
        for (int i = 0; i < 60; i++)
        {
            eta.Wait(0.25f, true);
        }
        Check(eta.State == TransitEta.Mode.Delayed && !eta.Known && !eta.Figure, "bus panel: when no bus can set out, DELAYED some seconds after it was due, with no figure");
        eta.Assign(28f);
        Check(eta.State == TransitEta.Mode.Delayed && eta.Known && Near(eta.Seconds, 28f, 1e-3f), "bus panel: the bus that then sets out gives its figure, still DELAYED");

        // Set out too late to keep the timetable: DELAYED at once; a little early: the figure follows it down.
        eta = new TransitEta();
        eta.Timetable(10f);
        eta.Assign(40f);
        Check(eta.State == TransitEta.Mode.Delayed && Near(eta.Seconds, 40f, 1e-3f), "bus panel: a bus that could only set out too late is DELAYED with its own figure");
        eta = new TransitEta();
        eta.Timetable(60f);
        eta.Assign(30f);
        Check(eta.State == TransitEta.Mode.EnRoute && Near(eta.Seconds, 30f, 1e-3f), "bus panel: a bus that will come sooner than the timetable: the figure follows it");
        eta = new TransitEta();
        eta.NoService();
        Check(!eta.Figure && eta.State == TransitEta.Mode.None, "bus panel: a line not running now shows no figure");
        var copy = new TransitEta();
        int epoch = copy.Epoch;
        copy.CopyFrom(eta);
        Check(copy.State == eta.State && copy.Epoch > epoch, "bus panel: the bus behind, made the front one, keeps what it was showing");
    }

    // ---------------------------------------------------------------- the layout

    private static void BusDoublesBack()
    {
        var x = new List<float>();
        var z = new List<float>();
        for (int i = 0; i <= 40; i++) { x.Add(0f); z.Add(i * 20f); }
        Check(!TransitPlan.DoublesBack(x, z, x.Count) && TransitPlan.Unbelievable(x, z, x.Count, 800f) == null, "bus layout: a straight line does not double back");
        for (int i = 1; i <= 30; i++) { x.Add(i * 20f); z.Add(800f); }
        Check(!TransitPlan.DoublesBack(x, z, x.Count), "bus layout: a line that turns the corner does not double back");
        var ux = new List<float>();
        var uz = new List<float>();
        for (int i = 0; i <= 20; i++) { ux.Add(0f); uz.Add(i * 20f); }
        for (int i = 20; i >= 0; i--) { ux.Add(6f); uz.Add(i * 20f + 2f); }
        for (int i = 0; i < 10; i++) { ux.Add(6f + i * 20f); uz.Add(-10f); }
        Check(TransitPlan.DoublesBack(ux, uz, ux.Count), "bus layout: a line that comes back along the street it drove up doubles back");
        var bx = new List<float>();
        var bz = new List<float>();
        for (int i = 0; i <= 30; i++) { bx.Add(0f); bz.Add(i * 20f); }        // up
        for (int i = 1; i <= 15; i++) { bx.Add(i * 20f); bz.Add(600f); }     // across
        for (int i = 1; i <= 15; i++) { bx.Add(300f); bz.Add(600f - i * 20f); }   // back down, away from the end...
        for (int i = 1; i <= 40; i++) { bx.Add(300f + i * 20f); bz.Add(300f); }
        bx.Add(1100f); bz.Add(1400f);
        Check(TransitPlan.DoublesBack(bx, bz, bx.Count), "bus layout: a line that heads well away from its end before it gets there doubles back");
        Check(TransitPlan.Unbelievable(x, z, x.Count, 300f) != null, "bus layout: a line far longer than the straight line between its ends will not do");
    }

    // A made-up Manhattan: avenues 280 m apart running the long way, streets 80 m apart across, every road both ways, lane
    // points every 20 m, and subway stations here and there. The routes are laid as TransitMap lays them (TransitPlan.Pairing,
    // RouteSearch, the corridor, TransitPlan.Unbelievable) and the stops along them (TransitPlan.HostAlong).
    private sealed class Grid
    {
        public readonly List<float> X = new List<float>();
        public readonly List<float> Z = new List<float>();
        public readonly List<List<int>> Next = new List<List<int>>();
        public readonly List<bool> Plain = new List<bool>();
        public RouteSearch Search;

        public const float Avenue = 280f, Street = 80f, Step = 20f, Clear = 26f;
        public const int Avenues = 6, Streets = 40;

        private readonly Dictionary<long, int> _firstOut = new Dictionary<long, int>();
        private readonly Dictionary<long, int> _lastIn = new Dictionary<long, int>();
        private readonly List<long> _segments = new List<long>();

        private static long Node(int a, int s) => a * 1000L + s;
        private static long Seg(long from, long to) => from * 1000000L + to;

        private void Segment(int a1, int s1, int a2, int s2)
        {
            float x1 = a1 * Avenue, z1 = s1 * Street, x2 = a2 * Avenue, z2 = s2 * Street;
            float dx = x2 - x1, dz = z2 - z1;
            float length = (float)Math.Sqrt(dx * dx + dz * dz);
            float ux = dx / length, uz = dz / length;
            float rx = uz * 3f, rz = -ux * 3f;   // the lane to the right of the way it runs
            int previous = -1;
            for (float t = 6f; t <= length - 6f + 0.01f; t += Step)
            {
                int id = X.Count;
                X.Add(x1 + ux * t + rx);
                Z.Add(z1 + uz * t + rz);
                Next.Add(new List<int>(3));
                Plain.Add(t >= Clear && t <= length - Clear);
                if (previous >= 0)
                {
                    Next[previous].Add(id);
                }
                else
                {
                    _firstOut[Seg(Node(a1, s1), Node(a2, s2))] = id;
                }
                previous = id;
            }
            _lastIn[Seg(Node(a1, s1), Node(a2, s2))] = previous;
            _segments.Add(Seg(Node(a1, s1), Node(a2, s2)));
        }

        public Grid()
        {
            for (int a = 0; a < Avenues; a++)
            {
                for (int s = 0; s < Streets; s++)
                {
                    if (s + 1 < Streets) { Segment(a, s, a, s + 1); Segment(a, s + 1, a, s); }
                    if (a + 1 < Avenues) { Segment(a, s, a + 1, s); Segment(a + 1, s, a, s); }
                }
            }
            // At each junction: from the end of every road in to the start of every road out, but the way back.
            foreach (long seg in _segments)
            {
                long from = seg / 1000000L, to = seg % 1000000L;
                int last = _lastIn[seg];
                foreach (long other in _segments)
                {
                    if (other / 1000000L == to && other % 1000000L != from)
                    {
                        Next[last].Add(_firstOut[other]);
                    }
                }
            }
            Search = new RouteSearch(X.Count);
            var start = new int[X.Count + 1];
            var edgeTo = new List<int>();
            for (int i = 0; i < X.Count; i++)
            {
                Search.X[i] = X[i];
                Search.Z[i] = Z[i];
                start[i] = edgeTo.Count;
                edgeTo.AddRange(Next[i]);
            }
            start[X.Count] = edgeTo.Count;
            Search.SetEdges(start, edgeTo.ToArray(), new float[edgeTo.Count]);
        }

        public float Distance(int a, int b) => TransitPlan.Distance(X[a], Z[a], X[b], Z[b]);
    }

    private sealed class Line
    {
        public int[] Path;
        public float[] Metres;
        public string Number;
        public readonly List<int> Stops = new List<int>();   // path indices
        public readonly List<int> Places = new List<int>();  // the place of each stop
    }

    private static void BusLayout()
    {
        var grid = new Grid();
        // Stations: three across the island, six up it, a little off the grid.
        var sx = new List<float>();
        var sz = new List<float>();
        var rng = new Random(5);
        for (int row = 0; row < 6; row++)
        {
            for (int col = 0; col < 3; col++)
            {
                sx.Add(160f + col * 560f + (float)(rng.NextDouble() - 0.5) * 120f);
                sz.Add(180f + row * 560f + (float)(rng.NextDouble() - 0.5) * 120f);
            }
        }
        int stations = sx.Count;
        var terminals = new List<List<int>>();
        var ends = new bool[stations];
        for (int s = 0; s < stations; s++)
        {
            var near = new List<int>();
            for (int i = 0; i < grid.X.Count; i++)
            {
                if (TransitPlan.Distance(grid.X[i], grid.Z[i], sx[s], sz[s]) <= 150f && grid.Plain[i])
                {
                    near.Add(i);
                }
            }
            terminals.Add(near);
            ends[s] = near.Count > 0;
        }
        var pairs = new TransitPlan.Pairing(sx.ToArray(), sz.ToArray(), ends);
        var lines = new List<Line>();
        var path = new List<int>();
        int routes = 0, searches = 0, refused = 0;
        var xs = new List<float>();
        var zs = new List<float>();
        while (routes < 16 && searches < 100 && pairs.Next(out int a, out int b))
        {
            Line there = null, back = null;
            for (int way = 0; way < 2; way++)
            {
                int from = way == 0 ? a : b, to = way == 0 ? b : a;
                searches++;
                bool found = grid.Search.Find(terminals[from], terminals[to], sx[to], 0f, sz[to], 156f, 400000, path) && path.Count > 8;
                string why = null;
                if (found)
                {
                    xs.Clear();
                    zs.Clear();
                    foreach (int p in path) { xs.Add(grid.X[p]); zs.Add(grid.Z[p]); }
                    why = TransitPlan.Unbelievable(xs, zs, xs.Count, pairs.Span(from, to));
                }
                if (!found || why != null)
                {
                    break;
                }
                var line = new Line { Path = path.ToArray(), Metres = new float[path.Count], Number = "M" + (routes + 1) };
                for (int i = 1; i < line.Path.Length; i++)
                {
                    line.Metres[i] = line.Metres[i - 1] + grid.Distance(line.Path[i - 1], line.Path[i]);
                }
                if (way == 0) there = line; else back = line;
            }
            pairs.Done(a, b);
            if (there == null || back == null)
            {
                refused++;
                continue;
            }
            routes++;
            lines.Add(there);
            lines.Add(back);
            pairs.Ends(a, b);
            foreach (Line line in new[] { there, back })
            {
                // The corridor: the next routes gather on the streets this one runs along.
                for (int i = 0; i + 1 < line.Path.Length; i++)
                {
                    int p = line.Path[i], q = line.Path[i + 1];
                    for (int e = grid.Search.EdgeStart[p]; e < grid.Search.EdgeStart[p + 1]; e++)
                    {
                        if (grid.Search.EdgeTo[e] == q && grid.Search.EdgeExtra[e] == 0f)
                        {
                            grid.Search.EdgeExtra[e] = -TransitPlan.CorridorShare * grid.Distance(p, q);
                        }
                    }
                }
            }
            for (int s = 0; s < stations; s++)
            {
                bool passes = false;
                foreach (Line line in new[] { there, back })
                {
                    for (int i = 0; i < line.Path.Length && !passes; i += 2)
                    {
                        passes = TransitPlan.Distance(grid.X[line.Path[i]], grid.Z[line.Path[i]], sx[s], sz[s]) < TransitPlan.ServedWithin;
                    }
                }
                if (passes)
                {
                    pairs.Serve(s);
                }
            }
        }

        // The stops: shared where another line already stops on the stretch looked along, else the first plain point.
        var hostAt = new Dictionary<int, int>();   // lane point: place
        var placeX = new List<float>();
        var placeZ = new List<float>();
        int shared = 0;
        foreach (Line line in lines)
        {
            float total = line.Metres[line.Metres.Length - 1];
            float wanted = 90f;
            while (wanted < total - 40f)
            {
                int host = TransitPlan.HostAlong(line.Path, line.Metres, Math.Max(wanted - 50f, 90f), wanted + 200f, wanted, 3, p => hostAt.TryGetValue(p, out int h) ? h : -1, out int at);
                if (host >= 0)
                {
                    line.Stops.Add(at);
                    line.Places.Add(host);
                    shared++;
                    wanted = line.Metres[at] + 260f;
                    continue;
                }
                int pick = -1;
                for (int i = 3; i < line.Path.Length - 3; i++)
                {
                    if (line.Metres[i] >= wanted && line.Metres[i] <= wanted + 140f && grid.Plain[line.Path[i]])
                    {
                        pick = i;
                        break;
                    }
                }
                if (pick < 0)
                {
                    wanted += 140f;
                    continue;
                }
                Place(line, pick);
                wanted = line.Metres[pick] + 260f;
            }
            // The last stop as near the station as the street allows (TransitMap.Terminus).
            float lastAt = line.Stops.Count > 0 ? line.Metres[line.Stops[line.Stops.Count - 1]] : -30f;
            if (total - lastAt > 120f)
            {
                int host = TransitPlan.HostAlong(line.Path, line.Metres, lastAt + 120f, total, total, 3, p => hostAt.TryGetValue(p, out int h) ? h : -1, out int at);
                if (host >= 0)
                {
                    line.Stops.Add(at);
                    line.Places.Add(host);
                    shared++;
                }
                else
                {
                    for (int j = line.Path.Length - 4; j >= 3 && line.Metres[j] >= lastAt + 120f; j--)
                    {
                        if (grid.Plain[line.Path[j]])
                        {
                            Place(line, j);
                            break;
                        }
                    }
                }
            }
        }
        void Place(Line line, int pick)
        {
            {
                int place = placeX.Count;
                placeX.Add(grid.X[line.Path[pick]]);
                placeZ.Add(grid.Z[line.Path[pick]]);
                hostAt[line.Path[pick]] = place;
                line.Stops.Add(pick);
                line.Places.Add(place);
            }
        }

        // What it comes to.
        var linesAt = new int[placeX.Count];
        foreach (Line line in lines)
        {
            foreach (int place in new HashSet<int>(line.Places))
            {
                linesAt[place]++;
            }
        }
        int two = 0, three = 0;
        foreach (int n in linesAt) { two += n >= 2 ? 1 : 0; three += n >= 3 ? 1 : 0; }
        int served3 = 0, served2 = 0;
        for (int s = 0; s < stations; s++)
        {
            var near = new HashSet<string>();
            foreach (Line line in lines)
            {
                foreach (int place in line.Places)
                {
                    if (TransitPlan.Distance(placeX[place], placeZ[place], sx[s], sz[s]) <= TransitPlan.ServedWithin + 30f)
                    {
                        near.Add(line.Number);
                    }
                }
            }
            served3 += near.Count >= 3 ? 1 : 0;
            served2 += near.Count >= 2 ? 1 : 0;
        }
        bool none = true;
        foreach (Line line in lines)
        {
            xs.Clear();
            zs.Clear();
            foreach (int p in line.Path) { xs.Add(grid.X[p]); zs.Add(grid.Z[p]); }
            none &= !TransitPlan.DoublesBack(xs, zs, xs.Count);
        }
        Console.WriteLine("  Bus layout over a made-up Manhattan (" + stations + " stations, " + grid.X.Count + " lane points): " + routes + " routes, " + lines.Count + " lines, " + searches + " searches, "
                          + refused + " pairs refused; " + placeX.Count + " places, " + shared + " stops shared; places with 2 lines or more " + two + ", with 3 or more " + three
                          + "; stations with 3 lines or more " + served3 + ", with 2 or more " + served2);
        Check(routes >= 10, "bus layout: a dozen or so routes are laid between the stations (" + routes + ")");
        Check(lines.Count == routes * 2, "bus layout: every route runs both ways");
        Check(none, "bus layout: no line doubles back");
        Check(served2 == stations && served3 * 10 >= stations * 8, "bus layout: every station has two lines or more, and four in five of them three or more (" + served3 + " of " + stations + ")");
        Check(two * 10 >= placeX.Count * 4, "bus layout: two in five places or more are served by two lines or more, the lines sharing the avenues' stops (" + two + " of " + placeX.Count + ")");

        // Journeys between every two places: one bus, or two with a change at a shared place or a short walk.
        var net = new TransitPlan.Net();
        for (int i = 0; i < placeX.Count; i++)
        {
            net.AddPlace(placeX[i], placeZ[i]);
        }
        foreach (Line line in lines)
        {
            var minutes = new float[line.Stops.Count];
            for (int s = 0; s < line.Stops.Count; s++)
            {
                minutes[s] = (line.Metres[line.Stops[s]] - line.Metres[line.Stops[0]]) * 0.06f + s;
            }
            net.AddLine(line.Places.ToArray(), minutes);
        }
        net.Link(160f);
        net.Interchanges(sx, sz, TransitPlan.InterchangeWithin);
        var best = new TransitPlan.Journey[net.Places];
        int reached = 0, pairsAll = 0, direct = 0, changes = 0;
        bool honest = true;
        Func<int, int, double, double> call = (line, order, at) => TransitPlan.NextCall(at, line < 8, TransitPlan.Offset(line * 7 + 3, net.Minutes[line][order]), false, 1f);
        double now = 3 * 1440.0 + 9 * 60.0;
        for (int from = 0; from < net.Places; from++)
        {
            TransitPlan.From(net, from, now, call, best);
            for (int to = 0; to < net.Places; to++)
            {
                if (to == from || net.Apart(from, to) < 150f)
                {
                    continue;
                }
                pairsAll++;
                if (!best[to].Found)
                {
                    continue;
                }
                reached++;
                direct += best[to].Direct ? 1 : 0;
                changes += best[to].Direct ? 0 : 1;
                honest &= best[to].Board >= now && best[to].Arrive > best[to].Board && (best[to].Direct || best[to].Board2 >= best[to].Board);
            }
        }
        // Between the stations: from the place nearest one to the place nearest another.
        var nearestPlace = new int[stations];
        for (int s = 0; s < stations; s++)
        {
            float bestApart = float.MaxValue;
            for (int i = 0; i < net.Places; i++)
            {
                float apart = TransitPlan.Distance(placeX[i], placeZ[i], sx[s], sz[s]);
                if (apart < bestApart)
                {
                    bestApart = apart;
                    nearestPlace[s] = i;
                }
            }
        }
        // (From any stop of a station's interchange to any of another's: where a rider coming up from the subway would go.)
        var atStation = new List<List<int>>();
        for (int s = 0; s < stations; s++)
        {
            var around = new List<int>();
            for (int i = 0; i < net.Places; i++)
            {
                if (TransitPlan.Distance(placeX[i], placeZ[i], sx[s], sz[s]) <= TransitPlan.InterchangeWithin)
                {
                    around.Add(i);
                }
            }
            if (around.Count == 0)
            {
                around.Add(nearestPlace[s]);
            }
            atStation.Add(around);
        }
        int stationPairs = 0, stationReached = 0;
        var fromStation = new bool[stations];
        for (int s = 0; s < stations; s++)
        {
            Array.Clear(fromStation, 0, stations);
            foreach (int origin in atStation[s])
            {
                TransitPlan.From(net, origin, now, call, best);
                for (int t = 0; t < stations; t++)
                {
                    foreach (int to in atStation[t])
                    {
                        fromStation[t] |= best[to].Found;
                    }
                }
            }
            for (int t = 0; t < stations; t++)
            {
                if (t != s && TransitPlan.Distance(sx[s], sz[s], sx[t], sz[t]) > 2f * TransitPlan.InterchangeWithin)
                {
                    stationPairs++;
                    stationReached += fromStation[t] ? 1 : 0;
                }
            }
        }
        // The lines joined through their transfer points: a place two lines share, or two places a short walk apart.
        var joined = new bool[net.Lines];
        var queue = new Queue<int>();
        joined[0] = true;
        queue.Enqueue(0);
        while (queue.Count > 0)
        {
            int line = queue.Dequeue();
            foreach (int place in net.Calls[line])
            {
                var around = new List<int>(net.Near[place]) { place };
                foreach (int p in around)
                {
                    foreach (int other in net.LinesAt[p])
                    {
                        if (!joined[other])
                        {
                            joined[other] = true;
                            queue.Enqueue(other);
                        }
                    }
                }
            }
        }
        int joinedLines = 0;
        foreach (bool j in joined) { joinedLines += j ? 1 : 0; }
        Console.WriteLine("  Bus journeys over it: " + reached + " of " + pairsAll + " pairs of places reached on one bus or with one change (" + direct + " direct, " + changes + " with a change); between the stations "
                          + stationReached + " of " + stationPairs + "; " + joinedLines + " of " + net.Lines + " lines joined through transfer points");
        Check(joinedLines == net.Lines, "bus layout: every line meets the others at transfer points: any stop can be reached from any other, changing where lines meet");
        Check(stationReached * 4 >= stationPairs * 3, "bus layout: three in four journeys between two stations need one bus or one change at the most (" + stationReached + " of " + stationPairs + ")");
        Check(changes > 0 && direct > 0, "bus layout: some journeys are on one bus, others change at a transfer point");
        Check(honest, "bus layout: every journey boards when its line calls, arrives after it boards, and changes after the first bus");
    }

    // ---------------------------------------------------------------- a Going Public session

    private static void BusSession()
    {
        Check(RideRules.RealTime(false) && !RideRules.RealTime(true), "bus session: a ride is driven in real time alone and on the host's machine; never where the street is the host's (a guest's)");
        Check(RideRules.FastForwardOffered(true, false) && !RideRules.FastForwardOffered(true, true) && !RideRules.FastForwardOffered(false, false),
              "bus session: the panel's fast forward alone only, never in a session (the game's speed is everyone's)");
        Check(RideRules.SkipOffered(true) && !RideRules.SkipOffered(false), "bus session: the panel's skip while the ride runs, in a session too");
        Check(RideRules.SkipMinutes(25f, true) == 0f && RideRules.SkipMinutes(25f, false) == 25f, "bus session: the skip passes no time in a session, the rest of the ride alone");
        Check(RideRules.GetOffAt(2, 3, false, false, 6) == 3, "bus session: getting off early is as alone");
    }

    // ---------------------------------------------------------------- the journey planner

    private static void BusPlanner()
    {
        // Three places in a row on the A line, the B line crossing at the middle one, the C line from a place a short walk away.
        var net = new TransitPlan.Net();
        int p0 = net.AddPlace(0f, 0f), p1 = net.AddPlace(0f, 500f), p2 = net.AddPlace(0f, 1000f);
        int p3 = net.AddPlace(500f, 500f), p4 = net.AddPlace(80f, 520f), p5 = net.AddPlace(80f, 1500f);
        int lineA = net.AddLine(new[] { p0, p1, p2 }, new[] { 0f, 30f, 60f });
        int lineB = net.AddLine(new[] { p1, p3 }, new[] { 0f, 30f });
        int lineC = net.AddLine(new[] { p4, p5 }, new[] { 0f, 50f });
        net.Link(160f);
        Check(net.Near[p1].Contains(p4) && !net.Near[p1].Contains(p3), "bus planner: two places a short walk apart are joined for a change on foot; further ones are not");
        Func<int, int, double, double> call = (line, order, at) => Math.Ceiling(at / 10.0) * 10.0;   // every line every ten minutes, on the ten
        var best = new TransitPlan.Journey[net.Places];
        TransitPlan.From(net, p0, 1000.0, call, best);
        Check(best[p2].Found && best[p2].Direct && best[p2].Line == lineA && Math.Abs(best[p2].Arrive - 1060.0) < 1e-6, "bus planner: a place further along the line is one bus, arriving by its timetable");
        Check(best[p3].Found && !best[p3].Direct && best[p3].Line == lineA && best[p3].Line2 == lineB && best[p3].Board2 >= 1030.0 + TransitPlan.ChangeLeast,
              "bus planner: a place on a crossing line is reached with a change where the two lines meet, boarding the next bus of the second");
        Check(best[p5].Found && best[p5].Line2 == lineC && best[p5].Board2 >= 1030.0 + net.Apart(p1, p4) / TransitPlan.WalkMetresPerMinute,
              "bus planner: a change may be made at a place a short walk away, after the walk");
        Check(!best[p0].Found, "bus planner: the place boarded at is not a journey");
        TransitPlan.From(net, p3, 1000.0, call, best);
        Check(!best[p0].Found, "bus planner: a place no journey of one change reaches is not planned (it passes under the fade, as before)");
    }
}
