using System;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The game keeps the player's money as a float, and much of the mod works in whole money as an int. A float past
    /// 2,147,483,647 does not fit an int, and a cast of one (Mathf.FloorToInt, Mathf.RoundToInt, a plain (int)) gives
    /// int.MinValue in the game's Mono, as it does for NaN and Infinity. A rich player's "cash" then read as minus
    /// 2.1 billion, and a loss that was "never more than the money there is" took exactly that much (a life event's vehicle
    /// costs, 27 Oct 2026). Every float of money that becomes an int goes through here: it saturates at the largest int,
    /// reads NaN as nothing, and never overflows. Game-free, so the checkers compile it. COST: a few comparisons, only
    /// when the mod reads the player's money.
    /// </summary>
    public static class WholeMoney
    {
        /// <summary>The largest sum of whole money the mod's int fields hold.</summary>
        public const int Most = int.MaxValue;

        private const float IntLimit = 2147483648f;   // 2 to the 31st: the first float that is not an int

        /// <summary>
        /// Cash in hand as whole money: rounded down, never below nothing, nothing for NaN, and int.MaxValue for a purse
        /// of 2,147,483,647 or more.
        /// </summary>
        public static int Floor(float money)
        {
            if (!(money > 0f))   // nothing, below nothing, or NaN
            {
                return 0;
            }
            return money >= IntLimit ? Most : (int)Math.Floor(money);
        }

        /// <summary>Money to the nearest whole, in either direction: saturating at both ends of an int, NaN as nothing.</summary>
        public static int Round(double money)
        {
            if (double.IsNaN(money))
            {
                return 0;
            }
            if (money >= int.MaxValue)
            {
                return int.MaxValue;
            }
            if (money <= int.MinValue + 1.0)
            {
                return int.MinValue + 1;   // one short of int.MinValue, so that a negation of it still fits
            }
            return (int)Math.Round(money, MidpointRounding.AwayFromZero);
        }

        public static int Round(float money) => Round((double)money);

        /// <summary>A sum of whole money that stops at the ends of an int instead of wrapping round: what is owed, and what is added to it every day.</summary>
        public static int Add(int owed, long more)
        {
            long sum = (long)owed + more;
            return sum > int.MaxValue ? int.MaxValue : sum < int.MinValue + 1 ? int.MinValue + 1 : (int)sum;
        }

        /// <summary>A sum of whole money held within <paramref name="most"/>: a total that grows from many small charges and is never to pass a sane bound.</summary>
        public static int Add(int total, long more, int most)
        {
            long sum = (long)total + more;
            return sum > most ? most : sum < 0L ? 0 : (int)sum;
        }

        /// <summary>A share of an amount of money (cash, a balance), rounded down, saturating, never below nothing.</summary>
        public static int Share(float money, float share)
        {
            double part = (double)money * share;
            return part > 0.0 ? (part >= Most ? Most : (int)Math.Floor(part)) : 0;
        }

        /// <summary>
        /// What a loss takes from a purse: the loss asked, but never more than the money there is, and never more than
        /// <paramref name="most"/> (a bound the caller ties to what the thing could cost at most). Worked in long, so a
        /// loss of int.MinValue or a purse past an int cannot turn it round. Zero or more.
        /// </summary>
        public static int Take(long loss, float money, long most = long.MaxValue)
        {
            if (loss <= 0L)
            {
                return 0;
            }
            long sane = Math.Min(Math.Min(loss, most), Floor(money));
            return (int)Math.Max(0L, sane);
        }
    }
}
