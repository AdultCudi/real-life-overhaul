using System;
using System.Collections.Generic;
using Localizor;
using RealLifeOverhaul.DaySeries;
using RealLifeOverhaul.Economy;
using UI.Notification;

namespace RealLifeOverhaul.Ui
{
    public sealed partial class BanksLive
    {
        private const int LoanKind = 0, OverdraftKind = 1, CardKind = 2, BusinessKind = 3, CarKind = 4, HomeKind = 5, ConsolidateKind = 6;

        private static ProductKind ProductOf(int kind)
        {
            switch (kind)
            {
                case OverdraftKind: return ProductKind.Overdraft;
                case CardKind: return ProductKind.CreditCard;
                case CarKind: return ProductKind.CarFinance;
                default: return ProductKind.Mortgage;
            }
        }

        private static int KindOfRow(string row)
        {
            return row != null && row.StartsWith("p:", StringComparison.Ordinal) && int.TryParse(row.Substring(2), System.Globalization.NumberStyles.None, Inv, out int kind) ? kind : -1;
        }

        // ------------------------------------------------------------------------------------------------- Apply: reading

        private void ReadApply(BanksFacts f, CreditDesk desk, CreditScore score, MoneyView view, int today)
        {
            var products = new List<BankProduct>(BankTables.OfferCount);
            for (int p = 0; p < BankTables.OfferCount; p++)
            {
                if (p != BankTables.Loan && !f.ProductsOn)
                {
                    continue;   // "More kinds of credit" is off: only the bank's loan
                }
                products.Add(ProductRow(f, desk, p, score.Value));
            }
            f.Products = products.ToArray();
            int kind = KindOfRow(view.Row);
            f.Product = kind;
            if (kind >= 0 && !f.ProductsOn && kind != BankTables.Loan)
            {
                kind = -1;
                view.Row = null;
            }
            f.Quote = new BankQuote();
            if (kind >= 0)
            {
                f.Quote = QuoteOf(f, desk, score, view, kind);
            }
            var checks = new List<BankCheck>(5);
            ReadChecks(f, checks, kind, desk, score);
            f.Checks = checks.ToArray();
            if (!f.Lends)
            {
                int tips = CreditScoring.Tips(score, _tips);
                f.Tips = new string[tips];
                for (int i = 0; i < tips; i++)
                {
                    f.Tips[i] = FinanceText.Tip(score, _tips[i]);
                }
            }
        }

        // One row: the most, the rate, what borrowing the most would take each day, its fee, and whether it is on offer.
        private BankProduct ProductRow(BanksFacts f, CreditDesk desk, int p, int score)
        {
            var row = new BankProduct { Kind = p, Name = FinanceText.T(BankTables.OfferKinds[p]) };
            ProductAnswer answer;
            switch (p)
            {
                case BankTables.Loan:
                    {
                        float most = PhoneLending.MostNow(_bank, score), rate = PhoneLending.RateOffered(_bank, score);
                        answer = most >= CreditCatalogue.LoanLeast ? ProductAnswer.Yes : ProductAnswer.TooMuch;
                        row.Most = (int)most;
                        row.Rate = FinanceText.T("reallife_econ_row_rate", "rate", rate.ToString("0.#", Inv));
                        if (answer == ProductAnswer.Yes)
                        {
                            LoanQuote quote = PhoneLending.Quote(_bank, WholeMoney.Floor(most), score);
                            row.Daily = Money(quote.DailyPayment + quote.DailyInterest);
                            row.Fee = Money(quote.Fee);
                        }
                        break;
                    }
                case BankTables.Overdraft:
                case BankTables.Card:
                case BankTables.Car:
                case BankTables.Home:
                    {
                        ProductOffer offer = desk.Offer(ProductOf(p), _bank);
                        answer = offer.Answer;
                        if (answer == ProductAnswer.AlreadyOpen && offer.Bank != null && offer.Bank.Rank != _bank.Rank)
                        {
                            row.State = FinanceText.T("reallife_bank_row_held_with", "bank", offer.Bank.Name);
                            return row;
                        }
                        row.Most = offer.Most;
                        row.Rate = offer.RateBp > 0 ? FinanceApp.Rate(offer.RateBp) : null;
                        break;
                    }
                default:
                    answer = desk.Gate();   // a business loan and a consolidation say more on their page
                    break;
            }
            row.Open = answer == ProductAnswer.AlreadyOpen;
            row.On = answer == ProductAnswer.Yes;
            row.State = FinanceText.T(row.On ? "reallife_econ_row_on_offer" : row.Open ? "reallife_econ_row_open" : "reallife_econ_row_not_offered");
            return row;
        }

        private static string Range(UtilitiesWords w, int least, int most) => w.F("reallife_fin_range", "{least} to {most}", "least", Money(least), "most", Money(most));

        private static string Yearly(UtilitiesWords w, float rate) => w.F("reallife_fin_rate_year", "{rate}% a year", "rate", rate.ToString("0.#", Inv));

        private static void Lines(BankQuote q, params string[] pairs)
        {
            q.Labels = new string[pairs.Length / 2];
            q.Values = new string[pairs.Length / 2];
            for (int i = 0; i + 1 < pairs.Length; i += 2)
            {
                q.Labels[i / 2] = pairs[i];
                q.Values[i / 2] = pairs[i + 1];
            }
        }

        private static void Amount(BankQuote q, MoneyView view, int least, int most, UtilitiesWords w)
        {
            q.HasAmount = true;
            q.Least = least;
            q.Most = Math.Max(least, most);
            q.Amount = BankParts.Chosen(view.F[MoneyView.FAmount], BankParts.DefaultAmount(least, q.Most), least, q.Most);
            q.AmountHint = Range(w, least, q.Most);
        }

        private BankQuote QuoteOf(BanksFacts f, CreditDesk desk, CreditScore score, MoneyView view, int kind)
        {
            var q = new BankQuote();
            UtilitiesWords w = W;
            switch (kind)
            {
                case LoanKind: LoanQuoteOf(q, score, view, w); break;
                case OverdraftKind:
                case CardKind: LimitQuoteOf(q, desk, ProductOf(kind), w); break;
                case BusinessKind: BusinessQuoteOf(q, desk, view, w); break;
                case CarKind:
                case HomeKind: FinanceQuoteOf(q, desk, ProductOf(kind), view, w); break;
                default: ConsolidateQuoteOf(q, desk, w); break;
            }
            return q;
        }

        private void LoanQuoteOf(BankQuote q, CreditScore score, MoneyView view, UtilitiesWords w)
        {
            int most = (int)Math.Floor(PhoneLending.MostNow(_bank, score.Value));
            if (most < CreditCatalogue.LoanLeast)
            {
                q.Refused = FinanceText.T("reallife_econ_offer_nothing", "least", Money(CreditCatalogue.LoanLeast));
                return;
            }
            Amount(q, view, CreditCatalogue.LoanLeast, most, w);
            LoanQuote quote = PhoneLending.Quote(_bank, q.Amount, score.Value);
            q.Rate = Yearly(w, quote.AnnualRate);
            Lines(q, L("reallife_econ_offer_fee"), Money(quote.Fee), L("reallife_econ_offer_payment"), Money(quote.DailyPayment + quote.DailyInterest), L("reallife_tab_offer_repay"), Money(q.Amount + quote.Cost));
            var tomorrow = Books.Tomorrow(CreditLedger.File);
            q.Note = FinanceText.T("reallife_money_adds", "add", Money(quote.DailyPayment + quote.DailyInterest), "already", Money(tomorrow.Loans + tomorrow.Accounts));
        }

        private void LimitQuoteOf(BankQuote q, CreditDesk desk, ProductKind kind, UtilitiesWords w)
        {
            CreditAccount here = HeldHere(kind);
            if (here != null)
            {
                q.Refused = FinanceText.T("reallife_bank_open_here", "limit", Money(here.Agreed));
                return;
            }
            string other = HeldWith(kind);
            if (other != null)
            {
                q.Refused = FinanceText.T(kind == ProductKind.CreditCard ? "reallife_bank_card_elsewhere" : "reallife_bank_overdraft_elsewhere", "other", other, "bank", _bank.Name);
                return;
            }
            ProductOffer offer = desk.Offer(kind, _bank);
            if (offer.Answer != ProductAnswer.Yes)
            {
                q.Refused = Why(_bank, offer.Answer);
                return;
            }
            q.Rate = Yearly(w, offer.RateBp / 100f);
            Lines(q, L("reallife_econ_offer_limit"), Money(offer.Most), L("reallife_econ_offer_rate"), (offer.RateBp / 100f).ToString("0.#", Inv) + "%");
            q.Note = FinanceApp.AboutKind(kind);
        }

        private void BusinessQuoteOf(BankQuote q, CreditDesk desk, MoneyView view, UtilitiesWords w)
        {
            Books.Businesses(_shops);
            if (_shops.Count == 0)
            {
                q.Refused = FinanceText.T("reallife_econ_business_none");
                return;
            }
            int shopAt = Math.Max(0, Math.Min(_shops.Count - 1, view.F[MoneyView.FShop]));
            BuildingRegistration shop = _shops[shopAt];
            if (_shops.Count > 1)
            {
                q.StepLabel3 = L("reallife_econ_business_name");
                q.StepText3 = shop.BusinessName;
                q.Step3Prev = shopAt > 0 ? shopAt - 1 : -1;
                q.Step3Next = shopAt < _shops.Count - 1 ? shopAt + 1 : -1;
            }
            ProductOffer offer = desk.Business(shop, _bank, out float profit, out int days);
            q.Record = FinanceText.T("reallife_econ_business_record", "days", days.ToString(Inv), "profit", Money(profit));
            if (offer.Answer != ProductAnswer.Yes)
            {
                q.Refused = Why(_bank, offer.Answer);
                return;
            }
            Amount(q, view, CreditCatalogue.LoanLeast, offer.Most, w);
            LoanQuote quote = desk.BusinessQuote(_bank, q.Amount);
            q.Rate = Yearly(w, quote.AnnualRate);
            Lines(q, L("reallife_econ_offer_fee"), Money(quote.Fee), L("reallife_econ_offer_payment"), Money(quote.DailyPayment + quote.DailyInterest));
            q.Note = FinanceText.T(FinanceApp.AboutKeys[2]).Replace("{multiple}", ProductCatalogue.BusinessProfitMultiple.ToString(Inv));
        }

        private void FinanceQuoteOf(BankQuote q, CreditDesk desk, ProductKind kind, MoneyView view, UtilitiesWords w)
        {
            Relief relief = CreditLedger.File.Relief;
            if (relief.HasApproval && relief.ApprovedKind == (int)kind)
            {
                int today = SaveGameManager.Current.Day;
                int perDay = ProductTerms.AdvanceCharge(relief.ApprovedAmount, relief.ApprovedRateBp, 1, AccountRules.DaysPerYear);
                int charge = ReliefRules.CancelCharge(relief, today, AccountRules.DaysPerYear);
                q.HasApproval = true;
                q.Rate = Yearly(w, relief.ApprovedRateBp / 100f);
                q.ApprovalLine = FinanceText.T("reallife_econ_approval_stands", "amount", Money(relief.ApprovedAmount), "day", (relief.ApprovedDay + ProductCatalogue.ApprovalDays).ToString(Inv),
                    "deposit", relief.ApprovedDeposit + "%") + " " + FinanceText.T("reallife_econ_approval_charge", "per_day", Money(perDay), "charge", Money(charge));
                Lines(q, L("reallife_econ_offer_most"), Money(relief.ApprovedAmount));
                return;
            }
            ProductOffer offer = desk.Offer(kind, _bank);
            if (offer.Answer != ProductAnswer.Yes)
            {
                q.Refused = Why(_bank, offer.Answer);
                return;
            }
            int depositLeast = ProductTerms.DepositLeast(kind), depositStep = ProductTerms.DepositStep(kind), deposits = (ProductTerms.DepositMost(kind) - depositLeast) / depositStep + 1;
            int termLeast = ProductTerms.TermLeast(kind), termStep = ProductTerms.TermStep(kind), terms = (ProductTerms.TermMost(kind) - termLeast) / termStep + 1;
            int depositAt = Math.Max(0, Math.Min(deposits - 1, view.F[MoneyView.FDeposit])), termAt = Math.Max(0, Math.Min(terms - 1, view.F[MoneyView.FTerm]));
            int deposit = depositLeast + depositAt * depositStep, term = termLeast + termAt * termStep;
            int least = ProductTerms.FinanceLeast(kind);
            Amount(q, view, least, Math.Max(least, offer.Most), w);
            int own = ProductTerms.DepositFor(q.Amount, deposit), price = q.Amount + own;
            FinanceQuote quote = ProductTerms.Quote(price, deposit, q.Amount, term, offer.RateBp, AccountRules.DaysPerYear);
            q.Rate = Yearly(w, offer.RateBp / 100f);
            q.StepLabel1 = w.T("reallife_fin_step_deposit", "Deposit");
            q.StepText1 = deposit + "%";
            q.Step1Prev = depositAt > 0 ? depositAt - 1 : -1;
            q.Step1Next = depositAt < deposits - 1 ? depositAt + 1 : -1;
            q.StepLabel2 = w.T("reallife_fin_step_term", "Term");
            q.StepText2 = FinanceText.T("reallife_econ_row_days", "days", term.ToString(Inv));
            q.Step2Prev = termAt > 0 ? termAt - 1 : -1;
            q.Step2Next = termAt < terms - 1 ? termAt + 1 : -1;
            Lines(q, w.T("reallife_fin_l_price", "Price"), Money(price), w.T("reallife_fin_l_deposit", "Your deposit"), Money(own),
                w.T("reallife_fin_l_first", "First payment"), Money(quote.Instalment + quote.FirstInterest), w.T("reallife_fin_l_interest_term", "Interest over the term"), Money(quote.Interest));
            q.Note = FinanceApp.Detail(kind);
        }

        private void ConsolidateQuoteOf(BankQuote q, CreditDesk desk, UtilitiesWords w)
        {
            ProductAnswer answer = desk.Consolidation(_bank, out DebtTotals before, out DebtTotals after, out LoanQuote quote, out int count);
            if (before.Owed > 0)
            {
                q.Rate = Yearly(w, quote.AnnualRate);
                Lines(q, w.T("reallife_fin_l_now", "Now you owe"), Money(before.Owed), w.T("reallife_fin_l_now_day", "Now a day"), Money(before.Daily), w.T("reallife_fin_l_then", "Then you owe"),
                    Money(after.Owed), w.T("reallife_fin_l_then_day", "Then a day"), Money(after.Daily), L("reallife_econ_offer_fee"), Money(quote.Fee));
            }
            q.Note = FinanceText.T("reallife_bank_consolidate_about", "bank", _bank.Name);
            if (answer != ProductAnswer.Yes)
            {
                q.Refused = FinanceApp.NotConsolidated(answer, Why(_bank, answer));
            }
        }

        // What the bank checked, as the desk would: the rating against the line, what is overdue here, the amount against the most, and the applications on the file.
        private void ReadChecks(BanksFacts f, List<BankCheck> checks, int kind, CreditDesk desk, CreditScore score)
        {
            UtilitiesWords w = W;
            checks.Add(new BankCheck
            {
                Text = w.F("reallife_ui_bank_chk_rating", "Your rating is {score}; {bank} lends from {threshold}", "score", score.Value.ToString(Inv), "bank", _bank.Name, "threshold", f.Threshold.ToString(Inv)),
                Met = f.Lends
            });
            desk.Held(_bank, _debts);
            bool overdue = false;
            for (int i = 0; i < _debts.Count; i++)
            {
                overdue |= _debts[i].Ladder.Overdue > 0 || _debts[i].Ladder.Misses > 0;
            }
            checks.Add(new BankCheck { Text = w.T("reallife_ui_bank_chk_overdue", "Nothing you owe here is overdue"), Met = !overdue });
            int most = kind == LoanKind || kind < 0 ? f.Banks[f.Bank].Most : f.Quote.Most > 0 ? f.Quote.Most : f.Banks[f.Bank].Most;
            if (kind == LoanKind || kind < 0 || f.Quote.HasAmount)
            {
                int amount = f.Quote.HasAmount ? f.Quote.Amount : most;
                checks.Add(new BankCheck
                {
                    Text = w.F("reallife_ui_bank_chk_limit", "{amount} is under the {most} {bank} lends you", "amount", Money(amount), "most", Money(f.Quote.HasAmount ? f.Quote.Most : most), "bank", _bank.Name),
                    Met = f.Quote.HasAmount ? amount <= f.Quote.Most : most >= CreditCatalogue.LoanLeast
                });
            }
            checks.Add(new BankCheck
            {
                Text = w.F("reallife_ui_bank_chk_apps", "{count} applications in the last 90 days count against you", "count", score.Applications.ToString(Inv)), Met = score.Applications <= 1
            });
            if (kind == BusinessKind && !string.IsNullOrEmpty(f.Quote.Record))
            {
                checks.Add(new BankCheck { Text = f.Quote.Record, Met = f.Quote.Refused == null });
            }
        }

        // ------------------------------------------------------------------------------------------------- doing

        public bool Do(MoneyAct act, MoneyView view, int id, int value)
        {
            try
            {
                bool done = DoAct(act, view, id);
                if (done)
                {
                    Moved();
                }
                return done;
            }
            catch (Exception e)
            {
                EconomyDirector.Logger?.Warn("Banks (kit): " + act + " failed: " + e.Message);
                return false;
            }
        }

        private bool DoAct(MoneyAct act, MoneyView view, int id)
        {
            CreditDesk desk = Desk;
            if (desk == null || !CreditLedger.Ready || BankTerms.Banks == null || BankTerms.Banks.Count == 0)
            {
                return false;
            }
            _bank = BankTerms.Banks[Math.Max(0, Math.Min(Math.Min(BankRules.AppsMost, BankTerms.Banks.Count) - 1, view.Bank))];
            switch (act)
            {
                case MoneyAct.Pay:
                case MoneyAct.PayOff:
                case MoneyAct.PayOverdue:
                case MoneyAct.Plan:
                case MoneyAct.CannotPay:
                case MoneyAct.Draw:
                case MoneyAct.PayMinimum:
                case MoneyAct.Close: return DoDebt(desk, act, view, id);
                case MoneyAct.DailyPayment:
                    {
                        EconomyDirector director = EconomyDirector.Instance;
                        if (director == null)
                        {
                            return false;
                        }
                        director.KitEconoView();
                        return true;
                    }
                case MoneyAct.OpenOverdraft: return OpenLimit(desk, ProductKind.Overdraft);
                case MoneyAct.OpenCard: return OpenLimit(desk, ProductKind.CreditCard);
                case MoneyAct.ApplyLoan: return ApplyLoan(view);
                case MoneyAct.ApplyBusiness: return ApplyBusiness(desk, view);
                case MoneyAct.ApproveFinance: return Approve(desk, view);
                case MoneyAct.CancelApproval: return CancelApproval(desk);
                case MoneyAct.Consolidate: return Consolidate(desk);
                default: return false;
            }
        }

        private bool Said(ProductAnswer answer)
        {
            if (answer != ProductAnswer.Yes)
            {
                Tell(NotificationType.Error, "reallife_econ_note_not_done", new Dictionary<string, string> { { "why", Why(_bank, answer) } });
                return false;
            }
            return true;
        }

        private static void Tell(NotificationType type, string key, Dictionary<string, string> data = null)
        {
            Notifications.Show(type, key, data, 9f, null, null, true, false);
        }

        // An answer in the game's box is run behind the mod's guard, and the screen reads again after it.
        private void Guarded(Action act)
        {
            try
            {
                act();
            }
            catch (Exception e)
            {
                EconomyDirector.Logger?.Warn("Banks (kit): an answer failed: " + e.Message);
            }
            Moved();
        }

        // The debt a button is for: an account by its number, else the game loan picked on the Loans tab.
        private bool Find(CreditDesk desk, MoneyView view, int number, out CreditAccount account, out LoanMark mark)
        {
            account = null;
            mark = null;
            desk.Held(_bank, _debts);
            for (int i = 0; i < _debts.Count; i++)
            {
                Debt d = _debts[i];
                if (d.Account != null && number > 0 && d.Account.Id == number)
                {
                    account = d.Account;
                }
                else if (d.Account != null && number == 0 && view.Row != null && view.Row == "acct:" + d.Account.Id.ToString(Inv))
                {
                    account = d.Account;
                }
                else if (d.Account == null && view.Row != null && view.Row == FinanceApp.LoanRowId(d.Mark))
                {
                    mark = d.Mark;
                }
            }
            return account != null || mark != null;
        }

        private bool DoDebt(CreditDesk desk, MoneyAct act, MoneyView view, int number)
        {
            if (!Find(desk, view, number, out CreditAccount a, out LoanMark m))
            {
                return false;
            }
            if (act == MoneyAct.PayOverdue || act == MoneyAct.Plan || act == MoneyAct.CannotPay)
            {
                Debt debt = a != null ? new Debt(a) : new Debt(m);
                if (act == MoneyAct.CannotPay)
                {
                    desk.CannotPay(debt);
                    return true;
                }
                return Said(act == MoneyAct.PayOverdue ? desk.PayOverdue(debt) : desk.AgreePlan(debt));
            }
            if (a == null)
            {
                return PayGameLoan(desk, act, view, m);
            }
            string name = CreditDesk.Name(a);
            switch (act)
            {
                case MoneyAct.Pay:
                case MoneyAct.PayOff:
                case MoneyAct.PayMinimum:
                    {
                        int suggested = a.Frozen ? a.Owed : Math.Min(a.Owed, Math.Max(a.Ladder.Overdue, AccountRules.DueTomorrow(a, SaveGameManager.Current.Day)));
                        int amount = act == MoneyAct.PayOff ? a.Owed : act == MoneyAct.PayMinimum ? a.MinimumLeft : Math.Max(1, Math.Min(a.Owed, view.F[MoneyView.FPay] > 0 ? view.F[MoneyView.FPay] : suggested));
                        ProductAnswer answer = desk.Pay(a, amount, out int used);
                        if (answer == ProductAnswer.Yes)
                        {
                            Tell(NotificationType.Success, a.Closed ? "reallife_econ_note_paid_off" : "reallife_econ_note_paid", new Dictionary<string, string>
                            {
                                { "account", name }, { "amount", Money(used) }, { "left", Money(a.Owed) }
                            });
                            view.F[MoneyView.FPay] = 0;
                        }
                        return Said(answer);
                    }
                case MoneyAct.Draw:
                    {
                        int most = AccountRules.MostDraw(a);
                        int amount = Math.Max(1, Math.Min(most, view.F[MoneyView.FDraw] > 0 ? view.F[MoneyView.FDraw] : most));
                        ProductAnswer answer = desk.Draw(a, amount, out int fee);
                        if (answer == ProductAnswer.Yes)
                        {
                            Tell(NotificationType.Success, "reallife_econ_note_drawn", new Dictionary<string, string> { { "amount", Money(amount) }, { "fee", Money(fee) }, { "owed", Money(a.Owed) } });
                            view.F[MoneyView.FDraw] = 0;
                        }
                        return Said(answer);
                    }
                case MoneyAct.Close:
                    return Said(desk.CloseAccount(a));
                default:
                    return false;
            }
        }

        // A game loan: paid a part by hand, or off after the game's own question (as the old app asked it).
        private bool PayGameLoan(CreditDesk desk, MoneyAct act, MoneyView view, LoanMark m)
        {
            var loan = m.Ref as Loan;
            if (loan == null)
            {
                return false;
            }
            bool frozen = (m.Held & LoanHold.Frozen) != 0;
            int whole = WholeMoney.Round(Math.Ceiling((double)loan.remainingAmount));
            if (act == MoneyAct.Pay)
            {
                int amount = Math.Max(1, Math.Min(whole, view.F[MoneyView.FPay] > 0 ? view.F[MoneyView.FPay] : frozen ? whole : Math.Min(whole, loan.dailyPayment)));
                return PaidLoan(desk, m, amount, view);
            }
            if (act != MoneyAct.PayOff)
            {
                return false;
            }
            string bank = _bank.Name;
            GameConfirm.Show("reallife_bank_confirm_payoff_title".Localize(new { bank }), "reallife_bank_confirm_payoff".Localize(new { amount = Money(whole), bank }),
                () => Guarded(() => PaidLoan(desk, m, whole, view)), null, "reallife_econ_account_pay_off", "common_cancel", false, false);
            return true;
        }

        private bool PaidLoan(CreditDesk desk, LoanMark m, int amount, MoneyView view)
        {
            string name = _bank.Name;
            ProductAnswer answer = desk.PayLoan(m, amount, out int used);
            if (answer == ProductAnswer.Yes)
            {
                var loan = m.Ref as Loan;
                bool gone = loan == null || loan.remainingAmount <= 0f;
                Tell(NotificationType.Success, gone ? "reallife_bank_note_loan_paid_off" : "reallife_bank_note_loan_paid", new Dictionary<string, string>
                {
                    { "bank", name }, { "amount", Money(used) }, { "left", Money(gone ? 0f : loan.remainingAmount) }
                });
                view.F[MoneyView.FPay] = 0;
                if (gone)
                {
                    view.Row = null;
                }
            }
            return Said(answer);
        }

        private bool OpenLimit(CreditDesk desk, ProductKind kind)
        {
            ProductOffer offer = desk.Offer(kind, _bank);
            if (offer.Answer != ProductAnswer.Yes)
            {
                return Said(offer.Answer);
            }
            Lender bank = _bank;
            string kindName = FinanceText.T(ProductCatalogue.KindKeys[(int)kind]);
            GameConfirm.Show("reallife_econ_confirm_open_title".Localize(new { kind = kindName }),
                "reallife_econ_confirm_open_body".Localize(new { kind = kindName, limit = Money(offer.Most), rate = (offer.RateBp / 100f).ToString("0.#"), lender = bank.Name, detail = FinanceApp.Detail(kind) }),
                () => Guarded(() =>
                {
                    ProductAnswer answer = desk.OpenAccount(kind, bank, out CreditAccount account);
                    if (answer == ProductAnswer.Yes)
                    {
                        Tell(NotificationType.Success, "reallife_econ_note_opened", new Dictionary<string, string> { { "account", CreditDesk.Name(account) }, { "limit", Money(account.Agreed) } });
                    }
                    else
                    {
                        Said(answer);
                    }
                }), null, "reallife_econ_offer_open", "common_cancel", false, false);
            return true;
        }

        // The game's own question: everything the loan costs is said before it is taken.
        private bool ApplyLoan(MoneyView view)
        {
            Lender bank = _bank;
            if (!CreditLedger.CanBorrow(out CreditRefusal _))
            {
                EconomyDirector.Instance.TellRefused();
                return false;
            }
            int score = CreditLedger.Score.Value;
            int most = (int)Math.Floor(PhoneLending.MostNow(bank, score));
            if (most < CreditCatalogue.LoanLeast)
            {
                return false;
            }
            int amount = BankParts.Chosen(view.F[MoneyView.FAmount], BankParts.DefaultAmount(CreditCatalogue.LoanLeast, most), CreditCatalogue.LoanLeast, most);
            LoanQuote quote = PhoneLending.Quote(bank, amount, score);
            GameConfirm.Show("reallife_econ_confirm_title".Localize(new { lender = bank.Name }),
                "reallife_econ_confirm_body".Localize(new
                {
                    amount = Money(amount), lender = bank.Name, rate = quote.AnnualRate.ToString("0.#"), payment = Money(quote.DailyPayment), interest = Money(quote.DailyInterest),
                    days = quote.Days.ToString(), fee = Money(quote.Fee), cost = Money(quote.Cost)
                }), () => Guarded(() => Lent(bank, amount, view)), null, "reallife_econ_offer_apply", "common_cancel", false, false);
            return true;
        }

        private void Lent(Lender bank, int amount, MoneyView view)
        {
            LoanAnswer answer = EconomyDirector.Instance.Borrow(bank, amount, out LoanQuote quote);   // the Finance app's own call: the rating and the limit are asked again
            switch (answer)
            {
                case LoanAnswer.Approved:
                    Tell(NotificationType.Success, "reallife_econ_note_lent", new Dictionary<string, string>
                    {
                        { "lender", bank.Name }, { "amount", Money(amount) }, { "fee", Money(quote.Fee) }, { "payment", Money(quote.DailyPayment + quote.DailyInterest) }
                    });
                    view.Row = null;
                    view.F[MoneyView.FAmount] = 0;
                    break;
                case LoanAnswer.TooMuch:
                    Tell(NotificationType.Error, "reallife_econ_note_too_much", new Dictionary<string, string> { { "lender", bank.Name }, { "most", Money(PhoneLending.MostNow(bank, CreditLedger.Value)) } });
                    break;
                case LoanAnswer.TooLittle:
                    Tell(NotificationType.Error, "reallife_econ_note_too_little", new Dictionary<string, string> { { "least", Money(CreditCatalogue.LoanLeast) } });
                    break;
                default:
                    if (CreditLedger.Ready)
                    {
                        EconomyDirector.Instance.TellRefused();
                    }
                    break;
            }
        }

        private bool ApplyBusiness(CreditDesk desk, MoneyView view)
        {
            Books.Businesses(_shops);
            if (_shops.Count == 0)
            {
                return false;
            }
            BuildingRegistration shop = _shops[Math.Max(0, Math.Min(_shops.Count - 1, view.F[MoneyView.FShop]))];
            Lender bank = _bank;
            ProductOffer offer = desk.Business(shop, bank, out float _, out int _);
            if (offer.Answer != ProductAnswer.Yes)
            {
                return Said(offer.Answer);
            }
            int amount = BankParts.Chosen(view.F[MoneyView.FAmount], BankParts.DefaultAmount(CreditCatalogue.LoanLeast, offer.Most), CreditCatalogue.LoanLeast, offer.Most);
            LoanQuote quote = desk.BusinessQuote(bank, amount);
            GameConfirm.Show("reallife_econ_confirm_title".Localize(new { lender = bank.Name }),
                "reallife_econ_confirm_business".Localize(new
                {
                    amount = Money(amount), lender = bank.Name, business = shop.BusinessName, rate = quote.AnnualRate.ToString("0.#"), payment = Money(quote.DailyPayment),
                    interest = Money(quote.DailyInterest), days = quote.Days.ToString(), fee = Money(quote.Fee), share = ((int)Math.Round(ProductCatalogue.BusinessDivertShare * 100f)) + "%"
                }),
                () => Guarded(() =>
                {
                    ProductAnswer answer = desk.BorrowOnBusiness(shop, bank, amount, out LoanQuote made);
                    if (answer == ProductAnswer.Yes)
                    {
                        Tell(NotificationType.Success, "reallife_econ_note_lent", new Dictionary<string, string>
                        {
                            { "lender", bank.Name }, { "amount", Money(amount) }, { "fee", Money(made.Fee) }, { "payment", Money(made.DailyPayment + made.DailyInterest) }
                        });
                        view.Row = null;
                    }
                    else
                    {
                        Said(answer);
                    }
                }), null, "reallife_econ_offer_apply", "common_cancel", false, false);
            return true;
        }

        private bool Approve(CreditDesk desk, MoneyView view)
        {
            int product = KindOfRow(view.Row);
            if (product != CarKind && product != HomeKind)
            {
                return false;
            }
            ProductKind kind = ProductOf(product);
            Lender bank = _bank;
            ProductOffer offer = desk.Offer(kind, bank);
            if (offer.Answer != ProductAnswer.Yes)
            {
                return Said(offer.Answer);
            }
            int depositLeast = ProductTerms.DepositLeast(kind), depositStep = ProductTerms.DepositStep(kind);
            int termLeast = ProductTerms.TermLeast(kind), termStep = ProductTerms.TermStep(kind);
            int deposit = depositLeast + Math.Max(0, view.F[MoneyView.FDeposit]) * depositStep, term = termLeast + Math.Max(0, view.F[MoneyView.FTerm]) * termStep;
            int least = ProductTerms.FinanceLeast(kind), most = Math.Max(least, offer.Most);
            int amount = BankParts.Chosen(view.F[MoneyView.FAmount], BankParts.DefaultAmount(least, most), least, most);
            int own = ProductTerms.DepositFor(amount, deposit);
            string kindName = FinanceText.T(ProductCatalogue.KindKeys[(int)kind]);
            int day = SaveGameManager.Current.Day;
            GameConfirm.Show("reallife_econ_confirm_finance_title".Localize(new { kind = kindName }),
                (kind == ProductKind.Mortgage ? "reallife_econ_confirm_home" : "reallife_econ_confirm_car").Localize(new
                {
                    amount = Money(amount), lender = bank.Name, deposit = deposit + "%", own = Money(own), days = term.ToString(), rate = (offer.RateBp / 100f).ToString("0.#"),
                    day = (day + ProductCatalogue.ApprovalDays).ToString(), share = ((int)Math.Round(ProductCatalogue.RepossessionValueShare * 100f)) + "%",
                    per_day = Money(ProductTerms.AdvanceCharge(amount, offer.RateBp, 1, AccountRules.DaysPerYear))
                }),
                () => Guarded(() =>
                {
                    ProductAnswer answer = desk.Approve(kind, bank, amount, deposit, term);
                    if (answer == ProductAnswer.Yes)
                    {
                        Tell(NotificationType.Success, "reallife_econ_note_approved", new Dictionary<string, string> { { "amount", Money(amount) }, { "day", (day + ProductCatalogue.ApprovalDays).ToString() } });
                    }
                    else
                    {
                        Said(answer);
                    }
                }), null, "reallife_econ_finance_approve", "common_cancel", false, false);
            return true;
        }

        private bool CancelApproval(CreditDesk desk)
        {
            Relief relief = CreditLedger.File.Relief;
            if (!relief.HasApproval)
            {
                return false;
            }
            int today = SaveGameManager.Current.Day, held = today - relief.ApprovedDay + 1;
            int charge = ReliefRules.CancelCharge(relief, today, AccountRules.DaysPerYear);
            Lender bank = BankTerms.At(relief.ApprovedStreet, relief.ApprovedNumber);
            GameConfirm.Show("reallife_econ_approval_cancel".Localize(), "reallife_econ_confirm_cancel_approval".Localize(new
                {
                    amount = Money(relief.ApprovedAmount), lender = bank != null ? bank.Name : FinanceText.T("reallife_econ_lender_bank"), charge = Money(charge), days = held.ToString()
                }), () => Guarded(() => desk.CancelApproval(false)), null, "reallife_econ_approval_cancel", "common_cancel", false, true);
            return true;
        }

        private bool Consolidate(CreditDesk desk)
        {
            Lender bank = _bank;
            ProductAnswer answer = desk.Consolidation(bank, out DebtTotals before, out DebtTotals after, out LoanQuote quote, out int count);
            if (answer != ProductAnswer.Yes)
            {
                return Said(answer);
            }
            GameConfirm.Show("reallife_econ_confirm_title".Localize(new { lender = bank.Name }),
                "reallife_econ_confirm_consolidate".Localize(new
                {
                    count = count.ToString(), owed = Money(before.Owed), amount = Money(after.Owed), lender = bank.Name, rate = quote.AnnualRate.ToString("0.#"), daily = Money(after.Daily),
                    days = after.Days.ToString(), fee = Money(quote.Fee), was = Money(before.Daily)
                }),
                () => Guarded(() =>
                {
                    ProductAnswer done = desk.Consolidate(bank);
                    if (done == ProductAnswer.Yes)
                    {
                        Tell(NotificationType.Success, "reallife_econ_note_consolidated", new Dictionary<string, string> { { "lender", bank.Name }, { "amount", Money(after.Owed) }, { "daily", Money(after.Daily) } });
                    }
                    else
                    {
                        Said(done);
                    }
                }), null, "reallife_econ_consolidate_do", "common_cancel", false, false);
            return true;
        }
    }
}
