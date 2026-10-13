using System;

namespace RealLifeOverhaul.Economy
{
    /// <summary>What a let building is used as in the mod's books. Saved as a number: never renumber.</summary>
    public enum EstateUse
    {
        Homes = 0,
        Shops = 1,
        Offices = 2,
        Storage = 3
    }

    /// <summary>The works that can be under way at a building. Saved as a number.</summary>
    public enum EstateWork
    {
        None = 0,
        Repair = 1,     // the condition is put back to new
        Refurbish = 2,  // one level up
        Planning = 3,   // the council decides on a change of use
        Convert = 4     // the change of use is built
    }

    /// <summary>Where an instruction to the agency stands. Saved as a number.</summary>
    public enum InstructionStage
    {
        Offered = 0,    // an owner asks: the player takes it on or declines
        Marketing = 1,  // agent's hours are being spent on it
        Completing = 2  // a deal is agreed and completes on a day, or falls through
    }

    /// <summary>Where a car on hire stands. Saved as a number.</summary>
    public enum HireState
    {
        Idle = 0,       // on the lot, to be hired
        Out = 1,        // with a customer: it is not in the city
        Valet = 2,      // back and being cleaned: on the lot again, not to be hired until the day turns
        Waiting = 3     // due back, its space is taken: it comes back when the space is clear
    }

    /// <summary>
    /// Every figure of the property business: the market, letting, works, selling, the agency, a hotel and car hire.
    /// Game-free. Money is whole dollars; a share is in thousandths unless its name says otherwise; a condition, a
    /// rating and a reputation run from 0 to 1000; the price index is in tenths of a point (1000 at the start), as
    /// the company counts it; a district's income runs from 600 to 1400 with 1000 the middle (the city's economy).
    /// Tables "by cycle" are in the order Boom, Steady, Slowdown, Recession.
    ///
    /// THE BALANCE (EstateBalance works these out from the rules; EconomyCheck holds them to it). A year is the game's
    /// own, 60 days by default. Against "open another shop with the same money", which this table takes at the
    /// figures in <see cref="ShopCapital"/> and <see cref="ShopYearlyBp"/> (an assumption: the game's shops pay their
    /// set-up back in well under a year), in a steady economy:
    ///
    ///                         early ($25,000)        mid ($150,000)         late ($1,000,000)
    ///     a shop              about 140% a year      about 120%             about 90%: most money, most work
    ///     a let building      out of reach           2 to 7% net            2 to 7% net by district, plus or minus the market
    ///     a hotel, bought     out of reach           out of reach           9 to 22% of its price, by tier
    ///     a hotel, leased     out of reach           3 to 7% of its price a year for a deposit of twenty days' lease
    ///     a hire fleet        10 to 15% of the cars  10 to 15%              10 to 15%: capped by the lot's spaces
    ///     the agency          no capital: $40,000 to $55,000 a year before wages and the office, four times that
    ///                         in a boom with a name, a quarter of it in a recession
    ///
    /// None is free money: a let building can stand empty or be in arrears, and its value falls with the market; a
    /// hotel pays its staff and its lease on an empty night; a hire car wears, is damaged and now and then does not
    /// come back; an instruction costs its marketing whether or not the deal completes.
    /// </summary>
    public static class EstateCatalogue
    {
        // ---------------------------------------------------------------- how much is kept

        public const int HoldingsMost = 40;         // buildings on the books
        public const int ApplicantsMost = 3;        // applicants waiting for one unit (a building of one unit: for the building)
        public const int ApplicantsBuildingMost = 12;   // ...and for a whole building: one more for each unit after the first, no more than this
        public const int UnitsMost = 12;            // units a building is let in: the game's count, kept to this so a page and the save stay small
        public const int InstructionsMost = 8;      // instructions the agency has open
        public const int DistrictsMost = 24;        // districts with a reputation
        public const int HotelsMost = 8;            // as many as the city has
        public const int HireCarsMost = 12;         // cars on hire
        public const int ReviewsKept = 6;           // a hotel's last reviews
        public const int AlertsMost = 24;           // rows for the Planner
        public const int Thousand = 1000;

        // ---------------------------------------------------------------- the market

        public const int IndexStart = 1000;         // tenths of a point: prices as they stood when the save began
        public const int IncomeMiddle = 1000;       // a district of middling income
        public const int IncomeLeast = 600, IncomeMost = 1400;
        public const int DaysPerYearDefault = 60;   // the game's own year
        public const int RealStart = 1000;          // the property market against prices: 1000 is its long-run level
        public const int RealLeast = 600, RealMost = 1500;
        /// <summary>Where the market heads in each state of the cycle.</summary>
        public static readonly int[] RealTarget = { 1150, 1000, 930, 820 };
        public const int RealPerQuarterPoint = 6;   // each quarter point of the central bank's rate takes this off the target
        public const int RealPull = 120;            // thousandths of the gap to the target closed in a month
        public const int RealNoise = 10;            // a month's own move, either way
        public const int RealStepMost = 35;         // the most the market moves in a month

        public const int ConditionNew = 1000;
        public const int ConditionUnfit = 250;      // below this a building cannot be let
        public const int ConditionPoor = 450;       // below this applicants stay away
        public const int ValueAtWorst = 700;        // thousandths of its value a building at condition 0 keeps
        public const int RentAtWorst = 600;         // ...and of its rent
        public const int DistrictValuePerPoint = 4; // tenths of a thousandth of value for each point of district income off the middle

        // A building nobody has looked after: its condition when first seen, from the save's seed and its address.
        public const int RunDownShare = 250;        // thousandths of the city's buildings that are run down
        public const int RunDownLeast = 300, RunDownMost = 600;
        public const int KeptLeast = 650, KeptMost = 1000;

        // ---------------------------------------------------------------- letting

        public const int Uses = 4;
        /// <summary>The gross yield a year in a district of middling income, by use, thousandths of the value.</summary>
        public static readonly int[] YieldMiddle = { 60, 68, 74, 84 };
        public const int YieldPerIncomePoint = 5;   // hundredths of a thousandth for each point of income off the middle: dear districts yield less
        public const int YieldLeast = 40, YieldMost = 90;
        public const int RentLeverShare = 50;       // thousandths off (or on) the rent of homes when the council controls (or frees) rents

        public const int AskLeast = 800, AskMost = 1300, AskStep = 50, AskStart = 1000;   // the asking rent against the market's
        public const int ApplicantChance = 350;     // thousandths a day at the market's rent in a steady economy
        public static readonly int[] ApplicantCycle = { 1200, 1000, 850, 650 };
        public const int ApplicantAskSlope = 3;     // thousandths of that chance lost for each thousandth asked over the market
        public const int ApplicantChanceLeast = 100, ApplicantChanceMost = 1300;   // of the chance, thousandths
        public const int ApplicantPoorShare = 500;  // a building in poor condition gets half the applicants
        public const int ApplicantDays = 4;         // an application stands this long
        public const int OfferUnder = 80, OfferOver = 40;   // an offer lies this far under to this far over the asking rent
        public const int OfferLeast = 700, OfferMost = 1350;
        public const int RecordLeast = 20, RecordMost = 95;
        public const int RecordPremium = 2;         // thousandths more offered for each point of record under 60: a risky tenant bids higher
        public const int TermStep = 30, TermSteps = 5, TermLeast = 60;   // a lease runs 60 to 180 days
        public const int DepositDays = 8;           // days of rent held as a deposit
        public const int TenantKinds = 8;           // descriptions for each use (EstateText)

        public const int MissPerPoint = 12;         // tenths of a thousandth a day of a missed rent for each point of record under 100
        public static readonly int[] MissCycle = { 800, 1000, 1300, 1700 };
        public const int CatchUpBase = 150, CatchUpPerPoint = 4;   // thousandths a day that arrears are paid in full
        public const int NoticeArrearsDays = 3;     // days in arrears before notice can be served
        /// <summary>Days of notice, by the council's rent policy: freed, as it was, controlled.</summary>
        public static readonly int[] NoticeDays = { 7, 10, 14 };
        public const int NoticePaysUp = 350;        // thousandths that a tenant under notice pays everything on a day
        public const int CourtDays = 6;             // from the application to the possession order
        public const int CourtFee = 250;            // at the start's prices
        public const int RenewChance = 700, RenewChancePoor = 300;   // at a lease's end
        public const int RenewRentMost = 1100;      // a tenant paying more than this against the market thinks again
        public const int EvictionDamageMost = 120;  // condition lost when a tenant with a poor record is put out
        public const int WearLet = 1, WearVoid = 1; // condition lost a day
        public const int WearPerRecord = 40;        // one more a day for each this many points of record under 100

        public const int RepairShare = 50;          // thousandths of the value to bring a building from 0 to new
        public const int RepairDaysLeast = 1, RepairPerDay = 200;   // a day more for each 200 of condition
        public const int BreakdownBase = 8, BreakdownPerPoint = 50; // thousandths a day: 8, and one more for each 50 of condition lost
        public const int BreakdownLoss = 60;        // condition lost when something fails
        public const int FireChance = 1;            // thousandths a day: a fire or a flood
        public const int FireLoss = 300;
        public const int CoverShare = 4;            // thousandths of the value a year for landlord's cover
        public const int CoverExcess = 100;         // thousandths of a loss the landlord bears

        // ---------------------------------------------------------------- works

        public const int LevelMost = 3;
        /// <summary>What a level adds to value, thousandths; index 0 is as bought.</summary>
        public static readonly int[] LevelValue = { 0, 50, 110, 180 };
        /// <summary>...and to rent.</summary>
        public static readonly int[] LevelRent = { 0, 60, 130, 210 };
        /// <summary>What the step up to a level costs, thousandths of the value at the start's prices; index 0 unused.</summary>
        public static readonly int[] LevelCost = { 0, 50, 60, 70 };
        public static readonly int[] LevelDays = { 0, 6, 9, 12 };
        public const int PlanningFee = 4;           // thousandths of the value
        public const int PlanningDays = 8;
        /// <summary>The chance of permission, by the council's permits policy: tightened, as it was, eased.</summary>
        public static readonly int[] PlanningChance = { 500, 700, 880 };
        /// <summary>How fast a site works, by the same policy (Civic's own figures).</summary>
        public static readonly int[] SiteSpeed = { 750, 1000, 1350 };
        public const int ConvertCost = 90;
        public const int ConvertDays = 14;

        // ---------------------------------------------------------------- selling

        public const int ListLeast = 700, ListMost = 1500;   // the asking price against the value
        /// <summary>The chance of an offer on a day at asking prices of 900, 1000, 1100, 1200 and 1300 thousandths of the value.</summary>
        public static readonly int[] SaleChance = { 500, 250, 90, 25, 0 };
        public static readonly int[] SaleCycle = { 1300, 1000, 750, 500 };
        public const int SaleOfferLeast = 900, SaleOfferMost = 1040;   // what a buyer offers against the value
        public static readonly int[] SaleOfferCycle = { 20, 0, -30, -70 };
        public const int SaleOfferDays = 3;
        public const int CompletionDays = 5;
        public static readonly int[] FallThrough = { 60, 100, 160, 250 };
        public const int AgentFee = 15;             // thousandths of the price
        public const int AgentFeeLeast = 500;
        public const int GainsShare = 500;          // thousandths of the game's own tax rate charged on a gain
        public const int TaxThreshold = 150000;     // the game's own: no income tax while a year's business income is under it (TaxHelper.MeetsIncomeTaxThreshold)

        // ---------------------------------------------------------------- the agency

        public const int ReputationStart = 300;
        public const int InstructionBase = 40;      // thousandths a day with no reputation
        public const int InstructionPerPoint = 200; // thousandths of a thousandth more for each point of reputation
        public const int InstructionSaleShare = 400;
        public const int UnitSqmLeast = 40, UnitSqmMost = 200;
        public const int SaleHours = 30, SaleSqmPerHour = 10;   // an agent's hours a sale takes: 30 and one for each 10 square metres
        public const int LetHours = 12, LetSqmPerHour = 20;
        public const int OwnerAskLeast = 950, OwnerAskMost = 1200;   // what an owner wants against the value
        public const int DealAtLeast = 800, DealAtMost = 300;   // the chance of a deal at the lowest and the highest asking price
        public const int DealPerReputation = 20;    // one thousandth more for each 20 points of reputation
        public const int DealLeast = 50, DealMost = 950;
        public const int MarketingSale = 5, MarketingLet = 2;   // ten-thousandths of the value
        public const int MarketingLeast = 50;
        public const int CommissionSale = 10;       // thousandths of the price
        public const int CommissionLet = 80;        // thousandths of a year's rent
        public const int AgencyYield = 60;          // thousandths: the rent a let instruction is reckoned at
        public const int SaleCompletes = 6, LetCompletes = 2;
        public const int InstructionAnswerDays = 3;
        public const int InstructionLapseDays = 20;
        public const int RepCompleted = 25, RepFellThrough = -5, RepNoDeal = -10, RepLapsed = -30;

        // ---------------------------------------------------------------- a hotel

        public const int HotelTiers = 4, RoomTypes = 2;
        /// <summary>Rooms of each of a tier's two types: hostel, mid-range, luxury, cottages.</summary>
        public static readonly int[][] Rooms = { new[] { 24, 12 }, new[] { 40, 14 }, new[] { 60, 12 }, new[] { 6, 3 } };
        public const int Occupancy = 620;           // thousandths of the rooms sold on an ordinary night at the going rate
        /// <summary>By season: spring, summer, autumn, winter.</summary>
        public static readonly int[] SeasonCity = { 1000, 1150, 1000, 880 };
        public static readonly int[] SeasonCottage = { 1000, 1400, 1000, 700 };
        public const int Weekend = 1180;            // Friday and Saturday nights
        /// <summary>By what is on in the city (the event calendar's own order: none, market, farmers' market, parade, protest, fun run, film shoot, fireworks).</summary>
        public static readonly int[] EventNight = { 1000, 1060, 1030, 1200, 950, 1150, 1080, 1300 };
        public static readonly int[] HotelCycle = { 1080, 1000, 930, 820 };
        public const int DistrictNightPerPoint = 4; // an income point off the middle is worth a quarter of a thousandth
        public const int RateLeast = 600, RateMost = 1600, RateStep = 50;   // the rate against the going rate
        public const int RateSlope = 16;            // tenths: each thousandth over the going rate loses 1.6 thousandths of the guests
        public const int RateDemandLeast = 200, RateDemandMost = 1500;
        public const int RatingStart = 600;
        public const int RatingDemandLeast = 750, RatingDemandSpan = 450;   // a rating of 0 fills 75% of what one of 1000 would
        public const int CampaignDemand = 1120;
        public const int CampaignDays = 7;
        public const int CampaignPerRoom = 12;      // dollars a room for the week
        public const int NightNoise = 60;           // a night's own luck, either way, thousandths
        public const int StaffLevels = 3;
        public static readonly int[] ReceptionWage = { 12, 15, 19 };      // an hour, round the clock
        public static readonly int[] HousekeepingPerRoom = { 9, 12, 16 }; // a room sold
        public static readonly int[] MaintenancePerRoom = { 1, 2, 3 };    // every room, every day
        /// <summary>What a tier's staff costs against a mid-range hotel's, thousandths: a hostel has one clerk and bunks, a grand hotel a full house of staff.</summary>
        public static readonly int[] StaffTier = { 550, 1000, 1600, 700 };
        public static readonly int[] CleanTarget = { 450, 720, 930 };
        public static readonly int[] StaffScore = { 400, 700, 950 };
        public const int FullHouseShare = 900;      // above this share sold, housekeeping falls behind
        public const int FullHouseClean = 60;
        public const int LaundryPerRoom = 4;
        public static readonly int[] BreakfastPerRoom = { 0, 12, 28, 16 };
        public const int UtilitiesPerRoom = 5, UtilitiesPerSold = 2;
        public const int WearPerSold = 2;
        public const int RatingClean = 400, RatingStaff = 350, RatingPrice = 250;   // what a rating is made of, thousandths
        public const int PriceScoreAtGoing = 700, PriceScoreSlope = 15;   // tenths
        public const int RatingPull = 60;           // thousandths of the gap closed a night
        public const int HotelMultiple = 4500;      // thousandths: a hotel sells for this many years of its ordinary takings
        /// <summary>A year's lease by tier, thousandths of the price: most of what the place makes at the going rate.</summary>
        public static readonly int[] LeaseShare = { 70, 120, 150, 120 };
        public const int LeaseDepositDays = 20;
        public const int HotelSellShare = 950;      // what a sale back brings, of the going price
        public const int PipeChance = 12;           // thousandths a night
        public const int PipePerRoom = 40;
        public const int PipeRoomsShare = 100;      // thousandths of the rooms out of use
        public const int PipeNights = 2;
        public const int GroupChance = 60;
        public const int GroupLeast = 150, GroupMost = 350;   // thousandths of the larger type's rooms
        public const int GroupNightsLeast = 2, GroupNightsMost = 4;
        public const int GroupDiscount = 250;
        public const int GroupAnswerDays = 2;
        public const int InspectionEvery = 30;
        public const int InspectionPass = 600;
        public const int InspectionFinePerRoom = 25;
        public const int InspectionRating = 60;
        public const int BadReviewBelow = 500;      // a cleanliness under this brings a bad review now and then
        public const int BadReviewChance = 200;
        public const int BadReviewRating = 40;

        // ---------------------------------------------------------------- car hire

        public const int HireClasses = 4;
        /// <summary>A class ends under this list price: economy, standard, premium; anything dearer is luxury.</summary>
        public static readonly int[] ClassUnder = { 15000, 40000, 100000 };
        /// <summary>The going rate a day, ten-thousandths of the list price.</summary>
        public static readonly int[] HireRate = { 58, 54, 50, 46 };
        public const int HireRateLeast = 25;
        public static readonly int[] HireDemand = { 520, 480, 380, 300 };   // thousandths a day that an idle car goes out
        public static readonly int[] HireSeason = { 1000, 1100, 1000, 900 };
        public const int HireLuxurySummer = 1250;
        public static readonly int[] HireEvent = { 1000, 1000, 1000, 1150, 1000, 1050, 1000, 1150 };
        public static readonly int[] HireCycle = { 1100, 1000, 900, 760 };
        public const int HireSlope = 14;
        public const int HireRatingLeast = 800, HireRatingSpan = 350;
        public const int HireDemandMost = 950;
        public const int HireDaysMost = 4;
        public const int HireDepositDays = 2;
        public const int HireKmLeast = 40, HireKmMost = 160;   // a day
        public const int HireFuelLeast = 150, HireFuelMost = 700;   // thousandths of a tank left
        public const int HireDamageChance = 90;     // thousandths a hire
        public const int HireDamageLeast = 50, HireDamageMost = 300;   // thousandths of the car
        public const int HireRepairShare = 200;     // a wrecked car costs this share of its price to put right
        public const int HireLateChance = 100;
        public const int HireLateRate = 1500;       // a late day is charged at one and a half times the rate
        public const int HireStolenChance = 4, HireStolenChanceGood = 2;
        public const int HireGoodRating = 800;
        public const int HireCleaning = 18;
        public const int HireCoverShare = 25;       // thousandths of the list price a year
        public const int HireCoverPays = 800;
        public const int HireCoverExcess = 100;     // dollars
        public const int HireResidual = 700;        // what a car is reckoned worth against its list price
        public const int HireRatingStart = 600;
        public const int HireRatingGood = 6, HireRatingPoor = -30;
        public const int HireSpacesAnyBuilding = 2; // a building with no vehicle spaces of its own has the kerb in front of it
        public const int HireLotMetres = 60;        // a car is on the lot within this distance of its door

        // ---------------------------------------------------------------- the shop this is compared with

        /// <summary>The money at three points in a game: early, mid, late.</summary>
        public static readonly int[] ShopCapital = { 25000, 150000, 1000000 };
        /// <summary>What a shop set up with that money is taken to return a year, hundredths of a per cent.</summary>
        public static readonly int[] ShopYearlyBp = { 14000, 12000, 9000 };

        // ---------------------------------------------------------------- small arithmetic

        public static int Clamp(int value, int least, int most)
        {
            return value < least ? least : value > most ? most : value;
        }

        /// <summary>A share of an amount in thousandths, rounded to the nearest dollar, inside an int.</summary>
        public static int Share(long amount, long thousandths)
        {
            long share = (amount * thousandths + (amount >= 0 ? 500 : -500)) / 1000;
            return (int)Math.Max(int.MinValue / 4, Math.Min(int.MaxValue / 4, share));
        }

        public static int Cycle(CycleState state)
        {
            return Clamp((int)state, 0, 3);
        }

        /// <summary>The council's policy as a place in a table of three: -1, 0 or +1 becomes 0, 1 or 2.</summary>
        public static int Lever(int position)
        {
            return position < 0 ? 0 : position > 0 ? 2 : 1;
        }
    }
}
