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
using Buildings.BuildingTypes.Shared.BusinessRequirement;
using InteriorDesign;
using Localizor;
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
    ///   the basket          OrderEntries more in a customer of the coming hour, by the size of its order, as the game itself adds them
    ///                       (CustomerEntriesCalculatorRetail.AddProductsToCustomersEntriesList): a product the shop has on
    ///                       its shelves (reg.cachedAvailableProducts), never an entrance fee or a ticket, a service only
    ///                       once a customer. The game checks the stock and the price when the customer pays.
    ///   a refresh           the charge through business pressure's own (GameManager.ChangeMoneySafe at the address)
    /// Nothing here runs on a timer of its own.
    /// </summary>
    /// <summary>Today's customers of a business as the game counts them (InteriorGame.Count), for InteriorRules.Split.</summary>
    public struct CustomerCount
    {
        public int Drawn, Booked, OtherMod, Served;          // the game's own draw (this mod's figure on it), the feature's bookings, Meaningful Ambience's, served so far
        public int BaseMille, Traffic, Marketing, Own, Written;   // the game's base multiplier (thousandths) and the promotion figure's parts
        public int OtherPercent;                             // the rest of this mod's per cent: an issue's change and the ambient sum
    }

    /// <summary>One of the game's own rules for a business type, as the tab shows it.</summary>
    public sealed class GameRule
    {
        public const int NamesMost = 5;
        public bool Demand;                // a want of its customers; otherwise a requirement to open
        public string Kind = string.Empty; // the want's type, or the requirement's own text key
        public string Class = string.Empty;   // the game's class of it
        public bool Met;
        public int Chance = -1;            // per cent of customers who want it
        public int Have = -1, Need = -1;   // placed and needed (a count), or the interior score and its minimum
        public readonly List<string> Placed = new List<string>(GameRule.NamesMost);    // placed items that meet it
        public readonly List<string> Examples = new List<string>(GameRule.NamesMost);  // where none is placed: some of the game's own that would
    }

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
            s.Placed.Clear();
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
                int points = InteriorRules.KindPoints((DecorCategory)category, kind.Value);
                s.Points[category] += points;
                s.Placed.Add(new InteriorItem { Name = kind.Key, Category = (DecorCategory)category, Copies = kind.Value, Points = points });
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

        // ---------------------------------------------------------------- the tab's pages: today's customers

        /// <summary>
        /// Today's customers of a business as the game counts them, for the tab's page of where they came from: the day's
        /// own list (CustomerDraw.Entries: those served and those to come), the ones the feature booked (an office's),
        /// with Meaningful Ambience on what is over the game's own draw of each hour (its calculator's GetCustomersByHour),
        /// and the parts of the business's promotion figure (BusinessHelper.UpdatePromotion's: its street's traffic, its
        /// marketing at the district's marketing strength; what this mod wrote; the rest of this mod's per cent). Read when
        /// the page is shown, never per frame. False when the game keeps no list for it.
        /// </summary>
        public static bool Count(BuildingRegistration reg, Func<CustomerEntry, bool> ours, out CustomerCount c)
        {
            c = new CustomerCount();
            List<CustomerEntry> entries = CustomerDraw.Entries(reg);
            if (entries == null)
            {
                return false;
            }
            try
            {
                int today = TimeHelper.CurrentDay;
                var perHour = new int[24];
                for (int i = 0; i < entries.Count; i++)
                {
                    CustomerEntry e = entries[i];
                    if (e == null || e.spawnTime == null || e.spawnTime.Day != today)
                    {
                        continue;
                    }
                    c.Served += e.completed ? 1 : 0;
                    if (ours != null && ours(e))
                    {
                        c.Booked++;
                    }
                    else if (e.spawnTime.Hour >= 0 && e.spawnTime.Hour < 24)
                    {
                        perHour[e.spawnTime.Hour]++;
                    }
                }
                int drawn = 0;
                for (int h = 0; h < 24; h++)
                {
                    drawn += perHour[h];
                }
                if (OtherMods.MeaningfulAmbience.Loaded)
                {
                    CustomerEntriesCalculator calculator = CustomerEntriesCalculatorFactory.GetCustomerEntriesCalculator(reg);
                    float initial = calculator.GetInitialCustomers();
                    var day = TimeHelper.GetDayOfWeek();
                    for (int h = 0; h < 24; h++)
                    {
                        if (perHour[h] > 0)
                        {
                            int target = BusinessHelper.IsBusinessOpen(reg, h) ? calculator.GetCustomersByHour(h, day, initial) : 0;
                            c.OtherMod += Math.Max(0, perHour[h] - target);
                        }
                    }
                }
                c.Drawn = drawn - c.OtherMod;
                c.BaseMille = Mathf.RoundToInt(SaveGameManager.Current.gameVariables.baseCustomerPromotionMultiplier * 1000f);
                Promotion promotion = reg.promotion;
                if (promotion != null)
                {
                    float strength = 0.5f;
                    try
                    {
                        strength = NeighborhoodHelper.GetData(reg.Neighborhood).marketingStrength;
                    }
                    catch (Exception)
                    {
                        // (the game's own default strength)
                    }
                    c.Traffic = InteriorRules.Clamp(promotion.trafficIndex, 0, 100);
                    c.Own = InteriorRules.Clamp(Mathf.RoundToInt(promotion.trafficIndex + promotion.marketing * strength), 0, 100);
                    c.Written = InteriorRules.Clamp(promotion.total, 0, 100);
                    c.Marketing = c.Own - c.Traffic;
                }
                string place = BusinessGame.Place(reg);
                int issue = BusinessState.Loaded && BusinessState.EffectAt(place) >= 0 ? BusinessState.Effects[BusinessState.EffectAt(place)].Percent : 0;
                c.OtherPercent = BusinessCatalogue.ClampFootfall(issue) + BusinessCatalogue.ClampAmbient(BusinessGame.Ambient != null ? BusinessGame.Ambient(reg) : 0);
                return true;
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                return false;
            }
        }

        /// <summary>
        /// Business pressure's own change of a business's customers now, per cent (a complaint's -6% and the like; its
        /// state, BusinessState.Effects, as the tab's page of customers reads it), or 0: what the interior's shortfall gives
        /// way to so that the two together stay inside the bound. A look along a list of a few; asked on each footfall
        /// write and each morning, never per frame.
        /// </summary>
        public static int IssueDip(BuildingRegistration reg)
        {
            try
            {
                if (reg == null || !BusinessState.Loaded)
                {
                    return 0;
                }
                int at = BusinessState.EffectAt(BusinessGame.Place(reg));
                return at >= 0 && BusinessState.Effects[at].UntilDay > TimeHelper.CurrentDay ? BusinessCatalogue.ClampFootfall(BusinessState.Effects[at].Percent) : 0;
            }
            catch (Exception)
            {
                return 0;
            }
        }

        // ---------------------------------------------------------------- the tab's pages: the game's own rules

        /// <summary>
        /// The game's own rules for the business's type, for the tab's page of them: what its customers may want
        /// (BusinessType.customerDemandSets, each a CustomerDemand of the game's: music, seating, sinks, toilets, privacy,
        /// uniforms, workout variety, the interior design) and what the business needs to open (BusinessType
        /// .businessRequirements: tills, workstations, products, the licence...). Whether each is met is the game's own
        /// answer: its daily list of the wants met (reg.cachedFulfilledCustomerDemands) and BusinessHelper.IsRequirementMet.
        /// The items named are the placed ones that meet it, or, where none is placed, a few of the game's own that would.
        /// Read when the page is shown; nothing is changed.
        /// </summary>
        public static void Rules(BuildingRegistration reg, List<GameRule> into)
        {
            into.Clear();
            BusinessType type = BusinessTypeHelper.GetData(reg);
            if (type == null)
            {
                return;
            }
            float weight = 0f;
            try
            {
                weight = NeighborhoodHelper.GetData(reg.Neighborhood).customerDemandsWeight;
            }
            catch (Exception)
            {
                weight = 0f;
            }
            for (int i = 0; type.customerDemandSets != null && i < type.customerDemandSets.Count; i++)
            {
                CustomerDemandSet set = type.customerDemandSets[i];
                if (set == null || string.IsNullOrEmpty(set.type))
                {
                    continue;
                }
                var rule = new GameRule { Demand = true, Kind = set.type, Chance = Mathf.RoundToInt(Mathf.Clamp01(weight * set.weight) * 100f) };
                rule.Met = reg.cachedFulfilledCustomerDemands != null && reg.cachedFulfilledCustomerDemands.Contains(set.type);
                CustomerDemand demand = null;
                try
                {
                    demand = CustomerDemandHelper.GetByType(set.type);
                }
                catch (Exception)
                {
                    demand = null;
                }
                rule.Class = demand != null ? demand.GetType().Name : string.Empty;
                Func<BigAmbitions.Items.Item, bool> meets = Meets(rule.Class);
                if (meets != null)
                {
                    Named(reg, meets, rule);
                }
                if (demand is InteriorDesignCustomerDemand)
                {
                    rule.Have = InteriorScore(reg);
                    rule.Need = Minimum(reg.Neighborhood);
                }
                into.Add(rule);
            }
            for (int i = 0; type.businessRequirements != null && i < type.businessRequirements.Count; i++)
            {
                BusinessRequirement need = type.businessRequirements[i];
                if (need == null)
                {
                    continue;
                }
                var rule = new GameRule { Demand = false, Class = need.GetType().Name };
                try
                {
                    rule.Kind = need.GetLocalizeKey();
                    rule.Met = BusinessHelper.IsRequirementMet(reg, need);
                    string[] items = need.GetItems();
                    ItemType of = need.GetItemType();
                    if (need is SpecificItemsInBuildingBySqm bySqm)
                    {
                        rule.Need = bySqm.GetRequiredItemCount(reg);
                    }
                    else if (need is ItemsOfTypeInBuildingBySqm typeBySqm)
                    {
                        rule.Need = typeBySqm.GetRequiredItemCount(reg);
                    }
                    if (items != null && items.Length > 0)
                    {
                        var wanted = new HashSet<string>(items, StringComparer.Ordinal);
                        Named(reg, item => wanted.Contains(item.itemName), rule);
                    }
                    else if (of != 0)
                    {
                        Named(reg, item => (item.type & of) != 0, rule);
                    }
                }
                catch (Exception e)
                {
                    Logger?.Error(e);
                }
                into.Add(rule);
            }
        }

        // The game's own test of each kind of want, as a test of one item: what each CustomerDemand looks for.
        private static Func<BigAmbitions.Items.Item, bool> Meets(string demandClass)
        {
            switch (demandClass)
            {
                case "MusicCustomerDemand": return item => (item.type & ItemType.RadioSource) != 0;
                case "SeatingCustomerDemand": return item => (item.type & ItemType.Seat) != 0;
                case "SinkCustomerDemand": return item => (item.type & ItemType.Sink) != 0;
                case "ToiletCustomerDemand": return item => (item.type & ItemType.Toilet) != 0;
                case "ToiletPrivacyCustomerDemand": return item => item.HasTag(TagRef.Itemtag.isprivacytoilet);
                case "WorkoutVarietyCustomerDemand": return item => (item.type & ItemType.WorkoutMachine) != 0;
                default: return null;
            }
        }

        // The placed items that meet a rule (how many, and the names of a few), or a few of the game's own that would.
        private static void Named(BuildingRegistration reg, Func<BigAmbitions.Items.Item, bool> meets, GameRule rule)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            int count = 0;
            if (reg.itemInstances != null)
            {
                foreach (ItemInstance instance in reg.itemInstances.Values)
                {
                    BigAmbitions.Items.Item item = instance != null ? instance.ItemCached : null;
                    if (item == null || !meets(item))
                    {
                        continue;
                    }
                    count++;
                    if (seen.Add(item.itemName) && rule.Placed.Count < GameRule.NamesMost)
                    {
                        rule.Placed.Add(ItemName(item.itemName));
                    }
                }
            }
            if (rule.Have < 0)
            {
                rule.Have = count;
            }
            if (count > 0 || ItemsGetter.AllItems == null)
            {
                return;
            }
            var names = new List<string>(8);
            foreach (BigAmbitions.Items.Item item in ItemsGetter.AllItems)
            {
                if (item != null && item.isFurniture && (item.type & (ItemType.RetailProduct | ItemType.ServiceProduct | ItemType.ForSale)) == 0 && !item.HasTag(TagRef.Itemtag.dev)
                    && meets(item) && seen.Add(item.itemName))
                {
                    names.Add(ItemName(item.itemName));
                }
            }
            names.Sort(StringComparer.CurrentCulture);
            for (int i = 0; i < names.Count && rule.Examples.Count < GameRule.NamesMost; i++)
            {
                rule.Examples.Add(names[i]);
            }
        }

        /// <summary>An item's name as the game shows it.</summary>
        public static string ItemName(string item)
        {
            return string.IsNullOrEmpty(item) ? "?" : LocalizorManager.IsLocalizedKey(item) ? item.GetLocalization() : item;
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

        // ---------------------------------------------------------------- an office's clients

        /// <summary>
        /// Books an office of the player's more clients for the coming hour, as the game books them: its own calculator
        /// for offices (CustomerEntriesCalculatorOffice.GetCustomersEntriesForHour, with the district's demand weight), the
        /// clients added to the day's list beside the game's. Only an office the game's office simulator serves
        /// (BusinessType.simulator is OfficeBusinessSimulator) that takes money, meets its requirements and is open then;
        /// never more than the stations staffed then (one client a station an hour, as the simulator serves them) less the
        /// clients already booked. The booked ones are kept in <paramref name="ours"/>. Returns how many.
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
            int added = 0;
            for (int i = 0; made != null && i < made.Count && added < n; i++)   // (the calculator's own list: taken from at once)
            {
                if (made[i] != null)
                {
                    entries.Add(made[i]);
                    ours.Add(made[i]);
                    added++;
                }
            }
            return added;
        }

        // The stations an office's simulator serves a client at in an hour: a shift on it then (not a cleaning one) with
        // somebody of the business type's own skills at it, and nothing the station needs missing.
        private static int Stations(BuildingRegistration reg, BusinessType type, int hour)
        {
            ScheduleDay today = BuildingHelper.GetTodaySchedule(reg);
            if (today == null || today.workShifts == null || type.employeePrimarySkills == null)
            {
                return 0;
            }
            int count = 0;
            foreach (WorkShift shift in today.workShifts)
            {
                ItemInstance station;
                if (shift == null || string.IsNullOrEmpty(shift.employeeId) || shift.type == WorkShiftType.Cleaning || shift.startingHour > hour || shift.endingHour <= hour
                    || !reg.itemInstances.TryGetValue(shift.itemInstanceId, out station) || ItemHelper.HasAnyMissingRequirements(station))
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

        // ---------------------------------------------------------------- the basket

        /// <summary>
        /// The customers still to come at a shop this hour (not those due in the next two minutes) and the next hour,
        /// each looked at once: the products in its order (never the door's entrance fee) times the share (per mille,
        /// InteriorRules.BasketExtra) more of what the shop sells and has on its shelves, each a different one while there
        /// are enough. Returns how many were added. Only a shop the game draws retail customers for (not a casino, a cinema
        /// or a theatre, whose customers come for tickets). Asked once a game hour, as the hour turns.
        /// </summary>
        public static int Basket(BuildingRegistration reg, int share, bool office)
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
