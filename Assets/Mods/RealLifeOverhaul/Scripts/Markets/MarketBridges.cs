using System;
using System.Collections.Generic;
using RealLifeOverhaul.Economy.Ventures;

namespace RealLifeOverhaul.Markets
{
    /// <summary>
    /// What the stock market needs from the rest of the mod. Game-free. Set by Wiring/MarketLinks on every city load and
    /// put back by <see cref="Reset"/>; the market calls no other feature itself. Each is read when it is needed (the
    /// city at a market day's open, the rivals when a market is opened, the old market once), never per frame.
    /// </summary>
    public static class MarketBridges
    {
        /// <summary>
        /// The city on a market day (MarketEnv: the cycle, the bank rate and its next decision, inflation and the price
        /// level, the districts' income, the supply of each kind of goods, the council's policies and an election, the
        /// weather and the street, what was reported the day before, the player's own takings by trade). Null: a quiet
        /// city at today's price level.
        /// </summary>
        public static Func<int, MarketEnv> City;

        /// <summary>The game's named rivals: their names and the game's ids for them, in the same order (for the market's rival holdings).</summary>
        public static Action<List<string>, List<string>> Rivals;

        /// <summary>
        /// The old market of this save (Ventures' file), for its holdings to be brought over once. Null while Ventures is
        /// still opening its file (ask again later); a hand-over with no file when there is nothing to bring.
        /// </summary>
        public static Func<OldMarket> Old;

        /// <summary>
        /// Asked once a second while the market runs: what happened since the last look that the market answers at once
        /// (a policy moved, an election counted, a storm breaking), each told with (kind, a, b) as MarketEngine.Event
        /// takes it. Null: nothing is told.
        /// </summary>
        public static Action<Action<int, int, int>> Poll;

        public static void Reset()
        {
            City = null;
            Rivals = null;
            Old = null;
            Poll = null;
        }
    }

    /// <summary>The old market's file as the wiring hands it over: the file (none: nothing to bring) and what to do once it has been changed.</summary>
    public sealed class OldMarket
    {
        public VenturesFile File;

        /// <summary>Called once the holdings have been moved out of the file: writes it back into the save when it is not Ventures' live one.</summary>
        public Action Done;
    }
}
