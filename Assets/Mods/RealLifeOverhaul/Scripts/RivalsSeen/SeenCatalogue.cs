namespace RealLifeOverhaul.RivalsSeen
{
    /// <summary>How a rival's business is doing, read from their real figures (<see cref="SeenRules.Form"/>). Saved as a number: never renumber, add at the end.</summary>
    public enum SeenForm
    {
        Trouble = 0,    // the week's figure fell, a business was lost, or a counter of the player's holds them back
        Steady = 1,
        Thriving = 2,   // the week's figure rose, or a business was added
        Count = 3
    }

    /// <summary>Where a rival is at an hour of their week (<see cref="SeenRules.Where"/>). Saved as a number: never renumber, add at the end.</summary>
    public enum SeenSpot
    {
        Nowhere = 0,    // at home, asleep, on the way, or out of the picture: not seen
        Office = 1,     // at their firm's own premises in work hours
        Lunch = 2,      // at an eating place of their district
        Dinner = 3,
        Beach = 4,      // on the beach in the Hamptons
        Hamptons = 5,   // in the Hamptons for the weekend
        Late = 6,       // still at the office after hours
        Count = 7
    }

    /// <summary>What a sighting is about. Saved as a number: never renumber, add at the end.</summary>
    public enum SeenNoteKind
    {
        Place = 0,      // a rival seen at the place their week has them at
        NewCar = 1,     // a rival seen in a dearer car than before
        CarDown = 2,    // a rival seen in a cheaper car than before
        PlayerCar = 3,  // the player seen in their own car
        PlayerPlace = 4, // the player seen at an eating place
        Count = 5
    }

    /// <summary>
    /// Every figure of the rivals seen around the city, each with its unit and where it comes from; game-free, read by the Guide and the offline checks. Money is whole
    /// money at the start's prices; a share is per mille; hours are the game's own (0 to 23).
    /// </summary>
    public static class SeenCatalogue
    {
        // ---- how a business is doing (the week's change of the real figure, per mille)
        public const int OwnerThrive = 80;        // a rival owner's week of takings up by this much: the game's own week of takings moves a few per cent from day to day
        public const int OwnerTrouble = 80;       // ...or down by this much
        public const int TycoonThrive = 8;        // a tycoon's worth up by this much in a week: a steady city gives about 3, a boom about 5, closing on the player more (Rivals/RivalCatalogue.CycleGrowthBp)
        public const int TycoonTrouble = 0;       // ...or not up at all: only a recession, a counter or their own costs do that

        // ---- what they can afford (the tiers of means, from the real figure at the start's prices)
        /// <summary>A rival owner's week of takings from which each tier of means starts: modest, comfortable, well off, rich, very rich.</summary>
        public static readonly long[] OwnerTierFrom = { 0L, 15000L, 60000L, 200000L, 600000L };

        /// <summary>A tycoon's worth from which each tier starts: a tycoon is never less than rich (the least of them comes forward worth 20 million).</summary>
        public static readonly long[] TycoonTierFrom = { 0L, 0L, 0L, 0L, 100000000L };

        public const int Tiers = 5;

        // ---- the car
        public const int Rungs = 6;               // the rungs of the car ladder: a tier of means, one up when the business thrives, one down when it is in trouble
        public const int CarHoldDays = 14;        // a car is kept at least this long: nobody changes car with every week's figures

        // ---- the week (the game's hours; a week starts on Monday as the game's does)
        public const int WorkFrom = 9, LunchFrom = 12, LunchTo = 14, WorkTo = 18, DinnerFrom = 19, DinnerTo = 21;
        public const int ThrivingLeaves = 17;     // a rival doing well leaves the office an hour early
        public const int LateTo = 21;             // a rival in trouble stays until this hour
        public const int BeachFrom = 11, BeachTo = 16, HamptonsFrom = 10, HamptonsTo = 20;
        public const int HamptonsTier = 3;        // from this tier of means a thriving rival without a house there still weekends in the Hamptons

        /// <summary>How likely a rival is out at lunch on a working day, per mille, by how the business is doing: trouble, steady, thriving.</summary>
        public static readonly int[] LunchOut = { 250, 600, 1000 };

        /// <summary>...and out at dinner.</summary>
        public static readonly int[] DinnerOut = { 0, 300, 700 };

        public const int SteadyBeach = 400;       // per mille a steady rival takes a Saturday afternoon on the beach
        public const int Grades = 3;              // an eating place is modest, middling or smart: a third of the district's each, by the rent of the address

        // ---- in the world
        public const int WorldMost = 2;           // rivals standing in the world at once
        public const int PoolKeep = 2;            // people of the living city's pool left free for its own scenes when a rival is put out
        public const int NearMetres = 110;        // a rival is put out only at a place this near the player
        public const int LeastMetres = 18;        // ...and never this near: nobody appears beside the player
        public const int GoneMetres = 160;        // a rival this far behind is taken away
        public const int LookSeconds = 6;         // real seconds between two looks at who is near, while nobody new is due
        public const int TalkMetres = 4;          // the greeting is offered within this distance, on foot

        // ---- sightings
        public const int DayMost = 3;             // sightings of rivals planned for one day
        public const int SightingChance = 180;    // per mille a rival who is out somewhere on a day is written about
        public const int NotesKept = 24;          // sightings kept for the pages and the paper, newest last
        public const int PaperMost = 2;           // society lines in one morning's paper
        public const int PostsMost = 3;           // posts a day on the City Ledger's Flare account from here
        public const int PlayerGapDays = 6;       // days at least between two lines about the player
        public const int PlayerChance = 120;      // per mille an hour the player is out in a car or at a table, once the gap is over
        public const int PlayerCarRung = 3;       // the player's car is only worth a line from this rung of the ladder up

        // ---- knowing them
        public const int GreetWarmMost = 100;     // how well a rival can come to know the player (the social life's own scale)

        /// <summary>The words for how a business is doing, for a page.</summary>
        public static readonly string[] FormNames = { "reallife_seen_form_trouble", "reallife_seen_form_steady", "reallife_seen_form_thriving" };

        /// <summary>The words for a place, for a page.</summary>
        public static readonly string[] SpotNames =
        {
            "reallife_seen_spot_nowhere", "reallife_seen_spot_office", "reallife_seen_spot_lunch", "reallife_seen_spot_dinner", "reallife_seen_spot_beach",
            "reallife_seen_spot_hamptons", "reallife_seen_spot_late"
        };

        /// <summary>Where a rival tends to be found, by how the business is doing: a line of their page.</summary>
        public static readonly string[] HabitKeys = { "reallife_seen_habit_trouble", "reallife_seen_habit_steady", "reallife_seen_habit_thriving" };

        /// <summary>The same in a few words, for a narrow place on a page.</summary>
        public static readonly string[] FoundKeys = { "reallife_seen_found_trouble", "reallife_seen_found_steady", "reallife_seen_found_thriving" };

        /// <summary>A sighting at a place, by the place (<see cref="SeenSpot"/> order) and then by how the business is doing: empty where nothing is written.</summary>
        public static readonly string[][] PlaceLines =
        {
            new[] { string.Empty, string.Empty, string.Empty },
            new[] { "reallife_seen_line_office_trouble", string.Empty, string.Empty },
            new[] { "reallife_seen_line_lunch_trouble", "reallife_seen_line_lunch_steady", "reallife_seen_line_lunch_thriving" },
            new[] { string.Empty, "reallife_seen_line_dinner_steady", "reallife_seen_line_dinner_thriving" },
            new[] { string.Empty, "reallife_seen_line_beach_steady", "reallife_seen_line_beach_thriving" },
            new[] { string.Empty, string.Empty, "reallife_seen_line_hamptons_thriving" },
            new[] { "reallife_seen_line_late_trouble", string.Empty, string.Empty }
        };

        public const string NewCarLine = "reallife_seen_line_car_new", CarDownLine = "reallife_seen_line_car_down";
        public const string PlayerCarLine = "reallife_seen_line_player_car", PlayerPlaceLine = "reallife_seen_line_player_place";

        /// <summary>What a rival says when greeted, by how the business is doing, two ways each; then to somebody they know.</summary>
        public static readonly string[][] Hello =
        {
            new[] { "reallife_seen_hello_trouble_0", "reallife_seen_hello_trouble_1" },
            new[] { "reallife_seen_hello_steady_0", "reallife_seen_hello_steady_1" },
            new[] { "reallife_seen_hello_thriving_0", "reallife_seen_hello_thriving_1" }
        };

        public static readonly string[] HelloKnown = { "reallife_seen_known_trouble", "reallife_seen_known_steady", "reallife_seen_known_thriving" };
        public static readonly string[] BusinessTalk = { "reallife_seen_business_trouble", "reallife_seen_business_steady", "reallife_seen_business_thriving" };
        public static readonly string[] CarTalk = { "reallife_seen_car_trouble", "reallife_seen_car_steady", "reallife_seen_car_thriving" };

        public static int Clamp(int value, int least, int most) => value < least ? least : value > most ? most : value;
    }
}
