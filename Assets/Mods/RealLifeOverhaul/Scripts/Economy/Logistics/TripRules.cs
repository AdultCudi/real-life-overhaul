using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Economy
{
    /// <summary>The kind of vehicle a trip is driven in, for its pace through the city and its fuel.</summary>
    public enum TripVehicle
    {
        Car = 0,
        Van = 1,
        Truck = 2
    }

    /// <summary>
    /// The rules of a delivery trip: when a route's vehicles leave, how long the drive and the stops take, what fits in a
    /// vehicle, what the fuel and the driver's hours cost, and what the city's rival shops pay for the same supply. All in
    /// whole game minutes, metres and cents, so the same inputs give the same trip on every machine. Game-free; checked
    /// offline (EconomyCheck, LogisticsChecks.cs).
    /// </summary>
    public static class TripRules
    {
        public const int DayMinutes = 1440;
        public const int GameRunHour = 2;          // the hour the game runs every route at once (GameManager)
        public const int LoadMinutes = 15;         // minutes: loading at the warehouse before a vehicle leaves
        public const int UnloadMinutes = 10;       // minutes: unloading at each stop
        public const int RoadMille = 1350;         // thousandths: a road's length over the straight line, where the roads are not measured
        public const int LogDays = 7;              // days of runs the log keeps
        public const int LogMost = 240;            // runs the log keeps at most
        public const int PumpCents = 130;          // cents a litre: the game's own pump price, where the game's cannot be read
        public const int RivalWageCents = 1800;    // cents an hour: a rival's driver
        public const int RivalShareMost = 250;     // thousandths: the most of a rival's day the supply costs take
        public const int DelayMillePerHour = 5;    // thousandths of a day's sales lost for each hour the goods are on the road

        /// <summary>The choices of a route's first run (hours), its runs a day and the hours between them.</summary>
        public static readonly int[] RunChoices = { 1, 2, 3, 4 };
        public static readonly int[] GapChoices = { 2, 3, 4, 6, 8, 12 };

        // Metres an hour through the city's streets, and tenths of a litre per 100 km, by TripVehicle.
        private static readonly int[] Pace = { 26000, 22000, 18000 };
        private static readonly int[] Thirst = { 75, 120, 260 };

        /// <summary>A route's departure hours in the day, ascending, each once: from the first, every gap hours, runs times.</summary>
        public static int Hours(int first, int runs, int gap, List<int> into)
        {
            into.Clear();
            int f = Clamp(first, 0, 23), r = Clamp(runs, 1, RunChoices[RunChoices.Length - 1]), g = Clamp(gap, 1, 23);
            for (int k = 0; k < r; k++)
            {
                int h = (f + k * g) % 24;
                if (!into.Contains(h))
                {
                    into.Add(h);
                }
            }
            into.Sort();
            return into.Count;
        }

        /// <summary>The first departure strictly after <paramref name="after"/> (an absolute game minute), on the hour.</summary>
        public static int Next(int after, int first, int runs, int gap, List<int> hours)
        {
            Hours(first, runs, gap, hours);
            int day = Floor(after, DayMinutes);
            for (int d = day; d <= day + 1; d++)
            {
                for (int i = 0; i < hours.Count; i++)
                {
                    int at = d * DayMinutes + hours[i] * 60;
                    if (at > after)
                    {
                        return at;
                    }
                }
            }
            return (day + 2) * DayMinutes + hours[0] * 60;
        }

        /// <summary>The departures a route keeps when the player set none: the game's own 02:00, and 14:00 too for goods that spoil.</summary>
        public static void Default(bool shortLife, out int first, out int runs, out int gap)
        {
            first = GameRunHour;
            runs = shortLife ? 2 : 1;
            gap = 12;
        }

        /// <summary>A road's metres from the straight line's.</summary>
        public static int Road(int straightMetres) => (int)Math.Min(int.MaxValue, (long)Math.Max(0, straightMetres) * RoadMille / 1000);

        /// <summary>Minutes on the road for so many metres, at least one.</summary>
        public static int Drive(int metres, TripVehicle v) => (int)Math.Max(1L, ((long)Math.Max(0, metres) * 60 + Pace[(int)v] - 1) / Pace[(int)v]);

        /// <summary>Cents of fuel for so many metres at the pump's price a litre.</summary>
        public static int Fuel(int metres, TripVehicle v, int pumpCents)
        {
            long tenthsTimesMetres = (long)Math.Max(0, metres) * Thirst[(int)v];
            return (int)((tenthsTimesMetres * Math.Max(0, pumpCents) + 500000L) / 1000000L);
        }

        /// <summary>Kilometres an hour through the city's streets.</summary>
        public static int Kmh(TripVehicle v) => Pace[(int)v] / 1000;

        /// <summary>Tenths of a litre per 100 km.</summary>
        public static int Litres(TripVehicle v) => Thirst[(int)v];

        /// <summary>Cents of a driver's time at an hourly wage.</summary>
        public static int Wage(int hourlyCents, int minutes) => (int)(((long)Math.Max(0, hourlyCents) * Math.Max(0, minutes) + 30) / 60);

        /// <summary>The boxes so many pieces fill.</summary>
        public static int Boxes(int pieces, int boxSize) => pieces <= 0 ? 0 : (pieces + Math.Max(1, boxSize) - 1) / Math.Max(1, boxSize);

        /// <summary>The pieces of a want that fit in the boxes left, a whole box at a time but never more than wanted; the boxes used come off.</summary>
        public static int Fit(int wanted, int boxSize, ref int boxesLeft)
        {
            if (wanted <= 0 || boxesLeft <= 0)
            {
                return 0;
            }
            int size = Math.Max(1, boxSize), need = Boxes(wanted, size);
            int boxes = Math.Min(need, boxesLeft);
            boxesLeft -= boxes;
            return Math.Min(wanted, boxes * size);
        }

        /// <summary>
        /// Each slot's stops of a route: its vehicle's own number of stops, and what the manager's skill adds over them handed out one
        /// at a time from the first slot that drives; never more than the route's most. Returns the stops given.
        /// </summary>
        public static int Shares(int[] slotStops, int count, int most, int[] into)
        {
            int given = 0, driving = 0;
            for (int i = 0; i < count; i++)
            {
                into[i] = Math.Max(0, slotStops[i]);
                given += into[i];
                driving += into[i] > 0 ? 1 : 0;
            }
            for (int i = 0; i < count && given > most; i++)
            {
                int cut = Math.Min(into[count - 1 - i], given - most);
                into[count - 1 - i] -= cut;
                given -= cut;
            }
            for (int round = 0; given < most && driving > 0; round++)
            {
                int i = round % count;
                if (slotStops[i] > 0)
                {
                    into[i]++;
                    given++;
                }
            }
            return given;
        }

        /// <summary>
        /// A trip's timeline: it leaves at <paramref name="depart"/>, loads, drives each leg (legs[0] to the first stop, legs[stops] back),
        /// unloads at each stop. Each stop's arrival goes into <paramref name="arrive"/>; returns the minute it is back.
        /// </summary>
        public static int Timeline(int depart, int[] legs, int stops, int[] arrive)
        {
            int t = depart + LoadMinutes;
            for (int i = 0; i < stops; i++)
            {
                t += Math.Max(1, legs[i]);
                arrive[i] = t;
                t += UnloadMinutes;
            }
            return t + (stops > 0 ? Math.Max(1, legs[stops]) : 0);
        }

        /// <summary>A rival shop's supply costs for a day, in cents: one van's round trip to its supplier and its driver's hours.</summary>
        public static int RivalTrip(int roadMetres, int pumpCents)
        {
            int drive = Drive(roadMetres, TripVehicle.Van);
            return Fuel(roadMetres * 2, TripVehicle.Van, pumpCents) + Wage(RivalWageCents, drive * 2 + LoadMinutes + UnloadMinutes);
        }

        /// <summary>The share of a day's sales a rival loses waiting for its goods, thousandths, by the hours they are on the road.</summary>
        public static int RivalDelay(int roadMetres) => Math.Min(40, Drive(roadMetres, TripVehicle.Van) * DelayMillePerHour / 60);

        /// <summary>
        /// What comes off a rival's day, cents: its trip, the delay and the spoiled share of its sales (thousandths, from
        /// SpoilRules.RivalLoss), never more than <see cref="RivalShareMost"/> of the day nor anything when it made nothing.
        /// </summary>
        public static int RivalDay(int dayCents, int tripCents, int delayMille, int spoilMille)
        {
            if (dayCents <= 0)
            {
                return 0;
            }
            long cost = Math.Max(0, tripCents) + (long)dayCents * (Math.Max(0, delayMille) + Math.Max(0, spoilMille)) / 1000;
            return (int)Math.Min(cost, (long)dayCents * RivalShareMost / 1000);
        }

        /// <summary>The runs a day, pieces, fuel and wages of the log's last seven days, as a day's average (rounded down).</summary>
        public static void Week(List<TripLog> log, string plan, int today, out int runs, out int pieces, out int fuelCents, out int wageCents)
        {
            runs = pieces = fuelCents = wageCents = 0;
            int r = 0;
            long p = 0, f = 0, w = 0;
            for (int i = 0; i < log.Count; i++)
            {
                TripLog e = log[i];
                if (e.Day > today - LogDays && e.Day <= today && (plan == null || e.Plan == plan))
                {
                    r++;
                    p += e.Pieces;
                    f += e.FuelCents;
                    w += e.WageCents;
                }
            }
            runs = r;
            pieces = (int)(p / LogDays);
            fuelCents = (int)(f / LogDays);
            wageCents = (int)(w / LogDays);
        }

        /// <summary>Drops the log's runs older than its seven days and over its most, oldest first.</summary>
        public static void Trim(List<TripLog> log, int today)
        {
            for (int i = log.Count - 1; i >= 0; i--)
            {
                if (log[i].Day <= today - LogDays)
                {
                    log.RemoveAt(i);
                }
            }
            if (log.Count > LogMost)
            {
                log.RemoveRange(0, log.Count - LogMost);
            }
        }

        private static int Clamp(int v, int lo, int hi) => v < lo ? lo : v > hi ? hi : v;

        private static int Floor(int a, int b) => a >= 0 ? a / b : -((-a + b - 1) / b);
    }
}
