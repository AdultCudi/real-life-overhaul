using System;
using System.Collections.Generic;
using AI.Customers.CustomerEntries;
using BAModAPI;
using BigAmbitions.Characters.Skills;
using BigAmbitions.DayNightCycle;
using BigAmbitions.Items;
using BigAmbitions.Tags;
using Buildings;
using Buildings.Office.Headquarters;
using Buildings.Retail.Businesses.CinemaTheater;
using Buildings.Schedule;
using Entities;
using Entities.Employee.JobDemands;
using Entities.Employee.JobDemands.Requirements;
using Helpers;
using Localizor;
using PlayerActivity;
using UI;
using UI.Smartphone.Apps.BizMan;
using UI.Smartphone.Apps.BizMan.Schedule;
using UnityEngine;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// Writing a rota and opening hours into the game, keeping the week that was there, and putting it back: the
    /// only place the planner changes anything of the game's.
    /// </summary>
    internal static partial class RotaGame
    {
        // ---------------------------------------------------------------- the rota as it was

        /// <summary>A copy of a business's week, to put back.</summary>
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
        /// Puts a kept week back: its hours, and every shift whose employee still works there and whose station is
        /// still in the building. The shifts of those the planner never places (delivery drivers) are not touched:
        /// they stay as they are today. Returns how many shifts are back.
        /// </summary>
        public static int PutBack(RotaPlace place, RotaUndo undo)
        {
            BuildingRegistration reg = place.Reg;
            BizManSchedule.AbortAutoFillForBusiness(reg);
            List<ScheduleDay> week = reg.scheduleDays;
            int back = 0;
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
                    if (day.workShifts[k] == null || !Left(day.workShifts[k]))
                    {
                        day.workShifts.RemoveAt(k);
                    }
                }
                for (int k = 0; k < undo.Shifts[i].Count; k++)
                {
                    WorkShift shift = undo.Shifts[i][k];
                    EmployeeInstance e = Left(shift) ? null : EmployeeHelper.GetEmployeeById(shift.employeeId, false);
                    bool here = e != null && e.assignedAddress != null && e.assignedAddress.streetNumber == reg.StreetNumber && e.assignedAddress.streetName == reg.StreetName;
                    if (here && (string.IsNullOrEmpty(shift.itemInstanceId) || reg.itemInstances.ContainsKey(shift.itemInstanceId)))
                    {
                        day.AddWorkShift(shift.Clone());
                        back++;
                    }
                }
            }
            EmployeeHelper.GetEmployeeInstances(new EmployeeInstancesQueryInfo { withAssignedAddress = reg.Address }, People);
            After(reg, People, true);
            People.Clear();
            return back;
        }

        // A shift the planner leaves alone: nobody's, or a delivery driver's.
        private static bool Left(WorkShift shift)
        {
            if (shift == null || string.IsNullOrEmpty(shift.employeeId))
            {
                return true;
            }
            EmployeeInstance e = EmployeeHelper.GetEmployeeById(shift.employeeId, false);
            return e != null && e.HasSkill(Drivers[0]);
        }

        // ---------------------------------------------------------------- writing a rota

        /// <summary>
        /// Writes a rota into the game, and with <paramref name="hours"/> the opening hours first.
        ///
        /// As the game's own auto fill does when it applies its result (ScheduleAutoFiller.ApplyResult): the shifts
        /// of the people it places are taken out of every day and the new ones put in, each employee's weekly hours,
        /// days and stations are worked out again, and then (ScheduleAutoFillerHelper.OnCompleted) whoever has no
        /// shift left is unassigned from work and gets the game's "idle" task, the security level and a
        /// headquarters' plans are brought up to date, the game's schedule screen is loaded again if it shows this
        /// business, and the game's two events go out. As the game's schedule screen does when it closes
        /// (BizManSchedule.OnDisable): today's customers are drawn again for the hours as they now are, and the
        /// "no employee" and "no actors" alerts are looked at again. A fill of the game's own that is still
        /// running for the business is stopped first, as the game stops it before anything else changes a business.
        /// Shifts of anybody the planner does not manage stay exactly as they are.
        /// </summary>
        public static int Write(RotaSheet sheet, RotaPlan plan, bool[] hours)
        {
            BuildingRegistration reg = sheet.Place.Reg;
            BizManSchedule.AbortAutoFillForBusiness(reg);
            Index(sheet);
            List<ScheduleDay> week = reg.scheduleDays;
            int stations = sheet.Stations.Count, written = 0;
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
                day.workShifts = day.workShifts ?? new List<WorkShift>();
                for (int k = day.workShifts.Count - 1; k >= 0; k--)
                {
                    WorkShift shift = day.workShifts[k];
                    if (shift == null || (!string.IsNullOrEmpty(shift.employeeId) && WorkerAt.ContainsKey(shift.employeeId)))
                    {
                        day.workShifts.RemoveAt(k);
                    }
                }
                for (int s = 0; s < stations && day.isOpen; s++)
                {
                    WorkShift shift = null;
                    int holder = -1;
                    for (int h = 0; h < RotaCatalogue.Hours; h++)
                    {
                        int w = plan.At[(d * RotaCatalogue.Hours + h) * stations + s];
                        if (w < 0 || w != holder)
                        {
                            shift = null;
                        }
                        holder = w;
                        if (w < 0)
                        {
                            continue;
                        }
                        if (shift == null)
                        {
                            shift = new WorkShift
                            {
                                startingHour = h, endingHour = h + 1, employeeId = sheet.Workers[w].id, itemInstanceId = sheet.Stations[s].id,
                                type = sheet.Problem.Kind[s] == RotaKind.Cleaning ? WorkShiftType.Cleaning : WorkShiftType.Default
                            };
                            day.AddWorkShift(shift);
                            written++;
                        }
                        else
                        {
                            shift.endingHour = h + 1;
                        }
                    }
                }
            }
            After(reg, sheet.Workers, hours != null);
            return written;
        }

        // A day's opening hours from the planner's hours: blocks of whole hours inside the day, in order, never touching,
        // as the game's own hour switches leave them (ScheduleHourToggleGroup.OnHourToggleChange). A day with none is closed
        // and keeps the hours it had, as a day closed by the game's own switch does.
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

        // Everything the game does after its own auto fill and after its schedule screen closes; see Write.
        private static void After(BuildingRegistration reg, List<EmployeeInstance> staff, bool hoursChanged)
        {
            for (int i = 0; i < staff.Count; i++)
            {
                EmployeeInstance e = staff[i];
                try
                {
                    e.UpdateWeeklyHoursAndDays();
                    e.UpdateAssignedWorkStationItems();
                    if (!e.IsAssignedToAnyWorkShift() && !e.HasSkill(Drivers[0]))
                    {
                        e.UnAssignWork();
                        e.AddTodoTask(TodoTaskType.EmployeeIdle);
                    }
                }
                catch (Exception fault)
                {
                    Logger?.Error(fault);   // (one employee's figures; the others are still brought up to date)
                }
            }
            UIs uis = InstanceBehavior<UIs>.Instance;
            if (uis != null && uis.tasksUI != null)
            {
                uis.tasksUI.forceCheckForCompletedTodoTasks = true;
            }
            reg.UpdateSecurityLevel();
            if (reg.businessTypeName == Headquarters)
            {
                HeadquartersPlans(reg.Address);
            }
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
                CustomerEntriesHelper.UpdateCustomerEntriesForPlayerBusiness(reg, TimeHelper.GetDayOfWeek());
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

        // One of the game's own follow-ups: a fault in it is logged and the rest still run, so that a rota already written is never left half looked after.
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

        // A headquarters' plans lose whoever no longer has a shift, plan by plan as the game's own ScheduleHelper.UpdateHQPlans does it.
        // (That one takes the game's own screen object for the business; this takes the address, and is otherwise the same calls.)
        private static void HeadquartersPlans(Address address)
        {
            foreach (HeadhunterPlan plan in HeadhunterHelper.GetAssignedPlansForHeadquarters(address))
            {
                if (plan.HeadhunterInstance != null && !plan.HeadhunterInstance.IsAssignedToAnyWorkShift())
                {
                    plan.assignedEmployeeId = null;
                }
            }
            foreach (HrManagerPlan plan in HrManagerHelper.GetAssignedPlansForHeadquarters(address))
            {
                if (plan.HrManagerInstance != null && !plan.HrManagerInstance.IsAssignedToAnyWorkShift())
                {
                    plan.UnAssignEmployee();
                }
            }
            foreach (LogisticsManagerPlan plan in LogisticsManagerHelper.GetAssignedPlansForHeadquarters(address))
            {
                if (plan.LogisticsManagerInstance != null && !plan.LogisticsManagerInstance.IsAssignedToAnyWorkShift())
                {
                    plan.assignedEmployeeId = null;
                }
            }
            foreach (ImportPartnership plan in PurchasingAgentHelper.GetAssignedPlansForHeadquarters(address))
            {
                if (plan.PurchasingAgentInstance != null && !plan.PurchasingAgentInstance.IsAssignedToAnyWorkShift())
                {
                    plan.UnAssignEmployee();
                }
            }
            foreach (PricingManagerPlan plan in PricingManagerHelper.GetPlansForHeadquarters(address))
            {
                EmployeeInstance analyst = plan.AnalystInstance;
                if (analyst != null && !analyst.IsAssignedToAnyWorkShift())
                {
                    plan.UnAssignEmployee();
                }
            }
        }

        /// <summary>True if the business has a shift of somebody the planner would place: a fill writes over it, so the player is asked first.</summary>
        public static bool HasRota(RotaSheet sheet)
        {
            Index(sheet);
            List<ScheduleDay> week = sheet.Place.Reg.scheduleDays;
            for (int i = 0; i < week.Count; i++)
            {
                for (int k = 0; week[i] != null && week[i].workShifts != null && k < week[i].workShifts.Count; k++)
                {
                    WorkShift shift = week[i].workShifts[k];
                    if (shift != null && !string.IsNullOrEmpty(shift.employeeId) && WorkerAt.ContainsKey(shift.employeeId))
                    {
                        return true;
                    }
                }
            }
            return false;
        }
    }
}
