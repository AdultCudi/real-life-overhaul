using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Markets
{
    /// <summary>Why a firm or a startup could not be put on the market now.</summary>
    public enum FirmWhy
    {
        None = 0,
        Off,        // the market is not running here, or this machine follows another player's
        NotMine,    // no player of this machine to own it
        Already,    // it is on the market already
        TooMany,    // as many of the player's firms, or of the city's startups, as the market holds
        Full        // the market holds as many companies as it may
    }

    /// <summary>
    /// A firm of the player's that listed, as the market needs it to make its stock: the firm as the tech sector knows it, in the market's own
    /// figures (whole units of money, a share's price in cents at today's prices). Game-free.
    /// </summary>
    public struct FirmListing
    {
        public string Place, Name;
        public int Sector;                // MarketSector
        public int Trade;                 // MarketInputs.Trades, -1 for none
        public long Shares;               // in issue, every holder's
        public long Founder;              // the player's
        public long Float;                // the public's
        public int PriceCents;            // the price it listed at
        public int LockDay;               // the founder sells nothing before this day
        public int ListedDay;             // the day it first listed; 0 for today (a firm that listed before there was a stock keeps its own day, so no first morning's pop is drawn)
        public long VestBase;             // the founder's shares that were vesting...
        public int VestDay, VestCliff, VestSpan;   // ...from this day, with a cliff and an end this many days on
        public long Debt;                 // money
        public double RevDay, ProfitDay;  // money a day over the last month, for a firm with no complete quarter yet
        public BookQuarter[] Books;       // the firm's own quarters from the game's statements, oldest first (the one under way too)
    }

    /// <summary>A city startup that listed, as the market needs it to make a company of it. Game-free.</summary>
    public struct StartupListing
    {
        public string Name;
        public int Sector;                // MarketSector
        public int RevenueM;              // a year's turnover in millions at the start's prices
        public long Shares;               // in issue
        public int PriceCents;            // the price it listed at, today's
    }

    /// <summary>
    /// A firm of the player's on the market as a company of its own. It is a player's company (<see cref="OwnListing"/>) with the firm's marker: its
    /// founder is the player, its founder's shares and lock-up are the ones the stock keeps, and its daily figures are the firm's statements, one
    /// column of the day's env a firm (<see cref="MarketEnv.FirmStock"/>). It never goes through the way to the market of the player's whole company:
    /// the tech sector's bankers, roadshow and pricing day have done that, and the money of the offer went into the firm's account. Nothing here
    /// moves money but the founder's own deals. Game-free and the same on every machine.
    /// </summary>
    public static class ListingFirm
    {
        /// <summary>A stock id for a firm of a founder, in the player's range, from a hash of both (the engine finds a free one).</summary>
        public static int IdFor(ulong owner, string place)
        {
            ulong h = 14695981039346656037UL;
            string text = place ?? string.Empty;
            for (int i = 0; i < text.Length; i++)
            {
                h = unchecked((h ^ text[i]) * 1099511628211UL);
            }
            return ListingCatalogue.IdFirst + (int)(MarketRandom.Mix64(owner ^ h) % (ulong)ListingCatalogue.IdCount);
        }

        /// <summary>The founder's shares of a firm they can sell now: those that have vested and are not in the lock-up's way.</summary>
        public static long Sellable(OwnListing o, int day)
        {
            return o == null ? 0L : Math.Max(0L, o.Founder - o.Unvested(day));
        }

        /// <summary>
        /// A ticker of three or four capitals from the name that no company of the market has: the name's own first, then its three letters with another
        /// letter, then with a digit. The same for the same name and market.
        /// </summary>
        public static string UniqueTicker(string name, IList<Stock> stocks, int forId)
        {
            string wanted = ListingRules.TickerOf(name);
            if (!Taken(wanted, stocks, forId))
            {
                return wanted;
            }
            string three = wanted.Substring(0, 3);
            for (char c = 'A'; c <= 'Z'; c++)
            {
                if (!Taken(three + c, stocks, forId))
                {
                    return three + c;
                }
            }
            for (int d = 0; d <= 9; d++)
            {
                if (!Taken(three + (char)('0' + d), stocks, forId))
                {
                    return three + (char)('0' + d);
                }
            }
            return wanted;
        }

        private static bool Taken(string ticker, IList<Stock> stocks, int forId)
        {
            for (int i = 0; i < stocks.Count; i++)
            {
                if (stocks[i].Id != forId && string.Equals(stocks[i].Ticker, ticker, StringComparison.Ordinal))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>The day's env as one firm's stock reads it: its own window, takings, profit and debt in the columns the player's whole company has. Nothing counted when it has none.</summary>
        public static MarketEnv EnvFor(in MarketEnv env, int stockId)
        {
            MarketEnv fe = env;
            fe.OwnDays = 0;
            fe.OwnRev = 0;
            fe.OwnProfit = 0;
            fe.OwnDebt = 0;
            for (int i = 0; i < env.Firms && env.FirmStock != null && i < env.FirmStock.Length && i < env.FirmRev.Length && i < env.FirmProfit.Length && i < env.FirmDebt.Length; i++)
            {
                if (env.FirmStock[i] == stockId)
                {
                    fe.OwnFrom = env.FirmFrom;
                    fe.OwnDays = env.FirmDays;
                    fe.OwnRev = env.FirmRev[i];
                    fe.OwnProfit = env.FirmProfit[i];
                    fe.OwnDebt = env.FirmDebt[i];
                    break;
                }
            }
            return fe;
        }

        /// <summary>
        /// The stock of a firm on its first day: made as a player's company is (its price, its results from the books' quarters), with the firm's own shares,
        /// price and lock-up in place of a way to the market's. A firm with no complete quarter yet is followed from the last month's rates until it has one.
        /// </summary>
        public static Stock Build(in FirmListing r, ulong owner, int day, int year, in MarketEnv env, int indexNow, int id, string ticker)
        {
            var f = new ListingFile
            {
                Stage = ListingStage.Listed, Name = r.Name ?? string.Empty, Ticker = ticker, Sector = MarketValuation.Clamp(r.Sector, 0, (int)MarketSector.Count - 1),
                Trade = r.Trade >= 0 && r.Trade < MarketInputs.Trades.Length ? r.Trade : -1, StockId = id, OldShares = Math.Max(0L, r.Founder),
                NewShares = Math.Max(1L, r.Shares - Math.Max(0L, r.Founder)), PriceCents = Math.Max(1, r.PriceCents), ListDay = day
            };
            for (int i = 0; r.Books != null && i < r.Books.Length && f.Books.Count < 12; i++)
            {
                if (r.Books[i].Days > 0)
                {
                    f.Books.Add(r.Books[i]);
                }
            }
            int now = MarketCatalogue.QuarterOf(day, year);
            if (ListingBook.Last(f, day, year, 1, out long _, out long _, out int _) == 0 && r.RevDay > 0.0)
            {
                int days = Math.Max(1, MarketCatalogue.QuarterStart(now, year) - MarketCatalogue.QuarterStart(now - 1, year));
                f.Books.Add(new BookQuarter { Quarter = now - 1, Revenue = (long)Math.Round(r.RevDay * days), Profit = (long)Math.Round(r.ProfitDay * days), Days = days });
                f.Books.Sort((a, b) => a.Quarter.CompareTo(b.Quarter));
            }
            Stock s = ListingBook.Make(f, owner, day, year, env, Math.Max(0L, r.Debt), indexNow);
            OwnListing o = s.Own;
            o.Firm = true;
            o.Place = MarketStore.Clean(r.Place);
            o.Float = Math.Max(0L, Math.Min(r.Float, s.Shares - o.Founder));
            o.LockDay = r.LockDay;
            o.VestBase = Math.Max(0L, Math.Min(r.VestBase, o.Founder));
            o.VestDay = r.VestDay;
            o.VestCliff = Math.Max(0, r.VestCliff);
            o.VestSpan = Math.Max(0, r.VestSpan);
            if (r.ListedDay > 0 && r.ListedDay < day)
            {
                s.ListedDay = r.ListedDay;   // a listing from before there was a stock: no new listing's first morning for it
            }
            o.Mixed = false;   // a firm keeps the sector it listed in: its trades are not the player's other businesses'
            o.PayBp = ListingCatalogue.PayRefBp;
            s.Adv = Math.Max(1000.0, o.Float * s.Def.TurnoverBp / 10000.0);   // only the public's shares trade
            return s;
        }
    }
}
