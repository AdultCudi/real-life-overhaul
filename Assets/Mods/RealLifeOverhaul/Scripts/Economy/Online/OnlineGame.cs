using System;
using System.Collections.Generic;
using AI.Citizens;
using BAModAPI;
using BigAmbitions.Items;
using BigAmbitions.Neighborhoods;
using BigAmbitions.Tags;
using Entities;
using Helpers;
using Localizor;
using Streets;
using UnityEngine;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// What the online shops ask of the rest of the mod. Each is optional and has a default, so Economy/Online works
    /// alone; Scripts/Wiring/OnlineLinks.cs fills them in. It never calls another feature directly.
    /// </summary>
    public static class OnlineBridges
    {
        /// <summary>The customers, in per cent, that one of the company's campaigns adds at a business today. Default: none.</summary>
        public static Func<BuildingRegistration, int> Campaign;

        /// <summary>The season of a game day: 0 winter, 1 spring, 2 summer, 3 autumn. Default: spring.</summary>
        public static Func<int, int> Season;

        /// <summary>
        /// Measures the road between two places a slice at a time: -1 while it is still looking (ask again with the
        /// same two places), 0 if there is no road graph or no way, otherwise the metres of road. Default: 0, and
        /// distances stay a straight line's guess.
        /// </summary>
        public static Func<Vector3, Vector3, int> Road;

        public static void Reset()
        {
            Campaign = null;
            Season = null;
            Road = null;
        }
    }

    /// <summary>One online shop with what was read from the game for it. Nothing of this is saved.</summary>
    public sealed class WebPlace
    {
        public WebShop Shop;
        public BuildingRegistration Reg;
        public Address Address;
        public string Business = string.Empty;  // the business's own name
        public bool Warehouse;
        public readonly Site Site = new Site();
        public Vector3 At;                      // where the building stands
        public bool HasAt;
        public int Pickers;                     // staff picking in the last hour worked through
        public int Measuring;                   // the next district whose road is to be measured
    }

    /// <summary>
    /// Everything the online shops read from or do to the game, in one place.
    ///
    /// READ: the player's businesses and warehouses; what a building holds in stock (ItemInstance.cargoInstances of
    /// its storage, pallets and showcase shelves); a business's prices (ItemHelper.GetPrice) and the going price
    /// (Item.DefaultMarketPrice by the districts' price indices, CitizenHelper); the game's demand for each product in
    /// each district (GameInstance.productMarketEntries, the figures ProductMarketHelper.GetNeighborhoodDemand
    /// answers from); today's shifts (BuildingHelper.GetTodaySchedule) and the warehouses' vans and drivers
    /// (Warehouse.vehicleSlots); where the city's buildings stand (CityManager.cityBuildingControllers).
    ///
    /// DONE: stock is taken out and put back with the game's own calls (ItemInstance.ReduceFromCargo and
    /// ItemHelper.SubtractFromStock out, ItemHelper.DeliverCargoToBuilding in), with a line in the warehouse's own
    /// delivery record (Warehouse.deliveryTransactions) as the game's logistics write one; money as dated
    /// transactions of the mod's own (GameManager.ChangeMoneySafe), costs marked deductible the way the game marks
    /// its own (TransactionInfo.SetTaxDeductibleName); and a day's online sales are entered in the game's statement
    /// of the business for that day (FinancialSummary), so that the game's tax and everything that reads a business's
    /// takings count them, once.
    ///
    /// Nothing here runs on a timer or per frame: on the game's hour, once a game day, and when the app is opened.
    /// </summary>
    internal static class OnlineGame
    {
        public const string TakingsKey = "reallife_transaction_online_takings";
        public const string RefundsKey = "reallife_transaction_online_refunds";
        public const string CourierKey = "reallife_transaction_online_courier";
        public const string VansKey = "reallife_transaction_online_vans";
        public const string HostingKey = "reallife_transaction_online_hosting";
        public const string SetUpKey = "reallife_transaction_online_setup";
        public const string AdsKey = "reallife_transaction_online_ads";

        private const string DeductKey = "reallife_online_deduct";
        private const string WarehouseType = "ba:businesstype_warehouse";
        private const string AgencyType = "ba:businesstype_webdevelopmentagency";
        private const string Global = "ba:neighborhood_global";
        private const string CustomerService = "ba:skill_customerservice";
        private const int NoDemand = 50;            // what the game itself answers for a product it keeps no demand for (ProductMarketHelper.GetNeighborhoodDemand)
        private const int FarMetres = 2500;         // a district with no building to measure to
        private const int DeliveriesKept = 60;      // as the game keeps a warehouse's delivery record (LogisticsManagerPlanDestination.Deliver)

        public static IModLogger Logger;

        private static readonly List<BuildingRegistration> Regs = new List<BuildingRegistration>(16);
        private static readonly List<ItemInstance> Holders = new List<ItemInstance>(64);
        private static readonly List<ItemInstance> Emptied = new List<ItemInstance>(8);
        private static readonly List<string> OnShift = new List<string>(16);
        private static readonly List<DeliveryItem> Taken = new List<DeliveryItem>(8);
        private static readonly List<int> Counts = new List<int>(16);
        private static readonly List<WebPlace> Before = new List<WebPlace>(OnlineCatalogue.ShopsMost);

        private static GameInstance Save => SaveGameManager.Current;

        public static int Cash => Save != null ? Mathf.FloorToInt(Mathf.Max(0f, Save.Money)) : 0;

        public static int DaysPerYear
        {
            get
            {
                int days = Save != null && Save.gameVariables != null ? Save.gameVariables.daysPerYear : 60;
                return days >= 8 ? days : 60;
            }
        }

        // ---------------------------------------------------------------- the places

        /// <summary>A business by its address, written as the company writes a place.</summary>
        public static string Place(BuildingRegistration reg)
        {
            return Uri.EscapeDataString(reg.StreetName ?? string.Empty) + "~" + reg.StreetNumber.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        public static bool IsWarehouse(BuildingRegistration reg)
        {
            return reg is Warehouse && reg.RentedByPlayer && reg.HasValidAddress && reg.businessTypeName == WarehouseType;
        }

        /// <summary>The businesses that take money and the warehouses the player runs: what a site can be opened for. One pass over the save's buildings.</summary>
        public static void Candidates(List<BuildingRegistration> into)
        {
            into.Clear();
            List<BuildingRegistration> all = Save != null ? Save.BuildingRegistrations : null;
            for (int i = 0; all != null && i < all.Count; i++)
            {
                BuildingRegistration reg = all[i];
                if (reg != null && (IsWarehouse(reg) || Books.IsBusiness(reg)))
                {
                    into.Add(reg);
                }
            }
        }

        public static BuildingRegistration Find(string place)
        {
            List<BuildingRegistration> all = Save != null ? Save.BuildingRegistrations : null;
            for (int i = 0; all != null && i < all.Count; i++)
            {
                BuildingRegistration reg = all[i];
                if (reg != null && reg.RentedByPlayer && reg.HasValidAddress && string.Equals(Place(reg), place, StringComparison.Ordinal) && (IsWarehouse(reg) || Books.IsBusiness(reg)))
                {
                    return reg;
                }
            }
            return null;
        }

        public static string NameOf(BuildingRegistration reg)
        {
            return reg == null ? "?" : !string.IsNullOrEmpty(reg.BusinessName) ? reg.BusinessName : reg.GetDisplayName();
        }

        /// <summary>The business's type as the game writes it.</summary>
        public static string TypeOf(BuildingRegistration reg)
        {
            string type = reg != null ? reg.businessTypeName : null;
            return string.IsNullOrEmpty(type) ? string.Empty : LocalizorManager.IsLocalizedKey(type) ? type.GetLocalization() : type;
        }

        /// <summary>True if the player runs a web development agency: one of the game's own office kinds.</summary>
        public static bool HasAgency()
        {
            List<BuildingRegistration> all = Save != null ? Save.BuildingRegistrations : null;
            for (int i = 0; all != null && i < all.Count; i++)
            {
                if (all[i] != null && all[i].businessTypeName == AgencyType && Books.IsBusiness(all[i]))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Ties each site to its building again. A site whose business is no longer the player's is marked unseen
        /// (the director closes it). A site keeps what was read for it before (its measured roads) for as long as its
        /// building stands where it stood. One pass over the save's buildings for each site (twelve sites at most).
        /// </summary>
        public static void ReadPlaces(OnlineState state, List<WebPlace> into, List<WebPlace> pool)
        {
            Before.Clear();
            Before.AddRange(into);
            into.Clear();
            for (int i = 0; i < state.Shops.Count; i++)
            {
                WebShop shop = state.Shops[i];
                BuildingRegistration reg = Find(shop.Place);
                shop.Seen = reg != null;
                WebPlace place = null;
                for (int b = 0; b < Before.Count && place == null; b++)
                {
                    if (Before[b] != null && ReferenceEquals(Before[b].Shop, shop))
                    {
                        place = Before[b];
                        Before[b] = null;
                    }
                }
                bool known = place != null;
                if (!known && pool.Count > 0)
                {
                    place = pool[pool.Count - 1];
                    pool.RemoveAt(pool.Count - 1);
                }
                place = place ?? new WebPlace();
                Vector3 stood = place.At;
                bool had = known && place.HasAt;
                place.Shop = shop;
                place.Reg = reg;
                place.Warehouse = reg != null && IsWarehouse(reg);
                place.Business = NameOf(reg);
                place.HasAt = false;
                try
                {
                    place.Address = reg != null ? reg.Address : null;
                    CityBuildingController building = place.Address != null && InstanceBehavior<CityManager>.Instance != null ? InstanceBehavior<CityManager>.Instance.FindCityBuildingController(place.Address) : null;
                    if (building != null)
                    {
                        place.At = building.transform.position;
                        place.HasAt = true;
                    }
                }
                catch (Exception)
                {
                    place.Address = null;   // a business whose building cannot be read stands nowhere: its distances stay the guess
                }
                if (!known || had != place.HasAt || (had && (stood - place.At).sqrMagnitude > 1f))
                {
                    place.Pickers = 0;
                    place.Measuring = 0;
                    for (int d = 0; d < place.Site.Measured.Length; d++)
                    {
                        place.Site.Measured[d] = false;
                    }
                }
                into.Add(place);
            }
            for (int b = 0; b < Before.Count; b++)
            {
                if (Before[b] != null)
                {
                    Before[b].Reg = null;
                    Before[b].Shop = null;
                    pool.Add(Before[b]);
                }
            }
            Before.Clear();
        }

        // ---------------------------------------------------------------- the districts

        /// <summary>
        /// The city's districts by the game's names, each added to the state's list the first time it is seen (an
        /// order keeps its district's place in that list), and the middle of each: the mean of where its buildings
        /// stand. One pass over the city's buildings, when the feature starts.
        /// </summary>
        public static void Districts(OnlineState state, List<Vector3> centres, List<bool> has)
        {
            List<string> names = NeighborhoodHelper.Neighborhoods;
            for (int i = 0; names != null && i < names.Count; i++)
            {
                if (!string.IsNullOrEmpty(names[i]) && names[i] != Global && state.District(names[i]) < 0 && state.Districts.Count < 64)
                {
                    state.Districts.Add(names[i]);
                }
            }
            centres.Clear();
            has.Clear();
            Counts.Clear();
            for (int i = 0; i < state.Districts.Count; i++)
            {
                centres.Add(Vector3.zero);
                has.Add(false);
                Counts.Add(0);
            }
            CityManager city = InstanceBehavior<CityManager>.Instance;
            CityBuildingController[] buildings = city != null ? city.cityBuildingControllers : null;
            for (int i = 0; buildings != null && i < buildings.Length; i++)
            {
                CityBuildingController b = buildings[i];
                int d = b != null && b.building != null ? state.District(b.building.Neighbourhood) : -1;
                if (d >= 0)
                {
                    centres[d] += b.transform.position;
                    Counts[d]++;
                }
            }
            for (int i = 0; i < centres.Count; i++)
            {
                if (Counts[i] > 0)
                {
                    centres[i] /= Counts[i];
                    has[i] = true;
                }
            }
        }

        /// <summary>The road to each district as a straight line's guess, for every district not yet measured along the roads.</summary>
        public static void GuessRoads(WebPlace place, List<Vector3> centres, List<bool> has)
        {
            Site site = place.Site;
            for (int d = 0; d < centres.Count && d < site.Metres.Length; d++)
            {
                if (site.Measured[d])
                {
                    continue;
                }
                if (!place.HasAt || !has[d])
                {
                    site.Metres[d] = FarMetres;
                    continue;
                }
                Vector3 to = centres[d] - place.At;
                to.y = 0f;
                site.Metres[d] = Mathf.RoundToInt(to.magnitude * OnlineCatalogue.RoadFactor / 1000f);
            }
        }

        // ---------------------------------------------------------------- a site's day

        /// <summary>
        /// What a site's demand is worked out from, read into its <see cref="Site"/>: each product's online price and
        /// the going price, and the game's demand for it in each district. One pass over the game's product market
        /// for each product of the catalogue; once a day and when the player changes the catalogue.
        /// </summary>
        public static void ReadSite(WebPlace place, OnlineState state)
        {
            WebShop shop = place.Shop;
            Site site = place.Site;
            int districts = state.Districts.Count, lines = Math.Min(shop.Lines.Count, OnlineCatalogue.LinesMost);
            site.Size(lines, districts);
            site.Warehouse = place.Warehouse;
            int rank = place.Reg != null && !place.Warehouse ? MacroCatalogue.TypeRank(place.Reg.businessTypeName) : -1;
            site.Kind = rank >= 0 ? MacroCatalogue.KindOfTypeIs[rank] : MacroCatalogue.General;
            float index = PriceIndex(state);
            List<ProductMarketEntry> market = Save != null ? Save.productMarketEntries : null;
            for (int l = 0; l < lines; l++)
            {
                WebLine line = shop.Lines[l];
                Item item = ItemsGetter.GetByName(line.Item, true);
                int own = item != null && place.Reg != null ? Mathf.RoundToInt(ItemHelper.GetPrice(line.Item, place.Reg) * 100f) : 0;
                site.Ask[l] = line.Price > 0 ? line.Price : own;
                site.Going[l] = item != null ? Mathf.Max(1, Mathf.RoundToInt(item.DefaultMarketPrice * index * 100f)) : 0;
                ProductMarketEntry entry = null;
                for (int m = 0; market != null && m < market.Count && entry == null; m++)
                {
                    entry = market[m] != null && market[m].itemName == line.Item ? market[m] : null;
                }
                for (int d = 0; d < districts; d++)
                {
                    site.Demand[l * districts + d] = entry != null && entry.demandValues != null ? 0 : NoDemand;
                }
                for (int v = 0; entry != null && entry.demandValues != null && v < entry.demandValues.Count; v++)
                {
                    NeighborhoodDemand demand = entry.demandValues[v];
                    int d = demand != null ? state.District(demand.neighborhood) : -1;
                    if (d >= 0)
                    {
                        site.Demand[l * districts + d] = Mathf.Clamp(demand.demand, 0, 100);
                    }
                }
            }
        }

        // What the city's customers pay over the game's market price, on average over the districts (the game's own indices, 1.2 to 1.7 by who lives there).
        private static float PriceIndex(OnlineState state)
        {
            float sum = 0f;
            int count = 0;
            for (int d = 0; d < state.Districts.Count; d++)
            {
                float index;
                if (CitizenHelper.averagePriceIndicesInNeighborhoods.TryGetValue(state.Districts[d], out index) && index > 0f)
                {
                    sum += index;
                    count++;
                }
            }
            return count > 0 ? sum / count : 1.4f;
        }

        /// <summary>The shop's own price of a product, cents (the game's: the business's retail price, or the market price where it has set none).</summary>
        public static int ShopPrice(WebPlace place, string item)
        {
            return place.Reg != null && ItemsGetter.GetByName(item, true) != null ? Mathf.RoundToInt(ItemHelper.GetPrice(item, place.Reg) * 100f) : 0;
        }

        public static string ItemName(string item)
        {
            return string.IsNullOrEmpty(item) ? "?" : LocalizorManager.IsLocalizedKey(item) ? item.GetLocalization() : item;
        }

        // ---------------------------------------------------------------- stock

        // What a building keeps goods in, behind the counter first: storage and pallets, then the showcase shelves customers
        // buy from. A shelf or a till is never counted as storage, whatever its tags: the game needs each to keep its one
        // load (ItemHelper.GetStockInstance), and only its own call may take from it.
        private static bool IsStorage(Item item)
        {
            return item != null && (item.type & (ItemType.PointOfSale | ItemType.ShowcaseShelf)) == 0
                   && (item.HasTag(TagRef.Itemtag.isstockcontainer) || item.HasTag(TagRef.Itemtag.iswarehousestorage));
        }

        private static bool IsShelf(Item item)
        {
            return item != null && (item.type & ItemType.ShowcaseShelf) != 0;
        }

        private static bool Loose(CargoInstance cargo)
        {
            return cargo != null && cargo.amount > 0 && !string.IsNullOrEmpty(cargo.itemName) && cargo.nestedCargoInstances.Count == 0 && !cargo.IsSealed;
        }

        /// <summary>The retail products a building has in stock, each once: what its site can offer. One pass over what stands in the building.</summary>
        public static void Stocked(BuildingRegistration reg, List<string> into)
        {
            into.Clear();
            if (reg == null || reg.itemInstances == null)
            {
                return;
            }
            foreach (ItemInstance holder in reg.itemInstances.Values)
            {
                Item kind = holder != null ? holder.ItemCached : null;
                if (holder == null || holder.cargoInstances == null || (!IsStorage(kind) && !IsShelf(kind)))
                {
                    continue;
                }
                for (int c = 0; c < holder.cargoInstances.Count; c++)
                {
                    CargoInstance cargo = holder.cargoInstances[c];
                    if (!Loose(cargo) || into.Contains(cargo.itemName))
                    {
                        continue;
                    }
                    Item item = cargo.ItemCached;
                    if (item != null && (item.type & ItemType.RetailProduct) != 0)
                    {
                        into.Add(cargo.itemName);
                    }
                }
            }
        }

        /// <summary>Units of a product in the building's storage, pallets and showcase shelves. One pass over what stands in the building.</summary>
        public static int InStock(BuildingRegistration reg, string item)
        {
            int units = 0;
            if (reg == null || reg.itemInstances == null)
            {
                return 0;
            }
            foreach (ItemInstance holder in reg.itemInstances.Values)
            {
                Item kind = holder != null ? holder.ItemCached : null;
                if (holder == null || holder.cargoInstances == null || (!IsStorage(kind) && !IsShelf(kind)))
                {
                    continue;
                }
                for (int c = 0; c < holder.cargoInstances.Count; c++)
                {
                    CargoInstance cargo = holder.cargoInstances[c];
                    units += Loose(cargo) && cargo.itemName == item ? cargo.amount : 0;
                }
            }
            return units;
        }

        /// <summary>The stock of every product of a site's catalogue, into its <see cref="Site.Stock"/>.</summary>
        public static void CountStock(WebPlace place)
        {
            for (int l = 0; l < place.Shop.Lines.Count && l < place.Site.Stock.Length; l++)
            {
                place.Site.Stock[l] = InStock(place.Reg, place.Shop.Lines[l].Item);
            }
        }

        /// <summary>
        /// Takes units of a product out of the building's real stock: from storage and pallets first
        /// (ItemInstance.ReduceFromCargo, which takes an emptied load out of its holder and tells the game's screens),
        /// then from the showcase shelves (ItemHelper.SubtractFromStock, the call a customer's purchase makes: it
        /// refills the shelf from storage and raises the game's own "out of stock" task). An emptied box of the kind
        /// the game throws away is thrown away as the game does. Answers how many it got, never more than there were,
        /// and what they cost the shop in cents. One pass over what stands in the building.
        /// </summary>
        public static int Take(BuildingRegistration reg, string item, int wanted, out int cost)
        {
            cost = 0;
            if (reg == null || reg.itemInstances == null || wanted <= 0 || string.IsNullOrEmpty(item))
            {
                return 0;
            }
            Holders.Clear();
            foreach (ItemInstance holder in reg.itemInstances.Values)   // (listed first: taking from one may change the building's list)
            {
                Item kind = holder != null ? holder.ItemCached : null;
                if (holder != null && holder.cargoInstances != null && IsStorage(kind))
                {
                    Holders.Add(holder);
                }
            }
            int storage = Holders.Count;
            foreach (ItemInstance holder in reg.itemInstances.Values)
            {
                Item kind = holder != null ? holder.ItemCached : null;
                if (holder != null && holder.cargoInstances != null && !IsStorage(kind) && IsShelf(kind))
                {
                    Holders.Add(holder);
                }
            }
            int got = 0;
            float paid = 0f;
            Emptied.Clear();
            for (int h = 0; h < Holders.Count && got < wanted; h++)
            {
                ItemInstance holder = Holders[h];
                if (h < storage)
                {
                    int before = got;
                    for (int c = holder.cargoInstances.Count - 1; c >= 0 && got < wanted; c--)
                    {
                        CargoInstance cargo = holder.cargoInstances[c];
                        if (!Loose(cargo) || cargo.itemName != item)
                        {
                            continue;
                        }
                        int n = Math.Min(wanted - got, cargo.amount);
                        paid += n * cargo.pricePerUnit;
                        holder.ReduceFromCargo(cargo, n);
                        got += n;
                    }
                    if (got > before && holder.cargoInstances.Count == 0 && holder.ItemCached.HasTag(TagRef.Itemtag.discardcontainerwhenempty))
                    {
                        Emptied.Add(holder);
                    }
                    continue;
                }
                if (holder.cargoInstances.Count != 1)
                {
                    continue;   // (a shelf has one load: ItemHelper.GetStockInstance)
                }
                CargoInstance stock = holder.cargoInstances[0];
                Item shelf = holder.ItemCached;
                if (!Loose(stock) || stock.itemName != item || shelf.producerSettings == null || !shelf.producerSettings.ResourcesRequired)
                {
                    continue;   // (a shelf that uses up no goods would answer yes and give nothing: it has nothing to pick)
                }
                int take = Math.Min(wanted - got, stock.amount);
                float each = stock.pricePerUnit;
                if (holder.SubtractFromStock(take))   // (an emptied shelf is refilled from storage inside the call: what left it is what was asked for)
                {
                    paid += take * each;
                    got += take;
                }
            }
            for (int i = 0; i < Emptied.Count; i++)
            {
                reg.RemoveItemInstanceFromBuilding(Emptied[i]);
            }
            Emptied.Clear();
            Holders.Clear();
            cost = Mathf.RoundToInt(paid * 100f);
            if (got > 0)
            {
                Taken.Add(new DeliveryItem(item, -got));
            }
            return got;
        }

        /// <summary>
        /// After an hour's picking at a place: a warehouse gets a line in its own delivery record, as the game's
        /// logistics write one (BizMan's "runs out in" reads it), and the game is told that stock has moved, by the
        /// flag its own deliveries set (GameManager.shouldUpdateAfterDeliveries).
        /// </summary>
        public static void Picked(BuildingRegistration reg)
        {
            if (Taken.Count == 0)
            {
                return;
            }
            var warehouse = reg as Warehouse;
            if (warehouse != null && warehouse.deliveryTransactions != null)
            {
                var record = new DeliveryTransaction();
                record.SetItems(Taken);
                warehouse.deliveryTransactions.Add(record);
                if (warehouse.deliveryTransactions.Count > DeliveriesKept)
                {
                    warehouse.deliveryTransactions.RemoveRange(0, warehouse.deliveryTransactions.Count - DeliveriesKept);
                }
            }
            Taken.Clear();
            if (InstanceBehavior<GameManager>.Instance != null)
            {
                InstanceBehavior<GameManager>.Instance.shouldUpdateAfterDeliveries = true;
            }
        }

        /// <summary>
        /// Puts returned units back into the building with the game's own delivery call
        /// (ItemHelper.DeliverCargoToBuilding, as its logistics fill a building): into storage and pallets that hold
        /// the product or have room. Answers how many found room; the rest is written off.
        /// </summary>
        public static int PutBack(BuildingRegistration reg, string item, int units, int unitCost)
        {
            if (reg == null || reg.itemInstances == null || units <= 0 || ItemsGetter.GetByName(item, true) == null)
            {
                return 0;
            }
            var cargo = new CargoInstance(item, units, unitCost / 100f);
            ItemHelper.DeliverCargoToBuilding(cargo, reg, IsStorageHolder);
            int back = units - Math.Max(0, cargo.amount);
            if (back > 0 && InstanceBehavior<GameManager>.Instance != null)
            {
                InstanceBehavior<GameManager>.Instance.shouldUpdateAfterDeliveries = true;
            }
            return back;
        }

        private static readonly Func<ItemInstance, bool> IsStorageHolder = holder => holder != null && IsStorage(holder.ItemCached);

        // ---------------------------------------------------------------- staff and vans

        /// <summary>
        /// The staff who pick at a place in an hour of today. At a business: everybody with the game's customer
        /// service skill whose shift today covers the hour and who is at work (the game has no skill of its own for
        /// handling stock: its shelves are refilled by nobody in particular at the end of the last shift,
        /// RetailBusinessSimulator.RestockShelves). At a warehouse, which has no shifts: its delivery drivers, between
        /// seven and seven, less any who is out on a round. Nobody while the business is shut.
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

        /// <summary>
        /// The vans that can take a round this hour: every slot of a warehouse of the player's that has a vehicle and
        /// a driver who is at work and may drive it (Warehouse.CanDriverOperateVehicle), with the game's cargo
        /// capacity of the vehicle as its parcels. A van known from before keeps its time; one whose driver or vehicle
        /// has gone is dropped once its day is over. One pass over the save's buildings and their slots.
        /// </summary>
        public static int ReadVans(OnlineState state, int day)
        {
            for (int i = 0; i < state.Vans.Count; i++)
            {
                state.Vans[i].Ready = false;
            }
            int ready = 0;
            List<BuildingRegistration> all = Save != null ? Save.BuildingRegistrations : null;
            List<VehicleInstance> vehicles = Save != null ? Save.VehicleInstances : null;
            for (int i = 0; all != null && vehicles != null && i < all.Count; i++)
            {
                var warehouse = all[i] as Warehouse;
                if (warehouse == null || !warehouse.RentedByPlayer || warehouse.vehicleSlots == null)
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
                    ready++;
                }
            }
            for (int i = state.Vans.Count - 1; i >= 0; i--)
            {
                if (!state.Vans[i].Ready && state.Vans[i].Day < day)
                {
                    state.Vans.RemoveAt(i);
                }
            }
            return ready;
        }

        // ---------------------------------------------------------------- the busiest business (a test tool)

        /// <summary>The business that took most over its counter in the days the game keeps (BuildingRegistration.orderHistory); a warehouse if the player has nothing else.</summary>
        public static BuildingRegistration Busiest(OnlineState state)
        {
            Candidates(Regs);
            BuildingRegistration best = null;
            float most = -1f;
            for (int i = 0; i < Regs.Count; i++)
            {
                BuildingRegistration reg = Regs[i];
                if (state.Shop(Place(reg)) != null)
                {
                    continue;
                }
                float took = 0f;
                for (int d = 0; reg.orderHistory != null && d < reg.orderHistory.Count; d++)
                {
                    took += reg.orderHistory[d] != null ? reg.orderHistory[d].totalRevenue : 0f;
                }
                if (took > most)
                {
                    most = took;
                    best = reg;
                }
            }
            Regs.Clear();
            return best;
        }

        // ---------------------------------------------------------------- money

        /// <summary>Pays cents in as a transaction of the mod's own at a business, dated.</summary>
        public static void Give(int cents, string key, WebPlace place, int? day)
        {
            if (cents > 0 && Save != null)
            {
                GameManager.ChangeMoneySafe(cents / 100f, Info(key, place), day, place != null ? place.Address : null, true);
            }
        }

        /// <summary>
        /// Takes cents as a transaction of the mod's own at a business. A cost of the business is marked deductible as
        /// the game marks its own. Unless <paramref name="force"/>, the game's call refuses a charge the cash does not
        /// cover: false, and nothing is taken.
        /// </summary>
        public static bool Take(int cents, string key, WebPlace place, int? day, bool deductible, bool force)
        {
            if (cents <= 0)
            {
                return true;
            }
            if (Save == null)
            {
                return false;
            }
            TransactionInfo info = Info(key, place);
            if (deductible)
            {
                info.SetTaxDeductibleName(DeductKey.GetLocalization());
            }
            return GameManager.ChangeMoneySafe(-cents / 100f, info, day, place != null ? place.Address : null, force);
        }

        // (a dictionary each: the game keeps hold of a transaction's data for as long as it lists the transaction)
        private static TransactionInfo Info(string key, WebPlace place)
        {
            return new TransactionInfo(key, new Dictionary<string, string>(2) { { "name", place != null && place.Shop != null ? place.Shop.Name : string.Empty } });
        }

        // ---------------------------------------------------------------- the game's statement of the business

        /// <summary>
        /// Enters a site's sales that are not yet in the game's books into the game's statement of the business for a
        /// day (the one its nightly run has just made): each product's takings among the statement's sales and its
        /// cost among its goods, less the refunds, with the totals and the day's profit made to match. From there the
        /// game's yearly tax, the mod's quarterly return, the bank's reading of the player's means and every page that
        /// shows a business's takings count the online sales as the business's own, once. False if the game has no
        /// statement for that business and day: the sums are kept and entered the next night.
        /// </summary>
        public static bool Book(WebPlace place, int day, out int sales, out int goods)
        {
            sales = goods = 0;
            WebShop shop = place.Shop;
            long takings = 0, cost = 0;
            for (int l = 0; l < shop.Lines.Count; l++)
            {
                takings += Math.Max(0, shop.Lines[l].BookTakings);
                cost += Math.Max(0, shop.Lines[l].BookCost);
            }
            if (takings == 0 && cost == 0)
            {
                return true;   // (refunds alone wait for takings to come off)
            }
            List<FinancialSummary> summaries = Save != null ? Save.financialSummaries : null;
            FinancialSummary summary = null;
            for (int i = summaries != null ? summaries.Count - 1 : -1; i >= 0 && summary == null; i--)
            {
                summary = summaries[i] != null && summaries[i].dayNumber == day ? summaries[i] : null;
            }
            FinancialSummary.BusinessIncomeStatement statement = null;
            for (int b = 0; summary != null && summary.businessIncomeStatements != null && place.Address != null && b < summary.businessIncomeStatements.Count && statement == null; b++)
            {
                FinancialSummary.BusinessIncomeStatement s = summary.businessIncomeStatements[b];
                statement = s != null && s.Address == place.Address ? s : null;
            }
            if (statement == null)
            {
                return false;
            }
            long refunds = Math.Min(takings, Math.Max(0, shop.BookRefunds)), net = takings - refunds;
            if (statement.Sales == null)
            {
                statement.Sales = new List<FinancialSummary.BusinessIncomeStatement.TransactionGroupEntry>();
            }
            if (statement.Resources == null)
            {
                statement.Resources = new List<FinancialSummary.BusinessIncomeStatement.TransactionGroupEntry>();
            }
            float before = statement.TotalProfit;
            for (int l = 0; l < shop.Lines.Count; l++)
            {
                WebLine line = shop.Lines[l];
                if (line.BookTakings > 0 && takings > 0)
                {
                    Entry(statement.Sales, line.Item).Amount += (float)((double)line.BookTakings * net / takings / 100.0);
                }
                if (line.BookCost > 0)
                {
                    Entry(statement.Resources, line.Item).Amount += line.BookCost / 100f;
                    line.BookCost = 0;
                }
                line.BookTakings = 0;
            }
            statement.TotalSales += net / 100f;
            statement.TotalResources += cost / 100f;
            statement.TotalProfit = statement.TotalSales - statement.TotalResources - statement.TotalOngoing;   // (the game's own sum, FinancialSummaryHelper.CreateBusinessIncomeStatement)
            summary.totalBusinessProfit += statement.TotalProfit - before;
            FinancialSummaryHelper.UpdateIncomeStatementTotalProfit(summary);
            shop.BookRefunds -= (int)refunds;
            sales = (int)net;
            goods = (int)cost;
            return true;
        }

        private static FinancialSummary.BusinessIncomeStatement.TransactionGroupEntry Entry(List<FinancialSummary.BusinessIncomeStatement.TransactionGroupEntry> list, string item)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] != null && list[i].ItemName == item)
                {
                    return list[i];
                }
            }
            var entry = new FinancialSummary.BusinessIncomeStatement.TransactionGroupEntry { ItemName = item, Amount = 0f };
            list.Add(entry);
            return entry;
        }
    }
}
