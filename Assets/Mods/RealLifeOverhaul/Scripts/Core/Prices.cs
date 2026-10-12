using System;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Money at the city's prices. Every amount of the mod's is written at the prices of the start (the city's price
    /// index at 100) and asked for here when it is charged, paid or shown, so that all of it moves with the city's
    /// price level: the factor the city's economy adjusts the game's own rents, wages, shops and services by each night
    /// (Economy/City, worked out once a month: inflation of -1 % to 12 % a year, moved by the cycle, the central bank and
    /// events). While the city's economy is off the level stays at the start's and every amount is as written. Every
    /// feature reads it, so it is the mod's and not the economy's (which no feature calls): game-free and on its own,
    /// the factor handed in by Wiring/EconomyLinks, so that the checkers compile it with the catalogues. The rules are
    /// <see cref="PriceRules"/>.
    /// </summary>
    public static class Prices
    {
        /// <summary>
        /// The factor the city's economy adjusts the game's own costs by, in tenths of a point (1000 at the start's
        /// prices): set by the wiring (Economy.CityEconomy.CostFactor, the start's while the city's economy is off).
        /// Null: the start's prices.
        /// </summary>
        public static Func<int> Factor;

        /// <summary>The city's price level in tenths of a point: 1000 at the start's prices.</summary>
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

        /// <summary>The same, in whole money.</summary>
        public static int At(int amount) => PriceRules.At(amount, Level);
    }

    /// <summary>The rules of <see cref="Prices"/>, game-free (checked by EconomyCheck).</summary>
    public static class PriceRules
    {
        public const int Start = 1000;       // the level at the start's prices, in tenths of a point
        public const int Quiet = 10;         // within this of the start (1 %), the start's: the city adjusts nothing there either (CityAdjust.Of)
        public const int Least = 500;        // the level is read within these (the index runs from 50 to 400)
        public const int Most = 4000;

        /// <summary>
        /// The level from the city's factor for costs (CityModel.Costs, tenths of a point): the start's within the quiet
        /// band, as the city adjusts nothing there, so a price of the mod's and the game's own one adjusted by the city
        /// are always at the same level.
        /// </summary>
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
