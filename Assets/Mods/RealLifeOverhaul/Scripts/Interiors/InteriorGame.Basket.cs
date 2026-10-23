using System;
using System.Collections.Generic;
using AI.Customers.CustomerEntries;
using BigAmbitions.Items;
using BigAmbitions.Tags;
using Entities;
using Helpers;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul.Interiors
{
    /// <summary>The products a shop's customers take more, and an office's clients booked, as the game adds them.</summary>
    internal static partial class InteriorGame
    {
        /// <summary>
        /// Books an open office more clients for the coming hour by the game's own calculator, never more than its free staffed
        /// stations; they go into <paramref name="ours"/>. Returns how many.
        /// </summary>
        public static int BookOffice(BuildingRegistration reg, int boostMille, ref int carryMille, HashSet<CustomerEntry> ours)
        {
            if (boostMille <= 0 || reg == null || reg.temporarilyClosed)
            {
                return 0;
            }
            BusinessType type = BusinessTypeHelper.GetData(reg);
            int next = TimeHelper.CurrentHour + 1;
            if (type == null || !(type.simulator is Buildings.Office.OfficeBusinessSimulator) || !type.HasTag(TagRef.Businesstag.generatesrevenue) || next > 23
                || !BusinessHelper.IsBusinessOpen(reg, next) || !reg.AreAllRequirementsMet())
            {
                return 0;
            }
            var calculator = CustomerEntriesCalculatorFactory.GetCustomerEntriesCalculator(reg) as CustomerEntriesCalculatorOffice;
            List<CustomerEntry> entries = CustomerDraw.Entries(reg);
            if (calculator == null || entries == null)
            {
                return 0;
            }
            int today = TimeHelper.CurrentDay, game = 0, booked = 0;
            for (int i = 0; i < entries.Count; i++)
            {
                CustomerEntry e = entries[i];
                if (e != null && !e.completed && e.spawnTime != null && e.spawnTime.Day == today && e.spawnTime.Hour == next)
                {
                    booked++;
                    game += ours.Contains(e) ? 0 : 1;
                }
            }
            int n = InteriorRules.OfficeClients(game, Stations(reg, type, next) - booked, boostMille, ref carryMille);
            if (n <= 0)
            {
                return 0;
            }
            List<CustomerEntry> made = calculator.GetCustomersEntriesForHour(next, n, NeighborhoodHelper.GetData(reg.Neighborhood).customerDemandsWeight);
            DrawPrint before = CustomerDraw.Print(entries);
            int added = 0;
            for (int i = 0; made != null && i < made.Count && added < n; i++)   // the calculator's own list: taken from at once
            {
                if (made[i] != null)
                {
                    entries.Add(made[i]);
                    ours.Add(made[i]);
                    added++;
                }
            }
            if (added > 0)
            {
                CustomerDraw.Added(reg, before);   // the list is still the one last drawn, with the feature's clients on it
            }
            return added;
        }

        // Stations served in an hour: a shift on it (not cleaning), somebody of the type's own skills at it, nothing missing.
        private static int Stations(BuildingRegistration reg, BusinessType type, int hour)
        {
            ScheduleDay today = BuildingHelper.GetTodaySchedule(reg);
            Dictionary<string, ItemInstance> placed = reg.itemInstances;   // may be null, as Items allows for it
            if (today == null || today.workShifts == null || type.employeePrimarySkills == null || placed == null)
            {
                return 0;
            }
            int count = 0;
            foreach (WorkShift shift in today.workShifts)
            {
                ItemInstance station;
                if (shift == null || string.IsNullOrEmpty(shift.employeeId) || shift.type == WorkShiftType.Cleaning || shift.startingHour > hour || shift.endingHour <= hour
                    || string.IsNullOrEmpty(shift.itemInstanceId) || !placed.TryGetValue(shift.itemInstanceId, out station) || station == null
                    || ItemHelper.HasAnyMissingRequirements(station))
                {
                    continue;
                }
                EmployeeInstance employee = EmployeeHelper.GetEmployeeAtStationAndHour(reg, station.id, hour);
                for (int k = 0; employee != null && employee.characterData != null && employee.characterData.skills != null && k < employee.characterData.skills.Count; k++)
                {
                    if (Array.IndexOf(type.employeePrimarySkills, employee.characterData.skills[k].name) >= 0)
                    {
                        count++;
                        break;
                    }
                }
            }
            return count;
        }

        public const float BasketLead = 2f;   // game minutes: a customer of the basket's hour due within them of its turn is left as drawn

        /// <summary>Adds products to a retail shop's customers to come this hour and the next, each once; returns how many.</summary>
        public static int Basket(BuildingRegistration reg, int share, bool office, int hour, float soonest)
        {
            if (share <= 0 || reg == null || reg.temporarilyClosed || !Retail(reg))
            {
                return 0;
            }
            List<CustomerEntry> entries = CustomerDraw.Entries(reg);
            BusinessType type = BusinessTypeHelper.GetData(reg);
            if (entries == null || entries.Count == 0 || type == null || !Sellable(reg, type))
            {
                return 0;
            }
            int today = TimeHelper.CurrentDay, added = 0;
            for (int i = 0; i < entries.Count; i++)
            {
                CustomerEntry e = entries[i];
                object seen;
                if (e == null || e.completed || e.spawnTime == null || e.spawnTime.Day != today || e.order == null || e.order.entries == null || e.order.entries.Count == 0
                    || !(e.spawnTime.Hour == hour + 1 || (e.spawnTime.Hour == hour && e.spawnTime.Minute > soonest)) || Seen.TryGetValue(e, out seen))
                {
                    continue;
                }
                Seen.Add(e, Mark);
                int more = InteriorRules.BasketExtra(Lines(e, type), share, Random.Range(0, 1000), office);
                if (more <= 0)
                {
                    continue;
                }
                int start = Random.Range(0, Products.Count);
                for (int k = 0, tried = 0; k < more && tried < Products.Count; tried++)
                {
                    string product = Products[(start + tried) % Products.Count];
                    if (Service(product) && Has(e, product))
                    {
                        continue;   // a service is had once a customer, as the game draws them
                    }
                    e.order.entries.Add(new OrderEntry { itemName = product });
                    added++;
                    k++;
                }
            }
            Products.Clear();
            return added;
        }

        // The products in a customer's order: every line but the door's entrance fee.
        private static int Lines(CustomerEntry e, BusinessType type)
        {
            int n = 0;
            for (int i = 0; i < e.order.entries.Count; i++)
            {
                string name = e.order.entries[i] != null ? e.order.entries[i].itemName : null;
                if (!string.IsNullOrEmpty(name) && name != type.defaultEntranceFee && name != type.weekendOnlyEntranceFee)
                {
                    n++;
                }
            }
            return n;
        }

        // A shop whose customers the game draws as retail ones, buying products off its shelves.
        private static bool Retail(BuildingRegistration reg)
        {
            try
            {
                if (reg.businessTypeName == Casino)
                {
                    return false;
                }
                return CustomerEntriesCalculatorFactory.GetCustomerEntriesCalculator(reg).GetType() == typeof(CustomerEntriesCalculatorRetail);
            }
            catch (Exception)
            {
                return false;
            }
        }

        // What the shop has on its shelves now, less entrance fees and tickets, into Products. False if it has nothing.
        private static bool Sellable(BuildingRegistration reg, BusinessType type)
        {
            Products.Clear();
            List<string> stocked = reg.cachedAvailableProducts;
            for (int i = 0; stocked != null && type != null && i < stocked.Count; i++)
            {
                string name = stocked[i];
                if (string.IsNullOrEmpty(name) || name == type.defaultEntranceFee || name == type.weekendOnlyEntranceFee)
                {
                    continue;
                }
                BigAmbitions.Items.Item item = ItemsGetter.GetByName(name, true);
                if (item == null || item.HasTag(TagRef.Itemtag.isticket))
                {
                    continue;
                }
                Products.Add(name);
            }
            return Products.Count > 0;
        }

        private static bool Service(string product)
        {
            BigAmbitions.Items.Item item = ItemsGetter.GetByName(product, true);
            return item != null && (item.type & ItemType.ServiceProduct) != 0;
        }

        private static bool Has(CustomerEntry e, string product)
        {
            for (int i = 0; i < e.order.entries.Count; i++)
            {
                if (e.order.entries[i] != null && e.order.entries[i].itemName == product)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
