using System;
using System.Collections.Generic;
using Extensions;
using UI.Notification;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The day's matched rivals' offers, told in one notice (<see cref="RivalMatchTally"/>): the Company's staff and the
    /// professionals each add what they matched, and the first hour of the day says it. Cost: nothing while no offer was matched.
    /// </summary>
    internal static class RivalMatches
    {
        private static readonly RivalMatchTally Tally = new RivalMatchTally();

        /// <summary>An offer was matched on <paramref name="day"/>; <paramref name="monthlyCents"/> is what it adds to the monthly pay.</summary>
        public static void Matched(int day, long monthlyCents)
        {
            Tally.Add(day, monthlyCents);
        }

        /// <summary>Called from the hourly pass of the parts that match: says the matches of the night once, from the first hour.</summary>
        public static void Flush(int day, int hour)
        {
            if (!Tally.Take(day, hour, out int count, out long cents))
            {
                return;
            }
            try
            {
                var data = new Dictionary<string, string>(2)
                {
                    { "count", count.ToString(System.Globalization.CultureInfo.InvariantCulture) },
                    { "cost", ((float)(cents / 100f)).ToCurrencyFormat() }
                };
                Notifications.Show(NotificationType.Info, "reallife_rival_matches_notice", data, 12f);
            }
            catch (Exception)
            {
                // a notice that cannot be shown is not worth a fault in the night's pass
            }
        }
    }
}
