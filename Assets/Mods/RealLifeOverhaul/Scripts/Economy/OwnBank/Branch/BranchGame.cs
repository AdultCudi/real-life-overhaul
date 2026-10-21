using System;
using System.Collections.Generic;
using AI.Customers.CustomerEntries;
using Controllers;
using Entities;
using Helpers;
using UnityEngine;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// Everything the bank branches read from the game: the player's branches (the game's own businesses of the branch type,
    /// Going Public's Mine), what an hour of one has to go on (open, the game's customer count, promotion, satisfaction, the
    /// tellers on shift), its rent and wages a day, and inside one the tills its counter is worked at. Each read is a pass
    /// over the save's lists or the room's furniture, once an hour, once a second while inside, or on a click; nothing per frame.
    /// </summary>
    internal static class BranchGame
    {
        private const string CustomerService = "ba:skill_customerservice";

        private static GameInstance Save => SaveGameManager.Current;

        /// <summary>A branch by its address, as the save names a place (street|number).</summary>
        public static string Place(BuildingRegistration reg)
        {
            return reg == null ? string.Empty : Uri.EscapeDataString(reg.StreetName ?? string.Empty) + "~" + reg.StreetNumber.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        public static string Place(Address address)
        {
            return address == null ? string.Empty : Uri.EscapeDataString(address.streetName ?? string.Empty) + "~" + address.streetNumber.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        /// <summary>The name the player gave a branch, else its address.</summary>
        public static string Name(BuildingRegistration reg)
        {
            return reg == null ? string.Empty : reg.GetDisplayName();
        }

        // A branch of the player's own (not another Going Public player's) that is set up as a business.
        private static bool Mine(BuildingRegistration reg)
        {
            return BranchType.Is(reg) && GoingPublic.Mine(reg) && reg.HasEstablishedBusiness && reg.HasValidAddress;
        }

        /// <summary>The player's branches, in the save's order. One pass over the save's buildings.</summary>
        public static void Branches(List<BuildingRegistration> into)
        {
            into.Clear();
            List<BuildingRegistration> all = Save != null ? Save.BuildingRegistrations : null;
            for (int i = 0; all != null && i < all.Count; i++)
            {
                if (Mine(all[i]))
                {
                    into.Add(all[i]);
                }
            }
        }

        /// <summary>The branch of the player's that the player stands in, or null.</summary>
        public static BuildingRegistration Inside()
        {
            BuildingManager manager = BuildingManager.IsInsideBuilding ? InstanceBehavior<BuildingManager>.Instance : null;
            BuildingRegistration reg = manager != null ? manager.buildingRegistration : null;
            return reg != null && !manager.enteringBuilding && !manager.exitingBuilding && Mine(reg) ? reg : null;
        }

        /// <summary>What a branch's hour has to go on, read from the game: open, its customer count, promotion, satisfaction and tellers on shift.</summary>
        public static BranchHourIn Hour(BuildingRegistration reg, bool player)
        {
            var h = new BranchHourIn();
            GameInstance save = Save;
            if (reg == null || save == null)
            {
                return h;
            }
            h.Open = !reg.temporarilyClosed && BusinessHelper.IsBusinessOpen(reg) && (!BusinessGame.Promoted(reg) || BusinessGame.HasFootfall(reg));
            try
            {
                CustomerEntriesCalculator calc = CustomerEntriesCalculatorFactory.GetCustomerEntriesCalculator(reg);
                h.ShopCustomers = calc != null ? Mathf.Max(0f, calc.GetInitialCustomers()) : 0f;
            }
            catch (Exception)
            {
                h.ShopCustomers = 0f;   // a building the game cannot size has no visitors
            }
            h.Promotion = reg.promotion != null ? reg.promotion.total : 0;
            h.BaseMultiplier = save.gameVariables != null ? save.gameVariables.baseCustomerPromotionMultiplier : 0.5f;
            h.Satisfaction = reg.satisfaction != null ? reg.satisfaction.overall : 50;
            Tellers(reg, out h.Tellers, out h.Skill);
            h.Player = player && h.Open;
            return h;
        }

        // The tellers on shift at a branch now (customer service their main skill, at work, not absent), and their average skill.
        private static void Tellers(BuildingRegistration reg, out int count, out int skill)
        {
            count = 0;
            skill = 0;
            List<EmployeeInstance> staff = Save != null ? Save.EmployeeInstances : null;
            float sum = 0f;
            for (int i = 0; staff != null && i < staff.Count; i++)
            {
                EmployeeInstance e = staff[i];
                if (e == null || e.assignedAddress == null || e.assignedAddress != reg.Address || e.isAbsent || e.characterData == null || e.characterData.skills == null
                    || e.characterData.skills.Count == 0 || e.GetPrimarySkill() != CustomerService || !e.IsWorking())
                {
                    continue;
                }
                count++;
                sum += e.GetSkillValue(CustomerService);
            }
            skill = count > 0 ? Mathf.RoundToInt(sum / count) : 0;
        }

        /// <summary>Everyone assigned to a branch with customer service as their main skill, on shift or not.</summary>
        public static int Staff(BuildingRegistration reg)
        {
            int n = 0;
            List<EmployeeInstance> staff = Save != null ? Save.EmployeeInstances : null;
            for (int i = 0; reg != null && staff != null && i < staff.Count; i++)
            {
                EmployeeInstance e = staff[i];
                n += e != null && e.assignedAddress != null && e.assignedAddress == reg.Address && e.characterData != null && e.characterData.skills != null
                     && e.characterData.skills.Count > 0 && e.GetPrimarySkill() == CustomerService ? 1 : 0;
            }
            return n;
        }

        /// <summary>
        /// What a branch costs a day, in cents at today's prices: its rent (none in a building the player owns) and its staff's
        /// wages over the week's shifts, a seventh of them. The game pays both itself; this is for the branch's page.
        /// </summary>
        public static long CostDay(BuildingRegistration reg)
        {
            if (reg == null || Save == null)
            {
                return 0;
            }
            double cents = reg.BuildingOwnedByPlayer ? 0.0 : Math.Max(0f, reg.RentPerDay) * 100.0;
            List<ScheduleDay> week = reg.scheduleDays;
            double wages = 0.0;
            for (int d = 0; week != null && d < week.Count; d++)
            {
                ScheduleDay sd = week[d];
                for (int s = 0; sd != null && sd.workShifts != null && s < sd.workShifts.Count; s++)
                {
                    WorkShift shift = sd.workShifts[s];
                    EmployeeInstance e = shift != null ? Employee(shift.employeeId) : null;
                    if (e != null)
                    {
                        wages += Math.Max(0, shift.endingHour - shift.startingHour) * Math.Max(0f, e.hourlyWage) * 100.0;
                    }
                }
            }
            return (long)Math.Round(cents + wages / 7.0);
        }

        private static EmployeeInstance Employee(string id)
        {
            List<EmployeeInstance> staff = Save != null ? Save.EmployeeInstances : null;
            for (int i = 0; !string.IsNullOrEmpty(id) && staff != null && i < staff.Count; i++)
            {
                if (staff[i] != null && staff[i].id == id)
                {
                    return staff[i];
                }
            }
            return null;
        }

        /// <summary>The tills of the room the player stands in (the game's points of sale, where tellers stand), in the layout's own order.</summary>
        public static void Tills(List<CashRegisterController> into)
        {
            into.Clear();
            BuildingManager manager = BuildingManager.IsInsideBuilding ? InstanceBehavior<BuildingManager>.Instance : null;
            List<ItemController> items = manager != null ? manager.allItemControllers : null;
            for (int i = 0; items != null && i < items.Count; i++)
            {
                if (items[i] is CashRegisterController till && till.gameObject.activeInHierarchy && !(till.playerItemPurchaserSettings != null && till.playerItemPurchaserSettings.enabled))
                {
                    into.Add(till);
                }
            }
            into.Sort(ById);
        }

        private static readonly Comparison<CashRegisterController> ById = (a, b) =>
            string.CompareOrdinal(a.ItemInstance != null ? a.ItemInstance.id ?? string.Empty : string.Empty, b.ItemInstance != null ? b.ItemInstance.id ?? string.Empty : string.Empty);

        /// <summary>True while somebody works the till: one of the game's people or the player.</summary>
        public static bool Staffed(CashRegisterController till)
        {
            return till != null && till.employee != null;
        }

        /// <summary>True while the player is the one at the till.</summary>
        public static bool PlayerAt(CashRegisterController till)
        {
            return till != null && till.employee != null && till.employee.isPlayer;
        }

        /// <summary>
        /// Where a customer stands to be seen to at a till: across it from where its teller stands, on the floor people walk on;
        /// and the teller's own place. False if no such place can be found.
        /// </summary>
        public static bool Front(CashRegisterController till, out Vector3 front, out Vector3 teller)
        {
            front = teller = Vector3.zero;
            if (till == null)
            {
                return false;
            }
            teller = till.GetEmployeePosition();
            Vector3 at = till.transform.position, away = at - teller;
            away.y = 0f;
            if (away.sqrMagnitude < 0.01f)
            {
                away = till.transform.forward;
                away.y = 0f;
            }
            Vector3 want = at + away.normalized * 0.9f;
            want.y = teller.y;
            if (UnityEngine.AI.NavMesh.SamplePosition(want, out UnityEngine.AI.NavMeshHit hit, 0.8f, UnityEngine.AI.NavMesh.AllAreas))
            {
                front = hit.position;
                return true;
            }
            return false;
        }
    }
}
