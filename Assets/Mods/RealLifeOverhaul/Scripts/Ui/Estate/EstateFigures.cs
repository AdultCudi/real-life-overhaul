using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Ui
{
    /// <summary>One unit of the rent roll as the figures see it: let or not, what it pays a day, what its tenant owes, and what it asks while it stands empty (a day).</summary>
    public struct RollLine
    {
        public bool Let;
        public int RentDay, Arrears, AskDay;
    }

    /// <summary>The head of the rent roll: what is due in a month, what is owed, what that leaves collected, and what the empty units ask in a month.</summary>
    public struct RollTotals
    {
        public long Due, Owed, Collected, EmptyAsking;
        public int Units, Let;
    }

    /// <summary>
    /// The sums the Property app puts in its heads, worked out in one place so the live pages and the samples cannot add them up two ways: the roll's totals, a mean satisfaction, a day's
    /// rent as a month's, and how a guest's review is rated. Game-free. COST: one pass over the units, when a page is written.
    /// </summary>
    public static class EstateFigures
    {
        /// <summary>A rent a day as the roll shows it, a month's.</summary>
        public static long Month(long perDay, int monthDays) => perDay * Math.Max(1, monthDays);

        /// <summary>The roll's totals from its units. A unit that is let adds its month's rent to what is due and its arrears to what is owed; one that stands empty adds what it asks.</summary>
        public static RollTotals Roll(IList<RollLine> lines, int monthDays)
        {
            var t = new RollTotals();
            for (int i = 0; i < lines.Count; i++)
            {
                RollLine l = lines[i];
                t.Units++;
                if (l.Let)
                {
                    t.Let++;
                    t.Due += Month(l.RentDay, monthDays);
                    t.Owed += l.Arrears;
                }
                else
                {
                    t.EmptyAsking += Month(l.AskDay, monthDays);
                }
            }
            t.Collected = Math.Max(0, t.Due - t.Owed);
            return t;
        }

        /// <summary>The mean of the tenants' satisfaction (0 to 1000 each), or -1 with none judged yet. Whole thousandths, as a building's own mean is.</summary>
        public static int MeanHappy(IList<int> happy)
        {
            long sum = 0;
            int count = 0;
            for (int i = 0; i < happy.Count; i++)
            {
                if (happy[i] >= 0)
                {
                    sum += happy[i];
                    count++;
                }
            }
            return count > 0 ? (int)(sum / count) : -1;
        }

        /// <summary>How many halves of a star a guest gives for a review the hotel was written (HotelRules.ReviewClean to ReviewDear): praise is four or five stars, a complaint one or two.</summary>
        public static int ReviewHalves(int review)
        {
            switch (review)
            {
                case 1: return 10;   // clean
                case 3: return 8;    // the staff
                case 5: return 8;    // good value
                case 2: return 2;    // dirty
                case 4: return 4;    // nobody at the desk
                case 6: return 4;    // too dear
                default: return -1;
            }
        }

        /// <summary>A yield in hundredths of a per cent from a rent a day, a year of days and a price.</summary>
        public static int YieldHundredths(long rentDay, int daysPerYear, long price) => price > 0 ? (int)(rentDay * daysPerYear * 10000L / price) : 0;
    }
}
