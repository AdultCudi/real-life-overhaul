using System.Collections.Generic;
using System.Globalization;
using Helpers;
using UnityEngine;

namespace RealLifeOverhaul.Economy
{
    /// <summary>The credit desk's loans of the game's own kind: one secured on a business, and one that consolidates every debt.</summary>
    public sealed partial class CreditDesk
    {
        private readonly List<DebtItem> _debts = new List<DebtItem>(8);

        /// <summary>What a business can secure at the house bank: so many days of its average daily profit, once it has a week on record.</summary>
        public ProductOffer Business(BuildingRegistration shop, out float profit, out int days)
        {
            return Business(shop, BankTerms.House, out profit, out days);
        }

        /// <summary>What a business can secure at one bank: the same record, that bank's share of the most and its points on the rate.</summary>
        public ProductOffer Business(BuildingRegistration shop, Lender bank, out float profit, out int days)
        {
            profit = 0f;
            days = 0;
            var offer = new ProductOffer { Answer = Gate(), Bank = bank ?? BankTerms.House };
            if (offer.Answer == ProductAnswer.Yes && offer.Bank == null)
            {
                offer.Answer = ProductAnswer.NoBank;
            }
            if (offer.Answer != ProductAnswer.Yes)
            {
                return offer;
            }
            if (shop == null || !Books.IsBusiness(shop))
            {
                offer.Answer = ProductAnswer.NoSecurity;
                return offer;
            }
            if (BankTerms.QuestHold(offer.Bank.Settings))
            {
                offer.Answer = ProductAnswer.Busy;   // one of the game's quest loans is open: the bank lends on its own terms only
                return offer;
            }
            string key = Books.Key(shop.Address);
            for (int i = 0; i < _file.Loans.Count; i++)
            {
                if (_file.Loans[i].Purpose == LoanPurpose.Business && _file.Loans[i].Security == key)
                {
                    offer.Answer = ProductAnswer.AlreadyOpen;
                    return offer;
                }
            }
            profit = Books.AverageProfit(shop.Address, ProductCatalogue.BusinessDaysOnRecord, out days);
            int score = CreditLedger.Value;
            offer.Most = BankRules.Most(ProductTerms.BusinessMost(score, profit), offer.Bank.Rank);
            offer.RateBp = Mathf.RoundToInt(BusinessRate(score, offer.Bank) * Multiplier * 100f);
            if (days < ProductCatalogue.BusinessDaysOnRecord || offer.Most < CreditCatalogue.LoanLeast)
            {
                offer.Answer = ProductAnswer.NoSecurity;
            }
            return offer;
        }

        // The whole-point rate of a business loan at a bank: the rating's less a point, with the bank's own points, never under the floor.
        private static int BusinessRate(int score, Lender bank)
        {
            return Mathf.Max(CreditCatalogue.RateFloor, ProductTerms.BusinessRate(score, BankTerms.OwnRate(bank.Settings)) + BankRules.Of(bank.Rank).RatePoints);
        }

        /// <summary>What a business loan of an amount costs: the game's own figures at the business loan's rate.</summary>
        public LoanQuote BusinessQuote(Lender bank, int amount)
        {
            int score = CreditLedger.Value;
            BankTerms.Apply(score);
            int rate = BusinessRate(score, bank);
            var quote = new LoanQuote
            {
                Amount = amount,
                AnnualRate = rate * Multiplier,
                DailyInterest = GameLoanMath.DailyInterest(amount, rate, Multiplier, bank.Settings.yearsToPayLoan, AccountRules.DaysPerYear),
                DailyPayment = LoanHelper.CalculateDailyPayment(amount, bank.Settings),
                Fee = BankRules.Fee(amount, bank.Rank)
            };
            quote.Days = LoanQuote.DaysToPay(amount, quote.DailyPayment);
            return quote;
        }

        /// <summary>Lends against a business from the house bank. The loan is the game's own; the mod remembers which business secures it.</summary>
        public ProductAnswer BorrowOnBusiness(BuildingRegistration shop, int amount, out LoanQuote quote)
        {
            return BorrowOnBusiness(shop, BankTerms.House, amount, out quote);
        }

        /// <summary>Lends against a business from one bank, on its terms.</summary>
        public ProductAnswer BorrowOnBusiness(BuildingRegistration shop, Lender bank, int amount, out LoanQuote quote)
        {
            quote = default(LoanQuote);
            float profit;
            int days;
            ProductOffer offer = Business(shop, bank, out profit, out days);
            if (offer.Answer != ProductAnswer.Yes)
            {
                return offer.Answer;
            }
            if (amount < CreditCatalogue.LoanLeast)
            {
                return ProductAnswer.TooLittle;
            }
            if (amount > offer.Most)
            {
                return ProductAnswer.TooMuch;
            }
            int day = Today;
            quote = BusinessQuote(offer.Bank, amount);
            Loan loan = PhoneLending.Make(offer.Bank, amount, quote.DailyInterest, quote.DailyPayment, quote.Fee);
            LoanMark mark = _director.Adopt(loan);
            if (mark != null)
            {
                mark.Purpose = LoanPurpose.Business;
                mark.Security = Books.Key(shop.Address);
            }
            CreditLedger.Post(CreditKind.Application, amount, day, "business");
            CreditLedger.Post(CreditKind.NewCredit, amount, day, "business");
            if (Verbose)
            {
                Logger?.Info("Economy check (business loan): " + amount + " from " + offer.Bank.Name + " against " + shop.BusinessName + " (average profit " + profit.ToString("0") + " a day over "
                             + days
                             + " day(s) on record, so " + offer.Most + " at most) at " + quote.AnnualRate.ToString("0.#", CultureInfo.InvariantCulture) + "% a year: " + quote.DailyPayment
                             + " a day and " + quote.DailyInterest + " interest, fee " + quote.Fee + ": " + (mark != null ? "PASS" : "FAIL"));
            }
            _director.TermsChanged("a business loan");   // the bank's cap in memory rises by the loan: it does not use up what the bank lends unsecured
            return ProductAnswer.Yes;
        }

        /// <summary>
        /// What one new loan from this bank in place of every loan, the card and the overdraft would come to. Busy while one of the
        /// game's quest loans is open, the bankruptcy quest's at whichever bank: the game's own story ends that loan.
        /// </summary>
        public ProductAnswer Consolidation(Lender bank, out DebtTotals before, out DebtTotals after, out LoanQuote quote, out int count)
        {
            before = default(DebtTotals);
            after = default(DebtTotals);
            quote = default(LoanQuote);
            count = 0;
            ProductAnswer gate = Gate();
            if (gate != ProductAnswer.Yes || bank == null || Save == null)
            {
                return gate == ProductAnswer.Yes ? ProductAnswer.NoBank : gate;
            }
            if (BankTerms.QuestHold(bank.Settings) || GameBankruptcy)
            {
                return ProductAnswer.Busy;   // a quest loan is open: at this bank, or the game's bankruptcy emergency loan at any, which consolidating would pay off
            }
            _debts.Clear();
            List<Loan> loans = Save.Loans;
            for (int i = 0; i < loans.Count; i++)
            {
                _debts.Add(new DebtItem
                {
                    Owed = WholeMoney.Round(System.Math.Ceiling((double)loans[i].remainingAmount)), DailyInterest = loans[i].dailyInterest, DailyPayment = loans[i].dailyPayment
                });
            }
            for (int i = 0; i < _file.Accounts.Count; i++)
            {
                CreditAccount a = _file.Accounts[i];
                if (!a.Closed && !a.Test && ProductCatalogue.Revolving(a.Kind) && a.Owed > 0)
                {
                    _debts.Add(new DebtItem { Owed = a.Owed, DailyInterest = AccountRules.InterestADay(a.Owed, a.RateNow, AccountRules.DaysPerYear) });
                }
            }
            count = _debts.Count;
            int total = 0;
            for (int i = 0; i < _debts.Count; i++)
            {
                total += _debts[i].Owed;
            }
            int score = CreditLedger.Value;
            ProductAnswer answer = ProductTerms.Consolidate(score, total, LendingTerms.For(score, BankTerms.OwnCap(bank.Settings), 0).Cap);
            if (total <= 0)
            {
                return answer;
            }
            int fee = ProductTerms.ConsolidationFee(total);
            quote = PhoneLending.Quote(bank, total + fee, score);
            quote.Fee = fee;
            after = ProductTerms.After(total + fee, quote.DailyInterest, quote.DailyPayment, fee);
            before = ProductTerms.Before(_debts, after.Days);
            return answer;
        }

        /// <summary>Does it: every loan, the card's balance and the overdraft's are paid off by one new loan of the game's own.</summary>
        public ProductAnswer Consolidate(Lender bank)
        {
            DebtTotals before, after;
            LoanQuote quote;
            int count;
            ProductAnswer answer = Consolidation(bank, out before, out after, out quote, out count);
            if (answer != ProductAnswer.Yes)
            {
                return answer;
            }
            int day = Today;
            for (int i = _file.Loans.Count - 1; i >= 0; i--)
            {
                var loan = _file.Loans[i].Ref as Loan;
                if (loan != null)
                {
                    RemoveLoan(_file.Loans[i], loan);
                }
            }
            Save.Loans.Clear();   // a loan the file has no mark for yet is in the total too
            _file.Loans.Clear();
            for (int i = 0; i < _file.Accounts.Count; i++)
            {
                CreditAccount a = _file.Accounts[i];
                if (!a.Closed && !a.Test && ProductCatalogue.Revolving(a.Kind) && a.Owed > 0)
                {
                    AccountRules.Pay(a, a.Owed);
                }
            }
            Loan made = PhoneLending.Make(bank, quote.Amount, quote.DailyInterest, quote.DailyPayment, 0);
            _wallet.Force(before.Owed, Money.ConsolidationOff, bank.Name);
            _wallet.Force(quote.Fee, Money.ConsolidationFee, bank.Name);
            LoanMark mark = _director.Adopt(made);
            if (mark != null)
            {
                mark.Purpose = LoanPurpose.Consolidation;
            }
            CreditLedger.Post(CreditKind.Application, quote.Amount, day, "consolidation");
            CreditLedger.Post(CreditKind.Consolidated, before.Owed, day, "consolidation", count);
            if (Verbose)
            {
                Logger?.Info("Economy check (consolidation): " + count + " debt(s) of " + before.Owed + " in all (" + before.Daily + " a day, " + before.Cost + " of interest still to come, "
                             + before.Days
                             + " days) replaced by one loan of " + quote.Amount + " from " + bank.Name + " at " + quote.AnnualRate.ToString("0.#", CultureInfo.InvariantCulture) + "% (" + after.Daily
                             + " a day, " + after.Cost + " of interest and fee, " + after.Days + " days), fee " + quote.Fee + "; " + Save.Loans.Count + " loan(s) in the game now: "
                             + (Save.Loans.Count == 1 && quote.Amount == before.Owed + quote.Fee && mark != null ? "PASS" : "FAIL"));
            }
            _director.TermsChanged("a consolidation");
            return ProductAnswer.Yes;
        }
    }
}
