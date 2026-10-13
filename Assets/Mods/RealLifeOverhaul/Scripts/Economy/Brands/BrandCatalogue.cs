using System;

namespace RealLifeOverhaul.Economy.Brands
{
    /// <summary>How a franchisee goes about the trade. Saved as a number: never renumber.</summary>
    public enum Temper
    {
        Careful = 0,          // keeps the standards up, grows slowly, seldom disputes
        Ambitious = 1,        // takes more, asks for a second shop, may bid for the brand
        CornerCutting = 2     // cheaper to run, standards that slip, disputes when warned
    }

    /// <summary>How a franchised shop stands in the city. Saved as a number.</summary>
    public enum Hold
    {
        Opened = 0,           // a business of the game's own, started for it in a building that was to let (CompetitionHelper.StartNewCompetitorBusiness)
        Converted = 1,        // a business of the game's own that was trading there already and joined the brand
        Books = 2             // in the mod's books only: the building stands as it was
    }

    /// <summary>What a franchisee is waiting to hear from the player. Saved as a number.</summary>
    public enum Ask
    {
        None = 0,
        Help = 1,             // short of cash: asks for a grant
        SecondShop = 2,       // asks to open another shop
        Renewal = 3,          // the agreement is about to run out
        Dispute = 4,          // disputes the terms
        BuyBrand = 5          // bids for the brand
    }

    /// <summary>Something at a shop that the city hears about. Saved nowhere: an event.</summary>
    public enum Scandal
    {
        Inspection = 0,       // a failed health or fire safety inspection
        Strike = 1,
        Fire = 2
    }

    /// <summary>Why terms put to an applicant are not taken.</summary>
    public enum Refusal
    {
        None = 0,
        TooDear = 1,          // the terms ask more than this applicant will bear for a brand this well known
        NoCapital = 2,        // the entry fee and the fit-out are more than they have
        Territory = 3,        // another franchisee holds the district
        Full = 4              // the brand has as many franchised shops as it can keep
    }

    /// <summary>What a day did to a franchised shop, for the director to act on.</summary>
    public enum DayEvent
    {
        None = 0,
        AsksHelp = 1,
        AsksSecond = 2,
        AsksRenewal = 3,
        Disputes = 4,
        Bids = 5,
        Failed = 6,
        Lapsed = 7
    }

    /// <summary>
    /// Every number of brands and franchises. The rules, the director, the Brands app, the Guide's page and the
    /// offline check all read these; nothing else writes a figure of its own. Game-free: compiled as it is by
    /// RealLifeOverhaul.Build/EconomyCheck.
    ///
    /// Whole numbers throughout, so the same seed and the same days always give the same history and a save holds
    /// exactly what was in memory: money in whole units, recognition in hundredths of a point (0 to 10000), standards
    /// in points (0 to 100) except a franchisee's own, which is kept in hundredths so that a slow drift is not lost,
    /// a share in thousandths unless its name says Bp (ten-thousandths) or per cent, the price index in tenths of a
    /// point (1000 at the start), a district's income in points.
    /// </summary>
    public static class BrandCatalogue
    {
        // ---------------------------------------------------------------- what is kept

        public const int BrandsMost = 6;
        public const int MembersMost = 48;
        public const int FranchisesMost = 60;
        public const int ApplicantsMost = 4;        // waiting at one time, over all brands
        public const int DistrictsMost = 16;        // a brand's districts with a recognition figure
        public const int TakenMost = 8;             // the player's shops under a city chain
        public const int AlertsMost = 24;
        public const int NameMost = 24;             // characters in a brand's name

        // ---------------------------------------------------------------- recognition (hundredths of a point a day unless said otherwise)

        public const int RecMost = 10000;
        public const int PresencePerShop = 12;      // for each shop of the brand in the district...
        public const int PresenceShopsMost = 4;     // ...up to this many
        public const int FootfallGainMost = 16;     // when the district's shops serve FootfallFull customers a day between them
        public const int FootfallFull = 400;        // customers a day
        public const int StandardsGainMost = 14;    // at standards of 100; nothing at 50; the same taken away at 0
        public const int CampaignLocalGain = 150;   // a brand campaign in one district, each day it runs
        public const int CampaignCityGain = 60;     // a city-wide one, in every district where the brand trades
        public const int DecayBp = 50;              // ten-thousandths of the figure lost a day where the brand trades (0.5%)
        public const int DecayEmptyBp = 150;        // ...and where it has no shop left (1.5%)
        public const int CityDistricts = 8;         // the city-wide figure heads for the districts' figures added up and divided by this
        public const int CityStepBp = 500;          // ten-thousandths of the way there each day (5%)
        public const int CityFloorShare = 500;      // thousandths of the city-wide figure a district is never counted under

        public static readonly int[] ScandalLoss = { 400, 300, 200 };   // a failed inspection, a strike, a fire: in the district; a quarter of it city-wide
        public const int ScandalCityShare = 250;    // thousandths
        public const int ScandalScaleLeast = 800, ScandalScaleMost = 1250;   // thousandths of a scandal's loss: how the talk of it spreads, at most either way
        public const int FailureLoss = 800;         // a franchisee that closes
        public const int UnfairLoss = 600;          // an agreement ended without cause
        public const int UnfairCityLoss = 150;

        // ---------------------------------------------------------------- standards (points)

        public const int WeightSatisfaction = 40, WeightClean = 25, WeightStock = 20, WeightSkill = 15;   // per cent of a shop's figure
        public const int BrandMeanShare = 600;      // thousandths of the brand's figure that is the mean of its shops...
        public const int BrandWorstShare = 400;     // ...and that is its worst shop: one bad shop drags the brand
        public static readonly int[] MinimumSteps = { 0, 50, 60, 70, 80 };   // the required minimum the player may set

        // ---------------------------------------------------------------- the effect on a member shop (per cent of customers)

        public const int RecFootfallMost = 12;      // at recognition of 100 in the district
        public const int LowStandards = 40;         // under this the brand's name is worth half
        public const int ToleranceMost = 4;         // of the customers prices cost, given back at standards of 100; nothing at 50 or under
        public const int CannibalRadius = 150;      // metres: a franchised shop in the books this near takes customers
        public const int CannibalEach = 4;          // at no distance at all, falling to nothing at the radius
        public const int CannibalMost = 8;          // from all of them together
        public const int FootfallLeast = -8, FootfallMost = 15;   // what a brand may ask for at one shop, everything together

        // ---------------------------------------------------------------- offering a franchise

        public const int OfferCityRec = 2500;       // city-wide recognition, or...
        public const int OfferDistrictRec = 4000;   // ...this much in one district
        public const int OfferShops = 3;            // member shops...
        public const int OfferStandards = 60;       // ...with standards of this or more

        // ---------------------------------------------------------------- applicants

        public const int ArriveBase = 60;           // per mille a day for a brand on offer...
        public const int ArrivePerPoint = 2;        // ...and this for each point of city-wide recognition
        public const int ApplicantDays = 10;        // an applicant waits this long
        public const int FitOut = 20000;            // what opening a shop costs a franchisee beside the entry fee, at a price index of 100
        public static readonly int[] CapitalLeast = { 50000, 60000, 35000 };   // by temperament
        public static readonly int[] CapitalMost = { 110000, 160000, 80000 };
        public const int ExperienceMost = 12;       // years
        public static readonly int[] AbilityBase = { 55, 50, 40 };   // by temperament, before experience
        public const int AbilityPerYear = 4;

        public static readonly string[] NameFirst = { "Harbour", "Copper", "Juniper", "Lantern", "Maple", "Quarry", "Orchard", "Saffron", "Pebble", "Kestrel", "Willow", "Ember" };
        public static readonly string[] NameSecond = { "Lane", "Gate", "Yard", "Row", "Field", "Wharf", "Court", "Hill", "Cross", "Green" };
        public static readonly string[] NameLast = { "Trading", "Retail", "Ventures", "Shopkeepers", "Stores" };

        // ---------------------------------------------------------------- terms (a small set of choices)

        public const int Tiers = 3;
        public static readonly int[] EntryFee = { 8000, 15000, 25000 };   // at a price index of 100
        public static readonly int[] RoyaltyBp = { 400, 600, 800 };       // of takings
        public static readonly int[] LevyBp = { 0, 100, 200 };            // of takings, for marketing
        public static readonly int[] LengthYears = { 1, 2, 3 };
        public const int RenewalShare = 250;        // thousandths of the entry fee, paid again at a renewal
        public const int SecondShopShare = 500;     // thousandths of the entry fee for a franchisee's second shop
        public const int BearBase = 3;              // what any applicant bears (see FranchiseRules.Burden)...
        public const int BearPerRec = 2500;         // ...one more for each this much recognition where they want to trade
        public static readonly int[] BearTemper = { 0, 1, 1 };
        public const int ExclusiveRelief = 2;       // an exclusive district is worth this much of the burden to an applicant

        // ---------------------------------------------------------------- a franchised shop's day

        public const int AbilityLeast = 600, AbilityPerPoint = 5;         // thousandths of the takings: 0.60 at no ability, 1.10 at 100
        public const int RecLeast = 850, RecPerPoint = 3;                 // 0.85 unknown, 1.15 at recognition of 100
        public const int IncomeBase = 700, IncomePerPoint = 3;            // 1.00 at a district income of 100
        public static readonly int[] CycleTakings = { 1050, 1000, 950, 880 };   // boom, steady, slowdown, recession
        public const int StandardsBase = 900, StandardsPerPoint = 2;      // 1.00 at standards of 50, 1.10 at 100
        public const int CrowdEach = 60, CrowdMost = 300;                 // thousandths lost for each other shop of the brand in the district
        public const int NoiseMost = 80;                                  // thousandths either way, from the day and the shop
        public const int MarginBp = 2200;           // of takings left to a franchisee after goods and wages
        public const int FixedBp = 1200;            // of an ordinary day's takings that the rent and the rest cost every day
        public const int CutterSaving = 200;        // a corner-cutter's fixed costs are this much lower, in ten-thousandths
        public const int SupportBase = 40;          // what supporting one franchised shop costs the brand a day, at a price index of 100...
        public const int SupportBp = 100;           // ...and this of its takings
        public const int TroubleDays = 2;           // cash under this many ordinary days' takings: in trouble
        public const int FailDays = 3;              // this many ordinary days' takings in debt: it closes
        public const int HelpDays = 5;              // a grant is this many ordinary days' takings
        public const int WaiveDays = 14;            // or the royalty is waived for this long

        public static readonly int[] StandardsTarget = { 82, 70, 52 };   // where a franchisee's standards head, by temperament
        public const int AbilityPull = 5;           // a point of target for each this many points of ability over or under 50
        public const int WarnedLift = 10, RetrainedLift = 8, TroubleDrop = 8;
        public const int DriftBp = 1000;            // ten-thousandths of the gap closed a day
        public const int DriftNoise = 200;          // hundredths either way a day

        // ---------------------------------------------------------------- keeping a franchisee to the standards

        public const int InspectorFee = 250;        // at a price index of 100
        public const int InspectDueDays = 30;       // the Planner asks for an inspection after this long without one
        public const int FreshDays = 10;            // an inspection counts as evidence for this long
        public const int WarnDays = 20;             // a warning lifts the standards for this long
        public const int RetrainFee = 1500;         // at a price index of 100
        public const int RetrainLift = 1500;        // hundredths, at once
        public const int RetrainDays = 30;
        public const int CauseWarnings = 2;         // warnings before an agreement may be ended with cause
        public const int CompensationBp = 500;      // of an ordinary day's takings, for each day left...
        public const int CompensationDaysMost = 60; // ...up to this many days
        public const int CompensationLeastShare = 500;   // thousandths of the entry fee: never less

        // ---------------------------------------------------------------- what a franchisee asks

        public const int AskDays = 10;              // days an ask waits for an answer
        public const int RenewalNotice = 14;        // days before the end that a renewal is asked for
        public const int SecondAfterDays = 30;      // days trading before a second shop is thought of
        public const int SecondCashDays = 12;       // ordinary days' takings in hand
        public const int SecondChance = 100;        // per mille a day once both hold
        public const int DisputeChance = 15;        // per mille a day while there is a grievance
        public const int DisputeDays = 30;          // the claim is this many days of royalty and levy
        public const int FundIdleMost = 5000;       // levy collected and not spent on campaigns beyond this is a grievance, at a price index of 100
        public const int BidAfterDays = 90;
        public const int BidCashDays = 16;
        public const int BidChance = 10;            // per mille a day
        public const int BidDays = 120;             // a bid is this many days of what the franchises bring in...
        public const int BidPerPoint = 400;         // ...and this for each point of city-wide recognition, at a price index of 100
        public const int AskRestDays = 30;          // after an ask is answered, no other of its kind for this long

        // ---------------------------------------------------------------- city chains the player may join (three, in every type of business)

        public const int Chains = 3;
        public static readonly int[] ChainFee = { 4000, 9000, 16000 };    // at a price index of 100
        public static readonly int[] ChainRoyaltyBp = { 400, 600, 800 };
        public static readonly int[] ChainRec = { 4000, 5500, 7000 };     // its recognition, the same everywhere, from the first day
        public static readonly int[] ChainStandards = { 50, 60, 70 };     // the standards to keep
        public static readonly int[] ChainPriceLeast = { 850, 950, 1000 };   // prices against the game's own market price, thousandths...
        public static readonly int[] ChainPriceMost = { 1000, 1100, 1200 };
        public const int ChainWarnDays = 3;         // days in breach before the chain writes...
        public const int ChainFineDays = 7;         // ...fines...
        public const int ChainEndDays = 14;         // ...and ends the agreement
        public const int ChainFineDaysOfRoyalty = 5;
        public const int ChainFineLeast = 200;
        public const int ChainExitDays = 20;        // leaving costs this many days of royalty...
        public const int ChainExitLeastShare = 500; // ...and never less than this of the fee, in thousandths

        // ---------------------------------------------------------------- brand campaigns (priced by the company's own campaign list)

        public const int PushLocal = 0, PushCity = 1;
        public const int PushCityTimes = 2;         // a city-wide brand campaign costs this many radio spots

        // ---------------------------------------------------------------- helpers

        public static int Clamp(int value, int least, int most)
        {
            return value < least ? least : value > most ? most : value;
        }

        /// <summary>a x b / 10000, rounded half up, without running over.</summary>
        public static int Bp(long amount, int bp)
        {
            long value = amount * bp;
            return (int)Math.Max(int.MinValue / 2, Math.Min(int.MaxValue / 2, (value + (value >= 0 ? 5000 : -5000)) / 10000));
        }

        /// <summary>A number from 0 to 999 that is always the same for the same three numbers: the mod's dice, so that a save replays the same way.</summary>
        public static int Dice(int seed, int a, int b)
        {
            unchecked
            {
                uint h = (uint)seed * 2654435761u;
                h ^= (uint)a * 2246822519u;
                h = (h << 13) | (h >> 19);
                h ^= (uint)b * 3266489917u;
                h *= 668265263u;
                h ^= h >> 15;
                h *= 2246822519u;
                h ^= h >> 13;
                return (int)(h % 1000u);
            }
        }
    }
}
