using System;
using System.Collections.Generic;
using System.Globalization;
using Entities;
using Enums;
using Helpers;
using Localizor;
using Localizor.LanguageChangeEvent;
using TMPro;
using UI.Smartphone.Apps.EconoView;
using UI.Smartphone.Apps.EconoView.Investments;
using UnityEngine;
using UnityEngine.UI;
using GameDropdown = UI.Elements.Dropdown;
using GameBar = UI.Elements.ProgressBar;
using GameField = UI.Components.InputField;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// EconoView's screen, its reading half: the game's app and save put into an <see cref="EconoState"/> as the game
    /// shows them, after the game and other mods filled them (Going Public's partners taken out of the businesses, its
    /// rows merged into the tables, its words on the taxes); the game's own pieces a press reaches; and the signature of
    /// what would change the screen. A transaction's time and balance come from the game's own record of it, found by its
    /// day, kind and money, so a line another player's books added (no record of the player's) shows without them.
    /// Cost: the signature's few counts and sums four times a second; the reading only for a layout.
    /// </summary>
    public sealed partial class EconoViewScreen
    {
        private const int Days = 4;                 // the days the game's income statement shows, yesterday first
        private const int TrendDays = 14;           // the cash's line: two weeks of days' closing balances
        private const string TabLoans = "loans", TabFunds = "investments", TabTaxes = "taxes";   // the game's banking tabs (EconoViewBanking.SetTab)
        private const string LabelSuffix = "_label";   // a transaction kind's name in the game's filter is its key and this
        private const int MostRecords = 12;         // the game's records of a loan, a fund or the taxes under their controls
        private const string TaxPaid = "ba:transaction_taxpayment";                                    // the game's own, as its tax page finds the last payment
        private const string Invested = "ba:transaction_investment", PaidOut = "ba:transaction_investmentpayout";   // the game's own, as a fund books them
        private const string BankName = "businessName", FundName = "investmentFundName";              // the data the game books a loan's and a fund's lines with

        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        private readonly List<EconoViewIncomeStatementModel> _income = new List<EconoViewIncomeStatementModel>(32);
        private readonly List<Transform> _businessRows = new List<Transform>(32);
        private readonly List<LoanEntryUi> _loans = new List<LoanEntryUi>(8);
        private readonly List<string> _options = new List<string>(32);
        private readonly Dictionary<TxKey, int> _txFirst = new Dictionary<TxKey, int>(256), _txLooseFirst = new Dictionary<TxKey, int>(256);
        private Transaction[] _txList = new Transaction[0];
        private int[] _txNext = new int[0], _txLooseNext = new int[0];
        private bool[] _txUsed = new bool[0];
        private int _txCount = -1;
        private float _txMoney;
        private object _latestOf, _allOf;           // the game's tables last read, so an unchanged one is not read again
        private int _latestCount = -1, _allCount = -1;
        private EconoTx[] _latest = new EconoTx[0];
        private EconoTx[] _all = new EconoTx[0];
        private float[] _trend;
        private int _trendDay = -1, _trendCount = -1;
        private bool _taxesSeen;

        // The game's own record of a transaction, as its tables' rows can be told from one another.
        private struct TxKey : IEquatable<TxKey>
        {
            public int Day;
            public string Type;
            public float Amount, Balance;
            public bool Loose;   // a row without a balance: the balance is not compared

            public bool Equals(TxKey o) => Day == o.Day && Amount == o.Amount && Loose == o.Loose && (Loose || Balance == o.Balance) && string.Equals(Type, o.Type, StringComparison.Ordinal);

            public override bool Equals(object obj) => obj is TxKey k && Equals(k);

            public override int GetHashCode() => ((Day * 397) ^ Amount.GetHashCode() ^ (Loose ? 0 : Balance.GetHashCode() * 31)) ^ (Type != null ? Type.GetHashCode() : 0);
        }

        // ------------------------------------------------------------------------------------------------- the page and the game's tabs

        // The page in view: one business and every transaction are the game's own state; the rest the kit's choice.
        private int Page()
        {
            if (_app.selectedBusiness != null)
            {
                return UiEcono.Business;
            }
            return _app.overview.fullTransactionsPanel.isActiveAndEnabled ? UiEcono.Transactions : _page;
        }

        // The app opened again: the overview, nothing open, nothing typed; the taxes when the game opened on them (the IRS
        // message's Open taxes: its banking tab never starts on taxes by itself).
        private void Opened()
        {
            _openList = -1;
            _typing = -1;
            _chartFocus = -1;
            _folded.Clear();
            EconoViewBanking banking = Banking();
            _taxesSeen = banking != null && banking.taxesContainer != null && banking.taxesContainer.gameObject.activeSelf;
            _page = _taxesSeen ? UiEcono.Taxes : UiEcono.Overview;
            _period = 0;
            _businessPeriod = 0;
            _dirty = true;
        }

        // Keeps the game's banking tab on the kit's (the game sets its own as its overview opens), and follows the game onto
        // its taxes when something of the game's shows them.
        private void Follow()
        {
            EconoViewBanking banking = Banking();
            if (banking == null || !banking.isActiveAndEnabled || _app.selectedBusiness != null)
            {
                return;
            }
            bool taxes = banking.taxesContainer != null && banking.taxesContainer.gameObject.activeSelf;
            if (taxes && !_taxesSeen && _page != UiEcono.Taxes)
            {
                _page = UiEcono.Taxes;
                _dirty = true;
            }
            _taxesSeen = taxes;
            Banking(_page);
        }

        // The game's banking tab for a page, set through its own SetTab only when it shows another.
        private void Banking(int page)
        {
            EconoViewBanking banking = Banking();
            if (banking == null || !banking.isActiveAndEnabled)
            {
                return;
            }
            string tab = page == UiEcono.Loans ? TabLoans : page == UiEcono.Investments ? TabFunds : page == UiEcono.Taxes ? TabTaxes : null;
            if (tab == null)
            {
                return;
            }
            Component shown = tab == TabLoans ? (Component)banking.loansContainer : tab == TabFunds ? banking.investmentsContainer : banking.taxesContainer;
            if (shown != null && !shown.gameObject.activeSelf)
            {
                banking.SetTab(tab);
                _taxesSeen = tab == TabTaxes;
            }
        }

        private EconoViewBanking Banking() => Get<EconoViewBanking>(GameContract.EconoBanking, _app);

        private EconoViewInvestments Investments()
        {
            EconoViewBanking banking = Banking();
            return banking != null ? banking.investmentsContainer : null;
        }

        private List<BuildingRegistration> Businesses() => GameContract.EconoBusinesses.Field != null ? GameContract.EconoBusinesses.Field.GetValue(_app) as List<BuildingRegistration> : null;

        private static T Get<T>(GameMember member, object owner) where T : class => owner != null && member.Field != null ? member.Field.GetValue(owner) as T : null;

        // ------------------------------------------------------------------------------------------------- what would change the screen

        private long Signature()
        {
            GameInstance save = SaveGameManager.Current;
            if (save == null)
            {
                return 0;
            }
            int page = Page();
            long sig = page * 7 + (_openList + 2) * 131 + _period * 17 + _businessPeriod * 19 + _loan * 23;
            sig = sig * 31 + save.Day;
            if (page == UiEcono.Overview)
            {
                sig = sig * 31 + (long)Math.Round(save.Money);   // only the overview shows the cash as it moves; the other pages show the game's lists, which keep still
            }
            List<BuildingRegistration> businesses = Businesses();
            sig = sig * 31 + (businesses != null ? businesses.Count : 0);
            sig = sig * 31 + (_app.selectedBusiness != null ? _app.selectedBusiness.GetHashCode() : 0);
            sig = sig * 31 + (_app.overview.fullTransactionsPanel.isActiveAndEnabled ? 1 : 0);
            var rows = Get<List<EconoViewIncomeStatementModel>>(GameContract.EconoRows, Get<EconoViewIncomeStatementScrollerController>(GameContract.EconoStatement, _app.overview));
            for (int i = 0; rows != null && i < rows.Count; i++)
            {
                sig = sig * 31 + (rows[i] != null && rows[i].isExpanded ? 2 : 1);
            }
            var latest = Get<EconoViewLastTransactionsScrollerController>(GameContract.EconoLatest, _app.overview);
            sig = sig * 31 + (latest != null && latest.data != null ? latest.data.Count : 0);
            var all = Get<EconoViewFullTransactionsScrollerController>(GameContract.EconoAll, _app.overview.fullTransactionsPanel);
            sig = sig * 31 + (all != null && all.data != null ? all.data.GetHashCode() + all.data.Count : 0);   // the game makes a new list each time it filters
            sig = sig * 31 + Chosen(GameContract.EconoTypes) * 3 + Chosen(GameContract.EconoAmounts) * 5 + Chosen(GameContract.EconoDayFilter) * 7;
            for (int i = 0; save.Loans != null && i < save.Loans.Count; i++)
            {
                sig = sig * 31 + (long)save.Loans[i].remainingAmount + save.Loans[i].dailyPayment;
            }
            for (int i = 0; save.investmentFunds != null && i < save.investmentFunds.Count; i++)
            {
                InvestmentFund f = save.investmentFunds[i];
                sig = sig * 31 + (long)f.CurrentValue + (long)f.autoInvestment * 3 + (f.isAutoInvesting ? 1 : 0);
            }
            EconoViewInvestments funds = Investments();
            sig = sig * 31 + (funds != null && Get<InvestmentFund>(GameContract.EconoFund, funds) != null ? Get<InvestmentFund>(GameContract.EconoFund, funds).GetHashCode() : 0);
            sig = sig * 31 + Words(GameContract.EconoOwed) + Words(GameContract.EconoProjection) * 3 + Words(GameContract.EconoBackAmount) * 5;
            EconoViewBanking banking = Banking();
            sig = sig * 31 + (banking != null && banking.loansContainer != null && banking.loansContainer.gameObject.activeSelf ? 1 : 0)
                  + (banking != null && banking.investmentsContainer != null && banking.investmentsContainer.gameObject.activeSelf ? 2 : 0);
            Transform content = _app.businessDetails.incomeStatement.defaultEntry != null ? _app.businessDetails.incomeStatement.defaultEntry.parent : null;
            for (int i = 0; content != null && i < content.childCount; i++)
            {
                sig = sig * 31 + (content.GetChild(i).gameObject.activeSelf ? 2 : 1);
            }
            return sig;
        }

        private int Chosen(GameMember dropdown)
        {
            var d = Get<GameDropdown>(dropdown, _app.overview.fullTransactionsPanel);
            return d != null ? d.SelectedOptionIndex + 1 : 0;
        }

        private int Words(GameMember label)
        {
            var t = Get<TMP_Text>(label, Taxes());
            return t != null && t.text != null ? t.text.GetHashCode() : 0;
        }

        private EconoViewTaxes Taxes()
        {
            EconoViewBanking banking = Banking();
            return banking != null ? banking.taxesContainer : null;
        }

        // ------------------------------------------------------------------------------------------------- reading the game

        private EconoState Read()
        {
            GameInstance save = SaveGameManager.Current;
            var v = new EconoState { Page = Page(), Money = UiText.Money, Cash = save != null ? save.Money : 0f, OpenList = _openList, Loan = _loan, Period = _period,
                BusinessPeriod = _businessPeriod };
            ReadBusinesses(v);
            v.LoanCount = save != null && save.Loans != null ? save.Loans.Count : 0;
            v.FundCount = save != null && save.investmentFunds != null ? save.investmentFunds.Count : 0;
            switch (v.Page)
            {
                case UiEcono.Loans: ReadLoans(v); break;
                case UiEcono.Investments: ReadFunds(v); break;
                case UiEcono.Transactions: ReadAll(v); break;
                case UiEcono.Taxes: ReadTaxes(v); break;
                case UiEcono.Business: ReadBusiness(v); break;
                case UiEcono.Overview: ReadOverview(v, save); break;
            }
            return v;
        }

        // The businesses as the game's choice lists them (Going Public's partners taken out), and the one shown.
        private void ReadBusinesses(EconoState v)
        {
            int chosen = Options(_app.businessNameDropdown, out bool words);
            v.Businesses = Words(words);
            List<BuildingRegistration> all = Businesses();
            v.BusinessChosen = all != null && _app.selectedBusiness != null ? all.IndexOf(_app.selectedBusiness) : chosen;
            if (v.BusinessChosen >= 0)
            {
                _business = v.BusinessChosen;
            }
        }

        // The game's drop-down's options as it shows them, and the chosen one's index.
        private int Options(GameDropdown d, out bool localise)
        {
            _options.Clear();
            localise = false;
            var raw = Get<List<string>>(GameContract.EconoDropdownOptions, d);
            if (raw != null)
            {
                _options.AddRange(raw);
            }
            object flag = d != null && GameContract.EconoDropdownWords.Field != null ? GameContract.EconoDropdownWords.Field.GetValue(d) : null;
            localise = flag is bool b && b;
            return d != null ? d.SelectedOptionIndex : -1;
        }

        private string[] Words(bool localise)
        {
            var words = new string[_options.Count];
            for (int i = 0; i < words.Length; i++)
            {
                words[i] = localise ? Localised(_options[i]) : _options[i];
            }
            return words;
        }

        // The overview: the income statement's rows, the days its heads name, the cash and its line, the last transactions.
        private void ReadOverview(EconoState v, GameInstance save)
        {
            var scroller = Get<EconoViewIncomeStatementScrollerController>(GameContract.EconoStatement, _app.overview);
            var rows = Get<List<EconoViewIncomeStatementModel>>(GameContract.EconoRows, scroller);
            _income.Clear();
            if (rows != null)
            {
                _income.AddRange(rows);
            }
            v.Income = new EconoIncomeRow[_income.Count];
            for (int i = 0; i < _income.Count; i++)
            {
                EconoViewIncomeStatementModel r = _income[i];
                if (r == null)
                {
                    continue;
                }
                var values = new float[Days];
                for (int d = 0; r.values != null && d < Days && d < r.values.Count; d++)
                {
                    values[d] = r.values[d];
                }
                v.Income[i] = new EconoIncomeRow
                {
                    Key = r.rowName, Name = Localised(r.rowName), Parent = r.parent != null ? _income.IndexOf(r.parent) : -1, Total = r.rowType == EconoViewRowType.Total,
                    Folded = !r.isExpanded, Values = values, Kind = r.rowType == EconoViewRowType.Success ? 1 : r.rowType == EconoViewRowType.Danger ? -1 : 0
                };
            }
            DayNames(Get<TextLocalizationComponent[]>(GameContract.EconoDays, _app.overview), save, out v.DayNames, out v.DayNumbers);
            v.CashTrend = Trend(save);
            var latest = Get<EconoViewLastTransactionsScrollerController>(GameContract.EconoLatest, _app.overview);
            List<EconoViewLastTransactionModel> data = latest != null ? latest.data : null;
            if (data != _latestOf || (data != null ? data.Count : -1) != _latestCount)
            {
                _latestOf = data;
                _latestCount = data != null ? data.Count : -1;
                int n = data != null ? Math.Min(EconoViews.MostLatest, data.Count) : 0;
                IndexTransactions(save);
                _latest = new EconoTx[n];
                for (int i = 0; i < n; i++)
                {
                    EconoViewLastTransactionModel x = data[i];
                    Transaction own = Match(x.day, x.labelData.Key, x.amount, float.NaN);
                    _latest[i] = new EconoTx { Day = x.day, Type = x.labelData.Key, What = Plain(x.labelData.ToString()), Amount = x.amount,
                        Balance = own != null ? UiText.Money(own.balance) : null };
                }
            }
            v.Latest = _latest;
        }

        // The days the statement's heads name, yesterday first, as many as the game has days for.
        private static void DayNames(TextLocalizationComponent[] heads, GameInstance save, out string[] names, out int[] numbers)
        {
            int have = save != null ? Math.Max(0, Math.Min(Days, save.Day - 1)) : 0;
            names = new string[have];
            numbers = new int[have];
            for (int i = 0; i < have; i++)
            {
                TMP_Text t = heads != null && i < heads.Length && heads[i] != null ? heads[i].GetComponent<TMP_Text>() : null;
                numbers[i] = save.Day - 1 - i;
                names[i] = t != null && !string.IsNullOrEmpty(t.text) ? Plain(t.text) : LocalizorManager.GetLocalization("common_day_number", (object)new { number = numbers[i] });
            }
        }

        // The cash at each of the last two weeks' closes, by the balance the game wrote after each transaction, today's now.
        private float[] Trend(GameInstance save)
        {
            int count = save != null && save.Transactions != null ? save.Transactions.Count : 0;
            if (save == null)
            {
                return null;
            }
            if (_trend != null && _trendDay == save.Day && _trendCount == count)
            {
                _trend[_trend.Length - 1] = save.Money;
                return _trend;
            }
            int first = save.Day - TrendDays + 1;
            var points = new float[TrendDays];
            var seen = new bool[TrendDays];
            float before = float.NaN;
            if (save.Transactions != null)
            {
                foreach (Transaction t in save.Transactions)
                {
                    if (t == null || t.timestamp == null)
                    {
                        continue;
                    }
                    int d = t.timestamp.Day - first;
                    if (d < 0)
                    {
                        before = t.balance;
                    }
                    else if (d < TrendDays)
                    {
                        points[d] = t.balance;
                        seen[d] = true;
                        before = float.IsNaN(before) ? t.balance - t.amount : before;
                    }
                }
            }
            float carry = float.IsNaN(before) ? save.Money : before;
            for (int d = 0; d < TrendDays; d++)
            {
                carry = seen[d] ? points[d] : carry;
                points[d] = carry;
            }
            points[TrendDays - 1] = save.Money;
            _trend = points;
            _trendDay = save.Day;
            _trendCount = count;
            return points;
        }

        // Every transaction as the game's full table lists it, with its filters' options.
        private void ReadAll(EconoState v)
        {
            GameInstance save = SaveGameManager.Current;
            EconoViewFullTransactions panel = _app.overview.fullTransactionsPanel;
            v.TypeChosen = Math.Max(0, Options(Get<GameDropdown>(GameContract.EconoTypes, panel), out bool words));
            v.TypeOptions = Words(words);
            v.AmountChosen = Math.Max(0, Options(Get<GameDropdown>(GameContract.EconoAmounts, panel), out words));
            v.AmountOptions = Words(words);
            v.DayChosen = Math.Max(0, Options(Get<GameDropdown>(GameContract.EconoDayFilter, panel), out words));
            v.DayOptions = Words(words);
            var table = Get<EconoViewFullTransactionsScrollerController>(GameContract.EconoAll, panel);
            List<TransactionModel> data = table != null ? table.data : null;
            if (data != _allOf || (data != null ? data.Count : -1) != _allCount)
            {
                _allOf = data;
                _allCount = data != null ? data.Count : -1;
                int n = data != null ? data.Count : 0;
                IndexTransactions(save);
                _all = new EconoTx[n];
                for (int i = 0; i < n; i++)
                {
                    TransactionModel x = data[i];
                    if (x == null)
                    {
                        continue;
                    }
                    Transaction own = Match(x.Day, x.Type, x.Amount, x.Balance);
                    _all[i] = new EconoTx { Day = x.Day, Type = x.Type, What = Plain(x.Label), Amount = x.Amount, Balance = UiText.Money(x.Balance), Time = own != null ? Clock(own) : null };
                }
            }
            v.All = _all;
            var folded = new int[_folded.Count];
            _folded.CopyTo(folded);
            v.FoldedDays = folded;
        }

        // The loans as the game's rows for them hold them, in their order.
        private void ReadLoans(EconoState v)
        {
            _loans.Clear();
            EconoViewBanking banking = Banking();
            var template = banking != null && banking.loansContainer != null ? Get<Transform>(GameContract.EconoLoanTemplate, banking.loansContainer) : null;
            Transform list = template != null ? template.parent : null;
            for (int i = 0; list != null && i < list.childCount; i++)
            {
                Transform row = list.GetChild(i);
                LoanEntryUi entry = row != template && row.gameObject.activeSelf ? row.GetComponent<LoanEntryUi>() : null;
                if (entry != null && Get<Loan>(GameContract.EconoLoan, entry) != null)
                {
                    _loans.Add(entry);
                }
            }
            v.Loans = new EconoLoanView[_loans.Count];
            for (int i = 0; i < _loans.Count; i++)
            {
                LoanEntryUi e = _loans[i];
                Loan loan = Get<Loan>(GameContract.EconoLoan, e);
                GameField daily = Get<GameField>(GameContract.EconoLoanDaily, e), payOff = Get<GameField>(GameContract.EconoLoanPayOff, e);
                long left = (long)Math.Floor(loan.remainingAmount);
                v.Loans[i] = new EconoLoanView
                {
                    Lender = Plain(Text(Get<TMP_Text>(GameContract.EconoLoanBank, e))), Total = loan.totalAmount, Left = loan.remainingAmount,
                    PaidTotal = Plain(Text(Get<TMP_Text>(GameContract.EconoLoanPaid, e))), Interest = Plain(Text(Get<TMP_Text>(GameContract.EconoLoanInterest, e))),
                    Daily = Shown(daily), DailyValue = Value(daily), DailyLeast = Math.Min(LoanHelper.CalculateMinimumDailyPayment(loan), left),
                    TimeLeft = Plain(Text(Label(Get<TextLocalizationComponent>(GameContract.EconoLoanTime, e)))), PayOff = Shown(payOff), PayOffValue = Value(payOff),
                    PayOffMost = left, CanPay = UiWidgets.Shows(Get<Button>(GameContract.EconoLoanPay, e))
                };
                v.Loans[i].Payments = Records(LoanLine, BankName, v.Loans[i].Lender);
            }
            _loan = Math.Max(0, Math.Min(_loan, Math.Max(0, _loans.Count - 1)));
            v.Loan = _loan;
        }

        // The funds as the game's fund buttons list them, the one its page shows, and that one's controls.
        private void ReadFunds(EconoState v)
        {
            EconoViewInvestments page = Investments();
            var buttons = Get<List<InvestmentFundButton>>(GameContract.EconoFundButtons, page);
            InvestmentFund shown = Get<InvestmentFund>(GameContract.EconoFund, page);
            int n = buttons != null ? buttons.Count : 0;
            var funds = new List<EconoFundView>(n);
            int chosen = 0;
            for (int i = 0; i < n; i++)
            {
                InvestmentFundButton b = buttons[i];
                InvestmentFund f = b != null ? InvestmentFundHelper.GetInvestmentByName(b.FundName) : null;
                if (f == null)
                {
                    continue;
                }
                chosen = f == shown ? funds.Count : chosen;
                EconoFundView view = Fund(f, f == shown ? page : null);
                view.Moves = f == shown ? Records(FundLine, FundName, view.Name) : new EconoTx[0];
                funds.Add(view);
            }
            v.Funds = funds.ToArray();
            v.Fund = chosen;
        }

        private static EconoFundView Fund(InvestmentFund f, EconoViewInvestments page)
        {
            InvestmentFundData data = InvestmentFundHelper.GetData(f.name);
            var view = new EconoFundView
            {
                Key = f.name, Name = Localised(f.name), Value = f.CurrentValue, Invested = f.initialDeposit + f.additionalInvestment, Interest = f.RoundedInterestPayment,
                Risk = data != null ? (int)InvestmentFundHelper.DetermineRisk(data).Item1 : -1, Initial = UiText.Money(f.initialDeposit), Added = UiText.Money(f.additionalInvestment),
                Withdrawn = UiText.Money(f.withdrawal), Earned = UiText.Money(f.RoundedInterestPayment), AutoOn = f.isAutoInvesting, Auto = UiText.Money(f.autoInvestment),
                AutoValue = (long)Math.Floor(f.autoInvestment), WithdrawMost = (long)Math.Floor(f.CurrentValue)
            };
            if (page != null)
            {
                GameField withdraw = Get<GameField>(GameContract.EconoWithdraw, page), auto = Get<GameField>(GameContract.EconoAutoField, page);
                view.Withdraw = Shown(withdraw);
                view.WithdrawValue = Value(withdraw);
                view.Auto = auto != null ? Shown(auto) : view.Auto;
                view.AutoValue = auto != null ? Value(auto) : view.AutoValue;
            }
            int last = f.developmentHistory != null ? f.developmentHistory.Count : 0;
            int year = SaveGameManager.Current != null ? Math.Max(2, SaveGameManager.Current.gameVariables.daysPerYear) : 60;
            int from = Math.Max(0, last - year);
            view.History = new float[last - from];
            view.HistoryDays = new int[last - from];
            for (int i = from; i < last; i++)
            {
                view.History[i - from] = f.developmentHistory[i].newBalance;
                view.HistoryDays[i - from] = f.developmentHistory[i].day;
            }
            return view;
        }

        // The taxes as the game's Init (and Going Public's postfix on it) wrote them.
        private void ReadTaxes(EconoState v)
        {
            EconoViewTaxes page = Taxes();
            if (page == null)
            {
                return;
            }
            var back = Get<GameObject>(GameContract.EconoBackArea, page);
            TMP_Text owed = Get<TMP_Text>(GameContract.EconoOwed, page), projection = Get<TMP_Text>(GameContract.EconoProjection, page);
            v.Tax = new EconoTaxView
            {
                Rate = Plain(Text(Get<TMP_Text>(GameContract.EconoTaxRate, page))), Back = back != null && back.activeSelf ? Plain(Text(Get<TMP_Text>(GameContract.EconoBackAmount, page))) : null,
                Owed = Plain(Text(owed)), OwedWhen = Plain(Text(Label(Get<TextLocalizationComponent>(GameContract.EconoOwedWhen, page)))), OwedTone = Tone(owed),
                OwedProgress = Progress(Get<GameBar>(GameContract.EconoOwedBar, page)), Projection = Plain(Text(projection)),
                ProjectionWhen = Plain(Text(Label(Get<TextLocalizationComponent>(GameContract.EconoProjectionWhen, page)))), ProjectionTone = Tone(projection),
                ProjectionProgress = Progress(Get<GameBar>(GameContract.EconoProjectionBar, page)), Deductions = Plain(Text(Get<TMP_Text>(GameContract.EconoDeductions, page))),
                DeductibleKinds = Kinds(LocalizorManager.GetLocalization("econoview_tax_taxdeductions_tooltip")),
                Irs = Get<GameObject>(GameContract.EconoIrsArea, page) is GameObject a && a.activeSelf,
                Paid = Records(TaxLine, null, null)
            };
            TaxBasis(v.Tax);
        }

        // What this tax year's income tax is on, by the game's own reckoning of its projection: each business's sales from
        // the year's first day to yesterday (a property's tax is its own, not on this).
        private static void TaxBasis(EconoTaxView tax)
        {
            GameInstance save = SaveGameManager.Current;
            if (save == null)
            {
                return;
            }
            int perYear = Math.Max(1, save.gameVariables.daysPerYear);
            int first = save.Day / perYear * perYear + 1, last = save.Day - 1;   // the game's own projected period (EconoViewTaxes)
            if (last < first)
            {
                return;
            }
            var parts = new List<(string, float)>(TaxCalculationHelper.GetBusinessIncome(first, last));
            tax.BasisNames = new string[parts.Count];
            tax.BasisAmounts = new float[parts.Count];
            for (int i = 0; i < parts.Count; i++)
            {
                tax.BasisNames[i] = parts[i].Item1;
                tax.BasisAmounts[i] = parts[i].Item2;
            }
        }

        // The game's colour for a tax figure: its red says to pay, its green settled.
        private static int Tone(TMP_Text t)
        {
            if (t == null)
            {
                return 0;
            }
            Color c = t.color;
            return c.r > c.g + 0.15f ? -1 : c.g > c.r + 0.15f ? 1 : 0;
        }

        private static int Progress(GameBar bar) => bar != null && bar.slider != null ? Mathf.RoundToInt(bar.slider.value * 100f) : -1;

        // The game's list of deductible costs, a line each, its dashes taken off.
        private static string[] Kinds(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return new string[0];
            }
            string[] lines = text.Split('\n');
            var kinds = new List<string>(lines.Length);
            for (int i = 0; i < lines.Length; i++)
            {
                string k = lines[i].Trim().TrimStart('-', '–', '—', '•').Trim();
                if (k.Length > 0)
                {
                    kinds.Add(k);
                }
            }
            return kinds.ToArray();
        }

        // One business: the rows its income statement made, what the business is, and the days its heads name.
        private void ReadBusiness(EconoState v)
        {
            EconoViewIncomeStatement statement = _app.businessDetails.incomeStatement;
            v.BusinessKind = Plain(Text(Label(_app.businessTypeLabel)));
            v.CanDestination = true;
            v.CanBizMan = true;
            DayNames(Get<TextLocalizationComponent[]>(GameContract.EconoBusinessDays, _app.businessDetails), SaveGameManager.Current, out v.BusinessDays, out v.DayNumbers);
            BusinessDays(v);
            _businessRows.Clear();
            Transform content = statement.defaultEntry != null ? statement.defaultEntry.parent : null;
            for (int i = 0; content != null && i < content.childCount; i++)
            {
                Transform row = content.GetChild(i);
                if (!Template(statement, row) && row.Find("Name/Title") != null)
                {
                    _businessRows.Add(row);
                }
            }
            var folds = Get<System.Collections.IList>(GameContract.EconoBusinessFolds, statement);
            v.BusinessRows = new EconoIncomeRow[_businessRows.Count];
            for (int i = 0; i < _businessRows.Count; i++)
            {
                Transform row = _businessRows[i];
                Transform title = row.Find("Name/Title");
                var key = title.GetComponent<TextLocalizationComponent>();
                string name = row.name;
                bool total = name.StartsWith(statement.totalEntrySuccess.name, StringComparison.Ordinal) || name.StartsWith(statement.totalEntryDanger.name, StringComparison.Ordinal);
                v.BusinessRows[i] = new EconoIncomeRow
                {
                    Key = key != null && !string.IsNullOrEmpty(key.Key) ? key.Key : Plain(Text(title.GetComponent<TMP_Text>())), Name = Plain(Text(title.GetComponent<TMP_Text>())),
                    Parent = ParentOf(folds, row), Total = total, Texts = Values(row),
                    Kind = name.StartsWith(statement.dangerEntry.name, StringComparison.Ordinal) ? -1 : name.StartsWith(statement.successEntry.name, StringComparison.Ordinal) ? 1 : 0
                };
            }
            for (int i = 0; i < v.BusinessRows.Length; i++)
            {
                if (v.BusinessRows[i].Parent >= 0)
                {
                    v.BusinessRows[v.BusinessRows[i].Parent].Folded |= !_businessRows[i].gameObject.activeSelf;   // the game folds a heading by hiding its rows
                }
            }
        }

        // The four days the business's page was made from, as the game keeps them: its sales, its costs by kind and its profit.
        private void BusinessDays(EconoState v)
        {
            var days = Get<List<FinancialSummary.BusinessIncomeStatement>>(GameContract.EconoBusinessData, _app.businessDetails);
            int n = days != null ? Math.Min(days.Count, v.BusinessDays.Length) : 0;
            v.BusinessSales = new float[n];
            v.BusinessProfit = new float[n];
            v.BusinessCosts = new float[n][];
            for (int i = 0; i < n; i++)
            {
                FinancialSummary.BusinessIncomeStatement d = days[i];
                if (d == null)
                {
                    v.BusinessCosts[i] = new float[EconoViews.CostKeys.Length];
                    continue;
                }
                v.BusinessSales[i] = d.TotalSales;
                v.BusinessProfit[i] = d.TotalProfit;
                v.BusinessCosts[i] = new[] { d.SalaryExpenses, d.RentExpenses, d.MarketingExpenses, d.Theft, d.LicensingFees, d.TotalResources };   // the order of EconoViews.CostKeys
            }
        }

        private static bool Template(EconoViewIncomeStatement s, Transform row) =>
            row == s.defaultEntry || row == s.successEntry || row == s.warningEntry || row == s.dangerEntry || row == s.totalEntrySuccess || row == s.totalEntryDanger;

        // A business row's heading, by the game's own record of which rows fold under which.
        private int ParentOf(System.Collections.IList folds, Transform row)
        {
            for (int i = 0; folds != null && i < folds.Count; i++)
            {
                if (folds[i] is EconoViewIncomeStatement.RowRelationship r && r.Child == row)
                {
                    return _businessRows.IndexOf(r.Parent);
                }
            }
            return -1;
        }

        // A business row's four days in the game's words: its first beside its name, the rest in their own boxes.
        private static string[] Values(Transform row)
        {
            var texts = new string[Days];
            texts[0] = Plain(Text(Child(row, "Name/Value1")));
            for (int d = 1; d < Days; d++)
            {
                Transform box = row.Find("Value" + (d + 1).ToString(Inv));
                texts[d] = box != null ? Plain(Text(box.GetComponentInChildren<TMP_Text>(true))) : null;
            }
            return texts;
        }

        private static TMP_Text Child(Transform row, string path)
        {
            Transform t = row.Find(path);
            return t != null ? t.GetComponent<TMP_Text>() : null;
        }

        // ------------------------------------------------------------------------------------------------- the game's pieces a press reaches

        private bool ToggleIncome(int row)
        {
            var scroller = Get<EconoViewIncomeStatementScrollerController>(GameContract.EconoStatement, _app.overview);
            if (row < 0 || row >= _income.Count || scroller == null || GameContract.EconoToggle.Method == null)
            {
                return false;
            }
            GameContract.EconoToggle.Method.Invoke(scroller, new object[] { _income[row] });   // the game's own toggle: it folds a heading, or opens a row's page
            return true;
        }

        // A line of the income statement that opens something (a business's page, a property in BizMan): the game's own toggle opens it.
        private bool OpenIncome(int row) => row >= 0 && row < _income.Count && _income[row] != null && _income[row].onClick != null && ToggleIncome(row);

        private bool ToggleBusiness(int row)
        {
            if (row < 0 || row >= _businessRows.Count)
            {
                return false;
            }
            Transform name = _businessRows[row].Find("Name");
            Button b = name != null ? name.GetComponent<Button>() : null;
            if (b == null)
            {
                return false;
            }
            b.onClick.Invoke();   // the row's own button: the game shows or hides the rows under it
            return true;
        }

        // The day a group of every transaction stands for, newest first as the page lists them.
        private int DayOfGroup(int group)
        {
            var days = new List<int>(16);
            for (int i = 0; i < _all.Length; i++)
            {
                if (!days.Contains(_all[i].Day))
                {
                    days.Add(_all[i].Day);
                }
            }
            days.Sort((a, b) => b.CompareTo(a));
            return group >= 0 && group < days.Count ? days[group] : -1;
        }

        // A transaction's line: the game's table filtered to its kind, through the game's own drop-down.
        private bool FilterTo(int group, int line)
        {
            int day = DayOfGroup(group), at = 0;
            for (int i = 0; i < _all.Length; i++)
            {
                if (_all[i].Day != day)
                {
                    continue;
                }
                if (at++ == line)
                {
                    var d = Get<GameDropdown>(GameContract.EconoTypes, _app.overview.fullTransactionsPanel);
                    Options(d, out bool _);
                    int option = _options.IndexOf(_all[i].Type + LabelSuffix);
                    if (option < 0 || d == null || option == d.SelectedOptionIndex)
                    {
                        return false;
                    }
                    d.SelectOption(option);
                    return Done(true, "every transaction filtered to one kind");
                }
            }
            return false;
        }

        private bool PressFund(int index)
        {
            var buttons = Get<List<InvestmentFundButton>>(GameContract.EconoFundButtons, Investments());
            InvestmentFundButton fund = buttons != null && index >= 0 && index < buttons.Count ? buttons[index] : null;
            Button b = fund != null ? fund.GetComponent<Button>() : null;
            _typing = -1;
            _chartFocus = -1;
            return b != null && UiWidgets.Press(b);   // the fund's own button: the game shows its page
        }

        private Button PayOffButton() => _loan >= 0 && _loan < _loans.Count ? Get<Button>(GameContract.EconoLoanPay, _loans[_loan]) : null;

        private Button IrsButton()
        {
            EconoViewTaxes page = Taxes();
            var area = Get<GameObject>(GameContract.EconoIrsArea, page);
            return area != null && area.activeSelf ? Get<Button>(GameContract.EconoIrs, page) : null;
        }

        private bool Auto()
        {
            var toggle = Get<Toggle>(GameContract.EconoAutoToggle, Investments());
            return toggle != null && toggle.isOn;
        }

        private bool AutoInvest(bool on) => Page() == UiEcono.Investments && UiWidgets.Set(Get<Toggle>(GameContract.EconoAutoToggle, Investments()), on);

        // The game's field behind a control of the page in view: a loan's daily payment and pay off, a fund's withdraw and auto-invest.
        private GameField Field(int control)
        {
            int page = Page();
            if (page == UiEcono.Loans && _loan >= 0 && _loan < _loans.Count)
            {
                return control == 0 ? Get<GameField>(GameContract.EconoLoanDaily, _loans[_loan]) : control == 1 ? Get<GameField>(GameContract.EconoLoanPayOff, _loans[_loan]) : null;
            }
            if (page == UiEcono.Investments)
            {
                return control == 0 ? Get<GameField>(GameContract.EconoWithdraw, Investments()) : control == 2 ? Get<GameField>(GameContract.EconoAutoField, Investments()) : null;
            }
            return null;
        }

        // An amount's value and its ends, as the game holds them: a daily payment between the bank's least and what is left.
        private bool Ends(int control, out long value, out long least, out long most)
        {
            GameField field = Field(control);
            value = Value(field);
            least = 0;
            most = 0;
            if (field == null)
            {
                return false;
            }
            int page = Page();
            if (page == UiEcono.Loans)
            {
                Loan loan = Get<Loan>(GameContract.EconoLoan, _loans[_loan]);
                most = (long)Math.Floor(loan.remainingAmount);
                least = control == 0 ? Math.Min(LoanHelper.CalculateMinimumDailyPayment(loan), most) : 1;
                return true;
            }
            InvestmentFund fund = Get<InvestmentFund>(GameContract.EconoFund, Investments());
            if (fund == null)
            {
                return false;
            }
            most = control == 0 ? (long)Math.Floor(fund.CurrentValue) : (long)Math.Floor(fund.RemainingValueCapacity);
            return true;
        }

        // An amount written into the game's own field as typed, its own formatting and listeners running; a daily payment
        // applied as the game applies it when its field is left.
        private void Amount(int control, string digits, bool apply)
        {
            GameField field = Field(control);
            if (field == null || field.tmpInputField == null || !field.tmpInputField.interactable)
            {
                return;
            }
            field.SetText(digits);
            if (apply && control == 0 && Page() == UiEcono.Loans)
            {
                field.tmpInputField.onEndEdit.Invoke(field.tmpInputField.text);
            }
            _dirty = true;
        }

        private static long Value(GameField field)
        {
            string raw = field != null ? field.GetRawValue() : null;
            return long.TryParse(Digits(raw), NumberStyles.Integer, Inv, out long v) ? v : 0;
        }

        private static string Raw(GameField field) => field != null ? Digits(field.GetRawValue()) : string.Empty;

        private static string Shown(GameField field) => field != null && field.tmpInputField != null ? Plain(field.tmpInputField.text) : string.Empty;

        // ------------------------------------------------------------------------------------------------- the game's records and words

        private static bool LoanLine(Transaction t) => t.transactionType != null && t.transactionType.IndexOf("loan", StringComparison.OrdinalIgnoreCase) >= 0;

        private static bool FundLine(Transaction t) => t.transactionType == Invested || t.transactionType == PaidOut;

        private static bool TaxLine(Transaction t) => t.transactionType == TaxPaid;

        // The save's own lines of one kind, newest first, the few the page has room for: where a name is given, only those the
        // game booked with it (a loan's bank, a fund).
        private EconoTx[] Records(Func<Transaction, bool> kind, string dataKey, string name)
        {
            IndexTransactions(SaveGameManager.Current);
            var found = new List<EconoTx>(MostRecords);
            Transaction[] all = _txList;
            for (int i = all.Length - 1; i >= 0 && found.Count < MostRecords; i--)
            {
                Transaction t = all[i];
                if (t == null || t.timestamp == null || !kind(t))
                {
                    continue;
                }
                if (dataKey != null && !string.IsNullOrEmpty(name) && (t.transactionData == null || !t.transactionData.TryGetValue(dataKey, out string booked) || booked != name))
                {
                    continue;
                }
                found.Add(new EconoTx { Day = t.timestamp.Day, Type = t.transactionType, What = Plain(EconoViewOverview.GenerateTransactionData(t).ToString()), Amount = t.amount,
                    Balance = UiText.Money(t.balance), Time = Clock(t) });
            }
            return found.ToArray();
        }

        // The save's transactions as they stand, copied once each time they change (a new line moves the money; the game
        // drops the oldest only as new ones come), and indexed by what their rows say, newest first under each, so each table
        // row finds its own record: by day, kind, money and balance where the row has a balance, else by the first three.
        private void IndexTransactions(GameInstance save)
        {
            int count = save != null && save.Transactions != null ? save.Transactions.Count : 0;
            float money = save != null ? save.Money : 0f;
            if (count == _txCount && money == _txMoney && _txList.Length == count)
            {
                Array.Clear(_txUsed, 0, _txUsed.Length);   // the same records: only the rows' matches start afresh
                return;
            }
            _txCount = count;
            _txMoney = money;
            _txFirst.Clear();
            _txLooseFirst.Clear();
            _txList = count > 0 ? save.Transactions.ToArray() : new Transaction[0];
            if (_txNext.Length < _txList.Length)
            {
                _txNext = new int[_txList.Length];
                _txLooseNext = new int[_txList.Length];
                _txUsed = new bool[_txList.Length];
            }
            for (int i = 0; i < _txList.Length; i++)
            {
                Transaction t = _txList[i];
                _txUsed[i] = false;
                _txNext[i] = -1;
                _txLooseNext[i] = -1;
                if (t == null || t.timestamp == null)
                {
                    continue;
                }
                var key = new TxKey { Day = t.timestamp.Day, Type = t.transactionType, Amount = t.amount, Balance = t.balance };
                _txNext[i] = _txFirst.TryGetValue(key, out int older) ? older : -1;
                _txFirst[key] = i;
                var loose = new TxKey { Day = key.Day, Type = key.Type, Amount = key.Amount, Loose = true };
                _txLooseNext[i] = _txLooseFirst.TryGetValue(loose, out older) ? older : -1;
                _txLooseFirst[loose] = i;
            }
        }

        // The game's own record of a row, the newest not yet taken; NaN for a row that carries no balance.
        private Transaction Match(int day, string type, float amount, float balance)
        {
            bool exact = !float.IsNaN(balance);
            var key = exact ? new TxKey { Day = day, Type = type, Amount = amount, Balance = balance } : new TxKey { Day = day, Type = type, Amount = amount, Loose = true };
            Dictionary<TxKey, int> first = exact ? _txFirst : _txLooseFirst;
            int[] next = exact ? _txNext : _txLooseNext;
            for (int i = first.TryGetValue(key, out int at) ? at : -1; i >= 0; i = next[i])
            {
                if (!_txUsed[i])
                {
                    _txUsed[i] = true;
                    return _txList[i];
                }
            }
            return null;
        }

        private static string Clock(Transaction t) => t.timestamp.Hour.ToString("00", Inv) + ":" + ((int)t.timestamp.Minute).ToString("00", Inv);

        private static string Localised(string key) => string.IsNullOrEmpty(key) ? string.Empty : LocalizorManager.IsLocalizedKey(key) ? LocalizorManager.GetLocalization(key) : key;

        private static string Text(TMP_Text t) => t != null ? t.text : null;

        private static TMP_Text Label(TextLocalizationComponent c) => c != null ? c.GetComponent<TMP_Text>() : null;

        // The game's rich text (colours, bold) as plain words: the kit sets its own type.
        private static string Plain(string text)
        {
            if (string.IsNullOrEmpty(text) || text.IndexOf('<') < 0)
            {
                return text;
            }
            var sb = new System.Text.StringBuilder(text.Length);
            bool tag = false;
            foreach (char ch in text)
            {
                if (ch == '<')
                {
                    tag = true;
                }
                else if (ch == '>')
                {
                    tag = false;
                }
                else if (!tag)
                {
                    sb.Append(ch);
                }
            }
            return sb.ToString();
        }
    }
}
