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
    /// The game's rota demands as the planner's numbers, each asked of the demand itself.
    /// </summary>
    internal static partial class RotaGame
    {
        // ---------------------------------------------------------------- demands

        /// <summary>
        /// One of the game's demands as the planner's numbers; false for one that is not about the rota. Hours a
        /// week and the stations asked for are read from the demand's public members. A number of days, free days
        /// and hours not worked have no public figure: the demand's own Fulfilled is asked about an employee who
        /// is nobody (never added to the game) with a made-up week, day by day and hour by hour, once for each
        /// demand in a session.
        /// </summary>
        private static bool Demand(string name, RotaSheet sheet, BuildingRegistration reg, out RotaDemand demand)
        {
            demand = default(RotaDemand);
            if (string.IsNullOrEmpty(name) || Unknown.Contains(name))
            {
                return false;
            }
            JobDemand asked;
            try
            {
                asked = JobDemandHelper.GetByName(name);
            }
            catch (Exception)
            {
                asked = null;
            }
            if (asked == null)
            {
                Unknown.Add(name);
                return false;
            }
            var onItem = asked as WorksOnItem;
            if (onItem != null)
            {
                // Which of this business's stations it accepts, by the demand's own answer (the item, the one it stands on, the ones stacked on it).
                demand.Kind = RotaDemandKind.OnStation;
                demand.Priority = (int)asked.priority;
                for (int s = 0; s < sheet.Stations.Count; s++)
                {
                    Info.id = sheet.Stations[s].id;
                    Info.itemInstance = sheet.Stations[s];
                    Info.skillsRequired = sheet.Stations[s].ItemCached.suitableSkills;
                    Info.workShiftType = sheet.Problem.Kind[s] == RotaKind.Cleaning ? WorkShiftType.Cleaning : WorkShiftType.Default;
                    demand.Mask |= onItem.AcceptsWorkStation(Info, reg) ? 1UL << s : 0UL;
                }
                Info.itemInstance = null;
                return true;
            }
            if (Known.TryGetValue(name, out demand))
            {
                return true;
            }
            try
            {
                demand.Priority = (int)asked.priority;
                var hours = asked as HoursWorkingPerWeek;
                if (hours != null)
                {
                    demand.Kind = RotaDemandKind.Hours;
                    demand.A = hours.MinHours;
                    demand.B = hours.MaxHours;
                }
                else if (asked is DaysWorkingPerWeek)
                {
                    demand.Kind = RotaDemandKind.Days;
                    EmployeeInstance nobody = Nobody();
                    for (int days = 1; days <= RotaCatalogue.Days && demand.A == 0; days++)
                    {
                        nobody.assignedWeeklyDays.Clear();
                        for (int d = 1; d <= days; d++)
                        {
                            nobody.assignedWeeklyDays.Add((DayOfWeekOrdered)d);
                        }
                        demand.A = asked.Fulfilled(nobody, _madeWeek) ? days : 0;
                    }
                    if (demand.A == 0)
                    {
                        throw new InvalidOperationException("no number of days fulfils it");
                    }
                }
                else if (asked is FreeOnDays)
                {
                    demand.Kind = RotaDemandKind.FreeDays;
                    EmployeeInstance nobody = Nobody();
                    for (int d = 1; d <= RotaCatalogue.Days; d++)
                    {
                        nobody.assignedWeeklyDays.Clear();
                        nobody.assignedWeeklyDays.Add((DayOfWeekOrdered)d);
                        demand.Mask |= asked.Fulfilled(nobody, _madeWeek) ? 0UL : 1UL << (d - 1);
                    }
                }
                else if (asked is HasNoShiftBetweenHours)
                {
                    demand.Kind = RotaDemandKind.NoHours;
                    EmployeeInstance nobody = Nobody();
                    for (int h = 0; h < RotaCatalogue.Hours; h++)
                    {
                        _madeShift.startingHour = h;
                        _madeShift.endingHour = h + 1;
                        demand.Mask |= asked.Fulfilled(nobody, _madeWeek) ? 0UL : 1UL << h;
                    }
                }
                else if (asked is NoSpecificShift)
                {
                    demand.Kind = RotaDemandKind.NoShiftKind;
                    var filter = (IWorkStationFilter)asked;
                    Info.itemInstance = null;
                    Info.workShiftType = WorkShiftType.Default;
                    demand.Mask |= filter.AcceptsWorkStation(Info, reg) ? 0UL : 1UL << RotaKind.Work;
                    Info.workShiftType = WorkShiftType.Cleaning;
                    demand.Mask |= filter.AcceptsWorkStation(Info, reg) ? 0UL : 1UL << RotaKind.Cleaning;
                }
                else
                {
                    // Not about the rota (a clean workplace, health insurance, an item in the building, the player's happiness), or a kind this version does not know.
                    if (asked is IScheduleDemand || asked is IScheduleConstraint)
                    {
                        Logger?.Warn("Rota: the game's demand '" + name + "' (" + asked.GetType().Name + ") is about the schedule and is of a kind this version of the planner does not know; rotas are made without it.");
                    }
                    Unknown.Add(name);
                    return false;
                }
            }
            catch (Exception e)
            {
                Logger?.Warn("Rota: the game's demand '" + name + "' (" + asked.GetType().Name + ") could not be read (" + e.Message + "); rotas are made without it.");
                Unknown.Add(name);
                return false;
            }
            Known[name] = demand;
            return true;
        }

        // The employee a demand is asked about, with a week of one open day and one shift of one hour: never added to the game, never saved.
        private static EmployeeInstance Nobody()
        {
            if (_made == null)
            {
                _made = new EmployeeInstance();
                _madeShift = new WorkShift { employeeId = _made.id, itemInstanceId = string.Empty, type = WorkShiftType.Default };
                var day = new ScheduleDay { day = DayOfWeekOrdered.Monday, isOpen = true };
                day.workShifts.Add(_madeShift);
                _madeWeek = new List<ScheduleDay>(1) { day };
            }
            _made.assignedWeeklyDays.Clear();
            _made.workedDays = 0;
            _made.assignedWeeklyHours = 0;
            _madeShift.startingHour = 0;
            _madeShift.endingHour = 0;
            return _made;
        }

        /// <summary>A demand's name as the game writes it.</summary>
        public static string DemandName(EmployeeInstance e, RotaDemand demand)
        {
            for (int i = 0; e != null && e.demands != null && i < e.demands.Count; i++)
            {
                RotaDemand known;
                JobDemand asked;
                try
                {
                    asked = JobDemandHelper.GetByName(e.demands[i]);
                }
                catch (Exception)
                {
                    continue;
                }
                bool same = asked is WorksOnItem ? demand.Kind == RotaDemandKind.OnStation
                    : Known.TryGetValue(e.demands[i], out known) && known.Kind == demand.Kind && known.A == demand.A && known.B == demand.B && known.Mask == demand.Mask;
                if (same && asked != null)
                {
                    return asked.GetLocalization();
                }
            }
            return string.Empty;
        }
    }
}
