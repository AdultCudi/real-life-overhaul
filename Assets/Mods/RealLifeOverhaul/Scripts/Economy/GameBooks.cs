using System;
using System.Collections.Generic;
using Entities;
using Helpers;
using UnityEngine;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// The mod's business costs in the game's own daily statement (GameInstance.financialSummaries, built by
    /// FinancialSummaryHelper first thing each day for the day that ended). The game reads four lines of a business's
    /// costs from that day's transactions, by the business's address and the game's categories: salaries, rent,
    /// marketing and licensing fees. A cost of the mod's that is one of those is booked with that category and the
    /// business's address (CompanyGame.Take). One booked after midnight for the day that ended (the mod's day comes after
    /// the game has built the statement) is put into it here once the mod's day is done (EconomyDirector.OnDay): the
    /// four lines are summed again from the day's transactions as the game sums them, and the totals with them. Goods,
    /// sales and theft are the game's own and stay as they are. What the statement has no line for (insurance, an
    /// accountant, hosting) stays in the mod's own books. So the game's bank (a loan's size), its daily summary and its
    /// statements see what the mod costs a business. The city's lines of a day (rent, wages, takings, shops and services:
    /// Economy/City) are booked the morning after under the address and category of the game's own charges they adjust,
    /// so they are summed in here too: a business's four lines by category, its takings (the game's sales of the day and
    /// the city's line on them, the goods sold scaled row by row so that they still add up), a rented home's rent (the
    /// game's daily rent and the city's line on it), and the day's own figures (loans, health insurance, the headhunter,
    /// parking, salary income, the wages of staff of no business), each summed as the game sums them. So the game's taxes,
    /// its bank and EconoView count them. Once or twice a day, one look at the day's transactions.
    /// </summary>
    public static class GameBooks
    {
        public const string Salaries = "ba:transactioncategory_salaryexpenses";
        public const string Rent = "ba:transactioncategory_rent";
        public const string Marketing = "ba:transactioncategory_marketing";
        public const string Licensing = "ba:transactioncategory_licensingfees";
        public const string Parking = "ba:transactioncategory_parkingfees";
        public const string HealthInsurance = "ba:transactioncategory_healthinsuranceexpenses";
        public const string Headhunter = "ba:transactioncategory_headhunterreplacementfees";
        private const string Loans = "ba:transactioncategory_loanexpenses";
        private const string SalaryIncome = "ba:transactioncategory_salaryincome";
        private const string RevenueType = "ba:transaction_revenue";
        private const string UnassignedWageType = "ba:transaction_unassignedwage";

        // A business's figures of the day by address: its four lines, the game's takings, the city's takings, whether the
        // game booked takings at all, and the city's rent line there as spent (for a home).
        private const int Salary = 0, RentLine = 1, MarketingLine = 2, LicensingLine = 3, Sales = 4, CitySales = 5, Sold = 6, CityRent = 7, Width = 8;

        private const string OurKeys = "reallife" + "_";   // every key of the mod's begins so (split: it is no key itself)

        /// <summary>True for a transaction of the mod's own (its type is one of the mod's keys).</summary>
        public static bool Ours(Transaction t)
        {
            return t != null && t.transactionType != null && t.transactionType.StartsWith(OurKeys, StringComparison.Ordinal);
        }

        /// <summary>The day's statement brought up to date with what was booked for it since it was made. False if there is none, or nothing changed.</summary>
        public static bool Refresh(int day)
        {
            GameInstance save = SaveGameManager.Current;
            List<FinancialSummary> summaries = save != null ? save.financialSummaries : null;
            if (summaries == null || save.Transactions == null)
            {
                return false;
            }
            FinancialSummary summary = null;
            for (int i = summaries.Count - 1; i >= 0 && summary == null; i--)
            {
                if (summaries[i] != null && summaries[i].dayNumber == day)
                {
                    summary = summaries[i];
                }
            }
            if (summary == null)
            {
                return false;
            }

            // The day's transactions, once, summed as the game sums them (FinancialSummaryHelper.CreateFinancialSummary).
            var byAddress = new Dictionary<Address, float[]>();
            var byCategory = new Dictionary<string, float>();
            float unassigned = 0f;
            foreach (Transaction t in save.Transactions)
            {
                if (t == null || t.timestamp == null || t.timestamp.Day != day)
                {
                    continue;
                }
                float amount = t.amount;
                bool here = !ReferenceEquals(t.address, null);
                if (t.transactionType == UnassignedWageType || (t.transactionType == CityGame.WagesKey && !here))
                {
                    unassigned += amount;   // (the city's line on wages of no business goes with them)
                }
                List<string> categories = t.transactionCategories;
                for (int c = 0; categories != null && c < categories.Count; c++)
                {
                    if (categories[c] != null)
                    {
                        byCategory.TryGetValue(categories[c], out float sum);
                        byCategory[categories[c]] = sum + amount;
                    }
                }
                if (!here)
                {
                    continue;
                }
                if (!byAddress.TryGetValue(t.address, out float[] line))
                {
                    line = new float[Width];
                    byAddress[t.address] = line;
                }
                if (categories != null)
                {
                    if (categories.Contains(Salaries)) line[Salary] += amount;
                    if (categories.Contains(Rent)) line[RentLine] += amount;
                    if (categories.Contains(Marketing)) line[MarketingLine] += amount;
                    if (categories.Contains(Licensing)) line[LicensingLine] += amount;
                }
                if (t.transactionType == RevenueType)
                {
                    line[Sales] += amount;
                    line[Sold] = 1f;
                }
                else if (t.transactionType == CityGame.TakingsKey)
                {
                    line[CitySales] += amount;
                }
                else if (t.transactionType == CityGame.RentKey)
                {
                    line[CityRent] -= amount;
                }
            }

            bool changed = false;
            float businesses = 0f;
            for (int b = 0; summary.businessIncomeStatements != null && b < summary.businessIncomeStatements.Count; b++)
            {
                FinancialSummary.BusinessIncomeStatement statement = summary.businessIncomeStatements[b];
                if (statement == null)
                {
                    continue;
                }
                byAddress.TryGetValue(statement.Address, out float[] line);
                // (As the game writes them: each line as an amount spent.)
                float salaries = line != null ? Mathf.Abs(line[Salary]) : 0f;
                float rent = line != null ? Mathf.Abs(line[RentLine]) : 0f;
                float marketing = line != null ? Mathf.Abs(line[MarketingLine]) : 0f;
                float licensing = line != null ? Mathf.Abs(line[LicensingLine]) : 0f;
                // The takings: the game's own of the day and the city's line on them. Where the game booked none there is
                // nothing to go by, and they are left as the game made them.
                float sales = line != null && line[Sold] > 0f ? line[Sales] + line[CitySales] : statement.TotalSales;
                if (!Mathf.Approximately(salaries, statement.SalaryExpenses) || !Mathf.Approximately(rent, statement.RentExpenses)
                    || !Mathf.Approximately(marketing, statement.MarketingExpenses) || !Mathf.Approximately(licensing, statement.LicensingFees)
                    || !Mathf.Approximately(sales, statement.TotalSales))
                {
                    statement.SalaryExpenses = salaries;
                    statement.RentExpenses = rent;
                    statement.MarketingExpenses = marketing;
                    statement.LicensingFees = licensing;
                    Scale(statement.Sales, sales);
                    statement.TotalSales = sales;
                    statement.TotalOngoing = salaries + rent + marketing + licensing + statement.Theft;
                    statement.TotalProfit = statement.TotalSales - statement.TotalResources - statement.TotalOngoing;
                    changed = true;
                }
                businesses += statement.TotalProfit;
            }

            // A rented home: the game's daily rent and the city's line on it.
            float homes = 0f;
            for (int r = 0; summary.residentialStatements != null && r < summary.residentialStatements.Count; r++)
            {
                FinancialSummary.ResidentialStatement home = summary.residentialStatements[r];
                if (home == null)
                {
                    continue;
                }
                BuildingRegistration reg = BuildingHelper.GetBuildingRegistration(home.Address);
                if (reg != null)
                {
                    float amount = reg.RentPerDay + (byAddress.TryGetValue(home.Address, out float[] line) ? line[CityRent] : 0f);
                    if (!Mathf.Approximately(amount, home.Amount))
                    {
                        home.Amount = amount;
                        changed = true;
                    }
                }
                homes += home.Amount;
            }

            float loans = Sum(byCategory, Loans), health = Sum(byCategory, HealthInsurance), headhunter = Sum(byCategory, Headhunter);
            float parking = Sum(byCategory, Parking), income = Sum(byCategory, SalaryIncome);
            if (!Mathf.Approximately(loans, summary.totalLoanExpenses) || !Mathf.Approximately(health, summary.totalHealthInsuranceExpenses)
                || !Mathf.Approximately(headhunter, summary.totalHeadhunterReplacementFees) || !Mathf.Approximately(parking, summary.parkingFees)
                || !Mathf.Approximately(income, summary.salaryIncome) || !Mathf.Approximately(unassigned, summary.totalUnassignedStaffWages)
                || !Mathf.Approximately(homes, summary.totalResidentialExpenses))
            {
                summary.totalLoanExpenses = loans;
                summary.totalHealthInsuranceExpenses = health;
                summary.totalHeadhunterReplacementFees = headhunter;
                summary.parkingFees = parking;
                summary.salaryIncome = income;
                summary.totalUnassignedStaffWages = unassigned;
                summary.totalResidentialExpenses = homes;
                changed = true;
            }
            if (changed)
            {
                summary.totalBusinessProfit = businesses;
                FinancialSummaryHelper.UpdateIncomeStatementTotalProfit(summary);
            }
            return changed;
        }

        private static float Sum(Dictionary<string, float> byCategory, string category)
        {
            return byCategory.TryGetValue(category, out float sum) ? sum : 0f;
        }

        // The goods sold, row by row, in proportion to a new total (their sum taken as the old one), so that they add up to it.
        private static void Scale(List<FinancialSummary.BusinessIncomeStatement.TransactionGroupEntry> rows, float total)
        {
            float sum = 0f;
            for (int i = 0; rows != null && i < rows.Count; i++)
            {
                sum += rows[i] != null ? rows[i].Amount : 0f;
            }
            if (sum <= 0f || Mathf.Approximately(sum, total))
            {
                return;
            }
            float k = total / sum;
            for (int i = 0; i < rows.Count; i++)
            {
                if (rows[i] != null)
                {
                    rows[i].Amount *= k;
                }
            }
        }
    }
}
