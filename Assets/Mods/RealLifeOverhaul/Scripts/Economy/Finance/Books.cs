using System;
using System.Collections.Generic;
using BigAmbitions.Tags;
using Entities;
using Helpers;
using Streets;
using UI.Smartphone.Apps.Persona;
using UnityEngine;

namespace RealLifeOverhaul.Economy
{
    /// <summary>What leaves in the next daily run, as the Finance app's Overview adds it up.</summary>
    public struct Obligations
    {
        public float Loans;         // the game's loans: each one's payment and interest
        public float LoanInterest;  // of that, interest
        public float Accounts;      // the mod's own accounts
        public float Other;         // fines and court debts
        public float Rent;
        public float Wages;         // as the game paid them yesterday
        public float Total => Loans + Accounts + Other + Rent + Wages;
    }

    /// <summary>
    /// The game's own figures the economy reads from the save when asked, never per frame: what leaves tonight, the
    /// player's businesses and their statements, what accounts are secured on, and net worth. Only look-ups are kept.
    /// </summary>
    public static class Books
    {
        private const string EmptyType = "ba:businesstype_empty";
        private const string Residential = "ba:buildingtype_residential";
        private const string RentFreeUntil = "tutorial_quest_get_some_sleep_objective_4";
        private const string Salaries = "ba:transactioncategory_salaryexpenses";
        private const int StatementDaysKept = 64;   // days of statement look-ups kept before they start afresh

        private static readonly List<DebtLine> Debts = new List<DebtLine>(6);
        private static readonly ListIndex<BuildingRegistration, string> BusinessKeys = new ListIndex<BuildingRegistration, string>(BusinessKey, StringComparer.Ordinal);
        private static readonly ListIndex<RealEstate, string> EstateKeys = new ListIndex<RealEstate, string>(EstateKey, StringComparer.Ordinal);
        private static readonly Dictionary<FinancialSummary, ListIndex<FinancialSummary.BusinessIncomeStatement, Address>> StatementsOf
            = new Dictionary<FinancialSummary, ListIndex<FinancialSummary.BusinessIncomeStatement, Address>>(16, ReferenceComparer<FinancialSummary>.Instance);

        /// <summary>What tonight's run takes: loans, rent, the mod's accounts, fines, and wages as last night.</summary>
        public static Obligations Tomorrow(CreditFile file)
        {
            return Tomorrow(file, SaveGameManager.Current != null ? SaveGameManager.Current.Day : 0);
        }

        /// <summary>
        /// As of a day. The hour event that opens a day comes after the game moves its day on but before its run, so it
        /// asks as of the day before.
        /// </summary>
        public static Obligations Tomorrow(CreditFile file, int today)
        {
            var o = new Obligations();
            GameInstance save = SaveGameManager.Current;
            if (save == null)
            {
                return o;
            }
            List<Loan> loans = save.Loans;
            for (int i = 0; i < loans.Count; i++)
            {
                o.Loans += GameLoans.Due(loans[i]);
                o.LoanInterest += loans[i].dailyInterest;
            }
            for (int i = 0; file != null && i < file.Accounts.Count; i++)
            {
                o.Accounts += file.Accounts[i].Test ? 0 : AccountRules.DueTomorrow(file.Accounts[i], today);
            }
            if (file != null && file.Relief.Arranged)
            {
                o.Accounts += file.Relief.ArrangedPerDay;
            }
            Debts.Clear();
            EconomyBridges.OtherDebts?.Invoke(Debts);
            for (int i = 0; i < Debts.Count; i++)
            {
                o.Other += Mathf.Min(Debts[i].PerDay, Debts[i].Owed);
            }
            bool homeRentFree = !TutorialHelper.HasCompletedObjective(RentFreeUntil);
            List<BuildingRegistration> registrations = save.BuildingRegistrations;
            for (int i = 0; i < registrations.Count; i++)
            {
                BuildingRegistration r = registrations[i];
                if (GoingPublic.Mine(r) && !r.BuildingOwnedByPlayer && r.RentPerDay > 0f && (!homeRentFree || r.GetBuildingType() != Residential))
                {
                    o.Rent += r.RentPerDay;
                }
            }
            o.Rent = CityEconomyDirector.RentWithCity(o.Rent);   // the city's change to the night's rent, booked the next morning
            List<Transaction> paid = GameBooks.Day(today - 1);   // last night's run, filed under the day it ended
            for (int i = 0; i < paid.Count; i++)
            {
                Transaction t = paid[i];
                if (t.transactionCategories != null && t.transactionCategories.Contains(Salaries))
                {
                    o.Wages -= t.amount;
                }
            }
            return o;
        }

        /// <summary>
        /// The game's own wealth figures (it walks vehicles, boats and buildings); false, with cash alone as the wealth, if
        /// they cannot be read.
        /// </summary>
        public static bool Wealth(out PersonalWealthData wealth)
        {
            try
            {
                wealth = PlayerHelper.GetPersonalWealth();
                return true;
            }
            catch (Exception)
            {
                wealth = default(PersonalWealthData);
                wealth.cash = SaveGameManager.Current != null ? SaveGameManager.Current.Money : 0f;
                return false;
            }
        }

        /// <summary>
        /// Net worth as the Overview shows it: the game's figure (cash alone if it cannot be read) less the mod's accounts,
        /// the private lender, fines, court debts and an approval's money, which goes back. Shares are left to the market,
        /// which adds its own (Wiring/MarketLinks).
        /// </summary>
        public static long NetWorth()
        {
            GameInstance save = SaveGameManager.Current;
            if (save == null)
            {
                return 0L;
            }
            Wealth(out PersonalWealthData wealth);
            float worth = wealth.CurrentWealth;
            CreditFile file = CreditLedger.File;
            float owed = file != null && file.Relief.HasApproval ? file.Relief.ApprovedAmount : 0f;
            for (int i = 0; file != null && i < file.Accounts.Count; i++)
            {
                owed += Owing(file.Accounts[i]) ? file.Accounts[i].Owed : 0f;
            }
            if (file != null && file.Shark.Active)
            {
                owed += file.Shark.Owed;
            }
            Debts.Clear();
            EconomyBridges.OtherDebts?.Invoke(Debts);
            for (int i = 0; i < Debts.Count; i++)
            {
                owed += Debts[i].Owed;
            }
            return (long)Math.Floor(worth - owed);
        }

        /// <summary>Owed in the Overview and <see cref="NetWorth"/>: open, not a test's, and a card or overdraft only if drawn on.</summary>
        public static bool Owing(CreditAccount a)
        {
            return a != null && !a.Closed && !a.Test && (a.Owed > 0 || !ProductCatalogue.Revolving(a.Kind));
        }

        /// <summary>The businesses the player runs that take money: rented, named, of a type the game says generates revenue.</summary>
        public static void Businesses(List<BuildingRegistration> into)
        {
            into.Clear();
            GameInstance save = SaveGameManager.Current;
            List<BuildingRegistration> all = save != null ? save.BuildingRegistrations : null;
            for (int i = 0; all != null && i < all.Count; i++)
            {
                if (IsBusiness(all[i]))
                {
                    into.Add(all[i]);
                }
            }
        }

        public static bool IsBusiness(BuildingRegistration r)
        {
            try
            {
                if (r == null || !GoingPublic.Mine(r) || !r.HasEstablishedBusiness || string.IsNullOrEmpty(r.businessTypeName) || r.businessTypeName == EmptyType || !r.HasValidAddress)
                {
                    return false;
                }
                BusinessType type = BusinessTypeHelper.GetData(r);
                return type != null && type.HasTag(TagRef.Businesstag.generatesrevenue);
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>Average daily profit over the last days, a day without a statement as nothing; and how many had one.</summary>
        public static float AverageProfit(Address address, int days, out int onRecord)
        {
            onRecord = 0;
            GameInstance save = SaveGameManager.Current;
            if (save == null || save.financialSummaries == null || address == null)
            {
                return 0f;
            }
            float sum = 0f;
            int from = save.Day - days;
            List<FinancialSummary> summaries = save.financialSummaries;
            for (int i = summaries.Count - 1; i >= 0; i--)
            {
                FinancialSummary day = summaries[i];
                if (day == null || day.dayNumber < from || day.dayNumber >= save.Day || day.businessIncomeStatements == null)
                {
                    continue;
                }
                FinancialSummary.BusinessIncomeStatement statement = StatementOf(day, address);
                if (statement != null)
                {
                    sum += statement.TotalProfit;
                    onRecord++;
                }
            }
            return onRecord > 0 ? sum / days : 0f;
        }

        /// <summary>What a business took yesterday, from the game's statement of that day; nothing if it has none.</summary>
        public static float SalesYesterday(Address address)
        {
            GameInstance save = SaveGameManager.Current;
            if (save == null || save.financialSummaries == null || address == null)
            {
                return 0f;
            }
            List<FinancialSummary> summaries = save.financialSummaries;
            for (int i = summaries.Count - 1; i >= 0; i--)
            {
                FinancialSummary day = summaries[i];
                if (day == null || day.dayNumber != save.Day - 1 || day.businessIncomeStatements == null)
                {
                    continue;
                }
                FinancialSummary.BusinessIncomeStatement statement = StatementOf(day, address);
                if (statement != null)
                {
                    return Mathf.Max(0f, statement.TotalSales);
                }
            }
            return 0f;
        }

        // The first at the address by the game's own Address ==, from the day's index; the game's empty address, which
        // also matches a statement without one, is walked for.
        private static FinancialSummary.BusinessIncomeStatement StatementOf(FinancialSummary day, Address address)
        {
            List<FinancialSummary.BusinessIncomeStatement> statements = day.businessIncomeStatements;
            if (string.IsNullOrEmpty(address.streetName) && address.streetNumber == 0)
            {
                for (int b = 0; b < statements.Count; b++)
                {
                    if (statements[b] != null && statements[b].Address == address)
                    {
                        return statements[b];
                    }
                }
                return null;
            }
            if (!StatementsOf.TryGetValue(day, out ListIndex<FinancialSummary.BusinessIncomeStatement, Address> index))
            {
                if (StatementsOf.Count >= StatementDaysKept)
                {
                    StatementsOf.Clear();
                }
                index = new ListIndex<FinancialSummary.BusinessIncomeStatement, Address>(StatementKey);
                StatementsOf.Add(day, index);
            }
            return index.First(statements, address, 0);
        }

        private static Address StatementKey(FinancialSummary.BusinessIncomeStatement statement)
        {
            return statement.Address;
        }

        /// <summary>A building the player owns, by the key a mortgage keeps; null if it is not owned (any more).</summary>
        public static RealEstate Estate(string key)
        {
            GameInstance save = SaveGameManager.Current;
            List<RealEstate> owned = save != null ? save.realEstate : null;
            return owned != null && key != null ? EstateKeys.First(owned, key, save.Day) : null;
        }

        private static string EstateKey(RealEstate owned)
        {
            return owned.address != null ? Key(owned.address) : null;
        }

        /// <summary>A business of the player's by its key; null if they do not run it (any more).</summary>
        public static BuildingRegistration Business(string key)
        {
            GameInstance save = SaveGameManager.Current;
            List<BuildingRegistration> all = save != null ? save.BuildingRegistrations : null;
            BuildingRegistration first = all != null && key != null ? BusinessKeys.First(all, key, save.Day) : null;
            if (first != null && GoingPublic.Mine(first) && first.HasValidAddress)
            {
                return first;
            }
            // None under the key, another player's, or rented since the look-up was made.
            for (int i = 0; all != null && i < all.Count; i++)
            {
                if (all[i] != null && GoingPublic.Mine(all[i]) && all[i].HasValidAddress && Key(all[i].Address) == key)
                {
                    return all[i];
                }
            }
            return null;
        }

        private static string BusinessKey(BuildingRegistration reg)
        {
            return reg.RentedByPlayer && reg.HasValidAddress ? Key(reg.Address) : null;
        }

        /// <summary>A vehicle of the player's by its id; null if they do not have it (any more).</summary>
        public static VehicleInstance Vehicle(string id)
        {
            GameInstance save = SaveGameManager.Current;
            List<VehicleInstance> all = save != null ? save.VehicleInstances : null;
            for (int i = 0; all != null && i < all.Count; i++)
            {
                if (all[i] != null && all[i].id == id)
                {
                    return all[i];
                }
            }
            return null;
        }

        /// <summary>An address as an account keeps it: the street as the credit file writes one, and the number.</summary>
        public static string Key(Address address)
        {
            return address == null ? string.Empty : CreditStore.Clean(address.streetName).Replace('#', ' ') + "#" + address.streetNumber;
        }

        /// <summary>The newest transaction of a type since a day whose data says a thing by this name; null if there is none.</summary>
        public static Transaction Newest(string type, string dataKey, string dataValue, int sinceDay)
        {
            GameInstance save = SaveGameManager.Current;
            Transaction found = null;
            if (save == null || save.Transactions == null)
            {
                return null;
            }
            foreach (Transaction t in save.Transactions)
            {
                if (t == null || t.transactionType != type || t.timestamp.Day < sinceDay)
                {
                    continue;
                }
                string value;
                if (dataValue == null || (t.transactionData != null && t.transactionData.TryGetValue(dataKey, out value) && value == dataValue))
                {
                    found = t;   // the game keeps them oldest first: the last match is the newest
                }
            }
            return found;
        }
    }
}
