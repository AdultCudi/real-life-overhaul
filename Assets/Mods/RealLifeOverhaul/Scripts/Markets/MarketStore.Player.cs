using System;
using System.Text;

namespace RealLifeOverhaul.Markets
{
    /// <summary>Part of the save line: the player's orders, worked orders, bots and way to the market.</summary>
    public static partial class MarketStore
    {
        // An order's own fields, as part o has had them from the first version: its number first.
        private static void OrderFields(StringBuilder t, Order o)
        {
            t.Append(o.Id.ToString(Inv));
            Join(t, o.StockId, (int)o.Side, (int)o.Type, (int)o.Life);
            L(t, o.Qty, o.Filled, o.FilledValue);
            Join(t, o.LimitCents, o.StopCents, o.PlacedDay, o.PlacedPhase, o.ExpireDay, (int)o.State, (int)o.Why, o.Triggered ? 1 : 0, o.Test ? 1 : 0, o.Forced ? 1 : 0);
        }

        // Part g: each an order's fields, then what it is, its money, the day it stopped and its last day's slices, then the
        // day's money with the broker and the day it was parked.
        private static void WorkedOrders(StringBuilder t, MarketState s)
        {
            int n = 0;
            for (int i = 0; i < s.Orders.Count; i++)
            {
                Order o = s.Orders[i];
                if (!o.Worked && !o.Back)
                {
                    continue;
                }
                t.Append(n++ > 0 ? ";" : string.Empty);
                OrderFields(t, o);
                Join(t, o.Closing ? 1 : 0, o.Worked ? 1 : 0, o.Back ? 1 : 0);
                L(t, o.Budget);
                Join(t, o.EndDay, o.DayOf);
                L(t, o.DayQty, o.DayValue);
                L(t, o.Parked, o.Due);
                Join(t, o.ParkedDay);
            }
        }

        // An order's own fields read back; null for one that cannot be. A payout put back has no shares yet: 'back' lets it through.
        private static Order ReadOrder(string[] c, bool back)
        {
            if (c.Length < 18 || I(c, 0) <= 0 || (L(c, 5) <= 0 && !back))
            {
                return null;
            }
            var o = new Order
            {
                Id = I(c, 0), StockId = I(c, 1), Side = (OrderSide)Clamp(I(c, 2), 0, 1), Type = (OrderType)Clamp(I(c, 3), 0, 2), Life = (OrderLife)Clamp(I(c, 4), 0, 1),
                Qty = Math.Max(0, L(c, 5)), Filled = Math.Max(0, Math.Min(L(c, 5), L(c, 6))), FilledValue = Math.Max(0, L(c, 7)), LimitCents = Math.Max(0, I(c, 8)),
                StopCents = Math.Max(0, I(c, 9)), PlacedDay = I(c, 10), PlacedPhase = I(c, 11), ExpireDay = I(c, 12), State = (OrderState)Clamp(I(c, 13), 0, 4),
                Why = (OrderWhy)Clamp(I(c, 14), 0, (int)OrderWhy.Count - 1), Triggered = I(c, 15) != 0, Test = I(c, 16) != 0, Forced = I(c, 17) != 0, Closing = I(c, 18) != 0
            };
            if ((o.Type == OrderType.Limit && o.LimitCents <= 0) || (o.Type == OrderType.Stop && o.StopCents <= 0))
            {
                return null;
            }
            o.Closing &= o.Type != OrderType.Market;
            return o;
        }

        // Part g read back. It has a limit of its own, apart from part o's, and a worked order still open or with money with
        // the broker is read whatever the count, so no worked order and none of the player's money is lost on a load.
        private static void ReadWorked(MarketState s, string body)
        {
            int read = 0;
            foreach (string record in Records(body))
            {
                string[] c = record.Split('|');
                bool worked = I(c, 19) != 0, back = !worked && I(c, 20) != 0;
                bool keep = worked && (I(c, 13) == (int)OrderState.Open || L(c, 26) > 0 || L(c, 27) > 0);
                Order o = (worked || back) && (keep || read < MarketCatalogue.OrdersMost * 2) ? ReadOrder(c, back) : null;
                if (o == null || s.Order(o.Id) != null || (back && Math.Max(0, L(c, 21)) <= 0))
                {
                    continue;
                }
                read++;
                o.Type = OrderType.Market;
                o.Closing = false;
                o.Worked = worked;
                o.Back = back;
                o.Life = worked ? OrderLife.UntilDone : OrderLife.Day;
                o.Budget = back ? Math.Max(0, L(c, 21)) : 0;
                o.EndDay = Math.Max(0, I(c, 22));
                o.DayOf = Math.Max(0, I(c, 23));
                o.DayQty = Math.Max(0, L(c, 24));
                o.DayValue = Math.Max(0, L(c, 25));
                o.Parked = worked ? Math.Max(0, L(c, 26)) : 0;   // a record without them: nothing parked, nothing due
                o.Due = worked ? Math.Max(0, L(c, 27)) : 0;
                o.ParkedDay = worked ? Math.Max(0, I(c, 28)) : 0;
                s.Orders.Add(o);
            }
            // Every order back in its place: the market fills them in the order of their numbers.
            s.Orders.Sort(ById);
        }

        private static readonly Comparison<Order> ById = (a, b) => a.Id.CompareTo(b.Id);

        // Every bot's shares of a company added to a holding as it is written, each of their holdings marked so.
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
                // Then what an older reader stops before: its scope, payouts and trading in the player's own company.
                Join(t, (int)b.Scope);
                t.Append('|');
                for (int j = 0; j < b.Picks.Count; j++)
                {
                    t.Append(j > 0 ? ":" : string.Empty).Append(b.Picks[j].ToString(Inv));
                }
                Join(t, b.PayPct, (int)b.PayEvery);
                L(t, b.PayMark, b.PayOwed, b.PaidOut, b.MonthPaid);
                Join(t, b.OwnDay);
                L(t, b.OwnQty);
                Join(t, b.OwnYear, b.OwnBoughtBp, b.OwnSoldBp, b.OwnBuyDay, b.OwnSellDay);
                Join(t, b.TopUp);   // then capital added while it runs, and its fee's money-days
                L(t, b.MonthAdded);
            }
        }

        // The bots read back, each figure in bounds; a firm that is not one, or one twice, dropped.
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
                // A record from before scope and payouts: its whole market, nothing paid, and what it made before counted as
                // reckoned already.
                b.Scope = (BotScope)Clamp(I(c, 27), 0, (int)BotScope.Held);
                foreach (string r in S(c, 28).Length > 0 ? c[28].Split(':') : Array.Empty<string>())
                {
                    int id = Int(r);
                    if (id > 0 && !b.Picks.Contains(id) && b.Picks.Count < MarketBots.PicksMost)
                    {
                        b.Picks.Add(id);
                    }
                }
                b.PayPct = MarketBots.Choice(MarketBots.PayPcts, I(c, 29));
                b.PayEvery = (BotPayEvery)Clamp(I(c, 30, (int)BotPayEvery.Monthly), 0, (int)BotPayEvery.Monthly);
                b.PayMark = c.Length > 31 ? L(c, 31) : MarketBots.Profit(b);
                b.PayOwed = Math.Max(0, L(c, 32));
                b.PaidOut = Math.Max(0, L(c, 33));
                b.MonthPaid = Math.Max(0, L(c, 34));
                b.OwnDay = I(c, 35);
                b.OwnQty = Math.Max(0, L(c, 36));
                b.OwnYear = I(c, 37);
                b.OwnBoughtBp = Clamp(I(c, 38), 0, 100000);
                b.OwnSoldBp = Clamp(I(c, 39), 0, 100000);
                b.OwnBuyDay = I(c, 40);
                b.OwnSellDay = I(c, 41);
                b.TopUp = Clamp(I(c, 42), 0, MarketBots.TopUpDecidedAgain);
                b.MonthAdded = L(c, 43);
                s.Bots.Add(b);
            }
        }

        // A bot's shares written into the player's holding too, taken out of it again; a bot keeps no more of a company than
        // the line's holding had.
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

        // The player's own way to the market and their company's books.
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
                t.Append(i > 0 ? ":" : string.Empty).Append((f.HandOver[i] ?? string.Empty).Replace('~', '!'));   // a place's own '~' kept apart from the line's
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
    }
}
