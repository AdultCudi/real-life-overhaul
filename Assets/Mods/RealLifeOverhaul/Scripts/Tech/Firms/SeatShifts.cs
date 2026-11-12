using System;
using System.Collections.Generic;
using BAModAPI;
using BigAmbitions.DayNightCycle;
using BigAmbitions.Items;
using Buildings.Schedule;
using Entities;
using Entities.Employee.JobDemands;
using Entities.Employee.JobDemands.Requirements;
using UI;
using UI.Smartphone.Apps.BizMan.Schedule;
using UnityEngine;

namespace RealLifeOverhaul.Tech
{
    /// <summary>
    /// The one place the seat rule (SeatPlan) touches the game's schedule: a business's open hours, desks and shifts are read once for a run of hires
    /// (Begin), each hire is planned against them and given their shifts as the game's own schedule screen writes one (ScheduleDay.AddWorkShift, then the
    /// employee's weekly hours, days and stations brought up to date, as ScheduleHelper does), and the business is told once at the end (End: the idle task
    /// checked, the schedule screen reloaded if it shows this business, the game's events). A demand of the hire's the game can test (HoursWorkingPerWeek,
    /// DaysWorkingPerWeek, FreeOnDays, HasNoShiftBetweenHours, NoSpecificShift) is asked of the demand itself against the week tried, on a week of the hire's
    /// shifts alone; a wish to work at an item (WorksOnItem) decides which desk is preferred. Nothing of another employee's is changed. Cost: on an event
    /// (a hire, an hour with one waiting), a pass over the business's shifts and desks; nothing per frame.
    /// </summary>
    internal static class SeatShifts
    {
        public static IModLogger Logger;

        private const string Cleaning = "ba:skill_cleaning";
        private const int DemandWeightLeast = 1;   // a demand always weighs something, so a week that breaks one is never as good as one that breaks none

        private static readonly SeatSpace Room = new SeatSpace();
        private static readonly SeatShift[] Week = new SeatShift[SeatSpace.Days];
        private static readonly List<ItemInstance> Items = new List<ItemInstance>(64), Stations = new List<ItemInstance>(64);
        private static readonly Dictionary<string, int> StationAt = new Dictionary<string, int>(64);
        private static readonly List<JobDemand> Kept = new List<JobDemand>(8), OnItem = new List<JobDemand>(4);
        private static readonly WorkStationInfo Info = new WorkStationInfo();
        private static readonly List<ScheduleDay> Mini = new List<ScheduleDay>(SeatSpace.Days);
        private static readonly WorkShift[] MiniShift = new WorkShift[SeatSpace.Days];
        private static readonly SeatJudge Judge = JudgeWeek;
        private static BuildingRegistration _reg;
        private static EmployeeInstance _who;
        private static bool _written;

        /// <summary>
        /// Reads a business for the hires to be seated in it: its open hours, its desks (every assignable item that is no cleaning station) and every
        /// hour of a desk that a shift already has. Nothing is changed.
        /// </summary>
        public static void Begin(BuildingRegistration reg)
        {
            _reg = reg;
            _written = false;
            Items.Clear();
            Stations.Clear();
            StationAt.Clear();
            reg.GetAssignableItems(Items);
            for (int i = 0; i < Items.Count; i++)
            {
                ItemInstance item = Items[i];
                Item data = item != null ? item.ItemCached : null;
                if (data != null && data.suitableSkills != null && Array.IndexOf(data.suitableSkills, Cleaning) < 0 && !StationAt.ContainsKey(item.id))
                {
                    StationAt.Add(item.id, Stations.Count);
                    Stations.Add(item);
                }
            }
            Room.Size(Stations.Count);
            List<ScheduleDay> week = reg.scheduleDays;
            for (int i = 0; week != null && i < week.Count; i++)
            {
                ScheduleDay day = week[i];
                int d = day != null ? (int)day.day - 1 : -1;
                if (d < 0 || d >= SeatSpace.Days)
                {
                    continue;
                }
                for (int h = 0; h < SeatSpace.Hours; h++)
                {
                    Room.Open[d * SeatSpace.Hours + h] = day.IsOpenAtHour(h);
                }
                for (int k = 0; day.workShifts != null && k < day.workShifts.Count; k++)
                {
                    WorkShift shift = day.workShifts[k];
                    if (shift != null && !string.IsNullOrEmpty(shift.itemInstanceId) && StationAt.TryGetValue(shift.itemInstanceId, out int station))
                    {
                        Room.Take(station, d, shift.startingHour, shift.endingHour);
                    }
                }
            }
            Room.Seal();
        }

        /// <summary>
        /// Seats one employee in the business last read: <see cref="SeatPlan.Seated"/> with their shifts written, or <see cref="SeatPlan.Closed"/> (no open
        /// hour), <see cref="SeatPlan.NoDesk"/> (no desk they may use) or <see cref="SeatPlan.Full"/> (no week of enough hours in the hours free), with nothing
        /// written. <paramref name="unmet"/> is the weight of the schedule demands the week breaks, 0 when it keeps every one. <paramref name="week"/> is the
        /// hours a week the plan was made for (their demands read again), which the answer is remembered by.
        /// </summary>
        public static int Seat(EmployeeInstance e, out int shifts, out int hours, out int unmet, out int week)
        {
            shifts = 0;
            hours = 0;
            unmet = 0;
            week = 0;
            _who = e;
            int result = SeatPlan.Full;
            try
            {
                week = ReadDemands(e);
                for (int s = 0; s < Stations.Count; s++)
                {
                    Item data = Stations[s].ItemCached;
                    bool usable = data.suitableSkills != null && Array.Exists(data.suitableSkills, e.HasSkill) && !ItemHelper.HasAnyMissingRequirements(Stations[s]);
                    Room.Usable[s] = usable;
                    Room.Rank[s] = usable ? RankOf(s) : 0;
                }
                result = SeatPlan.Plan(Room, week, Judge, Week, out int count, out unmet);
                if (result == SeatPlan.Seated)
                {
                    Write(e, count, out shifts, out hours);
                }
            }
            finally
            {
                Info.itemInstance = null;
                _who = null;
                e.UpdateWeeklyHoursAndDays();   // the plan was tried on weeks of its own: the employee's figures are the business's again
            }
            return result;
        }

        /// <summary>The business is done with: the game is told once if anything was written, and what was read is let go.</summary>
        public static void End()
        {
            BuildingRegistration reg = _reg;
            bool written = _written;
            _reg = null;
            _who = null;
            _written = false;
            Items.Clear();
            Stations.Clear();
            StationAt.Clear();
            Kept.Clear();
            OnItem.Clear();
            Info.itemInstance = null;
            if (written && reg != null)
            {
                After(reg);
            }
        }

        /// <summary>
        /// The hours a week the employee's demands leave, which the week for them is planned for: a full week of the usual forty, held between the least and the most
        /// any demand of hours allows. Read from the demands alone, with no business and no schedule, so it can be asked of each of thousands of professionals;
        /// the rounds remember what a business answered by it, since a seat for a part-timer is not a seat for a full week.
        /// </summary>
        public static int WeekHoursFor(EmployeeInstance e)
        {
            int least = 0, most = int.MaxValue;
            for (int i = 0; e != null && e.demands != null && i < e.demands.Count; i++)
            {
                var hoursWanted = DemandOf(e.demands[i]) as HoursWorkingPerWeek;
                if (hoursWanted != null)
                {
                    BoundHours(hoursWanted, ref least, ref most);
                }
            }
            return HoursOf(least, most);
        }

        // The game's demand of a name, null for one this build does not know: the week is made without it.
        private static JobDemand DemandOf(string name)
        {
            try
            {
                return JobDemandHelper.GetByName(name);
            }
            catch (Exception)
            {
                return null;
            }
        }

        // What a demand of hours a week asks of the week: no less than its least, no more than its most.
        private static void BoundHours(HoursWorkingPerWeek demand, ref int least, ref int most)
        {
            least = Math.Max(least, demand.MinHours);
            most = demand.MaxHours > 0 ? Math.Min(most, demand.MaxHours) : most;
        }

        // A full week of the usual forty, held between the least and the most the demands of hours allow.
        private static int HoursOf(int least, int most) => Math.Min(Math.Max(TalentCatalogue.WeekHours, least), Math.Max(least, most));

        // The employee's demands sorted: the schedule demands the judge asks, the wishes to work at an item, and the hours a week the demands leave.
        private static int ReadDemands(EmployeeInstance e)
        {
            Kept.Clear();
            OnItem.Clear();
            int least = 0, most = int.MaxValue;
            for (int i = 0; e.demands != null && i < e.demands.Count; i++)
            {
                JobDemand demand = DemandOf(e.demands[i]);
                if (demand == null)
                {
                    continue;
                }
                if (demand is WorksOnItem)
                {
                    OnItem.Add(demand);
                    continue;
                }
                if (!(demand is IScheduleDemand))
                {
                    continue;
                }
                Kept.Add(demand);
                var hoursWanted = demand as HoursWorkingPerWeek;
                if (hoursWanted != null)
                {
                    BoundHours(hoursWanted, ref least, ref most);
                }
            }
            return HoursOf(least, most);
        }

        // 0 for a desk that every wish of the employee to work at an item accepts, 1 for one that one of them refuses (still a desk they may use).
        private static int RankOf(int station)
        {
            ItemInstance item = Stations[station];
            for (int i = 0; i < OnItem.Count; i++)
            {
                var filter = OnItem[i] as IWorkStationFilter;
                if (filter == null)
                {
                    continue;
                }
                Info.id = item.id;
                Info.itemInstance = item;
                Info.skillsRequired = item.ItemCached.suitableSkills;
                Info.workShiftType = WorkShiftType.Default;
                if (!filter.AcceptsWorkStation(Info, _reg))
                {
                    return 1;
                }
            }
            return 0;
        }

        // The weight of the schedule demands a week of shifts breaks, each asked of the demand itself against a week holding the employee's shifts alone.
        private static int JudgeWeek(SeatShift[] week, int count)
        {
            if (Mini.Count == 0)
            {
                for (int d = 0; d < SeatSpace.Days; d++)
                {
                    Mini.Add(new ScheduleDay { day = (DayOfWeekOrdered)(d + 1), isOpen = true });
                    MiniShift[d] = new WorkShift { itemInstanceId = string.Empty, type = WorkShiftType.Default };
                }
            }
            for (int d = 0; d < SeatSpace.Days; d++)
            {
                Mini[d].workShifts.Clear();
            }
            for (int i = 0; i < count; i++)
            {
                WorkShift shift = MiniShift[week[i].Day];
                shift.employeeId = _who.id;
                shift.startingHour = week[i].From;
                shift.endingHour = week[i].To;
                Mini[week[i].Day].workShifts.Add(shift);
            }
            _who.UpdateWeeklyHoursAndDays(Mini);
            int unmet = 0;
            for (int i = 0; i < Kept.Count; i++)
            {
                try
                {
                    if (!Kept[i].Fulfilled(_who, Mini))
                    {
                        unmet += Math.Max(DemandWeightLeast, Mathf.RoundToInt(Kept[i].Impact * 10f));   // 2, 3 and 5 for a low, medium and high demand
                    }
                }
                catch (Exception)
                {
                    unmet += DemandWeightLeast;
                }
            }
            return unmet;
        }

        // The week's shifts into the business's days, the hours taken at their desks, and the employee's figures and stations brought up to date.
        private static void Write(EmployeeInstance e, int count, out int shifts, out int hours)
        {
            shifts = 0;
            hours = 0;
            if (!_written)
            {
                BizManSchedule.AbortAutoFillForBusiness(_reg);   // a fill of the game's still working on this business would write over these shifts
            }
            for (int i = 0; i < count; i++)
            {
                SeatShift w = Week[i];
                ScheduleDay day = DayOf(w.Day);
                if (day == null || w.Station < 0 || w.Station >= Stations.Count)
                {
                    continue;
                }
                day.workShifts = day.workShifts ?? new List<WorkShift>();
                day.AddWorkShift(new WorkShift
                {
                    startingHour = w.From, endingHour = w.To, employeeId = e.id, itemInstanceId = Stations[w.Station].id, type = WorkShiftType.Default
                });
                Room.Take(w.Station, w.Day, w.From, w.To);
                shifts++;
                hours += w.To - w.From;
            }
            _written |= shifts > 0;
            e.UpdateWeeklyHoursAndDays();
            e.UpdateAssignedWorkStationItems();
        }

        private static ScheduleDay DayOf(int d)
        {
            List<ScheduleDay> week = _reg.scheduleDays;
            for (int i = 0; week != null && i < week.Count; i++)
            {
                if (week[i] != null && (int)week[i].day - 1 == d)
                {
                    return week[i];
                }
            }
            return null;
        }

        // What the game does when its schedule screen or its auto fill has changed shifts (ScheduleHelper.UpdateEmployeeAfterWorkShiftChange,
        // ScheduleAutoFillerHelper.OnCompleted): the idle tasks checked, the screen if it shows this business loaded again, the events, the save marked.
        private static void After(BuildingRegistration reg)
        {
            UIs uis = InstanceBehavior<UIs>.Instance;
            if (uis != null && uis.tasksUI != null)
            {
                uis.tasksUI.forceCheckForCompletedTodoTasks = true;
            }
            Guarded("the game's schedule screen", () =>
            {
                var shown = uis != null && uis.fullMenu != null && uis.fullMenu.bizMan != null ? uis.fullMenu.bizMan.business : null;
                if (shown != null && shown.isActiveAndEnabled && shown.buildingRegistration == reg)
                {
                    var schedule = uis.fullMenu.schedule;
                    if (schedule != null && schedule.isActiveAndEnabled)
                    {
                        schedule.LoadScheduler();
                    }
                }
            });
            Guarded("the game's schedule events", () =>
            {
                ScheduleHelper.OnWorkShiftChanged?.Invoke();
                GameEvent.Invoke("ba:gameevent_changedbuildingregistration");
            });
            SaveGameManager.MarkChange();
        }

        // One of the game's follow-ups: a fault is logged and the rest still run.
        private static void Guarded(string what, Action work)
        {
            try
            {
                work();
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                Logger?.Warn("Tech: " + what + " could not be brought up to date after a hire was seated; the shifts themselves are in place.");
            }
        }
    }
}
