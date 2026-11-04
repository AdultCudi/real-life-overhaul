using System;
using System.Collections.Generic;
using RealLifeOverhaul.Planner;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// What the Planner's screen needs of the game: the clock, the entries the director found, the words the game writes times
    /// and days in, the text box and the question box, the guider, and the way to a place. The screen gives the real one; the
    /// checks give a recording one, so each press can be followed to the state it changes.
    /// </summary>
    public interface IPlannerHost
    {
        int Today { get; }
        int Minute { get; }                 // minutes since midnight now
        int NowMinutes { get; }             // game minutes, as PlannerTime counts them
        bool Reminders { get; }             // the option for reminders is on

        /// <summary>Fills the list with the arranged entries of the days; the hidden kinds are already left out.</summary>
        void Look(int fromDay, int toDay, List<PlannerEntry> into);

        string Clock(int minuteOfDay);      // "09:00" or "9:00 AM" as the game writes it
        string Weekday(int day);            // "Monday"
        string DayWord(int day, int today); // "Today", "Tomorrow", "Monday (Day 12)"
        string Span(int minutes);           // "1 h 30 min"
        string Money(float amount);

        void AskTitle(string initial, Action<string> done);
        void ConfirmDelete(Action yes);     // the game's question box
        void Locate(PlannerEntry entry);    // the guider's destination, the menu put away
        bool MetresTo(PlannerEntry entry, out float metres);
        bool ByCar { get; }
        bool HerePlace(out string street, out int number, out string name);   // the building the player is in, if it has an address
        void Notice(string key);            // an error's words as a notification
        void Looked();                      // the player has seen the list: the badge goes
    }

    /// <summary>
    /// The Planner on the kit, game-free: its tabs, its group filter, the board with every hour entries stand in, Jump to now, the
    /// day's box, an entry's page, the note form and the settings, and what each press does to the notes and the settings kept in
    /// PlannerStore. PlannerScreen is its skin over the game. Every press tests the page it belongs to before it acts, and its own
    /// numbers (LifePages, UiBoard, UiPlanDay, UiPlanEntry, UiPlanForm) never meet another's. Cost: a look and a layout when the
    /// day, the tab, the page or a note changes.
    /// </summary>
    public sealed class PlannerFlow
    {
        public const int Tabs = 4, TabToday = 0, TabUpcoming = 1, TabWeek = 2, TabNotes = 3;
        public const int DaysAhead = 59;           // days a note can be written for, from today
        private const int Groups = 6;
        private const float Row = 48f;

        private static readonly string[] Presets =
        {
            "reallife_planner_preset_shopping", "reallife_planner_preset_call", "reallife_planner_preset_meet", "reallife_planner_preset_bill",
            "reallife_planner_preset_gym", "reallife_planner_preset_collect", "reallife_planner_preset_clean", "reallife_planner_preset_rest"
        };

        private static readonly string[] PresetWords = { "Go shopping", "Call someone", "Meet someone", "Pay a bill", "Go to the gym", "Collect something", "Clean up at home", "Take a day off" };

        // a new note's reminder: any of these
        private static readonly ReminderLead[] NoteLeads =
        {
            ReminderLead.None, ReminderLead.QuarterBefore, ReminderLead.HalfHourBefore, ReminderLead.HourBefore, ReminderLead.AtTheTime, ReminderLead.EveningBefore
        };

        private static readonly string[] RepeatKeys = { "reallife_planner_repeat_once", "reallife_planner_repeat_daily", "reallife_ui_life_repeat_weekly" };
        private static readonly string[] RepeatWords = { "Once", "Every day", "Every week" };

        // what each switch of the settings shows and hides
        private static readonly PlannerCategory[][] Kinds =
        {
            new[] { PlannerCategory.Work },
            new[] { PlannerCategory.Court, PlannerCategory.Licence },
            new[] { PlannerCategory.Family, PlannerCategory.Social },
            new[] { PlannerCategory.Travel },
            new[] { PlannerCategory.City },
            new[] { PlannerCategory.Business, PlannerCategory.Money },
            new[] { PlannerCategory.Health },
            new[] { PlannerCategory.Note }
        };

        private static readonly string[] KindKeys =
        {
            "reallife_planner_show_work", "reallife_planner_show_court", "reallife_planner_show_people", "reallife_planner_show_travel",
            "reallife_planner_show_city", "reallife_planner_show_business", "reallife_planner_show_health", "reallife_planner_show_notes"
        };

        private static readonly string[] KindWords =
        {
            "Work shifts", "Court, police and licences", "Family and friends", "Travel and hotels", "City events and elections", "Business and taxes", "Health", "My notes"
        };

        private static readonly string[] CategoryKeys =
        {
            "reallife_planner_cat_work", "reallife_planner_cat_court", "reallife_planner_cat_licence", "reallife_planner_cat_family",
            "reallife_planner_cat_social", "reallife_planner_cat_travel", "reallife_planner_cat_city", "reallife_planner_cat_business",
            "reallife_planner_cat_health", "reallife_planner_cat_money", "reallife_planner_cat_note"
        };

        private readonly IPlannerHost _host;
        private readonly UiWords _t;
        private readonly List<PlannerEntry> _items = new List<PlannerEntry>(32);
        private readonly List<PlannerEntry> _rows = new List<PlannerEntry>(32);
        private readonly List<PlannerEntry> _shiftEntries = new List<PlannerEntry>(32);
        private readonly List<int> _laneGroups = new List<int>(8);
        private readonly List<int> _laterDays = new List<int>(4);
        private readonly List<PlannerEntry> _next = new List<PlannerEntry>(5);
        private readonly List<int> _actions = new List<int>(2);
        private readonly List<PlannerNote> _noteRows = new List<PlannerNote>(16);
        private readonly int[] _freeFrom = new int[PlannerRules.MostGaps], _freeTo = new int[PlannerRules.MostGaps];
        private int[] _entryActions = new int[4];

        private int _tab, _chip, _page, _fromPage;
        private int _dayShown;
        private EntryId _selId;
        private int _selDay;
        private bool _force = true;
        private int _sig = int.MinValue;
        private bool _settings;

        // the note being written
        private int _draftId, _draftDay, _draftStart, _draftNumber;
        private string _draftTitle, _draftPlace, _draftStreet;
        private ReminderLead _draftLead;
        private PlannerRepeat _draftRepeat;

        // Jump to now: the entry it chose, and whether the board must reach the hour now stands in
        private EntryId _chosen;
        private bool _hasChosen, _showNow;

        public PlannerFlow(IPlannerHost host, UiWords words)
        {
            _host = host;
            _t = words;
        }

        /// <summary>What the screen lays out: the page's view model, refilled by <see cref="Build"/>.</summary>
        public UiPlanModel Model { get; } = new UiPlanModel();

        public int CurrentTab => _tab;

        public int Chip => _chip;

        public int Page => _page;

        /// <summary>The entry shown on its page, or an id of none.</summary>
        public EntryId Selected => _selId;

        public int DayShown => _dayShown;

        /// <summary>The entries the last Build looked at, for the checks.</summary>
        public List<PlannerEntry> Items => _items;

        public bool Dirty => _force || _sig != Signature();

        /// <summary>The note being written, as the form holds it.</summary>
        public string DraftTitle => _draftTitle;

        public string DraftPlace => _draftPlace;

        public int DraftDay => _draftDay;

        public int DraftStart => _draftStart;

        public PlannerRepeat DraftRepeat => _draftRepeat;

        public ReminderLead DraftLead => _draftLead;

        public bool Settings => _settings;

        public int Signature()
        {
            int minute = _tab == TabToday && _page == UiPlanPageKind.List ? _host.Minute / 10 : 0;
            return unchecked((((_tab * 31 + _chip) * 31 + _page) * 31 + _host.Today) * 31 + (_host.Minute / 60) * 17 + minute * 7 + PlannerStore.Notes.Count * 101 + PlannerStore.HiddenCategories);
        }

        /// <summary>Asks for the next layout to read everything again (a note was changed from outside).</summary>
        public void Refresh() => _force = true;

        // ---------------------------------------------------------------- what is shown

        public void Build()
        {
            _sig = Signature();
            _force = false;
            int today = _host.Today;
            Model.Title = _t("reallife_planner_app", "Planner");
            Model.Tab = _tab;
            Model.Chip = _chip;
            Model.Week = _t("reallife_ui_life_week", "Week {week}").Replace("{week}", CalendarRules.WeekNumber(today).ToString());
            Model.Nav = Nav();
            Model.Chips = _tab == TabNotes && _page == UiPlanPageKind.List ? new string[0] : Chips();   // the filter reads the day's entries: notes have none to filter
            Model.LocateWord = _t("reallife_ui_life_locate", "Locate");
            Model.Move = _t("reallife_ui_prompt_move", "Move");
            Model.Select = _t("reallife_ui_prompt_select", "Select");
            Model.Back = _t("reallife_ui_prompt_back", "Back");
            Model.Tabs = _t("reallife_ui_prompt_page", "Page");
            Model.Side = null;
            Model.Board = null;
            Model.Entry = null;
            Model.Form = null;
            Model.CanSettings = true;
            Model.Page = _page;
            switch (_page)
            {
                case UiPlanPageKind.Entry:
                    if (!BuildEntry())
                    {
                        _page = _fromPage;
                        BuildList(today);
                    }
                    break;
                case UiPlanPageKind.Form:
                    BuildForm();
                    break;
                case UiPlanPageKind.Day:
                    BuildDay(today);
                    break;
                default:
                    BuildList(today);
                    break;
            }
            Model.Page = _page;
            _host.Looked();
        }

        private void BuildList(int today)
        {
            Model.Page = _page = UiPlanPageKind.List;
            int from = _tab == TabUpcoming ? today + 1 : today;
            int to = _tab == TabUpcoming ? today + 14 : _tab == TabWeek || _tab == TabToday ? today + 6 : today;
            _items.Clear();
            if (_tab != TabNotes)
            {
                _host.Look(from, to, _items);
            }
            _rows.Clear();
            int group = GroupOfChip(_chip);
            for (int i = 0; i < _items.Count; i++)
            {
                PlannerEntry e = _items[i];
                if (_tab == TabNotes || !PlannerRules.OnTab(e, _tab == TabToday ? PlannerRules.TabToday : _tab, today))
                {
                    continue;
                }
                if (group >= 0 && (int)PlannerRules.GroupOf(e) != group)
                {
                    continue;
                }
                _rows.Add(e);
            }
            if (_tab == TabNotes)
            {
                _rows.Clear();
                _noteRows.Clear();
                for (int i = 0; i < PlannerStore.Notes.Count; i++)
                {
                    PlannerNote note = PlannerStore.Notes[i];
                    int day = note.Repeat == PlannerRepeat.Once ? note.Day : Math.Max(0, PlannerStore.NextTime(note, today));
                    if (day <= 0)
                    {
                        day = note.Day;
                    }
                    PlannerStore.Falls(note, day, out bool done);
                    var e = new PlannerEntry
                    {
                        Title = note.Title, Day = day, Start = note.Start, Own = true, Done = done, Category = PlannerCategory.Note,
                        Street = note.Street, Number = note.Number, HasPlace = note.HasPlace,
                    };
                    e.Id = PlannerStore.NoteEntry(note, day);
                    _rows.Add(e);
                }
            }
            FillList(today);
        }

        private void FillList(int today)
        {
            Model.Add = _tab == TabToday || _tab == TabWeek ? null : _t("reallife_ui_life_add", "Add an entry");
            Model.EmptyTitle = _t(_tab == TabNotes ? "reallife_ui_life_empty_notes_title" : _tab == TabUpcoming ? "reallife_ui_life_empty_upcoming_title" : "reallife_ui_life_empty_today_title",
                _tab == TabNotes ? "No notes yet" : _tab == TabUpcoming ? "Nothing coming up" : "Nothing on today");
            Model.EmptyBody = _t(_tab == TabNotes ? "reallife_ui_life_empty_notes_body" : _tab == TabUpcoming ? "reallife_ui_life_empty_upcoming_body" : "reallife_ui_life_empty_today_body",
                _tab == TabNotes ? "Add a note and it stays on the day you pick." : _tab == TabUpcoming ? "Later shifts, bills and classes show here."
                    : "Shifts, classes, bills and notes land here as the day fills.");
            if (_tab == TabToday || _tab == TabWeek)
            {
                Model.Board = Board(today);
                Model.RowCount = 0;
                Model.Side = _tab == TabToday ? DayBox(today, today, true) : null;
                return;
            }
            Model.Rows = Rows(today);
            Model.RowCount = _rows.Count;
        }

        private UiPlanRow[] Rows(int today)
        {
            var rows = new UiPlanRow[_rows.Count];
            for (int i = 0; i < _rows.Count; i++)
            {
                PlannerEntry e = _rows[i];
                rows[i] = new UiPlanRow
                {
                    Icon = e.Done ? UiIcon.Check : IconOf(PlannerRules.GroupOf(e)),
                    Title = string.IsNullOrEmpty(e.Title) ? _t("reallife_ui_life_a_business", "A business") : e.Title,
                    Meta = Meta(e, today),
                    Locate = e.HasPlace || !string.IsNullOrEmpty(e.Street),
                    Done = e.Done,
                };
            }
            return rows;
        }

        private string Meta(PlannerEntry e, int today)
        {
            string time = e.Start >= 0 || (e.Deadline && e.End >= 0) ? When(e) : _t("reallife_ui_life_all_day", "All day");
            if (_page == UiPlanPageKind.Day || e.Day == today && _tab != TabUpcoming && _tab != TabNotes)
            {
                return time;
            }
            return _host.DayWord(e.Day, today) + (e.Start >= 0 || (e.Deadline && e.End >= 0) ? " " + time : string.Empty);
        }

        private string When(PlannerEntry e)
        {
            if (e.Deadline && e.End >= 0)
            {
                return _t("reallife_planner_when_by", "By {time}").Replace("{time}", _host.Clock(e.End));
            }
            return e.Start >= 0 ? _host.Clock(e.Start) : _t("reallife_planner_when_all_day", "All day");
        }

        // ---------------------------------------------------------------- the board

        /// <summary>The span of a bar on the board in minutes since midnight: from its start to its end, an entry with only a last moment ending there; false for one with no hour.</summary>
        public static bool Span(PlannerEntry e, out int start, out int end)
        {
            start = end = 0;
            if (e.Start >= 0)
            {
                start = e.Start;
                end = e.End >= 0 ? (e.End < e.Start ? PlannerTime.MinutesPerDay : e.End) : e.Start + 60;
                if (end <= start)
                {
                    end = start + PlannerRules.FreeLeast;   // a moment with no length: a bar of the least
                }
            }
            else if (e.Deadline && e.End >= 0)
            {
                end = e.End;
                start = Math.Max(0, end - PlannerRules.FreeLeast);
                if (end <= start)
                {
                    end = start + PlannerRules.FreeLeast;
                }
            }
            else
            {
                return false;
            }
            end = Math.Min(end, PlannerTime.MinutesPerDay);
            start = Math.Min(start, end - 1);
            return true;
        }

        /// <summary>The first and last hour a board shows: 07:00 to 23:00, wider to hold every hour an entry stands in, never past the day.</summary>
        public static void Hours(List<PlannerEntry> rows, int today, bool week, int nowHour, out int first, out int last)
        {
            first = PlannerRules.WakeHour;
            last = PlannerRules.SleepHour;
            for (int i = 0; i < rows.Count; i++)
            {
                PlannerEntry e = rows[i];
                if (week && (e.Day < today || e.Day >= today + 7))
                {
                    continue;
                }
                if (Span(e, out int s, out int f))
                {
                    first = Math.Min(first, s / 60);
                    last = Math.Max(last, (f + 59) / 60);
                }
            }
            if (nowHour >= 0)
            {
                first = Math.Min(first, nowHour);
                last = Math.Max(last, nowHour + 1);
            }
            first = Math.Max(0, first);
            last = Math.Min(24, Math.Max(last, first + 1));
        }

        private UiBoardModel Board(int today)
        {
            bool week = _tab == TabWeek;
            Hours(_rows, today, week, _showNow && !week ? _host.Minute / 60 : -1, out int first, out int last);
            var hours = new string[last - first];
            for (int i = 0; i < hours.Length; i++)
            {
                hours[i] = _host.Clock((first + i) * 60);
            }
            var lanes = new List<UiBoardLane>(8);
            var index = new int[(int)PlannerGroup.Count];
            for (int g = 0; g < index.Length; g++)
            {
                index[g] = -1;
            }
            _laneGroups.Clear();
            if (week)
            {
                for (int d = 0; d < 7; d++)
                {
                    lanes.Add(new UiBoardLane { Name = _host.Weekday(today + d), Icon = UiIcon.Calendar, Tint = UiRole.Chart1 });
                }
            }
            else
            {
                for (int i = 0; i < _rows.Count; i++)
                {
                    if (!Span(_rows[i], out _, out _))
                    {
                        continue;
                    }
                    int g = (int)PlannerRules.GroupOf(_rows[i]);
                    if (g >= 0 && g < index.Length && index[g] < 0)
                    {
                        index[g] = lanes.Count;
                        _laneGroups.Add(g);
                        lanes.Add(new UiBoardLane { Name = GroupName(g), Icon = IconOf((PlannerGroup)g), Tint = TintOf(g) });
                    }
                }
            }
            _shiftEntries.Clear();
            var shifts = new List<UiBoardShift>(_rows.Count);
            int chosen = -1, chosenLane = 0;
            for (int i = 0; i < _rows.Count; i++)
            {
                PlannerEntry e = _rows[i];
                if (!Span(e, out int start, out int end))
                {
                    continue;
                }
                int lane = week ? e.Day - today : index[(int)PlannerRules.GroupOf(e)];
                if (lane < 0 || lane >= lanes.Count)
                {
                    continue;
                }
                if (_hasChosen && e.Id.Equals(_chosen) && !week)
                {
                    chosen = shifts.Count;
                    chosenLane = lane;
                }
                _shiftEntries.Add(e);
                shifts.Add(new UiBoardShift
                {
                    Lane = lane, Start = start, End = end, Name = string.IsNullOrEmpty(e.Title) ? _t("reallife_ui_life_a_business", "A business") : e.Title,
                    Warn = e.Clash || e.State == PlannerState.Missed, Span = Fill("reallife_planner_glance_free_span", "from", _host.Clock(start), "to", _host.Clock(end)),
                });
            }
            _actions.Clear();
            var actions = new List<UiActionModel>(2);
            if (_tab == TabToday)
            {
                _actions.Add(ActionJump);
                actions.Add(new UiActionModel { Label = _t("reallife_ui_life_jump", "Jump to now"), Kind = UiButtonKind.Secondary, Key = UiAction.Option, Disabled = JumpTarget(today) == null });
            }
            _actions.Add(ActionAdd);
            actions.Add(new UiActionModel { Label = _t("reallife_ui_life_add", "Add an entry"), Kind = UiButtonKind.Primary, Key = UiAction.Alt });
            return new UiBoardModel
            {
                FirstHour = first, LastHour = last, Hours = hours, Now = _tab == TabToday ? _host.Minute : -1,
                Lanes = lanes.ToArray(), Shifts = shifts.ToArray(), Actions = actions.ToArray(), Chosen = chosen,
                Offset = chosen >= 0 ? chosenLane * UiBoard.LaneHeight : 0f,
                EmptyTitle = Model.EmptyTitle, EmptyBody = Model.EmptyBody,
            };
        }

        private const int ActionJump = 1, ActionAdd = 2;

        // The entry Jump to now goes to: what is on now at a set hour, else the next one still to come today.
        private PlannerEntry JumpTarget(int today)
        {
            PlannerEntry next = null;
            List<PlannerEntry> pool = _tab == TabToday && _chip == 0 ? _rows : _items;
            for (int i = 0; i < pool.Count; i++)
            {
                PlannerEntry e = pool[i];
                if (e.Day != today || !Span(e, out int start, out int end) || e.Done)
                {
                    continue;
                }
                if (e.State == PlannerState.Now)
                {
                    return e;
                }
                if (e.State == PlannerState.Upcoming && (next == null || start < NextStart(next)))
                {
                    next = e;
                }
            }
            return next;
        }

        private static int NextStart(PlannerEntry e)
        {
            Span(e, out int start, out _);
            return start;
        }

        /// <summary>
        /// Jump to now: the entry on now or next today is chosen and its lane brought into view, the board's hours reaching the hour now
        /// stands in; false when nothing is left today.
        /// </summary>
        public bool Jump()
        {
            if (_page != UiPlanPageKind.List || _tab != TabToday)
            {
                return false;
            }
            _chip = 0;
            Build();
            PlannerEntry e = JumpTarget(_host.Today);
            if (e == null)
            {
                return false;
            }
            _chosen = e.Id;
            _hasChosen = true;
            _showNow = true;
            _force = true;
            return true;
        }

        private void ClearJump()
        {
            _hasChosen = false;
            _showNow = false;
        }

        // ---------------------------------------------------------------- the day's box

        private UiPlanDayModel DayBox(int day, int today, bool later)
        {
            var m = new UiPlanDayModel
            {
                NextHead = _t("reallife_ui_life_next_head", "Up next"),
                NextNone = _t("reallife_ui_life_next_none", "Nothing more at a set hour"),
                FreeHead = _t("reallife_ui_life_free_head", "Free time"),
                FreeNone = _t("reallife_ui_life_free_none", "No free time left"),
                AllDayHead = _t("reallife_ui_life_all_day_head", "All day"),
                TotalsHead = _t("reallife_ui_life_totals_head", "The day"),
                LocateWord = _t("reallife_ui_life_locate", "Locate"),
            };
            _next.Clear();
            int coming = 0;
            int nowMinute = day == today ? _host.Minute : 0;
            for (int i = 0; i < _items.Count; i++)
            {
                PlannerEntry e = _items[i];
                if (e.Day != day || e.AllDay || e.Done || e.State == PlannerState.Missed || e.State == PlannerState.Over)
                {
                    continue;
                }
                coming++;
                if (_next.Count < UiPlanDay.MostNext)
                {
                    _next.Add(e);
                }
            }
            var next = new UiPlanNext[_next.Count];
            for (int i = 0; i < next.Length; i++)
            {
                PlannerEntry e = _next[i];
                PlannerGroup g = PlannerRules.GroupOf(e);
                next[i] = new UiPlanNext
                {
                    Time = When(e), Icon = IconOf(g), Tint = UiRole.None, Title = string.IsNullOrEmpty(e.Title) ? _t("reallife_ui_life_a_business", "A business") : e.Title,
                    Sub = e.Where, Locate = e.HasPlace || !string.IsNullOrEmpty(e.Street), Warn = e.Clash,
                };
            }
            m.Next = next;
            if (coming > next.Length)
            {
                m.NextMore = Fill(day == today ? "reallife_ui_life_next_more" : "reallife_ui_life_next_more_day", "count", (coming - next.Length).ToString());
            }
            int clashes = PlannerLogic.ClashPairs(_items, day, PlannerRules.DrivePerMinute);
            if (clashes > 0)
            {
                m.ClashLine = clashes == 1 ? _t("reallife_ui_life_clash_one", "1 clash") : Fill("reallife_ui_life_clash_many", "count", clashes.ToString());
            }
            int gaps = PlannerRules.FreeTime(_items, day, nowMinute, _freeFrom, _freeTo, out _);
            m.Free = new UiPlanSpan[gaps];
            for (int g = 0; g < gaps; g++)
            {
                m.Free[g] = new UiPlanSpan
                {
                    Span = Fill("reallife_planner_glance_free_span", "from", _host.Clock(_freeFrom[g]), "to", _host.Clock(_freeTo[g])),
                    Length = _host.Span(_freeTo[g] - _freeFrom[g]),
                };
            }
            _laterDays.Clear();
            if (later)
            {
                m.LaterHead = _t("reallife_ui_life_later_head", "Later this week");
                var rows = new List<UiPlanLater>(3);
                for (int d = day + 1; d <= day + 6 && rows.Count < UiPlanDay.MostLater; d++)
                {
                    string words = null;
                    int count = 0;
                    for (int i = 0; i < _items.Count; i++)
                    {
                        if (_items[i].Day != d || string.IsNullOrEmpty(_items[i].Title) || _items[i].State == PlannerState.Over)
                        {
                            continue;
                        }
                        if (count++ < 2)
                        {
                            words = words == null ? _items[i].Title : words + ", " + _items[i].Title;
                        }
                    }
                    if (count > 0)
                    {
                        _laterDays.Add(d);
                        rows.Add(new UiPlanLater { Day = _host.Weekday(d) + " " + d, Title = words });
                    }
                }
                m.Later = rows.ToArray();
            }
            var allDay = new List<string>(3);
            int toDo = 0, done = 0, missed = 0;
            for (int i = 0; i < _items.Count; i++)
            {
                PlannerEntry e = _items[i];
                if (e.Day != day)
                {
                    continue;
                }
                if (e.AllDay && !string.IsNullOrEmpty(e.Title) && allDay.Count < UiPlanDay.MostAllDay)
                {
                    allDay.Add(e.Title);
                }
                if (e.State == PlannerState.Done)
                {
                    done++;
                }
                else if (e.State == PlannerState.Missed)
                {
                    missed++;
                }
                else if (e.State != PlannerState.Over)
                {
                    toDo++;
                }
            }
            m.AllDay = allDay.ToArray();
            var totals = new List<UiPairModel>(4)
            {
                new UiPairModel { Key = _t("reallife_ui_life_count_todo", "To do"), Value = toDo.ToString() },
            };
            if (done > 0)
            {
                totals.Add(new UiPairModel { Key = _t("reallife_ui_life_count_done", "Done"), Value = done.ToString() });
            }
            if (missed > 0)
            {
                totals.Add(new UiPairModel { Key = _t("reallife_ui_life_count_missed", "Missed"), Value = missed.ToString() });
            }
            float owed = PlannerRules.OwedOn(_items, day, day);
            if (owed > 0f)
            {
                totals.Add(new UiPairModel { Key = _t("reallife_ui_life_money_due", "Money due"), Value = _host.Money(owed) });
            }
            m.Totals = totals.ToArray();
            return m;
        }

        // ---------------------------------------------------------------- a day of the week

        private void BuildDay(int today)
        {
            _items.Clear();
            _host.Look(_dayShown, _dayShown, _items);
            _rows.Clear();
            for (int i = 0; i < _items.Count; i++)
            {
                if (_items[i].Day == _dayShown)
                {
                    _rows.Add(_items[i]);
                }
            }
            Model.DayTitle = _host.DayWord(_dayShown, today);
            Model.Add = _t("reallife_ui_life_add", "Add an entry");
            Model.EmptyTitle = _t("reallife_ui_life_empty_today_title", "Nothing on today");
            Model.EmptyBody = _t("reallife_ui_life_empty_today_body", "Shifts, classes, bills and notes land here as the day fills.");
            Model.Rows = Rows(today);
            Model.RowCount = _rows.Count;
            Model.Board = null;
            Model.CanSettings = false;
            Model.Side = DayBox(_dayShown, today, false);
        }

        // ---------------------------------------------------------------- an entry's page

        private bool BuildEntry()
        {
            _items.Clear();
            _host.Look(_selDay, _selDay, _items);
            PlannerEntry e = null;
            for (int i = 0; i < _items.Count; i++)
            {
                if (_items[i].Id.Equals(_selId))
                {
                    e = _items[i];
                    break;
                }
            }
            if (e == null)
            {
                return false;
            }
            int today = _host.Today;
            PlannerGroup group = PlannerRules.GroupOf(e);
            var m = new UiPlanEntryModel
            {
                Title = string.IsNullOrEmpty(e.Title) ? _t("reallife_ui_life_a_business", "A business") : e.Title,
                Icon = IconOf(group), Tint = UiRole.None,
                FactsHead = _t("reallife_ibox_sec_when_where", "When and where"),
                WhatHead = _t("reallife_ibox_sec_what", "What it is"),
                WayHead = _t("reallife_ibox_sec_getting_there", "Getting there"),
            };
            string status = StatusKey(e, out string english);
            if (status != null)
            {
                m.Status = _t(status, english);
                m.StatusTone = e.State == PlannerState.Missed ? UiRole.Danger : e.State == PlannerState.Done || e.State == PlannerState.Now ? UiRole.Ok : e.Clash ? UiRole.Warn : UiRole.None;
            }
            PlannerEntry other = e.Clash && e.State != PlannerState.Done ? e.ClashWith : null;
            if (other != null)
            {
                string with = other.Title + ", " + (other.Day == e.Day ? When(other) : _host.DayWord(other.Day, today) + ", " + When(other));
                m.Clash = Fill(e.ClashReach ? "reallife_planner_detail_clash_reach" : "reallife_planner_detail_clash", "title", with);
            }
            var facts = new List<UiPlanFact>(4)
            {
                new UiPlanFact { Label = _t("reallife_ui_life_d_when", "When"), Value = WhenLong(e, today) },
            };
            if (!string.IsNullOrEmpty(e.Where))
            {
                facts.Add(new UiPlanFact { Label = _t("reallife_ui_life_d_where", "Where"), Value = e.Where });
            }
            int kind = (int)e.Category;
            facts.Add(new UiPlanFact { Label = _t("reallife_ui_life_d_kind", "Kind"), Value = _t(CategoryKeys[kind >= 0 && kind < CategoryKeys.Length ? kind : CategoryKeys.Length - 1], "Note") });
            m.Facts = facts.ToArray();
            var lines = new List<string>(3);
            if (!string.IsNullOrEmpty(e.What))
            {
                lines.Add(e.What);
            }
            if (!string.IsNullOrEmpty(e.Money))
            {
                lines.Add(e.Money);
            }
            if (!string.IsNullOrEmpty(e.IfMissed))
            {
                m.WarnLine = lines.Count;
                lines.Add(e.IfMissed);
            }
            m.Lines = lines.ToArray();
            Way(m, e, today);
            Reminder(m, e);
            Actions(m, e);
            Model.Entry = m;
            Model.CanSettings = false;
            return true;
        }

        private static string StatusKey(PlannerEntry e, out string english)
        {
            english = null;
            if (e.Clash && e.State != PlannerState.Done)
            {
                english = "Clash";
                return "reallife_planner_state_clash";
            }
            switch (e.State)
            {
                case PlannerState.Now: english = "Now"; return "reallife_planner_state_now";
                case PlannerState.Done: english = "Done"; return "reallife_planner_state_done";
                case PlannerState.Missed: english = "Missed"; return "reallife_planner_state_missed";
                case PlannerState.Over: english = "Over"; return "reallife_planner_state_over";
                default:
                    english = "Next";
                    return e.Next ? "reallife_planner_state_next" : null;
            }
        }

        private string WhenLong(PlannerEntry e, int today)
        {
            string day = _host.DayWord(e.Day, today);
            if (e.Deadline && e.End >= 0)
            {
                return Fill("reallife_planner_when_long_by", "day", day, "time", _host.Clock(e.End));
            }
            if (e.Start < 0)
            {
                return Fill("reallife_planner_when_long_all_day", "day", day);
            }
            if (e.End >= 0 && e.End != e.Start)
            {
                return Fill("reallife_planner_when_long_from_to", "day", day, "time", _host.Clock(e.Start)).Replace("{end}", _host.Clock(e.End));
            }
            return Fill("reallife_planner_when_long_at", "day", day, "time", _host.Clock(e.Start));
        }

        // The way to a place from where the player is: when to leave, how long by car and on foot.
        private void Way(UiPlanEntryModel m, PlannerEntry e, int today)
        {
            if (e.State != PlannerState.Upcoming || e.AllDay || !_host.MetresTo(e, out float metres))
            {
                return;
            }
            if (!(metres > PlannerRules.HereMetres))
            {
                m.Way = new[] { new UiPlanFact { Label = _t("reallife_ui_life_d_way", "Way"), Value = _t("reallife_planner_way_here", "You are there."), Ink = UiRole.OkText } };
                return;
            }
            bool car = _host.ByCar;
            int drive = PlannerRules.TravelMinutes(metres, PlannerRules.DrivePerMinute), walk = PlannerRules.TravelMinutes(metres, PlannerRules.WalkPerMinute);
            int leave = PlannerRules.LeaveBy(e, car ? drive : walk);
            int now = _host.NowMinutes;
            m.Way = new[]
            {
                new UiPlanFact
                {
                    Label = _t("reallife_ui_life_d_leave", "Leave by"),
                    Value = leave <= now ? _t("reallife_planner_way_now", "now") : At(leave, e.Day, today),
                    Ink = leave <= now ? UiRole.DangerText : UiRole.None,
                },
            };
            if (leave < now)
            {
                m.WayLate = Fill("reallife_planner_way_late", "late", _host.Span(now - leave));
            }
            m.WayNote = Fill(car ? "reallife_planner_way_about_car" : "reallife_planner_way_about_foot", "drive", _host.Span(drive), "walk", _host.Span(walk));
        }

        // the hour, with the day too when it is not 'day'
        private string At(int minutes, int day, int today)
        {
            int on = PlannerTime.DayOf(minutes);
            return on == day ? _host.Clock(PlannerTime.OfDay(minutes)) : _host.DayWord(on, today) + ", " + _host.Clock(PlannerTime.OfDay(minutes));
        }

        // The reminder: for a repeating note it is kept for every time, but "when to leave" for this time only.
        private void Reminder(UiPlanEntryModel m, PlannerEntry e)
        {
            bool open = e.State == PlannerState.Upcoming || e.State == PlannerState.Now;
            if (!open || !_host.Reminders)
            {
                return;
            }
            ReminderLead set = CurrentLead(e, out int count);
            int at = Math.Min(LeadIndexOf(set), count - 1);
            m.ReminderLabel = _t("reallife_planner_reminder_head", "Reminder");
            m.ReminderValue = LeadWords(Leads[at]);
            m.ReminderLess = at > 0;
            m.ReminderMore = at < count - 1;
        }

        // the reminders an entry's page offers, in order; "when to leave", last, only for one at a place
        private static readonly ReminderLead[] Leads =
        {
            ReminderLead.None, ReminderLead.QuarterBefore, ReminderLead.HalfHourBefore, ReminderLead.HourBefore,
            ReminderLead.AtTheTime, ReminderLead.EveningBefore, ReminderLead.LeaveBy
        };

        private static int LeadIndexOf(ReminderLead lead)
        {
            int index = Array.IndexOf(Leads, lead);
            return index < 0 ? 0 : index;
        }

        private ReminderLead CurrentLead(PlannerEntry e, out int count)
        {
            PlannerNote series = e.Own ? PlannerStore.Note(e.Id.A) : null;
            if (series != null && series.Repeat == PlannerRepeat.Once)
            {
                series = null;
            }
            bool leave = !e.AllDay && _host.MetresTo(e, out float metres) && metres > PlannerRules.HereMetres;
            ReminderLead set = PlannerStore.LeadOf(e.Id);
            if (series != null && set != ReminderLead.LeaveBy)
            {
                set = series.Lead;
            }
            leave |= set == ReminderLead.LeaveBy;   // one set while the place was known stays shown
            count = leave ? Leads.Length : Leads.Length - 1;
            return set;
        }

        private string LeadWords(ReminderLead lead)
        {
            switch (lead)
            {
                case ReminderLead.AtTheTime: return _t("reallife_planner_lead_at", "At the time");
                case ReminderLead.QuarterBefore: return _t("reallife_planner_lead_quarter", "15 minutes before");
                case ReminderLead.HalfHourBefore: return _t("reallife_planner_lead_half", "30 minutes before");
                case ReminderLead.HourBefore: return _t("reallife_planner_lead_hour", "1 hour before");
                case ReminderLead.EveningBefore: return _t("reallife_planner_lead_evening", "The evening before");
                case ReminderLead.LeaveBy: return _t("reallife_planner_lead_leave", "When to leave");
                default: return _t("reallife_planner_lead_none", "No reminder");
            }
        }

        private const int EntryLocate = 1, EntryDone = 2, EntryEdit = 3, EntryDelete = 4;

        private void Actions(UiPlanEntryModel m, PlannerEntry e)
        {
            var actions = new List<UiActionModel>(4);
            if (_entryActions.Length < 4)
            {
                _entryActions = new int[4];
            }
            int n = 0;
            if (e.HasPlace || !string.IsNullOrEmpty(e.Street))
            {
                _entryActions[n++] = EntryLocate;
                actions.Add(new UiActionModel { Label = _t("common_setdestination", "Set destination"), Kind = UiButtonKind.Primary, Icon = UiIcon.Locate, Key = UiAction.Alt });
            }
            if (e.Own)
            {
                _entryActions[n++] = EntryDone;
                actions.Add(new UiActionModel
                {
                    Label = e.Done ? _t("reallife_planner_note_not_done", "Mark as not done") : _t("reallife_planner_note_done", "Mark as done"),
                    Kind = n == 1 ? UiButtonKind.Primary : UiButtonKind.Secondary, Icon = UiIcon.Check, Key = n == 1 ? UiAction.Alt : UiAction.Count,
                });
                _entryActions[n++] = EntryEdit;
                actions.Add(new UiActionModel { Label = _t("reallife_planner_note_edit", "Edit note"), Kind = UiButtonKind.Secondary, Icon = UiIcon.Gear, Key = UiAction.Count });
                _entryActions[n++] = EntryDelete;
                actions.Add(new UiActionModel { Label = _t("filebrowser_delete", "Delete"), Kind = UiButtonKind.Destructive, Icon = UiIcon.Trash, Key = UiAction.Count });
            }
            for (int i = n; i < _entryActions.Length; i++)
            {
                _entryActions[i] = 0;
            }
            m.Actions = actions.ToArray();
        }

        // ---------------------------------------------------------------- the note form and the settings

        private void BuildForm()
        {
            Model.CanSettings = false;
            if (_settings)
            {
                Model.Form = SettingsForm();
                return;
            }
            int today = _host.Today;
            bool fresh = _draftId == 0;
            var presets = new string[Presets.Length];
            int chosen = -1;
            for (int i = 0; i < presets.Length; i++)
            {
                presets[i] = _t(Presets[i], PresetWords[i]);
                if (presets[i] == _draftTitle)
                {
                    chosen = i;
                }
            }
            var fields = new List<UiPlanField>(8)
            {
                new UiPlanField { Kind = UiPlanFieldKind.Text, Label = _t("reallife_ui_life_f_title", "Title"), Value = _draftTitle, Hint = _t("reallife_planner_note_placeholder", "What is it?") },
                new UiPlanField { Kind = UiPlanFieldKind.Choices, Label = _t("reallife_ui_life_f_ready", "Ready titles"), Options = presets, Chosen = chosen },
                new UiPlanField
                {
                    Kind = UiPlanFieldKind.Selector, Label = _t("reallife_ui_life_f_day", "Day"), Value = _host.DayWord(_draftDay, today),
                    CanLess = _draftDay > today, CanMore = _draftDay < today + DaysAhead,
                },
                new UiPlanField
                {
                    Kind = UiPlanFieldKind.Selector, Label = _t("reallife_ui_life_f_time", "Time"), Value = _draftStart < 0 ? _t("reallife_planner_when_all_day", "All day") : _host.Clock(_draftStart),
                    CanLess = _draftStart >= 0, CanMore = _draftStart < 23 * 60,
                },
                new UiPlanField
                {
                    Kind = UiPlanFieldKind.Choices, Label = _t("reallife_ui_life_f_repeat", "Repeat"),
                    Options = new[] { _t(RepeatKeys[0], RepeatWords[0]), _t(RepeatKeys[1], RepeatWords[1]), Fill(RepeatKeys[2], "day", _host.Weekday(_draftDay)) }, Chosen = (int)_draftRepeat,
                },
                new UiPlanField
                {
                    Kind = UiPlanFieldKind.Place, Label = _t("reallife_ui_life_f_place", "Place"), Value = _draftPlace, Hint = _t("reallife_planner_note_no_place", "No place"),
                    Action = _t("reallife_planner_note_here", "This building"), Action2 = _draftPlace != null ? _t("reallife_planner_note_clear_place", "Remove place") : null,
                },
            };
            if (_host.Reminders)
            {
                int at = Math.Max(0, Array.IndexOf(NoteLeads, _draftLead));
                fields.Add(new UiPlanField
                {
                    Kind = UiPlanFieldKind.Selector, Label = _t("reallife_planner_reminder_head", "Reminder"), Value = LeadWords(NoteLeads[at]), CanLess = at > 0, CanMore = at < NoteLeads.Length - 1,
                });
            }
            Model.Form = new UiPlanFormModel
            {
                Title = _t(fresh ? "reallife_planner_new_note" : "reallife_planner_note_edit", fresh ? "New note" : "Edit note"),
                Icon = UiIcon.Info,
                Fields = fields.ToArray(),
                Offset = _formOffset,
                Actions = new[]
                {
                    new UiActionModel { Label = _t("menu_save", "Save"), Kind = UiButtonKind.Primary, Icon = UiIcon.Check, Key = UiAction.Alt, Disabled = string.IsNullOrEmpty(_draftTitle) },
                    new UiActionModel { Label = _t("common_cancel", "Cancel"), Kind = UiButtonKind.Secondary, Icon = UiIcon.Close, Key = UiAction.Count },
                },
            };
        }

        private float _formOffset;

        private UiPlanFormModel SettingsForm()
        {
            var fields = new List<UiPlanField>(12)
            {
                new UiPlanField { Kind = UiPlanFieldKind.Heading, Label = _t("reallife_planner_settings_show", "Show") },
            };
            for (int g = 0; g < Kinds.Length; g++)
            {
                bool on = PlannerStore.Shows(Kinds[g][0]);
                fields.Add(new UiPlanField
                {
                    Kind = UiPlanFieldKind.Switch, Label = _t(KindKeys[g], KindWords[g]), On = on, Value = on ? _t("reallife_ui_life_on", "On") : _t("reallife_ui_life_off", "Off"),
                });
            }
            if (_host.Reminders)
            {
                int at = Math.Max(1, Array.IndexOf(NoteLeads, PlannerStore.DefaultLead)) - 1;
                fields.Add(new UiPlanField { Kind = UiPlanFieldKind.Heading, Label = _t("reallife_planner_settings_lead", "Reminder for a new note") });
                fields.Add(new UiPlanField
                {
                    Kind = UiPlanFieldKind.Selector, Label = _t("reallife_planner_reminder_head", "Reminder"), Value = LeadWords(NoteLeads[at + 1]),
                    CanLess = at > 0, CanMore = at < NoteLeads.Length - 2,
                });
            }
            return new UiPlanFormModel
            {
                Title = _t("reallife_ui_life_settings", "Planner settings"), Icon = UiIcon.Gear, Fields = fields.ToArray(), Offset = _formOffset,
                Actions = new[] { new UiActionModel { Label = _t("common_close", "Close"), Kind = UiButtonKind.Secondary, Icon = UiIcon.Close, Key = UiAction.Count } },
            };
        }

        // ---------------------------------------------------------------- presses

        /// <summary>A target pressed. Every branch tests the page its command belongs to before it acts.</summary>
        public bool Press(int command, int arg)
        {
            if (command == UiAppWindow.NavCommand)
            {
                if (arg < 0 || arg >= Tabs)
                {
                    return false;
                }
                if (arg == _tab && _page == UiPlanPageKind.List)
                {
                    return false;
                }
                GoTab(arg);
                return true;
            }
            if (command == UiPlanPage.BackCommand)
            {
                return Back();
            }
            switch (_page)
            {
                case UiPlanPageKind.Entry:
                    return PressEntry(command, arg);
                case UiPlanPageKind.Form:
                    return PressForm(command, arg);
                default:
                    return PressList(command, arg);
            }
        }

        private void GoTab(int tab)
        {
            _tab = tab;
            _page = UiPlanPageKind.List;
            Model.Offset = 0f;
            ClearJump();
            _force = true;
        }

        private bool PressList(int command, int arg)
        {
            bool list = _page == UiPlanPageKind.List;
            bool day = _page == UiPlanPageKind.Day;
            if (command == LifePages.Chip && list && _tab != TabNotes && arg >= 0 && arg < Groups)
            {
                if (arg == _chip)
                {
                    return false;
                }
                _chip = arg;
                ClearJump();
                _force = true;
                return true;
            }
            if (command == UiPlanPage.SettingsCommand && list)
            {
                OpenSettings();
                return true;
            }
            if ((command == LifePages.Add && (list && (_tab == TabUpcoming || _tab == TabNotes) || day)) || command == UiBoard.ActionCommand && list && ActionOf(arg) == ActionAdd)
            {
                OpenNote(null, day ? _dayShown : _host.Today);
                return true;
            }
            if (command == UiBoard.ActionCommand && list && ActionOf(arg) == ActionJump)
            {
                return Jump();
            }
            if (command == LifePages.Row && arg >= 0 && arg < _rows.Count && (list && (_tab == TabUpcoming || _tab == TabNotes) || day))
            {
                OpenEntry(_rows[arg]);
                return true;
            }
            if (command == LifePages.Locate && arg >= 0 && arg < _rows.Count && (list && (_tab == TabUpcoming || _tab == TabNotes) || day))
            {
                return Locate(_rows[arg]);
            }
            if (command == UiBoard.ShiftCommand && list && arg >= 0 && arg < _shiftEntries.Count)
            {
                OpenEntry(_shiftEntries[arg]);
                return true;
            }
            if ((command == UiBoard.LaneCommand || command == UiBoard.DayCommand) && list)
            {
                return PressLane(arg);
            }
            if (command == UiPlanDay.RowCommand && (list && _tab == TabToday || day) && arg >= 0 && arg < _next.Count)
            {
                OpenEntry(_next[arg]);
                return true;
            }
            if (command == UiPlanDay.LocateCommand && (list && _tab == TabToday || day) && arg >= 0 && arg < _next.Count)
            {
                return Locate(_next[arg]);
            }
            if (command == UiPlanDay.LaterCommand && list && _tab == TabToday && arg >= 0 && arg < _laterDays.Count)
            {
                OpenDay(_laterDays[arg]);
                return true;
            }
            return false;
        }

        private int ActionOf(int arg) => arg >= 0 && arg < _actions.Count ? _actions[arg] : 0;

        // A lane of the week is a day: its page. A lane of Today is a group: the filter to it, or back to everything.
        private bool PressLane(int lane)
        {
            if (_tab == TabWeek)
            {
                if (lane < 0 || lane >= 7)
                {
                    return false;
                }
                OpenDay(_host.Today + lane);
                return true;
            }
            if (_tab == TabToday && lane >= 0 && lane < _laneGroups.Count)
            {
                int chip = ChipOfGroup(_laneGroups[lane]);
                _chip = _chip == chip ? 0 : chip;
                ClearJump();
                _force = true;
                return true;
            }
            return false;
        }

        private void OpenDay(int day)
        {
            _dayShown = day;
            _page = UiPlanPageKind.Day;
            Model.Offset = 0f;
            ClearJump();
            _force = true;
        }

        private void OpenEntry(PlannerEntry e)
        {
            _fromPage = _page == UiPlanPageKind.Day ? UiPlanPageKind.Day : UiPlanPageKind.List;
            _selId = e.Id;
            _selDay = e.Day;
            _page = UiPlanPageKind.Entry;
            _force = true;
        }

        private bool Locate(PlannerEntry e)
        {
            if (e == null || !(e.HasPlace || !string.IsNullOrEmpty(e.Street)))
            {
                return false;
            }
            _host.Locate(e);
            return true;
        }

        private bool PressEntry(int command, int arg)
        {
            PlannerEntry e = FindSelected();
            if (e == null)
            {
                return false;
            }
            if (command == UiPlanEntry.ActionCommand && arg >= 0 && arg < _entryActions.Length)
            {
                switch (_entryActions[arg])
                {
                    case EntryLocate:
                        return Locate(e);
                    case EntryDone:
                        return ToggleDone(e);
                    case EntryEdit:
                        OpenNote(PlannerStore.Note(e.Id.A), e.Day);
                        return true;
                    case EntryDelete:
                        return Delete(e);
                    default:
                        return false;
                }
            }
            if ((command == UiPlanEntry.LessCommand || command == UiPlanEntry.MoreCommand) && arg == 0)
            {
                return StepReminder(e, command == UiPlanEntry.LessCommand ? -1 : 1);
            }
            return false;
        }

        private PlannerEntry FindSelected()
        {
            for (int i = 0; i < _items.Count; i++)
            {
                if (_items[i].Id.Equals(_selId) && _items[i].Day == _selDay)
                {
                    return _items[i];
                }
            }
            return null;
        }

        private bool ToggleDone(PlannerEntry e)
        {
            if (!e.Own)
            {
                return false;
            }
            PlannerNote note = PlannerStore.Note(e.Id.A);
            if (note == null)
            {
                return false;
            }
            PlannerStore.Falls(note, e.Day, out bool done);
            PlannerStore.SetDone(note, e.Day, !done);
            if (done)
            {
                PlannerStore.Arm(note, _host.NowMinutes);   // not done after all: its reminder again
            }
            _force = true;
            return true;
        }

        private bool Delete(PlannerEntry e)
        {
            if (!e.Own || PlannerStore.Note(e.Id.A) == null)
            {
                return false;
            }
            int id = e.Id.A;
            _host.ConfirmDelete(() =>
            {
                PlannerStore.RemoveNote(id);
                _page = _fromPage;
                _force = true;
            });
            return true;
        }

        private bool StepReminder(PlannerEntry e, int step)
        {
            ReminderLead set = CurrentLead(e, out int count);
            int to = LeadIndexOf(set) + step;
            if (to < 0 || to >= count)
            {
                return false;
            }
            SetLead(e, Leads[to]);
            _force = true;
            return true;
        }

        private void SetLead(PlannerEntry live, ReminderLead lead)
        {
            EntryId id = live.Id;
            int now = _host.NowMinutes;
            PlannerNote series = live.Own ? PlannerStore.Note(id.A) : null;
            if (series != null && series.Repeat != PlannerRepeat.Once && lead != ReminderLead.LeaveBy)
            {
                PlannerStore.SetReminder(id, 0, 0, ReminderLead.None, 0);   // a "when to leave" for this time goes
                series.Lead = lead;
                PlannerStore.Arm(series, now);
                return;
            }
            int travel = 0;
            if (lead == ReminderLead.LeaveBy && _host.MetresTo(live, out float metres))
            {
                travel = PlannerRules.TravelMinutes(metres, _host.ByCar ? PlannerRules.DrivePerMinute : PlannerRules.WalkPerMinute);
            }
            if (!PlannerStore.SetReminder(id, live.Day, live.Deadline && live.End >= 0 ? live.End : live.Start, lead, now, travel))
            {
                _host.Notice("reallife_planner_reminder_too_late");
            }
        }

        // ---------------------------------------------------------------- the form's presses

        private void OpenNote(PlannerNote note, int day)
        {
            int today = _host.Today;
            if (note == null && PlannerStore.Notes.Count >= PlannerStore.MostNotes)
            {
                _host.Notice("reallife_planner_notes_full");
                return;
            }
            _draftId = note != null ? note.Id : 0;
            _draftTitle = note != null ? note.Title : string.Empty;
            _draftRepeat = note != null ? note.Repeat : PlannerRepeat.Once;
            int first = note == null ? day : note.Repeat == PlannerRepeat.Once ? note.Day : Math.Max(PlannerRules.NextOccurrence(note.Repeat, note.Day, today), day);
            _draftDay = Math.Max(today, Math.Min(today + DaysAhead, first));
            _draftStart = note != null ? note.Start : (day == today ? Math.Min(23, _host.Minute / 60 + 1) : 9) * 60;
            _draftPlace = note != null ? note.Place : null;
            _draftStreet = note != null ? note.Street : null;
            _draftNumber = note != null ? note.Number : 0;
            _draftLead = note == null ? PlannerStore.DefaultLead
                : note.Repeat == PlannerRepeat.Once ? PlannerStore.LeadOf(PlannerStore.NoteEntry(note, note.Day)) : note.Lead;
            if (Array.IndexOf(NoteLeads, _draftLead) < 0)
            {
                _draftLead = ReminderLead.None;   // when to leave is set on the entry's page
            }
            _fromFormPage = _page;
            _settings = false;
            _formOffset = 0f;
            _page = UiPlanPageKind.Form;
            _force = true;
        }

        private int _fromFormPage;

        private void OpenSettings()
        {
            _settings = true;
            _formOffset = 0f;
            _fromFormPage = _page;
            _page = UiPlanPageKind.Form;
            _force = true;
        }

        private bool PressForm(int command, int arg)
        {
            if (_settings)
            {
                return PressSettings(command, arg);
            }
            switch (command)
            {
                case UiPlanForm.FieldCommand:
                    if (arg != 0)
                    {
                        return false;
                    }
                    _host.AskTitle(_draftTitle, text =>
                    {
                        string clean = PlannerStore.Clean(text);
                        if (clean.Length > 0)
                        {
                            _draftTitle = clean;
                            _force = true;
                        }
                    });
                    return true;
                case UiPlanForm.OptionCommand:
                    return PressOption(UiPlanForm.OptionField(arg), UiPlanForm.OptionIndex(arg));
                case UiPlanForm.LessCommand:
                case UiPlanForm.MoreCommand:
                    return StepField(arg, command == UiPlanForm.LessCommand ? -1 : 1);
                case UiPlanForm.PlaceCommand:
                    return arg == 5 * 2 ? UseThisPlace() : arg == 5 * 2 + 1 && _draftPlace != null && ClearPlace();
                case UiPlanForm.ActionCommand:
                    return arg == 0 ? Save() : arg == 1 && CloseForm();
                default:
                    return false;
            }
        }

        private bool PressOption(int field, int option)
        {
            if (field == 1 && option >= 0 && option < Presets.Length)
            {
                _draftTitle = _t(Presets[option], PresetWords[option]);
            }
            else if (field == 4 && option >= 0 && option <= (int)PlannerRepeat.Weekly)
            {
                _draftRepeat = (PlannerRepeat)option;
            }
            else
            {
                return false;
            }
            _force = true;
            return true;
        }

        private bool StepField(int field, int step)
        {
            int today = _host.Today;
            switch (field)
            {
                case 2:
                {
                    int to = _draftDay + step;
                    if (to < today || to > today + DaysAhead)
                    {
                        return false;
                    }
                    _draftDay = to;
                    break;
                }
                case 3:
                {
                    int i = (_draftStart < 0 ? 0 : _draftStart / 60 + 1) + step;
                    if (i < 0 || i > 24)
                    {
                        return false;
                    }
                    _draftStart = i == 0 ? -1 : (i - 1) * 60;
                    break;
                }
                case 6:
                {
                    int at = Array.IndexOf(NoteLeads, _draftLead) + step;
                    if (!_host.Reminders || at < 0 || at >= NoteLeads.Length)
                    {
                        return false;
                    }
                    _draftLead = NoteLeads[at];
                    break;
                }
                default:
                    return false;
            }
            _force = true;
            return true;
        }

        // the building the player is in; the street has no address to keep
        private bool UseThisPlace()
        {
            if (!_host.HerePlace(out string street, out int number, out string name) || string.IsNullOrEmpty(street))
            {
                _host.Notice("reallife_planner_note_here_street");
                return false;
            }
            _draftStreet = street;
            _draftNumber = number;
            _draftPlace = name;
            _force = true;
            return true;
        }

        private bool ClearPlace()
        {
            _draftPlace = null;
            _draftStreet = null;
            _draftNumber = 0;
            _force = true;
            return true;
        }

        private bool CloseForm()
        {
            _page = _fromFormPage;
            _settings = false;
            _force = true;
            return true;
        }

        private bool Save()
        {
            string title = PlannerStore.Clean(_draftTitle);
            if (title.Length == 0)
            {
                return false;
            }
            PlannerNote note = _draftId != 0 ? PlannerStore.Note(_draftId) : PlannerStore.AddNote(title, _draftDay, _draftStart);
            if (note == null)
            {
                _host.Notice("reallife_planner_notes_full");
                _page = UiPlanPageKind.List;
                _force = true;
                return false;
            }
            int now = _host.NowMinutes;
            PlannerStore.DropReminders(PlannerStore.NoteSourceId, note.Id);   // set again below for its new time
            if (note.Repeat != _draftRepeat)
            {
                note.Done = false;
                note.DoneDay = -1;
            }
            note.Title = title;
            note.Day = _draftDay;
            note.Start = _draftStart;
            note.Repeat = _draftRepeat;
            note.Lead = _draftLead;
            note.Place = _draftPlace;
            note.Street = _draftPlace != null ? _draftStreet : null;
            note.Number = _draftPlace != null ? _draftNumber : 0;
            note.HasPlace = false;
            bool set = true;
            if (_host.Reminders && _draftLead != ReminderLead.None)
            {
                if (note.Repeat == PlannerRepeat.Once)
                {
                    set = PlannerStore.SetReminder(PlannerStore.NoteEntry(note, note.Day), note.Day, note.Start, _draftLead, now);
                }
                else
                {
                    PlannerStore.Arm(note, now);
                }
            }
            if (!set)
            {
                _host.Notice("reallife_planner_reminder_too_late");
            }
            _selId = PlannerStore.NoteEntry(note, note.Day);
            _selDay = note.Day;
            _fromPage = UiPlanPageKind.List;
            _page = UiPlanPageKind.Entry;
            _draftId = 0;
            _force = true;
            return true;
        }

        private bool PressSettings(int command, int arg)
        {
            switch (command)
            {
                case UiPlanForm.FieldCommand:
                {
                    int g = arg - 1;   // the heading is the first field
                    if (g < 0 || g >= Kinds.Length)
                    {
                        return false;
                    }
                    bool on = !PlannerStore.Shows(Kinds[g][0]);
                    for (int c = 0; c < Kinds[g].Length; c++)
                    {
                        PlannerStore.Show(Kinds[g][c], on);
                    }
                    _force = true;
                    return true;
                }
                case UiPlanForm.LessCommand:
                case UiPlanForm.MoreCommand:
                {
                    int field = Kinds.Length + 2;   // the heading, the switches, the heading
                    if (arg != field || !_host.Reminders)
                    {
                        return false;
                    }
                    int at = Array.IndexOf(NoteLeads, PlannerStore.DefaultLead) + (command == UiPlanForm.LessCommand ? -1 : 1);
                    if (at < 1 || at >= NoteLeads.Length)
                    {
                        return false;
                    }
                    PlannerStore.DefaultLead = NoteLeads[at];
                    _force = true;
                    return true;
                }
                case UiPlanForm.ActionCommand:
                    return arg == 0 && CloseForm();
                default:
                    return false;
            }
        }

        // ---------------------------------------------------------------- keys, tabs, wheel

        /// <summary>Back: a form closes to where it was opened, an entry or a day to the list; false at the list, which lets the game close the app.</summary>
        public bool Back()
        {
            switch (_page)
            {
                case UiPlanPageKind.Form:
                    return CloseForm();
                case UiPlanPageKind.Entry:
                    _page = _fromPage;
                    _force = true;
                    return true;
                case UiPlanPageKind.Day:
                    _page = UiPlanPageKind.List;
                    _force = true;
                    return true;
                default:
                    return false;
            }
        }

        public void Tab(int step)
        {
            int next = _tab + (step < 0 ? -1 : 1);
            if (next >= 0 && next < Tabs)
            {
                GoTab(next);
            }
        }

        /// <summary>A screen action: Alt adds an entry, or saves the form, or sets the destination; Option jumps to now.</summary>
        public bool Act(UiAction action)
        {
            switch (_page)
            {
                case UiPlanPageKind.Form:
                    return action == UiAction.Alt && !_settings && Save();
                case UiPlanPageKind.Entry:
                {
                    PlannerEntry e = FindSelected();
                    if (action == UiAction.Alt && e != null)
                    {
                        if (e.HasPlace || !string.IsNullOrEmpty(e.Street))
                        {
                            return Locate(e);
                        }
                        return e.Own && ToggleDone(e);
                    }
                    return false;
                }
                default:
                    if (action == UiAction.Alt)
                    {
                        OpenNote(null, _page == UiPlanPageKind.Day ? _dayShown : _host.Today);
                        return _page == UiPlanPageKind.Form;
                    }
                    return action == UiAction.Option && _tab == TabToday && _page == UiPlanPageKind.List && Jump();
            }
        }

        public void Reveal(int list, int index)
        {
            if (_page == UiPlanPageKind.Form && Model.Form != null && index >= 0 && index < Model.Form.Tops.Length)
            {
                _formOffset = Math.Max(0f, Model.Form.Tops[index] - Row);
                _force = true;
                return;
            }
            Model.Offset = Math.Max(0, index) * Row;
        }

        public void Wheel(int clip, float notches)
        {
            if (_page == UiPlanPageKind.Form)
            {
                _formOffset = Math.Max(0f, _formOffset - notches * Row);
            }
            else
            {
                Model.Offset = Math.Max(0f, Model.Offset - notches * Row);
            }
            _force = true;
        }

        // ---------------------------------------------------------------- words

        private string Fill(string key, params string[] pairs)
        {
            string text = _t(key, null) ?? key;
            for (int i = 0; i + 1 < pairs.Length; i += 2)
            {
                text = text.Replace("{" + pairs[i] + "}", pairs[i + 1] ?? string.Empty);
            }
            return text;
        }

        private UiNavEntry[] Nav()
        {
            return new[]
            {
                new UiNavEntry { Icon = UiIcon.Clock, Label = _t("reallife_ui_life_tab_today", "Today") },
                new UiNavEntry { Icon = UiIcon.Calendar, Label = _t("reallife_planner_tab_upcoming", "Upcoming") },
                new UiNavEntry { Icon = UiIcon.Grid, Label = _t("reallife_planner_tab_week", "Week") },
                new UiNavEntry { Icon = UiIcon.Info, Label = _t("reallife_ui_life_tab_notes", "Notes"), Count = PlannerStore.Notes.Count > 0 ? PlannerStore.Notes.Count.ToString() : null },
            };
        }

        private string[] Chips()
        {
            return new[]
            {
                _t("reallife_planner_filter_all", "Everything"), _t("reallife_ui_life_group_work", "Work"), _t("reallife_ui_life_group_life", "Life"),
                _t("reallife_planner_group_money", "Money"), _t("reallife_planner_group_business", "Business"), _t("reallife_ui_life_group_study", "Study"),
            };
        }

        private string GroupName(int group)
        {
            switch ((PlannerGroup)group)
            {
                case PlannerGroup.Work: return _t("reallife_ui_life_group_work", "Work");
                case PlannerGroup.Money: return _t("reallife_planner_group_money", "Money");
                case PlannerGroup.Business: return _t("reallife_planner_group_business", "Business");
                case PlannerGroup.Study: return _t("reallife_ui_life_group_study", "Study");
                default: return _t("reallife_ui_life_group_life", "Life");
            }
        }

        public static int GroupOfChip(int chip)
        {
            switch (chip)
            {
                case 1: return (int)PlannerGroup.Work;
                case 2: return (int)PlannerGroup.Life;
                case 3: return (int)PlannerGroup.Money;
                case 4: return (int)PlannerGroup.Business;
                case 5: return (int)PlannerGroup.Study;
                default: return -1;
            }
        }

        public static int ChipOfGroup(int group)
        {
            for (int chip = 1; chip < Groups; chip++)
            {
                if (GroupOfChip(chip) == group)
                {
                    return chip;
                }
            }
            return 0;
        }

        public static UiIcon IconOf(PlannerGroup group)
        {
            switch (group)
            {
                case PlannerGroup.Work: return UiIcon.Briefcase;
                case PlannerGroup.Money: return UiIcon.Coin;
                case PlannerGroup.Business: return UiIcon.Store;
                case PlannerGroup.Study: return UiIcon.Cap;
                default: return UiIcon.Heart;
            }
        }

        public static UiRole TintOf(int group)
        {
            switch ((PlannerGroup)group)
            {
                case PlannerGroup.Work: return UiRole.Chart1;
                case PlannerGroup.Study: return UiRole.Chart6;
                case PlannerGroup.Money: return UiRole.Chart2;
                case PlannerGroup.Business: return UiRole.Chart3;
                default: return UiRole.Chart5;
            }
        }
    }
}
