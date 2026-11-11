using System;

namespace RealLifeOverhaul.Economy
{
    /// <summary>Whether a game begins owing a student loan, or why it does not.</summary>
    public enum StartDebtAnswer
    {
        Yes = 0,
        Off = 1,        // the option is off
        NotNew = 2,     // the game is past its first day, or already owes something
        Session = 3,    // a Going Public session: the loan would be one player's in a world they share
        Tutorial = 4,   // the game's tutorial is on: it lends the first business its loan and reads every loan after
        NoBank = 5      // the city has no bank to lend it (yet)
    }

    /// <summary>What a student loan the game starts with comes to, in whole money.</summary>
    public struct StartDebtFigures
    {
        public int Amount;
        public int Payment;     // the part of the amount the game takes each day
        public int Interest;    // and the interest it takes with it

        /// <summary>The days until the amount is paid at that payment; 0 with no payment.</summary>
        public int Days => Payment > 0 ? (Amount + Payment - 1) / Payment : 0;
    }

    /// <summary>
    /// A new game that starts owing a student loan (option "New games start with a student loan"): which study it paid
    /// for, what it comes to, the terms it is made on and when a game is new enough to be given one. The loan is one of
    /// the game's own, so its payments, its missed payments and the credit rating are the loans' own
    /// (<see cref="LoanBook"/>); only its size and its rate are chosen here. Game-free: compiled as it is by EconomyCheck.
    /// </summary>
    public static class StartDebtRules
    {
        // the drop-down's choices: saved as a number, add at the end, never renumber
        public const int Diploma = 0, Bachelor = 1, Master = 2, Mba = 3, Count = 4;
        public const int DegreeDefault = Bachelor;

        /// <summary>The text of each choice, in the order of the numbers above.</summary>
        public static readonly string[] DegreeKeys =
        {
            "reallife_econ_start_debt_diploma", "reallife_econ_start_debt_bachelor",
            "reallife_econ_start_debt_master", "reallife_econ_start_debt_mba"
        };

        /// <summary>
        /// What each study costs, whole money at the start's prices, from the fees of the school's and the university's
        /// courses (Careers/Study/StudyCatalogue, which EconomyCheck holds them to): a diploma, a bachelor's degree, a
        /// bachelor's degree and a master's course, a bachelor's degree and the MBA.
        /// </summary>
        public static readonly int[] Amounts = { 2500, 9000, 21000, 27000 };

        public const int RateBp = 450;      // basis points a year: the student loan of Careers (StudyCatalogue.LoanRateBp), whichever the bank
        public const int TermDays = 360;    // game days the loan is paid over, about, whatever the year's length (as the finance products count theirs)
        public const int LeastPayment = LoanTalkRules.LeastDailyPayment;   // money a day: the game's own least (LoanHelper.MinimumDailyPayment)
        public const int NewDays = 1;       // a game is new on its first day and not after

        /// <summary>The choice from a saved number: an unknown one is the nearest known.</summary>
        public static int Level(int saved)
        {
            return saved < 0 ? 0 : saved >= Count ? Count - 1 : saved;
        }

        /// <summary>
        /// The least daily payment the game lets a loan of this size carry at a bank that pays loans over so many years:
        /// half the bank's own payment, never under its least (LoanHelper.CalculateMinimumDailyPayment, term for term).
        /// The game's loans page raises a payment under it the first time the page is opened.
        /// </summary>
        public static int MinimumPayment(int amount, int bankYears, int daysPerYear)
        {
            if (amount <= 0)
            {
                return 0;
            }
            int standard = LoanTalkRules.DailyPayment(amount, bankYears, daysPerYear);
            return Math.Max(LeastPayment, (int)Math.Floor(standard * 0.5f));
        }

        /// <summary>The daily payment: the amount over <see cref="TermDays"/>, never under what the game lets the loan carry.</summary>
        public static int Payment(int amount, int minimum)
        {
            return amount <= 0 ? 0 : Math.Max(Math.Max(LeastPayment, minimum), amount / TermDays);
        }

        /// <summary>The loan for a choice at the city's prices, at a bank that pays loans over so many years, under the game's interest multiplier.</summary>
        public static StartDebtFigures Figures(int degree, int priceLevel, int bankYears, float multiplier, int daysPerYear)
        {
            int amount = PriceRules.At(Amounts[Level(degree)], priceLevel);
            return new StartDebtFigures
            {
                Amount = amount,
                Payment = Payment(amount, MinimumPayment(amount, bankYears, daysPerYear)),
                Interest = GameLoanMath.DailyInterest(amount, RateBp / 100f, multiplier, 1, daysPerYear)
            };
        }

        /// <summary>
        /// Whether a game just opened on the credit side is given its loan: the option, a game on its first day that
        /// owes nothing, no shared session, no tutorial, and a bank to lend it. The first reason that fails is the answer.
        /// </summary>
        public static StartDebtAnswer Decide(bool optionOn, int day, int loans, bool inSession, bool tutorial, bool hasBank)
        {
            if (!optionOn)
            {
                return StartDebtAnswer.Off;
            }
            if (day > NewDays || loans > 0)
            {
                return StartDebtAnswer.NotNew;
            }
            if (inSession)
            {
                return StartDebtAnswer.Session;
            }
            if (tutorial)
            {
                return StartDebtAnswer.Tutorial;
            }
            return hasBank ? StartDebtAnswer.Yes : StartDebtAnswer.NoBank;
        }
    }
}
