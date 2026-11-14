using System;
using System.Collections.Generic;
using BigAmbitions.Items;
using Buildings.Office.Headquarters;
using Entities;
using Helpers;
using Streets;
using UnityEngine;
using Vehicles.VehicleTypes;

namespace RealLifeOverhaul.Economy
{
    /// <summary>A vehicle slot of a warehouse as a trip reads it: its vehicle, its driver and the stops its vehicle serves.</summary>
    public struct TripSlot
    {
        public string Vehicle, Driver;
        public int Stops;          // the game's own stops a slot's vehicle serves (VehicleSlot.DestinationsThatCanDeliver)
        public int Room;           // boxes
        public int WageCents;      // the driver's hourly wage
        public TripVehicle Kind;
        public bool Ready;         // a vehicle and a driver who is at work
    }

    /// <summary>
    /// Where delivery trips and food spoilage meet the game: the player's logistics plans, the hold that keeps the game's nightly
    /// run from moving their goods, a warehouse's slots, what a stop needs and what a delivery moved (the game's own
    /// LogisticsManagerPlan.DeliverDestination), the places' stock (BuildingHelper's own count, the game's own calls to take it
    /// out), the buildings' places in the city and the rival shops' daily books. Nothing here runs per frame.
    /// </summary>
    internal static partial class LogisticsGame
    {
        private static readonly List<LogisticsManagerPlan> Held = new List<LogisticsManagerPlan>(8);
        private static readonly List<List<LogisticsManagerPlanDestination>> HeldStops = new List<List<LogisticsManagerPlanDestination>>(8);
        private static readonly Dictionary<string, Vector3> Places = new Dictionary<string, Vector3>(StringComparer.Ordinal);
        private static readonly List<int> Kept = new List<int>(8);

        private static GameInstance Save => SaveGameManager.Current;

        /// <summary>The absolute game minute: the day's minutes and the hour's.</summary>
        public static int Now => Save != null ? Save.Day * TripRules.DayMinutes + Save.Hour * 60 + Mathf.Clamp((int)Save.Minute, 0, 59) : 0;

        public static int Day => Save != null ? Save.Day : 0;

        public static bool Ready => Save != null && Save.logisticsManagerPlans != null && Save.BuildingRegistrations != null;

        /// <summary>A place's key, as the company's files name it: its street (escaped) and number.</summary>
        public static string Key(Address a) => a == null ? string.Empty
            : Uri.EscapeDataString(a.streetName ?? string.Empty) + "~" + a.streetNumber.ToString(System.Globalization.CultureInfo.InvariantCulture);

        public static BuildingRegistration Reg(string key)
        {
            int at = key != null ? key.LastIndexOf('~') : -1;
            if (at <= 0 || !int.TryParse(key.Substring(at + 1), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out int number))
            {
                return null;
            }
            return BuildingHelper.GetBuildingRegistration(new Address(Uri.UnescapeDataString(key.Substring(0, at)), number));
        }

        public static List<LogisticsManagerPlan> Plans => Save != null ? Save.logisticsManagerPlans : null;

        public static LogisticsManagerPlan Plan(string id)
        {
            List<LogisticsManagerPlan> all = Plans;
            for (int i = 0; all != null && i < all.Count; i++)
            {
                if (all[i] != null && all[i].id == id)
                {
                    return all[i];
                }
            }
            return null;
        }

        /// <summary>The route's warehouse when the route is the player's own to run as trips: its warehouse and every stop theirs.</summary>
        public static Warehouse From(LogisticsManagerPlan plan)
        {
            if (plan == null || plan.targetAddress == null || plan.targetAddress.IsUndefined())
            {
                return null;
            }
            var w = BuildingHelper.GetBuildingRegistration(plan.targetAddress) as Warehouse;
            if (w == null || !w.RentedByPlayer || !GoingPublic.Mine(w))
            {
                return null;
            }
            for (int i = 0; plan.destinations != null && i < plan.destinations.Count; i++)
            {
                Address to = plan.destinations[i] != null ? plan.destinations[i].deliveryTargetAddress : null;
                if (to != null && !to.IsUndefined() && GoingPublic.Foreign(BuildingHelper.GetBuildingRegistration(to)))
                {
                    return null;   // a stop of another player's: the game's own run, as before
                }
            }
            return w;
        }

        /// <summary>Whether the route's manager is on it and at work, as the game asks before it runs a route (GetPlannedDeliveries).</summary>
        public static bool Managed(LogisticsManagerPlan plan)
        {
            EmployeeInstance m = plan != null && !string.IsNullOrEmpty(plan.assignedEmployeeId) ? EmployeeHelper.GetEmployeeById(plan.assignedEmployeeId, false) : null;
            return m != null && m.IsEmployeeAvailable();
        }

        public static int MostStops(LogisticsManagerPlan plan) => plan != null ? Math.Max(0, plan.MaxDestinations) : 0;

        // ------------------------------------------------------------------------------------------------- the hold

        /// <summary>
        /// Holds the routes the trips run: each one's stops swapped for none in the hour the game runs every route (its run then
        /// finds nothing to send), until <see cref="Release"/> puts them back in the same tick. Returns the routes held.
        /// </summary>
        public static int Hold(Func<LogisticsManagerPlan, bool> taken)
        {
            Release();
            List<LogisticsManagerPlan> all = Plans;
            for (int i = 0; all != null && i < all.Count; i++)
            {
                LogisticsManagerPlan p = all[i];
                if (p != null && p.destinations != null && p.destinations.Count > 0 && taken(p))
                {
                    Held.Add(p);
                    HeldStops.Add(p.destinations);
                    p.destinations = new List<LogisticsManagerPlanDestination>();
                }
            }
            return Held.Count;
        }

        public static bool Holding => Held.Count > 0;

        /// <summary>Puts every held route's stops back. Returns the routes put back.</summary>
        public static int Release()
        {
            int n = Held.Count;
            for (int i = 0; i < n; i++)
            {
                if (Held[i].destinations == null || Held[i].destinations.Count == 0)
                {
                    Held[i].destinations = HeldStops[i];
                }
                else
                {
                    Held[i].destinations.InsertRange(0, HeldStops[i]);   // something added a stop while it was held: kept after the route's own
                }
            }
            Held.Clear();
            HeldStops.Clear();
            return n;
        }

        /// <summary>Whether the game's own deliveries are running (its queues), so a stop is not delivered beside them.</summary>
        public static bool GameBusy => LogisticsManagerHelper.WarehouseDeliveriesWork.HasPendingWork || LogisticsManagerHelper.FactoryDeliveriesWork.HasPendingWork;

        // ------------------------------------------------------------------------------------------------- slots and stops

        /// <summary>The warehouse's vehicle slots, in their order. Returns how many.</summary>
        public static int Slots(Warehouse w, List<TripSlot> into)
        {
            into.Clear();
            for (int i = 0; w != null && w.vehicleSlots != null && i < w.vehicleSlots.Count; i++)
            {
                VehicleSlot s = w.vehicleSlots[i];
                var t = new TripSlot { Vehicle = s != null ? s.vehicleInstanceId : null, Driver = s != null ? s.employeeDriverId : null };
                VehicleInstance v = !string.IsNullOrEmpty(t.Vehicle) ? Vehicle(t.Vehicle) : null;
                VehicleType type = v != null ? v.VehicleType : null;
                EmployeeInstance d = !string.IsNullOrEmpty(t.Driver) ? EmployeeHelper.GetEmployeeById(t.Driver, false) : null;
                if (type != null)
                {
                    t.Room = Math.Max(0, type.maxCargoCapacity);
                    t.Stops = Math.Max(0, type.destinationsThatCanDeliver);
                    t.Kind = Kind(type);
                }
                if (d != null)
                {
                    t.WageCents = Mathf.RoundToInt(Mathf.Max(0f, d.hourlyWage) * 100f);
                }
                t.Ready = type != null && d != null && d.IsEmployeeAvailable();
                if (!t.Ready || string.IsNullOrEmpty(t.Driver))
                {
                    t.Stops = string.IsNullOrEmpty(t.Driver) ? 0 : t.Stops;   // as the game counts a slot's stops: none without a driver
                }
                into.Add(t);
            }
            return into.Count;
        }

        public static VehicleInstance Vehicle(string id)
        {
            List<VehicleInstance> all = Save != null ? Save.VehicleInstances : null;
            for (int i = 0; all != null && i < all.Count; i++)
            {
                if (all[i] != null && all[i].id == id)
                {
                    return all[i];
                }
            }
            return null;
        }

        private static TripVehicle Kind(VehicleType type)
        {
            var traits = new CarTraits(type.maxFuel > 0f, type.fitsHandTruck || type.fitsFlatbed, type.maxSpeed, type.maxCargoCapacity, type.isLuxuryCar, 1500f);
            CarKind k = CarCatalogue.For(type.vehicleTypeName, traits).Kind;
            return k == CarKind.Truck ? TripVehicle.Truck : k == CarKind.Van ? TripVehicle.Van : TripVehicle.Car;
        }

        /// <summary>The route's stops in its order (those the game would run: the first of them up to its most). Returns how many.</summary>
        public static int Stops(LogisticsManagerPlan plan, List<LogisticsManagerPlanDestination> into)
        {
            into.Clear();
            int most = MostStops(plan);
            for (int i = 0; plan != null && plan.destinations != null && i < plan.destinations.Count && into.Count < most; i++)
            {
                LogisticsManagerPlanDestination d = plan.destinations[i];
                if (d != null && d.deliveryTargetAddress != null && !d.deliveryTargetAddress.IsUndefined())
                {
                    into.Add(d);
                }
            }
            return into.Count;
        }

        /// <summary>
        /// What a stop needs of each of its products, as the game's own run reckons it (LogisticsManagerPlanDestination.ProcessStockTarget):
        /// its target less what it holds, or the whole target for an exporter.
        /// </summary>
        public static void Needs(LogisticsManagerPlanDestination d, List<string> items, List<int> wanted)
        {
            items.Clear();
            wanted.Clear();
            bool exporter = d.IsTargetExporter;
            for (int i = 0; d.stockTargets != null && i < d.stockTargets.Count; i++)
            {
                ItemAmountTarget t = d.stockTargets[i];
                if (t == null || string.IsNullOrEmpty(t.itemName) || t.targetAmount <= 0)
                {
                    continue;
                }
                int need = exporter ? t.targetAmount : t.targetAmount - BuildingHelper.CountTotalResourcesInStock(d.deliveryTargetAddress, t.itemName);
                if (need > 0)
                {
                    items.Add(t.itemName);
                    wanted.Add(need);
                }
            }
        }

        public static int InStock(BuildingRegistration reg, string item) => reg != null && !string.IsNullOrEmpty(item) ? BuildingHelper.CountTotalResourcesInStock(reg, item) : 0;

        public static int BoxSize(string item)
        {
            Item it = !string.IsNullOrEmpty(item) ? ItemsGetter.GetByName(item, true) : null;
            return it != null ? Math.Max(1, it.boxSize) : 1;
        }

        /// <summary>
        /// Delivers a trip's stop by the game's own call: each of its products' targets set for the moment to what the stop holds and
        /// the trip's load (an exporter's to the load), any other to none, then put back. Returns, in the stop's order, the pieces each
        /// product moved out of the warehouse (<paramref name="moved"/>), the pieces the stop's own count of that product rose by
        /// (<paramref name="credited"/>: an exporter sells what it takes, so its count is the moved) and what the warehouse held of it
        /// before (<paramref name="held"/>); false when the stop is no longer on the route.
        /// </summary>
        public static bool Deliver(LogisticsManagerPlan plan, Warehouse from, TripStop stop, List<int> moved, List<int> credited, List<int> held)
        {
            moved.Clear();
            credited.Clear();
            held.Clear();
            LogisticsManagerPlanDestination d = null;
            for (int i = 0; plan.destinations != null && i < plan.destinations.Count && d == null; i++)
            {
                LogisticsManagerPlanDestination c = plan.destinations[i];
                d = c != null && c.deliveryTargetAddress != null && Key(c.deliveryTargetAddress) == stop.Place ? c : null;
            }
            if (d == null || d.stockTargets == null)
            {
                return false;
            }
            bool exporter = d.IsTargetExporter;
            Kept.Clear();
            for (int i = 0; i < d.stockTargets.Count; i++)
            {
                ItemAmountTarget t = d.stockTargets[i];
                Kept.Add(t != null ? t.targetAmount : 0);
            }
            var before = new int[stop.Items.Count];
            var there = new int[stop.Items.Count];
            for (int g = 0; g < stop.Items.Count; g++)
            {
                before[g] = InStock(from, stop.Items[g]);
                there[g] = BuildingHelper.CountTotalResourcesInStock(d.deliveryTargetAddress, stop.Items[g]);
            }
            try
            {
                for (int i = 0; i < d.stockTargets.Count; i++)
                {
                    ItemAmountTarget t = d.stockTargets[i];
                    if (t == null)
                    {
                        continue;
                    }
                    int g = stop.Items.IndexOf(t.itemName);
                    int load = g >= 0 ? stop.Pieces[g] : 0;
                    t.targetAmount = load <= 0 ? 0 : exporter ? load : BuildingHelper.CountTotalResourcesInStock(d.deliveryTargetAddress, t.itemName) + load;
                }
                plan.DeliverDestination(d);
            }
            finally
            {
                for (int i = 0; i < d.stockTargets.Count && i < Kept.Count; i++)
                {
                    if (d.stockTargets[i] != null)
                    {
                        d.stockTargets[i].targetAmount = Kept[i];
                    }
                }
                Kept.Clear();
            }
            for (int g = 0; g < stop.Items.Count; g++)
            {
                int gone = Math.Max(0, before[g] - InStock(from, stop.Items[g]));
                moved.Add(gone);
                held.Add(before[g]);
                int rose = BuildingHelper.CountTotalResourcesInStock(d.deliveryTargetAddress, stop.Items[g]) - there[g];
                credited.Add(exporter ? gone : Math.Max(0, Math.Min(gone, rose)));
            }
            if (!GameBusy)
            {
                LogisticsManagerPlan.OnFinishedDeliveries();   // the game's end of a run: its reports of what could not go, an exporter's takings
            }
            return true;
        }

        // ------------------------------------------------------------------------------------------------- the city

        /// <summary>A building's place in the city, looked up once a session through the game's own table of them.</summary>
        public static bool At(string key, Address a, out Vector3 at)
        {
            if (Places.TryGetValue(key, out at))
            {
                return true;
            }
            CityManager city = InstanceBehavior<CityManager>.Instance;
            CityBuildingController c = city != null && a != null && !a.IsUndefined() ? city.FindCityBuildingController(a) : null;
            if (c == null)
            {
                return false;
            }
            at = c.transform.position;
            Places[key] = at;
            return true;
        }

        public static void Forget() => Places.Clear();

        /// <summary>The straight line between two places, whole metres on the ground.</summary>
        public static int Straight(Vector3 a, Vector3 b)
        {
            float x = a.x - b.x, z = a.z - b.z;
            return Mathf.RoundToInt(Mathf.Sqrt(x * x + z * z));
        }

        /// <summary>The game's pump price a litre, in cents (GasStationOverlay's own); the rule's own when it cannot be read.</summary>
        public static int PumpCents()
        {
            try
            {
                object v = GameContract.PumpPrice.Field?.GetValue(null);
                return v is float f && f > 0f ? Mathf.RoundToInt(f * 100f) : TripRules.PumpCents;
            }
            catch (Exception)
            {
                return TripRules.PumpCents;
            }
        }

        /// <summary>Books a trip's fuel at its warehouse, as a running cost of the warehouse's.</summary>
        public static void Fuel(Warehouse from, int cents)
        {
            if (from != null && cents > 0)
            {
                BizBooks.Book(-cents / 100f, new TransactionInfo(FuelKey), null, from.Address, true, false);
            }
        }

        public const string FuelKey = "reallife_transaction_trips_fuel";
    }
}
