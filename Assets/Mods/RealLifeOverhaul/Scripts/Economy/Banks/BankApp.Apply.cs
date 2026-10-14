using System.Collections.Generic;
using Localizor;
using RealLifeOverhaul.GameUi;
using UI;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Economy
{
    // A bank app's Apply tab: what this bank offers today at the player's rating, on its own terms, one row for each
    // kind of credit. Picking one shows its page; applying goes through the game's own question box and then the same
    // call the Finance app makes (EconomyDirector.Borrow for a loan, the credit desk for the rest), which asks the
    // rating's gate again at that moment. Under the lending threshold the tab refuses in the bank's own words, lists
    // nothing to apply for, and has no button that could lend.
    public sealed partial class BankApp
    {
        private readonly List<BuildingRegistration> _shops = new List<BuildingRegistration>(8);
        private int _product = -1;  // the kind whose page shows (BankTables.Loan ...); -1 for none
        private int _amount, _deposit, _term, _shop;

        private string WriteApply(InfoBox box, CreditScore score)
        {
            _spec = _tables.Apply;
            ProductAnswer gate = Desk.Gate();
            if (!score.Lends || !BankRules.Lends(score.Value))
            {
                // Under the threshold: the refusal, what would raise the rating, and nothing to press.
                _product = -1;
                box.Title(FinanceText.T("reallife_bank_refused_title", "bank", Bank.Name));
                box.Chip(FinanceText.Grade(score.Grade), FinanceApp.Tone(score.Grade));
                box.Line(FinanceText.T("reallife_bank_refused", "bank", Bank.Name, "score", score.Value.ToString(), "threshold", CreditCatalogue.LendingThreshold.ToString()), CellTone.Red);
                int tips = CreditScoring.Tips(score, _tips);
                if (tips > 0)
                {
                    box.Headline("reallife_econ_credit_raise");
                    for (int i = 0; i < tips; i++)
                    {
                        box.Line(FinanceText.Tip(score, _tips[i]));
                    }
                }
                Say("refused", FinanceText.T("reallife_bank_row_refused_all", "bank", Bank.Name, "threshold", CreditCatalogue.LendingThreshold.ToString()));
                Meta.Guide.Point(box, GuideTitle);
                return null;
            }
            bool products = Settings.EconomyProducts && Desk.Running;
            string selected = null;
            for (int p = 0; p < BankTables.OfferCount; p++)
            {
                if (p != BankTables.Loan && !products)
                {
                    continue;   // "More kinds of credit" is off: only the bank's loan
                }
                TableRow row = Row("offer:" + p);
                row.Pick = true;
                row.Index = p;
                row.Kind = p;
                row.Set(0, FinanceText.T(BankTables.OfferKinds[p]));
                OfferCells(row, p, score.Value);
                if (p == _product)
                {
                    selected = row.Id;
                }
            }
            if (_product < 0 || (_product != BankTables.Loan && !products))
            {
                _product = -1;
                box.Title(FinanceText.T("reallife_bank_tab_apply"));
                if (gate != ProductAnswer.Yes)
                {
                    box.Line(Why(gate), CellTone.Red);
                }
                box.Note(FinanceText.T("reallife_bank_apply_intro", "bank", Bank.Name));
                box.Note(FinanceText.T(ProfileKeys[Mathf.Clamp(Bank.Rank, 0, ProfileKeys.Length - 1)]));
                box.Note(FinanceText.T("reallife_bank_apply_one_desk"));
                Meta.Guide.Point(box, GuideTitle);
                return null;
            }
            box.Title(FinanceText.T(BankTables.OfferKinds[_product]));
            box.Subtitle(Bank.Name);
            switch (_product)
            {
                case BankTables.Loan: WriteLoanOffer(box, score); break;
                case BankTables.Overdraft: WriteLimit(box, ProductKind.Overdraft); break;
                case BankTables.Card: WriteLimit(box, ProductKind.CreditCard); break;
                case BankTables.Business: WriteBusiness(box); break;
                case BankTables.Car: WriteFinance(box, ProductKind.CarFinance); break;
                case BankTables.Home: WriteFinance(box, ProductKind.Mortgage); break;
                default: WriteConsolidate(box); break;
            }
            switch (_product)
            {
                case BankTables.Overdraft: box.Note(FinanceApp.AboutKind(ProductKind.Overdraft)); break;
                case BankTables.Card: box.Note(FinanceApp.AboutKind(ProductKind.CreditCard)); break;
                case BankTables.Consolidate: box.Note(FinanceText.T("reallife_bank_consolidate_about", "bank", Bank.Name)); break;
            }
            Meta.Guide.Point(box, GuideTitle);
            return selected;
        }

        // The figures of one row: the most, the rate, what borrowing the most would take each day, its fee, and whether it is on offer.
        private void OfferCells(TableRow row, int p, int score)
        {
            CreditDesk desk = Desk;
            ProductAnswer answer;
            switch (p)
            {
                case BankTables.Loan:
                    {
                        float most = PhoneLending.MostNow(Bank, score), rate = PhoneLending.RateOffered(Bank, score);
                        answer = most >= CreditCatalogue.LoanLeast ? ProductAnswer.Yes : ProductAnswer.TooMuch;
                        row.Set(1, FinanceText.Money(most), most);
                        row.Set(2, FinanceText.T("reallife_econ_row_rate", "rate", rate.ToString("0.#")), rate);
                        if (answer == ProductAnswer.Yes)
                        {
                            LoanQuote quote = PhoneLending.Quote(Bank, Mathf.FloorToInt(most), score);
                            row.Set(3, FinanceText.Money(quote.DailyPayment + quote.DailyInterest), quote.DailyPayment + quote.DailyInterest);
                            row.Set(4, FinanceText.Money(quote.Fee), quote.Fee);
                        }
                        break;
                    }
                case BankTables.Overdraft:
                case BankTables.Card:
                case BankTables.Car:
                case BankTables.Home:
                    {
                        ProductOffer offer = desk.Offer(KindOf(p), Bank);
                        answer = offer.Answer;
                        if (answer == ProductAnswer.AlreadyOpen && offer.Bank != null && offer.Bank.Rank != Bank.Rank)
                        {
                            row.Set(5, FinanceText.T("reallife_bank_row_held_with", "bank", offer.Bank.Name));
                            return;
                        }
                        if (offer.Most > 0)
                        {
                            row.Set(1, FinanceText.Money(offer.Most), offer.Most);
                        }
                        if (offer.RateBp > 0)
                        {
                            row.Set(2, FinanceApp.Rate(offer.RateBp), offer.RateBp / 100.0);
                        }
                        break;
                    }
                default:
                    answer = desk.Gate();   // a business loan and a consolidation say more on their page
                    break;
            }
            bool open = answer == ProductAnswer.AlreadyOpen, offered = answer == ProductAnswer.Yes;
            row.Set(5, FinanceText.T(offered ? "reallife_econ_row_on_offer" : open ? "reallife_econ_row_open" : "reallife_econ_row_not_offered"), offered || open ? TableRow.Plain : TableRow.Red);
        }

        private static ProductKind KindOf(int p)
        {
            switch (p)
            {
                case BankTables.Overdraft: return ProductKind.Overdraft;
                case BankTables.Card: return ProductKind.CreditCard;
                case BankTables.Car: return ProductKind.CarFinance;
                default: return ProductKind.Mortgage;
            }
        }

        private void Close(InfoBox box)
        {
            InfoBox.Button(box.ButtonRow(), InfoBox.Additional, "common_close", ClosePick);
        }

        // ---------------------------------------------------------------- a loan

        private void WriteLoanOffer(InfoBox box, CreditScore score)
        {
            int most = Mathf.FloorToInt(PhoneLending.MostNow(Bank, score.Value));
            box.Row("reallife_econ_offer_most", FinanceText.Money(most));
            if (most < CreditCatalogue.LoanLeast)
            {
                box.Line(FinanceText.T("reallife_econ_offer_nothing", "least", FinanceText.Money(CreditCatalogue.LoanLeast)), CellTone.Red);
                Close(box);
                return;
            }
            Steps(CreditCatalogue.LoanLeast, most);
            AmountSelector(box);
            LoanQuote quote = PhoneLending.Quote(Bank, _amount, score.Value);
            box.Row("reallife_econ_offer_rate", FinanceText.T("reallife_econ_row_rate", "rate", quote.AnnualRate.ToString("0.#")));
            box.RowPair("reallife_econ_offer_payment", FinanceText.Money(quote.DailyPayment), "reallife_econ_offer_interest", FinanceText.Money(quote.DailyInterest));
            box.RowPair("reallife_econ_offer_days", quote.Days.ToString(), "reallife_econ_offer_fee", FinanceText.Money(quote.Fee));
            box.Row("reallife_tab_offer_repay", FinanceText.Money(_amount + quote.Cost));
            Transform buttons = box.ButtonRow();
            int amount = _amount;
            InfoBox.Button(buttons, InfoBox.Positive, "reallife_econ_offer_apply", () => ApplyLoan(amount));
            AskAmount(buttons, "reallife_econ_offer_type", "reallife_econ_offer_placeholder", _amount, typed =>
            {
                int now = Mathf.FloorToInt(PhoneLending.MostNow(Bank, CreditLedger.Value));
                if (typed < CreditCatalogue.LoanLeast)
                {
                    Tell(NotificationType.Error, "reallife_econ_note_too_little", new Dictionary<string, string> { { "least", FinanceText.Money(CreditCatalogue.LoanLeast) } });
                    typed = CreditCatalogue.LoanLeast;
                }
                else if (typed > now)
                {
                    Tell(NotificationType.Error, "reallife_econ_note_too_much", new Dictionary<string, string> { { "lender", Bank.Name }, { "most", FinanceText.Money(now) } });
                    typed = now;
                }
                _amount = typed;
                Refresh();
            });
            InfoBox.Button(buttons, InfoBox.Additional, "common_cancel", ClosePick);
        }

        // The game's own question box: everything the loan costs is said before it is taken.
        private void ApplyLoan(int amount)
        {
            Lender bank = Bank;
            CreditRefusal refusal;
            if (bank == null || !CreditLedger.Ready || !CreditLedger.CanBorrow(out refusal))
            {
                _director.TellRefused();
                After();
                return;
            }
            LoanQuote quote = PhoneLending.Quote(bank, amount, CreditLedger.Score.Value);
            GameConfirm.Show("reallife_econ_confirm_title".Localize(new { lender = bank.Name }),
                "reallife_econ_confirm_body".Localize(new
                {
                    amount = FinanceText.Money(amount), lender = bank.Name, rate = quote.AnnualRate.ToString("0.#"), payment = FinanceText.Money(quote.DailyPayment),
                    interest = FinanceText.Money(quote.DailyInterest), days = quote.Days.ToString(), fee = FinanceText.Money(quote.Fee), cost = FinanceText.Money(quote.Cost)
                }),
                () => UiClone.Safe(() => Borrowed(bank, amount)), null, "reallife_econ_offer_apply", "common_cancel", false, false);
        }

        private void Borrowed(Lender bank, int amount)
        {
            LoanQuote quote;
            LoanAnswer answer = _director.Borrow(bank, amount, out quote);   // the Finance app's own call: the rating and the limit are asked again
            switch (answer)
            {
                case LoanAnswer.Approved:
                    Tell(NotificationType.Success, "reallife_econ_note_lent", new Dictionary<string, string>
                    {
                        { "lender", bank.Name }, { "amount", FinanceText.Money(amount) }, { "fee", FinanceText.Money(quote.Fee) }, { "payment", FinanceText.Money(quote.DailyPayment + quote.DailyInterest) }
                    });
                    _product = -1;
                    _tab = Loans;
                    break;
                case LoanAnswer.TooMuch:
                    Tell(NotificationType.Error, "reallife_econ_note_too_much", new Dictionary<string, string> { { "lender", bank.Name }, { "most", FinanceText.Money(PhoneLending.MostNow(bank, CreditLedger.Value)) } });
                    break;
                case LoanAnswer.TooLittle:
                    Tell(NotificationType.Error, "reallife_econ_note_too_little", new Dictionary<string, string> { { "least", FinanceText.Money(CreditCatalogue.LoanLeast) } });
                    break;
                default:
                    if (CreditLedger.Ready)
                    {
                        _director.TellRefused();
                    }
                    break;
            }
            After();
        }

        // ---------------------------------------------------------------- an overdraft or a card

        private void WriteLimit(InfoBox box, ProductKind kind)
        {
            CreditDesk desk = Desk;
            ProductOffer offer = desk.Offer(kind, Bank);
            if (offer.Answer == ProductAnswer.AlreadyOpen)
            {
                bool here = offer.Bank != null && offer.Bank.Rank == Bank.Rank;
                box.Line(here
                    ? FinanceText.T("reallife_bank_open_here", "limit", FinanceText.Money(offer.Most))
                    : FinanceText.T(kind == ProductKind.CreditCard ? "reallife_bank_card_elsewhere" : "reallife_bank_overdraft_elsewhere", "other", offer.Bank != null ? offer.Bank.Name : string.Empty, "bank", Bank.Name));
                if (here)
                {
                    int tab = kind == ProductKind.CreditCard ? Card : Accounts;
                    InfoBox.Button(box.ButtonRow(), InfoBox.Additional, "reallife_bank_see_it", () =>
                    {
                        _tab = tab;
                        _product = -1;
                        Refresh();
                    });
                }
                else
                {
                    Close(box);
                }
                return;
            }
            if (offer.Answer != ProductAnswer.Yes)
            {
                box.Line(Why(offer.Answer), CellTone.Red);
                Close(box);
                return;
            }
            box.RowPair("reallife_econ_offer_limit", FinanceText.Money(offer.Most), "reallife_econ_offer_rate", FinanceApp.Rate(offer.RateBp));
            Transform buttons = box.ButtonRow();
            string kindName = FinanceText.T(ProductCatalogue.KindKeys[(int)kind]);
            Lender bank = Bank;
            InfoBox.Button(buttons, InfoBox.Positive, "reallife_econ_offer_open", () =>
                GameConfirm.Show("reallife_econ_confirm_open_title".Localize(new { kind = kindName }),
                    "reallife_econ_confirm_open_body".Localize(new
                    {
                        kind = kindName, limit = FinanceText.Money(offer.Most), rate = (offer.RateBp / 100f).ToString("0.#"), lender = bank.Name, detail = FinanceApp.Detail(kind)
                    }),
                    () => UiClone.Safe(() =>
                    {
                        CreditAccount account;
                        ProductAnswer answer = desk.OpenAccount(kind, bank, out account);
                        if (answer == ProductAnswer.Yes)
                        {
                            Tell(NotificationType.Success, "reallife_econ_note_opened", new Dictionary<string, string> { { "account", CreditDesk.Name(account) }, { "limit", FinanceText.Money(account.Agreed) } });
                            _product = -1;
                            _tab = kind == ProductKind.CreditCard ? Card : Accounts;
                        }
                        Said(answer);
                    }), null, "reallife_econ_offer_open", "common_cancel", false, false));
            InfoBox.Button(buttons, InfoBox.Additional, "common_cancel", ClosePick);
        }

        // ---------------------------------------------------------------- a business loan

        private void WriteBusiness(InfoBox box)
        {
            CreditDesk desk = Desk;
            Books.Businesses(_shops);
            if (_shops.Count == 0)
            {
                box.Line(FinanceText.T("reallife_econ_business_none"), CellTone.Yellow);
                Close(box);
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
            float profit;
            int days;
            ProductOffer offer = desk.Business(shop, Bank, out profit, out days);
            box.Note(FinanceText.T("reallife_econ_business_record", "days", days.ToString(), "profit", FinanceText.Money(profit)));
            if (offer.Answer != ProductAnswer.Yes)
            {
                box.Line(Why(offer.Answer), CellTone.Red);
                Close(box);
                return;
            }
            box.Row("reallife_econ_offer_most", FinanceText.Money(offer.Most));
            Steps(CreditCatalogue.LoanLeast, offer.Most);
            AmountSelector(box);
            LoanQuote quote = desk.BusinessQuote(Bank, _amount);
            box.RowPair("reallife_econ_offer_rate", FinanceText.T("reallife_econ_row_rate", "rate", quote.AnnualRate.ToString("0.#")), "reallife_econ_offer_fee", FinanceText.Money(quote.Fee));
            box.RowPair("reallife_econ_offer_payment", FinanceText.Money(quote.DailyPayment), "reallife_econ_offer_interest", FinanceText.Money(quote.DailyInterest));
            int amount = _amount;
            Lender bank = Bank;
            Transform buttons = box.ButtonRow();
            InfoBox.Button(buttons, InfoBox.Positive, "reallife_econ_offer_apply", () =>
                GameConfirm.Show("reallife_econ_confirm_title".Localize(new { lender = bank.Name }),
                    "reallife_econ_confirm_business".Localize(new
                    {
                        amount = FinanceText.Money(amount), lender = bank.Name, business = shop.BusinessName, rate = quote.AnnualRate.ToString("0.#"),
                        payment = FinanceText.Money(quote.DailyPayment), interest = FinanceText.Money(quote.DailyInterest), days = quote.Days.ToString(), fee = FinanceText.Money(quote.Fee),
                        share = Mathf.RoundToInt(ProductCatalogue.BusinessDivertShare * 100f) + "%"
                    }),
                    () => UiClone.Safe(() =>
                    {
                        LoanQuote made;
                        ProductAnswer answer = desk.BorrowOnBusiness(shop, bank, amount, out made);
                        if (answer == ProductAnswer.Yes)
                        {
                            Tell(NotificationType.Success, "reallife_econ_note_lent", new Dictionary<string, string>
                            {
                                { "lender", bank.Name }, { "amount", FinanceText.Money(amount) }, { "fee", FinanceText.Money(made.Fee) }, { "payment", FinanceText.Money(made.DailyPayment + made.DailyInterest) }
                            });
                            _product = -1;
                            _tab = Loans;
                        }
                        Said(answer);
                    }), null, "reallife_econ_offer_apply", "common_cancel", false, false));
            InfoBox.Button(buttons, InfoBox.Additional, "common_cancel", ClosePick);
        }

        // ---------------------------------------------------------------- a vehicle or a building on finance

        private void WriteFinance(InfoBox box, ProductKind kind)
        {
            CreditDesk desk = Desk;
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
            ProductOffer offer = desk.Offer(kind, Bank);
            if (offer.Answer != ProductAnswer.Yes)
            {
                box.Line(Why(offer.Answer), CellTone.Red);
                Close(box);
                return;
            }
            int depositLeast = ProductTerms.DepositLeast(kind), depositStep = ProductTerms.DepositStep(kind), deposits = (ProductTerms.DepositMost(kind) - depositLeast) / depositStep + 1;
            int termLeast = ProductTerms.TermLeast(kind), termStep = ProductTerms.TermStep(kind), terms = (ProductTerms.TermMost(kind) - termLeast) / termStep + 1;
            _deposit = Mathf.Clamp(_deposit, 0, deposits - 1);
            _term = Mathf.Clamp(_term, 0, terms - 1);
            int deposit = depositLeast + _deposit * depositStep, term = termLeast + _term * termStep;
            box.Row("reallife_econ_offer_rate", FinanceApp.Rate(offer.RateBp));
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
            box.RowPair("reallife_econ_finance_first", FinanceText.Money(quote.Instalment + quote.FirstInterest), "reallife_econ_finance_interest", FinanceText.Money(quote.Interest));
            Lender bank = Bank;
            string kindName = FinanceText.T(ProductCatalogue.KindKeys[(int)kind]);
            Transform buttons = box.ButtonRow();
            InfoBox.Button(buttons, InfoBox.Positive, "reallife_econ_finance_approve", () =>
                GameConfirm.Show("reallife_econ_confirm_finance_title".Localize(new { kind = kindName }),
                    (kind == ProductKind.Mortgage ? "reallife_econ_confirm_home" : "reallife_econ_confirm_car").Localize(new
                    {
                        amount = FinanceText.Money(amount), lender = bank.Name, deposit = deposit + "%", own = FinanceText.Money(own), days = term.ToString(),
                        rate = (offer.RateBp / 100f).ToString("0.#"), day = (SaveGameManager.Current.Day + ProductCatalogue.ApprovalDays).ToString(),
                        share = Mathf.RoundToInt(ProductCatalogue.RepossessionValueShare * 100f) + "%"
                    }),
                    () => UiClone.Safe(() =>
                    {
                        ProductAnswer answer = desk.Approve(kind, bank, amount, deposit, term);
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

        // ---------------------------------------------------------------- consolidation with this bank

        private void WriteConsolidate(InfoBox box)
        {
            CreditDesk desk = Desk;
            DebtTotals before, after;
            LoanQuote quote;
            int count;
            ProductAnswer answer = desk.Consolidation(Bank, out before, out after, out quote, out count);
            if (before.Owed > 0)
            {
                box.Line(FinanceText.T("reallife_econ_consolidate_line", "owed", FinanceText.Money(before.Owed), "daily", FinanceText.Money(before.Daily), "days", before.Days.ToString())
                         + " " + FinanceText.Pair("reallife_econ_consolidate_cost", FinanceText.Money(before.Cost)));
                box.Headline("reallife_econ_consolidate_then");
                box.Line(FinanceText.T("reallife_econ_consolidate_line", "owed", FinanceText.Money(after.Owed), "daily", FinanceText.Money(after.Daily), "days", after.Days.ToString())
                         + " " + FinanceText.Pair("reallife_econ_consolidate_cost", FinanceText.Money(after.Cost)));
                box.Note(FinanceText.T("reallife_econ_consolidate_rate", "rate", quote.AnnualRate.ToString("0.#"), "fee", FinanceText.Money(quote.Fee)));
            }
            if (answer != ProductAnswer.Yes)
            {
                box.Line(Why(answer), CellTone.Red);
                Close(box);
                return;
            }
            Lender bank = Bank;
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
                            Tell(NotificationType.Success, "reallife_econ_note_consolidated", new Dictionary<string, string> { { "lender", bank.Name }, { "amount", FinanceText.Money(after.Owed) }, { "daily", FinanceText.Money(after.Daily) } });
                            _product = -1;
                            _tab = Loans;
                        }
                        Said(done);
                    }), null, "reallife_econ_consolidate_do", "common_cancel", false, false));
            InfoBox.Button(buttons, InfoBox.Additional, "common_cancel", ClosePick);
        }
    }
}
