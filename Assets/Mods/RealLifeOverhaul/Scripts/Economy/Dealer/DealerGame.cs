using System;
using System.Collections.Generic;
using AI.Customers.CustomerEntries;
using Data.VehicleColors;
using Entities;
using Extensions;
using Helpers;
using Localizor;
using UnityEngine;
using Vehicles.VehicleTypes;

namespace RealLifeOverhaul.Economy.Dealership
{
    /// <summary>What the car showroom asks of the rest of the mod, each with a default; Wiring fills them in.</summary>
    public static class DealerBridges
    {
        /// <summary>Thousandths of the anchor day's car buyers among the city's people (the living population); 0 for none, as before. Default: none.</summary>
        public static Func<int> Buyers;

        public static void Reset()
        {
            Buyers = null;
        }
    }

    /// <summary>
    /// Everything the dealership reads from or does to the game. A car of the stock is a game vehicle at the showroom's address,
    /// so the game spawns and saves it; money moves as the game's own transactions there. Nothing per frame.
    /// </summary>
    internal static class DealerGame
    {
        /// <summary>Owner deduction labels whose mixed costs have no single portable recurrence rule.</summary>
        internal static string[] CompanionUnclassifiedTaxLabels() => new[] { DeductKey.GetLocalization() };

        private const string CustomerService = "ba:skill_customerservice";
        private const string DeductKey = "reallife_dealer_deduct";
        private const float FloorLift = 0.08f;   // metres over the floor a car is put down at

        private static readonly List<int> Offsets = new List<int>(2 * DealerCatalogue.SpotsMost);
        private static int[] _current = new int[DealerCatalogue.CarsMost], _chosen = new int[DealerCatalogue.CarsMost], _into = new int[DealerCatalogue.CarsMost];

        private static GameInstance Save => SaveGameManager.Current;

        public static int Cash => Save != null ? WholeMoney.Floor(Save.Money) : 0;

        public static int DaysPerYear
        {
            get
            {
                int days = GameCalendar.DaysPerYear;
                return days >= 8 ? days : 60;
            }
        }

        /// <summary>A business by its address, written as the company writes a place.</summary>
        public static string Place(BuildingRegistration reg)
        {
            return Uri.EscapeDataString(reg.StreetName ?? string.Empty) + "~" + reg.StreetNumber.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        public static string Place(Address address)
        {
            return address == null ? string.Empty : Uri.EscapeDataString(address.streetName ?? string.Empty) + "~" + address.streetNumber.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        // A showroom of the player's own (not another Going Public player's) that is open for business.
        private static bool Mine(BuildingRegistration reg)
        {
            return DealerType.Is(reg) && GoingPublic.Mine(reg) && reg.HasEstablishedBusiness && reg.HasValidAddress && GameRegistrations.Found(reg);
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

        public static bool Inside(BuildingRegistration reg)
        {
            BuildingManager manager = InstanceBehavior<BuildingManager>.Instance;
            return reg != null && BuildingManager.IsInsideBuilding && manager != null && manager.building != null && manager.building.Address == reg.Address;
        }

        /// <summary>
        /// The cars a showroom deals in (the game's cars, vans and off-roaders with a price) and the game's colours. Other mods'
        /// vehicles are left out: the game spawns them again without their shaders. Made once a city.
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

        /// <summary>The class of a vehicle type as the mod reads it for its gearbox (CarCatalogue): what the showroom's pages colour a car by.</summary>
        public static CarKind KindOf(string type)
        {
            VehicleType t = string.IsNullOrEmpty(type) ? null : VehicleTypeHelper.GetVehicleType(type);
            return t == null ? CarKind.Untouched : CarCatalogue.For(type, new CarTraits(true, false, t.maxSpeed, t.maxCargoCapacity, t.isLuxuryCar, 1200f)).Kind;
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

        /// <summary>True when the game has this model: a listing of a car it does not have (a car mod taken out, a changed game) is refused before any money moves.</summary>
        public static bool Knows(string type)
        {
            return !string.IsNullOrEmpty(type) && VehicleTypeHelper.GetVehicleType(type) != null;
        }

        /// <summary>What a showroom's hour has to go on, read from the game: open, customer count, promotion, satisfaction and staff on shift.</summary>
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
                h.ShopCustomers = calc != null ? Mathf.Max(0f, calc.GetInitialCustomers()) * CustomerScale.For(reg) : 0f;   // the living population's people, where it runs
            }
            catch (Exception)
            {
                h.ShopCustomers = 0f;   // a building the game cannot size has no visitors
            }
            h.Promotion = reg.promotion != null ? reg.promotion.total : 0;
            h.Buyers = DealerBridges.Buyers != null ? Mathf.Max(0, DealerBridges.Buyers()) : 0;
            h.BaseMultiplier = save.gameVariables != null ? save.gameVariables.baseCustomerPromotionMultiplier : 0.5f;
            h.Satisfaction = reg.satisfaction != null ? reg.satisfaction.overall : 50;
            Staff(reg, out h.Staff, out h.Skill);
            return h;
        }

        // The salespeople on shift at a business now (customer service their main skill, at work, not absent), and their average skill.
        private static void Staff(BuildingRegistration reg, out int count, out int skill)
        {
            count = 0;
            skill = 0;
            List<EmployeeInstance> staff = Save != null ? Save.EmployeeInstances : null;
            float sum = 0f;
            for (int i = 0; staff != null && i < staff.Count; i++)
            {
                EmployeeInstance e = staff[i];
                if (e == null || e.assignedAddress == null || e.assignedAddress != reg.Address || e.isAbsent
                    || e.characterData == null || e.characterData.skills == null || e.characterData.skills.Count == 0)
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

        /// <summary>The lot of the showroom the player is in, laid out on its floor as the game measured it; false without a floor.</summary>
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
        /// The lot's cars held still on their bays (ShowroomFloorRules.Arrange); cars the save lost go into <paramref name="lost"/>, and cars whose vehicle
        /// is not the model paid for (the save's entry or the object the game stood) into <paramref name="wrong"/>, which are not shown. Returns the cars placed.
        /// </summary>
        public static int Park(DealerYard y, List<StockCar> lost, List<StockCar> wrong, ShowroomBays bays)
        {
            lost?.Clear();
            wrong?.Clear();
            int n = y.Cars.Count;
            if (_current.Length < n)
            {
                _current = new int[n];
                _chosen = new int[n];
                _into = new int[n];
            }
            for (int c = 0; c < n; c++)
            {
                StockCar car = y.Cars[c];
                VehicleInstance own = Vehicle(car.VehicleId);
                if (own == null || !Stands(car, own))
                {
                    // The save lost it, or the model paid for is not the car that stands: no bay for it.
                    List<StockCar> list = own == null ? lost : wrong;
                    list?.Add(car);
                    _current[c] = ShowroomFloorRules.Absent;
                    _chosen[c] = -1;
                    continue;
                }
                _current[c] = car.Spot;
                _chosen[c] = bays != null ? bays.BayOf(y.Place, car.VehicleId) : -1;
            }
            ShowroomFloorRules.Arrange(y.Spots.Count, _current, _chosen, n, _into);
            int placed = 0;
            for (int c = 0; c < n; c++)
            {
                if (_current[c] == ShowroomFloorRules.Absent)
                {
                    continue;
                }
                StockCar car = y.Cars[c];
                car.Spot = _into[c];
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

        // True when the vehicle of a car of the stock is the model that was paid for: the save's entry and, where the game has stood it, its object.
        private static bool Stands(StockCar car, VehicleInstance own)
        {
            return CarStandRules.Verdict(car.Type, own.vehicleTypeName) == CarMaking.Made && CarStandRules.Verdict(car.Type, ModelOf(Controller(car.VehicleId))) == CarMaking.Made;
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
        /// A new car of the stock on a place, damaged by what its condition lacks; spawned now if the player is inside. Its id, or null with
        /// <paramref name="why"/>: the game has not the model, the place is not there, the game stood another model than the one asked for, or it
        /// would not make it. The one place a car paid for becomes a vehicle, so whatever it makes is the model that was paid for or is taken out again.
        /// </summary>
        public static string MakeCar(BuildingRegistration reg, DealerYard y, int spot, string type, string colour, int condition, out CarMaking why)
        {
            why = CarMaking.Made;
            VehicleType t = Knows(type) ? VehicleTypeHelper.GetVehicleType(type) : null;
            if (t == null)
            {
                why = CarMaking.UnknownModel;
                return null;
            }
            if (spot < 0 || spot >= y.Spots.Count || Save == null)
            {
                why = CarMaking.NoPlace;
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
                why = Spawn(v, s);
                return why == CarMaking.Made ? v.id : null;
            }
            v.position = At(s);
            v.rotation = Facing(s);
            Save.VehicleInstances.Add(v);   // the game stands it, from its model's name, when the player walks in (Park reads it then)
            return v.id;
        }

        // A car made in the room the player stands in, the game's own way, and read back: the model that stands is the model asked for. One that is not,
        // and one the game throws on, is taken out again whole (its object and the save's entry), so that nothing half made is left on the lot.
        private static CarMaking Spawn(VehicleInstance v, LotSpot s)
        {
            VehicleController made = null;
            try
            {
                made = VehicleHelper.CreateAndSpawnVehicle(v, At(s), Facing(s));   // it adds the vehicle to the save
                string stood = ModelOf(made);
                CarMaking verdict = made != null ? CarStandRules.Verdict(v.vehicleTypeName, stood) : CarMaking.Failed;
                if (verdict == CarMaking.Made)
                {
                    Hold(made, At(s), Facing(s));
                    if (Meta.Dev.Verbose)
                    {
                        DealerDirector.Logger?.Info("Dealership check (a car made): the '" + v.vehicleTypeName + "' paid for stands as '" + (stood ?? "(not said)") + "': PASS");
                    }
                    return CarMaking.Made;
                }
                DealerDirector.Logger?.Warn("Dealership check (a car made): the '" + v.vehicleTypeName + "' paid for stands as '" + (stood ?? "(nothing)")
                                            + "' in the game; it was taken out again: FAIL");
                Unmake(v, made);
                return verdict;
            }
            catch (Exception e)
            {
                DealerDirector.Logger?.Error(e);
                Unmake(v, made);
                return CarMaking.Failed;
            }
        }

        // The model a spawned car is, as its object says; null where the game says nothing.
        private static string ModelOf(VehicleController car)
        {
            return car != null && car.vehicleType != null ? car.vehicleType.vehicleTypeName : null;
        }

        /// <summary>The model a car of the stock is as the game has it, for the log: the save's entry, and the object where the game has stood it.</summary>
        public static string StoodAs(string vehicleId)
        {
            VehicleInstance v = Vehicle(vehicleId);
            return (v != null ? v.vehicleTypeName : "no entry") + " in the save, " + (ModelOf(Controller(vehicleId)) ?? "nothing said") + " on the floor";
        }

        // A car made and not to stand: its object destroyed and its entry taken out of the save, the game's own way. A make that threw after the object
        // stood is found by its vehicle. Never throws.
        private static void Unmake(VehicleInstance v, VehicleController made)
        {
            try
            {
                VehicleController controller = made != null ? made : Controller(v.id);
                OverlayGuard.Release(controller);
                v.Delete(controller);
                if (Save != null)
                {
                    Save.VehicleInstances.Remove(v);
                }
            }
            catch (Exception e)
            {
                DealerDirector.Logger?.Error(e);
            }
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

        /// <summary>Money in at a showroom, as a transaction of the game's own type or the mod's.</summary>
        public static void Give(int amount, string type, Dictionary<string, string> data, BuildingRegistration reg)
        {
            if (amount > 0 && Save != null)
            {
                RealLifeOverhaul.Economy.BizBooks.Book(amount, data != null ? new TransactionInfo(type, data) : new TransactionInfo(type), null, reg != null ? reg.Address : null, true);
            }
        }

        /// <summary>Money out at a showroom, deductible; unless forced, false and nothing taken when the cash does not cover it.</summary>
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
            return RealLifeOverhaul.Economy.BizBooks.Book(-amount, info, null, reg != null ? reg.Address : null, force);
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
