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

        // The game's own rival offers (EmployeeInstance.PoachByRival: a message with Accept and Decline) to the player's staff and professionals, answered
        // through the game's own Accept call, as if pressed, when the option is on and the raise it asks is within the limit of the automatic raises. One
        // pass an hour over the employees (a field read each; nearly none has an offer). Above the limit the buttons stay for the player.
        private static readonly System.Reflection.MethodInfo Accept = GameContract.OfferAccept.Method;

        private static void MatchGameOffers(int day)
        {
            int limit = RivalMatchRules.Limit(Settings.EconomyCompanyAgreeUpTo, Economy.CompanyCatalogue.AgreeUpToMost);
            GameInstance save = SaveGameManager.Current;
            if (Accept == null || !Settings.EconomyCompanyMatchRivals || !Settings.EconomyCompanyStaff || limit <= 0 || save == null || save.EmployeeInstances == null)
            {
                return;
            }
            try
            {
                List<Entities.EmployeeInstance> staff = save.EmployeeInstances;
                bool any = false;
                for (int i = 0; i < staff.Count; i++)
                {
                    Entities.EmployeeInstance e = staff[i];
                    if (!RivalOfferGuard.OfferOpen(e) || RivalOfferGuard.Standing(e, null) != RivalOfferRules.State.Live)
                    {
                        continue;
                    }
                    BuildingRegistration at = Tech.TalentGame.Business(e);
                    if (GoingPublic.InSession && !GoingPublic.Mine(at))
                    {
                        continue;   // another player's staff are theirs to answer
                    }
                    int was = PaidWage.Base(e.hourlyWage), asked = PaidWage.Base(e.poachProposedHourlyWage);
                    if (asked <= 0 || !RivalMatchRules.WithinOffer(was, asked, limit))
                    {
                        continue;
                    }
                    int before = PaidWage.Cents(was, Prices.Level, PaidWage.RaiseOf(e.id));
                    Accept.Invoke(e, null);
                    int after = PaidWage.Cents(PaidWage.Base(e.hourlyWage), Prices.Level, PaidWage.RaiseOf(e.id));
                    Matched(day, RivalMatchRules.Monthly(Math.Max(0, after - before), GameCalendar.MonthDays));
                    any = true;
                }
                if (any)
                {
                    RivalOfferGuard.Look("the game's own offers matched by themselves");
                }
            }
            catch (Exception)
            {
                // an offer that cannot be answered stays for the player, with its buttons
            }
        }

        /// <summary>Called from the hourly pass of the parts that match: says the matches of the night once, from the first hour.</summary>
        public static void Flush(int day, int hour)
        {
            MatchGameOffers(day);
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
