using System;
using System.Collections.Generic;
using BAModAPI;
using Entities;
using Helpers;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// A visiting cleaner seen at work in the shop the player stands in.
    ///
    /// HOW THE GAME SHOWS A CLEANER. Each cleaning station of the building the player is in looks, when the hour
    /// turns or the building's registration changes, for a shift of this hour on itself in the building's own
    /// schedule, and puts the employee of that shift into the room by their id alone
    /// (EmployeeStationController.UpdateEmployee, GetEmployeeWorkShift: the employee is found with
    /// EmployeeHelper.GetEmployeeById and is not asked where they are assigned). The character then walks from dirty
    /// tile to dirty tile with a mop (CleanerEmployee); that is for the eye only, the cleaning itself is the hourly sum.
    ///
    /// WHAT THIS DOES. While a visit is on in the building the player is in, one shift of the game's own kind is put
    /// into that building's schedule of today: this hour only, on a cleaning station nobody is at, with the visiting
    /// cleaner's id; and the station is asked to look. That is all. The shift is the mod's and never the save's:
    ///
    ///   it never cleans   it covers the running hour only and is put in after that hour's cleaning has run, and it is
    ///                     taken out on the game's hour event, which comes before the next hour's cleaning
    ///                     (GameManager.RunMainGameTick). The visit's cleaning is the service's own, by the minute
    ///   it is taken out   when the visit ends, when the hour turns, when the player leaves the building, before the
    ///                     game writes a save, when the feature stops for any reason and when the city unloads
    ///   it pays nobody    wages follow the cleaner's own building's schedule (EmployeeInstance.IsWorking)
    ///
    /// A shop with no cleaning station, or none that is free, shows nobody: the visit still cleans, and the page says
    /// when the cleaner was in. A fault here switches only this off for the session.
    ///
    /// Cost: nothing per frame. <see cref="Show"/> is asked once a second while a visit is on in the building the
    /// player is in (a few reference comparisons while the cleaner is already there; a walk over the building's item
    /// controllers and today's shifts when a cleaner is first put in).
    /// </summary>
    internal static class CleaningPresence
    {
        public static IModLogger Logger;

        private static BuildingRegistration _reg;
        private static ScheduleDay _day;
        private static WorkShift _shift;
        private static CleaningStationController _station;
        private static CleaningStationController _left;   // a station whose visitor has gone and that has still to send the character away
        private static string _leftId;
        private static bool _broken;

        /// <summary>True while a shift of the mod's is in a schedule.</summary>
        public static bool Showing => _shift != null;

        /// <summary>True while a station has still to send a visitor away.</summary>
        public static bool Leaving => _left != null;

        public static bool Broken => _broken;

        /// <summary>
        /// The cleaner is shown in this building for this hour, if the player is in it and a station is free.
        /// True if they are in the room (or already were).
        /// </summary>
        public static bool Show(BuildingRegistration reg, EmployeeInstance e, int hour)
        {
            if (_broken || reg == null || e == null || !GoingPublic.Mine(reg))
            {
                return false;   // (a business that is not the player's has its own people: its schedule is never touched)
            }
            try
            {
                Settle();
                if (_shift != null)
                {
                    if (ReferenceEquals(_reg, reg) && _shift.employeeId == e.id && _shift.startingHour == hour && _day.workShifts.Contains(_shift) && _station != null && Inside(reg))
                    {
                        if (_station.employee == null)
                        {
                            _station.UpdateEmployee();   // (the station was in the middle of a look of its own when it was first asked)
                        }
                        return _station.employee != null;
                    }
                    Clear(true);
                }
                if (!Inside(reg) || reg.temporarilyClosed || hour < 0 || hour > 23)
                {
                    return false;
                }
                ScheduleDay today = BuildingHelper.GetTodaySchedule(reg);
                if (today == null || !today.isOpen || today.workShifts == null)
                {
                    return false;
                }
                CleaningStationController station = FreeStation(today, hour);
                if (station == null)
                {
                    return false;
                }
                _reg = reg;
                _day = today;
                _station = station;
                _shift = new WorkShift { startingHour = hour, endingHour = hour + 1, employeeId = e.id, itemInstanceId = station.ItemInstance.id, type = WorkShiftType.Cleaning };
                today.AddWorkShift(_shift);
                station.UpdateEmployee();
                return station.employee != null;
            }
            catch (Exception ex)
            {
                Fault(ex);
                return false;
            }
        }

        /// <summary>
        /// The mod's shift is taken out of the schedule. With 'leave' the station is asked to look again, so that the
        /// character goes at once; without it the character stays until the station next looks by itself (the hour
        /// turning, a save in the middle of a visit).
        /// </summary>
        public static void Clear(bool leave)
        {
            if (_shift == null)
            {
                if (leave)
                {
                    Tidy();
                }
                return;
            }
            try
            {
                if (_day != null && _day.workShifts != null)
                {
                    _day.workShifts.Remove(_shift);
                }
                if (leave && _station != null && _reg != null && Inside(_reg))
                {
                    _left = _station;
                    _leftId = _shift.employeeId;
                    _station.UpdateEmployee();
                }
            }
            catch (Exception ex)
            {
                Fault(ex);
            }
            _shift = null;
            _day = null;
            _reg = null;
            _station = null;
        }

        /// <summary>A station that has still to send a visitor away is asked again. Nothing else.</summary>
        public static void Tidy()
        {
            if (_left == null)
            {
                return;
            }
            try
            {
                Settle();
            }
            catch (Exception ex)
            {
                Fault(ex);
            }
        }

        /// <summary>The city is unloading: the shift is taken out and nothing of the game's is asked to do anything.</summary>
        public static void Forget()
        {
            if (_shift != null && _day != null && _day.workShifts != null)
            {
                _day.workShifts.Remove(_shift);
            }
            _shift = null;
            _day = null;
            _reg = null;
            _station = null;
            _left = null;
            _leftId = null;
            _broken = false;
        }

        // A station that was asked to send its visitor away while it was in the middle of a look of its own is asked again.
        private static void Settle()
        {
            if (_left == null)
            {
                return;
            }
            CleaningStationController station = _left;
            if (station != null && station.employee != null && station.employeeInstance != null && station.employeeInstance.id == _leftId && BuildingManager.IsInsideBuilding)
            {
                station.UpdateEmployee();
                if (station != null && station.employee != null && station.employeeInstance != null && station.employeeInstance.id == _leftId)
                {
                    return;   // still busy: once more next time
                }
            }
            _left = null;
            _leftId = null;
        }

        private static bool Inside(BuildingRegistration reg)
        {
            return BuildingManager.IsInsideBuilding && ReferenceEquals(InstanceBehavior<BuildingManager>.Instance.buildingRegistration, reg);
        }

        // A cleaning station of the building the player is in that has nobody at it and no shift of its own in this hour.
        private static CleaningStationController FreeStation(ScheduleDay today, int hour)
        {
            List<ItemController> controllers = InstanceBehavior<BuildingManager>.Instance.allItemControllers;
            for (int i = 0; controllers != null && i < controllers.Count; i++)
            {
                var station = controllers[i] as CleaningStationController;
                if (station == null || station.employee != null || station.ItemInstance == null || string.IsNullOrEmpty(station.ItemInstance.id) || !station.gameObject.activeInHierarchy)
                {
                    continue;
                }
                bool taken = false;
                for (int s = 0; s < today.workShifts.Count && !taken; s++)
                {
                    WorkShift shift = today.workShifts[s];
                    taken = shift != null && shift.itemInstanceId == station.ItemInstance.id && shift.startingHour <= hour && shift.endingHour > hour && !string.IsNullOrEmpty(shift.employeeId);
                }
                if (!taken)
                {
                    return station;
                }
            }
            return null;
        }

        private static void Fault(Exception e)
        {
            _broken = true;
            Logger?.Error(e);
            try
            {
                if (_shift != null && _day != null && _day.workShifts != null)
                {
                    _day.workShifts.Remove(_shift);
                }
            }
            catch (Exception)
            {
                // nothing more can be done for it
            }
            _shift = null;
            _day = null;
            _reg = null;
            _station = null;
            _left = null;
            _leftId = null;
            Logger?.Warn("Cleaning service: showing a visiting cleaner in the shop is switched off for this session after an error. The mod's shift is out of the schedule; visits clean as before and the page says when a cleaner was in.");
        }
    }
}
