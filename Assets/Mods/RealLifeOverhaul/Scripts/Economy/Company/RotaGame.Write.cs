using System;
using System.Collections.Generic;
using BigAmbitions.Items;
using Buildings.Office.Headquarters;
using Entities;
using Helpers;
using PlayerActivity;
using UI;
using UI.Smartphone.Apps.BizMan.Schedule;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// Writing a rota and opening hours into the game, and putting a kept week back, as <see cref="RotaLedger"/>
    /// decides: the only place the planner changes the game. Cost: on a button only, one pass over shifts and staff.
    /// </summary>
    internal static partial class RotaGame
    {
        private static readonly List<RotaShift> Planned = new List<RotaShift>(64);
        private static readonly HashSet<string> Had = new HashSet<string>();
        private static bool[] _stationThere = new bool[0], _workerThere = new bool[0];
        private static readonly bool[] DayOpen = new bool[RotaCatalogue.Days];

        /// <summary>A copy of a business's week to put back: each day's switch, hours and shifts.</summary>
        public static RotaUndo Keep(RotaPlace place, bool kept)
        {
            var undo = new RotaUndo { Place = place.Place, Kept = kept };
            List<ScheduleDay> week = place.Reg.scheduleDays;
            for (int i = 0; i < RotaCatalogue.Days; i++)
            {
                ScheduleDay day = i < week.Count ? week[i] : null;
                undo.Open[i] = day != null && day.isOpen;
                undo.Slots[i] = new List<OpeningHourSlot>(2);
                undo.Shifts[i] = new List<WorkShift>(8);
                for (int k = 0; day != null && day.openingHourSlots != null && k < day.openingHourSlots.Count; k++)
                {
                    undo.Slots[i].Add(day.openingHourSlots[k].Clone());
                }
                for (int k = 0; day != null && day.workShifts != null && k < day.workShifts.Count; k++)
                {
                    undo.Shifts[i].Add(day.workShifts[k].Clone());
                }
            }
            return undo;
        }

        /// <summary>
        /// Puts a kept week back: its hours, and every shift whose employee still works there at a station still
        /// standing; those the planner never places keep today's shifts. Returns the shifts put back.
        /// </summary>
        public static int PutBack(RotaPlace place, RotaUndo undo)
        {
            BuildingRegistration reg = place.Reg;
            BizManSchedule.AbortAutoFillForBusiness(reg);
            List<ScheduleDay> week = reg.scheduleDays;
            int back = 0;
            Had.Clear();
            for (int i = 0; i < RotaCatalogue.Days && i < week.Count; i++)
            {
                ScheduleDay day = week[i];
                day.isOpen = undo.Open[i];
                day.workShifts = day.workShifts ?? new List<WorkShift>();
                day.openingHourSlots = new List<OpeningHourSlot>(undo.Slots[i].Count);
                for (int k = 0; k < undo.Slots[i].Count; k++)
                {
                    day.openingHourSlots.Add(undo.Slots[i][k].Clone());
                }
                for (int k = day.workShifts.Count - 1; k >= 0; k--)
                {
                    WorkShift shift = day.workShifts[k];
                    if (shift == null || !RotaLedger.Stays(Coded(shift, reg)))
                    {
                        if (shift != null && !string.IsNullOrEmpty(shift.employeeId))
                        {
                            Had.Add(shift.employeeId);
                        }
                        day.workShifts.RemoveAt(k);
                    }
                }
                for (int k = 0; k < undo.Shifts[i].Count; k++)
                {
                    WorkShift shift = undo.Shifts[i][k];
                    if (shift != null && RotaLedger.Restores(Coded(shift, reg)))
                    {
                        day.AddWorkShift(shift.Clone());
                        back++;
                    }
                }
            }
            EmployeeHelper.GetEmployeeInstances(new EmployeeInstancesQueryInfo { withAssignedAddress = reg.Address, withoutSkills = Drivers }, People);
            After(reg, People, null);
            People.Clear();
            Had.Clear();
            return back;
        }

        // A shift in the ledger's terms without a sheet: still here, left alone or gone; its station there or gone.
        private static RotaShift Coded(WorkShift shift, BuildingRegistration reg)
        {
            EmployeeInstance e = string.IsNullOrEmpty(shift.employeeId) ? null : EmployeeHelper.GetEmployeeById(shift.employeeId, false);
            bool left = string.IsNullOrEmpty(shift.employeeId) || (e != null && e.HasSkill(Drivers[0]));
            return new RotaShift
            {
                From = shift.startingHour, To = shift.endingHour,
                Worker = left ? RotaLedger.Unread : Here(e, reg) ? 0 : RotaLedger.Gone,
                Station = string.IsNullOrEmpty(shift.itemInstanceId) || reg.itemInstances.ContainsKey(shift.itemInstanceId) ? 0 : RotaLedger.Gone
            };
        }

        private static bool Here(EmployeeInstance e, BuildingRegistration reg)
        {
            return e != null && e.assignedAddress != null && e.assignedAddress.streetNumber == reg.StreetNumber && e.assignedAddress.streetName == reg.StreetName;
        }

        /// <summary>
        /// Writes a rota, and with <paramref name="hours"/> the opening hours first, as <see cref="RotaLedger"/>
        /// decides; then does what the game does after its own auto fill (ScheduleAutoFiller.ApplyResult,
        /// ScheduleAutoFillerHelper.OnCompleted) and when its schedule screen closes (BizManSchedule.OnDisable). Left
        /// out on purpose: OnCompleted's clearing of headquarters plans and of a purchasing agent's partnership, which
        /// the game has no call to give back, so nobody holding one is unassigned. Returns the shifts written.
        /// </summary>
        public static int Write(RotaSheet sheet, RotaPlan plan, bool[] hours)
        {
            BuildingRegistration reg = sheet.Reg;
            BizManSchedule.AbortAutoFillForBusiness(reg);
            Index(sheet);
            int stations = sheet.Stations.Count, workers = sheet.Workers.Count, written = 0;
            _stationThere = _stationThere.Length < stations ? new bool[stations] : _stationThere;
            _workerThere = _workerThere.Length < workers ? new bool[workers] : _workerThere;
            for (int s = 0; s < stations; s++)
            {
                ItemInstance there;
                _stationThere[s] = reg.itemInstances.TryGetValue(sheet.Stations[s].id, out there) && ReferenceEquals(there, sheet.Stations[s]);
            }
            for (int w = 0; w < workers; w++)
            {
                _workerThere[w] = ReferenceEquals(EmployeeHelper.GetEmployeeById(sheet.Workers[w].id, false), sheet.Workers[w]) && Here(sheet.Workers[w], reg);
            }
            Had.Clear();
            List<ScheduleDay> week = reg.scheduleDays;
            Array.Clear(DayOpen, 0, DayOpen.Length);
            for (int i = 0; i < week.Count; i++)
            {
                ScheduleDay day = week[i];
                int d = day != null ? (int)day.day - 1 : -1;
                if (d < 0 || d >= RotaCatalogue.Days)
                {
                    continue;
                }
                if (hours != null)
                {
                    Hours(day, hours, d);
                }
                DayOpen[d] = day.isOpen;
                day.workShifts = day.workShifts ?? new List<WorkShift>();
                for (int k = day.workShifts.Count - 1; k >= 0; k--)
                {
                    WorkShift shift = day.workShifts[k];
                    if (shift != null && RotaLedger.Removes(Coded(shift, reg, d), sheet.Fixed))
                    {
                        Had.Add(shift.employeeId);
                        day.workShifts.RemoveAt(k);
                    }
                }
            }
            RotaLedger.Shifts(plan, DayOpen, _stationThere, _workerThere, sheet.Fixed, Planned);
            for (int i = 0; i < week.Count; i++)
            {
                ScheduleDay day = week[i];
                int d = day != null ? (int)day.day - 1 : -1;
                for (int k = 0; d >= 0 && k < Planned.Count; k++)
                {
                    RotaShift shift = Planned[k];
                    if (shift.Day != d)
                    {
                        continue;
                    }
                    day.AddWorkShift(new WorkShift
                    {
                        startingHour = shift.From, endingHour = shift.To, employeeId = sheet.Workers[shift.Worker].id,
                        itemInstanceId = sheet.Stations[shift.Station].id,
                        type = sheet.Problem.Kind[shift.Station] == RotaKind.Cleaning ? WorkShiftType.Cleaning : WorkShiftType.Default
                    });
                    written++;
                }
            }
            Planned.Clear();
            After(reg, sheet.Workers, sheet.Fixed);
            Had.Clear();
            return written;
        }

        // A shift in the ledger's terms by the sheet at hand (after Index): indices, or there but not read, or gone.
        private static RotaShift Coded(WorkShift shift, BuildingRegistration reg, int d)
        {
            int w, s;
            var coded = new RotaShift { Day = d, From = shift.startingHour, To = shift.endingHour, Worker = RotaLedger.Unread, Station = RotaLedger.Unread };
            if (!string.IsNullOrEmpty(shift.employeeId) && WorkerAt.TryGetValue(shift.employeeId, out w))
            {
                coded.Worker = w;
            }
            if (shift.itemInstanceId != null && StationAt.TryGetValue(shift.itemInstanceId, out s))
            {
                coded.Station = s;
            }
            else if (!string.IsNullOrEmpty(shift.itemInstanceId) && !reg.itemInstances.ContainsKey(shift.itemInstanceId))
            {
                coded.Station = RotaLedger.Gone;
            }
            return coded;
        }

        // Blocks of whole hours in order, never touching, as the game's hour switches leave them; a day with none is
        // closed and keeps its hours, as the game's own switch leaves it (ScheduleHourToggleGroup.OnHourToggleChange).
        private static void Hours(ScheduleDay day, bool[] hours, int d)
        {
            List<OpeningHourSlot> slots = null;
            for (int h = 0; h < RotaCatalogue.Hours;)
            {
                if (!hours[d * RotaCatalogue.Hours + h])
                {
                    h++;
                    continue;
                }
                int from = h;
                while (h < RotaCatalogue.Hours && hours[d * RotaCatalogue.Hours + h])
                {
                    h++;
                }
                slots = slots ?? new List<OpeningHourSlot>(2);
                slots.Add(new OpeningHourSlot(from, h));
            }
            day.isOpen = slots != null;
            if (slots != null)
            {
                day.openingHourSlots = slots;
            }
        }

        // What the game does after its own auto fill and when its schedule screen closes (see Write). fixedWorker (may
        // be null): who of staff was left as they were, so nothing of theirs is worked out again.
        private static void After(BuildingRegistration reg, List<EmployeeInstance> staff, bool[] fixedWorker)
        {
            for (int i = 0; i < staff.Count; i++)
            {
                EmployeeInstance e = staff[i];
                if (e == null || (fixedWorker != null && fixedWorker[i]) || !Here(e, reg))
                {
                    continue;
                }
                try
                {
                    e.UpdateWeeklyHoursAndDays();
                    e.UpdateAssignedWorkStationItems();
                    if (RotaLedger.Idles(Had.Contains(e.id), e.IsAssignedToAnyWorkShift(), HoldsPlan(e)))
                    {
                        e.UnAssignWork();
                        e.AddTodoTask(TodoTaskType.EmployeeIdle);
                    }
                }
                catch (Exception fault)
                {
                    Logger?.Error(fault);   // one employee's figures: the rest are still brought up to date
                }
            }
            UIs uis = InstanceBehavior<UIs>.Instance;
            if (uis != null && uis.tasksUI != null)
            {
                uis.tasksUI.forceCheckForCompletedTodoTasks = true;
            }
            Guarded("the security level", () => reg.UpdateSecurityLevel());
            Guarded("the game's schedule screen", () =>
            {
                BizManBusiness shown = uis != null && uis.fullMenu != null && uis.fullMenu.bizMan != null ? uis.fullMenu.bizMan.business : null;
                if (shown != null && shown.isActiveAndEnabled && shown.buildingRegistration == reg)
                {
                    shown.UpdateSecurityInfo();
                    BizManSchedule schedule = uis.fullMenu.schedule;
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
            Guarded("today's customers and alerts", () =>
            {
                CustomerDraw.Redraw(reg);   // the game's own redraw (Core/CustomerDraw keeps the hour under way with Meaningful Ambience on)
                BusinessHelper.CheckIfTheaterHasNoActors(reg);
                if (!BusinessHelper.IsMissingEmployeeTaskActive(reg))
                {
                    BusinessHelper.ForceRecheckMissingEmployeeAlert(reg);
                }
                GlobalEvents.onBuildingRegistrationChange?.Invoke(reg.Address);
                var work = uis != null && uis.playerActivityUI != null ? uis.playerActivityUI.GetCurrentActivity as WorkActivity : null;
                if (work != null)
                {
                    work.SetTimeToWork();
                    uis.playerActivityUI.UpdateActivityDisplay();
                }
            });
            SaveGameManager.MarkChange();
        }

        private static bool HoldsPlan(EmployeeInstance e)
        {
            return HeadhunterHelper.GetAssignedPlanForHeadhunter(e.id) != null || HrManagerHelper.GetAssignedPlanForHrManager(e.id) != null
                   || LogisticsManagerHelper.GetAssignedPlanForEmployee(e.id) != null || PricingManagerHelper.GetAssignedPlanForPricingManager(e.id) != null
                   || PurchasingAgentHelper.GetAssignedPlanForPurchasingAgent(e.id) != null;
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
                Logger?.Warn("Rota: " + what + " could not be brought up to date after the rota was written; the rota itself is in place.");
            }
        }

        /// <summary>True if a fill would take out a shift: the player is asked before it is written over.</summary>
        public static bool HasRota(RotaSheet sheet)
        {
            Index(sheet);
            List<ScheduleDay> week = sheet.Reg.scheduleDays;
            for (int i = 0; i < week.Count; i++)
            {
                for (int k = 0; week[i] != null && week[i].workShifts != null && k < week[i].workShifts.Count; k++)
                {
                    WorkShift shift = week[i].workShifts[k];
                    if (shift != null && RotaLedger.Removes(Coded(shift, sheet.Reg, i), sheet.Fixed))
                    {
                        return true;
                    }
                }
            }
            return false;
        }
    }
}
