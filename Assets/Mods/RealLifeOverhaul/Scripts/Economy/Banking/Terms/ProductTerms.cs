using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Economy
{
    /// <summary>What buying something on finance comes to, worked out before it is agreed.</summary>
    public struct FinanceQuote
    {
        public int Price;
        public int Deposit;         // the buyer's own money
        public int Financed;        // what the lender pays
        public int Term;            // days
        public int Instalment;      // the daily repayment, before interest
        public int RateBp;
        public int FirstInterest;   // the first day's interest: the dearest day
        public int Interest;        // interest over the whole term if every payment is made
    }

    /// <summary>One debt as a consolidation counts it.</summary>
    public struct DebtItem
    {
        public int Owed;
        public int DailyInterest;
        public int DailyPayment;    // 0 for a limit with no fixed repayment (a card, an overdraft)
    }

    /// <summary>Debts added up: what is owed, what leaves each day, what the interest comes to and how long it runs.</summary>
    public struct DebtTotals
    {
        public int Owed, Daily, Cost, Days;
    }

    /// <summary>
    /// Each of the mod's products' terms by the borrower's rating. A product's rate is the bank's rate for the rating
    /// (<see cref="LendingTerms"/>) with the product's points, times the game's interest multiplier. Game-free.
    /// </summary>
    public static class ProductTerms
    {
        public static int OverdraftLimit(int score, float ownCapTotal)
        {
            return Revolving(score, ownCapTotal, ProductCatalogue.OverdraftShareAtThreshold, ProductCatalogue.OverdraftShareAtBest, ProductCatalogue.OverdraftRound, ProductCatalogue.OverdraftLeast);
        }

        public static int CardLimit(int score, float ownCapTotal)
        {
            return Revolving(score, ownCapTotal, ProductCatalogue.CardShareAtThreshold, ProductCatalogue.CardShareAtBest, ProductCatalogue.CardRound, ProductCatalogue.CardLeast);
        }

        private static int Revolving(int score, float ownCapTotal, float low, float high, int round, int least)
        {
            LenderTerms terms = LendingTerms.For(score, ownCapTotal, 0);
            if (!terms.Lends)
            {
                return 0;
            }
            float share = low + (high - low) * LendingTerms.Position(score);
            return Math.Max(least, ProductCatalogue.Round(terms.Cap * (double)share, round));
        }

        /// <summary>A product's rate in hundredths of a per cent a year by the rating alone, without the central bank's (<see cref="WithBase"/>).</summary>
        public static int RateBp(int score, int ownRate, int points, float multiplier)
        {
            int rate = Math.Max(CreditCatalogue.RateFloor, LendingTerms.For(score, 0f, ownRate, 0f).AnnualRate + points);
            return Math.Max(1, (int)Math.Round(rate * (double)Math.Max(0f, multiplier) * 100.0));
        }

        /// <summary>The central bank's rate in hundredths of a per cent a year: its points times the game's multiplier.</summary>
        public static int BaseBp(float basePoints, float multiplier)
        {
            return (int)Math.Round(basePoints * (double)Math.Max(0f, multiplier) * 100.0, MidpointRounding.AwayFromZero);
        }

        /// <summary>A rate with the central bank's on top, never under the floor: a fixed product's for good, a revolving one's for today.</summary>
        public static int WithBase(int rateBp, int baseBp, float multiplier)
        {
            int floor = Math.Max(1, (int)Math.Round(CreditCatalogue.RateFloor * (double)Math.Max(0f, multiplier) * 100.0));
            return baseBp == 0 ? rateBp : Math.Max(Math.Min(floor, rateBp), rateBp + baseBp);
        }

        /// <summary>The rate a purchase on finance is approved at and keeps: by the rating, with the central bank's rate of the day.</summary>
        public static int FixedRateBp(int score, int ownRate, int points, float multiplier)
        {
            return WithBase(RateBp(score, ownRate, points, multiplier), BaseBp(LendingTerms.BasePoints, multiplier), multiplier);
        }

        /// <summary>The whole-point rate a business loan is made at: the bank's rate for the rating less a point.</summary>
        public static int BusinessRate(int score, int ownRate)
        {
            return Math.Max(CreditCatalogue.RateFloor, LendingTerms.For(score, 0f, ownRate).AnnualRate + ProductCatalogue.BusinessRatePoints);
        }

        public static int BusinessMost(int score, float averageDailyProfit)
        {
            if (score < CreditCatalogue.LendingThreshold || averageDailyProfit <= 0f)
            {
                return 0;
            }
            return (int)Math.Floor(averageDailyProfit * (double)ProductCatalogue.BusinessProfitMultiple);
        }

        /// <summary>The most a lender puts into a vehicle (what it would lend unsecured at the rating) or a building (a multiple of that).</summary>
        public static int FinanceMost(ProductKind kind, int score, float ownCap)
        {
            LenderTerms terms = LendingTerms.For(score, ownCap, 0);
            if (!terms.Lends)
            {
                return 0;
            }
            return (int)Math.Min(int.MaxValue, Math.Floor(terms.Cap * (double)(kind == ProductKind.Mortgage ? ProductCatalogue.HomeCapMultiple : 1)));
        }

        public static int Points(ProductKind kind)
        {
            switch (kind)
            {
                case ProductKind.Overdraft: return ProductCatalogue.OverdraftRatePoints;
                case ProductKind.CreditCard: return ProductCatalogue.CardRatePoints;
                case ProductKind.CarFinance: return ProductCatalogue.CarRatePoints;
                case ProductKind.Mortgage: return ProductCatalogue.HomeRatePoints;
                default: return 0;
            }
        }

        public static int DepositLeast(ProductKind kind) => kind == ProductKind.Mortgage ? ProductCatalogue.HomeDepositLeast : ProductCatalogue.CarDepositLeast;
        public static int DepositMost(ProductKind kind) => kind == ProductKind.Mortgage ? ProductCatalogue.HomeDepositMost : ProductCatalogue.CarDepositMost;
        public static int DepositStep(ProductKind kind) => kind == ProductKind.Mortgage ? ProductCatalogue.HomeDepositStep : ProductCatalogue.CarDepositStep;
        public static int TermLeast(ProductKind kind) => kind == ProductKind.Mortgage ? ProductCatalogue.HomeTermLeast : ProductCatalogue.CarTermLeast;
        public static int TermMost(ProductKind kind) => kind == ProductKind.Mortgage ? ProductCatalogue.HomeTermMost : ProductCatalogue.CarTermMost;
        public static int TermStep(ProductKind kind) => kind == ProductKind.Mortgage ? ProductCatalogue.HomeTermStep : ProductCatalogue.CarTermStep;
        public static int FinanceLeast(ProductKind kind) => kind == ProductKind.Mortgage ? ProductCatalogue.HomeLeast : ProductCatalogue.CarLeast;

        public static int Deposit(int price, int depositPercent)
        {
            return ProductCatalogue.ShareUp(price, depositPercent / 100.0);
        }

        /// <summary>The buyer's own money an approval for this amount asks for: the deposit on the dearest thing the amount can finance.</summary>
        public static int DepositFor(int approved, int depositPercent)
        {
            int percent = Math.Max(1, Math.Min(99, depositPercent));
            return ProductCatalogue.ShareUp(approved, percent / (double)(100 - percent));
        }

        /// <summary>What the lender pays towards a price: never more than was approved, and never the buyer's deposit.</summary>
        public static int Financed(int price, int depositPercent, int approved)
        {
            return Math.Max(0, Math.Min(approved, price - Deposit(price, depositPercent)));
        }

        /// <summary>The figures of a purchase on finance, its interest added up day by day as the account will charge it.</summary>
        public static FinanceQuote Quote(int price, int depositPercent, int approved, int term, int rateBp, int daysPerYear)
        {
            var q = new FinanceQuote { Price = price, Term = Math.Max(1, term), RateBp = rateBp };
            q.Financed = Financed(price, depositPercent, approved);
            q.Deposit = price - q.Financed;
            q.Instalment = AccountRules.InstalmentFor(q.Financed, q.Term);
            long carry = 0;
            int left = q.Financed;
            for (int day = 0; day < q.Term && left > 0; day++)
            {
                int interest = AccountRules.Interest(left, rateBp, daysPerYear, 1, ref carry);
                if (day == 0)
                {
                    q.FirstInterest = interest;
                }
                q.Interest += interest;
                left -= Math.Min(q.Instalment, left);
            }
            return q;
        }

        /// <summary>What holding approved purchase money costs: a day's interest at the approved rate for each day held, at least 1.</summary>
        public static int AdvanceCharge(int amount, int rateBp, int daysHeld, int daysPerYear)
        {
            if (amount <= 0)
            {
                return 0;
            }
            int days = daysHeld < 1 ? 1 : daysHeld;
            double charge = (double)amount * rateBp / 10000.0 * days / (daysPerYear < 1 ? 1 : daysPerYear);
            int whole = (int)Math.Ceiling(charge);
            return whole < 1 ? 1 : whole;
        }

        /// <summary>
        /// What holding approved money costs when the purchase is made on the last of the days held: interest on the part the
        /// lender finances only (what goes back unused costs nothing), for the days before the purchase, as the account it
        /// becomes charges its own from the day it opens. Nothing when it is bought on the day it was approved.
        /// </summary>
        public static int PurchaseCharge(int financed, int rateBp, int daysHeld, int daysPerYear)
        {
            return financed <= 0 || daysHeld <= 1 ? 0 : AdvanceCharge(financed, rateBp, daysHeld - 1, daysPerYear);
        }

        public static int ConsolidationFee(int total)
        {
            return total <= 0 ? 0 : Math.Max(ProductCatalogue.ConsolidateFeeLeast, ProductCatalogue.ShareUp(total, ProductCatalogue.ConsolidateFeeShare));
        }

        /// <summary>The debts as they stand; a card's or an overdraft's interest, having no end, over <paramref name="compareDays"/>.</summary>
        public static DebtTotals Before(IList<DebtItem> debts, int compareDays)
        {
            var t = new DebtTotals();
            for (int i = 0; i < debts.Count; i++)
            {
                DebtItem d = debts[i];
                if (d.Owed <= 0)
                {
                    continue;
                }
                int days = d.DailyPayment > 0 ? (d.Owed + d.DailyPayment - 1) / d.DailyPayment : 0;
                t.Owed += d.Owed;
                t.Daily += d.DailyInterest + Math.Min(d.DailyPayment, d.Owed);
                t.Cost += d.DailyInterest * (days > 0 ? days : Math.Max(0, compareDays));
                t.Days = Math.Max(t.Days, days);
            }
            return t;
        }

        /// <summary>The one loan that replaces them, its fee in what it costs.</summary>
        public static DebtTotals After(int amount, int dailyInterest, int dailyPayment, int fee)
        {
            int days = dailyPayment > 0 ? (amount + dailyPayment - 1) / dailyPayment : 0;
            return new DebtTotals { Owed = amount, Daily = dailyInterest + Math.Min(dailyPayment, amount), Cost = dailyInterest * days + fee, Days = days };
        }

        /// <summary>Whether a bank will consolidate this much: the rating lends, and the new loan with its fee fits what the bank lends at the rating.</summary>
        public static ProductAnswer Consolidate(int score, int total, float capAtScore)
        {
            if (score < CreditCatalogue.LendingThreshold)
            {
                return ProductAnswer.BelowThreshold;
            }
            if (total <= 0)
            {
                return ProductAnswer.NothingOwed;
            }
            int amount = total + ConsolidationFee(total);
            return amount < CreditCatalogue.LoanLeast ? ProductAnswer.TooLittle : amount > capAtScore ? ProductAnswer.TooMuch : ProductAnswer.Yes;
        }
    }
}
