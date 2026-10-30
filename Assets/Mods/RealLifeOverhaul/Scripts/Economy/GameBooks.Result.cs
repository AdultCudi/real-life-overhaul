using System.Collections.Generic;
using Entities;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// The day's result (<see cref="DayRules"/>): the game's profit or loss for a day with the rest of the money that moved the player's cash. The
    /// walk is the one <see cref="Refresh"/> makes, over the day's transactions the game keeps once a day (<see cref="Day"/>); it decides for each whether
    /// the game's figure holds it already, by the same tests Refresh counts a transaction into the statements by, so nothing is counted twice. Nothing
    /// is written to the save: the figure is read, never stored.
    /// </summary>
    public static partial class GameBooks
    {
        private static readonly List<Address> Statements = new List<Address>(16);   // the addresses of the day's business statements

        /// <summary>
        /// The day's result. False if the game made no statement for the day. <paramref name="parts"/> is the game's figure worked out again from the
        /// statement's own parts, which must equal it once the day's statement is up to date. Cost: one pass over the day's transactions.
        /// </summary>
        public static bool Result(int day, out DayFigure figure, out float parts)
        {
            figure = default;
            parts = 0f;
            GameInstance save = SaveGameManager.Current;
            FinancialSummary summary = save != null && save.financialSummaries != null ? save.financialSummaries.FindLast(s => s != null && s.dayNumber == day) : null;
            if (summary == null || save.Transactions == null)
            {
                return false;
            }
            Statements.Clear();
            for (int b = 0; summary.businessIncomeStatements != null && b < summary.businessIncomeStatements.Count; b++)
            {
                if (summary.businessIncomeStatements[b] != null)
                {
                    Statements.Add(summary.businessIncomeStatements[b].Address);
                }
            }
            ReadDealers();
            var tally = new ResultTally();
            List<Transaction> ofDay = Day(day);
            for (int n = 0; n < ofDay.Count; n++)
            {
                Transaction t = ofDay[n];
                if (t != null && t.amount != 0f)
                {
                    tally.Add(t.transactionType, t.amount, Holds(t));
                }
            }
            parts = summary.totalBusinessProfit + summary.totalRealEstate - summary.totalResidentialExpenses + summary.totalLoanExpenses + summary.parkingFees
                    + summary.totalUnassignedStaffWages + summary.salaryIncome + summary.totalHealthInsuranceExpenses + summary.totalHeadhunterReplacementFees;
            figure = tally.Figure(day, summary.totalProfit);
            return true;
        }

        // True if the game's figure holds the transaction already: counted by Refresh (or by the game) into a statement, a home's rent or one of the day's lines.
        private static bool Holds(Transaction t)
        {
            string type = t.transactionType;
            bool here = !ReferenceEquals(t.address, null);
            if (Dealers.Count > 0 && EmpireMotors.LineOf(t, Dealers.Count) != DealerLine.None)
            {
                return true;
            }
            if (here && OwnDealers.Count > 0 && OwnDealers.Contains(t.address) && Dealership.DealerLines.Of(type) != DealerLine.None)
            {
                return true;
            }
            if (type == UnassignedWageType || (type == CityGame.WagesKey && !here))
            {
                return true;
            }
            List<string> categories = t.transactionCategories;
            if (categories != null)
            {
                if (categories.Contains(GameLoans.ExpenseCategory) || categories.Contains(HealthInsurance) || categories.Contains(Headhunter) || categories.Contains(Parking)
                    || categories.Contains(SalaryIncome) || (here && categories.Contains(Rent)))
                {
                    return true;
                }
            }
            if (!here)
            {
                return false;
            }
            if (type == CityGame.RentKey || type == EstateGame.RentKey)
            {
                return true;   // a home's rent with the city's line on it, and the daily income of the property the player rents out
            }
            if (!Statements.Contains(t.address))
            {
                return false;
            }
            if (categories != null && (categories.Contains(Salaries) || categories.Contains(Marketing) || categories.Contains(Licensing)))
            {
                return true;
            }
            if (type == RevenueType || type == CityGame.TakingsKey || type == OnlineGame.TakingsKey || type == OnlineGame.RefundsKey || type == EstateGame.CommissionKey)
            {
                return true;
            }
            if (!Ours(t))
            {
                return CashCategory.For(type) == CashCategory.Stock;   // stock bought for the business: the statement holds its cost as the goods sold
            }
            BizLine kind = BizLines.Of(type);
            if (kind == BizLine.Takings || kind == BizLine.Cost)
            {
                return true;
            }
            System.Func<string, bool> takings = EconomyBridges.Takings;
            if (takings != null && takings(type))
            {
                return true;
            }
            System.Func<string, string> costRow = EconomyBridges.CostRow;
            return t.amount < 0f && costRow != null && costRow(type) != null;
        }
    }
}
