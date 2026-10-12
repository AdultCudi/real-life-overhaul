using System;
using System.Collections.Generic;
using RealLifeOverhaul.Economy.Ventures;

namespace RealLifeOverhaul.Markets
{
    /// <summary>
    /// The old stock market (Ventures' twelve companies) brought into this one, once. Game-free.
    ///
    ///   - each of the old companies is a company here (<see cref="MarketCatalogue.VenturesMap"/>: by its sector and
    ///     which of its sector's two names it had; a rival's holding by the name it was listed under), and a new market
    ///     opened for a save that had the old one starts those companies at the old prices with their last closes;
    ///   - the player's holdings move exactly: the same shares, what they cost and the dividends they had paid,
    ///     marked as a test's where they were. Nothing is bought or sold and no money moves;
    ///   - the old file is left with no holdings and marked as moved, so a second look finds nothing to bring.
    ///
    /// A holding whose company cannot be found here (it cannot happen with the old market's companies) is paid out at
    /// its old price, with no commission, so nothing is lost: <see cref="Move"/> says so in its answer.
    /// </summary>
    public static class MarketCarryOver
    {
        /// <summary>What a carry-over did.</summary>
        public struct Result
        {
            public int Moved;            // holdings moved
            public long Shares;          // shares in them
            public long Cost;            // what they had cost
            public int PaidOut;          // holdings that had no company here, paid out
            public long PaidMoney;
        }

        /// <summary>The id here of one of the old market's companies; 0 if there is none.</summary>
        public static int IdOf(Company old, MarketState state)
        {
            if (old == null)
            {
                return 0;
            }
            if (old.Rival)
            {
                Stock rival = state != null ? MarketSetup.Rival(state, old.RivalName) : null;
                return rival != null ? rival.Id : 0;
            }
            int sector = (int)old.Sector;
            if (sector < 0 || sector >= MarketCatalogue.VenturesMap.Length)
            {
                return 0;
            }
            return MarketCatalogue.VenturesMap[sector][old.NameIndex & 1];
        }

        /// <summary>The old companies' prices, for a new market (a rival's under a rival id here, matched by name when the market is opened).</summary>
        public static List<CarriedPrice> Prices(VenturesFile file)
        {
            var prices = new List<CarriedPrice>(16);
            if (file == null)
            {
                return prices;
            }
            int rival = 0;
            for (int i = 0; i < file.Companies.Count; i++)
            {
                Company c = file.Companies[i];
                int id = c.Rival ? (rival < MarketCatalogue.RivalTemplates.Length ? MarketCatalogue.RivalTemplates[rival++].Id : 0) : IdOf(c, null);
                if (id <= 0 || c.PriceCents <= 0)
                {
                    continue;
                }
                var closes = new int[c.HistoryCount];
                Array.Copy(c.History, closes, c.HistoryCount);
                prices.Add(new CarriedPrice { Id = id, Cents = c.PriceCents, Closes = closes, CloseCount = c.HistoryCount, Name = c.Rival ? c.RivalName : null });
            }
            return prices;
        }

        /// <summary>True if the old file still has holdings to bring.</summary>
        public static bool Waiting(VenturesFile file)
        {
            return file != null && !file.MarketMoved && file.Holdings.Count > 0;
        }

        /// <summary>
        /// The holdings moved from the old file into the market, once. The old file is emptied of them and marked as
        /// moved whatever happens, so this can never bring the same shares twice. A holding of a company that is gone
        /// from this market (it failed, it was taken over) is paid out at its old price with no commission.
        /// </summary>
        public static Result Move(VenturesFile file, MarketState state, IMarketWallet wallet)
        {
            var result = new Result();
            if (file == null || state == null || file.MarketMoved)
            {
                return result;
            }
            for (int i = 0; i < file.Holdings.Count; i++)
            {
                Holding h = file.Holdings[i];
                Company old = file.Company(h.CompanyId);
                if (h.Shares <= 0)
                {
                    continue;
                }
                int id = IdOf(old, state);
                Stock s = id > 0 ? state.Stock(id) : null;
                if (s == null || !s.Listed)
                {
                    long money = old != null ? (long)h.Shares * old.PriceCents / 100L : 0L;
                    if (money > 0 && wallet != null)
                    {
                        wallet.Give(money, MarketMoney.Payout, old.Name);
                    }
                    result.PaidOut++;
                    result.PaidMoney += money;
                    continue;
                }
                Position p = state.Position(id);
                if (p == null)
                {
                    p = new Position { StockId = id, OpenedDay = state.Day, Test = h.Test };
                    state.Positions.Add(p);
                }
                p.Shares += h.Shares;
                p.Cost += h.Cost;
                p.Dividends += h.Dividends;
                result.Moved++;
                result.Shares += h.Shares;
                result.Cost += h.Cost;
            }
            file.Holdings.Clear();
            file.MarketMoved = true;
            state.CarriedOver = true;
            return result;
        }
    }
}
