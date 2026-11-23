using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Markets
{
    /// <summary>
    /// Why a sector or a large company moved, from what the engine itself applied that day. Saved as a number in a piece of news (<see cref="MarketNews.A"/> low byte): never
    /// renumber, add at the end.
    /// </summary>
    public enum WhyCause
    {
        None = 0,
        Crash = 1,        // the whole market fell hard at the open
        Rate = 2,         // the bank rate changed: the discount the market's prices use; detail the change in bp
        Election = 3,     // an election was counted: the doubt was over
        Craze = 4,        // a craze for the sector is running
        CrazeBegun = 5,   // ...began this morning
        CrazeBurst = 6,   // ...burst this morning
        Weather = 7,      // the day's sky: detail 1 rain, 2 a storm
        Spell = 8,        // a heat wave (1), a cold snap (2) or ice (3)
        Street = 9,       // a street event is on: detail its number (1 to 7)
        Crime = 10,       // crimes reported yesterday
        BreakIn = 11,     // break-ins reported yesterday
        Fire = 12,        // fires reported yesterday, or heard of today
        Accident = 13,    // accidents reported yesterday, or heard of today
        Buzz = 14,        // the city talks about the sector's trade
        Policy = 15,      // the council changed a policy the sector depends on: detail the lever, plus 100 if tightened
        Economy = 16,     // the city's economy turned: detail the new state (0 boom, 1 steady, 2 slowdown, 3 recession)
        Inflation = 17,   // inflation moved: detail the change in bp
        Prices = 18,      // the prices of the goods the sector deals in moved
        Incomes = 19,     // the districts' incomes moved, and with them what people spend
        Results = 20,     // its results came out: detail 1 beat what was expected, 0 missed it
        Crisis = 21,      // a crisis at the company
        Windfall = 22,    // a windfall at the company
        Bid = 23,         // a takeover bid for it
        Builder = 24,     // news of its building work: detail the kind (BuilderNewsKind)
        Count
    }

    /// <summary>The kinds of the lines derived on each machine (<see cref="MarketState.Derived"/>): beyond every kind of <see cref="MarketNewsKind"/>, so none is a new kind on the tape.</summary>
    public static class DerivedNews
    {
        public const MarketNewsKind Why = (MarketNewsKind)100;       // Stock (0 for a sector), A the cause (WhyCause) with the sector above it, B the move in bp, C the cause's detail
        public const MarketNewsKind Segment = (MarketNewsKind)101;   // Stock, A the new segment, B the old, C its market value in millions
    }

    /// <summary>One cause picked: what it was, its detail and the move it applied, in basis points.</summary>
    public struct WhyPick
    {
        public WhyCause Cause;
        public int Detail;
        public int Bp;
    }

    /// <summary>
    /// What the engine applied to each sector today, written down as it is applied (an event heard of, a policy priced, an election counted) so the close can name it. Cleared each
    /// open. Never saved: a load in the middle of a day names fewer causes, never one that was not applied. COST: a few array writes an event.
    /// </summary>
    public sealed class WhyDay
    {
        private const int Sectors = (int)MarketSector.Count, Causes = (int)WhyCause.Count;

        public int Day;
        public bool Election;
        public readonly int[] EventBp = new int[Sectors * Causes];   // each sector's move by an event heard of today, by cause
        public readonly double[] PolicyLn = new double[Sectors];     // what a policy priced today changed the worth of the sector's companies by (ln), summed
        public readonly int[] PolicyN = new int[Sectors];
        public int PolicyLever = -1, PolicyDirection;

        public void Clear(int day)
        {
            Day = day;
            Election = false;
            Array.Clear(EventBp, 0, EventBp.Length);
            Array.Clear(PolicyLn, 0, PolicyLn.Length);
            Array.Clear(PolicyN, 0, PolicyN.Length);
            PolicyLever = -1;
            PolicyDirection = 0;
        }

        public void Event(int sector, WhyCause cause, int bp)
        {
            if (sector >= 0 && sector < Sectors && cause > WhyCause.None && cause < WhyCause.Count)
            {
                EventBp[sector * Causes + (int)cause] += bp;
            }
        }

        public int PolicyBp(int sector)
        {
            return sector >= 0 && sector < Sectors && PolicyN[sector] > 0 ? (int)Math.Round(PolicyLn[sector] / PolicyN[sector] * 10000.0) : 0;
        }
    }

    /// <summary>
    /// The rules that name a cause. A cause is named only if the engine applied it that day, in the direction the sector or company moved and by at least a fifth of the move
    /// (<see cref="ShareBp"/>): a move the engine drew at random, a mood or a fat tail has no cause and gets no line. Pure functions of the day's record, the market's state and the
    /// city's environment: the same on every machine. Game-free. COST: for the one sector that moved most, a few valuations of its companies.
    /// </summary>
    public static class MarketWhy
    {
        public const int LeastBp = 8;          // a cause under this many bp moved nothing worth naming
        public const int ShareBp = 2000;       // a cause must account for this share of the move, basis points of it: a fifth
        public const int CompanyShareBp = 5000;   // a sector's cause stands for a company's move only if the sector moved this share of it
        public const int CompanyLeastBp = 300;    // a company's move is notable from this many bp (3%)
        public const int SectorLeastBp = 60;      // a sector's move is notable from this many bp

        private const int Sectors = (int)MarketSector.Count, Parts = 7;
        private static readonly int[] Part = new int[Parts];

        /// <summary>A craze's give-back at its burst, in bp: its drift for the days it was expected to last (MarketEngine.Mania).</summary>
        public static int BurstBp()
        {
            double perDay = 1.0 / Math.Max(5.0, (MarketCatalogue.ManiaDaysLeast + MarketCatalogue.ManiaDaysMost) / 2.0);
            return -(int)Math.Round(MarketCatalogue.ManiaDrift / perDay * 10000.0);
        }

        private static void Offer(ref WhyPick best, WhyCause cause, int detail, int bp, int moveBp)
        {
            if (bp == 0 || (bp > 0) != (moveBp > 0))
            {
                return;
            }
            long need = Math.Max(LeastBp, Math.Abs((long)moveBp) * ShareBp / 10000L);
            if (Math.Abs(bp) >= need && Math.Abs(bp) > Math.Abs(best.Bp))
            {
                best = new WhyPick { Cause = cause, Detail = detail, Bp = bp };
            }
        }

        /// <summary>
        /// The cause of a sector's move today, if the engine applied one: a crash, an election, a craze, the day's sky, spell, street event or reports, an event heard of,
        /// a policy priced, or the sector's companies revalued at the open for the city's economy, the bank rate, inflation, goods' prices or incomes.
        /// </summary>
        public static bool ForSector(MarketState s, WhyDay w, in MarketEnv env, in MarketEnv prev, bool hasPrev, int sector, int moveBp, int day, out WhyPick pick)
        {
            pick = default;
            if (s == null || w == null || sector < 0 || sector >= Sectors || moveBp == 0)
            {
                return false;
            }
            for (int i = s.News.Count - 1; i >= 0 && s.News[i].Day >= day; i--)
            {
                MarketNews n = s.News[i];
                if (n.Day != day)
                {
                    continue;
                }
                if (n.Kind == MarketNewsKind.Crash)
                {
                    Offer(ref pick, WhyCause.Crash, 0, n.A, moveBp);
                }
                else if (n.Kind == MarketNewsKind.Mania && n.A == sector)
                {
                    Offer(ref pick, n.B == 1 ? WhyCause.CrazeBegun : WhyCause.CrazeBurst, 0, n.B == 1 ? (int)Math.Round(MarketCatalogue.ManiaDrift * 10000.0) : BurstBp(), moveBp);
                }
            }
            if (s.ManiaDays[sector] > 0)
            {
                Offer(ref pick, WhyCause.Craze, 0, (int)Math.Round(MarketCatalogue.ManiaDrift * 10000.0), moveBp);
            }
            if (w.Election)
            {
                Offer(ref pick, WhyCause.Election, 0, MarketInputs.ElectionKickBp, moveBp);
            }
            MarketInputs.DayKickParts(env, (MarketSector)sector, Part);
            Offer(ref pick, WhyCause.Weather, env.Sky, Part[0], moveBp);
            Offer(ref pick, WhyCause.Spell, env.Spell, Part[1], moveBp);
            Offer(ref pick, WhyCause.Street, env.Event, Part[2], moveBp);
            Offer(ref pick, WhyCause.Crime, 0, Part[3], moveBp);
            Offer(ref pick, WhyCause.BreakIn, 0, Part[4], moveBp);
            Offer(ref pick, WhyCause.Fire, 0, Part[5], moveBp);
            Offer(ref pick, WhyCause.Accident, 0, Part[6], moveBp);
            for (WhyCause c = WhyCause.Weather; c < WhyCause.Count; c++)
            {
                int heard = w.EventBp[sector * (int)WhyCause.Count + (int)c];
                if (heard != 0)
                {
                    Offer(ref pick, c, c == WhyCause.Weather ? 2 : 0, heard, moveBp);
                }
            }
            if (w.PolicyLever >= 0)
            {
                Offer(ref pick, WhyCause.Policy, w.PolicyLever + (w.PolicyDirection > 0 ? 100 : 0), w.PolicyBp(sector), moveBp);
            }
            if (hasPrev)
            {
                Revalued(s, env, prev, sector, moveBp, ref pick);
            }
            return pick.Cause != WhyCause.None;
        }

        // What each change of the city between yesterday and today was worth to the sector's companies' worth, by valuing them with that one part of today's city put back as
        // it was: the most of them, in the direction of the move, is the cause. The valuation is the one the engine prices them by.
        private static void Revalued(MarketState s, in MarketEnv env, in MarketEnv prev, int sector, int moveBp, ref WhyPick pick)
        {
            int count = 0;
            for (int i = 0; i < s.Stocks.Count; i++)
            {
                Stock k = s.Stocks[i];
                count += k.Listed && k.Own == null && (int)k.Sector == sector && k.BidCents <= 0 ? 1 : 0;
            }
            if (count == 0)
            {
                return;
            }
            if (env.Cycle != prev.Cycle)
            {
                MarketEnv was = env;
                was.Cycle = prev.Cycle;
                Offer(ref pick, WhyCause.Economy, env.Cycle, Worth(s, sector, env, was), moveBp);
            }
            if (env.RateBp != prev.RateBp || env.RateNextBp != prev.RateNextBp)
            {
                MarketEnv was = env;
                was.RateBp = prev.RateBp;
                was.RateNextBp = prev.RateNextBp;
                Offer(ref pick, WhyCause.Rate, env.RateBp - prev.RateBp, Worth(s, sector, env, was), moveBp);
            }
            if (env.InflationBp != prev.InflationBp)
            {
                MarketEnv was = env;
                was.InflationBp = prev.InflationBp;
                Offer(ref pick, WhyCause.Inflation, env.InflationBp - prev.InflationBp, Worth(s, sector, env, was), moveBp);
            }
            if (!SameSupply(env.Supply, prev.Supply))
            {
                MarketEnv was = env;
                was.Supply = prev.Supply;
                Offer(ref pick, WhyCause.Prices, 0, Worth(s, sector, env, was), moveBp);
            }
            if (env.DistrictIncome != prev.DistrictIncome)
            {
                MarketEnv was = env;
                was.DistrictIncome = prev.DistrictIncome;
                Offer(ref pick, WhyCause.Incomes, env.DistrictIncome - prev.DistrictIncome, Worth(s, sector, env, was), moveBp);
            }
        }

        private static bool SameSupply(int[] a, int[] b)
        {
            if (a == null || b == null || a.Length != b.Length)
            {
                return a == b;
            }
            for (int i = 0; i < a.Length; i++)
            {
                if (a[i] != b[i])
                {
                    return false;
                }
            }
            return true;
        }

        // The mean change in the sector's companies' worth, in bp, between a city and the same city with one part as it was.
        private static int Worth(MarketState s, int sector, in MarketEnv now, in MarketEnv was)
        {
            double sum = 0.0;
            int count = 0;
            for (int i = 0; i < s.Stocks.Count; i++)
            {
                Stock k = s.Stocks[i];
                if (!k.Listed || k.Own != null || (int)k.Sector != sector || k.BidCents > 0)
                {
                    continue;
                }
                double a = MarketValuation.Fair(k, now), b = MarketValuation.Fair(k, was);
                if (a > 0.0 && b > 0.0)
                {
                    sum += Math.Log(a / b);
                    count++;
                }
            }
            return count > 0 ? (int)Math.Round(sum / count * 10000.0) : 0;
        }

        /// <summary>
        /// The cause of a company's move today: its own results, a crisis or windfall, a takeover bid or its building work, in the news of today; else the cause of its sector's
        /// move, if the sector moved by at least half as much the same way (<paramref name="sectorPick"/>, <paramref name="sectorBp"/>).
        /// </summary>
        public static bool ForStock(MarketState s, Stock k, int moveBp, int day, bool sectorGiven, in WhyPick sectorPick, int sectorBp, out WhyPick pick)
        {
            pick = default;
            if (s == null || k == null || moveBp == 0)
            {
                return false;
            }
            for (int i = s.News.Count - 1; i >= 0 && s.News[i].Day >= day; i--)
            {
                MarketNews n = s.News[i];
                if (n.Day != day || n.Stock != k.Id)
                {
                    continue;
                }
                switch (n.Kind)
                {
                    case MarketNewsKind.Results: Offer(ref pick, WhyCause.Results, n.A >= n.B ? 1 : 0, n.C, moveBp); break;
                    case MarketNewsKind.Crisis: Offer(ref pick, WhyCause.Crisis, 0, -Math.Abs(n.A), moveBp); break;
                    case MarketNewsKind.Windfall: Offer(ref pick, WhyCause.Windfall, 0, Math.Abs(n.A), moveBp); break;
                    case MarketNewsKind.Bid: Offer(ref pick, WhyCause.Bid, 0, moveBp > 0 ? moveBp : 0, moveBp); break;   // a bid lifts the price: a fall is not the bid's
                    case MarketNewsKind.Builder: Offer(ref pick, WhyCause.Builder, n.A, n.B, moveBp); break;
                }
            }
            if (pick.Cause == WhyCause.None && sectorGiven && (sectorBp > 0) == (moveBp > 0) && Math.Abs((long)sectorBp) * 10000L >= Math.Abs((long)moveBp) * CompanyShareBp)
            {
                pick = sectorPick;
            }
            return pick.Cause != WhyCause.None;
        }

        /// <summary>A piece of news saying why: the cause in the low byte of A and the sector above it, B the move in bp, C the cause's detail. Stock is 0 for a sector.</summary>
        public static MarketNews News(int day, int stock, int sector, int moveBp, in WhyPick pick)
        {
            return new MarketNews { Day = day, Kind = DerivedNews.Why, Stock = stock, A = (int)pick.Cause | (sector << 8), B = moveBp, C = pick.Detail };
        }

        public static WhyCause CauseOf(in MarketNews n) => (WhyCause)Math.Max(0, Math.Min((int)WhyCause.Count - 1, n.A & 0xFF));

        public static int SectorOf(in MarketNews n) => Math.Max(0, Math.Min(Sectors - 1, n.A >> 8));
    }
}
