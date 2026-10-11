using System;
using System.Collections.Generic;
using BAModAPI;
using RealLifeOverhaul.GameUi;
using TMPro;
using UI.Smartphone;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// What the Services app shows, in the game's frame (GameUi.AppShell, copied from MyEmployees) as Finance and
    /// Company are, with Finance's table (FinanceTable, the game's own BaTable). Four tabs over the table on the
    /// right, and on the left the white box with the one thing picked:
    ///
    ///   Cleaning     every business on the round with what it needs, what it gets, its cleanliness and its next
    ///                visit, then the player's other shops to add; the box: the service's cost a week against one
    ///                cleaner per shop, the van, and for a business its visiting pattern
    ///   Crew         every cleaner the player employs; pick one to put them on the crew or take them off
    ///   Contracts    offers from other businesses, then the contracts that run
    ///   Sell         offers for businesses on the market, then every business with what it is worth; pick one for
    ///                its value line by line, an asking price, the market and a quick sale
    ///
    /// A list goes in the table, which scrolls; the white box, which does not, shows only the one thing picked, with a
    /// Back button to the tab's own page. Each tab's page is in its own file (ServicesApp.Cleaning.cs, ServicesApp.Sale.cs).
    ///
    /// THE COLUMNS of every tab are declared once, in <see cref="Columns"/>: their names, title keys and widths. A
    /// table that learns to sort is given its columns there and nowhere else.
    ///
    /// Everything on it is the game's. Nothing runs while the app is closed: the rows and the page are made when it
    /// opens, when a tab or a row is picked, and when the director says something has changed.
    /// </summary>
    public sealed partial class ServicesApp
    {
        /// <summary>The table's columns, tab by tab: the one place they are declared.</summary>
        private static class Columns
        {
            public static readonly string[] Names = { "ColumnA", "ColumnB", "ColumnC", "ColumnD" };
            public static readonly float[] Widths = { 620f, 500f, 300f, 300f };
            public const int Flexible = 1;   // the second column takes what is left
            public static readonly string[][] Keys =
            {
                new[] { "reallife_co_col_business", "reallife_svc_col_cleaning", "reallife_svc_col_cleanliness", "reallife_svc_col_next" },
                new[] { "reallife_co_col_name", "reallife_svc_col_base", "reallife_svc_col_today", "reallife_svc_col_crew" },
                new[] { "reallife_co_col_business", "reallife_co_col_district", "reallife_svc_col_fee", "reallife_co_col_state" },
                new[] { "reallife_co_col_business", "reallife_svc_col_buyer", "reallife_svc_col_worth", "reallife_co_col_state" }
            };
        }

        private static readonly string[] TabKeys = { "reallife_svc_tab_cleaning", "reallife_svc_tab_crew", "reallife_svc_tab_contracts", "reallife_svc_tab_sale" };

        public static IModLogger Logger;

        private readonly ServicesDirector _director;
        private readonly List<FinanceRow> _rows = new List<FinanceRow>(48);
        private readonly List<FinanceRow> _pool = new List<FinanceRow>(48);

        private AppShell _shell;
        private FinanceTable _table;
        private int _tab, _wanted = -1;
        private string _pick, _wantPick;   // what is picked: a row's id ("cli:...", "emp:...", "offer:...", "sell:...", "bid:...")
        private bool _ready;               // the director has read the game for this opening
        private bool _writing;             // a page is being written: a change it causes does not write it again

        public ServicesApp(ServicesDirector director)
        {
            _director = director;
        }

        // ---------------------------------------------------------------- building it from the game's parts

        /// <summary>The app's panel, asleep; null if the game's parts are not as expected (each has said so in Player.log).</summary>
        public GameObject Build(FullMenu menu, Transform dormant)
        {
            SessionWatch.Mark("services: the app being built from the game's MyEmployees");   // once per city: a few hundred objects copied in one frame
            _shell = AppShell.Make(menu, dormant, ServicesDirector.AppId);
            if (_shell == null || !PrepareRow(_shell.RowPrefab) || !_shell.SetTabCount(TabKeys.Length))
            {
                return null;
            }
            if (!_shell.SetColumns(Columns.Names, Columns.Keys[0], Columns.Widths, Columns.Flexible))
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

        // The game's employee row has a tick box and ten cells. Four are kept and renamed, as Finance, Company and the
        // Planner keep them: the name, the task (its warning sign hidden), the business and the satisfaction.
        private static bool PrepareRow(GameObject row)
        {
            Transform t = row.transform;
            var layout = row.GetComponent<HorizontalLayoutGroup>();
            Transform a = t.Find("EmployeeName"), b = t.Find("TaskInfo"), c = t.Find("BusinessName"), d = t.Find("Satisfaction");
            if (layout == null || a == null || b == null || c == null || d == null || b.Find("Task") == null)
            {
                UiClone.Fallback("the game's employee row is not built as expected, so the Services app is not added");
                return false;
            }
            UiClone.KeepOnly(t, "Background", "RaycastTarget", "EmployeeName", "TaskInfo", "BusinessName", "Satisfaction");
            a.name = "A";
            b.name = "B";
            c.name = "C";
            d.name = "D";
            AppShell.RowLayout(layout);
            AppShell.Width(a, Columns.Widths[0], Columns.Flexible == 0);
            AppShell.Width(b, Columns.Widths[1], Columns.Flexible == 1);
            AppShell.Width(c, Columns.Widths[2], Columns.Flexible == 2);
            AppShell.Width(d, Columns.Widths[3], Columns.Flexible == 3);
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

        private bool Showing => _shell != null && _shell.Root != null && _shell.Root.activeInHierarchy;

        /// <summary>The tab to show the next time the app opens, and what is picked on it (a notification was clicked, or another app's button).</summary>
        public void Want(int tab, string pick)
        {
            _wanted = Mathf.Clamp(tab, 0, TabKeys.Length - 1);
            _wantPick = pick;
            if (Showing)
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
                _pick = _wantPick;
                _wanted = -1;
                _wantPick = null;
                _step = -1;
            }
            _ready = _director.AppOpened();
            Refresh();
        }

        public void Closed()
        {
            _ready = false;
        }

        /// <summary>The director says something has changed: the page is written again if it is showing.</summary>
        public void Changed()
        {
            if (Showing && _ready && !_writing)
            {
                Refresh();
            }
        }

        private void ShowTab(int tab)
        {
            if (tab != _tab || _pick != null)
            {
                _tab = tab;
                _pick = null;
                _step = -1;
                Refresh();
            }
        }

        private void RowClicked(FinanceRow row)
        {
            if (row == null || !row.Pick)
            {
                _table.ClearSelection();
                return;
            }
            _pick = row.Id;
            _step = -1;
            Refresh();
        }

        private void Back()
        {
            _pick = null;
            _step = -1;
            _table.ClearSelection();
            Refresh();
        }

        // Everything the open tab shows, made again: asked for when the app opens, a tab or a row is picked, or something has changed.
        private void Refresh()
        {
            if (_shell == null || _table == null || _writing)
            {
                return;
            }
            _writing = true;
            try
            {
                float started = Time.realtimeSinceStartup;
                for (int i = 0; i < _rows.Count; i++)
                {
                    _pool.Add(_rows[i]);
                }
                _rows.Clear();
                _shell.ShowTab(_tab);
                _shell.SetColumns(Columns.Names, Columns.Keys[_tab], Columns.Widths, Columns.Flexible);
                InfoBox box = _shell.Box;
                box.Clear();
                string selected = null;
                ServicesState state = _ready ? _director.State : null;
                if (state == null || SaveGameManager.Current == null)
                {
                    UiClone.Words(_shell.Header, ServicesText.T("reallife_svc_app"));
                    box.Title(ServicesText.T("reallife_co_off_title"));
                    box.Line(ServicesText.T("reallife_svc_off_body"));
                }
                else
                {
                    int day = SaveGameManager.Current.Day;
                    UiClone.Words(_shell.Header, ServicesText.T("reallife_svc_headline", "crew", state.Crew.Count.ToString(), "businesses", state.Clients.Count.ToString(), "waiting", _director.Waiting().ToString()));
                    if (_tab == ServicesDirector.TabSale)
                    {
                        if (PartPage(_director.SaleOn, box))
                        {
                            WriteSale(box, state, day, ref selected);
                        }
                    }
                    else if (PartPage(_director.CleaningOn, box))
                    {
                        switch (_tab)
                        {
                            case ServicesDirector.TabCrew: WriteCrew(box, state, ref selected); break;
                            case ServicesDirector.TabContracts: WriteContracts(box, state, day, ref selected); break;
                            default: WriteCleaning(box, state, day, ref selected); break;
                        }
                    }
                }
                box.Done();
                _table.Show(_rows, selected);
                if (Meta.Dev.Verbose)
                {
                    Logger?.Info("Services: tab " + _tab + " written, " + _rows.Count + " row(s)" + (_pick != null ? ", picked " + _pick : string.Empty) + "; "
                                 + ((Time.realtimeSinceStartup - started) * 1000f).ToString("0.0") + " ms.");
                }
            }
            finally
            {
                _writing = false;
            }
        }

        // A tab whose part is switched off, or stopped by a fault, says so and shows nothing else.
        private bool PartPage(bool on, InfoBox box)
        {
            if (on)
            {
                return true;
            }
            box.Title(ServicesText.T(TabKeys[_tab]));
            box.Line(ServicesText.T("reallife_co_part_off"));
            Say("off", ServicesText.T("reallife_co_part_off_row"));
            return false;
        }

        // ---------------------------------------------------------------- rows

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

        // A row that can be picked. Returns true if it is the one picked now.
        private bool Pick(string id, string a, string b, string c, string d, int tone, ref string selected)
        {
            FinanceRow row = Row(id);
            row.Pick = true;
            row.A = a;
            row.B = b;
            row.C = c;
            row.D = d;
            row.Tone = tone;
            if (id == _pick)
            {
                selected = id;
                return true;
            }
            return false;
        }

        private void Say(string id, string words)
        {
            FinanceRow row = Row(id);
            row.Info = true;
            row.A = words;
        }

        // What follows a prefix in what is picked ("cli:" and a place); null if something else is picked.
        private string Picked(string prefix)
        {
            return _pick != null && _pick.StartsWith(prefix, StringComparison.Ordinal) ? _pick.Substring(prefix.Length) : null;
        }

        private void BackButton(InfoBox box)
        {
            InfoBox.Button(box.ButtonRow(), InfoBox.Additional, "reallife_co_back", Back);
        }
    }
}
