using System.Collections.Generic;
using Entities;
using Helpers;
using SpecialServices.Bank;
using Streets;
using UnityEngine;

namespace RealLifeOverhaul.Economy
{
    /// <summary>The game's loans (SaveGameManager.Current.Loans) as a credit account. It reads the game's list and keeps nothing.</summary>
    public sealed class GameLoans : ICreditAccount
    {
        public const string AccountKey = "loans";

        public string Key => AccountKey;
        public string NameKey => "reallife_econ_account_loans";

        public float Balance
        {
            get
            {
                List<Loan> loans = Loans;
                float sum = 0f;
                for (int i = 0; loans != null && i < loans.Count; i++)
                {
                    sum += Mathf.Max(0f, loans[i].remainingAmount);
                }
                return sum;
            }
        }

        /// <summary>What every bank of the city together would lend on its own terms.</summary>
        public float Limit => BankTerms.OwnCapTotal;

        public float PaymentDue
        {
            get
            {
                List<Loan> loans = Loans;
                float sum = 0f;
                for (int i = 0; loans != null && i < loans.Count; i++)
                {
                    sum += Due(loans[i]);
                }
                return sum;
            }
        }

        private static List<Loan> Loans => SaveGameManager.Current != null ? SaveGameManager.Current.Loans : null;

        /// <summary>What the game's daily run takes from a loan next: its interest and its payment, or what is left (LoanHelper.RunDaily).</summary>
        public static float Due(Loan loan)
        {
            return loan.dailyInterest + Mathf.Min(loan.dailyPayment, Mathf.Max(0f, loan.remainingAmount));
        }

        public static void Read(List<LoanView> into)
        {
            into.Clear();
            List<Loan> loans = Loans;
            for (int i = 0; loans != null && i < loans.Count; i++)
            {
                Loan loan = loans[i];
                if (loan == null)
                {
                    continue;
                }
                Address bank = loan.bankAddress;
                into.Add(new LoanView
                {
                    Ref = loan,
                    Street = bank != null ? CreditStore.Clean(bank.streetName) : string.Empty,
                    Number = bank != null ? bank.streetNumber : 0,
                    Total = loan.totalAmount,
                    Remaining = loan.remainingAmount,
                    Payment = loan.dailyPayment,
                    Interest = loan.dailyInterest
                });
            }
        }

        /// <summary>What is owed to the bank at this address, counted as the bank itself counts it (BankDialog.GetRemainingLoanAmount).</summary>
        public static float OwedTo(Address bank)
        {
            List<Loan> loans = Loans;
            float sum = 0f;
            for (int i = 0; loans != null && i < loans.Count; i++)
            {
                if (!(loans[i].bankAddress != bank))
                {
                    sum += loans[i].remainingAmount;
                }
            }
            return sum;
        }

        /// <summary>The bank's name as the game's loan pages write it.</summary>
        public static string BankName(Loan loan)
        {
            BuildingRegistration registration = loan != null && loan.bankAddress != null ? BuildingHelper.GetBuildingRegistration(loan.bankAddress) : null;
            return registration != null && !string.IsNullOrEmpty(registration.BusinessName) ? registration.BusinessName
                : loan != null && loan.bankAddress != null ? loan.bankAddress.ToFormattedString() : string.Empty;
        }

        /// <summary>The most the borrower's means allow in new loans: BankDialog.GetEconomyMaxLoanAmount (private there), term for term.</summary>
        public static float MeansLimit(BankSettings settings)
        {
            GameInstance save = SaveGameManager.Current;
            float most = 0f;
            if (TutorialHelper.IsTutorialEnabled() && !save.CompletedQuestEntries.Contains("tutorial_quest_establish_first_business_objective_1"))
            {
                most = 15500f;
            }
            most = Mathf.Max(most, PlayerHelper.GetPersonalWealth().WealthBeforeLoans);
            most = Mathf.Max(most, PlayerHelper.CalculateDailyIncome() * LoanHelper.CalculatePayBackDays(settings) * 0.25f);
            float owed = 0f;
            for (int i = 0; i < save.Loans.Count; i++)
            {
                owed += save.Loans[i].remainingAmount;
            }
            return Mathf.FloorToInt(Mathf.Max(0f, most - owed));
        }
    }
}
