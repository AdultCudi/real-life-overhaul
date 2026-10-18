using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace RealLifeOverhaul.Markets
{
    /// <summary>
    /// The market in the save: one line of modData under <see cref="Key"/>. Game-free. Parts are divided by '~' and begin
    /// with a letter and a colon; records by ';', fields by '|'.
    ///     v=1~h:...~e:...~k:...~p:...~o:...~t:...~w:...~a:...~n:...~d:...~x:...~y:...~b:...~f:...~g:...~l:...~u:...
    ///   h the header (id, clock, frontier, mood, divisors), e the city as last seen, k the companies with their business,
    ///   results and candles (MarketCodec; today's step candles only for those the player follows), p positions, o orders,
    ///   t the account's history, w the watch list, a the account, n the news, d dividends owed, x the index, y the sectors.
    ///   Never in the city's line a host sends: l the player's way to the market and books, u their listed company while set
    ///   aside as a guest, b their bots, f their bonds (BondStore), g worked orders and dividends to put back.
    /// A bot's shares are also written into the player's holding of part p, marked, and taken out again on reading, so an
    /// older reader takes them as the player's own. A bot's capital added or taken out is a deposit or withdrawal marked 1;
    /// worked orders are apart from part o so an older reader never fills one whole. A player's listed company is a k record
    /// with its own fields after the rest. No secret of the market's chance is ever in it: those are on the tape. Newer
    /// fields go at the end of a record or in a new part; this reader takes what it knows, keeps every figure in bounds and
    /// drops a record it cannot read; a catalogue company missing from a line is made again so no holding points at nothing.
    /// </summary>
    public static partial class MarketStore
    {
        public const string Key = "reallife:markets";
        public const int Version = 1;

        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        // The line being written, kept so its room (250 to 400 thousand characters) grows once a session. One per thread.
        [ThreadStatic] private static StringBuilder _text;

        public static MarketState Load(Dictionary<string, string> data, out int version)
        {
            version = 0;
            if (data == null || !data.TryGetValue(Key, out string text) || string.IsNullOrEmpty(text))
            {
                return null;
            }
            try
            {
                return SaveLines.Checked(data, Key, Read(text, out version), version, Version);   // a line that cannot be read is kept as it is (Core/SaveLines)
            }
            catch (Exception)
            {
                SaveLines.Unreadable(data, Key, "it could not be read");
                return null;
            }
        }

        public static void Save(Dictionary<string, string> data, MarketState state)
        {
            if (data != null && state != null)
            {
                data[Key] = Write(state);
            }
        }

        public static string Write(MarketState s)
        {
            return Write(s, false);
        }

        /// <summary>
        /// With <paramref name="city"/>, the city's market alone as a Going Public host sends it: every company with all its
        /// candles, the indices and the news, and nothing of the player's.
        /// </summary>
        public static string Write(MarketState s, bool city)
        {
            StringBuilder t = _text ?? (_text = new StringBuilder(64 * 1024));
            t.Length = 0;
            t.Append("v=").Append(Version).Append("~h:");
            t.Append(s.Id.ToString("X16", Inv));
            Join(t, s.CreatedDay, s.YearDays, s.Day, s.Phase, s.FrontierDay, s.FrontierPhase);
            D(t, s.Variance);
            D(t, s.LastShock);
            D(t, s.IndexDivisor);
            Join(t, s.ListingId, s.ListingDay, city ? 1 : s.NextOrderId, !city && s.CarriedOver ? 1 : 0, s.RateSeenBp, s.ElectionsSeen, s.BusinessDay, s.IndexOpen, s.IndexHigh,
                s.IndexLow, s.IndexLast, s.IndexPrevClose, city ? 0 : s.HeldDay, city ? 0 : s.HeldPhase);
            D(t, s.DayZ);
            D(t, s.DayVar);
            Join(t, s.Weighted ? 1 : 0);
            t.Append("~e:");
            MarketTape.WriteEnv(t, s.Env);
            t.Append("~k:");
            for (int i = 0; i < s.Stocks.Count; i++)
            {
                if (i > 0)
                {
                    t.Append(';');
                }
                Stock(t, s, s.Stocks[i], city);
            }
            t.Append("~p:");
            int written = 0;
            for (int i = 0; i < s.Bots.Count; i++)
            {
                for (int j = 0; j < s.Bots[i].Holdings.Count; j++)
                {
                    s.Bots[i].Holdings[j].Merged = false;
                }
            }
            for (int i = 0; i < s.Positions.Count && !city; i++)
            {
                Position p = s.Positions[i];
                if (p.Shares == 0)
                {
                    continue;
                }
                // A bot's shares of the same company go with it, unless the player is short of it.
                long shares = p.Shares, cost = p.Cost;
                if (p.Shares > 0 && !p.Test)
                {
                    MergeBots(s, p.StockId, ref shares, ref cost);
                }
                t.Append(written++ > 0 ? ";" : string.Empty).Append(p.StockId.ToString(Inv));
                L(t, shares, cost, p.Dividends, p.Realised);
                Join(t, p.Test ? 1 : 0, p.OpenedDay);
                if (p.Reinvest > 0)
                {
                    Join(t, p.Reinvest);   // the share of each dividend put back
                }
            }
            for (int i = 0; i < s.Bots.Count && !city; i++)
            {
                // A bot's holding of a company the player holds none of: a holding of the player's in the line.
                Bot b = s.Bots[i];
                for (int j = 0; j < b.Holdings.Count; j++)
                {
                    BotHolding h = b.Holdings[j];
                    if (h.Shares <= 0 || s.Position(h.StockId) != null || EarlierBotHolds(s, i, h.StockId))
                    {
                        continue;
                    }
                    long shares = 0, cost = 0;
                    MergeBots(s, h.StockId, ref shares, ref cost);
                    t.Append(written++ > 0 ? ";" : string.Empty).Append(h.StockId.ToString(Inv));
                    L(t, shares, cost, 0, 0);
                    Join(t, 0, h.OpenedDay);
                }
            }
            t.Append("~o:");
            int worked = 0;
            for (int i = 0, n = 0; i < s.Orders.Count && !city; i++)
            {
                Order o = s.Orders[i];
                if (o.Worked || o.Back)
                {
                    worked++;   // part g
                    continue;
                }
                t.Append(n++ > 0 ? ";" : string.Empty);
                OrderFields(t, o);
                if (o.Closing)
                {
                    Join(t, 1);   // a stop loss or a take profit; an older reader takes a stop or a limit
                }
            }
            t.Append("~t:");
            for (int i = 0; i < s.Trades.Count && !city; i++)
            {
                Trade r = s.Trades[i];
                t.Append(i > 0 ? ";" : string.Empty).Append(r.Day.ToString(Inv));
                bool capital = r.Kind == TradeKind.BotAdded || r.Kind == TradeKind.BotTaken;
                Join(t, r.Phase, r.StockId, (int)(capital ? (r.Kind == TradeKind.BotAdded ? TradeKind.Deposit : TradeKind.Withdrawal) : r.Kind));
                L(t, r.Qty);
                Join(t, r.PriceCents);
                L(t, r.Value, r.Fee);
                Join(t, r.Test ? 1 : 0);
                if (r.Bot > 0 || r.Order > 0 || r.Back || capital)
                {
                    Join(t, r.Bot);
                }
                if (r.Order > 0 || r.Back || capital)
                {
                    Join(t, r.Order, r.Back ? 1 : 0);   // a worked order's day of slices, a dividend put back
                }
                if (capital)
                {
                    Join(t, 1);   // a bot's capital added or taken out
                }
            }
            t.Append("~w:");
            for (int i = 0; i < s.Watch.Count && !city; i++)
            {
                t.Append(i > 0 ? "," : string.Empty).Append(s.Watch[i].ToString(Inv));
            }
            Account a = city ? new Account() : s.Account;
            t.Append("~a:").Append(a.Margin ? 1 : 0);
            L(t, a.Cash);
            Join(t, a.CallDay);
            L(t, a.CallAmount, a.Commission, a.Interest, a.BorrowFees, a.Dividends, a.DividendsPaid, a.Realised, a.Payouts);
            Join(t, a.OpenedDay);
            t.Append("~n:");
            int told = 0;
            for (int i = 0; i < s.News.Count; i++)
            {
                MarketNews n = s.News[i];
                if (city && n.Kind == MarketNewsKind.Paid)
                {
                    continue;   // a dividend paid to the host is theirs alone
                }
                t.Append(told++ > 0 ? ";" : string.Empty).Append(n.Day.ToString(Inv));
                Join(t, (int)n.Kind, n.Stock, n.A, n.B, n.C);
            }
            t.Append("~d:");
            for (int i = 0; i < s.Dues.Count && !city; i++)
            {
                DividendDue d = s.Dues[i];
                t.Append(i > 0 ? ";" : string.Empty).Append(d.StockId.ToString(Inv));
                L(t, d.Cents);
                Join(t, d.PayDay, d.Test ? 1 : 0);
                if (d.Bot > 0)
                {
                    Join(t, d.Bot);   // an older reader pays it to the player as their own: their money either way
                }
            }
            t.Append("~x:");
            MarketCodec.Write(t, s.IndexDaily);
            t.Append('|');
            MarketCodec.Write(t, s.IndexMonthly);
            t.Append('|');
            MarketCodec.Write(t, s.IndexBars, s.IndexBarCount);
            t.Append('|');
            MarketCodec.Write(t, s.IndexLastBars, s.IndexLastBarCount);
            Join(t, s.IndexLastBarsDay);
            t.Append("~y:");
            for (int sec = 0; sec < (int)MarketSector.Count; sec++)
            {
                if (sec > 0)
                {
                    t.Append(';');
                }
                t.Append(s.SectorVar[sec].ToString("R", Inv));
                D(t, s.SectorShock[sec]);
                Join(t, s.ManiaDays[sec]);
                D(t, s.ManiaAdded[sec]);
                D(t, s.SectorDivisor[sec]);
                Join(t, s.SectorLast[sec], s.SectorPrevClose[sec]);
                t.Append('|');
                MarketCodec.WriteInts(t, s.SectorDaily[sec]);
                D(t, s.SectorDayZ[sec]);
            }
            if (!city && s.Bots.Count > 0)
            {
                t.Append("~b:");
                Bots(t, s);
            }
            if (!city && s.Bonds.Holdings.Count > 0)
            {
                t.Append("~f:");
                BondStore.Write(t, s.Bonds);
            }
            if (!city && worked > 0)
            {
                t.Append("~g:");
                WorkedOrders(t, s);
            }
            if (!city && s.Listing != null && !s.Listing.Empty)
            {
                t.Append("~l:");
                Listing(t, s.Listing);
                if (!string.IsNullOrEmpty(s.Listing.Suspended))
                {
                    t.Append("~u:").Append(s.Listing.Suspended);
                }
            }
            return t.ToString();
        }

        public static MarketState Read(string text, out int version)
        {
            version = 0;
            string[] parts = text.Split('~');
            if (parts.Length == 0 || !parts[0].StartsWith("v=", StringComparison.Ordinal) || !int.TryParse(parts[0].Substring(2), NumberStyles.Integer, Inv, out version) || version < 1)
            {
                return null;
            }
            var s = new MarketState();
            bool header = false;
            string suspended = null, bots = null, worked = null;
            for (int p = 1; p < parts.Length; p++)
            {
                string part = parts[p];
                if (part.Length < 2 || part[1] != ':')
                {
                    continue;
                }
                string body = part.Substring(2);
                switch (part[0])
                {
                    case 'h': header = Header(s, body.Split('|')); break;
                    case 'e': if (MarketTape.ReadEnv(body, out MarketEnv env)) s.Env = env; break;
                    case 'k':
                        foreach (string record in Records(body))
                        {
                            Stock k = ReadStock(record.Split('|'), s.YearDays);
                            if (k != null && s.Stock(k.Id) == null && s.Stocks.Count < MarketCatalogue.CompaniesMost)
                            {
                                s.Stocks.Add(k);
                            }
                        }
                        break;
                    case 'p':
                        foreach (string record in Records(body))
                        {
                            string[] c = record.Split('|');
                            if (c.Length >= 5 && I(c, 0) > 0 && L(c, 1) != 0 && s.Position(I(c, 0)) == null)
                            {
                                s.Positions.Add(new Position
                                {
                                    StockId = I(c, 0), Shares = L(c, 1), Cost = Math.Max(0, L(c, 2)), Dividends = L(c, 3), Realised = L(c, 4), Test = I(c, 5) != 0, OpenedDay = I(c, 6),
                                    Reinvest = L(c, 1) > 0 ? InvestRules.ReinvestOf(Clamp(I(c, 7), 0, 100)) : 0
                                });
                            }
                        }
                        break;
                    case 'o':
                        foreach (string record in Records(body))
                        {
                            Order o = s.Orders.Count < MarketCatalogue.OrdersMost * 2 ? ReadOrder(record.Split('|'), false) : null;
                            if (o != null)
                            {
                                s.Orders.Add(o);
                            }
                        }
                        break;
                    case 'g':
                        worked = body;
                        break;
                    case 't':
                        foreach (string record in Records(body))
                        {
                            string[] c = record.Split('|');
                            if (c.Length >= 9 && s.Trades.Count < MarketCatalogue.TradesKept)
                            {
                                var trade = new Trade
                                {
                                    Day = I(c, 0), Phase = I(c, 1), StockId = I(c, 2), Kind = (TradeKind)Clamp(I(c, 3), 0, 9), Qty = L(c, 4), PriceCents = I(c, 5), Value = L(c, 6),
                                    Fee = L(c, 7), Test = I(c, 8) != 0, Bot = MarketBots.Firm(I(c, 9)) != null ? I(c, 9) : 0, Order = Math.Max(0, I(c, 10)), Back = I(c, 11) != 0
                                };
                                if (I(c, 12) == 1 && trade.Bot > 0 && (trade.Kind == TradeKind.Deposit || trade.Kind == TradeKind.Withdrawal))
                                {
                                    trade.Kind = trade.Kind == TradeKind.Deposit ? TradeKind.BotAdded : TradeKind.BotTaken;   // a bot's capital added or taken out
                                }
                                s.Trades.Add(trade);
                            }
                        }
                        break;
                    case 'w':
                        foreach (string id in body.Length > 0 ? body.Split(',') : Array.Empty<string>())
                        {
                            if (int.TryParse(id, NumberStyles.Integer, Inv, out int value) && value > 0 && !s.Watch.Contains(value) && s.Watch.Count < MarketCatalogue.WatchMost)
                            {
                                s.Watch.Add(value);
                            }
                        }
                        break;
                    case 'a':
                        {
                            string[] c = body.Split('|');
                            Account a = s.Account;
                            a.Margin = I(c, 0) != 0;
                            a.Cash = L(c, 1);
                            a.CallDay = Math.Max(0, I(c, 2));
                            a.CallAmount = Math.Max(0, L(c, 3));
                            a.Commission = Math.Max(0, L(c, 4));
                            a.Interest = Math.Max(0, L(c, 5));
                            a.BorrowFees = Math.Max(0, L(c, 6));
                            a.Dividends = Math.Max(0, L(c, 7));
                            a.DividendsPaid = Math.Max(0, L(c, 8));
                            a.Realised = L(c, 9);
                            a.Payouts = L(c, 10);
                            a.OpenedDay = I(c, 11);
                            if (!a.Margin)
                            {
                                a.Cash = 0;
                            }
                        }
                        break;
                    case 'n':
                        foreach (string record in Records(body))
                        {
                            string[] c = record.Split('|');
                            if (c.Length >= 6 && I(c, 1) >= 0 && I(c, 1) < (int)MarketNewsKind.Count && s.News.Count < MarketCatalogue.NewsKept)
                            {
                                s.News.Add(new MarketNews { Day = I(c, 0), Kind = (MarketNewsKind)I(c, 1), Stock = I(c, 2), A = I(c, 3), B = I(c, 4), C = I(c, 5) });
                            }
                        }
                        break;
                    case 'd':
                        foreach (string record in Records(body))
                        {
                            string[] c = record.Split('|');
                            if (c.Length >= 3 && I(c, 0) > 0 && L(c, 1) != 0 && s.Dues.Count < 200)
                            {
                                s.Dues.Add(new DividendDue { StockId = I(c, 0), Cents = L(c, 1), PayDay = I(c, 2), Test = I(c, 3) != 0,
                                    Bot = MarketBots.Firm(I(c, 4)) != null ? I(c, 4) : 0 });
                            }
                        }
                        break;
                    case 'x':
                        {
                            string[] c = body.Split('|');
                            if (c.Length > 0)
                            {
                                MarketCodec.Read(c[0], s.IndexDaily, MarketCatalogue.DailyKept);
                            }
                            if (c.Length > 1)
                            {
                                MarketCodec.Read(c[1], s.IndexMonthly, MarketCatalogue.MonthlyKept);
                            }
                            s.IndexBarCount = c.Length > 2 ? MarketCodec.Read(c[2], s.IndexBars) : 0;
                            s.IndexLastBarCount = c.Length > 3 ? MarketCodec.Read(c[3], s.IndexLastBars) : 0;
                            s.IndexLastBarsDay = I(c, 4);
                        }
                        break;
                    case 'l':
                        s.Listing = ReadListing(body.Split('|'));
                        break;
                    case 'u':
                        suspended = body;
                        break;
                    case 'b':
                        bots = body;
                        break;
                    case 'f':
                        BondStore.Read(body, s.Bonds);
                        break;
                    case 'y':
                        {
                            string[] records = body.Split(';');
                            for (int sec = 0; sec < records.Length && sec < (int)MarketSector.Count; sec++)
                            {
                                string[] c = records[sec].Split('|');
                                s.SectorVar[sec] = Clamp(F(c, 0, 1.0), MarketCatalogue.VarianceLeast, MarketCatalogue.VarianceMost);
                                s.SectorShock[sec] = Clamp(F(c, 1, 0.0), -50.0, 50.0);
                                s.ManiaDays[sec] = Math.Max(0, I(c, 2));
                                s.ManiaAdded[sec] = Clamp(F(c, 3, 0.0), 0.0, 5.0);
                                s.SectorDivisor[sec] = Math.Max(0.0, F(c, 4, 0.0));
                                s.SectorLast[sec] = Math.Max(0, I(c, 5));
                                s.SectorPrevClose[sec] = Math.Max(0, I(c, 6));
                                ReadInts(c.Length > 7 ? c[7] : null, s.SectorDaily[sec], MarketCatalogue.DailyKept);
                                s.SectorDayZ[sec] = Clamp(F(c, 8, 0.0), -100.0, 100.0);
                            }
                        }
                        break;
                }
            }
            if (!header || s.Stocks.Count == 0)
            {
                return null;
            }
            if (bots != null)
            {
                ReadBots(s, bots);   // after the holdings: their shares are taken out of the player's
                Unmerge(s);
            }
            if (worked != null)
            {
                ReadWorked(s, worked);   // after part o: every order back in its place by its number
            }
            if (s.Listing != null && suspended != null && ReadStockLine(suspended, s.YearDays) != null)
            {
                s.Listing.Suspended = suspended;
            }
            Repair(s);
            MarketLife.DayBases(s, s.InSession);   // a save made after a close by an old version has every company's at its close
            return s;
        }

        private static bool Header(MarketState s, string[] c)
        {
            if (c.Length < 6 || !ulong.TryParse(c[0], NumberStyles.HexNumber, Inv, out ulong id))
            {
                return false;
            }
            s.Id = id;
            s.CreatedDay = Math.Max(1, I(c, 1));
            s.YearDays = Clamp(I(c, 2, 60), 8, 400);
            s.Day = Math.Max(1, I(c, 3));
            s.Phase = Clamp(I(c, 4), 0, MarketCatalogue.PhaseClose);
            s.FrontierDay = Math.Max(0, I(c, 5));
            s.FrontierPhase = Clamp(I(c, 6), 0, MarketCatalogue.PhaseClose);
            s.Variance = Clamp(F(c, 7, 1.0), MarketCatalogue.VarianceLeast, MarketCatalogue.VarianceMost);
            s.LastShock = Clamp(F(c, 8, 0.0), -50.0, 50.0);
            s.IndexDivisor = Math.Max(0.0, F(c, 9, 0.0));
            s.ListingId = Math.Max(0, I(c, 10));
            s.ListingDay = Math.Max(0, I(c, 11));
            s.NextOrderId = Math.Max(1, I(c, 12, 1));
            s.CarriedOver = I(c, 13) != 0;
            s.RateSeenBp = I(c, 14, -1);
            s.ElectionsSeen = I(c, 15, -1);
            s.BusinessDay = Math.Max(0, I(c, 16));
            s.IndexOpen = Math.Max(0, I(c, 17));
            s.IndexHigh = Math.Max(0, I(c, 18));
            s.IndexLow = Math.Max(0, I(c, 19));
            s.IndexLast = Math.Max(0, I(c, 20));
            s.IndexPrevClose = Math.Max(0, I(c, 21));
            s.HeldDay = Math.Max(0, I(c, 22));
            s.HeldPhase = Clamp(I(c, 23), 0, MarketCatalogue.PhaseClose);
            s.DayZ = Clamp(F(c, 24, 0.0), -100.0, 100.0);
            s.DayVar = Clamp(F(c, 25, 0.0), 0.0, 10.0);
            s.Weighted = I(c, 26) != 0;   // appended: an older save has none and is set right at its first step
            if (MarketState.Time(s.FrontierDay, s.FrontierPhase) < s.Now)
            {
                s.FrontierDay = s.Day;
                s.FrontierPhase = s.Phase;
            }
            return true;
        }

        // A holding or order of a company missing from the line has its company made again at its catalogue price; an order
        // of an unknown company goes.
        private static void Repair(MarketState s)
        {
            MarketEnv env = s.Env;
            for (int i = 0; i < s.Positions.Count; i++)
            {
                int id = s.Positions[i].StockId;
                CompanyDef def = MarketCatalogue.Find(id);
                if (s.Stock(id) == null && def != null && !MarketCatalogue.IsRival(id))
                {
                    Stock made = MarketLife.Make(def, null, s.Day, env, s.YearDays, MarketRandom.Mix64(s.Id ^ (ulong)id), 0);
                    made.ListedDay = MarketSetup.ListedLongAgo(s.Day, s.YearDays);
                    s.Stocks.Add(made);
                }
            }
            s.Positions.RemoveAll(p => s.Stock(p.StockId) == null);
            s.Orders.RemoveAll(o => s.Stock(o.StockId) == null);
            s.Watch.RemoveAll(w => s.Stock(w) == null);
            for (int i = 0; i < s.Bots.Count; i++)
            {
                s.Bots[i].Holdings.RemoveAll(h => s.Stock(h.StockId) == null);
                s.Bots[i].Pending.RemoveAll(o => s.Stock(o.StockId) == null);
                s.Bots[i].Picks.RemoveAll(id => s.Stock(id) == null);
            }
            for (int i = 0; i < s.Stocks.Count; i++)
            {
                if (s.Stocks[i].Listed && s.Stocks[i].Quarter < 0)
                {
                    s.Stocks[i].Quarter = MarketCatalogue.QuarterOf(s.Day, s.YearDays);
                }
            }
        }

        private static string[] Records(string body)
        {
            return body.Length == 0 ? Array.Empty<string>() : body.Split(';');
        }

        private static void Join(StringBuilder t, params int[] values)
        {
            for (int i = 0; i < values.Length; i++)
            {
                t.Append('|').Append(values[i].ToString(Inv));
            }
        }

        private static void L(StringBuilder t, params long[] values)
        {
            for (int i = 0; i < values.Length; i++)
            {
                t.Append('|').Append(values[i].ToString(Inv));
            }
        }

        private static void D(StringBuilder t, double value)
        {
            t.Append('|').Append(double.IsNaN(value) || double.IsInfinity(value) ? "0" : value.ToString("R", Inv));
        }

        private static void ReadInts(string text, List<int> into, int most)
        {
            if (string.IsNullOrEmpty(text) || !MarketCodec.ReadInts(text, into, most))
            {
                into.Clear();
            }
        }

        private static int I(string[] c, int at, int fallback = 0)
        {
            return at < c.Length && int.TryParse(c[at], NumberStyles.Integer, Inv, out int value) ? value : fallback;
        }

        private static long L(string[] c, int at, long fallback = 0)
        {
            return at < c.Length && long.TryParse(c[at], NumberStyles.Integer, Inv, out long value) ? value : fallback;
        }

        private static double F(string[] c, int at, double fallback)
        {
            return at < c.Length && double.TryParse(c[at], NumberStyles.Float, Inv, out double value) && !double.IsNaN(value) && !double.IsInfinity(value) ? value : fallback;
        }

        private static string S(string[] c, int at)
        {
            return at < c.Length ? c[at] : string.Empty;
        }

        private static int Int(string text)
        {
            return int.TryParse(text, NumberStyles.Integer, Inv, out int value) ? value : 0;
        }

        private static long Long(string text)
        {
            return long.TryParse(text, NumberStyles.Integer, Inv, out long value) ? value : 0;
        }

        private static int Clamp(int value, int least, int most)
        {
            return value < least ? least : value > most ? most : value;
        }

        private static double Clamp(double value, double least, double most)
        {
            return value < least ? least : value > most ? most : value;
        }

        /// <summary>A text with the line's dividers taken out, so it can never break the line apart.</summary>
        public static string Clean(string text)
        {
            return string.IsNullOrEmpty(text) ? string.Empty : text.Replace('|', ' ').Replace(';', ' ').Replace('~', ' ').Replace(':', ' ').Replace(',', ' ');
        }
    }
}
