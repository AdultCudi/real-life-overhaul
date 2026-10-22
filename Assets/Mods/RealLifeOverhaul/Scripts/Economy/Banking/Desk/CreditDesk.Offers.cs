using UnityEngine;

namespace RealLifeOverhaul.Economy
{
    /// <summary>What a lender offers of one product now, or why it does not.</summary>
    public struct ProductOffer
    {
        public ProductAnswer Answer;
        public int Most;                // the limit it would give, or the most it would finance
        public int RateBp;              // the rate as it would be charged today
        public int AgreedBp;            // the rate an overdraft or a card is opened at: by the rating alone (the central bank's is charged on top day by day)
        public Lender Bank;
        public CreditAccount Account;   // the one that is open, when the answer is AlreadyOpen
    }

    /// <summary>The credit desk's products as the apps ask for them: what is on offer, opening, drawing, paying and closing. Each asks the rating's gate again.</summary>
    public sealed partial class CreditDesk
    {
        private const int FinanceAccountsMost = CreditFile.AccountsMost - 6;

        /// <summary>What every product asks first: the option, the rating's gate, no arrangement running, a bank to lend.</summary>
        public ProductAnswer Gate()
        {
            CreditRefusal refusal;
            if (_file == null || !Settings.EconomyProducts)
            {
                return ProductAnswer.Off;
            }
            if (!CreditLedger.CanBorrow(out refusal))
            {
                return refusal == CreditRefusal.Bankrupt ? ProductAnswer.Bankrupt : refusal == CreditRefusal.BelowThreshold ? ProductAnswer.BelowThreshold : ProductAnswer.Off;
            }
            return _file.Relief.Arranged ? ProductAnswer.Arrangement : BankTerms.House == null ? ProductAnswer.NoBank : ProductAnswer.Yes;
        }

        /// <summary>The house bank's offer of an overdraft, a card, car finance or a mortgage as it stands now.</summary>
        public ProductOffer Offer(ProductKind kind)
        {
            return Offer(kind, BankTerms.House);
        }

        /// <summary>One bank's offer as it stands now; one overdraft and one card are held in all, so an open one answers AlreadyOpen with its bank.</summary>
        public ProductOffer Offer(ProductKind kind, Lender bank)
        {
            var offer = new ProductOffer { Answer = Gate(), Bank = bank ?? BankTerms.House };
            CreditAccount open = _file != null && ProductCatalogue.Revolving(kind) ? _file.Account(kind) : null;
            if (open != null)
            {
                offer.Answer = ProductAnswer.AlreadyOpen;
                offer.Account = open;
                offer.Most = open.Agreed;
                offer.RateBp = open.RateNow;
                offer.Bank = BankTerms.BankOf(open.Street, open.Number) ?? offer.Bank;
                return offer;
            }
            if (offer.Answer == ProductAnswer.Yes && offer.Bank == null)
            {
                offer.Answer = ProductAnswer.NoBank;
            }
            if (offer.Answer != ProductAnswer.Yes)
            {
                return offer;
            }
            int score = CreditLedger.Value;
            offer.AgreedBp = AgreedAt(kind, score, offer.Bank);
            offer.RateBp = ProductCatalogue.Revolving(kind) ? ProductTerms.WithBase(offer.AgreedBp, AccountRules.BaseBp, AccountRules.BaseMultiplier) : offer.AgreedBp;
            offer.Most = LimitAt(kind, score, offer.Bank, true);
            if (!ProductCatalogue.Revolving(kind))
            {
                if (_file.Relief.HasApproval || OpenAccounts() >= FinanceAccountsMost)
                {
                    offer.Answer = ProductAnswer.Busy;
                }
                else if (offer.Most < ProductTerms.FinanceLeast(kind))
                {
                    offer.Answer = ProductAnswer.TooLittle;
                }
            }
            return offer;
        }

        private int OpenAccounts()
        {
            int open = 0;
            for (int i = 0; i < _file.Accounts.Count; i++)
            {
                open += _file.Accounts[i].Closed || _file.Accounts[i].Test ? 0 : 1;
            }
            return open;
        }

        /// <summary>Opens an overdraft or a card with the house bank on today's terms; it costs nothing to hold.</summary>
        public ProductAnswer OpenAccount(ProductKind kind, out CreditAccount account)
        {
            return OpenAccount(kind, BankTerms.House, out account);
        }

        /// <summary>Opens an overdraft or a card with one bank on its terms of today.</summary>
        public ProductAnswer OpenAccount(ProductKind kind, Lender bank, out CreditAccount account)
        {
            account = null;
            ProductOffer offer = Offer(kind, bank);
            if (offer.Answer != ProductAnswer.Yes || !ProductCatalogue.Revolving(kind))
            {
                return offer.Answer == ProductAnswer.Yes ? ProductAnswer.Off : offer.Answer;
            }
            int day = Today;
            account = AccountRules.Open(kind, offer.Most, offer.AgreedBp, day);
            account.Street = offer.Bank.Street;
            account.Number = offer.Bank.Number;
            Opened(account);
            CreditLedger.Post(CreditKind.Application, offer.Most, day, account.Key);
            _director.TermsChanged("an account opened");
            return ProductAnswer.Yes;
        }

        /// <summary>Closes an overdraft or a card with nothing owed.</summary>
        public ProductAnswer CloseAccount(CreditAccount a)
        {
            if (_file == null || a == null || a.Closed || !ProductCatalogue.Revolving(a.Kind))
            {
                return ProductAnswer.Off;
            }
            if (a.Owed > 0)
            {
                return ProductAnswer.TooMuch;
            }
            Closed(a);
            if (Verbose)
            {
                Logger?.Info("Economy check (account closed): " + a.Kind + " #" + a.Id + " closed by the borrower with nothing owed: PASS");
            }
            _director.TermsChanged("an account closed");
            return ProductAnswer.Yes;
        }

        /// <summary>Cash drawn on the card; it and its fee go on the balance.</summary>
        public ProductAnswer Draw(CreditAccount card, int amount, out int fee)
        {
            fee = AccountRules.CashFee(amount);
            ProductAnswer gate = Gate();
            if (gate != ProductAnswer.Yes && gate != ProductAnswer.NoBank)
            {
                return gate;   // under the threshold an open card takes no new borrowing; what is owed stays owed
            }
            if (card == null || card.Closed || card.Frozen || card.Kind != ProductKind.CreditCard)
            {
                return ProductAnswer.Off;
            }
            if (amount < ProductCatalogue.CardDrawLeast)
            {
                return ProductAnswer.TooLittle;
            }
            if (!AccountRules.Draw(card, amount, out fee))
            {
                return ProductAnswer.TooMuch;
            }
            _wallet.Give(amount, Money.Drawn, Name(card));
            CreditLedger.Touch();
            if (Verbose)
            {
                Logger?.Info("Economy check (card): " + amount + " drawn with a fee of " + fee + ", " + card.Owed + " of " + card.Agreed + " owed: " + (card.Owed <= card.Agreed ? "PASS" : "FAIL"));
            }
            _director.TermsChanged("cash drawn on the card");
            return ProductAnswer.Yes;
        }

        /// <summary>The borrower pays into an account, with no fee; one in a debt arrangement too, which counts it.</summary>
        public ProductAnswer Pay(CreditAccount a, int amount, out int used)
        {
            used = 0;
            if (_file == null || a == null || a.Closed)
            {
                return ProductAnswer.Off;
            }
            amount = Mathf.Min(amount, a.Owed);
            if (amount <= 0)
            {
                return ProductAnswer.NothingOwed;
            }
            if (!a.Test && !_wallet.Take(amount, Money.Payment, Name(a)))
            {
                return ProductAnswer.NoCash;   // a test account takes no money
            }
            bool wasBehind = a.Ladder.Misses > 0, wasPenalty = a.Ladder.Penalty;
            used = AccountRules.Pay(a, amount);
            int day = Today;
            if (a.Closed)
            {
                CreditLedger.Unregister(a);
                CreditLedger.Post(CreditKind.PaidOffEarly, used, day, a.Key, 1, a.Test);
                if (a.Test)
                {
                    RemoveTest(a);
                }
            }
            else if (ProductCatalogue.Instalment(a.Kind))
            {
                CreditLedger.Post(CreditKind.ExtraPayment, used, day, a.Key, 1, a.Test);
            }
            CreditLedger.Touch();
            if (Verbose)
            {
                Logger?.Info("Economy check (payment by hand): " + used + " paid into " + a.Kind + " #" + a.Id + ", " + a.Owed + " left" + (a.Closed ? ", paid off early with no fee" : string.Empty)
                             + (wasBehind && a.Ladder.Misses == 0 ? ", in order again" : string.Empty) + (wasPenalty && !a.Ladder.Penalty ? ", the penalty rate ended" : string.Empty) + ": PASS");
            }
            if (a.Frozen)
            {
                PaidByHand(used, a.Kind + " #" + a.Id);   // paid under the arrangement, which ends here if this was the last of it
            }
            _director.TermsChanged("a payment by hand");
            return ProductAnswer.Yes;
        }
    }
}
