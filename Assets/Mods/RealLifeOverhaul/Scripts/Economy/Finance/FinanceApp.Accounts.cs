using System;
using System.Collections.Generic;
using Localizor;
using RealLifeOverhaul.GameUi;
using UI;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Economy
{
    // The Finance app's part for the credit desk: the other kinds of credit under the banks in Borrow, and the
    // Accounts tab. Lists are rows of the table (it scrolls); the white box shows the one row that is picked, with
    // at most three buttons to a row of buttons. Every action is asked of the credit desk, which asks the rating's
    // gate again; anything that costs or cannot be undone goes through the game's own question box first.
    public sealed partial class FinanceApp
    {
        private const int Overdraft = 0, Card = 1, BusinessLoan = 2, CarFinance = 3, HomeLoan = 4, Consolidate = 5, ProductCount = 6;
        private const string WaysRow = "ways", LenderRow = "lender", ApprovalRow = "approval";

        private static readonly string[] ProductKeys =
        {
            "reallife_econ_kind_overdraft", "reallife_econ_kind_card", "reallife_econ_kind_business", "reallife_econ_kind_car", "reallife_econ_kind_home", "reallife_econ_kind_consolidate"
        };

        private static readonly string[] AboutKeys =
        {
            "reallife_econ_about_overdraft", "reallife_econ_about_card", "reallife_econ_about_business", "reallife_econ_about_car", "reallife_econ_about_home", "reallife_econ_about_consolidate"
        };

        private readonly List<BuildingRegistration> _shops = new List<BuildingRegistration>(8);
        private int _product = -1;      // the kind of credit whose page shows in Borrow; -1 for none
        private string _picked;         // the row picked in Accounts
        private int _shop, _bank;       // which business secures, which bank consolidates
        private int _deposit, _term;

        private CreditDesk Desk => _director.Desk;

        // ---------------------------------------------------------------- shared

        private static string Rate(int rateBp)
        {
            return FinanceText.T("reallife_econ_row_rate", "rate", (rateBp / 100f).ToString("0.#"));
        }

        /// <summary>Why a product is not on offer, as one sentence.</summary>
        private static string Why(ProductAnswer answer)
        {
            switch (answer)
            {
                case ProductAnswer.BelowThreshold: return FinanceText.T("reallife_econ_why_threshold", "threshold", CreditCatalogue.LendingThreshold.ToString());
                case ProductAnswer.Bankrupt: return FinanceText.T("reallife_econ_why_bankrupt", "days", ProductCatalogue.BankruptNoCreditDays.ToString());
                case ProductAnswer.Arrangement: return FinanceText.T("reallife_econ_why_arrangement");
                case ProductAnswer.NoBank: return FinanceText.T("reallife_econ_row_no_banks");
                case ProductAnswer.TooLittle: return FinanceText.T("reallife_econ_why_too_little");
                case ProductAnswer.TooMuch: return FinanceText.T("reallife_econ_why_too_much");
                case ProductAnswer.NoSecurity: return FinanceText.T("reallife_econ_why_no_security", "days", ProductCatalogue.BusinessDaysOnRecord.ToString());
                case ProductAnswer.NoCash: return FinanceText.T("reallife_econ_why_no_cash");
                case ProductAnswer.NothingOwed: return FinanceText.T("reallife_econ_why_nothing");
                case ProductAnswer.Busy: return FinanceText.T("reallife_econ_why_busy");
                case ProductAnswer.AlreadyOpen: return FinanceText.T("reallife_econ_why_open");
                default: return FinanceText.T("reallife_econ_why_off");
            }
        }

        private static void Tell(NotificationType type, string key, Dictionary<string, string> data = null)
        {
            Notifications.Show(type, key, data, 9f, null, null, true, false);
        }

        private void Said(ProductAnswer answer)
        {
            if (answer != ProductAnswer.Yes)
            {
                Tell(NotificationType.Error, "reallife_econ_note_not_done", new Dictionary<string, string> { { "why", Why(answer) } });
            }
            After();
        }

        // Something was done: the app is written again if it is still showing.
        private void After()
        {
            if (_shell != null && _shell.Root != null && _shell.Root.activeInHierarchy)
            {
                _ready = _director.Running && CreditLedger.Ready;
                Refresh();
            }
        }

        // A whole amount read from what was typed (digits; the comma and the sign the game writes in "$5,000" are let through). False for anything else.
        private static bool Whole(string text, out int amount)
        {
            amount = 0;
            bool digits = false;
            for (int i = 0; text != null && i < text.Length; i++)
            {
                char c = text[i];
                if (c >= '0' && c <= '9')
                {
                    amount = Mathf.Min(amount * 10 + (c - '0'), 99999999);
                    digits = true;
                }
                else if (c != ',' && c != ' ' && c != '$')
                {
                    return false;
                }
            }
            return digits;
        }

        // The game's own text box for an amount; what is typed is checked by the action it is for.
        private void AskAmount(Transform row, string buttonKey, string placeholderKey, int initial, Action<int> then)
        {
            UIs uis = InstanceBehavior<UIs>.Instance;
            if (uis == null || uis.textInputUi == null)
            {
                return;
            }
            InfoBox.Button(row, InfoBox.Additional, buttonKey, () => uis.textInputUi.Show(placeholderKey, "common_confirm", text => UiClone.Safe(() =>
            {
                int amount;
                if (!Whole(text, out amount) || amount <= 0)
                {
                    Notifications.ShowError("common_notification_invalid_amount", null, false);
                    return;
                }
                then(amount);
            }), null, Mathf.Max(0, initial).ToString(), false, TypedAmountLength));
        }

        // The amounts a selector steps through, between a least and a most.
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
            if (!_amounts.Contains(_amount))
            {
                int at = 0;
                while (at < _amounts.Count && _amounts[at] < _amount)
                {
                    at++;
                }
                _amounts.Insert(at, _amount);
            }
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

        // A row was clicked that is not a bank: a kind of credit in Borrow, or anything in Accounts.
        private bool PickedOther(TableRow row)
        {
            if (_tab == Finance.Accounts)
            {
                _picked = row.Id;
                return true;
            }
            if (_tab != Finance.Borrow || row.Id == null || !row.Id.StartsWith("product:", StringComparison.Ordinal))
            {
                return false;
            }
            if (_product != row.Index)
            {
                _product = row.Index;
                _amount = 0;
                _deposit = 0;
                _term = 0;
            }
            _lender = null;
            return true;
        }

        // ---------------------------------------------------------------- Overview and Credit

        // The mod's own accounts and the private lender in the Overview's list. Returns what is owed on them.
        private float OverviewAccounts(CreditFile file, int today)
        {
            float owed = 0f;
            for (int i = 0; i < file.Accounts.Count; i++)
            {
                CreditAccount a = file.Accounts[i];
                if (a.Closed || a.Test || (a.Owed <= 0 && ProductCatalogue.Revolving(a.Kind)))
                {
                    continue;
                }
                owed += a.Owed;
                TableRow row = Row("account:" + a.Id);
                DebtCells(row, CreditDesk.Name(a), FinanceTables.Of(a.Kind), a.Owed, AccountRules.DueTomorrow(a, today));
                int tone = a.Ladder.Misses > 0 ? TableRow.Red : TableRow.Plain;
                if (ProductCatalogue.Instalment(a.Kind) && a.Instalment > 0)
                {
                    int days = Mathf.CeilToInt(a.Owed / (float)a.Instalment);
                    row.Set(4, FinanceText.T("reallife_econ_row_days", "days", days.ToString()), days, tone);
                }
                else
                {
                    row.Set(4, FinanceText.State(a), tone);
                }
            }
            if (file.Shark.Active)
            {
                owed += file.Shark.Owed;
                TableRow row = Row("shark");
                DebtCells(row, FinanceText.T("reallife-lender"), FinanceTables.Private, file.Shark.Owed, 0f);
                row.Set(4, FinanceText.T("reallife_econ_row_due_day", "day", file.Shark.DueDay.ToString()), file.Shark.DueDay - today, today > file.Shark.DueDay ? TableRow.Red : TableRow.Plain);
            }
            return owed;
        }

        // The Credit tab names the debt that hurts the rating most now: the one furthest behind, else a limit that is nearly used up.
        private void WorstLine(InfoBox box, CreditScore score)
        {
            CreditFile file = CreditLedger.File;
            string name = null;
            int misses = 0;
            float used = 0.5f;
            bool behind = false;
            for (int i = 0; i < file.Loans.Count; i++)
            {
                LoanMark m = file.Loans[i];
                if (m.Ladder.Misses > misses && m.Ref is Loan)
                {
                    misses = m.Ladder.Misses;
                    name = new Debt(m).Name;
                    behind = true;
                }
            }
            for (int i = 0; i < file.Accounts.Count; i++)
            {
                CreditAccount a = file.Accounts[i];
                if (a.Closed || a.Test)
                {
                    continue;
                }
                if (a.Ladder.Misses > misses)
                {
                    misses = a.Ladder.Misses;
                    name = CreditDesk.Name(a);
                    behind = true;
                }
                else if (!behind && ProductCatalogue.Revolving(a.Kind) && a.Agreed > 0 && a.Owed / (float)a.Agreed > used)
                {
                    used = a.Owed / (float)a.Agreed;
                    name = CreditDesk.Name(a);
                }
            }
            if (name == null && !score.Held)
            {
                return;
            }
            box.Headline("reallife_econ_credit_worst");
            if (name != null)
            {
                box.Line(behind
                    ? FinanceText.T("reallife_econ_credit_worst_behind", "account", name, "count", misses.ToString())
                    : FinanceText.T("reallife_econ_credit_worst_used", "account", name, "share", Mathf.RoundToInt(used * 100f) + "%"));
            }
            if (score.Held)
            {
                box.Line(FinanceText.T("reallife_econ_credit_held", "score", score.Ceiling.ToString()));
            }
        }

        // ---------------------------------------------------------------- Borrow: the other kinds of credit

        // One row for each kind, in the same list as the banks. Returns the id of the one whose page is showing.
        private string ProductRows()
        {
            string selected = null;
            CreditDesk desk = Desk;
            if (!Settings.EconomyProducts || desk == null || !desk.Running)
            {
                _product = -1;
                return null;
            }
            for (int p = 0; p < ProductCount; p++)
            {
                TableRow row = Row("product:" + p);
                row.Pick = true;
                row.Index = p;
                row.Kind = p + 1;   // (after the banks' loans in FinanceTables.OfferKinds)
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

        private void WriteProduct(InfoBox box)
        {
            CreditDesk desk = Desk;
            box.Title(FinanceText.T(ProductKeys[_product]));
            box.Line(About(_product));
            switch (_product)
            {
                case Overdraft: WriteLimit(box, desk, ProductKind.Overdraft); break;
                case Card: WriteLimit(box, desk, ProductKind.CreditCard); break;
                case BusinessLoan: WriteBusiness(box, desk); break;
                case CarFinance: WriteFinance(box, desk, ProductKind.CarFinance); break;
                case HomeLoan: WriteFinance(box, desk, ProductKind.Mortgage); break;
                default: WriteConsolidate(box, desk); break;
            }
        }

        // What a kind of credit is, in one sentence: the white box does not scroll, so the page keeps to its figures and
        // the whole of the terms is in the game's question box before anything is agreed.
        private static string About(int product)
        {
            string text = FinanceText.T(AboutKeys[product]);
            return product == Card ? text.Replace("{days}", ProductCatalogue.CardStatementDays.ToString())
                : product == BusinessLoan ? text.Replace("{multiple}", ProductCatalogue.BusinessProfitMultiple.ToString()) : text;
        }

        // The terms of an overdraft or a card in full, for the question box.
        private static string Detail(ProductKind kind)
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
                box.Line(Why(offer.Answer));
                InfoBox.Button(box.ButtonRow(), InfoBox.Additional, "common_close", ClosePick);
                return;
            }
            box.Line(FinanceText.Pair("reallife_econ_offer_limit", FinanceText.Money(offer.Most)));
            box.Line(FinanceText.Pair("reallife_econ_offer_rate", Rate(offer.RateBp)));
            if (offer.Answer == ProductAnswer.AlreadyOpen)
            {
                box.Line(FinanceText.Pair("reallife_econ_offer_drawn", FinanceText.Money(offer.Account.Owed)));
                InfoBox.Button(box.ButtonRow(), InfoBox.Additional, "reallife_econ_see_account", () => ShowAccount(offer.Account));
                return;
            }
            box.Line(FinanceText.Pair("reallife_econ_offer_with", offer.Bank.Name));
            Transform buttons = box.ButtonRow();
            InfoBox.Button(buttons, InfoBox.Positive, "reallife_econ_offer_open", () =>
                HudConfirm.Show("reallife_econ_confirm_open_title".Localize(new { kind = FinanceText.T(ProductKeys[_product]) }),
                    "reallife_econ_confirm_open_body".Localize(new
                    {
                        kind = FinanceText.T(ProductKeys[_product]), limit = FinanceText.Money(offer.Most), rate = (offer.RateBp / 100f).ToString("0.#"), lender = offer.Bank.Name, detail = Detail(kind)
                    }),
                    () => UiClone.Safe(() =>
                    {
                        CreditAccount account;
                        ProductAnswer answer = desk.OpenAccount(kind, out account);
                        if (answer == ProductAnswer.Yes)
                        {
                            Tell(NotificationType.Success, "reallife_econ_note_opened", new Dictionary<string, string> { { "account", CreditDesk.Name(account) }, { "limit", FinanceText.Money(account.Agreed) } });
                            ShowAccount(account);
                            return;
                        }
                        Said(answer);
                    }), null, "reallife_econ_offer_open", "common_cancel", false, false));
            InfoBox.Button(buttons, InfoBox.Additional, "common_cancel", ClosePick);
        }

        private void ShowAccount(CreditAccount account)
        {
            _tab = Finance.Accounts;
            _picked = "acct:" + account.Id;
            _product = -1;
            After();
        }

        // A loan against one of the player's businesses: the business, what it has made, the amount, what it costs.
        private void WriteBusiness(InfoBox box, CreditDesk desk)
        {
            Books.Businesses(_shops);
            if (_shops.Count == 0)
            {
                box.Line(FinanceText.T("reallife_econ_business_none"));
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
                box.Line(FinanceText.Pair("reallife_econ_business_name", _shops[_shop].BusinessName));
            }
            BuildingRegistration shop = _shops[_shop];
            float profit;
            int days;
            ProductOffer offer = desk.Business(shop, out profit, out days);
            if (offer.Answer != ProductAnswer.Yes)
            {
                if (offer.Answer == ProductAnswer.NoSecurity)
                {
                    box.Line(FinanceText.T("reallife_econ_business_record", "days", days.ToString(), "profit", FinanceText.Money(profit)));
                }
                box.Line(Why(offer.Answer));
                InfoBox.Button(box.ButtonRow(), InfoBox.Additional, "common_close", ClosePick);
                return;
            }
            box.Line(FinanceText.T("reallife_econ_business_record", "days", days.ToString(), "profit", FinanceText.Money(profit)));
            box.Line(FinanceText.Pair("reallife_econ_offer_most", FinanceText.Money(offer.Most)));
            Steps(CreditCatalogue.LoanLeast, offer.Most);
            AmountSelector(box);
            LoanQuote quote = desk.BusinessQuote(offer.Bank, _amount);
            box.Line(FinanceText.Pair("reallife_econ_offer_rate", FinanceText.T("reallife_econ_row_rate", "rate", quote.AnnualRate.ToString("0.#"))));
            box.Line(FinanceText.Pair("reallife_econ_offer_payment", FinanceText.Money(quote.DailyPayment)));
            box.Line(FinanceText.Pair("reallife_econ_offer_interest", FinanceText.Money(quote.DailyInterest)));
            box.Line(FinanceText.Pair("reallife_econ_offer_days", quote.Days.ToString()));
            box.Line(FinanceText.Pair("reallife_econ_offer_fee", FinanceText.Money(quote.Fee)));
            int amount = _amount;
            Transform buttons = box.ButtonRow();
            InfoBox.Button(buttons, InfoBox.Positive, "reallife_econ_offer_apply", () =>
                HudConfirm.Show("reallife_econ_confirm_title".Localize(new { lender = offer.Bank.Name }),
                    "reallife_econ_confirm_business".Localize(new
                    {
                        amount = FinanceText.Money(amount), lender = offer.Bank.Name, business = shop.BusinessName, rate = quote.AnnualRate.ToString("0.#"),
                        payment = FinanceText.Money(quote.DailyPayment), interest = FinanceText.Money(quote.DailyInterest), days = quote.Days.ToString(), fee = FinanceText.Money(quote.Fee),
                        share = Mathf.RoundToInt(ProductCatalogue.BusinessDivertShare * 100f) + "%"
                    }),
                    () => UiClone.Safe(() =>
                    {
                        LoanQuote made;
                        ProductAnswer answer = desk.BorrowOnBusiness(shop, amount, out made);
                        if (answer == ProductAnswer.Yes)
                        {
                            Tell(NotificationType.Success, "reallife_econ_note_lent", new Dictionary<string, string>
                            {
                                { "lender", offer.Bank.Name }, { "amount", FinanceText.Money(amount) }, { "fee", FinanceText.Money(made.Fee) }, { "payment", FinanceText.Money(made.DailyPayment + made.DailyInterest) }
                            });
                            _product = -1;
                            _tab = Finance.Overview;
                        }
                        Said(answer);
                    }), null, "reallife_econ_offer_apply", "common_cancel", false, false));
            InfoBox.Button(buttons, InfoBox.Additional, "common_cancel", ClosePick);
        }

        // A vehicle or a building on finance: an approval that stands, or the one on offer with its amount, deposit and term.
        private void WriteFinance(InfoBox box, CreditDesk desk, ProductKind kind)
        {
            Relief relief = CreditLedger.File.Relief;
            if (relief.HasApproval && relief.ApprovedKind == (int)kind)
            {
                box.Line(FinanceText.T("reallife_econ_approval_stands", "amount", FinanceText.Money(relief.ApprovedAmount), "day", (relief.ApprovedDay + ProductCatalogue.ApprovalDays).ToString(),
                    "deposit", relief.ApprovedDeposit + "%"));
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
                box.Line(Why(offer.Answer));
                InfoBox.Button(box.ButtonRow(), InfoBox.Additional, "common_close", ClosePick);
                return;
            }
            int depositLeast = ProductTerms.DepositLeast(kind), depositStep = ProductTerms.DepositStep(kind), deposits = (ProductTerms.DepositMost(kind) - depositLeast) / depositStep + 1;
            int termLeast = ProductTerms.TermLeast(kind), termStep = ProductTerms.TermStep(kind), terms = (ProductTerms.TermMost(kind) - termLeast) / termStep + 1;
            _deposit = Mathf.Clamp(_deposit, 0, deposits - 1);
            _term = Mathf.Clamp(_term, 0, terms - 1);
            int deposit = depositLeast + _deposit * depositStep, term = termLeast + _term * termStep;
            box.Line(FinanceText.Pair("reallife_econ_offer_rate", Rate(offer.RateBp)));
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
            box.Line(FinanceText.T("reallife_econ_finance_example", "price", FinanceText.Money(price), "own", FinanceText.Money(own)));
            box.Line(FinanceText.Pair("reallife_econ_finance_first", FinanceText.Money(quote.Instalment + quote.FirstInterest)));
            box.Line(FinanceText.Pair("reallife_econ_finance_interest", FinanceText.Money(quote.Interest)));
            Transform buttons = box.ButtonRow();
            InfoBox.Button(buttons, InfoBox.Positive, "reallife_econ_finance_approve", () =>
                HudConfirm.Show("reallife_econ_confirm_finance_title".Localize(new { kind = FinanceText.T(ProductKeys[_product]) }),
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

        // One new loan in place of every loan, the card and the overdraft: the bank, and before and after side by side.
        private void WriteConsolidate(InfoBox box, CreditDesk desk)
        {
            IReadOnlyList<Lender> lenders = BankTerms.Lenders;
            if (lenders.Count == 0)
            {
                box.Line(FinanceText.T("reallife_econ_row_no_banks"));
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
                box.Line(FinanceText.Pair("reallife_econ_offer_with", lenders[_bank].Name));
            }
            Lender bank = lenders[_bank];
            DebtTotals before, after;
            LoanQuote quote;
            int count;
            ProductAnswer answer = desk.Consolidation(bank, out before, out after, out quote, out count);
            if (before.Owed > 0)
            {
                box.Headline("reallife_econ_consolidate_now");
                box.Line(FinanceText.T("reallife_econ_consolidate_line", "owed", FinanceText.Money(before.Owed), "daily", FinanceText.Money(before.Daily), "days", before.Days.ToString()));
                box.Line(FinanceText.Pair("reallife_econ_consolidate_cost", FinanceText.Money(before.Cost)));
                box.Headline("reallife_econ_consolidate_then");
                box.Line(FinanceText.T("reallife_econ_consolidate_line", "owed", FinanceText.Money(after.Owed), "daily", FinanceText.Money(after.Daily), "days", after.Days.ToString()));
                box.Line(FinanceText.Pair("reallife_econ_consolidate_cost", FinanceText.Money(after.Cost)));
                box.Line(FinanceText.T("reallife_econ_consolidate_rate", "rate", quote.AnnualRate.ToString("0.#"), "fee", FinanceText.Money(quote.Fee)));
            }
            if (answer != ProductAnswer.Yes)
            {
                box.Line(Why(answer));
                InfoBox.Button(box.ButtonRow(), InfoBox.Additional, "common_close", ClosePick);
                return;
            }
            Transform buttons = box.ButtonRow();
            InfoBox.Button(buttons, InfoBox.Positive, "reallife_econ_consolidate_do", () =>
                HudConfirm.Show("reallife_econ_confirm_title".Localize(new { lender = bank.Name }),
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
                            Tell(NotificationType.Success, "reallife_econ_note_consolidated", new Dictionary<string, string> { { "lender", bank.Name }, { "amount", FinanceText.Money(after.Owed) }, { "daily", FinanceText.Money(after.Daily) } });
                            _product = -1;
                            _tab = Finance.Overview;
                        }
                        Said(done);
                    }), null, "reallife_econ_consolidate_do", "common_cancel", false, false));
            InfoBox.Button(buttons, InfoBox.Additional, "common_cancel", ClosePick);
        }

        // ---------------------------------------------------------------- Accounts

        private string WriteAccounts(InfoBox box)
        {
            CreditDesk desk = Desk;
            CreditFile file = CreditLedger.File;
            GameInstance save = SaveGameManager.Current;
            int today = save.Day;
            string selected = null;
            CreditAccount picked = null;
            LoanMark pickedLoan = null;
            float owed = 0f;
            _spec = FinanceTables.Accounts;
            for (int i = 0; i < file.Accounts.Count; i++)
            {
                CreditAccount a = file.Accounts[i];
                if (a.Closed)
                {
                    continue;
                }
                owed += a.Test ? 0 : a.Owed;
                TableRow row = Row("acct:" + a.Id);
                row.Pick = true;
                row.Kind = FinanceTables.Of(a.Kind);
                bool sound = a.Ladder.InOrder && !a.Ladder.Penalty;
                row.Set(0, FinanceText.T("reallife_tab_account_with", "account", CreditDesk.Name(a), "lender", CreditDesk.LenderName(a)));
                row.Set(1, FinanceText.T(FinanceTables.DebtKinds[row.Kind]));
                row.Set(2, FinanceText.Money(a.Owed), a.Test ? double.NaN : a.Owed);   // (a test tool's account is in no total)
                if (a.RateBp > 0)
                {
                    row.Set(3, Rate(a.RateNow), a.RateNow / 100.0, a.Ladder.Penalty ? TableRow.Red : TableRow.Plain);
                }
                int next, day;
                NextPayment(a, today, out next, out day);
                PaymentCells(row, next, day, today);
                row.Set(6, FinanceText.State(a), Gravity(a.Ladder, a.Frozen), sound ? TableRow.Plain : TableRow.Red);
                if (row.Id == _picked)
                {
                    picked = a;
                    selected = row.Id;
                }
            }
            int perYear = Mathf.Max(1, AccountRules.DaysPerYear);
            for (int i = 0; i < file.Loans.Count; i++)
            {
                LoanMark m = file.Loans[i];
                var loan = m.Ref as Loan;
                if (loan == null)
                {
                    continue;
                }
                owed += loan.remainingAmount;
                TableRow row = Row("loan:" + i);
                row.Pick = true;
                row.Kind = FinanceTables.OfLoan(m.Purpose);
                bool frozen = (m.Held & LoanHold.Frozen) != 0, sound = m.Ladder.InOrder && !m.Ladder.Penalty;
                string kind = FinanceText.T(FinanceTables.DebtKinds[row.Kind]);
                row.Set(0, FinanceText.T("reallife_tab_account_with", "account", kind, "lender", GameLoans.BankName(loan)));
                row.Set(1, kind);
                row.Set(2, FinanceText.Money(loan.remainingAmount), loan.remainingAmount);
                float yearly = YearlyRate(loan, perYear);
                if (yearly > 0f)
                {
                    row.Set(3, FinanceText.T("reallife_econ_row_rate", "rate", yearly.ToString("0.#")), yearly, (m.Held & LoanHold.Penalty) != 0 ? TableRow.Red : TableRow.Plain);
                }
                PaymentCells(row, frozen ? 0 : Mathf.RoundToInt(GameLoans.Due(loan)), today + 1, today);
                row.Set(6, FinanceText.State(m.Ladder, frozen, false), Gravity(m.Ladder, frozen), sound ? TableRow.Plain : TableRow.Red);
                if (row.Id == _picked)
                {
                    pickedLoan = m;
                    selected = row.Id;
                }
            }
            if (file.Shark.Active)
            {
                owed += file.Shark.Owed;
                TableRow row = Row(LenderRow);
                row.Pick = true;
                row.Kind = FinanceTables.Private;
                int late = today - file.Shark.DueDay;
                string kind = FinanceText.T("reallife_econ_kind_private");
                row.Set(0, FinanceText.T("reallife_tab_account_with", "account", kind, "lender", FinanceText.T("reallife-lender")));
                row.Set(1, kind);
                row.Set(2, FinanceText.Money(file.Shark.Owed), file.Shark.Owed);
                PaymentCells(row, file.Shark.Owed, file.Shark.DueDay, today);   // (the whole sum falls due on one day)
                row.Set(6, late > 0 ? FinanceText.T("reallife_econ_row_late", "days", late.ToString()) : FinanceText.T("reallife_econ_state_ok"), Mathf.Max(0, late), late > 0 ? TableRow.Red : TableRow.Plain);
                selected = row.Id == _picked ? row.Id : selected;
            }
            if (file.Relief.HasApproval)
            {
                // An approval is money promised, not owed: it is listed with its last day, and is in no total.
                TableRow row = Row(ApprovalRow);
                row.Pick = true;
                row.Kind = FinanceTables.Approval;
                int until = file.Relief.ApprovedDay + ProductCatalogue.ApprovalDays;
                row.Set(0, FinanceText.T("reallife_tab_account_with", "account", FinanceText.T("reallife_econ_row_approval", "kind", FinanceText.T(ProductCatalogue.KindKeys[file.Relief.ApprovedKind])),
                    "lender", BankName(file.Relief.ApprovedStreet, file.Relief.ApprovedNumber)));
                row.Set(1, FinanceText.T(FinanceTables.DebtKinds[FinanceTables.Approval]));
                row.Set(2, FinanceText.Money(file.Relief.ApprovedAmount));
                if (file.Relief.ApprovedRateBp > 0)
                {
                    row.Set(3, Rate(file.Relief.ApprovedRateBp), file.Relief.ApprovedRateBp / 100.0);
                }
                row.Set(5, DayWord(until, today), until);
                row.Set(6, FinanceText.T("reallife_econ_row_until_day", "day", until.ToString()));
                selected = row.Id == _picked ? row.Id : selected;
            }
            int claims = file.Relief.Claims.Count;
            if (owed > 0f || file.Relief.Arranged || claims > 0)
            {
                // Not a debt but a way to its page: it stays at the head of the list whatever the order and the filter.
                TableRow row = Row(WaysRow);
                row.Pick = true;
                row.Pinned = true;
                row.Set(0, FinanceText.T("reallife_econ_row_ways"));
                row.Set(6, file.Relief.Arranged ? FinanceText.T("reallife_econ_state_arranged") : claims > 0 ? FinanceText.T("reallife_econ_state_claims", "count", claims.ToString()) : string.Empty);
                selected = row.Id == _picked ? row.Id : selected;
            }
            if (_rows.Count == 0)
            {
                Say("none", FinanceText.T("reallife_econ_row_no_accounts"));
            }

            if (picked != null)
            {
                WriteAccount(box, desk, picked, today);
            }
            else if (pickedLoan != null)
            {
                WriteLoan(box, desk, pickedLoan);
            }
            else if (selected == LenderRow)
            {
                box.Title(FinanceText.T("reallife_econ_kind_private"));
                box.Line(FinanceText.Pair("reallife_econ_over_owed", FinanceText.Money(file.Shark.Owed)));
                box.Line(FinanceText.T("reallife_econ_private_terms", "day", file.Shark.DueDay.ToString(), "perDay", FinanceText.Money(SharkRules.ADay(file.Shark.Borrowed)),
                    "cap", FinanceText.Money(SharkRules.Cap(file.Shark.Borrowed))));
                box.Line(FinanceText.T("reallife_econ_private_how"));
            }
            else if (selected == ApprovalRow)
            {
                _product = file.Relief.ApprovedKind == (int)ProductKind.Mortgage ? HomeLoan : CarFinance;
                box.Title(FinanceText.T(ProductKeys[_product]));
                WriteFinance(box, desk, (ProductKind)file.Relief.ApprovedKind);
                _product = -1;
            }
            else if (selected == WaysRow)
            {
                WriteWays(box, desk);
            }
            else
            {
                Obligations due = Books.Tomorrow(file);
                box.Title(FinanceText.T("reallife_econ_tab_accounts"));
                box.Line(FinanceText.Pair("reallife_econ_over_owed", FinanceText.Money(owed)));
                box.Line(FinanceText.Pair("reallife_econ_over_leaves", FinanceText.Money(due.Loans + due.Accounts)));
                box.Line(FinanceText.T("reallife_econ_accounts_pick"));
                box.Line(FinanceText.T("reallife_econ_accounts_order"));
            }
            return selected;
        }

        private static string BankName(string street, int number)
        {
            Lender lender = BankTerms.At(street, number);
            return lender != null ? lender.Name : FinanceText.T("reallife_econ_lender_bank");
        }

        // What one of the mod's accounts takes next and on which day: an instalment tomorrow, a card's minimum on its
        // due day, an overdraft only what is over its limit. Nothing while it is in a debt arrangement.
        private static void NextPayment(CreditAccount a, int today, out int amount, out int day)
        {
            amount = day = 0;
            if (a.Closed || a.Frozen || a.Owed <= 0)
            {
                return;
            }
            if (a.Kind == ProductKind.CreditCard)
            {
                if (a.DueDay > 0 && a.MinimumLeft > 0)
                {
                    amount = a.MinimumLeft;
                    day = a.DueDay;
                }
                return;
            }
            amount = AccountRules.DueTomorrow(a, today);
            day = amount > 0 ? today + 1 : 0;
        }

        // The "next payment" and "due" cells of a row; left empty where nothing is due.
        private static void PaymentCells(TableRow row, int amount, int day, int today)
        {
            if (amount <= 0 || day <= 0)
            {
                return;
            }
            row.Set(4, FinanceText.Money(amount), amount);
            row.Set(5, DayWord(day, today), day, day < today ? TableRow.Red : TableRow.Plain);
        }

        // A day as the game says it: "Today", "Tomorrow", else "Day 14".
        private static string DayWord(int day, int today)
        {
            return day == today ? FinanceText.T("common_today") : day == today + 1 ? FinanceText.T("common_tomorrow") : FinanceText.Day(day);
        }

        // How grave a debt's standing is, as a number to order the "how it stands" column by: in order first, then
        // by payments missed, then the steps a lender takes, an arrangement last.
        private static double Gravity(LadderState s, bool frozen)
        {
            return s.Misses + (s.Penalty ? 0.25 : 0) + (s.PlanLeft > 0 ? 0.5 : 0) + (s.RepoDay > 0 ? 20 : 0) + (s.CallDue ? 50 : 0) + (s.RepoPending ? 100 : 0) + (frozen ? 200 : 0);
        }

        // What one of the game's loans is charged a year, in per cent of the sum borrowed. The game keeps a loan's
        // daily interest, not its rate (LoanHelper.CalculateDailyInterestPayment), so the rate is read back from it.
        private static float YearlyRate(Loan loan, int daysPerYear)
        {
            return loan.totalAmount > 0f ? loan.dailyInterest * (float)daysPerYear / loan.totalAmount * 100f : 0f;
        }

        // The app's own sentence about a kind of credit, for the page of an account of that kind.
        private static string AboutKind(ProductKind kind)
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

        // What missing payments would bring next, from the ladder's own counts. Nothing is written where no step is left.
        private static void NextStepLine(InfoBox box, LadderState s, bool secured)
        {
            string step;
            int away;
            if (FinanceTables.NextStep(s, CreditDesk.Level, secured, out step, out away))
            {
                box.Line(away <= 1
                    ? FinanceText.T("reallife_tab_account_next_miss", "step", FinanceText.T(step))
                    : FinanceText.T("reallife_tab_account_later_miss", "step", FinanceText.T(step), "count", away.ToString()));
            }
        }

        // One of the mod's accounts: its figures, how it stands, and what can be done with it.
        private void WriteAccount(InfoBox box, CreditDesk desk, CreditAccount a, int today)
        {
            bool revolving = ProductCatalogue.Revolving(a.Kind), instalment = ProductCatalogue.Instalment(a.Kind);
            box.Title(CreditDesk.Name(a));
            string about = AboutKind(a.Kind);
            if (about != null)
            {
                box.Line(about);
            }
            box.Line(FinanceText.T("reallife_tab_account_opened", "lender", CreditDesk.LenderName(a), "day", a.OpenedDay.ToString()));
            box.Line(FinanceText.Pair("reallife_econ_over_owed", a.Agreed > 0
                ? FinanceText.T("reallife_econ_row_owed_of", "owed", FinanceText.Money(a.Owed), "total", FinanceText.Money(a.Agreed)) : FinanceText.Money(a.Owed)));
            if (revolving && a.Agreed > 0)
            {
                box.Line(FinanceText.T("reallife_tab_account_used", "share", Mathf.RoundToInt(a.Owed * 100f / a.Agreed) + "%", "free", FinanceText.Money(a.Free)));
            }
            else if (instalment && a.Agreed > 0)
            {
                box.Line(FinanceText.T("reallife_tab_account_paid", "paid", FinanceText.Money(Mathf.Max(0, a.Agreed - a.Owed)), "total", FinanceText.Money(a.Agreed)));
            }
            if (a.RateBp > 0)
            {
                // The rate and why it is what it is: agreed by the rating, moved by the bank rate (overdraft, card), raised by a penalty.
                box.Line(FinanceText.Pair("reallife_econ_offer_rate", Rate(a.RateNow)) + " "
                         + (a.Ladder.Penalty ? FinanceText.T("reallife_econ_account_penalty", "points", ProductCatalogue.PenaltyPoints.ToString(), "run", ProductCatalogue.PenaltyClearRun.ToString())
                             : revolving ? FinanceText.T("reallife_tab_account_rate_moves", "agreed", Rate(a.RateBp)) : FinanceText.T("reallife_tab_account_rate_fixed")));
            }
            int due = AccountRules.DueTomorrow(a, today);
            switch (a.Kind)
            {
                case ProductKind.CreditCard:
                    box.Line(a.DueDay > 0
                        ? FinanceText.T("reallife_econ_account_card_due", "minimum", FinanceText.Money(a.MinimumLeft), "day", a.DueDay.ToString(), "statement", FinanceText.Money(a.StatementLeft))
                        : FinanceText.T("reallife_econ_account_card_next", "day", a.NextDay.ToString()));
                    break;
                case ProductKind.Overdraft:
                    box.Line(FinanceText.T("reallife_econ_account_overdraft", "free", FinanceText.Money(a.Free), "float", FinanceText.Money(ProductCatalogue.OverdraftFloat)));
                    break;
                case ProductKind.Arrears:
                    box.Line(FinanceText.T("reallife_econ_account_arrears"));
                    break;
                default:
                    {
                        int left = a.Instalment > 0 ? Mathf.CeilToInt(a.Owed / (float)a.Instalment) : 0;
                        box.Line(FinanceText.T("reallife_econ_account_instalment", "payment", FinanceText.Money(due), "days", left.ToString()));
                        if (left > 0 && !a.Frozen)
                        {
                            box.Line(FinanceText.Pair("reallife_tab_account_paid_off_day", FinanceText.Day(today + left)));
                        }
                        box.Line(FinanceText.T(a.Secured ? "reallife_econ_account_secured" : "reallife_econ_account_unsecured", "thing", CreditDesk.Thing(a)));
                        break;
                    }
            }
            LadderState s = a.Ladder;
            box.Line(FinanceText.Pair("reallife_econ_account_stands", FinanceText.State(a)));
            if (!a.Frozen && a.Owed > 0)
            {
                NextStepLine(box, s, a.Secured);
            }
            if (s.PlanLeft > 0)
            {
                box.Line(FinanceText.T("reallife_econ_account_plan", "left", FinanceText.Money(s.PlanLeft), "payment", FinanceText.Money(s.PlanPerDay)));
            }
            if (s.RepoPending)
            {
                box.Line(FinanceText.T("reallife_econ_account_repo", "thing", CreditDesk.Thing(a)));
            }
            if (a.Frozen)
            {
                return;   // in a debt arrangement: it is paid by the arrangement, not here
            }
            Transform buttons = box.ButtonRow();
            var debt = new Debt(a);
            if (s.CallDue)
            {
                CallButtons(buttons, desk, debt);
                return;
            }
            if (a.Owed > 0)
            {
                AskAmount(buttons, "reallife_econ_account_pay", "reallife_econ_account_pay_how_much", Mathf.Min(a.Owed, Mathf.Max(s.Overdue, due)), amount => Paid(desk, a, amount));
                InfoBox.Button(buttons, InfoBox.Positive, "reallife_econ_account_pay_off", () => Paid(desk, a, a.Owed));
            }
            if (a.Kind == ProductKind.CreditCard)
            {
                Transform second = a.Owed > 0 ? box.ButtonRow() : buttons;
                int most = AccountRules.MostDraw(a);
                AskAmount(second, "reallife_econ_account_draw", "reallife_econ_account_draw_how_much", most, amount => Drew(desk, a, amount));
                if (a.Owed == 0)
                {
                    InfoBox.Button(second, InfoBox.Negative, "reallife_econ_account_close", () => Said(desk.CloseAccount(a)));
                }
            }
            else if (a.Kind == ProductKind.Overdraft && a.Owed == 0)
            {
                InfoBox.Button(buttons, InfoBox.Negative, "reallife_econ_account_close", () => Said(desk.CloseAccount(a)));
            }
        }

        private void Paid(CreditDesk desk, CreditAccount a, int amount)
        {
            int used;
            string name = CreditDesk.Name(a);
            ProductAnswer answer = desk.Pay(a, amount, out used);
            if (answer == ProductAnswer.Yes)
            {
                Tell(NotificationType.Success, a.Closed ? "reallife_econ_note_paid_off" : "reallife_econ_note_paid", new Dictionary<string, string>
                {
                    { "account", name }, { "amount", FinanceText.Money(used) }, { "left", FinanceText.Money(a.Owed) }
                });
            }
            Said(answer);
        }

        private void Drew(CreditDesk desk, CreditAccount card, int amount)
        {
            int fee;
            ProductAnswer answer = desk.Draw(card, amount, out fee);
            if (answer == ProductAnswer.Yes)
            {
                Tell(NotificationType.Success, "reallife_econ_note_drawn", new Dictionary<string, string>
                {
                    { "amount", FinanceText.Money(amount) }, { "fee", FinanceText.Money(fee) }, { "owed", FinanceText.Money(card.Owed) }
                });
            }
            Said(answer);
        }

        // The collections call's two answers, for a debt whose call is due.
        private void CallButtons(Transform buttons, CreditDesk desk, Debt debt)
        {
            InfoBox.Button(buttons, InfoBox.Positive, "reallife_econ_account_pay_overdue", () => Said(desk.PayOverdue(debt)));
            if (!debt.Ladder.PlanUsed)
            {
                InfoBox.Button(buttons, InfoBox.Additional, "reallife_collect_plan", () => Said(desk.AgreePlan(debt)));
            }
            InfoBox.Button(buttons, InfoBox.Negative, "reallife_collect_cannot", () =>
            {
                desk.CannotPay(debt);
                After();
            });
        }

        // One of the game's loans: how it stands with its lender. Repaying it stays in EconoView.
        private void WriteLoan(InfoBox box, CreditDesk desk, LoanMark m)
        {
            var loan = (Loan)m.Ref;
            var debt = new Debt(m);
            int today = SaveGameManager.Current.Day;
            bool frozen = (m.Held & LoanHold.Frozen) != 0;
            box.Title(GameLoans.BankName(loan));
            box.Line(FinanceText.T("reallife_tab_loan_about", "kind", FinanceText.T(FinanceTables.DebtKinds[FinanceTables.OfLoan(m.Purpose)]), "day", m.FirstDay.ToString()));
            box.Line(FinanceText.Pair("reallife_econ_over_owed", FinanceText.T("reallife_econ_row_owed_of", "owed", FinanceText.Money(loan.remainingAmount), "total", FinanceText.Money(loan.totalAmount))));
            box.Line(FinanceText.T("reallife_tab_account_paid", "paid", FinanceText.Money(Mathf.Max(0f, loan.PaidAmount)), "total", FinanceText.Money(loan.totalAmount)));
            float yearly = YearlyRate(loan, Mathf.Max(1, AccountRules.DaysPerYear));
            if (yearly > 0f)
            {
                box.Line(FinanceText.Pair("reallife_econ_offer_rate", FinanceText.T("reallife_econ_row_rate", "rate", yearly.ToString("0.#"))) + " " + FinanceText.T("reallife_tab_loan_rate_note"));
            }
            box.Line(FinanceText.Pair("reallife_econ_offer_payment", FinanceText.Money(loan.dailyPayment)));
            if (loan.dailyPayment > 0 && !frozen)
            {
                int left = Mathf.CeilToInt(loan.remainingAmount / loan.dailyPayment);
                box.Line(FinanceText.T("reallife_tab_loan_left", "days", left.ToString(), "day", (today + left).ToString()));
            }
            box.Line(FinanceText.Pair("reallife_econ_offer_interest", FinanceText.Money(loan.dailyInterest))
                     + ((m.Held & LoanHold.Penalty) != 0 ? " " + FinanceText.T("reallife_econ_account_penalty", "points", ProductCatalogue.PenaltyPoints.ToString(), "run", ProductCatalogue.PenaltyClearRun.ToString()) : string.Empty));
            if (m.Purpose == LoanPurpose.Business)
            {
                BuildingRegistration shop = Books.Business(m.Security);
                box.Line(shop != null ? FinanceText.T("reallife_econ_account_secured", "thing", shop.BusinessName) : FinanceText.T("reallife_econ_loan_unsecured"));
            }
            box.Line(FinanceText.Pair("reallife_econ_account_stands", FinanceText.State(m.Ladder, frozen, false)));
            if (!frozen)
            {
                NextStepLine(box, m.Ladder, m.Purpose == LoanPurpose.Business && Books.Business(m.Security) != null);
            }
            if (m.Ladder.PlanLeft > 0)
            {
                box.Line(FinanceText.T("reallife_econ_account_plan", "left", FinanceText.Money(m.Ladder.PlanLeft), "payment", FinanceText.Money(m.Ladder.PlanPerDay)));
            }
            if (m.Ladder.RepoDay > 0)
            {
                box.Line(FinanceText.T("reallife_econ_loan_takings", "share", Mathf.RoundToInt(ProductCatalogue.BusinessDivertShare * 100f) + "%", "amount", FinanceText.Money(m.Ladder.Overdue)));
            }
            Transform buttons = box.ButtonRow();
            if (m.Ladder.CallDue)
            {
                CallButtons(buttons, desk, debt);
                return;
            }
            InfoBox.Button(buttons, InfoBox.Additional, "reallife_econ_open_econoview", OpenEconoView);
        }

        // The ways out of debt, with the player's own figures; the two that change anything are confirmed in the game's question box.
        private void WriteWays(InfoBox box, CreditDesk desk)
        {
            CreditFile file = CreditLedger.File;
            int loans, unsecured, secured, shark, other;
            desk.Owed(out loans, out unsecured, out secured, out shark, out other);
            var wealth = Helpers.PlayerHelper.GetPersonalWealth();
            box.Title(FinanceText.T("reallife_econ_row_ways"));
            box.Line(FinanceText.Pair("reallife_econ_over_owed", FinanceText.Money(loans + unsecured + secured + shark + other)));
            box.Headline("reallife_econ_ways_sell");
            box.Line(FinanceText.T("reallife_econ_ways_sell_line", "cash", FinanceText.Money(wealth.cash), "investments", FinanceText.Money(wealth.totalInvestments), "assets", FinanceText.Money(wealth.totalAssets)));
            ArrangementPlan plan;
            ProductAnswer arrange = desk.ArrangementOffer(out plan);
            box.Headline("reallife_econ_ways_arrange");
            if (file.Relief.Arranged)
            {
                box.Line(FinanceText.Fill("reallife_econ_ways_arranged", "paid", FinanceText.Money(file.Relief.ArrangedPaid), "total", FinanceText.Money(file.Relief.ArrangedTotal),
                    "payment", FinanceText.Money(file.Relief.ArrangedPerDay), "count", file.Relief.ArrangedMisses.ToString(), "most", ProductCatalogue.ArrangementMissesMost.ToString()));
            }
            else if (arrange == ProductAnswer.Yes)
            {
                box.Line(FinanceText.T("reallife_econ_ways_arrange_line", "amount", FinanceText.Money(plan.Total), "payment", FinanceText.Money(plan.PerDay), "days", plan.Days.ToString()));
            }
            else
            {
                box.Line(Why(arrange));
            }
            BankruptcyPlan ruin;
            ProductAnswer bankrupt = desk.BankruptcyOffer(out ruin);
            box.Headline("reallife_econ_ways_bankrupt");
            box.Line(bankrupt == ProductAnswer.Yes
                ? FinanceText.T("reallife_econ_ways_bankrupt_line", "amount", FinanceText.Money(ruin.WrittenOff), "cash", FinanceText.Money(ruin.CashTaken), "days", ProductCatalogue.BankruptNoCreditDays.ToString())
                : bankrupt == ProductAnswer.Busy ? FinanceText.T("reallife_advice_game_quest")
                : bankrupt == ProductAnswer.Bankrupt ? FinanceText.T("reallife_advice_bankrupt_soon", "days", ProductCatalogue.BankruptEveryDays.ToString())
                : Why(bankrupt));
            Transform buttons = box.ButtonRow();
            if (arrange == ProductAnswer.Yes && !file.Relief.Arranged)
            {
                InfoBox.Button(buttons, InfoBox.Additional, "reallife_econ_ways_arrange_do", () =>
                    HudConfirm.Show("reallife_econ_ways_arrange".Localize(),
                        "reallife_advice_arrange_page".Localize(new
                        {
                            amount = FinanceText.Money(plan.Total), payment = FinanceText.Money(plan.PerDay), days = plan.Days.ToString(), cap = ProductCatalogue.ArrangementScoreCap.ToString(),
                            after = ProductCatalogue.ArrangementCapDaysAfter.ToString(), misses = ProductCatalogue.ArrangementMissesMost.ToString()
                        }),
                        () => UiClone.Safe(() =>
                        {
                            ProductAnswer answer = desk.Arrange();
                            if (answer == ProductAnswer.Yes)
                            {
                                Tell(NotificationType.Info, "reallife_econ_note_arranged", new Dictionary<string, string> { { "amount", FinanceText.Money(plan.Total) }, { "payment", FinanceText.Money(plan.PerDay) } });
                            }
                            Said(answer);
                        }), null, "reallife_econ_ways_arrange_do", "common_cancel", false, false));
            }
            if (bankrupt == ProductAnswer.Yes)
            {
                InfoBox.Button(buttons, InfoBox.Negative, "reallife_econ_ways_bankrupt_do", () =>
                    HudConfirm.Show("reallife_econ_ways_bankrupt".Localize(),
                        "reallife_advice_bankrupt_page".Localize(new
                        {
                            amount = FinanceText.Money(ruin.WrittenOff), kept = FinanceText.Money(ruin.Kept), cash = FinanceText.Money(ruin.CashTaken), left = FinanceText.Money(ruin.CashLeft),
                            days = ProductCatalogue.BankruptNoCreditDays.ToString(), from = ProductCatalogue.BankruptRebuildFrom.ToString(), every = ProductCatalogue.BankruptEveryDays.ToString()
                        }),
                        () => UiClone.Safe(() => Said(desk.Bankrupt())), null, "reallife_econ_ways_bankrupt_do", "common_cancel", false, true));   // the game's red question box
            }
        }
    }
}
