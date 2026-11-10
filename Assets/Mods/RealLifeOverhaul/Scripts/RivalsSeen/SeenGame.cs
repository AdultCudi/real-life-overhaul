using System;
using System.Collections.Generic;
using BAModAPI;
using BigAmbitions.DayNightCycle;
using BigAmbitions.Rivals;
using GleyTrafficSystem;
using Helpers;
using Localizor;
using RealLifeOverhaul.Economy;
using RealLifeOverhaul.Economy.Dealership;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.RivalsSeen
{
    /// <summary>
    /// Everything the rivals seen around the city read from the game, and the one thing they do to it (a car of the traffic pool put on a lane): the game's own rival
    /// owners with their week of takings and their businesses, the game's cars as a ladder by price, the city's eating places by district and the rent of their address,
    /// the doors of a rival's own premises, the player's car and table, the game's notifications. A table of the city is made once a game day; nothing is read per frame.
    /// Nothing of the game's people, businesses or money is changed.
    /// </summary>
    public sealed class SeenGame : ISeenCity
    {
        public const string Hamptons = "ba:neighborhood_thehamptons";

        public static readonly SeenGame City = new SeenGame();
        public static IModLogger Logger;

        private struct Eating
        {
            public string District, Key, Name;
            public int Rent;
            public CityBuildingController Building;
        }

        private static readonly Comparison<Eating> ByDistrictAndRent = (a, b) =>
        {
            int d = string.CompareOrdinal(a.District, b.District);
            return d != 0 ? d : a.Rent != b.Rent ? a.Rent.CompareTo(b.Rent) : string.CompareOrdinal(a.Key, b.Key);
        };

        private static readonly Comparison<MarketModel> ByPrice = (a, b) => a.Price != b.Price ? a.Price.CompareTo(b.Price) : string.CompareOrdinal(a.Type, b.Type);
        private static readonly Comparison<RivalData> ById = (a, b) => string.CompareOrdinal(a.id, b.id);
        private static readonly Predicate<Waypoint> PlainLane = IsPlainLane;

        private readonly List<Eating> _eating = new List<Eating>(64);
        private readonly Dictionary<string, CityBuildingController> _doors = new Dictionary<string, CityBuildingController>(256, StringComparer.Ordinal);
        private readonly List<string> _cars = new List<string>(16);
        private readonly List<string> _districts = new List<string>(8);
        private readonly List<int> _open = new List<int>(16);
        private readonly List<MarketModel> _models = new List<MarketModel>(16);
        private readonly List<string> _colours = new List<string>(8);
        private static readonly List<BuildingRegistration> Regs = new List<BuildingRegistration>(16);
        private static readonly List<Waypoint> Lane = new List<Waypoint>(96);
        private int _tableDay = -1;

        public static int Day => SaveGameManager.Current != null ? SaveGameManager.Current.Day : 0;
        public static int Hour => TimeHelper.CurrentHour;
        public static int Level => Prices.Level;

        // ---- the city's table

        /// <summary>The address of a registration as the table writes a place: its street and number.</summary>
        public static string KeyOf(BuildingRegistration r) => (r.StreetName ?? string.Empty) + "#" + r.StreetNumber;

        /// <summary>True for a business people eat at: the game's restaurants, coffee shops and cafes.</summary>
        public static bool Eats(string type)
        {
            return !string.IsNullOrEmpty(type) && (type.IndexOf("restaurant", StringComparison.OrdinalIgnoreCase) >= 0 || type.IndexOf("coffeeshop", StringComparison.OrdinalIgnoreCase) >= 0
                                                    || type.IndexOf("cafe", StringComparison.OrdinalIgnoreCase) >= 0);
        }

        /// <summary>
        /// Once a game day: the game's cars by price, every business's door by its address, and the eating places by district and rent. One pass over the city's
        /// buildings (never one a DLC keeps: Core/GameDlc).
        /// </summary>
        public void Refresh(int day)
        {
            if (day == _tableDay)
            {
                return;
            }
            _tableDay = day;
            _eating.Clear();
            _doors.Clear();
            _districts.Clear();
            ReadCars();
            CityBuildingController[] all = GameDlc.Open(InstanceBehavior<CityManager>.Instance);
            for (int i = 0; all != null && i < all.Length; i++)
            {
                CityBuildingController c = all[i];
                BuildingRegistration r = c != null ? c.buildingRegistration : null;
                if (r == null || c.building == null || !r.HasEstablishedBusiness || string.IsNullOrEmpty(r.businessTypeName))
                {
                    continue;
                }
                string key = KeyOf(r);
                _doors[key] = c;
                string district = c.building.Neighbourhood;
                if (Eats(r.businessTypeName) && !string.IsNullOrEmpty(district))
                {
                    _eating.Add(new Eating { District = district, Key = key, Name = r.BusinessName, Rent = Mathf.Max(0, Mathf.RoundToInt(r.RentPerDay)), Building = c });
                }
            }
            _eating.Sort(ByDistrictAndRent);
            for (int i = 0; i < _eating.Count; i++)
            {
                string d = _eating[i].District;
                if (d != Hamptons && (_districts.Count == 0 || _districts[_districts.Count - 1] != d))
                {
                    _districts.Add(d);   // (sorted by district, so each comes once)
                }
            }
        }

        /// <summary>True where the city has the Hamptons (Core/CityCheck): without them nobody weekends there.</summary>
        public static bool HasCoast() => CityCheck.Here(Hamptons);

        /// <summary>The table is forgotten: the next day's pass makes it again (a city unloading, the feature switched off).</summary>
        public void Forget()
        {
            _tableDay = -1;
            _eating.Clear();
            _doors.Clear();
            _districts.Clear();
            _cars.Clear();
        }

        // The game's cars a showroom deals in (Economy/Dealer reads them: no trucks, nothing of another mod's), the cheapest first.
        private void ReadCars()
        {
            _cars.Clear();
            try
            {
                DealerGame.Models(_models, _colours);
                _models.Sort(ByPrice);
                for (int i = 0; i < _models.Count; i++)
                {
                    _cars.Add(_models[i].Type);
                }
            }
            catch (Exception e)
            {
                Logger?.Warn("Rivals seen: the game's cars could not be read, so nobody has a car on record this session: " + e.Message);
            }
            _models.Clear();
            _colours.Clear();
        }

        public int Cars => _cars.Count;

        public string Car(int index) => index >= 0 && index < _cars.Count ? _cars[index] : string.Empty;

        /// <summary>The place of a car on the ladder; -1 for one that is not on it.</summary>
        public int CarPlace(string type) => string.IsNullOrEmpty(type) ? -1 : _cars.IndexOf(type);

        public int Districts => _districts.Count;

        public string District(int index) => index >= 0 && index < _districts.Count ? _districts[index] : string.Empty;

        public bool Eatery(string district, int grade, int pick, int day, int hour, out string name)
        {
            int at = Find(district, grade, pick, day, hour);
            name = at >= 0 ? _eating[at].Name : string.Empty;
            return at >= 0;
        }

        // The eating place of a district for a grade and a pick, among those open at that hour of that day; -1 with none.
        private int Find(string district, int grade, int pick, int day, int hour)
        {
            _open.Clear();
            for (int i = 0; i < _eating.Count; i++)
            {
                if (_eating[i].District == district && _eating[i].Building != null && OpenAt(_eating[i].Building.buildingRegistration, day, hour))
                {
                    _open.Add(i);
                }
            }
            int index = SeenRules.VenueIndex(_open.Count, grade, pick);
            int found = index >= 0 ? _open[index] : -1;
            _open.Clear();
            return found;
        }

        // A business's own opening hours for a weekday and an hour; one with no hours on record is taken as open.
        private static bool OpenAt(BuildingRegistration r, int day, int hour)
        {
            if (r == null || r.temporarilyClosed)
            {
                return false;
            }
            if (r.scheduleDays == null || r.scheduleDays.Count == 0)
            {
                return true;
            }
            DayOfWeekOrdered weekday = TimeHelper.GetDayOfWeek(Mathf.Max(1, day));
            for (int i = 0; i < r.scheduleDays.Count; i++)
            {
                ScheduleDay s = r.scheduleDays[i];
                if (s != null && s.day == weekday)
                {
                    return s.isOpen && s.IsOpenAtHour(hour);
                }
            }
            return false;
        }

        /// <summary>
        /// The building a rival is at for a place of their week, and what it is called: their own premises for the office, an eating place of their district for a
        /// meal, one of the Hamptons' for the beach and the weekend there. Null where the city has no such building, or it is shut at that hour.
        /// </summary>
        public CityBuildingController Venue(SeenFile f, SeenSubject s, SeenDue due, int day, int hour, out string name)
        {
            name = string.Empty;
            switch (due.Where.Spot)
            {
                case SeenSpot.Office:
                case SeenSpot.Late:
                {
                    name = s.Firm ?? string.Empty;
                    return !string.IsNullOrEmpty(s.Office) && _doors.TryGetValue(s.Office, out CityBuildingController office) ? office : null;
                }
                case SeenSpot.Lunch:
                case SeenSpot.Dinner:
                case SeenSpot.Beach:
                case SeenSpot.Hamptons:
                {
                    bool away = due.Where.Spot == SeenSpot.Beach || due.Where.Spot == SeenSpot.Hamptons;
                    int at = Find(away ? Hamptons : SeenDay.DistrictOf(f, s, this), away ? due.Pick % SeenCatalogue.Grades : due.Where.Grade, due.Pick, day, hour);
                    name = at >= 0 ? _eating[at].Name : string.Empty;
                    return at >= 0 ? _eating[at].Building : null;
                }
                default:
                    return null;
            }
        }

        // ---- the rivals

        /// <summary>
        /// The day's rivals: the rich list's tycoons through their bridge, then the game's own rival owners in the order of their ids (the game keeps them in a
        /// dictionary, whose order is nobody's rule). Nothing with the game's rivals switched off in the save.
        /// </summary>
        public void Subjects(List<SeenSubject> into, int day)
        {
            into.Clear();
            try
            {
                SeenBridges.Tycoons?.Invoke(into);
            }
            catch (Exception e)
            {
                Logger?.Warn("Rivals seen: the tycoons could not be read today: " + e.Message);
            }
            try
            {
                Owners(into, day);
            }
            catch (Exception e)
            {
                Logger?.Warn("Rivals seen: the game's rival owners could not be read today: " + e.Message);
            }
        }

        private static void Owners(List<SeenSubject> into, int day)
        {
            if (SaveGameManager.Current == null || !RivalsHelper.IsFeatureEnabled)
            {
                return;
            }
            List<RivalData> all = RivalsHelper.GetAllRivalData();
            all.Sort(ById);
            for (int i = 0; i < all.Count; i++)
            {
                RivalData d = all[i];
                if (d == null || string.IsNullOrEmpty(d.id) || string.IsNullOrEmpty(d.rivalName))
                {
                    continue;
                }
                var s = new SeenSubject { Key = "g" + d.id, Name = d.rivalName, Gone = RivalsHelper.IsRivalDefeated(d.id), Shops = d.ownedBusinesses != null ? d.ownedBusinesses.Count : 0, ShopsBefore = -1 };
                Takings(d, s);
                History(d.id, day, s);
                Home(d, s);
                into.Add(s);
            }
        }

        // The week's takings of everything they run, and their busiest business: its name is their firm, its address their office, its district theirs.
        private static void Takings(RivalData d, SeenSubject s)
        {
            double all = 0.0, best = -1.0;
            for (int i = 0; d.ownedBusinesses != null && i < d.ownedBusinesses.Count; i++)
            {
                BuildingRegistration r = d.ownedBusinesses[i];
                if (r == null)
                {
                    continue;
                }
                double week = 0.0;
                for (int k = r.dailyIncomes != null ? Math.Max(0, r.dailyIncomes.Count - SeenRules.WeekDays) : 0; r.dailyIncomes != null && k < r.dailyIncomes.Count; k++)
                {
                    week += r.dailyIncomes[k];
                }
                all += week;
                string key = KeyOf(r);
                if (!string.IsNullOrEmpty(r.BusinessName) && (week > best || (week == best && string.CompareOrdinal(key, s.Office) < 0)))
                {
                    best = week;
                    s.Office = key;
                    s.Firm = r.BusinessName;
                    s.District = GameRegistrations.District(r) ?? string.Empty;
                }
            }
            s.Figure = (long)Math.Max(0.0, Math.Round(all));
        }

        // What the game's own history says they took, and how many businesses they ran, about a week ago: the oldest day it keeps within the week.
        private static void History(string id, int day, SeenSubject s)
        {
            RivalState state = RivalsHelper.GetRivalState(id);
            if (state == null)
            {
                return;
            }
            int oldest = int.MaxValue;
            for (int i = 0; state.weeklyIncomeHistory != null && i < state.weeklyIncomeHistory.Count; i++)
            {
                Tuple<int, float> e = state.weeklyIncomeHistory[i];
                if (e != null && e.Item1 >= day - SeenRules.WeekDays && e.Item1 < day && e.Item1 < oldest)
                {
                    oldest = e.Item1;
                    s.Before = (long)Math.Max(0.0, Math.Round(e.Item2));
                }
            }
            oldest = int.MaxValue;
            for (int i = 0; state.numberOfBusinessesHistory != null && i < state.numberOfBusinessesHistory.Count; i++)
            {
                Tuple<int, int> e = state.numberOfBusinessesHistory[i];
                if (e != null && e.Item1 >= day - SeenRules.WeekDays && e.Item1 < day && e.Item1 < oldest)
                {
                    oldest = e.Item1;
                    s.ShopsBefore = Math.Max(0, e.Item2);
                }
            }
        }

        // A house in the Hamptons among the buildings they own.
        private static void Home(RivalData d, SeenSubject s)
        {
            for (int i = 0; d.ownedBuildings != null && i < d.ownedBuildings.Count && !s.Hamptons; i++)
            {
                BuildingRegistration r = d.ownedBuildings[i];
                Buildings.Building b = r != null ? r.BuildingCached : null;
                s.Hamptons = b != null && b.IsHamptonsHouse();
            }
        }

        // ---- the player, seen in turn

        /// <summary>True if the player runs a business of their own: only then does the column know them.</summary>
        public static bool PlayerKnown()
        {
            Books.Businesses(Regs);
            bool any = Regs.Count > 0;
            Regs.Clear();
            return any;
        }

        public static string PlayerName()
        {
            try
            {
                return PlayerHelper.CharacterData != null && !string.IsNullOrEmpty(PlayerHelper.CharacterData.name) ? PlayerHelper.CharacterData.name : string.Empty;
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }

        /// <summary>The car the player is driving now, by the game's own id of its type; null on foot, indoors or in none.</summary>
        public static string PlayerCar()
        {
            GameManager gm = InstanceBehavior<GameManager>.Instance;
            VehicleController car = gm != null && !BuildingManager.IsInsideBuilding ? gm.selectedVehicle : null;
            return car != null && car.controlledByPlayer && car.vehicleInstance != null ? car.vehicleInstance.vehicleTypeName : null;
        }

        /// <summary>The eating place the player is inside now: its name and district; false anywhere else.</summary>
        public static bool PlayerTable(out string name, out string district)
        {
            name = null;
            district = null;
            BuildingManager manager = BuildingManager.IsInsideBuilding ? InstanceBehavior<BuildingManager>.Instance : null;
            Buildings.Building b = manager != null ? manager.building : null;
            BuildingRegistration r = b != null && b.Address != null ? BuildingHelper.GetBuildingRegistration(b.Address) : null;
            if (r == null || !r.HasEstablishedBusiness || !Eats(r.businessTypeName))
            {
                return false;
            }
            name = r.BusinessName;
            district = b.Neighbourhood ?? string.Empty;
            return true;
        }

        // ---- words

        /// <summary>A car by the game's own id of its type, in the game's words.</summary>
        public static string CarName(string type) => string.IsNullOrEmpty(type) ? string.Empty : type.GetLocalization();

        /// <summary>A district by the game's own key, in the game's words.</summary>
        public static string DistrictName(string district)
        {
            return string.IsNullOrEmpty(district) ? string.Empty : LocalizorManager.IsLocalizedKey(district) ? district.GetLocalization() : district;
        }

        public static void Tell(NotificationType type, string key, Dictionary<string, string> data = null)
        {
            Notifications.Show(type, key, data, 10f, null, null, true, false);
        }

        // ---- their car on the road

        /// <summary>
        /// A rival's car passing: an idle car of the traffic pool of that very model is put on the lane that leads past a place, well back and out of view, and left to
        /// the game's own traffic, which drives it by like any other and takes it off again as it does every car. The game's private driver summons a car the same way
        /// (PrivateDriverHelpers.GetAiVehiclePrefab names the pool's car after the type). Nothing is held or steered, so there is nothing to give back. False where the
        /// pool has no idle car of the model, no plain lane leads there, or the traffic is another player's.
        /// </summary>
        public static bool PassBy(string type, Vector3 near, Vector3 player)
        {
            if (string.IsNullOrEmpty(type) || !TrafficTools.Ready)
            {
                return false;
            }
            GameObject prefab = PoolCar(type);
            Waypoint lane = prefab != null ? TrafficTools.ClosestWaypoint(near, 35f, PlainLane) : null;
            if (lane == null)
            {
                return false;
            }
            bool sent = false;
            try
            {
                float walked = TrafficTools.WalkBack(lane, 150f, 90, Lane);
                for (int i = 0; walked >= 70f && i < 4 && i < Lane.Count - 2 && !sent; i++)
                {
                    Vector3 at = Lane[i].position;
                    if (CitySpots.InView(at) || CitySpots.Flat(at - player) < 45f * 45f)
                    {
                        continue;
                    }
                    sent = TrafficTools.Load(prefab, Lane[i]) != null;
                }
            }
            finally
            {
                Lane.Clear();
            }
            return sent;
        }

        // The prefab of the pool's idle cars of a model: the pool's own object, found by the name the game gives a type's traffic car.
        private static GameObject PoolCar(string type)
        {
            int cut = type.IndexOf('_');
            string id = cut >= 0 && cut + 1 < type.Length ? type.Substring(cut + 1) : null;
            List<VehicleComponent> pool = id != null ? Manager.GetVehicleList() : null;
            for (int i = 0; pool != null && i < pool.Count; i++)
            {
                VehicleComponent car = pool[i];
                if (car != null && car.prefab != null && !car.gameObject.activeInHierarchy && string.Equals(car.prefab.name, id, StringComparison.OrdinalIgnoreCase))
                {
                    return car.prefab;
                }
            }
            return null;
        }

        private static bool IsPlainLane(Waypoint point)
        {
            return point.neighbors != null && point.neighbors.Count == 1 && point.prev != null && point.prev.Count == 1 && !point.temporaryDisabled && TrafficManager.IsNotIntersection(point);
        }
    }
}
