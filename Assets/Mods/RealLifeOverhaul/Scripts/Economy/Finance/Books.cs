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
    /// each has made, what a building the player owns brings in. Nothing here is kept: every figure is read from the
    /// save when it is asked for (when the app opens, once an hour before the day's run, once a day), never per frame.
    /// </summary>
    public static class Books
    {
        private const string EmptyType = "ba:businesstype_empty";
        private const string Residential = "ba:buildingtype_residential";
        private const string RentFreeUntil = "tutorial_quest_get_some_sleep_objective_4";
        private const string Salaries = "ba:transactioncategory_salaryexpenses";

        private static readonly List<DebtLine> Debts = new List<DebtLine>(6);

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
            int yesterday = today - 1;
            foreach (Transaction t in save.Transactions)
            {
                if (t != null && t.timestamp.Day == yesterday && t.transactionCategories != null && t.transactionCategories.Contains(Salaries))
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
                for (int b = 0; b < day.businessIncomeStatements.Count; b++)
                {
                    FinancialSummary.BusinessIncomeStatement statement = day.businessIncomeStatements[b];
                    if (statement != null && statement.Address == address)
                    {
                        sum += statement.TotalProfit;
                        onRecord++;
                        break;
                    }
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
                for (int b = 0; b < day.businessIncomeStatements.Count; b++)
                {
                    if (day.businessIncomeStatements[b] != null && day.businessIncomeStatements[b].Address == address)
                    {
                        return Mathf.Max(0f, day.businessIncomeStatements[b].TotalSales);
                    }
                }
            }
            return 0f;
        }

        /// <summary>A building the player owns, by the key a mortgage keeps; null if it is not owned (any more).</summary>
        public static RealEstate Estate(string key)
        {
            GameInstance save = SaveGameManager.Current;
            List<RealEstate> owned = save != null ? save.realEstate : null;
            for (int i = 0; owned != null && i < owned.Count; i++)
            {
                if (owned[i] != null && owned[i].address != null && Key(owned[i].address) == key)
                {
                    return owned[i];
                }
            }
            return null;
        }

        /// <summary>A business of the player's by its key; null if they do not run it (any more).</summary>
        public static BuildingRegistration Business(string key)
        {
            GameInstance save = SaveGameManager.Current;
            List<BuildingRegistration> all = save != null ? save.BuildingRegistrations : null;
            for (int i = 0; all != null && i < all.Count; i++)
            {
                if (all[i] != null && GoingPublic.Mine(all[i]) && all[i].HasValidAddress && Key(all[i].Address) == key)
                {
                    return all[i];
                }
            }
            return null;
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
