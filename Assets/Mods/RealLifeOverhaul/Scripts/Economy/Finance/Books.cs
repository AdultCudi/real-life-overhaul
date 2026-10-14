using System;
using System.Collections.Generic;
using BigAmbitions.Tags;
using Entities;
using Helpers;
using Streets;
using UnityEngine;

namespace RealLifeOverhaul.Economy
{
    /// <summary>What leaves tomorrow at the day's end, as the Finance app's Overview adds it up.</summary>
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
    /// The game's own figures that the economy reads: what leaves tomorrow, which businesses the player runs and what
    /// each has made, what a building the player owns brings in. Every figure is read from the save when it is asked for
    /// (when the app opens, once an hour before the day's run, once a day), never per frame. What is kept is only where to
    /// find things (Core/LookupRules ListIndex): a business and a building by the key an account keeps, made again when
    /// the save's lists change or the day turns; a business's statement in a day's statements, made again when that day's
    /// list changes. The same answers as a walk of the save's lists (a business found by its key is the player's still,
    /// or the walk is made), a look-up each: Flare and Standing ask for every business's yesterday and week, the credit
    /// desk for every secured loan each hour.
    /// </summary>
    public static class Books
    {
        private const string EmptyType = "ba:businesstype_empty";
        private const string Residential = "ba:buildingtype_residential";
        private const string RentFreeUntil = "tutorial_quest_get_some_sleep_objective_4";
        private const string Salaries = "ba:transactioncategory_salaryexpenses";
        private const int StatementDaysKept = 64;   // days of statements whose look-ups are kept at once (more than any reader asks for; then they start afresh)

        private static readonly List<DebtLine> Debts = new List<DebtLine>(6);
        private static readonly ListIndex<BuildingRegistration, string> BusinessKeys = new ListIndex<BuildingRegistration, string>(BusinessKey, StringComparer.Ordinal);
        private static readonly ListIndex<RealEstate, string> EstateKeys = new ListIndex<RealEstate, string>(EstateKey, StringComparer.Ordinal);
        private static readonly Dictionary<FinancialSummary, ListIndex<FinancialSummary.BusinessIncomeStatement, Address>> StatementsOf
            = new Dictionary<FinancialSummary, ListIndex<FinancialSummary.BusinessIncomeStatement, Address>>(16, ReferenceComparer<FinancialSummary>.Instance);

        /// <summary>
        /// What leaves at the next day's end: the loans as the game's run takes them (LoanHelper.RunDaily), the mod's
        /// accounts, fines and court debts, rent as the game's run takes it (BusinessHelper.RunDaily: every rented
        /// building the player does not own) and wages as the game paid them yesterday (it works them out as it pays).
        /// </summary>
        public static Obligations Tomorrow(CreditFile file)
        {
            return Tomorrow(file, SaveGameManager.Current != null ? SaveGameManager.Current.Day : 0);
        }

        /// <summary>
        /// The same as of a given day. The hour event that opens a new day comes after the game has moved its day on
        /// and before its run, so what "leaves tonight" there is asked as of the day before.
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
            List<Transaction> paid = GameBooks.Day(today - 1);   // (the day's transactions, as the morning's readers share them)
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

        /// <summary>
        /// A business's average daily profit over the last days of the game's own daily statements (the figures
        /// EconoView's business page shows), and on how many of those days it has a statement.
        /// </summary>
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

        /// <summary>
        /// A business's statement in one day's statements: the first whose address is the business's (the game's own
        /// test, Address ==), as a walk along them finds it; null if it has none. A look-up in that day's index; an address
        /// that is the game's "no address" (which a statement with none matches) is walked for.
        /// </summary>
        public static FinancialSummary.BusinessIncomeStatement StatementOf(FinancialSummary day, Address address)
        {
            List<FinancialSummary.BusinessIncomeStatement> statements = day != null ? day.businessIncomeStatements : null;
            if (statements == null || address == null)
            {
                return null;
            }
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

        /// <summary>
        /// A business of the player's by its key; null if they do not run it (any more). The first business rented by the
        /// player under the key, if it is the player's own; otherwise (none listed, another player's, rented since the list
        /// was made) the walk of the save's buildings, as it always was.
        /// </summary>
        public static BuildingRegistration Business(string key)
        {
            GameInstance save = SaveGameManager.Current;
            List<BuildingRegistration> all = save != null ? save.BuildingRegistrations : null;
            BuildingRegistration first = all != null && key != null ? BusinessKeys.First(all, key, save.Day) : null;
            if (first != null && GoingPublic.Mine(first) && first.HasValidAddress)
            {
                return first;
            }
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
