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
    }

    /// <summary>
    /// A loan arranged from the phone. What is made is one of the game's own loans (Loan, in the save's list) with a
    /// real bank's address, made exactly as the bank's dialogue makes one (BankDialog.LoanRequest): the same figures
    /// from the same formulas (LoanHelper), the same payout transaction, the same game event. So EconoView lists it,
    /// the game's daily run collects it, and it is still there with the mod removed. The one difference is the
    /// arrangement fee, taken at once (the bank in person charges none).
    /// </summary>
    public static class PhoneLending
    {
        public const string FeeTransaction = "reallife_transaction_loan_fee";
        private const string PayoutTransaction = "ba:transaction_loanpayout";       // the game's own
        private const string LoanExpenses = "ba:transactioncategory_loanexpenses";  // the game's own category, so its summaries count the fee
        private const string NewLoanEvent = "ba:gameevent_newloan";

        /// <summary>The most this lender will lend the player now: its cap for this rating less what it is owed, and no more than the game's own means test allows.</summary>
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
            float multiplier = SaveGameManager.Current != null && SaveGameManager.Current.gameVariables != null ? SaveGameManager.Current.gameVariables.bankInterestMultiplier : 1f;
            return BankTerms.Wanted(lender.Settings, score).AnnualRate * multiplier;
        }

        /// <summary>
        /// The figures for an amount. They are the game's (LoanHelper.CalculateDailyInterestPayment and
        /// CalculateDailyPayment read the bank's settings in memory), so the terms for this rating are written on the
        /// banks first: what is quoted is what the bank in person would quote at this moment.
        /// </summary>
        public static LoanQuote Quote(Lender lender, int amount, int score)
        {
            BankTerms.Apply(score);
            var quote = new LoanQuote
            {
                Amount = amount,
                AnnualRate = LoanHelper.CalculateEffectiveAnnualInterestRate(lender.Settings),
                DailyInterest = LoanHelper.CalculateDailyInterestPayment(amount, lender.Settings),
                DailyPayment = LoanHelper.CalculateDailyPayment(amount, lender.Settings),
                Fee = LendingTerms.PhoneFee(amount)
            };
            quote.Days = quote.DailyPayment > 0 ? Mathf.CeilToInt(amount / (float)quote.DailyPayment) : 0;
            return quote;
        }

        /// <summary>
        /// Lends, if the rating and the lender's limit allow it. The loan is added to the game's list, the amount is
        /// paid out and the fee taken; the caller writes the credit file.
        /// </summary>
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
            loan = new Loan(amount, quote.DailyInterest, quote.DailyPayment, lender.Address);
            save.Loans.Add(loan);
            GameManager.ChangeMoneySafe(amount, new TransactionInfo(PayoutTransaction, new Dictionary<string, string> { { "businessName", lender.Name } }), null, null, true);
            // (a dictionary each: the game keeps hold of a transaction's data for as long as it lists the transaction)
            GameManager.ChangeMoneySafe(-quote.Fee, new TransactionInfo(FeeTransaction, LoanExpenses, new Dictionary<string, string> { { "businessName", lender.Name } }), null, null, true);
            GameEvent.Invoke(NewLoanEvent);
            return LoanAnswer.Approved;
        }
    }
}
