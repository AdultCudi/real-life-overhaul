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

        // ---------------------------------------------------------------- the player's own company (Markets/Listing)

        /// <summary>
        /// The player's own businesses' takings and profit over the statement days from one day up to (not with) another,
        /// from the game's daily statements: only businesses that are the player's own (Going Public's test). Null: none.
        /// </summary>
        public static Func<int, int, OwnBooks> Books;

        /// <summary>What the advisers and the auditors read of the player: years in business, the tax, the credit file, Standing, debt. Null: nothing known.</summary>
        public static Func<ListingFacts> Facts;

        /// <summary>The places of the player's own businesses now (the ones a takeover would hand over).</summary>
        public static Action<List<string>> Businesses;

        /// <summary>
        /// One business of the player's handed over to a buyer as a sale is completed, with no money (a takeover paid for
        /// the shares): true when it is no longer the player's; false while it cannot be done now (to be tried again).
        /// </summary>
        public static Func<string, string, bool> HandOver;

        /// <summary>A name for the player's company to begin with (their largest business's), and the trade its takings are most in (MarketInputs.Trades; -1 none).</summary>
        public static Func<string> Name;

        public static Func<int> Trade;

        /// <summary>The names of the city's three largest banks, the largest first (Economy/Lending's BankTerms.Banks): each offers a certificate of deposit (Markets/Bonds). Null: none.</summary>
        public static Action<List<string>> Banks;

        /// <summary>
        /// What the player is worth as the Finance app's Overview says it (Economy/Finance, Books.NetWorth: the game's own
        /// figure less what the player owes on the mod's own accounts and other debts). It counts nothing on the market: the
        /// market adds its own (the most of one bond purchase). Null: the game's own figure alone.
        /// </summary>
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

    /// <summary>A stretch of the player's own businesses' statements: takings and profit together, in money, and the statement days found.</summary>
    public struct OwnBooks
    {
        public long Revenue, Profit;
        public int Days;
    }

    /// <summary>The old market's file as the wiring hands it over: the file (none: nothing to bring) and what to do once it has been changed.</summary>
    public sealed class OldMarket
    {
        public VenturesFile File;

        /// <summary>Called once the holdings have been moved out of the file: writes it back into the save when it is not Ventures' live one.</summary>
        public Action Done;
    }
}
