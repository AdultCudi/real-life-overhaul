using System;
using System.Collections.Generic;
using BAModAPI;
using BigAmbitions.Rivals;
using Buildings;
using Helpers;
using Localizor;
using Streets;
using UI;
using UnityEngine;

namespace RealLifeOverhaul.Economy.Brands
{
    /// <summary>One business of the player's as brands see it today, with its file if it trades under a brand or a city chain.</summary>
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
    /// Everything brands and franchises read from or do to the game, each done as the game itself does it. Footfall goes
    /// through <see cref="BrandsBridges"/>. Nothing per frame: the game is read at the start, each game day and when the
    /// app opens, and once a second <see cref="Marks"/> compares one number with the last.
    /// </summary>
    internal static partial class BrandsGame
    {
        /// <summary>Owner deduction labels whose mixed costs have no single portable recurrence rule.</summary>
        internal static string[] CompanionUnclassifiedTaxLabels() => new[] { DeductibleKey.GetLocalization() };

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
        private const string GlobalDistrict = "ba:neighborhood_global";
        private const string MapId = "brand_shops";
        private const int SitesMost = 12;           // buildings offered for one applicant
        private const int OpenFrom = 9, OpenTo = 21;   // hours of the day a shop in the books keeps
        private const int MarksAtOnce = 6;          // markers made in one go, as the mod's other places are fed to the map

        public static IModLogger Logger;

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

        /// <summary>A business by its address, as business pressure and the company write a place.</summary>
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

        /// <summary>The city's districts with a building in them, by the game's own names.</summary>
        public static void Districts(List<string> into)
        {
            into.Clear();
            try
            {
                foreach (KeyValuePair<string, List<Building>> pair in BuildingHelper.AllNeighbourhoodBuildings)
                {
                    if (!string.IsNullOrEmpty(pair.Key) && pair.Key != GlobalDistrict && pair.Value != null && pair.Value.Count > 0 && CityCheck.Here(pair.Key))
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

        /// <summary>The buildings in a district to let by the game's own list, empty and of the type's kind, busiest street first.</summary>
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
                    if (building == null || building.BuildingType != kind || building.SpecialService != null || GameDlc.Gated(building) || RivalsHelper.IgnoredAddresses.Contains(building.Address)
                        || SpecialBuildings.Holds(building.StreetName, building.StreetNumber))   // one of the mod's buildings, or the university's: off the market
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
        /// The customers an ordinary day brings a business of a type in a building, by the game's own formula
        /// (CustomerEntriesCalculator) averaged over a week; a registration's own hours and limit where it has them.
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

        /// <summary>Asks for a name with the game's own text input; false if it has none to show.</summary>
        public static bool AskName(string initial, Action<string> done)
        {
            try
            {
                UIs uis = InstanceBehavior<UIs>.Instance;
                if (uis == null || uis.textInputUi == null)
                {
                    return false;
                }
                GameUi.TextBox.Ask("reallife_brand_name_placeholder", "reallife_brand_name_confirm", done, null, initial ?? string.Empty, false, BrandCatalogue.NameMost);
                return true;
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                return false;
            }
        }

        /// <summary>Takes an amount as a transaction of the mod's own. Unless <paramref name="force"/>, false and nothing taken if the cash is short.</summary>
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

        // A dictionary each: the game keeps a transaction's data for as long as it lists the transaction.
        private static TransactionInfo Info(string key, string name)
        {
            return new TransactionInfo(key, new Dictionary<string, string>(2) { { "name", name ?? string.Empty } });
        }

        /// <summary>The franchised shops on the city map. Once a second: one comparison while nothing has changed, else the markers again, a few a call.</summary>
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
                            CityManager city = InstanceBehavior<CityManager>.Instance;
                            CityBuildingController building = city != null ? city.FindCityBuildingController(address) : null;
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
