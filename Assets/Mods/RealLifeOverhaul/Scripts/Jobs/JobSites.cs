using System;
using Extensions;
using BigAmbitions.Tags;
using Helpers;
using UnityEngine;
using UnityEngine.AI;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Where the jobs that go indoors are worked: a client's premises. A client is one of the
    /// city's own businesses that is not the player's and is open right now, so the player
    /// walks in through its door the way any customer does; inside, the floor is the game's own
    /// interior and its navigation mesh, which the furniture is already cut out of. Every place
    /// handed out here is a point on that mesh with room round it that the player can walk to
    /// from where they stand, or "no". Asked when a shift is set up or a new task is wanted,
    /// never on a timer.
    /// </summary>
    public static class JobSites
    {
        private const float Room = 0.6f;         // clear floor wanted round a place somebody works at
        private const int Attempts = 24;

        /// <summary>Any business that is somebody else's and open: kept as fields so that asking allocates nothing.</summary>
        public static readonly Predicate<CityBuildingController> AnyClient = IsClient;
        /// <summary>The same, and a shop with shelves and customers.</summary>
        public static readonly Predicate<CityBuildingController> ShopClient = IsShop;

        private static NavMeshPath _path;

        public static bool IsClient(CityBuildingController building)
        {
            try
            {
                BuildingRegistration registration = building != null ? building.buildingRegistration : null;
                return registration != null && building.building != null && !registration.RentedByPlayer
                       && registration.businessTypeName != "ba:businesstype_empty" && registration.HasEstablishedBusiness
                       && building.building.locatedInsidePrivateFenceIndex < 0
                       && BuildingHelper.CanEnterBuilding(building.building.Address)
                       && registration.GetEntranceFeeForPlayer() <= 0f          // nobody pays to get in to work
                       && building.building.requiredDLC.DlcIsOwned()
                       && !BuildingManager.IsBuildingBlockedByAnyService(building.building.Address);
            }
            catch (Exception)
            {
                return false;   // a building the game has no data for is nobody's client
            }
        }

        public static bool IsShop(CityBuildingController building)
        {
            if (!IsClient(building))
            {
                return false;
            }
            try
            {
                BusinessType type = BusinessTypeHelper.GetData(building.buildingRegistration);
                return type != null && type.spawnCustomers && type.HasTag(TagRef.Businesstag.allowtheft);
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>What a client is called: its business name, or its address when it has none.</summary>
        public static string NameOf(in DoorSpot spot)
        {
            BuildingRegistration registration = spot.Building != null ? spot.Building.buildingRegistration : null;
            return registration != null && !string.IsNullOrEmpty(registration.BusinessName) ? registration.BusinessName : spot.Name;
        }

        /// <summary>True while the player stands inside that client's premises.</summary>
        public static bool Inside(in DoorSpot spot)
        {
            return BuildingManager.IsInsideBuilding && spot.Building != null
                   && ReferenceEquals(InstanceBehavior<BuildingManager>.Instance.building, spot.Building.building);
        }

        /// <summary>
        /// A place on the floor of the building the player is in: clear all round, on their
        /// storey, walkable to from where they stand, and at least so far from each of the
        /// places already taken. False if the floor has no such place left.
        /// </summary>
        public static bool FloorSpot(Vector3 from, Vector3[] taken, int count, float apart, out Vector3 spot)
        {
            spot = default;
            if (!BuildingManager.IsInsideBuilding)
            {
                return false;
            }
            for (int i = 0; i < Attempts; i++)
            {
                if (!ShopFloor.RandomSpot(out Vector3 found) || !Good(from, found, taken, count, apart))
                {
                    continue;
                }
                spot = found;
                return true;
            }
            return false;
        }

        /// <summary>The standing place at one of the shop's shelves, with the shelf to face. The same tests as <see cref="FloorSpot"/>, less the room (a shelf is at one's elbow).</summary>
        public static bool ShelfSpot(Vector3 from, Vector3[] taken, int count, float apart, out Vector3 spot, out Vector3 shelf)
        {
            spot = default;
            shelf = default;
            if (!BuildingManager.IsInsideBuilding)
            {
                return false;
            }
            for (int i = 0; i < Attempts; i++)
            {
                if (!ShopFloor.RandomShelf(out Vector3 standAt, out Vector3 at, out _, out _) || Mathf.Abs(standAt.y - from.y) > 1.5f
                    || !Apart(standAt, taken, count, apart) || !Reachable(from, standAt))
                {
                    continue;
                }
                spot = standAt;
                shelf = at;
                return true;
            }
            return false;
        }

        private static bool Good(Vector3 from, Vector3 found, Vector3[] taken, int count, float apart)
        {
            if (Mathf.Abs(found.y - from.y) > 1.5f || !Apart(found, taken, count, apart))
            {
                return false;
            }
            if (NavMesh.FindClosestEdge(found, out NavMeshHit edge, NavMeshHelper.NpcNavMeshFilter) && edge.distance < Room)
            {
                return false;   // against furniture or a wall
            }
            return Reachable(from, found);
        }

        private static bool Apart(Vector3 found, Vector3[] taken, int count, float apart)
        {
            for (int i = 0; taken != null && i < count; i++)
            {
                if (JobSpots.Flat(found, taken[i]) < apart)
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>True if someone on foot indoors can walk the whole way from one point to the other.</summary>
        public static bool Reachable(Vector3 from, Vector3 to)
        {
            if (_path == null)
            {
                _path = new NavMeshPath();   // one, reused: asking allocates nothing after the first time
            }
            return NavMesh.SamplePosition(from, out NavMeshHit start, 2f, NavMeshHelper.NpcNavMeshFilter)
                   && NavMesh.CalculatePath(start.position, to, NavMeshHelper.NpcNavMeshFilter, _path)
                   && _path.status == NavMeshPathStatus.PathComplete;
        }
    }
}
