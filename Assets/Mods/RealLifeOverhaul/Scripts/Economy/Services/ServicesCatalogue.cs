using System;

namespace RealLifeOverhaul.Economy
{
    /// <summary>Who makes an offer for a business. Saved as a number.</summary>
    public enum BuyerKind { Rival = 0, Investor = 1, Staff = 2, Chain = 3, Count = 4 }

    /// <summary>
    /// Every figure of the cleaning service and of selling a business, game-free. Dirt in hundredths of the game's points
    /// (a tile runs from 0 to 100), time in game minutes, money in whole units, a share in per cent.
    /// </summary>
    public static class ServicesCatalogue
    {
        public const string CleaningSkill = "ba:skill_cleaning";   // BuildingCleanliness.GetCleaningSkill
        public const int ForcePerThousandSqm = 5;    // cleaners a floor needs per 1,000 m2, rounded up (the game's 0.005 a m2)
        public const int DirtVisible = 500;          // hundredths: a tile under this shows no dirt (the game's 5)
        public const int DirtMost = 10000;           // hundredths: BuildingCleanliness.MaxDirtiness
        public const int CleanlinessWarn = 80;       // under this the game asks for the floor to be cleaned

        public const int WalkPerMinute = 350;        // hundredths of a metre a game minute: the player's own pace on foot
        public const int VanPerMinute = 800;         // hundredths of a metre a game minute: what traffic makes over a street
        public const int StreetFactor = 130;         // per cent: the way along the streets against the straight line
        public const int VanCostPerKm = 4;           // money a kilometre at a price index of 100: fuel and wear
        public const int TravelMost = 240;           // minutes: a door further away is on nobody's round

        public const int VisitLeast = 15;            // minutes: a shorter visit is not worth the walk
        public const int VisitMost = 180;            // minutes
        public const int VisitDefault = 45;          // minutes, for a place whose dirt is not yet known
        public const int VisitsMost = 3;             // fixed visits a day
        public const int ThresholdDefault = 80;      // cleanliness under which "as needed" sends a cleaner
        public static readonly int[] Thresholds = { 60, 70, 80, 90 };   // the thresholds the page offers
        public const int ModeAsNeeded = 0;           // a client's mode: 0 as needed, 1 to 3 that many visits a day
        public static readonly int[] HoursDefault = { 9, 14, 19 };      // hours of the first, second and third visit
        public const int ClientsMost = 32;
        public const int CrewMost = 12;
        public const int VisitsDayMost = 96;         // visits kept of one day's round
        public const int NeedDaysAverage = 3;        // days a business's daily dirt is averaged over
        public const int OwnShiftLeastHours = 1;     // hours: the shortest daily shift a shop's own cleaner would be given

        public const int OffersMost = 3;             // contract offers open at once
        public const int OfferEveryDays = 3;         // days between looks for a new offer
        public const int OfferOpenDays = 5;          // days an offer stands
        public const int ContractsMost = 8;
        public const int ContractWeekDays = 7;       // days between settlements of the fee and the standard
        public const int ContractDaysUnderMost = 2;  // mornings of a week the floor may be under the standard
        public const int ContractPenaltyPct = 25;    // of the week's fee, for a missed week (the fee itself is not paid)
        public const int ContractMissesMost = 2;     // missed weeks running that end the contract
        public static readonly int[] ContractStandards = { 70, 80, 90 };   // the cleanliness a client asks for
        public const int ContractDirtBase = 600;     // hundredths of dirt a day at any client...
        public const int ContractDirtPerCustomer = 25;   // ...and this much more for each customer it has room for
        public const int ContractDirtMost = 3000;    // hundredths a day
        public const int ContractWageRef = 14;       // money an hour of cleaning is priced at, at a price index of 100
        public const int ContractMarginPct = 220;    // the fee against the wages of the cleaning it takes
        public static int ContractFeeLeast => Prices.At(150);     // money a week
        public const int ContractSkillRef = 4000;    // hundredths an hour: the cleaner a fee is worked out for (skill 50, satisfied 80%)
        public const int ContractStartDirt = 800;    // hundredths: a client's floor when the contract begins

        public static readonly int[] TypePct = { 100, 105, 110, 95, 90 };   // worth against profit by Ventures' sector: retail, food, office, leisure, industry
        public const int ClimateBoomPct = 112;
        public const int ClimateSlowPct = 85;        // in a slowdown or a recession
        public const int DistrictSwingPct = 50;      // of a district's income above or below the middle that goes into the value
        public const int DistrictLeastPct = 80;
        public const int DistrictMostPct = 125;
        public const int HistoryDays = 7;            // days of books a full value needs; fewer count in proportion
        public static readonly int[] AskingPct = { 80, 90, 100, 110, 120, 135, 150 };   // the asking prices the page offers, against the value
        public const int AskingDefault = 2;          // the index of 100%
        public static readonly int[] AskingGapDays = { 1, 1, 2, 3, 4, 6, 8 };      // days between offers at each asking price
        public static readonly int[] AskingOffersMost = { 5, 5, 4, 3, 2, 1, 1 };   // offers a listing draws at each asking price
        public const int AskingPullPct = 35;         // of the asking price's distance from the value that an offer follows
        public const int QuickPct = 70;              // of the value, paid at once
        public const int ListingDays = 30;           // days a listing stands
        public static readonly int[] BuyerPct = { 95, 90, 82, 108 };          // what each BuyerKind offers against the value
        public static readonly int[] BuyerCeilingPct = { 104, 100, 88, 118 }; // the most each would pay, before their own part
        public const int CeilingSpreadPct = 8;       // a buyer's own part on top of the ceiling: 0 to this
        public const int JitterPct = 5;              // an offer moves this much either way
        public static readonly bool[] BuyerKeepsStaff = { false, true, true, false };
        public static readonly int[] BuyerInstalments = { 0, 0, 4, 2 };       // parts a price is paid in, a week apart (0: at once)
        public static readonly int[] BuyerClauseDays = { 30, 0, 0, 60 };      // days the seller stays out of that trade in the district
        public const int OfferDaysLeast = 3, OfferDaysMost = 5;               // days an offer stands
        public const int CounterStepPct = 10;        // a counter asks this much more
        public const int CounterWalkPct = 25;        // the chance a buyer walks away from a counter they will not meet
        public const int InstalmentDays = 7;
        public const int ClausePenaltyPct = 15;      // of the price, charged once if the clause is broken
        public const int OffersKept = 12;
        public const int DuesMost = 16;
        public const int ClausesMost = 16;
        public const int RecordsKept = 24;
        public const int ListingsMost = 16;

        public static int Clamp(int value, int least, int most) => value < least ? least : value > most ? most : value;

        /// <summary>amount x percent / 100, rounded to the nearest whole, without running over.</summary>
        public static int Pct(long amount, int percent)
        {
            long value = amount * percent;
            return (int)Math.Max(int.MinValue / 2, Math.Min(int.MaxValue / 2, (value + (value >= 0 ? 50 : -50)) / 100));
        }

        /// <summary>An amount of money at a price index in tenths of a point (1000 at the start).</summary>
        public static int AtPrices(long amount, int index) => (int)Math.Min(int.MaxValue / 2, (amount * Clamp(index, 500, 4000) + 500) / 1000);

        /// <summary>Money rounded down to the ten, as people name a price.</summary>
        public static int Round10(long amount) => (int)Math.Max(0, Math.Min(int.MaxValue / 2, amount / 10 * 10));

        /// <summary>A whole number from 0 to below 'below', always the same for the same seed and question.</summary>
        public static int Roll(int seed, int a, int b, int c, int below) => below <= 1 ? 0 : (int)(Hash(seed, a, b, c) % (uint)below);

        /// <summary>A text as a number that is always the same for it (a place, an employee's id).</summary>
        public static int Number(string text)
        {
            unchecked
            {
                uint h = 2166136261u;
                for (int i = 0; text != null && i < text.Length; i++)
                {
                    h = (h ^ text[i]) * 16777619u;
                }
                return (int)(h & 0x3fffffff);
            }
        }

        private static uint Hash(int seed, int a, int b, int c)
        {
            unchecked
            {
                uint h = 2166136261u;
                h = (h ^ (uint)seed) * 16777619u;
                h = (h ^ (uint)a) * 16777619u;
                h = (h ^ (uint)b) * 16777619u;
                h = (h ^ (uint)c) * 16777619u;
                h ^= h >> 15;
                h *= 2246822519u;
                h ^= h >> 13;
                return h;
            }
        }
    }
}
