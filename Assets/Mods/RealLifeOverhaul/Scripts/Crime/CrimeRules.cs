using System;

namespace RealLifeOverhaul
{
    // Game-free: EconomyCheck compiles this file as it is. Every locale key is written out in full.

    /// <summary>
    /// The money and the words of the crime scenes that are worked out, not dealt: what a hold-up takes from the till of
    /// the player's own shop, a reward at today's prices, and which line tells of a release from a holding cell.
    /// </summary>
    internal static class CrimeRules
    {
        public const float HoldUpTillShare = 0.25f;              // of what the shop takes in a day: the till is emptied before it is banked
        public const int HoldUpLeast = 40, HoldUpMost = 500;     // dollars at the start's prices: the float in a quiet till, and what one grab carries
        public const int RewardLeast = 20, RewardMost = 80;      // dollars at the start's prices: thanks for a citizen's arrest

        /// <summary>
        /// What a hold-up takes from the till of the player's own shop: a share of the shop's day (the game's own takings,
        /// at today's prices), within a floor and a cap at the start's prices moved to <paramref name="level"/>, rounded to
        /// 5 and never more than the money there is (the game keeps one purse for the player and every business). 0: nothing.
        /// </summary>
        public static int TillTake(float dayTakings, float cash, int level)
        {
            float least = PriceRules.At((float)HoldUpLeast, level), most = PriceRules.At((float)HoldUpMost, level);
            float amount = Math.Max(0f, dayTakings) * HoldUpTillShare;
            amount = amount < least ? least : amount > most ? most : amount;
            int taken = (int)Math.Round(amount / 5f) * 5;
            return Math.Max(0, Math.Min(taken, WholeMoney.Floor(cash)));
        }

        /// <summary>
        /// An amount between two at the start's prices, picked by <paramref name="roll"/> (0 to 1), rounded to 5 and moved
        /// to <paramref name="level"/>: at the start's prices exactly what it was written as.
        /// </summary>
        public static int Thanks(float roll, int least, int most, int level)
        {
            float pick = least + (most - least) * Math.Max(0f, Math.Min(1f, roll));
            return PriceRules.At((int)Math.Round(pick / 5f) * 5, level);
        }

        /// <summary>
        /// The line for a release from a holding cell when no precinct held the player: in a Going Public session no time
        /// is passed, so no hours are named.
        /// </summary>
        public static string CustodyReleasedKey(bool clockShared)
        {
            return clockShared ? "reallife_crime_custody_released_shared" : "reallife_crime_custody_released";
        }
    }
}
