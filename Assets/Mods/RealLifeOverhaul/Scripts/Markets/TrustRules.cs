using System;
using RealLifeOverhaul.Economy;

namespace RealLifeOverhaul.Markets
{
    /// <summary>
    /// PROPERTY TRUSTS (MarketSector.Trusts): listed companies that own buildings and let them, pay out nearly all they
    /// earn (MarketCatalogue.TrustPayout), and pay it in small sums often. Game-free.
    ///
    /// THE SCHEDULE. A trust's calendar is cut into periods of its CompanyDef.PayDays days from day 1 (a week, Monday to
    /// Sunday, or three days). Each period's record day is its last market day; holders at that close are owed the
    /// period's distribution, paid on the next market day, and the price drops by it at the open after the record day,
    /// as a quarterly dividend does. At that same open the next one is declared (<see cref="Declare"/>), so the existing
    /// dividend fields carry it (DeclaredCents, RecordDay, PayDay, ExDone, LastExDay): every holder, the trading bots'
    /// shares among them, is owed it at the record day by MarketEngine.Dividends as for any company, and the price builds
    /// it up by the days of the calendar from the last record day to its own (MarketLife.Accrued), so a record day can
    /// be the very next market day and the price still holds all of it. A period whose record day had already gone when
    /// it was declared (a long gap) is paid with the next one: nothing of the year's rate is skipped.
    ///
    /// HOW MUCH. The board sets its quarterly dividend at its results as every company does (DpsCents, from what it can
    /// pay out); a period's share is that dividend's year spread by the days, the fractions carried from one period to the
    /// next (<see cref="Cents"/>), so over a year a trust pays its quarterly rate four times, whatever the length of the
    /// year, and a price never jumps when the rate is set. So a trust's yield is its earnings' at a payout of nine tenths:
    /// no more than its price makes up for (a fair bet, as every share is), and buying before a record day and selling
    /// after earns the distribution less the drop it makes in the price, and pays the spread and commission twice.
    ///
    /// ITS PRICE follows where the city's property market is heading: Economy/Estate's own rule
    /// (EstateRules.RealTarget: the cycle less what the bank rate takes off) as the market expects the cycle to go,
    /// for <see cref="MarketCatalogue.TrustPropertyShare"/> of its worth (<see cref="PropertyFactor"/>); its rents
    /// follow prices, households' income and the council's rent policy (MarketInputs). Both are read from the market's
    /// own city of the day (MarketEnv, kept on the tape), so it is the same on every machine of a Going Public session.
    ///
    /// The cost: a few comparisons for each trust at each open; nothing per frame.
    /// </summary>
    public static class TrustRules
    {
        /// <summary>True for a property trust: a company of the catalogue that pays every few days.</summary>
        public static bool Is(Stock s)
        {
            return s != null && s.Own == null && s.Def != null && s.Def.PayDays > 0;
        }

        private static int Every(Stock s)
        {
            return Math.Max(MarketCatalogue.TrustThreeDays, s.Def.PayDays);   // (three days at least: every period then has a market day)
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

        /// <summary>
        /// A period's distribution, in cents a share: the quarterly dividend's year shared by the days, a period's share
        /// rounded so that the fractions carry on into the next (the sums over any run of periods are the year's rate to
        /// the cent).
        /// </summary>
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
        /// At a market day's open, once the last distribution has gone out of the price: the next is declared for the
        /// first period whose record day is today or later (after today with <paramref name="today"/> false), with any
        /// period between whose record day had already gone, and the price builds it up from the last period's record
        /// day (LastExDay holds that day for a trust: MarketLife.Accrued). Nothing while one waits for its record day.
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
                from = next;   // (a first declaration, one re-declared that had nothing, or a record day of another schedule)
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
                s.DeclaredCents = 0;   // (nothing this period: its record day is kept, so the next one starts after it)
                s.PayDay = 0;
                s.ExDone = true;
                return;
            }
            s.DeclaredCents = cents;
            s.PayDay = MarketTrading.NextTradingDay(record);
            s.ExDone = false;
        }

        /// <summary>A trust made (a new market, a listing, a save from before trusts): its first distribution declared for a record day after today.</summary>
        public static void Begin(Stock s, int day, int year)
        {
            s.DeclaredCents = 0;
            s.RecordDay = 0;
            s.PayDay = 0;
            s.ExDone = true;
            Declare(s, day, year, false);
        }

        /// <summary>
        /// What the property market does to a trust's worth: its buildings (<see cref="MarketCatalogue.TrustPropertyShare"/>
        /// of it) valued where Economy/Estate's market is heading in each state of the economy, by the market's odds of
        /// the next state (MarketCatalogue.Expected), at the bank rate of the day. 1 at the long-run level.
        /// </summary>
        public static double PropertyFactor(in MarketEnv env)
        {
            int quarters = (int)Math.Round((env.RateBp - 300) / 25.0);   // (the city's rate in quarter points over the banks' own, as Estate reads it)
            int now = env.Cycle < 0 ? 0 : env.Cycle > 3 ? 3 : env.Cycle;
            double next = 0.0;
            for (int state = 0; state < 4; state++)
            {
                next += MarketCatalogue.CycleNext[now][state] * EstateRules.RealTarget(state, quarters);
            }
            double level = (MarketCatalogue.CycleNowWeight * EstateRules.RealTarget(now, quarters) + (1.0 - MarketCatalogue.CycleNowWeight) * next / 100.0) / EstateCatalogue.RealStart;
            return 1.0 + MarketCatalogue.TrustPropertyShare * (level - 1.0);
        }

        /// <summary>A trust's distributions over a year at its rate now, in cents a share (its quarterly dividend four times).</summary>
        public static int YearCents(Stock s)
        {
            return Math.Max(0, s.DpsCents) * 4;
        }
    }
}
