using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using AI.Customers.CustomerEntries;
using BAModAPI;
using BigAmbitions.Items;
using BigAmbitions.Neighborhoods;
using BigAmbitions.Tags;
using Helpers;
using InteriorDesign;
using RealLifeOverhaul.Economy;
using UnityEngine;

namespace RealLifeOverhaul.Interiors
{
    /// <summary>
    /// What interiors and decor asks of the rest of the mod, each optional so the feature works alone; Wiring/InteriorLinks
    /// fills them in.
    /// </summary>
    public static class InteriorBridges
    {
        /// <summary>Business pressure's writer of a business's footfall; unset, nothing is written.</summary>
        public static Economy.CityBridges.FootfallWriter Footfall;

        /// <summary>True while the cleaning service looks after the business at a place.</summary>
        public static Func<string, bool> Cleaned;

        /// <summary>Starts a complaint about the place as business pressure's own story; false if it will not today.</summary>
        public static Func<BuildingRegistration, bool> Complain;

        /// <summary>A piece for tomorrow's City Ledger: one locale key with no placeholders.</summary>
        public static Action<string> Story;

        /// <summary>A Flare draft about a striking interior: the business's place and name.</summary>
        public static Action<string, string> Striking;

        /// <summary>The district's income level, 60 to 140, by the game's name for it; 0 if it cannot be told (then the game's class shares).</summary>
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

    /// <summary>Today's customers of a business as the game counts them (InteriorGame.Count), for InteriorRules.Split.</summary>
    public struct CustomerCount
    {
        public int Drawn, Booked, OtherMod, Served;          // the game's draw, the feature's bookings, Meaningful Ambience's, served so far
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

    /// <summary>
    /// Everything interiors and decor reads from the game or does to it: the game's interior score, the placed items
    /// sorted once by their own tags and types, the floor area, the district's minimum (read live), the basket's products
    /// added as the game adds them, an office's clients booked by its own calculator, and a refresh's charge. No timer.
    /// </summary>
    internal static partial class InteriorGame
    {
        private const string Headquarters = "ba:businesstype_headquarters";
        private const string Empty = "ba:businesstype_empty";
        private const string Casino = "ba:businesstype_casino";
        private const string OfficeBuilding = "ba:buildingtype_office";

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

        /// <summary>What sort of place a building of the player's is to the feature.</summary>
        public static InteriorKind KindOf(BuildingRegistration reg)
        {
            try
            {
                if (reg == null || !GoingPublic.Mine(reg) || !reg.HasEstablishedBusiness || !reg.HasValidAddress || string.IsNullOrEmpty(reg.businessTypeName)
                    || reg.businessTypeName == Empty || !GameRegistrations.Found(reg))
                {
                    return InteriorKind.None;
                }
                if (reg.businessTypeName == Headquarters || GameRegistrations.BuildingType(reg) == OfficeBuilding)
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

        /// <summary>The game's own interior design score of a building, 0 to 100 (0 for one with no design saved).</summary>
        public static int InteriorScore(BuildingRegistration reg)
        {
            try
            {
                if (reg == null || reg.interiorDesigns == null || reg.interiorDesigns.Count == 0)
                {
                    return 0;
                }
                return InteriorRules.Clamp(InteriorScoreCalculator.GetInteriorScorePercentage(reg.interiorDesigns), 0, 100);   // no known material gives 0
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
            s.District = GameRegistrations.District(reg) ?? string.Empty;
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

        /// <summary>A district's income level for the report, as the interiors judge it.</summary>
        internal static int CompanionIncome(string district) => IncomeOf(district);

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
                    ? InteriorRules.Clamp((CityModel.StartIncome(data.workingClassPercentage, data.middleClassPercentage, data.upperClassClassPercentage) + 5) / 10,
                                          InteriorRules.IncomeLeast, InteriorRules.IncomeMost)
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

        /// <summary>Today's customers of a business for the tab, with the promotion figure's parts; false where the game keeps no list.</summary>
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
                        string district = GameRegistrations.District(reg);
                        strength = district != null ? NeighborhoodHelper.GetData(district).marketingStrength : strength;
                    }
                    catch (Exception)
                    {
                        // the game's own default strength
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

        /// <summary>Business pressure's own change of a business's customers now, per cent, or 0: what the shortfall gives way to.</summary>
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

        /// <summary>Why a business that takes money sells nothing now, from the game's own figures; None if it takes none or cannot tell.</summary>
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
