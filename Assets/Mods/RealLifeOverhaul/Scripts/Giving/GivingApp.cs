using System.Collections.Generic;
using RealLifeOverhaul.GameUi;
using UI.Smartphone;
using UnityEngine;

namespace RealLifeOverhaul.Giving
{
    /// <summary>The Giving app's tables as data (GameUi.TableSpec).</summary>
    public static class GivingColumns
    {
        public static readonly TableSpec Off = new TableSpec("Giving", "Off").Column("reallife_giving_col_name", 600f, TableSort.None, flexible: true);

        /// <summary>Charities: 0 name, 1 held now, 2 helped this year, 3 given by you this year.</summary>
        public static readonly TableSpec Charities = new TableSpec("Giving", "Charities")
            .Column("reallife_giving_col_name", 420f, TableSort.Text, flexible: true)
            .Column("reallife_giving_col_held", 240f, TableSort.Money, sums: true)
            .Column("reallife_giving_col_helped", 240f, TableSort.Number)
            .Column("reallife_giving_col_yours", 260f, TableSort.Money, sums: true, right: true)
            .Empty("reallife_giving_empty_charities");

        /// <summary>Loans: 0 person, 1 lent, 2 paid back, 3 left, 4 state.</summary>
        public static readonly TableSpec Loans = new TableSpec("Giving", "Loans")
            .Column("reallife_giving_col_person", 380f, TableSort.Text, flexible: true)
            .Column("reallife_giving_col_lent", 220f, TableSort.Money, sums: true)
            .Column("reallife_giving_col_paid", 220f, TableSort.Money, sums: true)
            .Column("reallife_giving_col_left", 220f, TableSort.Money, sums: true)
            .Column("reallife_giving_col_state", 220f, TableSort.Text, right: true)
            .Empty("reallife_giving_empty_loans");

        /// <summary>This year: 0 charity, 1 paid out, 2 helped.</summary>
        public static readonly TableSpec Year = new TableSpec("Giving", "This year")
            .Column("reallife_giving_col_name", 460f, TableSort.Text, flexible: true)
            .Column("reallife_giving_col_paid_out", 280f, TableSort.Money, sums: true)
            .Column("reallife_giving_col_helped", 240f, TableSort.Number, right: true);
    }

    /// <summary>
    /// The Giving app in the game's frame (GameUi.AppShell), for a player with the RLO interface off: Charities, Loans and This year tabs over a table, and the white box for what is
    /// picked, with the amount to give and the buttons. The same facts and the same director calls as the kit's page. Nothing runs while it is closed.
    /// </summary>
    public sealed class GivingApp
    {
        private static readonly string[] TabKeys = { "reallife_giving_tab_charities", "reallife_giving_tab_loans", "reallife_giving_tab_year" };

        private readonly GiveFacts _facts = new GiveFacts();
        private readonly List<TableRow> _rows = new List<TableRow>(12);
        private readonly List<TableRow> _pool = new List<TableRow>(12);
        private AppShell _shell;
        private DataTable _table;
        private int _tab, _step = 3, _pick;
        private bool _writing;

        /// <summary>The app's panel, asleep; null if the game's parts are not as expected (each has said so in Player.log).</summary>
        public GameObject Build(FullMenu menu, Transform dormant)
        {
            using var piece = SessionWatch.Piece("giving: the app being built from the game's MyEmployees");
            _shell = AppShell.Make(menu, dormant, GivingDirector.AppId);
            _table = DataTable.On(_shell, "Giving");
            if (_table == null || !_shell.SetTabCount(TabKeys.Length) || !_shell.SetColumns(GivingColumns.Off))
            {
                return null;
            }
            _table.Money = amount => GivingGame.Money((long)amount);
            _table.onRowSelected.AddListener(row => UiClone.Safe(() => RowClicked(row)));
            for (int i = 0; i < TabKeys.Length; i++)
            {
                int tab = i;
                UiClone.Key(_shell.TabLabels[i], TabKeys[i]);
                UiClone.Rewire(_shell.Tabs[i], () => ShowTab(tab));
            }
            return _shell.Root;
        }

        public void Opened()
        {
            Refresh();
        }

        public void Closed()
        {
        }

        private void ShowTab(int tab)
        {
            _tab = tab;
            _pick = 0;
            Refresh();
        }

        private void RowClicked(TableRow row)
        {
            if (row != null && row.Pick && _tab == 0)
            {
                _pick = row.Index;
            }
            Refresh();
        }

        private TableRow Row(string id)
        {
            TableRow row;
            if (_pool.Count > 0)
            {
                row = _pool[_pool.Count - 1];
                _pool.RemoveAt(_pool.Count - 1);
            }
            else
            {
                row = new TableRow();
            }
            row.Reset(id);
            row.Pick = true;
            row.Index = _rows.Count;
            _rows.Add(row);
            return row;
        }

        private void Refresh()
        {
            if (_shell == null || _table == null || _writing)
            {
                return;
            }
            _writing = true;
            try
            {
                for (int i = 0; i < _rows.Count; i++)
                {
                    _pool.Add(_rows[i]);
                }
                _rows.Clear();
                _shell.ShowTab(_tab);
                InfoBox box = _shell.Box;
                box.Clear();
                GivingDirector d = GivingDirector.Instance;
                bool ready = d != null && d.Facts(_facts);
                TableSpec spec = GivingColumns.Off;
                UiClone.Words(_shell.Header, GivingText.Fill("reallife_giving_app_head", "spare", GivingGame.Money(_facts.Spare)));
                if (!ready)
                {
                    box.Title(GivingText.Fill("reallife_giving_off_title"));
                    box.Note(GivingText.Fill("reallife_giving_off_body"));
                }
                else
                {
                    switch (_tab)
                    {
                        case 1: spec = Loans(box); break;
                        case 2: spec = Year(box); break;
                        default: spec = Charities(box, d); break;
                    }
                }
                box.Done();
                _table.Show(spec, _rows, null);
            }
            finally
            {
                _writing = false;
            }
        }

        private TableSpec Charities(InfoBox box, GivingDirector d)
        {
            GiveFacts f = _facts;
            for (int i = 0; i < f.Charities.Length; i++)
            {
                GiveCharityFact c = f.Charities[i];
                TableRow row = Row("c" + i);
                row.Set(0, GivingText.Charity(c.Id));
                row.Set(1, GivingGame.Money(c.Pot), c.Pot);
                row.Set(2, c.PeopleYear.ToString(), c.PeopleYear);
                row.Set(3, GivingGame.Money(c.PlayerYear), c.PlayerYear);
            }
            int at = Mathf.Clamp(_pick, 0, f.Charities.Length - 1);
            GiveCharityFact p = f.Charities[at];
            box.Title(GivingText.Charity(p.Id));
            box.Note(GivingText.CharityAbout(p.Id));
            if (f.Why != 0)
            {
                box.Line(GivingText.Fill(f.Why == 2 ? "reallife_giving_why_session" : "reallife_giving_why_people"), CellTone.Yellow);
            }
            box.RowWords(GivingText.Fill("reallife_giving_row_held"), GivingGame.Money(p.Pot));
            box.RowWords(GivingText.Fill("reallife_giving_row_cap"), GivingGame.Money(p.Cap));
            box.RowWords(GivingText.Fill("reallife_giving_row_last"), GivingText.Fill("reallife_giving_row_last_value", "amount", GivingGame.Money(p.LastMoney), "n", p.LastPeople.ToString()));
            box.RowWords(GivingText.Fill("reallife_giving_row_yours"), GivingGame.Money(p.PlayerTotal));
            if (p.Standing > 0)
            {
                box.RowWords(GivingText.Fill("reallife_giving_row_monthly"), GivingGame.Money(p.Standing));
            }
            _step = Mathf.Clamp(_step, 0, GivingCatalogue.Steps.Length - 1);
            box.Selector(GivingCatalogue.Steps.Length, _step, i => GivingGame.Money(GivingRules.StepNow(i, f.Level)), i => { _step = i; Refresh(); });
            Transform buttons = box.ButtonRow();
            int charity = p.Id, step = _step;
            InfoBox.Button(buttons, InfoBox.Positive, "reallife_giving_give", () => { RealLifeOverhaul.Ui.GivingLive.Instance.Give(charity, step); Refresh(); });
            InfoBox.Button(buttons, InfoBox.Additional, "reallife_giving_monthly", () => { RealLifeOverhaul.Ui.GivingLive.Instance.Monthly(charity, step); Refresh(); });
            if (p.Standing > 0)
            {
                InfoBox.Button(buttons, InfoBox.Negative, "reallife_giving_stop_monthly", () => { RealLifeOverhaul.Ui.GivingLive.Instance.StopMonthly(charity); Refresh(); });
            }
            return GivingColumns.Charities;
        }

        private TableSpec Loans(InfoBox box)
        {
            GiveFacts f = _facts;
            for (int i = f.Loans.Count - 1; i >= 0; i--)
            {
                GiveLoanFact l = f.Loans[i];
                TableRow row = Row("l" + i);
                row.Pick = false;
                row.Set(0, l.Name);
                row.Set(1, GivingGame.Money(l.Principal), l.Principal);
                row.Set(2, GivingGame.Money(l.Paid), l.Paid);
                row.Set(3, GivingGame.Money(l.Remaining), l.Remaining);
                row.Set(4, GivingText.Fill(l.State == LoanState.Open ? l.Misses > 0 ? "reallife_giving_loan_late" : "reallife_giving_loan_open"
                    : l.State == LoanState.Repaid ? "reallife_giving_loan_repaid_chip" : "reallife_giving_loan_off_chip"),
                    l.State == LoanState.Open && l.Misses == 0 ? TableRow.Plain : l.State == LoanState.Repaid ? TableRow.Green : TableRow.Yellow);
            }
            box.Title(GivingText.Fill("reallife_giving_sec_how_loans"));
            box.Note(GivingText.Fill("reallife_giving_loans_how", "parts", GivingCatalogue.LoanParts.ToString(), "misses", GivingCatalogue.LoanMisses.ToString()));
            if (f.Loans.Count == 0)
            {
                box.Note(GivingText.Fill("reallife_giving_loans_none"));
            }
            return GivingColumns.Loans;
        }

        private TableSpec Year(InfoBox box)
        {
            GiveFacts f = _facts;
            for (int i = 0; i < f.Charities.Length; i++)
            {
                GiveCharityFact c = f.Charities[i];
                TableRow row = Row("y" + i);
                row.Pick = false;
                row.Set(0, GivingText.Charity(c.Id));
                row.Set(1, GivingGame.Money(c.PaidYear), c.PaidYear);
                row.Set(2, c.PeopleYear.ToString(), c.PeopleYear);
            }
            box.Title(GivingText.Fill("reallife_giving_sec_tax"));
            box.RowWords(GivingText.Fill("reallife_giving_row_gifts"), GivingGame.Money(f.TaxGifts));
            box.RowWords(GivingText.Fill("reallife_giving_row_income"), GivingGame.Money(f.Income));
            box.RowWords(GivingText.Fill("reallife_giving_row_cap_share", "share", (f.DeductionShare / 10f).ToString("0.#")), GivingGame.Money(f.TaxCap));
            box.RowWords(GivingText.Fill("reallife_giving_row_deducted"), GivingGame.Money(f.TaxClaimed));
            box.Note(GivingText.Fill("reallife_giving_tax_note"));
            string neighbours = GivingText.Fill("reallife_giving_row_neighbours_value", "amount", GivingGame.Money(f.NeighbourYear), "n", f.NeighbourHelped.ToString());
            box.RowWords(GivingText.Fill("reallife_giving_row_neighbours"), neighbours);
            box.RowWords(GivingText.Fill("reallife_giving_row_rivals"), GivingGame.Money(f.RivalsYear));
            return GivingColumns.Year;
        }
    }
}
