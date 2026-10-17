using System;
using System.Collections.Generic;
using Entities;
using Helpers;
using Localizor;
using UnityEngine;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// The mod's figures in the game's own daily statement, so the game's tax, its bank and EconoView count them. The game
    /// builds a day's statement the next morning, before the mod's day books its lines, so <see cref="Refresh"/> sums the
    /// day's transactions again as the game does (FinancialSummaryHelper.CreateFinancialSummary): a business's cost lines,
    /// its takings with the city's and the mod's, the mod's rows among its goods, a home's rent and the day's totals, always
    /// afresh from the save, so a second refresh counts nothing twice. Dealerships (Empire Motors and Economy/Dealer) are
    /// booked by their address too. Cost: one walk of the transaction history a day (<see cref="Day"/>).
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

        // A business's figures of the day by address: its four lines, the game's takings, the city's takings, whether the game
        // booked takings at all, the city's rent line there as spent (for a home), and the mod's own takings there.
        private const int Salary = 0, RentLine = 1, MarketingLine = 2, LicensingLine = 3, Sales = 4, CitySales = 5, Sold = 6, CityRent = 7, OwnSales = 8, Width = 9;

        private const string OurKeys = "reallife" + "_";   // every key of the mod's begins so (split: it is no key itself)

        private static readonly List<Address> Dealers = new List<Address>(4);      // the player's Empire Motors dealerships
        private static readonly List<Address> OwnDealers = new List<Address>(4);   // this mod's own (Economy/Dealer)
        private static readonly List<BuildingRegistration> OwnRegs = new List<BuildingRegistration>(4);
        private static readonly Dictionary<Address, float[]> DealerDay = new Dictionary<Address, float[]>(4);   // a dealership's day: takings, cars sold
        private static readonly DayCut<Transaction> Days = new DayCut<Transaction>(t => t.timestamp != null ? t.timestamp.Day : int.MinValue);
        private static readonly Dictionary<Address, float[]> ByAddress = new Dictionary<Address, float[]>(64);
        private static readonly Dictionary<string, float> ByCategory = new Dictionary<string, float>(32, StringComparer.Ordinal);
        private static readonly List<float[]> Lines = new List<float[]>(64);   // the figures of a day's addresses, kept for the next day's
        private static int _linesUsed;
        private static GameInstance _deductedSave;   // the save and the day whose dealership costs the tax was last given
        private static int _deductedDay = int.MinValue;

        /// <summary>For the log: the dealership takings and costs the last refresh booked.</summary>
        public static float DealerTakings, DealerCosts;

        /// <summary>True for a transaction of the mod's own (its type is one of the mod's keys).</summary>
        public static bool Ours(Transaction t)
        {
            return t != null && t.transactionType != null && t.transactionType.StartsWith(OurKeys, StringComparison.Ordinal);
        }

        /// <summary>The game's transactions dated on a day, oldest first, from one walk of the history a day (DayCut). Read it, never change or keep it.</summary>
        public static List<Transaction> Day(int day)
        {
            GameInstance save = SaveGameManager.Current;
            return Days.Of(save != null ? save.Transactions : null, day, save != null ? save.Day - 6 : int.MaxValue);
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

            Dictionary<Address, float[]> byAddress = ByAddress;
            Dictionary<string, float> byCategory = ByCategory;
            byAddress.Clear();
            byCategory.Clear();
            _linesUsed = 0;
            Dictionary<Address, Dictionary<string, float>> goods = null;   // the mod's rows among a business's goods: by address, then by row
            Func<string, bool> takings = EconomyBridges.Takings;
            Func<string, string> costRow = EconomyBridges.CostRow;
            float unassigned = 0f;
            EmpireMotors.Dealerships(Dealers);
            Dealership.DealerGame.Dealerships(OwnRegs);
            OwnDealers.Clear();
            for (int i = 0; i < OwnRegs.Count; i++)
            {
                OwnDealers.Add(OwnRegs[i].Address);
            }
            OwnRegs.Clear();
            DealerDay.Clear();
            DealerTakings = DealerCosts = 0f;
            List<Transaction> ofDay = Day(day);
            for (int n = 0; n < ofDay.Count; n++)
            {
                Transaction t = ofDay[n];
                DealerLine dealer = Dealers.Count > 0 ? EmpireMotors.LineOf(t, Dealers.Count) : DealerLine.None;
                if (dealer != DealerLine.None)
                {
                    Dealer(t, dealer, byAddress, ref goods);   // it has none of the game's categories: nothing below would count it
                    continue;
                }
                DealerLine own = OwnDealers.Count > 0 && !ReferenceEquals(t.address, null) && OwnDealers.Contains(t.address) ? Dealership.DealerLines.Of(t.transactionType) : DealerLine.None;
                if (own != DealerLine.None)
                {
                    Dealer(t, own, byAddress, ref goods);
                    continue;
                }
                float amount = t.amount;
                bool here = !ReferenceEquals(t.address, null);
                if (t.transactionType == UnassignedWageType || (t.transactionType == CityGame.WagesKey && !here))
                {
                    unassigned += amount;   // the city's line on wages of no business goes with them
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
                    line = Line();
                    byAddress[t.address] = line;
                }
                if (categories != null)
                {
                    if (categories.Contains(Salaries))
                    {
                        line[Salary] += amount;
                    }
                    if (categories.Contains(Rent))
                    {
                        line[RentLine] += amount;
                    }
                    if (categories.Contains(Marketing))
                    {
                        line[MarketingLine] += amount;
                    }
                    if (categories.Contains(Licensing))
                    {
                        line[LicensingLine] += amount;
                    }
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
                else if (t.transactionType == OnlineGame.TakingsKey || t.transactionType == OnlineGame.RefundsKey || t.transactionType == EstateGame.CommissionKey
                         || (takings != null && Ours(t) && takings(t.transactionType)))
                {
                    line[OwnSales] += amount;   // a refund is negative
                }
                else if (costRow != null && Ours(t) && amount < 0f)
                {
                    string row = costRow(t.transactionType);
                    if (row != null)
                    {
                        AddGood(ref goods, t.address, row, -amount);
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
                // as the game writes them: each line as an amount spent
                float salaries = line != null ? Mathf.Abs(line[Salary]) : 0f;
                float rent = line != null ? Mathf.Abs(line[RentLine]) : 0f;
                float marketing = line != null ? Mathf.Abs(line[MarketingLine]) : 0f;
                float licensing = line != null ? Mathf.Abs(line[LicensingLine]) : 0f;
                float own = line != null ? line[OwnSales] : 0f;
                float sales = line != null && line[Sold] > 0f ? line[Sales] + line[CitySales] + own : OrderBook(statement, day, own);
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
            DealerOrders(day);

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

        // An address's figures of the day, empty: one kept from an earlier refresh, or a new one.
        private static float[] Line()
        {
            if (_linesUsed == Lines.Count)
            {
                Lines.Add(new float[Width]);
            }
            float[] line = Lines[_linesUsed++];
            Array.Clear(line, 0, line.Length);
            return line;
        }

        // Money spent on a row among a business's goods, made only when a business has such a row.
        private static void AddGood(ref Dictionary<Address, Dictionary<string, float>> goods, Address at, string row, float spent)
        {
            goods = goods ?? new Dictionary<Address, Dictionary<string, float>>();
            if (!goods.TryGetValue(at, out Dictionary<string, float> rows))
            {
                rows = new Dictionary<string, float>(2);
                goods[at] = rows;
            }
            rows.TryGetValue(row, out float sum);
            rows[row] = sum + spent;
        }

        // A dealership's transaction into the day's figures at its address or, booked with none, a share into each of the
        // player's Empire Motors dealerships: takings among its own takings, a cost in its row among the goods.
        private static void Dealer(Transaction t, DealerLine kind, Dictionary<Address, float[]> byAddress, ref Dictionary<Address, Dictionary<string, float>> goods)
        {
            bool here = !ReferenceEquals(t.address, null);
            int count = here ? 1 : Dealers.Count;
            float amount = here ? t.amount : EmpireMotorsLines.Share(t.amount, count);
            float car = t.transactionType == EmpireMotorsLines.SoldType && t.amount > 0f ? 1f / count : 0f;   // a car sold: a customer served
            bool ours = here && OwnDealers.Contains(t.address);
            string row = ours ? Dealership.DealerLines.RowOf(kind) : EmpireMotorsLines.RowOf(kind);
            for (int i = 0; i < count; i++)
            {
                Address at = here ? t.address : Dealers[i];
                if (kind == DealerLine.Takings)
                {
                    if (!byAddress.TryGetValue(at, out float[] line))
                    {
                        line = Line();
                        byAddress[at] = line;
                    }
                    line[OwnSales] += amount;
                    if (!DealerDay.TryGetValue(at, out float[] took))
                    {
                        took = new float[2];
                        DealerDay[at] = took;
                    }
                    took[0] += amount;
                    took[1] += car;
                    DealerTakings += amount;
                }
                else if (row != null)
                {
                    AddGood(ref goods, at, row, -amount);
                    DealerCosts -= amount;
                }
            }
        }

        // Each dealership's order book for the day: what it took and a customer for each car sold, beside what the game booked
        // there. Set, never added to, so a second refresh changes nothing.
        private static void DealerOrders(int day)
        {
            foreach (KeyValuePair<Address, float[]> took in DealerDay)
            {
                BuildingRegistration reg = BuildingHelper.GetBuildingRegistration(took.Key);
                List<OrderHistoryEntry> history = reg != null ? reg.orderHistory : null;
                for (int i = history != null ? history.Count - 1 : -1; i >= 0; i--)
                {
                    OrderHistoryEntry entry = history[i];
                    if (entry == null || entry.dayNumber != day)
                    {
                        continue;
                    }
                    double sold = 0.0;
                    for (int s = 0; entry.itemSales != null && s < entry.itemSales.Count; s++)
                    {
                        sold += entry.itemSales[s] != null ? entry.itemSales[s].totalPrice : 0f;
                    }
                    entry.totalRevenue = Mathf.Max(0f, (float)sold + took.Value[0]);
                    entry.totalCustomers = Mathf.Max(entry.totalCustomers, Mathf.RoundToInt(took.Value[1]));
                    break;
                }
            }
        }

        /// <summary>
        /// What the player's Empire Motors dealerships spent on the day, given to the game's tax to deduct as it deducts a shop's
        /// goods (TaxHelper.TrackTransaction). Never the same day of a save twice. Returns what was deducted.
        /// </summary>
        public static float Deduct(int day)
        {
            GameInstance save = SaveGameManager.Current;
            if (save == null || save.Transactions == null || !EmpireMotors.On || (day == _deductedDay && ReferenceEquals(save, _deductedSave)))
            {
                return 0f;
            }
            _deductedDay = day;
            _deductedSave = save;
            EmpireMotors.Dealerships(Dealers);
            if (Dealers.Count == 0)
            {
                return 0f;
            }
            float stock = 0f, running = 0f;
            List<Transaction> ofDay = Day(day);
            for (int n = 0; n < ofDay.Count; n++)
            {
                Transaction t = ofDay[n];
                DealerLine line = EmpireMotors.LineOf(t, Dealers.Count);
                stock += line == DealerLine.Stock ? t.amount : 0f;
                running += line == DealerLine.Running ? t.amount : 0f;
            }
            float deductible = Mathf.Round(EmpireMotorsLines.Deductible(stock, running) * 100f) / 100f;
            if (deductible <= 0f)
            {
                return 0f;
            }
            var info = new TransactionInfo("reallife_transaction_empire_costs");
            info.SetTaxDeductibleName("reallife_empire_deduct".GetLocalization());   // written as words, as the mod's other costs are
            TaxHelper.TrackTransaction(new Transaction(info) { amount = -deductible });
            return deductible;
        }

        // A business whose takings the game did not book (no customers came, or its sales are a factory's exports): the sales
        // its order book has for the day, and the mod's own takings there. Without its registration the statement's figure stays.
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

        // The mod's own rows among a statement's goods (each under its text key) made what the day's transactions say, and the
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
