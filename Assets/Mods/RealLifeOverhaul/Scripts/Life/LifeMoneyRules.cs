using System;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The money of a life event, game-free so the checkers run it. An event's figures are written at the start's prices and
    /// are small; what an event takes or gives is bound by them whatever the purse holds or the arithmetic did. The bug it
    /// replaced: "never more than the money there is" read a purse past an int as int.MinValue and took that much, 2.1
    /// billion twice as "Vehicle costs" (27 Oct 2026). COST: a few comparisons when an event pays out, once an hour at most.
    /// </summary>
    internal static class LifeMoneyRules
    {
        /// <summary>The rounding an event's amount may be above its written worst: it is rounded to 5, then to the prices' own step.</summary>
        public const int RoundingSlack = 10;

        /// <summary>
        /// The most one event may move at a price level: its worst written figure at today's prices, and the rounding. The bound
        /// that ties a single charge to what the event could cost.
        /// </summary>
        public static long Bound(int writtenWorst, int level)
        {
            return (long)PriceRules.At(Math.Abs(writtenWorst), level) + RoundingSlack;
        }

        /// <summary>The charge a purse past an int made before the fix: int.MinValue as money. No price of the mod is this, so the amount names its own fault.</summary>
        public const float Wrapped = -2147483648f;

        /// <summary>What a wrapped charge is paid back by: the whole of it. The event's own figure was a few hundred at most; the player is never the poorer for the mod's fault.</summary>
        public const float RefundEach = 2147483648f;

        public const string TransactionPrefix = "reallife_transaction_";

        /// <summary>True for a charge in the game's history that the fault made: exactly int.MinValue, under one of the mod's own transaction types.</summary>
        public static bool IsWrapped(float amount, string transactionType)
        {
            return amount == Wrapped && transactionType != null && transactionType.StartsWith(TransactionPrefix, StringComparison.Ordinal);
        }

        /// <summary>How many wrapped charges are still to be paid back: those the history holds now, less those already paid back (kept in the save). Never negative.</summary>
        public static int Due(int found, int refunded)
        {
            return Math.Max(0, found - Math.Max(0, refunded));
        }

        /// <summary>
        /// The amount an event moves after its roll: a loss is never more than the money there is, a gain and a loss are
        /// never more than <see cref="Bound"/>. Negative for a loss. Never int.MinValue, whatever came in.
        /// </summary>
        public static int Settle(int amount, float money, int writtenWorst, int level)
        {
            long bound = Bound(writtenWorst, level);
            if (amount < 0)
            {
                return -WholeMoney.Take(-(long)amount, money, bound);
            }
            return (int)Math.Min(amount, bound);
        }
    }
}
