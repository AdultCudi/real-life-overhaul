namespace RealLifeOverhaul.Tech
{
    /// <summary>What a startup does.</summary>
    public enum Venture
    {
        Software = 0, Fintech = 1, Health = 2, Ai = 3, Games = 4, Marketplace = 5, Climate = 6, Security = 7, DevTools = 8, Count = 9
    }

    /// <summary>A startup's stage: the round it has raised. Saved as a number.</summary>
    public enum RoundStage
    {
        PreSeed = 0, Seed = 1, SeriesA = 2, SeriesB = 3, SeriesC = 4, Growth = 5, Count = 6
    }

    /// <summary>Where a startup stands.</summary>
    public enum StartupState
    {
        Running = 0, Raising = 1, Acquired = 2, Failed = 3, Listed = 4
    }

    /// <summary>A fictional venture capital firm or angel group of the city.</summary>
    public sealed class VcDef
    {
        public readonly int Id;
        public readonly string Name;
        public readonly int Least, Most;      // the stages it leads (RoundStage)
        public readonly int Likes;            // one bit per Venture it is keen on
        public readonly int Diligence;        // 0 to 1000: how hard it looks
        public readonly bool Angels;

        public VcDef(int id, string name, int least, int most, int likes, int diligence, bool angels)
        {
            Id = id;
            Name = name;
            Least = least;
            Most = most;
            Likes = likes;
            Diligence = diligence;
            Angels = angels;
        }
    }

    /// <summary>The figures of startups and venture capital. Game-free; cents at the start's prices; a month is 30 days.</summary>
    public static class VentureCatalogue
    {
        // The firms
        public static readonly VcDef[] Firms =
        {
            new VcDef(0, "Old Mill Angels", 0, 1, 0x1FF, 250, true),
            new VcDef(1, "Harbour Seed Partners", 0, 2, (1 << 0) | (1 << 3) | (1 << 8) | (1 << 7), 400, false),
            new VcDef(2, "Brightwater Ventures", 1, 3, (1 << 0) | (1 << 1) | (1 << 5), 600, false),
            new VcDef(3, "Copperline Capital", 2, 4, (1 << 1) | (1 << 2) | (1 << 6), 700, false),
            new VcDef(4, "Granite Peak Ventures", 2, 5, (1 << 0) | (1 << 3) | (1 << 7) | (1 << 8), 750, false),
            new VcDef(5, "Northlight Partners", 3, 5, 0x1FF, 800, false),
            new VcDef(6, "Ironbark Growth", 4, 5, (1 << 1) | (1 << 3) | (1 << 5), 850, false),
            new VcDef(7, "Lantern Row Angels", 0, 1, (1 << 2) | (1 << 4) | (1 << 6), 200, true)
        };

        public const int FirmCount = 8;

        // The city's startups
        public const int StartupsMost = 20;             // startups running at once
        public const int BirthChance = 60;              // per mille a day at a neutral mood
        public static readonly int[] MoodByCycle = { 1250, 1000, 780, 560 };   // the market's mood by the city's cycle, thousandths
        public const int HypeSwing = 600;               // thousandths the AI wave adds at its top and takes at its foot
        public const int HypeYears = 2;                 // years the AI wave takes to come round

        /// <summary>Recurring revenue a month a stage needs before it can raise the next round, cents.</summary>
        public static readonly long[] RoundBar = { 0, 1000000, 8000000, 40000000, 150000000, 400000000 };

        /// <summary>The least a round values a company at before the money, cents.</summary>
        public static readonly long[] FloorValue = { 150000000, 500000000, 1500000000, 5000000000, 15000000000, 40000000000 };

        public static readonly int[] BurnByStage = { 80000, 250000, 900000, 3000000, 8000000, 18000000 };   // cents a day
        public const int RunwayMonthsRaised = 18;       // a round raises this many months of burn
        public const int RaiseWhenMonths = 6;           // a startup raises when its runway falls under this
        public const int OptionPool = 100;              // thousandths set aside for staff at a seed round
        public const int LiquidationPreference = 1000;  // thousandths of the money put in that comes back first on a sale (one times)
        public const int GrowthBest = 250, GrowthWorst = 20;   // thousandths a month of recurring revenue growth, by the founders' quality: 25% to 2%
        public const int MultipleLeast = 6, MultipleMost = 30;   // a round's revenue multiple (recurring a year) by growth
        public const int DownRoundCut = 400;            // thousandths off the last value in a down round
        public const int FailedRaisesToDie = 2;         // failed raises before a startup out of money goes
        public const int AcquireChance = 4;             // per mille a day for a company at series B or later (a third of it at seed, two thirds at series A)
        public const int AcquireMultipleLeast = 3, AcquireMultipleMost = 9;   // times a year's recurring revenue a buyer pays
        public const int AcquihireShare = 300;          // thousandths of what was put in that an acqui-hire pays
        public const long ListingArr = 3000000000;      // cents of recurring revenue a year at which a growth company could list
        public const int ListingMultiple = 10;          // ...and the times its recurring revenue a year the market values it at

        // The player as an investor
        public const int PitchChance = 350;             // per mille a raising startup comes to the player as well
        public const long TicketLeast = 2500000;        // the least the player can put in, cents
        public const int AskPlayerShare = 250;          // thousandths of a round the founders ask the player to fill
        public const int DiligenceFee = 150000;         // cents, for a due diligence report
        public const int HaggleCut = 150;               // thousandths off the valuation the player can ask for
        public const int MeetingQuestions = 3;          // questions at a pitch meeting
        public const int ProRataDays = 5;               // days to take up a follow-on offer

        // The player's own firms raising
        public const int RaiseDays = 10;                // a fundraising round runs this long
        public const int VcMeetingsMost = 3;            // meetings in a raise
        public const int TermSheetDays = 5;             // days a term sheet stands
        public const int BoardSeatsPerRound = 1;
        public const int TargetGrowth = 150;            // thousandths of growth in recurring revenue a quarter a board expects
        public const int TargetDays = 90;               // days to a board's target
        public const int MissesToReplace = 2;           // targets missed in a row before a board with a majority replaces the chief executive
        public const int BridgeInterestBp = 800;        // a year
        public const int BridgeDiscount = 200;          // thousandths off the next round's price a bridge converts at
        public const int BridgeMonths = 6;              // months of burn a bridge lends
        public const int OptionGrant = 20;              // thousandths of the firm granted to staff at a time
        public const int VestYears = 4, CliffYears = 1;   // years of the game's length
        public const int OptionMorale = 100;            // thousandths of morale a grant adds
        public const int OptionPoach = 600;             // thousandths of the rivals' approaches with options unvested

        /// <summary>Names for the city's startups: two halves.</summary>
        public static readonly string[] NameA =
        {
            "Nimble", "Bright", "Clear", "True", "Swift", "Kind", "Open", "Little", "North", "Blue", "Green", "Silver", "Quiet", "Bold", "Tidy", "Lucky"
        };

        public static readonly string[] NameB =
        {
            "loop", "dash", "nest", "lane", "spark", "ledger", "wise", "grid", "field", "path", "byte", "craft", "harvest", "pocket", "signal", "mint"
        };
    }
}
