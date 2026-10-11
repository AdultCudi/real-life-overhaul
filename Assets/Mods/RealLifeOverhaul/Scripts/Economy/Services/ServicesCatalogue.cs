using System;

namespace RealLifeOverhaul.Economy
{
    /// <summary>Who makes an offer for a business. Saved as a number.</summary>
    public enum BuyerKind { Rival = 0, Investor = 1, Staff = 2, Chain = 3, Count = 4 }

    /// <summary>
    /// Every figure of the cleaning service and of selling a business, in one place. Game-free. Nothing here runs: the
    /// rules, the page and the Guide all read their numbers from it, so they cannot disagree.
    ///
    /// Dirt is counted in hundredths of the game's own points (a floor tile runs from 0 to 100 points,
    /// BuildingCleanliness.MaxDirtiness), time in minutes of the game's clock, distance in metres, money in whole
    /// units, a share in per cent unless its name says otherwise.
    /// </summary>
    public static class ServicesCatalogue
    {
        // ---------------------------------------------------------------- the game's own cleaning figures

        public const string CleaningSkill = "ba:skill_cleaning";   // the skill the game's cleaning asks for (BuildingCleanliness.GetCleaningSkill)
        public const int ForcePerThousandSqm = 5;    // cleaners a floor needs per 1,000 m2, rounded up (BuildingCleanliness.CleanersNeededPerSquareMeter, 0.005)
        public const int DirtVisible = 500;          // hundredths: under this a tile shows no dirt and does not count against cleanliness (DirtinessNeededToShowVisualDirt, 5)
        public const int DirtMost = 10000;           // hundredths: a tile is never dirtier (MaxDirtiness, 100)
        public const int CleanlinessWarn = 80;       // under this the game asks for the floor to be cleaned (BuildingCleanlinessHelper.FloorTileCleanlinessStates[0])

        // ---------------------------------------------------------------- travel

        public const int WalkPerMinute = 350;        // hundredths of a metre a game minute on foot: 3.5 m in a second of play, the player's own pace (one game minute is one second at normal speed)
        public const int VanPerMinute = 800;         // hundredths of a metre a game minute by van: 8 m in a second of play, what traffic makes over a street with its junctions
        public const int StreetFactor = 130;         // per cent: the way along the streets against the straight line between two doors
        public const int VanCostPerKm = 4;           // money a kilometre by van at a price index of 100: fuel and wear
        public const int TravelMost = 240;           // minutes: a door further away than this is not on anybody's round

        // ---------------------------------------------------------------- a round

        public const int VisitLeast = 15;            // minutes: a shorter visit is not worth the walk
        public const int VisitMost = 180;            // minutes: the longest a cleaner stays at one place
        public const int VisitDefault = 45;          // minutes planned for a place whose dirt is not yet known
        public const int VisitsMost = 3;             // visits a day at fixed hours
        public const int ThresholdDefault = 80;      // "as needed": a visit is due when cleanliness is under this
        public static readonly int[] Thresholds = { 60, 70, 80, 90 };   // the thresholds the page offers
        public const int ModeAsNeeded = 0;           // a client's mode: 0 as needed, 1 to 3 that many visits a day
        public static readonly int[] HoursDefault = { 9, 14, 19 };      // the hours of a first, second and third visit until the player sets them
        public const int ClientsMost = 32;
        public const int CrewMost = 12;
        public const int VisitsDayMost = 96;         // visits kept of one day's round
        public const int NeedDaysAverage = 3;        // days a business's daily dirt is averaged over
        public const int OwnShiftLeastHours = 1;     // "one cleaner per shop": the shortest cleaning shift a shop would be given a day

        // ---------------------------------------------------------------- contracts with other businesses

        public const int OffersMost = 3;             // contract offers open at once
        public const int OfferEveryDays = 3;         // a new one is looked for this often
        public const int OfferOpenDays = 5;          // an offer stands this long
        public const int ContractsMost = 8;
        public const int ContractWeekDays = 7;       // the fee and the standard are settled this often
        public const int ContractDaysUnderMost = 2;  // days of a week the floor may be under the standard
        public const int ContractPenaltyPct = 25;    // of the week's fee, charged for a week that is missed (the fee itself is not paid)
        public const int ContractMissesMost = 2;     // weeks missed running that lose the contract
        public static readonly int[] ContractStandards = { 70, 80, 90 };   // the cleanliness a client asks for
        public const int ContractDirtBase = 600;     // hundredths of dirt a day at any client...
        public const int ContractDirtPerCustomer = 25;   // ...and this much more for each customer it has room for
        public const int ContractDirtMost = 3000;    // hundredths a day at the busiest
        public const int ContractWageRef = 14;       // money an hour of cleaning is priced from at a price index of 100
        public const int ContractMarginPct = 220;    // the fee against the wages of the cleaning it takes
        public const int ContractFeeLeast = 150;     // money a week
        public const int ContractSkillRef = 4000;    // hundredths an hour: the cleaner a fee is worked out for (skill 50, satisfied 80%)
        public const int ContractStartDirt = 800;    // hundredths: how a client's floor stands when the contract begins

        // ---------------------------------------------------------------- selling a business

        public static readonly int[] TypePct = { 100, 105, 110, 95, 90 };   // what a kind of business is worth against its profit: retail, food, office, leisure, industry (Ventures' sectors)
        public const int ClimateBoomPct = 112;       // in a boom...
        public const int ClimateSlowPct = 85;        // ...and in a slowdown or a recession
        public const int DistrictSwingPct = 50;      // of a district's income above or below the middle that goes into the value
        public const int DistrictLeastPct = 80;
        public const int DistrictMostPct = 125;
        public const int HistoryDays = 7;            // days of books a full value needs; fewer count in proportion
        public static readonly int[] AskingPct = { 80, 90, 100, 110, 120, 135, 150 };   // the asking prices the page offers, against the value
        public const int AskingDefault = 2;          // the index of 100%
        public static readonly int[] AskingGapDays = { 1, 1, 2, 3, 4, 6, 8 };      // days between offers at each asking price
        public static readonly int[] AskingOffersMost = { 5, 5, 4, 3, 2, 1, 1 };   // offers a listing draws at each asking price
        public const int AskingPullPct = 35;         // of the asking price's distance from the value that an offer follows
        public const int QuickPct = 70;              // a quick sale pays this much of the value, at once
        public const int ListingDays = 30;           // a listing ends by itself after this long
        public static readonly int[] BuyerPct = { 95, 90, 82, 108 };          // what each kind of buyer offers against the value: a rival owner, an investor, the staff, a chain
        public static readonly int[] BuyerCeilingPct = { 104, 100, 88, 118 }; // the most each would pay, before the part that is their own
        public const int CeilingSpreadPct = 8;       // a buyer's own part on top of the ceiling: 0 to this
        public const int JitterPct = 5;              // an offer moves this much either way
        public static readonly bool[] BuyerKeepsStaff = { false, true, true, false };
        public static readonly int[] BuyerInstalments = { 0, 0, 4, 2 };       // parts a price is paid in (0: at once); the first on the day, one a week after
        public static readonly int[] BuyerClauseDays = { 30, 0, 0, 60 };      // days the seller must stay out of that trade in the district
        public const int OfferDaysLeast = 3, OfferDaysMost = 5;               // an offer stands this long
        public const int CounterStepPct = 10;        // a counter asks this much more
        public const int CounterWalkPct = 25;        // the chance a buyer walks away from a counter they will not meet
        public const int InstalmentDays = 7;
        public const int ClausePenaltyPct = 15;      // of the price, charged once if the clause is broken
        public const int OffersKept = 12;
        public const int DuesMost = 16;
        public const int ClausesMost = 16;
        public const int RecordsKept = 24;
        public const int ListingsMost = 16;

        // ---------------------------------------------------------------- arithmetic used everywhere

        public static int Clamp(int value, int least, int most)
        {
            return value < least ? least : value > most ? most : value;
        }

        /// <summary>a x b / 100, rounded to the nearest whole, without running over.</summary>
        public static int Pct(long amount, int percent)
        {
            long value = amount * percent;
            return (int)Math.Max(int.MinValue / 2, Math.Min(int.MaxValue / 2, (value + (value >= 0 ? 50 : -50)) / 100));
        }

        /// <summary>An amount of money at today's prices. The index is in tenths of a point (1000 at the start).</summary>
        public static int AtPrices(long amount, int index)
        {
            return (int)Math.Min(int.MaxValue / 2, (amount * Clamp(index, 500, 4000) + 500) / 1000);
        }

        /// <summary>Money rounded down to the ten: a price as people name one.</summary>
        public static int Round10(long amount)
        {
            return (int)Math.Max(0, Math.Min(int.MaxValue / 2, amount / 10 * 10));
        }

        /// <summary>
        /// A number from the save's seed and what it is about, the same every time for the same question: every
        /// chance and every drawn figure of these rules comes from it, so a save always plays out the same way.
        /// </summary>
        public static uint Hash(int seed, int a, int b, int c)
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

        /// <summary>A whole number from 0 to below 'below' by <see cref="Hash"/>.</summary>
        public static int Roll(int seed, int a, int b, int c, int below)
        {
            return below <= 1 ? 0 : (int)(Hash(seed, a, b, c) % (uint)below);
        }

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
    }
}
