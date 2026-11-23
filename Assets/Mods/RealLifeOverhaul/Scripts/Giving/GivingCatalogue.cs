using System;

namespace RealLifeOverhaul.Giving
{
    /// <summary>The city's causes, in the order they are saved: never reorder, add at the end.</summary>
    public enum Charity
    {
        FoodBank = 0,   // households that owe more than a month's income: their debt paid down
        Housing = 1,    // tenants whose rent takes over the line the population moves a household at: the part over paid
        Training = 2,   // people out of work: a course, so a higher skill and so a better paid job
        Shelter = 3,    // the homeless: tenants the rent is about to drive out of the city keep their homes, at a rent they can bear
        Scholars = 4,   // schools: the debts of households with a pupil or a student, and a student's skill
        Care = 5,       // health: part of the care bills the city's households owe, before they reach their savings and debt
        Upkeep = 6,     // a district's upkeep: standing in that district and more custom there for a while, at once (no pot)
        Count = 7
    }

    /// <summary>
    /// Every number of giving, each with its unit and where it comes from. Money is whole money at the start's prices (<see cref="GivingRules.At"/> turns it into
    /// today's); a share is thousandths. Game-free: EconomyCheck compiles it.
    /// </summary>
    public static class GivingCatalogue
    {
        // ---- the charities
        /// <summary>The steps a gift is given in, at the start's prices (Civic's donation step is 500, a household's month is some 5,000).</summary>
        public static readonly int[] Steps =
        {
            50, 100, 250, 500, 1000, 2500, 5000, 10000, 25000, 50000, 100000,
            250000, 500000, 1000000, 2500000, 5000000, 10000000, 25000000, 50000000, 100000000, 250000000, 500000000
        };

        public const int Potted = (int)Charity.Upkeep;   // the causes that hold a pot and pay it out (the rivals give to these): all but a district's upkeep, which is spent at once

        public const int StepsBase = 11;           // the steps every player is shown (50 to 100,000); a richer player is shown more, up to the cash that can be given
        public const int AmountMost = int.MaxValue / 2;   // whole money: a gift or a price is held under this, so no sum overflows (the game's cash is a float, far under it)

        /// <summary>A charity's pay-out cap a month, in whole money for each household of the city. Giving USA 2023: human services take about 12% of what
        /// Americans give, and Americans give some 2% of an income of some 6,000 a month: about 14 a household a month, and a charity of its own cause a share of that.</summary>
        public static readonly int[] CapPerHousehold = { 14, 10, 4, 6, 5, 8, 0 };   // the last, a district's upkeep, holds no pot and pays out nothing a month

        public const int HelpMost = 1500;          // whole money a household or person is helped by in one month by one charity, the course for a person included
        public const int TrainingCourse = 800;     // whole money a course costs
        public const int TrainingSkill = 3;        // skill points (0 to 100) a course adds: the population's pay rises by 0.2 of the field's pay over a hundred points (SkillBoostMost)
        public const int HelpedMost = 48;          // households or people one charity helps in a month at most (a pass keeps the worst off this many)
        public const int ContentLift = 2;          // points (0 to 100) a household's content rises by when it is helped
        public const int StandingMost = (int)Charity.Count;   // standing gifts at once: one a cause
        public const int BigGift = 25000;          // a gift the paper may print, whole money at the start's prices
        public const int FrontPageGift = 100000;   // ... and one that leads the front page
        public const int PaperKept = 6;            // days a gift stays a story for the paper

        // ---- the newer causes
        public const int ShelterMonths = 6;        // months of the rent gap the shelter pays when it holds a household's rent at what it can bear (the regulated tenancy the population already has)
        public const int ShelterMost = 6000;       // whole money one household's holding costs the shelter at most, the months fewer when the gap is wide (a year of a fair rent, to a family)
        public const int ShelterContent = 6;       // points (0 to 100) a household's content rises by when it keeps its home: three times a charity's lift, for it is the worse fear
        public const int ScholarMost = 2500;       // whole money the school fund puts against one household's debt (a term's fees and books)
        public const int ScholarSkill = 2;         // skill points a student gains from a grant, so the pay they leave university at is a little better (LabourRules.PayFor)
        public const int CareShare = 700;          // thousandths of a household's own share of a care bill (what its plan leaves) the care fund pays before it reaches savings and debt
        public const int UpkeepPointsMost = 6;     // points of neighbourhood standing in the district a gift buys at most (the city's own sponsored event gives 4, FameCity)
        public const int UpkeepHalf = 10000;       // the gift to a district's upkeep that earns half of the most, at the start's prices
        public static readonly int[] UpkeepFrom = { 1000, 10000, 100000 };   // gifts, at the start's prices, from which the district's shops see more custom for longer
        public static readonly int[] UpkeepDays = { 5, 10, 20 };             // ... for this many days (the city's sponsored event runs 5)
        public const int UpkeepBoost = 3;          // per cent more custom in the district while it lasts: the city's own sponsored event gives 3, and Standing holds all boosts to 4

        // ---- naming
        public const int NamingFrom = 250000;      // a gift to one cause from which a hall, a wing or a scholarship takes the giver's name (a few years of a rich household's giving)
        public const int NamingPress = 20;         // tenths of a point of recognition a naming earns once, on top of the month's curve: the city sees the name every day
        public const int NamingIntegrity = 2;      // points of integrity a naming earns once (Standing holds a part to 8 a day)
        public const int NamingsKept = 12;         // namings the page keeps, the oldest dropped
        public const int CarryYears = 5;           // years an excess of the deduction carries forward (the US allows five, IRS Publication 526)

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

        // ---- helping a person (what money can buy and what it cannot)
        public const int CourseCost = 1200;        // whole money a course for a person costs, at the start's prices (a term at a city college)
        public const int CourseSkill = 5;          // skill points a course gives: more than the fund's, for this is the whole course and not a share of it
        public const int BusinessSkill = 4;        // skill points backing a small business gives: they learn by running it
        public const int BusinessPayShare = 700;   // thousandths of what their skill fetches that somebody out of work earns in a business of their own at first
        public const int BusinessLeast = 1000;     // the least a business is backed with, at the start's prices (below it is no business)
        public const int BusinessParts = 12;       // months a business loan is paid back over (a business needs longer than a rent does)
        public const int BusinessInterest = 40;    // thousandths of the sum a friend adds to a business loan, once (a bank's small business rate is about 7% a year, a friend asks less)
        public const int CarsMost = 2;             // cars a household holds at most (the population's own most, GenerateRules)
        public const int CarContent = 6;           // points a household's content rises by with a car of its own
        public const int HomeContent = 10;         // points it rises by with a home of its own (no rent, and nobody to be put out by)
        public const int HomePriceYears = 20;      // years of a flat's market rent a home costs: the price to rent ratio of New York is about 20 to 25
        public const int RentOptionMonths = 3;     // months of rent the Pay the rent answer starts at; the other answers are one and six
        // affection and trust together (0 to 200) a person wants before taking: money, rent, a debt, a business, a course, a car, a home (a friend is 50, a close friend 115)
        public static readonly int[] HelpLeast = { 0, 70, 70, 90, 60, 115, 130 };
        public const int ProudShare = 200;         // thousandths of people who are proud and refuse the first offer of money (seeded by the person)
        public const int ProudAbove = 500;         // a proud person refuses a first offer over this, at the start's prices: a small kindness they accept
        public const int ProudGapDays = 10;        // days after a refusal before the same offer is taken: their pride eases
        public const int EnviousTrustShare = 500;  // thousandths of the trust gain an envious person feels: money from the rich is not warmly taken
        public const int DependDecayDays = 30;     // days that take one point of dependence away: somebody helped long ago has found their feet
        public const int DependMost = 5;           // points of dependence at most: how often they have been helped lately
        public const int HelpCooldownDays = 21;    // days after help in which nobody asks again
        public const int MoreFrom = 2;             // points of dependence from which somebody asks for more without being in real need
        public const int AskPerDependence = 300;   // thousandths more likely to ask for each point of dependence
        public const int MoreShare = 800;          // thousandths of their last help that they ask for when they ask for more
        public const int FreeNos = 1;              // nos that cost nothing: the first time the player says no, the person understands
        public const int NoCost = 10;              // tenths of a point of affection every no after the first costs

        // ---- rivals
        public const int RivalYearShare = 5;       // thousandths of a tycoon's worth given a year on average (Giving USA: individuals' giving is about 2% of income; a fortune is mostly not income)
        public const int RivalSpread = 600;        // thousandths each tycoon's own rate may be off the average, up or down (seeded)

        public static readonly string[] NameKeys =
        {
            "reallife_giving_charity_0_name", "reallife_giving_charity_1_name", "reallife_giving_charity_2_name", "reallife_giving_charity_3_name", "reallife_giving_charity_4_name",
            "reallife_giving_charity_5_name", "reallife_giving_charity_6_name"
        };

        public static readonly string[] AboutKeys =
        {
            "reallife_giving_charity_0_about", "reallife_giving_charity_1_about", "reallife_giving_charity_2_about", "reallife_giving_charity_3_about", "reallife_giving_charity_4_about",
            "reallife_giving_charity_5_about", "reallife_giving_charity_6_about"
        };

        /// <summary>What a cause is called when a gift to it takes the giver's name: a kitchen, a wing, a scholarship. {name} is the giver's, {district} a district's.</summary>
        public static readonly string[] NamingKeys =
        {
            "reallife_giving_naming_0", "reallife_giving_naming_1", "reallife_giving_naming_2", "reallife_giving_naming_3", "reallife_giving_naming_4", "reallife_giving_naming_5",
            "reallife_giving_naming_6"
        };

        public static int Clamp(int value, int least, int most) => value < least ? least : value > most ? most : value;

        public static long Clamp(long value, long least, long most) => value < least ? least : value > most ? most : value;
    }
}
