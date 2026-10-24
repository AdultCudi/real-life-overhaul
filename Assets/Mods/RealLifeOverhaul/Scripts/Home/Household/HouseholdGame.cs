using System;
using System.Collections.Generic;
using BAModAPI;
using BigAmbitions.Tags;
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
    /// Where household staff touch the game: the clock, the home, money, the city's going wage, the home's tidiness and the
    /// fridge's portions (home life), and the cars at home. Every call is made on an hour's round, a page of the call or a
    /// test, never per frame.
    /// </summary>
    public static class HouseholdGame
    {
        private const string CleaningSkill = "ba:skill_cleaning";   // the game's skill of its own cleaners: the measure of domestic work

        private static CityManager _doorCity;
        private static string _doorStreet;
        private static int _doorNumber;
        private static Vector3 _door;
        private static bool _doorKnown;

        public static IModLogger Logger;

        public static int Day => TimeHelper.CurrentDay;
        public static int Hour => TimeHelper.CurrentHour;
        public static int Stamp => TimeHelper.CurrentDay * 24 + TimeHelper.CurrentHour;
        public static int Weekday(int day) => Planner.PlannerTime.Weekday(day);

        public static BuildingRegistration HomeAt(string street, int number) => RoommateGame.HomeAt(street, number);

        /// <summary>A home of the player's for the staff: the one they stand in, else the first they have. Null with none.</summary>
        public static BuildingRegistration AnyHome() => RoommateGame.AnyHome();

        public static bool Inside(string street, int number) => RoommateGame.Inside(street, number);

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

        /// <summary>The home tidier by so many points, on the game's own floor where it has one; returns its tidiness after (0..100).</summary>
        public static float Clean(BuildingRegistration home, float points)
        {
            HomeTidy.Change(home, points);
            return HomeTidy.Read();
        }

        public static float Tidiness() => HomeTidy.Read();

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
                    atHome = (p - at).sqrMagnitude <= reach;
                }
                if (atHome)
                {
                    into.Add(new HomeCar { Car = car, Live = live });
                }
            }
        }

        // The home's building in the city: looked up once per home and city, from the city's list of buildings.
        private static bool Door(BuildingRegistration home, out Vector3 at)
        {
            CityManager city = InstanceBehavior<CityManager>.Instance;
            if (!ReferenceEquals(city, _doorCity) || home.StreetName != _doorStreet || home.StreetNumber != _doorNumber)
            {
                _doorCity = city;
                _doorStreet = home.StreetName;
                _doorNumber = home.StreetNumber;
                _doorKnown = false;
                CityBuildingController[] all = city != null ? city.cityBuildingControllers : null;
                for (int i = 0; all != null && i < all.Length; i++)
                {
                    Buildings.Building b = all[i] != null ? all[i].building : null;
                    if (b != null && b.StreetName == home.StreetName && b.StreetNumber == home.StreetNumber)
                    {
                        _door = all[i].transform.position;
                        _doorKnown = true;
                        break;
                    }
                }
            }
            at = _door;
            return _doorKnown;
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
