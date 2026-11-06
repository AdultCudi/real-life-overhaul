using System.Collections.Generic;
using Helpers;
using UnityEngine;

namespace RealLifeOverhaul.Economy
{
    /// <summary>What a loan of one amount from one lender costs, worked out with the game's own formulas.</summary>
    public struct LoanQuote
    {
        public int Amount;
        public float AnnualRate;    // as the bank's dialogue shows it: the lender's rate times the game's interest multiplier
        public int DailyInterest;
        public int DailyPayment;
        public int Days;            // instalments until it is paid at that daily payment
        public int Fee;

        /// <summary>What borrowing costs in all: the interest over its days, and the fee.</summary>
        public int Cost => DailyInterest * Days + Fee;

        /// <summary>The instalments until an amount is paid at a daily payment; 0 with no payment.</summary>
        public static int DaysToPay(int amount, int dailyPayment)
        {
            return dailyPayment > 0 ? Mathf.CeilToInt(amount / (float)dailyPayment) : 0;
        }
    }

    /// <summary>
    /// A loan arranged from the phone: one of the game's own loans, made as the bank's dialogue makes one (BankDialog.LoanRequest),
    /// so it stays with the mod removed. The one difference is the arrangement fee; the bank in person charges none.
    /// </summary>
    public static class PhoneLending
    {
        public const string FeeTransaction = "reallife_transaction_loan_fee";
        private const string PayoutTransaction = "ba:transaction_loanpayout";       // the game's own
        private const string NewLoanEvent = "ba:gameevent_newloan";

        /// <summary>The most this lender lends the player now: its cap for the rating less what it is owed, within the game's own means test.</summary>
        public static float MostNow(Lender lender, int score)
        {
            if (lender == null || lender.Settings == null || SaveGameManager.Current == null)
            {
                return 0f;
            }
            LenderTerms terms = BankTerms.Wanted(lender.Settings, score);
            if (!terms.Lends)
            {
                return 0f;
            }
            return LendingTerms.MostNow(terms.Cap, GameLoans.OwedTo(lender.Address), GameLoans.MeansLimit(lender.Settings));
        }

        /// <summary>The annual rate this lender offers at this rating, as the bank's dialogue would show it.</summary>
        public static float RateOffered(Lender lender, int score)
        {
            return BankTerms.Wanted(lender.Settings, score).AnnualRate * GameLoans.Multiplier;
        }

        /// <summary>The figures for an amount by the game's formulas, with the rating's terms written on the banks first: what the bank in person would quote now.</summary>
        public static LoanQuote Quote(Lender lender, int amount, int score)
        {
            BankTerms.Apply(score);
            var quote = new LoanQuote
            {
                Amount = amount,
                AnnualRate = LoanHelper.CalculateEffectiveAnnualInterestRate(lender.Settings),
                DailyInterest = LoanHelper.CalculateDailyInterestPayment(amount, lender.Settings),
                DailyPayment = LoanHelper.CalculateDailyPayment(amount, lender.Settings),
                Fee = BankRules.Fee(amount, lender.Rank)
            };
            quote.Days = LoanQuote.DaysToPay(amount, quote.DailyPayment);
            return quote;
        }

        /// <summary>Lends if the rating and the lender's limit allow: the loan goes in the game's list, the amount is paid out and the fee taken. The caller writes the credit file.</summary>
        public static LoanAnswer Take(Lender lender, int amount, int score, out LoanQuote quote, out Loan loan)
        {
            quote = default(LoanQuote);
            loan = null;
            GameInstance save = SaveGameManager.Current;
            if (save == null || lender == null || lender.Settings == null || lender.Address == null)
            {
                return LoanAnswer.BelowThreshold;
            }
            LoanAnswer answer = LendingTerms.Decide(score, amount, MostNow(lender, score));
            if (answer != LoanAnswer.Approved)
            {
                return answer;
            }
            quote = Quote(lender, amount, score);
            loan = Make(lender, amount, quote.DailyInterest, quote.DailyPayment, quote.Fee);
            return LoanAnswer.Approved;
        }

        /// <summary>Makes one of the game's loans with these figures, as the bank's dialogue does, with the arrangement fee. The caller decides whether it may.</summary>
        public static Loan Make(Lender lender, int amount, int dailyInterest, int dailyPayment, int fee)
        {
            var loan = new Loan(amount, dailyInterest, dailyPayment, lender.Address);
            SaveGameManager.Current.Loans.Add(loan);
            // a dictionary each: the game keeps a transaction's data for as long as it lists the transaction
            Economy.BizBooks.Book(amount, new TransactionInfo(PayoutTransaction, new Dictionary<string, string> { { "businessName", lender.Name } }), null, null, true);
            Economy.BizBooks.Book(-fee, new TransactionInfo(FeeTransaction, GameLoans.ExpenseCategory, new Dictionary<string, string> { { "businessName", lender.Name } }), null, null, true);
            GameEvent.Invoke(NewLoanEvent);
            return loan;
        }
    }
}
