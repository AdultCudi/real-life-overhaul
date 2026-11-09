using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Markets
{
    /// <summary>
    /// Part of the engine: the player's firms that listed on their own and the city's startups that listed (<see cref="ListingFirm"/>). They are made,
    /// changed and ended from outside the market's step, by the tech sector on a game day or a press, on the machine that plays the market alone; the
    /// director then marks the market changed, so a Going Public host's guests are given it whole. Nothing here runs per frame.
    /// </summary>
    public sealed partial class MarketEngine
    {
        /// <summary>The founder dealt in a firm's shares on the founder's machine: the stock, the shares (under nothing: sold), the price in cents and the money before commission.</summary>
        public Action<int, long, int, long> FirmDealt;

        /// <summary>The player's firm of a place on the market, or null; with <paramref name="live"/> only one still listed.</summary>
        public Stock FirmByPlace(string place, bool live = true)
        {
            string key = MarketStore.Clean(place);
            Stock found = null;
            for (int i = 0; i < State.Stocks.Count; i++)
            {
                Stock s = State.Stocks[i];
                if (s.Own != null && s.Own.Firm && s.Own.Owner == State.Me && State.Me != 0UL && s.Own.Place == key && (s.Listed || !live) && (found == null || s.Listed))
                {
                    found = s;
                }
            }
            return found;
        }

        /// <summary>The player's firms that are listed now, into the list.</summary>
        public int MineFirms(List<Stock> into)
        {
            into.Clear();
            for (int i = 0; i < State.Stocks.Count; i++)
            {
                Stock s = State.Stocks[i];
                if (s.Listed && s.Own != null && s.Own.Firm && Mine(s))
                {
                    into.Add(s);
                }
            }
            return into.Count;
        }

        /// <summary>
        /// A firm of the player's on the market now, with its own price, shares and lock-up. No money moves: what the offer raised went into the firm's
        /// account when the tech sector priced it. Null, with the reason, when the market cannot take it.
        /// </summary>
        public Stock ListFirm(in FirmListing r, out FirmWhy why)
        {
            Finish();
            why = FirmWhy.None;
            if (State.Me == 0UL || State.Following)
            {
                why = FirmWhy.NotMine;
                return null;
            }
            int mine = 0;
            string key = MarketStore.Clean(r.Place);
            for (int i = 0; i < State.Stocks.Count; i++)
            {
                Stock there = State.Stocks[i];
                if (there.Listed && there.Own != null && there.Own.Firm && there.Own.Owner == State.Me)
                {
                    if (there.Own.Place == key)
                    {
                        why = FirmWhy.Already;
                        return there;
                    }
                    mine++;
                }
            }
            if (mine >= MarketEnv.FirmsMost)
            {
                why = FirmWhy.TooMany;
                return null;
            }
            int id = FreeId(ListingFirm.IdFor(State.Me, key));
            Stock old = State.Stock(id);
            if (old == null && State.Stocks.Count >= MarketCatalogue.CompaniesMost)
            {
                why = FirmWhy.Full;
                return null;
            }
            Stock s = ListingFirm.Build(r, State.Me, State.Day, Year, State.Env, State.IndexLast, id, ListingFirm.UniqueTicker(r.Name, State.Stocks, id));
            if (old != null)
            {
                State.Stocks.Remove(old);
            }
            Reweigh(s, true);   // before it is counted: the indices stay where they were
            State.Stocks.Add(s);
            Say(new MarketNews { Day = State.Day, Kind = MarketNewsKind.Listing, Stock = s.Id, A = s.Last, B = State.Day });
            return s;
        }

        /// <summary>
        /// A startup of the city that listed, as a company of the market's own: its business is the market's model of one in its sector, its shares and
        /// price the listing's. The market holds so many such companies at once (<see cref="MarketCatalogue.StartupsMost"/>); those long gone make room.
        /// </summary>
        public Stock ListStartup(in StartupListing r, out FirmWhy why)
        {
            Finish();
            why = FirmWhy.None;
            if (State.Following)
            {
                why = FirmWhy.Off;
                return null;
            }
            int live = 0;
            for (int i = State.Stocks.Count - 1; i >= 0; i--)
            {
                Stock there = State.Stocks[i];
                if (!MarketCatalogue.IsStartup(there.Id))
                {
                    continue;
                }
                if (there.Listed)
                {
                    live++;
                }
                else if (State.Day - there.GoneDay > Year && State.Position(there.Id) == null && !Mentioned(there.Id))
                {
                    State.Stocks.RemoveAt(i);   // long gone and nothing of the player's points at it: the room is the next startup's
                }
            }
            if (live >= MarketCatalogue.StartupsMost)
            {
                why = FirmWhy.TooMany;
                return null;
            }
            int id = 0;
            for (int k = 0; k < MarketCatalogue.StartupCount && id == 0; k++)
            {
                id = State.Stock(MarketCatalogue.StartupFirst + k) == null ? MarketCatalogue.StartupFirst + k : 0;
            }
            if (id == 0 || State.Stocks.Count >= MarketCatalogue.CompaniesMost)
            {
                why = FirmWhy.Full;
                return null;
            }
            string name = MarketStore.Clean(r.Name);
            string ticker = ListingFirm.UniqueTicker(name, State.Stocks, id);
            string trade = null;
            CompanyDef d = MarketCatalogue.StartupDef(id, name, ticker, (MarketSector)MarketValuation.Clamp(r.Sector, 0, (int)MarketSector.Count - 1), r.RevenueM, trade);
            ulong seed = MarketRandom.Mix64(State.Id ^ ((ulong)(uint)id * 0x9E3779B97F4A7C15UL) ^ (ulong)(uint)State.Day);
            Stock s = MarketLife.Make(d, null, State.Day, State.Env, Year, seed, Math.Max(1, r.PriceCents), Math.Max(100000L, r.Shares));
            s.Daily.Clear();
            s.Monthly.Clear();
            Reweigh(s, true);
            State.Stocks.Add(s);
            Say(new MarketNews { Day = State.Day, Kind = MarketNewsKind.Listing, Stock = s.Id, A = s.MidCents, B = State.Day });
            return s;
        }

        // Whether an order, a watch, a bot's pick or a chart alert still names a company.
        private bool Mentioned(int id)
        {
            for (int i = 0; i < State.Orders.Count; i++)
            {
                if (State.Orders[i].StockId == id)
                {
                    return true;
                }
            }
            for (int i = 0; i < State.Bots.Count; i++)
            {
                if (State.Bots[i].Holding(id) != null || State.Bots[i].Picks.Contains(id))
                {
                    return true;
                }
            }
            return State.Watching(id);
        }

        /// <summary>
        /// A firm's stock ends: it leaves the market paying what is left to whoever holds it, as a company that is taken private or taken over does.
        /// <paramref name="cents"/> is what each share is paid out at (0: nothing). True if it was listed.
        /// </summary>
        public bool EndFirm(int stockId, GoneReason reason, int cents)
        {
            Finish();
            Stock s = State.Stock(stockId);
            if (s == null || !s.Listed || s.Own == null || !s.Own.Firm)
            {
                return false;
            }
            Leave(s, State.Day, reason, cents, reason == GoneReason.Failed ? MarketNewsKind.Failure : MarketNewsKind.Delisted);
            return true;
        }

        /// <summary>
        /// The player's firms on the market whose place the tech sector no longer lists (a firm set aside while its player was a guest and wound down meanwhile, a
        /// stock left over) leave it, paying nothing. Returns how many left.
        /// </summary>
        public int SweepFirms(IList<string> places)
        {
            Finish();
            var keep = new HashSet<string>();
            for (int i = 0; places != null && i < places.Count; i++)
            {
                keep.Add(MarketStore.Clean(places[i]));
            }
            int left = 0;
            for (int i = 0; i < State.Stocks.Count; i++)
            {
                Stock s = State.Stocks[i];
                if (s.Listed && s.Own != null && s.Own.Firm && Mine(s) && !keep.Contains(s.Own.Place))
                {
                    Leave(s, State.Day, GoneReason.Delisted, 0, MarketNewsKind.Delisted);
                    left++;
                }
            }
            return left;
        }

        /// <summary>
        /// Shares of a firm made or cancelled that are neither the founder's nor the public's (staff options, an investor's line): every holder's share of it
        /// changes with the number in issue. Under nothing for shares cancelled. True if it was listed.
        /// </summary>
        public bool GrowFirm(int stockId, long shares)
        {
            Finish();
            Stock s = State.Stock(stockId);
            if (s == null || !s.Listed || s.Own == null || !s.Own.Firm || shares == 0)
            {
                return false;
            }
            long least = s.Own.Founder + s.Own.Float;
            Reweigh(s, false);
            s.Shares = Math.Max(Math.Max(1L, least), s.Shares + shares);
            Reweigh(s, true);
            return true;
        }

        /// <summary>
        /// The founder's unvested shares of a firm, as of a game day, taken back as a bad leaver's are: they are no longer in issue, and what the founder holds is
        /// all vested. Returns the shares cancelled; 0 if it is not listed.
        /// </summary>
        public long CancelUnvested(int stockId, int day)
        {
            Finish();
            Stock s = State.Stock(stockId);
            if (s == null || !s.Listed || s.Own == null || !s.Own.Firm)
            {
                return 0L;
            }
            OwnListing o = s.Own;
            long take = o.Unvested(day);
            Reweigh(s, false);
            o.Founder -= take;
            s.Shares = Math.Max(Math.Max(1L, o.Founder + o.Float), s.Shares - take);
            o.VestBase = 0;
            Reweigh(s, true);
            return take;
        }
    }
}
