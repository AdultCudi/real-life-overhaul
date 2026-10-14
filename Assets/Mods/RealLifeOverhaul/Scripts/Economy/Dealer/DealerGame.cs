using System;
using System.Collections.Generic;
using AI.Customers.CustomerEntries;
using BAModAPI;
using Data.VehicleColors;
using Entities;
using Extensions;
using Helpers;
using Localizor;
using UnityEngine;
using Vehicles.VehicleTypes;

namespace RealLifeOverhaul.Economy.Dealership
{
    /// <summary>
    /// Everything the dealership reads from or does to the game, in one place.
    ///
    /// READ: the player's dealerships (the business type's name on a building of the player's, Going Public's other
    /// players' left out: GoingPublic.Mine); the cars the market can offer (the game's vehicle types with a price, a tank
    /// and a body of their own, by the mod's car catalogue's kinds, trucks and the game's work vans left out, and other
    /// mods' cars left out because the game spawns them again on entering a building without their shaders); the game's
    /// car colours; for an hour, whether the business is open (BusinessHelper.IsBusinessOpen, a temporary closure, and the
    /// business pressure's stopping its footfall: BusinessGame.HasFootfall), the customers the game's own count gives a
    /// shop of the building (CustomerEntriesCalculator: its capacity) at its promotion (BuildingRegistration.promotion,
    /// which the game, the company's campaigns, brands, the city, Flare and Standing write), the building's customer
    /// satisfaction, and the salespeople on shift there now (EmployeeInstance.IsWorking, customer service as their main
    /// skill, not off sick) with their skill.
    ///
    /// DONE: a car of the stock is a VehicleInstance of the game's, at the dealership's address and on a place of its lot
    /// (SetStreetData and its position), so the game itself spawns it there whenever the player walks in
    /// (GameManager.SpawnPlayerVehicles) and keeps it in the save. While the player is inside, the cars are held still on
    /// their places (their rigid bodies frozen, their door closed to the player, their map pin hidden). A car sold is
    /// deleted the game's own way (VehicleHelper.Delete). Money is the game's own transactions at the dealership's
    /// address: a car sold as the game books a vehicle sold, a car bought as it books one bought, the rest under the
    /// mod's own types (DealerLines); costs are marked deductible as the game marks a business's, so its tax deducts them.
    ///
    /// Nothing here runs per frame: on the game's hour, once a day, on entering the dealership, and on the player's word.
    /// </summary>
    internal static class DealerGame
    {
        private const string CustomerService = "ba:skill_customerservice";
        private const string DeductKey = "reallife_dealer_deduct";
        private const float FloorLift = 0.08f;   // metres over the floor a car is put down at

        public static IModLogger Logger;

        private static readonly List<BuildingRegistration> Regs = new List<BuildingRegistration>(8);
        private static readonly List<int> Offsets = new List<int>(2 * DealerCatalogue.SpotsMost);
        private static int[] Current = new int[DealerCatalogue.CarsMost], Chosen = new int[DealerCatalogue.CarsMost], Into = new int[DealerCatalogue.CarsMost];   // Park's arrangement, made once

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

        // ---------------------------------------------------------------- the dealerships

        /// <summary>A business by its address, written as the company writes a place.</summary>
        public static string Place(BuildingRegistration reg)
        {
            return Uri.EscapeDataString(reg.StreetName ?? string.Empty) + "~" + reg.StreetNumber.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        public static string Place(Address address)
        {
            return address == null ? string.Empty : Uri.EscapeDataString(address.streetName ?? string.Empty) + "~" + address.streetNumber.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        /// <summary>True for a dealership of the player's own that is open for business (a name, an address).</summary>
        public static bool Mine(BuildingRegistration reg)
        {
            return DealerType.Is(reg) && GoingPublic.Mine(reg) && reg.HasEstablishedBusiness && reg.HasValidAddress;
        }

        /// <summary>The player's dealerships. One pass over the save's buildings.</summary>
        public static void Dealerships(List<BuildingRegistration> into)
        {
            into.Clear();
            List<BuildingRegistration> all = Save != null ? Save.BuildingRegistrations : null;
            for (int i = 0; all != null && i < all.Count; i++)
            {
                if (Mine(all[i]))
                {
                    into.Add(all[i]);
                }
            }
        }

        public static BuildingRegistration Registration(string place)
        {
            List<BuildingRegistration> all = Save != null ? Save.BuildingRegistrations : null;
            for (int i = 0; all != null && i < all.Count; i++)
            {
                if (Mine(all[i]) && Place(all[i]) == place)
                {
                    return all[i];
                }
            }
            return null;
        }

        public static string NameOf(BuildingRegistration reg)
        {
            return reg != null ? reg.GetDisplayName() : string.Empty;
        }

        /// <summary>The player stands inside this dealership now.</summary>
        public static bool Inside(BuildingRegistration reg)
        {
            BuildingManager manager = InstanceBehavior<BuildingManager>.Instance;
            return reg != null && BuildingManager.IsInsideBuilding && manager != null && manager.building != null && manager.building.Address == reg.Address;
        }

        // ---------------------------------------------------------------- what the market can offer

        /// <summary>
        /// The cars a dealership deals in: the game's own vehicle types that are cars, vans or off-roaders to the mod's car
        /// catalogue, with a list price, a fuel tank, a body of their own (not carried, not ridden on foot). Made once a city.
        /// </summary>
        public static void Models(List<MarketModel> into, List<string> colours)
        {
            into.Clear();
            colours.Clear();
            List<string> names = VehicleTypeHelper.GetVehicleTypeNames();
            names.Sort(StringComparer.Ordinal);
            for (int i = 0; i < names.Count; i++)
            {
                VehicleType t = VehicleTypeHelper.GetVehicleType(names[i]);
                if (t == null || t.price < 1000f || !t.IsMotorVehicle || t.spawnInPlayerObject || t.usePedestrianCam || VehicleTypeHelper.IsModVehicleType(names[i])
                    || names[i].IndexOf("deliveryjob", StringComparison.Ordinal) >= 0)
                {
                    continue;
                }
                CarSpec spec = CarCatalogue.For(names[i], new CarTraits(true, false, t.maxSpeed, t.maxCargoCapacity, t.isLuxuryCar, 1200f));
                if (spec.Kind == CarKind.Untouched || spec.Kind == CarKind.Truck)
                {
                    continue;
                }
                into.Add(new MarketModel(names[i], t.price, (int)spec.Kind));
            }
            GlobalReferences refs = InstanceBehavior<GlobalReferences>.Instance;
            VehicleColor[] all = refs != null ? refs.vehicleColors : null;
            for (int i = 0; all != null && i < all.Length; i++)
            {
                if (all[i] != null && all[i].randomWeight > 0f)
                {
                    colours.Add(all[i].name);
                }
            }
        }

        /// <summary>A vehicle type's name as the game writes it.</summary>
        public static string CarName(string type)
        {
            return string.IsNullOrEmpty(type) ? string.Empty : type.GetLocalization();
        }

        /// <summary>A colour as the game writes it in a vehicle contract: its name and a mark in its own tint.</summary>
        public static string ColourName(string colour)
        {
            if (string.IsNullOrEmpty(colour) || !VehicleHelper.TryGetVehicleColor(colour, out VehicleColor c) || c == null)
            {
                return string.Empty;
            }
            return c.name + " <color=#" + ColorUtility.ToHtmlStringRGB(c.tint) + ">■</color>";
        }

        public static float ListPrice(string type)
        {
            VehicleType t = string.IsNullOrEmpty(type) ? null : VehicleTypeHelper.GetVehicleType(type);
            return t != null ? t.price : 0f;
        }

        // ---------------------------------------------------------------- an hour of a dealership

        /// <summary>What a dealership's hour has to go on, read from the game.</summary>
        public static YardHour Hour(BuildingRegistration reg)
        {
            var h = new YardHour();
            GameInstance save = Save;
            if (reg == null || save == null)
            {
                return h;
            }
            h.Open = !reg.temporarilyClosed && BusinessHelper.IsBusinessOpen(reg) && (!BusinessGame.Promoted(reg) || BusinessGame.HasFootfall(reg));
            try
            {
                CustomerEntriesCalculator calc = CustomerEntriesCalculatorFactory.GetCustomerEntriesCalculator(reg);
                h.ShopCustomers = calc != null ? Mathf.Max(0f, calc.GetInitialCustomers()) : 0f;
            }
            catch (Exception)
            {
                h.ShopCustomers = 0f;   // a building the game cannot size has no visitors
            }
            h.Promotion = reg.promotion != null ? reg.promotion.total : 0;
            h.BaseMultiplier = save.gameVariables != null ? save.gameVariables.baseCustomerPromotionMultiplier : 0.5f;
            h.Satisfaction = reg.satisfaction != null ? reg.satisfaction.overall : 50;
            Staff(reg, out h.Staff, out h.Skill);
            return h;
        }

        /// <summary>The salespeople on shift at a business now: customer service their main skill, at work this hour; and their average skill.</summary>
        public static void Staff(BuildingRegistration reg, out int count, out int skill)
        {
            count = 0;
            skill = 0;
            List<EmployeeInstance> staff = Save != null ? Save.EmployeeInstances : null;
            float sum = 0f;
            for (int i = 0; staff != null && i < staff.Count; i++)
            {
                EmployeeInstance e = staff[i];
                if (e == null || e.assignedAddress == null || e.assignedAddress != reg.Address || e.isAbsent || e.characterData == null || e.characterData.skills == null || e.characterData.skills.Count == 0)
                {
                    continue;
                }
                if (e.GetPrimarySkill() != CustomerService || !e.IsWorking())
                {
                    continue;
                }
                count++;
                sum += e.GetSkillValue(CustomerService);
            }
            skill = count > 0 ? Mathf.RoundToInt(sum / count) : 0;
        }

        /// <summary>Everyone assigned to a business with customer service as their main skill, on shift or not.</summary>
        public static int Salespeople(BuildingRegistration reg)
        {
            int n = 0;
            List<EmployeeInstance> staff = Save != null ? Save.EmployeeInstances : null;
            for (int i = 0; staff != null && i < staff.Count; i++)
            {
                EmployeeInstance e = staff[i];
                n += e != null && e.assignedAddress != null && e.assignedAddress == reg.Address && e.characterData != null && e.characterData.skills != null && e.characterData.skills.Count > 0
                     && e.GetPrimarySkill() == CustomerService ? 1 : 0;
            }
            return n;
        }

        // ---------------------------------------------------------------- the lot

        /// <summary>
        /// The places of the lot of the dealership the player stands in, from its floor as the game measured it when the
        /// player came in (BuildingManager.interiorBounds): rows of cars inside its walls (DealerRules.Layout), in the
        /// world's centimetres. False if the floor cannot be had.
        /// </summary>
        public static bool Lay(DealerYard y)
        {
            BuildingManager manager = InstanceBehavior<BuildingManager>.Instance;
            if (manager == null || !BuildingManager.IsInsideBuilding)
            {
                return false;
            }
            Bounds b = manager.interiorBounds;
            if (b.size.x < 1f || b.size.z < 1f)
            {
                return false;
            }
            int count = DealerRules.Layout(Mathf.RoundToInt(b.size.x * 100f), Mathf.RoundToInt(b.size.z * 100f), Offsets, out bool across);
            y.Spots.Clear();
            int floor = Mathf.RoundToInt((b.min.y + FloorLift) * 100f);
            for (int s = 0; s < count; s++)
            {
                y.Spots.Add(new LotSpot
                {
                    X = Mathf.RoundToInt(b.center.x * 100f) + Offsets[2 * s], Y = floor, Z = Mathf.RoundToInt(b.center.z * 100f) + Offsets[2 * s + 1], Yaw = across ? 90 : 0
                });
            }
            return true;
        }

        public static Vector3 At(LotSpot s)
        {
            return new Vector3(s.X / 100f, s.Y / 100f, s.Z / 100f);
        }

        public static Quaternion Facing(LotSpot s)
        {
            return Quaternion.Euler(0f, s.Yaw, 0f);
        }

        /// <summary>The way a car faces on its bay: the bay's own, or turned round as the player chose.</summary>
        public static Quaternion Facing(LotSpot s, bool turned)
        {
            return Quaternion.Euler(0f, ShowroomFloorRules.Yaw(s.Yaw, turned), 0f);
        }

        /// <summary>The game's vehicle of a car of the stock; null if the save has it no more.</summary>
        public static VehicleInstance Vehicle(string id)
        {
            List<VehicleInstance> all = Save != null ? Save.VehicleInstances : null;
            for (int i = 0; all != null && id != null && i < all.Count; i++)
            {
                if (all[i] != null && all[i].id == id)
                {
                    return all[i];
                }
            }
            return null;
        }

        /// <summary>The spawned controller of a vehicle; null while the game has not made it (the player is elsewhere).</summary>
        public static VehicleController Controller(string id)
        {
            List<VehicleController> all = VehicleHelper.AllPlayerVehicles;
            for (int i = 0; id != null && i < all.Count; i++)
            {
                if (all[i] != null && all[i].vehicleInstance != null && all[i].vehicleInstance.id == id)
                {
                    return all[i];
                }
            }
            return null;
        }

        /// <summary>
        /// The cars of a lot on their places, as the player sees them inside: each moved to its bay, held still, its door
        /// closed to the player and its map pin hidden. A car the player chose a bay for stands there, the way round they
        /// chose (Floor/ShowroomBays); the others keep theirs while it is free, and one with none takes the first bay free
        /// (Floor/ShowroomFloorRules.Arrange). A car of the stock with no game vehicle any more is reported back. Returns
        /// the cars put in place.
        /// </summary>
        public static int Park(DealerYard y, List<StockCar> lost, ShowroomBays bays)
        {
            lost?.Clear();
            int n = y.Cars.Count;
            if (Current.Length < n)
            {
                Current = new int[n];
                Chosen = new int[n];
                Into = new int[n];
            }
            for (int c = 0; c < n; c++)
            {
                StockCar car = y.Cars[c];
                if (Vehicle(car.VehicleId) == null)
                {
                    lost?.Add(car);
                    Current[c] = ShowroomFloorRules.Absent;
                    Chosen[c] = -1;
                    continue;
                }
                Current[c] = car.Spot;
                Chosen[c] = bays != null ? bays.BayOf(y.Place, car.VehicleId) : -1;
            }
            ShowroomFloorRules.Arrange(y.Spots.Count, Current, Chosen, n, Into);
            int placed = 0;
            for (int c = 0; c < n; c++)
            {
                if (Current[c] == ShowroomFloorRules.Absent)
                {
                    continue;
                }
                StockCar car = y.Cars[c];
                car.Spot = Into[c];
                if (car.Spot < 0)
                {
                    continue;
                }
                VehicleInstance v = Vehicle(car.VehicleId);
                LotSpot s = y.Spots[car.Spot];
                Quaternion facing = Facing(s, bays != null && bays.TurnedOf(y.Place, car.VehicleId));
                v.position = At(s);
                v.rotation = facing;
                VehicleController controller = Controller(car.VehicleId);
                if (controller != null)
                {
                    Hold(controller, At(s), facing);
                    placed++;
                }
            }
            return placed;
        }

        // A car on show: on its place, still, not to be driven off, no pin on the map.
        private static void Hold(VehicleController controller, Vector3 at, Quaternion facing)
        {
            Rigidbody body = controller.GetComponent<Rigidbody>();
            if (body != null)
            {
                body.velocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }
            controller.transform.SetPositionAndRotation(at, facing);
            controller.SetFreeze(true);
            controller.primaryInteractionEnabled = false;
            if (controller.poi != null)
            {
                controller.poi.SetHidden(true);
            }
        }

        /// <summary>
        /// A new car of the stock: a vehicle of the game's at the dealership, on the given place, of the kilometres and the
        /// condition it came with (the game's damage is what it is short of perfect). Spawned at once if the player stands
        /// inside, else made by the game when the player next walks in. Returns the vehicle's id; null if it could not be made.
        /// </summary>
        public static string MakeCar(BuildingRegistration reg, DealerYard y, int spot, string type, string colour, int condition)
        {
            VehicleType t = VehicleTypeHelper.GetVehicleType(type);
            if (t == null || spot < 0 || spot >= y.Spots.Count || Save == null)
            {
                return null;
            }
            LotSpot s = y.Spots[spot];
            var v = new VehicleInstance(type)
            {
                id = UuidHelper.GenerateBase64Uuid(),
                fuel = t.maxFuel * 0.5f,
                dirtiness = 0f,
                damage = Mathf.Clamp01((1000 - condition) / 1000f)
            };
            if (!string.IsNullOrEmpty(colour))
            {
                v.vehicleColorName = colour;
            }
            v.SetStreetData(reg.StreetName, reg.StreetNumber);
            if (Inside(reg))
            {
                VehicleController made = VehicleHelper.CreateAndSpawnVehicle(v, At(s), Facing(s));   // (it adds the vehicle to the save)
                if (made != null)
                {
                    Hold(made, At(s), Facing(s));
                }
            }
            else
            {
                v.position = At(s);
                v.rotation = Facing(s);
                Save.VehicleInstances.Add(v);
            }
            return v.id;
        }

        /// <summary>A car leaves the stock: its vehicle is deleted the game's own way.</summary>
        public static void RemoveCar(string vehicleId)
        {
            VehicleInstance v = Vehicle(vehicleId);
            if (v != null)
            {
                VehicleController controller = Controller(vehicleId);
                OverlayGuard.Release(controller);
                v.Delete(controller);
            }
        }

        /// <summary>A car's kilometres and condition written onto its vehicle (the game's damage).</summary>
        public static void Wear(StockCar car)
        {
            VehicleInstance v = Vehicle(car.VehicleId);
            if (v != null)
            {
                v.damage = Mathf.Clamp01((1000 - car.Condition) / 1000f);
            }
        }

        // ---------------------------------------------------------------- money

        /// <summary>Money in at a dealership, as a dated transaction of the game's own type or the mod's.</summary>
        public static void Give(int amount, string type, Dictionary<string, string> data, BuildingRegistration reg)
        {
            if (amount > 0 && Save != null)
            {
                GameManager.ChangeMoneySafe(amount, data != null ? new TransactionInfo(type, data) : new TransactionInfo(type), null, reg != null ? reg.Address : null, true);
            }
        }

        /// <summary>
        /// Money out at a dealership, marked deductible as the game marks a business's costs. Unless forced, the game's call
        /// refuses a charge the cash does not cover: false, and nothing is taken.
        /// </summary>
        public static bool Take(int amount, string type, Dictionary<string, string> data, BuildingRegistration reg, bool force)
        {
            if (amount <= 0)
            {
                return true;
            }
            if (Save == null)
            {
                return false;
            }
            TransactionInfo info = data != null ? new TransactionInfo(type, data) : new TransactionInfo(type);
            info.SetTaxDeductibleName(DeductKey.GetLocalization());
            return GameManager.ChangeMoneySafe(-amount, info, null, reg != null ? reg.Address : null, force);
        }

        /// <summary>The data of a car bought, as the game writes a vehicle bought (its name), and of one sold (what was sold).</summary>
        public static Dictionary<string, string> Bought(string type)
        {
            return new Dictionary<string, string>(1) { { "vehicleName", CarName(type) } };
        }

        public static Dictionary<string, string> Sold(string type, BuildingRegistration reg)
        {
            return new Dictionary<string, string>(1) { { "itemSoldInfo", CarName(type) + " (" + NameOf(reg) + ")" } };
        }

        public static Dictionary<string, string> Named(BuildingRegistration reg)
        {
            return new Dictionary<string, string>(1) { { "name", NameOf(reg) } };
        }
    }
}
