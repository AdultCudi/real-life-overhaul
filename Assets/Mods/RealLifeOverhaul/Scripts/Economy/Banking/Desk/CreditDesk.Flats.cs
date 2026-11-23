using System;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// The credit desk's mortgage on a flat of the property business (<see cref="FlatRules"/>): the same offer, deposit, term and rate as a
    /// building's, the flat as its security. The game does not sell a flat, so no approval is held and no purchase of the game's is waited for:
    /// the desk is asked what it would lend, the director takes the deposit, and the account is opened. A flat gone from the player's book
    /// is a security gone as a building is; a flat folded into a building bought whole moves its mortgage to the building.
    /// </summary>
    public sealed partial class CreditDesk
    {
        public const int FlatUnknown = 0, FlatHeld = 1, FlatGone = 2;

        /// <summary>Whether a flat of this key is still the player's: filled in by Wiring/EstateLinks; null or <see cref="FlatUnknown"/> leaves a mortgage as it is.</summary>
        public static Func<string, int> FlatSecurity;

        /// <summary>What the house bank would lend against a flat of this price at a deposit and a term, as it stands now; the answer says why not.</summary>
        public ProductAnswer QuoteFlat(int price, int depositPercent, int term, out int financed, out ProductOffer offer)
        {
            financed = 0;
            offer = Offer(ProductKind.Mortgage);
            if (offer.Answer != ProductAnswer.Yes)
            {
                return offer.Answer;
            }
            depositPercent = Mathf.Clamp(depositPercent, ProductTerms.DepositLeast(ProductKind.Mortgage), ProductTerms.DepositMost(ProductKind.Mortgage));
            financed = FlatRules.Financed(price, depositPercent);
            if (financed < ProductTerms.FinanceLeast(ProductKind.Mortgage))
            {
                return ProductAnswer.TooLittle;
            }
            return financed > offer.Most ? ProductAnswer.TooMuch : ProductAnswer.Yes;
        }

        /// <summary>The mortgage opens once the deposit is paid: an account with the flat as its security.</summary>
        public CreditAccount OpenFlatAccount(ProductOffer offer, int price, int financed, int term, string security, string name)
        {
            int day = Today;
            term = Mathf.Clamp(term, ProductTerms.TermLeast(ProductKind.Mortgage), ProductTerms.TermMost(ProductKind.Mortgage));
            CreditAccount account = AccountRules.Finance(ProductKind.Mortgage, financed, term, offer.RateBp, day, security, name, price);
            account.Street = offer.Bank.Street;
            account.Number = offer.Bank.Number;
            Opened(account);
            CreditLedger.Post(CreditKind.Application, financed, day, ProductCatalogue.KindSources[(int)ProductKind.Mortgage]);
            CreditLedger.Post(CreditKind.NewCredit, financed, day, account.Key);
            Note(NotificationType.Success, "reallife_econ_note_financed", Data("thing", Thing(account), "amount", FinanceText.Money(financed), "back", FinanceText.Money(0),
                "payment", FinanceText.Money(AccountRules.DueTomorrow(account, day))));
            if (Verbose)
            {
                Logger?.Info("Economy check (mortgage on a flat): '" + name + "' bought for " + price + ": " + financed + " financed over " + term + " day(s) at " + offer.RateBp
                             + " hundredths of a per cent, the buyer's own money " + (price - financed) + ": " + (financed <= offer.Most && price - financed >= 0 ? "PASS" : "FAIL"));
            }
            _director.TermsChanged("a mortgage on a flat");
            return account;
        }

        /// <summary>A flat is sold: what is owed on its mortgage is paid out of what the sale brought, as far as cash allows, and the account is no longer secured.</summary>
        public void SettleFlatSale(string security, int proceeds)
        {
            for (int i = 0; _file != null && i < _file.Accounts.Count; i++)
            {
                CreditAccount a = _file.Accounts[i];
                if (a.Closed || a.Kind != ProductKind.Mortgage || a.Security != security)
                {
                    continue;
                }
                int got = a.Test ? 0 : _wallet.TakeUpTo(Mathf.Min(Mathf.Max(0, proceeds), a.Owed), Money.Payment, Name(a));
                AccountRules.Unsecure(a);
                AccountRules.Pay(a, got);
                if (a.Owed == 0)
                {
                    a.Closed = true;
                    CreditLedger.Unregister(a);
                }
                if (got > 0)
                {
                    CreditLedger.Post(CreditKind.PaidOffEarly, got, Today, a.Key, 1, a.Test);
                }
                CreditLedger.Touch();
                Note(a.Closed ? NotificationType.Info : NotificationType.Warning, a.Closed ? "reallife_econ_note_sold_settled" : "reallife_econ_note_sold_owed",
                    Data("thing", Thing(a), "amount", FinanceText.Money(got), "left", FinanceText.Money(a.Owed)));
                _director.TermsChanged("a flat sold");
            }
        }

        /// <summary>What is owed on the open mortgages secured by one thing (a flat's key), 0 for none.</summary>
        public int OwedOn(string security)
        {
            long owed = 0;
            for (int i = 0; _file != null && i < _file.Accounts.Count; i++)
            {
                CreditAccount a = _file.Accounts[i];
                owed += !a.Closed && a.Kind == ProductKind.Mortgage && a.Security == security ? a.Owed : 0;
            }
            return (int)Math.Min(int.MaxValue / 4, owed);
        }

        /// <summary>The mortgage on a flat is now secured by the building it has folded into (bought whole).</summary>
        public void MoveSecurity(string from, string to, string name, int value)
        {
            for (int i = 0; _file != null && i < _file.Accounts.Count; i++)
            {
                CreditAccount a = _file.Accounts[i];
                if (!a.Closed && a.Kind == ProductKind.Mortgage && a.Security == from)
                {
                    a.Security = to ?? string.Empty;
                    a.SecurityName = name ?? string.Empty;
                    a.SecurityValue = Math.Max(0, value);
                    a.Secured = !string.IsNullOrEmpty(to);
                }
            }
            CreditLedger.Touch();
        }
    }
}
