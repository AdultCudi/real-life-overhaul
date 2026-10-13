using System;
using System.Collections.Generic;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The rules of the bus network that are only arithmetic, with nothing of the game or the engine in them, so that the
    /// build's EconomyCheck runs them on made-up cities and days: the timetable (how often each line runs, hour by hour),
    /// the estimate of a bus's arrival and how it learns from the buses that have come, which station pairs the lines
    /// are laid between, whether a line's path will do, where a line shares a stop already laid by another, and the
    /// journey planner (one bus, or two with a change at a transfer point).
    ///
    /// GAME MINUTES. The game's clock runs at about one minute a second while the player walks the street, and a bus
    /// near the player drives in real time: a bus 150 m off is some 25 game minutes away. The timetable is kept in game
    /// minutes on the game's own clock (so that it is one timetable, the same whichever stop the player stands at, and
    /// a journey's change can be planned on it); the director turns it into real seconds at the rate the clock is
    /// measured to run.
    /// </summary>
    public static class TransitPlan
    {
        // ---------------------------------------------------------------- the timetable

        public const int NightFrom = 1;            // from 01:00 only the main lines run (the night service)...
        public const int NightUntil = 5;           // ...until 05:00
        public const float SpacingSeconds = 22f;   // real seconds between two buses at one stop, whatever their lines: one in, stands, pulls out
        public const float RainSlower = 1.15f;     // in the rain every line runs this much less often
        public const int MainRoutes = 4;           // the longest routes are the main lines: they run more often and through the night

        // Game minutes between two buses of one line at one stop, hour by hour on a weekday; 0 for none. The rush hours
        // (07:00 to 09:00 and 16:00 to 19:00 on a weekday) are the busiest; at the weekend they run as the rest of the day.
        private static readonly float[] Main =
        {
            //  0    1    2    3    4    5    6    7    8    9   10   11   12   13   14   15   16   17   18   19   20   21   22   23
            70f, 90f, 90f, 90f, 90f, 60f, 50f, 36f, 36f, 45f, 45f, 45f, 45f, 45f, 45f, 45f, 36f, 36f, 36f, 45f, 55f, 55f, 65f, 65f
        };

        private static readonly float[] Local =
        {
            80f, 0f, 0f, 0f, 0f, 75f, 60f, 48f, 48f, 60f, 60f, 60f, 60f, 60f, 60f, 60f, 48f, 48f, 48f, 60f, 70f, 70f, 80f, 80f
        };

        private const float MainDay = 45f;          // a main line at the weekend's rush hours
        private const float LocalDay = 60f;

        /// <summary>True for the game's weekend (Friday to Sunday: TimeHelper.IsWeekend) of a day number (day 1 is a Monday).</summary>
        public static bool Weekend(int day)
        {
            int weekday = ((day - 1) % 7 + 7) % 7;
            return weekday >= 4;
        }

        public static bool IsRush(int hour, bool weekend)
        {
            return !weekend && ((hour >= 7 && hour < 9) || (hour >= 16 && hour < 19));
        }

        /// <summary>
        /// Game minutes between two buses of a line at a stop in an hour; 0 when the line does not run then. Rain makes it
        /// less often; the city's transit policy (CivicPolicy.ServiceScale, 1 unless a council changed it) more or less often.
        /// </summary>
        public static float Headway(bool main, int hour, bool weekend, bool raining, float serviceScale)
        {
            hour = ((hour % 24) + 24) % 24;
            float minutes = (main ? Main : Local)[hour];
            if (minutes <= 0f)
            {
                return 0f;
            }
            if (weekend && ((hour >= 7 && hour < 9) || (hour >= 16 && hour < 19)))
            {
                minutes = main ? MainDay : LocalDay;
            }
            if (raining)
            {
                minutes *= RainSlower;
            }
            return serviceScale > 0.05f ? minutes / serviceScale : minutes;
        }

        /// <summary>
        /// The next time (game minutes, the game's total: day and time of day) at or after <paramref name="now"/> that a line
        /// calls at a stop, on its timetable: every headway from the line's own offset, which is where along the line the
        /// stop is (the same bus calls at each of its stops a little later than at the one before). A line that does not
        /// run in an hour is looked for in the next, up to a day ahead; double.MaxValue if it never runs.
        /// </summary>
        public static double NextCall(double now, bool main, double offset, bool raining, float serviceScale)
        {
            double hourStart = Math.Floor(now / 60.0) * 60.0;
            for (int step = 0; step < 26; step++)
            {
                double from = Math.Max(now, hourStart);
                int totalHour = (int)Math.Floor(hourStart / 60.0);
                int day = (int)Math.Floor(hourStart / 1440.0);
                int hour = totalHour - day * 24;
                float headway = Headway(main, hour, Weekend(day), raining, serviceScale);
                if (headway > 0f)
                {
                    double k = Math.Ceiling((from - offset) / headway - 1e-9);
                    double slot = offset + k * headway;
                    if (slot < hourStart + 60.0)
                    {
                        return slot;
                    }
                }
                hourStart += 60.0;
            }
            return double.MaxValue;
        }

        /// <summary>A line's offset in its timetable at one of its stops: its own share of a headway, and the minutes from its first stop.</summary>
        public static double Offset(int lineSeed, float minutesAlong)
        {
            uint h = (uint)lineSeed * 2654435761u;
            return (h % 997u) / 997.0 * 90.0 + minutesAlong;
        }

        /// <summary>
        /// The calls at one stop, soonest first, pushed apart so that no two are nearer than <paramref name="spacing"/>
        /// (game minutes) and none nearer than that to <paramref name="last"/> (the bus that last stood there). Sorts
        /// <paramref name="times"/> and <paramref name="who"/> together; what is pushed is what is shown, so it is kept.
        /// </summary>
        public static void Space(double[] times, int[] who, int count, double spacing, double last)
        {
            for (int i = 1; i < count; i++)
            {
                double t = times[i];
                int w = who[i];
                int j = i - 1;
                while (j >= 0 && times[j] > t)
                {
                    times[j + 1] = times[j];
                    who[j + 1] = who[j];
                    j--;
                }
                times[j + 1] = t;
                who[j + 1] = w;
            }
            double previous = last;
            for (int i = 0; i < count; i++)
            {
                if (times[i] < double.MaxValue && times[i] < previous + spacing)
                {
                    times[i] = previous + spacing;
                }
                if (times[i] < double.MaxValue)
                {
                    previous = times[i];
                }
            }
        }

        // ---------------------------------------------------------------- the estimate

        public const float TrafficStart = 1.12f;   // the drive takes this much longer than the bus's own reckoning of an open road, at first
        public const float TrafficLeast = 0.95f;
        public const float TrafficMost = 1.9f;
        public const float LightStart = 6f;        // seconds each lit junction ahead adds on average (half of them red, some 12 s at those)
        public const float LightMost = 30f;
        public const float RedExpect = 14f;        // seconds a bus that has pulled up at a red light is expected to stand there
        public const float LearnShare = 0.25f;     // of each bus that comes, what is learnt
        public const float SlackLeast = 6f;        // real seconds a bus may be behind its figure before it is late
        public const float SlackShare = 0.12f;     // ...or this share of the figure, whichever is more

        /// <summary>
        /// How the buses really drive, learnt from those that have come this session: how much longer than the open road's
        /// reckoning the drive takes (traffic, the queue at a junction, a car pulling out), and what a lit junction costs.
        /// </summary>
        public sealed class Pace
        {
            public float Traffic = TrafficStart;
            public float Light = LightStart;
            public int Samples;

            /// <summary>
            /// Seconds to the stop: the open road's reckoning (<paramref name="drive"/>, as the bus will drive its path) at the
            /// pace traffic makes, each lit junction still ahead, any hold left at its start, and at a red light what is
            /// still to be expected of it.
            /// </summary>
            public float Seconds(float drive, int lightsAhead, float holdLeft, bool atRed, float redFor)
            {
                float seconds = Math.Max(0f, drive) * Traffic + Math.Max(0, lightsAhead) * Light + Math.Max(0f, holdLeft);
                if (atRed)
                {
                    seconds += Math.Max(2f, RedExpect - redFor);
                }
                return seconds;
            }

            /// <summary>
            /// A bus has come: it was reckoned <paramref name="drive"/> s of open road and <paramref name="lights"/> lit junctions
            /// when it set off; it took <paramref name="moving"/> s apart from <paramref name="atLights"/> s standing at red
            /// lights. What it says is learnt a share at a time, within bounds, so that one odd bus does not upset the next.
            /// </summary>
            public void Learn(float drive, int lights, float moving, float atLights)
            {
                if (drive < 5f || moving <= 0f)
                {
                    return;
                }
                float traffic = Clamp(moving / drive, TrafficLeast, TrafficMost);
                Traffic = Clamp(Traffic + (traffic - Traffic) * LearnShare, TrafficLeast, TrafficMost);
                if (lights > 0)
                {
                    float light = Clamp(atLights / lights, 0f, LightMost);
                    Light = Clamp(Light + (light - Light) * LearnShare, 0f, LightMost);
                }
                Samples++;
            }
        }

        /// <summary>Real seconds a bus may be behind the figure shown before it counts as late.</summary>
        public static float Slack(float seconds)
        {
            return Math.Max(SlackLeast, SlackShare * Math.Max(0f, seconds));
        }

        /// <summary>
        /// Where along its line a bus should start so that, set off at once, it reaches the stop in the seconds left
        /// (metres of line before the stop): the pace learnt, the lights on the way, kept within what a start may be.
        /// </summary>
        public static float StartBack(float secondsLeft, float cruise, Pace pace, float lightsPerMetre, float least, float most)
        {
            float perMetre = pace.Traffic / Math.Max(1f, cruise) + lightsPerMetre * pace.Light;
            float metres = (secondsLeft - 4f) / Math.Max(0.02f, perMetre);   // (some seconds of it gathering speed and pulling in)
            return Clamp(metres, least, most);
        }

        /// <summary>
        /// Seconds before its call at which a bus is put on the road: what a start <paramref name="mostBack"/> metres back takes
        /// to drive, and a little more (it is held at its start for what is over). Earlier would only hold it longer.
        /// </summary>
        public static float LeadSeconds(float cruise, Pace pace, float lightsPerMetre, float mostBack)
        {
            float perMetre = pace.Traffic / Math.Max(1f, cruise) + lightsPerMetre * pace.Light;
            return 4f + mostBack * perMetre + 3f;
        }

        // ---------------------------------------------------------------- the layout: which stations a line joins

        public const float SpanLeast = 600f;       // metres between the two ends of a route, as the crow flies
        public const float SpanMost = 3200f;
        public const float SpanBest = 1900f;       // ...the length a route is best laid at: cross-town, not round the corner
        public const int StationTarget = 3;        // lines that should serve each station: as ends, or passing within ServedWithin
        public const float ServedWithin = 220f;    // metres of a station a line passes to serve it
        public const float BearingApart = 28f;     // degrees two routes from one station must be apart: one up the avenue, one across town
        public const float StretchMost = 1.65f;    // a line's path may be this much longer than the straight line between its ends...
        public const float StretchExtra = 350f;    // ...and this many metres more
        public const float CorridorShare = 0.2f;   // a stretch of road a line already runs along counts this much less to the next line laid

        /// <summary>
        /// Which pair of stations the next route joins. Every station should have <see cref="StationTarget"/> lines; the pair
        /// asked for next is the one that serves the stations furthest short of that, at a believable length, in a
        /// direction neither end has a route in yet. A pair is never asked for twice, nor one known to have no road between.
        /// </summary>
        public sealed class Pairing
        {
            private readonly float[] _x;
            private readonly float[] _z;
            private readonly bool[] _ends;           // the station has lane points to start from
            private readonly int[] _served;
            private readonly List<float>[] _bearings;
            private readonly HashSet<long> _tried = new HashSet<long>();

            public Pairing(float[] x, float[] z, bool[] hasEnds)
            {
                _x = x;
                _z = z;
                _ends = hasEnds;
                _served = new int[x.Length];
                _bearings = new List<float>[x.Length];
                for (int i = 0; i < x.Length; i++)
                {
                    _bearings[i] = new List<float>(4);
                }
            }

            public int Count => _x.Length;

            /// <summary>Lines serving a station so far.</summary>
            public int Served(int station)
            {
                return _served[station];
            }

            private static long Key(int a, int b)
            {
                return a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
            }

            public bool Tried(int a, int b)
            {
                return _tried.Contains(Key(a, b));
            }

            /// <summary>No road joins the two (the search from one never got near the other): never asked for.</summary>
            public void NoRoad(int a, int b)
            {
                _tried.Add(Key(a, b));
            }

            /// <summary>The pair was tried: a route was laid between them, or none would do.</summary>
            public void Done(int a, int b)
            {
                _tried.Add(Key(a, b));
            }

            /// <summary>A route has been laid with its ends at these two stations.</summary>
            public void Ends(int a, int b)
            {
                float bearing = Bearing(a, b);
                _bearings[a].Add(bearing);
                _bearings[b].Add(bearing);
            }

            /// <summary>A route serves this station (it ends there or passes near).</summary>
            public void Serve(int station)
            {
                _served[station]++;
            }

            public float Span(int a, int b)
            {
                float dx = _x[a] - _x[b];
                float dz = _z[a] - _z[b];
                return (float)Math.Sqrt(dx * dx + dz * dz);
            }

            // The undirected way a route runs, 0 to 180 degrees.
            private float Bearing(int a, int b)
            {
                double degrees = Math.Atan2(_x[b] - _x[a], _z[b] - _z[a]) * 180.0 / Math.PI;
                if (degrees < 0)
                {
                    degrees += 180.0;
                }
                return (float)(degrees % 180.0);
            }

            private bool Crowded(int station, float bearing)
            {
                List<float> taken = _bearings[station];
                for (int i = 0; i < taken.Count; i++)
                {
                    float apart = Math.Abs(taken[i] - bearing) % 180f;
                    if (Math.Min(apart, 180f - apart) < BearingApart)
                    {
                        return true;
                    }
                }
                return false;
            }

            /// <summary>The pair to try next; false when no pair would bring any station nearer its target.</summary>
            public bool Next(out int a, out int b)
            {
                a = -1;
                b = -1;
                float best = float.MinValue;
                for (int i = 0; i < _x.Length; i++)
                {
                    if (!_ends[i])
                    {
                        continue;
                    }
                    for (int k = i + 1; k < _x.Length; k++)
                    {
                        if (!_ends[k] || Tried(i, k))
                        {
                            continue;
                        }
                        float span = Span(i, k);
                        if (span < SpanLeast || span > SpanMost)
                        {
                            continue;
                        }
                        int short1 = Math.Max(0, StationTarget - _served[i]);
                        int short2 = Math.Max(0, StationTarget - _served[k]);
                        if (short1 + short2 == 0)
                        {
                            continue;
                        }
                        float bearing = Bearing(i, k);
                        if (Crowded(i, bearing) || Crowded(k, bearing))
                        {
                            continue;
                        }
                        // The most short first (a station with none counts most), then the believable length.
                        float score = short1 * short1 + short2 * short2 - Math.Abs(span - SpanBest) / 1000f;
                        if (score > best)
                        {
                            best = score;
                            a = i;
                            b = k;
                        }
                    }
                }
                return a >= 0;
            }
        }

        /// <summary>
        /// Whether a line's path will do: no longer than <see cref="StretchMost"/> times the span and
        /// <see cref="StretchExtra"/> more, and never doubling back on itself. Null if it will, else why not.
        /// </summary>
        public static string Unbelievable(IList<float> x, IList<float> z, int count, float span)
        {
            if (count < 2)
            {
                return "it has no length";
            }
            float length = 0f;
            for (int i = 1; i < count; i++)
            {
                length += Distance(x[i - 1], z[i - 1], x[i], z[i]);
            }
            if (length > span * StretchMost + StretchExtra)
            {
                return "it is " + (length / 1000f).ToString("F1") + " km long for " + (span / 1000f).ToString("F1") + " km as the crow flies";
            }
            return DoublesBack(x, z, count) ? "it doubles back on itself" : null;
        }

        public const float BackNear = 12f;       // metres: a path that comes this near where it has been...
        public const float BackAfter = 120f;     // ...this many metres of path later, heading the other way, doubles back
        public const float BackAhead = 150f;     // ...and a path that ends up further back from its end than it was this far before, does too

        /// <summary>
        /// True when a path doubles back: it comes back along a street it has driven, the other way (within
        /// <see cref="BackNear"/> of a point it passed more than <see cref="BackAfter"/> metres earlier, heading against it),
        /// or it heads away from where it ends for a long way (more than <see cref="BackAhead"/> metres further from its end
        /// than it already was), as a line that goes round the block to come back does.
        /// </summary>
        public static bool DoublesBack(IList<float> x, IList<float> z, int count)
        {
            if (count < 3)
            {
                return false;
            }
            var along = new float[count];
            for (int i = 1; i < count; i++)
            {
                along[i] = along[i - 1] + Distance(x[i - 1], z[i - 1], x[i], z[i]);
            }
            float endX = x[count - 1];
            float endZ = z[count - 1];
            float nearest = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                float toEnd = Distance(x[i], z[i], endX, endZ);
                if (toEnd > nearest + BackAhead)
                {
                    return true;
                }
                nearest = Math.Min(nearest, toEnd);
            }
            // (A grid of cells BackNear across, so that the look is not every point against every other.)
            var cells = new Dictionary<long, List<int>>();
            for (int i = 0; i + 1 < count; i++)
            {
                long cell = Cell(x[i], z[i]);
                for (int dx = -1; dx <= 1; dx++)
                {
                    for (int dz = -1; dz <= 1; dz++)
                    {
                        if (!cells.TryGetValue(cell + dx * 100000L + dz, out List<int> there))
                        {
                            continue;
                        }
                        for (int k = 0; k < there.Count; k++)
                        {
                            int j = there[k];
                            if (along[i] - along[j] < BackAfter || Distance(x[i], z[i], x[j], z[j]) > BackNear)
                            {
                                continue;
                            }
                            float hx = x[i + 1] - x[i];
                            float hz = z[i + 1] - z[i];
                            float jx = x[j + 1] - x[j];
                            float jz = z[j + 1] - z[j];
                            float dot = hx * jx + hz * jz;
                            float lengths = (float)Math.Sqrt((hx * hx + hz * hz) * (jx * jx + jz * jz));
                            if (lengths > 0.01f && dot < -0.7f * lengths)
                            {
                                return true;
                            }
                        }
                    }
                }
                if (!cells.TryGetValue(cell, out List<int> own))
                {
                    own = new List<int>(4);
                    cells[cell] = own;
                }
                own.Add(i);
            }
            return false;
        }

        private static long Cell(float x, float z)
        {
            return (long)Math.Floor(x / BackNear + 50000) * 100000L + (long)Math.Floor(z / BackNear + 50000);
        }

        // ---------------------------------------------------------------- the layout: stops another line already has

        /// <summary>
        /// A stop already laid by another line that this line's path runs through, from <paramref name="least"/> to
        /// <paramref name="most"/> metres along it, the one nearest <paramref name="wanted"/>: it stops there too, so that
        /// the lines that share a street share its stops. <paramref name="hostAt"/> names the stop at a lane point, or -1.
        /// Returns the stop's id and, in <paramref name="pathIndex"/>, where on this path it is; -1 if there is none. A stop
        /// within <paramref name="ends"/> points of either end of the path is not taken (a bus must be able to come up to it).
        /// </summary>
        public static int HostAlong(int[] path, float[] metres, float least, float most, float wanted, int ends, Func<int, int> hostAt, out int pathIndex)
        {
            pathIndex = -1;
            int found = -1;
            float best = float.MaxValue;
            for (int i = Math.Max(ends, 0); i < path.Length - ends; i++)
            {
                if (metres[i] < least)
                {
                    continue;
                }
                if (metres[i] > most)
                {
                    break;
                }
                int host = hostAt(path[i]);
                if (host < 0)
                {
                    continue;
                }
                float off = Math.Abs(metres[i] - wanted);
                if (off < best)
                {
                    best = off;
                    found = host;
                    pathIndex = i;
                }
            }
            return found;
        }

        // ---------------------------------------------------------------- journeys

        /// <summary>
        /// The network as the journey planner reads it: the places a bus stops at (one place may be served by several lines)
        /// and, for each line, the places it calls at in order with the game minutes from its first stop to each. Places
        /// within a short walk of each other are joined: a change of bus may be made between them on foot.
        /// </summary>
        public sealed class Net
        {
            public readonly List<float> X = new List<float>();
            public readonly List<float> Z = new List<float>();
            public readonly List<List<int>> Near = new List<List<int>>();   // other places within the walk, each place's own
            public readonly List<int[]> Calls = new List<int[]>();          // per line: its places in order
            public readonly List<float[]> Minutes = new List<float[]>();    // per line: game minutes from its first stop
            public readonly List<List<int>> LinesAt = new List<List<int>>(); // per place: the lines that call there

            public int Places => X.Count;
            public int Lines => Calls.Count;

            public int AddPlace(float x, float z)
            {
                X.Add(x);
                Z.Add(z);
                Near.Add(new List<int>(4));
                LinesAt.Add(new List<int>(4));
                return X.Count - 1;
            }

            public int AddLine(int[] places, float[] minutes)
            {
                Calls.Add(places);
                Minutes.Add(minutes);
                int line = Calls.Count - 1;
                for (int i = 0; i < places.Length; i++)
                {
                    if (!LinesAt[places[i]].Contains(line))
                    {
                        LinesAt[places[i]].Add(line);
                    }
                }
                return line;
            }

            /// <summary>Joins every two places within a walk of each other.</summary>
            public void Link(float walk)
            {
                for (int i = 0; i < X.Count; i++)
                {
                    Near[i].Clear();
                }
                for (int i = 0; i < X.Count; i++)
                {
                    for (int k = i + 1; k < X.Count; k++)
                    {
                        if (Distance(X[i], Z[i], X[k], Z[k]) <= walk)
                        {
                            Near[i].Add(k);
                            Near[k].Add(i);
                        }
                    }
                }
            }

            public float Apart(int a, int b)
            {
                return Distance(X[a], Z[a], X[b], Z[b]);
            }

            /// <summary>Joins two places for a change on foot (once).</summary>
            public void Join(int a, int b)
            {
                if (a != b && !Near[a].Contains(b))
                {
                    Near[a].Add(b);
                    Near[b].Add(a);
                }
            }

            /// <summary>
            /// The interchange at each station: every two places within <paramref name="within"/> of the same station are
            /// joined, as the stops round a subway station are, whichever corners of it they stand at.
            /// </summary>
            public void Interchanges(IList<float> stationX, IList<float> stationZ, float within)
            {
                var near = new List<int>(8);
                for (int s = 0; s < stationX.Count; s++)
                {
                    near.Clear();
                    for (int i = 0; i < X.Count; i++)
                    {
                        if (Distance(X[i], Z[i], stationX[s], stationZ[s]) <= within)
                        {
                            near.Add(i);
                        }
                    }
                    for (int i = 0; i < near.Count; i++)
                    {
                        for (int k = i + 1; k < near.Count; k++)
                        {
                            Join(near[i], near[k]);
                        }
                    }
                }
            }
        }

        public const float InterchangeWithin = 230f;   // metres of a subway station within which its stops are one interchange

        /// <summary>One journey from a place: the bus to board and where to get off it, and where there is one, the change.</summary>
        public struct Journey
        {
            public bool Found;
            public int Line;          // the first bus
            public int From;          // its order at the place boarded at
            public int Off;           // ...and where they get off it
            public int Line2;         // the second bus, -1 for none
            public int On2;           // its order where they board it
            public int Off2;          // ...and get off
            public double Arrive;     // game minutes (the game's total) they are at the destination
            public double Board;      // ...and they board the first bus
            public double Board2;     // ...and the second
            public bool Direct => Line2 < 0;
        }

        public const float WalkMetresPerMinute = 70f;   // a change on foot, in game minutes (the game's walk is about 1.2 m a second)
        public const float ChangeLeast = 2f;            // game minutes a change takes at the least: off one bus and onto the next

        /// <summary>
        /// The best journey from one place to every other, by arrival: one bus where a line runs there, else two with a
        /// change at the same place or a short walk away. <paramref name="call"/> gives the time (game minutes) a line next
        /// calls at a stop of its (line, order) at or after a time. Fills <paramref name="best"/> (one per place).
        /// </summary>
        public static void From(Net net, int place, double now, Func<int, int, double, double> call, Journey[] best)
        {
            for (int i = 0; i < best.Length; i++)
            {
                best[i] = default(Journey);
                best[i].Line2 = -1;
                best[i].Arrive = double.MaxValue;
            }
            List<int> lines = net.LinesAt[place];
            for (int l = 0; l < lines.Count; l++)
            {
                int line = lines[l];
                int[] calls = net.Calls[line];
                float[] minutes = net.Minutes[line];
                for (int from = 0; from < calls.Length; from++)
                {
                    if (calls[from] != place)
                    {
                        continue;
                    }
                    double board = call(line, from, now);
                    if (board >= double.MaxValue)
                    {
                        continue;
                    }
                    for (int off = from + 1; off < calls.Length; off++)
                    {
                        double at = board + (minutes[off] - minutes[from]);
                        Offer(best, calls[off], at, line, from, off, -1, 0, 0, board, 0);
                        // A change here: at this place, or a short walk away, onto another line.
                        Change(net, calls[off], at, line, from, off, board, call, best, calls[off]);
                        List<int> near = net.Near[calls[off]];
                        for (int n = 0; n < near.Count; n++)
                        {
                            Change(net, near[n], at + net.Apart(calls[off], near[n]) / WalkMetresPerMinute, line, from, off, board, call, best, calls[off]);
                        }
                    }
                }
            }
            best[place] = default(Journey);
            best[place].Line2 = -1;
            best[place].Arrive = double.MaxValue;
        }

        private static void Change(Net net, int at, double ready, int line, int from, int off, double board, Func<int, int, double, double> call, Journey[] best, int offPlace)
        {
            List<int> lines = net.LinesAt[at];
            for (int l = 0; l < lines.Count; l++)
            {
                int second = lines[l];
                if (second == line)
                {
                    continue;
                }
                int[] calls = net.Calls[second];
                float[] minutes = net.Minutes[second];
                for (int on = 0; on < calls.Length; on++)
                {
                    if (calls[on] != at)
                    {
                        continue;
                    }
                    double board2 = call(second, on, ready + (at == offPlace ? ChangeLeast : 0f));
                    if (board2 >= double.MaxValue)
                    {
                        continue;
                    }
                    for (int off2 = on + 1; off2 < calls.Length; off2++)
                    {
                        Offer(best, calls[off2], board2 + (minutes[off2] - minutes[on]), line, from, off, second, on, off2, board, board2);
                    }
                }
            }
        }

        private static void Offer(Journey[] best, int place, double arrive, int line, int from, int off, int line2, int on2, int off2, double board, double board2)
        {
            // One bus is taken over a change unless the change gets there a good deal sooner.
            double held = best[place].Arrive;
            bool better = !best[place].Found || (line2 < 0 && !best[place].Direct ? arrive <= held + 15.0 : best[place].Direct && line2 >= 0 ? arrive < held - 15.0 : arrive < held);
            if (!better)
            {
                return;
            }
            best[place] = new Journey { Found = true, Line = line, From = from, Off = off, Line2 = line2, On2 = on2, Off2 = off2, Arrive = arrive, Board = board, Board2 = board2 };
        }

        // ---------------------------------------------------------------- arithmetic

        public static float Clamp(float value, float least, float most)
        {
            return value < least ? least : value > most ? most : value;
        }

        public static float Distance(float x1, float z1, float x2, float z2)
        {
            float dx = x1 - x2;
            float dz = z1 - z2;
            return (float)Math.Sqrt(dx * dx + dz * dz);
        }
    }
}
