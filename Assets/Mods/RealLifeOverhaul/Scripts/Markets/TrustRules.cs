using System;
using RealLifeOverhaul.Economy;

namespace RealLifeOverhaul.Markets
{
    /// <summary>
    /// Property trusts: listed companies that let buildings, pay out nearly all they earn and pay it every few days. Game-free.
    /// Each period of CompanyDef.PayDays days has its last market day as record day, and the next distribution is declared at
    /// the open its last leaves the price, carried in the usual dividend fields so every holder is owed it as for any
    /// company. Over a year a trust pays its quarterly rate four times whatever the year's length. Its price follows where
    /// Economy/Estate's property market is heading (<see cref="PropertyFactor"/>), read from the day's MarketEnv so every
    /// machine agrees. A few comparisons for each trust at each open.
    /// </summary>
    public static class TrustRules
    {
        /// <summary>True for a property trust: a catalogue company that pays every few days.</summary>
        public static bool Is(Stock s)
        {
            return s != null && s.Own == null && s.Def != null && s.Def.PayDays > 0;
        }

        private static int Every(Stock s)
        {
            return Math.Max(MarketCatalogue.TrustThreeDays, s.Def.PayDays);   // three days at least, so every period has a market day
        }

        /// <summary>The period a day is in: period n has the days n * every + 1 to (n + 1) * every.</summary>
        public static int Period(int day, int every)
        {
            return MarketCatalogue.FloorDiv(day - 1, Math.Max(1, every));
        }

        /// <summary>A period's record day: its last market day.</summary>
        public static int RecordDay(int period, int every)
        {
            int day = (period + 1) * every;
            while (!MarketCatalogue.TradingDay(day))
            {
                day--;
            }
            return day;
        }

        /// <summary>A period's distribution in cents a share, rounded so the fractions carry on and any run of periods sums to the year's rate.</summary>
        public static int Cents(int quarterlyCents, int period, int every, int year)
        {
            if (quarterlyCents <= 0)
            {
                return 0;
            }
            double each = quarterlyCents * 4.0 * every / Math.Max(8, year);
            return Math.Max(0, (int)(Math.Floor((period + 1) * each) - Math.Floor(period * each)));
        }

        /// <summary>
        /// At an open, once the last distribution has left the price: the next declared for the first period whose record day
        /// is today or later (after today when <paramref name="today"/> is false), with any period between whose record day
        /// had gone. Nothing while one waits for its record day.
        /// </summary>
        public static void Declare(Stock s, int day, int year, bool today = true)
        {
            if (s.DeclaredCents > 0 && !s.ExDone)
            {
                return;
            }
            int every = Every(s);
            int next = Period(day, every);
            while (today ? RecordDay(next, every) < day : RecordDay(next, every) <= day)
            {
                next++;
            }
            int from = s.RecordDay > 0 ? Period(s.RecordDay, every) + 1 : next;
            if (from > next || from < next - 8)
            {
                from = next;   // a first declaration, one with nothing, or another schedule's record day
            }
            int cents = 0;
            for (int p = from; p <= next; p++)
            {
                cents += Cents(s.DpsCents, p, every, year);
            }
            int record = RecordDay(next, every);
            s.RecordDay = record;
            s.LastExDay = RecordDay(from - 1, every);
            if (cents <= 0)
            {
                s.DeclaredCents = 0;   // nothing this period: its record day is kept, so the next starts after it
                s.PayDay = 0;
                s.ExDone = true;
                return;
            }
            s.DeclaredCents = cents;
            s.PayDay = MarketTrading.NextTradingDay(record);
            s.ExDone = false;
        }

        /// <summary>A trust made: its first distribution declared for a record day after today.</summary>
        public static void Begin(Stock s, int day, int year)
        {
            s.DeclaredCents = 0;
            s.RecordDay = 0;
            s.PayDay = 0;
            s.ExDone = true;
            Declare(s, day, year, false);
        }

        /// <summary>
        /// What the property market does to a trust's worth: its buildings' share valued where Economy/Estate's market is heading
        /// by the market's odds of the next state, at the day's bank rate. 1 at the long-run level.
        /// </summary>
        public static double PropertyFactor(in MarketEnv env)
        {
            int quarters = (int)Math.Round((env.RateBp - 300) / 25.0);   // the city's rate in quarter points over the banks' own, as Estate reads it
            int now = env.Cycle < 0 ? 0 : env.Cycle > 3 ? 3 : env.Cycle;
            double next = 0.0;
            for (int state = 0; state < 4; state++)
            {
                next += MarketCatalogue.CycleNext[now][state] * EstateRules.RealTarget(state, quarters);
            }
            double level = (MarketCatalogue.CycleNowWeight * EstateRules.RealTarget(now, quarters) + (1.0 - MarketCatalogue.CycleNowWeight) * next / 100.0) / EstateCatalogue.RealStart;
            return 1.0 + MarketCatalogue.TrustPropertyShare * (level - 1.0);
        }

        /// <summary>A trust's distributions over a year at its rate now, in cents a share.</summary>
        public static int YearCents(Stock s)
        {
            return Math.Max(0, s.DpsCents) * 4;
        }
    }
}
