using System;
using System.Collections.Generic;
using AI.Customers.CustomerEntries;
using BAModAPI;
using BigAmbitions.DayNightCycle;
using BigAmbitions.Rivals;
using Blueprints;
using Buildings;
using Buildings.BuildingTypes.Shared.Dirtiness;
using BusinessLayoutSets;
using Entities;
using Helpers;
using Localizor;
using Streets;
using UI;
using UI.Smartphone.Apps.BizMan.Schedule;
using UnityEngine;

namespace RealLifeOverhaul.Economy.Brands
{
    /// <summary>
    /// What brands and franchises ask of the rest of the mod. Each is optional and has a default, so Economy/Brands
    /// works alone; Scripts/Wiring/BrandLinks.cs fills them in. It never calls another feature directly.
    /// </summary>
    public static class BrandsBridges
    {
        /// <summary>Has a business's promotion figure written again as it should stand now (business pressure's own writer, the mod's one place for it). Default: nothing is written, so footfall is not changed.</summary>
        public static CityBridges.FootfallWriter Footfall;

        /// <summary>True where the game keeps a promotion figure for a business, so that its footfall can be changed at all. Default: nowhere.</summary>
        public static Func<BuildingRegistration, bool> Promoted;

        /// <summary>The customers the city's economy takes from a business for its prices, per cent (0 or negative). Default: none.</summary>
        public static Func<BuildingRegistration, int> PriceLoss;

        /// <summary>True on the day the company's own strike starts at a business, by its place. Default: never.</summary>
        public static Func<string, int, bool> StrikeStarts;

        /// <summary>Files a franchisee's civil claim for a sum with the courts. Returns the court's number; 0 with no court to file with. Default: 0.</summary>
        public static Func<int, int> FileClaim;

        /// <summary>True while a claim with this number is before the court. Default: never.</summary>
        public static Func<int, bool> ClaimOpen;

        /// <summary>Withdraws a claim that has not been judged. Default: nothing happens.</summary>
        public static Func<int, bool> WithdrawClaim;

        /// <summary>How a scandal at a shop spreads, thousandths of its loss (Flare's account reputation). Default: 1000.</summary>
        public static Func<string, int> ScandalScale;

        /// <summary>A scandal has cost a brand: the shop's place, its name, the recognition lost (hundredths) and the scale it was lost at. Default: nobody is told.</summary>
        public static Action<string, string, int, int> ScandalHeard;

        public static void Reset()
        {
            Footfall = null;
            Promoted = null;
            PriceLoss = null;
            StrikeStarts = null;
            FileClaim = null;
            ClaimOpen = null;
            WithdrawClaim = null;
            ScandalScale = null;
            ScandalHeard = null;
        }
    }

    /// <summary>One business of the player's as brands see it today: what was read from the game, and its file if it trades under a brand or a city chain.</summary>
    public sealed class BrandShop
    {
        public BuildingRegistration Reg;
        public Address Address;
        public string Place = string.Empty;
        public string Name = string.Empty;
        public string Type = string.Empty;
        public string District;
        public Member File;             // under a brand of the player's
        public Taken Chain;             // under a city chain
        public int Customers, Takings;  // yesterday, by the game's order history
        public int Asked;               // per cent of customers asked for here now
        public int Last;                // what was asked for when the figure was last written
        public bool Applied;            // something of the brand's is in the game's figure
    }

    /// <summary>A building a franchised shop could open in.</summary>
    public sealed class BrandSite
    {
        public BuildingRegistration Reg;
        public Building Building;
        public string Place = string.Empty;
        public string Label = string.Empty;
        public int Traffic;
    }

    /// <summary>
    /// Everything brands and franchises read from or do to the game, in one place.
    ///
    /// READ: the player's businesses (the same test the Finance app uses); for a member shop the game's customer
    /// satisfaction (BuildingRegistration.satisfaction.overall), its cleanliness (GetCleanliness), which of its type's
    /// main goods are on the shelves (cachedAvailableProducts against BusinessTypeHelper.GetPrimaryProducts, the test
    /// the game's own pricing manager uses), its staff's main skill, yesterday's customers and takings (orderHistory)
    /// and its prices against the game's own market price; the buildings to let in a district; and the customers the
    /// game's own formula gives a building and a business type (CustomerEntriesCalculator: capacity x (base + 0.75 x
    /// traffic / 100) x the type's day and hour factors).
    ///
    /// DONE, each through what the game itself uses for the same thing:
    ///   money        dated transactions of the mod's own with their own keys and labels (GameManager.ChangeMoneySafe)
    ///   a shop       a franchised shop that can be one is a business of the game's own, started in a building that is
    ///                to let by the game's own call (CompetitionHelper.StartNewCompetitorBusiness, what its console's
    ///                "RerollAIBusiness" and its daily run use) and closed by the game's own closure
    ///                (CompetitionHelper.ShutdownBusinessesImmediate). ITS NAME IS THE GAME'S, NOT THE BRAND'S: the
    ///                game finds such a business's sign and where its goods come from by its name
    ///                (BuildingLogoSignController.UpdateSign, EstimatedWeeklyIncomeHelper line 167, which has no check
    ///                for a name it does not know and runs in the game's daily valuation), so a renamed one would lose
    ///                its sign and throw in the game's own day. The brand's name is on the mod's pages and on the map.
    ///   its keeping  while an agreement runs the game's own market does not close the shop (see <see cref="Keep"/>)
    ///   prices       the price of a product the shop already sells, in its price list and its stored one, as the
    ///                game's own price cell writes both (InventoryProductCellView), then the game's own market update
    ///   hours        a day's opening hours and whether it is open, as the game's own hour switches leave them
    ///                (ScheduleHourToggleGroup.OnHourToggleChange) and as the rota planner writes them
    ///                (RotaGame.Write.cs), with the same steps after: a fill of the game's own is stopped first,
    ///                today's customers are drawn again, the "no employee" alert is looked at and the game's events go
    ///                out. No shift is touched: the Rota tab fills a rota for the new hours.
    /// Footfall is asked for through <see cref="BrandsBridges"/> and never written here.
    ///
    /// COST: nothing per frame. The game is read when the feature starts, once a game day and when the app is opened
    /// (one pass over the save's buildings; four figures, the newest order history entry, the price list and the
    /// staff for each shop under a brand or a chain); everything else runs when the player presses a button. Once a
    /// second <see cref="Marks"/> compares one number made from the franchised shops (three multiplications each)
    /// with the last, and makes the map's markers again only when it differs.
    /// </summary>
    internal static class BrandsGame
    {
        public const string RoyaltyKey = "reallife_transaction_brand_royalty";
        public const string LevyKey = "reallife_transaction_brand_levy";
        public const string SupportKey = "reallife_transaction_brand_support";
        public const string EntryKey = "reallife_transaction_brand_entry";
        public const string RenewalKey = "reallife_transaction_brand_renewal";
        public const string InspectorKey = "reallife_transaction_brand_inspector";
        public const string RetrainKey = "reallife_transaction_brand_retrain";
        public const string GrantKey = "reallife_transaction_brand_grant";
        public const string CompensationKey = "reallife_transaction_brand_compensation";
        public const string CampaignKey = "reallife_transaction_brand_campaign";
        public const string SaleKey = "reallife_transaction_brand_sale";
        public const string ChainFeeKey = "reallife_transaction_brand_chain_fee";
        public const string ChainRoyaltyKey = "reallife_transaction_brand_chain_royalty";
        public const string ChainFineKey = "reallife_transaction_brand_chain_fine";
        public const string ChainExitKey = "reallife_transaction_brand_chain_exit";

        private const string DeductibleKey = "reallife_brand_deduct";
        private const string EmptyType = "ba:businesstype_empty";
        private const string GlobalDistrict = "ba:neighborhood_global";
        private const string MapId = "brand_shops";
        private const int SitesMost = 12;           // buildings offered for one applicant
        private const int OpenFrom = 9, OpenTo = 21;   // the hours a shop in the books keeps
        private const int MarksAtOnce = 6;          // markers made in one go, as the mod's other places are fed to the map

        public static IModLogger Logger;

        private static readonly List<BuildingRegistration> Regs = new List<BuildingRegistration>(16);
        private static readonly List<BrandShop> Before = new List<BrandShop>(16);
        private static readonly List<EmployeeInstance> Staff = new List<EmployeeInstance>(16);
        private static readonly List<BuildingRegistration> One = new List<BuildingRegistration>(1);
        private static readonly List<MapPlace> MarksWaiting = new List<MapPlace>(16);
        private static MapPlaces.Category _marks;
        private static long _marksStamp;

        private static GameInstance Save => SaveGameManager.Current;

        public static int Cash => Save != null ? Mathf.FloorToInt(Mathf.Max(0f, Save.Money)) : 0;

        public static int DaysPerYear
        {
            get
            {
                int days = GameCalendar.DaysPerYear;
                return days >= 8 ? days : 60;
            }
        }

        // ---------------------------------------------------------------- places

        /// <summary>A business by its address, written as business pressure and the company write a place.</summary>
        public static string Place(BuildingRegistration reg)
        {
            return Place(reg.StreetName, reg.StreetNumber);
        }

        public static string Place(string street, int number)
        {
            return Uri.EscapeDataString(street ?? string.Empty) + "~" + number.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        /// <summary>The address a place stands for; null for one that cannot be read.</summary>
        public static Address AddressOf(string place)
        {
            int at = place != null ? place.LastIndexOf('~') : -1;
            int number;
            if (at <= 0 || !int.TryParse(place.Substring(at + 1), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out number))
            {
                return null;
            }
            return new Address(Uri.UnescapeDataString(place.Substring(0, at)), number);
        }

        /// <summary>The game's registration of the building at a place, whoever has it; null if there is none.</summary>
        public static BuildingRegistration RegAt(string place)
        {
            try
            {
                Address address = AddressOf(place);
                return address != null ? BuildingHelper.GetBuildingRegistration(address) : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>A building's address as the game writes one.</summary>
        public static string AddressWords(string place)
        {
            try
            {
                Address address = AddressOf(place);
                return address != null ? address.ToFormattedString() : string.Empty;
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }

        // A building's door in whole metres. False while the city's buildings cannot be asked.
        private static bool Spot(Address address, out Vector3 spot)
        {
            spot = Vector3.zero;
            try
            {
                Transform door = address != null && InstanceBehavior<CityManager>.Instance != null ? BuildingHelper.GetAddressEntranceTransform(address) : null;
                if (door == null)
                {
                    return false;
                }
                spot = door.position;
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        // ---------------------------------------------------------------- the player's businesses

        /// <summary>
        /// The player's businesses as they stand today, each with its file if it trades under a brand or a chain, and
        /// for those the game's figures. A member or a chain shop that is no longer the player's is dropped from the
        /// state (<paramref name="left"/> counts them). One pass over the save's buildings; for a shop with a file,
        /// its order history from the newest entry back, its price list and its staff.
        /// </summary>
        public static void ReadShops(BrandsState state, List<BrandShop> into, List<BrandShop> pool, int day, out int left)
        {
            left = 0;
            Before.Clear();
            Before.AddRange(into);   // (a shop that is still the player's keeps its object, and with it what was last asked for and written there)
            into.Clear();
            for (int i = 0; i < state.Members.Count; i++)
            {
                state.Members[i].Seen = false;
            }
            for (int i = 0; i < state.Taken.Count; i++)
            {
                state.Taken[i].Seen = false;
            }
            Books.Businesses(Regs);
            for (int i = 0; i < Regs.Count; i++)
            {
                BuildingRegistration reg = Regs[i];
                BrandShop shop = null;
                for (int k = 0; k < Before.Count; k++)
                {
                    if (ReferenceEquals(Before[k].Reg, reg))
                    {
                        shop = Before[k];
                        Before.RemoveAt(k);
                        break;
                    }
                }
                if (shop == null)
                {
                    if (pool.Count > 0)
                    {
                        shop = pool[pool.Count - 1];
                        pool.RemoveAt(pool.Count - 1);
                    }
                    else
                    {
                        shop = new BrandShop();
                    }
                    shop.Asked = shop.Last = 0;
                    shop.Applied = false;
                }
                shop.Reg = reg;
                shop.Place = Place(reg);
                shop.Name = string.IsNullOrEmpty(reg.BusinessName) ? "?" : reg.BusinessName;
                shop.Type = reg.businessTypeName ?? string.Empty;
                try
                {
                    shop.Address = reg.Address;
                    shop.District = reg.Neighborhood;
                }
                catch (Exception)
                {
                    shop.Address = null;   // a business whose building cannot be read is in no district
                    shop.District = null;
                }
                shop.File = state.Member(shop.Place);
                shop.Chain = state.TakenAt(shop.Place);
                shop.Customers = shop.Takings = 0;
                if (shop.File != null || shop.Chain != null)
                {
                    Figures(shop, day);
                }
                into.Add(shop);
            }
            Regs.Clear();
            for (int i = 0; i < Before.Count; i++)
            {
                Before[i].Reg = null;
                Before[i].File = null;
                Before[i].Chain = null;
                pool.Add(Before[i]);
            }
            Before.Clear();
            for (int i = state.Members.Count - 1; i >= 0; i--)
            {
                if (!state.Members[i].Seen)
                {
                    BrandRules.Leave(state, state.Members[i].Place);
                    left++;
                }
            }
            for (int i = state.Taken.Count - 1; i >= 0; i--)
            {
                if (!state.Taken[i].Seen)
                {
                    state.Taken.RemoveAt(i);
                    left++;
                }
            }
        }

        // The game's own figures for one shop with a file.
        private static void Figures(BrandShop shop, int day)
        {
            BuildingRegistration reg = shop.Reg;
            int satisfaction = reg.satisfaction != null ? reg.satisfaction.overall : 50;
            int clean = Cleanliness(reg), stock = Stock(reg), skill = Skill(reg);
            int standards = BrandRules.ShopStandards(satisfaction, clean, stock, skill);
            Yesterday(reg, day, out shop.Customers, out shop.Takings);
            if (shop.File != null)
            {
                Member m = shop.File;
                m.Seen = true;
                m.District = shop.District;
                m.Satisfaction = satisfaction;
                m.Clean = clean;
                m.Stock = stock;
                m.Skill = skill;
                m.Standards = standards;
                m.Customers = shop.Customers;
                m.Takings = shop.Takings;
                Vector3 spot;
                m.HasSpot = Spot(shop.Address, out spot);
                if (m.HasSpot)
                {
                    m.X = Mathf.RoundToInt(spot.x);
                    m.Z = Mathf.RoundToInt(spot.z);
                }
                int loss = 0;
                try
                {
                    loss = BrandsBridges.PriceLoss != null ? BrandsBridges.PriceLoss(reg) : 0;
                }
                catch (Exception e)
                {
                    Logger?.Error(e);
                }
                m.PriceLoss = Math.Min(0, loss);
            }
            if (shop.Chain != null)
            {
                shop.Chain.Seen = true;
                shop.Chain.Standards = standards;
                shop.Chain.Prices = PriceRatio(reg);
            }
        }

        private static int Cleanliness(BuildingRegistration reg)
        {
            try
            {
                return Mathf.Clamp(Mathf.RoundToInt(reg.GetCleanliness()), 0, 100);
            }
            catch (Exception)
            {
                return 100;   // a business with no floor to dirty is clean
            }
        }

        // The share of the type's main goods the shop has to sell, per cent; 100 for a type with none (a service).
        private static int Stock(BuildingRegistration reg)
        {
            try
            {
                HashSet<string> main = BusinessTypeHelper.GetPrimaryProducts(reg.businessTypeName);
                List<string> shelves = reg.cachedAvailableProducts;
                if (main == null || main.Count == 0 || shelves == null)
                {
                    return 100;
                }
                int have = 0;
                foreach (string item in main)
                {
                    have += shelves.Contains(item) ? 1 : 0;
                }
                return have * 100 / main.Count;
            }
            catch (Exception)
            {
                return 100;
            }
        }

        // The mean of the staff's main skill, as the game counts skill (0 to 100); 50 for a shop the player runs alone.
        private static int Skill(BuildingRegistration reg)
        {
            Staff.Clear();
            try
            {
                EmployeeHelper.GetEmployeeInstances(new EmployeeInstancesQueryInfo { withAssignedAddress = reg.Address, excludeBeingReplaced = true }, Staff);
                float sum = 0f;
                int count = 0;
                for (int i = 0; i < Staff.Count; i++)
                {
                    EmployeeInstance e = Staff[i];
                    if (e != null && !e.IsCandidate && e.characterData != null && e.characterData.skills != null && e.characterData.skills.Count > 0 && e.characterData.skills[0] != null)
                    {
                        sum += e.characterData.skills[0].value;
                        count++;
                    }
                }
                return count > 0 ? Mathf.Clamp(Mathf.RoundToInt(sum / count), 0, 100) : 50;
            }
            catch (Exception)
            {
                return 50;
            }
            finally
            {
                Staff.Clear();
            }
        }

        // Yesterday's customers and takings by the game's own order history, from the newest entry back.
        private static void Yesterday(BuildingRegistration reg, int day, out int customers, out int takings)
        {
            customers = takings = 0;
            List<OrderHistoryEntry> history = reg.orderHistory;
            for (int i = history != null ? history.Count - 1 : -1; i >= 0; i--)
            {
                OrderHistoryEntry entry = history[i];
                if (entry == null || entry.dayNumber >= day)
                {
                    continue;
                }
                if (entry.dayNumber == day - 1)
                {
                    customers = Mathf.Max(0, entry.totalCustomers);
                    takings = Mathf.RoundToInt(Mathf.Max(0f, entry.totalRevenue));
                }
                break;
            }
        }

        /// <summary>A shop's prices against the game's own market price for the same goods, thousandths (1000: the same); 0 with no price to judge by.</summary>
        public static int PriceRatio(BuildingRegistration reg)
        {
            try
            {
                List<RetailPrice> prices = reg.retailPrices;
                double sum = 0d;
                int count = 0;
                for (int i = 0; prices != null && i < prices.Count; i++)
                {
                    RetailPrice p = prices[i];
                    float market = p != null && p.price > 0f && !string.IsNullOrEmpty(p.itemName) ? ItemHelper.GetDefaultMarketPrice(p.itemName) : 0f;
                    if (market > 0f)
                    {
                        sum += p.price / market;
                        count++;
                    }
                }
                return count > 0 ? Mathf.RoundToInt((float)(sum / count * 1000d)) : 0;
            }
            catch (Exception)
            {
                return 0;
            }
        }

        public static BrandShop ShopAt(List<BrandShop> shops, string place)
        {
            for (int i = 0; i < shops.Count; i++)
            {
                if (string.Equals(shops[i].Place, place, StringComparison.Ordinal))
                {
                    return shops[i];
                }
            }
            return null;
        }

        // ---------------------------------------------------------------- the city: districts, buildings to let, the game's customer formula

        /// <summary>The city's districts by the game's own names, those with a building in them.</summary>
        public static void Districts(List<string> into)
        {
            into.Clear();
            try
            {
                foreach (KeyValuePair<string, List<Building>> pair in BuildingHelper.AllNeighbourhoodBuildings)
                {
                    if (!string.IsNullOrEmpty(pair.Key) && pair.Key != GlobalDistrict && pair.Value != null && pair.Value.Count > 0)
                    {
                        into.Add(pair.Key);
                    }
                }
                into.Sort(string.CompareOrdinal);   // the same order on every machine: an applicant's district is picked by number
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                into.Clear();
            }
        }

        /// <summary>
        /// The buildings in a district that are to let by the game's own list (BuildingRegistration.AvailableForRent),
        /// stand empty and are of the kind the business type goes in, busiest street first, twelve at most. One pass
        /// over the district's buildings; asked when an applicant's page is opened.
        /// </summary>
        public static void Sites(string district, string type, List<BrandSite> into)
        {
            into.Clear();
            try
            {
                List<Building> all;
                if (string.IsNullOrEmpty(district) || !BuildingHelper.AllNeighbourhoodBuildings.TryGetValue(district, out all) || all == null)
                {
                    return;
                }
                string kind = BusinessTypeHelper.GetSuitableBuildingType(type);
                for (int i = 0; i < all.Count; i++)
                {
                    Building building = all[i];
                    if (building == null || building.BuildingType != kind || building.SpecialService != null || RivalsHelper.IgnoredAddresses.Contains(building.Address))
                    {
                        continue;
                    }
                    BuildingRegistration reg = BuildingHelper.GetBuildingRegistration(building.Address);
                    if (!Free(reg) || !reg.HasValidAddress)
                    {
                        continue;
                    }
                    into.Add(new BrandSite { Reg = reg, Building = building, Place = Place(reg), Label = building.Address.ToFormattedString(), Traffic = building.trafficIndex });
                }
                into.Sort((a, b) => b.Traffic != a.Traffic ? b.Traffic.CompareTo(a.Traffic) : string.CompareOrdinal(a.Place, b.Place));
                if (into.Count > SitesMost)
                {
                    into.RemoveRange(SitesMost, into.Count - SitesMost);
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                into.Clear();
            }
        }

        /// <summary>
        /// The customers an ordinary day brings a business of a type in a building, by the game's own formula for a
        /// business that is not the player's (CustomerEntriesCalculator.GetCustomersByHour and GetPromotionMultiplier):
        /// for each hour it is open, the building's capacity for its kind and size x (the save's base multiplier +
        /// 0.75 x the street's traffic index / 100) x the type's factor for the day and the hour, rounded up; a week
        /// of that, divided by seven. With a registration that has opening hours (a business of the game's own) those
        /// are the hours, and its own customer limit holds each hour; otherwise 9:00 to 21:00 every day.
        /// </summary>
        public static int Customers(Building building, string type, BuildingRegistration reg)
        {
            try
            {
                BusinessType data = BusinessTypeHelper.GetData(type);
                int capacity = building != null ? BuildingSizeHelper.GetData(building).GetCustomerCapacity(building.BuildingType, building.BuildingVersion) : 0;
                if (data == null || capacity <= 0 || Save == null || Save.gameVariables == null)
                {
                    return 0;
                }
                float promotion = Save.gameVariables.baseCustomerPromotionMultiplier + 0.75f * (building.trafficIndex / 100f);
                int limit = reg != null && reg.customerCapacity > 0 ? reg.customerCapacity : capacity;
                List<ScheduleDay> week = reg != null && reg.scheduleDays != null && reg.scheduleDays.Count > 0 ? reg.scheduleDays : null;
                float total = 0f;
                for (int d = 0; data.dayFactorMultipliers != null && d < data.dayFactorMultipliers.Count; d++)
                {
                    DayFactorMultiplier factor = data.dayFactorMultipliers[d];
                    if (factor == null)
                    {
                        continue;
                    }
                    ScheduleDay hours = null;
                    for (int k = 0; week != null && k < week.Count; k++)
                    {
                        hours = week[k] != null && week[k].day == factor.dayOfWeekOrdered ? week[k] : hours;
                    }
                    if (week != null && (hours == null || !hours.isOpen))
                    {
                        continue;
                    }
                    for (int hour = 0; hour < 24; hour++)
                    {
                        if (!(week == null ? hour >= OpenFrom && hour < OpenTo : IsOpen(hours, hour)))
                        {
                            continue;
                        }
                        float perHour = capacity * promotion * factor.multiplier * HourFactor(data, hour);
                        total += Mathf.Min(Mathf.CeilToInt(perHour), limit);
                    }
                }
                return Mathf.Max(0, Mathf.RoundToInt(total / 7f));
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                return 0;
            }
        }

        private static bool IsOpen(ScheduleDay day, int hour)
        {
            for (int i = 0; day.openingHourSlots != null && i < day.openingHourSlots.Count; i++)
            {
                OpeningHourSlot slot = day.openingHourSlots[i];
                if (slot != null && hour >= slot.startingHour && hour < slot.endingHour)
                {
                    return true;
                }
            }
            return false;
        }

        private static float HourFactor(BusinessType data, int hour)
        {
            for (int i = 0; data.hourlyFactorMultipliers != null && i < data.hourlyFactorMultipliers.Count; i++)
            {
                HourlyFactorMultiplier factor = data.hourlyFactorMultipliers[i];
                if (factor != null && hour >= factor.startingHour && hour <= factor.endingHour - 1)
                {
                    return factor.multiplier;
                }
            }
            return 0f;
        }

        // ---------------------------------------------------------------- a franchised shop in the city

        /// <summary>
        /// Opens a franchised shop at a building that is to let, as a business of the game's own where the game has
        /// one of the type that fits the building (see the class summary), in the books only where it has none. The
        /// file gets its place, its hold, the game's name for the business, its door and its customers.
        /// </summary>
        public static void Open(Franchise f, BrandSite site, string type)
        {
            f.Place = site.Place;
            f.Hold = Hold.Books;
            f.GameName = string.Empty;
            BuildingRegistration reg = site.Reg;
            try
            {
                AiBusinessDefault chosen = Default(type, reg);
                // A guest of a Going Public session opens it in the books only: the city's own businesses are the host's there, and
                // Going Public does not let a guest start one (CompetitionHelper.StartNewCompetitorBusiness; Core/GoingPublic).
                if (chosen != null && Free(reg) && !GoingPublic.IsGuest)
                {
                    CompetitionHelper.StartNewCompetitorBusiness(type, reg, true, chosen, CompetitionHelper.GetRivalIdForBusinessDefault(chosen));
                    f.Hold = Hold.Opened;
                    f.GameName = reg.BusinessName ?? string.Empty;
                    SaveGameManager.MarkChange();
                }
            }
            catch (Exception e)
            {
                // Nothing half-opened is left standing: the game's own closure puts the building back to let.
                Logger?.Error(e);
                try
                {
                    if (reg != null && !reg.RentedByPlayer && reg.businessTypeName == type)
                    {
                        reg.ShutDownAIBusiness();
                    }
                }
                catch (Exception second)
                {
                    Logger?.Error(second);
                }
                f.Hold = Hold.Books;
                f.GameName = string.Empty;
            }
            Settle(f, site.Building, type, f.Hold == Hold.Opened ? reg : null);
            Logger?.Info("Brands: " + f.Name + " opens at " + site.Label + (f.Hold == Hold.Opened
                ? " as a business of the game's own, '" + f.GameName + "' (the game's name and sign for it are kept: it finds such a business by its name)"
                : " in the mod's books only (the game has no business of this type that fits the building)") + "; " + f.Base + " customers on an ordinary day by the game's formula.");
        }

        /// <summary>True while a building is still to let by the game's own list, empty and not the player's: what a franchised shop may open in.</summary>
        public static bool Free(BuildingRegistration reg)
        {
            return reg != null && !reg.RentedByPlayer && !reg.BuildingOwnedByPlayer && reg.AvailableForRent && (string.IsNullOrEmpty(reg.businessTypeName) || reg.businessTypeName == EmptyType);
        }

        /// <summary>
        /// A business of the game's own of the brand's type that already trades in the district and is no franchised
        /// shop yet joins the brand as it stands: the file given is filled in and nothing of the game's is changed, so
        /// this also serves to ask whether there is one. False if there is none.
        /// </summary>
        public static bool Convert(Franchise f, BrandsState state, string district, string type)
        {
            try
            {
                List<Building> all;
                if (string.IsNullOrEmpty(district) || !BuildingHelper.AllNeighbourhoodBuildings.TryGetValue(district, out all) || all == null)
                {
                    return false;
                }
                for (int i = 0; i < all.Count; i++)
                {
                    Building building = all[i];
                    BuildingRegistration reg = building != null && building.SpecialService == null ? BuildingHelper.GetBuildingRegistration(building.Address) : null;
                    if (reg == null || reg.RentedByPlayer || reg.businessTypeName != type || !reg.HasEstablishedBusiness || Taken(state, Place(reg)))
                    {
                        continue;
                    }
                    f.Place = Place(reg);
                    f.Hold = Hold.Converted;
                    f.GameName = reg.BusinessName ?? string.Empty;
                    f.Born = reg.creationDay;
                    Settle(f, building, type, reg);
                    return true;
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
            return false;
        }

        private static bool Taken(BrandsState state, string place)
        {
            for (int i = 0; i < state.Franchises.Count; i++)
            {
                if (string.Equals(state.Franchises[i].Place, place, StringComparison.Ordinal))
                {
                    return true;
                }
            }
            return false;
        }

        // The door and the customers of a shop that has just been placed.
        private static void Settle(Franchise f, Building building, string type, BuildingRegistration reg)
        {
            Vector3 spot = Vector3.zero;
            f.HasSpot = building != null && Spot(building.Address, out spot);
            if (f.HasSpot)
            {
                f.X = Mathf.RoundToInt(spot.x);
                f.Z = Mathf.RoundToInt(spot.z);
            }
            f.Base = Customers(building, type, reg);
        }

        // One of the game's ready-made businesses of the type whose layout exists for this building's size, an
        // ordinary trader before one of a rival's chain (a rival that is beaten takes its chain's shops with it).
        private static AiBusinessDefault Default(string type, BuildingRegistration reg)
        {
            if (reg == null || reg.BuildingCached == null)
            {
                return null;
            }
            AiBusinessDefault[] all = CompetitionHelper.GetBusinessDefaultsByType(type);
            var size = new BuildingSizeInfo(reg);
            AiBusinessDefault chain = null;
            for (int i = 0; all != null && i < all.Length; i++)
            {
                AiBusinessDefault d = all[i];
                if (d == null || string.IsNullOrEmpty(d.businessName) || d.schedule == null || BusinessLayoutSetHelper.GetOrLoadBusinessLayoutSet(type, size, d.buildingLayout, false) == null)
                {
                    continue;
                }
                if (string.IsNullOrEmpty(d.corporationRivalId))
                {
                    return d;
                }
                chain = chain ?? d;
            }
            return chain;
        }

        /// <summary>True while a franchised shop that is a business of the game's is still the business it was: the same type under the same name, and not the player's.</summary>
        public static bool Stands(Franchise f, string type)
        {
            if (f.Hold == Hold.Books)
            {
                return true;
            }
            BuildingRegistration reg = RegAt(f.Place);
            return reg != null && !reg.RentedByPlayer && reg.businessTypeName == type && string.Equals(reg.BusinessName ?? string.Empty, f.GameName, StringComparison.Ordinal);
        }

        /// <summary>
        /// The city's own market leaves a franchised shop alone while its agreement runs. The game's daily run closes
        /// a business of the city's that earns little, one or two a district every few days, but never one that
        /// started within the last forty days (CompetitionHelper.TryShutdownBusinesses, line 685: its creationDay is
        /// later than today - 40). So the day the game has for the shop's start is kept at today: one number, written
        /// once a day, read by nothing else for a business that is not the player's. For a shop that traded before the
        /// agreement the day it had is put back when the agreement ends (<see cref="Close"/>).
        /// </summary>
        public static void Keep(Franchise f, string type, int day)
        {
            if (f.Hold == Hold.Books)
            {
                return;
            }
            BuildingRegistration reg = RegAt(f.Place);
            if (reg != null && !reg.RentedByPlayer && reg.businessTypeName == type)
            {
                reg.creationDay = day;
            }
        }

        /// <summary>
        /// A franchised shop closes. One the game started for the brand is closed by the game's own closure, which
        /// puts the building back to let; one that was trading before goes on trading as the business it is, with the
        /// start day it had; one in the books was never in the city.
        /// </summary>
        public static void Close(Franchise f, string type)
        {
            if (f.Hold == Hold.Converted && Stands(f, type))
            {
                BuildingRegistration was = RegAt(f.Place);
                if (was != null)
                {
                    was.creationDay = f.Born;
                }
                return;
            }
            if (f.Hold != Hold.Opened || !Stands(f, type))
            {
                return;
            }
            if (GoingPublic.IsGuest)
            {
                return;   // the business stands in the host's city, which a guest of a Going Public session does not close (Core/GoingPublic)
            }
            try
            {
                One.Clear();
                One.Add(RegAt(f.Place));
                CompetitionHelper.ShutdownBusinessesImmediate(One);
                SaveGameManager.MarkChange();
                Logger?.Info("Brands: '" + f.GameName + "' at " + AddressWords(f.Place) + " closed by the game's own closure; the building is to let again.");
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
            finally
            {
                One.Clear();
            }
        }

        /// <summary>True while the player stands inside the building a franchised shop is in.</summary>
        public static bool PlayerInside(Franchise f)
        {
            try
            {
                BuildingRegistration reg = f.Hold != Hold.Books ? RegAt(f.Place) : null;
                return reg != null && BuildingManager.IsInsideBuilding && InstanceBehavior<BuildingManager>.Instance != null && InstanceBehavior<BuildingManager>.Instance.buildingRegistration == reg;
            }
            catch (Exception)
            {
                return false;
            }
        }

        // ---------------------------------------------------------------- shared settings: prices and hours

        /// <summary>
        /// The flagship's prices at another shop of the brand: for each product both sell, the price in the shop's
        /// price list and in its stored one, as the game's own price cell writes both, and the game's own market
        /// update for the product. A product the shop does not sell is not added. Returns how many prices changed.
        /// </summary>
        public static int SharePrices(BuildingRegistration from, BuildingRegistration to)
        {
            int changed = 0;
            List<RetailPrice> source = from != null ? from.retailPrices : null;
            for (int i = 0; to != null && to.retailPrices != null && source != null && i < source.Count; i++)
            {
                RetailPrice p = source[i];
                if (p != null && !string.IsNullOrEmpty(p.itemName) && p.price > 0f && SetPrice(to, p.itemName, p.price))
                {
                    changed++;
                }
            }
            return changed;
        }

        /// <summary>A shop's prices put at the middle of what a city chain asks, against the game's own market price for each product. Returns how many changed.</summary>
        public static int ChainPrices(BuildingRegistration reg, int chain)
        {
            int changed = 0;
            float factor = (BrandCatalogue.ChainPriceLeast[chain] + BrandCatalogue.ChainPriceMost[chain]) / 2000f;
            List<RetailPrice> prices = reg != null ? reg.retailPrices : null;
            for (int i = 0; prices != null && i < prices.Count; i++)
            {
                RetailPrice p = prices[i];
                float market = p != null && !string.IsNullOrEmpty(p.itemName) ? ItemHelper.GetDefaultMarketPrice(p.itemName) : 0f;
                if (market > 0f && SetPrice(reg, p.itemName, Mathf.Round(market * factor * 100f) / 100f))
                {
                    changed++;
                }
            }
            return changed;
        }

        // One price, where the shop already has one for the product. True if it changed.
        private static bool SetPrice(BuildingRegistration reg, string item, float price)
        {
            RetailPrice mine = null;
            for (int i = 0; i < reg.retailPrices.Count; i++)
            {
                if (reg.retailPrices[i] != null && reg.retailPrices[i].itemName == item)
                {
                    mine = reg.retailPrices[i];
                    break;
                }
            }
            if (mine == null || Mathf.Abs(mine.price - price) < 0.005f)
            {
                return false;
            }
            mine.price = price;
            bool stored = false;
            for (int i = 0; reg.storedRetailPrices != null && i < reg.storedRetailPrices.Count; i++)
            {
                if (reg.storedRetailPrices[i] != null && reg.storedRetailPrices[i].itemName == item)
                {
                    reg.storedRetailPrices[i].price = price;
                    stored = true;
                }
            }
            if (!stored && reg.storedRetailPrices != null)
            {
                reg.storedRetailPrices.Add(new RetailPrice { itemName = item, price = price });
            }
            try
            {
                ProductMarketHelper.UpdateMarketDemand(item);
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
            SaveGameManager.MarkChange();
            return true;
        }

        /// <summary>
        /// The flagship's opening hours at another shop of the brand: each day's hours and whether it is open, with
        /// the steps the game takes after its own schedule screen (see the class summary). No shift is changed.
        /// Returns how many days changed.
        /// </summary>
        public static int ShareHours(BuildingRegistration from, BuildingRegistration to)
        {
            if (from == null || to == null || from.scheduleDays == null || to.scheduleDays == null || ReferenceEquals(from, to))
            {
                return 0;
            }
            BizManSchedule.AbortAutoFillForBusiness(to);
            int changed = 0;
            for (int i = 0; i < to.scheduleDays.Count; i++)
            {
                ScheduleDay day = to.scheduleDays[i], source = null;
                for (int k = 0; day != null && k < from.scheduleDays.Count; k++)
                {
                    source = from.scheduleDays[k] != null && from.scheduleDays[k].day == day.day ? from.scheduleDays[k] : source;
                }
                if (day == null || source == null || SameHours(day, source))
                {
                    continue;
                }
                day.isOpen = source.isOpen;
                var slots = new List<OpeningHourSlot>(source.openingHourSlots != null ? source.openingHourSlots.Count : 0);
                for (int k = 0; source.openingHourSlots != null && k < source.openingHourSlots.Count; k++)
                {
                    if (source.openingHourSlots[k] != null)
                    {
                        slots.Add(source.openingHourSlots[k].Clone());
                    }
                }
                day.openingHourSlots = slots;
                changed++;
            }
            if (changed > 0)
            {
                After(to);
            }
            return changed;
        }

        private static bool SameHours(ScheduleDay a, ScheduleDay b)
        {
            int count = a.openingHourSlots != null ? a.openingHourSlots.Count : 0;
            if (a.isOpen != b.isOpen || count != (b.openingHourSlots != null ? b.openingHourSlots.Count : 0))
            {
                return false;
            }
            for (int i = 0; i < count; i++)
            {
                OpeningHourSlot x = a.openingHourSlots[i], y = b.openingHourSlots[i];
                if (x == null || y == null || x.startingHour != y.startingHour || x.endingHour != y.endingHour)
                {
                    return false;
                }
            }
            return true;
        }

        // What the game does when its own schedule screen closes after the hours were changed (BizManSchedule.OnDisable), as the rota planner does it.
        private static void After(BuildingRegistration reg)
        {
            UIs uis = InstanceBehavior<UIs>.Instance;
            Guarded("the security level", () => reg.UpdateSecurityLevel());
            Guarded("the game's schedule screen", () =>
            {
                BizManBusiness shown = uis != null && uis.fullMenu != null && uis.fullMenu.bizMan != null ? uis.fullMenu.bizMan.business : null;
                BizManSchedule schedule = uis != null && uis.fullMenu != null ? uis.fullMenu.schedule : null;
                if (shown != null && shown.isActiveAndEnabled && shown.buildingRegistration == reg && schedule != null && schedule.isActiveAndEnabled)
                {
                    schedule.LoadScheduler();
                }
            });
            Guarded("the game's schedule events", () => GameEvent.Invoke("ba:gameevent_changedbuildingregistration"));
            Guarded("today's customers and alerts", () =>
            {
                CustomerEntriesHelper.UpdateCustomerEntriesForPlayerBusiness(reg, TimeHelper.GetDayOfWeek());
                BusinessHelper.CheckIfTheaterHasNoActors(reg);
                if (!BusinessHelper.IsMissingEmployeeTaskActive(reg))
                {
                    BusinessHelper.ForceRecheckMissingEmployeeAlert(reg);
                }
                GlobalEvents.onBuildingRegistrationChange?.Invoke(reg.Address);
                var work = uis != null && uis.playerActivityUI != null ? uis.playerActivityUI.GetCurrentActivity as PlayerActivity.WorkActivity : null;
                if (work != null)
                {
                    work.SetTimeToWork();
                    uis.playerActivityUI.UpdateActivityDisplay();
                }
            });
            SaveGameManager.MarkChange();
        }

        // One of the game's own follow-ups: a fault in it is logged and the rest still run.
        private static void Guarded(string what, Action work)
        {
            try
            {
                work();
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                Logger?.Warn("Brands: " + what + " could not be brought up to date after the opening hours were changed; the hours themselves are in place.");
            }
        }

        // ---------------------------------------------------------------- the game's own text input

        /// <summary>
        /// Asks for a name with the game's own text input (UIs.textInputUi, TextInputUI.Show). False if the game has
        /// none to show: the caller then keeps the name there is.
        /// </summary>
        public static bool AskName(string initial, Action<string> done)
        {
            try
            {
                UIs uis = InstanceBehavior<UIs>.Instance;
                if (uis == null || uis.textInputUi == null)
                {
                    return false;
                }
                uis.textInputUi.Show("reallife_brand_name_placeholder", "reallife_brand_name_confirm", done, null, initial ?? string.Empty, false, BrandCatalogue.NameMost);
                return true;
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                return false;
            }
        }

        // ---------------------------------------------------------------- money

        /// <summary>
        /// Takes an amount as a transaction of the mod's own. A cost of running the brand is marked deductible as the
        /// game marks its own. Unless <paramref name="force"/>, the game's call refuses a charge the cash does not
        /// cover: false, and nothing is taken.
        /// </summary>
        public static bool Take(int amount, string key, string name, bool deductible, bool force)
        {
            if (amount <= 0)
            {
                return true;
            }
            if (Save == null)
            {
                return false;
            }
            TransactionInfo info = Info(key, name);
            if (deductible)
            {
                info.SetTaxDeductibleName(DeductibleKey.GetLocalization());
            }
            return GameManager.ChangeMoneySafe(-amount, info, null, null, force);
        }

        public static void Give(int amount, string key, string name)
        {
            if (amount > 0 && Save != null)
            {
                GameManager.ChangeMoneySafe(amount, Info(key, name), null, null, true);
            }
        }

        // (a dictionary each: the game keeps hold of a transaction's data for as long as it lists the transaction)
        private static TransactionInfo Info(string key, string name)
        {
            return new TransactionInfo(key, new Dictionary<string, string>(2) { { "name", name ?? string.Empty } });
        }

        // ---------------------------------------------------------------- the city map

        /// <summary>
        /// The franchised shops on the city map, as the mod shows its other places (Core/MapPlaces): a row "Brand
        /// shops" in the map's filter list and a marker with the brand's and the franchisee's names on each. Called
        /// once a second; while nothing has changed it is one comparison. When the set of shops has changed the
        /// markers are made again, six in a call.
        /// </summary>
        public static void Marks(BrandsState state, bool on, Func<Franchise, string> name)
        {
            try
            {
                long stamp = on && state != null && Settings.MapPlacesShown ? Stamp(state) : 0L;
                if (stamp != _marksStamp)
                {
                    _marksStamp = stamp;
                    MarksWaiting.Clear();
                    if (_marks != null)
                    {
                        _marks.Clear();
                    }
                    for (int i = 0; stamp != 0L && i < state.Franchises.Count; i++)
                    {
                        Franchise f = state.Franchises[i];
                        Address address = AddressOf(f.Place);
                        Vector3 spot;
                        if (address != null && Spot(address, out spot))
                        {
                            CityBuildingController building = InstanceBehavior<CityManager>.Instance != null ? InstanceBehavior<CityManager>.Instance.FindCityBuildingController(address) : null;
                            MarksWaiting.Add(new MapPlace(spot, name(f), building) { Address = address });
                        }
                    }
                    if (MarksWaiting.Count > 0 && _marks == null)
                    {
                        _marks = MapPlaces.Register(MapId, "reallife_brand_map_row", new Color(0.78f, 0.42f, 0.16f), MapIcon.Flag, true);
                        _marks.Available = () => _marksStamp != 0L;
                    }
                }
                for (int made = 0; _marks != null && made < MarksAtOnce && MarksWaiting.Count > 0; made++)
                {
                    _marks.Add(MarksWaiting[MarksWaiting.Count - 1]);
                    MarksWaiting.RemoveAt(MarksWaiting.Count - 1);
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                MarksWaiting.Clear();
            }
        }

        // Changes when a franchised shop comes, goes or moves; never 0 while there is one.
        private static long Stamp(BrandsState state)
        {
            long stamp = 0L;
            for (int i = 0; i < state.Franchises.Count; i++)
            {
                Franchise f = state.Franchises[i];
                stamp = unchecked(stamp * 31L + f.Id * 7L + f.X * 3L + f.Z + (f.HasSpot ? 1 : 0));
            }
            return state.Franchises.Count == 0 ? 0L : stamp == 0L ? 1L : stamp;
        }

        /// <summary>A name on the markers has changed: they are made again at the next look.</summary>
        public static void Remark()
        {
            _marksStamp = -1L;
        }

        /// <summary>The city has gone: the map's row went with it (MapPlaces.Shutdown).</summary>
        public static void Forget()
        {
            _marks = null;
            _marksStamp = 0L;
            MarksWaiting.Clear();
        }
    }
}
