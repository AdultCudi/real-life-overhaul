using System;

namespace RealLifeOverhaul.Economy
{
    /// <summary>The kinds of account the mod keeps itself. Saved as a number: add at the end, never renumber.</summary>
    public enum ProductKind
    {
        Overdraft = 0,      // an arranged overdraft: it covers a short day, and is paid back from spare cash
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
        Reminder = 1,       // a notice
        LateFee = 2,        // a fee added to what is owed
        PenaltyRate = 3,    // a dearer rate until payments are made again
        Collections = 4,    // the lender rings: pay, agree a plan, or neither
        Repossession = 5,   // the security is taken (secured accounts only)
        Claim = 6           // a civil claim through the courts
    }

    /// <summary>Why a product is not offered or an amount not lent.</summary>
    public enum ProductAnswer
    {
        Yes = 0,
        Off = 1,            // "More kinds of credit" is switched off, or the economy is not running
        BelowThreshold = 2, // the rating is under the lending threshold
        Bankrupt = 3,       // nobody lends for a while after a bankruptcy
        Arrangement = 4,    // a debt arrangement is running
        NoBank = 5,         // the city has no bank to lend
        AlreadyOpen = 6,
        TooLittle = 7,
        TooMuch = 8,        // over the limit, or over what is free of it
        NoSecurity = 9,     // nothing to lend against (no business with takings on record)
        NoCash = 10,        // the deposit or the payment is not in hand
        NothingOwed = 11,
        Busy = 12           // something of the same kind is already under way
    }

    /// <summary>
    /// Every number of the credit products, of what happens when a payment is missed, of the lender of last resort
    /// and of the ways out of debt. The accounts, the Finance app, the Guide's page and the offline check all read
    /// these; nothing else writes a figure of its own. Game-free: compiled as it is by RealLifeOverhaul.Build/EconomyCheck.
    ///
    /// The lending threshold (<see cref="CreditCatalogue.LendingThreshold"/>) applies to every product here on every
    /// preset: under it nothing is opened and nothing is drawn. What is already owed stays owed.
    /// </summary>
    public static class ProductCatalogue
    {
        // ---------------------------------------------------------------- how hard not paying hits (the option's four steps)

        public const int Relaxed = 0, Standard = 1, Realistic = 2, Hardcore = 3;
        public const int LevelDefault = Standard;

        /// <summary>The consecutive missed payments at which each step is taken, in the order of <see cref="LadderStep"/> from Reminder.</summary>
        public static readonly int[] StepCounts = { 1, 2, 3, 5, 8, 12 };
        public static readonly int[] StepCountsHardcore = { 1, 2, 3, 4, 6, 9 };

        /// <summary>The last step a level goes to: Relaxed stops at the call, Standard at repossession, the others at the claim.</summary>
        public static readonly LadderStep[] LastStep = { LadderStep.Collections, LadderStep.Repossession, LadderStep.Claim, LadderStep.Claim };

        public static int[] Counts(int level)
        {
            return level >= Hardcore ? StepCountsHardcore : StepCounts;
        }

        public static int Level(int level)
        {
            return level < Relaxed ? Relaxed : level > Hardcore ? Hardcore : level;
        }

        /// <summary>The late fee: a share of the instalment missed, never less than the least; halved on Relaxed.</summary>
        public const float LateFeeShare = 0.02f;
        public static int LateFeeLeast => Prices.At(15);

        /// <summary>Points a year put on an account's rate from the third miss, until this many payments in a row are made.</summary>
        public const int PenaltyPoints = 5;
        public const int PenaltyClearRun = 3;

        /// <summary>A payment plan spreads what is overdue over this many days, on top of the normal payment.</summary>
        public const int PlanDays = 10;

        /// <summary>What a repossessed vehicle fetches, as a share of the price it was bought for.</summary>
        public const float RepossessionValueShare = 0.60f;

        /// <summary>The share of a business's daily takings that goes to the lender of a business loan in default, and of a mortgaged building's rents.</summary>
        public const float BusinessDivertShare = 0.25f;
        public const float RentDivertShare = 1f;

        /// <summary>A balance left unsecured by a repossession goes to court if it still stands after this many days.</summary>
        public const int UnsecuredClaimDays = 10;

        /// <summary>The lender's costs added to a claim: a share of the balance, never less than the least.</summary>
        public const float ClaimCostsShare = 0.10f;
        public static int ClaimCostsLeast => Prices.At(50);

        // ---------------------------------------------------------------- the overdraft

        /// <summary>The limit: this share of what the city's banks would lend at the rating, at the threshold and from the best terms up.</summary>
        public const float OverdraftShareAtThreshold = 0.02f;
        public const float OverdraftShareAtBest = 0.08f;
        public const int OverdraftRound = 50;
        public static int OverdraftLeast => Prices.At(200);
        /// <summary>Points on the bank's rate for the rating.</summary>
        public const int OverdraftRatePoints = 8;
        /// <summary>Cash above this is used to pay the overdraft back at each day's end.</summary>
        public static int OverdraftFloat => Prices.At(500);

        // ---------------------------------------------------------------- the credit card

        public const float CardShareAtThreshold = 0.03f;
        public const float CardShareAtBest = 0.12f;
        public const int CardRound = 50;
        public static int CardLeast => Prices.At(300);
        public const int CardRatePoints = 12;
        /// <summary>The fee for drawing cash: a share of the amount, never less than the least.</summary>
        public const float CardCashFeeShare = 0.03f;
        public static int CardCashFeeLeast => Prices.At(5);
        public const int CardDrawLeast = 20;
        /// <summary>A statement every so many days; its minimum is taken this many days after it.</summary>
        public const int CardStatementDays = 7;
        public const int CardGraceDays = 3;
        /// <summary>The minimum payment: this share of the statement, never less than the least, or the whole of a smaller balance.</summary>
        public const float CardMinimumShare = 0.05f;
        public static int CardMinimumLeast => Prices.At(25);

        // ---------------------------------------------------------------- the business loan (one of the game's loans, with a business as security)

        /// <summary>Days of takings a business needs on record before it can secure a loan, and the days its profit is averaged over.</summary>
        public const int BusinessDaysOnRecord = 7;
        /// <summary>The most: this many times the average daily profit of the business.</summary>
        public const int BusinessProfitMultiple = 30;
        public const int BusinessRatePoints = -1;

        // ---------------------------------------------------------------- car finance and the mortgage

        /// <summary>Game days an approval stands: the purchase has to be made by the end of the day after it is set up.</summary>
        public const int ApprovalDays = 1;

        public const int CarDepositLeast = 10, CarDepositMost = 50, CarDepositStep = 10;   // per cent of the price
        public const int CarTermLeast = 60, CarTermMost = 240, CarTermStep = 30;           // game days
        public const int CarRatePoints = 2;
        public const int CarLeast = 500;

        public const int HomeDepositLeast = 20, HomeDepositMost = 50, HomeDepositStep = 10;
        public const int HomeTermLeast = 360, HomeTermMost = 1440, HomeTermStep = 180;
        public const int HomeRatePoints = -1;
        public const int HomeLeast = 5000;
        /// <summary>The most a mortgage lends: this many times what the lending bank would lend unsecured at the rating.</summary>
        public const int HomeCapMultiple = 5;

        // ---------------------------------------------------------------- consolidation

        public const float ConsolidateFeeShare = 0.01f;
        public static int ConsolidateFeeLeast => Prices.At(25);

        // ---------------------------------------------------------------- the lender of last resort

        public static int SharkLeast => Prices.At(500);
        public static int SharkMost => Prices.At(5000);
        /// <summary>With an income the most is this many days of it, if that is more.</summary>
        public const int SharkIncomeDays = 10;
        public const int SharkStep = 500;
        /// <summary>Simple interest a day on the sum borrowed.</summary>
        public const float SharkDailyShare = 0.03f;
        public const int SharkDueDays = 10;
        /// <summary>One extension by the same number of days, for this share of what is owed.</summary>
        public const float SharkExtendFeeShare = 0.15f;
        /// <summary>What is owed never passes this many times the sum borrowed.</summary>
        public const int SharkCapMultiple = 3;
        /// <summary>Days after the due day: a warning, the borrower is set upon, a business is damaged; then one or the other every few days.</summary>
        public const int SharkWarnDay = 1, SharkAssaultDay = 3, SharkDamageDay = 6, SharkRepeatDays = 3;
        /// <summary>The injury of a beating, as the mod's muggings give it: hours hurt and energy lost.</summary>
        public const int SharkInjuryHours = 12;
        public const float SharkInjuryEnergy = 8f;
        /// <summary>The repair bill of a damaged business: a share of the debt, between a least and a most.</summary>
        public const float SharkDamageShare = 0.10f;
        public static int SharkDamageLeast => Prices.At(150);
        public static int SharkDamageMost => Prices.At(1500);

        // ---------------------------------------------------------------- the ways out of debt

        /// <summary>Debt advice is offered once something has been overdue this many days running.</summary>
        public const int AdviceOverdueDays = 10;

        /// <summary>A debt arrangement's daily payment: this share of the average daily income, never less than the least.</summary>
        public const float ArrangementShare = 0.20f;
        public static int ArrangementLeast => Prices.At(25);
        /// <summary>The rating is held at no more than this while an arrangement runs and for some days after.</summary>
        public const int ArrangementScoreCap = 560;
        public const int ArrangementCapDaysAfter = 20;
        /// <summary>Missing this many of its payments ends an arrangement.</summary>
        public const int ArrangementMissesMost = 3;

        /// <summary>Bankruptcy leaves this much cash; the rest goes to the creditors.</summary>
        public static int BankruptKeepCash => Prices.At(500);
        public const int BankruptScore = CreditCatalogue.ScoreMin;
        /// <summary>Nobody lends for this many days, whatever the score.</summary>
        public const int BankruptNoCreditDays = 60;
        /// <summary>After that the rating rebuilds from here, by no more than so many points a day.</summary>
        public const int BankruptRebuildFrom = 450;
        public const int BankruptRebuildPerDay = 2;
        /// <summary>One bankruptcy in this many days.</summary>
        public const int BankruptEveryDays = 180;

        // ---------------------------------------------------------------- names

        public static readonly string[] KindKeys =
        {
            "reallife_econ_kind_overdraft", "reallife_econ_kind_card", "reallife_econ_kind_car", "reallife_econ_kind_home", "reallife_econ_kind_arrears"
        };

        /// <summary>The source written on the credit file's events, by kind: short and never changed.</summary>
        public static readonly string[] KindSources = { "overdraft", "card", "car", "home", "arrears" };

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

        /// <summary>A share of an amount rounded up to a whole unit of money (the small step back absorbs float noise).</summary>
        public static int ShareUp(double amount, double share)
        {
            return amount <= 0 ? 0 : (int)Math.Ceiling(amount * share - 1e-4);
        }
    }
}
