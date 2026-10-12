using System;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// Money at the city's prices. Every amount of the mod's is written at the prices of the start (the city's price
    /// index at 100) and asked for here when it is charged, paid or shown, so that all of it moves with the city's
    /// price level (<see cref="CityEconomy.PriceIndex"/>, worked out by the city's economy once a month: inflation of
    /// -1 % to 12 % a year, moved by the cycle, the central bank and events). While the city's economy is off the level
    /// stays at the start's and every amount is as written. Game-free; the rules are <see cref="PriceRules"/>.
    /// </summary>
    public static class Prices
    {
        /// <summary>The city's price level in tenths of a point: 1000 at the start's prices.</summary>
        public static int Level => PriceRules.Level(CityEconomy.PriceIndex);

        /// <summary>An amount written at the start's prices, at today's, rounded as prices are named.</summary>
        public static float At(float amount) => PriceRules.At(amount, Level);

        /// <summary>The same, in whole money.</summary>
        public static int At(int amount) => PriceRules.At(amount, Level);
    }

    /// <summary>The rules of <see cref="Prices"/>, game-free (checked by EconomyCheck).</summary>
    public static class PriceRules
    {
        public const int Start = 1000;       // the level at the start's prices, in tenths of a point
        public const int Least = 500;        // the level is read within these (the index runs from 50 to 400)
        public const int Most = 4000;

        /// <summary>The level in tenths of a point from the city's index (100 at the start).</summary>
        public static int Level(float index)
        {
            int level = (int)Math.Round(index * 10f, MidpointRounding.AwayFromZero);
            return level < Least ? Least : level > Most ? Most : level;
        }

        /// <summary>
        /// An amount at a level, rounded as prices are named: to five cents under 10, to fifty cents under 100, to 5 under
        /// 1,000, to 10 under 10,000, to 50 above. At the start's level a round amount is itself.
        /// </summary>
        public static float At(float amount, int level)
        {
            if (level == Start)
            {
                return amount;
            }
            return Round(amount * Math.Max(Least, Math.Min(Most, level)) / Start);
        }

        /// <summary>The same, in whole money (an amount under 10 is rounded to the whole after its own rounding).</summary>
        public static int At(int amount, int level)
        {
            if (level == Start)
            {
                return amount;
            }
            return (int)Math.Round(At((float)amount, level), MidpointRounding.AwayFromZero);
        }

        /// <summary>Money rounded as prices are named (see <see cref="At(float, int)"/>).</summary>
        public static float Round(float money)
        {
            float size = Math.Abs(money);
            float step = size < 10f ? 0.05f : size < 100f ? 0.5f : size < 1000f ? 5f : size < 10000f ? 10f : 50f;
            return (float)(Math.Round(money / step, MidpointRounding.AwayFromZero) * step);
        }
    }
}
