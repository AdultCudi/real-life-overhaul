using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Markets
{
    /// <summary>
    /// The account's figures, as the Portfolio's Account page says them: the margin call, what was paid in dividends and in commission,
    /// and for a margin account its equity, what it must keep, its buying power and what it costs. Read off the market state. Money in
    /// whole units. Game-free.
    /// </summary>
    public struct AccountFigures
    {
        public bool Margin;
        public int CallDay;              // a margin call to meet by the close of this day; 0 none
        public long CallAmount;
        public long Cash, Equity, Need, Power, Withdrawable;
        public long Dividends;           // paid to the player on the shares held
        public long DividendsPaid;       // paid by the player on shares sold short
        public long Commission;          // paid on every trade
        public long Interest, BorrowFees;
        public long Realised;            // gains and losses taken, all the time the account has run

        public bool Called => Margin && CallDay > 0;
    }

    /// <summary>
    /// What the Portfolio's Pays page says: the next year's income by kind of holding, what is owed on shorts, the yield, and the
    /// payments to come with their day. From <see cref="InvestRules"/>, the sums the old Pays view read. Game-free.
    /// </summary>
    public sealed class PaysFigures
    {
        public const int NextMost = 8;

        public readonly PortfolioSums Sums = new PortfolioSums();
        public readonly List<PortfolioRow> Rows = new List<PortfolioRow>(32);
        public readonly List<ComingPayment> Next = new List<ComingPayment>(NextMost + 1);
        public int NextAll;              // every payment to come, of which Next lists the first few

        /// <summary>The year's income after what the shorts owe, in cents.</summary>
        public long YearCents => Sums.TotalYearCents - Sums.OwedCents;

        public bool Any
        {
            get
            {
                for (int k = 0; k < (int)HoldingKind.Count; k++)
                {
                    if (Sums.YearCents[k] > 0)
                    {
                        return true;
                    }
                }
                return Sums.OwedCents > 0;
            }
        }
    }

    /// <summary>The Stocks desk's figures read off the market state for the pages that have no screen: game-free, so the checks read them.</summary>
    public static class StocksFigures
    {
        /// <summary>The account's figures. <paramref name="withdrawable"/> is what the director says may be taken out (it knows the broker's money).</summary>
        public static AccountFigures Account(MarketState s, int phase, long withdrawable)
        {
            Account a = s.Account;
            var f = new AccountFigures
            {
                Margin = a.Margin, CallDay = a.CallDay, CallAmount = a.CallAmount, Cash = a.Cash, Dividends = a.Dividends, DividendsPaid = a.DividendsPaid,
                Commission = a.Commission, Interest = a.Interest, BorrowFees = a.BorrowFees, Realised = a.Realised, Withdrawable = withdrawable
            };
            if (a.Margin)
            {
                long gross = MarketTrading.GrossExposure(s, phase);
                f.Equity = MarketTrading.Equity(s, phase);
                f.Need = MarketTrading.Maintenance(s, phase);
                f.Power = Math.Max(0L, f.Equity * 100L / MarketCatalogue.InitialMarginPct - gross);
            }
            return f;
        }

        /// <summary>The Pays page's figures: what every holding pays over the next year and the first payments to come.</summary>
        public static void Pays(MarketState s, int phase, bool bondsOn, PaysFigures into)
        {
            InvestRules.Holdings(s, phase, into.Rows, bondsOn);
            InvestRules.Sums(into.Rows, into.Sums);
            into.NextAll = InvestRules.NextPayments(into.Rows, into.Next, PaysFigures.NextMost);
        }

        /// <summary>
        /// The player's gain on a day: each holding's shares times the day's move of its price, in whole money. Shares sold short move
        /// the other way. A holding of a company no longer listed has none.
        /// </summary>
        public static long YourDay(MarketState s)
        {
            long day = 0;
            for (int i = 0; s != null && i < s.Positions.Count; i++)
            {
                Position p = s.Positions[i];
                Stock k = s.Stock(p.StockId);
                if (k != null && k.Listed && !p.Test)
                {
                    day += p.Shares * (k.Last - k.PrevClose) / 100L;
                }
            }
            return day;
        }

        /// <summary>
        /// What a position is after an order: its shares and its average cost in cents, as <see cref="MarketTrading.Book"/> will leave it. A
        /// sale keeps the average cost of what stays (the cost falls with the shares), a buy over a short covers it first, and an order past
        /// zero opens the other way at the price of what is left of it. <paramref name="money"/> is the order's total with its fee: what is paid
        /// for a buy, what is brought for a sale.
        /// </summary>
        public static void PositionAfter(long held, long cost, bool buy, long qty, long money, out long shares, out long averageCents)
        {
            long delta = buy ? qty : -qty;
            long afterCost;
            if (held == 0 || Math.Sign(held) == Math.Sign(delta))
            {
                shares = held + delta;
                afterCost = cost + money;
            }
            else
            {
                long closing = Math.Min(Math.Abs(delta), Math.Abs(held));
                long part = cost * closing / Math.Abs(held);
                long closed = Math.Abs(delta) > 0 ? money * closing / Math.Abs(delta) : 0L;
                shares = held + Math.Sign(delta) * closing;
                afterCost = cost - part;
                long rest = Math.Abs(delta) - closing;
                if (rest > 0)
                {
                    shares = Math.Sign(delta) * rest;
                    afterCost = money - closed;
                }
                else if (shares == 0)
                {
                    afterCost = 0;
                }
            }
            averageCents = shares != 0 ? Math.Max(0L, afterCost) * 100L / Math.Abs(shares) : 0L;
        }
    }
}
