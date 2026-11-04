using System;

namespace RealLifeOverhaul.Economy
{
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
