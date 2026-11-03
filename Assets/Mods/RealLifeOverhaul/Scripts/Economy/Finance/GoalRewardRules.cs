using System;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// What a personal goal reached pays (the economy's option, on by default): sized by the goal's difficulty, its tier
    /// among the goals of its kind as the game's Persona app counts them (the first, the second, the third and last). Whole
    /// money at the start's prices, asked of Prices when paid. Rivals have no goals. Game-free; checked offline (EconomyCheck).
    /// </summary>
    public static class GoalRewardRules
    {
        /// <summary>The reward by tier, at the start's prices.</summary>
        public static readonly int[] ByTier = { 5000, 20000, 75000 };

        /// <summary>The reward for a goal of a tier (0 the first of its kind); a tier past the last pays as the last.</summary>
        public static int Reward(int tier) => ByTier[Math.Max(0, Math.Min(ByTier.Length - 1, tier))];

        /// <summary>
        /// A goal's tier among the goals of its kind: how many of them ask for less (their sort values, the game's own order
        /// of a kind's tiers). <paramref name="values"/> holds the kind's goals' sort values, the goal's own among them.
        /// </summary>
        public static int Tier(float own, float[] values, int count)
        {
            int tier = 0;
            for (int i = 0; i < count && i < values.Length; i++)
            {
                if (values[i] < own)
                {
                    tier++;
                }
            }
            return tier;
        }
    }
}
