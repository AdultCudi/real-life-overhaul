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
    /// The terms of each product by the borrower's rating. Every one of them is nothing under the lending threshold
    /// and moves one way as the score rises: a limit never falls, a rate never rises. Game-free.
    ///
    /// "The bank's rate" is a bank's own rate moved by the rating as <see cref="LendingTerms"/> moves it; a product
    /// adds or takes off its own points, and the game's interest multiplier applies to it as to a loan.
    /// </summary>
    public static class ProductTerms
    {
        /// <summary>The overdraft's limit: a share of what the city's banks would lend at this rating.</summary>
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

        /// <summary>A product's rate in hundredths of a per cent a year: the bank's rate for the rating, the product's points, the game's multiplier.</summary>
        public static int RateBp(int score, int ownRate, int points, float multiplier)
        {
            int rate = Math.Max(CreditCatalogue.RateFloor, LendingTerms.For(score, 0f, ownRate).AnnualRate + points);
            return Math.Max(1, (int)Math.Round(rate * (double)Math.Max(0f, multiplier) * 100.0));
        }

        /// <summary>The whole-point rate a business loan is made at: the bank's rate for the rating less a point, never under the floor.</summary>
        public static int BusinessRate(int score, int ownRate)
        {
            return Math.Max(CreditCatalogue.RateFloor, LendingTerms.For(score, 0f, ownRate).AnnualRate + ProductCatalogue.BusinessRatePoints);
        }

        /// <summary>The most a business loan lends against a business with this average daily profit.</summary>
        public static int BusinessMost(int score, float averageDailyProfit)
        {
            if (score < CreditCatalogue.LendingThreshold || averageDailyProfit <= 0f)
            {
                return 0;
            }
            return (int)Math.Floor(averageDailyProfit * (double)ProductCatalogue.BusinessProfitMultiple);
        }

        /// <summary>The most a lender puts into a vehicle or a building: what it would lend unsecured at the rating, and a multiple of it for a building.</summary>
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

        /// <summary>The deposit that goes with a price: the share, rounded up.</summary>
        public static int Deposit(int price, int depositPercent)
        {
            return ProductCatalogue.ShareUp(price, depositPercent / 100.0);
        }

        /// <summary>The buyer's own money that an approval for this amount asks for: the deposit on the dearest thing the amount can finance.</summary>
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

        /// <summary>The figures of a purchase on finance, with the interest of the whole term added up day by day as the account will charge it.</summary>
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

        // ---------------------------------------------------------------- consolidation

        /// <summary>The fee for consolidating this much debt.</summary>
        /// <summary>
        /// What holding approved purchase money costs: a day's interest at the approved rate for every day it was held,
        /// the day of approval counted, at least 1. Without it an approval was a day or two of cash for nothing.
        /// </summary>
        public static int AdvanceCharge(int amount, int rateBp, int daysHeld, int daysPerYear)
        {
            if (amount <= 0)
            {
                return 0;
            }
            int days = daysHeld < 1 ? 1 : daysHeld;
            double charge = (double)amount * rateBp / 10000.0 * days / (daysPerYear < 1 ? 1 : daysPerYear);
            int whole = (int)System.Math.Ceiling(charge);
            return whole < 1 ? 1 : whole;
        }

        public static int ConsolidationFee(int total)
        {
            return total <= 0 ? 0 : Math.Max(ProductCatalogue.ConsolidateFeeLeast, ProductCatalogue.ShareUp(total, ProductCatalogue.ConsolidateFeeShare));
        }

        /// <summary>
        /// The debts as they stand. A loan's interest is what it would still cost over the days it has left; a card or
        /// an overdraft has no end, so its interest is counted over <paramref name="compareDays"/> (the days of the
        /// loan that would replace it) and it adds nothing to the days.
        /// </summary>
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

        /// <summary>The one loan that replaces them, with its fee in what it costs.</summary>
        public static DebtTotals After(int amount, int dailyInterest, int dailyPayment, int fee)
        {
            int days = dailyPayment > 0 ? (amount + dailyPayment - 1) / dailyPayment : 0;
            return new DebtTotals { Owed = amount, Daily = dailyInterest + Math.Min(dailyPayment, amount), Cost = dailyInterest * days + fee, Days = days };
        }

        /// <summary>Whether a bank will consolidate this much: the rating is at the threshold or over, and the new loan fits what it lends at the rating.</summary>
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
