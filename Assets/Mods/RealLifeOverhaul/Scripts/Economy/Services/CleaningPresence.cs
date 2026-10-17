using System;
using System.Collections.Generic;
using BAModAPI;
using Entities;
using Helpers;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// A visiting cleaner seen at work in the shop the player stands in: a station puts into the room whoever a shift of
    /// this hour on it names, by id alone (EmployeeStationController.UpdateEmployee), so one shift of the running hour goes
    /// on a free station. It never cleans or pays: it comes in after that hour's cleaning and out on the next hour event,
    /// before a save and whenever the visit ends. Cost: nothing per frame; <see cref="Show"/> once a second during a visit.
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

        /// <summary>Shows the cleaner in this building for this hour, if the player is in it and a station is free. True if they are in the room.</summary>
        public static bool Show(BuildingRegistration reg, EmployeeInstance e, int hour)
        {
            if (_broken || reg == null || e == null || !GoingPublic.Mine(reg))
            {
                return false;   // a business that is not the player's has its own people: its schedule is never touched
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
                            _station.UpdateEmployee();   // the station was in the middle of a look of its own when first asked
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

        /// <summary>Takes the mod's shift out of the schedule; with 'leave' the station looks again so that the character goes at once.</summary>
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

        /// <summary>Asks a station that has still to send a visitor away again.</summary>
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

        // A station asked to send its visitor away during a look of its own is asked again.
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

        // A cleaning station of the building the player is in with nobody at it and no shift of its own in this hour.
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
            Logger?.Warn("Cleaning service: showing a visiting cleaner in the shop is switched off for this session after an error. "
                         + "The mod's shift is out of the schedule; visits clean as before and the page says when a cleaner was in.");
        }
    }
}
