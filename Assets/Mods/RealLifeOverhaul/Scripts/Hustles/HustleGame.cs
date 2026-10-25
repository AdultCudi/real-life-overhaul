using System;
using System.Collections.Generic;
using Entities;
using Extensions;
using Helpers;
using RealLifeOverhaul.Economy.Dealership;
using Streets;
using UI.Guiders;
using UnityEngine;
using Vehicles.VehicleTypes;

namespace RealLifeOverhaul.Hustles
{
    /// <summary>
    /// Everything the Marketplace reads from or does to the game: the clock and money, the city's homes and doors, the
    /// player's skills and energy, cars bought and sold as the game's own vehicles, a van at a door, the repair shops.
    /// Nothing per frame; the city's buildings are walked only when a list is made or a test is pressed.
    /// </summary>
    internal static class HustleGame
    {
        private const string Residential = "ba:buildingtype_residential";
        private const string ElectronicsStore = "ba:businesstype_electronicsstore";
        private const string ApplianceStore = "ba:businesstype_appliancestore";
        private const string ItemSold = "ba:transaction_itemsold";
        private const string VehicleBought = "ba:transaction_vehiclebought";
        private const string SalaryCategory = "ba:transactioncategory_salaryincome";
        private const float EnergyFloor = 12f;              // work never takes the player below this (the game faints them at zero)
        private const float WorstEnergyMultiplier = 1.9f;   // the most the game multiplies energy spent by (low mood, hunger)

        private static GameInstance Save => SaveGameManager.Current;

        public static bool Ready => Save != null && !UI.Load.LoadScene.isLoading && ModState.Loaded;

        public static int Day => Save != null ? Save.Day : 0;

        public static int Hour => Save != null ? Save.Hour : 0;

        /// <summary>The hour as one number: day x 24 + hour.</summary>
        public static int Stamp => Day * 24 + Hour;

        public static int Cash => Save != null ? Mathf.FloorToInt(Mathf.Max(0f, Save.Money)) : 0;

        public static int Level => Prices.Level;

        /// <summary>The save's own name for the playthrough, which every machine of a session reads alike.</summary>
        public static string SeedText => Save == null ? "city" : !string.IsNullOrEmpty(Save.seed) ? Save.seed : Save.characterId ?? "city";

        // ---------------------------------------------------------------- the city's doors

        /// <summary>The city's homes a stranger may live in, ordered by street and number so that every machine has the same list.</summary>
        public static void Homes(List<HustlePlace> into)
        {
            into.Clear();
            CityManager city = InstanceBehavior<CityManager>.Instance;
            CityBuildingController[] all = GameDlc.Open(city);
            for (int i = 0; all != null && i < all.Length; i++)
            {
                CityBuildingController b = all[i];
                if (Door(b) && b.building.BuildingType == Residential)
                {
                    into.Add(new HustlePlace(b.building.StreetName, b.building.StreetNumber));
                }
            }
            into.Sort((a, b) =>
            {
                int byStreet = string.CompareOrdinal(a.Street, b.Street);
                return byStreet != 0 ? byStreet : a.Number.CompareTo(b.Number);
            });
        }

        // A building with a door on the street that nothing keeps apart: no DLC's, no private fence, no special service.
        private static bool Door(CityBuildingController b)
        {
            return b != null && b.building != null && b.entranceDoors != null && b.entranceDoors.Length > 0 && b.entranceDoors[0] != null
                   && b.building.SpecialService == null && !GameMembers.Fenced(b.building) && !GameDlc.Gated(b.building) && !b.building.IsHamptonsHouse();
        }

        public static CityBuildingController Building(string street, int number)
        {
            CityManager city = InstanceBehavior<CityManager>.Instance;
            CityBuildingController[] all = GameDlc.Open(city);
            for (int i = 0; all != null && i < all.Length; i++)
            {
                CityBuildingController b = all[i];
                if (b != null && b.building != null && b.building.StreetNumber == number && string.Equals(b.building.StreetName, street, StringComparison.Ordinal))
                {
                    return b;
                }
            }
            return null;
        }

        /// <summary>The door nearest the player of a building of the city, within <paramref name="within"/> metres, for the tests.</summary>
        public static bool NearestDoor(float within, bool homesOnly, out HustlePlace place, out Vector3 front)
        {
            place = default;
            front = default;
            PlayerController player = PlayerHelper.PlayerController;
            CityManager city = InstanceBehavior<CityManager>.Instance;
            CityBuildingController[] all = GameDlc.Open(city);
            if (player == null || all == null)
            {
                return false;
            }
            Vector3 from = player.transform.position;
            float best = within * within;
            CityBuildingController found = null;
            for (int i = 0; i < all.Length; i++)
            {
                CityBuildingController b = all[i];
                if (!Door(b) || (homesOnly && b.building.BuildingType != Residential) || b.entranceDoors[0].doorTransform == null)
                {
                    continue;
                }
                Vector3 d = b.entranceDoors[0].doorTransform.position - from;
                d.y = 0f;
                if (d.sqrMagnitude < best && (b.buildingRegistration == null || !GoingPublic.Foreign(b.buildingRegistration)))
                {
                    best = d.sqrMagnitude;
                    found = b;
                }
            }
            if (found == null || !JobSpots.Front(found, out front))
            {
                return false;
            }
            place = new HustlePlace(found.building.StreetName, found.building.StreetNumber);
            return true;
        }

        /// <summary>An address as the game writes it.</summary>
        public static string Name(string street, int number)
        {
            return string.IsNullOrEmpty(street) ? string.Empty : new Address(street, number).ToFormattedString();
        }

        /// <summary>The game's own destination marker on an address, as the player would set it on the map.</summary>
        public static void Guide(string street, int number)
        {
            if (!string.IsNullOrEmpty(street))
            {
                GuidersManager.SetGuiderTarget(new Address(street, number), DirectionGuiderType.Destination);
            }
        }

        // ---------------------------------------------------------------- the player

        public static PlayerController Player => PlayerHelper.PlayerController;

        public static bool OnFootOutside => Player != null && !BuildingManager.IsInsideBuilding && !PlayerHelper.IsUsingVehicle;

        /// <summary>True inside a building that is the player's own (a home, a business, a warehouse): a place to work on things.</summary>
        public static bool AtOwnPlace
        {
            get
            {
                if (!BuildingManager.IsInsideBuilding || PlayerHelper.IsUsingVehicle)
                {
                    return false;
                }
                BuildingManager manager = InstanceBehavior<BuildingManager>.Instance;
                return manager != null && manager.buildingRegistration != null && manager.buildingRegistration.RentedByPlayer && GoingPublic.Mine(manager.buildingRegistration);
            }
        }

        /// <summary>True while nothing else has the player: no menu, no activity, no time skip, no mission of the game's.</summary>
        public static bool Free
        {
            get
            {
                UI.UIs uis = InstanceBehavior<UI.UIs>.Instance;
                return uis != null && Save != null && !UI.Load.LoadScene.isLoading && !GameMembers.PlayerDead && !EnergyHelper.goingToHospital
                       && !uis.timeMachine.isRunning && !PlayerActivity.PlayerActivityUI.IsPanelOpen && !ThirdPersonCamera.IsUiOpen();
            }
        }

        /// <summary>The player's level of one of the game's skills, 0 to 100.</summary>
        public static int Skill(string skill)
        {
            EmployeeInstance self = PlayerHelper.GetPlayerEmployeeInstance();
            return self != null && skill != null ? Mathf.Clamp(Mathf.RoundToInt(self.GetSkillValue(skill)), 0, 100) : 0;
        }

        public static void Train(string skill, float points)
        {
            EmployeeInstance self = PlayerHelper.GetPlayerEmployeeInstance();
            if (self != null && skill != null && points > 0f)
            {
                self.IncreaseSkill(skill, points);
            }
        }

        /// <summary>Tiring, never dangerous: work stops short of the energy at which the game takes over.</summary>
        public static void Tire(int minutes)
        {
            if (Save == null || minutes <= 0)
            {
                return;
            }
            float amount = HustleCatalogue.EnergyAnHour * minutes / 60f;
            float room = (Save.Energy - EnergyFloor) / WorstEnergyMultiplier;
            amount = Mathf.Min(amount, room);
            if (amount > 0f)
            {
                EnergyHelper.SpentEnergyOnce(amount);
            }
        }

        // ---------------------------------------------------------------- money

        /// <summary>Money out under one of the mod's transactions; false and nothing taken when the cash does not cover it.</summary>
        public static bool Pay(int amount, string transaction)
        {
            return amount <= 0 || (Save != null && GameManager.ChangeMoneySafe(-amount, new TransactionInfo(transaction), null, null, false, true));
        }

        /// <summary>A car bought, as the game books one: under its name.</summary>
        public static bool PayForCar(int amount, string type)
        {
            if (amount <= 0)
            {
                return true;
            }
            var data = new Dictionary<string, string>(1) { { "vehicleName", CarName(type) } };
            return Save != null && GameManager.ChangeMoneySafe(-amount, new TransactionInfo(VehicleBought, data), null, null, false, true);
        }

        /// <summary>A thing sold, as the game books what it sells: what was sold, by name.</summary>
        public static void Sold(int amount, string what)
        {
            if (amount > 0 && Save != null)
            {
                var data = new Dictionary<string, string>(1) { { "itemSoldInfo", what } };
                GameManager.ChangeMoneySafe(amount, new TransactionInfo(ItemSold, data), null, null, true);
            }
        }

        /// <summary>A client's money for a visit, booked as income for work.</summary>
        public static void Earned(int amount, string transaction)
        {
            if (amount > 0 && Save != null)
            {
                GameManager.ChangeMoneySafe(amount, new TransactionInfo(transaction, SalaryCategory), null, null, true);
            }
        }

        // ---------------------------------------------------------------- cars

        /// <summary>The cars the market deals in and the game's colours, as the showroom reads them. Made once a city.</summary>
        public static void Cars(List<MarketModel> into, List<string> colours)
        {
            DealerGame.Models(into, colours);
        }

        public static string CarName(string type)
        {
            return DealerGame.CarName(type);
        }

        public static string ColourName(string colour)
        {
            return DealerGame.ColourName(colour);
        }

        public static VehicleInstance Vehicle(string id)
        {
            return DealerGame.Vehicle(id);
        }

        /// <summary>A car bought on the Marketplace, made the game's own way at a free stretch of kerb, worn as its condition says. Its id, or null.</summary>
        public static string MakeCar(Owned o, Vector3 spot, Quaternion heading)
        {
            VehicleType t = VehicleTypeHelper.GetVehicleType(o.Thing);
            if (t == null || Save == null)
            {
                return null;
            }
            var v = new VehicleInstance(o.Thing)
            {
                id = UuidHelper.GenerateBase64Uuid(),
                fuel = t.maxFuel * 0.35f,
                damage = Mathf.Clamp01((100 - o.Condition) / 100f),
                dirtiness = Mathf.Clamp01((100 - o.Condition) / 140f),   // a car that was let go is dirty with it
                zeroSellingPrice = o.Test                                  // a test's car is lent, not the player's to sell
            };
            if (!string.IsNullOrEmpty(o.Colour))
            {
                v.vehicleColorName = o.Colour;
            }
            VehicleController made = VehicleHelper.CreateAndSpawnVehicle(v, spot, heading);   // it adds the vehicle to the save
            if (made != null)
            {
                VehicleHelper.TeleportVehicleToGround(made, spot, heading);
                made.SavePosition();
            }
            return v.id;
        }

        /// <summary>A car's condition (percent, from the game's damage) and dirt (percent) as the game has them; false if the save has it no more.</summary>
        public static bool ReadCar(string id, out int condition, out int dirt)
        {
            VehicleInstance v = Vehicle(id);
            condition = v != null ? Mathf.Clamp(Mathf.RoundToInt((1f - v.damage) * 100f), 1, 100) : 0;
            dirt = v != null ? Mathf.Clamp(Mathf.RoundToInt(v.dirtiness * 100f), 0, 100) : 0;
            return v != null;
        }

        public static int ListPrice(string type)
        {
            return Mathf.RoundToInt(DealerGame.ListPrice(type));
        }

        /// <summary>True while the player sits in this car.</summary>
        public static bool Driving(string id)
        {
            GameManager gm = InstanceBehavior<GameManager>.Instance;
            return id != null && gm != null && gm.selectedVehicle != null && gm.selectedVehicle.vehicleInstance != null && gm.selectedVehicle.vehicleInstance.id == id;
        }

        public static void RemoveCar(string id)
        {
            DealerGame.RemoveCar(id);
        }

        /// <summary>A vehicle of the player's with room for a load (a van or a pickup) within reach of a place.</summary>
        public static bool VanNear(Vector3 at)
        {
            List<VehicleController> own = VehicleHelper.AllPlayerVehicles;
            float reach = HustleCatalogue.VanReach * HustleCatalogue.VanReach;
            for (int i = 0; i < own.Count; i++)
            {
                VehicleController v = own[i];
                if (v == null || !v.isActiveAndEnabled || v.vehicleInstance == null || v.vehicleInstance.VehicleType == null || !GoingPublic.MineVehicle(v))
                {
                    continue;
                }
                Vector3 d = v.transform.position - at;
                d.y = 0f;
                if (d.sqrMagnitude < reach && v.vehicleInstance.VehicleType.maxCargoCapacity >= HustleCatalogue.VanCargo)
                {
                    return true;
                }
            }
            return false;
        }

        // ---------------------------------------------------------------- the repair shops

        /// <summary>The nearest electronics store open now (an appliance store if the city has none): a repair shop. False if there is none.</summary>
        public static bool RepairShop(out HustlePlace place, out string name)
        {
            place = default;
            name = null;
            PlayerController player = Player;
            List<BuildingRegistration> all = Save != null ? Save.BuildingRegistrations : null;
            if (player == null || all == null)
            {
                return false;
            }
            Vector3 from = player.transform.position;
            for (int pass = 0; pass < 2 && name == null; pass++)
            {
                string wanted = pass == 0 ? ElectronicsStore : ApplianceStore;
                float best = float.MaxValue;
                for (int i = 0; i < all.Count; i++)
                {
                    BuildingRegistration reg = all[i];
                    if (reg == null || reg.businessTypeName != wanted || !reg.HasEstablishedBusiness || !reg.HasValidAddress || reg.temporarilyClosed || !GameRegistrations.Found(reg)
                        || !BusinessHelper.IsBusinessOpen(reg))
                    {
                        continue;
                    }
                    CityBuildingController b = Building(reg.StreetName, reg.StreetNumber);
                    if (b == null || !Door(b))
                    {
                        continue;
                    }
                    float d = (b.transform.position - from).sqrMagnitude;
                    if (d < best)
                    {
                        best = d;
                        place = new HustlePlace(reg.StreetName, reg.StreetNumber);
                        name = !string.IsNullOrEmpty(reg.BusinessName) ? reg.BusinessName : Name(reg.StreetName, reg.StreetNumber);
                    }
                }
            }
            return name != null;
        }

        /// <summary>True while a repair shop at this address is open for business.</summary>
        public static bool ShopOpen(string street, int number)
        {
            List<BuildingRegistration> all = Save != null ? Save.BuildingRegistrations : null;
            for (int i = 0; all != null && i < all.Count; i++)
            {
                BuildingRegistration reg = all[i];
                if (reg != null && reg.StreetNumber == number && string.Equals(reg.StreetName, street, StringComparison.Ordinal))
                {
                    return reg.HasEstablishedBusiness && !reg.temporarilyClosed && BusinessHelper.IsBusinessOpen(reg);
                }
            }
            return false;
        }
    }
}
