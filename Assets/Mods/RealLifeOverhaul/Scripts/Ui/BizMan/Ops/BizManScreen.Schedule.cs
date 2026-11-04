using System;
using System.Collections.Generic;
using BigAmbitions.DayNightCycle;
using BigAmbitions.Items;
using Buildings.Retail.Businesses.CinemaTheater;
using Entities;
using Entities.Employee.JobDemands;
using Entities.Employee.JobDemands.Requirements;
using Extensions;
using Helpers;
using Localizor.LanguageChangeEvent;
using TMPro;
using UI;
using UI.Notification;
using UI.Smartphone.Apps.BizMan;
using UI.Smartphone.Apps.BizMan.Schedule;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Company = RealLifeOverhaul.Economy.CompanyDirector;
using RotaGapShift = RealLifeOverhaul.Economy.RotaGapShift;
using RotaKind = RealLifeOverhaul.Economy.RotaKind;
using RotaSheet = RealLifeOverhaul.Economy.RotaSheet;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The schedule of a place as a board, read from the game's own schedule after it loaded the day: the day buttons as tabs
    /// with their open switch and their actions, the hour toggles as the opening hours' sheet (and the ruler's figures), the
    /// lanes as the game's (filtered) list of workstations, their shifts from the game's own cache, and while an hour is
    /// being filled, the game's own picker as a sheet in its order with its search, sort and filters. Every edit is the
    /// game's: a day's buttons and toggles are pressed, a new shift is the game's picker for that hour, a moved or stretched
    /// shift goes through ScheduleHelper with the hours its own drag would give. Over the board, the day's facts (on shift,
    /// wages, the open hours covered, the week against what the rota planner wants) and its problems; under the lanes, the
    /// staff placed against the staff the planner needs each hour with the customers as bars; in the picker, a person's free
    /// hours and the hours their demand leaves them; and beside the day's facts Fill the gaps, the planner's shifts written
    /// only into the hours nobody works. The lanes' search shows where a place has more stations than a board holds, or
    /// words are typed in it. Cost: a look at a few counts four times a second; a layout only when they change; the planner's own
    /// work spread over frames as on its rota page.
    /// </summary>
    public sealed partial class BizManScreen : IUiDragging, IUiSteering
    {
        // The day line's short names, Monday first as the game lays its days.
        private static readonly string[] DayWords =
        {
            "reallife_ui_bizops_day_1", "reallife_ui_bizops_day_2", "reallife_ui_bizops_day_3", "reallife_ui_bizops_day_4", "reallife_ui_bizops_day_5",
            "reallife_ui_bizops_day_6", "reallife_ui_bizops_day_7"
        };

        private readonly List<WorkShift> _shifts = new List<WorkShift>(64);   // the board's shifts, as the game holds them, by the board's index
        private readonly List<string> _lanes = new List<string>(32);          // the board's lanes, by the workstation's id
        private readonly List<bool> _feeLanes = new List<bool>(32);           // a cinema's screen or a theatre's stage: a daily fee, no hours to fill
        private int _feeLane = -1;                  // a screen whose fee switch waits for the game to lay its row
        private readonly List<UiActionModel> _week = new List<UiActionModel>(2);
        private readonly List<int> _weekDo = new List<int>(2);
        private readonly int[] _fromHours = new int[12], _toHours = new int[12];
        private readonly List<ScheduleEmployeeSortToggle> _sorts = new List<ScheduleEmployeeSortToggle>(6);
        private readonly List<ScheduleEmployeeFilterToggle> _filters = new List<ScheduleEmployeeFilterToggle>(32);
        private int[] _others = new int[32], _othersEnd = new int[32];
        private WorkShift _chosen;                  // the shift chosen to move with the keys
        private WorkShift _dragged;                 // the shift held by the pointer
        private UiBoardGrip _grip;
        private float _fromX, _fromY;
        private int _dragLane = -1, _dragStart, _dragEnd, _dragHour;
        private float _downX = -1f;                 // where the pointer last went down on a lane, so its click says which hour
        private int _downLane = -1;
        private string _renaming;                   // the workstation whose name is being typed
        private string _crewPick;                   // the person chosen in the people beside the board, to be put on the next hour pressed
        private bool _crewDrag;                     // a person of the column held by the pointer
        private int _crewLane = -1, _crewHour = -1; // where that drag rests: a lane and an hour, or -1
        private bool _hoursOpen;                    // the opening hours' sheet is open
        private readonly int[] _placedAt = new int[BizOpsRules.Hours];    // the day's work shifts at each hour
        private readonly int[] _neededAt = new int[BizOpsRules.Hours];    // the stations the planner wants worked at each hour
        private readonly int[] _wantingAt = new int[BizOpsRules.Hours];   // the customers the planner expects at each hour
        private readonly HashSet<string> _onNow = new HashSet<string>(), _onDay = new HashSet<string>();
        private readonly List<RotaGapShift> _gapShifts = new List<RotaGapShift>(32);
        private readonly int[] _freeStarts = new int[16], _freeEnds = new int[16];
        private BuildingRegistration _plannerReg;   // the place the planner was last asked about...
        private string _plannerKey;                 // ...by the company's key for it...
        private string _plannerPlace;               // ...the same while the planner keeps a rota for it; null otherwise
        private string _weekPlanned;                // the week's hours against the planner's, as last worked out
        private bool _weekShort, _plannerReady;
        private BuildingRegistration _coverReg;     // the place and the day the needs in _neededAt are of; null for none
        private int _coverDay = -1;

        // What each of the day line's actions does.
        private const int DoCopyDay = 40, DoPasteDay = 41, DoPasteAll = 42, DoAutoDay = 43, DoClearDay = 44, DoRemoveShift = 45, DoManageShift = 46, DoShiftDone = 47,
            DoRenameDone = 48, DoFillGaps = 49;
        private const int RenameArg = -2;           // the typed command's argument for the workstation's name
        private const float EdgeStep = 8f;          // px the lanes scroll each frame a drag rests at their edge

        // ------------------------------------------------------------------------------------------------- the model

        private void Schedule(BizManBusiness b)
        {
            BizManSchedule sch = b.bizManSchedule;
            ScheduleScrollerController lanes = sch != null ? Get<ScheduleScrollerController>(GameContract.SchedLanes, sch) : null;
            List<ScheduleDayButton> days = sch != null ? Get<List<ScheduleDayButton>>(GameContract.SchedDayButtons, Get<ScheduleDaySelectionController>(GameContract.SchedDays, sch)) : null;
            if (lanes == null || days == null || days.Count != DayWords.Length || ScheduleHelper.CurrentScheduleDay == null || ScheduleHelper.WorkstationsById == null
                || ScheduleHelper.Business != b)
            {
                Handover(b);   // not as this version builds it, or not loaded yet: the game's own view
                return;
            }
            _m.Part = BizManPart.Schedule;
            BizOpsModel o = _m.Ops;
            UiBoardModel board = o.Board ?? new UiBoardModel();
            o.Board = board;
            ScheduleDay day = ScheduleHelper.CurrentScheduleDay;
            bool hq = ScheduleHelper.IsHeadquarters;
            board.Days = new string[DayWords.Length];
            for (int i = 0; i < DayWords.Length; i++)
            {
                board.Days[i] = UiText.T(DayWords[i]);
                board.Day = days[i].dayIndex == (int)day.day ? i : board.Day;
            }
            board.FirstHour = 0;
            board.LastHour = BizOpsRules.Hours;
            board.Hours = new string[BizOpsRules.Hours];
            for (int h = 0; h < BizOpsRules.Hours; h++)
            {
                board.Hours[h] = h % 2 == 0 ? h.GetFormattedTime(0f, true) : string.Empty;   // every other hour named: a day of 24 at a page's width
            }
            board.OpenHours = (bool[])ScheduleHelper.OpeningHoursState?.Clone() ?? new bool[BizOpsRules.Hours];
            board.Faces = true;
            board.Step = 60;
            board.Shortest = 60;
            board.Longest = BizOpsRules.Hours * 60;   // the game's handle stretches a shift to the next one or the day's end
            board.Now = day.day == TimeHelper.GetDayOfWeek() ? TimeHelper.CurrentHour * 60 + (int)TimeHelper.CurrentMinute : -1;
            board.EmptyTitle = UiText.T("reallife_ui_bizops_lanes_none");
            board.EmptyBody = UiText.T("reallife_ui_bizops_lanes_none_body");
            Lanes(b, lanes, board);
            if (_feeLane >= 0)
            {
                int lane = _feeLane;
                _feeLane = -1;
                ToggleFee(lanes, lane, false);
            }
            WeekActions(sch, board);
            DayFacts(b, day, board, hq);
            DayLine(b, days, day, hq);
            Hours(sch, hq);
            People();
            Crew(hq);
            Hint(day);
            Filler(sch);
        }

        // A lane's own state for the board's shades and its plus signs: a cleaning station (every hour is its own), the hours a shift may
        // start at now (the game's own hour button: open and not taken, none on a closed day), and why the person chosen beside the board
        // cannot take it (the game's own skill test, ScheduleHelper.HasSkillForWorkstation).
        private static void LaneState(ref UiBoardLane lane, string id, EmployeeInstance who, string whoId, bool[] open, bool dayOpen)
        {
            bool cleaning = ScheduleHelper.IsCleaningStation(id);
            bool[] taken = Taken(id);
            lane.Cleaning = cleaning;
            for (int h = 0; dayOpen && h < BizOpsRules.Hours; h++)
            {
                lane.Free |= BizOpsRules.CanStart(open, taken, cleaning, h) ? 1 << h : 0;
            }
            if (who != null && !ScheduleHelper.HasSkillForWorkstation(id, whoId))
            {
                lane.Blocked = UiText.T("reallife_ui_bizops_lane_blocked");
            }
        }

        // What the shades under the board mean, the person chosen to place, and the hour the mouse rests on.
        private void Shades(UiBoardModel board, EmployeeInstance who)
        {
            bool cleaning = false, blocked = false;
            for (int i = 0; i < board.Lanes.Length; i++)
            {
                cleaning |= board.Lanes[i].Cleaning;
                blocked |= !string.IsNullOrEmpty(board.Lanes[i].Blocked);
            }
            board.Placing = who != null && who.characterData != null ? who.characterData.name : null;
            board.HoverLane = _hoverLane < board.Lanes.Length ? _hoverLane : -1;
            board.HoverHour = board.HoverLane >= 0 ? _hoverHour : -1;
            board.LegendOpen = UiText.T("reallife_ui_bizops_legend_open");
            board.LegendClosed = UiText.T("reallife_ui_bizops_legend_closed");
            board.LegendAdd = UiText.T("reallife_ui_bizops_legend_add");
            board.LegendCleaning = cleaning ? UiText.T("reallife_ui_bizops_legend_cleaning") : null;
            board.LegendBlocked = blocked ? UiText.T("reallife_ui_bizops_legend_blocked") : null;
        }

        // The mouse's rest on a lane's hour, for the + the board draws there; read through IUiTracking, which asks only for the lanes.
        private int _hoverLane = -1, _hoverHour = -1;

        /// <summary>The lanes follow the pointer, so the hour it rests on can show its +.</summary>
        public bool Tracks(int command) => command == UiBoard.LaneCommand && _m.Part == BizManPart.Schedule;

        /// <summary>The pointer moved over a lane: the board lays out again only when the lane or its hour changed.</summary>
        public void Moved(int command, int arg, float x, float y)
        {
            UiBoardModel board = _m.Ops != null ? _m.Ops.Board : null;
            if (command != UiBoard.LaneCommand || board == null)
            {
                return;
            }
            int hour = UiBoardRules.MinuteAt(board, x) / 60;
            if (arg != _hoverLane || hour != _hoverHour)
            {
                _hoverLane = arg;
                _hoverHour = hour;
                _dirty = true;
            }
        }

        // The lanes as the game lists its workstations (its search applied), each its glyph and its words, and their shifts.
        private void Lanes(BizManBusiness b, ScheduleScrollerController lanes, UiBoardModel board)
        {
            _lanes.Clear();
            _feeLanes.Clear();
            _shifts.Clear();
            List<ScheduleWorkstationModel> data = lanes.data;
            int n = data != null ? data.Count : 0;
            board.Lanes = new UiBoardLane[n];
            var shifts = new List<UiBoardShift>(32);
            UiIcon kind = BizManRules.KindIcon(b.buildingRegistration.businessTypeName, b.building != null ? b.building.BuildingType : null);
            EmployeeInstance who = _crewPick != null ? ScheduleHelper.GetScheduleEmployeeById(_crewPick) : null;   // the person chosen beside the board, if any
            bool[] open = ScheduleHelper.OpeningHoursState ?? new bool[BizOpsRules.Hours];
            bool dayOpen = ScheduleHelper.CurrentScheduleDay != null && ScheduleHelper.CurrentScheduleDay.isOpen;
            for (int i = 0; i < n; i++)
            {
                ScheduleWorkstationModel w = data[i];
                _lanes.Add(w.workstationId ?? string.Empty);
                ScheduleHelper.WorkstationsById.TryGetValue(w.workstationId ?? string.Empty, out ItemInstance item);
                bool fee = item == null || item.itemName == CinemaScreen;   // as the game's row tells a screen or the stage, whose hours it hides
                _feeLanes.Add(fee);
                UiIcon glyph = LaneGlyph(w, item, kind);
                board.Lanes[i] = new UiBoardLane { Name = w.workstationName, Sub = fee ? FeeLine(b, w, item) : LaneSub(w, item), Icon = glyph, Tint = UiIconTints.Of(glyph) };
                if (string.IsNullOrEmpty(w.workstationId))
                {
                    continue;   // a theatre's stage has no hours of its own
                }
                LaneState(ref board.Lanes[i], w.workstationId, who, _crewPick, open, dayOpen);
                List<WorkShift> on = ScheduleHelper.GetWorkShiftsByWorkstationId(w.workstationId);
                for (int k = 0; k < on.Count; k++)
                {
                    WorkShift s = on[k];
                    EmployeeInstance e = ScheduleHelper.GetScheduleEmployeeById(s.employeeId);
                    bool held = s == _dragged;
                    string name = e != null && e.characterData != null ? e.characterData.name : string.Empty;
                    shifts.Add(new UiBoardShift
                    {
                        Lane = held && _dragLane >= 0 ? _dragLane : i, Start = (held ? _dragStart : s.startingHour) * 60, End = (held ? _dragEnd : s.endingHour) * 60,
                        Name = name, Warn = e != null && Warns(e), Hue = UiColour.HueOf(name), Ring = e != null ? StaffViews.Mood((int)e.satisfaction) : UiRole.None,
                        Span = UiText.Fill("reallife_ui_bizops_span", "from", s.startingHour.GetFormattedTime(0f), "to", (s.endingHour % BizOpsRules.Hours).GetFormattedTime(0f))
                    });
                    _shifts.Add(s);
                }
            }
            board.Shifts = shifts.ToArray();
            Shades(board, who);
            board.Chosen = _chosen != null ? _shifts.IndexOf(_dragged ?? _chosen) : _dragged != null ? _shifts.IndexOf(_dragged) : -1;
            if (board.Chosen < 0)
            {
                _chosen = null;
            }
            board.Moving = board.Chosen >= 0;
            if (board.Moving)
            {
                ref UiBoardShift c = ref board.Shifts[board.Chosen];
                board.MovingSpan = UiText.Fill("reallife_ui_bizops_span", "from", (c.Start / 60).GetFormattedTime(0f), "to", (c.End / 60 % 24).GetFormattedTime(0f));
            }
        }

        // A lane's glyph by what is worked there: the cleaning, a factory's machine, a cinema's screen, a theatre's stage, a
        // headquarters' desk, or the place's own kind.
        private static UiIcon LaneGlyph(ScheduleWorkstationModel w, ItemInstance item, UiIcon kind)
        {
            if (string.IsNullOrEmpty(w.workstationId))
            {
                return UiIcon.MusicNotes;
            }
            if (w.shiftType == WorkShiftType.Cleaning)
            {
                return UiIcon.Spark;
            }
            if (w.isFactoryMachine)
            {
                return UiIcon.Factory;
            }
            if (item != null && item.itemName == CinemaScreen)
            {
                return UiIcon.Image;
            }
            return ScheduleHelper.IsHeadquarters ? UiIcon.Briefcase : kind;
        }

        private const string CinemaScreen = "ba:itemname_screencinema";

        // A screen's or the stage's quiet line: its daily licensing fee on the day shown, on or off, as the game's row says it.
        private static string FeeLine(BizManBusiness b, ScheduleWorkstationModel w, ItemInstance item)
        {
            bool on = ScheduleHelper.IsLicensingFeeActive(b.buildingRegistration.Address, w.workstationId ?? string.Empty, ScheduleHelper.CurrentScheduleDay.day);
            float fee = item == null ? CinemaTheaterHelper.TheaterStageLicensingFee : CinemaTheaterHelper.CinemaScreenLicensingFee;
            return UiText.Fill("reallife_ui_bizops_fee", "state", UiText.T(on ? "stage_with_licensing_fee_enabled" : "stage_with_licensing_fee_disabled"), "amount", fee.ToShortCurrencyFormat());
        }

        // A lane's quiet line: the customers it serves an hour, what stands attached to it, or that it cleans.
        private static string LaneSub(ScheduleWorkstationModel w, ItemInstance item)
        {
            if (w.customersPerHour > 1)
            {
                return UiText.Fill("bizman_schedule_customers_per_hour", "amount", UiText.Whole(w.customersPerHour));
            }
            if (w.customersPerHour == 1)
            {
                return UiText.T("bizman_schedule_one_customer_per_hour");
            }
            if (w.attachedItems != null && w.attachedItems.Count > 0)
            {
                string first = UiText.T(w.attachedItems[0]);
                return w.attachedItems.Count > 1 ? UiText.Fill("reallife_ui_bizops_and_more", "first", first, "count", UiText.Whole(w.attachedItems.Count - 1)) : first;
            }
            return item != null && item.alias != null && item.alias.Length > 0 ? UiText.T(item.itemName) : null;   // a renamed station says what it is
        }

        // A shift the player should look at, as the game's own bar warns: a demand of the person's the week does not meet, or a day they work too long.
        private static bool Warns(EmployeeInstance e)
        {
            if (e.isAbsent || e.isBeingReplaced)
            {
                return true;
            }
            for (int i = 0; e.demands != null && i < e.demands.Count; i++)
            {
                JobDemand d = JobDemandHelper.GetByName(e.demands[i]);
                if (d is IScheduleDemand && !d.Fulfilled(e, ScheduleHelper.ScheduleDays))
                {
                    return true;
                }
            }
            return ScheduleHelper.GetOverworkedDays(e).Count > 0;
        }

        // The week's actions at the board's head: the game's own auto fill and clear for every day, as it shows them.
        private void WeekActions(BizManSchedule sch, UiBoardModel board)
        {
            _week.Clear();
            _weekDo.Clear();
            ScheduleDaySelectionController days = Get<ScheduleDaySelectionController>(GameContract.SchedDays, sch);
            Button auto = Get<Button>(GameContract.SchedWeekAuto, days);
            if (auto != null && auto.gameObject.activeInHierarchy)
            {
                _week.Add(new UiActionModel { Label = UiText.T("bizman_schedule_auto_fill"), Kind = UiButtonKind.Secondary, Icon = UiIcon.Spark, Key = UiAction.Count, Disabled = !auto.interactable });
                _weekDo.Add(DoAutoDay + 100);
            }
            Button clear = Calling(days, "OnClearScheduleClick");
            if (clear != null)
            {
                _week.Add(new UiActionModel { Label = UiText.T("reallife_ui_bizops_clear_week"), Kind = UiButtonKind.Secondary, Icon = UiIcon.Trash, Key = UiAction.Count,
                    Disabled = !clear.interactable });
                _weekDo.Add(DoClearDay + 100);
            }
            board.Actions = _week.ToArray();
        }

        // The day's line: its open switch and hours, the lanes' search (or the name being typed), and its actions as the
        // game's own day buttons show them, or a chosen shift's.
        private void DayLine(BizManBusiness b, List<ScheduleDayButton> days, ScheduleDay day, bool hq)
        {
            BizOpsModel o = _m.Ops;
            ScheduleDayButton shown = DayButton(days, day);
            Toggle open = Get<Toggle>(GameContract.SchedDayOpen, shown);
            o.DayOpenWords = UiText.Fill("reallife_ui_bizops_open_on", "day", UiText.T(day.day.GetLocalizeKey()));
            o.DayOpenShort = UiText.T(day.day.GetLocalizeKey());   // the game's own name of the day
            o.DayOpen = open != null ? open.isOn : day.isOpen;
            o.DayOpenDisabled = hq || open == null || !open.interactable;
            int runs = BizOpsRules.Runs(_m.Ops.Board.OpenHours, _fromHours, _toHours);
            if (runs == 0)
            {
                o.HoursWords = UiText.T("reallife_ui_bizops_closed_day");
            }
            else
            {
                var words = new System.Text.StringBuilder(48);
                for (int i = 0; i < runs; i++)
                {
                    words.Append(i > 0 ? ", " : string.Empty)
                        .Append(UiText.Fill("reallife_ui_bizops_hours", "from", _fromHours[i].GetFormattedTime(0f), "to", (_toHours[i] % 24).GetFormattedTime(0f)));
                }
                o.HoursWords = words.ToString();
            }
            // The game's search over the lanes where it helps: more stations than a board holds at once, or words typed in it
            // already (they stay in view, so its filter can be cleared).
            TMP_InputField search = Get<TMP_InputField>(GameContract.SchedSearch, Get<ScheduleHeaderController>(GameContract.SchedHeader, b.bizManSchedule));
            bool searching = search != null && (!string.IsNullOrEmpty(search.text) || ScheduleHelper.WorkstationsById.Count >= BizOpsRules.LaneSearchFrom);
            o.LaneSearchHint = searching ? UiText.T("bizman_schedule_search_workstation") : null;
            o.LaneSearch = searching ? search.text : null;
            _actions.Clear();
            if (_renaming != null && ScheduleHelper.WorkstationsById.TryGetValue(_renaming, out ItemInstance station) && station != null)
            {
                o.LaneSearchHint = UiText.T("bizman_schedule_rename_workstation");
                o.LaneSearch = string.IsNullOrEmpty(station.alias) ? UiText.T(station.itemName) : station.alias;
                _actions.Add(Do(DoRenameDone, UiText.T("reallife_ui_bizops_done"), UiButtonKind.Primary, UiIcon.Check, UiAction.Alt));
            }
            else if (_chosen != null)
            {
                _actions.Add(Do(DoManageShift, UiText.T("bizman_schedule_manage_employee_tooltip"), UiButtonKind.Secondary, UiIcon.Person, UiAction.Count));   // Option is the head's EconoView
                _actions.Add(Do(DoRemoveShift, UiText.T("reallife_ui_bizops_remove_shift"), UiButtonKind.Destructive, UiIcon.Trash, UiAction.Count));
                _actions.Add(Do(DoShiftDone, UiText.T("reallife_ui_bizops_done"), UiButtonKind.Primary, UiIcon.Check, UiAction.Alt));
            }
            else
            {
                Offer(_actions, Get<Button>(GameContract.SchedDayCopy, shown), DoCopyDay, UiButtonKind.Secondary, UiIcon.None, UiAction.Count, "reallife_ui_bizops_copy");
                Offer(_actions, Get<Button>(GameContract.SchedDayPaste, shown), DoPasteDay, UiButtonKind.Secondary, UiIcon.None, UiAction.Count, "reallife_ui_bizops_paste");
                Offer(_actions, Get<Button>(GameContract.SchedDayPasteAll, shown), DoPasteAll, UiButtonKind.Secondary, UiIcon.None, UiAction.Count, "reallife_ui_bizops_paste_all");
                Offer(_actions, Get<Button>(GameContract.SchedDayClear, shown), DoClearDay, UiButtonKind.Secondary, UiIcon.Trash, UiAction.Count, "reallife_ui_bizops_clear");
                bool gaps = !hq && _plannerPlace != null && Company.Instance != null && Company.Instance.RotaOn;
                if (gaps)
                {
                    // Beside the day's facts and problems it mends, not among the game's own day buttons; the page's one
                    // main action on F while the day is open.
                    o.FactActions = new[]
                    {
                        new UiActionModel { Label = UiText.T("reallife_ui_bizops_fill_gaps"), Kind = day.isOpen ? UiButtonKind.Primary : UiButtonKind.Secondary, Icon = UiIcon.Plus,
                            Key = day.isOpen ? UiAction.Alt : UiAction.Count, Arg = DoFillGaps, Disabled = !_plannerReady || !day.isOpen }
                    };
                }
                // The game's auto fill is the main action only where Fill the gaps is not offered and the day is open.
                bool autoMain = !gaps && day.isOpen;
                Offer(_actions, Get<Button>(GameContract.SchedDayAuto, shown), DoAutoDay, autoMain ? UiButtonKind.Primary : UiButtonKind.Secondary, UiIcon.Spark,
                    autoMain ? UiAction.Alt : UiAction.Count, "bizman_schedule_auto_fill_day");
                Reword(DoCopyDay, "reallife_ui_bizops_copy");
                Reword(DoPasteDay, "reallife_ui_bizops_paste");
                Reword(DoPasteAll, "reallife_ui_bizops_paste_all");
                Reword(DoClearDay, "reallife_ui_bizops_clear");
                Reword(DoAutoDay, "bizman_schedule_auto_fill_day");
            }
            _m.Actions = _actions.ToArray();
        }

        // The opening hours' sheet: the game's own hour toggles, one an hour, open on a day that opens; a headquarters keeps
        // the game's working hours, so its hours are words only.
        private void Hours(BizManSchedule sch, bool hq)
        {
            BizOpsModel o = _m.Ops;
            List<ScheduleHourToggle> toggles = HourToggles(sch);
            o.HoursPress = !hq && o.DayOpen && toggles != null && toggles.Count >= BizOpsRules.Hours;
            _hoursOpen &= o.HoursPress;
            o.HoursOpen = _hoursOpen;
            o.HoursTitle = UiText.T("bizman_schedule_opening_hours");
            o.HoursLine = UiText.T("reallife_ui_bizops_hours_line");
            o.DoneWords = UiText.T("reallife_ui_bizops_done");
            if (o.HourWords.Length != BizOpsRules.Hours)
            {
                o.HourWords = new string[BizOpsRules.Hours];
            }
            for (int h = 0; h < BizOpsRules.Hours; h++)
            {
                o.HourWords[h] = h.GetFormattedTime(0f, true);
            }
        }

        private static List<ScheduleHourToggle> HourToggles(BizManSchedule sch) =>
            Get<List<ScheduleHourToggle>>(GameContract.SchedHours, Get<ScheduleHourToggleGroup>(GameContract.SchedHourGroup, Get<ScheduleHeaderController>(GameContract.SchedHeader, sch)));

        // The game's day buttons are icons with a tip: the kit's button says it in words.
        private void Reword(int what, string key)
        {
            for (int i = 0; i < _actions.Count; i++)
            {
                if (_actions[i].Arg == what)
                {
                    UiActionModel a = _actions[i];
                    a.Label = UiText.T(key);
                    _actions[i] = a;
                }
            }
        }

        private static ScheduleDayButton DayButton(List<ScheduleDayButton> days, ScheduleDay day)
        {
            for (int i = 0; i < days.Count; i++)
            {
                if (days[i] != null && days[i].dayIndex == (int)day.day)
                {
                    return days[i];
                }
            }
            return null;
        }

        // While an hour is being filled, the game's picker as it sorted and filtered it, as a sheet over the board.
        private void People()
        {
            BizOpsModel o = _m.Ops;
            ScheduleEmployeeSelection pick = PickBox(out ScheduleEmployeeScrollerController list);
            UiDataTableModel old = o.People;
            o.Picking = pick != null && pick.IsOpen && list != null;
            if (!o.Picking)
            {
                o.People = null;
                o.PeopleTitle = o.PeopleLine = null;
                return;
            }
            var t = new UiDataTableModel { Offset = old != null ? old.Offset : 0f, Reveal = old != null ? old.Reveal : -1, EmptyIcon = UiIcon.People };
            string lane = Get<string>(GameContract.SchedPickLane, pick);
            object hourBox = GameContract.SchedPickHour.Field != null ? GameContract.SchedPickHour.Field.GetValue(pick) : null;
            int hour = hourBox is int hh ? hh : -1;
            string station = !string.IsNullOrEmpty(lane) && ScheduleHelper.WorkstationsById.TryGetValue(lane, out ItemInstance it) && it != null
                ? (string.IsNullOrEmpty(it.alias) ? UiText.T(it.itemName) : it.alias) : null;
            o.PeopleTitle = station != null && hour >= 0 ? UiText.Fill("reallife_ui_bizops_pick_title", "station", station, "hour", hour.GetFormattedTime(0f))
                : UiText.T("bizman_schedule_employee_header_no_workstation");
            o.PeopleLine = UiText.T("reallife_ui_bizops_pick_line");
            PickRows(list, t, hour);
            o.People = t;
        }

        // The game's picker: its rows in its order (best fits first, its sort and filters applied), its search, its demand filter, its sorts as the heads.
        private void PickRows(ScheduleEmployeeScrollerController list, UiDataTableModel t, int hour)
        {
            t.Columns = new[]
            {
                new UiDataColumn { Title = UiText.T("reallife_ui_bizops_col_person"), Kind = UiCellKind.Person },
                new UiDataColumn { Title = UiText.T("reallife_ui_bizops_col_skill"), Kind = UiCellKind.Figure, Width = 64f, Graded = true, Sortable = true },
                new UiDataColumn { Title = UiText.T("reallife_ui_bizops_col_week"), Kind = UiCellKind.Figure, Width = 96f, Sortable = true },
                new UiDataColumn { Title = UiText.T("reallife_ui_bizops_col_free"), Kind = UiCellKind.Text, Width = 120f },
                new UiDataColumn { Title = UiText.T("reallife_ui_bizops_col_fit"), Kind = UiCellKind.Chip, Width = 112f },
            };
            List<ScheduleEmployeeModel> data = list.data;
            int n = data != null ? data.Count : 0;
            t.Rows = new UiDataRow[n];
            for (int i = 0; i < n; i++)
            {
                ScheduleEmployeeModel p = data[i];
                bool demand = !string.IsNullOrEmpty(p.violatedDemandName), worn = p.overworkedDays != null && p.overworkedDays.Count > 0;
                string fit = !p.isAvailable ? "reallife_ui_bizops_fit_busy" : demand ? "reallife_ui_bizops_fit_demand" : worn ? "reallife_ui_bizops_fit_tired"
                    : p.perfectMatch ? "reallife_ui_bizops_fit_best" : "reallife_ui_bizops_fit_free";
                UiRole tone = !p.isAvailable ? UiRole.Muted : demand || worn ? UiRole.Warn : UiRole.Ok;
                // The hours they work this week against what their demand allows, and the hours free around the one being
                // filled, each a column of its own so neither is cut from a long line under the name.
                int most = MostHours(ScheduleHelper.GetScheduleEmployeeById(p.employeeId));
                string week = most > 0 ? UiText.Fill("reallife_ui_bizops_hours_of", "count", UiText.Whole(p.hoursAssigned), "most", UiText.Whole(most))
                    : UiText.Fill("reallife_ui_bizops_hours_week", "hours", UiText.Whole(p.hoursAssigned));
                t.Rows[i] = new UiDataRow
                {
                    Cells = new[] { p.employeeName, UiText.Whole(p.primarySkillPercentage) + "%", week, PickFree(p, hour) ?? "-", UiText.T(fit) },
                    Values = new[] { 0, p.primarySkillPercentage, 0, 0, 0 },
                    Tones = new[] { UiRole.None, UiRole.None, most > 0 && p.hoursAssigned >= most ? UiRole.WarnText : UiRole.None, UiRole.None, tone },
                    Sub = string.IsNullOrEmpty(p.primarySkillName) ? null
                        : UiText.T(p.primarySkillName) + ", " + UiText.Fill("bizman_schedule_hourly_wage", "wage", PickWage(p).ToShortCurrencyFormat()),
                    Hue = UiColour.HueOf(p.employeeName ?? string.Empty), Ring = StaffViews.Mood((int)p.satisfaction)
                };
            }
            t.RowCount = n;
            Component filters = Get<Component>(GameContract.SchedPickFilters, list);
            TMP_InputField search = filters != null ? filters.GetComponentInChildren<TMP_InputField>(true) : null;
            t.SearchPlaceholder = search != null ? UiText.T("bizman_schedule_employee_search_placeholder") : null;
            t.Query = search != null ? search.text : null;
            Sorts(filters, t);
            Filters(filters, t);
            TextLocalizationComponent none = Get<TextLocalizationComponent>(GameContract.SchedPickNone, list);
            t.EmptyTitle = none != null && none.gameObject.activeSelf && !string.IsNullOrEmpty(none.Key) ? UiText.T(none.Key) : UiText.T("bizman_schedule_employee_no_employees");
            t.EmptyAction = !string.IsNullOrEmpty(t.Query) || FiltersOn() > 0 ? UiText.T("bizman_schedule_clear_filter") : null;
        }

        // A person's free hours around the hour being filled on the day shown, by their own shifts that day ("17:00 to
        // 23:00"), "All day" with nothing else that day, or null when busy then.
        private string PickFree(ScheduleEmployeeModel p, int hour)
        {
            string words = null;
            List<WorkShift> own = p.isAvailable && hour >= 0 ? ScheduleHelper.GetWorkShiftsByEmployeeId(p.employeeId) : null;
            if (own != null)
            {
                int n = 0;
                for (int i = 0; i < own.Count && n < _freeStarts.Length; i++)
                {
                    if (own[i] != null)
                    {
                        _freeStarts[n] = own[i].startingHour;
                        _freeEnds[n] = own[i].endingHour;
                        n++;
                    }
                }
                if (BizOpsRules.FreeWindow(_freeStarts, _freeEnds, n, hour, out int from, out int to))
                {
                    words = from > 0 || to < BizOpsRules.Hours
                        ? UiText.Fill("reallife_ui_bizops_span", "from", from.GetFormattedTime(0f), "to", (to % BizOpsRules.Hours).GetFormattedTime(0f))
                        : UiText.T("reallife_ui_bizops_free_all_day");
                }
            }
            return words;
        }

        // ------------------------------------------------------------------------------------------------- the people beside the board

        private readonly List<string> _crew = new List<string>(32);   // the column's people, by row: each one's id

        // The place's people as the game's schedule holds them (ScheduleHelper.Employees, its own list for this place), each
        // their face, role and wage, and their hours this week against what their demand allows; the one chosen in the cyan
        // fifth. A headquarters keeps the game's own working hours, and while the game's picker is open it is the list.
        private void Crew(bool hq)
        {
            BizOpsModel o = _m.Ops;
            List<EmployeeInstance> staff = ScheduleHelper.Employees;
            _crew.Clear();
            if (hq || staff == null || o.Board == null || o.Board.Lanes.Length == 0)
            {
                o.Crew = null;
                _crewPick = null;
                return;
            }
            UiDataTableModel old = o.Crew;
            var t = new UiDataTableModel
            {
                Columns = new[]
                {
                    new UiDataColumn { Title = UiText.T("reallife_ui_bizops_col_person"), Kind = UiCellKind.Person },
                    new UiDataColumn { Title = UiText.T("reallife_ui_bizops_col_week"), Kind = UiCellKind.Chip, Width = 96f },
                },
                Offset = old != null ? old.Offset : 0f, EmptyIcon = UiIcon.People, EmptyTitle = UiText.T("bizman_schedule_employee_no_employees")
            };
            var rows = new List<UiDataRow>(staff.Count);
            for (int i = 0; i < staff.Count; i++)
            {
                EmployeeInstance e = staff[i];
                if (e == null || e.characterData == null)
                {
                    continue;
                }
                int most = MostHours(e), left = BizOpsRules.HoursLeft(most, e.assignedWeeklyHours);
                string hours = left == 0 ? UiText.Fill("reallife_ui_bizops_hours_full", "hours", UiText.Whole(e.assignedWeeklyHours))
                    : left > 0 ? UiText.Fill("reallife_ui_bizops_hours_left_short", "hours", UiText.Whole(left))
                    : UiText.Fill("reallife_ui_bizops_hours_week", "hours", UiText.Whole(e.assignedWeeklyHours));
                string skill = e.GetPrimarySkill();
                rows.Add(new UiDataRow
                {
                    Cells = new[] { e.characterData.name, hours }, Tones = new[] { UiRole.None, left == 0 ? UiRole.Warn : UiRole.Ok },
                    Sub = (string.IsNullOrEmpty(skill) ? string.Empty : UiText.T(skill) + ", ")
                          + UiText.Fill("bizman_schedule_hourly_wage", "wage", PaidWage.NowMoney(e.hourlyWage, e.id).ToShortCurrencyFormat()),
                    Hue = UiColour.HueOf(e.characterData.name), Ring = StaffViews.Mood((int)e.satisfaction)
                });
                t.Selected = e.id == _crewPick ? _crew.Count : t.Selected;
                _crew.Add(e.id);
            }
            if (t.Selected < 0)
            {
                _crewPick = null;   // gone from the place since it was chosen
            }
            t.Rows = rows.ToArray();
            t.RowCount = rows.Count;
            BizOpsRules.FitChips(t);
            o.Crew = t;
            o.CrewTitle = UiText.T("bizman_schedule_employee_header_no_workstation");
            o.CrewLine = UiText.T(_crewPick != null ? "reallife_ui_bizops_crew_line_chosen" : "reallife_ui_bizops_crew_line");
        }

        // A day with stations and nobody on it: the first thing to do, as one sentence over the board.
        private void Hint(ScheduleDay day)
        {
            BizOpsModel o = _m.Ops;
            bool empty = day.isOpen && o.Board != null && o.Board.Lanes.Length > 0 && o.Board.Shifts.Length == 0;
            o.BoardHint = !empty ? null : UiText.T(o.FactActions.Length > 0 ? "reallife_ui_bizops_empty_hint_gaps" : "reallife_ui_bizops_empty_hint");
        }

        // The hours a week a person's demand allows (the game's HoursWorkingPerWeek), or 0 where they ask for no limit.
        private static int MostHours(EmployeeInstance e)
        {
            int most = 0;
            for (int i = 0; e != null && e.demands != null && i < e.demands.Count; i++)
            {
                most = JobDemandHelper.GetByName(e.demands[i]) is HoursWorkingPerWeek week && week.MaxHours > 0 ? week.MaxHours : most;
            }
            return most;
        }

        // A person of the column pressed: chosen to place, or let go when pressed again.
        private bool CrewChoose(int row)
        {
            if (row < 0 || row >= _crew.Count)
            {
                return false;
            }
            _crewPick = _crew[row] == _crewPick ? null : _crew[row];
            _chosen = null;
            _renaming = null;
            return true;
        }

        // The person dropped on a lane at an hour: as a press there, if the game's own hour button would take a shift.
        private void CrewDrop(string lane, int hour)
        {
            if (!ScheduleHelper.CurrentScheduleDay.isOpen)
            {
                Notifications.ShowError("bizman_schedule_notification_cant_assign_on_closed_days");
                return;
            }
            bool[] open = ScheduleHelper.OpeningHoursState ?? new bool[BizOpsRules.Hours];
            if (BizOpsRules.CanStart(open, Taken(lane), ScheduleHelper.IsCleaningStation(lane), hour))
            {
                CrewPlace(lane, hour);
            }
        }

        // The chosen person given the hour of a lane, by the game's own ScheduleHelper.AddWorkShift as its picker gives it
        // (it sets the shift's end by its own rule, Going Public and the schedule mods see the same call); refused with the
        // game's own notice where they lack the skill or work elsewhere then, and only where the game's row takes edits.
        private bool CrewPlace(string lane, int hour)
        {
            string who = _crewPick;
            if (who == null || ScheduleHelper.GetScheduleEmployeeById(who) == null || !Renamable(lane))
            {
                return false;
            }
            if (!ScheduleHelper.IsEmployeeAvailable(lane, who, hour))
            {
                Notifications.ShowError("bizman_schedule_employee_not_available");
                return false;
            }
            ScheduleHelper.AddWorkShift(hour, lane, who);
            _crewPick = null;
            UiDirector.Logger?.Info("BizMan (kit): a person of the column put on the schedule through the game's ScheduleHelper");
            return true;
        }

        // ------------------------------------------------------------------------------------------------- the day's facts

        // The rota planner's sheet for the place (Economy: the one its rota page and a place's manager write by), if it is
        // worked out and still true; else null, and the planner set to work, its share of a frame as on its page.
        private RotaSheet PlannerSheet(BuildingRegistration reg)
        {
            Company director = Company.Instance;
            if (director == null || !director.RotaOn || reg == null)
            {
                _plannerPlace = null;
                return null;
            }
            if (!ReferenceEquals(reg, _plannerReg))
            {
                _plannerReg = reg;
                _plannerKey = Economy.CompanyGame.Place(reg);
            }
            Economy.RotaPlace place = director.RotaPlaceAt(_plannerKey);
            _plannerPlace = place != null && place.Type != Economy.RotaType.Warehouse ? _plannerKey : null;   // the planner keeps no warehouse's rota
            RotaSheet sheet = _plannerPlace != null ? director.RotaSheetFor(place) : null;
            if (sheet != null && Company.RotaRefusal(sheet) != null)
            {
                _plannerPlace = null;   // no stations, nobody on the staff, or more than it plans for: no fill of its own
                return null;
            }
            return sheet;
        }

        // The day's facts over the board, by the game's own shifts: on shift now (or on the day shown), the day's wages, the
        // open hours somebody works; and by the planner's sheet, the week's hours against those it wants, each hour's staff
        // against its need under the lanes with the customers it expects, and the day's problems. While a shift is dragged
        // the planner is not asked again: what it last said stays.
        private void DayFacts(BizManBusiness b, ScheduleDay day, UiBoardModel board, bool hq)
        {
            BizOpsModel o = _m.Ops;
            bool today = day.day == TimeHelper.GetDayOfWeek();
            int now = TimeHelper.CurrentHour, open = 0, covered = 0;
            float wages = 0f;
            Array.Clear(_placedAt, 0, _placedAt.Length);
            _onNow.Clear();
            _onDay.Clear();
            List<WorkShift> shifts = day.workShifts;
            for (int i = 0; shifts != null && i < shifts.Count; i++)
            {
                WorkShift s = shifts[i];
                if (s == null || string.IsNullOrEmpty(s.employeeId))
                {
                    continue;
                }
                int from = Mathf.Clamp(s.startingHour, 0, BizOpsRules.Hours), to = Mathf.Clamp(s.endingHour, from, BizOpsRules.Hours);
                for (int h = from; h < to && s.type != WorkShiftType.Cleaning; h++)
                {
                    _placedAt[h]++;
                }
                _onDay.Add(s.employeeId);
                if (today && now >= from && now < to)
                {
                    _onNow.Add(s.employeeId);
                }
                EmployeeInstance e = ScheduleHelper.GetScheduleEmployeeById(s.employeeId);
                wages += e != null ? PaidWage.NowMoney(e.hourlyWage, e.id) * (to - from) : 0f;
            }
            bool[] hours = board.OpenHours;
            for (int h = 0; hours != null && h < hours.Length && h < BizOpsRules.Hours; h++)
            {
                open += hours[h] ? 1 : 0;
                covered += hours[h] && _placedAt[h] > 0 ? 1 : 0;
            }
            if (_dragged == null)
            {
                _plannerPlace = hq ? null : _plannerPlace;   // the planner keeps no headquarters' rota
                RotaSheet sheet = hq ? null : PlannerSheet(b.buildingRegistration);
                _plannerReady = sheet != null;
                PlannerFacts(sheet, b.buildingRegistration, day, board);
            }
            var labels = new List<string>(4);
            var values = new List<string>(4);
            var warn = new List<bool>(4);
            labels.Add(UiText.T(today ? "reallife_ui_bizops_on_shift_now" : "reallife_ui_bizops_staff_on_day"));
            values.Add(UiText.Whole(today ? _onNow.Count : _onDay.Count));
            warn.Add(false);
            labels.Add(UiText.T("reallife_ui_bizops_wages_day"));
            values.Add(wages.ToShortCurrencyFormat());
            warn.Add(false);
            if (open > 0)
            {
                labels.Add(UiText.T("reallife_ui_bizops_hours_covered"));
                values.Add(UiText.Fill("reallife_ui_bizops_of", "count", UiText.Whole(covered), "most", UiText.Whole(open)));
                warn.Add(covered < open);
            }
            if (_weekPlanned != null)
            {
                labels.Add(UiText.T("reallife_ui_bizops_week_planned"));
                values.Add(_weekPlanned);
                warn.Add(_weekShort);
            }
            o.FactLabels = labels.ToArray();
            o.FactValues = values.ToArray();
            o.FactWarn = warn.ToArray();
            DayProblems(day, board);
        }

        // By the planner's sheet: the week's wanted hours covered (the rota as it stands, rated), and for the day shown each
        // hour's stations wanted (its work stations, not the cleaning) against the work shifts placed, with the customers it
        // expects. While the planner works the same place out afresh (a shift changed its stamp), the needs it last gave for
        // the day stay, against the staff placed now. No planner, or another place or day not worked out yet: no strip, and
        // no figure of the week.
        private void PlannerFacts(RotaSheet sheet, BuildingRegistration reg, ScheduleDay day, UiBoardModel board)
        {
            int d = (int)day.day - 1;   // the planner's weekdays start on Monday, the game's DayOfWeekOrdered at 1
            Economy.RotaProblem p = sheet != null ? sheet.Problem : null;
            int stations = p != null ? p.Stations : 0;
            bool fresh = p != null && d >= 0 && d < Economy.RotaCatalogue.Days && stations > 0 && p.Value.Length >= Economy.RotaCatalogue.Cells * stations;
            bool working = sheet == null && _plannerPlace != null && ReferenceEquals(reg, _coverReg) && d == _coverDay;
            board.CoverTitle = board.CoverLine = null;
            board.Cover = null;
            board.Short = null;
            board.Bars = null;
            if (!fresh && !working)
            {
                _weekPlanned = null;
                _weekShort = false;
                _coverReg = null;
                _coverDay = -1;
                Array.Clear(_neededAt, 0, _neededAt.Length);
                Array.Clear(_wantingAt, 0, _wantingAt.Length);
                return;
            }
            if (fresh)
            {
                _coverReg = reg;
                _coverDay = d;
                _weekPlanned = sheet.Now.WantedHours > 0
                    ? UiText.Fill("reallife_ui_bizops_hours_of", "count", UiText.Whole(sheet.Now.CoveredHours), "most", UiText.Whole(sheet.Now.WantedHours)) : null;
                _weekShort = sheet.Now.WantedHours > 0 && sheet.Now.CoveredHours < sheet.Now.WantedHours;
                Array.Clear(_neededAt, 0, _neededAt.Length);
                int[] wanting = sheet.Shop.Wanting;
                for (int h = 0; h < BizOpsRules.Hours; h++)
                {
                    int cell = d * BizOpsRules.Hours + h;
                    for (int s = 0; s < stations; s++)
                    {
                        _neededAt[h] += p.Kind[s] == RotaKind.Work && p.Value[cell * stations + s] > 0 ? 1 : 0;
                    }
                    _wantingAt[h] = wanting != null && cell < wanting.Length ? Math.Max(0, wanting[cell]) : 0;
                }
            }
            int busiest = 0;
            bool any = false;
            var cover = new string[BizOpsRules.Hours];
            var needed = new string[BizOpsRules.Hours];
            var shortHour = new bool[BizOpsRules.Hours];
            var bars = new float[BizOpsRules.Hours];
            for (int h = 0; h < BizOpsRules.Hours; h++)
            {
                busiest = Math.Max(busiest, _wantingAt[h]);
            }
            for (int h = 0; h < BizOpsRules.Hours; h++)
            {
                if (_neededAt[h] > 0 || _placedAt[h] > 0)
                {
                    cover[h] = UiText.Whole(_placedAt[h]);    // a figure for each hour's column: the staff placed, the staff needed under it
                    needed[h] = UiText.Whole(_neededAt[h]);
                    any = true;
                }
                shortHour[h] = _placedAt[h] < _neededAt[h];
                bars[h] = busiest > 0 ? _wantingAt[h] / (float)busiest : 0f;
            }
            if (!any && busiest == 0)
            {
                return;
            }
            board.CoverTitle = UiText.T("reallife_ui_bizops_cover_title");
            board.CoverLine = busiest > 0 ? UiText.T("reallife_ui_bizops_cover_line") : null;
            board.Cover = cover;
            board.Needed = needed;
            board.Short = shortHour;
            board.Bars = busiest > 0 ? bars : null;
        }

        // The day's problems: somebody on two stations at once, and each run of hours short of the staff the planner wants,
        // with the customers it expects. Two at the most; past two, the second says how many more.
        private void DayProblems(ScheduleDay day, UiBoardModel board)
        {
            var list = new List<string>(4);
            List<WorkShift> shifts = day.workShifts;
            for (int i = 0; shifts != null && i < shifts.Count; i++)
            {
                for (int k = i + 1; shifts[i] != null && k < shifts.Count; k++)
                {
                    WorkShift a = shifts[i], c = shifts[k];
                    if (c == null || string.IsNullOrEmpty(a.employeeId) || a.employeeId != c.employeeId)
                    {
                        continue;
                    }
                    int from = Math.Max(a.startingHour, c.startingHour), to = Math.Min(a.endingHour, c.endingHour);
                    if (from < to)
                    {
                        EmployeeInstance e = ScheduleHelper.GetScheduleEmployeeById(a.employeeId);
                        list.Add(UiText.Fill("reallife_ui_bizops_problem_double", "name", e != null && e.characterData != null ? e.characterData.name : "?", "hour",
                            from.GetFormattedTime(0f)));
                    }
                }
            }
            int runs = board.Short != null ? BizOpsRules.Runs(board.Short, _fromHours, _toHours) : 0;
            for (int r = 0; r < runs; r++)
            {
                int need = 0, placed = int.MaxValue, customers = 0;
                for (int h = _fromHours[r]; h < _toHours[r]; h++)
                {
                    need = Math.Max(need, _neededAt[h]);
                    placed = Math.Min(placed, _placedAt[h]);
                    customers = Math.Max(customers, _wantingAt[h]);
                }
                string at = _toHours[r] - _fromHours[r] == 1 ? _fromHours[r].GetFormattedTime(0f)
                    : UiText.Fill("reallife_ui_bizops_span", "from", _fromHours[r].GetFormattedTime(0f), "to", (_toHours[r] % BizOpsRules.Hours).GetFormattedTime(0f));
                list.Add(UiText.Fill(customers > 0 ? "reallife_ui_bizops_problem_short" : "reallife_ui_bizops_problem_short_quiet", "at", at, "placed", UiText.Whole(placed),
                    "needed", UiText.Whole(need), "customers", UiText.Whole(customers)));
            }
            _m.Ops.Problems = list.Count <= 2 ? list.ToArray() : new[] { list[0], UiText.Fill("reallife_ui_bizops_problems_more", "count", UiText.Whole(list.Count - 1)) };
        }

        // Fill the gaps: the planner's shifts for the day shown (its plan for the week, Economy: RotaSheet.Shown), and of
        // each the hours its station and its person are both free (and the place open, but for the cleaning), written as
        // shifts through the game's own ScheduleHelper; a shift a demand of the person's would no longer meet is taken back.
        // Every shift already placed stays as it is.
        private bool FillGaps(BizManBusiness b)
        {
            ScheduleDay day = ScheduleHelper.CurrentScheduleDay;
            RotaSheet sheet = day != null && day.isOpen && !ScheduleHelper.IsHeadquarters ? PlannerSheet(b.buildingRegistration) : null;
            if (sheet == null)
            {
                return false;
            }
            Company.Instance.RotaDayShifts(sheet, (int)day.day - 1, _gapShifts);
            bool[] open = ScheduleHelper.OpeningHoursState;
            int written = 0, hours = 0;
            for (int i = 0; i < _gapShifts.Count; i++)
            {
                RotaGapShift g = _gapShifts[i];
                if (g.Station == null || !ScheduleHelper.WorkstationsById.ContainsKey(g.Station) || ScheduleHelper.GetScheduleEmployeeById(g.Worker) == null)
                {
                    continue;
                }
                int h = g.From;
                while (h < g.To)
                {
                    while (h < g.To && !FreeAt(g, open, h))
                    {
                        h++;
                    }
                    int from = h;
                    while (h < g.To && FreeAt(g, open, h))
                    {
                        h++;
                    }
                    if (h > from && Place(g, from, h))
                    {
                        written++;
                        hours += h - from;
                    }
                }
            }
            _gapShifts.Clear();
            if (written > 0)
            {
                ScheduleHelper.RequestScheduleScrollerReload.Invoke(false);
            }
            MorningNews.Show(written > 0 ? NotificationType.Success : NotificationType.Info, written > 0 ? "reallife_ui_bizops_gaps_filled" : "reallife_ui_bizops_gaps_none",
                new Dictionary<string, string> { { "shifts", UiText.Whole(written) }, { "hours", UiText.Whole(hours) } }, 6f);
            UiDirector.Logger?.Info("BizMan (kit): the gaps of the day filled by the rota planner's plan through the game's own schedule: " + written + " shift(s), " + hours
                                    + " hour(s)");
            return true;
        }

        // An hour of a planned shift free to write: open (the cleaning keeps every hour), its station with no shift then,
        // and its person with none anywhere and the skill it takes (the game's own test).
        private static bool FreeAt(RotaGapShift g, bool[] open, int hour)
        {
            if (!g.Cleaning && (open == null || hour >= open.Length || !open[hour]))
            {
                return false;
            }
            List<WorkShift> lane = ScheduleHelper.GetWorkShiftsByWorkstationId(g.Station);
            for (int i = 0; lane != null && i < lane.Count; i++)
            {
                if (lane[i] != null && hour >= lane[i].startingHour && hour < lane[i].endingHour)
                {
                    return false;
                }
            }
            return ScheduleHelper.IsEmployeeAvailable(g.Station, g.Worker, hour);
        }

        // A run of a planned shift written as the game's own new shift, its end the plan's; taken back where a demand of the
        // person's that the week met before no longer is (the game's own test of each demand).
        private static bool Place(RotaGapShift g, int from, int to)
        {
            EmployeeInstance e = ScheduleHelper.GetScheduleEmployeeById(g.Worker);
            int demands = e != null && e.demands != null ? Math.Min(e.demands.Count, 31) : 0, met = 0;
            for (int i = 0; i < demands; i++)
            {
                JobDemand d = JobDemandHelper.GetByName(e.demands[i]);
                met |= d is IScheduleDemand && d.Fulfilled(e, ScheduleHelper.ScheduleDays) ? 1 << i : 0;
            }
            ScheduleHelper.AddWorkShift(from, g.Station, g.Worker);
            WorkShift added = null;
            List<WorkShift> lane = ScheduleHelper.GetWorkShiftsByWorkstationId(g.Station);
            for (int i = 0; lane != null && i < lane.Count && added == null; i++)
            {
                added = lane[i] != null && lane[i].startingHour == from && lane[i].employeeId == g.Worker ? lane[i] : null;
            }
            if (added == null)
            {
                return false;
            }
            if (added.endingHour != to)
            {
                ScheduleHelper.EditWorkShift(g.Station, g.Worker, from, from, to);
            }
            for (int i = 0; i < demands; i++)
            {
                JobDemand d = JobDemandHelper.GetByName(e.demands[i]);
                if ((met & 1 << i) != 0 && d is IScheduleDemand && !d.Fulfilled(e, ScheduleHelper.ScheduleDays))
                {
                    ScheduleHelper.RemoveWorkShift(g.Station, g.Worker, from);
                    return false;
                }
            }
            return true;
        }

        private static float PickWage(ScheduleEmployeeModel p)
        {
            EmployeeInstance e = ScheduleHelper.GetScheduleEmployeeById(p.employeeId);
            return e != null ? PaidWage.NowMoney(e.hourlyWage, e.id) : p.hourlyWage;
        }

        // The picker's sorts, the game's own toggles, as the heads that sort: the skill and the hours this week; the one on with its arrow.
        private void Sorts(Component filters, UiDataTableModel t)
        {
            _sorts.Clear();
            if (filters != null)
            {
                filters.GetComponentsInChildren(true, _sorts);
            }
            t.SortColumn = -1;
            for (int i = 0; i < _sorts.Count; i++)
            {
                int column = SortColumn(_sorts[i]);
                if (column >= 0 && _sorts[i].IsOn)
                {
                    t.SortColumn = column;
                    t.SortDown = _sorts[i].state == 2;
                }
            }
        }

        private static int SortColumn(ScheduleEmployeeSortToggle toggle)
        {
            object by = GameContract.SchedSortBy.Field != null ? GameContract.SchedSortBy.Field.GetValue(toggle) : null;
            return by is ScheduleEmployeeSortingMethod m ? m == ScheduleEmployeeSortingMethod.Skill ? 1 : m == ScheduleEmployeeSortingMethod.ScheduledHours ? 2 : -1 : -1;
        }

        // The picker's demand filters, the game's own toggles, as one choice: every demand, the one on, or how many are on.
        private void Filters(Component filters, UiDataTableModel t)
        {
            _filters.Clear();
            if (filters != null)
            {
                filters.GetComponentsInChildren(true, _filters);
            }
            if (_filters.Count == 0)
            {
                return;
            }
            int on = FiltersOn(), last = -1;
            for (int i = 0; i < _filters.Count; i++)
            {
                last = _filters[i].IsOn ? i : last;
            }
            t.Filters = new[] { on == 0 ? UiText.T("reallife_ui_staff_all_demands") : on == 1 ? FilterWords(_filters[last]) : UiText.Fill("reallife_ui_staff_chosen", "count", UiText.Whole(on)) };
            if (_m.Ops.ListOpen == BizManPages.OpsPeopleList)
            {
                var words = new string[_filters.Count];
                for (int i = 0; i < words.Length; i++)
                {
                    words[i] = FilterWords(_filters[i]);
                }
                _m.Ops.Options = words;
                _m.Ops.OptionChosen = on == 1 ? last : -1;
            }
        }

        private int FiltersOn()
        {
            int on = 0;
            for (int i = 0; i < _filters.Count; i++)
            {
                on += _filters[i] != null && _filters[i].IsOn ? 1 : 0;
            }
            return on;
        }

        private static string FilterWords(ScheduleEmployeeFilterToggle t)
        {
            var label = t.GetComponentInChildren<TextLocalizationComponent>(true);
            return label != null && !string.IsNullOrEmpty(label.Key) ? UiText.T(label.Key) : UiWidgets.Label(t) ?? string.Empty;
        }

        // The game's auto fill at work (its share, its Cancel) and its question before it starts (its speed's tick).
        private void Filler(BizManSchedule sch)
        {
            BizOpsModel o = _m.Ops;
            o.CancelWords = UiText.T("common_cancel");
            ScheduleAutoFillerUI filler = sch.scheduleAutoFillerUI;
            if (filler != null && filler.gameObject.activeSelf)
            {
                var job = Get<Buildings.Schedule.ScheduleAutoFiller>(GameContract.SchedFiller, filler);
                o.Progress = UiText.T("bizman_schedule_auto_fill_loading");
                o.ProgressShare = job != null ? Mathf.Clamp(Mathf.RoundToInt(job.CurrentProgress * 100f), 0, 100) : 0;
            }
            Buildings.Schedule.ScheduleConfirm ask = sch.scheduleConfirm;
            if (ask != null && ask.gameObject.activeSelf)
            {
                _m.Ops.Card = new UiCardModel
                {
                    Icon = UiIcon.Spark, Title = UiText.T("reallife_ui_bizops_auto_title"), Body = ask.warninglabel != null ? UiWidgets.Text(ask.warninglabel.TextContainer) : null,
                    Check = ask.fastToggle != null ? UiText.T("bizman_schedule_fast_autofill") : null, Checked = ask.fastToggle != null && ask.fastToggle.isOn,
                    Buttons = new[]
                    {
                        new UiActionModel { Label = UiText.T("common_cancel"), Kind = UiButtonKind.Secondary, Key = UiAction.Back },
                        new UiActionModel { Label = UiText.T("bizman_schedule_auto_fill"), Kind = UiButtonKind.Primary, Icon = UiIcon.Spark, Key = UiAction.Confirm }
                    },
                    Prompts = new[] { new UiPrompt { Action = UiAction.Back, Label = UiText.T("reallife_ui_prompt_back") } }, PromptCount = 1
                };
            }
        }

        // The game's picker of people for an hour, and its list; null when this version builds it otherwise.
        private static ScheduleEmployeeSelection PickBox(out ScheduleEmployeeScrollerController list)
        {
            UIs uis = InstanceBehavior<UIs>.Instance;
            ScheduleEmployeeSelection pick = uis != null ? uis.scheduleEmployeeSelection : null;
            list = pick != null ? Get<ScheduleEmployeeScrollerController>(GameContract.SchedPickList, pick) : null;
            return pick;
        }

        // ------------------------------------------------------------------------------------------------- what changed

        private long ScheduleSignature(BizManBusiness b)
        {
            ScheduleDay day = ScheduleHelper.CurrentScheduleDay;
            if (day == null || b.bizManSchedule == null)
            {
                return 0;
            }
            long sig = (int)day.day * 7 + (day.isOpen ? 1 : 0) + (_renaming != null ? 1000 : 0) + (_hoursOpen ? 2000 : 0) + (_chosen != null ? _chosen.GetHashCode() : 0)
                + (_crewPick != null ? _crewPick.GetHashCode() * 3 : 0);
            for (int i = 0; i < day.workShifts.Count; i++)
            {
                WorkShift s = day.workShifts[i];
                sig = sig * 31 + s.startingHour * 97 + s.endingHour * 13 + (s.itemInstanceId != null ? s.itemInstanceId.GetHashCode() : 0);
            }
            bool[] open = ScheduleHelper.OpeningHoursState;
            for (int h = 0; open != null && h < open.Length; h++)
            {
                sig = sig * 3 + (open[h] ? 1 : 0);
            }
            ScheduleScrollerController lanes = Get<ScheduleScrollerController>(GameContract.SchedLanes, b.bizManSchedule);
            sig = sig * 31 + (lanes != null && lanes.data != null ? lanes.data.Count : -1);
            GameInstance game = SaveGameManager.Current;
            sig = sig * 31 + (game != null && game.disabledLicensingFees != null ? game.disabledLicensingFees.Count : 0) + (_feeLane >= 0 ? Time.frameCount : 0);
            List<ScheduleDayButton> days = Get<List<ScheduleDayButton>>(GameContract.SchedDayButtons, Get<ScheduleDaySelectionController>(GameContract.SchedDays, b.bizManSchedule));
            ScheduleDayButton shown = days != null ? DayButton(days, day) : null;
            sig = sig * 31 + Shows(Get<Button>(GameContract.SchedDayPaste, shown)) + Shows(Get<Button>(GameContract.SchedDayPasteAll, shown)) * 2
                + Shows(Get<Button>(GameContract.SchedDayAuto, shown)) * 4;
            ScheduleEmployeeSelection pick = PickBox(out ScheduleEmployeeScrollerController list);
            if (pick != null && pick.IsOpen && list != null && list.data != null)
            {
                sig = sig * 31 + list.data.Count + 1;
                for (int i = 0; i < list.data.Count; i++)
                {
                    sig = sig * 31 + (list.data[i] != null ? list.data[i].employeeId.GetHashCode() : 0);
                }
            }
            ScheduleAutoFillerUI filler = b.bizManSchedule.scheduleAutoFillerUI;
            var job = filler != null && filler.gameObject.activeSelf ? Get<Buildings.Schedule.ScheduleAutoFiller>(GameContract.SchedFiller, filler) : null;
            sig = sig * 31 + (filler != null && filler.gameObject.activeSelf ? 1 + (job != null ? (int)(job.CurrentProgress * 50f) : 0) : 0);
            Buildings.Schedule.ScheduleConfirm ask = b.bizManSchedule.scheduleConfirm;
            sig = sig * 31 + (ask != null && ask.gameObject.activeSelf ? 2 + (ask.fastToggle != null && ask.fastToggle.isOn ? 1 : 0) : 0);
            sig = sig * 31 + (day.day == TimeHelper.GetDayOfWeek() ? TimeHelper.CurrentHour * 4 + (int)TimeHelper.CurrentMinute / 15 : 0);   // the now line, a quarter at a time
            Company director = Company.Instance;
            sig = sig * 3 + (_plannerPlace != null && director != null && director.RotaWorking(_plannerPlace) ? 1 : 0) + (_plannerReady ? 2 : 0);   // the planner done
            return sig;
        }

        private static int Shows(Component c) => c != null && c.gameObject.activeInHierarchy ? 1 : 0;

        // ------------------------------------------------------------------------------------------------- presses

        private bool PressSchedule(BizManBusiness b, int command, int arg)
        {
            BizManSchedule sch = b.bizManSchedule;
            List<ScheduleDayButton> days = sch != null ? Get<List<ScheduleDayButton>>(GameContract.SchedDayButtons, Get<ScheduleDaySelectionController>(GameContract.SchedDays, sch)) : null;
            if (days == null || ScheduleHelper.CurrentScheduleDay == null)
            {
                return false;
            }
            if (_m.Ops.Card != null && (command == UiCard.ButtonCommand || command == UiCard.CheckCommand))
            {
                return PressConfirm(sch, command, arg);
            }
            switch (command)
            {
                case UiBoard.DayCommand:
                    _chosen = null;
                    _renaming = null;
                    _hoursOpen = false;
                    return arg >= 0 && arg < days.Count && Click(Get<Button>(GameContract.SchedDaySelect, days[arg]), "the schedule's day " + (arg + 1));
                case BizManPages.OpsHours:
                    _hoursOpen = !_hoursOpen && _m.Ops.HoursPress;
                    _chosen = null;
                    _renaming = null;
                    return true;
                case BizManPages.OpsHour: return _hoursOpen && ToggleHour(sch, arg);
                case BizManPages.OpsSheetDone: return SheetDone();
                case BizManPages.OpsDayOpen:
                {
                    Toggle open = Get<Toggle>(GameContract.SchedDayOpen, DayButton(days, ScheduleHelper.CurrentScheduleDay));
                    return open != null && UiWidgets.Set(open, !open.isOn);   // the game's own toggle: it refuses for a headquarters with its own notice
                }
                case UiBoard.ShiftCommand:
                    _chosen = arg >= 0 && arg < _shifts.Count && _shifts[arg] != _chosen ? _shifts[arg] : null;
                    _renaming = null;
                    return true;
                case UiBoard.LaneCommand: return PressLane(arg);
                case UiBoard.ActionCommand:
                    return arg >= 0 && arg < _weekDo.Count && Week(sch, _weekDo[arg]);
                case BizManPages.OpsAction when arg == DoFillGaps: return FillGaps(b);
                case BizManPages.OpsAction: return DayAction(days, arg);
                case BizManPages.OpsCancelFill:
                    return Click(Get<Button>(GameContract.SchedFillerCancel, sch.scheduleAutoFillerUI), "cancel the auto fill");
                case BizManPages.OpsSearch: return true;   // typing: the host opens the field
                case BizManPages.People + UiDataTable.Row: return Pick(arg);
                case BizManPages.Crew + UiDataTable.Row: return CrewChoose(arg);
                case BizManPages.People + UiDataTable.Head: return Sort(arg);
                case BizManPages.People + UiDataTable.Search: return true;
                case BizManPages.People + UiDataTable.Filter:
                    _m.Ops.ListOpen = _m.Ops.ListOpen == BizManPages.OpsPeopleList ? -1 : BizManPages.OpsPeopleList;
                    return true;
                case BizManPages.People + UiDataTable.Clear: return ClearPick();
                case BizManPages.OpsOption: return _m.Ops.ListOpen == BizManPages.OpsPeopleList && FilterPick(arg);
                default: return false;
            }
        }

        // An hour of the opening hours' sheet opened or closed by the game's own hour toggle, as a click on it does.
        private static bool ToggleHour(BizManSchedule sch, int hour)
        {
            List<ScheduleHourToggle> hours = HourToggles(sch);
            if (hours == null || hour < 0 || hour >= hours.Count || hours[hour] == null)
            {
                return false;
            }
            hours[hour].OnPointerDown(new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left });
            ScheduleHourToggleGroup group = Get<ScheduleHourToggleGroup>(GameContract.SchedHourGroup, Get<ScheduleHeaderController>(GameContract.SchedHeader, sch));
            if (group != null)
            {
                group.isMultiSelecting = false;   // a click, not a drag across hours: the hours the pointer passes next are not toggled
            }
            UiDirector.Logger?.Info("BizMan (kit): an opening hour handed to the game's own toggle");
            return true;
        }

        // A lane pressed: its name to type where the press fell on its head, otherwise the game's picker for the hour pressed
        // (by the keys, the hour the game offers with its add mark), as its own hour button opens it.
        private bool PressLane(int lane)
        {
            UiBoardModel board = _m.Ops.Board;
            if (board == null || lane < 0 || lane >= _lanes.Count)
            {
                return false;
            }
            string id = _lanes[lane];
            float x = _downLane == lane ? _downX : -1f;
            _downLane = -1;
            _chosen = null;
            if (x >= 0f && x < board.RulerX && !string.IsNullOrEmpty(id))
            {
                if (!Renamable(id))
                {
                    return false;
                }
                _renaming = id;
                return true;
            }
            _renaming = null;
            if (lane < _feeLanes.Count && _feeLanes[lane])
            {
                BizManSchedule sch = _app.business != null ? _app.business.bizManSchedule : null;
                return sch != null && ToggleFee(Get<ScheduleScrollerController>(GameContract.SchedLanes, sch), lane, true);
            }
            if (string.IsNullOrEmpty(id))
            {
                return false;
            }
            if (!ScheduleHelper.CurrentScheduleDay.isOpen)
            {
                Notifications.ShowError("bizman_schedule_notification_cant_assign_on_closed_days");
                return false;
            }
            bool cleaning = ScheduleHelper.IsCleaningStation(id);
            bool[] open = ScheduleHelper.OpeningHoursState ?? new bool[BizOpsRules.Hours];
            bool[] taken = Taken(id);
            int hour = x >= 0f ? UiBoardRules.MinuteAt(board, x) / 60 : BizOpsRules.OfferHour(open, taken, cleaning);
            if (!BizOpsRules.CanStart(open, taken, cleaning, hour))
            {
                return false;   // the game's own hour there is closed or taken: its button does nothing
            }
            if (_crewPick != null)
            {
                return CrewPlace(id, hour);   // the person chosen beside the board takes this hour
            }
            ScheduleHelper.OnRequestEmployeeSelection.Invoke(hour, id);   // what the game's hour button does
            UiDirector.Logger?.Info("BizMan (kit): the game's picker opened for an hour of a workstation");
            return true;
        }

        // Whether the game's own name field of a station takes words: the station's row as the game laid it, or any row the
        // game laid (Going Public locks them all alike on a shop that is another player's); none laid, none taken.
        private bool Renamable(string id)
        {
            BizManSchedule sch = _app.business != null ? _app.business.bizManSchedule : null;
            ScheduleScrollerController lanes = sch != null ? Get<ScheduleScrollerController>(GameContract.SchedLanes, sch) : null;
            if (lanes == null)
            {
                return false;
            }
            lanes.GetComponentsInChildren(true, _cells);
            TMP_InputField any = null, own = null;
            for (int i = 0; i < _cells.Count && own == null; i++)
            {
                ScheduleCellView cell = _cells[i];
                if (cell == null || !cell.gameObject.activeInHierarchy)
                {
                    continue;
                }
                TMP_InputField field = Get<TMP_InputField>(GameContract.SchedLaneName, cell);
                any = any ?? field;
                own = string.Equals(cell.WorkstationId, id, StringComparison.Ordinal) ? field : null;
            }
            _cells.Clear();
            TMP_InputField f = own ?? any;
            return f != null && f.interactable && !f.readOnly;
        }

        private readonly List<ScheduleCellView> _cells = new List<ScheduleCellView>(16);

        // A screen's licensing fee switched by the game's own toggle on its row, which asks before it turns one on and pays
        // today's at once. A row the game has not laid (its list is virtual) is brought into its unseen list, and the switch
        // waits for it to be laid at the next look. A toggle the game does not show or holds (a closed day, Going Public's
        // lock on another's shop) is not pressed.
        private bool ToggleFee(ScheduleScrollerController lanes, int lane, bool mayWait)
        {
            if (lanes == null || lanes.scroller == null || lane < 0 || lane >= lanes.data.Count)
            {
                return false;
            }
            var cell = lanes.scroller.GetCellViewAtDataIndex(lane) as ScheduleCellView;
            if (cell == null)
            {
                if (mayWait)
                {
                    lanes.scroller.JumpToDataIndex(lane);
                    _feeLane = lane;
                    return true;
                }
                UiDirector.Logger?.Info("BizMan (kit): a screen's licensing fee was not switched: the game did not lay its row");
                return false;
            }
            Toggle t = Get<Toggle>(GameContract.SchedLaneFee, cell);
            if (t == null || !t.gameObject.activeInHierarchy || !t.interactable)
            {
                return false;
            }
            bool done = UiWidgets.Set(t, !t.isOn);
            UiDirector.Logger?.Info("BizMan (kit): a screen's licensing fee handed to the game's own toggle" + (done ? string.Empty : " was refused by the game"));
            return done;
        }

        private static bool[] Taken(string lane)
        {
            var taken = new bool[BizOpsRules.Hours];
            List<WorkShift> on = ScheduleHelper.GetWorkShiftsByWorkstationId(lane);
            for (int i = 0; i < on.Count; i++)
            {
                for (int h = Math.Max(0, on[i].startingHour); h < Math.Min(BizOpsRules.Hours, on[i].endingHour); h++)
                {
                    taken[h] = true;
                }
            }
            return taken;
        }

        // A week's action: the game's own auto fill for every day (its question first) or its clear (its question first).
        private bool Week(BizManSchedule sch, int what)
        {
            ScheduleDaySelectionController days = Get<ScheduleDaySelectionController>(GameContract.SchedDays, sch);
            return what == DoAutoDay + 100 ? Click(Get<Button>(GameContract.SchedWeekAuto, days), "auto fill the week")
                : Click(Calling(days, "OnClearScheduleClick"), "clear the week (the game asks first)");
        }

        private bool DayAction(List<ScheduleDayButton> days, int what)
        {
            ScheduleDayButton shown = DayButton(days, ScheduleHelper.CurrentScheduleDay);
            switch (what)
            {
                case DoCopyDay: return Click(Get<Button>(GameContract.SchedDayCopy, shown), "copy the day");
                case DoPasteDay: return Click(Get<Button>(GameContract.SchedDayPaste, shown), "paste the day");
                case DoPasteAll: return Click(Get<Button>(GameContract.SchedDayPasteAll, shown), "paste to every day (the game asks first)");
                case DoClearDay: return Click(Get<Button>(GameContract.SchedDayClear, shown), "clear the day (the game asks first)");
                case DoAutoDay: return Click(Get<Button>(GameContract.SchedDayAuto, shown), "auto fill the day");
                case DoRemoveShift:
                {
                    WorkShift s = _chosen;
                    _chosen = null;
                    if (s == null)
                    {
                        return false;
                    }
                    ScheduleHelper.RemoveWorkShift(s.itemInstanceId, s.employeeId, s.startingHour);   // what the game's right click on a shift does
                    return true;
                }
                case DoManageShift: return ManageShift();
                case DoShiftDone:
                    _chosen = null;
                    return true;
                case DoRenameDone:
                    _renaming = null;
                    return true;
                default: return false;
            }
        }

        // The person of the chosen shift opened in My Employees, as the game's own bar's button does.
        private bool ManageShift()
        {
            EmployeeInstance e = _chosen != null ? ScheduleHelper.GetScheduleEmployeeById(_chosen.employeeId) : null;
            UIs uis = InstanceBehavior<UIs>.Instance;
            if (e == null || uis == null)
            {
                return false;
            }
            _chosen = null;
            uis.smartphoneUI.OpenApp(AppName.MyEmployees);
            uis.fullMenu.myEmployees.DelayShowEmployee(e);
            return true;
        }

        // A person of the game's picker pressed: the game's own choice for that row (it refuses one not free with its notice).
        private bool Pick(int row)
        {
            PickBox(out ScheduleEmployeeScrollerController list);
            if (list == null || list.data == null || row < 0 || row >= list.data.Count)
            {
                return false;
            }
            ScheduleEmployeeModel p = list.data[row];
            if (!p.isAvailable)
            {
                Notifications.ShowError("bizman_schedule_employee_not_available");
                return false;
            }
            p.onEmployeeSelected?.Invoke(p.employeeId);
            UiDirector.Logger?.Info("BizMan (kit): a person put on the schedule through the game's own picker");
            return true;
        }

        // A head of the picker pressed: the game's own sort toggle for it clicked, which sorts the list again.
        private bool Sort(int column)
        {
            for (int i = 0; i < _sorts.Count; i++)
            {
                if (_sorts[i] != null && SortColumn(_sorts[i]) == column)
                {
                    _sorts[i].OnPointerClick(new PointerEventData(EventSystem.current));
                    return true;
                }
            }
            return false;
        }

        // A demand filter of the picker switched, then the game's list sorted and filtered again as its own toggles have it.
        private bool FilterPick(int option)
        {
            if (option < 0 || option >= _filters.Count || _filters[option] == null)
            {
                return false;
            }
            _filters[option].SetToggleWithoutNotify(!_filters[option].IsOn);
            Repick();
            return true;
        }

        private bool ClearPick()
        {
            PickBox(out ScheduleEmployeeScrollerController list);
            var filters = list != null ? Get<ScheduleEmployeeFilterController>(GameContract.SchedPickFilters, list) : null;
            if (filters == null)
            {
                return false;
            }
            filters.OnClearFilters();   // the game's own Clear filter: its search emptied, its toggles off, the list again
            return true;
        }

        private static void Repick()
        {
            PickBox(out ScheduleEmployeeScrollerController list);
            if (list != null && GameContract.SchedPickRefresh.Method != null)
            {
                GameContract.SchedPickRefresh.Method.Invoke(list, null);
            }
        }

        // The auto fill's question: its tick is the game's own speed toggle, its buttons the game's own.
        private bool PressConfirm(BizManSchedule sch, int command, int arg)
        {
            Buildings.Schedule.ScheduleConfirm ask = sch.scheduleConfirm;
            if (ask == null || !ask.gameObject.activeSelf)
            {
                return false;
            }
            if (command == UiCard.CheckCommand)
            {
                return ask.fastToggle != null && UiWidgets.Set(ask.fastToggle, !ask.fastToggle.isOn);
            }
            if (arg == 0)
            {
                ask.ClickCancel();
            }
            else
            {
                ask.ClickConfirm();
            }
            return true;
        }

        // ------------------------------------------------------------------------------------------------- dragging and steering

        public void PointerDown(int command, int arg, float x, float y)
        {
            if (command == UiBoard.LaneCommand)
            {
                _downLane = arg;
                _downX = x;
            }
        }

        public bool DragBegin(int command, int arg, float x, float y)
        {
            UiBoardModel board = _m.Ops.Board;
            if (_m.Part == BizManPart.Schedule && command == BizManPages.Crew + UiDataTable.Row && board != null && arg >= 0 && arg < _crew.Count)
            {
                // A person of the column held: dropped on an hour of a lane, they take it as a press there would give it.
                _crewPick = _crew[arg];
                _crewDrag = true;
                _crewLane = _crewHour = -1;
                _dirty = true;
                return true;
            }
            if (_m.Part != BizManPart.Schedule || command != UiBoard.ShiftCommand || board == null || arg < 0 || arg >= _shifts.Count)
            {
                return false;
            }
            WorkShift s = _shifts[arg];
            UiBoardGrip grip = UiBoardRules.Grip(board, arg, x);
            if (grip != UiBoardGrip.Body && ScheduleHelper.IsHeadquarters && s.type != WorkShiftType.Cleaning)
            {
                Notifications.ShowError("bizman_schedule_cannot_change_working_hours");   // the game's own refusal for a headquarters' working hours
                return false;
            }
            _dragged = s;
            _chosen = s;
            _grip = grip;
            _fromX = x;
            _fromY = y;
            _dragLane = board.Shifts[arg].Lane;
            _dragStart = s.startingHour;
            _dragEnd = s.endingHour;
            _dragHour = UiBoardRules.MinuteAt(board, x) / 60;
            _dirty = true;
            return true;
        }

        public void DragMove(float x, float y)
        {
            UiBoardModel board = _m.Ops.Board;
            if (_crewDrag)
            {
                bool over = board != null && x >= board.RulerX && x < board.RulerX + board.RulerW && y >= board.LanesTop && y < board.LanesTop + board.LanesHeight;
                _crewLane = over ? UiBoardRules.LaneAt(board, y) : -1;
                _crewHour = over ? UiBoardRules.MinuteAt(board, x) / 60 : -1;
                return;
            }
            int index = _dragged != null && board != null ? board.Chosen : -1;
            if (index < 0)
            {
                return;
            }
            // The drag's hours from where it started, by the board's own rule; the bar shows them until it is let go.
            WorkShift s = _dragged;
            UiBoardShift held = board.Shifts[index];
            board.Shifts[index].Lane = _lanes.IndexOf(s.itemInstanceId);
            board.Shifts[index].Start = s.startingHour * 60;
            board.Shifts[index].End = s.endingHour * 60;
            UiBoardRules.Drag(board, index, _grip, _fromX, _fromY, x, y, out int lane, out int start, out int end);
            board.Shifts[index] = held;
            _dragHour = UiBoardRules.MinuteAt(board, x) / 60;
            int scroll = UiBoardRules.EdgeScroll(board, y);
            if (scroll != 0)
            {
                board.Offset = Math.Max(0f, board.Offset + scroll * EdgeStep);
            }
            if (lane != _dragLane || start / 60 != _dragStart || end / 60 != _dragEnd || scroll != 0)
            {
                _dragLane = lane;
                _dragStart = start / 60;
                _dragEnd = end / 60;
                _dirty = true;
            }
        }

        public void DragEnd(bool cancelled)
        {
            if (_crewDrag)
            {
                _crewDrag = false;
                _dirty = true;
                int lane = _crewLane, hour = _crewHour;
                _crewLane = _crewHour = -1;
                if (!cancelled && lane >= 0 && lane < _lanes.Count && hour >= 0 && !string.IsNullOrEmpty(_lanes[lane]) && (lane >= _feeLanes.Count || !_feeLanes[lane]))
                {
                    CrewDrop(_lanes[lane], hour);
                }
                return;
            }
            WorkShift s = _dragged;
            _dragged = null;
            _dirty = true;
            if (cancelled || s == null || _dragLane < 0 || _dragLane >= _lanes.Count)
            {
                return;
            }
            if (_grip == UiBoardGrip.Body)
            {
                Move(s, _lanes[_dragLane], _dragStart, _dragEnd, _dragHour);
            }
            else
            {
                Stretch(s, _grip == UiBoardGrip.Start, _grip == UiBoardGrip.Start ? _dragStart : _dragEnd);
            }
        }

        public bool Steer(int dx, int dy, bool shift)
        {
            UiBoardModel board = _m.Ops.Board;
            if (_m.Part != BizManPart.Schedule || _chosen == null || board == null || board.Chosen < 0 || _renaming != null)
            {
                return false;
            }
            bool fixedHours = ScheduleHelper.IsHeadquarters && _chosen.type != WorkShiftType.Cleaning;
            UiBoardRules.Nudge(board, board.Chosen, dx, dy, shift, out int lane, out int start, out int end);
            if (shift)
            {
                if (fixedHours)
                {
                    Notifications.ShowError("bizman_schedule_cannot_change_working_hours");
                    return true;
                }
                Stretch(_chosen, false, end / 60);
            }
            else
            {
                Move(_chosen, _lanes[lane], start / 60, end / 60, start / 60);
            }
            _dirty = true;
            return true;
        }

        // A shift moved to a lane and hours by the game's own rule for its drag: the person must have a skill the station
        // takes (else its notice), a headquarters' desk keeps its opening hours, and the hours move off any shift they would
        // overlap there or of the same person; then ScheduleHelper.MoveWorkShift, and the game's own lanes laid again.
        private void Move(WorkShift s, string lane, int start, int end, int released)
        {
            if (string.IsNullOrEmpty(lane))
            {
                return;
            }
            if (!ScheduleHelper.HasSkillForWorkstation(lane, s.employeeId))
            {
                Notifications.ShowError("bizman_schedule_employee_doesnt_have_required_skill");
                return;
            }
            int count = Others(s, lane);
            if (ScheduleHelper.IsHeadquarters && s.type != WorkShiftType.Cleaning)
            {
                OpeningHourSlot slot = ScheduleHelper.CurrentScheduleDay.openingHourSlots.Count > 0 ? ScheduleHelper.CurrentScheduleDay.openingHourSlots[0] : null;
                if (slot == null)
                {
                    return;
                }
                start = slot.startingHour;
                end = slot.endingHour;
                for (int i = 0; i < count; i++)
                {
                    if (Math.Max(start, _others[i]) < Math.Min(end, _othersEnd[i]))
                    {
                        return;   // the game refuses a desk whose day would overlap another's
                    }
                }
            }
            else
            {
                start = Mathf.Clamp(start, 0, BizOpsRules.Hours);
                end = Mathf.Clamp(end, 0, BizOpsRules.Hours);
                BizOpsRules.MoveOff(_others, _othersEnd, count, released, ref start, ref end);
            }
            if (start >= end)
            {
                return;   // it does not fit there: the game puts it back where it was
            }
            ScheduleHelper.MoveWorkShift(s, start, end, lane);
            ScheduleHelper.RequestScheduleScrollerReload.Invoke(false);
            UiDirector.Logger?.Info("BizMan (kit): a shift moved through the game's own schedule");
        }

        // A shift's start or end pulled to an hour within what the game's own handle allows, then ScheduleHelper.EditWorkShift.
        private void Stretch(WorkShift s, bool startEnd, int hour)
        {
            int count = Others(s, s.itemInstanceId, true);
            BizOpsRules.HandleRange(startEnd, s.startingHour, s.endingHour, _others, _othersEnd, count, out int lo, out int hi);
            hour = Mathf.Clamp(hour, lo, hi);
            int start = startEnd ? hour : s.startingHour, end = startEnd ? s.endingHour : hour;
            if (start >= end || (start == s.startingHour && end == s.endingHour))
            {
                return;
            }
            ScheduleHelper.EditWorkShift(s.itemInstanceId, s.employeeId, s.startingHour, start, end);
            ScheduleHelper.RequestScheduleScrollerReload.Invoke(false);
            UiDirector.Logger?.Info("BizMan (kit): a shift's hours changed through the game's own schedule");
        }

        // The hours of the shifts a moved one must keep off: the lane's and the person's, the shift itself left out (or, for
        // a handle's range, kept in as the game's own list has it).
        private int Others(WorkShift s, string lane, bool withSelf = false)
        {
            List<WorkShift> onLane = ScheduleHelper.GetWorkShiftsByWorkstationId(lane), ofPerson = ScheduleHelper.GetWorkShiftsByEmployeeId(s.employeeId);
            int need = onLane.Count + ofPerson.Count;
            if (_others.Length < need)
            {
                _others = new int[need * 2];
                _othersEnd = new int[need * 2];
            }
            int n = 0;
            for (int k = 0; k < 2; k++)
            {
                List<WorkShift> from = k == 0 ? onLane : ofPerson;
                for (int i = 0; i < from.Count; i++)
                {
                    if (from[i] == s && !withSelf)
                    {
                        continue;
                    }
                    _others[n] = from[i].startingHour;
                    _othersEnd[n] = from[i].endingHour;
                    n++;
                }
            }
            return n;
        }

        // ------------------------------------------------------------------------------------------------- typing

        // The field a typed schedule command types into: the lanes' search, or a workstation's name.
        private TMP_InputField ScheduleField(BizManBusiness b) =>
            Get<TMP_InputField>(GameContract.SchedSearch, Get<ScheduleHeaderController>(GameContract.SchedHeader, b.bizManSchedule));

        private string ScheduleTyping(BizManBusiness b, int command)
        {
            if (command == BizManPages.OpsSearch && _renaming != null && ScheduleHelper.WorkstationsById.TryGetValue(_renaming, out ItemInstance station) && station != null)
            {
                return string.IsNullOrEmpty(station.alias) ? UiText.T(station.itemName) : station.alias;
            }
            if (command == BizManPages.OpsSearch)
            {
                return ScheduleField(b)?.text;
            }
            PickBox(out ScheduleEmployeeScrollerController list);
            Component filters = list != null ? Get<Component>(GameContract.SchedPickFilters, list) : null;
            TMP_InputField search = filters != null ? filters.GetComponentInChildren<TMP_InputField>(true) : null;
            return search != null ? search.text : null;
        }

        private void ScheduleTyped(BizManBusiness b, int command, string text)
        {
            if (command == BizManPages.OpsSearch && _renaming != null)
            {
                if (!string.IsNullOrEmpty(text) && ScheduleHelper.WorkstationsById.ContainsKey(_renaming) && Renamable(_renaming))
                {
                    ScheduleHelper.RenameWorkstation(_renaming, text);   // what the game's own name field does when its words are done
                }
                return;
            }
            if (command == BizManPages.OpsSearch)
            {
                TMP_InputField f = ScheduleField(b);
                if (f != null)
                {
                    UiWidgets.Type(f, text, false);   // its listener filters the lanes as typing into it would
                }
                return;
            }
            PickBox(out ScheduleEmployeeScrollerController list);
            Component filters = list != null ? Get<Component>(GameContract.SchedPickFilters, list) : null;
            TMP_InputField search = filters != null ? filters.GetComponentInChildren<TMP_InputField>(true) : null;
            if (search != null)
            {
                UiWidgets.Type(search, text, false);
                Repick();
            }
        }

        // Back on the schedule: the hours' sheet closes, the name typed is done, the picker closes as the game's own Escape
        // closes it, a chosen shift is let go; false when nothing of the schedule was open.
        private bool ScheduleBack()
        {
            if (_hoursOpen || _renaming != null || _chosen != null)
            {
                _hoursOpen = false;
                _renaming = null;
                _chosen = null;
                return true;
            }
            return HidePicker();
        }

        // A sheet's Done or Cancel: the hours' sheet closed, or the game's picker as its own close does.
        private bool SheetDone()
        {
            if (_hoursOpen)
            {
                _hoursOpen = false;
                return true;
            }
            return HidePicker();
        }

        private static bool HidePicker()
        {
            ScheduleEmployeeSelection pick = PickBox(out _);
            if (pick != null && pick.IsOpen)
            {
                pick.Hide();
                return true;
            }
            return false;
        }
    }
}
