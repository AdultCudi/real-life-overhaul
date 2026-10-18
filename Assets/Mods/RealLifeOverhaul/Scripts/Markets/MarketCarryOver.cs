using System;
using System.Collections.Generic;
using RealLifeOverhaul.Economy.Ventures;

namespace RealLifeOverhaul.Markets
{
    /// <summary>
    /// The old Ventures stock market brought into this one, once. Game-free. Each old company maps to one here
    /// (<see cref="MarketCatalogue.VenturesMap"/>, a rival's by name) and a new market starts them at their old prices; the
    /// player's holdings move exactly, no money moving, and the old file is emptied and marked as moved. A holding with no
    /// company here is paid out at its old price.
    /// </summary>
    public static class MarketCarryOver
    {
        /// <summary>What a carry-over did.</summary>
        public struct Result
        {
            public int Moved;
            public long Shares;
            public long Cost;
            public int PaidOut;          // holdings with no company here, paid out
            public long PaidMoney;
        }

        /// <summary>The id here of one of the old market's companies; 0 for none.</summary>
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

        /// <summary>The old companies' prices for a new market; a rival's under a rival id here, matched by name at the opening.</summary>
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

        public static bool Waiting(VenturesFile file)
        {
            return file != null && !file.MarketMoved && file.Holdings.Count > 0;
        }

        /// <summary>
        /// The holdings moved from the old file into the market, once: the old file is emptied and marked as moved whatever
        /// happens. A holding of a company gone from this market is paid out at its old price with no commission.
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
