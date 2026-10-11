using System.Collections.Generic;
using Helpers;
using UnityEngine;

namespace RealLifeOverhaul.Economy
{
    /// <summary>What one bank holds of the borrower's, added up: the figures a bank app shows and the self-check compares.</summary>
    public struct BankSlice
    {
        public int Debts;           // accounts and loans with something on them
        public float Owed;          // what is owed on them (a test tool's account is in no total)
        public float Tomorrow;      // what they take at the next day's end
    }

    // The credit desk as the bank apps ask for it (Economy/Banks): which debts are one bank's, paying a game loan off
    // in part or whole, and one bank's figures added up. Every figure is the same record the Finance app reads, so a
    // change made in one shows in the other at once: nothing here keeps a balance of its own.
    public sealed partial class CreditDesk
    {
        private const string LoanPayOff = "ba:transaction_loanpayoff";   // the game's own, as EconoView pays a loan off
        private const string LoanPaidEvent = "ba:gameevent_loanpaid";

        /// <summary>
        /// The limit of an overdraft or a card, or the most put into a vehicle or a building, at one bank at a rating:
        /// the figure <see cref="Offer(ProductKind, Lender)"/> offers, and the one a bank app's Rating tab shows for
        /// other ratings. Nothing under the lending threshold, at every bank.
        /// </summary>
        public static int LimitAt(ProductKind kind, int score, Lender bank)
        {
            if (bank == null)
            {
                return 0;
            }
            switch (kind)
            {
                case ProductKind.Overdraft:
                    return BankRules.Limit(ProductTerms.OverdraftLimit(score, BankTerms.OwnCapTotal), bank.Rank, ProductCatalogue.OverdraftRound, ProductCatalogue.OverdraftLeast);
                case ProductKind.CreditCard:
                    return BankRules.Limit(ProductTerms.CardLimit(score, BankTerms.OwnCapTotal), bank.Rank, ProductCatalogue.CardRound, ProductCatalogue.CardLeast);
                case ProductKind.CarFinance:
                case ProductKind.Mortgage:
                    return BankRules.Most(ProductTerms.FinanceMost(kind, score, BankTerms.OwnCap(bank.Settings)), bank.Rank);
                default:
                    return 0;
            }
        }

        /// <summary>
        /// The rate a kind of the mod's credit is agreed at with one bank at a rating, in hundredths of a per cent a year:
        /// the bank's rate for the rating, the product's points and the bank's own (BankRules), the game's multiplier.
        /// An overdraft and a card follow the central bank's rate day by day on top (CreditAccount.RateNow); a purchase on
        /// finance takes it once, here.
        /// </summary>
        public static int AgreedAt(ProductKind kind, int score, Lender bank)
        {
            if (bank == null)
            {
                return 0;
            }
            int points = BankRules.Points(ProductTerms.Points(kind), bank.Rank);
            return ProductCatalogue.Revolving(kind)
                ? ProductTerms.RateBp(score, BankTerms.OwnRate(bank.Settings), points, Multiplier)
                : ProductTerms.FixedRateBp(score, BankTerms.OwnRate(bank.Settings), points, Multiplier);
        }

        /// <summary>The rate of a kind at one bank at a rating as it would be charged today (the central bank's rate on an overdraft and a card).</summary>
        public static int RateAt(ProductKind kind, int score, Lender bank)
        {
            int agreed = AgreedAt(kind, score, bank);
            return ProductCatalogue.Revolving(kind) ? ProductTerms.WithBase(agreed, AccountRules.BaseBp, AccountRules.BaseMultiplier) : agreed;
        }

        /// <summary>
        /// Every debt held with one bank: the mod's accounts it opened (the address on the account) and the game's
        /// loans made at any of its branches (the loan's own bank address). The list is the caller's and is used again.
        /// Closed accounts are left out; a test tool's account is in, as the Finance app lists it.
        /// </summary>
        public void Held(Lender bank, List<Debt> into)
        {
            into.Clear();
            if (_file == null || bank == null)
            {
                return;
            }
            for (int i = 0; i < _file.Accounts.Count; i++)
            {
                CreditAccount a = _file.Accounts[i];
                if (!a.Closed && BankTerms.IsBank(bank, a.Street, a.Number))
                {
                    into.Add(new Debt(a));
                }
            }
            for (int i = 0; i < _file.Loans.Count; i++)
            {
                LoanMark m = _file.Loans[i];
                if (m.Ref is Loan && BankTerms.IsBank(bank, m.Street, m.Number))
                {
                    into.Add(new Debt(m));
                }
            }
        }

        /// <summary>One bank's figures, from <see cref="Held"/>: what the app's Loans tab adds up.</summary>
        public BankSlice Slice(Lender bank, List<Debt> work)
        {
            Held(bank, work);
            var slice = new BankSlice();
            int today = Today;
            for (int i = 0; i < work.Count; i++)
            {
                Debt d = work[i];
                if (d.Account != null)
                {
                    if (d.Account.Test || d.Account.Owed <= 0)
                    {
                        continue;
                    }
                    slice.Debts++;
                    slice.Owed += d.Account.Owed;
                    slice.Tomorrow += AccountRules.DueTomorrow(d.Account, today);
                    continue;
                }
                Loan loan = d.Loan;
                slice.Debts++;
                slice.Owed += Mathf.Max(0f, loan.remainingAmount);
                slice.Tomorrow += (d.Mark.Held & LoanHold.Frozen) != 0 ? 0f : GameLoans.Due(loan);
            }
            return slice;
        }

        /// <summary>
        /// Money paid off one of the game's loans, exactly as the game's own loans page does it (LoanEntryUi.PayOffLoan):
        /// the game's pay-off transaction under the bank's name, the amount off what is left, and a loan with nothing left
        /// taken out of the game's list with the game's own event. The credit file then reads the change as it reads one
        /// made in EconoView. Not while the loan is in a debt arrangement: that pays it.
        /// </summary>
        public ProductAnswer PayLoan(LoanMark m, int amount, out int used)
        {
            used = 0;
            var loan = m != null ? m.Ref as Loan : null;
            if (_file == null || loan == null || (m.Held & LoanHold.Frozen) != 0 || Save == null)
            {
                return ProductAnswer.Off;
            }
            int left = Mathf.CeilToInt(Mathf.Max(0f, loan.remainingAmount));
            amount = Mathf.Min(amount, left);
            if (amount <= 0)
            {
                return ProductAnswer.NothingOwed;
            }
            string bank = GameLoans.BankName(loan);
            // (a dictionary each: the game keeps hold of a transaction's data for as long as it lists the transaction)
            var info = new TransactionInfo(LoanPayOff, new Dictionary<string, string> { { "businessName", bank } });
            if (!GameManager.ChangeMoneySafe(-amount, info, null, null, false, true))
            {
                return ProductAnswer.NoCash;
            }
            float before = loan.remainingAmount;
            loan.remainingAmount -= amount;
            used = amount;
            bool paidOff = loan.remainingAmount <= 0f;
            if (paidOff)
            {
                Save.Loans.Remove(loan);
                GameEvent.Invoke(LoanPaidEvent);
            }
            _director.BringUpToDate("a payment from " + bank + "'s app");   // the file reads the loan as it reads a payment made in EconoView
            if (Verbose)
            {
                Logger?.Info("Bank check (loan payment): " + amount + " paid off the loan of " + m.Total.ToString("0") + " at " + bank + " with the game's own pay-off: " + before.ToString("0") + " left before, "
                             + Mathf.Max(0f, loan.remainingAmount).ToString("0") + " after" + (paidOff ? ", paid off and out of the game's list" : string.Empty) + ": "
                             + (Mathf.Abs(before - amount - loan.remainingAmount) < 0.01f && (!paidOff || !Save.Loans.Contains(loan)) ? "PASS" : "FAIL"));
            }
            return ProductAnswer.Yes;
        }
    }
}
