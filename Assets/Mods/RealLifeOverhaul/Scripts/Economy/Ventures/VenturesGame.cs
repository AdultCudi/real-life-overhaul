using System;
using System.Collections.Generic;
using BigAmbitions.Rivals;
using Entities;
using Helpers;
using Localizor;
using PlayerActivity;
using Streets;
using UI;
using UI.Load;
using UnityEngine;

namespace RealLifeOverhaul.Economy.Ventures
{
    /// <summary>Ventures' side of the player's cash. No address goes with it: an investor's share must not show as a cost of the business.</summary>
    public sealed class VenturesWallet : IWallet
    {
        /// <summary>The day transactions are dated on (the day that ended, during the day's run, as the game dates its own); null for now.</summary>
        public int? BookDay;

        public int Cash => SaveGameManager.Current != null ? Mathf.FloorToInt(Mathf.Max(0f, SaveGameManager.Current.Money)) : 0;

        public bool Take(int amount, string why, string name)
        {
            return amount <= 0 || (SaveGameManager.Current != null && GameManager.ChangeMoneySafe(-amount, Info(why, name), BookDay, null, false));
        }

        public void Give(int amount, string why, string name)
        {
            if (amount > 0 && SaveGameManager.Current != null)
            {
                GameManager.ChangeMoneySafe(amount, Info(why, name), BookDay, null, true);
            }
        }

        // A dictionary each: the game keeps a transaction's data for as long as it lists the transaction.
        private static TransactionInfo Info(string why, string name)
        {
            return new TransactionInfo(why, new Dictionary<string, string>(1) { { "name", name ?? string.Empty } });
        }
    }

    /// <summary>What Ventures reads from the game, when asked and never per frame; nothing of the game's is changed.</summary>
    public static class VenturesGame
    {
        private static readonly List<BuildingRegistration> Scratch = new List<BuildingRegistration>(16);
        private static readonly List<EmployeeInstance> Staff = new List<EmployeeInstance>(16);
        private static readonly List<DebtLine> Debts = new List<DebtLine>(6);

        private static GameInstance Save => SaveGameManager.Current;

        /// <summary>True while the game itself holds the player. Not the pause: the phone, where a meeting is asked for, pauses the game.</summary>
        public static bool Busy()
        {
            UIs uis = InstanceBehavior<UIs>.Instance;
            GameManager game = InstanceBehavior<GameManager>.Instance;
            return uis == null || LoadScene.isLoading || GameMembers.PlayerDead || EnergyHelper.goingToHospital || (uis.timeMachine != null && uis.timeMachine.isRunning)
                   || PlayerActivityUI.IsPanelOpen || DialogController.current != null || CasinoBoatManager.IsOnCasinoBoat || SubwaySystem.IsRiding
                   || (game != null && game.selectedVehicle != null);
        }

        /// <summary>A business of the player's by the key a deal keeps; null if they do not run it.</summary>
        public static BuildingRegistration Business(string place)
        {
            BuildingRegistration r = string.IsNullOrEmpty(place) ? null : Books.Business(place);
            return r != null && Books.IsBusiness(r) ? r : null;
        }

        public static string Name(BuildingRegistration r)
        {
            return r == null ? string.Empty : r.GetDisplayName();
        }

        /// <summary>Days the player has had a business; one older than the game remembers counts from day one.</summary>
        private static int Age(BuildingRegistration r)
        {
            GameInstance save = Save;
            return save == null || r == null ? 0 : Mathf.Max(0, save.Day - Mathf.Max(1, r.creationDay));
        }

        /// <summary>A business's last fourteen days from its statements, means over the days on record; Exists false if the player does not run it.</summary>
        public static BusinessFigures Figures(BuildingRegistration r)
        {
            var b = new BusinessFigures();
            GameInstance save = Save;
            if (save == null || r == null || !Books.IsBusiness(r))
            {
                return b;
            }
            b.Exists = true;
            b.Sector = VenturesCatalogue.SectorOf(r.businessTypeName);
            b.AgeDays = Age(r);
            Address address = r.Address;
            List<FinancialSummary> summaries = save.financialSummaries;
            float sum7 = 0f, sum14 = 0f, cost14 = 0f;
            int days7 = 0;
            for (int i = summaries != null ? summaries.Count - 1 : -1; i >= 0; i--)
            {
                FinancialSummary day = summaries[i];
                int back = day != null ? save.Day - day.dayNumber : 0;
                if (back < 1 || back > VenturesCatalogue.ProfitWindowDays || day.businessIncomeStatements == null)
                {
                    continue;
                }
                for (int s = 0; s < day.businessIncomeStatements.Count; s++)
                {
                    FinancialSummary.BusinessIncomeStatement statement = day.businessIncomeStatements[s];
                    if (statement == null || statement.Address != address)
                    {
                        continue;
                    }
                    b.DaysOnRecord++;
                    b.ProfitableDays += statement.TotalProfit > 0f ? 1 : 0;
                    sum14 += statement.TotalProfit;
                    cost14 += Mathf.Max(0f, statement.TotalSales - statement.TotalProfit);
                    if (back <= VenturesCatalogue.AverageDays)
                    {
                        sum7 += statement.TotalProfit;
                        b.SalesWeek += Mathf.Max(0f, statement.TotalSales);
                        days7++;
                    }
                    if (back == 1)
                    {
                        b.HasYesterday = true;
                        b.ProfitYesterday = statement.TotalProfit;
                        b.SalesYesterday = Mathf.Max(0f, statement.TotalSales);
                    }
                    break;
                }
            }
            b.AvgProfit7 = days7 > 0 ? sum7 / days7 : 0f;
            b.AvgProfit14 = b.DaysOnRecord > 0 ? sum14 / b.DaysOnRecord : 0f;
            b.AvgCost14 = b.DaysOnRecord > 0 ? cost14 / b.DaysOnRecord : 0f;
            return b;
        }

        /// <summary>The true answers to anything an investor may ask about the business ('r' null for one not yet open) and the player.</summary>
        public static PitchFacts Facts(VenturesFile file, BuildingRegistration r, string typeName, int budget)
        {
            var f = new PitchFacts { Running = r != null, Budget = budget, Sector = VenturesCatalogue.SectorOf(r != null ? r.businessTypeName : typeName) };
            GameInstance save = Save;
            if (save == null)
            {
                return f;
            }
            if (r != null)
            {
                BusinessFigures b = Figures(r);
                f.SalesYesterday = Mathf.RoundToInt(b.SalesYesterday);
                f.SalesWeek = Mathf.RoundToInt(b.SalesWeek);
                f.ProfitYesterday = Mathf.RoundToInt(b.ProfitYesterday);
                f.ProfitWeek = Mathf.RoundToInt(b.AvgProfit7);
                f.AvgProfit7 = b.AvgProfit7;
                f.ProfitableDays = b.ProfitableDays;
                f.AgeDays = b.AgeDays;
                f.Staff = StaffAt(r);
            }
            f.Cash = Mathf.FloorToInt(Mathf.Max(0f, save.Money));
            CreditFile credit = CreditLedger.Ready ? CreditLedger.File : null;
            float owed = 0f;
            for (int i = 0; save.Loans != null && i < save.Loans.Count; i++)
            {
                owed += Mathf.Max(0f, save.Loans[i].remainingAmount);
            }
            for (int i = 0; credit != null && i < credit.Accounts.Count; i++)
            {
                owed += credit.Accounts[i].Closed || credit.Accounts[i].Test ? 0 : credit.Accounts[i].Owed;
            }
            Debts.Clear();
            EconomyBridges.OtherDebts?.Invoke(Debts);
            for (int i = 0; i < Debts.Count; i++)
            {
                owed += Debts[i].Owed;
            }
            f.Owed = Mathf.RoundToInt(owed);
            Obligations due = Books.Tomorrow(credit);
            f.Payments = Mathf.RoundToInt(due.Loans + due.Accounts);
            if (credit != null)
            {
                CreditLedger.Touch();
                f.Rating = CreditLedger.Score.Value;
                f.Missed = CreditLedger.Score.Missed;
                f.Bankruptcies = credit.Relief.BankruptDay > 0 ? 1 : 0;
            }
            f.Convictions = EconomyBridges.Legal != null ? EconomyBridges.Legal().Convictions : 0;
            Books.Businesses(Scratch);
            f.Businesses = Scratch.Count;
            for (int i = 0; i < Scratch.Count; i++)
            {
                f.OldestDays = Mathf.Max(f.OldestDays, Age(Scratch[i]));
            }
            Scratch.Clear();
            f.Jobs = VenturesBridges.JobsHeld != null ? Mathf.Max(0, VenturesBridges.JobsHeld()) : 0;
            for (int i = 0; file != null && i < file.Deals.Count; i++)
            {
                f.Backers += file.Deals[i].Stage != DealStage.Called && !file.Deals[i].Test ? 1 : 0;
            }
            return f;
        }

        private static int StaffAt(BuildingRegistration r)
        {
            Staff.Clear();
            try
            {
                EmployeeHelper.GetEmployeeInstances(new EmployeeInstancesQueryInfo { withAssignedAddress = r.Address, excludeBeingReplaced = true }, Staff);
                int count = 0;
                for (int i = 0; i < Staff.Count; i++)
                {
                    count += Staff[i] != null && !Staff[i].IsCandidate ? 1 : 0;
                }
                return count;
            }
            catch (Exception)
            {
                return -1;   // the game's list could not be read: the question is left out
            }
            finally
            {
                Staff.Clear();
            }
        }

        /// <summary>The game's named rivals, at most four; none while its rivals are off or not loaded.</summary>
        public static void RivalNames(List<string> into)
        {
            into.Clear();
            try
            {
                if (Save == null || !RivalsHelper.IsFeatureEnabled)
                {
                    return;
                }
                IReadOnlyCollection<SpecialRival> rivals = RivalsHelper.GetSpecialRivals();
                if (rivals == null)
                {
                    return;
                }
                foreach (SpecialRival rival in rivals)
                {
                    string name = rival != null && rival.rivalData != null ? rival.rivalData.rivalName : null;
                    if (!string.IsNullOrEmpty(name) && !into.Contains(name) && into.Count < VenturesCatalogue.RivalsListed)
                    {
                        into.Add(name);
                    }
                }
            }
            catch (Exception)
            {
                into.Clear();   // the rivals are not as expected: invented firms only
            }
        }

        public static void StartableTypes(List<string> into)
        {
            into.Clear();
            try
            {
                foreach (BusinessType type in BusinessTypeHelper.GetAllPlayerAvailableBusinesses())
                {
                    if (type != null && !string.IsNullOrEmpty(type.businessTypeName) && !into.Contains(type.businessTypeName))
                    {
                        into.Add(type.businessTypeName);
                    }
                }
                into.Sort((a, b) => string.CompareOrdinal(TypeName(a), TypeName(b)));
            }
            catch (Exception)
            {
                into.Clear();
            }
        }

        public static string TypeName(string businessType)
        {
            return string.IsNullOrEmpty(businessType) ? string.Empty : businessType.GetLocalization();
        }

        /// <summary>The business a promise starts on once the player runs more of its kind: the newest with no deal; null until then.</summary>
        public static BuildingRegistration Opened(VenturesFile file, Pledge pledge)
        {
            if (CountOf(pledge.TypeName) <= pledge.Had)
            {
                return null;
            }
            Books.Businesses(Scratch);
            BuildingRegistration found = null;
            int fewest = int.MaxValue;
            for (int i = 0; i < Scratch.Count; i++)
            {
                BuildingRegistration r = Scratch[i];
                int deals;
                if (r.businessTypeName != pledge.TypeName || file.ShareOn(Books.Key(r.Address), 0, out deals) > 0 || deals > 0)
                {
                    continue;
                }
                int onRecord = Figures(r).DaysOnRecord;
                if (found == null || onRecord < fewest || (onRecord == fewest && r.creationDay > found.creationDay))
                {
                    found = r;
                    fewest = onRecord;
                }
            }
            Scratch.Clear();
            return found;
        }

        public static int CountOf(string businessType)
        {
            Books.Businesses(Scratch);
            int count = 0;
            for (int i = 0; i < Scratch.Count; i++)
            {
                count += Scratch[i].businessTypeName == businessType ? 1 : 0;
            }
            Scratch.Clear();
            return count;
        }

        /// <summary>What a building has brought in over the last days, from the daily statements (kept for a year; the transactions are not).</summary>
        public static float Rent(Address address, int days)
        {
            GameInstance save = Save;
            List<FinancialSummary> summaries = save != null ? save.financialSummaries : null;
            float sum = 0f;
            for (int i = summaries != null ? summaries.Count - 1 : -1; i >= 0; i--)
            {
                FinancialSummary day = summaries[i];
                int back = day != null ? save.Day - day.dayNumber : 0;
                if (back < 1 || back > days || day.realEstateStatements == null)
                {
                    continue;
                }
                for (int s = 0; s < day.realEstateStatements.Count; s++)
                {
                    if (day.realEstateStatements[s] != null && day.realEstateStatements[s].Address == address)
                    {
                        sum += day.realEstateStatements[s].Amount;
                    }
                }
            }
            return sum;
        }
    }
}
