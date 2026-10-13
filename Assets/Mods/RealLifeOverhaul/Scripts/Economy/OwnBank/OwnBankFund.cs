using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// The bank's fund: a share of its deposits put into the Stocks app's companies, within rules the player chooses
    /// from (a strategy and a limit) and the regulator's. Game-free; the market's figures come in as quotes
    /// (<see cref="FundQuote"/>, read by Wiring/OwnBankLinks), and the bank trades at the last price, over it to buy and
    /// under it to sell by FundSpreadBp, with a commission, as an institution's trades are not the player's orders and
    /// do not move the market's prices.
    ///
    ///   the rules   a cautious fund holds only large companies under a tenth in distress, a balanced one large and mid
    ///               sized ones under a fifth, a bold one any listed company under three tenths; never one under a bid.
    ///               It holds FundCompanies of them, the largest by market value, each an even part of what it may
    ///               hold: the limit's share of the deposits, as far as the reserves over what the deposits need and the
    ///               capital over the lending line (a share counts FundWeight against it) allow.
    ///   the day     each holding's price and dividend are brought up to date; a company gone from the market pays out
    ///               what it was paid out at (nothing for one that failed); the dividends come in a day at a time (the
    ///               last quarterly dividend, four a year); a holding the rules no longer allow is sold; once a week,
    ///               or the day after a choice changed, the fund is traded back to its parts (a part more than
    ///               FundDrift off). In a run or a breach nothing is bought.
    ///
    /// Every sum moves the reserves, the fund's cost and the capital together, so the balance sheet still balances.
    /// Cost: a pass over the quotes (some 150) once a day; the weekly rebalance sorts the eligible ones once.
    /// </summary>
    public static class OwnBankFund
    {
        private static readonly List<int> Picked = new List<int>(16);    // places in the quotes, worked in here
        private static readonly List<int> Order = new List<int>(160);

        /// <summary>Whether the fund's rules let it hold a company under a strategy.</summary>
        public static bool Eligible(FundQuote q, int strategy)
        {
            if (strategy <= 0 || strategy >= OwnBankCatalogue.Strategies || !q.Listed || q.UnderBid || q.LastCents <= 0 || q.Distress > OwnBankCatalogue.FundDistress[strategy])
            {
                return false;
            }
            return strategy == 1 ? q.Size == 0 : strategy == 2 ? q.Size <= 1 : true;
        }

        /// <summary>What the fund may hold in all now, cents: the limit's share of the deposits, as far as the cash and the capital allow.</summary>
        public static long Room(OwnBank b)
        {
            if (b.Strategy <= 0 || b.FundLimit <= 0)
            {
                return 0;
            }
            long value = b.FundValue;
            long byLimit = b.Savings * Math.Min(OwnBankCatalogue.FundLimitMost, b.FundLimit) / 1000;
            long byCash = b.Reserves - OwnBankRules.ReserveNeed(b) + value;
            long others = OwnBankRules.RiskWeighted(b) - value * OwnBankCatalogue.FundWeight / 1000;
            long byCapital = (OwnBankRules.CapitalNow(b) * 1000 / OwnBankCatalogue.LendRatio - others) * 1000 / OwnBankCatalogue.FundWeight;
            return Math.Max(0, Math.Min(byLimit, Math.Min(byCash, byCapital)));
        }

        /// <summary>The fund's day (see the summary). Nothing while there are no quotes: the market is not running.</summary>
        public static void Day(OwnBank b, BankCity c, int day, IList<FundQuote> quotes, BankDay into)
        {
            if (quotes == null)
            {
                return;
            }
            int y = Math.Max(1, c.YearDays);
            for (int i = b.Holdings.Count - 1; i >= 0; i--)
            {
                FundHolding h = b.Holdings[i];
                int at = IndexOf(quotes, h.StockId);
                if (at < 0)
                {
                    continue;   // (a market that does not list it today: kept at its last price)
                }
                FundQuote q = quotes[at];
                h.Name = q.Name ?? h.Name;
                h.Ticker = q.Ticker ?? h.Ticker;
                if (!q.Listed)
                {
                    long payout = h.Qty * Math.Max(0, q.GoneCents);
                    Settle(b, h, payout, 0, into);
                    b.Holdings.RemoveAt(i);
                    continue;
                }
                h.LastCents = q.LastCents;
                h.DpsCents = Math.Max(0, q.DpsCents);
                long dividend = h.Qty * h.DpsCents * 4 / y;
                b.Reserves += dividend;
                b.Capital += dividend;
                into.Dividends += dividend;
                if (!Eligible(q, b.Strategy) || b.FundLimit <= 0)
                {
                    SellShares(b, i, h.Qty, OwnBankCatalogue.FundSpreadBp * 10, into);
                }
            }
            if (b.Strategy <= 0 || b.FundLimit <= 0)
            {
                b.Rebalance = false;
                return;
            }
            if (b.Rebalance || day % 7 == 0)
            {
                Rebalance(b, quotes, !b.Run && b.Stage != Regulator.Breach, into);
                b.Rebalance = false;
            }
        }

        // The fund traded back to its parts: the largest eligible companies, an even part each; what is not among them sold.
        private static void Rebalance(OwnBank b, IList<FundQuote> quotes, bool mayBuy, BankDay into)
        {
            int n = OwnBankCatalogue.FundCompanies[b.Strategy];
            Order.Clear();
            for (int i = 0; i < quotes.Count; i++)
            {
                if (Eligible(quotes[i], b.Strategy))
                {
                    Order.Add(i);
                }
            }
            Order.Sort((x, y) =>
            {
                long vx = quotes[x].LastCents * quotes[x].Shares, vy = quotes[y].LastCents * quotes[y].Shares;
                return vx != vy ? vy.CompareTo(vx) : quotes[x].StockId.CompareTo(quotes[y].StockId);
            });
            Picked.Clear();
            for (int i = 0; i < Order.Count && Picked.Count < n; i++)
            {
                Picked.Add(Order[i]);
            }
            for (int i = b.Holdings.Count - 1; i >= 0; i--)
            {
                if (!PickedHas(quotes, b.Holdings[i].StockId))
                {
                    SellShares(b, i, b.Holdings[i].Qty, OwnBankCatalogue.FundSpreadBp * 10, into);
                }
            }
            if (Picked.Count == 0)
            {
                return;
            }
            long part = Room(b) / Picked.Count;
            for (int p = 0; p < Picked.Count; p++)
            {
                FundQuote q = quotes[Picked[p]];
                FundHolding h = b.Find(q.StockId);
                long have = h != null ? h.Qty * q.LastCents : 0;
                if (Math.Abs(part - have) * 1000 <= part * OwnBankCatalogue.FundDrift && h != null)
                {
                    continue;
                }
                if (part > have)
                {
                    if (mayBuy)
                    {
                        Buy(b, q, part - have, into);
                    }
                }
                else if (h != null)
                {
                    long sell = (have - part) / Math.Max(1, q.LastCents);
                    SellShares(b, b.Holdings.IndexOf(h), Math.Min(h.Qty, sell), OwnBankCatalogue.FundSpreadBp * 10, into);
                }
            }
        }

        // Shares bought for about an amount: as many whole ones as it pays for at the last price and the spread over it, with the commission.
        private static void Buy(OwnBank b, FundQuote q, long cents, BankDay into)
        {
            long price = (long)q.LastCents * (10000 + OwnBankCatalogue.FundSpreadBp) / 10000;
            long cash = b.Reserves - OwnBankRules.ReserveNeed(b);
            long qty = Math.Min(cents, cash) / Math.Max(1, price);
            if (qty <= 0)
            {
                return;
            }
            long cost = qty * price, fee = Fee(cost);
            FundHolding h = b.Find(q.StockId);
            if (h == null)
            {
                h = new FundHolding { StockId = q.StockId, Ticker = q.Ticker ?? string.Empty, Name = q.Name ?? string.Empty };
                b.Holdings.Add(h);
            }
            h.Qty += qty;
            h.CostCents += cost;
            h.LastCents = q.LastCents;
            h.DpsCents = Math.Max(0, q.DpsCents);
            b.Reserves -= cost + fee;
            b.Capital -= fee;
            into.Fees += fee;
            into.Bought += cost;
        }

        // Shares of a holding sold at its last price less a discount (hundred-thousandths of the price: 200 is the spread's 0.2%), with the commission.
        private static void SellShares(OwnBank b, int index, long qty, int under, BankDay into)
        {
            if (index < 0 || index >= b.Holdings.Count || qty <= 0)
            {
                return;
            }
            FundHolding h = b.Holdings[index];
            qty = Math.Min(qty, h.Qty);
            long proceeds = qty * h.LastCents * (100000 - under) / 100000;
            long fee = Fee(proceeds);
            long basis = h.Qty > 0 ? h.CostCents * qty / h.Qty : h.CostCents;
            h.Qty -= qty;
            h.CostCents -= basis;
            Settle(b, null, proceeds, basis, into);
            b.Reserves -= fee;
            b.Capital -= fee;
            into.Fees += fee;
            into.Sold += proceeds;
            if (h.Qty <= 0)
            {
                b.Holdings.RemoveAt(index);
            }
        }

        // Money come back for shares: into the reserves, and what it is over (or under) what they cost into the capital.
        private static void Settle(OwnBank b, FundHolding gone, long proceeds, long basis, BankDay into)
        {
            if (gone != null)
            {
                basis = gone.CostCents;
                gone.CostCents = 0;
                gone.Qty = 0;
            }
            b.Reserves += proceeds;
            long gain = proceeds - basis;
            b.Capital += gain;
            if (gain >= 0)
            {
                into.Gains += gain;
            }
            else
            {
                into.FundLost -= gain;
            }
        }

        /// <summary>
        /// The bank must pay its savers more than its reserves hold: shares are sold, largest holding first, at the last
        /// price less FundFireSale, until it can or nothing is left.
        /// </summary>
        public static void FireSale(OwnBank b, long need, BankDay into)
        {
            while (need > 0 && b.Holdings.Count > 0)
            {
                int largest = 0;
                for (int i = 1; i < b.Holdings.Count; i++)
                {
                    largest = b.Holdings[i].Qty * b.Holdings[i].LastCents > b.Holdings[largest].Qty * b.Holdings[largest].LastCents ? i : largest;
                }
                FundHolding h = b.Holdings[largest];
                long each = (long)h.LastCents * (1000 - OwnBankCatalogue.FundFireSale) / 1000;
                long qty = each > 0 ? Math.Min(h.Qty, need / each + 1) : h.Qty;
                long before = b.Reserves;
                SellShares(b, largest, qty, OwnBankCatalogue.FundFireSale * 100, into);
                into.FireSold += b.Reserves - before;
                need -= b.Reserves - before;
                if (b.Reserves <= before)
                {
                    break;   // (shares worth nothing: nothing more to be had)
                }
            }
        }

        /// <summary>A trade's commission, cents.</summary>
        public static long Fee(long cents)
        {
            return cents <= 0 ? 0 : (cents * OwnBankCatalogue.FundCommissionBp + 9999) / 10000;
        }

        private static int IndexOf(IList<FundQuote> quotes, int stockId)
        {
            for (int i = 0; i < quotes.Count; i++)
            {
                if (quotes[i].StockId == stockId)
                {
                    return i;
                }
            }
            return -1;
        }

        private static bool PickedHas(IList<FundQuote> quotes, int stockId)
        {
            for (int p = 0; p < Picked.Count; p++)
            {
                if (quotes[Picked[p]].StockId == stockId)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
