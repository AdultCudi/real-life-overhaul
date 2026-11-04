using System;
using System.Collections.Generic;
using Localizor;
using RealLifeOverhaul.DaySeries;
using RealLifeOverhaul.Economy;
using RealLifeOverhaul.Rivals;
using UI.Notification;

namespace RealLifeOverhaul.Ui
{
    public sealed partial class FinanceLive
    {
        private const int Overdraft = 0, Card = 1, BusinessLoan = 2, CarFinance = 3, HomeLoan = 4, Consolidate = 5, ProductCount = 6;

        private readonly List<BuildingRegistration> _shops = new List<BuildingRegistration>(8);

        private static CreditDesk Desk => EconomyDirector.Instance != null ? EconomyDirector.Instance.Desk : null;

        private static bool ProductsOn => Settings.EconomyProducts && Desk != null && Desk.Running;

        private static ProductKind KindOf(int product)
        {
            switch (product)
            {
                case Overdraft: return ProductKind.Overdraft;
                case Card: return ProductKind.CreditCard;
                case CarFinance: return ProductKind.CarFinance;
                default: return ProductKind.Mortgage;
            }
        }

        private static UiIcon ProductIcon(int product)
        {
            switch (product)
            {
                case Overdraft: return UiIcon.Wallet;
                case Card: return UiIcon.Coin;
                case BusinessLoan: return UiIcon.Store;
                case CarFinance: return UiIcon.Car;
                case HomeLoan: return UiIcon.Home;
                default: return UiIcon.Reset;
            }
        }

        // ------------------------------------------------------------------------------------------------- the list of offers

        /// <summary>The banks' loans, then the other kinds of credit, one row each, in the order both Credit and Borrow list them (a row's place is its offer).</summary>
        private void OfferRows(List<FinanceRow> rows, CreditScore score, out int lenderCount)
        {
            UtilitiesWords w = W;
            IReadOnlyList<Lender> lenders = BankTerms.Lenders;
            lenderCount = lenders.Count;
            string fee = (CreditCatalogue.PhoneFeeShare * 100f).ToString("0.#", Inv);
            for (int i = 0; i < lenders.Count; i++)
            {
                Lender lender = lenders[i];
                float most = PhoneLending.MostNow(lender, score.Value), rate = PhoneLending.RateOffered(lender, score.Value);
                bool lends = most >= CreditCatalogue.LoanLeast;
                rows.Add(new FinanceRow
                {
                    Name = lender.Name, Icon = UiIcon.Bank,
                    Sub = lends ? w.F("reallife_fin_up_to", "Up to {most}, fee {fee}%", "most", Money(most), "fee", fee) : FinanceText.T("reallife_econ_row_not_offered"),
                    Cells = new[] { lends ? FinanceText.T("reallife_econ_row_rate", "rate", rate.ToString("0.#", Inv)) : string.Empty }
                });
            }
            if (!ProductsOn)
            {
                return;
            }
            CreditDesk desk = Desk;
            for (int p = 0; p < ProductCount; p++)
            {
                ProductOffer offer = ProductOfferOf(desk, p);
                bool offered = offer.Answer == ProductAnswer.Yes, open = offer.Answer == ProductAnswer.AlreadyOpen;
                string sub = offered || open
                    ? (p == Overdraft || p == Card ? w.F("reallife_fin_limit", "Limit {limit}", "limit", Money(offer.Most)) : p == CarFinance || p == HomeLoan
                        ? w.F("reallife_fin_from", "From {amount}", "amount", Money(ProductTerms.FinanceLeast(KindOf(p)))) : FinanceText.T(FinanceApp.AboutKeys[p]).Split('.')[0])
                    : FinanceApp.Why(offer.Answer);
                rows.Add(new FinanceRow
                {
                    Name = FinanceText.T(FinanceApp.ProductKeys[p]), Sub = sub, Icon = ProductIcon(p),
                    Cells = new[] { offer.RateBp > 0 ? Rate(offer.RateBp) : string.Empty }
                });
            }
        }

        private static ProductOffer ProductOfferOf(CreditDesk desk, int p)
        {
            var offer = default(ProductOffer);
            switch (p)
            {
                case Overdraft: offer = desk.Offer(ProductKind.Overdraft); break;
                case Card: offer = desk.Offer(ProductKind.CreditCard); break;
                case CarFinance: offer = desk.Offer(ProductKind.CarFinance); break;
                case HomeLoan: offer = desk.Offer(ProductKind.Mortgage); break;
                default: offer.Answer = desk.Gate(); break;   // a business loan and a consolidation say more on their page
            }
            return offer;
        }

        // ------------------------------------------------------------------------------------------------- Borrow

        private void ReadBorrow(FinanceFacts f, FinanceView v)
        {
            FinanceBorrowFacts b = f.Borrow;
            CreditScore score = CreditLedger.Score;
            UtilitiesWords w = W;
            var rows = new List<FinanceRow>(12);
            OfferRows(rows, score, out int lenders);
            v.Offer = FinanceRules.Clamp(v.Offer, rows.Count);
            b.List = FinanceKitTables.Make(FinanceKitTables.OfferColumns(w, false), rows, v.Offer, w.T("reallife_fin_no_offers", "No offers"),
                FinanceText.T("reallife_econ_row_no_banks"), UiIcon.Bank);
            b.FeedTitle = w.T("reallife_fin_feed_title", "Others are borrowing too");
            b.Feed = FinanceKitTables.Make(FinanceKitTables.FeedColumns(w), FeedRows(w), -1, FinanceText.T("reallife_money_feed_empty"), string.Empty, UiIcon.People);
            b.Refused = null;
            b.Quote = null;
            if (!score.Lends)
            {
                b.Refused = FinanceText.T("reallife_econ_borrow_refused", "score", score.Value.ToString(Inv), "grade", FinanceText.Grade(score.Grade),
                    "threshold", CreditCatalogue.LendingThreshold.ToString(Inv));
                int tips = CreditScoring.Tips(score, _tips);
                b.RefusedTips = new string[tips];
                for (int i = 0; i < tips; i++)
                {
                    b.RefusedTips[i] = FinanceText.Tip(score, _tips[i]);
                }
                return;
            }
            if (v.Offer < 0)
            {
                return;
            }
            b.Quote = v.Offer < lenders ? LoanQuoteFacts(BankTerms.Lenders[v.Offer], score, v, w) : ProductQuoteFacts(v.Offer - lenders, score, v, w);
        }

        private static List<FinanceRow> FeedRows(UtilitiesWords w)
        {
            DaySeriesBook book = DaySeriesDirector.Book;
            var rows = new List<FinanceRow>(6);
            int n = book != null ? book.LoanCount : 0;
            for (int i = n - 1; i >= 0 && rows.Count < 6; i--)
            {
                DayLoan loan = book.LoanAt(i);
                string bank = BankTerms.Banks != null && loan.Rank >= 0 && loan.Rank < BankTerms.Banks.Count ? BankTerms.Banks[loan.Rank].Name : FinanceText.T("reallife_banks_app");
                rows.Add(new FinanceRow
                {
                    Name = loan.Who, Hue = UiColour.HueOf(loan.Who), Sub = w.F("reallife_fin_took_at", "took a loan at {bank}", "bank", bank),
                    Cells = new[] { Money(loan.Amount), loan.Day.ToString(Inv) }
                });
            }
            return rows;
        }

        /// <summary>The amount a page starts at: half of the most it lends, on the round figures, never under the least.</summary>
        private static int DefaultAmount(int least, int most)
        {
            int pick = least;
            int[] steps = FinanceApp.AmountSteps;
            for (int i = 0; i < steps.Length; i++)
            {
                if (steps[i] >= least && steps[i] <= most / 2)
                {
                    pick = steps[i];
                }
            }
            return Math.Max(least, Math.Min(most, pick));
        }

        private static int ChosenAmount(FinanceView v, int least, int most)
        {
            int amount = v.Amount > 0 ? v.Amount : DefaultAmount(least, most);
            amount = Math.Max(least, Math.Min(Math.Max(least, most), amount));
            v.Amount = amount;
            return amount;
        }

        private static void Range(FinanceQuoteFacts q, int amount, int least, int most, UtilitiesWords w)
        {
            q.HasAmount = true;
            q.Amount = amount;
            q.Least = least;
            q.Most = most;
            q.AmountPrev = FinanceRules.StepAmount(amount, least, most, false, FinanceApp.AmountSteps);
            q.AmountNext = FinanceRules.StepAmount(amount, least, most, true, FinanceApp.AmountSteps);
            q.AmountHint = w.F("reallife_fin_range", "{least} to {most}", "least", Money(least), "most", Money(most));
        }

        private static void Lines(FinanceQuoteFacts q, params string[] pairs)
        {
            q.Labels = new string[pairs.Length / 2];
            q.Values = new string[pairs.Length / 2];
            for (int i = 0; i + 1 < pairs.Length; i += 2)
            {
                q.Labels[i / 2] = pairs[i];
                q.Values[i / 2] = pairs[i + 1];
            }
        }

        // A bank's loan by phone: the amount, what it costs over the term, and what you still owe day by day.
        private FinanceQuoteFacts LoanQuoteFacts(Lender lender, CreditScore score, FinanceView v, UtilitiesWords w)
        {
            var q = new FinanceQuoteFacts();
            int most = (int)Math.Floor(PhoneLending.MostNow(lender, score.Value));
            int least = CreditCatalogue.LoanLeast;
            q.Name = lender.Name;
            q.Kind = FinanceText.T(FinanceTables.OfferKinds[0]);
            if (most < least)
            {
                q.Rate = string.Empty;
                q.Refused = FinanceText.T("reallife_econ_offer_nothing", "least", Money(least));
                return q;
            }
            int amount = ChosenAmount(v, least, most);
            LoanQuote quote = PhoneLending.Quote(lender, amount, score.Value);
            q.Rate = w.F("reallife_fin_rate_year", "{rate}% a year", "rate", quote.AnnualRate.ToString("0.#", Inv));
            q.Big = w.F("reallife_fin_a_day", "{money} a day", "money", Money(quote.DailyPayment + quote.DailyInterest));
            q.BigNote = FinanceText.T("reallife_econ_borrow_fee_note", "share", (CreditCatalogue.PhoneFeeShare * 100f).ToString("0.#", Inv), "least", Money(CreditCatalogue.PhoneFeeLeast));
            Range(q, amount, least, most, w);
            Lines(q, w.T("reallife_fin_l_borrow", "You borrow"), Money(amount), Label("reallife_econ_offer_fee"), Money(quote.Fee),
                w.F("reallife_fin_l_interest", "Interest over {days} days", "days", quote.Days.ToString(Inv)), Money(quote.DailyInterest * quote.Days),
                Label("reallife_econ_offer_payment"), Money(quote.DailyPayment), w.T("reallife_fin_l_total", "All you repay"), Money(amount + quote.Cost));
            float already = Books.Tomorrow(CreditLedger.File).Loans + Books.Tomorrow(CreditLedger.File).Accounts;
            q.Note = FinanceText.T("reallife_money_adds", "add", Money(quote.DailyPayment + quote.DailyInterest), "already", Money(already));
            float best = PhoneLending.RateOffered(lender, CreditCatalogue.ScoreMax);
            q.Note2 = quote.AnnualRate > best + 0.05f
                ? FinanceText.T("reallife_tab_offer_rating_adds", "points", (quote.AnnualRate - best).ToString("0.#", Inv), "best", best.ToString("0.#", Inv))
                : FinanceText.T("reallife_tab_offer_rating_best");
            q.TakeLabel = FinanceText.T("reallife_money_take");
            q.CanTake = true;
            q.Owing = OwingChart(w, amount, quote);
            return q;
        }

        // What you still owe at the end of each stretch of the term: the loan's own payments take off the principal a day at a time.
        private UiChartModel OwingChart(UtilitiesWords w, int amount, LoanQuote quote)
        {
            const int points = 12;
            int today = RivalGame.Day;
            var values = new float[points];
            var days = new int[points];
            for (int i = 0; i < points; i++)
            {
                int d = quote.Days > 0 ? (int)((long)quote.Days * i / (points - 1)) : 0;
                values[i] = Math.Max(0f, amount - (float)quote.DailyPayment * d);
                days[i] = today + d;
            }
            string title = w.T("reallife_fin_owing_chart", "What you still owe, day by day");
            return FinanceRules.Chart(title, new[] { title }, new[] { values }, days, points, Money, string.Empty, string.Empty);
        }

        private static UtilitiesStep Step(string label, string text, string hint, int index, int count, out FinanceStep move, int which)
        {
            move = new FinanceStep { Label = label, Text = text, Hint = hint, Prev = index > 0 ? index - 1 : -1, Next = index < count - 1 ? index + 1 : -1, Which = which };
            return new UtilitiesStep { Label = label, Value = text, Hint = hint, CanLess = index > 0, CanMore = index < count - 1 };
        }

        // The other kinds of credit: each as the old page wrote it, with the terms and the one button that does it.
        private FinanceQuoteFacts ProductQuoteFacts(int product, CreditScore score, FinanceView v, UtilitiesWords w)
        {
            var q = new FinanceQuoteFacts();
            if (!ProductsOn || product < 0 || product >= ProductCount)
            {
                q.Refused = FinanceText.T("reallife_econ_why_off");
                return q;
            }
            CreditDesk desk = Desk;
            q.Name = FinanceText.T(FinanceApp.ProductKeys[product]);
            q.Kind = FinanceText.T("reallife_econ_tab_borrow");
            q.Note = ProductAbout(product);
            switch (product)
            {
                case Overdraft:
                case Card: LimitQuote(q, desk, product, w); break;
                case BusinessLoan: BusinessQuoteFacts(q, desk, v, w); break;
                case CarFinance:
                case HomeLoan: FinanceQuoteOf(q, desk, product, v, w); break;
                default: ConsolidateQuote(q, desk, v, w); break;
            }
            return q;
        }

        private static string ProductAbout(int product)
        {
            string text = FinanceText.T(FinanceApp.AboutKeys[product]);
            return product == Card ? text.Replace("{days}", ProductCatalogue.CardStatementDays.ToString(Inv))
                : product == BusinessLoan ? text.Replace("{multiple}", ProductCatalogue.BusinessProfitMultiple.ToString(Inv)) : text;
        }

        private static void LimitQuote(FinanceQuoteFacts q, CreditDesk desk, int product, UtilitiesWords w)
        {
            ProductKind kind = KindOf(product);
            ProductOffer offer = desk.Offer(kind);
            if (offer.Answer != ProductAnswer.Yes && offer.Answer != ProductAnswer.AlreadyOpen)
            {
                q.Refused = FinanceApp.Why(offer.Answer);
                return;
            }
            q.Rate = w.F("reallife_fin_rate_year", "{rate}% a year", "rate", (offer.RateBp / 100f).ToString("0.#", Inv));
            q.Big = w.F("reallife_fin_limit", "Limit {limit}", "limit", Money(offer.Most));
            if (offer.Answer == ProductAnswer.AlreadyOpen)
            {
                Lines(q, Label("reallife_econ_offer_limit"), Money(offer.Most), Label("reallife_econ_offer_drawn"), Money(offer.Account.Owed));
                q.Note2 = FinanceText.T("reallife_econ_why_open");
                return;
            }
            Lines(q, Label("reallife_econ_offer_limit"), Money(offer.Most), Label("reallife_econ_offer_rate"), (offer.RateBp / 100f).ToString("0.#",
                Inv) + "%", Label("reallife_econ_offer_with"), offer.Bank.Name);
            q.TakeLabel = FinanceText.T("reallife_econ_offer_open");
            q.CanTake = true;
        }

        private void BusinessQuoteFacts(FinanceQuoteFacts q, CreditDesk desk, FinanceView v, UtilitiesWords w)
        {
            Books.Businesses(_shops);
            if (_shops.Count == 0)
            {
                q.Refused = FinanceText.T("reallife_econ_business_none");
                return;
            }
            v.Pick = Math.Max(0, Math.Min(_shops.Count - 1, v.Pick));
            BuildingRegistration shop = _shops[v.Pick];
            var steps = new List<UtilitiesStep>(2);
            var moves = new List<FinanceStep>(2);
            if (_shops.Count > 1)
            {
                steps.Add(Step(FinanceText.T("reallife_econ_business_name").TrimEnd(' ', ':'), shop.BusinessName, null, v.Pick, _shops.Count, out FinanceStep move, FinanceFace.StepPick));
                moves.Add(move);
            }
            ProductOffer offer = desk.Business(shop, out float profit, out int days);
            string record = FinanceText.T("reallife_econ_business_record", "days", days.ToString(Inv), "profit", Money(profit));
            if (offer.Answer != ProductAnswer.Yes)
            {
                q.Refused = record + " " + FinanceApp.Why(offer.Answer);
                q.Steps = steps.ToArray();
                q.StepMoves = moves.ToArray();
                return;
            }
            int most = offer.Most, least = CreditCatalogue.LoanLeast;
            int amount = ChosenAmount(v, least, most);
            LoanQuote quote = desk.BusinessQuote(offer.Bank, amount);
            q.Kind = offer.Bank.Name;
            q.Rate = w.F("reallife_fin_rate_year", "{rate}% a year", "rate", quote.AnnualRate.ToString("0.#", Inv));
            q.Big = w.F("reallife_fin_a_day", "{money} a day", "money", Money(quote.DailyPayment + quote.DailyInterest));
            Range(q, amount, least, most, w);
            Lines(q, w.T("reallife_fin_l_borrow", "You borrow"), Money(amount), Label("reallife_econ_offer_fee"), Money(quote.Fee),
                Label("reallife_econ_offer_payment"), Money(quote.DailyPayment),
                Label("reallife_econ_offer_interest"), Money(quote.DailyInterest), Label("reallife_econ_offer_days"), quote.Days.ToString(Inv));
            q.Note = record + " " + ProductAbout(BusinessLoan);
            q.Steps = steps.ToArray();
            q.StepMoves = moves.ToArray();
            q.TakeLabel = FinanceText.T("reallife_econ_offer_apply");
            q.CanTake = true;
        }

        private static void FinanceQuoteOf(FinanceQuoteFacts q, CreditDesk desk, int product, FinanceView v, UtilitiesWords w)
        {
            ProductKind kind = KindOf(product);
            Relief relief = CreditLedger.File.Relief;
            if (relief.HasApproval && relief.ApprovedKind == (int)kind)
            {
                int today = RivalGame.Day;
                int perDay = ProductTerms.AdvanceCharge(relief.ApprovedAmount, relief.ApprovedRateBp, 1, AccountRules.DaysPerYear);
                int charge = ReliefRules.CancelCharge(relief, today, AccountRules.DaysPerYear);
                q.Rate = Rate(relief.ApprovedRateBp);
                q.Big = Money(relief.ApprovedAmount);
                Lines(q, Label("reallife_econ_offer_most"), Money(relief.ApprovedAmount), w.T("reallife_fin_l_until", "Approved until day"),
                    (relief.ApprovedDay + ProductCatalogue.ApprovalDays).ToString(Inv),
                    w.T("reallife_fin_l_hold", "Holding it costs a day"), Money(perDay), w.T("reallife_fin_l_cancel", "Cancelling now costs"), Money(charge));
                q.TakeMore = new[] { FinanceText.T("reallife_econ_approval_cancel") };
                q.TakeMoreOn = new[] { true };
                q.TakeMoreActs = new[] { FinanceAct.CancelApproval };
                return;
            }
            ProductOffer offer = desk.Offer(kind);
            if (offer.Answer != ProductAnswer.Yes)
            {
                q.Refused = FinanceApp.Why(offer.Answer);
                return;
            }
            int depositLeast = ProductTerms.DepositLeast(kind), depositStep = ProductTerms.DepositStep(kind), deposits = (ProductTerms.DepositMost(kind) - depositLeast) / depositStep + 1;
            int termLeast = ProductTerms.TermLeast(kind), termStep = ProductTerms.TermStep(kind), terms = (ProductTerms.TermMost(kind) - termLeast) / termStep + 1;
            v.Deposit = Math.Max(0, Math.Min(deposits - 1, v.Deposit));
            v.Term = Math.Max(0, Math.Min(terms - 1, v.Term));
            int deposit = depositLeast + v.Deposit * depositStep, term = termLeast + v.Term * termStep;
            int least = ProductTerms.FinanceLeast(kind), most = Math.Max(least, offer.Most);
            int amount = ChosenAmount(v, least, most);
            int own = ProductTerms.DepositFor(amount, deposit), price = amount + own;
            FinanceQuote quote = ProductTerms.Quote(price, deposit, amount, term, offer.RateBp, AccountRules.DaysPerYear);
            q.Kind = offer.Bank.Name;
            q.Rate = w.F("reallife_fin_rate_year", "{rate}% a year", "rate", (offer.RateBp / 100f).ToString("0.#", Inv));
            q.Big = w.F("reallife_fin_a_day", "{money} a day", "money", Money(quote.Instalment + quote.FirstInterest));
            Range(q, amount, least, most, w);
            var moves = new FinanceStep[2];
            q.Steps = new[]
            {
                Step(w.T("reallife_fin_step_deposit", "Deposit"), deposit + "%", null, v.Deposit, deposits, out moves[0], FinanceFace.StepDeposit),
                Step(w.T("reallife_fin_step_term", "Term"), FinanceText.T("reallife_econ_row_days", "days", term.ToString(Inv)), null, v.Term, terms, out moves[1], FinanceFace.StepTerm)
            };
            q.StepMoves = moves;
            Lines(q, w.T("reallife_fin_l_price", "Price"), Money(price), w.T("reallife_fin_l_deposit", "Your deposit"), Money(own),
                w.T("reallife_fin_l_first", "First payment"), Money(quote.Instalment + quote.FirstInterest),
                w.T("reallife_fin_l_interest_term", "Interest over the term"), Money(quote.Interest));
            q.TakeLabel = FinanceText.T("reallife_econ_finance_approve");
            q.CanTake = true;
        }

        private void ConsolidateQuote(FinanceQuoteFacts q, CreditDesk desk, FinanceView v, UtilitiesWords w)
        {
            IReadOnlyList<Lender> lenders = BankTerms.Lenders;
            if (lenders.Count == 0)
            {
                q.Refused = FinanceText.T("reallife_econ_row_no_banks");
                return;
            }
            v.Pick = Math.Max(0, Math.Min(lenders.Count - 1, v.Pick));
            Lender bank = lenders[v.Pick];
            q.Steps = new UtilitiesStep[0];
            var moves = new List<FinanceStep>(1);
            var steps = new List<UtilitiesStep>(1);
            if (lenders.Count > 1)
            {
                steps.Add(Step(FinanceText.T("reallife_econ_offer_with").TrimEnd(' ', ':'), bank.Name, null, v.Pick, lenders.Count, out FinanceStep move, FinanceFace.StepPick));
                moves.Add(move);
            }
            ProductAnswer answer = desk.Consolidation(bank, out DebtTotals before, out DebtTotals after, out LoanQuote quote, out int count);
            q.Kind = bank.Name;
            q.Steps = steps.ToArray();
            q.StepMoves = moves.ToArray();
            if (before.Owed > 0)
            {
                q.Rate = w.F("reallife_fin_rate_year", "{rate}% a year", "rate", quote.AnnualRate.ToString("0.#", Inv));
                q.Big = w.F("reallife_fin_a_day", "{money} a day", "money", Money(after.Daily));
                Lines(q, w.T("reallife_fin_l_now", "Now you owe"), Money(before.Owed), w.T("reallife_fin_l_now_day", "Now a day"), Money(before.Daily),
                    w.T("reallife_fin_l_then", "Then you owe"), Money(after.Owed),
                    w.T("reallife_fin_l_then_day", "Then a day"), Money(after.Daily), Label("reallife_econ_offer_fee"), Money(quote.Fee));
            }
            if (answer != ProductAnswer.Yes)
            {
                q.Refused = FinanceApp.NotConsolidated(answer, FinanceApp.Why(answer));
                return;
            }
            q.TakeLabel = FinanceText.T("reallife_econ_consolidate_do");
            q.CanTake = true;
        }

        // ------------------------------------------------------------------------------------------------- Take it

        /// <summary>Take it: the question in the game's own box with everything the loan costs, then the director does it. The amount is the one stepped to.</summary>
        private bool Take(FinanceView v)
        {
            if (!CreditLedger.Ready || EconomyDirector.Instance == null)
            {
                return false;
            }
            CreditScore score = CreditLedger.Score;
            var rows = new List<FinanceRow>(12);
            OfferRows(rows, score, out int lenders);
            int offer = FinanceRules.Clamp(v.Offer, rows.Count);
            if (offer < 0)
            {
                return false;
            }
            if (offer < lenders)
            {
                return TakeLoan(BankTerms.Lenders[offer], score, v);
            }
            return TakeProduct(offer - lenders, v);
        }

        private bool TakeLoan(Lender lender, CreditScore score, FinanceView v)
        {
            int most = (int)Math.Floor(PhoneLending.MostNow(lender, score.Value));
            if (most < CreditCatalogue.LoanLeast)
            {
                return false;
            }
            if (!CreditLedger.CanBorrow(out CreditRefusal _))
            {
                EconomyDirector.Instance.TellRefused();
                return false;
            }
            int amount = ChosenAmount(v, CreditCatalogue.LoanLeast, most);
            LoanQuote quote = PhoneLending.Quote(lender, amount, score.Value);
            GameConfirm.Show("reallife_econ_confirm_title".Localize(new { lender = lender.Name }),
                "reallife_econ_confirm_body".Localize(new
                {
                    amount = Money(amount), lender = lender.Name, rate = quote.AnnualRate.ToString("0.#"), payment = Money(quote.DailyPayment), interest = Money(quote.DailyInterest),
                    days = quote.Days.ToString(), fee = Money(quote.Fee), cost = Money(quote.Cost)
                }),
                () => Guarded(() => Lent(lender, amount)), null, "reallife_econ_offer_apply", "common_cancel", false, false);
            return true;
        }

        // The director takes the loan and the money arrives; a refusal is said as the old page said it.
        private void Lent(Lender lender, int amount)
        {
            LoanAnswer answer = EconomyDirector.Instance.Borrow(lender, amount, out LoanQuote quote);
            switch (answer)
            {
                case LoanAnswer.Approved:
                    Notifications.Show(NotificationType.Success, "reallife_econ_note_lent", new Dictionary<string, string>
                    {
                        { "lender", lender.Name }, { "amount", Money(amount) }, { "fee", Money(quote.Fee) }, { "payment", Money(quote.DailyPayment + quote.DailyInterest) }
                    }, 10f, null, null, true, false);
                    break;
                case LoanAnswer.TooMuch:
                    Notifications.Show(NotificationType.Error, "reallife_econ_note_too_much", new Dictionary<string, string>
                    {
                        { "lender", lender.Name }, { "most", Money(PhoneLending.MostNow(lender, CreditLedger.Value)) }
                    }, 8f, null, null, true, false);
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
            Moved();
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
                EconomyDirector.Logger?.Warn("Finance (kit): an answer failed: " + e.Message);
            }
            Moved();
        }

        private bool TakeProduct(int product, FinanceView v)
        {
            CreditDesk desk = Desk;
            if (!ProductsOn || product < 0 || product >= ProductCount)
            {
                return false;
            }
            switch (product)
            {
                case Overdraft:
                case Card: return OpenLimit(desk, product);
                case BusinessLoan: return TakeBusiness(desk, v);
                case CarFinance:
                case HomeLoan: return Approve(desk, product, v);
                default: return TakeConsolidation(desk, v);
            }
        }

        private bool OpenLimit(CreditDesk desk, int product)
        {
            ProductKind kind = KindOf(product);
            ProductOffer offer = desk.Offer(kind);
            if (offer.Answer != ProductAnswer.Yes)
            {
                Said(offer.Answer);
                return false;
            }
            GameConfirm.Show("reallife_econ_confirm_open_title".Localize(new { kind = FinanceText.T(FinanceApp.ProductKeys[product]) }),
                "reallife_econ_confirm_open_body".Localize(new
                {
                    kind = FinanceText.T(FinanceApp.ProductKeys[product]), limit = Money(offer.Most), rate = (offer.RateBp / 100f).ToString("0.#"),
                        lender = offer.Bank.Name, detail = FinanceApp.Detail(kind)
                }),
                () => Guarded(() =>
                {
                    ProductAnswer answer = desk.OpenAccount(kind, out CreditAccount account);
                    if (answer == ProductAnswer.Yes)
                    {
                        Tell(NotificationType.Success, "reallife_econ_note_opened", new Dictionary<string, string> { { "account",
                            CreditDesk.Name(account) }, { "limit", Money(account.Agreed) } });
                    }
                    else
                    {
                        Said(answer);
                    }
                }), null, "reallife_econ_offer_open", "common_cancel", false, false);
            return true;
        }

        private bool TakeBusiness(CreditDesk desk, FinanceView v)
        {
            Books.Businesses(_shops);
            if (_shops.Count == 0)
            {
                return false;
            }
            BuildingRegistration shop = _shops[Math.Max(0, Math.Min(_shops.Count - 1, v.Pick))];
            ProductOffer offer = desk.Business(shop, out float _, out int _);
            if (offer.Answer != ProductAnswer.Yes)
            {
                Said(offer.Answer);
                return false;
            }
            int amount = ChosenAmount(v, CreditCatalogue.LoanLeast, offer.Most);
            LoanQuote quote = desk.BusinessQuote(offer.Bank, amount);
            string bank = offer.Bank.Name;
            GameConfirm.Show("reallife_econ_confirm_title".Localize(new { lender = bank }),
                "reallife_econ_confirm_business".Localize(new
                {
                    amount = Money(amount), lender = bank, business = shop.BusinessName, rate = quote.AnnualRate.ToString("0.#"),
                        payment = Money(quote.DailyPayment), interest = Money(quote.DailyInterest),
                    days = quote.Days.ToString(), fee = Money(quote.Fee), share = ((int)Math.Round(ProductCatalogue.BusinessDivertShare * 100f)) + "%"
                }),
                () => Guarded(() =>
                {
                    ProductAnswer answer = desk.BorrowOnBusiness(shop, amount, out LoanQuote made);
                    if (answer == ProductAnswer.Yes)
                    {
                        Tell(NotificationType.Success, "reallife_econ_note_lent", new Dictionary<string, string>
                        {
                            { "lender", bank }, { "amount", Money(amount) }, { "fee", Money(made.Fee) }, { "payment", Money(made.DailyPayment + made.DailyInterest) }
                        });
                    }
                    else
                    {
                        Said(answer);
                    }
                }), null, "reallife_econ_offer_apply", "common_cancel", false, false);
            return true;
        }

        private bool Approve(CreditDesk desk, int product, FinanceView v)
        {
            ProductKind kind = KindOf(product);
            ProductOffer offer = desk.Offer(kind);
            if (offer.Answer != ProductAnswer.Yes)
            {
                Said(offer.Answer);
                return false;
            }
            int depositLeast = ProductTerms.DepositLeast(kind), depositStep = ProductTerms.DepositStep(kind);
            int termLeast = ProductTerms.TermLeast(kind), termStep = ProductTerms.TermStep(kind);
            int deposit = depositLeast + Math.Max(0, v.Deposit) * depositStep, term = termLeast + Math.Max(0, v.Term) * termStep;
            int amount = ChosenAmount(v, ProductTerms.FinanceLeast(kind), Math.Max(ProductTerms.FinanceLeast(kind), offer.Most));
            int own = ProductTerms.DepositFor(amount, deposit);
            GameConfirm.Show("reallife_econ_confirm_finance_title".Localize(new { kind = FinanceText.T(FinanceApp.ProductKeys[product]) }),
                (kind == ProductKind.Mortgage ? "reallife_econ_confirm_home" : "reallife_econ_confirm_car").Localize(new
                {
                    amount = Money(amount), lender = offer.Bank.Name, deposit = deposit + "%", own = Money(own), days = term.ToString(), rate = (offer.RateBp / 100f).ToString("0.#"),
                    day = (RivalGame.Day + ProductCatalogue.ApprovalDays).ToString(), share = ((int)Math.Round(ProductCatalogue.RepossessionValueShare * 100f)) + "%",
                    per_day = Money(ProductTerms.AdvanceCharge(amount, offer.RateBp, 1, AccountRules.DaysPerYear))
                }),
                () => Guarded(() =>
                {
                    ProductAnswer answer = desk.Approve(kind, amount, deposit, term);
                    if (answer == ProductAnswer.Yes)
                    {
                        Tell(NotificationType.Success, "reallife_econ_note_approved", new Dictionary<string, string>
                        {
                            { "amount", Money(amount) }, { "day", (RivalGame.Day + ProductCatalogue.ApprovalDays).ToString() }
                        });
                    }
                    else
                    {
                        Said(answer);
                    }
                }), null, "reallife_econ_finance_approve", "common_cancel", false, false);
            return true;
        }

        private bool TakeConsolidation(CreditDesk desk, FinanceView v)
        {
            IReadOnlyList<Lender> lenders = BankTerms.Lenders;
            if (lenders.Count == 0)
            {
                return false;
            }
            Lender bank = lenders[Math.Max(0, Math.Min(lenders.Count - 1, v.Pick))];
            ProductAnswer answer = desk.Consolidation(bank, out DebtTotals before, out DebtTotals after, out LoanQuote quote, out int count);
            if (answer != ProductAnswer.Yes)
            {
                Said(answer);
                return false;
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
                        Tell(NotificationType.Success, "reallife_econ_note_consolidated", new Dictionary<string, string>
                        {
                            { "lender", bank.Name }, { "amount", Money(after.Owed) }, { "daily", Money(after.Daily) }
                        });
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
