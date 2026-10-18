using System;

namespace RealLifeOverhaul.Markets
{
    /// <summary>
    /// The player's cash as the market counts it, in front of the game's: money worked orders have with the broker stays the
    /// player's for every other purchase. A purchase beyond the cash in hand takes back what it needs, sales' takings first,
    /// then parked money, latest order first; a worked order parks from the cash in hand alone (<see cref="Free"/>). A look
    /// at the orders only when the cash in hand is short.
    /// </summary>
    public sealed class BrokerWallet : IMarketWallet
    {
        private readonly IMarketWallet _hand;   // the game's cash, or the checks' own
        private readonly MarketState _state;

        public BrokerWallet(IMarketWallet hand, MarketState state)
        {
            _hand = hand;
            _state = state;
        }

        /// <summary>What the app shows as the cash, and all a worked order parks from.</summary>
        public long Free => _hand.Cash;

        /// <summary>The cash in hand and the money worked orders have with the broker.</summary>
        public long Cash => _hand.Cash + InvestRules.WithBroker(_state, out _, out _);

        /// <summary>
        /// What a give-back adds for the game's rounding: its money is a float moved in pieces, so over 2^24 a sum can come a
        /// few units short. A millionth of the sum and 4 covers the worst case found (19,077 on 20 billion) four times over.
        /// </summary>
        public static long Margin(long amount)
        {
            return amount <= 0 ? 0L : (amount >> 20) + 4L;
        }

        public bool Ready(long amount)
        {
            if (amount <= 0 || _hand.Cash >= amount)
            {
                return true;
            }
            if (Cash < amount)
            {
                return false;   // not even with the money with the broker: none of it is taken back
            }
            Release(amount - _hand.Cash + Margin(amount));
            return _hand.Cash >= amount;
        }

        public bool Take(long amount, string why, string name)
        {
            return amount <= 0 || (Ready(amount) && _hand.Take(amount, why, name));
        }

        public void Force(long amount, string why, string name)
        {
            Cover(amount);   // what is owed comes from the money with the broker first, as far as it goes
            _hand.Force(amount, why, name);
        }

        /// <summary>Charges to come: what they need beyond the cash in hand, with the margin, given back now so none takes back again.</summary>
        public void Cover(long amount)
        {
            if (amount > _hand.Cash)
            {
                Release(amount - _hand.Cash + Margin(amount));
            }
        }

        public void Give(long amount, string why, string name)
        {
            _hand.Give(amount, why, name);
        }

        /// <summary>A worked order's own day of money: from the cash in hand only, none taken back from the broker.</summary>
        public bool TakeFree(long amount, string why, string name)
        {
            return amount <= 0 || _hand.Take(amount, why, name);
        }

        // Money with the broker given back, at most 'need': sales' takings first, then parked money, the latest order first.
        private void Release(long need)
        {
            for (int pass = 0; pass < 2 && need > 0; pass++)
            {
                for (int i = _state.Orders.Count - 1; i >= 0 && need > 0; i--)
                {
                    Order o = _state.Orders[i];
                    long has = !o.Worked ? 0L : pass == 0 ? o.Due : o.Parked;
                    if (has <= 0)
                    {
                        continue;
                    }
                    long back = Math.Min(has, need);
                    if (pass == 0)
                    {
                        o.Due -= back;
                    }
                    else
                    {
                        o.Parked -= back;
                    }
                    need -= back;
                    Stock s = _state.Stock(o.StockId);
                    _hand.Give(back, pass == 0 ? MarketMoney.WorkedSold : MarketMoney.WorkedBack, s != null ? s.Name : string.Empty);
                }
            }
        }
    }
}
