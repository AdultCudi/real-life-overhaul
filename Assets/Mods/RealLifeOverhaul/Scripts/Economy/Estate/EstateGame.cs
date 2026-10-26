using System;
using System.Collections.Generic;
using BigAmbitions.Rivals;
using Buildings;
using Entities;
using Helpers;
using Localizor;
using Streets;
using UI;
using UnityEngine;

namespace RealLifeOverhaul.Economy
{
    /// <summary>What the property business asks of the rest of the mod, filled in by Wiring/EstateLinks; each is optional.</summary>
    public static class EstateBridges
    {
        /// <summary>The council's rent policy: -1 freed, 0 as it was, +1 controlled. Default: as it was.</summary>
        public static Func<int> RentLever;

        /// <summary>The council's permits policy: -1 tightened, +1 eased. Default: as it was.</summary>
        public static Func<int> PermitLever;

        /// <summary>What is on in the city on a day, by the event calendar's number (0 for nothing).</summary>
        public static Func<int, int> EventOn;

        /// <summary>The Planner's rows for the property business have changed.</summary>
        public static Action Changed;

        /// <summary>Per mille a rival buying up a district adds to the price of its buildings that are not the player's, by the game's district key.</summary>
        public static Func<string, int> Premium;

        public static void Reset()
        {
            RentLever = null;
            PermitLever = null;
            EventOn = null;
            Changed = null;
            Premium = null;
        }
    }

    /// <summary>One building of the city as the property business keeps it for the session.</summary>
    internal sealed class CityBuilding
    {
        public Building B;
        public float Custom;        // the game's own custom price, put back when the feature stops
        public int Base;            // the game's own value of it
        public int Key;             // a number for its address
        public int Units = 1;       // the units the game counts in it
        public string Place, District;
        public EstateHolding Holding;   // the player's, while they own it
    }

    /// <summary>
    /// Everything the property business reads from or does to the game. Building.customMarketPrice and the rent figures
    /// of a building the player lets himself are put back when the feature stops, and the rents before every save too;
    /// the game's listings that are not the player's are priced at the mod's value.
    /// </summary>
    internal static partial class EstateGame
    {
        /// <summary>Owner deduction labels whose mixed costs have no single portable recurrence rule.</summary>
        internal static string[] CompanionUnclassifiedTaxLabels() => new[] { DeductKey.GetLocalization() };

        public const string RentKey = "ba:transaction_rentrevenue";       // the game's own, with the address
        public const string SoldKey = "ba:transaction_buildingsold";      // the game's own: a mortgage's lender looks for it
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
        public const string RepairsKey = "reallife_transaction_estate_repairs";   // a tenant's repair call, by a contractor or the player's materials
        public const string ManagerKey = "reallife_transaction_estate_manager";   // a letting agent's or a property manager's fee
        public const string FineKey = "reallife_transaction_estate_fine";         // the city's fine after a failed inspection
        private const string DeductKey = "reallife_estate_deduct";
        private const string CustomerService = "ba:skill_customerservice";
        private const string OfficeType = "ba:buildingtype_office";
        private const float GameRentShare = 50f;   // per cent let: what the game gives a building it has just sold

        private static readonly List<CityBuilding> City = new List<CityBuilding>(512);
        private static readonly Dictionary<string, CityBuilding> ByPlace = new Dictionary<string, CityBuilding>(512);
        private static readonly Dictionary<(string, int), CityBuilding> ByAddress = new Dictionary<(string, int), CityBuilding>(512);   // by street and number: no place made to look one up
        private static readonly Dictionary<string, long> SqmSum = new Dictionary<string, long>(24);
        private static readonly Dictionary<string, int> SqmCount = new Dictionary<string, int>(24);

        private static GameInstance Save => SaveGameManager.Current;

        public static int Buildings => City.Count;

        /// <summary>The city as the rules see it on a day.</summary>
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

        /// <summary>The option's rental income as the rules take it: thousandths of the market's rent.</summary>
        public static int RentScale => EstateRules.RentShare(Settings.EstateRent * 10);

        /// <summary>A building by its address: the street escaped, a tilde, the number.</summary>
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

        /// <summary>Every building of the city and each district's price a square metre, once per start; false while the city is not there.</summary>
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
                if (b == null || string.IsNullOrEmpty(b.StreetName) || GameDlc.Gated(b))
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
                    B = b, Custom = b.customMarketPrice, Base = Mathf.RoundToInt(Mathf.Min(value, int.MaxValue / 8)), Place = place, Key = EstateRules.Hash(place),
                    District = b.Neighbourhood ?? string.Empty, Units = EstateRules.UnitCount(b.totalSqm, UnitSqm(b))
                };
                City.Add(c);
                ByPlace[place] = c;
                ByAddress[(b.StreetName, b.StreetNumber)] = c;
                long sum;
                int count;
                SqmSum.TryGetValue(c.District, out sum);
                SqmCount.TryGetValue(c.District, out count);
                SqmSum[c.District] = sum + (long)Mathf.Max(0f, sqm);
                SqmCount[c.District] = count + 1;
            }
            return City.Count > 0;
        }

        // The size of the unit the game lets on its own in a building (BuildingSizeData, as RealEstate.MaxOccupancy reads
        // it); 0 for a size the game has no data of, for which BuildingSizeHelper.GetData throws.
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

        private static CityBuilding FindAt(string street, int number)
        {
            CityBuilding c;
            return ByAddress.TryGetValue((street ?? string.Empty, number), out c) ? c : null;
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
            if (c.Holding != null)
            {
                return EstateRules.Value(c.Holding, s.Real, index);
            }
            int value = EstateRules.Value(c.Base, s.Real, index, Income(c.District), EstateRules.StreetCondition(s.Seed, c.Key), 0);
            int premium = EstateBridges.Premium != null ? Mathf.Clamp(EstateBridges.Premium(c.District), 0, 1000) : 0;
            return premium > 0 ? value + value / 1000 * premium : value;
        }

        /// <summary>Every building's value written where the game reads it, and the listings not the player's priced by it.</summary>
        public static int WriteValues(EstateState s, int index)
        {
            for (int i = 0; i < City.Count; i++)
            {
                CityBuilding c = City[i];
                if (c.B != null)
                {
                    float value = Mathf.Max(1, Value(c, s, index));
                    if (c.B.customMarketPrice != value)
                    {
                        c.B.customMarketPrice = value;
                    }
                }
            }
            Reprice();
            return City.Count;
        }

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
                CityBuilding c = f != null && f.address != null ? FindAt(f.address.streetName, f.address.streetNumber) : null;
                if (c != null && c.Holding == null && c.B != null)
                {
                    f.buildingPrice = c.B.GetMarketValue();
                }
            }
        }

        /// <summary>Every building's custom price, and the listings priced by it, are the game's own again, and nothing of the city is kept.</summary>
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
            Reprice();   // the listings the mod priced (they are in the save) take the game's own values again
            City.Clear();
            ByPlace.Clear();
            ByAddress.Clear();
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

        /// <summary>The buildings the player owns today, each with its file; the Hamptons houses are homes and left to the game.</summary>
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
                    int sold = SoldOn(s, s.Holdings[i]);
                    if (Meta.Dev.Verbose)
                    {
                        EstateDirector.Logger?.Info("Estate check (a building let go): " + s.Holdings[i].Name + " (" + s.Holdings[i].Place
                                                    + ") is no longer among the game's buildings of the player's: dropped from the books"
                                                    + (sold > 0 ? "; sold on day " + sold + " in the game's books, remembered so the same ownership cannot come back" : string.Empty) + ": PASS");
                    }
                    s.Holdings.RemoveAt(i);
                    dropped++;
                }
            }
        }

        /// <summary>
        /// The game is told what its nightly run pays for a building the player lets himself, or with <paramref name="own"/>
        /// its own figures again. False if the building is no longer the player's.
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

        /// <summary>What the game's nightly run pays for a building as its figures stand.</summary>
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
        /// A sale completes as the game's own sale to a competitor does (RealEstateHelper.SimulateCompetitorBuyingPlayerBuildings).
        /// False, with nothing done, if the building is no longer the player's or the player stands inside it.
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
            GameManager.ChangeMoneySafe(price, AtAddress(SoldKey, address), null, null, true);
            Release(save, e, null);
            return true;
        }

        /// <summary>
        /// A building is no longer the player's, as the game's own sale leaves it: a rival owner (<paramref name="rival"/>, or
        /// one the game picks), the market rent on a unit the player still uses, out of the player's list and off the market.
        /// </summary>
        private static void Release(GameInstance save, RealEstate e, string rival)
        {
            Address address = e.address;
            BuildingRegistration reg = BuildingHelper.GetBuildingRegistration(address);
            Building building = BuildingHelper.GetBuilding(address);
            string district = GameRegistrations.District(reg);
            if (district != null)   // a registration with no building is left as it is
            {
                reg.buildingOwnerRivalId = !string.IsNullOrEmpty(rival) ? rival : RivalsHelper.GetRandomRivalForBuilding(district);
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
            if (ui != null)
            {
                MapPlaces.Refilter(ui.mapFilters);
            }
            RealEstateHelper.AddNoHomeModifierIfNeeded();
        }

        /// <summary>The office buildings the player holds, for the agency to be run from.</summary>
        public static void Offices(List<BuildingRegistration> into)
        {
            into.Clear();
            GameInstance save = Save;
            List<BuildingRegistration> all = save != null ? save.BuildingRegistrations : null;
            for (int i = 0; all != null && i < all.Count; i++)
            {
                BuildingRegistration reg = all[i];
                if (reg != null && GoingPublic.Mine(reg) && reg.HasValidAddress && GameRegistrations.BuildingType(reg) == OfficeType)
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

        /// <summary>The districts' prices, whether the office is still the player's, and its negotiators: staff there with the customer service skill.</summary>
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
                agency.Office = string.Empty;   // no longer held: the agency shuts, what it had in hand lapses by its own days
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

        /// <summary>A cost as a transaction of the mod's own; unless <paramref name="force"/>, false with nothing taken if cash is short.</summary>
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

        /// <summary>
        /// Rent outside the game's nightly run (arrears paid, a deposit applied, the rental income option's top-up), under
        /// the game's own rent transaction and address, so its books and tax read it as that building's rent.
        /// </summary>
        public static void BookRent(int amount, EstateHolding h)
        {
            Address address = AddressOf(h.Place);
            if (amount != 0 && Save != null && address != null)
            {
                GameManager.ChangeMoneySafe(amount, AtAddress(RentKey, address), null, address, true);
            }
        }

        /// <summary>The businesses' takings in the game's daily statements over some days, as the game's tax adds them up.</summary>
        public static long BusinessSales(int from, int to)
        {
            return Save != null && Save.financialSummaries != null ? (long)TaxCalculationHelper.GetBusinessSales(from, to) : 0L;
        }

        // A dictionary each: the game keeps hold of a transaction's data for as long as it lists the transaction.
        private static TransactionInfo Info(string key, string name)
        {
            return new TransactionInfo(key, new Dictionary<string, string>(2) { { "name", name ?? string.Empty } });
        }

        private static TransactionInfo AtAddress(string key, Address address)
        {
            return new TransactionInfo(key, new Dictionary<string, string>(2) { { "address", address.ToFormattedString() } });
        }
    }
}
