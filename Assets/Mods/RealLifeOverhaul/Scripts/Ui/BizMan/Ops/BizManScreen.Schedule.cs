using System;
using System.Collections.Generic;
using BigAmbitions.DayNightCycle;
using BigAmbitions.Items;
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

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The schedule of a place as a board, read from the game's own schedule after it loaded the day: the day buttons as tabs
    /// with their open switch and their actions, the hour toggles as the ruler, the lanes as the game's (filtered) list of
    /// workstations, their shifts from the game's own cache, the people beside it from the game's list of the place's staff,
    /// and while an hour is being filled, the game's own picker in its order with its search, sort and filters. Every edit
    /// is the game's: a day's buttons and toggles are pressed, a new shift is the game's picker for that hour, a moved or
    /// stretched shift goes through ScheduleHelper with the hours its own drag would give. Cost: a look at a few counts four
    /// times a second; a layout only when they change.
    /// </summary>
    public sealed partial class BizManScreen : IUiDragging, IUiSteering
    {
        private static readonly int[] DayKeys = { 1, 2, 3, 4, 5, 6, 7 };   // the game's days in its order, Monday first
        private readonly List<WorkShift> _shifts = new List<WorkShift>(64);   // the board's shifts, as the game holds them, by the board's index
        private readonly List<string> _lanes = new List<string>(32);          // the board's lanes, by the workstation's id
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

        // What each of the day line's actions does.
        private const int DoCopyDay = 40, DoPasteDay = 41, DoPasteAll = 42, DoAutoDay = 43, DoClearDay = 44, DoRemoveShift = 45, DoManageShift = 46, DoShiftDone = 47,
            DoRenameDone = 48;
        private const int RenameArg = -2;           // the typed command's argument for the workstation's name
        private const float EdgeStep = 8f;          // px the lanes scroll each frame a drag rests at their edge

        // ------------------------------------------------------------------------------------------------- the model

        private void Schedule(BizManBusiness b)
        {
            BizManSchedule sch = b.bizManSchedule;
            ScheduleScrollerController lanes = sch != null ? Get<ScheduleScrollerController>(GameContract.SchedLanes, sch) : null;
            List<ScheduleDayButton> days = sch != null ? Get<List<ScheduleDayButton>>(GameContract.SchedDayButtons, Get<ScheduleDaySelectionController>(GameContract.SchedDays, sch)) : null;
            if (lanes == null || days == null || days.Count != DayKeys.Length || ScheduleHelper.CurrentScheduleDay == null || ScheduleHelper.WorkstationsById == null
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
            board.Days = new string[DayKeys.Length];
            for (int i = 0; i < DayKeys.Length; i++)
            {
                board.Days[i] = UiText.T("reallife_ui_bizops_day_" + DayKeys[i].ToString(System.Globalization.CultureInfo.InvariantCulture));
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
            board.HoursPress = !hq;
            board.Faces = true;
            board.Step = 60;
            board.Shortest = 60;
            board.Longest = BizOpsRules.Hours * 60;   // the game's handle stretches a shift to the next one or the day's end
            board.Now = day.day == TimeHelper.GetDayOfWeek() ? TimeHelper.CurrentHour * 60 + (int)TimeHelper.CurrentMinute : -1;
            board.EmptyTitle = UiText.T("reallife_ui_bizops_lanes_none");
            board.EmptyBody = UiText.T("reallife_ui_bizops_lanes_none_body");
            Lanes(b, lanes, board);
            WeekActions(sch, board);
            DayLine(b, days, day, hq);
            People(b);
            Filler(sch);
        }

        // The lanes as the game lists its workstations (its search applied), each its glyph and its words, and their shifts.
        private void Lanes(BizManBusiness b, ScheduleScrollerController lanes, UiBoardModel board)
        {
            _lanes.Clear();
            _shifts.Clear();
            List<ScheduleWorkstationModel> data = lanes.data;
            int n = data != null ? data.Count : 0;
            board.Lanes = new UiBoardLane[n];
            var shifts = new List<UiBoardShift>(32);
            UiIcon kind = BizManRules.KindIcon(b.buildingRegistration.businessTypeName, b.building != null ? b.building.BuildingType : null);
            for (int i = 0; i < n; i++)
            {
                ScheduleWorkstationModel w = data[i];
                _lanes.Add(w.workstationId ?? string.Empty);
                ScheduleHelper.WorkstationsById.TryGetValue(w.workstationId ?? string.Empty, out ItemInstance item);
                UiIcon glyph = LaneGlyph(w, item, kind);
                board.Lanes[i] = new UiBoardLane { Name = w.workstationName, Sub = LaneSub(w, item), Icon = glyph, Tint = UiIconTints.Of(glyph) };
                if (string.IsNullOrEmpty(w.workstationId))
                {
                    continue;   // a theatre's stage has no hours of its own
                }
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
                        Name = name, Warn = e != null && Warns(e), Hue = UiColour.HueOf(name), Ring = e != null ? StaffViews.Mood((int)e.satisfaction) : UiRole.None
                    });
                    _shifts.Add(s);
                }
            }
            board.Shifts = shifts.ToArray();
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
            if (item != null && item.itemName == "ba:itemname_screencinema")
            {
                return UiIcon.Image;
            }
            return ScheduleHelper.IsHeadquarters ? UiIcon.Briefcase : kind;
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
                return UiText.T(w.attachedItems[0]) + (w.attachedItems.Count > 1 ? UiText.Fill("reallife_ui_bizops_and_more", "count", UiText.Whole(w.attachedItems.Count - 1)) : string.Empty);
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
                _week.Add(new UiActionModel { Label = UiText.T("reallife_ui_bizops_clear_week"), Kind = UiButtonKind.Secondary, Icon = UiIcon.Trash, Key = UiAction.Count, Disabled = !clear.interactable });
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
                    words.Append(i > 0 ? ", " : string.Empty).Append(UiText.Fill("reallife_ui_bizops_hours", "from", _fromHours[i].GetFormattedTime(0f), "to", (_toHours[i] % 24).GetFormattedTime(0f)));
                }
                o.HoursWords = words.ToString();
            }
            TMP_InputField search = Get<TMP_InputField>(GameContract.SchedSearch, Get<ScheduleHeaderController>(GameContract.SchedHeader, b.bizManSchedule));
            o.LaneSearchHint = search != null ? UiText.T("bizman_schedule_search_workstation") : null;
            o.LaneSearch = search != null ? search.text : null;
            _actions.Clear();
            if (_renaming != null && ScheduleHelper.WorkstationsById.TryGetValue(_renaming, out ItemInstance station) && station != null)
            {
                o.LaneSearchHint = UiText.T("bizman_schedule_rename_workstation");
                o.LaneSearch = string.IsNullOrEmpty(station.alias) ? UiText.T(station.itemName) : station.alias;
                _actions.Add(Do(DoRenameDone, UiText.T("reallife_ui_bizops_done"), UiButtonKind.Primary, UiIcon.Check, UiAction.Alt));
            }
            else if (_chosen != null)
            {
                _actions.Add(Do(DoManageShift, UiText.T("bizman_schedule_manage_employee_tooltip"), UiButtonKind.Secondary, UiIcon.Person, UiAction.Option));
                _actions.Add(Do(DoRemoveShift, UiText.T("reallife_ui_bizops_remove_shift"), UiButtonKind.Destructive, UiIcon.Trash, UiAction.Count));
                _actions.Add(Do(DoShiftDone, UiText.T("reallife_ui_bizops_done"), UiButtonKind.Primary, UiIcon.Check, UiAction.Alt));
            }
            else
            {
                Offer(_actions, Get<Button>(GameContract.SchedDayCopy, shown), DoCopyDay, UiButtonKind.Secondary, UiIcon.None, UiAction.Count, "reallife_ui_bizops_copy");
                Offer(_actions, Get<Button>(GameContract.SchedDayPaste, shown), DoPasteDay, UiButtonKind.Secondary, UiIcon.None, UiAction.Count, "reallife_ui_bizops_paste");
                Offer(_actions, Get<Button>(GameContract.SchedDayPasteAll, shown), DoPasteAll, UiButtonKind.Secondary, UiIcon.None, UiAction.Count, "reallife_ui_bizops_paste_all");
                Offer(_actions, Get<Button>(GameContract.SchedDayClear, shown), DoClearDay, UiButtonKind.Secondary, UiIcon.Trash, UiAction.Count, "reallife_ui_bizops_clear");
                Offer(_actions, Get<Button>(GameContract.SchedDayAuto, shown), DoAutoDay, UiButtonKind.Primary, UiIcon.Spark, UiAction.Alt, "bizman_schedule_auto_fill_day");
                Reword(DoCopyDay, "reallife_ui_bizops_copy");
                Reword(DoPasteDay, "reallife_ui_bizops_paste");
                Reword(DoPasteAll, "reallife_ui_bizops_paste_all");
                Reword(DoClearDay, "reallife_ui_bizops_clear");
                Reword(DoAutoDay, "bizman_schedule_auto_fill_day");
            }
            _m.Actions = _actions.ToArray();
        }

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

        // The people beside the board: while an hour is being filled, the game's picker as it sorted and filtered it; else the
        // place's staff as the game lists them, with their hours this week.
        private void People(BizManBusiness b)
        {
            BizOpsModel o = _m.Ops;
            ScheduleEmployeeSelection pick = PickBox(out ScheduleEmployeeScrollerController list);
            UiDataTableModel old = o.People;
            var t = new UiDataTableModel { Offset = old != null ? old.Offset : 0f, Reveal = old != null ? old.Reveal : -1, EmptyIcon = UiIcon.People };
            o.Picking = pick != null && pick.IsOpen && list != null;
            if (o.Picking)
            {
                string lane = Get<string>(GameContract.SchedPickLane, pick);
                object hourBox = GameContract.SchedPickHour.Field != null ? GameContract.SchedPickHour.Field.GetValue(pick) : null;
                int hour = hourBox is int hh ? hh : -1;
                string station = !string.IsNullOrEmpty(lane) && ScheduleHelper.WorkstationsById.TryGetValue(lane, out ItemInstance it) && it != null
                    ? (string.IsNullOrEmpty(it.alias) ? UiText.T(it.itemName) : it.alias) : null;
                o.PeopleTitle = station != null && hour >= 0 ? UiText.Fill("reallife_ui_bizops_pick_title", "station", station, "hour", hour.GetFormattedTime(0f)) : UiText.T("bizman_schedule_employee_header_no_workstation");
                o.PeopleLine = UiText.T("reallife_ui_bizops_pick_line");
                PickRows(list, t);
            }
            else
            {
                o.PeopleTitle = UiText.T("reallife_ui_bizops_people");
                o.PeopleLine = UiText.T(ScheduleHelper.CurrentScheduleDay.isOpen ? "reallife_ui_bizops_people_line" : "bizman_schedule_cant_assign_on_closed_days");
                StaffRows(t);
            }
            o.People = t;
        }

        // The place's staff as the game's schedule lists them: their face in their mood, their main skill graded, their hours this week, their pay.
        private static void StaffRows(UiDataTableModel t)
        {
            t.Columns = new[]
            {
                new UiDataColumn { Title = UiText.T("reallife_ui_bizops_col_person"), Kind = UiCellKind.Person },
                new UiDataColumn { Title = UiText.T("reallife_ui_bizops_col_skill"), Kind = UiCellKind.Figure, Width = 64f, Graded = true },
                new UiDataColumn { Title = UiText.T("reallife_ui_bizops_col_week"), Kind = UiCellKind.Figure, Width = 72f },
                new UiDataColumn { Title = UiText.T("reallife_ui_bizops_col_wage"), Kind = UiCellKind.Money, Width = 88f },
            };
            List<EmployeeInstance> staff = ScheduleHelper.Employees;
            int n = staff != null ? staff.Count : 0;
            t.Rows = new UiDataRow[n];
            for (int i = 0; i < n; i++)
            {
                EmployeeInstance e = staff[i];
                string skill = e.GetPrimarySkill();
                int level = string.IsNullOrEmpty(skill) ? 0 : Mathf.FloorToInt(e.GetSkillValue(skill));
                string name = e.characterData != null ? e.characterData.name : string.Empty;
                t.Rows[i] = new UiDataRow
                {
                    Cells = new[] { name, UiText.Whole(level) + "%", UiText.Fill("reallife_ui_bizops_hours_week", "hours", UiText.Whole(e.assignedWeeklyHours)), Wage(e) },
                    Values = new[] { 0, level, 0, 0 }, Sub = string.IsNullOrEmpty(skill) ? null : UiText.T(skill), Hue = UiColour.HueOf(name), Ring = StaffViews.Mood((int)e.satisfaction)
                };
            }
            t.RowCount = n;
            t.EmptyTitle = UiText.T("bizman_schedule_employee_no_employees");
        }

        private static string Wage(EmployeeInstance e) => UiText.Fill("bizman_schedule_hourly_wage", "wage", PaidWage.NowMoney(e.hourlyWage, e.id).ToShortCurrencyFormat());

        // The game's picker: its rows in its order (best fits first, its sort and filters applied), its search, its demand filter, its sorts as the heads.
        private void PickRows(ScheduleEmployeeScrollerController list, UiDataTableModel t)
        {
            t.Columns = new[]
            {
                new UiDataColumn { Title = UiText.T("reallife_ui_bizops_col_person"), Kind = UiCellKind.Person },
                new UiDataColumn { Title = UiText.T("reallife_ui_bizops_col_skill"), Kind = UiCellKind.Figure, Width = 64f, Graded = true, Sortable = true },
                new UiDataColumn { Title = UiText.T("reallife_ui_bizops_col_week"), Kind = UiCellKind.Figure, Width = 72f, Sortable = true },
                new UiDataColumn { Title = UiText.T("reallife_ui_bizops_col_fit"), Kind = UiCellKind.Chip, Width = 112f },
            };
            List<ScheduleEmployeeModel> data = list.data;
            int n = data != null ? data.Count : 0;
            t.Rows = new UiDataRow[n];
            for (int i = 0; i < n; i++)
            {
                ScheduleEmployeeModel p = data[i];
                bool demand = !string.IsNullOrEmpty(p.violatedDemandName), worn = p.overworkedDays != null && p.overworkedDays.Count > 0;
                string fit = !p.isAvailable ? "reallife_ui_bizops_fit_busy" : demand ? "reallife_ui_bizops_fit_demand" : worn ? "reallife_ui_bizops_fit_tired" : p.perfectMatch ? "reallife_ui_bizops_fit_best" : "reallife_ui_bizops_fit_free";
                UiRole tone = !p.isAvailable ? UiRole.Muted : demand || worn ? UiRole.Warn : UiRole.Ok;
                t.Rows[i] = new UiDataRow
                {
                    Cells = new[] { p.employeeName, UiText.Whole(p.primarySkillPercentage) + "%", UiText.Fill("reallife_ui_bizops_hours_week", "hours", UiText.Whole(p.hoursAssigned)), UiText.T(fit) },
                    Values = new[] { 0, p.primarySkillPercentage, 0, 0 }, Tones = new[] { UiRole.None, UiRole.None, UiRole.None, tone },
                    Sub = string.IsNullOrEmpty(p.primarySkillName) ? null : UiText.T(p.primarySkillName) + ", " + UiText.Fill("bizman_schedule_hourly_wage", "wage", PickWage(p).ToShortCurrencyFormat()),
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
            long sig = (int)day.day * 7 + (day.isOpen ? 1 : 0) + (_renaming != null ? 1000 : 0) + (_chosen != null ? _chosen.GetHashCode() : 0);
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
            List<ScheduleDayButton> days = Get<List<ScheduleDayButton>>(GameContract.SchedDayButtons, Get<ScheduleDaySelectionController>(GameContract.SchedDays, b.bizManSchedule));
            ScheduleDayButton shown = days != null ? DayButton(days, day) : null;
            sig = sig * 31 + Shows(Get<Button>(GameContract.SchedDayPaste, shown)) + Shows(Get<Button>(GameContract.SchedDayPasteAll, shown)) * 2 + Shows(Get<Button>(GameContract.SchedDayAuto, shown)) * 4;
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
                    return arg >= 0 && arg < days.Count && Click(Get<Button>(GameContract.SchedDaySelect, days[arg]), "the schedule's day " + (arg + 1));
                case UiBoard.RulerCommand: return ToggleHour(sch, arg);
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
                case BizManPages.OpsAction: return DayAction(days, arg);
                case BizManPages.OpsCancelFill:
                    return Click(Get<Button>(GameContract.SchedFillerCancel, sch.scheduleAutoFillerUI), "cancel the auto fill");
                case BizManPages.OpsSearch: return true;   // typing: the host opens the field
                case BizManPages.People + UiDataTable.Row: return Pick(arg);
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

        // An hour of the ruler opened or closed by the game's own hour toggle, as a press on it does (Shift held stretches it as the game's does).
        private static bool ToggleHour(BizManSchedule sch, int hour)
        {
            List<ScheduleHourToggle> hours = Get<List<ScheduleHourToggle>>(GameContract.SchedHours, Get<ScheduleHourToggleGroup>(GameContract.SchedHourGroup,
                Get<ScheduleHeaderController>(GameContract.SchedHeader, sch)));
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
            return what == DoAutoDay + 100 ? Click(Get<Button>(GameContract.SchedWeekAuto, days), "auto fill the week") : Click(Calling(days, "OnClearScheduleClick"), "clear the week (the game asks first)");
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

        // Back on the schedule: the name typed is done, the picker closes as the game's own Escape closes it, a chosen shift
        // is let go; false when nothing of the schedule was open.
        private bool ScheduleBack()
        {
            if (_renaming != null || _chosen != null)
            {
                _renaming = null;
                _chosen = null;
                return true;
            }
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
