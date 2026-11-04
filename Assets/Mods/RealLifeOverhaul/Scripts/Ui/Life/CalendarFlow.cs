using System;
using System.Collections.Generic;
using RealLifeOverhaul.Calendar;
using RealLifeOverhaul.Planner;
using RealLifeOverhaul.Seasons;

namespace RealLifeOverhaul.Ui
{
    /// <summary>What the Calendar's screen needs of the game: the day, the entries of a month, tomorrow's from the Planner, the weather, the phone's apps and the text box.</summary>
    public interface ICalendarHost
    {
        int Today { get; }
        int Hour { get; }
        int YearDays { get; }

        /// <summary>Fills the list with the calendar's entries of a month, in the calendar's order.</summary>
        void Look(int month, List<CalendarEntry> into);

        /// <summary>Fills the list with the Planner's arranged entries of one day.</summary>
        void Plan(int day, List<PlannerEntry> into);

        string Clock(int minuteOfDay);
        string Money(float amount);

        /// <summary>The weather's words key, the air's whole degrees Celsius and whether the game counts in imperial units; false when it has no weather to say.</summary>
        bool Weather(out string skyKey, out string skyEnglish, out int celsius, out bool imperial);

        string AppName(string appId);          // the phone's name for an app, or null when it is not there
        bool OpenApp(string appId);
        void OpenPlanner();
        void AskTitle(string initial, Action<string> done);
        void Notice(string key);
    }

    /// <summary>
    /// The Calendar on the kit, game-free: the month as a diary opened on today, the chosen day's entries with their count and
    /// how many are done (both from the one list the rows come from), today's weather in the game's unit, tomorrow from the
    /// Planner, Add an entry, the way back to today and each entry's app. CalendarScreen is its skin over the game. Cost: a look
    /// and a layout when the month, the chosen day or the hour changes.
    /// </summary>
    public sealed class CalendarFlow
    {
        private static readonly string[] MonthKeys =
        {
            "reallife_calendar_month_1", "reallife_calendar_month_2", "reallife_calendar_month_3", "reallife_calendar_month_4",
            "reallife_calendar_month_5", "reallife_calendar_month_6", "reallife_calendar_month_7", "reallife_calendar_month_8",
            "reallife_calendar_month_9", "reallife_calendar_month_10", "reallife_calendar_month_11", "reallife_calendar_month_12",
        };

        private static readonly string[] MonthWords =
        {
            "January", "February", "March", "April", "May", "June", "July", "August", "September", "October", "November", "December",
        };

        private readonly ICalendarHost _host;
        private readonly UiWords _t;
        private readonly List<CalendarEntry> _list = new List<CalendarEntry>(64);
        private readonly List<PlannerEntry> _plan = new List<PlannerEntry>(8);
        private readonly List<UiDiaryEntry> _entries = new List<UiDiaryEntry>(64);
        private readonly List<string> _apps = new List<string>(64);
        private int _month;
        private int _chosen;
        private int _builtOn = -1;     // the game day the last look was for
        private int _sig = int.MinValue;
        private bool _force = true;

        public CalendarFlow(ICalendarHost host, UiWords words)
        {
            _host = host;
            _t = words;
            ToToday();
        }

        public UiCalModel Model { get; } = new UiCalModel();

        /// <summary>The month shown, counted over the years from 0 (CalendarRules.MonthOf).</summary>
        public int Month => _month;

        /// <summary>The chosen day of the month shown, from 1.</summary>
        public int Chosen => _chosen;

        /// <summary>The chosen day's number in the game's days.</summary>
        public int ChosenDay => CalendarRules.MonthStart(_month, _host.YearDays) + _chosen - 1;

        public bool Dirty => _force || _sig != Signature();

        /// <summary>The count and the done count the chosen day's header shows, read from the rows' own list.</summary>
        public int DayCount { get; private set; }

        public int DayDone { get; private set; }

        private int Signature() => unchecked(_month * 1000 + _chosen * 17 + _host.Today * 31 + _host.Hour + PlannerStore.Notes.Count * 101);

        // Today's month, today chosen: how the Calendar opens and where the way back leads.
        private void ToToday()
        {
            int year = _host.YearDays, today = _host.Today;
            _month = CalendarRules.MonthOf(today, year);
            _chosen = MonthViewRules.ChosenDay(today, CalendarRules.MonthStart(_month, year), CalendarRules.MonthEnd(_month, year)) - CalendarRules.MonthStart(_month, year) + 1;
            _force = true;
        }

        public void Refresh() => _force = true;

        /// <summary>The app was opened: on today's month and day, or on the month asked for with its first day (or today in it).</summary>
        public void Opened(int month = -1)
        {
            ToToday();
            if (month >= 0)
            {
                int year = _host.YearDays;
                _month = MonthViewRules.Step(month, 0, CalendarRules.MonthOf(_host.Today, year));
                int first = CalendarRules.MonthStart(_month, year);
                _chosen = MonthViewRules.ChosenDay(_host.Today, first, CalendarRules.MonthEnd(_month, year)) - first + 1;
            }
        }

        // ---------------------------------------------------------------- what is shown

        public void Build()
        {
            _sig = Signature();
            _force = false;
            int today = _host.Today, year = _host.YearDays;
            if (_builtOn >= 0 && _builtOn != today && _month == CalendarRules.MonthOf(_builtOn, year) && ChosenDay == _builtOn)
            {
                ToToday();   // a new day while the page stood on the old today: it follows
            }
            _builtOn = today;
            int from = CalendarRules.MonthStart(_month, year), length = CalendarRules.MonthLength(_month, year);
            if (_chosen > length)
            {
                _chosen = length;
            }
            if (_chosen < 1)
            {
                _chosen = 1;
            }
            _list.Clear();
            _host.Look(_month, _list);
            _entries.Clear();
            _apps.Clear();
            int count = 0, done = 0;
            for (int i = 0; i < _list.Count; i++)
            {
                CalendarEntry e = _list[i];
                int day = e.Day - from + 1;
                if (day < 1 || day > length)
                {
                    continue;
                }
                string app = string.IsNullOrEmpty(e.App) ? null : _host.AppName(e.App);
                _entries.Add(new UiDiaryEntry
                {
                    Day = day, Icon = e.Group == CalendarGroup.Money ? UiIcon.Coin : e.Group == CalendarGroup.City ? UiIcon.Building : UiIcon.Calendar,
                    Title = string.IsNullOrEmpty(e.Title) ? _t("reallife_ui_life_a_plan", "A plan") : e.Title, Value = e.Value,
                    Action = app != null ? Fill("reallife_calendar_open_app", "app", app) : null,
                });
                _apps.Add(app != null ? e.App : null);
                if (day == _chosen)
                {
                    count++;
                    done += e.Done ? 1 : 0;
                }
            }
            DayCount = count;
            DayDone = done;
            string[] days =
            {
                _t("common_monday", "Mon"), _t("common_tuesday", "Tue"), _t("common_wednesday", "Wed"), _t("common_thursday", "Thu"),
                _t("common_friday", "Fri"), _t("common_saturday", "Sat"), _t("common_sunday", "Sun"),
            };
            int lead = MonthViewRules.Lead(from);
            int chosenDay = from + _chosen - 1;
            int todayMonth = CalendarRules.MonthOf(today, year);
            Model.Title = _t("reallife_calendar_app", "Calendar");
            Model.Add = _t("reallife_ui_life_add", "Add an entry");
            Model.Open = _t("reallife_ui_life_open_planner", "Open Planner");
            Model.AddOff = chosenDay < today;
            Model.Today = _month == todayMonth && chosenDay == today ? null : _t("reallife_ui_life_today", "Today");
            Model.Move = _t("reallife_ui_prompt_move", "Move");
            Model.Select = _t("reallife_ui_prompt_select", "Select");
            Model.Back = _t("reallife_ui_prompt_back", "Back");
            Model.Weather = chosenDay == today ? WeatherLine() : null;
            Model.Diary = new UiDiaryModel
            {
                Month = MonthName(_month, year) + ", " + Fill("reallife_ui_life_week", "week", CalendarRules.WeekNumber(from).ToString()),
                Weekdays = days, Lead = lead, Length = length,
                Today = today >= from && today < from + length ? today - from + 1 : 0,
                Chosen = _chosen, PastBefore = today >= from && today < from + length ? today - from + 1 : 0,
                Entries = _entries.ToArray(),
                ChosenTitle = count == 1 ? Fill("reallife_ui_life_day_line_one", "done", done.ToString())
                    : Fill("reallife_ui_life_day_line", "count", count.ToString(), "done", done.ToString()),
                ChosenEmpty = _t("reallife_ui_life_nothing_day", "Nothing is set for this day. What you plan shows here."),
                AheadTitle = chosenDay == today ? _t("reallife_ui_life_tomorrow", "Tomorrow") : _t("reallife_ui_life_day_after", "The next day"),
                More = _t("reallife_ui_life_more", "+{0} more"),
                CanBack = _month > 0, CanOn = MonthViewRules.Step(_month, 1, todayMonth) != _month,
                Ahead = Tomorrow(chosenDay, from, length, lead, days),
            };
        }

        // The day after the chosen one, from the Planner: the next month's first day on the last day of a month.
        private UiDiaryEntry[] Tomorrow(int chosenDay, int from, int length, int lead, string[] days)
        {
            _plan.Clear();
            _host.Plan(chosenDay + 1, _plan);
            var rows = new List<UiDiaryEntry>(_plan.Count);
            int dayOfMonth = _chosen < length ? _chosen + 1 : 1;
            string date = dayOfMonth + " " + days[(lead + _chosen) % 7];
            for (int i = 0; i < _plan.Count; i++)
            {
                PlannerEntry e = _plan[i];
                if (string.IsNullOrEmpty(e.Title) || e.State == PlannerState.Over)
                {
                    continue;
                }
                rows.Add(new UiDiaryEntry
                {
                    Day = _chosen + 1, Icon = PlannerFlow.IconOf(PlannerRules.GroupOf(e)), Title = e.Title, Date = date,
                    Value = e.Deadline && e.End >= 0 ? _t("reallife_planner_when_by", "By {time}").Replace("{time}", _host.Clock(e.End))
                        : e.Start >= 0 ? _host.Clock(e.Start) : _t("reallife_planner_when_all_day", "All day"),
                });
            }
            return rows.ToArray();
        }

        private string WeatherLine()
        {
            if (!_host.Weather(out string skyKey, out string skyEnglish, out int celsius, out bool imperial))
            {
                return null;
            }
            return Fill("reallife_ui_life_weather", "sky", _t(skyKey, skyEnglish), "temp", TemperatureRules.Text(celsius, imperial));
        }

        private string MonthName(int month, int year)
        {
            int inYear = CalendarRules.MonthInYear(month, year);
            int at = inYear >= 0 && inYear < MonthKeys.Length ? inYear : 0;
            return _t(MonthKeys[at], MonthWords[at]);
        }

        private string Fill(string key, params string[] pairs)
        {
            string text = _t(key, null) ?? key;
            for (int i = 0; i + 1 < pairs.Length; i += 2)
            {
                text = text.Replace("{" + pairs[i] + "}", pairs[i + 1] ?? string.Empty);
            }
            return text;
        }

        // ---------------------------------------------------------------- presses

        /// <summary>A target pressed; false when it does nothing now.</summary>
        public bool Press(int command, int arg)
        {
            int year = _host.YearDays;
            switch (command)
            {
                case UiDiary.DayCommand:
                {
                    if (arg < 1)
                    {
                        return false;
                    }
                    int length = CalendarRules.MonthLength(_month, year);
                    if (arg > length)
                    {
                        _month++;   // the next day was in the next month: its first day
                        _chosen = arg - length;
                    }
                    else
                    {
                        _chosen = arg;
                    }
                    _force = true;
                    return true;
                }
                case UiDiary.MonthCommand:
                {
                    int to = MonthViewRules.Step(_month, arg < 0 ? -1 : 1, CalendarRules.MonthOf(_host.Today, year));
                    if (to == _month)
                    {
                        return false;
                    }
                    _month = to;
                    int first = CalendarRules.MonthStart(_month, year);
                    _chosen = MonthViewRules.ChosenDay(_host.Today, first, CalendarRules.MonthEnd(_month, year)) - first + 1;
                    _force = true;
                    return true;
                }
                case LifePages.Add:
                    if (ChosenDay < _host.Today)
                    {
                        return false;   // the Planner's form takes no day gone by either
                    }
                    _host.AskTitle(string.Empty, text =>
                    {
                        string clean = PlannerStore.Clean(text);
                        if (clean.Length > 0 && PlannerStore.AddNote(clean, ChosenDay, 9 * 60) == null)
                        {
                            _host.Notice("reallife_planner_notes_full");
                        }
                        _force = true;
                    });
                    return true;
                case LifePages.Second:
                    _host.OpenPlanner();
                    return true;
                case LifePages.Today:
                    if (_month == CalendarRules.MonthOf(_host.Today, year) && ChosenDay == _host.Today)
                    {
                        return false;
                    }
                    ToToday();
                    return true;
                case UiDiary.EntryCommand:
                    return arg >= 0 && arg < _apps.Count && OpenApp(_apps[arg]);
                default:
                    return false;
            }
        }

        private bool OpenApp(string app)
        {
            if (string.IsNullOrEmpty(app))
            {
                return false;
            }
            if (!_host.OpenApp(app))
            {
                _host.Notice("reallife_calendar_app_gone");
                return false;
            }
            return true;
        }

        /// <summary>The tab keys turn the month.</summary>
        public void Tab(int step) => Press(UiDiary.MonthCommand, step < 0 ? -1 : 1);

        public bool Act(UiAction action)
        {
            if (action == UiAction.Alt)
            {
                return Press(LifePages.Add, 0);
            }
            if (action == UiAction.Option)
            {
                return Press(LifePages.Second, 0);
            }
            return false;
        }
    }
}
