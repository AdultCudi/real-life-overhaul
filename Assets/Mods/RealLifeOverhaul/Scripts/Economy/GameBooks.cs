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
    /// its bank and EconoView count them. The mod's own takings at a business are among its takings too: its online
    /// orders less refunds (Economy/Online, which puts each product's sales among the statement's rows itself) and, from
    /// another feature, the broadband company's bills at its headquarters (EconomyBridges.Takings), with its network's
    /// cost as a row among the goods (EconomyBridges.CostRow). A statement's takings are summed afresh each time from what
    /// the save keeps (the day's transactions, and the order book where the game booked no takings of its own: a
    /// factory's exports), never from the statement itself, so entering them twice counts nothing twice and a refresh
    /// after another entry loses nothing. Once or twice a day, one look at the day's transactions.
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
        // game booked takings at all, the city's rent line there as spent (for a home), and the mod's own takings there.
        private const int Salary = 0, RentLine = 1, MarketingLine = 2, LicensingLine = 3, Sales = 4, CitySales = 5, Sold = 6, CityRent = 7, OwnSales = 8, Width = 9;

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
            Dictionary<Address, Dictionary<string, float>> goods = null;   // the mod's rows among a business's goods: by address, then by row
            Func<string, bool> takings = EconomyBridges.Takings;
            Func<string, string> costRow = EconomyBridges.CostRow;
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
                else if (t.transactionType == OnlineGame.TakingsKey || t.transactionType == OnlineGame.RefundsKey || (takings != null && Ours(t) && takings(t.transactionType)))
                {
                    line[OwnSales] += amount;   // (a refund is negative)
                }
                else if (costRow != null && Ours(t) && amount < 0f)
                {
                    string row = costRow(t.transactionType);
                    if (row != null)
                    {
                        goods = goods ?? new Dictionary<Address, Dictionary<string, float>>();
                        if (!goods.TryGetValue(t.address, out Dictionary<string, float> rows))
                        {
                            rows = new Dictionary<string, float>(2);
                            goods[t.address] = rows;
                        }
                        rows.TryGetValue(row, out float spent);
                        rows[row] = spent - amount;
                    }
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
                // The takings: the game's own of the day and the city's line on them; where the game booked none, the sales of
                // its order book (a factory's exports); and the mod's own takings there. Never read from the statement itself.
                float own = line != null ? line[OwnSales] : 0f;
                float sales = line != null && line[Sold] > 0f ? line[Sales] + line[CitySales] + own : OrderBook(statement, day, own);
                // The goods: the game's rows (and the online products', each under its item's name) as they are, the mod's own as booked.
                Dictionary<string, float> rows = null;
                goods?.TryGetValue(statement.Address, out rows);
                bool goodsChanged = OwnGoods(statement, rows);
                if (goodsChanged || !Mathf.Approximately(salaries, statement.SalaryExpenses) || !Mathf.Approximately(rent, statement.RentExpenses)
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

        // A business whose takings the game did not book (no customers came: a headquarters, a warehouse; or its sales are
        // exports: a factory): the sales its order book has for the day, as the game put them in the statement, and the
        // mod's own takings there. If its registration cannot be had, the statement's own figure is kept as it is.
        private static float OrderBook(FinancialSummary.BusinessIncomeStatement statement, int day, float own)
        {
            BuildingRegistration reg = BuildingHelper.GetBuildingRegistration(statement.Address);
            if (reg == null || reg.orderHistory == null)
            {
                return statement.TotalSales;
            }
            double sales = 0.0;
            for (int i = 0; i < reg.orderHistory.Count; i++)
            {
                OrderHistoryEntry entry = reg.orderHistory[i];
                if (entry != null && entry.dayNumber == day && entry.itemSales != null)
                {
                    for (int s = 0; s < entry.itemSales.Count; s++)
                    {
                        sales += entry.itemSales[s] != null ? entry.itemSales[s].totalPrice : 0f;
                    }
                    break;
                }
            }
            return (float)sales + own;
        }

        // The mod's own rows among a statement's goods (each under its text key) made what the day's transactions say, the
        // total goods moved by as much; true if anything changed. The game's rows and the online products' are left alone.
        private static bool OwnGoods(FinancialSummary.BusinessIncomeStatement statement, Dictionary<string, float> rows)
        {
            List<FinancialSummary.BusinessIncomeStatement.TransactionGroupEntry> list = statement.Resources;
            if (list == null)
            {
                if (rows == null)
                {
                    return false;
                }
                list = new List<FinancialSummary.BusinessIncomeStatement.TransactionGroupEntry>();
                statement.Resources = list;
            }
            float was = 0f, now = 0f;
            for (int i = list.Count - 1; i >= 0; i--)
            {
                FinancialSummary.BusinessIncomeStatement.TransactionGroupEntry entry = list[i];
                if (entry == null || entry.ItemName == null || !entry.ItemName.StartsWith(OurKeys, StringComparison.Ordinal))
                {
                    continue;
                }
                was += entry.Amount;
                float wanted = rows != null && rows.TryGetValue(entry.ItemName, out float spent) ? spent : 0f;
                if (wanted <= 0f)
                {
                    list.RemoveAt(i);
                }
                else
                {
                    entry.Amount = wanted;
                    now += wanted;
                }
            }
            if (rows != null)
            {
                foreach (KeyValuePair<string, float> row in rows)
                {
                    if (row.Value > 0f && list.FindIndex(x => x != null && x.ItemName == row.Key) < 0)
                    {
                        list.Add(new FinancialSummary.BusinessIncomeStatement.TransactionGroupEntry { ItemName = row.Key, Amount = row.Value });
                        now += row.Value;
                    }
                }
            }
            if (Mathf.Approximately(was, now))
            {
                return false;
            }
            statement.TotalResources += now - was;
            return true;
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
