using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using AI.Customers.CustomerEntries;
using BAModAPI;
using BigAmbitions.Items;
using BigAmbitions.Neighborhoods;
using BigAmbitions.Tags;
using Entities;
using Helpers;
using InteriorDesign;
using RealLifeOverhaul.Economy;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul.Interiors
{
    /// <summary>
    /// What "interiors and decor matter" asks of the rest of the mod. Each is optional with a default, so the feature works
    /// alone; Scripts/Wiring/InteriorLinks.cs fills them in. The feature never calls another feature directly.
    /// </summary>
    public static class InteriorBridges
    {
        /// <summary>Business pressure's writer of a business's footfall (BusinessDirector.Footfall). Default: nothing is written.</summary>
        public static Economy.CityBridges.FootfallWriter Footfall;

        /// <summary>True while the cleaning service looks after the business at a place. Default: never.</summary>
        public static Func<string, bool> Cleaned;

        /// <summary>A complaint about the place starts at a business, as business pressure's own story. False if it will not start one today. Default: never.</summary>
        public static Func<BuildingRegistration, bool> Complain;

        /// <summary>A piece for tomorrow's City Ledger, one locale key with no placeholders (Wiring.Paper.Print). Default: nothing.</summary>
        public static Action<string> Story;

        /// <summary>A Flare draft about a striking interior at a business of the player's: its place and name. Default: nothing.</summary>
        public static Action<string, string> Striking;

        /// <summary>The district's income level, 60 to 140, by the game's name for it; false if it cannot be told. Default: from the game's class shares.</summary>
        public static Func<string, int> Income;

        public static void Reset()
        {
            Footfall = null;
            Cleaned = null;
            Complain = null;
            Story = null;
            Striking = null;
            Income = null;
        }
    }

    /// <summary>
    /// Everything the feature reads from the game or does to it:
    ///   the interior score  InteriorScoreCalculator.GetInteriorScorePercentage(reg.interiorDesigns), the game's own
    ///                       figure for walls and floors, worked out from the saved design (it is kept for no building)
    ///   the placed items    reg.itemInstances, each sorted once by its item's own tags and types (ItemsGetter.GetByName)
    ///   the floor area      BuildingHelper.GetBuildingSquareMeters
    ///   the minimum         NeighborhoodData.minimumInteriorScore, read live (another mod may set it)
    ///   the basket          one OrderEntry more in a customer of the coming hour, as the game itself adds one
    ///                       (CustomerEntriesCalculatorRetail.AddProductsToCustomersEntriesList): a product the shop has on
    ///                       its shelves (reg.cachedAvailableProducts), never an entrance fee or a ticket, a service only
    ///                       once a customer. The game checks the stock and the price when the customer pays.
    ///   a refresh           the charge through business pressure's own (GameManager.ChangeMoneySafe at the address)
    /// Nothing here runs on a timer of its own.
    /// </summary>
    internal static class InteriorGame
    {
        private const string Headquarters = "ba:businesstype_headquarters";
        private const string Empty = "ba:businesstype_empty";
        private const string Casino = "ba:businesstype_casino";
        private const string OfficeBuilding = "ba:buildingtype_office";
        private const string RetailBuilding = "ba:buildingtype_retail";

        public static IModLogger Logger;

        // An item's category by its name, worked out once: -1 for one that is not counted.
        private static readonly Dictionary<string, int> Sorted = new Dictionary<string, int>(256, StringComparer.Ordinal);
        private static readonly Dictionary<string, int> Copies = new Dictionary<string, int>(64, StringComparer.Ordinal);
        private static readonly List<string> Products = new List<string>(32);
        private static readonly ConditionalWeakTable<CustomerEntry, object> Seen = new ConditionalWeakTable<CustomerEntry, object>();
        private static readonly object Mark = new object();
        private const int Seat = 8, Table = 9, Kitchen = 10;   // flags of the sorting beyond the categories: for the rest corner

        // The game's own types of the things a business works with: equipment, whatever else they are.
        private const ItemType Working = ItemType.PointOfSale | ItemType.EmployeeWorkstation | ItemType.StorageShelf | ItemType.ShowcaseShelf | ItemType.FactoryMachine
                                         | ItemType.Security | ItemType.Computer | ItemType.Sink | ItemType.Toilet | ItemType.WorkoutMachine;

        // ---------------------------------------------------------------- which businesses

        /// <summary>What sort of place a building of the player's is to the feature.</summary>
        public static InteriorKind KindOf(BuildingRegistration reg)
        {
            try
            {
                if (reg == null || !GoingPublic.Mine(reg) || !reg.HasEstablishedBusiness || !reg.HasValidAddress || string.IsNullOrEmpty(reg.businessTypeName)
                    || reg.businessTypeName == Empty)
                {
                    return InteriorKind.None;
                }
                if (reg.businessTypeName == Headquarters || reg.GetBuildingType() == OfficeBuilding)
                {
                    return InteriorKind.Office;
                }
                return BusinessGame.IsBusiness(reg) ? InteriorKind.Shop : InteriorKind.None;
            }
            catch (Exception)
            {
                return InteriorKind.None;
            }
        }

        /// <summary>Every building of the player's the feature looks at.</summary>
        public static void Places(List<BuildingRegistration> into)
        {
            into.Clear();
            GameInstance save = SaveGameManager.Current;
            List<BuildingRegistration> all = save != null ? save.BuildingRegistrations : null;
            for (int i = 0; all != null && i < all.Count; i++)
            {
                if (KindOf(all[i]) != InteriorKind.None)
                {
                    into.Add(all[i]);
                }
            }
        }

        // ---------------------------------------------------------------- reading one business

        /// <summary>The game's own interior design score of a building, 0 to 100 (0 for one with no design saved).</summary>
        public static int InteriorScore(BuildingRegistration reg)
        {
            try
            {
                if (reg == null || reg.interiorDesigns == null || reg.interiorDesigns.Count == 0)
                {
                    return 0;
                }
                return InteriorRules.Clamp(InteriorScoreCalculator.GetInteriorScorePercentage(reg.interiorDesigns), 0, 100);   // (a design with no material the game knows gives no number: 0)
            }
            catch (Exception)
            {
                return 0;
            }
        }

        /// <summary>Everything the rules need of one business, read into a shop.</summary>
        public static void Read(BuildingRegistration reg, InteriorShop s)
        {
            s.Place = BusinessGame.Place(reg);
            s.Name = BusinessGame.Name(reg);
            s.Kind = KindOf(reg);
            s.Interior = InteriorScore(reg);
            s.Clean = BusinessGame.Cleanliness(reg);
            try
            {
                s.Sqm = Math.Max(0, BuildingHelper.GetBuildingSquareMeters(reg.Address));
            }
            catch (Exception)
            {
                s.Sqm = 0;
            }
            try
            {
                s.District = reg.Neighborhood ?? string.Empty;
            }
            catch (Exception)
            {
                s.District = string.Empty;
            }
            s.GameMinimum = Minimum(s.District);
            s.Income = IncomeOf(s.District);
            Items(reg, s);
        }

        /// <summary>The game's own minimum interior score for a district, as it stands now (0 where it asks for none).</summary>
        public static int Minimum(string district)
        {
            try
            {
                NeighborhoodData data = string.IsNullOrEmpty(district) ? null : NeighborhoodHelper.GetData(district);
                return data != null ? InteriorRules.Clamp(data.minimumInteriorScore, 0, 100) : 0;
            }
            catch (Exception)
            {
                return 0;
            }
        }

        private static int IncomeOf(string district)
        {
            try
            {
                if (InteriorBridges.Income != null)
                {
                    int level = InteriorBridges.Income(district);
                    if (level > 0)
                    {
                        return InteriorRules.Clamp(level, InteriorRules.IncomeLeast, InteriorRules.IncomeMost);
                    }
                }
                NeighborhoodData data = string.IsNullOrEmpty(district) ? null : NeighborhoodHelper.GetData(district);
                return data != null
                    ? InteriorRules.Clamp((CityModel.StartIncome(data.workingClassPercentage, data.middleClassPercentage, data.upperClassClassPercentage) + 5) / 10, InteriorRules.IncomeLeast, InteriorRules.IncomeMost)
                    : InteriorRules.IncomeMiddle;
            }
            catch (Exception)
            {
                return InteriorRules.IncomeMiddle;
            }
        }

        // The placed items: copies of each kind counted, then each kind's points by the rules.
        private static void Items(BuildingRegistration reg, InteriorShop s)
        {
            Array.Clear(s.Points, 0, s.Points.Length);
            Array.Clear(s.Items, 0, s.Items.Length);
            s.Kinds = 0;
            s.RestCorner = false;
            Copies.Clear();
            int flags = 0;
            Dictionary<string, ItemInstance> placed = reg.itemInstances;
            if (placed == null)
            {
                return;
            }
            foreach (ItemInstance instance in placed.Values)
            {
                if (instance == null || string.IsNullOrEmpty(instance.itemName))
                {
                    continue;
                }
                int sort = Sort(instance);
                flags |= sort >> 8;
                int category = sort & 0xff;
                if (category == 0xff)
                {
                    continue;
                }
                int n;
                Copies.TryGetValue(instance.itemName, out n);
                Copies[instance.itemName] = n + 1;
            }
            foreach (KeyValuePair<string, int> kind in Copies)
            {
                int category = Sorted[kind.Key] & 0xff;
                s.Points[category] += InteriorRules.KindPoints((DecorCategory)category, kind.Value);
                s.Items[category] += kind.Value;
                if (category < (int)DecorCategory.Furniture)
                {
                    s.Kinds++;
                }
            }
            Copies.Clear();
            s.RestCorner = (flags & (1 << (Seat - 8))) != 0 && (flags & (1 << (Table - 8))) != 0 && (flags & (1 << (Kitchen - 8))) != 0;
        }

        // An item's category in the low byte (0xff: not counted), and in the next bits whether it is a seat, a table, a kitchen's.
        private static int Sort(ItemInstance instance)
        {
            int sort;
            if (Sorted.TryGetValue(instance.itemName, out sort))
            {
                return sort;
            }
            sort = 0xff;
            try
            {
                BigAmbitions.Items.Item item = instance.ItemCached ?? ItemsGetter.GetByName(instance.itemName, true);
                if (item != null)
                {
                    sort = Classify(item);
                }
            }
            catch (Exception)
            {
                sort = 0xff;
            }
            Sorted[instance.itemName] = sort;
            return sort;
        }

        private static int Classify(BigAmbitions.Items.Item item)
        {
            ItemType t = item.type;
            if ((t & (ItemType.RetailProduct | ItemType.ServiceProduct | ItemType.ForSale)) != 0 || item.HasTag(TagRef.Itemtag.dev))
            {
                return 0xff;
            }
            bool seat = (t & ItemType.Seat) != 0 || item.HasTag(TagRef.Itemtag.seat) || item.HasTag(TagRef.Itemtag.isbed);
            bool table = (t & (ItemType.Table | ItemType.Desk)) != 0 || item.HasTag(TagRef.Itemtag.table);
            int extra = (seat ? 1 << Seat : 0) | (table ? 1 << Table : 0) | (item.HasTag(TagRef.Itemtag.kitchen) ? 1 << Kitchen : 0);
            DecorCategory c;
            if ((t & Working) != 0)
            {
                c = DecorCategory.Equipment;   // a till, a shelf, a workstation, a machine: a lit fridge is no lamp
            }
            else if (item.HasTag(TagRef.Itemtag.loudspeaker) || (t & ItemType.RadioSource) != 0)
            {
                c = DecorCategory.Audio;
            }
            else if (item.HasTag(TagRef.Itemtag.plant))
            {
                c = DecorCategory.Greenery;
            }
            else if (item.HasTag(TagRef.Itemtag.light) || (t & ItemType.LightSource) != 0)
            {
                c = DecorCategory.Lighting;
            }
            else if (item.HasTag(TagRef.Itemtag.flatonfloor) || (t & ItemType.FlatDecoration) != 0)
            {
                c = DecorCategory.Floor;
            }
            else if (item.HasTag(TagRef.Itemtag.decoration) || (t & (ItemType.Decoration | ItemType.Seasonal)) != 0)
            {
                c = DecorCategory.Art;
            }
            else if (seat || table)
            {
                c = DecorCategory.Furniture;
            }
            else if (item.isFurniture)
            {
                c = DecorCategory.Equipment;
            }
            else
            {
                return 0xff | extra;
            }
            return (int)c | extra;
        }

        // ---------------------------------------------------------------- selling nothing

        /// <summary>
        /// Why a business of the player's that takes money sells nothing now (InteriorRules.Stop), from the game's own
        /// figures: its closed switch and today's opening (BuildingHelper.GetTodaySchedule), what it has to sell
        /// (reg.cachedAvailableProducts: what is on its shelves, its services and entrances), the game's own test for a
        /// station with nobody on shift (BusinessHelper.IsMissingEmployeeAlertNeeded, the one behind its "missing employee"
        /// task), and today's completed orders (reg.unprocessedCompletedOrders), the last few of them. None for a place
        /// that takes no money (a headquarters) or when it cannot be told.
        /// </summary>
        public static SaleStop Stop(BuildingRegistration reg)
        {
            try
            {
                if (!BusinessGame.IsBusiness(reg))
                {
                    return SaleStop.None;
                }
                ScheduleDay today = BuildingHelper.GetTodaySchedule(reg);
                bool closed = reg.temporarilyClosed || today == null || !today.isOpen;
                bool stocked = reg.cachedAvailableProducts != null && reg.cachedAvailableProducts.Count > 0;
                bool staffed = !BusinessHelper.IsMissingEmployeeAlertNeeded(reg);
                int recent = 0, refused = 0;
                List<Order> orders = reg.unprocessedCompletedOrders;
                for (int i = orders != null ? orders.Count - 1 : -1; i >= 0 && recent < InteriorRules.RecentOrders; i--)
                {
                    Order order = orders[i];
                    if (order == null || order.entries == null || order.entries.Count == 0)
                    {
                        continue;
                    }
                    recent++;
                    bool accepted = false;
                    for (int k = 0; k < order.entries.Count && !accepted; k++)
                    {
                        accepted = order.entries[k] != null && order.entries[k].priceAccceptable;
                    }
                    refused += accepted ? 0 : 1;
                }
                return InteriorRules.Stop(closed, stocked, staffed, recent, refused);
            }
            catch (Exception)
            {
                return SaleStop.None;
            }
        }

        // ---------------------------------------------------------------- the basket

        /// <summary>
        /// The customers still to come at a shop this hour (not those due in the next two minutes) and the next hour,
        /// each looked at once: with a chance (per mille) one more product it sells and has on its shelves. Returns how
        /// many were added. Only a shop the game draws retail customers for (not a casino, a cinema or a theatre, whose
        /// customers come for tickets). Asked once a game hour, as the hour turns.
        /// </summary>
        public static int Basket(BuildingRegistration reg, int chance)
        {
            if (chance <= 0 || reg == null || reg.temporarilyClosed || !Retail(reg))
            {
                return 0;
            }
            List<CustomerEntry> entries = CustomerDraw.Entries(reg);
            if (entries == null || entries.Count == 0 || !Sellable(reg))
            {
                return 0;
            }
            int today = TimeHelper.CurrentDay, hour = TimeHelper.CurrentHour, added = 0;
            float soonest = TimeHelper.CurrentMinute + 2f;
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
                if (Random.Range(0, 1000) >= chance)
                {
                    continue;
                }
                string product = Products[Random.Range(0, Products.Count)];
                if (Service(product) && Has(e, product))
                {
                    continue;   // a service is had once a customer, as the game draws them
                }
                e.order.entries.Add(new OrderEntry { itemName = product });
                added++;
            }
            Products.Clear();
            return added;
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
        private static bool Sellable(BuildingRegistration reg)
        {
            Products.Clear();
            List<string> stocked = reg.cachedAvailableProducts;
            BusinessType type = BusinessTypeHelper.GetData(reg);
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

        // ---------------------------------------------------------------- refreshing the finishes

        /// <summary>What refreshing a business's finishes costs today, whole dollars at today's prices.</summary>
        public static int RefreshCost(int sqm)
        {
            return Prices.At(InteriorRules.RefreshCost(sqm));
        }

        /// <summary>Charges a refresh at the business. False (nothing taken) if the player cannot pay it.</summary>
        public static bool Charge(BuildingRegistration reg, int amount)
        {
            return BusinessGame.Charge(reg, amount, "reallife_transaction_interior_refresh");
        }
    }
}
