using System;
using System.Collections.Generic;
using System.Globalization;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The bus network's arithmetic, nothing of the game in it, so EconomyCheck runs it on made-up cities: the timetable, the
    /// estimate of an arrival and what it learns, which stations the lines join, and the journey planner. The timetable is in
    /// game minutes on the game's own clock, so it is one timetable whichever stop the player stands at.
    /// </summary>
    public static partial class TransitPlan
    {
        public const int NightFrom = 1;            // from 01:00 only the main lines run (the night service)
        public const int NightUntil = 5;           // until 05:00
        public const float SpacingSeconds = 22f;   // real seconds between two buses at one stop, whatever their lines: one in, stands, pulls out
        public const float RainSlower = 1.15f;     // in the rain every line runs this much less often
        public const int MainRoutes = 4;           // the longest routes are the main lines: they run more often and through the night

        // Game minutes between two buses of one line at one stop, hour by hour on a weekday; 0 for none. At the weekend the
        // rush hours run as the rest of the day.
        private static readonly float[] Main =
        {
            //  0    1    2    3    4    5    6    7    8    9   10   11   12   13   14   15   16   17   18   19   20   21   22   23
            70f, 90f, 90f, 90f, 90f, 60f, 50f, 36f, 36f, 45f, 45f, 45f, 45f, 45f, 45f, 45f, 36f, 36f, 36f, 45f, 55f, 55f, 65f, 65f
        };

        private static readonly float[] Local =
        {
            80f, 0f, 0f, 0f, 0f, 75f, 60f, 48f, 48f, 60f, 60f, 60f, 60f, 60f, 60f, 60f, 48f, 48f, 48f, 60f, 70f, 70f, 80f, 80f
        };

        private const float MainDay = 45f;          // game minutes: a main line at the weekend's rush hours
        private const float LocalDay = 60f;         // and a local one

        /// <summary>True for the game's weekend (Friday to Sunday) of a day number, day 1 a Monday.</summary>
        public static bool Weekend(int day)
        {
            int weekday = ((day - 1) % 7 + 7) % 7;
            return weekday >= 4;
        }

        public static bool IsRush(int hour, bool weekend)
        {
            return !weekend && ((hour >= 7 && hour < 9) || (hour >= 16 && hour < 19));
        }

        /// <summary>Game minutes between two buses of a line at a stop in an hour; 0 when it does not run then.</summary>
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

        /// <summary>The next time (game minutes) at or after <paramref name="now"/> a line calls at a stop; double.MaxValue if it never runs.</summary>
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

        /// <summary>Sorts the calls at one stop, soonest first, and pushes them <paramref name="spacing"/> game minutes apart.</summary>
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

        public const float TrafficStart = 1.12f;   // the drive takes this much longer than the bus's own reckoning of an open road, at first
        public const float TrafficLeast = 0.95f;   // and between these, as learnt
        public const float TrafficMost = 1.9f;
        public const float LightStart = 6f;        // seconds each lit junction ahead adds on average (half of them red, some 12 s at those)
        public const float LightMost = 30f;        // seconds at most
        public const float RedExpect = 14f;        // seconds a bus that has pulled up at a red light is expected to stand there
        public const float LearnShare = 0.25f;     // of each bus that comes, what is learnt
        public const float SlackLeast = 6f;        // real seconds a bus may be behind its figure before it is late
        public const float SlackShare = 0.12f;     // or this share of the figure, whichever is more

        /// <summary>How the buses really drive, learnt this session: traffic's share over the open road's reckoning, and a lit junction's cost.</summary>
        public sealed class Pace
        {
            public float Traffic = TrafficStart;
            public float Light = LightStart;
            public int Samples;

            /// <summary>Seconds to the stop from the open road's reckoning, the lights ahead, the hold left, and a red light it stands at.</summary>
            public float Seconds(float drive, int lightsAhead, float holdLeft, bool atRed, float redFor)
            {
                float seconds = Math.Max(0f, drive) * Traffic + Math.Max(0, lightsAhead) * Light + Math.Max(0f, holdLeft);
                if (atRed)
                {
                    seconds += Math.Max(2f, RedExpect - redFor);
                }
                return seconds;
            }

            /// <summary>Learns from a bus that has come, a share at a time within bounds, so one odd bus does not upset the next.</summary>
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

        /// <summary>Metres of line before the stop a bus should start from to reach it in the seconds left, set off at once.</summary>
        public static float StartBack(float secondsLeft, float cruise, Pace pace, float lightsPerMetre, float least, float most)
        {
            float perMetre = pace.Traffic / Math.Max(1f, cruise) + lightsPerMetre * pace.Light;
            float metres = (secondsLeft - 4f) / Math.Max(0.02f, perMetre);   // some seconds of it gathering speed and pulling in
            return Clamp(metres, least, most);
        }

        /// <summary>Seconds before its call a bus is put on the road: what a start <paramref name="mostBack"/> metres back takes, and a little more.</summary>
        public static float LeadSeconds(float cruise, Pace pace, float lightsPerMetre, float mostBack)
        {
            float perMetre = pace.Traffic / Math.Max(1f, cruise) + lightsPerMetre * pace.Light;
            return 4f + mostBack * perMetre + 3f;
        }

        public const float SpanLeast = 600f;       // metres between the two ends of a route, as the crow flies
        public const float SpanMost = 3200f;       // and at most
        public const float SpanBest = 1900f;       // the length a route is best laid at: cross-town, not round the corner
        public const int StationTarget = 3;        // lines that should serve each station: as ends, or passing within ServedWithin
        public const float ServedWithin = 220f;    // metres of a station a line passes to serve it
        public const float BearingApart = 28f;     // degrees two routes from one station must be apart: one up the avenue, one across town
        public const float StretchMost = 1.65f;    // a line's path may be this much longer than the straight line between its ends
        public const float StretchExtra = 350f;    // and this many metres more
        public const float CorridorShare = 0.2f;   // a stretch of road a line already runs along counts this much less to the next line laid

        /// <summary>
        /// Which pair of stations the next route joins: the one serving the stations furthest short of <see cref="StationTarget"/>
        /// lines, at a believable length, in a direction neither end has a route in yet.
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

            /// <summary>No road joins the two: never asked for.</summary>
            public void NoRoad(int a, int b)
            {
                _tried.Add(Key(a, b));
            }

            /// <summary>The pair was tried, a route laid or not.</summary>
            public void Done(int a, int b)
            {
                _tried.Add(Key(a, b));
            }

            public void Ends(int a, int b)
            {
                float bearing = Bearing(a, b);
                _bearings[a].Add(bearing);
                _bearings[b].Add(bearing);
            }

            /// <summary>A route ends at this station or passes near it.</summary>
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

            /// <summary>The pair to try next; false when none would bring any station nearer its target.</summary>
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

        /// <summary>Why a line's path will not do (too long for its span, or doubling back), or null.</summary>
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
                return "it is " + (length / 1000f).ToString("F1", CultureInfo.InvariantCulture) + " km long for " + (span / 1000f).ToString("F1", CultureInfo.InvariantCulture)
                    + " km as the crow flies";
            }
            return DoublesBack(x, z, count) ? "it doubles back on itself" : null;
        }

        public const float BackNear = 12f;       // metres: a path that comes this near where it has been
        public const float BackAfter = 120f;     // this many metres of path later, heading the other way, doubles back
        public const float BackAhead = 150f;     // and so does one that ends up this much further from its end than it was before

        /// <summary>True when a path comes back along a street it has driven, the other way, or heads away from its end for a long way.</summary>
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
            // A grid of cells BackNear across, so the look is not every point against every other.
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

        /// <summary>
        /// The stop another line has laid on this path between <paramref name="least"/> and <paramref name="most"/> metres, nearest
        /// <paramref name="wanted"/> and not within <paramref name="ends"/> points of an end; -1 if none. Lines on one street share its stops.
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
