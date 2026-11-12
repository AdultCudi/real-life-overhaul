using System;

namespace RealLifeOverhaul.Giving
{
    /// <summary>The city's charities, in the order they are saved: never reorder, add at the end.</summary>
    public enum Charity
    {
        FoodBank = 0,   // households that owe more than a month's income: their debt paid down
        Housing = 1,    // tenants whose rent takes over the line the population moves a household at: the part over paid
        Training = 2,   // people out of work: a course, so a higher skill and so a better paid job
        Count = 3
    }

    /// <summary>
    /// Every number of giving, each with its unit and where it comes from. Money is whole money at the start's prices (<see cref="GivingRules.At"/> turns it into
    /// today's); a share is thousandths. Game-free: EconomyCheck compiles it.
    /// </summary>
    public static class GivingCatalogue
    {
        // ---- the charities
        /// <summary>The steps a gift is given in, at the start's prices (Civic's donation step is 500, a household's month is some 5,000).</summary>
        public static readonly int[] Steps = { 50, 100, 250, 500, 1000, 2500, 5000, 10000, 25000, 50000, 100000 };

        /// <summary>A charity's pay-out cap a month, in whole money for each household of the city. Giving USA 2023: human services take about 12% of what
        /// Americans give, and Americans give some 2% of an income of some 6,000 a month: about 14 a household a month, and a charity of its own cause a share of that.</summary>
        public static readonly int[] CapPerHousehold = { 14, 10, 4 };

        public const int HelpMost = 1500;          // whole money a household or person is helped by in one month by one charity, the course for a person included
        public const int TrainingCourse = 800;     // whole money a course costs
        public const int TrainingSkill = 3;        // skill points (0 to 100) a course adds: the population's pay rises by 0.2 of the field's pay over a hundred points (SkillBoostMost)
        public const int HelpedMost = 48;          // households or people one charity helps in a month at most (a pass keeps the worst off this many)
        public const int ContentLift = 2;          // points (0 to 100) a household's content rises by when it is helped
        public const int StandingMost = 3;         // standing gifts at once: one a charity
        public const int BigGift = 25000;          // a gift the paper may print, whole money at the start's prices
        public const int FrontPageGift = 100000;   // ... and one that leads the front page
        public const int PaperKept = 6;            // days a gift stays a story for the paper

        // ---- the tax return
        /// <summary>The share of a year's income a deduction for gifts may reach, in thousandths. The US allows cash gifts to public charities up to 60% of adjusted gross
        /// income (IRS Publication 526); the game taxes sales less costs and a business keeps about a tenth of its sales, so 60% of a tenth of the sales is 6%.</summary>
        public const int DeductionShare = 60;

        // ---- standing and the press (per month, by what was given in it, at the start's prices; the curve is most x gift / (gift + half))
        public const int IntegrityMost = 3;        // points of Standing's integrity a month at most (Fame's DayMost is 8 a day)
        public const int IntegrityHalf = 5000;     // the gift that earns half of it
        public const int PressMost = 8;            // tenths of a point of recognition a month at most (Fame's PressMost is 300)
        public const int PressHalf = 20000;

        // ---- people
        public const int AffectionMost = 6;        // points of affection a gift earns in one window at most
        public const int TrustMost = 4;            // ... of trust
        public const int GiftHalf = 1000;          // the gifts in a window that earn half of it, at the start's prices
        public const int WindowDays = 30;          // a person's gifts are counted over this many days from the first
        public const int NoNeedShare = 500;        // thousandths of the gain when the person was in no need of it
        public const int LoanShare = 500;          // thousandths of a gift's warmth that a loan of the same size earns: it comes back, so it counts half
        public const int GiftsMost = 10000;        // a gift to a person at most, at the start's prices
        public const int LoanMost = 10000;         // a loan at most
        public const int LoansMost = 8;            // loans open at once
        public const int LoanParts = 6;            // months a loan is paid back over
        public const int LoanMisses = 3;           // months in a row nothing could be paid before the loan is written off
        public const int LoanKept = 12;            // closed loans the page keeps
        public const int AskGapDays = 14;          // days between any two asks
        public const int AskPersonGapDays = 60;    // ... and between two of one person's
        public const int AskChance = 1;            // thousandths an hour that a person in trouble who may ask does
        public const int AskBoostMost = 1000;      // thousandths more a rich or famous player is asked at most
        public const int AskLeast = 150;           // least of a person's affection and trust to ask
        public const int AskMost = 3000;           // whole money asked at most, at the start's prices
        public const int LessShare = 500;          // thousandths of the ask the player may give instead ("less")
        public const int AskExpireHours = 36;      // hours an ask waits for an answer
        public const int NeighbourShare = 20;      // thousandths of its savings a household that has plenty gives a neighbour in a month
        public const int NeighbourRich = 12;       // months of its income a household keeps in savings before it has plenty
        public const int NeighboursPer = 400;      // one gift between households a month for this many households

        // ---- rivals
        public const int RivalYearShare = 5;       // thousandths of a tycoon's worth given a year on average (Giving USA: individuals' giving is about 2% of income; a fortune is mostly not income)
        public const int RivalSpread = 600;        // thousandths each tycoon's own rate may be off the average, up or down (seeded)

        public static readonly string[] NameKeys =
        {
            "reallife_giving_charity_0_name", "reallife_giving_charity_1_name", "reallife_giving_charity_2_name"
        };

        public static readonly string[] AboutKeys =
        {
            "reallife_giving_charity_0_about", "reallife_giving_charity_1_about", "reallife_giving_charity_2_about"
        };

        public static int Clamp(int value, int least, int most) => value < least ? least : value > most ? most : value;

        public static long Clamp(long value, long least, long most) => value < least ? least : value > most ? most : value;
    }
}
