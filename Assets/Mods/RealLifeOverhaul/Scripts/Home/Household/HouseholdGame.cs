using System;
using System.Collections.Generic;
using BAModAPI;
using BigAmbitions.Tags;
using Buildings.BuildingTypes.Shared.Dirtiness;
using Helpers;
using RealLifeOverhaul.Roommates;
using UnityEngine;

namespace RealLifeOverhaul.Household
{
    /// <summary>A car of the player's at home: the save's own record, and the car itself while the city has it out.</summary>
    public struct HomeCar
    {
        public VehicleInstance Car;
        public VehicleController Live;   // null while the car is not spawned
    }

    /// <summary>
    /// Where household staff touch the game: the clock, the player's homes, money, the city's going wage, a home's tidiness
    /// and the fridge's portions (home life), and the cars at a home. A house in the Hamptons is a home like any other here,
    /// though the game builds it differently: its inside is the city building itself, on a plot of its own, not an interior
    /// scene loaded at a door, so its floor and its plot are read through the game's Hamptons paths. Every call is made on an
    /// hour's round, a page of the call, a look in a staffed home or a test, never per frame.
    /// </summary>
    public static class HouseholdGame
    {
        private const string CleaningSkill = "ba:skill_cleaning";   // the game's skill of its own cleaners: the measure of domestic work
        private const float GateReach = 10f;                        // metres outside a Hamptons plot within which a car left at its gate is at home

        private static CityManager _doorCity;
        private static string _doorStreet;
        private static int _doorNumber;
        private static Vector3 _door;
        private static bool _doorKnown;
        private static SizeOrientedBounds _plot;   // a Hamptons house's plot, the game's own bounds of it; null for any other home

        public static IModLogger Logger;

        public static int Day => TimeHelper.CurrentDay;
        public static int Hour => TimeHelper.CurrentHour;
        public static int Stamp => TimeHelper.CurrentDay * 24 + TimeHelper.CurrentHour;
        public static int Weekday(int day) => Planner.PlannerTime.Weekday(day);

        public static BuildingRegistration HomeAt(string street, int number) => RoommateGame.HomeAt(street, number);

        /// <summary>A home of the player's for the staff: the one they stand in, else the first they have. Null with none.</summary>
        public static BuildingRegistration AnyHome() => RoommateGame.AnyHome();

        public static bool Inside(string street, int number) => RoommateGame.Inside(street, number);

        /// <summary>
        /// The home of the player's they stand in, false when they are not in one. On a Hamptons house's plot the game counts
        /// the player inside the house (HamptonsHouse.OnEnterPlot enters it), garden and drive included.
        /// </summary>
        public static bool InsideHome(out string street, out int number)
        {
            street = null;
            number = 0;
            if (!BuildingManager.IsInsideBuilding)
            {
                return false;
            }
            var manager = InstanceBehavior<BuildingManager>.Instance;
            BuildingRegistration r = manager != null ? manager.buildingRegistration : null;
            if (r == null || !GoingPublic.Mine(r) || !RoommateGame.IsHome(r))
            {
                return false;
            }
            street = r.StreetName;
            number = r.StreetNumber;
            return !string.IsNullOrEmpty(street);
        }

        /// <summary>Fills the list with every home of the player's, in the save's own order (the same on every machine).</summary>
        public static void Homes(List<BuildingRegistration> into)
        {
            into.Clear();
            List<BuildingRegistration> all = SaveGameManager.Current != null ? SaveGameManager.Current.BuildingRegistrations : null;
            for (int i = 0; all != null && i < all.Count; i++)
            {
                if (all[i] != null && GoingPublic.Mine(all[i]) && RoommateGame.IsHome(all[i]) && !string.IsNullOrEmpty(all[i].StreetName))
                {
                    into.Add(all[i]);
                }
            }
        }

        /// <summary>A home's address as the game writes it ("12 Legacy Avenue"), in the player's language.</summary>
        public static string Where(string street, int number)
        {
            if (string.IsNullOrEmpty(street))
            {
                return string.Empty;
            }
            try
            {
                return Streets.AddressHelper.ToFormattedString(new Address(street, number));
            }
            catch (Exception)
            {
                return number + " " + street;
            }
        }

        public static string Where(BuildingRegistration home) => home != null ? Where(home.StreetName, home.StreetNumber) : string.Empty;

        /// <summary>True for a house in the Hamptons (the game's size t), whose inside is the city building itself.</summary>
        public static bool IsHamptons(BuildingRegistration home)
        {
            try
            {
                Buildings.Building b = GameRegistrations.BuildingOf(home);
                return b != null && b.IsHamptonsHouse();
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>Money from the player through the game's own call. False, with the game's own notice, if they have not got it.</summary>
        public static bool Pay(int amount, string transaction) => RoommateGame.Pay(amount, transaction);

        /// <summary>The city's going rate for domestic work now, cents an hour.</summary>
        public static int GoingRate()
        {
            int formula = 0;
            try
            {
                formula = Mathf.RoundToInt(EmployeeHelper.CalculateHourlyWageForSkill(CleaningSkill, HouseholdRules.GoingSkill) * 100f);
            }
            catch (Exception e)
            {
                Logger?.Warn("Household staff: the game's wage for a cleaner could not be read (" + e.GetType().Name + "); its usual figure is used.");
            }
            Func<int> market = HouseholdBridges.WageMarket;
            return HouseholdRules.Going(formula, market != null ? market() : Prices.Level);
        }

        public static int PriceLevel => Prices.Level;

        /// <summary>True while home cooking can take portions put by: home life and its routines on, its state read.</summary>
        public static bool CanStore => Settings.HomeLife && Settings.HomeRoutines && HomeState.Loaded;

        public static int Leftovers => HomeState.Leftovers < 0 ? 0 : HomeState.Leftovers;

        /// <summary>Puts portions by in the fridge, as home cooking's leftovers; returns how many went in.</summary>
        public static int PutBy(int portions, int dishIndex)
        {
            Dish dish = HouseholdRules.DishAt(dishIndex);
            return CanStore && dish != null ? HomeState.PutBy(portions, dish.Number, HouseholdRules.MealFill) : 0;
        }

        /// <summary>The home tidier by so many points, on the game's own floor where it has one; returns that home's tidiness after (0..100).</summary>
        public static float Clean(BuildingRegistration home, float points)
        {
            HamptonsFloor(home);
            HomeTidy.Change(home, points);
            return HomeTidy.ReadOf(home);
        }

        /// <summary>The tidiness of one home in particular (0..100): its own floor where it has one, else home life's figure.</summary>
        public static float Tidiness(BuildingRegistration home) => HomeTidy.ReadOf(home);

        /// <summary>The number of floor cells the game keeps the home's dirt on; 0 before the game has laid them out.</summary>
        public static int FloorCells(BuildingRegistration home) => home != null && home.dirtSpots != null ? home.dirtSpots.Count : 0;

        /// <summary>
        /// A house in the Hamptons the player has not walked onto since it became theirs has no floor cells yet (the game's
        /// rent clears them and lays them out on entering), so a cleaning would land on home life's one figure instead of the
        /// house. Its floor is laid out here as the game does on entering: through the game's own helper, which for a Hamptons
        /// house reads the city building (CityManager.FindCityBuildingController), always there, rather than an interior
        /// scene that is loaded only at a door. Any other home waits for the player's first visit, as the game does.
        /// </summary>
        public static void HamptonsFloor(BuildingRegistration home)
        {
            if (home == null || (home.dirtSpots != null && home.dirtSpots.Count > 0) || !IsHamptons(home))
            {
                return;
            }
            try
            {
                List<Entities.DirtSpot> cells = BuildingCleanlinessHelper.GetDirtSpotsForBuilding(GameRegistrations.BuildingOf(home));
                if (cells != null && cells.Count > 0)
                {
                    home.dirtSpots = cells;
                    Logger?.Info("Household staff: the floor of the house at " + Where(home) + " was laid out through the game's Hamptons path (" + cells.Count + " cells).");
                }
            }
            catch (Exception e)
            {
                Logger?.Warn("Household staff: the floor of the house at " + Where(home) + " could not be read (" + e.GetType().Name + "); its cleaning counts on home life's figure.");
            }
        }

        /// <summary>Fills the list with the player's cars at this home: in its own garage, or parked within reach of its door.</summary>
        public static void CarsAt(BuildingRegistration home, List<HomeCar> into)
        {
            into.Clear();
            GameInstance save = SaveGameManager.Current;
            List<VehicleInstance> cars = save != null ? save.VehicleInstances : null;
            if (home == null || cars == null)
            {
                return;
            }
            bool door = Door(home, out Vector3 at);
            GameManager game = InstanceBehavior<GameManager>.Instance;
            VehicleController driven = game != null ? game.selectedVehicle : null;
            Func<VehicleInstance, bool> notOurs = HouseholdBridges.NotOurs;
            Address address = home.Address;
            float reach = HouseholdRules.HomeReach * HouseholdRules.HomeReach;
            for (int i = 0; i < cars.Count; i++)
            {
                VehicleInstance car = cars[i];
                if (car == null || car.VehicleType == null || !car.VehicleType.IsMotorVehicle || car.VehicleType.HasTag(TagRef.Vehicletag.ishandvehicle)
                    || (notOurs != null && notOurs(car)))
                {
                    continue;
                }
                VehicleController live = VehicleHelper.GetVehicleController(car);
                if (live != null && driven != null && ReferenceEquals(live, driven))
                {
                    continue;   // the car the player is in is the repair bay's, not the staff's
                }
                bool atHome = car.IsAtAddress(address);
                if (!atHome && door)
                {
                    Vector3 p = live != null ? live.transform.position : new Vector3(car.position.x, car.position.y, car.position.z);
                    atHome = _plot != null ? OnPlot(_plot, p) : (p - at).sqrMagnitude <= reach;
                }
                if (atHome)
                {
                    into.Add(new HomeCar { Car = car, Live = live });
                }
            }
        }

        // The home's building in the city, looked up once per home and city through the game's own table of them; for a
        // Hamptons house its plot too, since a plot can reach further from the building's origin than any door's reach.
        private static bool Door(BuildingRegistration home, out Vector3 at)
        {
            CityManager city = InstanceBehavior<CityManager>.Instance;
            if (!ReferenceEquals(city, _doorCity) || home.StreetName != _doorStreet || home.StreetNumber != _doorNumber)
            {
                _doorCity = city;
                _doorStreet = home.StreetName;
                _doorNumber = home.StreetNumber;
                _doorKnown = false;
                _plot = null;
                CityBuildingController c = city != null ? city.FindCityBuildingController(home.Address) : null;
                if (c != null)
                {
                    _door = c.transform.position;
                    _doorKnown = true;
                    try
                    {
                        _plot = PlotOf(c);
                    }
                    catch (Exception)
                    {
                        _plot = null;   // a game without the Hamptons' plot: the door's reach is used
                    }
                }
            }
            at = _door;
            return _doorKnown;
        }

        // A method of its own, so a game without the Hamptons' plot fails at its call alone (Mono compiles a method whole).
        private static SizeOrientedBounds PlotOf(CityBuildingController c)
        {
            return c is CityHamptonsHouseController h && h.hamptonsHouse != null ? h.hamptonsHouse.plotBounds : null;
        }

        private static bool OnPlot(SizeOrientedBounds plot, Vector3 p)
        {
            if (plot.Contains(p))
            {
                return true;
            }
            Vector3 edge = plot.ClosestPointOnBounds(p);
            return CitySpots.Flat(p - edge) <= GateReach * GateReach;
        }

        /// <summary>How the cars at a home are found, for the check lines: its plot for a Hamptons house, else the reach of its door.</summary>
        public static string CarRule(BuildingRegistration home)
        {
            if (home == null || !Door(home, out _))
            {
                return "its garage only (the building was not found in the city)";
            }
            return _plot != null ? "its garage and its plot, or within " + GateReach + " m of the plot" : "its garage, or within " + HouseholdRules.HomeReach + " m of its door";
        }

        public static bool Dirty(HomeCar c) => c.Car.dirtiness > HouseholdRules.DirtyFrom;

        public static int DirtPercent(HomeCar c) => Mathf.RoundToInt(c.Car.dirtiness * 100f);

        /// <summary>What the game's own repair of the car comes to (its repair bay's price); 0 for none.</summary>
        public static int RepairCost(HomeCar c) => Mathf.RoundToInt(Mathf.Max(0f, c.Car.CalculateRepairCost()));

        /// <summary>Car ownership's service due on the car, at the bay's prices; 0 for none or without car ownership.</summary>
        public static int ServiceCost(HomeCar c)
        {
            Func<VehicleInstance, bool, float> service = HouseholdBridges.Service;
            return service != null ? Mathf.RoundToInt(Mathf.Max(0f, service(c.Car, false))) : 0;
        }

        /// <summary>Washed clean, as the game's wash leaves a car.</summary>
        public static void Wash(HomeCar c)
        {
            if (c.Live != null)
            {
                c.Live.SetDirtiness(0f);
            }
            else
            {
                c.Car.dirtiness = 0f;
            }
        }

        /// <summary>Repaired as the game's repair bay repairs: no damage and no dents.</summary>
        public static void Repair(HomeCar c)
        {
            if (c.Live != null)
            {
                c.Live.Repair();
            }
            else
            {
                c.Car.damage = 0f;
                c.Car.deformations.Clear();
            }
        }

        /// <summary>Car ownership's service done on the car; what it came to.</summary>
        public static int Service(HomeCar c)
        {
            Func<VehicleInstance, bool, float> service = HouseholdBridges.Service;
            return service != null ? Mathf.RoundToInt(Mathf.Max(0f, service(c.Car, true))) : 0;
        }

        public static string CarName(HomeCar c)
        {
            try
            {
                return c.Car.VehicleType != null ? c.Car.VehicleType.vehicleTypeName : c.Car.vehicleTypeName;
            }
            catch (Exception)
            {
                return c.Car.vehicleTypeName;
            }
        }

        /// <summary>True when a notice may go up now: not in a loading screen, a conversation or a time skip.</summary>
        public static bool Quiet => LifeEvents.IsSafeForEvent();

        /// <summary>The person a member is, for the character at home and the contact's picture.</summary>
        public static SocialPerson Person(StaffMember m)
        {
            return new SocialPerson { Index = -1, Origin = SocialOrigin.Neighbour, Name = m.Name, Female = m.Female, Age = m.Age, Seed = m.Seed != 0 ? m.Seed : 1 };
        }
    }
}
