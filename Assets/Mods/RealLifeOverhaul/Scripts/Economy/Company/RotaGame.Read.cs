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
    /// Reading one business from the game into the planner's plain numbers: its stations in groups, who may stand
    /// where, the week it keeps, the customers of every hour, what a customer is worth, the day's fees and the
    /// cleaning its customers make. What is read from where, and why nothing of the mod's is counted twice, is
    /// said in RotaGame.cs.
    /// </summary>
    internal static partial class RotaGame
    {
        // ---------------------------------------------------------------- reading a business

        /// <summary>
        /// Reads one business into a sheet: its stations in groups, who may stand where and what they ask for, the
        /// customers of every hour of the week, what a customer is worth, and the rota it has now.
        /// <paramref name="dayPromotion"/> (may be null) is the promotion figure the mod will have written on each
        /// weekday, or less than nothing for "as it stands"; <paramref name="raise"/> answers what an employee is
        /// paid on top of the game's wage. A business with more stations or employees than the planner keeps is
        /// not read at all (<see cref="RotaSheet.TooLarge"/>).
        /// </summary>
        public static void Read(RotaSheet sheet, RotaPlace place, int[] dayPromotion, Func<string, int> raise)
        {
            BuildingRegistration reg = place.Reg;
            sheet.Place = place;
            sheet.Reg = reg;
            sheet.Shown = null;
            sheet.Late = false;
            sheet.FixedCount = 0;
            ReadStations(sheet, reg);
            ReadWorkers(sheet, reg);
            sheet.AllStations = sheet.Stations.Count;
            sheet.AllWorkers = sheet.Workers.Count;
            sheet.TooLarge = !RotaLedger.Fits(sheet.AllStations, sheet.AllWorkers);
            if (sheet.TooLarge)
            {
                sheet.Stations.Clear();
                sheet.Workers.Clear();
                GroupKeys.Clear();
            }
            int stations = sheet.Stations.Count, workers = sheet.Workers.Count;
            RotaShop shop = sheet.Shop;
            shop.Size(stations, GroupKeys.Count);
            RotaProblem problem = sheet.Problem;
            problem.Size(stations, workers);
            sheet.Now.Size(stations);
            sheet.Fixed = sheet.Fixed.Length < workers ? new bool[workers] : sheet.Fixed;
            if (sheet.TooLarge)
            {
                return;
            }
            bool hq = place.Type == RotaType.Headquarters;
            for (int g = 0; g < GroupKeys.Count; g++)
            {
                shop.GroupKind[g] = GroupKinds[g];
            }
            for (int s = 0; s < stations; s++)
            {
                shop.Group[s] = GroupOf[s];
                shop.Cap[s] = CapOf[s];
                bool cleans = GroupKinds[GroupOf[s]] == RotaGroupKind.Cleaning;
                problem.Kind[s] = cleans ? RotaKind.Cleaning : RotaKind.Work;
                problem.Whole[s] = hq && !cleans;   // the game's rule at a headquarters: the same person at a desk for every open hour of the week (ScheduleHelper.AddWorkShift, ScheduleAutoFiller.AddHeadquartersConstraint)
            }
            ReadWeek(sheet, reg);
            ReadSkillsAndDemands(sheet, reg, raise);
            RotaLedger.Bench(problem, sheet.Fixed);
            shop.FixedHours = hq;   // (the game's schedule screen refuses to change a headquarters' hours: ScheduleHourToggle.OnPointerDown)
            ReadCustomers(sheet, reg, place.Type == RotaType.Customers, dayPromotion);
            ReadRoom(sheet, reg);
            ReadWorth(sheet, reg);
            ReadStock(sheet, reg);
            ReadFees(shop, reg);
            ReadCleaning(sheet, reg);
            for (int s = 0; s < stations; s++)
            {
                shop.StationWage[s] = StationWage(sheet, s);
            }
        }

        // The stations, in the order the planner keeps them: those that serve customers group by group, then the rest, then
        // cleaning. Each item's kind and what it serves is asked of the game once. A station that lacks something it requires
        // is not read: the game's day simulation passes over it, so nobody is put at it, and whoever has a shift there is left as they are.
        private static void ReadStations(RotaSheet sheet, BuildingRegistration reg)
        {
            sheet.Stations.Clear();
            sheet.GroupNames.Clear();
            sheet.Broken.Clear();
            GroupOf.Clear();
            CapOf.Clear();
            GroupKeys.Clear();
            GroupLimits.Clear();
            GroupKinds.Clear();
            ItemKinds.Clear();
            ItemCaps.Clear();
            ItemKeys.Clear();
            ItemLimits.Clear();
            reg.GetAssignableItems(Items);
            Items.Sort(ByNameThenId);
            bool office = CustomerEntriesCalculatorFactory.GetCustomerEntriesCalculator(reg) is CustomerEntriesCalculatorOffice;
            for (int i = 0; i < Items.Count; i++)
            {
                ItemInstance item = Items[i];
                Item data = item != null ? item.ItemCached : null;
                byte kind = NotRead;
                int cap = 0;
                string key = null, limit = null;
                if (data != null && data.suitableSkills != null)
                {
                    bool cleans = Array.IndexOf(data.suitableSkills, Cleaning) >= 0;   // (how the game's auto fill tells a cleaning shift: ScheduleAutoFiller.GetWorkStationInfo)
                    key = cleans ? Cleaning : data.itemName;
                    if (cleans)
                    {
                        kind = RotaGroupKind.Cleaning;
                    }
                    else if (ItemHelper.HasAnyMissingRequirements(item))
                    {
                        if (sheet.Broken.Count < 2)
                        {
                            sheet.Broken.Add(Name(item));
                        }
                    }
                    else if (office)
                    {
                        cap = data.addedCustomersPerHour > 0 ? 1 : 0;   // an office serves one customer a staffed desk each hour (OfficeBusinessSimulator.SimulateCurrentHour)
                        key = cap > 0 ? "desk" : key;
                        kind = cap > 0 ? RotaGroupKind.Service : RotaGroupKind.Support;
                    }
                    else
                    {
                        One.Clear();
                        One.Add(item);
                        foreach (Item.ItemCapacity capacity in One.GetItemsSortedByCapacity(reg, false, false))
                        {
                            cap = capacity.CustomersLimit;
                            limit = capacity.itemName ?? string.Empty;
                            key = string.IsNullOrEmpty(capacity.itemName) ? key : capacity.itemName;
                            break;
                        }
                        kind = cap > 0 ? RotaGroupKind.Service : RotaGroupKind.Support;
                    }
                }
                ItemKinds.Add(kind);
                ItemCaps.Add(cap);
                ItemKeys.Add(key);
                ItemLimits.Add(limit);
            }
            for (byte pass = 0; pass < 3; pass++)
            {
                for (int i = 0; i < Items.Count; i++)
                {
                    if (ItemKinds[i] != pass)
                    {
                        continue;
                    }
                    int group = GroupKeys.IndexOf(ItemKeys[i]);
                    if (group < 0)
                    {
                        group = GroupKeys.Count;
                        GroupKeys.Add(ItemKeys[i]);
                        GroupLimits.Add(pass == RotaGroupKind.Service ? ItemLimits[i] : null);
                        GroupKinds.Add(pass);
                        sheet.GroupNames.Add(Text(Items[i].ItemCached.itemName));
                    }
                    sheet.Stations.Add(Items[i]);
                    GroupOf.Add(group);
                    CapOf.Add(ItemCaps[i]);
                }
            }
            One.Clear();
            Items.Clear();
        }

        // Where each station and each employee of a sheet stands in its lists, by the game's ids: asked again before every use,
        // so that it is always the sheet at hand that answers.
        private static void Index(RotaSheet sheet)
        {
            StationAt.Clear();
            WorkerAt.Clear();
            for (int s = 0; s < sheet.Stations.Count; s++)
            {
                StationAt[sheet.Stations[s].id] = s;
            }
            for (int w = 0; w < sheet.Workers.Count; w++)
            {
                WorkerAt[sheet.Workers[w].id] = w;
            }
        }

        private static int ByNameThenId(ItemInstance a, ItemInstance b)
        {
            int byName = string.CompareOrdinal(a.itemName, b.itemName);
            return byName != 0 ? byName : string.CompareOrdinal(a.id, b.id);
        }

        // Everybody assigned to the business whom the game's own auto fill would place: not its delivery drivers, and not the player.
        private static void ReadWorkers(RotaSheet sheet, BuildingRegistration reg)
        {
            sheet.Workers.Clear();
            EmployeeHelper.GetEmployeeInstances(new EmployeeInstancesQueryInfo { withAssignedAddress = reg.Address, withoutSkills = Drivers }, People);
            EmployeeInstance self = null;
            try
            {
                self = PlayerHelper.GetPlayerEmployeeInstance();
            }
            catch (Exception)
            {
                // No player employee to leave out.
            }
            for (int i = 0; i < People.Count; i++)
            {
                EmployeeInstance e = People[i];
                if (e == null || ReferenceEquals(e, self) || string.IsNullOrEmpty(e.id) || e.IsCandidate || e.characterData == null || e.characterData.skills == null)
                {
                    continue;
                }
                sheet.Workers.Add(e);
            }
            People.Clear();
        }

        private static void ReadSkillsAndDemands(RotaSheet sheet, BuildingRegistration reg, Func<string, int> raise)
        {
            RotaProblem problem = sheet.Problem;
            int stations = sheet.Stations.Count;
            for (int w = 0; w < sheet.Workers.Count; w++)
            {
                EmployeeInstance e = sheet.Workers[w];
                // What they cost an hour: the game's wage at today's prices (the city's cost of living pays it so) and any raise on top.
                problem.Worker[w].Wage = Mathf.Max(0, CompanyCatalogue.Share(Mathf.RoundToInt(e.hourlyWage * 100f), Prices.Level) + (raise != null ? raise(e.id) : 0));
                for (int s = 0; s < stations; s++)
                {
                    problem.Can[w * stations + s] = HowWell(e, sheet.Stations[s].ItemCached);
                }
                Demands.Clear();
                for (int i = 0; e.demands != null && i < e.demands.Count && Demands.Count < RotaCatalogue.DemandsMost; i++)
                {
                    RotaDemand demand;
                    if (Demand(e.demands[i], sheet, reg, out demand))
                    {
                        Demands.Add(demand);
                    }
                }
                problem.Worker[w].Demands = Demands.Count > 0 ? Demands.ToArray() : RotaWorker.Empty;
            }
            Demands.Clear();
        }

        // How well somebody does a station: the first of their skills the item takes, by its value and their satisfaction, as the
        // game works out the service a customer gets (EmployeeInstance.GetCustomerSatisfaction). 0: no skill for it.
        private static byte HowWell(EmployeeInstance e, Item item)
        {
            List<Skill> skills = e.characterData.skills;
            for (int i = 0; i < skills.Count; i++)
            {
                if (skills[i] != null && Array.IndexOf(item.suitableSkills, skills[i].name) >= 0)
                {
                    return (byte)Mathf.Clamp(Mathf.RoundToInt(skills[i].value * Mathf.Clamp(e.satisfaction, 0f, 100f) * 0.01f), 1, 100);
                }
            }
            return 0;
        }


        // ---------------------------------------------------------------- the week it has

        private static void ReadWeek(RotaSheet sheet, BuildingRegistration reg)
        {
            RotaShop shop = sheet.Shop;
            int stations = sheet.Stations.Count, workers = sheet.Workers.Count;
            Index(sheet);
            Present.Clear();
            Counted.Clear();
            List<ScheduleDay> week = reg.scheduleDays;
            for (int i = 0; i < week.Count; i++)
            {
                ScheduleDay day = week[i];
                int d = day != null ? (int)day.day - 1 : -1;
                if (d < 0 || d >= RotaCatalogue.Days)
                {
                    continue;
                }
                for (int k = 0; day.isOpen && day.openingHourSlots != null && k < day.openingHourSlots.Count; k++)
                {
                    OpeningHourSlot slot = day.openingHourSlots[k];
                    for (int h = Mathf.Max(0, slot.startingHour); h < slot.endingHour && h < RotaCatalogue.Hours; h++)
                    {
                        shop.Open[d * RotaCatalogue.Hours + h] = true;
                    }
                }
                for (int k = 0; day.workShifts != null && k < day.workShifts.Count; k++)
                {
                    WorkShift shift = day.workShifts[k];
                    if (shift == null || string.IsNullOrEmpty(shift.employeeId))
                    {
                        continue;
                    }
                    RotaShift coded = Coded(shift, reg, d);
                    Present.Add(coded);
                    if (day.isOpen)
                    {
                        Counted.Add(coded);   // (the game counts no hours and pays no wages for a shift on a day that is closed: EmployeeInstance.GetWorkingHoursOnDay)
                    }
                }
            }
            // Who is left exactly as they are, by every shift they have; then the week as it counts: the hours of those a fill
            // may move are the rota to be rated, every other hour at a station is taken.
            RotaLedger.Fixed(Present, sheet.Problem.Whole, workers, sheet.Fixed);
            RotaLedger.Lay(Counted, sheet.Fixed, stations, sheet.Now.At, shop.Taken);
            for (int w = 0; w < workers; w++)
            {
                sheet.FixedCount += sheet.Fixed[w] ? 1 : 0;
            }
            Present.Clear();
            Counted.Clear();
        }

        // ---------------------------------------------------------------- customers

        /// <summary>
        /// The game's uncapped figure for an office, and its satisfaction factor: both protected in the game's
        /// calculator, so asked from inside it. The office figure is the game's own line
        /// (CustomerEntriesCalculatorOffice.GetCustomersByHour) without its last step, the cap at the desks staffed now.
        /// </summary>
        private sealed class Probe : CustomerEntriesCalculatorOffice
        {
            public int Office(BuildingRegistration reg, int hour, DayOfWeekOrdered day, float initial)
            {
                Init(reg);
                string product = reg.cachedAvailableProducts != null && reg.cachedAvailableProducts.Count > 0 ? reg.cachedAvailableProducts[0] : null;
                if (string.IsNullOrEmpty(product))
                {
                    return 0;
                }
                float demand = ProductMarketHelper.GetNeighborhoodDemand(product, reg.Neighborhood).demand / 100f;
                return Mathf.Max(0, Mathf.CeilToInt(initial * GetCustomerSatisfactionMultiplier() * GetPromotionMultiplier() * GetDayAndHourMultiplier(hour, day) * demand - 0.2f));
            }

            public float Satisfaction(BuildingRegistration reg)
            {
                Init(reg);
                return GetCustomerSatisfactionMultiplier();
            }
        }

        private static void ReadCustomers(RotaSheet sheet, BuildingRegistration reg, bool walkIn, int[] dayPromotion)
        {
            RotaShop shop = sheet.Shop;
            shop.HasCustomers = false;
            sheet.RequirementsMet = true;
            if (!walkIn)
            {
                return;
            }
            CustomerEntriesCalculator calculator = CustomerEntriesCalculatorFactory.GetCustomerEntriesCalculator(reg);
            float initial = calculator.GetInitialCustomers();
            if (initial <= 0f)
            {
                return;
            }
            bool office = calculator is CustomerEntriesCalculatorOffice, retail = calculator is CustomerEntriesCalculatorRetail;
            shop.HasCustomers = true;
            if (retail)
            {
                Plain.Init(reg);
            }
            try
            {
                sheet.RequirementsMet = reg.AreAllRequirementsMet();
            }
            catch (Exception)
            {
                sheet.RequirementsMet = true;
            }
            for (int d = 0; d < RotaCatalogue.Days; d++)
            {
                // A weekday on which the mod will have written another promotion figure than today's is asked of the game's
                // calculator with that figure in the business's record, which is put back before anything else can read it:
                // the game's own arithmetic, cap and rounding and all, on the figure the mod's own writer will produce.
                var day = (DayOfWeekOrdered)(d + 1);
                int stands = reg.promotion != null ? reg.promotion.total : 0, then = dayPromotion != null ? dayPromotion[d] : -1;
                bool other = reg.promotion != null && then >= 0 && then != stands;
                try
                {
                    if (other)
                    {
                        reg.promotion.total = then;
                    }
                    for (int h = 0; h < RotaCatalogue.Hours; h++)
                    {
                        int customers = office ? Calculator.Office(reg, h, day, initial) : calculator.GetCustomersByHour(h, day, initial);
                        // A shop's figure stops at what its stations serve (CustomerEntriesCalculatorRetail); the game's plain calculator gives the same figure without that stop.
                        int wanting = retail ? Plain.GetCustomersByHour(h, day, initial) : customers;
                        shop.Customers[d * RotaCatalogue.Hours + h] = Mathf.Max(0, customers);
                        shop.Wanting[d * RotaCatalogue.Hours + h] = Mathf.Max(0, wanting);
                    }
                }
                finally
                {
                    if (other)
                    {
                        reg.promotion.total = stands;
                    }
                }
            }
        }

        // For each group of service stations, the most customers an hour everything else in the building allows: the building's
        // own capacity and every other group the game counts, shelves among them (BusinessHelper.UpdateCustomerCapacity). What
        // one more till would serve is never said to be more than that.
        private static void ReadRoom(RotaSheet sheet, BuildingRegistration reg)
        {
            RotaShop shop = sheet.Shop;
            if (!shop.HasCustomers || reg.BuildingCached == null)
            {
                return;
            }
            int building = BuildingSizeHelper.GetData(reg).GetCustomerCapacity(reg.BuildingCached.BuildingType, reg.BuildingCached.BuildingVersion);
            for (int g = 0; g < shop.Groups; g++)
            {
                shop.GroupRoom[g] = GroupLimits[g] != null && building > 0 ? building : 0;
            }
            foreach (Item.ItemCapacity capacity in reg.itemInstances.Values.GetItemsSortedByCapacity(reg))
            {
                for (int g = 0; g < shop.Groups; g++)
                {
                    if (GroupLimits[g] != null && GroupLimits[g] != (capacity.itemName ?? string.Empty))
                    {
                        shop.GroupRoom[g] = shop.GroupRoom[g] > 0 ? Mathf.Min(shop.GroupRoom[g], capacity.CustomersLimit) : capacity.CustomersLimit;
                    }
                }
            }
            for (int cell = 0; building > 0 && cell < RotaCatalogue.Cells; cell++)
            {
                shop.Customers[cell] = Mathf.Min(shop.Customers[cell], building);   // (the game's day simulation serves no more than the building holds: RetailBusinessSimulator.GetMaxCustomerCapacityThisHour)
            }
        }

        /// <summary>The customers the game would send a business in a week if it were open all of it; 0 where nobody walks in.</summary>
        public static int WeekCustomers(BuildingRegistration reg)
        {
            CustomerEntriesCalculator calculator = CustomerEntriesCalculatorFactory.GetCustomerEntriesCalculator(reg);
            float initial = calculator.GetInitialCustomers();
            int sum = 0;
            bool office = calculator is CustomerEntriesCalculatorOffice;
            for (int d = 1; initial > 0f && d <= RotaCatalogue.Days; d++)
            {
                for (int h = 0; h < RotaCatalogue.Hours; h++)
                {
                    sum += Mathf.Max(0, office ? Calculator.Office(reg, h, (DayOfWeekOrdered)d, initial) : calculator.GetCustomersByHour(h, (DayOfWeekOrdered)d, initial));
                }
            }
            return sum;
        }

        // What a customer is worth: from the last week's order history if it has enough customers, else from the prices.
        private static void ReadWorth(RotaSheet sheet, BuildingRegistration reg)
        {
            RotaShop shop = sheet.Shop;
            sheet.FromBooks = false;
            if (!shop.HasCustomers)
            {
                return;
            }
            int today = Save.Day, customers = 0;
            double takings = 0, goods = 0;
            List<OrderHistoryEntry> history = reg.orderHistory;
            for (int i = history != null ? history.Count - 1 : -1; i >= 0; i--)
            {
                OrderHistoryEntry entry = history[i];
                if (entry == null || entry.dayNumber >= today || entry.totalCustomers <= 0)
                {
                    continue;
                }
                if (entry.dayNumber < today - RotaCatalogue.BooksDays)
                {
                    break;
                }
                customers += entry.totalCustomers;
                takings += entry.totalRevenue;
                for (int k = 0; entry.itemSales != null && k < entry.itemSales.Count; k++)
                {
                    goods += entry.itemSales[k] != null ? entry.itemSales[k].totalWholesalePrice : 0f;
                }
            }
            // (The order history and the shop's prices are the game's own: what the city's takings line makes of them is
            // today's, as the wages are; the goods are at today's wholesale prices already.)
            double factor = CityEconomyDirector.TakingsFactor / 1000.0;
            if (customers >= RotaCatalogue.BooksCustomers && takings > 0)
            {
                sheet.FromBooks = true;
                shop.Takings = (int)Math.Round(takings * factor * 100.0 / customers);
                shop.Margin = (int)Math.Round((takings * factor - goods) * 100.0 / customers);
                return;
            }
            // No books yet: what one customer buys by the game's own arithmetic for the products on sale, at the prices set.
            BusinessType type = BusinessTypeHelper.GetData(reg);
            List<string> products = reg.cachedAvailableProducts;
            if (type == null || products == null)
            {
                return;
            }
            takings = goods = 0;
            if (CustomerEntriesCalculatorFactory.GetCustomerEntriesCalculator(reg) is CustomerEntriesCalculatorOffice)
            {
                takings = products.Count > 0 ? ItemHelper.GetPrice(products[0], reg) : 0f;   // one customer, one order of the first product (CustomerEntriesCalculatorOffice.AddProductsToCustomersEntriesList)
            }
            else
            {
                float content = Calculator.Satisfaction(reg);
                string fee = type.hasEntranceFee ? BusinessTypeHelper.GetEntranceFeeNameForBusinessType(type) : null;
                takings += string.IsNullOrEmpty(fee) ? 0f : ItemHelper.GetPrice(fee, reg);
                for (int i = 0; i < products.Count; i++)
                {
                    Item item = ItemsGetter.GetByName(products[i], true);
                    if (item == null || products[i] == type.defaultEntranceFee || products[i] == type.weekendOnlyEntranceFee || item.HasTag(TagRef.Itemtag.isticket))
                    {
                        continue;
                    }
                    float demand = item.isADemandedProduct ? ProductMarketHelper.GetNeighborhoodDemand(products[i], reg.Neighborhood).demand : 100f;
                    float amount = type.maxAmountPerProduct > 1f ? (type.IsPrimaryProduct(products[i]) ? (1f + type.maxAmountPerProduct) * 0.5f : 1f) : type.maxAmountPerProduct;
                    float sold = (item.productSalesRatio == 0f ? 1f : item.productSalesRatio) * (demand / 100f) * content * type.GetProductImpact(products[i]) * amount;
                    takings += sold * ItemHelper.GetPrice(products[i], reg);
                    goods += (item.type & ItemType.ServiceProduct) != 0 ? 0f : sold * item.GetWholesalePrice();
                }
            }
            shop.Takings = (int)Math.Round(Math.Max(0.0, takings * factor) * 100.0);
            shop.Margin = (int)Math.Round((takings * factor - goods) * 100.0);
        }

        // The licensing fee the game takes on a day a cinema or a theatre opens: a screen each, or the stage (LicensingFeesHelper.PayLicensingFees).
        private static void ReadFees(RotaShop shop, BuildingRegistration reg)
        {
            bool cinema = reg.businessTypeName == "ba:businesstype_cinema", theatre = reg.businessTypeName == "ba:businesstype_theater";
            if (!cinema && !theatre)
            {
                return;
            }
            try
            {
                for (int d = 0; d < RotaCatalogue.Days; d++)
                {
                    var day = (DayOfWeekOrdered)(d + 1);
                    float fee = 0f;
                    if (theatre)
                    {
                        fee = ScheduleHelper.IsLicensingFeeActive(reg.Address, string.Empty, day) ? CinemaTheaterHelper.TheaterStageLicensingFee : 0f;
                    }
                    else
                    {
                        foreach (KeyValuePair<string, ItemInstance> item in reg.itemInstances)
                        {
                            fee += item.Value != null && item.Value.itemName == CinemaScreen && ScheduleHelper.IsLicensingFeeActive(reg.Address, item.Key, day) ? CinemaTheaterHelper.CinemaScreenLicensingFee : 0f;
                        }
                    }
                    shop.DayFee[d] = Mathf.RoundToInt(fee * 100f);
                }
            }
            catch (Exception e)
            {
                Logger?.Warn("Rota: the licensing fees of " + reg.BusinessName + " could not be read (" + e.Message + "); the advice counts none.");
            }
        }

        // How much cleaning the customers make, in the planner's two rates, from the game's own dirt figures.
        private static void ReadCleaning(RotaSheet sheet, BuildingRegistration reg)
        {
            RotaShop shop = sheet.Shop;
            int spots = reg.dirtSpots != null ? reg.dirtSpots.Count : 0, cleaning = 0, touched = 0, counted = 0, hands = 0, handed = 0;
            for (int s = 0; s < sheet.Stations.Count; s++)
            {
                bool cleans = shop.GroupKind[shop.Group[s]] == RotaGroupKind.Cleaning;
                cleaning += cleans ? 1 : 0;
                List<int> affects = sheet.Stations[s].dirtSpotsThatAffects;
                if (!cleans && affects != null && affects.Count > 0)
                {
                    touched += affects.Count;
                    counted++;
                }
                for (int w = 0; cleans && w < sheet.Workers.Count; w++)
                {
                    int can = sheet.Problem.Can[w * sheet.Stations.Count + s];
                    hands += can;
                    handed += can > 0 ? 1 : 0;
                }
            }
            Building building = BuildingHelper.GetBuilding(reg.Address);
            if (spots == 0 || cleaning == 0 || building == null || !BuildingTypeHelper.GetData(building).NeedsCleaning)
            {
                return;   // nothing to clean, nothing to clean it with, or a building the game never cleans (BuildingCleanlinessHelper.RunHourly)
            }
            bool toilet = false, sink = false;
            foreach (ItemInstance item in reg.itemInstances.Values)
            {
                Item data = item != null ? item.ItemCached : null;
                toilet |= data != null && (data.type & ItemType.Toilet) != 0;
                sink |= data != null && (data.type & ItemType.Sink) != 0;
            }
            // A customer served dirties the till and a shelf, and half the time a toilet, and then nearly always a sink (RetailBusinessSimulator.ProcessCustomer).
            shop.DirtEvents = 200 + (toilet ? 50 : 0) + (toilet && sink ? 45 : 0);
            // One dirtying adds so much to a share of the spots an item touches; an hour of a cleaner takes their skill off every spot, divided by the cleaners the floor needs.
            float perSpot = DirtPerTime * Mathf.Ceil(DirtShare * (counted > 0 ? touched / (float)counted : 4f)) / spots;
            float force = Mathf.Max(1f, Mathf.Ceil(BuildingSizeHelper.GetData(building).squareMeters * CleanersPerSquareMetre));
            float skill = handed > 0 ? hands / (float)handed : 50f;
            shop.CleanHours = Mathf.Max(1, Mathf.RoundToInt(1000f * perSpot * force / Mathf.Max(1f, skill)));
            // The same two figures as the ladder of plans runs its week with: points of mean dirt a customer adds, and points a cleaner's hour takes off.
            shop.DirtPerCustomer = Mathf.Max(1, Mathf.RoundToInt(shop.DirtEvents * 10f * perSpot));
            shop.CleanPerHour = Mathf.Max(1, Mathf.RoundToInt(1000f * Mathf.Max(1f, skill) / force));
        }

        // What an hour of somebody at a station costs when nobody is rostered for it yet: the average of those who could stand
        // there; with nobody, the game's own wage for the station's skill at a middling value.
        private static int StationWage(RotaSheet sheet, int station)
        {
            int stations = sheet.Stations.Count, sum = 0, n = 0;
            for (int w = 0; w < sheet.Workers.Count; w++)
            {
                if (sheet.Problem.Can[w * stations + station] != 0)
                {
                    sum += sheet.Problem.Worker[w].Wage;
                    n++;
                }
            }
            if (n > 0)
            {
                return sum / n;
            }
            string[] skills = sheet.Stations[station].ItemCached.suitableSkills;
            try
            {
                return skills != null && skills.Length > 0 ? Mathf.RoundToInt(EmployeeHelper.CalculateHourlyWageForSkill(skills[0], 50f) * 100f) : 0;
            }
            catch (Exception)
            {
                return 0;
            }
        }
    }
}
