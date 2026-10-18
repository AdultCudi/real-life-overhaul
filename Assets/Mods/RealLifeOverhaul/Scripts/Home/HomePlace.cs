using System;
using System.Collections.Generic;
using BigAmbitions.Items;
using UnityEngine;
using UnityEngine.AI;

namespace RealLifeOverhaul
{
    /// <summary>The cookers, coffee machines and fridges of the building the player is in, the food in the fridges, and clear floor.</summary>
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

        /// <summary>Takes what the plan uses out of the fridges, as the game takes a unit that is eaten. All or nothing: false if something has gone.</summary>
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

        /// <summary>Clear floor the player can walk to, with room all round: where they stand if it will do, else the nearest of a few places within four metres.</summary>
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

        /// <summary>The game's own doctor's desk in this building (the longevity doctor's, if there are several); false if there is none.</summary>
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
