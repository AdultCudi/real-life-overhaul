using System;

namespace RealLifeOverhaul.BuildTrades
{
    /// <summary>One line of a tender: what it is, how much, what was paid last and how the market has moved, and the three rates offered.</summary>
    public struct TenderItem
    {
        public int Kind;             // reallife_trades_tender_item_N: brickwork, concrete...
        public int Quantity;         // in the item's unit
        public int LastCents;        // the rate paid on the last job, cents a unit
        public int MovePercent;      // the market since, per cent (may be less than nothing)
        public int TrueCents;        // what it will really cost now: the last rate moved by the market
        public int Rate0, Rate1, Rate2;   // the three rates offered, cents a unit, in the order shown
        public int Right;            // which of the three is the true one

        public int Rate(int i) => i == 0 ? Rate0 : i == 1 ? Rate1 : Rate2;
    }

    /// <summary>
    /// The estimator's tender, game-free (EconomyCheck runs it): five items, each priced by picking the rate that is the last
    /// one moved by the market, then a margin; the bid is won if it comes in under the rival's, and earns if it covers the cost.
    /// </summary>
    public static class TenderRules
    {
        public const int Items = 5;
        public const int Kinds = 12;
        public static readonly int[] Margins = { 4, 8, 12 };      // per cent on top, the panel's three choices
        public const float RivalLow = 1.05f, RivalHigh = 1.15f;   // the rival's bid against the true cost
        public const float LowWorth = 0.5f, HighWorth = 0.6f;     // an item priced under its cost loses money; over it, the bid
        public const float WinBonus = 0.15f;                      // a tender won at a profit: this share of a unit's pay on top

        // Each kind's usual rate (cents a unit) and the quantity's range.
        private static readonly int[] BaseCents = { 4500, 16000, 9500, 6500, 7000, 52000, 3200, 8500, 11000, 30000, 2400, 210000 };
        private static readonly int[] QuantityLow = { 80, 40, 200, 150, 120, 12, 300, 30, 20, 6, 300, 4 };
        private static readonly int[] QuantityHigh = { 260, 140, 620, 480, 400, 48, 900, 110, 70, 20, 950, 16 };

        /// <summary>A tender's five items, drawn from a seed: no kind twice, each with its three rates, the true one somewhere among them.</summary>
        public static void Draw(uint seed, TenderItem[] items)
        {
            var dice = new TradeDice(seed);
            int used = 0;
            for (int i = 0; i < Items && i < items.Length; i++)
            {
                int kind;
                do
                {
                    kind = dice.Range(0, Kinds);
                }
                while ((used & (1 << kind)) != 0);
                used |= 1 << kind;
                var item = new TenderItem { Kind = kind, Quantity = dice.Range(QuantityLow[kind], QuantityHigh[kind] + 1) };
                item.LastCents = Round50((int)(BaseCents[kind] * dice.Range(0.85f, 1.15f)));
                item.MovePercent = dice.Range(-10, 16);
                item.TrueCents = Round50((int)Math.Round(item.LastCents * (100 + item.MovePercent) / 100.0));
                int low = Round50((int)(item.TrueCents * dice.Range(0.84f, 0.92f)));
                int high = Round50((int)(item.TrueCents * dice.Range(1.09f, 1.17f)));
                if (low == item.TrueCents)
                {
                    low -= 50;
                }
                if (high == item.TrueCents)
                {
                    high += 50;
                }
                // The three in order of price would give the true one away by its place: the true one at a drawn place, the
                // other two round it in a drawn order.
                item.Right = dice.Range(0, 3);
                bool lowFirst = dice.Value() < 0.5f;
                int first = lowFirst ? low : high, second = lowFirst ? high : low;
                item.Rate0 = item.Right == 0 ? item.TrueCents : first;
                item.Rate1 = item.Right == 1 ? item.TrueCents : item.Right == 0 ? first : second;
                item.Rate2 = item.Right == 2 ? item.TrueCents : second;
                items[i] = item;
            }
        }

        private static int Round50(int cents) => Math.Max(50, (cents + 25) / 50 * 50);

        /// <summary>What a pick of an item is worth: the true rate 1, under it 0.5 (the job would lose money), over it 0.6.</summary>
        public static float Worth(in TenderItem item, int pick)
        {
            int rate = item.Rate(pick);
            return rate == item.TrueCents ? 1f : rate < item.TrueCents ? LowWorth : HighWorth;
        }

        /// <summary>The tender's true cost, cents.</summary>
        public static long TrueTotal(TenderItem[] items)
        {
            long total = 0;
            for (int i = 0; i < Items && i < items.Length; i++)
            {
                total += (long)items[i].TrueCents * items[i].Quantity;
            }
            return total;
        }

        /// <summary>The bid: each item at the rate picked, the margin on top. An item left unpriced goes in at the dearest rate.</summary>
        public static long Bid(TenderItem[] items, int[] picks, int marginPercent)
        {
            long total = 0;
            for (int i = 0; i < Items && i < items.Length; i++)
            {
                int rate = picks[i] >= 0 ? items[i].Rate(picks[i]) : Math.Max(items[i].Rate0, Math.Max(items[i].Rate1, items[i].Rate2));
                total += (long)rate * items[i].Quantity;
            }
            return total + total * marginPercent / 100;
        }

        /// <summary>The rival's bid for the same work, drawn from the seed.</summary>
        public static long Rival(uint seed, long trueTotal)
        {
            var dice = new TradeDice(seed ^ 0x51CE2A0Bu);
            return (long)Math.Round(trueTotal * dice.Range(RivalLow, RivalHigh));
        }

        /// <summary>The tender's quality, 0 to 1: the mean worth of its picks; a tender lost is worth four fifths of that.</summary>
        public static float Quality(TenderItem[] items, int[] picks, bool won)
        {
            float sum = 0f;
            for (int i = 0; i < Items && i < items.Length; i++)
            {
                sum += picks[i] >= 0 ? Worth(items[i], picks[i]) : 0f;
            }
            float q = sum / Items;
            return won ? q : q * 0.8f;
        }

        /// <summary>True if the bid is won and covers the cost (the margin is real money): the bonus is paid.</summary>
        public static bool Earns(long bid, long rival, long trueTotal) => bid < rival && bid >= trueTotal;
    }
}
