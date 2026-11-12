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
using UI.Dialog;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.RivalsSeen
{
    /// <summary>
    /// Everything the rivals seen around the city read from the game, and the one thing they do to it (a car of the traffic pool put on a lane): the game's own rival
    /// owners with their weekly income as its Rivals app shows it and their businesses, the game's cars by price, the city's eating places and the buildings of
    /// the rivals' own premises (fed to the game-free <see cref="SeenTable"/> once a game day), the doors to stand by, the player's car and table, the game's
    /// dialogue and notifications. Nothing is read per frame. Nothing of the game's people, businesses or money is changed.
    /// </summary>
    public sealed class SeenGame
    {
        public const string Hamptons = "ba:neighborhood_thehamptons";

        // (before City: a static field is made in the order it is written, and the city's table takes this one)
        private static readonly Func<object, int, int, bool> Opens = (building, day, hour) => building is CityBuildingController c && OpenAt(c.buildingRegistration, day, hour);

        public static readonly SeenGame City = new SeenGame();
        public static IModLogger Logger;

        private static readonly Comparison<MarketModel> ByPrice = (a, b) => a.Price != b.Price ? a.Price.CompareTo(b.Price) : string.CompareOrdinal(a.Type, b.Type);
        private static readonly Comparison<RivalData> ById = (a, b) => string.CompareOrdinal(a != null ? a.id : null, b != null ? b.id : null);
        private static readonly Predicate<Waypoint> PlainLane = IsPlainLane;

        /// <summary>The city as the rivals need it, made once a game day (game-free: the checks drive the same table).</summary>
        public readonly SeenTable Table = new SeenTable { Open = Opens };

        private readonly List<MarketModel> _models = new List<MarketModel>(16);
        private readonly List<string> _colours = new List<string>(8);
        private static readonly List<BuildingRegistration> Regs = new List<BuildingRegistration>(16);
        private static readonly List<Waypoint> Lane = new List<Waypoint>(96);
        private static bool _incomeFailed;

        public static int Day => SaveGameManager.Current != null ? SaveGameManager.Current.Day : 0;
        public static int Hour => TimeHelper.CurrentHour;
        public static int Level => Prices.Level;

        // ---- the city's table

        /// <summary>The address of a registration as the rivals' sources write an office: its street and number.</summary>
        public static string KeyOf(BuildingRegistration r) => (r.StreetName ?? string.Empty) + "#" + r.StreetNumber;

        /// <summary>True once the city's list of buildings is there to read (Core/GameDlc keeps its own copy: asking twice costs nothing).</summary>
        public static bool CityReady()
        {
            CityBuildingController[] all = GameDlc.Open(InstanceBehavior<CityManager>.Instance);
            return all != null && all.Length > 0;
        }

        /// <summary>
        /// Once a game day, and again when the day's rivals change: the game's cars by price, the city's eating places, and the buildings at the day's rivals'
        /// offices, in one pass over the city's buildings (never one a DLC keeps: Core/GameDlc). Only an eating place or a building at one of those offices is
        /// kept, and no string is made for the others. False while the city's list of buildings is not there yet: nothing is kept, and the day's pass waits.
        /// </summary>
        public bool Refresh(IList<SeenSubject> subjects)
        {
            CityBuildingController[] all = GameDlc.Open(InstanceBehavior<CityManager>.Instance);
            if (all == null || all.Length == 0)
            {
                return false;
            }
            Table.Begin(subjects, Hamptons);
            ReadCars();
            for (int i = 0; i < all.Length; i++)
            {
                CityBuildingController c = all[i];
                BuildingRegistration r = c != null ? c.buildingRegistration : null;
                if (r == null || c.building == null || !r.HasEstablishedBusiness || string.IsNullOrEmpty(r.businessTypeName))
                {
                    continue;
                }
                bool eats = SeenTable.Eats(r.businessTypeName);
                int office = Table.OfficeAt(r.StreetName, r.StreetNumber);
                if (!eats && office < 0)
                {
                    continue;
                }
                Vector3 at = c.transform.position;
                Table.SetOffice(office, c, at.x, at.z);
                if (eats)
                {
                    Table.AddEating(c.building.Neighbourhood, r.StreetName, r.StreetNumber, r.BusinessName, Mathf.Max(0, Mathf.RoundToInt(r.RentPerDay)), at.x, at.z, c);
                }
            }
            Table.Done();
            return true;
        }

        /// <summary>True where the city has the Hamptons (Core/CityCheck): without them nobody weekends there.</summary>
        public static bool HasCoast() => CityCheck.Here(Hamptons);

        /// <summary>The table is forgotten: the next day's pass makes it again (a city unloading, the feature switched off).</summary>
        public void Forget()
        {
            Table.Forget();
        }

        // The game's cars a showroom deals in (Economy/Dealer reads them: no trucks, nothing of another mod's), the cheapest first.
        private void ReadCars()
        {
            try
            {
                DealerGame.Models(_models, _colours);
                _models.Sort(ByPrice);
                for (int i = 0; i < _models.Count; i++)
                {
                    Table.AddCar(_models[i].Type);
                }
            }
            catch (Exception e)
            {
                Logger?.Warn("Rivals seen: the game's cars could not be read, so nobody has a car on record this session: " + e.Message);
            }
            _models.Clear();
            _colours.Clear();
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

        /// <summary>True if an eating place is open now for a reason no timetable knows (a fire, an inspection): the game's own test, asked as a rival is put out.</summary>
        public static bool OpenNow(object building)
        {
            return building is CityBuildingController c && BusinessHelper.IsBusinessOpen(c.buildingRegistration);
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
                var s = new SeenSubject
                {
                    Key = "g" + d.id, Id = d.id, Name = d.rivalName, Gone = RivalsHelper.IsRivalDefeated(d.id), Shops = d.ownedBusinesses != null ? d.ownedBusinesses.Count : 0,
                    ShopsBefore = -1
                };
                Takings(d, s);
                History(d.id, day, s);
                Home(d, s);
                into.Add(s);
            }
        }

        // Their weekly income as the game's Rivals app shows it (RivalData.WeeklyIncome: the last seven days of their shops and offices, the figure the game's
        // weekly history keeps too, so the first mark sets like against like), and the busiest of those shops and offices, or of all they run with none: its
        // name is their firm, its address their office, its district theirs.
        private static void Takings(RivalData d, SeenSubject s)
        {
            List<BuildingRegistration> shops = d.ownedRetailOfficeBusinesses;
            double weekly = 0.0;
            try
            {
                weekly = shops != null ? d.WeeklyIncome : 0f;
            }
            catch (Exception e)
            {
                weekly = Week(shops);   // the same sum, leaving out a business with no days on record
                if (!_incomeFailed)
                {
                    _incomeFailed = true;
                    Logger?.Warn("Rivals seen: the game's weekly income of a rival could not be read (" + e.GetType().Name + ": " + e.Message
                                 + "); their shops' and offices' last seven days are summed instead.");
                }
            }
            s.Figure = (long)Math.Max(0.0, Math.Round(weekly));
            List<BuildingRegistration> from = shops != null && shops.Count > 0 ? shops : d.ownedBusinesses;
            BuildingRegistration busiest = null;
            double best = -1.0;
            for (int i = 0; from != null && i < from.Count; i++)
            {
                BuildingRegistration r = from[i];
                if (r == null || string.IsNullOrEmpty(r.BusinessName))
                {
                    continue;
                }
                double week = Days(r);
                if (busiest == null || week > best || (week == best && SeenTable.AddressOrder(r.StreetName, r.StreetNumber, busiest.StreetName, busiest.StreetNumber) < 0))
                {
                    best = week;
                    busiest = r;
                }
            }
            if (busiest != null)
            {
                s.Office = KeyOf(busiest);
                s.Firm = busiest.BusinessName;
                s.District = GameRegistrations.District(busiest) ?? string.Empty;
            }
        }

        // A business's last seven days of takings, as the game sums them.
        private static double Days(BuildingRegistration r)
        {
            double week = 0.0;
            for (int k = r.dailyIncomes != null ? Math.Max(0, r.dailyIncomes.Count - SeenRules.WeekDays) : 0; r.dailyIncomes != null && k < r.dailyIncomes.Count; k++)
            {
                week += r.dailyIncomes[k];
            }
            return week;
        }

        private static double Week(List<BuildingRegistration> shops)
        {
            double sum = 0.0;
            for (int i = 0; shops != null && i < shops.Count; i++)
            {
                sum += shops[i] != null ? Days(shops[i]) : 0.0;
            }
            return sum;
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
                Buildings.Building b = GameRegistrations.BuildingOf(r);
                s.Hamptons = b != null && b.IsHamptonsHouse();
            }
        }

        /// <summary>
        /// Whether each of the day's rivals is still there, asked again as the hour turns, before anything about them goes out: an owner defeated or taken over
        /// since the morning (the game's own test, a lookup by id), a tycoon taken over (through their bridge). Somebody gone stays gone for the day. Returns how
        /// many went.
        /// </summary>
        public static int Still(IList<SeenSubject> subjects)
        {
            int went = 0;
            for (int i = 0; subjects != null && i < subjects.Count; i++)
            {
                SeenSubject s = subjects[i];
                if (s == null || s.Gone)
                {
                    continue;
                }
                bool gone = s.Tycoon ? SeenBridges.TycoonHere != null && !SeenBridges.TycoonHere(s.Index) : !string.IsNullOrEmpty(s.Id) && RivalsHelper.IsRivalDefeated(s.Id);
                if (gone)
                {
                    s.Gone = true;
                    went++;
                }
            }
            return went;
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

        /// <summary>The eating place the player is inside now, if it is not their own: its name and district; false anywhere else.</summary>
        public static bool PlayerTable(out string name, out string district)
        {
            name = null;
            district = null;
            BuildingManager manager = BuildingManager.IsInsideBuilding ? InstanceBehavior<BuildingManager>.Instance : null;
            Buildings.Building b = manager != null ? manager.building : null;
            BuildingRegistration r = b != null && b.Address != null ? BuildingHelper.GetBuildingRegistration(b.Address) : null;
            if (r == null || !r.HasEstablishedBusiness || !SeenTable.Eats(r.businessTypeName) || GoingPublic.Mine(r))
            {
                return false;   // (at work in their own restaurant the player is not out to eat)
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

        // ---- the talk

        /// <summary>True while the game's dialogue panel is open with this person, as the game asks of its own staff (SpecialEmployeeController).</summary>
        public static bool TalkingWith(ThirdPersonCharacter who)
        {
            DialogUI ui = Dialogue();
            return who != null && ui != null && ui.isPanelOpen && ReferenceEquals(ui.npcInDialog, who);
        }

        /// <summary>
        /// Our greeting with this person is ended, if it is open, before they are let go: the game's panel then never holds somebody switched off (it would bear it,
        /// as it only compares its person, but nothing of the mod leaves it to). Another feature's dialogue is never closed from here.
        /// </summary>
        public static void EndTalk(ThirdPersonCharacter who)
        {
            if (TalkingWith(who) && Dialogue().dialog is SeenDialog)
            {
                Dialogue().CancelDialog();
            }
        }

        private static DialogUI Dialogue()
        {
            UI.UIs uis = InstanceBehavior<UI.UIs>.Instance;
            return uis != null && uis.playerHUD != null ? uis.playerHUD.dialogUI : null;
        }

        // ---- a place to stand by a door

        /// <summary>
        /// The spot to stand by a building's door, if any is free: <see cref="SeenRules.DoorSpot"/>'s spots along the kerb either side of the pavement point
        /// before the door, in turn, the first on the pavement and clear of everything (CitySpots.Stand, whose door keep-out the spots are far enough along to
        /// pass); for a rival put out unseen, out of view too. A spot refused is no news: the living city's counts of refusals are left as they were. False with none.
        /// </summary>
        public static bool DoorStand(object building, bool test, out Vector3 spot, out Vector3 front, out Vector3 face)
        {
            spot = front = face = default;
            var venue = building as CityBuildingController;
            if (venue == null || !JobSpots.Front(venue, out front) || !CitySpots.Along(front, out Vector3 along) || !JobSpots.DoorOf(venue, out Vector3 door))
            {
                return false;
            }
            Vector3 outward = front - door;
            outward.y = 0f;
            face = outward.sqrMagnitude > 0.01f ? outward.normalized : along;
            int off = CitySpots.OffPavement, blocked = CitySpots.Blocked, near = CitySpots.NearDoor;
            try
            {
                for (int attempt = 0; SeenRules.DoorSpot(attempt, out int side, out float metres); attempt++)
                {
                    if (CitySpots.Stand(front + along * (side * metres), 0.35f, out spot) && SeenRules.MayStand(CitySpots.InView(spot), test))
                    {
                        return true;
                    }
                }
                return false;
            }
            finally
            {
                CitySpots.OffPavement = off;
                CitySpots.Blocked = blocked;
                CitySpots.NearDoor = near;
            }
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

        // The prefab of the pool's idle cars of a model: the pool's own object, found by the name the game gives a type's car, the part of the type's id between
        // its first and second '_' (StringIdExtensions.GetIdWithoutType, as the private driver loads "Vehicles/" and it).
        private static GameObject PoolCar(string type)
        {
            if (!SeenRules.CarId(type, out int from, out int length))
            {
                return null;
            }
            List<VehicleComponent> pool = Manager.GetVehicleList();
            for (int i = 0; pool != null && i < pool.Count; i++)
            {
                VehicleComponent car = pool[i];
                if (car == null || car.prefab == null || car.gameObject.activeInHierarchy)
                {
                    continue;
                }
                string name = car.prefab.name;
                if (name.Length == length && string.Compare(name, 0, type, from, length, StringComparison.OrdinalIgnoreCase) == 0)
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
