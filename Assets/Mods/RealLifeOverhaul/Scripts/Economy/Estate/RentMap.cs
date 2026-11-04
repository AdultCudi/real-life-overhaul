using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Economy
{
    /// <summary>One other landlord of the city, as the game's rival-owned buildings show them: their units and what a flat of theirs rents for.</summary>
    public sealed class RentLandlord
    {
        public string Id = string.Empty;    // the game's rival id
        public int Units;                   // units they hold in the city's buildings
        public int Flats;                   // ...of them flats
        public long FlatRent;               // dollars a day, summed over their flats
        public int Last;                    // their mean flat rent a month at the last month's turn, dollars; 0 until a month has turned
        public int Mean(int monthDays) => Flats > 0 ? (int)(FlatRent / Flats * monthDays) : 0;
    }

    /// <summary>
    /// The city's rents by district and use as the game's buildings and their occupancy give them, one read a day: the data the info view
    /// of rents shows and the Rents layer of build mode will colour the map by (shared, game-free). A building contributes its units' rent
    /// at the market (<see cref="EstateRules.MarketRent(int,int,int,int,int,EstateClimate)"/>), its floor area and how many of its units stand empty.
    /// </summary>
    public sealed class RentMapData
    {
        public const int Uses = 4;                       // EstateUse
        public int Day = -1;                             // the day it was read
        public int MonthDays = 30;
        public readonly List<string> Districts = new List<string>(16);
        public readonly List<long> RentDay = new List<long>(64);   // by district * Uses + use: dollars a day, all the units
        public readonly List<long> Sqm = new List<long>(64);       // ...the floor area of those buildings
        public readonly List<int> Units = new List<int>(64);       // ...the units
        public readonly List<int> Empty = new List<int>(64);       // ...the units standing empty
        public readonly int[] ForSale = new int[Uses];             // buildings for sale, by use
        public readonly List<RentLandlord> Landlords = new List<RentLandlord>(12);

        public int DistrictCount => Districts.Count;

        /// <summary>Clears the data for a new read; the districts and landlords are found again.</summary>
        public void Clear(int day, int monthDays)
        {
            Day = day;
            MonthDays = Math.Max(1, monthDays);
            Districts.Clear();
            RentDay.Clear();
            Sqm.Clear();
            Units.Clear();
            Empty.Clear();
            Landlords.Clear();
            Array.Clear(ForSale, 0, ForSale.Length);
        }

        private int Place(string district)
        {
            int at = Districts.IndexOf(district ?? string.Empty);
            if (at < 0)
            {
                at = Districts.Count;
                Districts.Add(district ?? string.Empty);
                for (int u = 0; u < Uses; u++)
                {
                    RentDay.Add(0);
                    Sqm.Add(0);
                    Units.Add(0);
                    Empty.Add(0);
                }
            }
            return at;
        }

        /// <summary>One building: its district, its use, the rent of its units a day (all of them, dollars), its floor area, its units and how many stand empty.</summary>
        public void Add(string district, int use, int rentADay, int sqm, int units, int empty)
        {
            if (use < 0 || use >= Uses || units <= 0 || sqm <= 0 || rentADay < 0)
            {
                return;
            }
            int cell = Place(district) * Uses + use;
            RentDay[cell] += rentADay;
            Sqm[cell] += sqm;
            Units[cell] += units;
            Empty[cell] += Math.Max(0, Math.Min(units, empty));
        }

        /// <summary>Units of a district and use that stand empty (a building for rent with nobody in it), added after <see cref="Add"/>; never more than the units.</summary>
        public void AddEmpty(string district, int use, int units)
        {
            int at = Districts.IndexOf(district ?? string.Empty);
            if (at < 0 || use < 0 || use >= Uses || units <= 0)
            {
                return;
            }
            int cell = at * Uses + use;
            Empty[cell] = Math.Min(Units[cell], Empty[cell] + units);
        }

        /// <summary>A rival-owned building's units are counted to its owner.</summary>
        public void AddLandlord(string rival, int use, int rentADay, int units)
        {
            if (string.IsNullOrEmpty(rival) || units <= 0)
            {
                return;
            }
            RentLandlord l = Landlord(rival);
            if (l == null)
            {
                if (Landlords.Count >= EstateCatalogue.LandlordsMost)
                {
                    return;
                }
                l = new RentLandlord { Id = rival };
                Landlords.Add(l);
            }
            l.Units += units;
            if (use == (int)EstateUse.Homes)
            {
                l.Flats += units;
                l.FlatRent += rentADay;
            }
        }

        public RentLandlord Landlord(string id)
        {
            for (int i = 0; i < Landlords.Count; i++)
            {
                if (string.Equals(Landlords[i].Id, id, StringComparison.Ordinal))
                {
                    return Landlords[i];
                }
            }
            return null;
        }

        // ------------------------------------------------------------------------------------------------- reads

        /// <summary>Rent a month for a square metre, in cents, in a district and use; -1 when no building of that use is there.</summary>
        public int PerSqm(int district, int use)
        {
            if (district < 0 || district >= Districts.Count || use < 0 || use >= Uses)
            {
                return -1;
            }
            int cell = district * Uses + use;
            return Sqm[cell] > 0 ? (int)Math.Min(int.MaxValue / 4, RentDay[cell] * MonthDays * 100 / Sqm[cell]) : -1;
        }

        /// <summary>Rent a month for a square metre over the whole city for a use, in cents; -1 for none.</summary>
        public int PerSqm(int use)
        {
            long rent = 0, sqm = 0;
            for (int d = 0; d < Districts.Count; d++)
            {
                int cell = d * Uses + use;
                rent += RentDay[cell];
                sqm += Sqm[cell];
            }
            return sqm > 0 ? (int)Math.Min(int.MaxValue / 4, rent * MonthDays * 100 / sqm) : -1;
        }

        /// <summary>The mean rent a month of one unit of a use over the whole city, dollars; -1 for none.</summary>
        public int MeanUnit(int use)
        {
            long rent = 0;
            long units = 0;
            for (int d = 0; d < Districts.Count; d++)
            {
                rent += RentDay[d * Uses + use];
                units += Units[d * Uses + use];
            }
            return units > 0 ? (int)Math.Min(int.MaxValue / 4, rent * MonthDays / units) : -1;
        }

        /// <summary>The share of units standing empty over the whole city, thousandths; -1 when there are none.</summary>
        public int EmptyShare()
        {
            long units = 0, empty = 0;
            for (int c = 0; c < Units.Count; c++)
            {
                units += Units[c];
                empty += Empty[c];
            }
            return units > 0 ? (int)(empty * 1000 / units) : -1;
        }

        /// <summary>The districts that have buildings of a use, dearest a square metre first, written into the array (as long as the most wanted); returns how many.</summary>
        public int Ranked(int use, int[] into)
        {
            int count = 0;
            for (int d = 0; d < Districts.Count && count < into.Length; d++)
            {
                if (PerSqm(d, use) >= 0)
                {
                    into[count++] = d;
                }
            }
            for (int i = 1; i < count; i++)
            {
                int d = into[i];
                int j = i - 1;
                while (j >= 0 && PerSqm(into[j], use) < PerSqm(d, use))
                {
                    into[j + 1] = into[j];
                    j--;
                }
                into[j + 1] = d;
            }
            return count;
        }

        public int ForSaleAll()
        {
            int n = 0;
            for (int i = 0; i < ForSale.Length; i++)
            {
                n += ForSale[i];
            }
            return n;
        }
    }

    /// <summary>The city's rent over months: one mean a month in a ring, and the change on the month before. Game-free.</summary>
    public static class RentRingRules
    {
        /// <summary>The month's mean flat rent (dollars a month) is written once for the month: false when this month is already in or the figure is none.</summary>
        public static bool Month(EstateState s, int month, int meanFlatRent)
        {
            if (meanFlatRent <= 0 || (s.RentRingCount > 0 && month <= s.RentRingMonth))
            {
                return false;
            }
            int[] ring = s.RentRing;
            if (s.RentRingCount < ring.Length)
            {
                ring[s.RentRingCount++] = meanFlatRent;
            }
            else
            {
                for (int i = 1; i < ring.Length; i++)
                {
                    ring[i - 1] = ring[i];
                }
                ring[ring.Length - 1] = meanFlatRent;
            }
            s.RentRingMonth = month;
            return true;
        }

        /// <summary>The last month's change in thousandths (+12 is 1.2%); <see cref="int.MinValue"/> while there are not two months.</summary>
        public static int Change(EstateState s)
        {
            if (s.RentRingCount < 2 || s.RentRing[s.RentRingCount - 2] <= 0)
            {
                return int.MinValue;
            }
            int last = s.RentRing[s.RentRingCount - 1], before = s.RentRing[s.RentRingCount - 2];
            return (int)((long)(last - before) * 1000 / before);
        }

        /// <summary>
        /// A month has turned: each rival landlord's mean flat rent now becomes their mark, and the one before it their previous mark, so the
        /// change between the last two months can be shown. Capped at <see cref="EstateCatalogue.LandlordsMost"/>.
        /// </summary>
        public static void Mark(EstateState s, RentMapData data)
        {
            for (int i = 0; i < data.Landlords.Count; i++)
            {
                RentLandlord l = data.Landlords[i];
                int mean = l.Mean(data.MonthDays);
                LandlordMark mark = null;
                for (int k = 0; k < s.Marks.Count && mark == null; k++)
                {
                    mark = string.Equals(s.Marks[k].Id, l.Id, StringComparison.Ordinal) ? s.Marks[k] : null;
                }
                if (mark != null)
                {
                    mark.Prev = mark.Rent;
                    mark.Rent = mean;
                }
                else if (s.Marks.Count < EstateCatalogue.LandlordsMost)
                {
                    s.Marks.Add(new LandlordMark { Id = l.Id, Rent = mean });
                }
            }
        }

        /// <summary>A landlord's change between the last two month turns, in thousandths; <see cref="int.MinValue"/> with none.</summary>
        public static int LandlordChange(EstateState s, string id)
        {
            for (int k = 0; k < s.Marks.Count; k++)
            {
                LandlordMark m = s.Marks[k];
                if (string.Equals(m.Id, id, StringComparison.Ordinal))
                {
                    return m.Prev > 0 && m.Rent > 0 ? (int)((long)(m.Rent - m.Prev) * 1000 / m.Prev) : int.MinValue;
                }
            }
            return int.MinValue;
        }
    }

    /// <summary>What a rival landlord's mean flat rent was at the last month's turn.</summary>
    public sealed class LandlordMark
    {
        public string Id = string.Empty;
        public int Rent, Prev;     // at the last month's turn, and the turn before
    }
}
