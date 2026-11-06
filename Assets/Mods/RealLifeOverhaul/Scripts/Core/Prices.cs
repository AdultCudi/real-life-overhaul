using System;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Money at the city's prices. Every amount of the mod's is written at the start's prices and asked for here when
    /// charged, paid or shown, so it moves with the factor the city's economy adjusts the game's own costs by; with the
    /// economy off it is as written. Game-free, with the factor handed in by the wiring, so the checkers compile it.
    /// </summary>
    public static class Prices
    {
        /// <summary>The city's cost factor in tenths of a point (1000 at the start), set by the wiring; null: the start's.</summary>
        public static Func<int> Factor;

        /// <summary>The city's price level in tenths of a point: 1000 at the start.</summary>
        public static int Level
        {
            get
            {
                Func<int> factor = Factor;
                return factor != null ? PriceRules.Level(factor()) : PriceRules.Start;
            }
        }

        /// <summary>An amount written at the start's prices, at today's, rounded as prices are named.</summary>
        public static float At(float amount) => PriceRules.At(amount, Level);

        public static int At(int amount) => PriceRules.At(amount, Level);
    }

    /// <summary>The rules of <see cref="Prices"/>, game-free.</summary>
    public static class PriceRules
    {
        public const int Start = 1000;       // the level at the start's prices, in tenths of a point
        public const int Quiet = 10;         // within this of the start (1 %) the start's level: the city adjusts nothing there either
        public const int Least = 500;        // the level is read within these (the index runs from 50 to 400)
        public const int Most = 4000;

        /// <summary>The level from the city's cost factor: the start's within the quiet band, as the city's own adjusting.</summary>
        public static int Level(int factor)
        {
            if (Math.Abs(factor - Start) < Quiet)
            {
                return Start;
            }
            return factor < Least ? Least : factor > Most ? Most : factor;
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

        /// <summary>The same in whole money (an amount under 10 is rounded to the whole after its own rounding).</summary>
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
