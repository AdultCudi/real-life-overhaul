using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace RealLifeOverhaul.Markets
{
    /// <summary>
    /// The market in the save: one line of the save's modData under <see cref="Key"/>. Game-free.
    ///
    ///     v=1~h:...~e:...~k:...~p:...~o:...~t:...~w:...~a:...~n:...~d:...~x:...~y:...
    ///
    /// Parts are divided by '~' and begin with a letter and a colon; records by ';', fields by '|'.
    ///   h  the header: the playthrough's id, the clock, the frontier, the market's mood, the indices' divisor...
    ///   e  the city as the market last saw it
    ///   k  the companies: their price (what the market expects of them and its mood), their business, this quarter,
    ///      results, dividends, events, and their history (candles written small: MarketCodec). Today's step candles are
    ///      kept only for the companies the player watches, holds or has an order in (the others' start again at the load)
    ///   p  positions, o orders, t the account's history, w the watch list, a the account, n the last days' news,
    ///   d dividends owed, x the city's index and its history, y the sectors,
    ///   l the player's own way to the market and their company's books, u their listed company while it is set aside
    ///      (its founder is a guest in another player's city): Markets/Listing. Never in the city's line.
    ///   b the player's trading bots (MarketBots): each its firm, money, settings, record, holdings and what it waits to
    ///      trade. Never in the city's line.
    ///   f the player's bonds (Markets/Bonds/BondStore). Never in the city's line. A bot's shares are written into the player's holding of part p as well
    ///      (each holding of b says so), and taken out of it again here: a version that does not know bots reads them as
    ///      the player's own shares, bought with the player's money, and nothing is lost.
    /// A player's listed company is a record of k like every company's, with its own fields after the rest (its
    /// founder, its name and sector, its reserve, board and meetings): another player's is in the city's line too.
    ///
    /// No secret of the market's chance is ever in it: those are on the tape (<see cref="MarketTape"/>). A newer
    /// version adds fields at the end of a record or a part with a new letter; this reader takes the fields it knows,
    /// skips parts it does not, keeps every figure inside its bounds, and drops a record it cannot read. A line that
    /// cannot be read at all is left alone and the market opens afresh. A company of the catalogue missing from a line
    /// (it was damaged) is made again at its catalogue price, so a holding never points at nothing.
    /// </summary>
    public static class MarketStore
    {
        public const string Key = "reallife:markets";
        public const int Version = 1;

        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static MarketState Load(Dictionary<string, string> data, out int version)
        {
            version = 0;
            string text;
            if (data == null || !data.TryGetValue(Key, out text) || string.IsNullOrEmpty(text))
            {
                return null;
            }
            try
            {
                return SaveLines.Checked(data, Key, Read(text, out version), version, Version);   // (a line that cannot be read is kept as it is: Core/SaveLines)
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

        // ---------------------------------------------------------------- writing

        public static string Write(MarketState s)
        {
            return Write(s, false);
        }

        /// <summary>
        /// With <paramref name="city"/>, the city's market alone, as a Going Public host sends it to its guests (Markets/
        /// MarketShare): every company with all its candles, the indices, the news; and nothing of the player's: no
        /// holding, order, trade, watch list, account, dividend owed or recent trading of theirs.
        /// </summary>
        public static string Write(MarketState s, bool city)
        {
            var t = new StringBuilder(64 * 1024);
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
                // (a bot's shares of the same company with it, unless the player is short of it)
                long shares = p.Shares, cost = p.Cost;
                if (p.Shares > 0 && !p.Test)
                {
                    MergeBots(s, p.StockId, ref shares, ref cost);
                }
                t.Append(written++ > 0 ? ";" : string.Empty).Append(p.StockId.ToString(Inv));
                L(t, shares, cost, p.Dividends, p.Realised);
                Join(t, p.Test ? 1 : 0, p.OpenedDay);
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
            for (int i = 0, n = 0; i < s.Orders.Count && !city; i++)
            {
                Order o = s.Orders[i];
                t.Append(n++ > 0 ? ";" : string.Empty).Append(o.Id.ToString(Inv));
                Join(t, o.StockId, (int)o.Side, (int)o.Type, (int)o.Life);
                L(t, o.Qty, o.Filled, o.FilledValue);
                Join(t, o.LimitCents, o.StopCents, o.PlacedDay, o.PlacedPhase, o.ExpireDay, (int)o.State, (int)o.Why, o.Triggered ? 1 : 0, o.Test ? 1 : 0, o.Forced ? 1 : 0);
                if (o.Closing)
                {
                    Join(t, 1);   // a stop loss or a take profit (a version before them reads a stop or a limit)
                }
            }
            t.Append("~t:");
            for (int i = 0; i < s.Trades.Count && !city; i++)
            {
                Trade r = s.Trades[i];
                t.Append(i > 0 ? ";" : string.Empty).Append(r.Day.ToString(Inv));
                Join(t, r.Phase, r.StockId, (int)r.Kind);
                L(t, r.Qty);
                Join(t, r.PriceCents);
                L(t, r.Value, r.Fee);
                Join(t, r.Test ? 1 : 0);
                if (r.Bot > 0)
                {
                    Join(t, r.Bot);
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
            for (int i = 0; i < s.News.Count; i++)
            {
                MarketNews n = s.News[i];
                t.Append(i > 0 ? ";" : string.Empty).Append(n.Day.ToString(Inv));
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
                    Join(t, d.Bot);   // (a version before bots pays it to the player as their own: it is their money either way)
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
                Ints(t, s.SectorDaily[sec]);
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
                BondStore.Write(t, s.Bonds);   // the player's bonds (Markets/Bonds/BondStore)
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

        // ---------------------------------------------------------------- the player's trading bots

        // Every bot's shares of a company added to a holding's figures as it is written; each of their holdings marked as written so.
        private static void MergeBots(MarketState s, int stockId, ref long shares, ref long cost)
        {
            for (int i = 0; i < s.Bots.Count; i++)
            {
                BotHolding h = s.Bots[i].Holding(stockId);
                if (h != null && h.Shares > 0)
                {
                    shares += h.Shares;
                    cost += Math.Max(0, h.Cost);
                    h.Merged = true;
                }
            }
        }

        private static bool EarlierBotHolds(MarketState s, int bot, int stockId)
        {
            for (int i = 0; i < bot; i++)
            {
                BotHolding h = s.Bots[i].Holding(stockId);
                if (h != null && h.Shares > 0)
                {
                    return true;
                }
            }
            return false;
        }

        private static void Bots(StringBuilder t, MarketState s)
        {
            for (int i = 0; i < s.Bots.Count; i++)
            {
                Bot b = s.Bots[i];
                t.Append(i > 0 ? ";" : string.Empty).Append(b.Firm.ToString(Inv));
                Join(t, (int)b.Status);
                L(t, b.Capital, b.Base, b.Cash);
                Join(t, b.Size, b.Stop, b.Loss, b.HiredDay, b.LastDay);
                L(t, b.Peak);
                Join(t, b.DrawdownBp);
                L(t, b.HighWater);
                Join(t, b.Trades, b.Closed, b.Wins);
                L(t, b.Realised, b.Fees, b.Commission, b.Dividends);
                Join(t, b.Month);
                L(t, b.MonthWorth);
                Join(t, b.MonthTrades, b.MonthDays, b.ShortDay);
                t.Append('|');
                for (int j = 0; j < b.Holdings.Count; j++)
                {
                    BotHolding h = b.Holdings[j];
                    t.Append(j > 0 ? ":" : string.Empty).Append(h.StockId.ToString(Inv)).Append(',').Append(h.Shares.ToString(Inv)).Append(',').Append(h.Cost.ToString(Inv)).Append(',')
                        .Append(h.StopCents.ToString(Inv)).Append(',').Append(h.OpenedDay.ToString(Inv)).Append(',').Append(h.Gain.ToString(Inv)).Append(',').Append(h.Merged ? 1 : 0);
                }
                t.Append('|');
                for (int j = 0; j < b.Pending.Count; j++)
                {
                    BotOrder o = b.Pending[j];
                    t.Append(j > 0 ? ":" : string.Empty).Append(o.StockId.ToString(Inv)).Append(',').Append(o.Buy ? 1 : 0).Append(',').Append(o.Qty.ToString(Inv));
                }
            }
        }

        // The bots read back: each figure inside its bounds, a firm that is not one (or one twice) dropped, holdings of companies not in the line dropped.
        private static void ReadBots(MarketState s, string body)
        {
            foreach (string record in Records(body))
            {
                string[] c = record.Split('|');
                int firm = I(c, 0);
                if (c.Length < 25 || MarketBots.Firm(firm) == null || MarketBots.Hired(s, firm) != null || s.Bots.Count >= MarketBots.FirmsMost)
                {
                    continue;
                }
                var b = new Bot
                {
                    Firm = firm, Status = (BotStatus)Clamp(I(c, 1), 0, 2), Capital = Math.Max(0, L(c, 2)), Base = Math.Max(0, L(c, 3)), Cash = L(c, 4),
                    Size = MarketBots.Choice(MarketBots.Sizes, I(c, 5)), Stop = MarketBots.Choice(MarketBots.Stops, I(c, 6)), Loss = MarketBots.Choice(MarketBots.Losses, I(c, 7)),
                    HiredDay = I(c, 8), LastDay = I(c, 9), Peak = Math.Max(0, L(c, 10)), DrawdownBp = Clamp(I(c, 11), 0, 10000), HighWater = L(c, 12), Trades = Math.Max(0, I(c, 13)),
                    Closed = Math.Max(0, I(c, 14)), Wins = Math.Max(0, I(c, 15)), Realised = L(c, 16), Fees = Math.Max(0, L(c, 17)), Commission = Math.Max(0, L(c, 18)),
                    Dividends = Math.Max(0, L(c, 19)), Month = I(c, 20), MonthWorth = L(c, 21), MonthTrades = Math.Max(0, I(c, 22)), MonthDays = Math.Max(0, I(c, 23)), ShortDay = I(c, 24)
                };
                b.Wins = Math.Min(b.Wins, b.Closed);
                foreach (string r in S(c, 25).Length > 0 ? c[25].Split(':') : Array.Empty<string>())
                {
                    string[] f = r.Split(',');
                    int id = f.Length >= 7 ? Int(f[0]) : 0;
                    long shares = f.Length >= 7 ? Long(f[1]) : 0;
                    if (id <= 0 || shares <= 0 || b.Holding(id) != null || b.Holdings.Count >= 100)
                    {
                        continue;
                    }
                    b.Holdings.Add(new BotHolding { StockId = id, Shares = shares, Cost = Math.Max(0, Long(f[2])), StopCents = Math.Max(0, Int(f[3])), OpenedDay = Int(f[4]), Gain = Long(f[5]),
                        Merged = Int(f[6]) != 0 });
                }
                foreach (string r in S(c, 26).Length > 0 ? c[26].Split(':') : Array.Empty<string>())
                {
                    string[] f = r.Split(',');
                    if (f.Length >= 3 && Int(f[0]) > 0 && Long(f[2]) > 0 && b.Pending.Count < 100)
                    {
                        b.Pending.Add(new BotOrder { StockId = Int(f[0]), Buy = Int(f[1]) != 0, Qty = Long(f[2]) });
                    }
                }
                s.Bots.Add(b);
            }
        }

        // A bot's shares written into the player's holding too (MergeBots) taken out of it again; a bot keeps no more of a
        // company than the line's holding had (a line a version before bots wrote again has none of theirs in it).
        private static void Unmerge(MarketState s)
        {
            for (int i = 0; i < s.Bots.Count; i++)
            {
                Bot b = s.Bots[i];
                for (int j = b.Holdings.Count - 1; j >= 0; j--)
                {
                    BotHolding h = b.Holdings[j];
                    if (!h.Merged)
                    {
                        continue;
                    }
                    h.Merged = false;
                    Position p = s.Position(h.StockId);
                    long take = p != null && !p.Test ? Math.Max(0L, Math.Min(h.Shares, p.Shares)) : 0L;
                    if (take < h.Shares)
                    {
                        h.Cost = h.Shares > 0 ? h.Cost * take / h.Shares : 0;
                        h.Shares = take;
                    }
                    if (p != null && take > 0)
                    {
                        p.Shares -= take;
                        p.Cost = Math.Max(0, p.Cost - h.Cost);
                        if (p.Shares == 0)
                        {
                            s.Positions.Remove(p);
                        }
                    }
                    if (h.Shares <= 0)
                    {
                        b.Holdings.RemoveAt(j);
                    }
                }
            }
        }

        // The player's own way to the market and their company's books (never in the city's line).
        private static void Listing(StringBuilder t, ListingFile f)
        {
            t.Append(((int)f.Stage).ToString(Inv));
            Join(t, f.AdvisedDay, f.AuditFrom, f.AuditDone, f.AuditDay, f.Findings, f.FloatBp, f.LowCents, f.HighCents, f.WalkCents, f.BookFrom, f.BookEnd, f.PriceCents, f.ListDay);
            L(t, f.OldShares, f.NewShares);
            Join(t, f.FairCents);
            t.Append('|').Append(Clean(f.Name)).Append('|').Append(Clean(f.Ticker));
            Join(t, f.Sector, f.StockId, f.Test ? 1 : 0);
            L(t, f.Raised, f.Fees);
            Join(t, f.FeeYear, f.FirstDay, f.BooksDay);
            t.Append('|');
            for (int i = 0; i < f.Books.Count; i++)
            {
                BookQuarter q = f.Books[i];
                t.Append(i > 0 ? ":" : string.Empty).Append(q.Quarter.ToString(Inv)).Append(',').Append(q.Revenue.ToString(Inv)).Append(',').Append(q.Profit.ToString(Inv)).Append(',')
                    .Append(q.Days.ToString(Inv));
            }
            Join(t, f.SetAsideDay, f.Trade, f.TestBooks ? 1 : 0);
            L(t, f.Proceeds);
            t.Append('|').Append(Clean(f.HandOverTo));
            Join(t, f.HandOverBy);
            L(t, f.HandOverOwed);
            Join(t, f.HandOverCount);
            t.Append('|');
            for (int i = 0; i < f.HandOver.Count; i++)
            {
                t.Append(i > 0 ? ":" : string.Empty).Append((f.HandOver[i] ?? string.Empty).Replace('~', '!'));   // (a place's own '~' kept apart from the line's)
            }
        }

        private static ListingFile ReadListing(string[] c)
        {
            var f = new ListingFile
            {
                Stage = (ListingStage)Clamp(I(c, 0), 0, (int)ListingStage.Listed), AdvisedDay = I(c, 1), AuditFrom = I(c, 2), AuditDone = I(c, 3), AuditDay = Math.Max(0, I(c, 4)),
                Findings = Math.Max(0, I(c, 5)), FloatBp = Clamp(I(c, 6), 0, 9000), LowCents = Math.Max(0, I(c, 7)), HighCents = Math.Max(0, I(c, 8)),
                WalkCents = Math.Max(0, I(c, 9)), BookFrom = I(c, 10), BookEnd = I(c, 11), PriceCents = Math.Max(0, I(c, 12)), ListDay = I(c, 13), OldShares = Math.Max(0, L(c, 14)),
                NewShares = Math.Max(0, L(c, 15)), FairCents = Math.Max(0, I(c, 16)), Name = S(c, 17), Ticker = S(c, 18), Sector = Clamp(I(c, 19), 0, (int)MarketSector.Count - 1),
                StockId = I(c, 20), Test = I(c, 21) != 0, Raised = L(c, 22), Fees = Math.Max(0, L(c, 23)), FeeYear = I(c, 24, -1), FirstDay = Math.Max(0, I(c, 25)), BooksDay = I(c, 26)
            };
            foreach (string r in S(c, 27).Length > 0 ? c[27].Split(':') : Array.Empty<string>())
            {
                string[] q = r.Split(',');
                if (q.Length >= 4 && f.Books.Count < 12)
                {
                    f.Books.Add(new BookQuarter { Quarter = Int(q[0]), Revenue = Math.Max(0, Long(q[1])), Profit = Long(q[2]), Days = Math.Max(0, Int(q[3])) });
                }
            }
            f.SetAsideDay = Math.Max(0, I(c, 28));
            f.Trade = Clamp(I(c, 29, -1), -1, MarketInputs.Trades.Length - 1);
            f.TestBooks = I(c, 30) != 0;
            f.Proceeds = Math.Max(0, L(c, 31));
            f.HandOverTo = S(c, 32).Length > 0 ? S(c, 32) : null;
            f.HandOverBy = Math.Max(0, I(c, 33));
            f.HandOverOwed = Math.Max(0, L(c, 34));
            f.HandOverCount = Math.Max(0, I(c, 35));
            foreach (string place in S(c, 36).Length > 0 ? c[36].Split(':') : Array.Empty<string>())
            {
                if (place.Length > 0 && f.HandOver.Count < 64)
                {
                    f.HandOver.Add(place.Replace('!', '~'));
                }
            }
            if (f.StockId != 0 && !ListingRules.IsPlayer(f.StockId))
            {
                f.StockId = 0;
            }
            return f;
        }

        // Today's step candles are kept for the companies the player follows: watched, held or with an order in.
        private static bool Followed(MarketState state, int id)
        {
            if (state.Watching(id) || state.Held(id) != 0)
            {
                return true;
            }
            for (int i = 0; i < state.Orders.Count; i++)
            {
                if (state.Orders[i].StockId == id && state.Orders[i].IsOpen)
                {
                    return true;
                }
            }
            return false;
        }

        private static void Stock(StringBuilder t, MarketState state, Stock k, bool city)
        {
            t.Append(k.Id.ToString(Inv));
            Join(t, (int)k.Status, k.ListedDay, k.GoneDay, (int)k.Reason, k.GoneCents);
            t.Append('|').Append(MarketCatalogue.IsRival(k.Id) ? Clean(k.Name) : string.Empty).Append('|').Append(Clean(k.RivalId));
            D(t, k.LogFair);
            D(t, k.Mis);
            D(t, k.Sent);
            D(t, k.Outlook);
            L(t, k.Shares);
            D(t, city ? 0.0 : k.Impact);
            Join(t, k.PrevClose, k.Open, k.High, k.Low, k.Last);
            L(t, k.DayVolume);
            D(t, k.Rev);
            D(t, k.Margin);
            D(t, k.Growth);
            D(t, k.MarginShift);
            D(t, k.MarginTarget);
            D(t, k.GrowthTarget);
            D(t, k.Debt);
            D(t, k.DebtRate);
            D(t, k.Equity);
            D(t, k.Distress);
            D(t, k.Competition);
            Join(t, k.Quarter);
            D(t, k.QRev);
            D(t, k.QNi);
            Join(t, k.PendingQuarter);
            D(t, k.PendingRev);
            D(t, k.PendingNi);
            Join(t, k.ResultsDay, k.DpsCents, k.DeclaredCents, k.RecordDay, k.PayDay, k.ExDone ? 1 : 0, k.LastExDay);
            D(t, k.Adv);
            Join(t, k.SplitDay, k.SplitRatio, k.BidDay, k.BidEndDay, k.BidCents, k.BidderId, k.ResultsBoostDay, k.LowDays);
            t.Append('|');
            for (int i = 0; i < k.Results.Count; i++)
            {
                QuarterResult r = k.Results[i];
                t.Append(i > 0 ? ":" : string.Empty).Append(r.Quarter.ToString(Inv)).Append(',').Append(r.Day.ToString(Inv)).Append(',').Append(r.RevenueK.ToString(Inv)).Append(',')
                    .Append(r.ProfitK.ToString(Inv)).Append(',').Append(r.Eps.ToString(Inv)).Append(',').Append(r.Consensus.ToString(Inv)).Append(',').Append(r.DpsCents.ToString(Inv));
            }
            t.Append('|');
            MarketCodec.Write(t, k.Daily);
            t.Append('|');
            MarketCodec.Write(t, k.Monthly);
            bool followed = city || Followed(state, k.Id);
            t.Append('|');
            MarketCodec.Write(t, k.Bars, followed ? k.BarCount : 0);
            t.Append('|');
            MarketCodec.Write(t, k.LastBars, followed ? k.LastBarCount : 0);
            Join(t, k.LastBarsDay);
            if (k.Own != null)
            {
                Own(t, k);
            }
        }

        /// <summary>One company's record alone (a player's company set aside while its founder is a guest in another city: ListingFile.Suspended).</summary>
        public static string StockLine(MarketState state, Stock k)
        {
            var t = new StringBuilder(4096);
            Stock(t, state, k, true);
            return t.ToString();
        }

        /// <summary>A company read back from its record alone; null if it cannot be read.</summary>
        public static Stock ReadStockLine(string record, int year)
        {
            try
            {
                return string.IsNullOrEmpty(record) ? null : ReadStock(record.Split('|'), year);
            }
            catch (Exception)
            {
                return null;
            }
        }

        // A player's company: its founder, name, ticker, sector and usual debt, then its own figures, then its founder's deals.
        private static void Own(StringBuilder t, Stock k)
        {
            OwnListing o = k.Own;
            t.Append('|').Append(o.Owner.ToString("X16", Inv)).Append('|').Append(Clean(k.Name)).Append('|').Append(Clean(k.Ticker));
            Join(t, (int)k.Sector, o.DebtBp);
            t.Append('|').Append(o.Test ? 1 : 0).Append(',').Append(o.Founder.ToString(Inv)).Append(',').Append(R(o.Rps)).Append(',').Append(o.PayBp.ToString(Inv)).Append(',')
                .Append(o.ProposedCents.ToString(Inv)).Append(',').Append(o.Board ? 1 : 0).Append(',').Append(R(o.RevQ)).Append(',').Append(R(o.NiQ)).Append(',').Append(R(o.RevY)).Append(',')
                .Append(R(o.NiY)).Append(',').Append(R(o.LeverY)).Append(',').Append(R(o.Expect)).Append(',').Append(R(o.PendingExpect)).Append(',').Append(o.ListedCents.ToString(Inv)).Append(',')
                .Append(o.LockDay.ToString(Inv)).Append(',').Append(o.AgmDay.ToString(Inv)).Append(',').Append(o.YearCents.ToString(Inv)).Append(',').Append(o.YearIndex.ToString(Inv)).Append(',')
                .Append(o.YearScandals.ToString(Inv)).Append(',').Append(o.YearDividendCents.ToString(Inv)).Append(',').Append(o.LastStanding.ToString(Inv)).Append(',')
                .Append(o.ActBp.ToString(Inv)).Append(',').Append(((int)o.ActDemand).ToString(Inv)).Append(',').Append(o.ActDay.ToString(Inv)).Append(',').Append(o.Votes.ToString(Inv)).Append(',')
                .Append(o.VotesAgainstBp.ToString(Inv)).Append(',').Append(o.IssuedYear.ToString(Inv)).Append(',').Append(o.IssuedBp.ToString(Inv)).Append(',').Append(o.SaleDay.ToString(Inv))
                .Append(',').Append(R(o.Carry)).Append(',').Append(o.DealYear.ToString(Inv)).Append(',').Append(o.BoughtBp.ToString(Inv)).Append(',').Append(o.SoldBp.ToString(Inv)).Append(',')
                .Append(o.BackBp.ToString(Inv));
            t.Append('|');
            for (int i = 0; i < o.Deals.Count; i++)
            {
                OwnDeal d = o.Deals[i];
                t.Append(i > 0 ? ":" : string.Empty).Append(d.Day.ToString(Inv)).Append(',').Append(d.Shares.ToString(Inv)).Append(',').Append(d.Cents.ToString(Inv));
            }
            t.Append('|').Append(Clean(k.Def.Trade));
        }

        private static string R(double value)
        {
            return double.IsNaN(value) || double.IsInfinity(value) ? "0" : value.ToString("R", Inv);
        }

        // A player's company read back: its row of the catalogue made again from its own fields, and its figures.
        private static OwnListing ReadOwn(string[] c, out CompanyDef def, int id)
        {
            def = null;
            if (c.Length < 66 || !ulong.TryParse(S(c, 59), NumberStyles.HexNumber, Inv, out ulong owner) || owner == 0UL)
            {
                return null;
            }
            string ticker = S(c, 61).Length > 0 ? S(c, 61) : "OWN";
            int debtBp = Clamp(I(c, 63), 0, 30000);
            def = MarketCatalogue.PlayerDef(id, S(c, 60), ticker, (MarketSector)Clamp(I(c, 62), 0, (int)MarketSector.Count - 1), debtBp, S(c, 66).Length > 0 ? S(c, 66) : null);
            string[] f = S(c, 64).Split(',');
            var o = new OwnListing
            {
                Owner = owner, Test = I(f, 0) != 0, Founder = Math.Max(0, L(f, 1)), Rps = Clamp(F(f, 2, 0.0), -1e7, 1e7), PayBp = Clamp(I(f, 3), 0, 10000),
                ProposedCents = Clamp(I(f, 4), 0, 10000000), Board = I(f, 5, 1) != 0, RevQ = Math.Max(0.0, F(f, 6, 0.0)), NiQ = F(f, 7, 0.0), RevY = Math.Max(0.0, F(f, 8, 0.0)),
                NiY = F(f, 9, 0.0), LeverY = Clamp(F(f, 10, 0.0), -1.0, 1.0), Expect = F(f, 11, 0.0), PendingExpect = F(f, 12, 0.0), ListedCents = Math.Max(0, I(f, 13)), LockDay = I(f, 14),
                AgmDay = Math.Max(0, I(f, 15)), YearCents = Math.Max(0, I(f, 16)), YearIndex = Math.Max(0, I(f, 17)), YearScandals = Math.Max(0, I(f, 18)), YearDividendCents = Math.Max(0, I(f, 19)),
                LastStanding = Clamp(I(f, 20, -1), -1, 100), ActBp = Clamp(I(f, 21), 0, 10000), ActDemand = (ActivistDemand)Clamp(I(f, 22), 0, (int)ActivistDemand.Count - 1),
                ActDay = Math.Max(0, I(f, 23)), Votes = Math.Max(0, I(f, 24)), VotesAgainstBp = Clamp(I(f, 25), 0, 10000), IssuedYear = I(f, 26, -1), IssuedBp = Math.Max(0, I(f, 27)),
                SaleDay = Math.Max(0, I(f, 28)), Carry = Clamp(F(f, 29, 0.0), -1.0, 1.0), DebtBp = debtBp, DealYear = I(f, 30, -1), BoughtBp = Math.Max(0, I(f, 31)), SoldBp = Math.Max(0, I(f, 32)),
                BackBp = Math.Max(0, I(f, 33))
            };
            foreach (string r in S(c, 65).Length > 0 ? c[65].Split(':') : Array.Empty<string>())
            {
                string[] d = r.Split(',');
                if (d.Length >= 3 && o.Deals.Count < 16)
                {
                    o.Deals.Add(new OwnDeal { Day = Int(d[0]), Shares = Long(d[1]), Cents = Math.Max(1, Int(d[2])) });
                }
            }
            if (o.ActDemand == ActivistDemand.None)
            {
                o.ActBp = 0;
            }
            return o;
        }

        // ---------------------------------------------------------------- reading

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
            string suspended = null, bots = null;
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
                    case 'e': MarketEnv env; if (MarketTape.ReadEnv(body, out env)) s.Env = env; break;
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
                                s.Positions.Add(new Position { StockId = I(c, 0), Shares = L(c, 1), Cost = Math.Max(0, L(c, 2)), Dividends = L(c, 3), Realised = L(c, 4), Test = I(c, 5) != 0, OpenedDay = I(c, 6) });
                            }
                        }
                        break;
                    case 'o':
                        foreach (string record in Records(body))
                        {
                            string[] c = record.Split('|');
                            if (c.Length < 18 || I(c, 0) <= 0 || L(c, 5) <= 0 || s.Orders.Count >= MarketCatalogue.OrdersMost * 2)
                            {
                                continue;
                            }
                            var o = new Order
                            {
                                Id = I(c, 0), StockId = I(c, 1), Side = (OrderSide)Clamp(I(c, 2), 0, 1), Type = (OrderType)Clamp(I(c, 3), 0, 2), Life = (OrderLife)Clamp(I(c, 4), 0, 1),
                                Qty = L(c, 5), Filled = Math.Max(0, Math.Min(L(c, 5), L(c, 6))), FilledValue = Math.Max(0, L(c, 7)), LimitCents = Math.Max(0, I(c, 8)), StopCents = Math.Max(0, I(c, 9)),
                                PlacedDay = I(c, 10), PlacedPhase = I(c, 11), ExpireDay = I(c, 12), State = (OrderState)Clamp(I(c, 13), 0, 4), Why = (OrderWhy)Clamp(I(c, 14), 0, (int)OrderWhy.Count - 1),
                                Triggered = I(c, 15) != 0, Test = I(c, 16) != 0, Forced = I(c, 17) != 0, Closing = I(c, 18) != 0
                            };
                            if ((o.Type == OrderType.Limit && o.LimitCents <= 0) || (o.Type == OrderType.Stop && o.StopCents <= 0))
                            {
                                continue;
                            }
                            o.Closing &= o.Type != OrderType.Market;
                            s.Orders.Add(o);
                        }
                        break;
                    case 't':
                        foreach (string record in Records(body))
                        {
                            string[] c = record.Split('|');
                            if (c.Length >= 9 && s.Trades.Count < MarketCatalogue.TradesKept)
                            {
                                s.Trades.Add(new Trade { Day = I(c, 0), Phase = I(c, 1), StockId = I(c, 2), Kind = (TradeKind)Clamp(I(c, 3), 0, 9), Qty = L(c, 4), PriceCents = I(c, 5), Value = L(c, 6), Fee = L(c, 7), Test = I(c, 8) != 0,
                                    Bot = MarketBots.Firm(I(c, 9)) != null ? I(c, 9) : 0 });
                            }
                        }
                        break;
                    case 'w':
                        foreach (string id in body.Length > 0 ? body.Split(',') : Array.Empty<string>())
                        {
                            int value;
                            if (int.TryParse(id, NumberStyles.Integer, Inv, out value) && value > 0 && !s.Watch.Contains(value) && s.Watch.Count < MarketCatalogue.WatchMost)
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
                ReadBots(s, bots);   // (after the holdings: their shares are taken out of the player's)
                Unmerge(s);
            }
            if (s.Listing != null && suspended != null && ReadStockLine(suspended, s.YearDays) != null)
            {
                s.Listing.Suspended = suspended;
            }
            Repair(s);
            MarketLife.DayBases(s, s.InSession);   // (a save made after a close by 0.15.1 or older has every company's at its close)
            return s;
        }

        private static bool Header(MarketState s, string[] c)
        {
            ulong id;
            if (c.Length < 6 || !ulong.TryParse(c[0], NumberStyles.HexNumber, Inv, out id))
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
            if (MarketState.Time(s.FrontierDay, s.FrontierPhase) < s.Now)
            {
                s.FrontierDay = s.Day;
                s.FrontierPhase = s.Phase;
            }
            return true;
        }

        private static Stock ReadStock(string[] c, int year)
        {
            int id = I(c, 0);
            CompanyDef def = MarketCatalogue.Find(id);
            OwnListing own = null;
            if (def == null && ListingRules.IsPlayer(id))
            {
                own = ReadOwn(c, out def, id);   // a player's company: its row is in its record
            }
            if (def == null || c.Length < 54)
            {
                return null;
            }
            var k = new Stock
            {
                Id = id, Def = def, Name = MarketCatalogue.IsRival(id) ? S(c, 6) : def.Name, Ticker = def.Ticker, Sector = def.Sector, Size = def.Size,
                Status = I(c, 1) == (int)StockStatus.Gone ? StockStatus.Gone : StockStatus.Listed, ListedDay = I(c, 2), GoneDay = I(c, 3), Reason = (GoneReason)Clamp(I(c, 4), 0, 3),
                GoneCents = Math.Max(0, I(c, 5)), RivalId = S(c, 7).Length > 0 ? S(c, 7) : null
            };
            if (MarketCatalogue.IsRival(id) && string.IsNullOrEmpty(k.Name))
            {
                return null;
            }
            k.LogFair = Clamp(F(c, 8, 0.0), -10.0, 15.0);
            k.Mis = Clamp(F(c, 9, 0.0), -3.0, 3.0);
            k.Sent = Clamp(F(c, 10, 0.0), -3.0, 3.0);
            k.Outlook = Clamp(F(c, 11, 0.0), -8.0, 8.0);
            k.Shares = Math.Max(1000, L(c, 12));
            k.Impact = Math.Max(0.0, F(c, 13, 0.0));
            k.PrevClose = Math.Max(1, I(c, 14, 1));
            k.Open = Math.Max(1, I(c, 15, 1));
            k.High = Math.Max(1, I(c, 16, 1));
            k.Low = Math.Max(1, I(c, 17, 1));
            k.Last = Math.Max(1, I(c, 18, 1));
            k.DayVolume = Math.Max(0, L(c, 19));
            k.Rev = Math.Max(1.0, F(c, 20, def.RevenueM * 1e6));
            k.Margin = Clamp(F(c, 21, def.MarginBp / 10000.0), -0.5, 0.8);
            k.Growth = Clamp(F(c, 22, def.GrowthBp / 10000.0), -0.25, 0.4);
            k.MarginShift = own != null ? Clamp(F(c, 23, 0.0), -2.0, 2.0) : Clamp(F(c, 23, 0.0), -0.3, 0.9);   // (a player's company's usual margin is its own books')
            k.MarginTarget = Clamp(F(c, 24, k.Margin), -1.0, 1.0);
            k.GrowthTarget = Clamp(F(c, 25, MarketCatalogue.LongRealGrowth), -0.25, 0.4);
            k.Debt = Math.Max(0.0, F(c, 26, 0.0));
            k.DebtRate = Clamp(F(c, 27, 0.05), 0.0, 0.5);
            k.Equity = F(c, 28, 0.0);
            k.Distress = Clamp(F(c, 29, 0.0), 0.0, 1.0);
            k.Competition = Clamp(F(c, 30, 0.0), 0.0, MarketInputs.CompetitionMost);
            k.Quarter = I(c, 31, -1);
            k.QRev = F(c, 32, 0.0);
            k.QNi = F(c, 33, 0.0);
            k.PendingQuarter = I(c, 34, -1);
            k.PendingRev = F(c, 35, 0.0);
            k.PendingNi = F(c, 36, 0.0);
            k.ResultsDay = Math.Max(0, I(c, 37));
            k.DpsCents = Math.Max(0, I(c, 38));
            k.DeclaredCents = Math.Max(0, I(c, 39));
            k.RecordDay = Math.Max(0, I(c, 40));
            k.PayDay = Math.Max(0, I(c, 41));
            k.ExDone = I(c, 42, 1) != 0;
            k.LastExDay = I(c, 43);
            k.Adv = Math.Max(1.0, F(c, 44, 1000.0));
            k.SplitDay = Math.Max(0, I(c, 45));
            k.SplitRatio = Clamp(I(c, 46), 0, 10);
            k.BidDay = Math.Max(0, I(c, 47));
            k.BidEndDay = Math.Max(0, I(c, 48));
            k.BidCents = Math.Max(0, I(c, 49));
            k.BidderId = Math.Max(0, I(c, 50));
            k.ResultsBoostDay = I(c, 51);
            k.LowDays = Math.Max(0, I(c, 52));
            if (k.SplitDay > 0 && k.SplitRatio < 2)
            {
                k.SplitDay = 0;
            }
            if (k.BidCents > 0 && k.BidEndDay <= 0)
            {
                k.BidCents = 0;
            }
            foreach (string r in S(c, 53).Length > 0 ? c[53].Split(':') : Array.Empty<string>())
            {
                string[] f = r.Split(',');
                if (f.Length >= 7 && k.Results.Count < MarketCatalogue.QuartersKept)
                {
                    k.Results.Add(new QuarterResult { Quarter = Int(f[0]), Day = Int(f[1]), RevenueK = Long(f[2]), ProfitK = Long(f[3]), Eps = Int(f[4]), Consensus = Int(f[5]), DpsCents = Math.Max(0, Int(f[6])) });
                }
            }
            if (c.Length > 54)
            {
                MarketCodec.Read(c[54], k.Daily, MarketCatalogue.DailyKept);
            }
            if (c.Length > 55)
            {
                MarketCodec.Read(c[55], k.Monthly, MarketCatalogue.MonthlyKept);
            }
            k.BarCount = c.Length > 56 ? MarketCodec.Read(c[56], k.Bars) : 0;
            k.LastBarCount = c.Length > 57 ? MarketCodec.Read(c[57], k.LastBars) : 0;
            k.LastBarsDay = I(c, 58);
            if (double.IsNaN(k.LogFair) || double.IsNaN(k.Mis) || double.IsInfinity(k.LogFair + k.Mis))
            {
                k.LogFair = Math.Log(k.Last / 100.0);
                k.Mis = k.Sent = 0.0;
            }
            k.Own = own;
            if (own != null)
            {
                own.Founder = Math.Min(own.Founder, k.Shares);
            }
            return k;
        }

        // What points at something that is not there: a holding or an order of a company missing from the line has its
        // company made again at its catalogue price (a line that lost a record), an order of an unknown company goes.
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
            }
            for (int i = 0; i < s.Stocks.Count; i++)
            {
                if (s.Stocks[i].Listed && s.Stocks[i].Quarter < 0)
                {
                    s.Stocks[i].Quarter = MarketCatalogue.QuarterOf(s.Day, s.YearDays);
                }
            }
        }

        // ---------------------------------------------------------------- the small parts

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

        private static void G(StringBuilder t, double value)
        {
            t.Append('|').Append(double.IsNaN(value) || double.IsInfinity(value) ? "0" : value.ToString("G6", Inv));
        }

        private static void Ints(StringBuilder t, List<int> values)
        {
            MarketCodec.WriteInts(t, values);
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
            int value;
            return at < c.Length && int.TryParse(c[at], NumberStyles.Integer, Inv, out value) ? value : fallback;
        }

        private static long L(string[] c, int at, long fallback = 0)
        {
            long value;
            return at < c.Length && long.TryParse(c[at], NumberStyles.Integer, Inv, out value) ? value : fallback;
        }

        private static double F(string[] c, int at, double fallback)
        {
            double value;
            return at < c.Length && double.TryParse(c[at], NumberStyles.Float, Inv, out value) && !double.IsNaN(value) && !double.IsInfinity(value) ? value : fallback;
        }

        private static string S(string[] c, int at)
        {
            return at < c.Length ? c[at] : string.Empty;
        }

        private static int Int(string text)
        {
            int value;
            return int.TryParse(text, NumberStyles.Integer, Inv, out value) ? value : 0;
        }

        private static long Long(string text)
        {
            long value;
            return long.TryParse(text, NumberStyles.Integer, Inv, out value) ? value : 0;
        }

        private static int Clamp(int value, int least, int most)
        {
            return value < least ? least : value > most ? most : value;
        }

        private static double Clamp(double value, double least, double most)
        {
            return value < least ? least : value > most ? most : value;
        }

        /// <summary>A text with the dividers taken out, so that it can never break the line apart.</summary>
        public static string Clean(string text)
        {
            return string.IsNullOrEmpty(text) ? string.Empty : text.Replace('|', ' ').Replace(';', ' ').Replace('~', ' ').Replace(':', ' ').Replace(',', ' ');
        }
    }
}
