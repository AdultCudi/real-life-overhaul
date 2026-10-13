using System;
using System.Collections.Generic;
using BAModAPI;
using BigAmbitions.Rivals;
using Buildings;
using Entities;
using Extensions;
using GleyTrafficSystem;
using Helpers;
using Localizor;
using Streets;
using UI;
using UnityEngine;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// What the property business asks of the rest of the mod. Each is optional and has a default, so
    /// Economy/Estate works alone; Scripts/Wiring/EstateLinks.cs fills them in. It never calls another feature
    /// directly, except Travel's catalogue of hotels and rooms, which is static data.
    /// </summary>
    public static class EstateBridges
    {
        /// <summary>The council's rent policy: -1 freed, 0 as it was, +1 controlled. Default: as it was.</summary>
        public static Func<int> RentLever;

        /// <summary>The council's permits policy: -1 tightened, +1 eased. Default: as it was.</summary>
        public static Func<int> PermitLever;

        /// <summary>What is on in the city on a day, by the event calendar's own number (0 for nothing). Default: nothing.</summary>
        public static Func<int, int> EventOn;

        /// <summary>The rows the Planner shows for the property business are handed over by Wiring; this says they changed. Default: nobody listens.</summary>
        public static Action Changed;

        public static void Reset()
        {
            RentLever = null;
            PermitLever = null;
            EventOn = null;
            Changed = null;
        }
    }

    /// <summary>One building of the city as the property business keeps it for the session.</summary>
    internal sealed class CityBuilding
    {
        public Building B;
        public float Custom;        // the game's own custom price, as it was found: put back when the feature stops
        public int Base;            // the game's own value of it
        public int Key;             // a number for its address
        public int Units = 1;       // the units the game counts in it (EstateRules.UnitCount)
        public string Place, District;
        public EstateHolding Holding;   // the player's, while they own it
    }

    /// <summary>
    /// Everything the property business reads from or does to the game, in one place.
    ///
    /// READ: every building's own value and district (Building.GetMarketValue, Neighbourhood), and the units it is
    /// let in (its floor, Building.totalSqm, over its unit's size, BuildingSizeData.squareMeters); the buildings the
    /// player owns and what the game recorded of the purchase (GameInstance.realEstate); the game's listings
    /// (GameInstance.buildingsForSale); the employees at the agency's office (EmployeeInstance); Travel's hotels
    /// and rooms; the player's vehicles (GameInstance.VehicleInstances, VehicleHelper.AllPlayerVehicles).
    ///
    /// WRITTEN, each put back when the feature stops:
    ///   Building.customMarketPrice    the game's one figure for "what this building is worth" (GetMarketValue
    ///                                 answers it when it is set): every screen and rule of the game then uses the
    ///                                 mod's value. The asset's own figure is kept and put back.
    ///   BuildingForSale.buildingPrice of listings that are not the player's, so the game asks what the mod says
    ///   RealEstate.pricePerSqm and occupancy of a building the player lets himself: the game's own nightly run
    ///                                 (RealEstateHelper.UpdatePlayerRealEstate) then pays exactly the agreed rent,
    ///                                 or nothing. Put at the market's rent and half let before a save and when
    ///                                 the feature stops, which is what the game gives a building it has just sold.
    ///
    /// DONE: money, as dated transactions (GameManager.ChangeMoneySafe); a sale, by the lines of the game's own
    /// sale to a competitor (RealEstateHelper.SimulateCompetitorBuyingPlayerBuildings); a car that goes out on
    /// hire and comes back (the object is removed and made again with VehicleHelper.CreateAndSpawnVehicle; its
    /// entry in the save's list of vehicles stays where it is throughout, so a save always has the car).
    ///
    /// Nothing here runs on a timer: once when the feature starts, once a game day, once a month of the city's
    /// economy, and when the player presses something.
    /// </summary>
    internal static class EstateGame
    {
        public const string RentKey = "ba:transaction_rentrevenue";       // the game's own, with the address
        public const string SoldKey = "ba:transaction_buildingsold";      // the game's own: the mortgage's lender looks for it
        public const string WorksKey = "reallife_transaction_estate_works";
        public const string CoverKey = "reallife_transaction_estate_cover";
        public const string ClaimKey = "reallife_transaction_estate_claim";
        public const string CourtKey = "reallife_transaction_estate_court";
        public const string FeeKey = "reallife_transaction_estate_fee";
        public const string GainsKey = "reallife_transaction_estate_gains";
        public const string MarketingKey = "reallife_transaction_estate_marketing";
        public const string CommissionKey = "reallife_transaction_estate_commission";
        public const string HotelBuyKey = "reallife_transaction_estate_hotel_buy";
        public const string HotelSoldKey = "reallife_transaction_estate_hotel_sold";
        public const string HotelTakingsKey = "reallife_transaction_estate_hotel_takings";
        public const string HotelCostsKey = "reallife_transaction_estate_hotel_costs";
        public const string HireRentKey = "reallife_transaction_estate_hire_rent";
        public const string HireDepositKey = "reallife_transaction_estate_hire_deposit";
        public const string HireCostsKey = "reallife_transaction_estate_hire_costs";
        public const string HireClaimKey = "reallife_transaction_estate_hire_claim";
        public const string TradingTaxKey = "reallife_transaction_estate_trading_tax";
        private const string DeductKey = "reallife_estate_deduct";
        private const string CustomerService = "ba:skill_customerservice";
        private const string OfficeType = "ba:buildingtype_office";
        private const float KeepAway = 40f;        // metres: a car does not leave or come back with the player nearer than this
        private const float SpaceClear = 3f;       // metres: a space is taken if another of the player's vehicles stands this near its middle
        private const float GameRentShare = 50f;   // the share let that the game's own letting is handed back with

        public static IModLogger Logger;

        private static readonly List<CityBuilding> City = new List<CityBuilding>(512);
        private static readonly Dictionary<string, CityBuilding> ByPlace = new Dictionary<string, CityBuilding>(512);
        private static readonly Dictionary<string, long> SqmSum = new Dictionary<string, long>(24);
        private static readonly Dictionary<string, int> SqmCount = new Dictionary<string, int>(24);

        private static GameInstance Save => SaveGameManager.Current;

        public static int Cash => Save != null ? Mathf.FloorToInt(Mathf.Max(0f, Save.Money)) : 0;

        public static int Buildings => City.Count;

        // ---------------------------------------------------------------- the day's climate

        /// <summary>The city as it stands on a day: the cycle, prices, the bank's rate, the council's policies, the season, the weekday and what is on.</summary>
        public static EstateClimate Climate(int day)
        {
            GameInstance save = Save;
            int year = GameCalendar.DaysPerYear;
            return new EstateClimate
            {
                Cycle = EstateCatalogue.Cycle(CityEconomy.State),
                Index = Prices.Level,
                Quarters = Mathf.RoundToInt(CityEconomy.BasePoints * 4f),
                RentLever = EstateBridges.RentLever != null ? EstateBridges.RentLever() : 0,
                PermitLever = EstateBridges.PermitLever != null ? EstateBridges.PermitLever() : 0,
                DaysPerYear = year,
                TaxPercent = save != null && save.gameVariables != null ? Mathf.Max(0, save.gameVariables.taxPercentage) : 0,
                Event = EstateBridges.EventOn != null ? EstateBridges.EventOn(day) : 0,
                Season = Mathf.Clamp((int)TravelCatalogue.Season(day), 0, 3),
                Weekday = Mathf.Clamp((int)TimeHelper.GetDayOfWeek(day) - 1, 0, 6),
                RentScale = RentScale
            };
        }

        /// <summary>The options' rental income (percent) as the rules take it: thousandths of the market's rent.</summary>
        public static int RentScale => EstateRules.RentShare(Settings.EstateRent * 10);

        // ---------------------------------------------------------------- the city's buildings and their values

        /// <summary>A building by its address, written as business pressure writes a place.</summary>
        public static string Place(string street, int number)
        {
            return Uri.EscapeDataString(street ?? string.Empty) + "~" + number.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        public static Address AddressOf(string place)
        {
            int at = place != null ? place.LastIndexOf('~') : -1;
            int number;
            return at > 0 && int.TryParse(place.Substring(at + 1), out number) ? new Address(Uri.UnescapeDataString(place.Substring(0, at)), number) : null;
        }

        /// <summary>
        /// Every building of the city, once per start: its own value and custom price as the game has them, and what
        /// a square metre goes for in each district. One pass over the game's buildings. False while the city is not there.
        /// </summary>
        public static bool ReadCity()
        {
            List<Building> all = BuildingHelper.allBuildings;
            if (all == null || all.Count == 0)
            {
                return false;
            }
            RestoreValues();
            for (int i = 0; i < all.Count; i++)
            {
                Building b = all[i];
                if (b == null || string.IsNullOrEmpty(b.StreetName))
                {
                    continue;
                }
                string place = Place(b.StreetName, b.StreetNumber);
                if (ByPlace.ContainsKey(place))
                {
                    continue;
                }
                float value, sqm;
                try
                {
                    value = b.GetMarketValue();
                    sqm = b.GetMarketPricePerSqm();
                }
                catch (Exception)
                {
                    continue;   // a building the game cannot value is left alone
                }
                if (!(value > 0f))
                {
                    continue;
                }
                var c = new CityBuilding
                {
                    B = b, Custom = b.customMarketPrice, Base = Mathf.RoundToInt(Mathf.Min(value, int.MaxValue / 8)), Place = place, Key = EstateRules.Hash(place), District = b.Neighbourhood ?? string.Empty,
                    Units = EstateRules.UnitCount(b.totalSqm, UnitSqm(b))
                };
                City.Add(c);
                ByPlace[place] = c;
                long sum;
                int count;
                SqmSum.TryGetValue(c.District, out sum);
                SqmCount.TryGetValue(c.District, out count);
                SqmSum[c.District] = sum + (long)Mathf.Max(0f, sqm);
                SqmCount[c.District] = count + 1;
            }
            return City.Count > 0;
        }

        // The size of the unit the game lets on its own in a building and lets the player take for a business (its
        // BuildingSizeData, as Building.GetBuildingDailyMarketRent and RealEstate.MaxOccupancy read it); 0 for a size
        // the game has no data of (BuildingSizeHelper.GetData looks it up in a dictionary and throws for one it lacks).
        private static int UnitSqm(Building b)
        {
            try
            {
                BuildingSizeData size = BuildingSizeHelper.GetData(b.BuildingSize);
                return size != null ? Mathf.Max(0, size.squareMeters) : 0;
            }
            catch (Exception)
            {
                return 0;
            }
        }

        internal static CityBuilding Find(string place)
        {
            CityBuilding c;
            return place != null && ByPlace.TryGetValue(place, out c) ? c : null;
        }

        private static int Income(string district)
        {
            return Mathf.RoundToInt(CityEconomy.DistrictIncome(district) * 10f);
        }

        /// <summary>What the mod says a building is worth now; 0 for one it does not know.</summary>
        public static int Value(CityBuilding c, EstateState s, int index)
        {
            if (c == null)
            {
                return 0;
            }
            return c.Holding != null ? EstateRules.Value(c.Holding, s.Real, index) : EstateRules.Value(c.Base, s.Real, index, Income(c.District), EstateRules.StreetCondition(s.Seed, c.Key), 0);
        }

        /// <summary>
        /// Every building's value is written where the game reads it (Building.customMarketPrice), and the game's
        /// listings that are not the player's are priced by it. One pass over the city's buildings and one over
        /// the listings. Returns how many buildings were written.
        /// </summary>
        public static int WriteValues(EstateState s, int index)
        {
            for (int i = 0; i < City.Count; i++)
            {
                CityBuilding c = City[i];
                if (c.B != null)
                {
                    c.B.customMarketPrice = Mathf.Max(1, Value(c, s, index));
                }
            }
            Reprice();
            return City.Count;
        }

        /// <summary>One building's value, written now: the player has changed something there.</summary>
        public static void WriteValue(EstateHolding h, EstateState s, int index)
        {
            CityBuilding c = Find(h.Place);
            if (c != null && c.B != null)
            {
                c.B.customMarketPrice = Mathf.Max(1, Value(c, s, index));
            }
        }

        private static void Reprice()
        {
            GameInstance save = Save;
            List<BuildingForSale> listed = save != null ? save.buildingsForSale : null;
            for (int i = 0; listed != null && i < listed.Count; i++)
            {
                BuildingForSale f = listed[i];
                CityBuilding c = f != null && f.address != null ? Find(Place(f.address.streetName, f.address.streetNumber)) : null;
                if (c != null && c.Holding == null && c.B != null)
                {
                    f.buildingPrice = c.B.GetMarketValue();
                }
            }
        }

        /// <summary>Every building's custom price is the game's own again, and nothing of the city is kept.</summary>
        public static int RestoreValues()
        {
            int put = City.Count;
            for (int i = 0; i < City.Count; i++)
            {
                if (City[i].B != null)
                {
                    City[i].B.customMarketPrice = City[i].Custom;
                }
            }
            City.Clear();
            ByPlace.Clear();
            SqmSum.Clear();
            SqmCount.Clear();
            return put;
        }

        public static int UseOf(string buildingType)
        {
            string type = buildingType ?? string.Empty;
            return type.EndsWith("residential", StringComparison.Ordinal) ? (int)EstateUse.Homes : type.EndsWith("office", StringComparison.Ordinal) ? (int)EstateUse.Offices
                : type.EndsWith("warehouse", StringComparison.Ordinal) ? (int)EstateUse.Storage : (int)EstateUse.Shops;
        }

        // ---------------------------------------------------------------- what the player owns

        private static RealEstate Estate(string place)
        {
            GameInstance save = Save;
            List<RealEstate> owned = save != null ? save.realEstate : null;
            for (int i = 0; owned != null && i < owned.Count; i++)
            {
                RealEstate e = owned[i];
                if (e != null && e.address != null && Place(e.address.streetName, e.address.streetNumber) == place)
                {
                    return e;
                }
            }
            return null;
        }

        /// <summary>
        /// The buildings the player owns as they stand today, each with its file. One seen for the first time gets
        /// one, with the condition the street has left it in; one that is no longer the player's is dropped (a sale
        /// in the game's own app, a seizure). The Hamptons houses are homes of the game's and are left to it.
        /// </summary>
        public static void ReadHoldings(EstateState s, out int added, out int dropped)
        {
            added = dropped = 0;
            GameInstance save = Save;
            for (int i = 0; i < s.Holdings.Count; i++)
            {
                s.Holdings[i].Seen = false;
            }
            for (int i = 0; i < City.Count; i++)
            {
                City[i].Holding = null;
            }
            List<RealEstate> owned = save != null ? save.realEstate : null;
            for (int i = 0; owned != null && i < owned.Count; i++)
            {
                RealEstate e = owned[i];
                CityBuilding c = e != null && e.address != null ? Find(Place(e.address.streetName, e.address.streetNumber)) : null;
                if (c == null || c.B == null || c.B.IsHamptonsHouse())
                {
                    continue;
                }
                EstateHolding h = s.Holding(c.Place);
                if (h == null)
                {
                    if (s.Holdings.Count >= EstateCatalogue.HoldingsMost)
                    {
                        continue;
                    }
                    h = new EstateHolding { Place = c.Place, Use = UseOf(c.B.BuildingType), Cond = EstateRules.StreetCondition(s.Seed, c.Key), Managed = false };
                    s.Holdings.Add(h);
                    added++;
                }
                BuildingRegistration reg = BuildingHelper.GetBuildingRegistration(e.address);
                h.Seen = true;
                h.Paid = (int)Math.Min(int.MaxValue / 4, Math.Max(0.0, e.purchasePrice));
                h.BoughtDay = e.purchaseDay;
                h.Base = c.Base;
                h.AsBuilt = UseOf(c.B.BuildingType);
                h.District = c.District;
                h.Income = Income(c.District);
                h.Name = e.address.ToFormattedString();
                h.Self = reg != null && GoingPublic.Mine(reg);
                EstateRules.FitUnits(h, c.Units);
                c.Holding = h;
            }
            for (int i = s.Holdings.Count - 1; i >= 0; i--)
            {
                if (!s.Holdings[i].Seen)
                {
                    s.Holdings.RemoveAt(i);
                    dropped++;
                }
            }
        }

        /// <summary>
        /// The game is told what its nightly run should pay for a building the player lets himself: the agreed rent
        /// of every unit whose tenant owes nothing (EstateRules.PayingRent), or nothing. With <paramref name="own"/>
        /// the figures are the game's own again (the market's rent, half let). A rent set in the game's own app
        /// meanwhile is taken off: the tenancies' rents are the agreed ones. False if the game no longer has the
        /// building as the player's.
        /// </summary>
        public static bool WriteRent(EstateHolding h, bool own)
        {
            RealEstate e = Estate(h.Place);
            if (e == null)
            {
                return false;
            }
            e.pendingPricePerSqm = 0f;
            e.daysUntilUpdatingPricePerSqm = 0;
            if (own)
            {
                CityBuilding c = Find(h.Place);
                e.pricePerSqm = c != null && c.B != null ? c.B.GetBuildingDailyMarketRentPerSqm() : e.pricePerSqm;
                e.occupancy = GameRentShare;
                return true;
            }
            float occupancy, price;
            int rent = EstateRules.PayingRent(h);
            EstateRules.Encode(rent, rent > 0, e.totalSqm, e.MaxOccupancy, out occupancy, out price);
            e.occupancy = occupancy;
            e.pricePerSqm = price;
            return true;
        }

        /// <summary>What the game's nightly run will pay for a building as its figures stand.</summary>
        public static int GameRent(EstateHolding h)
        {
            RealEstate e = Estate(h.Place);
            try
            {
                return e != null ? Mathf.RoundToInt(e.DailyIncome) : 0;
            }
            catch (Exception)
            {
                return 0;
            }
        }

        /// <summary>The game's own purchase screen for a building (BizMan), where an offer is made and the building bought.</summary>
        public static bool OpenPurchase(string place)
        {
            Address address = AddressOf(place);
            UIs ui = InstanceBehavior<UIs>.Instance;
            if (address == null || ui == null || ui.fullMenu == null || ui.fullMenu.bizMan == null)
            {
                return false;
            }
            ui.fullMenu.bizMan.Open(address);
            return true;
        }

        /// <summary>
        /// A sale completes: the lines of the game's own sale of a player's building to a competitor
        /// (RealEstateHelper.SimulateCompetitorBuyingPlayerBuildings), for the price agreed. The price arrives under
        /// the game's own "building sold" transaction, which is what a mortgage's lender looks for. False, with
        /// nothing done, if the game no longer has the building as the player's or the player stands inside it.
        /// </summary>
        public static bool Sell(EstateHolding h, int price)
        {
            GameInstance save = Save;
            RealEstate e = Estate(h.Place);
            if (save == null || e == null || price <= 0)
            {
                return false;
            }
            Address address = e.address;
            BuildingManager inside = InstanceBehavior<BuildingManager>.Instance;
            if (BuildingManager.IsInsideBuilding && inside != null && inside.building != null && inside.building.Address == address)
            {
                return false;
            }
            BuildingRegistration reg = BuildingHelper.GetBuildingRegistration(address);
            Building building = BuildingHelper.GetBuilding(address);
            GameManager.ChangeMoneySafe(price, new TransactionInfo(SoldKey, new Dictionary<string, string>(2) { { "address", address.ToFormattedString() } }), null, null, true);
            if (reg != null)
            {
                reg.buildingOwnerRivalId = RivalsHelper.GetRandomRivalForBuilding(reg.Neighborhood);
                if (reg.RentedByPlayer && building != null)
                {
                    reg.RentPerDay = building.GetBuildingDailyMarketRent();
                }
            }
            save.realEstate.Remove(e);
            for (int i = save.buildingsForSale.Count - 1; i >= 0; i--)
            {
                if (save.buildingsForSale[i] != null && save.buildingsForSale[i].address == address)
                {
                    save.buildingsForSale.RemoveAt(i);
                }
            }
            CityManager city = InstanceBehavior<CityManager>.Instance;
            CityBuildingController controller = city != null ? city.FindCityBuildingController(address) : null;
            if (controller != null)
            {
                controller.UpdatePoi();
            }
            UIs ui = InstanceBehavior<UIs>.Instance;
            if (ui != null && ui.mapFilters != null)
            {
                ui.mapFilters.ApplyFilters();
            }
            RealEstateHelper.AddNoHomeModifierIfNeeded();
            return true;
        }

        // ---------------------------------------------------------------- the agency

        /// <summary>The office buildings the player holds, for the agency to be run from.</summary>
        public static void Offices(List<BuildingRegistration> into)
        {
            into.Clear();
            GameInstance save = Save;
            List<BuildingRegistration> all = save != null ? save.BuildingRegistrations : null;
            for (int i = 0; all != null && i < all.Count; i++)
            {
                BuildingRegistration reg = all[i];
                if (reg != null && GoingPublic.Mine(reg) && reg.HasValidAddress && reg.GetBuildingType() == OfficeType)
                {
                    into.Add(reg);
                }
            }
        }

        public static string NameOf(string place)
        {
            Address address = AddressOf(place);
            return address != null ? address.ToFormattedString() : string.Empty;
        }

        /// <summary>
        /// The agency as it stands this morning: whether the office is still the player's, the negotiators assigned
        /// there (anybody with the game's customer service skill and hours on its rota) and what a day of them comes
        /// to, and every district with what a square metre goes for there. One pass over the game's employees.
        /// </summary>
        public static void ReadAgency(AgencyFile agency)
        {
            GameInstance save = Save;
            agency.Capacity = agency.Negotiators = 0;
            foreach (KeyValuePair<string, long> pair in SqmSum)
            {
                if (pair.Key.Length == 0)
                {
                    continue;
                }
                DistrictRep d = agency.District(pair.Key);
                if (d == null)
                {
                    if (agency.Districts.Count >= EstateCatalogue.DistrictsMost)
                    {
                        continue;
                    }
                    d = new DistrictRep { Name = pair.Key };
                    agency.Districts.Add(d);
                }
                d.SqmPrice = (int)Math.Min(int.MaxValue / 8, pair.Value / Math.Max(1, SqmCount[pair.Key]));
            }
            if (!agency.Running || save == null)
            {
                return;
            }
            Address office = AddressOf(agency.Office);
            BuildingRegistration reg = office != null ? BuildingHelper.GetBuildingRegistration(office) : null;
            if (reg == null || !GoingPublic.Mine(reg))
            {
                agency.Office = string.Empty;   // the office is no longer held: the agency is shut, what it had in hand lapses by its own days
                return;
            }
            List<EmployeeInstance> all = save.EmployeeInstances;
            for (int i = 0; all != null && i < all.Count; i++)
            {
                EmployeeInstance e = all[i];
                if (e == null || e.IsCandidate || e.assignedAddress == null || !(e.assignedAddress == office) || e.assignedWeeklyHours <= 0 || !e.HasSkill(CustomerService))
                {
                    continue;
                }
                agency.Negotiators++;
                agency.Capacity += AgencyRules.Capacity(e.assignedWeeklyHours, Mathf.RoundToInt(e.GetSkillValue(CustomerService) * Mathf.Clamp01(e.satisfaction / 100f)));
            }
        }

        // ---------------------------------------------------------------- hotels

        public static HotelSlot Slot(string id)
        {
            return TravelCatalogue.Hotel(id);
        }

        public static string HotelName(HotelSlot slot)
        {
            return slot != null ? slot.NameKey.GetLocalization() : string.Empty;
        }

        /// <summary>A hotel's tier, district and tonight's going rates, from Travel's own figures.</summary>
        public static bool ReadHotel(HotelFile f, int day)
        {
            HotelSlot slot = Slot(f.Slot);
            f.Seen = slot != null;
            if (slot == null)
            {
                return false;
            }
            RoomType[] rooms = TravelCatalogue.Rooms(slot.Tier);
            f.Tier = Mathf.Clamp((int)slot.Tier, 0, EstateCatalogue.HotelTiers - 1);
            f.Income = Income(slot.Neighbourhood);
            f.Name = HotelName(slot);
            for (int type = 0; type < EstateCatalogue.RoomTypes && type < rooms.Length; type++)
            {
                f.Going[type] = TravelCatalogue.NightRate(slot, rooms[type], day, (int)TimeHelper.GetDayOfWeek(day), 1f);
            }
            return true;
        }

        /// <summary>What a hotel is asked for today.</summary>
        public static int HotelPrice(HotelSlot slot, int real, int daysPerYear)
        {
            RoomType[] rooms = TravelCatalogue.Rooms(slot.Tier);
            return HotelRules.Price((int)slot.Tier, Mathf.RoundToInt(rooms[0].Rate * slot.RateFactor), Mathf.RoundToInt(rooms[rooms.Length > 1 ? 1 : 0].Rate * slot.RateFactor), real, daysPerYear);
        }

        public static string RoomName(int tier, int type)
        {
            RoomType[] rooms = TravelCatalogue.Rooms((HotelTier)Mathf.Clamp(tier, 0, EstateCatalogue.HotelTiers - 1));
            return rooms[Mathf.Clamp(type, 0, rooms.Length - 1)].NameKey.GetLocalization();
        }

        // ---------------------------------------------------------------- car hire

        /// <summary>The buildings the player holds, for the hire business to be run from.</summary>
        public static void Lots(List<BuildingRegistration> into)
        {
            into.Clear();
            GameInstance save = Save;
            List<BuildingRegistration> all = save != null ? save.BuildingRegistrations : null;
            for (int i = 0; all != null && i < all.Count; i++)
            {
                BuildingRegistration reg = all[i];
                if (reg != null && reg.HasValidAddress && (GoingPublic.Mine(reg) || reg.BuildingOwnedByPlayer) && !reg.BuildingCached.IsHamptonsHouse())
                {
                    into.Add(reg);
                }
            }
        }

        /// <summary>The spaces a lot has: the building's own vehicle spaces by the game's size data, or the kerb in front of one that has none. 0 if it is no longer held.</summary>
        public static int Spaces(string lot)
        {
            Address address = AddressOf(lot);
            BuildingRegistration reg = address != null ? BuildingHelper.GetBuildingRegistration(address) : null;
            if (reg == null || (!GoingPublic.Mine(reg) && !reg.BuildingOwnedByPlayer))
            {
                return 0;
            }
            BuildingSizeData size = BuildingSizeHelper.GetData(reg);
            return Math.Min(EstateCatalogue.HireCarsMost, Math.Max(EstateCatalogue.HireSpacesAnyBuilding, size != null ? size.numberOfVehicleSlots : 0));
        }

        private static bool Door(string lot, out Vector3 door)
        {
            door = default(Vector3);
            Address address = AddressOf(lot);
            CityManager city = InstanceBehavior<CityManager>.Instance;
            CityBuildingController controller = address != null && city != null ? city.FindCityBuildingController(address) : null;
            if (controller == null || controller.entranceDoors == null)
            {
                return false;
            }
            for (int i = 0; i < controller.entranceDoors.Length; i++)
            {
                if (controller.entranceDoors[i] != null && controller.entranceDoors[i].doorTransform != null)
                {
                    door = controller.entranceDoors[i].doorTransform.position;
                    return true;
                }
            }
            return false;
        }

        public static VehicleInstance Vehicle(string id)
        {
            GameInstance save = Save;
            List<VehicleInstance> all = save != null ? save.VehicleInstances : null;
            for (int i = 0; all != null && i < all.Count; i++)
            {
                if (all[i] != null && all[i].id == id)
                {
                    return all[i];
                }
            }
            return null;
        }

        private static VehicleController Controller(string id)
        {
            List<VehicleController> all = VehicleHelper.AllPlayerVehicles;
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i] != null && all[i].vehicleInstance != null && all[i].vehicleInstance.id == id)
                {
                    return all[i];
                }
            }
            return null;
        }

        /// <summary>
        /// Whether a vehicle of the player's can go on hire from a lot: a motor vehicle that is theirs to sell,
        /// standing empty and legally parked in the open within sixty metres of the lot's door, with nothing in it
        /// and nobody in it. With false, <paramref name="why"/> is the key of the sentence that says what is in the way.
        /// </summary>
        public static bool MayHire(VehicleInstance v, string lot, out string why)
        {
            why = null;
            GameInstance save = Save;
            VehicleController controller = v != null ? Controller(v.id) : null;
            Vector3 door;
            if (GoingPublic.InSession)
            {
                why = "reallife_gp_estate_hire";   // a car taken out of the city and made again would not be the same car to Going Public's other players (Core/GoingPublic)
            }
            else if (v == null || save == null || v.VehicleType == null || !v.VehicleType.IsMotorVehicle || v.zeroSellingPrice || v.VehicleType.spawnInPlayerObject)
            {
                why = "reallife_estate_hire_no_kind";
            }
            else if (Dealerships.Stock(v))
            {
                why = "reallife_estate_hire_no_stock";   // the dealership's to sell: a hire would take it out of its showroom and its records (Core/Dealerships)
            }
            else if (controller == null || controller.controlledByPlayer || save.ActiveVehicleId == v.id)
            {
                why = "reallife_estate_hire_no_driven";
            }
            else if (v.cargoInstances != null && v.cargoInstances.Count > 0)
            {
                why = "reallife_estate_hire_no_cargo";
            }
            else if (v.parkingState == ParkingState.Illegal || v.unpaidParkingAmount > 0f || !v.Address.IsUndefined())
            {
                why = "reallife_estate_hire_no_parked";
            }
            else if (!Door(lot, out door) || (controller.transform.position - door).sqrMagnitude > EstateCatalogue.HireLotMetres * EstateCatalogue.HireLotMetres)
            {
                why = "reallife_estate_hire_no_lot";
            }
            return why == null;
        }

        /// <summary>The player's vehicles that could go on hire from a lot now and are not on its books yet.</summary>
        public static void Hireable(HireFile hire, List<VehicleInstance> into)
        {
            into.Clear();
            GameInstance save = Save;
            List<VehicleInstance> all = save != null ? save.VehicleInstances : null;
            for (int i = 0; all != null && i < all.Count; i++)
            {
                string why;
                if (all[i] != null && hire.Car(all[i].id) == null && MayHire(all[i], hire.Lot, out why))
                {
                    into.Add(all[i]);
                }
            }
        }

        public static string CarName(string type)
        {
            return string.IsNullOrEmpty(type) ? string.Empty : LocalizorManager.IsLocalizedKey(type) ? type.GetLocalization() : type;
        }

        public static int CarPrice(VehicleInstance v)
        {
            return v != null && v.VehicleType != null ? Mathf.RoundToInt(Mathf.Max(0f, v.VehicleType.price)) : 0;
        }

        private static bool PlayerNear(Vector3 at)
        {
            PlayerController player = PlayerHelper.PlayerController;
            return (player != null && (player.transform.position - at).sqrMagnitude < KeepAway * KeepAway) || (EconomyBridges.InView != null && EconomyBridges.InView(at));
        }

        /// <summary>A car in good order is one whose service is not overdue and whose tyres and brakes are not worn out (the mod's own car records).</summary>
        public static bool GoodOrder(string id, int day)
        {
            CarRecord r = CarGarage.Loaded ? CarGarage.For(id, day) : null;
            return r == null || (!r.Broken && r.Tyres >= CarRules.WarnBelow && r.Brakes >= CarRules.WarnBelow);
        }

        /// <summary>
        /// A hired car leaves: its object is taken out of the city (as the game's own removal does, without taking
        /// its entry off the save's list: VehicleHelper.Delete, the first half) and where it stood is noted. It is
        /// unassigned from a warehouse the game's own way. False, with nothing done, if it cannot go now: driven,
        /// moved, or (unless <paramref name="force"/>, a test tool) the player is standing by it or looking at it.
        /// </summary>
        public static bool TakeOut(HireCar car, string lot, bool force)
        {
            VehicleInstance v = Vehicle(car.Id);
            VehicleController controller = Controller(car.Id);
            string why;
            if (v == null || controller == null || !MayHire(v, lot, out why) || (!force && PlayerNear(controller.transform.position)))
            {
                return false;
            }
            Vector3 at = controller.transform.position;
            car.X = Mathf.RoundToInt(at.x * 100f);
            car.Y = Mathf.RoundToInt(at.y * 100f);
            car.Z = Mathf.RoundToInt(at.z * 100f);
            car.Yaw = Mathf.RoundToInt(controller.transform.eulerAngles.y * 10f);
            v.UnAssignFromWarehouse(true);
            Hide(controller);
            v.parkingState = ParkingState.Legal;
            return true;
        }

        // The object of a vehicle goes; its entry in the save stays. The traffic is told its collider is gone, as the game tells it.
        private static void Hide(VehicleController controller)
        {
            if (controller.vehicleCollider != null)
            {
                Manager.TriggerColliderRemovedEvent(controller.vehicleCollider);
            }
            if (controller.additionalCollider != null)
            {
                Manager.TriggerColliderRemovedEvent(controller.additionalCollider);
            }
            VehicleHelper.onVehicleDestroyed?.Invoke(controller);
            UnityEngine.Object.Destroy(controller.gameObject);
        }

        /// <summary>A car that is out and has an object in the city (a save was loaded: the game made every vehicle on its list) is taken out again. True if it was.</summary>
        public static bool HideAgain(HireCar car)
        {
            VehicleController controller = Controller(car.Id);
            if (controller == null || controller.controlledByPlayer)
            {
                return false;
            }
            Hide(controller);
            return true;
        }

        /// <summary>True while a car that is out has no object in the city, as it should.</summary>
        public static bool IsOut(HireCar car)
        {
            return Vehicle(car.Id) != null && Controller(car.Id) == null;
        }

        /// <summary>
        /// A hired car comes back to where it stood, made the way the game makes a vehicle
        /// (VehicleHelper.CreateAndSpawnVehicle, which puts its entry on the save's list: so it is taken off first),
        /// with what the hire did to it: the fuel used, the dirt, the damage, and on the mod's car record the
        /// distance and the wear of tyres and brakes that go with it. False, with nothing done, while the space is
        /// taken by another of the player's vehicles or (unless <paramref name="force"/>: the feature is stopping, or
        /// a test tool) the player is standing there or looking.
        /// </summary>
        public static bool BringBack(HireCar car, HireReturn r, int day, bool force)
        {
            GameInstance save = Save;
            VehicleInstance v = Vehicle(car.Id);
            if (save == null || v == null)
            {
                return false;
            }
            if (Controller(car.Id) != null)
            {
                return true;   // it is in the city already (the feature was off for a while): nothing to make
            }
            var at = new Vector3(car.X / 100f, car.Y / 100f, car.Z / 100f);
            if (!force && PlayerNear(at))
            {
                return false;
            }
            List<VehicleController> all = VehicleHelper.AllPlayerVehicles;
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i] != null && (all[i].transform.position - at).sqrMagnitude < SpaceClear * SpaceClear)
                {
                    return false;
                }
            }
            Wear(v, r, day);
            save.VehicleInstances.Remove(v);
            try
            {
                VehicleHelper.CreateAndSpawnVehicle(v, at, Quaternion.Euler(0f, car.Yaw / 10f, 0f));
            }
            catch (Exception)
            {
                if (!save.VehicleInstances.Contains(v))
                {
                    save.VehicleInstances.Add(v);   // whatever happened, the car stays the player's
                }
                throw;
            }
            return true;
        }

        private static void Wear(VehicleInstance v, HireReturn r, int day)
        {
            if (r.Km <= 0)
            {
                return;   // (it was never out, or this is not the end of a hire)
            }
            float tank = v.VehicleType != null ? v.VehicleType.maxFuel : 0f;
            v.fuel = Mathf.Min(v.fuel, tank * r.Fuel / 1000f);
            v.dirtiness = Mathf.Clamp01(v.dirtiness + 0.1f * r.Days);
            v.damage = Mathf.Clamp(v.damage + r.Damage / 1000f, 0f, 1f);
            CarRecord record = CarGarage.Loaded ? CarGarage.For(v.id, day) : null;
            if (record != null && v.VehicleType != null)
            {
                // (the wear is by the kind of car its price makes it: the mod's own kinds need the car's object, which is not there yet)
                int cls = HireRules.Class(Mathf.RoundToInt(v.VehicleType.price));
                CarKind kind = cls == 0 ? CarKind.Economy : cls == 1 ? CarKind.Saloon : CarKind.Luxury;
                record.Km += r.Km;
                record.Tyres = Mathf.Clamp01(record.Tyres - r.Km / Mathf.Max(1f, CarRules.TyreLife(kind)));
                record.Brakes = Mathf.Clamp01(record.Brakes - r.Km / Mathf.Max(1f, CarRules.BrakeLife(kind)));
            }
        }

        /// <summary>A car that never came back is taken off the save's list, the game's own way (VehicleHelper.Delete with no object).</summary>
        public static void Lose(HireCar car)
        {
            VehicleInstance v = Vehicle(car.Id);
            if (v != null)
            {
                VehicleController controller = Controller(car.Id);
                OverlayGuard.Release(controller);
                v.Delete(controller);
            }
        }

        /// <summary>While a car is out the game's hourly parking round has nothing to say about it.</summary>
        public static void KeepLegal(HireCar car)
        {
            VehicleInstance v = Vehicle(car.Id);
            if (v != null && v.parkingState != ParkingState.Legal)
            {
                v.parkingState = ParkingState.Legal;
            }
        }

        // ---------------------------------------------------------------- money

        /// <summary>
        /// Takes an amount as a transaction of the mod's own, at a building or at none. A business cost is marked
        /// deductible as the game marks its own. Unless <paramref name="force"/>, the game's call refuses a charge
        /// the cash does not cover: false, and nothing is taken.
        /// </summary>
        public static bool Take(int amount, string key, string name, string place, bool deductible, bool force)
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
                info.SetTaxDeductibleName(DeductKey.GetLocalization());
            }
            return GameManager.ChangeMoneySafe(-amount, info, null, place != null ? AddressOf(place) : null, force);
        }

        public static void Give(int amount, string key, string name, string place)
        {
            if (amount > 0 && Save != null)
            {
                GameManager.ChangeMoneySafe(amount, Info(key, name), null, place != null ? AddressOf(place) : null, true);
            }
        }

        /// <summary>Rent that comes in outside the game's nightly run (arrears paid at once, a deposit applied): under the game's own rent transaction, so its books and its tax see it as rent.</summary>
        public static void GiveRent(int amount, EstateHolding h)
        {
            Address address = AddressOf(h.Place);
            if (amount > 0 && Save != null && address != null)
            {
                GameManager.ChangeMoneySafe(amount, new TransactionInfo(RentKey, new Dictionary<string, string>(2) { { "address", address.ToFormattedString() } }), null, address, true);
            }
        }

        /// <summary>
        /// The rental income option settled on a night of the game's own rent: added (or, under 100%, taken back) under
        /// the game's own rent transaction and the building's address, so its books read it as that building's rent and
        /// its tax treats it as it treats rent.
        /// </summary>
        public static void AdjustRent(int amount, EstateHolding h)
        {
            Address address = AddressOf(h.Place);
            if (amount != 0 && Save != null && address != null)
            {
                GameManager.ChangeMoneySafe(amount, new TransactionInfo(RentKey, new Dictionary<string, string>(2) { { "address", address.ToFormattedString() } }), null, address, true);
            }
        }

        /// <summary>The businesses' takings in the game's daily statements over some days, as the game's tax adds them up.</summary>
        public static long BusinessSales(int from, int to)
        {
            return Save != null && Save.financialSummaries != null ? (long)TaxCalculationHelper.GetBusinessSales(from, to) : 0L;
        }

        // (a dictionary each: the game keeps hold of a transaction's data for as long as it lists the transaction)
        private static TransactionInfo Info(string key, string name)
        {
            return new TransactionInfo(key, new Dictionary<string, string>(2) { { "name", name ?? string.Empty } });
        }
    }
}
