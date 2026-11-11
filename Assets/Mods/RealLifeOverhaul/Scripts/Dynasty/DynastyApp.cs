using System;
using System.Collections.Generic;
using RealLifeOverhaul.GameUi;
using RealLifeOverhaul.Giving;
using RealLifeOverhaul.Ui;
using UI.Smartphone;
using UnityEngine;

namespace RealLifeOverhaul.Dynasty
{
    /// <summary>The Legacy app's tables as data (GameUi.TableSpec).</summary>
    public static class DynastyColumns
    {
        public static readonly TableSpec Off = new TableSpec("Dynasty", "Off").Column("reallife_dynasty_col_name", 600f, TableSort.None, flexible: true);

        /// <summary>The will: 0 name, 1 who they are, 2 age, 3 what the draft leaves them.</summary>
        public static readonly TableSpec People = new TableSpec("Dynasty", "Will")
            .Column("reallife_dynasty_col_name", 420f, TableSort.Text, flexible: true)
            .Column("reallife_dynasty_col_kind", 260f, TableSort.Text)
            .Column("reallife_dynasty_col_age", 160f, TableSort.Number)
            .Column("reallife_dynasty_col_left", 240f, TableSort.Text, right: true)
            .Empty("reallife_dynasty_empty_people");

        /// <summary>The estate: 0 from and to, 1 day, 2 the estate, 3 the tax.</summary>
        public static readonly TableSpec Statements = new TableSpec("Dynasty", "Estate")
            .Column("reallife_dynasty_col_statement", 460f, TableSort.Text, flexible: true)
            .Column("reallife_dynasty_col_day", 160f, TableSort.Number)
            .Column("reallife_dynasty_col_estate", 260f, TableSort.Money)
            .Column("reallife_dynasty_col_tax", 240f, TableSort.Money, sums: true, right: true)
            .Empty("reallife_dynasty_statements_none");

        /// <summary>The family line: 0 name, 1 generation, 2 how they handed over, 3 the estate.</summary>
        public static readonly TableSpec Line = new TableSpec("Dynasty", "Line")
            .Column("reallife_dynasty_col_name", 420f, TableSort.Text, flexible: true)
            .Column("reallife_dynasty_col_generation", 200f, TableSort.Number)
            .Column("reallife_dynasty_col_how", 240f, TableSort.Text)
            .Column("reallife_dynasty_col_estate", 260f, TableSort.Money, right: true);
    }

    /// <summary>
    /// The Legacy app in the game's frame (GameUi.AppShell), for a player with the RLO interface off: Will, Estate and Family line tabs over a table, and the white box for what
    /// is picked, with the choices and the buttons. The same facts and the same director calls as the kit's page (through Ui/Ventures/DynastyLive). Nothing runs while it is closed.
    /// </summary>
    public sealed class DynastyApp
    {
        private static readonly string[] TabKeys = { "reallife_dynasty_tab_will", "reallife_dynasty_tab_estate", "reallife_dynasty_tab_line" };

        private readonly DynastyFacts _facts = new DynastyFacts();
        private readonly List<TableRow> _rows = new List<TableRow>(16);
        private readonly List<TableRow> _pool = new List<TableRow>(16);
        private AppShell _shell;
        private DataTable _table;
        private int _tab, _pick;
        private bool _writing;

        /// <summary>The app's panel, asleep; null if the game's parts are not as expected (each has said so in Player.log).</summary>
        public GameObject Build(FullMenu menu, Transform dormant)
        {
            using var piece = SessionWatch.Piece("dynasty: the app being built from the game's MyEmployees");
            _shell = AppShell.Make(menu, dormant, DynastyDirector.AppId);
            _table = DataTable.On(_shell, "Dynasty");
            if (_table == null || !_shell.SetTabCount(TabKeys.Length) || !_shell.SetColumns(DynastyColumns.Off))
            {
                return null;
            }
            _table.Money = amount => DynastyGame.Money((long)amount);
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

        /// <summary>Opens on a tab (a notice's click).</summary>
        public void Show(int tab)
        {
            _tab = Mathf.Clamp(tab, 0, TabKeys.Length - 1);
            _pick = 0;
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

        private TableRow Row(string id, bool pick)
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
            row.Pick = pick;
            row.Index = _rows.Count;
            _rows.Add(row);
            return row;
        }

        private static string T(string key, params string[] pairs) => DynastyText.Fill(key, pairs);

        private static string Percent(int mille) => (mille / 10).ToString() + "%";

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
                DynastyDirector d = DynastyDirector.Instance;
                bool ready = d != null && d.Facts(_facts, _tab == DynastyPages.TabEstate);
                TableSpec spec = DynastyColumns.Off;
                UiClone.Words(_shell.Header, T("reallife_dynasty_app_head", "estate", DynastyGame.Money(ready && _facts.Today != null ? _facts.Today.Net : 0),
                    "tax", DynastyGame.Money(ready && _facts.Today != null ? _facts.Today.Tax : 0)));
                if (!ready)
                {
                    box.Title(T("reallife_dynasty_off_title"));
                    box.Note(T("reallife_dynasty_off_body"));
                }
                else
                {
                    switch (_tab)
                    {
                        case 1: spec = Estate(box); break;
                        case 2: spec = Line(box); break;
                        default: spec = WillTab(box); break;
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

        private static string KindWord(KinPerson c)
        {
            return T(c.Kind == KinKind.Partner ? "reallife_dynasty_kind_partner" : c.Kind == KinKind.Child ? "reallife_dynasty_kind_child"
                : c.Family ? "reallife_dynasty_kind_relative" : "reallife_dynasty_kind_friend");
        }

        private TableSpec WillTab(InfoBox box)
        {
            DynastyFacts f = _facts;
            Will w = f.Draft;
            DynastyLive live = DynastyLive.Instance;
            for (int i = 0; i < f.People.Count; i++)
            {
                KinPerson c = f.People[i];
                Bequest b = w.Find(c.Kind, c.Id, c.Name);
                bool heir = DynastyWill.IsHeir(w, c.Kind, c.Id, c.Name);
                TableRow row = Row("p" + i, true);
                row.Set(0, c.Name);
                row.Set(1, KindWord(c));
                row.Set(2, c.Age > 0 ? c.Age.ToString() : string.Empty, c.Age);
                row.Set(3, heir ? T("reallife_dynasty_chip_heir") : b != null ? Percent(b.Share) : string.Empty, heir ? TableRow.Green : TableRow.Plain);
            }
            for (int i = 0; i < 3; i++)
            {
                Bequest b = w.Find(KinKind.Charity, i, null);
                TableRow row = Row("c" + i, true);
                row.Set(0, GivingText.Charity(i));
                row.Set(1, T("reallife_dynasty_kind_charity"));
                row.Set(2, string.Empty, 0);
                row.Set(3, b != null ? Percent(b.Share) : string.Empty);
            }
            int at = Mathf.Clamp(_pick, 0, _rows.Count - 1);
            bool can = !f.Handing;
            KinKind kind;
            int id;
            string name;
            if (at < f.People.Count)
            {
                KinPerson c = f.People[at];
                kind = c.Kind;
                id = c.Id;
                name = c.Name;
                bool heir = DynastyWill.IsHeir(w, kind, id, name);
                box.Title(name);
                if (DynastyRules.CanCarryOn(c))
                {
                    box.RowWords(T("reallife_dynasty_row_upbringing"), Percent(DynastyRules.Upbringing(c)));
                    if (can)
                    {
                        Transform heirRow = box.ButtonRow();
                        InfoBox.Button(heirRow, InfoBox.Additional, heir ? "reallife_dynasty_unname_heir" : "reallife_dynasty_name_heir",
                            () => { live.Heir(heir ? KinKind.None : kind, id, name); Refresh(); });
                    }
                }
                else
                {
                    box.Note(T("reallife_dynasty_cannot_carry", "least", DynastyCatalogue.HeirLeast.ToString(), "most", DynastyCatalogue.HeirMost.ToString()));
                }
                if (heir)
                {
                    box.Note(T("reallife_dynasty_heir_rest"));
                }
            }
            else
            {
                kind = KinKind.Charity;
                id = at - f.People.Count;
                name = GivingText.Charity(id);
                box.Title(name);
                box.Note(GivingText.CharityAbout(id));
            }
            if (f.Handing)
            {
                box.Line(T("reallife_dynasty_handing_note"), CellTone.Yellow);
            }
            if (can && !DynastyWill.IsHeir(w, kind, id, name))
            {
                Bequest b = w.Find(kind, id, name);
                int share = b != null ? b.Share : 0;
                int most = Math.Min(DynastyCatalogue.OthersMost, DynastyRules.ShareRoom(w, b)) / DynastyCatalogue.ShareStep;
                box.RowWords(T("reallife_dynasty_step_share"), Percent(share));
                box.Selector(most + 1, Math.Min(most, share / DynastyCatalogue.ShareStep), i => Percent(i * DynastyCatalogue.ShareStep),
                    i => { live.Share(kind, id, name, i - share / DynastyCatalogue.ShareStep); Refresh(); });
            }
            if (can)
            {
                int ages = (DynastyCatalogue.RetireMost - DynastyCatalogue.RetireLeast) / DynastyCatalogue.RetireStep;
                int ageAt = (w.RetireAge - DynastyCatalogue.RetireLeast) / DynastyCatalogue.RetireStep;
                box.RowWords(T("reallife_dynasty_step_age"), w.RetireAge.ToString());
                box.Selector(ages + 1, Mathf.Clamp(ageAt, 0, ages), i => (DynastyCatalogue.RetireLeast + i * DynastyCatalogue.RetireStep).ToString(),
                    i => { live.RetireAge(i - ageAt); Refresh(); });
                int keep = Mathf.Clamp(w.Keep, 0, DynastyCatalogue.KeepSteps.Length - 1);
                box.RowWords(T("reallife_dynasty_step_keep"), Percent(DynastyCatalogue.KeepSteps[keep]));
                box.Selector(DynastyCatalogue.KeepSteps.Length, keep, i => Percent(DynastyCatalogue.KeepSteps[i]), i => { live.Keep(i - keep); Refresh(); });
            }
            box.RowWords(T("reallife_dynasty_row_left"), T("reallife_dynasty_row_left_value", "share", Percent(w.Others()), "most", Percent(DynastyCatalogue.OthersMost)));
            box.RowWords(T("reallife_dynasty_row_firm"), string.IsNullOrEmpty(f.Firm) ? T("reallife_dynasty_notary") : f.Firm);
            box.RowWords(T("reallife_dynasty_row_fee"), f.OwnFirm ? T("reallife_dynasty_fee_own") : DynastyGame.Money(f.Fee));
            box.Note(T(f.Signed.Written ? "reallife_dynasty_law_note" : "reallife_dynasty_no_will"));
            if (can && f.Dirty)
            {
                Transform buttons = box.ButtonRow();
                InfoBox.Button(buttons, InfoBox.Positive, "reallife_dynasty_sign", () => { live.Sign(); Refresh(); });
                InfoBox.Button(buttons, InfoBox.Negative, "reallife_dynasty_discard", () => { live.Discard(); Refresh(); });
            }
            return DynastyColumns.People;
        }

        private TableSpec Estate(InfoBox box)
        {
            DynastyFacts f = _facts;
            for (int i = f.Statements.Count - 1; i >= 0; i--)
            {
                Statement p = f.Statements[i];
                TableRow row = Row("s" + i, false);
                row.Set(0, T("reallife_dynasty_statement_title", "from", p.From, "to", p.To));
                row.Set(1, p.Day.ToString(), p.Day);
                row.Set(2, DynastyGame.Money(p.Net), p.Net);
                row.Set(3, DynastyGame.Money(p.Tax), p.Tax);
            }
            Statement s = f.Today;
            box.Title(T("reallife_dynasty_sec_today"));
            if (f.Handing)
            {
                box.Line(T("reallife_dynasty_handing_count", "done", f.Step.ToString(), "steps", f.Steps.ToString()), CellTone.Yellow);
            }
            if (s != null)
            {
                box.RowWords(T("reallife_dynasty_row_cash"), DynastyGame.Money(s.Cash));
                if (s.Reserved > 0)
                {
                    box.RowWords(T("reallife_dynasty_row_reserved"), DynastyGame.Money(-s.Reserved));
                }
                box.RowWords(T("reallife_dynasty_row_other"), DynastyGame.Money(s.Other));
                box.RowWords(T("reallife_dynasty_row_net"), DynastyGame.Money(s.Net));
                if (s.UnderWater)
                {
                    box.Line(T("reallife_dynasty_under_water"), CellTone.Yellow);
                }
                box.RowWords(T("reallife_dynasty_row_gifts", "years", f.WindowYears.ToString()), DynastyGame.Money(s.Gifts));
                box.RowWords(T("reallife_dynasty_row_charity"), DynastyGame.Money(-s.Charity));
                box.RowWords(T("reallife_dynasty_row_spouse", "share", Percent(DynastyCatalogue.SpouseShare)), DynastyGame.Money(-s.Spouse));
                box.RowWords(T("reallife_dynasty_row_threshold", "amount", DynastyGame.Money(DynastyRules.At(DynastyCatalogue.Threshold, s.Level))), DynastyGame.Money(s.Taxable));
                box.RowWords(T("reallife_dynasty_row_tax"), DynastyGame.Money(s.Tax), s.Tax > 0 ? CellTone.Red : CellTone.Plain);
                if (s.Loan > 0)
                {
                    box.RowWords(T("reallife_dynasty_row_tax_owed"), DynastyGame.Money(s.Loan), CellTone.Red);
                }
                for (int i = 0; i < s.Lines.Count; i++)
                {
                    BequestLine l = s.Lines[i];
                    string who = l.Kind == KinKind.Charity ? GivingText.Charity(l.Id) : l.Name;
                    box.RowWords(T("reallife_dynasty_row_bequest", "name", who, "share", Percent(l.Share)),
                        l.Landing == Landing.None ? T("reallife_dynasty_bequest_lapsed") : DynastyGame.Money(l.Amount));
                }
                if (s.Scale < 1000 && s.Lines.Count > 0 && !s.UnderWater)
                {
                    box.Line(T("reallife_dynasty_scale_note", "share", Percent(s.Scale)), CellTone.Yellow);
                }
                box.RowWords(T("reallife_dynasty_row_residue", "name", string.IsNullOrEmpty(s.To) ? T("reallife_dynasty_fig_nobody") : s.To), DynastyGame.Money(s.Residue));
            }
            if (f.LoanOwed > 0)
            {
                box.RowWords(T("reallife_dynasty_row_owed"), DynastyGame.Money(f.LoanOwed), CellTone.Red);
                box.RowWords(T("reallife_dynasty_row_per_day"), DynastyGame.Money(f.LoanPerDay));
                box.Note(T("reallife_dynasty_loan_note", "rate", Percent(DynastyCatalogue.LoanRate), "days", DynastyCatalogue.LoanLateMost.ToString()));
            }
            box.RowWords(T("reallife_dynasty_row_age"), f.Age.ToString());
            box.RowWords(T("reallife_dynasty_row_retire_from"), f.Signed.Written ? f.Signed.RetireAge.ToString() : T("reallife_dynasty_no_will_short"));
            if (f.RetireToday != null && f.Signed.Written)
            {
                box.RowWords(T("reallife_dynasty_row_keep"), DynastyGame.Money(f.RetireToday.Keep));
                box.RowWords(T("reallife_dynasty_row_retire_tax"), DynastyGame.Money(f.RetireToday.Tax));
            }
            box.Line(T(DynastyPages.RetireKey(f.Retire)), f.Retire == RetireWhy.Ok ? CellTone.Plain : CellTone.Yellow);
            if (f.Retire == RetireWhy.Ok)
            {
                Transform buttons = box.ButtonRow();
                InfoBox.Button(buttons, InfoBox.Positive, "reallife_dynasty_retire", () => { DynastyLive.Instance.Retire(); Refresh(); });
            }
            return DynastyColumns.Statements;
        }

        private TableSpec Line(InfoBox box)
        {
            DynastyFacts f = _facts;
            TableRow now = Row("now", false);
            now.Set(0, f.Name);
            now.Set(1, f.Generation.ToString(), f.Generation);
            now.Set(2, T("reallife_dynasty_chip_head"), TableRow.Green);
            now.Set(3, DynastyGame.Money(f.Today != null ? f.Today.Net : 0), f.Today != null ? f.Today.Net : 0);
            for (int i = f.Line.Count - 1; i >= 0; i--)
            {
                LineEntry e = f.Line[i];
                TableRow row = Row("y" + i, false);
                row.Set(0, e.Name);
                row.Set(1, e.Generation.ToString(), e.Generation);
                row.Set(2, T(e.Mode == HandMode.Retire ? "reallife_dynasty_mode_retire" : e.Mode == HandMode.Elder ? "reallife_dynasty_mode_elder" : "reallife_dynasty_mode_death"));
                row.Set(3, DynastyGame.Money(e.Net), e.Net);
            }
            box.Title(T("reallife_dynasty_sec_line"));
            if (f.Elder != null)
            {
                box.RowWords(f.Elder.Name, T("reallife_dynasty_elder_age", "age", f.ElderAge.ToString()));
                box.RowWords(T("reallife_dynasty_row_fund"), DynastyGame.Money(f.Elder.Fund));
                box.RowWords(T("reallife_dynasty_row_draw"), DynastyGame.Money(f.Elder.Draw));
                box.Note(T("reallife_dynasty_elder_note", "age", DynastyCatalogue.LastAge.ToString(), "years", f.WindowYears.ToString()));
            }
            for (int i = 0; i < f.Kin.Count; i++)
            {
                box.RowWords(f.Kin[i].Name, DynastyGame.Money(f.Kin[i].Held));
            }
            if (f.Kin.Count > 0)
            {
                box.Note(T("reallife_dynasty_kin_note"));
            }
            if (f.RivalLines.Count == 0)
            {
                box.Note(T("reallife_dynasty_rivals_none"));
            }
            for (int i = 0; i < f.RivalLines.Count; i++)
            {
                box.Note(f.RivalLines[i]);
            }
            CityEstates c = f.City;
            box.RowWords(T("reallife_dynasty_row_city_now"), T("reallife_dynasty_row_city_value", "n", c.Count.ToString(), "amount", DynastyGame.Money(c.Value)));
            box.RowWords(T("reallife_dynasty_row_city_tax"), T("reallife_dynasty_row_city_tax_value", "amount", DynastyGame.Money(c.Tax), "n", c.Taxed.ToString()));
            box.RowWords(T("reallife_dynasty_row_city_unpaid"), DynastyGame.Money(c.Unpaid));
            box.RowWords(T("reallife_dynasty_row_city_last"), T("reallife_dynasty_row_city_value", "n", c.LastCount.ToString(), "amount", DynastyGame.Money(c.LastValue)));
            box.Note(T("reallife_dynasty_city_note"));
            return DynastyColumns.Line;
        }
    }
}
