using System.Collections.Generic;
using Entities;
using Helpers;
using Streets;
using UI.Notification;
using UnityEngine;
using Vehicles.VehicleTypes;

namespace RealLifeOverhaul.Economy
{
    /// <summary>What a lender offers of one product now, or why it does not.</summary>
    public struct ProductOffer
    {
        public ProductAnswer Answer;
        public int Most;                // the limit it would give, or the most it would finance
        public int RateBp;              // the rate as it would be charged today
        public int AgreedBp;            // the rate an overdraft or a card is opened at: by the rating alone (the central bank's rate is charged on top day by day)
        public Lender Bank;
        public CreditAccount Account;   // the one that is open, when the answer is AlreadyOpen
    }

    // The products as the Finance app asks for them: what is on offer, and each thing the player can do with an
    // account. Every one of them asks the rating's gate again at the moment it is done.
    public sealed partial class CreditDesk
    {
        private const string VehicleBought = "ba:transaction_vehiclebought";    // the game's own
        private const string ItemSold = "ba:transaction_itemsold";
        private const string BuildingSold = "ba:transaction_buildingsold";
        private const int FinanceAccountsMost = CreditFile.AccountsMost - 6;

        private readonly List<DebtItem> _debts = new List<DebtItem>(8);

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

        private int RateFor(ProductKind kind, Lender bank)
        {
            return AgreedAt(kind, CreditLedger.Value, bank);   // (Economy/Banks/CreditDesk.Banks.cs: the bank's own points are in it)
        }

        /// <summary>The offer of an overdraft, a card, car finance or a mortgage from the house bank as it stands now.</summary>
        public ProductOffer Offer(ProductKind kind)
        {
            return Offer(kind, BankTerms.House);
        }

        /// <summary>
        /// The offer of an overdraft, a card, car finance or a mortgage from one bank as it stands now. One overdraft and
        /// one card are held in all, with whichever bank opened them: the answer is then AlreadyOpen, with that account and
        /// its bank, whichever bank is asked.
        /// </summary>
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
            offer.AgreedBp = RateFor(kind, offer.Bank);
            offer.RateBp = ProductCatalogue.Revolving(kind) ? ProductTerms.WithBase(offer.AgreedBp, AccountRules.BaseBp, AccountRules.BaseMultiplier) : offer.AgreedBp;
            offer.Most = LimitAt(kind, score, offer.Bank);
            switch (kind)
            {
                case ProductKind.Overdraft:
                case ProductKind.CreditCard:
                    break;
                default:
                    if (_file.Relief.HasApproval || OpenAccounts() >= FinanceAccountsMost)
                    {
                        offer.Answer = ProductAnswer.Busy;
                    }
                    else if (offer.Most < ProductTerms.FinanceLeast(kind))
                    {
                        offer.Answer = ProductAnswer.TooLittle;
                    }
                    break;
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

        /// <summary>Opens an overdraft or a card on today's terms. It costs nothing to hold; the application goes on the credit file.</summary>
        public ProductAnswer OpenAccount(ProductKind kind, out CreditAccount account)
        {
            return OpenAccount(kind, BankTerms.House, out account);
        }

        /// <summary>Opens an overdraft or a card with one bank, on its terms of today; it is held there.</summary>
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

        /// <summary>Closes an overdraft or a card with nothing owed on it.</summary>
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

        /// <summary>Cash drawn on the card: the amount goes into cash, and it and its fee go on the balance.</summary>
        public ProductAnswer Draw(CreditAccount card, int amount, out int fee)
        {
            fee = AccountRules.CashFee(amount);
            ProductAnswer gate = Gate();
            if (gate != ProductAnswer.Yes && gate != ProductAnswer.NoBank)
            {
                return gate;   // under the threshold an open card is frozen for new borrowing; what is owed stays owed
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
            Touch();
            if (Verbose)
            {
                Logger?.Info("Economy check (card): " + amount + " drawn with a fee of " + fee + ", " + card.Owed + " of " + card.Agreed + " owed: " + (card.Owed <= card.Agreed ? "PASS" : "FAIL"));
            }
            _director.TermsChanged("cash drawn on the card");
            return ProductAnswer.Yes;
        }

        /// <summary>The borrower pays an amount into an account, at any time and with no fee. An account paid off this way is closed.</summary>
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
            string name = Name(a);
            if (!a.Test && !_wallet.Take(amount, Money.Payment, name))
            {
                return ProductAnswer.NoCash;   // (a test account takes no money)
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
            Touch();
            if (Verbose)
            {
                Logger?.Info("Economy check (payment by hand): " + used + " paid into " + a.Kind + " #" + a.Id + ", " + a.Owed + " left" + (a.Closed ? ", paid off early with no fee" : string.Empty)
                             + (wasBehind && a.Ladder.Misses == 0 ? ", in order again" : string.Empty) + (wasPenalty && !a.Ladder.Penalty ? ", the penalty rate ended" : string.Empty) + ": " + (a.Owed >= 0 ? "PASS" : "FAIL"));
            }
            _director.TermsChanged("a payment by hand");
            return ProductAnswer.Yes;
        }

        // ---------------------------------------------------------------- the business loan

        /// <summary>
        /// What a business can secure: thirty days of its average daily profit over the last seven days of the game's
        /// own statements, once it has seven days on record. The loan is one of the game's own, from the house bank.
        /// </summary>
        public ProductOffer Business(BuildingRegistration shop, out float profit, out int days)
        {
            return Business(shop, BankTerms.House, out profit, out days);
        }

        /// <summary>What a business can secure at one bank: the same record, that bank's share of the most (BankRules) and its points on the rate.</summary>
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

        /// <summary>What a business loan of an amount costs: the game's own figures, at the bank's rate for the rating less a point.</summary>
        // The whole-point rate of a business loan at a bank: the rating's less a point, and the bank's own points; never under the floor.
        private static int BusinessRate(int score, Lender bank)
        {
            return Mathf.Max(CreditCatalogue.RateFloor, ProductTerms.BusinessRate(score, BankTerms.OwnRate(bank.Settings)) + BankRules.Of(bank.Rank).RatePoints);
        }

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
            quote.Days = quote.DailyPayment > 0 ? Mathf.CeilToInt(amount / (float)quote.DailyPayment) : 0;
            return quote;
        }

        /// <summary>Lends against a business. The loan is the game's own (EconoView lists it, the game collects it); the mod remembers which business secures it.</summary>
        public ProductAnswer BorrowOnBusiness(BuildingRegistration shop, int amount, out LoanQuote quote)
        {
            return BorrowOnBusiness(shop, BankTerms.House, amount, out quote);
        }

        /// <summary>Lends against a business from one bank, on that bank's terms.</summary>
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
                Logger?.Info("Economy check (business loan): " + amount + " from " + offer.Bank.Name + " against " + shop.BusinessName + " (average profit " + profit.ToString("0") + " a day over " + days
                             + " day(s) on record, so " + offer.Most + " at most) at " + quote.AnnualRate.ToString("0.#") + "% a year: " + quote.DailyPayment + " a day and " + quote.DailyInterest
                             + " interest, fee " + quote.Fee + ": " + (mark != null && amount <= offer.Most ? "PASS" : "FAIL"));
            }
            _director.TermsChanged("a business loan");   // the bank's cap in memory rises by the loan: it does not use up what the bank lends unsecured
            return ProductAnswer.Yes;
        }

        // ---------------------------------------------------------------- car finance and the mortgage

        /// <summary>
        /// An approval for a vehicle or a building on finance. The game's own purchase asks for the whole price in
        /// cash, so the lender pays the approved amount into cash now; the purchase is then made in the game's own
        /// way, and what the lender in fact finances (never the buyer's deposit) becomes the account. What is not
        /// used goes back at once, and all of it if nothing is bought by the end of the next day.
        /// </summary>
        public ProductAnswer Approve(ProductKind kind, int amount, int depositPercent, int term)
        {
            return Approve(kind, BankTerms.House, amount, depositPercent, term);
        }

        /// <summary>An approval from one bank, on its terms; the account it becomes is held with that bank.</summary>
        public ProductAnswer Approve(ProductKind kind, Lender bank, int amount, int depositPercent, int term)
        {
            if (!ProductCatalogue.Instalment(kind))
            {
                return ProductAnswer.Off;
            }
            ProductOffer offer = Offer(kind, bank);
            if (offer.Answer != ProductAnswer.Yes)
            {
                return offer.Answer;
            }
            if (amount < ProductTerms.FinanceLeast(kind))
            {
                return ProductAnswer.TooLittle;
            }
            if (amount > offer.Most)
            {
                return ProductAnswer.TooMuch;
            }
            depositPercent = Mathf.Clamp(depositPercent, ProductTerms.DepositLeast(kind), ProductTerms.DepositMost(kind));
            term = Mathf.Clamp(term, ProductTerms.TermLeast(kind), ProductTerms.TermMost(kind));
            if (_wallet.Cash < ProductTerms.DepositFor(amount, depositPercent))
            {
                return ProductAnswer.NoCash;
            }
            int day = Today;
            Relief r = _file.Relief;
            r.ApprovedKind = (int)kind;
            r.ApprovedAmount = amount;
            r.ApprovedDeposit = depositPercent;
            r.ApprovedTerm = term;
            r.ApprovedRateBp = offer.RateBp;
            r.ApprovedDay = day;
            r.ApprovedStreet = offer.Bank.Street;
            r.ApprovedNumber = offer.Bank.Number;
            RememberOwned();
            _wallet.Give(amount, Money.Advance, FinanceText.T(ProductCatalogue.KindKeys[(int)kind]));
            CreditLedger.Post(CreditKind.Application, amount, day, ProductCatalogue.KindSources[(int)kind]);
            if (Verbose)
            {
                Logger?.Info("Economy check (approval): " + kind + " for up to " + amount + " with " + depositPercent + "% down over " + term + " days at " + (offer.RateBp / 100f).ToString("0.##")
                             + "% from " + offer.Bank.Name + ", paid into cash; the purchase has until the end of day " + (day + ProductCatalogue.ApprovalDays) + "; " + _ownedCount + " owned now: PASS");
            }
            _director.TermsChanged("an approval");
            return ProductAnswer.Yes;
        }

        /// <summary>The approval ends with nothing bought: the money goes back to the lender, whether or not it is still there.</summary>
        public void CancelApproval(bool ranOut)
        {
            Relief r = _file != null ? _file.Relief : null;
            if (r == null || !r.HasApproval)
            {
                return;
            }
            int amount = r.ApprovedAmount, cash = _wallet.Cash;
            string kind = FinanceText.T(ProductCatalogue.KindKeys[r.ApprovedKind]);
            int charge = ProductTerms.AdvanceCharge(amount, r.ApprovedRateBp, Today - r.ApprovedDay + 1, AccountRules.DaysPerYear);
            r.ClearApproval();
            _wallet.Force(amount, Money.AdvanceBack, kind);
            _wallet.Force(charge, Money.AdvanceCharge, kind);   // interest on the money for the days it was held
            Note(ranOut ? NotificationType.Warning : NotificationType.Info, ranOut ? "reallife_econ_note_approval_over" : "reallife_econ_note_approval_cancelled",
                Data("kind", kind, "amount", FinanceText.Money(amount)));
            if (Verbose)
            {
                Logger?.Info("Economy check (approval ended): " + (ranOut ? "nothing was bought in time" : "cancelled by the borrower") + "; " + amount + " went back to the lender out of " + cash + " in cash: PASS");
            }
        }

        // What the player owns of the kind an approval is for, so that a purchase is known by what is new.
        private void RememberOwned()
        {
            _owned.Clear();
            _ownedCount = -1;
            Relief r = _file != null ? _file.Relief : null;
            if (r == null || !r.HasApproval || Save == null)
            {
                return;
            }
            if (r.ApprovedKind == (int)ProductKind.Mortgage)
            {
                for (int i = 0; i < Save.realEstate.Count; i++)
                {
                    _owned.Add(Books.Key(Save.realEstate[i].address));
                }
                _ownedCount = Save.realEstate.Count;
                return;
            }
            for (int i = 0; i < Save.VehicleInstances.Count; i++)
            {
                _owned.Add(Save.VehicleInstances[i].id);
            }
            _ownedCount = Save.VehicleInstances.Count;
        }

        // While an approval stands: has the game added a vehicle or a building? One comparison of two counts while it has not.
        private void Purchases(int day)
        {
            Relief r = _file.Relief;
            if (!r.HasApproval || Save == null)
            {
                return;
            }
            if (ReliefRules.ApprovalOver(r, day))
            {
                CancelApproval(true);
                return;
            }
            var kind = (ProductKind)r.ApprovedKind;
            if (kind == ProductKind.Mortgage)
            {
                List<RealEstate> estates = Save.realEstate;
                if (estates.Count == _ownedCount)
                {
                    return;
                }
                for (int i = 0; i < estates.Count; i++)
                {
                    string key = Books.Key(estates[i].address);
                    if (!_owned.Contains(key))
                    {
                        Financed(kind, key, estates[i].address.ToFormattedString(), Mathf.RoundToInt((float)estates[i].purchasePrice), day);
                        return;
                    }
                }
                RememberOwned();
                return;
            }
            List<VehicleInstance> vehicles = Save.VehicleInstances;
            if (vehicles.Count == _ownedCount)
            {
                return;
            }
            for (int i = 0; i < vehicles.Count; i++)
            {
                VehicleInstance v = vehicles[i];
                VehicleType type = v != null && !_owned.Contains(v.id) && !v.zeroSellingPrice ? v.VehicleType : null;
                if (type == null || !type.IsMotorVehicle || Dealerships.Stock(v))
                {
                    continue;   // (a hand truck, a vehicle that is lent and not the player's to sell, or a car a dealership bought for its stock: Core/Dealerships)
                }
                Transaction bought = Books.Newest(VehicleBought, "vehicleName", v.vehicleTypeName, r.ApprovedDay);
                int price = bought != null ? Mathf.RoundToInt(-bought.amount) : Mathf.RoundToInt(type.price);
                Financed(kind, v.id, v.vehicleTypeName, price, day);
                return;
            }
            RememberOwned();
        }

        // The purchase has been made in the game's own way and paid in full from cash: the account opens for what the
        // lender finances, and what was approved over that goes back.
        private void Financed(ProductKind kind, string security, string name, int price, int day)
        {
            Relief r = _file.Relief;
            int approved = r.ApprovedAmount, asked = r.ApprovedDeposit;
            int financed = ProductTerms.Financed(price, asked, approved);
            string kindName = FinanceText.T(ProductCatalogue.KindKeys[(int)kind]);
            var account = financed > 0 ? AccountRules.Finance(kind, financed, r.ApprovedTerm, r.ApprovedRateBp, day, security, name, price) : null;
            if (account != null)
            {
                account.Street = r.ApprovedStreet;
                account.Number = r.ApprovedNumber;
            }
            int charge = ProductTerms.AdvanceCharge(approved, r.ApprovedRateBp, day - r.ApprovedDay + 1, AccountRules.DaysPerYear);
            r.ClearApproval();
            _owned.Clear();
            _ownedCount = -1;
            _wallet.Force(approved - financed, Money.AdvanceBack, kindName);
            _wallet.Force(charge, Money.AdvanceCharge, kindName);   // interest on the whole advance for the days it was held
            if (account == null)
            {
                return;
            }
            Opened(account);
            CreditLedger.Post(CreditKind.NewCredit, financed, day, account.Key);
            Note(NotificationType.Success, "reallife_econ_note_financed", Data("thing", Thing(account), "amount", FinanceText.Money(financed), "back", FinanceText.Money(approved - financed),
                "payment", FinanceText.Money(AccountRules.DueTomorrow(account, day))));
            if (Verbose)
            {
                Logger?.Info("Economy check (purchase on finance): '" + name + "' (" + security + ") bought for " + price + ": " + financed + " financed of " + approved + " approved, " + (approved - financed)
                             + " returned, the buyer's own money " + (price - financed) + " (" + asked + "% asked): " + (financed <= approved && price - financed >= 0 ? "PASS" : "FAIL"));
            }
            _director.TermsChanged("a purchase on finance");
        }

        // ---------------------------------------------------------------- a security that has gone, and one that is taken

        // A financed vehicle or building the player no longer has: what the sale raised goes to the lender as far as it
        // is still in cash, and the rest stays owed with nothing behind it.
        private void Securities(int day)
        {
            for (int i = 0; i < _file.Accounts.Count; i++)
            {
                CreditAccount a = _file.Accounts[i];
                if (a.Closed || !a.Secured || SecurityThere(a))
                {
                    continue;
                }
                bool home = a.Kind == ProductKind.Mortgage;
                Transaction sale = home ? Books.Newest(BuildingSold, "address", a.SecurityName, day - 1) : Books.Newest(ItemSold, "itemSoldInfo", a.SecurityName, day - 1);
                int raised = sale != null ? Mathf.Max(0, Mathf.FloorToInt(sale.amount)) : 0;
                string name = Name(a), thing = Thing(a);
                int got = raised > 0 && !a.Test ? _wallet.TakeUpTo(Mathf.Min(raised, a.Owed), Money.Payment, name) : 0;
                bool pending = a.Ladder.RepoPending;
                AccountRules.Unsecure(a);
                AccountRules.Pay(a, got);
                if (a.Owed == 0)
                {
                    a.Closed = true;   // (nothing was owed any more: a vehicle that was to go back after a bankruptcy)
                }
                if (a.Closed)
                {
                    CreditLedger.Unregister(a);
                    if (got > 0)
                    {
                        CreditLedger.Post(CreditKind.PaidOffEarly, got, day, a.Key, 1, a.Test);
                    }
                }
                Touch();
                if (!a.Closed || got > 0)
                {
                    Note(a.Closed ? NotificationType.Info : NotificationType.Warning, a.Closed ? "reallife_econ_note_sold_settled" : "reallife_econ_note_sold_owed",
                        Data("thing", thing, "amount", FinanceText.Money(got), "left", FinanceText.Money(a.Owed)));
                }
                if (Verbose)
                {
                    Logger?.Info("Economy check (security gone, day " + day + "): '" + a.SecurityName + "' of " + a.Kind + " #" + a.Id + " is no longer the borrower's ("
                                 + (sale != null ? "a sale for " + raised + " is in the game's transactions" : "no sale seen") + "): " + got + " taken, " + a.Owed + " stays owed unsecured"
                                 + (pending ? "; it was due to be taken back" : string.Empty) + ": PASS");
                }
            }
            for (int i = 0; i < _file.Loans.Count; i++)
            {
                LoanMark m = _file.Loans[i];
                if (m.Security.Length > 0 && Books.Business(m.Security) == null)
                {
                    if (Verbose)
                    {
                        Logger?.Info("Economy check (security gone, day " + day + "): the business at " + m.Security + " that secured the loan of " + m.Total.ToString("0") + " is no longer run by the borrower: the loan stays, unsecured: PASS");
                    }
                    m.Security = string.Empty;
                    m.Ladder.RepoDay = 0;
                }
            }
        }

        // A vehicle that is to be taken back is taken with the game's own call once the player is not in it, not near it
        // and not looking at it. Sold for the catalogue's share of its price against the balance.
        private void TakeVehicles(int day)
        {
            bool waiting = false;
            for (int i = 0; i < _file.Accounts.Count; i++)
            {
                CreditAccount a = _file.Accounts[i];
                if (a.Closed || !a.Ladder.RepoPending)
                {
                    continue;
                }
                VehicleInstance vehicle = Books.Vehicle(a.Security);
                if (vehicle == null)
                {
                    continue;   // gone already: the hour's look at the securities deals with it
                }
                if (!OutOfSight(vehicle, out VehicleController controller))
                {
                    waiting = true;
                    continue;
                }
                string name = Name(a), thing = Thing(a);
                int owed = a.Owed, surplus;
                OverlayGuard.Release(controller);
                vehicle.Delete(controller);   // the game's own way of removing a vehicle of the player's
                int used = AccountRules.Repossess(a, day, out surplus);
                if (a.SecurityValue <= 0 || a.Test)
                {
                    surplus = 0;   // taken back in a bankruptcy: what it fetches is the creditors' (and a test pays nothing out)
                }
                _wallet.Give(surplus, Money.Surplus, name);
                if (a.Owed == 0)
                {
                    a.Closed = true;
                }
                if (a.Closed)
                {
                    CreditLedger.Unregister(a);
                }
                CreditLedger.Post(CreditKind.Repossessed, used + surplus, day, a.Key, 1, a.Test);
                Touch();
                Note(NotificationType.Error, a.Owed > 0 ? "reallife_econ_note_repossessed_owed" : "reallife_econ_note_repossessed",
                    Data("thing", thing, "amount", FinanceText.Money(used + surplus), "left", FinanceText.Money(a.Owed), "days", ProductCatalogue.UnsecuredClaimDays.ToString()), null, 18f);
                if (Verbose)
                {
                    Logger?.Info("Economy check (repossession, day " + day + "): '" + a.SecurityName + "' (" + a.Security + ") taken out of the player's sight with the game's own call; bought for " + a.SecurityValue
                                 + ", sold for " + (used + surplus) + ": " + owed + " owed before, " + a.Owed + " after" + (surplus > 0 ? ", " + surplus + " paid to the borrower" : string.Empty)
                                 + ": " + (Books.Vehicle(a.Security) == null && a.Owed == owed - used ? "PASS" : "FAIL"));
                }
            }
            _repoWaiting = waiting;
        }

        // Never while the player is in it, near it or could see it.
        private static bool OutOfSight(VehicleInstance vehicle, out VehicleController controller)
        {
            controller = null;
            if (Save == null || Save.ActiveVehicleId == vehicle.id)
            {
                return false;
            }
            List<VehicleController> all = VehicleHelper.AllPlayerVehicles;
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i] != null && all[i].vehicleInstance != null && all[i].vehicleInstance.id == vehicle.id)
                {
                    controller = all[i];
                    break;
                }
            }
            if (controller == null)
            {
                return true;   // not in the scene the player is in
            }
            if (controller.controlledByPlayer)
            {
                return false;
            }
            Vector3 at = controller.transform.position;
            PlayerController player = PlayerHelper.PlayerController;
            if (player != null && (player.transform.position - at).sqrMagnitude < RepossessionKeepAway * RepossessionKeepAway)
            {
                return false;
            }
            return EconomyBridges.InView != null && !EconomyBridges.InView(at);
        }

        // ---------------------------------------------------------------- consolidation

        /// <summary>
        /// What one new loan from this bank in place of every loan, the card and the overdraft would come to. The new
        /// loan is for what is owed and the fee, at today's rate for the rating, and has to fit what the bank lends at
        /// the rating (everything owed to it is being paid off).
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
            if (BankTerms.QuestHold(bank.Settings))
            {
                return ProductAnswer.Busy;
            }
            _debts.Clear();
            List<Loan> loans = Save.Loans;
            for (int i = 0; i < loans.Count; i++)
            {
                _debts.Add(new DebtItem { Owed = Mathf.CeilToInt(loans[i].remainingAmount), DailyInterest = loans[i].dailyInterest, DailyPayment = loans[i].dailyPayment });
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
            Save.Loans.Clear();   // (a loan the file had no mark for yet is in the total too)
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
                Logger?.Info("Economy check (consolidation): " + count + " debt(s) of " + before.Owed + " in all (" + before.Daily + " a day, " + before.Cost + " of interest still to come, " + before.Days
                             + " days) replaced by one loan of " + quote.Amount + " from " + bank.Name + " at " + quote.AnnualRate.ToString("0.#") + "% (" + after.Daily + " a day, " + after.Cost
                             + " of interest and fee, " + after.Days + " days), fee " + quote.Fee + "; " + Save.Loans.Count + " loan(s) in the game now: "
                             + (Save.Loans.Count == 1 && quote.Amount == before.Owed + quote.Fee && mark != null ? "PASS" : "FAIL"));
            }
            _director.TermsChanged("a consolidation");
            return ProductAnswer.Yes;
        }
    }
}
