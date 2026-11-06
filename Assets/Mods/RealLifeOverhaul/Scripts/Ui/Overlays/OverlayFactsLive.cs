using System;
using System.Collections.Generic;
using BigAmbitions.DayNightCycle;
using BigAmbitions.Factories.Recipes;
using BigAmbitions.Items;
using Buildings;
using Controllers;
using EmployeeStations;
using Entities;
using Extensions;
using Helpers;
using Items.SpecialItems;
using Localizor;
using Streets;
using UnityEngine;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// What the cards over the world's items read from the game beyond the words its overlays write: a shelf's stock against its
    /// room, the price the player set and what the goods cost, the next delivery of an item, the seats taken at a table and the
    /// line at a till, a person's shift, and a factory machine's recipe, rate and pallets. Every read is of the game's own
    /// public state, none is saved, and each answers false or nothing when the game does not know. Called only when a card is
    /// read again (a change, or once a second while it shows), never a frame.
    /// </summary>
    internal static class OverlayFactsLive
    {
        private const string FactoryWorker = "ba:skill_factoryworker";   // the skill the game's own factory panel scales a recipe's output by

        private static readonly int[] Starts = new int[12], Ends = new int[12];

        // ------------------------------------------------------------------------------------------------- stock and price

        /// <summary>A shelf's, a pallet's or a delivery spot's stock: the amount and the room it has, as the game's own overlay counts them.</summary>
        public static bool Stock(EntityController entity, out int amount, out int room, out string item, out bool storage)
        {
            amount = room = 0;
            item = null;
            storage = false;
            var controller = entity as ItemController;
            if (controller == null || controller.ItemInstance == null)
            {
                return false;
            }
            storage = entity is StorageShelfController || entity is PalletController || entity is DeliverySpot;
            if (storage)
            {
                amount = controller.ItemInstance.cargoInstances.Count;
                room = controller.Item.cargoCapacity;
                return room > 0;
            }
            CargoInstance stock = controller.ItemInstance.GetStockInstance();
            if (stock == null || string.IsNullOrEmpty(stock.itemName))
            {
                return false;
            }
            item = stock.itemName;
            amount = stock.amount;
            room = stock.GetMaxStockCapacity(controller.ItemInstance);
            return room > 0;
        }

        /// <summary>The price the player set for an item in the business they are in, and what one unit of the stock cost them.</summary>
        public static bool Price(EntityController entity, string item, out float price, out float cost)
        {
            price = cost = 0f;
            BuildingManager manager = InstanceBehavior<BuildingManager>.Instance;
            var controller = entity as ItemController;
            if (manager == null || !manager.IsPlayerOwnedBusiness || manager.buildingRegistration == null || controller == null || string.IsNullOrEmpty(item))
            {
                return false;
            }
            List<RetailPrice> prices = manager.buildingRegistration.retailPrices;
            for (int i = 0; prices != null && i < prices.Count; i++)
            {
                if (prices[i] != null && prices[i].itemName == item)
                {
                    price = prices[i].price;
                    break;
                }
            }
            CargoInstance stock = controller.ItemInstance != null ? controller.ItemInstance.GetStockInstance() : null;
            cost = stock != null && stock.itemName == item ? stock.pricePerUnit : 0f;
            return price > 0f;
        }

        /// <summary>The day the next wholesale delivery of an item comes to the player's business (today counts before it has come); false when none is contracted.</summary>
        public static bool NextDelivery(string item, out int day)
        {
            day = -1;
            BuildingManager manager = InstanceBehavior<BuildingManager>.Instance;
            GameInstance save = SaveGameManager.Current;
            if (manager == null || !manager.IsPlayerOwnedBusiness || manager.buildingRegistration == null || save == null || save.DeliveryContracts == null || string.IsNullOrEmpty(item))
            {
                return false;
            }
            int today = TimeHelper.CurrentDay;
            List<DeliveryContract> contracts = save.DeliveryContracts;
            for (int i = 0; i < contracts.Count; i++)
            {
                DeliveryContract c = contracts[i];
                if (c == null || !c.enabled || c.businessAddress == null || c.businessAddress != manager.buildingRegistration.Address || c.items == null)
                {
                    continue;
                }
                bool coming = c.nextDeliveryDay > today || (c.nextDeliveryDay == today && TimeHelper.CurrentHour < 7);   // the game brings them from seven
                for (int k = 0; coming && k < c.items.Count; k++)
                {
                    if (c.items[k].itemName == item && (day < 0 || c.nextDeliveryDay < day))
                    {
                        day = c.nextDeliveryDay;
                    }
                }
            }
            return day >= 0;
        }

        /// <summary>The words for a delivery's day: "Today", "Tomorrow" or the weekday in the game's own words.</summary>
        public static string DayWords(int day)
        {
            int today = TimeHelper.CurrentDay;
            if (day <= today)
            {
                return UiText.T("reallife_ui_overlay_today");
            }
            if (day == today + 1)
            {
                return UiText.T("reallife_ui_overlay_tomorrow");
            }
            return TimeHelper.GetDayOfWeek(day).GetLocalizeKey().GetLocalization();
        }

        // ------------------------------------------------------------------------------------------------- seats and queues

        /// <summary>The seats of an item and of the chairs attached to it, and how many of them are taken now.</summary>
        public static bool Seats(EntityController entity, out int taken, out int seats)
        {
            taken = seats = 0;
            var controller = entity as ItemController;
            BuildingManager manager = InstanceBehavior<BuildingManager>.Instance;
            if (controller == null || manager == null || !manager.IsPlayerOwnedBusiness)
            {
                return false;
            }
            Count(controller.SeatSpots, ref taken, ref seats);
            List<ItemController> children = controller.childItemControllers;
            for (int i = 0; children != null && i < children.Count; i++)
            {
                if (children[i] != null)
                {
                    Count(children[i].SeatSpots, ref taken, ref seats);
                }
            }
            return seats > 0;
        }

        private static void Count(SeatSpot[] spots, ref int taken, ref int seats)
        {
            for (int i = 0; spots != null && i < spots.Length; i++)
            {
                seats++;
                taken += spots[i] != null && spots[i].occupied ? 1 : 0;
            }
        }

        /// <summary>The customers in the line at a till, from the game's own waiting line; zero when the item holds none.</summary>
        public static int Queue(EntityController entity)
        {
            var controller = entity as ItemController;
            if (controller == null || !controller.TryGetComponent(out IWaitingLineHolder holder))
            {
                return 0;
            }
            WaitingLine line = holder.GetWaitingLine();
            return line != null && line.data != null ? line.data.GetCustomersInWaitingLine() : 0;
        }

        // ------------------------------------------------------------------------------------------------- a person

        /// <summary>The employee at a station, or null.</summary>
        public static EmployeeInstance Employee(EntityController entity) =>
            entity is EmployeeStationController station ? station.employeeInstance : null;

        /// <summary>
        /// A person's shift today as they are on it now: its first and its last hour with the shifts that run on joined to it (the
        /// game's schedule, the game's clock). When they are not on one, the next shift today starts at <paramref name="from"/>;
        /// both are -1 with none left today.
        /// </summary>
        public static bool Shift(EmployeeInstance person, out int from, out int until, out bool on)
        {
            from = until = -1;
            on = false;
            if (person == null)
            {
                return false;
            }
            BuildingRegistration reg = BuildingHelper.GetBuildingRegistration(person.assignedAddress);
            if (reg == null || reg.scheduleDays == null)
            {
                return false;
            }
            DayOfWeekOrdered today = TimeHelper.GetDayOfWeek(SaveGameManager.Current.Day);
            int count = 0;
            for (int i = 0; i < reg.scheduleDays.Count; i++)
            {
                ScheduleDay day = reg.scheduleDays[i];
                if (day == null || day.day != today || !day.isOpen || day.workShifts == null)
                {
                    continue;
                }
                for (int k = 0; k < day.workShifts.Count && count < Starts.Length; k++)
                {
                    WorkShift w = day.workShifts[k];
                    if (w != null && w.employeeId == person.id)
                    {
                        Starts[count] = w.startingHour;
                        Ends[count++] = w.endingHour;
                    }
                }
            }
            int hour = TimeHelper.CurrentHour;
            if (OverlayRules.OnShift(Starts, Ends, count, hour, out from, out until))
            {
                on = person.IsEmployeeAvailable();
                return true;
            }
            int next = -1;
            for (int i = 0; i < count; i++)
            {
                next = Starts[i] > hour && (next < 0 || Starts[i] < next) ? Starts[i] : next;
            }
            if (next < 0)
            {
                return false;
            }
            from = next;
            until = -1;
            for (int i = 0; i < count; i++)
            {
                until = Starts[i] == next ? Ends[i] : until;
            }
            return true;
        }

        /// <summary>A person's strongest skill as the game names it, with its value 0 to 100.</summary>
        public static bool TopSkill(EmployeeInstance person, out string name, out float value)
        {
            name = null;
            value = 0f;
            string primary = person != null ? person.GetPrimarySkill() : null;
            if (string.IsNullOrEmpty(primary))
            {
                return false;
            }
            name = primary.GetLocalization();
            value = person.GetSkillValue(primary);
            return true;
        }

        // ------------------------------------------------------------------------------------------------- a factory machine

        /// <summary>A recipe machine's recipe: what it makes, what it needs, what it makes an hour with the person at it, and the pallets' count of its output.</summary>
        public static bool Machine(EntityController entity, out string makes, out string needs, out int perHour, out int onPallets)
        {
            makes = needs = null;
            perHour = onPallets = 0;
            var machine = entity as FactoryAssemblyMachineController;
            FactoryWorkstationInstance ws = machine != null ? machine.WorkstationInstance : null;
            Recipe recipe = ws != null ? ws.SelectedRecipe : null;
            BuildingManager manager = InstanceBehavior<BuildingManager>.Instance;
            if (recipe == null || manager == null || manager.buildingRegistration == null)
            {
                return false;
            }
            makes = recipe.output.item.GetLocalization();
            var names = new System.Text.StringBuilder(64);
            for (int i = 0; recipe.ingredients != null && i < recipe.ingredients.Count; i++)
            {
                names.Append(i > 0 ? ", " : string.Empty).Append(recipe.ingredients[i].item.GetLocalization());
            }
            needs = names.Length > 0 ? names.ToString() : null;
            EmployeeInstance person = machine.employeeInstance;
            perHour = person != null ? recipe.GetScaledOutputAmount(person.GetSkillValue(FactoryWorker)) : 0;
            onPallets = BuildingHelper.CountResourcesInPallets(manager.buildingRegistration.Address, recipe.output.item);
            return true;
        }
    }
}
