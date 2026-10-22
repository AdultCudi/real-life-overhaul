using System;

namespace RealLifeOverhaul.Economy
{
    /// <summary>What a let building is used as. Saved as a number: never renumber.</summary>
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
        Repair = 1,     // the condition back to new
        Refurbish = 2,  // one level up
        Planning = 3,   // the council decides on a change of use
        Convert = 4     // the change of use is built
    }

    /// <summary>Where an instruction to the agency stands. Saved as a number.</summary>
    public enum InstructionStage
    {
        Offered = 0,    // the player takes it on or declines
        Marketing = 1,  // agent's hours are being spent on it
        Completing = 2  // a deal is agreed and completes on a day, or falls through
    }

    /// <summary>Where a car on hire stands. Saved as a number.</summary>
    public enum HireState
    {
        Idle = 0,       // on the lot, to be hired
        Out = 1,        // with a customer: not in the city
        Valet = 2,      // back and being cleaned: not hired until the day turns
        Waiting = 3     // due back while its space is taken
    }

    /// <summary>
    /// Every figure of the property business. Game-free. Money is whole dollars and a share thousandths unless named
    /// otherwise; condition, rating and reputation run 0 to 1000; prices are tenths of a point; district income is 600 to
    /// 1400; tables "by cycle" go Boom, Steady, Slowdown, Recession.
    /// </summary>
    public static class EstateCatalogue
    {
        public const int HoldingsMost = 40;
        public const int SoldMost = 24;             // buildings sold remembered, the newest kept: a sold one put back is handed back to its buyer
        public const int SoldLookBack = 7;          // days of the game's books searched for a building's sale: it keeps at least six days of them
        public const int ApplicantsMost = 3;        // waiting for one unit
        public const int ApplicantsBuildingMost = 12;   // for a whole building: one more for each unit after the first
        public const int UnitsMost = 12;            // the game's count is kept to this, so a page and the save stay small
        public const int InstructionsMost = 8;
        public const int DistrictsMost = 24;
        public const int HotelsMost = 8;            // as many as the city has
        public const int HireCarsMost = 12;
        public const int ReviewsKept = 6;
        public const int AlertsMost = 24;           // rows for the Planner
        public const int Thousand = 1000;

        public const int IndexStart = 1000;         // tenths of a point
        public const int IncomeMiddle = 1000;
        public const int IncomeLeast = 600, IncomeMost = 1400;
        public const int DaysPerYearDefault = 60;   // the game's own
        public const int RealStart = 1000;          // the market's long-run level
        public const int RealLeast = 600, RealMost = 1500;
        public static readonly int[] RealTarget = { 1150, 1000, 930, 820 };   // by cycle
        public const int RealPerQuarterPoint = 6;   // off the target for each quarter point of the central bank's rate
        public const int RealPull = 120;            // thousandths of the gap to the target closed in a month
        public const int RealNoise = 10;            // a month's own move, either way
        public const int RealStepMost = 35;         // a month's most

        public const int ConditionNew = 1000;
        public const int ConditionUnfit = 250;      // below this a building cannot be let
        public const int ConditionPoor = 450;       // below this applicants stay away
        public const int ValueAtWorst = 700;        // thousandths of its value a building at condition 0 keeps
        public const int RentAtWorst = 600;         // ...and of its rent
        public const int DistrictValuePerPoint = 4; // tenths of a thousandth of value for each point of income off the middle

        // A building nobody has looked after: its condition when first seen, from the seed and its address.
        public const int RunDownShare = 250;        // thousandths of the city's buildings
        public const int RunDownLeast = 300, RunDownMost = 600;
        public const int KeptLeast = 650, KeptMost = 1000;

        public const int Uses = 4;
        public static readonly int[] YieldMiddle = { 60, 68, 74, 84 };   // gross a year by use, middling income
        public const int YieldPerIncomePoint = 5;   // hundredths of a thousandth for each point of income off the middle
        public const int YieldLeast = 40, YieldMost = 90;
        public const int RentLeverShare = 50;       // off (or on) the rent of homes when the council controls (or frees) rents
        public const int RentScaleLeast = 500, RentScaleMost = 3000;   // the option's rental income, of the market's rent

        public const int AskLeast = 800, AskMost = 1300, AskStep = 50, AskStart = 1000;   // of the market's rent
        public const int ApplicantChance = 350;     // a day, at the market's rent in a steady economy
        public static readonly int[] ApplicantCycle = { 1200, 1000, 850, 650 };
        public const int ApplicantAskSlope = 3;     // of the chance lost for each thousandth asked over the market
        public const int ApplicantChanceLeast = 100, ApplicantChanceMost = 1300;
        public const int ApplicantPoorShare = 500;  // a building in poor condition gets half the applicants
        public const int ApplicantDays = 4;
        public const int OfferUnder = 80, OfferOver = 40;   // an offer against the asking rent, thousandths of the market's
        public const int OfferLeast = 700, OfferMost = 1350;
        public const int RecordLeast = 20, RecordMost = 95;
        public const int RecordPremium = 2;         // more offered for each point of record under 60: a risky tenant bids higher
        public const int TermStep = 30, TermSteps = 5, TermLeast = 60;   // days: a lease runs 60 to 180
        public const int DepositDays = 8;           // days of rent
        public const int TenantKinds = 8;           // descriptions for each use

        public const int MissPerPoint = 12;         // tenths of a thousandth a day for each point of record under 100
        public static readonly int[] MissCycle = { 800, 1000, 1300, 1700 };
        public const int CatchUpBase = 150, CatchUpPerPoint = 4;   // a day that arrears are paid in full
        public const int NoticeArrearsDays = 3;
        public static readonly int[] NoticeDays = { 7, 10, 14 };   // by rent policy: freed, as it was, controlled
        public const int NoticePaysUp = 350;        // a day that a tenant under notice pays everything
        public const int CourtDays = 6;             // from the application to the possession order
        public const int CourtFee = 250;            // dollars at the start's prices
        public const int RenewChance = 700, RenewChancePoor = 300;   // at a lease's end
        public const int RenewRentMost = 1100;      // a tenant paying more than this of the market thinks again
        public const int EvictionDamageMost = 120;  // condition lost when a tenant with a poor record is put out
        public const int WearLet = 1, WearVoid = 1; // condition lost a day
        public const int WearPerRecord = 40;        // one more a day for each this many points of record under 100

        public const int RepairShare = 50;          // of the value, to bring a building from 0 to new
        public const int RepairDaysLeast = 1, RepairPerDay = 200;   // a day more for each 200 of condition
        public const int BreakdownBase = 8, BreakdownPerPoint = 50; // a day: 8, and one more for each 50 of condition lost
        public const int BreakdownLoss = 60;        // condition
        public const int FireChance = 1;            // a day: a fire or a flood
        public const int FireLoss = 300;            // condition
        public const int CoverShare = 4;            // of the value a year
        public const int CoverExcess = 100;         // of a loss, borne by the landlord

        public const int LevelMost = 3;
        public static readonly int[] LevelValue = { 0, 50, 110, 180 };  // added to value by level; 0 is as bought
        public static readonly int[] LevelRent = { 0, 60, 130, 210 };   // ...and to rent
        public static readonly int[] LevelCost = { 0, 50, 60, 70 };     // the step up to a level, of the value at the start's prices
        public static readonly int[] LevelDays = { 0, 6, 9, 12 };
        public const int PlanningFee = 4;           // of the value
        public const int PlanningDays = 8;
        public static readonly int[] PlanningChance = { 500, 700, 880 };   // by permits policy: tightened, as it was, eased
        public static readonly int[] SiteSpeed = { 750, 1000, 1350 };      // by the same policy (Civic's own figures)
        public const int ConvertCost = 90;          // of the value
        public const int ConvertDays = 14;

        public const int ListLeast = 700, ListMost = 1500;   // the asking price, of the value
        public static readonly int[] SaleChance = { 500, 250, 90, 25, 0 };   // an offer a day, asked 900 to 1300 of the value
        public static readonly int[] SaleCycle = { 1300, 1000, 750, 500 };
        public const int SaleOfferLeast = 900, SaleOfferMost = 1040;   // a buyer's offer, of the value
        public static readonly int[] SaleOfferCycle = { 20, 0, -30, -70 };
        public const int SaleOfferDays = 3;
        public const int CompletionDays = 5;
        public static readonly int[] FallThrough = { 60, 100, 160, 250 };
        public const int AgentFee = 15;             // of the price
        public const int AgentFeeLeast = 500;       // dollars at the start's prices
        public const int GainsShare = 500;          // of the game's tax rate, charged on a gain
        public const int TaxThreshold = 150000;     // dollars: the game's own (TaxHelper.MinimumIncomeForTaxes)

        public const int ReputationStart = 300;
        public const int InstructionBase = 40;      // a day with no reputation
        public const int InstructionPerPoint = 200; // thousandths of a thousandth more for each point of reputation
        public const int InstructionSaleShare = 400;
        public const int UnitSqmLeast = 40, UnitSqmMost = 200;
        public const int SaleHours = 30, SaleSqmPerHour = 10;   // an agent's hours: 30, and one for each 10 square metres
        public const int LetHours = 12, LetSqmPerHour = 20;
        public const int OwnerAskLeast = 950, OwnerAskMost = 1200;   // of the value
        public const int DealAtLeast = 800, DealAtMost = 300;   // the chance of a deal at the lowest and the highest ask
        public const int DealPerReputation = 20;    // points of reputation for each thousandth more
        public const int DealLeast = 50, DealMost = 950;
        public const int MarketingSale = 5, MarketingLet = 2;   // ten-thousandths of the value
        public const int MarketingLeast = 50;       // dollars
        public const int CommissionSale = 10;       // of the price
        public const int CommissionLet = 80;        // of a year's rent
        public const int AgencyYield = 60;          // the rent a year a let instruction is reckoned at, of its value
        public const int SaleCompletes = 6, LetCompletes = 2;   // days
        public const int InstructionAnswerDays = 3;
        public const int InstructionLapseDays = 20;
        public const int RepCompleted = 25, RepFellThrough = -5, RepNoDeal = -10, RepLapsed = -30;

        public const int HotelTiers = 4, RoomTypes = 2;
        public static readonly int[][] Rooms = { new[] { 24, 12 }, new[] { 40, 14 }, new[] { 60, 12 }, new[] { 6, 3 } };   // hostel, mid-range, luxury, cottages
        public const int Occupancy = 620;           // of the rooms, sold on an ordinary night at the going rate
        public static readonly int[] SeasonCity = { 1000, 1150, 1000, 880 };      // spring, summer, autumn, winter
        public static readonly int[] SeasonCottage = { 1000, 1400, 1000, 700 };
        public const int Weekend = 1180;            // Friday and Saturday nights
        public static readonly int[] EventNight = { 1000, 1060, 1030, 1200, 950, 1150, 1080, 1300 };   // the event calendar's order
        public static readonly int[] HotelCycle = { 1080, 1000, 930, 820 };
        public const int DistrictNightPerPoint = 4; // income points off the middle for each thousandth of demand
        public const int RateLeast = 600, RateMost = 1600, RateStep = 50;   // of the going rate
        public const int RateSlope = 16;            // tenths: each thousandth over the going rate loses 1.6 thousandths of the guests
        public const int RateDemandLeast = 200, RateDemandMost = 1500;
        public const int RatingStart = 600;
        public const int RatingDemandLeast = 750, RatingDemandSpan = 450;   // a rating of 0 fills 75% of what one of 1000 would
        public const int CampaignDemand = 1120;
        public const int CampaignDays = 7;
        public const int CampaignPerRoom = 12;      // dollars a room for the week
        public const int NightNoise = 60;           // a night's own luck, either way
        public const int StaffLevels = 3;
        public static readonly int[] ReceptionWage = { 12, 15, 19 };      // dollars an hour, round the clock
        public static readonly int[] HousekeepingPerRoom = { 9, 12, 16 }; // dollars a room sold
        public static readonly int[] MaintenancePerRoom = { 1, 2, 3 };    // dollars a room a day
        public static readonly int[] StaffTier = { 550, 1000, 1600, 700 };   // staff cost by tier, of a mid-range hotel's
        public static readonly int[] CleanTarget = { 450, 720, 930 };
        public static readonly int[] StaffScore = { 400, 700, 950 };
        public const int FullHouseShare = 900;      // of the rooms sold, above which housekeeping falls behind
        public const int FullHouseClean = 60;
        public const int LaundryPerRoom = 4;        // dollars a room sold
        public static readonly int[] BreakfastPerRoom = { 0, 12, 28, 16 };   // dollars a room sold, by tier
        public const int UtilitiesPerRoom = 5, UtilitiesPerSold = 2;   // dollars a room, and a room sold
        public const int WearPerSold = 2;           // dollars a room sold
        public const int RatingClean = 400, RatingStaff = 350, RatingPrice = 250;   // what a rating is made of
        public const int PriceScoreAtGoing = 700, PriceScoreSlope = 15;   // the slope in tenths
        public const int RatingPull = 60;           // of the gap closed a night
        public const int HotelMultiple = 4500;      // years of ordinary takings a hotel sells for
        public static readonly int[] LeaseShare = { 70, 120, 150, 120 };   // a year's lease by tier, of the price
        public const int LeaseDepositDays = 20;
        public const int HotelSellShare = 950;      // of the going price, for a sale back
        public const int PipeChance = 12;           // a night
        public const int PipePerRoom = 40;          // dollars
        public const int PipeRoomsShare = 100;      // of the larger type's rooms out of use
        public const int PipeNights = 2;
        public const int GroupChance = 60;          // a night
        public const int GroupLeast = 150, GroupMost = 350;   // of the larger type's rooms
        public const int GroupNightsLeast = 2, GroupNightsMost = 4;
        public const int GroupDiscount = 250;
        public const int GroupAnswerDays = 2;
        public const int InspectionEvery = 30;      // days
        public const int InspectionPass = 600;      // cleanliness
        public const int InspectionFinePerRoom = 25;   // dollars at the start's prices
        public const int InspectionRating = 60;
        public const int BadReviewBelow = 500;      // a cleanliness under this brings a bad review now and then
        public const int BadReviewChance = 200;
        public const int BadReviewRating = 40;

        public const int HireClasses = 4;
        public static readonly int[] ClassUnder = { 15000, 40000, 100000 };   // list price: economy, standard, premium; dearer is luxury
        public static readonly int[] HireRate = { 58, 54, 50, 46 };   // the going rate a day by class, ten-thousandths of the list price
        public const int HireRateLeast = 25;        // dollars a day
        public static readonly int[] HireDemand = { 520, 480, 380, 300 };   // a day that an idle car goes out, by class
        public static readonly int[] HireSeason = { 1000, 1100, 1000, 900 };
        public const int HireLuxurySummer = 1250;
        public static readonly int[] HireEvent = { 1000, 1000, 1000, 1150, 1000, 1050, 1000, 1150 };
        public static readonly int[] HireCycle = { 1100, 1000, 900, 760 };
        public const int HireSlope = 14;            // tenths, as RateSlope
        public const int HireRatingLeast = 800, HireRatingSpan = 350;
        public const int HireDemandMost = 950;
        public const int HireDaysMost = 4;
        public const int HireDepositDays = 2;       // days of the rate
        public const int HireKmLeast = 40, HireKmMost = 160;   // a day
        public const int HireFuelLeast = 150, HireFuelMost = 700;   // of a tank left
        public const int HireDamageChance = 90;     // a hire
        public const int HireDamageLeast = 50, HireDamageMost = 300;   // of the car
        public const int HireRepairShare = 200;     // of its price, to put a wrecked car right
        public const int HireLateChance = 100;
        public const int HireLateRate = 1500;       // a late day, of the rate
        public const int HireStolenChance = 4, HireStolenChanceGood = 2;
        public const int HireGoodRating = 800;
        public const int HireCleaning = 18;         // dollars at the start's prices
        public const int HireCoverShare = 25;       // of the list price a year
        public const int HireCoverPays = 800;
        public const int HireCoverExcess = 100;     // dollars
        public const int HireResidual = 700;        // what a car is reckoned worth, of its list price
        public const int HireRatingStart = 600;
        public const int HireRatingGood = 6, HireRatingPoor = -30;
        public const int HireSpacesAnyBuilding = 2; // a building with no vehicle spaces of its own has the kerb in front
        public const int HireLotMetres = 60;        // a car is on the lot within this distance of its door

        public static readonly int[] ShopCapital = { 25000, 150000, 1000000 };   // dollars, early, mid and late in a game
        public static readonly int[] ShopYearlyBp = { 14000, 12000, 9000 };      // what a shop set up with it returns a year, hundredths of a per cent

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
