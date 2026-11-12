using System;

namespace RealLifeOverhaul.Markets
{
    /// <summary>Why a block cannot be dealt. Each has a sentence (ExtrasText).</summary>
    public enum BlockWhy
    {
        None = 0,
        Off,             // the extras are switched off
        Closed,          // the market is not trading
        Held,            // after a load, until the market is back where it was
        NotListed,
        Founders,        // a player's own company: its founder deals by the founder's rules, and nobody takes a block of it here
        Bid,             // a bid for the company stands: its price is the bid's
        Young,           // listed too short a time
        TooSmall,        // no more than one order may be: the order ticket takes it
        HoldingLimit,    // more than one holder may have of the company
        NoShares,
        NoMoney,
        BelowMargin,
        Count
    }

    /// <summary>What a block comes to before it is dealt.</summary>
    public struct BlockQuote
    {
        public long Qty;
        public int Cents;            // a share: the offer or the bid, moved against the block by its size
        public long Value, Fee, Total;
        public int ImpactBp;
        public int StakeBp;          // of the company, held once it is dealt
        public long Least, Most;     // the shares a block may be now
        public BlockWhy Why;
    }

    /// <summary>
    /// A block trade: many shares of one company in one deal, where an order is held to a share of a day's trading. It is
    /// priced by the market's own depth rule (<see cref="MarketTrading.MarketPrice"/>: the square root of its size over a
    /// day's volume, the player's recent trading counted), so it costs what the same shares cost as orders placed at the
    /// same moment or less, moves the price as one order of its size, and a block bought and sold again loses the spread,
    /// both moves and two commissions. It is never a way round a rule: the most one holder may have binds it, a rival's
    /// holding has only its free float, a company under a bid or a player's own is not dealt in, and a holding is told to
    /// the market at every 5% it passes. Game-free; on a press.
    /// </summary>
    public static class BlockRules
    {
        /// <summary>The fewest shares a block is: one more than an order may be.</summary>
        public static long Least(Stock s)
        {
            return MarketTrading.MostPerOrder(s) + 1L;
        }

        /// <summary>The most shares a block may be now: to the most one holder may have, or all that is held and not already asked to be sold.</summary>
        public static long Most(MarketState state, Stock s, bool buying)
        {
            long held = state.Held(s.Id);
            return buying ? Math.Max(0L, MarketTrading.MostHeld(s) - held - MarketTrading.Reserved(state, s.Id, OrderSide.Buy))
                : Math.Max(0L, held - MarketTrading.Reserved(state, s.Id, OrderSide.Sell));
        }

        public static BlockQuote Quote(MarketState state, Stock s, bool buying, long qty, long cash, int least, int phase, bool on, bool trading, bool waiting)
        {
            var q = new BlockQuote { Qty = qty };
            if (!on)
            {
                q.Why = BlockWhy.Off;
                return q;
            }
            if (s == null || !s.Listed)
            {
                q.Why = BlockWhy.NotListed;
                return q;
            }
            q.Least = Least(s);
            q.Most = Most(state, s, buying);
            if (s.Own != null)
            {
                q.Why = BlockWhy.Founders;
                return q;
            }
            if (waiting)
            {
                q.Why = BlockWhy.Held;
                return q;
            }
            if (!trading)
            {
                q.Why = BlockWhy.Closed;
                return q;
            }
            if (s.BidCents > 0)
            {
                q.Why = BlockWhy.Bid;
                return q;
            }
            if (state.Day - s.ListedDay < ExtrasCatalogue.BlockListedDays * 7 / 5)
            {
                q.Why = BlockWhy.Young;
                return q;
            }
            long held = state.Held(s.Id);
            if (!buying && (held <= 0 || qty > q.Most))
            {
                q.Why = BlockWhy.NoShares;
                return q;
            }
            if (qty < q.Least)
            {
                q.Why = BlockWhy.TooSmall;
                return q;
            }
            if (buying && qty > q.Most)
            {
                q.Why = BlockWhy.HoldingLimit;
                return q;
            }
            double impact = MarketTrading.Impact(state, s, qty);
            q.ImpactBp = (int)Math.Round(impact * 10000.0);
            q.Cents = MarketTrading.MarketPrice(state, s, buying, qty, phase);
            q.Value = MarketTrading.Value(qty, q.Cents, buying);
            q.Fee = MarketTrading.Commission(q.Value, least);
            if (!buying)
            {
                q.Fee = Math.Min(q.Fee, q.Value);
            }
            q.Total = buying ? q.Value + q.Fee : q.Value - q.Fee;
            q.StakeBp = StakeBp(held + (buying ? qty : -qty), s.Shares);
            if (buying && !state.Account.Margin && q.Total > cash)
            {
                q.Why = BlockWhy.NoMoney;
            }
            else if (state.Account.Margin && !MarketTrading.MarginAllows(state, s, buying, qty, q.Fee, phase))
            {
                q.Why = BlockWhy.BelowMargin;
            }
            return q;
        }

        public static int StakeBp(long held, long shares)
        {
            return shares > 0 && held > 0 ? (int)Math.Min(10000L, held * 10000L / shares) : 0;
        }

        /// <summary>The 5% step a holding stands at: what the market is told.</summary>
        public static int StepOf(int bp)
        {
            return Math.Max(0, bp) / ExtrasCatalogue.DiscloseStepBp * ExtrasCatalogue.DiscloseStepBp;
        }

        /// <summary>
        /// Whether a holding has passed a step since the market was last told, up or down; the step it stands at is then what
        /// was told. A holding that never reached the first step is nobody's business.
        /// </summary>
        public static bool Disclose(ExtrasState x, int stock, int holder, int bpNow, out int step)
        {
            step = StepOf(bpNow);
            int told = x.ToldBp(stock, holder);
            if (step == told)
            {
                return false;
            }
            x.SetTold(stock, holder, step);
            return true;
        }

        /// <summary>The lasting move of a trade of so many shares by somebody who is not the player (a rival's block), in millionths as the tape keeps a trade.</summary>
        public static int Micros(MarketState state, Stock s, long qty, bool buying)
        {
            double kept = MarketCatalogue.ImpactPermanent * MarketCatalogue.ImpactTemporary * MarketTrading.DayVol(state, s) * Math.Sqrt(Math.Max(0L, qty) / Math.Max(1.0, s.Adv));
            return MarketInputs.TradeMicros(buying ? kept : -kept);
        }
    }
}
