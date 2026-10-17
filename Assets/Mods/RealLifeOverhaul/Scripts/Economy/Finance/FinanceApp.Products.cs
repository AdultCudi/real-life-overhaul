using System.Collections.Generic;
using Localizor;
using RealLifeOverhaul.GameUi;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Economy
{
    /// <summary>Borrow's other kinds of credit; each action is confirmed in the game's question box, then asked of the credit desk.</summary>
    public sealed partial class FinanceApp
    {
        private const int Overdraft = 0, Card = 1, BusinessLoan = 2, CarFinance = 3, HomeLoan = 4, Consolidate = 5, ProductCount = 6;

        private static readonly string[] ProductKeys =
        {
            "reallife_econ_kind_overdraft", "reallife_econ_kind_card", "reallife_econ_kind_business", "reallife_econ_kind_car", "reallife_econ_kind_home", "reallife_econ_kind_consolidate"
        };

        private static readonly string[] AboutKeys =
        {
            "reallife_econ_about_overdraft", "reallife_econ_about_card", "reallife_econ_about_business", "reallife_econ_about_car", "reallife_econ_about_home", "reallife_econ_about_consolidate"
        };

        private readonly List<BuildingRegistration> _shops = new List<BuildingRegistration>(8);
        private int _product = -1;   // the kind of credit whose page shows; -1 for none
        private int _shop, _bank;    // the business that secures, the bank that consolidates
        private int _deposit, _term; // the steps chosen on a finance page

        // One row for each kind, among the banks. Returns the id of the one whose page shows.
        private string ProductRows()
        {
            string selected = null;
            CreditDesk desk = Desk;
            if (!Settings.EconomyProducts || !desk.Running)
            {
                _product = -1;
                return null;
            }
            for (int p = 0; p < ProductCount; p++)
            {
                TableRow row = Row("product:" + p);
                row.Pick = true;
                row.Index = p;
                row.Kind = p + 1;   // after the banks' loans in FinanceTables.OfferKinds
                ProductOffer offer = default(ProductOffer);
                switch (p)
                {
                    case Overdraft: offer = desk.Offer(ProductKind.Overdraft); break;
                    case Card: offer = desk.Offer(ProductKind.CreditCard); break;
                    case CarFinance: offer = desk.Offer(ProductKind.CarFinance); break;
                    case HomeLoan: offer = desk.Offer(ProductKind.Mortgage); break;
                    default: offer.Answer = desk.Gate(); break;   // a business loan and a consolidation say more on their page
                }
                bool offered = offer.Answer == ProductAnswer.Yes, open = offer.Answer == ProductAnswer.AlreadyOpen;
                row.Set(0, offer.Bank != null ? offer.Bank.Name : FinanceText.T("reallife_tab_lender_any"));
                row.Set(1, FinanceText.T(ProductKeys[p]));
                if (offer.Most > 0)
                {
                    row.Set(2, FinanceText.Money(offer.Most), offer.Most);
                }
                if (offer.RateBp > 0)
                {
                    row.Set(3, Rate(offer.RateBp), offer.RateBp / 100.0);
                }
                row.Set(7, FinanceText.T(offered ? "reallife_econ_row_on_offer" : open ? "reallife_econ_row_open" : "reallife_econ_row_not_offered"), offered || open ? TableRow.Plain : TableRow.Red);
                if (p == _product && _lender == null)
                {
                    selected = row.Id;
                }
            }
            return selected;
        }

        private void PickProduct(int product)
        {
            if (_product != product)
            {
                _product = product;
                _amount = 0;
                _deposit = 0;
                _term = 0;
            }
            _lender = null;
        }

        private void WriteProduct(InfoBox box)
        {
            CreditDesk desk = Desk;
            box.Title(FinanceText.T(ProductKeys[_product]));
            box.Section("reallife_ibox_sec_terms");
            switch (_product)
            {
                case Overdraft: WriteLimit(box, desk, ProductKind.Overdraft); break;
                case Card: WriteLimit(box, desk, ProductKind.CreditCard); break;
                case BusinessLoan: WriteBusiness(box, desk); break;
                case CarFinance: WriteFinance(box, desk, ProductKind.CarFinance); break;
                case HomeLoan: WriteFinance(box, desk, ProductKind.Mortgage); break;
                default: WriteConsolidate(box, desk); break;
            }
            box.Note(About(_product));
            Meta.Guide.Point(box, GuideTitle);
        }

        // One sentence: the box does not scroll, so the whole terms are in the question box.
        private static string About(int product)
        {
            string text = FinanceText.T(AboutKeys[product]);
            return product == Card ? text.Replace("{days}", ProductCatalogue.CardStatementDays.ToString())
                : product == BusinessLoan ? text.Replace("{multiple}", ProductCatalogue.BusinessProfitMultiple.ToString()) : text;
        }

        /// <summary>The app's sentence about a kind of credit, for the page of an account of that kind; null for arrears.</summary>
        internal static string AboutKind(ProductKind kind)
        {
            switch (kind)
            {
                case ProductKind.Overdraft: return About(Overdraft);
                case ProductKind.CreditCard: return About(Card);
                case ProductKind.CarFinance: return About(CarFinance);
                case ProductKind.Mortgage: return About(HomeLoan);
                default: return null;
            }
        }

        /// <summary>The terms of an overdraft or a card in full, for the question box.</summary>
        internal static string Detail(ProductKind kind)
        {
            if (kind == ProductKind.Overdraft)
            {
                return FinanceText.T("reallife_econ_detail_overdraft", "float", FinanceText.Money(ProductCatalogue.OverdraftFloat));
            }
            return FinanceText.Fill("reallife_econ_detail_card", "fee", Mathf.RoundToInt(ProductCatalogue.CardCashFeeShare * 100f) + "%", "least", FinanceText.Money(ProductCatalogue.CardCashFeeLeast),
                "days", ProductCatalogue.CardStatementDays.ToString(), "grace", ProductCatalogue.CardGraceDays.ToString(),
                "share", Mathf.RoundToInt(ProductCatalogue.CardMinimumShare * 100f) + "%", "minimum", FinanceText.Money(ProductCatalogue.CardMinimumLeast));
        }

        // An overdraft or a card: the limit and the rate on offer, or the one that is open.
        private void WriteLimit(InfoBox box, CreditDesk desk, ProductKind kind)
        {
            ProductOffer offer = desk.Offer(kind);
            if (offer.Answer != ProductAnswer.Yes && offer.Answer != ProductAnswer.AlreadyOpen)
            {
                Refused(box, offer.Answer);
                return;
            }
            box.Row("reallife_econ_offer_limit", FinanceText.Money(offer.Most));
            box.Row("reallife_econ_offer_rate", Rate(offer.RateBp));
            if (offer.Answer == ProductAnswer.AlreadyOpen)
            {
                box.Row("reallife_econ_offer_drawn", FinanceText.Money(offer.Account.Owed));
                InfoBox.Button(box.ButtonRow(), InfoBox.Additional, "reallife_econ_see_account", () => ShowAccount(offer.Account));
                return;
            }
            box.Row("reallife_econ_offer_with", offer.Bank.Name);
            Transform buttons = box.ButtonRow();
            InfoBox.Button(buttons, InfoBox.Positive, "reallife_econ_offer_open", () =>
                GameConfirm.Show("reallife_econ_confirm_open_title".Localize(new { kind = FinanceText.T(ProductKeys[_product]) }),
                    "reallife_econ_confirm_open_body".Localize(new
                    {
                        kind = FinanceText.T(ProductKeys[_product]), limit = FinanceText.Money(offer.Most), rate = (offer.RateBp / 100f).ToString("0.#"), lender = offer.Bank.Name,
                        detail = Detail(kind)
                    }),
                    () => UiClone.Safe(() =>
                    {
                        ProductAnswer answer = desk.OpenAccount(kind, out CreditAccount account);
                        if (answer == ProductAnswer.Yes)
                        {
                            Tell(NotificationType.Success, "reallife_econ_note_opened", new Dictionary<string, string>
                            {
                                { "account", CreditDesk.Name(account) }, { "limit", FinanceText.Money(account.Agreed) }
                            });
                            ShowAccount(account);
                            return;
                        }
                        Said(answer);
                    }), null, "reallife_econ_offer_open", "common_cancel", false, false));
            InfoBox.Button(buttons, InfoBox.Additional, "common_cancel", ClosePick);
        }

        private void Refused(InfoBox box, ProductAnswer answer)
        {
            box.Line(Why(answer), CellTone.Red);
            InfoBox.Button(box.ButtonRow(), InfoBox.Additional, "common_close", ClosePick);
        }

        private void ShowAccount(CreditAccount account)
        {
            _tab = Finance.Accounts;
            _picked = "acct:" + account.Id;
            _product = -1;
            After();
        }

        private void WriteBusiness(InfoBox box, CreditDesk desk)
        {
            Books.Businesses(_shops);
            if (_shops.Count == 0)
            {
                box.Line(FinanceText.T("reallife_econ_business_none"), CellTone.Yellow);
                InfoBox.Button(box.ButtonRow(), InfoBox.Additional, "common_close", ClosePick);
                return;
            }
            _shop = Mathf.Clamp(_shop, 0, _shops.Count - 1);
            if (_shops.Count > 1 && box.HasSelector)
            {
                box.Selector(_shops.Count, _shop, i => _shops[i].BusinessName, i =>
                {
                    _shop = i;
                    _amount = 0;
                    Refresh();
                });
            }
            else
            {
                box.Row("reallife_econ_business_name", _shops[_shop].BusinessName);
            }
            BuildingRegistration shop = _shops[_shop];
            ProductOffer offer = desk.Business(shop, out float profit, out int days);
            string record = FinanceText.T("reallife_econ_business_record", "days", days.ToString(), "profit", FinanceText.Money(profit));
            if (offer.Answer != ProductAnswer.Yes)
            {
                if (offer.Answer == ProductAnswer.NoSecurity)
                {
                    box.Line(record);
                }
                Refused(box, offer.Answer);
                return;
            }
            box.Note(record);
            box.Row("reallife_econ_offer_most", FinanceText.Money(offer.Most));
            Steps(CreditCatalogue.LoanLeast, offer.Most);
            AmountSelector(box);
            LoanQuote quote = desk.BusinessQuote(offer.Bank, _amount);
            box.Row("reallife_econ_offer_rate", FinanceText.T("reallife_econ_row_rate", "rate", quote.AnnualRate.ToString("0.#")));
            box.Row("reallife_econ_offer_payment", FinanceText.Money(quote.DailyPayment));
            box.Row("reallife_econ_offer_interest", FinanceText.Money(quote.DailyInterest));
            box.Row("reallife_econ_offer_days", quote.Days.ToString());
            box.Row("reallife_econ_offer_fee", FinanceText.Money(quote.Fee));
            int amount = _amount;
            Transform buttons = box.ButtonRow();
            InfoBox.Button(buttons, InfoBox.Positive, "reallife_econ_offer_apply", () =>
                GameConfirm.Show("reallife_econ_confirm_title".Localize(new { lender = offer.Bank.Name }),
                    "reallife_econ_confirm_business".Localize(new
                    {
                        amount = FinanceText.Money(amount), lender = offer.Bank.Name, business = shop.BusinessName, rate = quote.AnnualRate.ToString("0.#"),
                        payment = FinanceText.Money(quote.DailyPayment), interest = FinanceText.Money(quote.DailyInterest), days = quote.Days.ToString(), fee = FinanceText.Money(quote.Fee),
                        share = Mathf.RoundToInt(ProductCatalogue.BusinessDivertShare * 100f) + "%"
                    }),
                    () => UiClone.Safe(() =>
                    {
                        ProductAnswer answer = desk.BorrowOnBusiness(shop, amount, out LoanQuote made);
                        if (answer == ProductAnswer.Yes)
                        {
                            Tell(NotificationType.Success, "reallife_econ_note_lent", new Dictionary<string, string>
                            {
                                { "lender", offer.Bank.Name }, { "amount", FinanceText.Money(amount) }, { "fee", FinanceText.Money(made.Fee) },
                                { "payment", FinanceText.Money(made.DailyPayment + made.DailyInterest) }
                            });
                            _product = -1;
                            _tab = Finance.Overview;
                        }
                        Said(answer);
                    }), null, "reallife_econ_offer_apply", "common_cancel", false, false));
            InfoBox.Button(buttons, InfoBox.Additional, "common_cancel", ClosePick);
        }

        // An approval that stands, or the one on offer with its amount, deposit and term.
        private void WriteFinance(InfoBox box, CreditDesk desk, ProductKind kind)
        {
            Relief relief = CreditLedger.File.Relief;
            if (relief.HasApproval && relief.ApprovedKind == (int)kind)
            {
                box.Line(FinanceText.T("reallife_econ_approval_stands", "amount", FinanceText.Money(relief.ApprovedAmount), "day", (relief.ApprovedDay + ProductCatalogue.ApprovalDays).ToString(),
                    "deposit", relief.ApprovedDeposit + "%"), CellTone.Green);
                InfoBox.Button(box.ButtonRow(), InfoBox.Negative, "reallife_econ_approval_cancel", () =>
                {
                    desk.CancelApproval(false);
                    After();
                });
                return;
            }
            ProductOffer offer = desk.Offer(kind);
            if (offer.Answer != ProductAnswer.Yes)
            {
                Refused(box, offer.Answer);
                return;
            }
            int depositLeast = ProductTerms.DepositLeast(kind), depositStep = ProductTerms.DepositStep(kind), deposits = (ProductTerms.DepositMost(kind) - depositLeast) / depositStep + 1;
            int termLeast = ProductTerms.TermLeast(kind), termStep = ProductTerms.TermStep(kind), terms = (ProductTerms.TermMost(kind) - termLeast) / termStep + 1;
            _deposit = Mathf.Clamp(_deposit, 0, deposits - 1);
            _term = Mathf.Clamp(_term, 0, terms - 1);
            int deposit = depositLeast + _deposit * depositStep, term = termLeast + _term * termStep;
            box.Row("reallife_econ_offer_rate", Rate(offer.RateBp));
            Steps(ProductTerms.FinanceLeast(kind), offer.Most);
            AmountSelector(box);
            if (box.HasSelector)
            {
                box.Selector(deposits, _deposit, i => FinanceText.T("reallife_econ_finance_deposit", "share", (depositLeast + i * depositStep) + "%"), i =>
                {
                    _deposit = i;
                    Refresh();
                });
                box.Selector(terms, _term, i => FinanceText.T("reallife_econ_finance_term", "days", (termLeast + i * termStep).ToString()), i =>
                {
                    _term = i;
                    Refresh();
                });
            }
            int own = ProductTerms.DepositFor(_amount, deposit), price = _amount + own, amount = _amount;
            FinanceQuote quote = ProductTerms.Quote(price, deposit, amount, term, offer.RateBp, AccountRules.DaysPerYear);
            box.Note(FinanceText.T("reallife_econ_finance_example", "price", FinanceText.Money(price), "own", FinanceText.Money(own)));
            box.Row("reallife_econ_finance_first", FinanceText.Money(quote.Instalment + quote.FirstInterest));
            box.Row("reallife_econ_finance_interest", FinanceText.Money(quote.Interest));
            Transform buttons = box.ButtonRow();
            InfoBox.Button(buttons, InfoBox.Positive, "reallife_econ_finance_approve", () =>
                GameConfirm.Show("reallife_econ_confirm_finance_title".Localize(new { kind = FinanceText.T(ProductKeys[_product]) }),
                    (kind == ProductKind.Mortgage ? "reallife_econ_confirm_home" : "reallife_econ_confirm_car").Localize(new
                    {
                        amount = FinanceText.Money(amount), lender = offer.Bank.Name, deposit = deposit + "%", own = FinanceText.Money(own), days = term.ToString(),
                        rate = (offer.RateBp / 100f).ToString("0.#"), day = (SaveGameManager.Current.Day + ProductCatalogue.ApprovalDays).ToString(),
                        share = Mathf.RoundToInt(ProductCatalogue.RepossessionValueShare * 100f) + "%"
                    }),
                    () => UiClone.Safe(() =>
                    {
                        ProductAnswer answer = desk.Approve(kind, amount, deposit, term);
                        if (answer == ProductAnswer.Yes)
                        {
                            Tell(NotificationType.Success, "reallife_econ_note_approved", new Dictionary<string, string>
                            {
                                { "amount", FinanceText.Money(amount) }, { "day", (SaveGameManager.Current.Day + ProductCatalogue.ApprovalDays).ToString() }
                            });
                        }
                        Said(answer);
                    }), null, "reallife_econ_finance_approve", "common_cancel", false, false));
            InfoBox.Button(buttons, InfoBox.Additional, "common_cancel", ClosePick);
        }

        // One new loan in place of every loan, the card and the overdraft: before and after side by side.
        private void WriteConsolidate(InfoBox box, CreditDesk desk)
        {
            IReadOnlyList<Lender> lenders = BankTerms.Lenders;
            if (lenders.Count == 0)
            {
                box.Note(FinanceText.T("reallife_econ_row_no_banks"));
                return;
            }
            _bank = Mathf.Clamp(_bank, 0, lenders.Count - 1);
            if (lenders.Count > 1 && box.HasSelector)
            {
                box.Selector(lenders.Count, _bank, i => lenders[i].Name, i =>
                {
                    _bank = i;
                    Refresh();
                });
            }
            else
            {
                box.Row("reallife_econ_offer_with", lenders[_bank].Name);
            }
            Lender bank = lenders[_bank];
            ProductAnswer answer = desk.Consolidation(bank, out DebtTotals before, out DebtTotals after, out LoanQuote quote, out int count);
            if (before.Owed > 0)
            {
                box.Headline("reallife_econ_consolidate_now");
                box.Line(FinanceText.T("reallife_econ_consolidate_line", "owed", FinanceText.Money(before.Owed), "daily", FinanceText.Money(before.Daily), "days", before.Days.ToString()));
                box.Row("reallife_econ_consolidate_cost", FinanceText.Money(before.Cost));
                box.Headline("reallife_econ_consolidate_then");
                box.Line(FinanceText.T("reallife_econ_consolidate_line", "owed", FinanceText.Money(after.Owed), "daily", FinanceText.Money(after.Daily), "days", after.Days.ToString()));
                box.Row("reallife_econ_consolidate_cost", FinanceText.Money(after.Cost));
                box.Line(FinanceText.T("reallife_econ_consolidate_rate", "rate", quote.AnnualRate.ToString("0.#"), "fee", FinanceText.Money(quote.Fee)));
            }
            if (answer != ProductAnswer.Yes)
            {
                Refused(box, answer);
                return;
            }
            Transform buttons = box.ButtonRow();
            InfoBox.Button(buttons, InfoBox.Positive, "reallife_econ_consolidate_do", () =>
                GameConfirm.Show("reallife_econ_confirm_title".Localize(new { lender = bank.Name }),
                    "reallife_econ_confirm_consolidate".Localize(new
                    {
                        count = count.ToString(), owed = FinanceText.Money(before.Owed), amount = FinanceText.Money(after.Owed), lender = bank.Name, rate = quote.AnnualRate.ToString("0.#"),
                        daily = FinanceText.Money(after.Daily), days = after.Days.ToString(), fee = FinanceText.Money(quote.Fee), was = FinanceText.Money(before.Daily)
                    }),
                    () => UiClone.Safe(() =>
                    {
                        ProductAnswer done = desk.Consolidate(bank);
                        if (done == ProductAnswer.Yes)
                        {
                            Tell(NotificationType.Success, "reallife_econ_note_consolidated", new Dictionary<string, string>
                            {
                                { "lender", bank.Name }, { "amount", FinanceText.Money(after.Owed) }, { "daily", FinanceText.Money(after.Daily) }
                            });
                            _product = -1;
                            _tab = Finance.Overview;
                        }
                        Said(done);
                    }), null, "reallife_econ_consolidate_do", "common_cancel", false, false));
            InfoBox.Button(buttons, InfoBox.Additional, "common_cancel", ClosePick);
        }

        // The amount chosen starts at the most.
        private void Steps(int least, int most)
        {
            _amount = Mathf.Clamp(_amount <= 0 ? most : _amount, least, Mathf.Max(least, most));
            _amounts.Clear();
            for (int i = 0; i < AmountSteps.Length && AmountSteps[i] < most; i++)
            {
                if (AmountSteps[i] >= least)
                {
                    _amounts.Add(AmountSteps[i]);
                }
            }
            _amounts.Add(most);
            InsertAmount();
        }

        private void InsertAmount()
        {
            if (_amounts.Contains(_amount))
            {
                return;
            }
            int at = 0;
            while (at < _amounts.Count && _amounts[at] < _amount)
            {
                at++;
            }
            _amounts.Insert(at, _amount);
        }

        private void AmountSelector(InfoBox box)
        {
            box.Headline("reallife_econ_offer_amount");
            if (!box.HasSelector || !box.Selector(_amounts.Count, _amounts.IndexOf(_amount), i => FinanceText.Money(_amounts[i]), i =>
                {
                    _amount = _amounts[i];
                    Refresh();
                }))
            {
                box.Line(FinanceText.Money(_amount));
            }
        }
    }
}
