using System;

namespace RealLifeOverhaul.Construction
{
    /// <summary>
    /// What land asks a square metre by district, month by month: the mean of the lots for sale in a district, one figure a month kept in a ring of twelve,
    /// and the change on the month before. The rival developers' asks (LandMarketRules) are in the lots' prices the mean reads. Game-free.
    /// </summary>
    public static class AskRules
    {
        public const int RingMonths = 12;
        public const int DistrictsMost = 8;

        /// <summary>The month a day falls in, by the length of the year the game keeps (twelve months).</summary>
        public static int MonthOf(int day, int daysPerYear) => Math.Max(0, day - 1) / Math.Max(1, daysPerYear / 12);

        /// <summary>The mean ask a square metre: the lots' price over their area; 0 for no area.</summary>
        public static long Mean(long price, long area) => area > 0L && price > 0L ? price / area : 0L;

        /// <summary>The change of an ask on the one before, in thousandths; 0 where there is no earlier ask to compare with.</summary>
        public static int ChangePermille(long before, long now)
        {
            if (before <= 0L || now <= 0L)
            {
                return 0;
            }
            return (int)Math.Max(-1000L, Math.Min(1000L, (now - before) * 1000L / before));
        }

        /// <summary>
        /// Puts one month's ask into a ring (months oldest first, the asks beside them): the same month again replaces its figure, a later month is added,
        /// the oldest dropped past the ring's size, an earlier month ignored. Returns the ring's count.
        /// </summary>
        public static int Put(int[] months, long[] asks, int count, int month, long ask)
        {
            if (ask <= 0L)
            {
                return count;
            }
            if (count > 0 && months[count - 1] == month)
            {
                asks[count - 1] = ask;
                return count;
            }
            if (count > 0 && months[count - 1] > month)
            {
                return count;
            }
            if (count == months.Length)
            {
                for (int i = 1; i < count; i++)
                {
                    months[i - 1] = months[i];
                    asks[i - 1] = asks[i];
                }
                count--;
            }
            months[count] = month;
            asks[count] = ask;
            return count + 1;
        }

        /// <summary>The change on the month before the last figure, in thousandths, or 0 where the ring holds fewer than two months or they are not neighbours.</summary>
        public static int Change(int[] months, long[] asks, int count)
        {
            if (count < 2 || months[count - 1] - months[count - 2] != 1)
            {
                return 0;
            }
            return ChangePermille(asks[count - 2], asks[count - 1]);
        }
    }
}
