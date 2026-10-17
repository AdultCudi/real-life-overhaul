using System.Collections.Generic;
using Entities;
using Helpers;

namespace RealLifeOverhaul.Economy
{
    /// <summary>The online shops' reach into the game, part: who picks at a place and which vans can take a round.</summary>
    internal static partial class OnlineGame
    {
        private const string CustomerService = "ba:skill_customerservice";

        private static readonly List<string> OnShift = new List<string>(16);

        /// <summary>
        /// Staff picking at a place in an hour of today: at a business, those at work on shift with customer service (the
        /// game has no stock-handling skill); at a warehouse, which has no shifts, its drivers not out on a round.
        /// </summary>
        public static int Pickers(WebPlace place, OnlineState state, int hour)
        {
            BuildingRegistration reg = place.Reg;
            int h = hour % 24, pickers = 0;
            if (reg == null)
            {
                return 0;
            }
            var warehouse = reg as Warehouse;
            if (place.Warehouse && warehouse != null)
            {
                if (h < OnlineCatalogue.WarehouseFrom || h >= OnlineCatalogue.WarehouseUntil || warehouse.vehicleSlots == null)
                {
                    return 0;
                }
                for (int i = 0; i < warehouse.vehicleSlots.Count; i++)
                {
                    VehicleSlot slot = warehouse.vehicleSlots[i];
                    EmployeeInstance driver = slot != null && !string.IsNullOrEmpty(slot.employeeDriverId) ? EmployeeHelper.GetEmployeeById(slot.employeeDriverId, false) : null;
                    if (driver == null || !driver.IsEmployeeAvailable())
                    {
                        continue;
                    }
                    Van van = state.VanOf(slot.employeeDriverId);
                    pickers += van == null || van.FreeAt <= hour * 60 ? 1 : 0;
                }
                return pickers;
            }
            if (reg.temporarilyClosed)
            {
                return 0;
            }
            ScheduleDay today = BuildingHelper.GetTodaySchedule(reg);
            if (today == null || today.workShifts == null)
            {
                return 0;
            }
            OnShift.Clear();
            for (int i = 0; i < today.workShifts.Count; i++)
            {
                WorkShift shift = today.workShifts[i];
                if (shift == null || string.IsNullOrEmpty(shift.employeeId) || !shift.IsHourInShift(h) || OnShift.Contains(shift.employeeId))
                {
                    continue;
                }
                EmployeeInstance employee = EmployeeHelper.GetEmployeeById(shift.employeeId, false);
                if (employee != null && employee.IsEmployeeAvailable() && employee.characterData != null && employee.characterData.skills != null && employee.HasSkill(CustomerService))
                {
                    OnShift.Add(shift.employeeId);
                    pickers++;
                }
            }
            OnShift.Clear();
            return pickers;
        }

        /// <summary>Marks ready each van at the player's warehouses with a vehicle and a driver at work; one whose driver or vehicle has gone is dropped after its day.</summary>
        public static void ReadVans(OnlineState state, int day)
        {
            for (int i = 0; i < state.Vans.Count; i++)
            {
                state.Vans[i].Ready = false;
            }
            List<BuildingRegistration> all = Save != null ? Save.BuildingRegistrations : null;
            List<VehicleInstance> vehicles = Save != null ? Save.VehicleInstances : null;
            for (int i = 0; all != null && vehicles != null && i < all.Count; i++)
            {
                var warehouse = all[i] as Warehouse;
                if (warehouse == null || !GoingPublic.Mine(warehouse) || warehouse.vehicleSlots == null)
                {
                    continue;
                }
                for (int k = 0; k < warehouse.vehicleSlots.Count; k++)
                {
                    VehicleSlot slot = warehouse.vehicleSlots[k];
                    if (slot == null || string.IsNullOrEmpty(slot.vehicleInstanceId) || string.IsNullOrEmpty(slot.employeeDriverId))
                    {
                        continue;
                    }
                    EmployeeInstance driver = EmployeeHelper.GetEmployeeById(slot.employeeDriverId, false);
                    VehicleInstance vehicle = null;
                    for (int v = 0; v < vehicles.Count && vehicle == null; v++)
                    {
                        vehicle = vehicles[v] != null && vehicles[v].id == slot.vehicleInstanceId ? vehicles[v] : null;
                    }
                    if (driver == null || vehicle == null || vehicle.VehicleType == null || !driver.IsEmployeeAvailable() || !Warehouse.CanDriverOperateVehicle(driver, vehicle))
                    {
                        continue;
                    }
                    Van van = state.VanOf(slot.employeeDriverId);
                    if (van == null)
                    {
                        if (state.Vans.Count >= OnlineCatalogue.VansMost)
                        {
                            continue;
                        }
                        van = new Van { Driver = slot.employeeDriverId, Day = day };
                        state.Vans.Add(van);
                    }
                    van.Ready = true;
                    van.Capacity = OnlineCatalogue.Clamp(vehicle.VehicleType.maxCargoCapacity, OnlineCatalogue.VanLeast, OnlineCatalogue.VanMost);
                }
            }
            for (int i = state.Vans.Count - 1; i >= 0; i--)
            {
                if (!state.Vans[i].Ready && state.Vans[i].Day < day)
                {
                    state.Vans.RemoveAt(i);
                }
            }
        }
    }
}
