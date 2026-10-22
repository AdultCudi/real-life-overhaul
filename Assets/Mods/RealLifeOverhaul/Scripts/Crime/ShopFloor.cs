using System.Collections.Generic;
using BigAmbitions.Items;
using BigAmbitions.Tags;
using Controllers;
using Extensions;
using UnityEngine;
using UnityEngine.AI;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The shop the player stands in, for the indoor scenes: whose it is, its doors, shelves and cameras, and its guard.
    /// Read from the game's objects when asked; nothing is kept or changed.
    /// </summary>
    internal static class ShopFloor
    {
        private static BuildingManager Building => BuildingManager.IsInsideBuilding ? InstanceBehavior<BuildingManager>.Instance : null;

        /// <summary>A shop of the player's own whose theft the game simulates.</summary>
        public static bool IsOwnShop
        {
            get
            {
                BuildingManager building = Building;
                return building != null && GoingPublic.Mine(building.buildingRegistration) && building.businessType != null
                       && building.businessType.HasTag(TagRef.Businesstag.allowtheft);
            }
        }

        /// <summary>Somebody else's shop, open, with customers in it.</summary>
        public static bool IsOtherShop
        {
            get
            {
                BuildingManager building = Building;
                return building != null && !GoingPublic.Mine(building.buildingRegistration) && building.isOpen && building.businessType != null
                       && building.businessType.spawnCustomers && building.businessType.HasTag(TagRef.Businesstag.allowtheft)
                       && IndoorCustomerSpawner.Customers.Count > 0;
            }
        }

        /// <summary>The registration of the player's shop they are standing in, or null.</summary>
        public static BuildingRegistration OwnRegistration => IsOwnShop ? Building.buildingRegistration : null;

        /// <summary>The door nearest a point: where people come in, and the point beyond it where they are gone.</summary>
        public static bool Door(Vector3 near, out Vector3 door, out Vector3 outside)
        {
            door = default;
            outside = default;
            BuildingManager building = Building;
            if (building == null || building.exitZones == null)
            {
                return false;
            }
            float best = float.MaxValue;
            bool found = false;
            List<ExitZone> exits = building.exitZones;
            for (int i = 0; i < exits.Count; i++)
            {
                ExitZone exit = exits[i];
                if (exit == null || exit.isElevator || exit.isDriveInBay)
                {
                    continue;
                }
                float sqr = (exit.transform.position - near).sqrMagnitude;
                if (sqr < best && Standable(exit.transform.position, 2.5f, out Vector3 inside))
                {
                    best = sqr;
                    door = inside;
                    outside = exit.despawner != null && Standable(exit.despawner.transform.position, 2f, out Vector3 beyond) ? beyond : inside;
                    found = true;
                }
            }
            return found;
        }

        /// <summary>A shelf picked at random: where to stand at it, whether a camera covers it, and what it is called.</summary>
        public static bool RandomShelf(out Vector3 standAt, out Vector3 shelf, out bool covered, out string itemName)
        {
            standAt = default;
            shelf = default;
            covered = false;
            itemName = null;
            BuildingManager building = Building;
            List<ItemController> items = building != null ? building.allItemControllers : null;
            if (items == null)
            {
                return false;
            }
            int seen = 0;
            ItemController picked = null;
            for (int i = 0; i < items.Count; i++)
            {
                ItemController item = items[i];
                if (item == null || item.Item == null || (item.Item.type & ItemType.ShowcaseShelf) == 0)
                {
                    continue;
                }
                if (Random.Range(0, ++seen) == 0)
                {
                    picked = item;   // each equally likely, in one pass
                }
            }
            if (picked == null || !Standable(picked.transform.position, 2f, out standAt))
            {
                return false;
            }
            shelf = picked.transform.position;
            covered = picked.ItemInstance != null && picked.ItemInstance.isSecured;
            itemName = picked.itemName;
            return true;
        }

        /// <summary>The security guard on the floor right now, or null.</summary>
        public static SecurityGuardEmployee Guard()
        {
            BuildingManager building = Building;
            List<ItemController> items = building != null ? building.allItemControllers : null;
            if (items == null)
            {
                return null;
            }
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i] is SecurityGuardLockerController locker && locker.employee is SecurityGuardEmployee guard
                    && !guard.IsAway && guard.employeeTpc != null && guard.employeeTpc.navmeshAgent != null)
                {
                    return guard;
                }
            }
            return null;
        }

        public static bool Standable(Vector3 near, float radius, out Vector3 spot)
        {
            bool found = NavMesh.SamplePosition(near, out NavMeshHit hit, radius, NavMeshHelper.NpcNavMeshFilter);
            spot = hit.position;
            return found;
        }

        /// <summary>A place on the shop floor from which a door can be reached.</summary>
        public static bool RandomSpot(out Vector3 spot)
        {
            return IndoorCustomerSpawner.GetRandomPositionInside(out spot) && Standable(spot, 1.5f, out spot);
        }

        /// <summary>A frightened or angry face over a few of the customers. Nothing about them is changed.</summary>
        public static void StartleCustomers(CharacterEmojiName emoji, int atMost)
        {
            ActorPool pool = ActorPool.Instance;
            List<Customer> customers = IndoorCustomerSpawner.Customers;
            if (pool == null || Building == null)
            {
                return;
            }
            int shown = 0;
            for (int i = 0; i < customers.Count && shown < atMost; i++)
            {
                Customer customer = customers[i];
                if (customer != null && !customer.isPlayer && customer.tpc != null && customer.isActiveAndEnabled)
                {
                    pool.ShowEmoji(customer.tpc, emoji, 4f);
                    shown++;
                }
            }
        }
    }
}
