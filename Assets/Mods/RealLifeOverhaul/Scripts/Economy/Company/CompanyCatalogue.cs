using System;

namespace RealLifeOverhaul.Economy
{
    /// <summary>The parts of running a business, each with its own switch, tab and rules. Saved as a number: never renumber.</summary>
    public enum CompanyPart
    {
        Staff = 0,
        Cover = 1,
        Marketing = 2,
        Tax = 3,
        Licences = 4,
        Count = 5
    }

    /// <summary>What a policy covers. Saved as a number.</summary>
    public enum CoverKind
    {
        Fire = 0,
        Theft = 1,
        Liability = 2
    }

    /// <summary>A campaign. Saved as a number.</summary>
    public enum CampaignKind
    {
        Flyers = 0,
        Radio = 1,
        DiscountDay = 2,
        Refit = 3,
        Sponsored = 4,         // a post of the player's Flare account, priced by Flare (CompanyBridges.Outside)
        InfluencerPost = 5,    // the city's influencers, hired from Flare's Business tab and priced by it: one post
        InfluencerSeries = 6,  // a week of posts
        Ambassador = 7,        // four weeks, two posts a week
        InfluencerPlan = 8,    // two or three influencers for a week
        Launch = 9,            // a business in its first fortnight
        Giveaway = 10          // costs stock, as a discount day does, and brings a crowd
    }

    /// <summary>A licence or permit. Saved as a number.</summary>
    public enum LicenceKind
    {
        Hygiene = 0,
        Alcohol = 1,
        Late = 2,
        Signage = 3
    }

    /// <summary>A business cost of the mod's own that the return counts and the game's tax deducts. Saved as a number.</summary>
    public enum CostKind
    {
        Wages = 0,      // agreed raises, sick pay, overtime, a strike's settlement
        Premiums = 1,
        Marketing = 2,
        Licences = 3,
        Accountant = 4,
        Count = 5
    }

    /// <summary>
    /// Every number of running a business, as whole numbers so a seed gives the same history: money in whole units, a
    /// wage in hundredths an hour, a share in thousandths unless named per cent, the price index in tenths of a point.
    /// Four values are by strictness unless said otherwise. Game-free: EconomyCheck compiles it.
    /// </summary>
    public static class CompanyCatalogue
    {
        public const int Relaxed = 0, Standard = 1, Realistic = 2, Hardcore = 3;
        public const int LevelDefault = Standard;

        public static int Level(int level)
        {
            return level < Relaxed ? Relaxed : level > Hardcore ? Hardcore : level;
        }

        public const int ShopsMost = 64;
        public const int StaffMost = 400;
        public const int ClaimsMost = 24;
        public const int ReportsKept = 8;
        public const int AlertsMost = 40;
        public const int BooksDays = 45;            // the most daily statements one day's pass reads back
        public const int IndexStart = 1000;         // tenths of a point
        public const int IncomeMiddle = 100;        // points of district income

        public static readonly int[] WageCycle = { 40, 0, -20, -50 };       // thousandths: boom, steady, slowdown, recession
        public const int WageIncomePerPoint = 2;                            // thousandths a point of district income from 100
        public const int WageMarketLeast = 800, WageMarketMost = 1300;      // thousandths of the game's wage formula, before prices

        public static readonly int[] RaiseGap = { 150, 100, 80, 60 };       // thousandths under the going rate that is underpaid
        public static readonly int[] RaiseDays = { 10, 7, 6, 5 };           // days underpaid before asking
        public const int RaiseAnswerDays = 4;                               // days; no answer is a refusal
        public const int RaiseRestDays = 14;                                // days before the same employee asks again
        public const int RaiseLessShare = 50;                               // per cent of the gap "offer less" closes

        public const int StandingStart = 60;         // standing: the mod's own figure, 0 to StandingMost
        public const int EsteemMost = 10;           // points the city's view of the player moves a newcomer's standing at most
        public const int EsteemPoachStep = 25;      // thousandths a point of esteem moves a rival's approach the other way
        public const int StandingCalm = 65;         // paid fairly, standing climbs back to this
        public const int StandingMost = 100;
        public const int StandAgree = 10;
        public const int StandLess = -8;
        public const int StandRefuse = -18;
        public const int StandUnderDay = -1;        // each day underpaid
        public const int StandFairDay = 1;          // each day paid fairly, up to StandingCalm
        public const int StandMatch = 5;            // a rival's offer matched
        public static readonly int[] SickShare = { 0, 500, 1000 };          // thousandths of a day's pay to somebody off sick: none, statutory, generous
        public static readonly int[] SickStanding = { -3, 0, 2 };           // standing a sick day gives, by policy
        public const int SickPolicyDefault = 1;
        public const int SickDayHours = 8;          // hours, when the game has no weekly hours for somebody

        public static readonly int[] PoachChance = { 0, 15, 25, 40 };       // thousandths a day that a rival approaches somebody who fits
        public const int PoachSkill = 50;           // the game's skill value from which a rival wants somebody
        public const int PoachGap = 80;             // thousandths under the going rate
        public const int PoachPremium = 50;         // thousandths over the going rate offered
        public const int PoachAnswerDays = 3;
        public const int PoachNoticeDays = 2;       // days worked after the offer is turned down or left unanswered
        public const int PoachRestDays = 21;        // days before the same employee
        public const int PoachCompanyRestDays = 7;  // days before anybody

        public static readonly int[] StrikeDays = { 0, 2, 3, 4 };           // its length in days
        public const int StrikeStanding = 30;       // at or under this an employee stands low
        public const int StrikeShare = 600;         // thousandths of a business's staff who stand low
        public const int StrikeStaffLeast = 2;
        public const int StrikeFootfall = -25;      // per cent of customers: the most business pressure lets an outside change take
        public const int StrikeClosedHours = 8;     // hours shut each strike morning
        public const int StrikeHour = 9;            // the hour the doors shut
        public const int StrikeRestDays = 21;
        public const int StrikePayHours = 16;       // the one-off payment: hours' pay each
        public const int StrikePayStanding = 25;
        public const int StrikeRaiseStanding = 60;  // after a raise for all nobody stands lower
        public const int StrikeAfterStanding = 40;  // after sitting it out

        public const int OvertimeChance = 300;      // thousandths a day that staff offer it while the business is busy
        public const int OvertimeAnswerDays = 2;
        public const int OvertimeDays = 3;
        public const int OvertimeHours = 2;         // extra hours a day each
        public const int OvertimeRate = 1500;       // thousandths of pay: time and a half
        public const int OvertimeFootfall = 4;      // per cent of customers
        public const int OvertimeRestDays = 10;
        public const int OvertimeStanding = 2;

        public const int Insurers = 3;
        public const int CoverKinds = 3;
        public static readonly int[] InsurerPrice = { 850, 1000, 1250 };        // thousandths of the list premium
        public static readonly int[] InsurerExcessShare = { 300, 150, 50 };     // thousandths of the loss the holder bears
        public static readonly int[] InsurerExcessLeast = { 200, 100, 50 };
        public static readonly int[] InsurerPayDays = { 6, 4, 2 };              // days from a claim to its payment
        public static readonly int[] InsurerClaimsMost = { 2, 3, 4 };           // claims in a term at which the insurer will not renew
        public static readonly int[] CoverBase = { 12, 10, 8 };                 // a day, by kind: fire, theft, liability
        public static readonly int[] CoverTakingsShare = { 4, 5, 3 };           // thousandths of an average day's takings, added
        public static readonly int[] CoverMost = { 20000, 8000, 5000 };         // the most one claim is counted at
        public static readonly int[] CoverIncomePerPoint = { 2, -3, 2 };        // thousandths a point of district income over 100: theft is dearer where it is low
        public const int CoverDistrictLeast = 850, CoverDistrictMost = 1150;    // thousandths
        public const int ClaimLoading = 250;        // thousandths on the next term's premium for each claim in this one
        public const int ClaimWindowDays = 5;       // days to put a claim in
        public const int PremiumMissedMost = 3;     // premiums missed running before the policy lapses
        public const int TermDaysLeast = 20;

        /// <summary>A policy's term: a year of the game, twenty days at least.</summary>
        public static int TermDays(int daysPerYear)
        {
            return Math.Max(TermDaysLeast, daysPerYear);
        }

        // Marketing, by CampaignKind. A sponsored post costs radio's price for two of its six days before Flare's own
        // figure; the influencers' six are bases Flare's fee is a share of.
        public const int CampaignKinds = 11;
        public const int CampaignOwnKinds = 4;      // the company's own; the rest are offered and priced from outside
        public static readonly int[] CampaignCost = { 150, 1200, 0, 2500, 400, 250, 1250, 1500, 1500, 750, 500 };
        public static readonly int[] CampaignDays = { 3, 6, 1, 20, 2, 2, 7, 28, 7, 3, 2 };
        public static readonly int[] CampaignFootfall = { 5, 8, 12, 3, 8, 8, 8, 4, 8, 10, 12 };   // per cent of customers, before the district, economy, repeats and reach
        public static readonly bool[] CampaignLocal = { true, false, true, true, false, false, false, false, false, false, false };   // stronger where the district suits
        public const int CampaignMost = 12;         // per cent of customers
        public const int DiscountShare = 150;       // thousandths of the day's takings given away on a discount day
        public const int SuitPerPoint = 5;          // thousandths a point of district income on the business's side
        public const int SuitLeast = 800, SuitMost = 1200;                      // thousandths
        public static readonly int[] CampaignCycle = { 1100, 1000, 850, 700 };  // thousandths, by the cycle
        public const int RepeatDays = 10;           // the same kind again at a business within these days is weaker
        public const int RepeatLoss = 500;          // thousandths lost the day after the last ended, less each day to RepeatDays

        public const int QuarterMonths = 3;
        public const int ReturnDays = 5;            // days to file
        public const int PenaltyShare = 5;          // thousandths of the quarter's takings
        public static int PenaltyLeast => Prices.At(200);   // at today's prices, as the takings it is a share of
        public static int PenaltyMost => Prices.At(5000);
        public const int PenaltyRepeatDays = 5;     // unfiled, the penalty comes again after each of these days
        public const int PenaltiesMost = 3;         // for one return
        public const int LateForAudit = 2;          // late returns running that bring an audit

        public const int Accountants = 3;
        public static readonly int[] AccountantFee = { 300, 700, 1500 };        // a month
        public static readonly int[] AccountantSaving = { 20, 40, 70 };         // thousandths of the quarter's deductible costs, as a rebate
        public static readonly int[] AccountantRisk = { 1000, 800, 600 };       // thousandths of the audit risk with them
        public const int AccountantErrorChance = 200;                           // thousandths that the cheapest gets a return wrong
        public const int AccountantErrorShare = 500;                            // thousandths of a late penalty, for an error
        public const int AccountantHidingFee = 1500;                            // thousandths: the cheapest's fee with cash off the books

        // Tax: cash kept off the books.
        public static readonly int[] HideSteps = { 0, 5, 10, 20, 30 };          // per cent of takings
        public const int HideMostRelaxed = 10;      // per cent
        public const int RiskPerPercent = 2;        // ten-thousandths a day for each per cent hidden, over the businesses
        public const int RiskDecay = 40;            // ten-thousandths a day while nothing is hidden
        public const int RiskMost = 2500;           // ten-thousandths: the most chance of an audit a day
        public static readonly int[] FineShare = { 250, 500, 750, 1000 };       // thousandths of the back-tax
        public static int CourtSum => Prices.At(5000);           // back-tax from which it goes to court, never on Relaxed

        public const int LicenceKinds = 4;          // the arrays that follow are by LicenceKind
        public static readonly int[] LicenceFee = { 400, 900, 300, 150 };
        public static readonly int[] LicenceTerm = { 30, 30, 20, 40 };          // days
        public const int LicenceRemindDays = 3;
        public const int LicenceGraceDays = 5;      // days a new business may trade before it needs them
        public const int LicenceFineMultiple = 2;   // the fine for trading without one, in fees
        public const int LicenceClosedHours = 6;    // hours shut as well, for food hygiene and alcohol
        public const int LicenceCheckChance = 100;  // thousandths a day that an inspection is asked for where one is missing
        public const int LateHour = 22;             // open at or past this hour, or before EarlyHour, needs the late permit
        public const int EarlyHour = 5;

        public const int LossRunDays = 3;           // days running at a loss before the Planner says so

        public static int Clamp(int value, int least, int most)
        {
            return value < least ? least : value > most ? most : value;
        }

        /// <summary>An amount at today's prices, rounded to the nearest ten. The index is in tenths of a point.</summary>
        public static int AtPrices(int amount, int index)
        {
            long priced = (long)amount * Clamp(index, 500, 4000);
            return (int)((priced + 5000) / 10000 * 10);
        }

        /// <summary>a x b / 1000, rounded, without running over.</summary>
        public static int Share(long amount, int thousandths)
        {
            long value = amount * thousandths;
            return (int)Math.Max(int.MinValue / 2, Math.Min(int.MaxValue / 2, (value + (value >= 0 ? 500 : -500)) / 1000));
        }
    }
}
