using System;

namespace RealLifeOverhaul.Insurance
{
    /// <summary>The four covers, in the order they are saved and shown: never reorder, add at the end.</summary>
    public enum CoverLine
    {
        Car = 0,        // a policy for each car the player owns: the Garage's policy, with a tier chosen here
        Health = 1,     // the player's health plan (Health's own rules; the app only shows and asks for it)
        Home = 2,       // a policy for each home of the player's
        Business = 3,   // fire, theft and liability at each business (the Company's own rules; the app only shows and asks for it)
        Count = 4
    }

    /// <summary>What a claim was for. Saved as a number: never reorder, add at the end.</summary>
    public enum ClaimKind
    {
        Repair = 0,       // a car's own repair bill
        Liability = 1,    // the damages to another driver's car after stopping at the scene
        Tow = 2,          // a tow after a crash or a breakdown
        HomeRepair = 3,   // a repair at home
        Belongings = 4,   // a repair to the player's own things away from home
        Count = 5
    }

    /// <summary>
    /// Every number of insurance, each with its unit and why. Money is whole money at the start's prices (<see cref="InsuranceRules.At"/> turns it into today's); a share is
    /// thousandths. A table by tier has three entries, from the least cover to the most. Game-free: EconomyCheck compiles it.
    /// </summary>
    public static class InsuranceCatalogue
    {
        public const int Tiers = 3;
        public const int Rivals = 3;               // the rival tycoons (RivalCatalogue.TycoonCount)
        public const int PremiumDays = 28;         // the days a month's premium is written for: a month of another length pays in proportion (CarRules.PremiumDays)
        public const int GraceDays = 3;            // mornings a missed premium is asked for again, the cover holding, before the policy lapses
        public const int ClaimsKept = 40;          // claims the history keeps, the oldest going first
        public const int StandardTier = 1;         // the tier of a policy bought before the feature, and of one bought at the Garage: the Garage's own terms

        // ---- car cover: the Garage's policy by tier (third party, standard, comprehensive)
        /// <summary>The tier's share of the Garage's premium. The standard tier is the Garage's policy as it has always been.</summary>
        public static readonly int[] CarPremiumShare = { 550, 1000, 1350 };
        /// <summary>Of a repair bill over the excess. Third party pays nothing for the holder's own car; standard is the Garage's 80% (CarRules.Cover).</summary>
        public static readonly int[] CarRepairShare = { 0, 800, 950 };
        /// <summary>Of each repair the holder pays first. Standard is the Garage's 100 (CarRules.Excess).</summary>
        public static readonly int[] CarRepairExcess = { 0, 100, 50 };
        /// <summary>Of the damages to another driver's car over the excess: every tier pays them, which is what third party cover is.</summary>
        public static readonly int[] CarLiabilityShare = { 1000, 1000, 1000 };
        /// <summary>Of each sum of damages the holder pays first (the least damages are 150: CrashConsequences.LiabilityLeast).</summary>
        public static readonly int[] CarLiabilityExcess = { 150, 100, 50 };
        /// <summary>Of a tow's fee: none, half, all of it.</summary>
        public static readonly int[] CarTowShare = { 0, 500, 1000 };
        /// <summary>The most a car's policy pays in a year of the calendar (the largest damages are 3,000 a crash).</summary>
        public static readonly int[] CarYearCap = { 6000, 12000, 30000 };
        /// <summary>The claims a car's policy pays in a year of the calendar.</summary>
        public static readonly int[] CarYearClaims = { 3, 4, 6 };
        public const int CarWaitDays = 2;          // a new policy pays nothing in its first days (a renewal has none)
        public const int SoundBelow = 30;          // thousandths of damage under which a car counts as sound: its tier can be moved up only then

        // ---- car cover: the driver's risk, each a share of the premium in thousandths
        /// <summary>By credit grade, very poor (0) to excellent (5): good (3) is the list price.</summary>
        public static readonly int[] CreditShare = { 1250, 1150, 1050, 1000, 950, 900 };
        public const int CreditUnknown = 3;        // the grade read while the credit side is off: the list price
        public const int PointShare = 30;          // thousandths added for each point on the licence
        public const int PointsMost = 12;          // points counted at most (the licence is suspended at 12: LicenceLaw.PointsLimit)
        public const int CrashShare = 100;         // thousandths added for each crash with damages to another car that still counts
        public const int CrashesMost = 5;          // crashes counted at most
        public const int CrashDays = 56;           // days a crash counts for (the Garage forgets a claim after as many: CarRules.ClaimForgottenAfter)
        public const int NoClaimStep = 50;         // thousandths off for each month renewed without a claim
        public const int NoClaimMost = 300;        // the most the no-claims discount reaches: six months
        public const int FactorLeast = 400, FactorMost = 3000;   // thousandths: the bounds of everything above multiplied together

        // ---- home cover (basic, standard, complete)
        /// <summary>A month's premium of a home, before its floor area, district and record.</summary>
        public static readonly int[] HomeBase = { 12, 22, 38 };
        /// <summary>Added for each square metre of the home, in thousandths of whole money: a larger home has more to mend.</summary>
        public static readonly int[] HomePerSqm = { 50, 80, 120 };
        public const int HomeSqmMost = 600;        // square metres counted at most
        /// <summary>Of a repair at home over the excess (a plumber, a locksmith, an appliance: 25 to 220 at the start's prices).</summary>
        public static readonly int[] HomeRepairShare = { 700, 850, 1000 };
        /// <summary>Of each repair the holder pays first.</summary>
        public static readonly int[] HomeExcess = { 40, 20, 0 };
        /// <summary>Of a repair to the player's own things away from home (a phone's screen, a laptop): only the complete tier covers belongings.</summary>
        public static readonly int[] BelongingsShare = { 0, 0, 800 };
        /// <summary>The most a home's policy pays in a year of the calendar.</summary>
        public static readonly int[] HomeYearCap = { 1500, 4000, 10000 };
        /// <summary>The claims a home's policy pays in a year of the calendar.</summary>
        public static readonly int[] HomeYearClaims = { 4, 6, 8 };
        public const int HomeWaitDays = 3;         // a new policy pays nothing in its first days (Health's plans wait as long: HealthCatalogue.WaitDays)
        public const int HomeIncomeMiddle = 100;   // points of district income at which the district changes nothing (MacroCatalogue.IncomeMiddle)
        public const int HomeIncomePerPoint = -3;  // thousandths a point of district income over the middle: dearer where incomes are low, as the Company's theft cover
        public const int HomeDistrictLeast = 850, HomeDistrictMost = 1150;   // thousandths (CompanyCatalogue.CoverDistrictLeast, CoverDistrictMost)
        public const int HomeClaimLoading = 150;   // thousandths on the premium for each claim that still counts
        public const int HomeClaimDays = 56;       // days a claim counts for
        public const int HomeLoadingMost = 600;    // thousandths: the most the claims add

        // ---- the rival tycoons' health cover
        public const int RivalLapseAfter = 2;      // premiums missed running at which a tycoon's plan lapses (HealthCatalogue.LapseAfter)
        public const int RivalStandardFrom = 2;    // times their floor a fortune is worth at which they hold the standard plan
        public const int RivalFullFrom = 4;        // ...and the full plan
        public const int RivalAge = 50;            // the age a tycoon's premium is priced at

        public static readonly string[] CarTierKeys = { "reallife_ins_car_tier_0", "reallife_ins_car_tier_1", "reallife_ins_car_tier_2" };
        public static readonly string[] HomeTierKeys = { "reallife_ins_home_tier_0", "reallife_ins_home_tier_1", "reallife_ins_home_tier_2" };
        public static readonly string[] ClaimKeys =
        {
            "reallife_ins_claim_repair", "reallife_ins_claim_liability", "reallife_ins_claim_tow", "reallife_ins_claim_home", "reallife_ins_claim_belongings"
        };

        public static int Clamp(int value, int least, int most) => value < least ? least : value > most ? most : value;

        public static long Clamp(long value, long least, long most) => value < least ? least : value > most ? most : value;

        /// <summary>A tier inside the tables.</summary>
        public static int Tier(int tier) => Clamp(tier, 0, Tiers - 1);

        /// <summary>A share of whole money in thousandths, rounded to the nearest and never over the sum for a share of a thousand or less.</summary>
        public static long Share(long amount, int thousandths) => (Math.Max(0L, amount) * Math.Max(0, thousandths) + 500) / 1000;
    }
}
