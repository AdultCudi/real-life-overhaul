using System;
using System.Collections.Generic;
using BAModAPI;
using Helpers;
using Localizor;
using RealLifeOverhaul.GameUi;
using TMPro;
using UI;
using UI.Notification;
using UI.Smartphone;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// What the Finance app shows, in the game's frame (GameUi.AppShell, copied from MyEmployees) as the Planner and
    /// the Guide are. Five tabs over the table on the right, and on the left the white box with the tab's figures:
    ///
    ///   Overview    cash, what is owed, what leaves tomorrow, net worth, the rating; the table lists every debt
    ///   Credit      the score and its grade, what would raise it, each factor; the table lists the file's last events
    ///   Borrow      the city's banks with today's offer, and the other kinds of credit; pick one, see what it costs, apply
    ///   Cash flow   money in and out by the game's kinds of transaction for yesterday, 7 days or 30
    ///   Accounts    every debt with its lender, what is owed, the next payment and how it stands; pick one to pay it,
    ///               draw on it or close it, and the ways out of debt (FinanceApp.Accounts.cs)
    ///
    /// A list goes in the table, which scrolls; the white box, which does not, shows only the one thing picked.
    ///
    /// Everything on it is the game's. Nothing runs while the app is closed: the rows and figures are made when it
    /// opens, when a tab is picked and when the player changes something in it.
    /// </summary>
    public sealed partial class FinanceApp
    {
        /// <summary>The glyph on the app's icon: the game's coin, or a picture the phone's own scene uses.</summary>
        public static readonly string[] AppGlyphs = { "Icon-Money", "Icon-Coin" };

        private const float WidthA = 620f, WidthB = 500f, WidthC = 300f, WidthD = 300f;
        private const int TypedAmountLength = 9;

        private static readonly string[] TabKeys = { "reallife_econ_tab_overview", "reallife_econ_tab_credit", "reallife_econ_tab_borrow", "reallife_econ_tab_cash", "reallife_econ_tab_accounts" };
        private static readonly string[] ColumnNames = { "ColumnA", "ColumnB", "ColumnC", "ColumnD" };
        private static readonly float[] ColumnWidths = { WidthA, WidthB, WidthC, WidthD };
        private static readonly string[][] ColumnKeys =
        {
            new[] { "reallife_econ_col_debt", "reallife_econ_col_owed", "reallife_econ_col_per_day", "reallife_econ_col_left" },
            new[] { "reallife_econ_col_event", "reallife_econ_col_day", "reallife_econ_col_amount", "reallife_econ_col_effect" },
            new[] { "reallife_econ_col_lender", "reallife_econ_col_owed_them", "reallife_econ_col_rate", "reallife_econ_col_most" },
            new[] { "reallife_econ_col_kind", "reallife_econ_col_net", "reallife_econ_col_in", "reallife_econ_col_out" },
            new[] { "reallife_econ_col_account", "reallife_econ_col_lender", "reallife_econ_col_owed", "reallife_econ_col_state" }
        };
        private static readonly int[] AmountSteps = { 500, 1000, 2500, 5000, 10000, 25000, 50000, 100000, 250000, 500000, 1000000, 2500000, 5000000 };

        public static IModLogger Logger;

        private readonly EconomyDirector _director;
        private readonly List<FinanceRow> _rows = new List<FinanceRow>(40);
        private readonly List<FinanceRow> _pool = new List<FinanceRow>(40);
        private readonly List<DebtLine> _debts = new List<DebtLine>(6);
        private readonly List<int> _amounts = new List<int>(16);
        private readonly CreditFactor[] _tips = new CreditFactor[2];
        private readonly CashFlow _flow = new CashFlow();

        private AppShell _shell;
        private FinanceTable _table;
        private FullMenu _menu;
        private int _tab, _wanted = -1;
        private int _period = CashFlow.Week;
        private Lender _lender;
        private int _amount;
        private bool _ready;       // the director has brought the file up to date for this opening

        public FinanceApp(EconomyDirector director)
        {
            _director = director;
        }

        public static Sprite Glyph()
        {
            for (int i = 0; i < AppGlyphs.Length; i++)
            {
                Sprite sprite = GameSprites.Get(AppGlyphs[i]);
                if (sprite != null)
                {
                    return sprite;
                }
            }
            return null;
        }

        // ---------------------------------------------------------------- building it from the game's parts

        /// <summary>The app's panel, asleep; null if the game's parts are not as expected (each has said so in Player.log).</summary>
        public GameObject Build(FullMenu menu, Transform dormant)
        {
            _menu = menu;
            SessionWatch.Mark("finance: the app being built from the game's MyEmployees");   // once per city: a few hundred objects copied in one frame
            _shell = AppShell.Make(menu, dormant, EconomyDirector.AppId);
            if (_shell == null || !PrepareRow(_shell.RowPrefab) || !_shell.SetTabCount(TabKeys.Length))
            {
                return null;
            }
            if (!_shell.SetColumns(ColumnNames, ColumnKeys[0], ColumnWidths, 1))
            {
                return null;
            }
            FinanceCellView.SelectedColour = _shell.RowSelected;
            _table = _shell.List.gameObject.AddComponent<FinanceTable>();
            _table.scroller = _shell.Scroller;
            _table.prefab = _shell.RowPrefab.AddComponent<FinanceCellView>();
            _table.allowRowSelection = true;
            _table.onRowSelected.AddListener(row => UiClone.Safe(() => RowClicked(row)));
            for (int i = 0; i < TabKeys.Length; i++)
            {
                int tab = i;
                UiClone.Key(_shell.TabLabels[i], TabKeys[i]);
                UiClone.Rewire(_shell.Tabs[i], () => ShowTab(tab));
            }
            return _shell.Root;
        }

        // The game's employee row has a tick box and ten cells. Four are kept and renamed, as the Planner keeps them:
        // the name, the task (its warning sign hidden), the business and the satisfaction, which the game sets to the right.
        private static bool PrepareRow(GameObject row)
        {
            Transform t = row.transform;
            var layout = row.GetComponent<HorizontalLayoutGroup>();
            Transform a = t.Find("EmployeeName"), b = t.Find("TaskInfo"), c = t.Find("BusinessName"), d = t.Find("Satisfaction");
            if (layout == null || a == null || b == null || c == null || d == null || b.Find("Task") == null)
            {
                UiClone.Fallback("the game's employee row is not built as expected, so the Finance app is not added");
                return false;
            }
            UiClone.KeepOnly(t, "Background", "RaycastTarget", "EmployeeName", "TaskInfo", "BusinessName", "Satisfaction");
            a.name = "A";
            b.name = "B";
            c.name = "C";
            d.name = "D";
            AppShell.RowLayout(layout);
            AppShell.Width(a, WidthA, false);
            AppShell.Width(b, WidthB, true);
            AppShell.Width(c, WidthC, false);
            AppShell.Width(d, WidthD, false);
            AppShell.OneLine(a.GetComponent<TMP_Text>());
            AppShell.OneLine(c.GetComponent<TMP_Text>());
            AppShell.OneLine(d.GetComponent<TMP_Text>());
            AppShell.OneLine(b.Find("Task").GetComponent<TMP_Text>());
            Transform sign = b.Find("WarningIcon");
            if (sign != null)
            {
                sign.gameObject.SetActive(false);
            }
            return true;
        }

        // ---------------------------------------------------------------- opening and the tabs

        /// <summary>The tab to show the next time the app opens (a notification was clicked).</summary>
        public void Want(int tab)
        {
            _wanted = Mathf.Clamp(tab, 0, TabKeys.Length - 1);
            if (_shell != null && _shell.Root != null && _shell.Root.activeInHierarchy)
            {
                Opened();
            }
        }

        public void Opened()
        {
            if (_shell == null || _table == null)
            {
                return;
            }
            if (_wanted >= 0)
            {
                _tab = _wanted;
                _wanted = -1;
            }
            _ready = _director.AppOpened();
            Refresh();
        }

        public void Closed()
        {
            _ready = false;
        }

        private void ShowTab(int tab)
        {
            if (tab != _tab)
            {
                _tab = tab;
                Refresh();
            }
        }

        // Everything the open tab shows, made again: asked for when the app opens, a tab is picked or a figure has changed.
        private void Refresh()
        {
            if (_shell == null || _table == null)
            {
                return;
            }
            float started = Time.realtimeSinceStartup;
            for (int i = 0; i < _rows.Count; i++)
            {
                _pool.Add(_rows[i]);
            }
            _rows.Clear();
            _shell.ShowTab(_tab);
            _shell.SetColumns(ColumnNames, ColumnKeys[_tab], ColumnWidths, 1);
            InfoBox box = _shell.Box;
            box.Clear();
            string selected = null;
            if (!_ready || !CreditLedger.Ready)
            {
                UiClone.Words(_shell.Header, FinanceText.T("reallife_econ_app"));
                box.Title(FinanceText.T("reallife_econ_off_title"));
                box.Line(FinanceText.T("reallife_econ_off_body"));
            }
            else
            {
                CreditScore score = CreditLedger.Score;
                UiClone.Words(_shell.Header, FinanceText.T("reallife_econ_headline", "score", score.Value.ToString(), "grade", FinanceText.Grade(score.Grade)));
                switch (_tab)
                {
                    case Finance.Credit: WriteCredit(box, score); break;
                    case Finance.Borrow: selected = WriteBorrow(box, score); break;
                    case Finance.CashFlow: WriteCashFlow(box); break;
                    case Finance.Accounts: selected = WriteAccounts(box); break;
                    default: WriteOverview(box, score); break;
                }
            }
            box.Done();
            _table.Show(_rows, selected);
            if (Meta.Dev.Verbose)
            {
                Logger?.Info("Finance: tab " + _tab + " written, " + _rows.Count + " row(s); " + ((Time.realtimeSinceStartup - started) * 1000f).ToString("0.0") + " ms.");
            }
        }

        private FinanceRow Row(string id)
        {
            FinanceRow row;
            if (_pool.Count > 0)
            {
                row = _pool[_pool.Count - 1];
                _pool.RemoveAt(_pool.Count - 1);
            }
            else
            {
                row = new FinanceRow();
            }
            row.Id = id;
            row.A = row.B = row.C = row.D = null;
            row.Info = row.Pick = false;
            row.Index = 0;
            row.Tone = 0;
            _rows.Add(row);
            return row;
        }

        private void Say(string id, string words)
        {
            FinanceRow row = Row(id);
            row.Info = true;
            row.A = words;
        }

        // ---------------------------------------------------------------- Overview

        private void WriteOverview(InfoBox box, CreditScore score)
        {
            GameInstance save = SaveGameManager.Current;
            List<Loan> loans = save.Loans;
            CreditFile file = CreditLedger.File;
            Obligations due = Books.Tomorrow(file);   // the figure the overdraft covers a short day against
            float owedLoans = 0f, payments = due.Loans, interest = due.LoanInterest;
            for (int i = 0; i < loans.Count; i++)
            {
                Loan loan = loans[i];
                owedLoans += loan.remainingAmount;
                FinanceRow row = Row("loan:" + i);
                row.A = GameLoans.BankName(loan);
                row.B = FinanceText.T("reallife_econ_row_owed_of", "owed", FinanceText.Money(loan.remainingAmount), "total", FinanceText.Money(loan.totalAmount));
                row.C = FinanceText.Money(GameLoans.Due(loan));
                row.D = loan.dailyPayment > 0 ? FinanceText.T("reallife_econ_row_days", "days", Mathf.CeilToInt(loan.remainingAmount / loan.dailyPayment).ToString()) : string.Empty;
            }
            float owedAccounts = OverviewAccounts(file, save.Day);
            _debts.Clear();
            EconomyBridges.OtherDebts?.Invoke(_debts);
            float owedOther = 0f, otherDaily = due.Other;
            for (int i = 0; i < _debts.Count; i++)
            {
                DebtLine debt = _debts[i];
                owedOther += debt.Owed;
                FinanceRow row = Row("debt:" + i);
                row.A = FinanceText.T(debt.NameKey);
                row.B = FinanceText.Money(debt.Owed);
                row.C = debt.PerDay > 0 ? FinanceText.Money(Mathf.Min(debt.PerDay, debt.Owed)) : string.Empty;
                row.D = debt.LateDays > 0 ? FinanceText.T("reallife_econ_row_late", "days", debt.LateDays.ToString()) : string.Empty;
                row.Tone = debt.LateDays > 0 ? 2 : 0;
            }
            if (_rows.Count == 0)
            {
                Say("none", FinanceText.T("reallife_econ_row_no_debts"));
            }

            // Rent and wages as the game's daily run takes them (Books.Tomorrow).
            float rent = due.Rent, wages = due.Wages;
            var wealth = PlayerHelper.GetPersonalWealth();   // the game's own figure for what the player is worth (Persona shows it)

            box.Title(FinanceText.T("reallife_econ_tab_overview"));
            box.Line(FinanceText.Pair("reallife_econ_over_cash", FinanceText.Money(save.Money)));
            box.Line(FinanceText.Pair("reallife_econ_over_owed", FinanceText.Money(owedLoans + owedAccounts + owedOther)));
            if (owedOther > 0f || owedAccounts > 0f)
            {
                box.Line(FinanceText.T("reallife_econ_over_owed_three", "loans", FinanceText.Money(owedLoans), "accounts", FinanceText.Money(owedAccounts), "other", FinanceText.Money(owedOther)));
            }
            box.Line(FinanceText.Pair("reallife_econ_over_rating", FinanceText.T("reallife_econ_rating_value", "score", score.Value.ToString(), "grade", FinanceText.Grade(score.Grade))));
            box.Headline("reallife_econ_over_costs");
            box.Line(FinanceText.Pair("reallife_econ_over_loan_payments", FinanceText.Money(payments)));
            if (interest > 0f)
            {
                box.Line(FinanceText.T("reallife_econ_over_interest", "amount", FinanceText.Money(interest)));
            }
            if (due.Accounts > 0f)
            {
                box.Line(FinanceText.Pair("reallife_econ_over_accounts", FinanceText.Money(due.Accounts)));
            }
            if (otherDaily > 0f)
            {
                box.Line(FinanceText.Pair("reallife_econ_over_fines", FinanceText.Money(otherDaily)));
            }
            box.Line(FinanceText.Pair("reallife_econ_over_rent", FinanceText.Money(rent)));
            box.Line(FinanceText.Pair("reallife_econ_over_wages", FinanceText.Money(wages)));
            box.Headline("reallife_econ_over_worth_head");
            box.Line(FinanceText.Pair("reallife_econ_over_leaves", FinanceText.Money(due.Total)));
            box.Line(FinanceText.Pair("reallife_econ_over_worth", FinanceText.Money(wealth.CurrentWealth - owedOther - owedAccounts)));
            box.Line(FinanceText.T("reallife_econ_over_worth_parts", "investments", FinanceText.Money(wealth.totalInvestments), "assets", FinanceText.Money(wealth.totalAssets)));
            Transform buttons = box.ButtonRow();
            InfoBox.Button(buttons, InfoBox.Additional, "reallife_econ_open_econoview", OpenEconoView);
        }

        // Repaying and changing a daily payment stay where the game has them: its own loans page.
        private void OpenEconoView()
        {
            if (_menu != null)
            {
                _menu.ShowApp(AppName.EconoView);
            }
        }

        // ---------------------------------------------------------------- Credit

        private void WriteCredit(InfoBox box, CreditScore score)
        {
            box.Title(FinanceText.T("reallife_econ_rating_value", "score", score.Value.ToString(), "grade", FinanceText.Grade(score.Grade)));
            box.Line(FinanceText.T(score.Lends ? "reallife_econ_credit_threshold_ok" : "reallife_econ_credit_threshold_no", "threshold", CreditCatalogue.LendingThreshold.ToString()));
            int tips = CreditScoring.Tips(score, _tips);
            if (tips > 0)
            {
                box.Headline("reallife_econ_credit_raise");
                for (int i = 0; i < tips; i++)
                {
                    box.Line(FinanceText.Tip(score, _tips[i]));
                }
            }
            box.Headline("reallife_econ_credit_factors");
            for (int f = 0; f < (int)CreditFactor.Count; f++)
            {
                box.Line(FinanceText.FactorName((CreditFactor)f) + ": <b>" + FinanceText.Stands(score, (CreditFactor)f) + "</b>");
            }

            List<CreditEvent> events = CreditLedger.File.Events;
            for (int i = events.Count - 1; i >= 0 && _rows.Count < CreditCatalogue.LedgerShown; i--)
            {
                CreditEvent e = events[i];
                FinanceRow row = Row("event:" + i);
                row.A = FinanceText.Event(e);
                row.B = FinanceText.Day(e.Day);
                row.C = FinanceText.EventAmount(e);
                row.D = FinanceText.Effect(e.Effect);
                row.Tone = e.Effect < 0 ? 2 : e.Effect > 0 ? 1 : 0;
            }
            if (_rows.Count == 0)
            {
                Say("none", FinanceText.T("reallife_econ_row_no_events"));
            }
            WorstLine(box, score);
        }

        // ---------------------------------------------------------------- Borrow

        private string WriteBorrow(InfoBox box, CreditScore score)
        {
            if (!score.Lends)
            {
                // Under the threshold: the reason, what would raise the rating, and no way to apply.
                _lender = null;
                box.Title(FinanceText.T("reallife_econ_borrow_refused_title"));
                box.Line(FinanceText.T("reallife_econ_borrow_refused", "score", score.Value.ToString(), "grade", FinanceText.Grade(score.Grade), "threshold", CreditCatalogue.LendingThreshold.ToString()));
                int tips = CreditScoring.Tips(score, _tips);
                if (tips > 0)
                {
                    box.Headline("reallife_econ_credit_raise");
                    for (int i = 0; i < tips; i++)
                    {
                        box.Line(FinanceText.Tip(score, _tips[i]));
                    }
                }
                Say("refused", FinanceText.T("reallife_econ_row_no_lender", "threshold", CreditCatalogue.LendingThreshold.ToString()));
                return null;
            }
            IReadOnlyList<Lender> lenders = BankTerms.Lenders;
            string selected = ProductRows();   // the other kinds of credit, under the banks
            bool found = false;
            for (int i = 0; i < lenders.Count; i++)
            {
                Lender lender = lenders[i];
                FinanceRow row = Row("lender:" + i);
                row.Pick = true;
                row.Index = i;
                row.A = lender.Name;
                row.B = FinanceText.Money(GameLoans.OwedTo(lender.Address));
                row.C = FinanceText.T("reallife_econ_row_rate", "rate", PhoneLending.RateOffered(lender, score.Value).ToString("0.#"));
                float most = PhoneLending.MostNow(lender, score.Value);
                row.D = FinanceText.Money(most);
                row.Tone = most < CreditCatalogue.LoanLeast ? 2 : 0;
                if (lender == _lender)
                {
                    selected = row.Id;
                    found = true;
                }
            }
            if (!found)
            {
                _lender = null;
            }
            if (lenders.Count == 0)
            {
                Say("none", FinanceText.T("reallife_econ_row_no_banks"));
            }
            if (_lender == null && _product >= 0)
            {
                WriteProduct(box);
                return selected;
            }
            if (_lender == null)
            {
                box.Title(FinanceText.T("reallife_econ_tab_borrow"));
                box.Line(FinanceText.T("reallife_econ_borrow_pick"));
                box.Line(FinanceText.T("reallife_econ_borrow_fee_note", "share", FeeShare(), "least", FinanceText.Money(CreditCatalogue.PhoneFeeLeast)));
                box.Line(FinanceText.T("reallife_econ_borrow_repay_note"));
                return null;
            }
            WriteOffer(box, score);
            return selected;
        }

        private static string FeeShare()
        {
            return (CreditCatalogue.PhoneFeeShare * 100f).ToString("0.#") + "%";
        }

        private void WriteOffer(InfoBox box, CreditScore score)
        {
            Lender lender = _lender;
            int most = Mathf.FloorToInt(PhoneLending.MostNow(lender, score.Value));
            box.Title(lender.Name);
            box.Line(FinanceText.Pair("reallife_econ_offer_most", FinanceText.Money(most)));
            if (most < CreditCatalogue.LoanLeast)
            {
                box.Line(FinanceText.T("reallife_econ_offer_nothing", "least", FinanceText.Money(CreditCatalogue.LoanLeast)));
                InfoBox.Button(box.ButtonRow(), InfoBox.Additional, "common_close", ClosePick);
                return;
            }
            _amount = Mathf.Clamp(_amount, CreditCatalogue.LoanLeast, most);
            Amounts(most);
            LoanQuote quote = PhoneLending.Quote(lender, _amount, score.Value);
            box.Line(FinanceText.Pair("reallife_econ_offer_rate", FinanceText.T("reallife_econ_row_rate", "rate", quote.AnnualRate.ToString("0.#"))));
            box.Headline("reallife_econ_offer_amount");
            if (!box.HasSelector || !box.Selector(_amounts.Count, _amounts.IndexOf(_amount), i => FinanceText.Money(_amounts[i]), i =>
                {
                    _amount = _amounts[i];
                    Refresh();
                }))
            {
                box.Line(FinanceText.Money(_amount));
            }
            UIs uis = InstanceBehavior<UIs>.Instance;
            Transform entry = box.ButtonRow();
            if (uis != null && uis.textInputUi != null)
            {
                // The game's own text box. It takes any text, so what is typed is read as a whole amount (digits; the
                // comma the game writes in "$5,000" is let through) and checked against the least and the most.
                InfoBox.Button(entry, InfoBox.Additional, "reallife_econ_offer_type", () =>
                    uis.textInputUi.Show("reallife_econ_offer_placeholder", "common_confirm", text => UiClone.Safe(() => Typed(text)), null, _amount.ToString(), false, TypedAmountLength));
            }
            box.Headline("reallife_econ_offer_cost");
            box.Line(FinanceText.Pair("reallife_econ_offer_payment", FinanceText.Money(quote.DailyPayment)));
            box.Line(FinanceText.Pair("reallife_econ_offer_interest", FinanceText.Money(quote.DailyInterest)));
            box.Line(FinanceText.Pair("reallife_econ_offer_days", quote.Days.ToString()));
            box.Line(FinanceText.Pair("reallife_econ_offer_fee", FinanceText.Money(quote.Fee)));
            box.Line(FinanceText.Pair("reallife_econ_offer_total", FinanceText.Money(quote.Cost)));
            Transform buttons = box.ButtonRow();
            InfoBox.Button(buttons, InfoBox.Positive, "reallife_econ_offer_apply", Apply);
            InfoBox.Button(buttons, InfoBox.Additional, "common_cancel", ClosePick);
        }

        // The amounts the selector steps through: round figures up to the most on offer, the most itself, and what was typed.
        private void Amounts(int most)
        {
            _amounts.Clear();
            for (int i = 0; i < AmountSteps.Length && AmountSteps[i] < most; i++)
            {
                _amounts.Add(AmountSteps[i]);
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

        private void Typed(string text)
        {
            if (_lender == null || !CreditLedger.Ready)
            {
                return;
            }
            int typed = 0;
            bool digits = false;
            for (int i = 0; text != null && i < text.Length; i++)
            {
                char c = text[i];
                if (c >= '0' && c <= '9')
                {
                    typed = Mathf.Min(typed * 10 + (c - '0'), 99999999);
                    digits = true;
                }
                else if (c != ',' && c != ' ' && c != '$')
                {
                    digits = false;   // anything else (a decimal point, a letter) is no whole amount: the game's own "invalid amount" says so
                    break;
                }
            }
            int most = Mathf.FloorToInt(PhoneLending.MostNow(_lender, CreditLedger.Score.Value));
            if (!digits)
            {
                Notifications.ShowError("common_notification_invalid_amount", null, false);
                return;
            }
            if (typed < CreditCatalogue.LoanLeast)
            {
                Notifications.Show(NotificationType.Error, "reallife_econ_note_too_little", new Dictionary<string, string> { { "least", FinanceText.Money(CreditCatalogue.LoanLeast) } }, 6f, null, null, true, false);
                typed = CreditCatalogue.LoanLeast;
            }
            else if (typed > most)
            {
                Notifications.Show(NotificationType.Error, "reallife_econ_note_too_much", new Dictionary<string, string> { { "lender", _lender.Name }, { "most", FinanceText.Money(most) } }, 6f, null, null, true, false);
                typed = most;
            }
            _amount = typed;
            Refresh();
        }

        private void RowClicked(FinanceRow row)
        {
            if (row != null && row.Pick && PickedOther(row))
            {
                Refresh();
                return;
            }
            if (row == null || !row.Pick || _tab != Finance.Borrow || row.Index < 0 || row.Index >= BankTerms.Lenders.Count)
            {
                _table.ClearSelection();
                return;
            }
            Lender lender = BankTerms.Lenders[row.Index];
            if (lender != _lender)
            {
                _lender = lender;
                _amount = 0;
            }
            _product = -1;
            Refresh();
        }

        private void ClosePick()
        {
            _lender = null;
            _product = -1;
            _picked = null;
            _table.ClearSelection();
            Refresh();
        }

        // The game's own question box: everything the loan costs is said before it is taken.
        private void Apply()
        {
            Lender lender = _lender;
            if (lender == null || !CreditLedger.Ready)
            {
                return;
            }
            CreditRefusal refusal;
            if (!CreditLedger.CanBorrow(out refusal))
            {
                _director.TellRefused();
                Refresh();
                return;
            }
            int amount = _amount;
            LoanQuote quote = PhoneLending.Quote(lender, amount, CreditLedger.Score.Value);
            HudConfirm.Show("reallife_econ_confirm_title".Localize(new { lender = lender.Name }),
                "reallife_econ_confirm_body".Localize(new
                {
                    amount = FinanceText.Money(amount),
                    lender = lender.Name,
                    rate = quote.AnnualRate.ToString("0.#"),
                    payment = FinanceText.Money(quote.DailyPayment),
                    interest = FinanceText.Money(quote.DailyInterest),
                    days = quote.Days.ToString(),
                    fee = FinanceText.Money(quote.Fee),
                    cost = FinanceText.Money(quote.Cost)
                }),
                () => UiClone.Safe(() => Confirmed(lender, amount)), null, "reallife_econ_offer_apply", "common_cancel", false, false);
        }

        private void Confirmed(Lender lender, int amount)
        {
            LoanQuote quote;
            LoanAnswer answer = _director.Borrow(lender, amount, out quote);
            switch (answer)
            {
                case LoanAnswer.Approved:
                    Notifications.Show(NotificationType.Success, "reallife_econ_note_lent", new Dictionary<string, string>
                    {
                        { "lender", lender.Name }, { "amount", FinanceText.Money(amount) }, { "fee", FinanceText.Money(quote.Fee) },
                        { "payment", FinanceText.Money(quote.DailyPayment + quote.DailyInterest) }
                    }, 10f, null, null, true, false);
                    _lender = null;
                    _tab = Finance.Overview;
                    break;
                case LoanAnswer.TooMuch:
                    Notifications.Show(NotificationType.Error, "reallife_econ_note_too_much", new Dictionary<string, string>
                    {
                        { "lender", lender.Name }, { "most", FinanceText.Money(PhoneLending.MostNow(lender, CreditLedger.Value)) }
                    }, 8f, null, null, true, false);
                    break;
                case LoanAnswer.TooLittle:
                    Notifications.Show(NotificationType.Error, "reallife_econ_note_too_little", new Dictionary<string, string> { { "least", FinanceText.Money(CreditCatalogue.LoanLeast) } }, 6f, null, null, true, false);
                    break;
                default:
                    if (CreditLedger.Ready)
                    {
                        _director.TellRefused();
                    }
                    break;
            }
            if (_shell != null && _shell.Root != null && _shell.Root.activeInHierarchy)
            {
                _ready = _director.Running && CreditLedger.Ready;
                Refresh();
            }
        }

        // ---------------------------------------------------------------- Cash flow

        private void WriteCashFlow(InfoBox box)
        {
            int today = SaveGameManager.Current.Day;
            _flow.Read(_period, today);
            box.Title(FinanceText.T("reallife_econ_tab_cash"));
            if (!box.HasSelector || !box.Selector(CashFlow.PeriodDays.Length, _period, CashFlow.PeriodName, i =>
                {
                    _period = i;
                    Refresh();
                }))
            {
                box.Line(CashFlow.PeriodName(_period));
            }
            box.Line(FinanceText.Pair("reallife_econ_cash_in", FinanceText.Money(_flow.In)));
            box.Line(FinanceText.Pair("reallife_econ_cash_out", FinanceText.Money(-_flow.Out)));
            box.Line(FinanceText.Pair("reallife_econ_cash_net", FinanceText.Money(_flow.In + _flow.Out)));
            if (_flow.OldestDay > _flow.FromDay && _flow.FromDay >= 1)
            {
                box.Line(FinanceText.T("reallife_econ_cash_reach", "day", _flow.OldestDay.ToString()));
            }
            if (_flow.TopCount > 0)
            {
                box.Headline("reallife_econ_cash_largest");
                for (int i = 0; i < _flow.TopCount; i++)
                {
                    CashFlow.Item item = _flow.Top[i];
                    box.Line(CashFlow.ItemName(item) + ": <b>" + FinanceText.Money(item.Amount) + "</b>");
                }
            }
            for (int i = 0; i < _flow.Kinds.Count; i++)
            {
                CashFlow.Kind kind = _flow.Kinds[i];
                FinanceRow row = Row("kind:" + i);
                row.A = CashFlow.KindName(kind.Type);
                row.B = FinanceText.Money(kind.Net);
                row.C = kind.In > 0f ? FinanceText.Money(kind.In) : string.Empty;
                row.D = kind.Out < 0f ? FinanceText.Money(-kind.Out) : string.Empty;
                row.Tone = kind.Net < 0f ? 2 : 0;
            }
            if (_rows.Count == 0)
            {
                Say("none", FinanceText.T("reallife_econ_row_no_cash"));
            }
        }
    }
}
