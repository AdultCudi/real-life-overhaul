using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// The bank's trading desk in its day (BankDesk): the clients' brokerage, whose commission follows the market's activity and, when the exchange has them,
    /// options and futures; and the own book, a limited share of the capital held in the market's companies by a mandate, marked to the day's prices and cut
    /// when its loss from its peak passes the owner's limit. Game-free; a few sums a day, and the book's weekly trade sorts the quotes once.
    /// </summary>
    public static partial class OwnBankRules
    {
        private static readonly List<int> DeskOrder = new List<int>(160);   // places in the quotes, worked in here
        private static readonly List<int> DeskPicked = new List<int>(12);

        /// <summary>Whether a mandate may hold a company: listed, not under a bid, not too distressed for it, of the size the mandate wants.</summary>
        public static bool DeskEligible(FundQuote q, int mandate)
        {
            int m = BankDesk.MandateOf(mandate);
            if (m == BankDesk.None || !q.Listed || q.UnderBid || q.LastCents <= 0 || q.Distress > BankDesk.Distress[m])
            {
                return false;
            }
            return m == BankDesk.Index ? q.Size <= 1 : m == BankDesk.Income ? q.Size <= 1 && q.DpsCents > 0 : q.Size >= 1;
        }

        /// <summary>What the own book may hold in all now: its share of the capital, as far as the cash and the lending line allow.</summary>
        public static long DeskRoom(OwnBank b) => DeskRoom(b, 0);

        private static long DeskRoom(OwnBank b, long hold)
        {
            if (!b.DeskOn || b.DeskMandate == BankDesk.None || b.DeskShare <= 0)
            {
                return 0;
            }
            long value = b.BookValue;
            long byShare = Math.Max(0, CapitalNow(b)) * Math.Min(BankDesk.ShareMost, b.DeskShare) / 1000;
            long byCash = FreeCash(b) - hold + value;
            long others = RiskWeighted(b) - value * BankDesk.Weight / 1000;
            long byCapital = (FreeCapital(b) * 1000 / LendRatioOf(b) - others) * 1000 / BankDesk.Weight;
            return Math.Max(0, Math.Min(byShare, Math.Min(byCash, byCapital)));
        }

        /// <summary>How far the own book is under its peak, thousandths of the peak; 0 with no book.</summary>
        public static int DeskDrawdown(OwnBank b)
        {
            return b.DeskPeak > 0 ? (int)Math.Max(0L, Math.Min(1000L, (b.DeskPeak - b.BookValue) * 1000 / b.DeskPeak)) : 0;
        }

        /// <summary>The market's activity as the desk reads it, within bounds; 0 while the market is not running.</summary>
        public static int ActivityOf(BankCity c)
        {
            return c.Activity <= 0 ? 0 : Math.Max(BankDesk.ActivityLeast, Math.Min(BankDesk.ActivityMost, c.Activity));
        }

        /// <summary>The money the clients hold for trading, by kind of saver, in all.</summary>
        public static long DeskCustody(OwnBank b)
        {
            long sum = 0;
            for (int k = 0; k < OwnBankCatalogue.Savers; k++)
            {
                sum += b.Deposits[k] * BankDesk.Custody[k] / 1000;
            }
            return sum;
        }

        /// <summary>
        /// The desk's day. Brokerage: a day's trading of the clients' custody at the market's activity, the bank's commission on it and on options and futures,
        /// less the desk's running costs and the clients' failed margin calls in a wild market. Own book: prices brought up to date, a quarter's dividend a day,
        /// a company no longer allowed sold, the loss from the peak weighed against the limit (a cut keeps part of the book and stops buying for a month), and
        /// traded back to its parts weekly or after a choice.
        /// </summary>
        internal static void DeskDay(OwnBank b, BankCity c, int day, IList<FundQuote> quotes, BankDay into, long hold)
        {
            int y = Math.Max(1, c.YearDays), m = Math.Max(1, c.MonthDays);
            long level = Level(c.Level);
            if (b.DeskOn)
            {
                int act = ActivityOf(c);
                long custody = DeskCustody(b), commission = 0, derivs = 0, marginLoss = 0;
                for (int k = 0; k < OwnBankCatalogue.Savers; k++)
                {
                    long held = b.Deposits[k] * BankDesk.Custody[k] / 1000;
                    long traded = held * BankDesk.Turnover[k] / 1000 / y * act / 1000;
                    commission += traded * BankDesk.CommissionBp / 10000;
                    if (c.Derivs && act > 0)
                    {
                        long notional = held * BankDesk.DerivShare[k] / 1000 * BankDesk.Turnover[k] / 1000 / y * act / 1000;
                        derivs += notional * BankDesk.DerivCommissionBp / 10000;
                        if (act > BankDesk.MarginActivity)
                        {
                            marginLoss += notional * BankDesk.MarginLossBp / 10000 * (act - BankDesk.MarginActivity) / BankDesk.MarginActivity;
                        }
                    }
                }
                long costs = BankDesk.DayCents * level / PriceRules.Start + custody * BankDesk.CustodyCost / (1000L * y) + marginLoss;
                b.Reserves += commission + derivs - costs;
                b.Capital += commission + derivs - costs;
                into.Brokerage += commission;
                into.Derivs += derivs;
                into.DeskCosts += costs;
                b.DeskBrokerage += commission;
                b.DeskDerivs += derivs;
                b.DeskCommissionAll += commission + derivs;
            }
            DeskBook(b, c, day, quotes, into, hold, m);
        }

        private static void DeskBook(OwnBank b, BankCity c, int day, IList<FundQuote> quotes, BankDay into, long hold, int monthDays)
        {
            if (quotes == null)
            {
                return;
            }
            int y = Math.Max(1, c.YearDays);
            for (int i = b.Book.Count - 1; i >= 0; i--)
            {
                FundHolding h = b.Book[i];
                int at = OwnBankFund.IndexOf(quotes, h.StockId);
                if (at < 0)
                {
                    continue;
                }
                FundQuote q = quotes[at];
                h.Name = q.Name ?? h.Name;
                h.Ticker = q.Ticker ?? h.Ticker;
                if (!q.Listed)
                {
                    long payout = h.Qty * Math.Max(0, q.GoneCents);
                    long basis = h.CostCents;
                    h.CostCents = 0;
                    h.Qty = 0;
                    b.Reserves += payout;
                    b.Capital += payout - basis;
                    if (payout >= basis)
                    {
                        into.Gains += payout - basis;
                    }
                    else
                    {
                        into.FundLost += basis - payout;
                    }
                    b.Book.RemoveAt(i);
                    continue;
                }
                h.LastCents = q.LastCents;
                h.DpsCents = Math.Max(0, q.DpsCents);
                long dividend = h.Qty * h.DpsCents * 4 / y;
                b.Reserves += dividend;
                b.Capital += dividend;
                into.Dividends += dividend;
                if (!b.DeskOn || !DeskEligible(q, b.DeskMandate) || b.DeskShare <= 0)
                {
                    OwnBankFund.SellShares(b, b.Book, i, h.Qty, OwnBankCatalogue.FundSpreadBp * 10, into);
                }
            }
            if (b.Book.Count == 0 && (!b.DeskOn || b.DeskMandate == BankDesk.None || b.DeskShare <= 0))
            {
                b.DeskPeak = 0;
                b.DeskDirty = false;
                return;
            }
            // The risk limit: a loss from the peak past the owner's limit cuts the book to what a cut keeps, and nothing is bought for a month.
            long value = b.BookValue;
            b.DeskPeak = Math.Max(b.DeskPeak, value);
            if (b.Book.Count > 0 && DeskDrawdown(b) >= b.DeskLimit)
            {
                for (int i = b.Book.Count - 1; i >= 0; i--)
                {
                    long sell = b.Book[i].Qty * (1000 - BankDesk.CutKeep) / 1000;
                    OwnBankFund.SellShares(b, b.Book, i, Math.Max(1, sell), OwnBankCatalogue.FundSpreadBp * 10, into);
                }
                b.DeskPeak = b.BookValue;
                b.DeskCutDay = day;
                b.DeskCuts++;
                into.DeskCut = true;
            }
            bool buying = b.DeskCutDay == 0 || day >= b.DeskCutDay + BankDesk.CutMonths * monthDays;
            if (b.DeskDirty || day % 7 == 3)
            {
                DeskRebalance(b, quotes, buying && !b.Run && b.Stage != Regulator.Breach && !WatchHeld(b), into, hold);
                b.DeskDirty = false;
                b.DeskPeak = Math.Max(b.DeskPeak, b.BookValue);
            }
        }

        // The mandate's companies by its own order, an even part each of the room; what is not among them is sold.
        private static void DeskRebalance(OwnBank b, IList<FundQuote> quotes, bool mayBuy, BankDay into, long hold)
        {
            int mandate = BankDesk.MandateOf(b.DeskMandate);
            int n = BankDesk.Companies[mandate];
            DeskOrder.Clear();
            for (int i = 0; i < quotes.Count; i++)
            {
                if (DeskEligible(quotes[i], mandate))
                {
                    DeskOrder.Add(i);
                }
            }
            DeskOrder.Sort((x, y) => DeskCompare(quotes[x], quotes[y], mandate));
            DeskPicked.Clear();
            for (int i = 0; i < DeskOrder.Count && DeskPicked.Count < n; i++)
            {
                DeskPicked.Add(DeskOrder[i]);
            }
            for (int i = b.Book.Count - 1; i >= 0; i--)
            {
                bool kept = false;
                for (int p = 0; p < DeskPicked.Count && !kept; p++)
                {
                    kept = quotes[DeskPicked[p]].StockId == b.Book[i].StockId;
                }
                if (!kept)
                {
                    OwnBankFund.SellShares(b, b.Book, i, b.Book[i].Qty, OwnBankCatalogue.FundSpreadBp * 10, into);
                }
            }
            if (DeskPicked.Count == 0)
            {
                return;
            }
            long part = DeskRoom(b, hold) / DeskPicked.Count;
            for (int p = 0; p < DeskPicked.Count; p++)
            {
                FundQuote q = quotes[DeskPicked[p]];
                FundHolding h = OwnBankFund.FindIn(b.Book, q.StockId);
                long have = h != null ? h.Qty * q.LastCents : 0;
                if (Math.Abs(part - have) * 1000 <= part * BankDesk.Drift && h != null)
                {
                    continue;
                }
                if (part > have)
                {
                    if (mayBuy)
                    {
                        OwnBankFund.Buy(b, b.Book, q, part - have, into, hold);
                    }
                }
                else if (h != null)
                {
                    long sell = (have - part) / Math.Max(1, q.LastCents);
                    OwnBankFund.SellShares(b, b.Book, b.Book.IndexOf(h), Math.Min(h.Qty, sell), OwnBankCatalogue.FundSpreadBp * 10, into);
                }
            }
        }

        // The index and growth mandates take the largest by market value, the income mandate the highest yield; ties by the stock's number, so the order is the same everywhere.
        private static int DeskCompare(FundQuote x, FundQuote y, int mandate)
        {
            if (mandate == BankDesk.Income)
            {
                long yx = x.DpsCents * 40000L / Math.Max(1, x.LastCents), yy = y.DpsCents * 40000L / Math.Max(1, y.LastCents);
                if (yx != yy)
                {
                    return yy.CompareTo(yx);
                }
            }
            else
            {
                long vx = x.LastCents * x.Shares, vy = y.LastCents * y.Shares;
                if (vx != vy)
                {
                    return vy.CompareTo(vx);
                }
            }
            return x.StockId.CompareTo(y.StockId);
        }
    }
}
