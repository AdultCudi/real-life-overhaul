using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// What a cleaner does to a floor, by the game's own sum (BuildingCleanliness.SimulateCleaning), for a visit's minutes.
    /// Game-free; dirt in hundredths of the game's points, a rate in hundredths an hour off every tile.
    /// </summary>
    public static class CleaningRules
    {
        /// <summary>The cleaners a floor of this size needs: one for each 200 m2 begun, one at least.</summary>
        public static int Force(int squareMetres) => Math.Max(1, (Math.Max(0, squareMetres) * ServicesCatalogue.ForcePerThousandSqm + 999) / 1000);

        /// <summary>What one cleaner takes off every tile in an hour: skill by satisfaction (each 0 to 100) over the force.</summary>
        public static int Rate(int skill, int satisfaction, int force)
        {
            return ServicesCatalogue.Clamp(skill, 0, 100) * ServicesCatalogue.Clamp(satisfaction, 0, 100) / Math.Max(1, force);
        }

        /// <summary>What comes off every tile in so many minutes at a rate.</summary>
        public static int Off(int rate, int minutes) => rate <= 0 || minutes <= 0 ? 0 : (int)((long)rate * minutes / 60);

        /// <summary>The minutes that bring a tile this dirty to nothing; 0 for a clean tile or a rate of nothing.</summary>
        public static int Minutes(int dirt, int rate) => dirt <= 0 || rate <= 0 ? 0 : (int)(((long)dirt * 60 + rate - 1) / rate);

        /// <summary>A visit's planned length: what the dirt takes, within the shortest and longest visit; the usual length for unknown (negative) dirt.</summary>
        public static int Planned(int dirt, int rate)
        {
            if (rate <= 0)
            {
                return 0;
            }
            return dirt < 0 ? ServicesCatalogue.VisitDefault : ServicesCatalogue.Clamp(Minutes(dirt, rate), ServicesCatalogue.VisitLeast, ServicesCatalogue.VisitMost);
        }

        public static int Clean(int dirt, int off) => Math.Max(0, dirt - Math.Max(0, off));

        /// <summary>Cleans every tile of a floor; returns the dirtiest that is left.</summary>
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

        /// <summary>What a visit took off a floor's worst tile, in hundredths: what it would take off, as far as there was dirt there.</summary>
        public static int Taken(int worst, int off) => Math.Max(0, Math.Min(worst, off));   // tiles stop at nothing

        /// <summary>A tile at a cleaner's base with their minutes away put back on, before the game takes a whole hour of their work off it.</summary>
        public static int Hold(int dirt, int rate, int awayMinutes) => Math.Min(ServicesCatalogue.DirtMost, dirt + Off(rate, Math.Min(60, awayMinutes)));

        /// <summary>A floor's cleanliness as BuildingCleanliness.GetCleanliness works it out: 100 less the mean dirt and the mean dirt that shows.</summary>
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

        /// <summary>The cleanliness of a floor evenly this dirty: a contract client's, which the mod keeps as one figure.</summary>
        public static int Even(int dirt) => Cleanliness(dirt, dirt >= ServicesCatalogue.DirtVisible ? dirt : 0, 1);

        /// <summary>A business's dirt a day: its worst tile's rise since yesterday plus what the service took off, averaged; today's alone at first.</summary>
        public static int NeedDay(int before, int worstStart, int worstEnd, int cleaned)
        {
            int today = Math.Max(0, worstEnd - worstStart + Math.Max(0, cleaned));
            int days = ServicesCatalogue.NeedDaysAverage;
            return before < 0 ? today : (int)(((long)before * (days - 1) + today + days / 2) / days);
        }

        /// <summary>The minutes of cleaning a day that keep such a business level; 0 while nothing has been measured.</summary>
        public static int NeedMinutes(int needDay, int rate) => needDay <= 0 ? 0 : Minutes(needDay, rate);

        /// <summary>What a shop's own cleaner would cost a week: whole hours a day, the shortest shift at least, at a wage in hundredths an hour.</summary>
        public static int OwnWeek(int needMinutesDay, int wage)
        {
            int hours = Math.Max(ServicesCatalogue.OwnShiftLeastHours, (Math.Max(0, needMinutesDay) + 59) / 60);
            return (int)((long)hours * 7 * Math.Max(0, wage) / 100);
        }
    }

    /// <summary>The way between two doors: the straight line times a factor for the streets, on foot or by van. Game-free.</summary>
    public static class TravelRules
    {
        public static int Metres(int x1, int z1, int x2, int z2)
        {
            double dx = x1 - x2, dz = z1 - z2;
            return (int)Math.Round(Math.Sqrt(dx * dx + dz * dz) * ServicesCatalogue.StreetFactor / 100.0);
        }

        /// <summary>Game minutes for a way, rounded up.</summary>
        public static int Minutes(int metres, bool van)
        {
            int pace = van ? ServicesCatalogue.VanPerMinute : ServicesCatalogue.WalkPerMinute;
            return metres <= 0 ? 0 : (int)(((long)metres * 100 + pace - 1) / pace);
        }

        /// <summary>What the van costs a kilometre at a price index, as it is charged (to the cent: 4.5 at an index of 112.5).</summary>
        public static float PerKm(int index) => ServicesCatalogue.VanCostPerKm * ServicesCatalogue.Clamp(index, 500, 4000) / 1000f;

        /// <summary>What a day's driving costs at a price index in tenths of a point.</summary>
        public static int Cost(int metres, int index)
        {
            return metres <= 0 ? 0 : (int)(((long)metres * ServicesCatalogue.VanCostPerKm * ServicesCatalogue.Clamp(index, 500, 4000) + 500000) / 1000000);
        }
    }

    /// <summary>Distances between doors, each worked out once: a round asks for the same pairs again and again.</summary>
    public sealed class TravelCache
    {
        private readonly Dictionary<long, int> _metres = new Dictionary<long, int>(64);

        /// <summary>How often a distance was already known; <see cref="Misses"/>: how often it was worked out.</summary>
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
            if (_metres.TryGetValue(key, out int metres))
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
