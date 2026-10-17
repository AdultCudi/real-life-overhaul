using System;

namespace RealLifeOverhaul.Economy.Dealership
{
    /// <summary>The money of a car sold: what comes in today, and what is left to come.</summary>
    public struct DealMoney
    {
        public int Price;           // what the buyer pays for the car, all in
        public int Allowance;       // what their own car is taken in part exchange for
        public int Deposit;         // their own money today (all of it for a cash sale), less the part exchange
        public int Financed;        // lent by the dealership
        public int Interest;        // over the plan's whole term, if every instalment is paid
        public int Weekly;          // the instalment
        public int Weeks;
        public int RateBp;
        public int Premium;         // a warranty's
        public bool OnFinance;
    }

    /// <summary>What a week of a finance plan came to.</summary>
    public enum PlanWeek { Paid, CaughtUp, Missed, Defaulted, PaidOff }

    public static partial class DealerRules
    {
        /// <summary>True when the dealership lends to a buyer with this rating: from the banks' own threshold up.</summary>
        public static bool Lends(int score)
        {
            return score >= CreditCatalogue.LendingThreshold;
        }

        /// <summary>The yearly rate (hundredths of a per cent) the city's banks ask for a car loan at this rating, and the dealership's points.</summary>
        public static int RateBp(int score)
        {
            return ProductTerms.FixedRateBp(score, DealerCatalogue.OwnRate, ProductCatalogue.CarRatePoints, 1f);
        }

        /// <summary>A sale's money: cash less the part exchange, or a deposit (the part exchange counts) and the rest lent as banks reckon it.</summary>
        public static DealMoney Settle(int price, int allowance, bool onFinance, int depositPercent, int termDays, int score, int premium, int daysPerYear)
        {
            var m = new DealMoney { Price = price, Allowance = Math.Max(0, Math.Min(price, allowance)), Premium = Math.Max(0, premium) };
            if (!onFinance)
            {
                m.Deposit = price - m.Allowance;
                return m;
            }
            m.OnFinance = true;
            m.RateBp = RateBp(score);
            int days = Math.Max(DealerCatalogue.WeekDays, termDays);
            FinanceQuote q = ProductTerms.Quote(price, DealerCatalogue.Clamp(depositPercent, 0, 90), price, days, m.RateBp, Math.Max(8, daysPerYear));
            int own = q.Deposit;
            m.Financed = q.Financed;
            if (m.Allowance > own)
            {
                m.Financed = Math.Max(0, m.Financed - (m.Allowance - own));   // a part exchange worth more than the deposit lends less
                q = ProductTerms.Quote(m.Financed, 0, m.Financed, days, m.RateBp, Math.Max(8, daysPerYear));
            }
            m.Deposit = Math.Max(0, price - m.Financed - m.Allowance);
            m.Interest = m.Financed > 0 ? q.Interest : 0;
            m.Weeks = (days + DealerCatalogue.WeekDays - 1) / DealerCatalogue.WeekDays;
            m.Weekly = AccountRules.InstalmentFor(m.Financed + m.Interest, m.Weeks);
            if (m.Financed <= 0)
            {
                m.OnFinance = false;
                m.Interest = m.Weekly = m.Weeks = 0;
            }
            return m;
        }

        /// <summary>A plan's week: paid, caught up, missed (likelier with a poorer rating or <paramref name="economy"/>), or the car taken back.</summary>
        public static PlanWeek Week(DealerPlan p, int seed, int economy, out int paid)
        {
            paid = 0;
            if (p.Owed <= 0)
            {
                return PlanWeek.PaidOff;
            }
            var dice = new DealerDice(seed, p.Id, p.Due, 61);
            int grade = (int)CreditCatalogue.GradeOf(p.Score);
            int miss = DealerCatalogue.MissChance[DealerCatalogue.Clamp(grade, 0, DealerCatalogue.MissChance.Length - 1)] * Math.Max(0, economy) / 1000;
            p.Due += DealerCatalogue.WeekDays;
            if (dice.Chance(miss))
            {
                p.Missed++;
                return p.Missed >= DealerCatalogue.MissesToDefault ? PlanWeek.Defaulted : PlanWeek.Missed;
            }
            int weeks = 1;
            bool caught = false;
            if (p.Missed > 0)
            {
                caught = dice.Chance(DealerCatalogue.CatchUpChance);
                weeks += caught ? p.Missed : 0;
                p.Missed = caught ? 0 : p.Missed;
            }
            paid = (int)Math.Min(p.Owed, (long)p.Weekly * weeks);
            p.Owed -= paid;
            p.Paid += paid;
            return p.Owed <= 0 ? PlanWeek.PaidOff : caught ? PlanWeek.CaughtUp : PlanWeek.Paid;
        }

        /// <summary>What a car taken back fetches at the trade auction.</summary>
        public static int Recovery(int worth)
        {
            return DealerCatalogue.Named(DealerCatalogue.Share(worth, DealerCatalogue.RecoverShare));
        }

        /// <summary>The warranty a buyer takes: the tier they want where the showroom offers one and a cover can still be kept; -1 for none.</summary>
        public static int Tier(bool offered, int wanted, DealerState s)
        {
            return offered && s.Covers.Count < DealerCatalogue.CoversMost ? wanted : -1;
        }

        /// <summary>
        /// What a sale brings in on the day: the deposit, the part exchange, a warranty, the extras, and the sum financed when it
        /// is sold on at once because no plan can be kept (<paramref name="planKept"/> false).
        /// </summary>
        public static int TakenIn(in DealMoney m, bool planKept, int extras)
        {
            return m.Deposit + m.Allowance + m.Premium + extras + (m.OnFinance && !planKept ? m.Financed : 0);
        }

        /// <summary>A warranty's premium on a car of this price, at least the least at today's prices.</summary>
        public static int Premium(int price, int tier, int leastAtPrices)
        {
            if (tier < 0 || tier >= DealerCatalogue.Tiers)
            {
                return 0;
            }
            return Math.Max(leastAtPrices, DealerCatalogue.Named(DealerCatalogue.Share(price, DealerCatalogue.TierPremium[tier])));
        }

        /// <summary>A warranty's day: now and then a claim, likelier in poorer condition; none while resting or done. The cost, or 0.</summary>
        public static int Claim(DealerCover c, int day, int seed, int floorAtPrices, int ceilingAtPrices)
        {
            if (day > c.Until || day <= c.RestUntil || c.Claims >= DealerCatalogue.ClaimsMost || c.Tier < 0 || c.Tier >= DealerCatalogue.Tiers)
            {
                return 0;
            }
            var dice = new DealerDice(seed, c.Id, day, 71);
            int wear = 1000 + Math.Max(0, 1000 - c.Condition) * 2;   // twice as likely at condition 500
            if (!dice.Chance(DealerCatalogue.ClaimChance[c.Tier] * wear / 1000))
            {
                return 0;
            }
            int share = dice.Range(DealerCatalogue.ClaimLeast, DealerCatalogue.ClaimMost);
            int cost = DealerCatalogue.Clamp(DealerCatalogue.Named(DealerCatalogue.Share(c.Worth, share)), floorAtPrices, Math.Max(floorAtPrices, ceilingAtPrices));
            c.Claims++;
            c.Paid += cost;
            c.RestUntil = day + DealerCatalogue.ClaimRest;
            return cost;
        }
    }
}
