using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Economy
{
    /// <summary>Marketing: a campaign's cost, the customers it adds, and its report. Game-free.</summary>
    public static class CampaignRules
    {
        /// <summary>One day of a business in the game's books. False if the game has no figures for that day.</summary>
        public delegate bool DayBook(int day, out int customers, out int takings, out int profit);

        public static int Cost(int kind, int index)
        {
            return CompanyCatalogue.AtPrices(CompanyCatalogue.CampaignCost[kind], index);
        }

        /// <summary>
        /// How well the district suits a local campaign, thousandths: a business selling to the well-off (taste +1)
        /// does better where incomes are high, one selling on price (-1) where they are low. Otherwise 1000.
        /// </summary>
        public static int Suit(int kind, int taste, int income)
        {
            if (!CompanyCatalogue.CampaignLocal[kind] || taste == 0)
            {
                return 1000;
            }
            return CompanyCatalogue.Clamp(1000 + (taste > 0 ? 1 : -1) * (income - CompanyCatalogue.IncomeMiddle) * CompanyCatalogue.SuitPerPoint,
                CompanyCatalogue.SuitLeast, CompanyCatalogue.SuitMost);
        }

        /// <summary>What a kind repeated too soon keeps, thousandths: half the day after, all after ten days.</summary>
        public static int Repeat(int ended, int from)
        {
            if (ended <= 0)
            {
                return 1000;
            }
            int since = Math.Max(0, from - ended);
            return since >= CompanyCatalogue.RepeatDays ? 1000 : 1000 - CompanyCatalogue.RepeatLoss * (CompanyCatalogue.RepeatDays - since) / CompanyCatalogue.RepeatDays;
        }

        public static int Effect(int kind, int suit, CycleState cycle, int repeat)
        {
            return Effect(kind, suit, cycle, repeat, 1000);
        }

        /// <summary>The customers a campaign adds, per cent; reach from outside is in thousandths.</summary>
        public static int Effect(int kind, int suit, CycleState cycle, int repeat, int reach)
        {
            int state = CompanyCatalogue.Clamp((int)cycle, 0, CompanyCatalogue.CampaignCycle.Length - 1);
            long effect = (long)CompanyCatalogue.CampaignFootfall[kind] * suit * CompanyCatalogue.CampaignCycle[state] / 1000 * repeat / 1000
                          * Math.Max(0, reach) / 1000;   // thousandths of a per cent
            return CompanyCatalogue.Clamp((int)((effect + 500) / 1000), 1, CompanyCatalogue.CampaignMost);
        }

        public static bool CanBook(ShopFile s)
        {
            return s.Push < 0;
        }

        /// <summary>What is booked at a business, however booked: false with nothing, else its kind and days.</summary>
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

        /// <summary>An influencer's deal, hired on Flare (a sponsored post is booked in the Company app).</summary>
        public static bool HiredOnFlare(int kind)
        {
            return kind >= (int)CampaignKind.InfluencerPost && kind < CompanyCatalogue.CampaignKinds;
        }

        /// <summary>
        /// A campaign cut short on Flare. Not started, it is taken off with no report and no repeat counted (true);
        /// started, it runs to the end of today and is reported as usual.
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

        public static int Discount(int takings)
        {
            return CompanyCatalogue.Share(Math.Max(0, takings), CompanyCatalogue.DiscountShare);
        }

        public static int Footfall(ShopFile s, int day)
        {
            return s.Pushing(day) ? s.PushPercent : 0;
        }

        /// <summary>The report: its days against as many before; a day without figures counts in neither.</summary>
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
