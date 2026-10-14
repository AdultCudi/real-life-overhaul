using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// Marketing by rule: what a campaign costs, how many customers it adds where and when, and what it did, measured
    /// from the game's own daily figures. Game-free.
    /// </summary>
    public static class CampaignRules
    {
        /// <summary>One day of a business in the game's books. False if the game has no figures for that day.</summary>
        public delegate bool DayBook(int day, out int customers, out int takings, out int profit);

        public static int Cost(int kind, int index)
        {
            return CompanyCatalogue.AtPrices(CompanyCatalogue.CampaignCost[kind], index);
        }

        /// <summary>
        /// How well the district suits the business, thousandths, for a local campaign: a business that sells to the
        /// well-off (taste +1) does better where incomes are high, one that sells on price (-1) where they are low.
        /// A city-wide campaign and a business of neither kind are at 1000.
        /// </summary>
        public static int Suit(int kind, int taste, int income)
        {
            if (!CompanyCatalogue.CampaignLocal[kind] || taste == 0)
            {
                return 1000;
            }
            return CompanyCatalogue.Clamp(1000 + (taste > 0 ? 1 : -1) * (income - CompanyCatalogue.IncomeMiddle) * CompanyCatalogue.SuitPerPoint, CompanyCatalogue.SuitLeast, CompanyCatalogue.SuitMost);
        }

        /// <summary>What repeating the same kind too soon leaves of it, thousandths: half the day after the last one ended, all of it again after ten days.</summary>
        public static int Repeat(int ended, int from)
        {
            if (ended <= 0)
            {
                return 1000;
            }
            int since = Math.Max(0, from - ended);
            return since >= CompanyCatalogue.RepeatDays ? 1000 : 1000 - CompanyCatalogue.RepeatLoss * (CompanyCatalogue.RepeatDays - since) / CompanyCatalogue.RepeatDays;
        }

        /// <summary>The customers a campaign adds, per cent: its own figure by the district, the economy and repeats; one at least, never over the most.</summary>
        public static int Effect(int kind, int suit, CycleState cycle, int repeat)
        {
            return Effect(kind, suit, cycle, repeat, 1000);
        }

        /// <summary>The same for a campaign whose reach is given from outside, thousandths of the catalogue's figure (a sponsored post: by the account's followers).</summary>
        public static int Effect(int kind, int suit, CycleState cycle, int repeat, int reach)
        {
            int state = CompanyCatalogue.Clamp((int)cycle, 0, CompanyCatalogue.CampaignCycle.Length - 1);
            long effect = (long)CompanyCatalogue.CampaignFootfall[kind] * suit * CompanyCatalogue.CampaignCycle[state] / 1000 * repeat / 1000 * Math.Max(0, reach) / 1000;   // thousandths of a per cent
            return CompanyCatalogue.Clamp((int)((effect + 500) / 1000), 1, CompanyCatalogue.CampaignMost);
        }

        public static bool CanBook(ShopFile s)
        {
            return s.Push < 0;
        }

        /// <summary>
        /// What stands in the way of booking at a business: false with nothing booked there; otherwise the kind, its
        /// first and last day, and whether it has started by the day given. One campaign at a time at a business, of
        /// any kind, booked from the Company app or hired on Flare.
        /// </summary>
        public static bool Busy(ShopFile s, int day, out int kind, out int from, out int to, out bool started)
        {
            kind = s != null ? s.Push : -1;
            from = to = 0;
            started = false;
            if (kind < 0)
            {
                kind = -1;
                return false;
            }
            from = s.PushFrom;
            to = Math.Max(s.PushFrom, s.PushEnd - 1);
            started = day >= s.PushFrom;
            return true;
        }

        /// <summary>
        /// Whether a kind of campaign is hired on Flare (an influencer's deal) rather than booked from the Company app.
        /// A sponsored post is priced by Flare but booked from the Company app's Marketing tab.
        /// </summary>
        public static bool HiredOnFlare(int kind)
        {
            return kind >= (int)CampaignKind.InfluencerPost && kind < CompanyCatalogue.CampaignKinds;
        }

        /// <summary>
        /// A campaign cut short (an influencer's deal the player ended on Flare). One that has not started is taken
        /// off at once, with no report and nothing counted as a repeat, and true is returned; one that has started
        /// runs to the end of today and is reported as usual once it is over. With nothing booked, nothing happens.
        /// </summary>
        public static bool EndEarly(ShopFile s, int day)
        {
            if (s.Push < 0)
            {
                return false;
            }
            if (day < s.PushFrom)
            {
                s.Push = -1;
                s.PushFrom = s.PushEnd = s.PushCost = s.PushPercent = 0;
                return true;
            }
            s.PushEnd = Math.Min(s.PushEnd, day + 1);
            return false;
        }

        /// <summary>Booked today, it runs from tomorrow for its days.</summary>
        public static void Book(ShopFile s, int kind, int cost, int percent, int day)
        {
            s.Push = kind;
            s.PushFrom = day + 1;
            s.PushEnd = s.PushFrom + CompanyCatalogue.CampaignDays[kind];
            s.PushCost = Math.Max(0, cost);
            s.PushPercent = CompanyCatalogue.Clamp(percent, 0, CompanyCatalogue.CampaignMost);
        }

        public static bool Over(ShopFile s, int day)
        {
            return s.Push >= 0 && day >= s.PushEnd;
        }

        public static void Close(ShopFile s)
        {
            if (s.Push >= 0 && s.Push < s.PushEnded.Length)
            {
                s.PushEnded[s.Push] = s.PushEnd;
            }
            s.Push = -1;
            s.PushFrom = s.PushEnd = s.PushCost = s.PushPercent = 0;
        }

        /// <summary>What a discount day gives away of the day's takings.</summary>
        public static int Discount(int takings)
        {
            return CompanyCatalogue.Share(Math.Max(0, takings), CompanyCatalogue.DiscountShare);
        }

        public static int Footfall(ShopFile s, int day)
        {
            return s.Pushing(day) ? s.PushPercent : 0;
        }

        /// <summary>
        /// The report: customers, takings and profit over the campaign's days, and over the same number of days before
        /// it, from the game's own books. A day the game has no figures for is left out and counted in neither total.
        /// </summary>
        public static CampaignReport Measure(string name, int kind, int from, int days, int cost, DayBook book)
        {
            var r = new CampaignReport { Name = name ?? string.Empty, Kind = kind, From = from, Days = days, Cost = Math.Max(0, cost) };
            for (int d = from - days; d < from + days; d++)
            {
                int customers, takings, profit;
                if (book == null || !book(d, out customers, out takings, out profit))
                {
                    continue;
                }
                if (d < from)
                {
                    r.DaysBefore++;
                    r.CustomersBefore += customers;
                    r.TakingsBefore += takings;
                    r.ProfitBefore += profit;
                }
                else
                {
                    r.DaysDuring++;
                    r.CustomersDuring += customers;
                    r.TakingsDuring += takings;
                    r.ProfitDuring += profit;
                }
            }
            return r;
        }

        /// <summary>The newest report is kept with the seven before it.</summary>
        public static void Keep(List<CampaignReport> reports, CampaignReport report)
        {
            reports.Add(report);
            while (reports.Count > CompanyCatalogue.ReportsKept)
            {
                reports.RemoveAt(0);
            }
        }
    }
}
