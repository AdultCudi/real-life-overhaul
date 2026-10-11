using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// What a cleaner does to a floor, by the game's own arithmetic. Game-free.
    ///
    /// The game cleans once an hour: every tile of a building loses the skill of each cleaner on a cleaning shift
    /// there, times their satisfaction, divided by the cleaners the floor needs for its size
    /// (BuildingCleanliness.SimulateCleaning). A visit by the cleaning service does exactly that for the minutes the
    /// cleaner is there, and nothing else: the same figure for a shop of the player's (written to the game's own
    /// tiles) and for a client under contract (kept by the mod, since the game keeps no dirt for a business that is
    /// not the player's).
    ///
    /// Dirt is in hundredths of the game's points, a rate in hundredths an hour off every tile.
    /// </summary>
    public static class CleaningRules
    {
        /// <summary>The cleaners a floor of this size needs: one for each 200 m2 begun, one at least.</summary>
        public static int Force(int squareMetres)
        {
            return Math.Max(1, (Math.Max(0, squareMetres) * ServicesCatalogue.ForcePerThousandSqm + 999) / 1000);
        }

        /// <summary>What one cleaner takes off every tile in an hour: skill (0 to 100) by satisfaction (0 to 100) over the force.</summary>
        public static int Rate(int skill, int satisfaction, int force)
        {
            return ServicesCatalogue.Clamp(skill, 0, 100) * ServicesCatalogue.Clamp(satisfaction, 0, 100) / Math.Max(1, force);
        }

        /// <summary>What comes off every tile in so many minutes at a rate.</summary>
        public static int Off(int rate, int minutes)
        {
            return rate <= 0 || minutes <= 0 ? 0 : (int)((long)rate * minutes / 60);
        }

        /// <summary>The minutes it takes to bring a tile this dirty to nothing; 0 for a clean one or a cleaner who takes nothing off.</summary>
        public static int Minutes(int dirt, int rate)
        {
            return dirt <= 0 || rate <= 0 ? 0 : (int)(((long)dirt * 60 + rate - 1) / rate);
        }

        /// <summary>The length a visit is planned at: what the dirt takes, between the shortest and the longest visit; the usual length where the dirt is not known.</summary>
        public static int Planned(int dirt, int rate)
        {
            if (rate <= 0)
            {
                return 0;
            }
            return dirt < 0 ? ServicesCatalogue.VisitDefault : ServicesCatalogue.Clamp(Minutes(dirt, rate), ServicesCatalogue.VisitLeast, ServicesCatalogue.VisitMost);
        }

        /// <summary>A tile after cleaning.</summary>
        public static int Clean(int dirt, int off)
        {
            return Math.Max(0, dirt - Math.Max(0, off));
        }

        /// <summary>Every tile of a floor after cleaning. Returns the dirtiest that is left.</summary>
        public static int Clean(int[] tiles, int off)
        {
            int worst = 0;
            for (int i = 0; tiles != null && i < tiles.Length; i++)
            {
                tiles[i] = Clean(tiles[i], off);
                worst = Math.Max(worst, tiles[i]);
            }
            return worst;
        }

        /// <summary>
        /// A tile at the cleaner's own base before the game's hourly cleaning, for the minutes they were out: the
        /// game is about to take a whole hour of their work off it, so the part of the hour they spent elsewhere is
        /// put on first and the two cancel. Never past the dirtiest a tile can be.
        /// </summary>
        public static int Hold(int dirt, int rate, int awayMinutes)
        {
            return Math.Min(ServicesCatalogue.DirtMost, dirt + Off(rate, Math.Min(60, awayMinutes)));
        }

        /// <summary>
        /// A floor's cleanliness as the game works it out (BuildingCleanliness.GetCleanliness): 100 less the mean dirt
        /// of all tiles and less the mean of the dirt that shows, cut to a whole number; 100 where nothing shows.
        /// </summary>
        public static int Cleanliness(long total, long visible, int tiles)
        {
            if (tiles <= 0 || visible <= 0)
            {
                return 100;
            }
            return Math.Max(0, (int)(100.0 - total / 100.0 / tiles - visible / 100.0 / tiles));
        }

        public static int Cleanliness(int[] tiles)
        {
            long total = 0, visible = 0;
            for (int i = 0; tiles != null && i < tiles.Length; i++)
            {
                total += tiles[i];
                visible += tiles[i] >= ServicesCatalogue.DirtVisible ? tiles[i] : 0;
            }
            return Cleanliness(total, visible, tiles != null ? tiles.Length : 0);
        }

        /// <summary>The cleanliness of a floor that is evenly this dirty: a client under contract, whose floor the mod keeps as one figure.</summary>
        public static int Even(int dirt)
        {
            return Cleanliness(dirt, dirt >= ServicesCatalogue.DirtVisible ? dirt : 0, 1);
        }

        /// <summary>
        /// The dirt a business makes in a day, measured: how much dirtier its worst tile is than a day ago, with what
        /// the service took off in between put back. Averaged over the last days; -1 until there is a first day.
        /// </summary>
        public static int NeedDay(int before, int worstStart, int worstEnd, int cleaned)
        {
            int today = Math.Max(0, worstEnd - worstStart + Math.Max(0, cleaned));
            int days = ServicesCatalogue.NeedDaysAverage;
            return before < 0 ? today : (int)(((long)before * (days - 1) + today + days / 2) / days);
        }

        /// <summary>The minutes of cleaning a day that keep such a business level, at a rate; 0 while nothing has been measured.</summary>
        public static int NeedMinutes(int needDay, int rate)
        {
            return needDay <= 0 ? 0 : Minutes(needDay, rate);
        }

        /// <summary>
        /// What a cleaner of its own would cost a business a week: whole hours a day, the shortest shift at least,
        /// seven days, at an hourly wage in hundredths.
        /// </summary>
        public static int OwnWeek(int needMinutesDay, int wage)
        {
            int hours = Math.Max(ServicesCatalogue.OwnShiftLeastHours, (Math.Max(0, needMinutesDay) + 59) / 60);
            return (int)((long)hours * 7 * Math.Max(0, wage) / 100);
        }
    }

    /// <summary>
    /// The way between two doors. Game-free.
    ///
    /// The distance is the straight line times a factor for the streets; the time is that distance at the pace of
    /// somebody on foot or of a van, in minutes of the game's clock. A distance is worked out once for a pair of doors
    /// and kept (<see cref="TravelCache"/>): a round asks for the same pairs again and again.
    /// </summary>
    public static class TravelRules
    {
        public static int Metres(int x1, int z1, int x2, int z2)
        {
            double dx = x1 - x2, dz = z1 - z2;
            return (int)Math.Round(Math.Sqrt(dx * dx + dz * dz) * ServicesCatalogue.StreetFactor / 100.0);
        }

        /// <summary>Minutes of the game's clock for a way, rounded up; nothing for no way at all.</summary>
        public static int Minutes(int metres, bool van)
        {
            int pace = van ? ServicesCatalogue.VanPerMinute : ServicesCatalogue.WalkPerMinute;
            return metres <= 0 ? 0 : (int)(((long)metres * 100 + pace - 1) / pace);
        }

        /// <summary>What a day's driving costs. The index is in tenths of a point.</summary>
        public static int Cost(int metres, int index)
        {
            return metres <= 0 ? 0 : (int)(((long)metres * ServicesCatalogue.VanCostPerKm * ServicesCatalogue.Clamp(index, 500, 4000) + 500000) / 1000000);
        }
    }

    /// <summary>Distances between doors, each worked out once. A few dozen entries; cleared when the list of places changes.</summary>
    public sealed class TravelCache
    {
        private readonly Dictionary<long, int> _metres = new Dictionary<long, int>(64);

        /// <summary>How often a distance was already known, and how often it had to be worked out.</summary>
        public int Hits { get; private set; }
        public int Misses { get; private set; }
        public int Count => _metres.Count;

        public int Metres(int x1, int z1, int x2, int z2)
        {
            long a = Pack(x1, z1), b = Pack(x2, z2);
            if (a == b)
            {
                return 0;
            }
            long key = a < b ? (a << 32) | b : (b << 32) | a;
            int metres;
            if (_metres.TryGetValue(key, out metres))
            {
                Hits++;
                return metres;
            }
            Misses++;
            metres = TravelRules.Metres(x1, z1, x2, z2);
            _metres[key] = metres;
            return metres;
        }

        public void Clear()
        {
            _metres.Clear();
            Hits = Misses = 0;
        }

        // A door as one number: the city is a few kilometres across, so sixteen bits each way are plenty.
        private static long Pack(int x, int z)
        {
            return (long)(((uint)(x + 32768) & 0xffffu) << 16 | ((uint)(z + 32768) & 0xffffu));
        }
    }
}
