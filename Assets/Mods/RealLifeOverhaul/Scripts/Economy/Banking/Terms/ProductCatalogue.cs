using System;

namespace RealLifeOverhaul.Economy
{
    /// <summary>The kinds of account the mod keeps itself. Saved as a number: add at the end, never renumber.</summary>
    public enum ProductKind
    {
        Overdraft = 0,      // covers a short day, paid back from spare cash
        CreditCard = 1,     // cash drawn against a limit, a statement every few days with a minimum payment
        CarFinance = 2,     // a vehicle bought on finance: a daily instalment, the vehicle as security
        Mortgage = 3,       // a building bought on finance: a daily instalment, the building as security
        Arrears = 4,        // late fees on one of the game's loans that could not be taken on their day
        Count = 5
    }

    /// <summary>What a lender does as due payments go on being missed, mildest first. Saved as a number.</summary>
    public enum LadderStep
    {
        None = 0,
        Reminder = 1,
        LateFee = 2,
        PenaltyRate = 3,    // a dearer rate until payments are made again
        Collections = 4,    // the lender rings: pay, agree a plan, or neither
        Repossession = 5,   // secured accounts only
        Claim = 6           // a civil claim through the courts
    }

    /// <summary>Why a product is not offered or an amount not lent.</summary>
    public enum ProductAnswer
    {
        Yes = 0,
        Off = 1,            // "More kinds of credit" is switched off, or the economy is not running
        BelowThreshold = 2,
        Bankrupt = 3,       // nobody lends for a while after a bankruptcy
        Arrangement = 4,    // a debt arrangement is running
        NoBank = 5,
        AlreadyOpen = 6,
        TooLittle = 7,
        TooMuch = 8,        // over the limit, or over what is free of it
        NoSecurity = 9,     // no business with takings on record to lend against
        NoCash = 10,        // the deposit or the payment is not in hand
        NothingOwed = 11,
        Busy = 12           // something of the same kind is already under way
    }

    /// <summary>
    /// Every number of the mod's own credit: the products' limits, rates and fees, what follows a missed payment, the private
    /// lender and the ways out of debt. Game-free: compiled as it is by EconomyCheck.
    /// </summary>
    public static class ProductCatalogue
    {
        // how hard not paying hits: the option's four levels
        public const int Relaxed = 0, Standard = 1, Realistic = 2, Hardcore = 3;
        public const int LevelDefault = Standard;

        /// <summary>The missed payments in a row at which each step is taken, in the order of <see cref="LadderStep"/> from Reminder.</summary>
        public static readonly int[] StepCounts = { 1, 2, 3, 5, 8, 12 };
        public static readonly int[] StepCountsHardcore = { 1, 2, 3, 4, 6, 9 };

        /// <summary>The last step of each level: Relaxed stops at the call, Standard at repossession, the others at the claim.</summary>
        public static readonly LadderStep[] LastStep = { LadderStep.Collections, LadderStep.Repossession, LadderStep.Claim, LadderStep.Claim };

        public const float LateFeeShare = 0.02f;             // of the instalment missed, never less than the least; halved on Relaxed
        public static int LateFeeLeast => Prices.At(15);
        public const int PenaltyPoints = 5;                  // a year on an account's rate from the third miss...
        public const int PenaltyClearRun = 3;                // ...until this many payments in a row are made
        public const int PlanDays = 10;                      // a plan spreads what is overdue over these days, on top of the normal payment
        public const float RepossessionValueShare = 0.60f;   // what a repossessed vehicle fetches, of the price it was bought for
        public const float BusinessDivertShare = 0.25f;      // of a business's daily takings, to the lender of a business loan in default
        public const float RentDivertShare = 1f;             // of a mortgaged building's rents, likewise
        public const int UnsecuredClaimDays = 10;            // a balance left unsecured by a repossession goes to court after these days
        public const float ClaimCostsShare = 0.10f;          // the lender's costs added to a claim, never less than the least
        public static int ClaimCostsLeast => Prices.At(50);

        // the overdraft's limit: a share of what the city's banks would lend at the rating, at the threshold and from the best terms up
        public const float OverdraftShareAtThreshold = 0.02f;
        public const float OverdraftShareAtBest = 0.08f;
        public const int OverdraftRound = 50;
        public static int OverdraftLeast => Prices.At(200);
        public const int OverdraftIncomeDays = 30;           // days of average income: a short-term cash buffer
        public const int OverdraftRatePoints = 8;            // on the bank's rate for the rating
        public static int OverdraftFloat => Prices.At(500);  // cash above this pays the overdraft back at each day's end

        public const float CardShareAtThreshold = 0.03f;
        public const float CardShareAtBest = 0.12f;
        public const int CardRound = 50;
        public static int CardLeast => Prices.At(300);
        public const int CardIncomeDays = 60;                // days of average income: two months of earnings
        public const int CardRatePoints = 12;
        public const float CardCashFeeShare = 0.03f;         // of cash drawn, never less than the least
        public static int CardCashFeeLeast => Prices.At(5);
        public const int CardDrawLeast = 20;
        public const int CardStatementDays = 7;              // a statement every so many days...
        public const int CardGraceDays = 3;                  // ...its minimum taken this many days after it
        public const float CardMinimumShare = 0.05f;         // of the statement, never less than the least, or the whole of a smaller balance
        public static int CardMinimumLeast => Prices.At(25);

        // the business loan: one of the game's loans with a business as security
        public const int BusinessDaysOnRecord = 7;           // days of takings needed on record, and the days its profit is averaged over
        public const int BusinessProfitMultiple = 30;        // the most it lends, in days of that average profit
        public const int BusinessRatePoints = -1;

        public const int ApprovalDays = 1;                   // game days an approval for finance stands: buy by the end of the next day
        public const int CarDepositLeast = 10, CarDepositMost = 50, CarDepositStep = 10;      // per cent of the price
        public const int CarTermLeast = 60, CarTermMost = 240, CarTermStep = 30;              // game days
        public const int CarRatePoints = 2;
        public const int CarLeast = 500;
        public const int HomeDepositLeast = 20, HomeDepositMost = 50, HomeDepositStep = 10;   // per cent of the price
        public const int HomeTermLeast = 360, HomeTermMost = 1440, HomeTermStep = 180;        // game days
        public const int HomeRatePoints = -1;
        public const int HomeLeast = 5000;
        public const int HomeCapMultiple = 5;                // a mortgage lends this many times what the bank would lend unsecured

        public const float ConsolidateFeeShare = 0.01f;
        public static int ConsolidateFeeLeast => Prices.At(25);

        // the private lender
        public static int SharkLeast => Prices.At(500);
        public static int SharkMost => Prices.At(5000);
        public const int SharkIncomeDays = 10;               // with an income the most is this many days of it, if that is more
        public const int SharkStep = 500;
        public const float SharkDailyShare = 0.03f;          // simple interest a day on the sum borrowed
        public const int SharkDueDays = 10;
        public const float SharkExtendFeeShare = 0.15f;      // of what is owed, for the one extension by SharkDueDays
        public const int SharkCapMultiple = 3;               // what is owed never passes this many times the sum borrowed
        public const int SharkWarnDay = 1, SharkAssaultDay = 3, SharkDamageDay = 6, SharkRepeatDays = 3;   // days past the due day
        public const int SharkInjuryHours = 12;              // a beating's injury, as the mod's muggings give it...
        public const float SharkInjuryEnergy = 8f;           // ...and the energy lost
        public const float SharkDamageShare = 0.10f;         // a damaged business's repair bill, of the debt, between the least and the most
        public static int SharkDamageLeast => Prices.At(150);
        public static int SharkDamageMost => Prices.At(1500);

        // the ways out of debt
        public const int AdviceOverdueDays = 10;             // debt advice is offered once something has been overdue these days running
        public const float ArrangementShare = 0.20f;         // an arrangement's daily payment, of the average daily income, at least the least
        public static int ArrangementLeast => Prices.At(25);
        public const int ArrangementScoreCap = 560;          // the rating is held at no more than this while an arrangement runs...
        public const int ArrangementCapDaysAfter = 20;       // ...and for these days after
        public const int ArrangementMissesMost = 3;          // missed payments that end an arrangement
        public static int BankruptKeepCash => Prices.At(500);   // bankruptcy leaves this much cash; the rest goes to the creditors
        public const int BankruptScore = CreditCatalogue.ScoreMin;
        public const int BankruptNoCreditDays = 60;          // nobody lends for these days, whatever the score...
        public const int BankruptRebuildFrom = 450;          // ...after which the rating rebuilds from here...
        public const int BankruptRebuildPerDay = 2;          // ...by no more than these points a day
        public const int BankruptEveryDays = 180;            // one bankruptcy in this many days

        public static readonly string[] KindKeys =
        {
            "reallife_econ_kind_overdraft", "reallife_econ_kind_card", "reallife_econ_kind_car", "reallife_econ_kind_home", "reallife_econ_kind_arrears"
        };

        /// <summary>The source written on the credit file's events, by kind: short and never changed.</summary>
        public static readonly string[] KindSources = { "overdraft", "card", "car", "home", "arrears" };

        public static int[] Counts(int level)
        {
            return level >= Hardcore ? StepCountsHardcore : StepCounts;
        }

        public static int Level(int level)
        {
            return level < Relaxed ? Relaxed : level > Hardcore ? Hardcore : level;
        }

        public static bool Revolving(ProductKind kind)
        {
            return kind == ProductKind.Overdraft || kind == ProductKind.CreditCard;
        }

        public static bool Instalment(ProductKind kind)
        {
            return kind == ProductKind.CarFinance || kind == ProductKind.Mortgage;
        }

        /// <summary>A value rounded to the nearest step.</summary>
        public static int Round(double value, int step)
        {
            return (int)Math.Round(value / step, MidpointRounding.AwayFromZero) * step;
        }

        /// <summary>A share of an amount rounded up to a whole unit of money; the small step back absorbs float noise.</summary>
        public static int ShareUp(double amount, double share)
        {
            return amount <= 0 ? 0 : (int)Math.Ceiling(amount * share - 1e-4);
        }
    }
}
