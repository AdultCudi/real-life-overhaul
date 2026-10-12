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
    /// statements see what the mod costs a business. Once a day, a look at the day's transactions for each business.
    /// </summary>
    public static class GameBooks
    {
        public const string Salaries = "ba:transactioncategory_salaryexpenses";
        public const string Rent = "ba:transactioncategory_rent";
        public const string Marketing = "ba:transactioncategory_marketing";
        public const string Licensing = "ba:transactioncategory_licensingfees";

        private const string OurKeys = "reallife" + "_";   // every key of the mod's begins so (split: it is no key itself)

        /// <summary>True for a transaction of the mod's own (its type is one of the mod's keys).</summary>
        public static bool Ours(Transaction t)
        {
            return t != null && t.transactionType != null && t.transactionType.StartsWith(OurKeys, StringComparison.Ordinal);
        }

        /// <summary>The day's statement brought up to date with the costs booked for it since it was made. False if there is none, or nothing changed.</summary>
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
            if (summary == null || summary.businessIncomeStatements == null || summary.businessIncomeStatements.Count == 0)
            {
                return false;
            }
            bool changed = false;
            float businesses = 0f;
            for (int b = 0; b < summary.businessIncomeStatements.Count; b++)
            {
                FinancialSummary.BusinessIncomeStatement statement = summary.businessIncomeStatements[b];
                if (statement == null)
                {
                    continue;
                }
                float salaries = 0f, rent = 0f, marketing = 0f, licensing = 0f;
                foreach (Transaction t in save.Transactions)
                {
                    if (t == null || t.timestamp == null || t.timestamp.Day != day || t.address != statement.Address || t.transactionCategories == null)
                    {
                        continue;
                    }
                    List<string> categories = t.transactionCategories;
                    if (categories.Contains(Salaries))
                    {
                        salaries += t.amount;
                    }
                    if (categories.Contains(Rent))
                    {
                        rent += t.amount;
                    }
                    if (categories.Contains(Marketing))
                    {
                        marketing += t.amount;
                    }
                    if (categories.Contains(Licensing))
                    {
                        licensing += t.amount;
                    }
                }
                // (As the game writes them: each line as an amount spent.)
                salaries = Mathf.Abs(salaries);
                rent = Mathf.Abs(rent);
                marketing = Mathf.Abs(marketing);
                licensing = Mathf.Abs(licensing);
                if (!Mathf.Approximately(salaries, statement.SalaryExpenses) || !Mathf.Approximately(rent, statement.RentExpenses)
                    || !Mathf.Approximately(marketing, statement.MarketingExpenses) || !Mathf.Approximately(licensing, statement.LicensingFees))
                {
                    statement.SalaryExpenses = salaries;
                    statement.RentExpenses = rent;
                    statement.MarketingExpenses = marketing;
                    statement.LicensingFees = licensing;
                    statement.TotalOngoing = salaries + rent + marketing + licensing + statement.Theft;
                    statement.TotalProfit = statement.TotalSales - statement.TotalResources - statement.TotalOngoing;
                    changed = true;
                }
                businesses += statement.TotalProfit;
            }
            if (changed)
            {
                summary.totalBusinessProfit = businesses;
                FinancialSummaryHelper.UpdateIncomeStatementTotalProfit(summary);
            }
            return changed;
        }
    }
}
