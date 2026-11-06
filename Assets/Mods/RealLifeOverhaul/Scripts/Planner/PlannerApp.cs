using System.Collections.Generic;
using System.Globalization;
using BAModAPI;
using RealLifeOverhaul.GameUi;
using TMPro;
using UI.Smartphone;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.Planner
{
    /// <summary>The Planner's panel: Today and Upcoming as tables, Week as columns, filtered by group; read only when opened or changed.</summary>
    public sealed partial class PlannerApp
    {
        public const int UpcomingDays = 7;
        private const int TodayTab = 0, UpcomingTab = 1, WeekTab = 2;

        public static IModLogger Logger;

        private enum Page
        {
            Glance, Entry, Note, Settings
        }

        private static readonly string[] TabKeys = { "common_today", "reallife_planner_tab_upcoming", "reallife_planner_tab_week" };

        // Each tab's filter (a PlannerGroup, -1 all) for the game's run; the table's own goes back to all when its group is empty.
        private static readonly int[] Chosen = { -1, -1, -1 };

        private readonly PlannerDirector _director;
        private readonly List<PlannerRow> _rows = new List<PlannerRow>(32);
        private readonly List<PlannerRow> _shown = new List<PlannerRow>(40);   // in the table's order, with the day headings
        private readonly WeekGrid _grid = new WeekGrid();
        private TableView _view;
        private AppShell _shell;
        private PlannerTable _table;
        private PlannerWeek _week;
        private FullMenu _menu;
        private int _tab;
        private Page _page;
        private EntryId _selected;
        private int _glanceDay;   // the day the box shows at a glance
        private int _tabs;        // two if the Week tab could not be made
        private readonly int[] _tabCounted = { -1, -1, -1 };   // the count each tab's name says
        private string _tabLanguage;

        public PlannerApp(PlannerDirector director)
        {
            _director = director;
        }

        /// <summary>The app's panel, asleep; null if the game's parts are not as expected, as logged.</summary>
        public GameObject Build(FullMenu menu, Transform dormant)
        {
            _menu = menu;
            using var piece = SessionWatch.Piece("planner: the app being built from the game's MyEmployees");   // once a city: a few hundred objects in one frame
            _shell = AppShell.Make(menu, dormant, PlannerDirector.AppId);
            if (_shell == null || !PrepareRow(_shell.RowPrefab))
            {
                return null;
            }
            if (!_shell.SetColumns(Tables[0]))
            {
                return null;
            }
            // the Week tab copies the row before the table's own script goes on it
            _week = PlannerWeek.Make(_shell.RowPrefab, _shell.List);
            if (_week != null)
            {
                _week.DayClicked = day => UiClone.Safe(() => ShowDay(day));
                _week.EntryClicked = entry => UiClone.Safe(() => WeekEntry(entry));
            }
            PlannerCellView.SelectedColour = _shell.RowSelected;
            _table = _shell.List.gameObject.AddComponent<PlannerTable>();
            _table.scroller = _shell.Scroller;
            _table.prefab = _shell.RowPrefab.AddComponent<PlannerCellView>();
            _table.allowRowSelection = true;
            _table.onRowSelected.AddListener(row => UiClone.Safe(() => RowClicked(row)));
            _view = new TableView(_shell) { RowCells = 4 };
            _view.Changed = filter => UiClone.Safe(() => Reordered(filter));

            int tabs = _week != null && _shell.SetTabCount(TabKeys.Length) ? TabKeys.Length : 2;
            _tabs = tabs;
            for (int i = 0; i < tabs; i++)
            {
                int tab = i;
                UiClone.Key(_shell.TabLabels[i], TabKeys[i]);
                UiClone.Rewire(_shell.Tabs[i], () => ShowTab(tab));
            }
            if (_shell.TopButtons != null)
            {
                InfoBox.Button(_shell.TopButtons, InfoBox.Positive, "reallife_planner_new_note", () => EditNote(null, TimeHelper.CurrentDay));
                InfoBox.Button(_shell.TopButtons, InfoBox.Additional, "bizman_menu_settings", () => Show(Page.Settings));
            }
            PlannerText.WantIcons();
            return _shell.Root;
        }

        private const float WhenWidth = 300f, WhatWidth = 600f, WhereWidth = 560f, StatusWidth = 300f;

        // One per tab, so each keeps its own order and filter; the Week tab uses only its filter.
        private static readonly TableSpec[] Tables = { Table("Today"), Table("Upcoming"), Table("Week") };

        private static TableSpec Table(string tab)
        {
            return new TableSpec("Planner", tab)
                .Column("reallife_planner_col_when", WhenWidth, TableSort.Date)
                .Column("reallife_planner_col_what", WhatWidth, TableSort.Text, flexible: true)
                .Column("reallife_planner_col_where", WhereWidth, TableSort.Text)
                .Column("bizman_status", StatusWidth, TableSort.Text, right: true)
                .Filter("reallife_planner_filter_all", PlannerText.GroupKeyList)
                .Empty("reallife_tab_empty_planner");
        }

        // The game's employee row keeps four cells: name (when), task and icon (what), business (where), satisfaction (status).
        private static bool PrepareRow(GameObject row)
        {
            Transform t = row.transform;
            var layout = row.GetComponent<HorizontalLayoutGroup>();
            Transform when = t.Find("EmployeeName"), what = t.Find("TaskInfo"), where = t.Find("BusinessName"), status = t.Find("Satisfaction");
            if (layout == null || when == null || what == null || where == null || status == null || what.Find("Task") == null)
            {
                UiClone.Fallback("the game's employee row is not built as expected, so the Planner is not added");
                return false;
            }
            UiClone.KeepOnly(t, "Background", "RaycastTarget", "EmployeeName", "TaskInfo", "BusinessName", "Satisfaction");
            when.name = "When";
            what.name = "What";
            where.name = "Where";
            status.name = "Status";
            AppShell.RowLayout(layout);
            AppShell.Width(when, WhenWidth, false);
            AppShell.Width(what, WhatWidth, true);
            AppShell.Width(where, WhereWidth, false);
            AppShell.Width(status, StatusWidth, false);
            AppShell.OneLine(when.GetComponent<TMP_Text>());
            AppShell.OneLine(where.GetComponent<TMP_Text>());
            AppShell.OneLine(status.GetComponent<TMP_Text>());
            AppShell.OneLine(what.Find("Task").GetComponent<TMP_Text>());
            Transform holder = what.Find("WarningIcon");
            var picture = holder != null ? holder.GetComponentInChildren<Image>(true) : null;
            if (picture != null)
            {
                // the game's warning sign hangs low and to the left; an icon sits in the middle
                var rect = (RectTransform)picture.transform;
                rect.anchoredPosition = new Vector2(0f, 0f);
                rect.sizeDelta = new Vector2(48f, 48f);
                picture.preserveAspect = true;
                picture.raycastTarget = false;
            }
            return true;
        }

        public void Opened()
        {
            if (_page != Page.Note && !(_page == Page.Entry && _selected.Source != null))
            {
                _page = Page.Glance;
                _glanceDay = TimeHelper.CurrentDay;
            }
            Refresh();
            _director.Seen();
        }

        public void Closed()
        {
            if (_table != null && _page != Page.Note)
            {
                _selected = default(EntryId);
                _page = Page.Glance;
            }
        }

        /// <summary>Opens on one entry, for a reminder clicked.</summary>
        public void Select(EntryId id)
        {
            _selected = id;
            _page = Page.Entry;
            if (_shell != null && _shell.Root != null && _shell.Root.activeInHierarchy)
            {
                Refresh();
            }
        }

        private PlannerEntry Selected => _selected.Source != null ? _director.List.Find(_selected) : null;

        private void Refresh()
        {
            if (_shell == null || _table == null)
            {
                return;
            }
            float started = Time.realtimeSinceStartup;
            int today = TimeHelper.CurrentDay;
            _director.Look(today, today + UpcomingDays, true);
            PlannerEntry selected = Selected;
            if (_page == Page.Entry && selected == null)
            {
                _page = Page.Glance;   // done elsewhere, cancelled or hidden
            }
            if (selected != null && _page == Page.Entry)
            {
                _glanceDay = selected.Day;
                if (_tab != WeekTab || selected.Day >= today + WeekGrid.Days)
                {
                    _tab = selected.Day == today ? TodayTab : UpcomingTab;
                }
            }
            if (_glanceDay < today || _glanceDay > today + UpcomingDays)
            {
                _glanceDay = today;
            }
            Rows(today);
            Frame();
            ShowRows(today, _page == Page.Entry && selected != null ? RowId(selected.Id) : null);
            TabCounts(today);
            Headline(today);
            Write();
            if (Meta.Dev.Verbose)
            {
                Logger?.Info("Planner: opened. " + _director.LookSummary() + "; " + _rows.Count + " rows on tab " + _tab + "; "
                             + ((Time.realtimeSinceStartup - started) * 1000f).ToString("0.0", CultureInfo.InvariantCulture) + " ms; " + _shell.Box.Fit() + ".");
            }
        }

        private void ShowTab(int tab)
        {
            if (tab == _tab)
            {
                return;
            }
            _tab = tab;
            if (_page == Page.Entry)
            {
                _page = Page.Glance;
                _selected = default(EntryId);
            }
            int today = TimeHelper.CurrentDay;
            if (_page == Page.Glance && tab != WeekTab)
            {
                _glanceDay = today;
            }
            Rows(today);
            Frame();
            ShowRows(today, null);
            Write();
        }

        private void Frame()
        {
            _shell.ShowTab(_tab);
            bool week = _tab == WeekTab && _week != null;
            if (_shell.List.gameObject.activeSelf == week)
            {
                _shell.List.gameObject.SetActive(!week);
            }
            if (_week != null)
            {
                _week.Show(week);
            }
        }

        private static string RowId(EntryId id)
        {
            return id.Source + ":" + id.A + ":" + id.B;
        }

        private bool OnTab(PlannerEntry e, int today) => PlannerRules.OnTab(e, _tab, today);

        // "Today (3)": each tab's count under its own filter, written only when it changes
        private void TabCounts(int today)
        {
            for (int i = 0; i < _tabs && i < _tabCounted.Length; i++)
            {
                int count = PlannerRules.TabCount(_director.List.Items, i, today, Chosen[i]);
                if (count == _tabCounted[i] && _tabLanguage == LanguageNow)
                {
                    continue;
                }
                _tabCounted[i] = count;
                UiClone.Words(_shell.TabLabels[i], PlannerText.T("reallife_planner_tab_count", "tab", PlannerText.T(TabKeys[i]), "count", count.ToString()));
            }
            _tabLanguage = LanguageNow;
        }

        private static string LanguageNow => PlannerText.T("common_today");   // a language change writes the names again

        // in the director's time order; the day headings belong to the order and are put in by Fill
        private void Rows(int today)
        {
            _rows.Clear();
            List<PlannerEntry> items = _director.List.Items;
            for (int i = 0; i < items.Count; i++)
            {
                PlannerEntry e = items[i];
                if (!OnTab(e, today))
                {
                    continue;
                }
                string status = PlannerText.StatusKey(e);
                _rows.Add(new PlannerRow
                {
                    Id = RowId(e.Id),
                    Kind = RowKind.Entry,
                    Entry = e,
                    Day = e.Day,
                    When = PlannerText.When(e),
                    What = e.Title,
                    Where = e.Where,
                    Status = status != null ? PlannerText.T(status) : string.Empty,
                    Icon = PlannerText.Icon(e.Category),
                    Tone = e.Clash && e.State != PlannerState.Done || e.State == PlannerState.Missed ? 2 : e.State == PlannerState.Now || e.Next ? 1 : 0
                });
            }
            if (_rows.Count == 0)
            {
                _rows.Add(new PlannerRow { Id = "empty", Kind = RowKind.Info, What = PlannerText.T(_tab == TodayTab ? "reallife_planner_empty_today" : "reallife_planner_empty_upcoming") });
            }
        }

        // The rows in the order and filter of the table's headers and drop-down; on the Week tab they fill the columns.
        private void ShowRows(int today, string selectedId)
        {
            TableModel model = _view.Model;
            TableSpec spec = Tables[_tab];
            if (_view.CanFilter && spec.FilterCount > 0)
            {
                spec.Filters[0].Chosen = Chosen[_tab];   // whatever the last look left
            }
            _view.Begin(spec, _rows.Count);
            for (int r = 0; r < _rows.Count; r++)
            {
                PlannerRow row = _rows[r];
                PlannerEntry e = row.Entry;
                if (row.Kind != RowKind.Entry || e == null)
                {
                    model.Pin(r);
                    continue;
                }
                model.Set(r, 0, e.Day * 1440.0 + Mathf.Max(0, e.Start >= 0 ? e.Start : e.End), row.When);
                model.Set(r, 1, double.NaN, row.What);
                model.Set(r, 2, double.NaN, row.Where);
                model.Set(r, 3, double.NaN, row.Status);
                model.Kind(r, 0, (int)PlannerRules.GroupOf(e.Category));
            }
            _view.Apply();
            if (_tab == WeekTab && _week != null)
            {
                FillWeek(today);
                return;
            }
            Fill(today);
            _table.Show(_shown, selectedId);
        }

        // In time order each upcoming day has a heading; in any other order the day goes in the entry's Time cell.
        private void Fill(int today)
        {
            TableModel model = _view.Model;
            TableSpec spec = model.Spec;
            bool byTime = spec.OrderColumn <= 0;
            int headed = -1, entries = 0;
            _shown.Clear();
            for (int i = 0; i < model.Count; i++)
            {
                PlannerRow row = _rows[model.RowAt(i)];
                PlannerEntry e = row.Entry;
                if (row.Kind == RowKind.Entry && e != null)
                {
                    entries++;
                    if (_tab == UpcomingTab && byTime && e.Day != headed)
                    {
                        headed = e.Day;
                        string date = PlannerText.Date(e.Day);
                        _shown.Add(new PlannerRow
                        {
                            Id = "day:" + e.Day, Kind = RowKind.DayHeader, Day = e.Day,
                            What = e.Day == today + 1 ? PlannerText.T("reallife_planner_day_tomorrow", "date", date) : date
                        });
                    }
                    row.When = _tab == UpcomingTab && !byTime ? PlannerText.Date(e.Day) + ", " + PlannerText.When(e) : PlannerText.When(e);
                }
                _shown.Add(row);
            }
            if (entries == 0 && model.Rows > model.Pinned)
            {
                _shown.Add(new PlannerRow { Id = "empty", Kind = RowKind.Info, What = PlannerText.T(spec.EmptyKey) });
            }
        }

        private void FillWeek(int today)
        {
            TableModel model = _view.Model;
            _grid.Begin(today);
            for (int i = 0; i < model.Count; i++)
            {
                PlannerRow row = _rows[model.RowAt(i)];
                if (row.Kind == RowKind.Entry && row.Entry != null)
                {
                    _grid.Add(row.Entry);
                }
            }
            _grid.Finish();
            _week.Fill(_grid, today, _page == Page.Entry ? Selected : null, _page == Page.Glance ? _glanceDay : -1);
        }

        // the open entry stays open if still shown, else the first entry shown, else the day at a glance
        private void Reordered(bool filter)
        {
            int today = TimeHelper.CurrentDay;
            if (filter)
            {
                Chosen[_tab] = Tables[_tab].Filters[0].Chosen;
                TabCounts(today);
            }
            if (_tab == WeekTab && _week != null)
            {
                PlannerEntry open = _page == Page.Entry ? Selected : null;
                if (open != null && !Listed(open))
                {
                    _page = Page.Glance;
                    _selected = default(EntryId);
                    Write();
                }
                FillWeek(today);
                return;
            }
            Fill(today);
            PlannerEntry selected = _page == Page.Entry ? Selected : null;
            string id = selected != null ? RowId(selected.Id) : null;
            bool moved = false;
            if (id != null)
            {
                PlannerRow first = null;
                bool shown = false;
                for (int i = 0; i < _shown.Count && !shown; i++)
                {
                    shown = _shown[i].Id == id;
                    first = first == null && _shown[i].Kind == RowKind.Entry ? _shown[i] : first;
                }
                if (!shown)
                {
                    moved = true;
                    _page = first != null ? Page.Entry : Page.Glance;
                    _selected = first != null ? first.Entry.Id : default(EntryId);
                    id = first != null ? first.Id : null;
                }
            }
            _table.Show(_shown, id);
            _table.ScrollTo(id);
            if (moved)
            {
                Write();
            }
        }

        private bool Listed(PlannerEntry entry)
        {
            TableModel model = _view.Model;
            for (int i = 0; i < model.Count; i++)
            {
                if (_rows[model.RowAt(i)].Entry == entry)
                {
                    return true;
                }
            }
            return false;
        }

        // what is next, over the left column
        private void Headline(int today)
        {
            PlannerEntry next = null;
            List<PlannerEntry> items = _director.List.Items;
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i].Next)
                {
                    next = items[i];
                    break;
                }
            }
            string words = next == null ? PlannerText.T("reallife_planner_next_none")
                : PlannerText.T(next.State == PlannerState.Now ? "reallife_planner_next_now" : "reallife_planner_next_up", "title", next.Title,
                    "when", next.Day == today ? PlannerText.When(next) : PlannerText.Day(next.Day, today) + ", " + PlannerText.When(next));
            UiClone.Words(_shell.Header, words);
        }

        // an entry opens, a day's heading shows that day at a glance
        private void RowClicked(PlannerRow row)
        {
            if (row != null && row.Kind == RowKind.DayHeader)
            {
                _table.ClearSelection();
                ShowDay(row.Day);
                return;
            }
            if (row == null || row.Kind != RowKind.Entry || row.Entry == null)
            {
                _table.ClearSelection();
                return;
            }
            _selected = row.Entry.Id;
            Show(Page.Entry);
        }

        private void WeekEntry(PlannerEntry entry)
        {
            if (entry == null)
            {
                return;
            }
            _selected = entry.Id;
            _glanceDay = entry.Day;
            _page = Page.Entry;
            Write();
            FillWeek(TimeHelper.CurrentDay);
        }

        private void ShowDay(int day)
        {
            _glanceDay = day;
            _selected = default(EntryId);
            _page = Page.Glance;
            Write();
            if (_tab == WeekTab && _week != null)
            {
                FillWeek(TimeHelper.CurrentDay);
            }
        }

        private void Show(Page page)
        {
            _page = page;
            if (page != Page.Entry)
            {
                _selected = default(EntryId);
                _table.ClearSelection();
            }
            Write();
            if (_tab == WeekTab && _week != null)
            {
                FillWeek(TimeHelper.CurrentDay);
            }
        }
    }
}
