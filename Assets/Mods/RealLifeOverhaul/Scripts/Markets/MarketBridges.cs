using System;
using System.Collections.Generic;
using RealLifeOverhaul.Economy.Ventures;

namespace RealLifeOverhaul.Markets
{
    /// <summary>
    /// What the stock market needs from the rest of the mod, set by Wiring/MarketLinks on every city load and put back by
    /// <see cref="Reset"/>; the market calls no other feature itself. Each is read when needed, never per frame.
    /// </summary>
    public static class MarketBridges
    {
        /// <summary>The city on a market day as the market reads it; null: a quiet city at today's price level.</summary>
        public static Func<int, MarketEnv> City;

        /// <summary>The game's named rivals: their names and the game's ids for them, in the same order.</summary>
        public static Action<List<string>, List<string>> Rivals;

        /// <summary>The save's old Ventures market, brought over once; null while Ventures is still opening its file, so ask again.</summary>
        public static Func<OldMarket> Old;

        /// <summary>Asked once a second: what happened since the last look, each told as MarketEngine.Event takes it.</summary>
        public static Action<Action<int, int, int>> Poll;

        /// <summary>The player's own businesses' takings and profit over the statement days from one day up to, not with, another.</summary>
        public static Func<int, int, OwnBooks> Books;

        /// <summary>What the advisers and the auditors read of the player; null: nothing known.</summary>
        public static Func<ListingFacts> Facts;

        /// <summary>The places of the player's own businesses now, which a takeover would hand over.</summary>
        public static Action<List<string>> Businesses;

        /// <summary>One business handed over after a takeover: true once it is no longer the player's, false while it cannot be done.</summary>
        public static Func<string, string, bool> HandOver;

        /// <summary>A name for the player's company to begin with, and the trade its takings are most in (-1 none).</summary>
        public static Func<string> Name;

        public static Func<int> Trade;

        /// <summary>The names of the city's three largest banks, the largest first, each offering a certificate of deposit.</summary>
        public static Action<List<string>> Banks;

        /// <summary>The player's worth as the Finance app's Overview says it, counting nothing on the market; null: the game's alone.</summary>
        public static Func<long> NetWorth;

        public static void Reset()
        {
            City = null;
            Rivals = null;
            Old = null;
            Poll = null;
            Books = null;
            Facts = null;
            Businesses = null;
            HandOver = null;
            Name = null;
            Trade = null;
            Banks = null;
            NetWorth = null;
        }
    }

        /// <summary>A stretch of the player's own businesses' statements: takings and profit in money, and the days found.</summary>
    public struct OwnBooks
    {
        public long Revenue, Profit;
        public int Days;
    }

        /// <summary>The old market's file as the wiring hands it over, and what to do once it is changed.</summary>
    public sealed class OldMarket
    {
        public VenturesFile File;

        /// <summary>Writes the file back into the save when it is not Ventures' live one.</summary>
        public Action Done;
    }
}
