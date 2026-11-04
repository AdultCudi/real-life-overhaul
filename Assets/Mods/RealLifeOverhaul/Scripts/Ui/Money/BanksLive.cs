using System;
using System.Collections.Generic;
using System.Globalization;
using RealLifeOverhaul.DaySeries;
using RealLifeOverhaul.Economy;
using RealLifeOverhaul.Rivals;
using UnityEngine;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The Banks screen's live source: reads the facts of the save (the credit file, the credit desk, the game's loans, the cash flow) when
    /// the day series, the hour or a press moves the generation, and carries each button to the desk or the director the old app used, as the
    /// old app did: the same calls, the same questions in the game's own box, the same refusals as notices. Only the tab shown is read. Nothing a frame.
    /// </summary>
    public sealed partial class BanksLive : IMoneySource, IMoneyActs
    {
        public static readonly BanksLive Instance = new BanksLive();

        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private readonly BanksFacts _facts = new BanksFacts();
        private readonly CashFlow _flow = new CashFlow();
        private readonly List<Debt> _debts = new List<Debt>(16);
        private readonly List<Debt> _work = new List<Debt>(16);
        private readonly List<string> _names = new List<string>(8);
        private readonly List<BuildingRegistration> _shops = new List<BuildingRegistration>(8);
        private readonly CreditFactor[] _tips = new CreditFactor[2];
        private Lender _bank;
        private string _cardName;
        private int _local;

        private BanksLive() { }

        public static UtilitiesWords Words() => new UtilitiesWords(UiText.Words, FinanceText.Money, () => Inv);

        private static UtilitiesWords W => Words();

        private static CreditDesk Desk => EconomyDirector.Instance != null ? EconomyDirector.Instance.Desk : null;

        public int Generation
        {
            get
            {
                GameInstance save = SaveGameManager.Current;
                return DaySeriesDirector.Generation + _local + (save != null ? save.Day * 24 + save.Hour : 0);
            }
        }

        private void Moved() => _local++;

        public void Fill(MoneyModel model)
        {
            try
            {
                Read(_facts, model.Tab, model.View);
            }
            catch (Exception e)
            {
                _facts.Ready = false;
                EconomyDirector.Logger?.Warn("Banks (kit): the page could not be read: " + e.Message);
            }
            BanksBuild.Fill(model, _facts, W);
        }

        public bool Press(MoneyModel model, int command, int arg) => false;

        // ------------------------------------------------------------------------------------------------- reading

        private static string Money(float amount) => FinanceText.Money(amount);

        private static string L(string key) => FinanceText.T(key).TrimEnd(' ', ':');

        private static string Pct(float value) => ((int)Math.Round(value)).ToString(Inv) + "%";

        private void Read(BanksFacts f, int tab, MoneyView view)
        {
            EconomyDirector director = EconomyDirector.Instance;
            GameInstance save = SaveGameManager.Current;
            CreditDesk desk = Desk;
            f.Ready = director != null && director.Running && CreditLedger.Ready && save != null && desk != null && desk.Running && BankTerms.Banks != null && BankTerms.Banks.Count > 0;
            if (!f.Ready)
            {
                f.Banks = new BankFact[0];
                return;
            }
            f.Day = save.Day;
            CreditScore score = CreditLedger.Score;
            f.Score = score.Value;
            f.Threshold = CreditCatalogue.LendingThreshold;
            f.ScoreMax = CreditCatalogue.ScoreMax;
            f.Grade = FinanceText.Grade(score.Grade);
            f.GradeTone = score.Grade >= CreditGrade.Good ? UiRole.Ok : score.Grade == CreditGrade.Fair ? UiRole.Warn : UiRole.Danger;
            f.Lends = score.Lends && BankRules.Lends(score.Value);
            int halves = (score.Value - CreditCatalogue.ScoreMin) * 10 / Math.Max(1, CreditCatalogue.ScoreMax - CreditCatalogue.ScoreMin);
            f.Halves = halves < 0 ? 0 : halves > 10 ? 10 : halves;
            f.Cash = save.Money;
            f.ProductsOn = Settings.EconomyProducts && desk.Running;
            f.Headroom = DaySeriesRules.Headroom(score.Value, CreditCatalogue.LendingThreshold, CreditCatalogue.ScoreMax);
            ProductAnswer gate = desk.Gate();
            f.Gate = gate != ProductAnswer.Yes ? Why(null, gate) : null;
            int count = Math.Min(BankRules.AppsMost, BankTerms.Banks.Count);
            f.Banks = new BankFact[count];
            for (int i = 0; i < count; i++)
            {
                f.Banks[i] = ReadBank(BankTerms.Banks[i], desk, score);
            }
            f.Bank = Math.Max(0, Math.Min(count - 1, view.Bank));
            view.Bank = f.Bank;
            _bank = BankTerms.Banks[f.Bank];
            f.IsHouse = BankTerms.House != null && _bank.Rank == BankTerms.House.Rank;
            f.HouseName = BankTerms.House != null ? BankTerms.House.Name : string.Empty;
            BankSlice slice = desk.Slice(_bank, _work);
            f.Owed = slice.Owed;
            f.Tomorrow = slice.Tomorrow;
            f.Debts = slice.Debts;
            f.Note = FinanceText.T(ProfileKey(_bank));
            switch (tab)
            {
                case BanksBuild.Loans: ReadLoans(f, desk, save.Day); break;
                case BanksBuild.Overdraft: f.Overdraft = ReadLimit(desk, ProductKind.Overdraft, save.Day); break;
                case BanksBuild.Card: f.Card = ReadLimit(desk, ProductKind.CreditCard, save.Day); ReadCardMoves(f, f.Card, save.Day); break;
                case BanksBuild.Apply: ReadApply(f, desk, score, view, save.Day); break;
                case BanksBuild.Rating: ReadRating(f, score); break;
                default: ReadAccounts(f, desk, view, save.Day); break;
            }
        }

        private static readonly string[] ProfileKeys = { "reallife_bank_profile_0", "reallife_bank_profile_1", "reallife_bank_profile_2" };

        private static string ProfileKey(Lender bank) => ProfileKeys[Math.Max(0, Math.Min(2, bank.Rank))];

        private BankFact ReadBank(Lender lender, CreditDesk desk, CreditScore score)
        {
            float most = PhoneLending.MostNow(lender, score.Value);
            int since = 0;
            desk.Held(lender, _debts);
            for (int i = 0; i < _debts.Count; i++)
            {
                int day = _debts[i].Account != null ? _debts[i].Account.OpenedDay : _debts[i].Mark.FirstDay;
                since = day > 0 && (since == 0 || day < since) ? day : since;
            }
            BankSlice slice = desk.Slice(lender, _work);
            bool lends = most >= CreditCatalogue.LoanLeast;
            return new BankFact
            {
                Name = lender.Name, Rank = lender.Rank, Rate = PhoneLending.RateOffered(lender, score.Value), Most = (int)Math.Floor(most), Lends = lends, Since = since, Owed = slice.Owed,
                    Debts = slice.Debts,
                Answer = lends && score.Lends && BankRules.Lends(score.Value)
            };
        }

        // The refusal in the bank's own words for the rating, else the game's reason.
        private string Why(Lender bank, ProductAnswer answer)
        {
            if (answer == ProductAnswer.BelowThreshold)
            {
                return FinanceText.T("reallife_bank_why_threshold", "bank", bank != null ? bank.Name : FinanceText.T("reallife_econ_lender_bank"), "threshold",
                    CreditCatalogue.LendingThreshold.ToString(Inv),
                    "score", CreditLedger.Value.ToString(Inv));
            }
            return FinanceApp.Why(answer);
        }

        // ---- Accounts

        private void ReadAccounts(BanksFacts f, CreditDesk desk, MoneyView view, int today)
        {
            int n = CashFlow.PeriodDays.Length;
            f.Periods = new string[n];
            for (int i = 0; i < n; i++)
            {
                f.Periods[i] = CashFlow.PeriodName(i);
            }
            int period = Math.Max(0, Math.Min(n - 1, view.F[MoneyView.FPeriod]));
            f.Period = period;
            Names(desk);
            _flow.Read(period, today, f.IsHouse ? null : (Func<Dictionary<string, string>, bool>)Mine);
            float came = 0f, went = 0f;
            for (int i = _flow.Entries.Count - 1; i >= 0; i--)
            {
                float amount = _flow.Entries[i].Amount;
                if (amount > 0f)
                {
                    came += amount;
                }
                else
                {
                    went -= amount;
                }
            }
            // Newest last in the statement, with the balance after each line: walked back from today's cash for the bank that holds it.
            var list = new List<BankMove>(_flow.Entries.Count);
            float running = f.IsHouse ? f.Cash : came - went;
            for (int i = _flow.Entries.Count - 1; i >= 0 && list.Count < 60; i--)
            {
                CashEntry e = _flow.Entries[i];
                list.Add(new BankMove { Day = FinanceText.Day(e.Day), What = CashFlow.ItemName(e.Type, e.Data), Amount = e.Amount, Balance = running });
                running -= e.Amount;
            }
            list.Reverse();
            f.Statement = list.ToArray();
            f.StatementIn = came;
            f.StatementOut = went;
        }

        // A movement of money is this bank's if it is under the bank's name or an account it holds.
        private bool Mine(Dictionary<string, string> data)
        {
            if (data == null)
            {
                return false;
            }
            if (data.TryGetValue("businessName", out string value) && string.Equals(value, _bank.Name, StringComparison.Ordinal))
            {
                return true;
            }
            if (!data.TryGetValue("name", out value) || string.IsNullOrEmpty(value))
            {
                return false;
            }
            for (int i = 0; i < _names.Count; i++)
            {
                if (string.Equals(value, _names[i], StringComparison.Ordinal))
                {
                    return true;
                }
            }
            return false;
        }

        private void Names(CreditDesk desk)
        {
            _names.Clear();
            _names.Add(_bank.Name);
            desk.Held(_bank, _debts);
            for (int i = 0; i < _debts.Count; i++)
            {
                if (_debts[i].Account != null)
                {
                    _names.Add(CreditDesk.Name(_debts[i].Account));
                }
            }
        }

        // ---- Loans

        private void ReadLoans(BanksFacts f, CreditDesk desk, int today)
        {
            desk.Held(_bank, _debts);
            var list = new List<BankDebt>(_debts.Count);
            for (int i = 0; i < _debts.Count; i++)
            {
                Debt d = _debts[i];
                if (d.Account != null && ProductCatalogue.Revolving(d.Account.Kind))
                {
                    continue;   // an overdraft and a card have their own tabs
                }
                list.Add(d.Account != null ? DebtOf(d.Account, today) : GameLoanOf(d.Mark, today));
            }
            f.Debt = list.ToArray();
        }

        private static int KindOf(ProductKind kind)
        {
            switch (kind)
            {
                case ProductKind.Overdraft: return 2;
                case ProductKind.CreditCard: return 3;
                case ProductKind.CarFinance: return 4;
                case ProductKind.Mortgage: return 5;
                case ProductKind.Arrears: return 6;
                default: return 7;
            }
        }

        private static BankDebt DebtOf(CreditAccount a, int today)
        {
            LadderState s = a.Ladder;
            FinanceApp.NextPayment(a, today, out int next, out int day);
            int due = AccountRules.DueTomorrow(a, today);
            var d = new BankDebt
            {
                Id = "acct:" + a.Id.ToString(Inv), Number = a.Id, Kind = KindOf(a.Kind), Name = CreditDesk.Name(a), Owed = a.Owed, Total = a.Agreed,
                    Rate = a.RateBp > 0 ? FinanceApp.Rate(a.RateNow) : null, RateRed = s.Penalty,
                NextAmount = next > 0 && day > 0 ? next : 0, NextDay = day, NextWord = next > 0 && day > 0 ? FinanceApp.DayWord(day, today) : null, State = FinanceText.State(a),
                    Sound = s.InOrder && !s.Penalty,
                Frozen = a.Frozen, CallDue = s.CallDue, PlanUsed = s.PlanUsed, PlanLeft = s.PlanLeft, PlanPerDay = s.PlanPerDay, Overdue = s.Overdue, About = FinanceApp.AboutKind(a.Kind),
                    Opened = a.OpenedDay,
                PayMost = a.Owed, PaySuggested = BankParts.PaySuggest(KindOf(a.Kind), a.Owed, a.Frozen, a.MinimumLeft, a.StatementLeft, Math.Min(a.Owed, Math.Max(s.Overdue, due))),
                    Minimum = a.MinimumLeft, StatementLeft = a.StatementLeft, DueDay = a.DueDay,
                    NextStatement = a.NextDay,
                Free = a.Free, DrawMost = a.Kind == ProductKind.CreditCard ? AccountRules.MostDraw(a) : 0
            };
            if (ProductCatalogue.Instalment(a.Kind) && a.Instalment > 0 && !a.Frozen)
            {
                d.Payment = due;
                d.Left = (int)Math.Ceiling(a.Owed / (float)a.Instalment);
                d.EndDay = today + d.Left;
            }
            if (a.Kind != ProductKind.CreditCard && a.Kind != ProductKind.Overdraft && a.Kind != ProductKind.Arrears)
            {
                d.Thing = FinanceText.T(a.Secured ? "reallife_econ_account_secured" : "reallife_econ_account_unsecured", "thing", CreditDesk.Thing(a));
            }
            if (a.Kind == ProductKind.Overdraft)
            {
                d.Thing = FinanceText.T("reallife_econ_account_overdraft", "free", Money(a.Free), "float", Money(ProductCatalogue.OverdraftFloat));
            }
            return d;
        }

        private static BankDebt GameLoanOf(LoanMark m, int today)
        {
            var loan = (Loan)m.Ref;
            bool frozen = (m.Held & LoanHold.Frozen) != 0;
            float yearly = FinanceApp.YearlyRate(loan, Math.Max(1, AccountRules.DaysPerYear));
            int whole = WholeMoney.Round(Math.Ceiling((double)loan.remainingAmount));
            int kind = FinanceTables.OfLoan(m.Purpose);
            string name = FinanceText.T(FinanceTables.DebtKinds[kind]);
            var d = new BankDebt
            {
                Id = FinanceApp.LoanRowId(m), GameLoan = true, Kind = m.Purpose == LoanPurpose.Business ? 1 : 0, Name = name, Owed = loan.remainingAmount, Total = loan.totalAmount,
                Rate = yearly > 0f ? FinanceText.T("reallife_econ_row_rate", "rate", yearly.ToString("0.#", Inv)) : null, RateRed = (m.Held & LoanHold.Penalty) != 0,
                State = FinanceText.State(m.Ladder, frozen, false), Sound = m.Ladder.InOrder && !m.Ladder.Penalty, Frozen = frozen, CallDue = m.Ladder.CallDue, PlanUsed = m.Ladder.PlanUsed,
                PlanLeft = m.Ladder.PlanLeft, PlanPerDay = m.Ladder.PlanPerDay, Overdue = m.Ladder.Overdue, Interest = loan.dailyInterest, Opened = m.FirstDay,
                About = FinanceText.T("reallife_tab_loan_about", "kind", name, "day", m.FirstDay.ToString(Inv)), PayMost = whole, PaySuggested = frozen ? whole : Math.Min(whole, loan.dailyPayment),
                Business = m.Purpose == LoanPurpose.Business
            };
            if (!frozen)
            {
                d.NextAmount = Mathf.RoundToInt(GameLoans.Due(loan));
                d.NextDay = today + 1;
                d.NextWord = FinanceApp.DayWord(today + 1, today);
                d.Payment = loan.dailyPayment;
                if (loan.dailyPayment > 0)
                {
                    d.Left = Mathf.CeilToInt(loan.remainingAmount / loan.dailyPayment);
                    d.EndDay = today + d.Left;
                }
            }
            if (m.Purpose == LoanPurpose.Business)
            {
                BuildingRegistration shop = Books.Business(m.Security);
                d.Thing = shop != null ? FinanceText.T("reallife_econ_account_secured", "thing", shop.BusinessName) : FinanceText.T("reallife_econ_loan_unsecured");
            }
            return d;
        }

        // ---- Overdraft and Card

        private CreditAccount HeldHere(ProductKind kind)
        {
            CreditAccount a = CreditLedger.File != null ? CreditLedger.File.Account(kind) : null;
            return a != null && BankTerms.IsBank(_bank, a.Street, a.Number) ? a : null;
        }

        private static string HeldWith(ProductKind kind)
        {
            CreditAccount a = CreditLedger.File != null ? CreditLedger.File.Account(kind) : null;
            return a != null ? CreditDesk.LenderName(a) : null;
        }

        private BankLimit ReadLimit(CreditDesk desk, ProductKind kind, int today)
        {
            var lim = new BankLimit();
            CreditAccount a = HeldHere(kind);
            if (a != null)
            {
                lim.Held = true;
                lim.Account = DebtOf(a, today);
                return lim;
            }
            lim.Elsewhere = HeldWith(kind);
            if (lim.Elsewhere != null)
            {
                return lim;
            }
            ProductOffer offer = desk.Offer(kind, _bank);
            if (offer.Answer != ProductAnswer.Yes)
            {
                lim.Refusal = Why(_bank, offer.Answer);
                return lim;
            }
            lim.OfferLimit = offer.Most;
            lim.OfferRate = offer.RateBp;
            lim.Free = FinanceApp.AboutKind(kind);
            return lim;
        }

        // The card's own movements over a month, newest first.
        private void ReadCardMoves(BanksFacts f, BankLimit card, int today)
        {
            if (!card.Held || card.Account == null)
            {
                f.CardMoves = new BankMove[0];
                return;
            }
            CreditAccount a = HeldHere(ProductKind.CreditCard);
            _cardName = a != null ? CreditDesk.Name(a) : null;
            _flow.Read(CashFlow.Month, today, data => data != null && data.TryGetValue("name", out string value) && string.Equals(value, _cardName, StringComparison.Ordinal));
            var list = new List<BankMove>(_flow.Entries.Count);
            for (int i = _flow.Entries.Count - 1; i >= 0 && list.Count < 40; i--)
            {
                CashEntry e = _flow.Entries[i];
                list.Add(new BankMove { Day = FinanceText.Day(e.Day), What = CashFlow.ItemName(e.Type, e.Data), Amount = e.Amount });
            }
            f.CardMoves = list.ToArray();
        }

        // ---- Rating

        private void ReadRating(BanksFacts f, CreditScore score)
        {
            int n = (int)CreditFactor.Count;
            f.Factors = new BankFactor[n];
            for (int i = 0; i < n; i++)
            {
                f.Factors[i] = new BankFactor
                {
                    Name = FinanceText.T(CreditCatalogue.FactorKeys[i]), Part = (int)Math.Round(score.Factor[i] * 100f), Stands = FinanceText.Stands(score, (CreditFactor)i)
                };
            }
            int tips = CreditScoring.Tips(score, _tips);
            f.Tips = new string[tips];
            for (int i = 0; i < tips; i++)
            {
                f.Tips[i] = FinanceText.Tip(score, _tips[i]);
            }
            int threshold = CreditCatalogue.LendingThreshold, best = CreditCatalogue.BestTermsFrom, third = (best - threshold) / 3;
            int[] at = { threshold - 1, threshold, threshold + third / 10 * 10, threshold + 2 * third / 10 * 10, best, score.Value };
            f.Bands = new BankBand[at.Length];
            for (int i = 0; i < at.Length; i++)
            {
                f.Bands[i] = BandOf(at[i], i == at.Length - 1);
            }
            int next = Math.Min(CreditCatalogue.ScoreMax, (score.Value / 50 + 1) * 50);
            f.Next = null;
            if (next > score.Value && score.Value < best)
            {
                LenderTerms then = LendingTerms.For(next, BankTerms.OwnCap(_bank.Settings), BankTerms.OwnRate(_bank.Settings));
                f.Next = FinanceText.Fill("reallife_bank_rating_next", "score", next.ToString(Inv), "bank", _bank.Name, "loan", Money(then.Cap), "rate",
                    (then.AnnualRate * GameLoans.Multiplier).ToString("0.#", Inv),
                    "card", Money(CreditDesk.LimitAt(ProductKind.CreditCard, next, _bank)));
            }
        }

        // One band: the bank's loan cap and rate at that rating before the borrower's means count, and its card, overdraft and car finance.
        private BankBand BandOf(int at, bool yours)
        {
            var band = new BankBand { Score = at.ToString(Inv), Kind = FinanceText.T(yours ? "reallife_bank_kind_yours" : "reallife_bank_kind_band"), Yours = yours };
            if (!BankRules.Lends(at))
            {
                band.Refused = true;
                return band;
            }
            LenderTerms terms = LendingTerms.For(at, BankTerms.OwnCap(_bank.Settings), BankTerms.OwnRate(_bank.Settings));
            band.Loan = Money(terms.Cap);
            band.Rate = FinanceText.T("reallife_econ_row_rate", "rate", (terms.AnnualRate * GameLoans.Multiplier).ToString("0.#", Inv));
            if (Settings.EconomyProducts)
            {
                band.Card = Money(CreditDesk.LimitAt(ProductKind.CreditCard, at, _bank));
                band.Overdraft = Money(CreditDesk.LimitAt(ProductKind.Overdraft, at, _bank));
                band.Car = Money(CreditDesk.LimitAt(ProductKind.CarFinance, at, _bank));
            }
            return band;
        }
    }
}
