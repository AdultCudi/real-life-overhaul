using System;
using System.Collections.Generic;
using BigAmbitions.Items;
using Buildings.BuildingTypes.Shared.Dirtiness;
using Entities;
using Helpers;
using UnityEngine;
using UnityEngine.AI;

namespace RealLifeOverhaul
{
    /// <summary>
    /// How tidy the player's home is. Where the home has the game's own floor dirt (the cells a
    /// business gets dirty and a mop cleans), that is what this reads and writes: living in the
    /// flat puts a little dirt on its floor, the game's own mop takes it off, tidying up takes it
    /// off everywhere at once, and nothing but the game's own numbers is in the save. A home
    /// without such cells keeps the figure in the mod's own state instead.
    /// </summary>
    public static class HomeTidy
    {
        private const string Residential = "ba:buildingtype_residential";

        private static BuildingRegistration _home;
        private static float _nextLook;
        private static float _owed;          // tidiness lost that is still too little to put on the floor

        public static bool HasHome => Home() != null;

        public static void Reset()
        {
            _home = null;
            _nextLook = 0f;
            _owed = 0f;
        }

        /// <summary>True while the player stands in a home of their own.</summary>
        /// <summary>Whether a home has power, and water, now (Utilities, through Wiring/UtilityLinks); null: always.</summary>
        public static Func<BuildingRegistration, bool> HasPower, HasWater;

        /// <summary>True if this home's power is cut.</summary>
        public static bool Dark(BuildingRegistration home)
        {
            Func<BuildingRegistration, bool> has = HasPower;
            return home != null && has != null && !has(home);
        }

        /// <summary>True if this home's water is cut.</summary>
        public static bool Dry(BuildingRegistration home)
        {
            Func<BuildingRegistration, bool> has = HasWater;
            return home != null && has != null && !has(home);
        }

        public static bool AtHome()
        {
            if (!BuildingManager.IsInsideBuilding)
            {
                return false;
            }
            var manager = InstanceBehavior<BuildingManager>.Instance;
            return manager != null && manager.building != null && GoingPublic.Mine(manager.buildingRegistration) && manager.building.BuildingType == Residential;
        }

        /// <summary>The home whose tidiness counts: the one the player is in, else the first they rent. Looked up at most every few seconds.</summary>
        public static BuildingRegistration Home()
        {
            if (AtHome())
            {
                _home = InstanceBehavior<BuildingManager>.Instance.buildingRegistration;
                return _home;
            }
            if (Time.unscaledTime < _nextLook)
            {
                return _home != null && GoingPublic.Mine(_home) ? _home : null;
            }
            _nextLook = Time.unscaledTime + 5f;
            if (_home != null && GoingPublic.Mine(_home))
            {
                return _home;
            }
            _home = null;
            try
            {
                List<BuildingRegistration> all = SaveGameManager.Current.BuildingRegistrations;
                for (int i = 0; all != null && i < all.Count; i++)
                {
                    if (all[i] != null && GoingPublic.Mine(all[i]) && all[i].GetBuildingType() == Residential)
                    {
                        _home = all[i];
                        break;
                    }
                }
            }
            catch (Exception)
            {
                _home = null;
            }
            return _home;
        }

        private static bool UsesFloor(BuildingRegistration home)
        {
            return home != null && home.dirtSpots != null && home.dirtSpots.Count > 0;
        }

        /// <summary>Reads the home's tidiness into the mod's state (where the game keeps it) and returns it.</summary>
        public static float Read()
        {
            BuildingRegistration home = Home();
            if (UsesFloor(home))
            {
                HomeState.Tidy = Mathf.Clamp(home.GetCleanliness(), 0f, 100f);
            }
            return HomeState.Tidy;
        }

        /// <summary>Tidier (positive) or messier (negative) by so many points of a hundred.</summary>
        public static void Change(float points)
        {
            BuildingRegistration home = Home();
            if (!UsesFloor(home))
            {
                HomeState.Tidy = HomeState.Clamp(HomeState.Tidy + points, 0f, 100f);
                return;
            }
            // The game's cleanliness is 100 less about twice the average dirt of the cells; small losses are saved up
            // so that a cell gets dirt it can show (the game draws it from 5 up) rather than a film over everything.
            _owed += points;
            if (_owed > -2f && _owed < 2f)
            {
                return;
            }
            float perCell = -_owed * 0.5f;
            _owed = 0f;
            List<DirtSpot> cells = home.dirtSpots;
            if (perCell < 0f)
            {
                for (int i = 0; i < cells.Count; i++)
                {
                    cells[i].dirtiness = Mathf.Max(0f, cells[i].dirtiness + perCell);
                }
            }
            else
            {
                // On a third of the cells, three times as much: patches, not an even grey.
                for (int i = 0; i < cells.Count; i++)
                {
                    if (UnityEngine.Random.value < 0.34f && cells[i].dirtiness < HomeBalance.DirtMost)
                    {
                        cells[i].dirtiness = Mathf.Min(HomeBalance.DirtMost, cells[i].dirtiness + perCell * 3f);
                    }
                }
            }
            var manager = InstanceBehavior<BuildingManager>.Instance;
            if (BuildingManager.IsInsideBuilding && manager != null && ReferenceEquals(manager.buildingRegistration, home))
            {
                manager.UpdateDirtinessInCurrentBuilding();
            }
            Read();
        }
    }

    /// <summary>
    /// The furniture the mod's routines use in the building the player is in: cookers, coffee
    /// machines and fridges, found by what the game calls them, once when the player comes in
    /// and again every quarter of a minute. Also the food in the fridges, which is the game's
    /// own cargo, read by kind of food for the cooking panel, and a clear piece of floor to tidy or exercise on.
    /// </summary>
    public static class HomePlace
    {
        public static readonly List<ItemController> Cookers = new List<ItemController>(4);
        public static readonly List<ItemController> CoffeeMachines = new List<ItemController>(4);
        public static readonly List<FridgeController> Fridges = new List<FridgeController>(4);

        private static Buildings.Building _scanned;
        private static float _nextScan;
        private static NavMeshPath _path;

        public static void Reset()
        {
            Cookers.Clear();
            CoffeeMachines.Clear();
            Fridges.Clear();
            _scanned = null;
            _nextScan = 0f;
        }

        /// <summary>Looks the building over if it is a different one or the last look is old. Cheap to call every second.</summary>
        public static void Scan(bool now = false)
        {
            var manager = InstanceBehavior<BuildingManager>.Instance;
            if (!BuildingManager.IsInsideBuilding || manager == null || manager.building == null)
            {
                if (_scanned != null)
                {
                    Reset();
                }
                return;
            }
            if (!now && ReferenceEquals(manager.building, _scanned) && Time.unscaledTime < _nextScan)
            {
                return;
            }
            _scanned = manager.building;
            _nextScan = Time.unscaledTime + 15f;
            Cookers.Clear();
            CoffeeMachines.Clear();
            Fridges.Clear();
            List<ItemController> items = manager.allItemControllers;
            for (int i = 0; items != null && i < items.Count; i++)
            {
                ItemController item = items[i];
                if (item == null || !item.isActiveAndEnabled || string.IsNullOrEmpty(item.itemName))
                {
                    continue;
                }
                if (item is FridgeController fridge)
                {
                    Fridges.Add(fridge);
                    continue;
                }
                string name = item.itemName;
                if (name.IndexOf("coffeemachine", StringComparison.Ordinal) >= 0)
                {
                    CoffeeMachines.Add(item);
                }
                else if (name.IndexOf("stove", StringComparison.Ordinal) >= 0 || name.IndexOf("pizzaoven", StringComparison.Ordinal) >= 0
                         || name.IndexOf("grill", StringComparison.Ordinal) >= 0)
                {
                    Cookers.Add(item);
                }
            }
        }

        /// <summary>The nearest of a list to a point, within a distance; null if none.</summary>
        public static ItemController Nearest(List<ItemController> items, Vector3 from, float within)
        {
            ItemController best = null;
            float bestSqr = within * within;
            for (int i = 0; i < items.Count; i++)
            {
                ItemController item = items[i];
                if (item == null || !item.isActiveAndEnabled)
                {
                    continue;
                }
                float sqr = (item.transform.position - from).sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    best = item;
                }
            }
            return best;
        }

        // ---------------------------------------------------------------- food

        /// <summary>
        /// What the fridges of this home hold that can be cooked with or eaten, all fridges together, one entry
        /// per kind of item. The game's own cargo is read, nothing is copied out of it but names and counts.
        /// </summary>
        public static void ReadPantry(List<PantryItem> into)
        {
            into.Clear();
            for (int f = 0; f < Fridges.Count; f++)
            {
                List<CargoInstance> cargo = Cargo(Fridges[f]);
                for (int i = 0; cargo != null && i < cargo.Count; i++)
                {
                    CargoInstance stack = cargo[i];
                    if (stack == null || stack.IsSealed || stack.amount <= 0 || stack.ItemCached == null)
                    {
                        continue;
                    }
                    FoodKind kind = Cooking.KindOf(stack.itemName);
                    if (kind == FoodKind.None)
                    {
                        continue;
                    }
                    PantryItem item = null;
                    for (int k = 0; k < into.Count; k++)
                    {
                        if (into[k].Id == stack.itemName)
                        {
                            item = into[k];
                            break;
                        }
                    }
                    if (item == null)
                    {
                        item = new PantryItem { Id = stack.itemName, Kind = kind, Saturation = stack.ItemCached.saturation };
                        into.Add(item);
                    }
                    item.Amount += stack.amount;
                }
            }
        }

        /// <summary>True if the fridges still hold everything the plan takes.</summary>
        public static bool Holds(CookPlan plan)
        {
            for (int u = 0; u < plan.Uses.Count; u++)
            {
                if (Stock(plan.Uses[u].Id) < plan.Uses[u].Amount)
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Takes what the plan uses out of the fridges, the way the game takes a unit when it is eaten. All of
        /// it or nothing: false, and nothing taken, if something is no longer there.
        /// </summary>
        public static bool Take(CookPlan plan)
        {
            if (!Holds(plan))
            {
                return false;
            }
            for (int u = 0; u < plan.Uses.Count; u++)
            {
                int left = plan.Uses[u].Amount;
                for (int f = 0; f < Fridges.Count && left > 0; f++)
                {
                    List<CargoInstance> cargo = Cargo(Fridges[f]);
                    for (int i = cargo != null ? cargo.Count - 1 : -1; i >= 0 && left > 0; i--)
                    {
                        CargoInstance stack = cargo[i];
                        if (stack == null || stack.IsSealed || stack.amount <= 0 || stack.itemName != plan.Uses[u].Id)
                        {
                            continue;
                        }
                        int take = Mathf.Min(left, stack.amount);
                        Fridges[f].ItemInstance.ReduceFromCargo(stack, take);   // an emptied stack leaves the list: hence backwards
                        left -= take;
                    }
                }
            }
            return true;
        }

        private static int Stock(string itemName)
        {
            int count = 0;
            for (int f = 0; f < Fridges.Count; f++)
            {
                List<CargoInstance> cargo = Cargo(Fridges[f]);
                for (int i = 0; cargo != null && i < cargo.Count; i++)
                {
                    if (cargo[i] != null && !cargo[i].IsSealed && cargo[i].amount > 0 && cargo[i].itemName == itemName)
                    {
                        count += cargo[i].amount;
                    }
                }
            }
            return count;
        }

        private static List<CargoInstance> Cargo(FridgeController fridge)
        {
            return fridge != null && fridge.ItemInstance != null ? fridge.ItemInstance.cargoInstances : null;
        }

        /// <summary>The nearest fridge to a point, within a distance; null if none.</summary>
        public static FridgeController NearestFridge(Vector3 from, float within)
        {
            FridgeController best = null;
            float bestSqr = within * within;
            for (int i = 0; i < Fridges.Count; i++)
            {
                FridgeController fridge = Fridges[i];
                if (fridge == null || !fridge.isActiveAndEnabled)
                {
                    continue;
                }
                float sqr = (fridge.transform.position - from).sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    best = fridge;
                }
            }
            return best;
        }

        // ---------------------------------------------------------------- floor

        /// <summary>
        /// A piece of floor (or pavement) with room all round that the player can walk to: where they stand
        /// if that will do, else the nearest of a few places around them. False if there is none within four metres.
        /// </summary>
        public static bool ClearFloor(PlayerController player, float room, out Vector3 spot)
        {
            spot = default;
            if (player == null || player.Character == null || player.Character.navmeshAgent == null)
            {
                return false;
            }
            NavMeshAgent agent = player.Character.navmeshAgent;
            var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
            Vector3 from = player.transform.position;
            if (Roomy(from, from, room, filter, out spot))
            {
                return true;
            }
            for (int ring = 1; ring <= 3; ring++)
            {
                float radius = 1.3f * ring;
                for (int i = 0; i < 8; i++)
                {
                    float angle = (i + ring * 0.5f) * Mathf.PI / 4f;
                    Vector3 at = from + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                    if (Roomy(from, at, room, filter, out spot))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private static bool Roomy(Vector3 from, Vector3 at, float room, NavMeshQueryFilter filter, out Vector3 spot)
        {
            spot = default;
            if (!NavMesh.SamplePosition(at, out NavMeshHit hit, 0.6f, filter) || Mathf.Abs(hit.position.y - from.y) > 1.2f)
            {
                return false;
            }
            if (NavMesh.FindClosestEdge(hit.position, out NavMeshHit edge, filter) && edge.distance < room)
            {
                return false;   // against furniture or a wall
            }
            if (_path == null)
            {
                _path = new NavMeshPath();
            }
            if (!NavMesh.SamplePosition(from, out NavMeshHit start, 1.5f, filter) || !NavMesh.CalculatePath(start.position, hit.position, filter, _path)
                || _path.status != NavMeshPathStatus.PathComplete)
            {
                return false;
            }
            spot = hit.position;
            return true;
        }

        /// <summary>
        /// The desk of the game's own doctor in the building the player is in (a SpecialEmployeeController; the
        /// doctor's if there are several desks), for the panel that offers a consultation. False if the building has
        /// no such desk. One pass over the building's furniture, when the player comes in.
        /// </summary>
        public static bool DoctorDesk(out Vector3 at)
        {
            at = default;
            var manager = InstanceBehavior<BuildingManager>.Instance;
            List<ItemController> items = manager != null ? manager.allItemControllers : null;
            SpecialEmployeeController found = null;
            for (int i = 0; items != null && i < items.Count; i++)
            {
                var desk = items[i] as SpecialEmployeeController;
                if (desk == null || !desk.isActiveAndEnabled)
                {
                    continue;
                }
                if (found == null || desk.GetEmployeeType == SpecialEmployeeController.SpecialEmployeeType.LongevityDoctor)
                {
                    found = desk;
                }
            }
            if (found == null)
            {
                return false;
            }
            at = found.transform.position;
            return true;
        }

        /// <summary>True in a hospital or a clinic.</summary>
        public static bool InHospital()
        {
            if (!BuildingManager.IsInsideBuilding)
            {
                return false;
            }
            var manager = InstanceBehavior<BuildingManager>.Instance;
            string service = manager != null && manager.building != null && manager.building.SpecialService != null
                ? manager.building.SpecialService.businessTypeName
                : null;
            return service == "ba:businesstype_hospital" || service == "ba:businesstype_clinic";
        }
    }
}
