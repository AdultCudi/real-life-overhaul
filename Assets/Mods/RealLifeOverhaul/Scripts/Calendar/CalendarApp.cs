using System.Collections.Generic;
using System.Globalization;
using BAModAPI;
using RealLifeOverhaul.GameUi;
using UI.Smartphone;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.Calendar
{
    /// <summary>
    /// What the Calendar shows, in the game's frame (GameUi.AppShell). On the right two tabs: "Month" is the month's grid
    /// of weeks (CalendarGrid), "Day" a table of everything on the chosen day, each row with the app that deals with it.
    /// Over the left column the month's name and the day it ends on; on the left the white box, which is one of two
    /// pages (CalendarApp.Box.cs): the chosen day, with its money, the player's own time, the city, the month's money and
    /// what moves every night; or one entry of the day, with a button that opens its app. The buttons at the top turn the
    /// month back and on. The app does nothing while it is closed: it reads the sources when it opens, when a month is
    /// turned and when a new day begins while it is open (CalendarDirector).
    /// </summary>
    public sealed partial class CalendarApp
    {
        private const int MonthTab = 0, DayTab = 1;
        private const string GuideTitle = "reallife_meta_title_calendar";   // the Guide's page the box points to at its foot

        public static IModLogger Logger;

        private static readonly string[] TabKeys = { "reallife_calendar_tab_month", "reallife_calendar_tab_day" };
        private static readonly string[] GroupKeys = { "reallife_calendar_kind_money", "reallife_calendar_kind_time", "reallife_calendar_kind_city" };

        /// <summary>The Day tab's table: 0 when, 1 what, 2 the money, 3 the app; filtered by money, time and the city.</summary>
        private static readonly TableSpec DaySpec = new TableSpec("Calendar", "Day")
            .Column("reallife_calendar_col_when", 260f, TableSort.Date)
            .Column("reallife_calendar_col_what", 700f, TableSort.Text, flexible: true)
            .Column("reallife_calendar_col_money", 300f, TableSort.Money)
            .Column("reallife_calendar_col_app", 380f, TableSort.Text, right: true)
            .Filter("reallife_calendar_filter_all", GroupKeys)
            .Empty("reallife_calendar_day_none");

        private readonly CalendarDirector _director;
        private readonly List<TableRow> _rows = new List<TableRow>(24);
        private readonly List<TableRow> _pool = new List<TableRow>(24);

        private AppShell _shell;
        private DataTable _table;
        private CalendarGrid _grid;
        private Button _previous, _following;
        private int _tab;
        private int _month = -1;      // the month shown (CalendarRules.MonthOf); -1 before the first opening
        private int _chosen;          // the day chosen
        private int _writtenFor = -1; // the game's day the page was read for
        private int _wanted = -2;     // a month asked for before the app opened; -1 for today's, -2 for none
        private string _pick;         // the Day tab's row picked: an entry's id
        private bool _writing;

        public CalendarApp(CalendarDirector director)
        {
            _director = director;
        }

        private bool Showing => _shell != null && _shell.Root != null && _shell.Root.activeInHierarchy;

        // ---------------------------------------------------------------- building it from the game's parts

        /// <summary>The app's panel, asleep; null if the game's parts are not as expected (each has said so in Player.log).</summary>
        public GameObject Build(FullMenu menu, Transform dormant)
        {
            using var piece = SessionWatch.Piece("calendar: the app being built from the game's MyEmployees");   // once per city: a few hundred objects copied in one frame
            _shell = AppShell.Make(menu, dormant, CalendarDirector.AppId);
            if (_shell == null)
            {
                return null;
            }
            // The month's cells are copies of the table's row, made before the table's own script goes on it.
            _grid = CalendarGrid.Make(_shell.RowPrefab, _shell.List, _shell.RowSelected);
            _table = DataTable.On(_shell, "Calendar");
            if (_grid == null || _table == null || !_shell.SetColumns(DaySpec))
            {
                return null;
            }
            _grid.DayClicked = day => UiClone.Safe(() => Choose(day));
            _table.Money = amount => CalendarText.Money((float)amount);
            _table.SelectionLost = Back;
            _table.onRowSelected.AddListener(row => UiClone.Safe(() => RowClicked(row)));
            for (int i = 0; i < TabKeys.Length; i++)
            {
                int tab = i;
                UiClone.Key(_shell.TabLabels[i], TabKeys[i]);
                UiClone.Rewire(_shell.Tabs[i], () => ShowTab(tab));
            }
            if (_shell.TopButtons != null)
            {
                _previous = InfoBox.Button(_shell.TopButtons, InfoBox.Additional, "reallife_calendar_previous", () => Turn(-1));
                _following = InfoBox.Button(_shell.TopButtons, InfoBox.Additional, "reallife_calendar_next", () => Turn(1));
            }
            return _shell.Root;
        }

        // ---------------------------------------------------------------- opening, turning, choosing

        /// <summary>The month to show the next time the app opens: -1 for today's.</summary>
        public void Want(int month)
        {
            _wanted = month;
        }

        public void Opened()
        {
            if (_shell == null || _table == null)
            {
                return;
            }
            int today = CalendarGame.Today, year = GameCalendar.DaysPerYear;
            int todayMonth = CalendarRules.MonthOf(today, year);
            _month = _wanted >= 0 ? MonthViewRules.Step(_wanted, 0, todayMonth) : todayMonth;
            _wanted = -2;
            _chosen = MonthViewRules.ChosenDay(today, CalendarRules.MonthStart(_month, year), CalendarRules.MonthEnd(_month, year));
            _tab = MonthTab;
            _pick = null;
            Refresh();
        }

        public void Closed()
        {
            _pick = null;
        }

        /// <summary>
        /// Once a second while the app is open (CalendarDirector): on a new day the month is read again, and a page that
        /// showed today's month and day moves on to the new ones.
        /// </summary>
        public void DayCheck(int today)
        {
            if (!Showing || _writing || today == _writtenFor || _writtenFor < 0)
            {
                return;
            }
            int year = GameCalendar.DaysPerYear;
            if (_chosen == _writtenFor)
            {
                _month = CalendarRules.MonthOf(today, year);
                _chosen = today;
                _pick = null;
            }
            Refresh();
        }

        private void Turn(int steps)
        {
            int today = CalendarGame.Today, year = GameCalendar.DaysPerYear;
            int month = MonthViewRules.Step(_month, steps, CalendarRules.MonthOf(today, year));
            if (month == _month)
            {
                return;
            }
            _month = month;
            _chosen = MonthViewRules.ChosenDay(today, CalendarRules.MonthStart(month, year), CalendarRules.MonthEnd(month, year));
            _pick = null;
            Refresh();
        }

        private void ToToday()
        {
            int today = CalendarGame.Today;
            _month = CalendarRules.MonthOf(today, GameCalendar.DaysPerYear);
            _chosen = today;
            _pick = null;
            Refresh();
        }

        private void Choose(int day)
        {
            if (day <= 0)
            {
                return;
            }
            _chosen = day;
            _pick = null;
            Write();
        }

        private void ShowTab(int tab)
        {
            if (tab == _tab)
            {
                return;
            }
            _tab = tab;
            _pick = null;
            Write();
        }

        private void RowClicked(TableRow row)
        {
            if (row == null || !row.Pick)
            {
                _table.ClearSelection();
                return;
            }
            if (row.Id == _pick)
            {
                return;   // the kit selects the same row again after a sort or a filter
            }
            _pick = row.Id;
            WriteBox(CalendarGame.Today);
        }

        private void Back()
        {
            if (_pick == null)
            {
                return;
            }
            _pick = null;
            _table.ClearSelection();
            WriteBox(CalendarGame.Today);
        }

        // ---------------------------------------------------------------- writing

        // The month read from the sources, then written.
        private void Refresh()
        {
            if (_shell == null || _table == null || _writing || _month < 0)
            {
                return;
            }
            _director.Look(_month);
            _writtenFor = CalendarGame.Today;
            Write();
        }

        private void Write()
        {
            if (_shell == null || _table == null || _writing)
            {
                return;
            }
            _writing = true;
            try
            {
                float started = Time.realtimeSinceStartup;
                int today = CalendarGame.Today, year = GameCalendar.DaysPerYear;
                int first = CalendarRules.MonthStart(_month, year), last = CalendarRules.MonthEnd(_month, year);
                if (_chosen < first || _chosen > last)
                {
                    _chosen = MonthViewRules.ChosenDay(today, first, last);
                }
                Headline(today, first, last, year);
                Buttons(today, year);
                _shell.ShowTab(_tab);
                UiClone.Words(_shell.TabLabels[DayTab], CalendarText.T("reallife_calendar_tab_day_n", "day", _chosen.ToString()));
                bool month = _tab == MonthTab;
                if (_shell.List.gameObject.activeSelf == month)
                {
                    _shell.List.gameObject.SetActive(!month);
                }
                _grid.Show(month);
                if (month)
                {
                    _grid.Fill(first, last - first + 1, today, _chosen, _director.List.Items);
                }
                else
                {
                    Rows();
                    _table.Show(DaySpec, _rows, _pick);
                }
                WriteBox(today);
                if (Meta.Dev.Verbose)
                {
                    Logger?.Info("Calendar: " + (month ? "the month" : "day " + _chosen) + " written, days " + first + " to " + last + ", " + _director.List.Items.Count + " entries; "
                                 + ((Time.realtimeSinceStartup - started) * 1000f).ToString("0.0", CultureInfo.InvariantCulture) + " ms; " + _shell.Box.Fit() + ".");
                }
            }
            finally
            {
                _writing = false;
            }
        }

        // "March, year 2. This month ends on day 90.": the month shown, and the day it ends on.
        private void Headline(int today, int first, int last, int year)
        {
            string name = CalendarText.Month(CalendarRules.MonthInYear(_month, year));
            string yearNumber = CalendarRules.YearOfMonth(_month, year).ToString();
            bool now = today >= first && today <= last;
            UiClone.Words(_shell.Header, now
                ? CalendarText.T("reallife_calendar_headline_now", "month", name, "year", yearNumber, "day", last.ToString())
                : CalendarText.T("reallife_calendar_headline", "month", name, "year", yearNumber, "day", last.ToString()));
        }

        // The month can be turned back to the save's first and on to a year from today's.
        private void Buttons(int today, int year)
        {
            int todayMonth = CalendarRules.MonthOf(today, year);
            if (_previous != null)
            {
                _previous.interactable = MonthViewRules.Step(_month, -1, todayMonth) != _month;
            }
            if (_following != null)
            {
                _following.interactable = MonthViewRules.Step(_month, 1, todayMonth) != _month;
            }
        }

        // The Day tab's rows: every entry of the chosen day, in the calendar's order.
        private void Rows()
        {
            for (int i = 0; i < _rows.Count; i++)
            {
                _pool.Add(_rows[i]);
            }
            _rows.Clear();
            List<CalendarEntry> items = _director.List.Items;
            for (int i = 0; i < items.Count; i++)
            {
                CalendarEntry e = items[i];
                if (e.Day != _chosen)
                {
                    continue;
                }
                TableRow row = Row(RowId(e));
                row.Pick = true;
                row.Kind = (int)e.Group;
                row.Set(0, When(e), e.Day * 1440.0 + (e.Start >= 0 ? e.Start : -1));
                row.Set(1, e.Title);
                if (e.Amount > 0f && e.Sign != 0)
                {
                    row.Set(2, Amount(e), e.Sign * (double)e.Amount, e.Sign > 0 ? CellTone.Green : CellTone.Plain);
                }
                else
                {
                    row.Set(2, e.Group == CalendarGroup.Money ? e.Value : string.Empty, double.NaN);
                }
                row.Set(3, AppName(e.App) ?? string.Empty);
            }
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
            _rows.Add(row.Reset(id));
            return row;
        }

        private static string RowId(CalendarEntry e)
        {
            return e.Source + ":" + e.A + ":" + e.B + ":" + e.Day;
        }

        // The entry a row stands for, from the last look; null if it is no longer there.
        private CalendarEntry Picked()
        {
            if (_pick == null)
            {
                return null;
            }
            List<CalendarEntry> items = _director.List.Items;
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i].Day == _chosen && RowId(items[i]) == _pick)
                {
                    return items[i];
                }
            }
            return null;
        }

        // "09:00", or "All day" for what has no hour.
        private static string When(CalendarEntry e)
        {
            return e.Start >= 0 ? CalendarText.Clock(e.Start) : CalendarText.T("reallife_calendar_all_day");
        }

        // "+$1,200" for money in, "$340" for money out.
        private static string Amount(CalendarEntry e)
        {
            return (e.Sign > 0 ? "+" : string.Empty) + CalendarText.Money(e.Amount);
        }

        // The name of an app as it stands on the phone; null for none, or one that is not on the phone now.
        private static string AppName(string app)
        {
            return string.IsNullOrEmpty(app) ? null : PhoneApp.TitleOf(app);
        }
    }
}
