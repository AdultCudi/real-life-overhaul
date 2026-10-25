using System;

namespace RealLifeOverhaul
{
    /// <summary>
    /// What a police report is worth to a car's insurer. A report on a car break-in or an accident is the paper the insurer asks
    /// for: the next repair paid within its days is paid without the excess. Game-free.
    /// </summary>
    public static class ReportedRepair
    {
        /// <summary>Whether a report handed over on <paramref name="reportDay"/> (0 for none) is still good on <paramref name="today"/>.</summary>
        public static bool Fresh(int reportDay, int today, int days)
        {
            return reportDay > 0 && today >= reportDay && today - reportDay <= days;
        }

        /// <summary>What the insurer pays of a repair bill: the bill less the excess (none with a report), the cover's share of the rest.</summary>
        public static float Payout(float bill, float excess, float cover, bool reported)
        {
            return Math.Max(0f, (float)Math.Round((bill - (reported ? 0f : excess)) * cover));
        }
    }
}
