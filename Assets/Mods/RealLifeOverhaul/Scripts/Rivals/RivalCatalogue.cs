namespace RealLifeOverhaul.Rivals
{
    /// <summary>What a rival does to the player. Saved as a number: never renumber, add at the end.</summary>
    public enum RivalMoveKind
    {
        Property = 0,   // buys up buildings in the district of the player's best business: buildings for sale there cost more, and the rent there rises
        Outlet = 1,     // opens a business of the same kind in the same district: fewer customers at the player's of that kind there
        Poach = 2,      // offers the player's most skilled employee more than the going rate (the company's own offer)
        Stake = 3,      // buys a block of the player's listed company and presses its board (the market's own investor)
        Bid = 4,        // bids for the player's listed company (the market's own bid, won only while the founder holds under the threshold)
        Lobby = 5,      // lobbies the council: inspectors at the player's businesses more often, and one called in now
        Suit = 6,       // sues the player in the civil court (Justice's own suit, answered in the Legal file)
        Press = 7,      // a piece against the player in the City Ledger: a smear at the best business, and the customers think less of them
        Count = 8
    }

    /// <summary>What the player does back. Saved as a number: never renumber, add at the end.</summary>
    public enum RivalCounter
    {
        Outbid = 0,     // pays to end a rival's buying up or a rival's business near theirs
        Poach = 1,      // hires away a rival's best people: the rival's next move waits, and its fortune grows half as fast for a while
        Press = 2,      // a piece in the City Ledger about the rival: a move it has announced never comes, or its next waits
        Lobby = 3,      // lobbies the council back: a rival's lobbying ends, and it may not lobby or buy up buildings for a while
        Takeover = 4,   // with the most of the rival's holding the market lets one holder have, buys the rest: the rival is out
        Count = 5
    }

    /// <summary>Where a move stands. Saved as a number: never renumber, add at the end.</summary>
    public enum RivalMoveState
    {
        Announced = 0,  // in the paper and on the phone, to come on its day
        Running = 1,    // has come: its effect runs to its end day
        Over = 2,       // ran to its end
        Stopped = 3,    // the player stopped it (a counter)
        Failed = 4,     // nothing of the player's was there for it on its day: it came to nothing
        Count = 5
    }

    /// <summary>One rival tycoon as the catalogue writes it. Never changed in play; what changes is their <see cref="Tycoon"/>.</summary>
    public sealed class TycoonDef
    {
        public readonly string Name;           // an invented person, never translated
        public readonly string Holding;        // their holding company, as the stock market lists it (Markets/MarketCatalogue)
        public readonly int StockId;           // the holding's id on the stock market
        public readonly long StartWorth;       // what they are worth when they first come forward, at the start's prices
        public readonly int TargetPermille;    // the fortune they aim for against the player's, at the Normal strength
        public readonly int[] Weights;         // their liking for each kind of move (RivalMoveKind order): what makes them themselves

        public TycoonDef(string name, string holding, int stockId, long startWorth, int target, int[] weights)
        {
            Name = name;
            Holding = holding;
            StockId = stockId;
            StartWorth = startWorth;
            TargetPermille = target;
            Weights = weights;
        }
    }

    /// <summary>An old fortune on the city's rich list: a name that has been rich for a long time and does nothing to the player.</summary>
    public struct FortuneDef
    {
        public readonly string Name;
        public readonly long Worth;            // at the start's prices

        public FortuneDef(string name, long worth)
        {
            Name = name;
            Worth = worth;
        }
    }

    /// <summary>
    /// Every figure of the rival tycoons; game-free, read by the Guide and the offline checks. Money is whole dollars at
    /// the start's prices, read at today's by the price level; shares are per mille unless named; days are game days.
    /// The pressure is for the late game: nothing moves below <see cref="TierFrom"/>[1], each tier past it brings closer,
    /// larger moves, and every move is announced ahead and bounded by its "Most" figure, so nothing ruins the player in
    /// one blow.
    /// </summary>
    public static class RivalCatalogue
    {
        public static readonly TycoonDef[] Tycoons =
        {
            //                 name                  holding                stock   worth at start  aim   property outlet poach stake bid lobby suit press
            new TycoonDef("Space Karen", "Karen Aerospace & Motors", 143, 40000000L, 700, new[] { 30, 10, 10, 15, 10, 25, 10, 10 }),
            new TycoonDef("Bald Luthor", "Luthor Everything Co.", 144, 60000000L, 900, new[] { 10, 35, 30, 10, 5, 10, 5, 15 }),
            new TycoonDef("The Zucc", "Zucc Social Holdings", 145, 90000000L, 1100, new[] { 10, 10, 10, 30, 25, 5, 25, 20 })
        };

        public const int TycoonCount = 3;

        /// <summary>The city's old fortunes on the rich list beside the tycoons and the player: they never move against anybody.</summary>
        public static readonly FortuneDef[] Fortunes =
        {
            new FortuneDef("The Oracle of Omaha", 2200000000L),
            new FortuneDef("The Wolf in Cashmere", 1400000000L),
            new FortuneDef("Chair-Leap Bill", 900000000L),
            new FortuneDef("Developers Steve", 650000000L),
            new FortuneDef("Yacht Larry", 400000000L),
            new FortuneDef("Rocket Richard", 250000000L)
        };

        /// <summary>The player's worth from which each tier starts, at the start's prices: quiet, tens of millions, hundreds of millions, billions, ten billion and more.</summary>
        public static readonly long[] TierFrom = { 0L, 5000000L, 100000000L, 1000000000L, 10000000000L };

        public const int Tiers = 5;

        /// <summary>
        /// The steps of the option for when the first rival comes forward, at the start's prices: today's threshold first (the
        /// default), up to 50 million for a player who wants to grow rich in peace. Every step is under the second tier's, so the
        /// second and third tycoons still come at hundreds of millions and billions.
        /// </summary>
        public static readonly long[] FirstFromSteps = { 5000000L, 10000000L, 20000000L, 30000000L, 50000000L };

        /// <summary>How many tycoons have come forward by each tier (they stay once they have).</summary>
        public static readonly int[] ActiveAt = { 0, 1, 2, 3, 3 };

        /// <summary>The fewest days between two rivals' moves (any rival), by tier, at the Normal strength: "every few days at most" in the end game.</summary>
        public static readonly int[] GapDays = { 0, 12, 7, 4, 3 };

        /// <summary>How hard a move hits by tier, per mille of its Normal size: smaller for a player who is small.</summary>
        public static readonly int[] TierPower = { 0, 700, 1000, 1200, 1400 };

        public const int StakeTier = 2;            // a stake in the player's listed company from hundreds of millions
        public const int BidTier = 3;              // a bid for it from billions
        public const int CompanyQuarters = 1;      // no stake or bid before the player's company has traded this many quarters of a year: the market's own guard for a new listing

        public const int Gentle = 0, Normal = 1, Hard = 2, Ruthless = 3, Strengths = 4;

        /// <summary>What the gap between moves is multiplied by, per mille, by strength.</summary>
        public static readonly int[] StrengthGap = { 1500, 1000, 800, 650 };

        /// <summary>How hard a move hits, per mille of its Normal size, by strength (the "Most" figures bound it whatever the strength).</summary>
        public static readonly int[] StrengthPower = { 600, 1000, 1300, 1600 };

        /// <summary>The fortune the tycoons aim for, per mille of their own aim, by strength.</summary>
        public static readonly int[] StrengthAim = { 700, 1000, 1150, 1300 };

        public const int TelegraphDays = 2;        // a move is in the paper and on the phone this many days before it comes: time to answer it
        public const int JitterDays = 2;           // the gap between moves is longer by up to this many days, drawn: not like clockwork
        public const int MovesKept = 24;           // moves kept for the page and the paper, newest last (the oldest that are over go first)
        public const int CountersKept = 16;        // the player's counters kept, newest last

        public const int MonthDays = 30;           // the growth figures are for thirty days, whatever the length of the save's year

        /// <summary>A tycoon's own growth over thirty days, basis points, by the city's cycle (boom, steady, slowdown, recession): their business is the city's.</summary>
        public static readonly int[] CycleGrowthBp = { 200, 120, 40, -80 };

        public const int CatchDays = 90;           // a tycoon behind their aim closes the gap over about this many days...
        public const int CatchMostBp = 150;        // ...but never by more than this a day (basis points of their worth): a rival is never made overnight
        public const int SlowShare = 500;          // per mille of their growth while a counter has slowed them
        public const int ForwardShare = 500;       // a tycoon comes forward worth at least this share of their aim, per mille: a rival a player already rich takes notice of
        public const int ForwardPerDay = 1;        // tycoons who come forward on one day, at most: a player rich on their first day meets them over a few days

        public const int PropertyDays = 14;
        public const int PropertyPremium = 120;    // per mille on the price of buildings for sale in the district
        public const int PropertyPremiumMost = 200;
        public const int RentRise = 80;            // per mille on the rent of the player's business there, if it is rented
        public const int RentRiseMost = 150;

        public const int OutletDays = 21;
        public const int OutletDip = 6;            // per cent of customers at the player's businesses of that kind in that district
        public const int OutletDipMost = 10;       // per cent, all a rival's businesses together at one of the player's
        public const int OutletsMost = 3;          // rivals' businesses open at once

        public const int PoachPremium = 150;       // per mille over the going rate (the company's own rivals offer 50)
        public const int PoachPremiumMost = 250;

        public const int LobbyDays = 10;
        public const int LobbyWeight = 500;        // per mille more inspections, licences and audits while it runs
        public const int LobbyWeightMost = 1000;

        public const int SuitShare = 25;           // per ten thousand of the player's worth claimed (a quarter of a per cent)...
        public const int SuitShareMost = 50;       // ...half a per cent at most
        public const long SuitLeast = 50000L;
        public const long SuitMost = 25000000L;    // at the start's prices: a claim is a nuisance, never a ruin

        public const int PressCustomers = -2;      // points of the customers' reputation (Standing)

        public const int BidAfterStakeDays = 90;   // a bid comes only within this many days of the same tycoon's stake...
        public const int BidRestDays = 180;        // ...and never twice within this many days

        /// <summary>How long each kind of move runs once it has come, in days (0: it is over the day it comes; its own feature carries it from there).</summary>
        public static readonly int[] MoveDays = { PropertyDays, OutletDays, 0, 0, 0, LobbyDays, 0, 0 };

        /// <summary>What a counter costs, per ten thousand of the player's worth, between the least and the most below (a takeover is priced by the market).</summary>
        public static readonly int[] CounterShare = { 20, 10, 5, 10, 0 };
        public static readonly long[] CounterLeast = { 50000L, 25000L, 10000L, 25000L, 0L };
        public static readonly long[] CounterMost = { 20000000L, 10000000L, 5000000L, 10000000L, 0L };

        /// <summary>What the most above is multiplied by at each tier: an answer costs its share of the worth at the top, not a rounding error of it.</summary>
        public static readonly int[] CounterTierScale = { 1, 1, 5, 25, 100 };

        /// <summary>Days before the same counter can be used against the same tycoon again (and each is used once against one move at most).</summary>
        public static readonly int[] CounterRestDays = { 7, 30, 14, 30, 0 };

        public const int PoachQuietDays = 10;      // a counter-poach: the rival's next move waits this many days...
        public const int PoachSlowDays = 30;       // ...and their fortune grows at half the pace for this many
        public const int PressDelayDays = 5;       // a piece about a rival with nothing announced: their next move waits this many days...
        public const int PressWorthCut = 10;       // ...and their worth falls this many per mille
        public const int LobbyBarDays = 30;        // a rival lobbied back may not lobby or buy up buildings for this many days

        public const int TakeoverPremiumBp = 2500; // the player's bid for a holding: this far over its price
        public const int BreakUpPermille = 900;    // what the holding's parts fetch, per mille of what the shares bought were worth at its price
        public const int BreakUpDays = 7;          // ...paid to the player this many days after the takeover

        public const int PiecesMost = 2;           // the rivals' pieces in one morning's paper
        public const int RichListDay = 0;          // the rich list is printed on the days whose number divides by seven with this left over
        public const int RichListSize = 10;        // three tycoons, the player and six old fortunes

        /// <summary>The name of each kind of move.</summary>
        public static readonly string[] MoveNames =
        {
            "reallife_rival_move_property", "reallife_rival_move_outlet", "reallife_rival_move_poach", "reallife_rival_move_stake",
            "reallife_rival_move_bid", "reallife_rival_move_lobby", "reallife_rival_move_suit", "reallife_rival_move_press"
        };

        /// <summary>What each kind of move does, for its page.</summary>
        public static readonly string[] MoveAbout =
        {
            "reallife_rival_move_about_property", "reallife_rival_move_about_outlet", "reallife_rival_move_about_poach", "reallife_rival_move_about_stake",
            "reallife_rival_move_about_bid", "reallife_rival_move_about_lobby", "reallife_rival_move_about_suit", "reallife_rival_move_about_press"
        };

        public static readonly string[] CounterNames =
        {
            "reallife_rival_counter_outbid", "reallife_rival_counter_poach", "reallife_rival_counter_press", "reallife_rival_counter_lobby", "reallife_rival_counter_takeover"
        };

        public static readonly string[] StateNames =
        {
            "reallife_rival_state_announced", "reallife_rival_state_running", "reallife_rival_state_over", "reallife_rival_state_stopped", "reallife_rival_state_failed"
        };

        public static readonly string[] StrengthNames =
        {
            "reallife_rival_strength_gentle", "reallife_rival_strength_normal", "reallife_rival_strength_hard", "reallife_rival_strength_ruthless"
        };

        public static readonly string[] TierNames =
        {
            "reallife_rival_tier_quiet", "reallife_rival_tier_tens", "reallife_rival_tier_hundreds", "reallife_rival_tier_billions", "reallife_rival_tier_ten_billions"
        };

        public static int Clamp(int v, int least, int most) => v < least ? least : v > most ? most : v;

        public static long Clamp(long v, long least, long most) => v < least ? least : v > most ? most : v;
    }
}
