using System;
using System.Collections.Generic;
using Buildings;
using Helpers;
using Streets;
using UI.Guiders;
using UnityEngine;
using UnityEngine.AI;

namespace RealLifeOverhaul.Family
{
    /// <summary>One of the city's own buildings that family life sends the player to.</summary>
    public struct Venue
    {
        public CityBuildingController Building;
        public Vector3 Door;
        public string Name;       // its business name, or its address
        public string Street;
        public int Number;

        public bool Valid => Building != null;
    }

    /// <summary>
    /// The real places of family life (a jeweller, a planner, a hospital, a school, a theatre, a restaurant, home), each one of
    /// the city's own buildings found by its business type, and floor spots indoors. Asked on events and once a second while an
    /// errand is on.
    /// </summary>
    public static class FamilyPlaces
    {
        public const string Residential = "ba:buildingtype_residential";

        // Parts of the game's business type names. A list is tried in order: the first kind that the city has wins.
        public static readonly string[] Jeweller = { "jewelrystore" };
        public static readonly string[] Planner = { "eventplanningagency" };
        public static readonly string[] Florist = { "florist" };
        public static readonly string[] Hospital = { "hospital", "clinic" };
        public static readonly string[] School = { "school" };
        public static readonly string[] Theatre = { "theater", "cinema" };
        public static readonly string[] Restaurant = { "restaurant", "coffeeshop" };
        public static readonly string[] CityHall = { "businesstype_irs", "lawfirm", "bank" };   // the city has no town hall: the tax office stands in as the registry office
        public static readonly string[] Agency = { "lawfirm", "businesstype_irs" };              // where adoption papers are filed
        public static readonly string[] Furniture = { "furniturestore" };
        public static readonly string[] BabyShop = { "giftshop", "clothingstore" };
        public static readonly string[] Supplies = { "supermarket", "fruitandvegetablestore", "giftshop" };

        private static bool _guiding;

        // What the game calls the business in a building: its special service if it is one, else its registered business.
        private static string TypeOf(CityBuildingController building)
        {
            if (building == null || building.building == null)
            {
                return null;
            }
            if (building.building.SpecialService != null && !string.IsNullOrEmpty(building.building.SpecialService.businessTypeName))
            {
                return building.building.SpecialService.businessTypeName;
            }
            BuildingRegistration registration = building.buildingRegistration;
            return registration != null ? registration.businessTypeName : null;
        }

        private static bool Matches(string type, string part)
        {
            return !string.IsNullOrEmpty(type) && type.IndexOf(part, StringComparison.Ordinal) >= 0;
        }

        private static bool Matches(string type, string[] kinds)
        {
            for (int i = 0; i < kinds.Length; i++)
            {
                if (Matches(type, kinds[i]))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>The nearest building of the first kind in the list the city has. One pass over the city's buildings: for events only.</summary>
        public static bool Nearest(string[] kinds, Vector3 from, out Venue venue)
        {
            venue = default;
            CityManager city = InstanceBehavior<CityManager>.Instance;
            CityBuildingController[] buildings = city != null ? city.cityBuildingControllers : null;
            if (buildings == null)
            {
                return false;
            }
            for (int k = 0; k < kinds.Length; k++)
            {
                CityBuildingController best = null;
                Vector3 bestDoor = default;
                float bestSqr = float.MaxValue;
                for (int i = 0; i < buildings.Length; i++)
                {
                    CityBuildingController building = buildings[i];
                    if (building == null || building.building == null || GameMembers.Fenced(building.building)
                        || !Matches(TypeOf(building), kinds[k]) || !Door(building, out Vector3 door))
                    {
                        continue;
                    }
                    BuildingRegistration registration = building.buildingRegistration;
                    if (registration != null && GoingPublic.Mine(registration) && building.building.SpecialService == null)
                    {
                        continue;   // the player's own shop is not where they go to be served
                    }
                    float sqr = (door - from).sqrMagnitude;
                    if (sqr < bestSqr)
                    {
                        bestSqr = sqr;
                        best = building;
                        bestDoor = door;
                    }
                }
                if (best != null)
                {
                    venue = Make(best, bestDoor);
                    return true;
                }
            }
            return false;
        }

        /// <summary>The building at an address, as a venue. False if the city has no such building (any more).</summary>
        public static bool At(string street, int number, out Venue venue)
        {
            venue = default;
            CityManager city = InstanceBehavior<CityManager>.Instance;
            CityBuildingController[] buildings = city != null ? city.cityBuildingControllers : null;
            if (buildings == null || string.IsNullOrEmpty(street))
            {
                return false;
            }
            for (int i = 0; i < buildings.Length; i++)
            {
                CityBuildingController building = buildings[i];
                if (building != null && building.building != null && building.building.StreetNumber == number && building.building.StreetName == street
                    && Door(building, out Vector3 door))
                {
                    venue = Make(building, door);
                    return true;
                }
            }
            return false;
        }

        private static Venue Make(CityBuildingController building, Vector3 door)
        {
            BuildingRegistration registration = building.buildingRegistration;
            string name = registration != null && !string.IsNullOrEmpty(registration.BusinessName) ? registration.BusinessName
                : building.building.SpecialService != null && !string.IsNullOrEmpty(building.building.SpecialService.businessName)
                    ? FamilyText.T(building.building.SpecialService.businessName)
                    : building.building.Address.ToFormattedString();
            return new Venue { Building = building, Door = door, Name = name, Street = building.building.StreetName, Number = building.building.StreetNumber };
        }

        private static bool Door(CityBuildingController building, out Vector3 door)
        {
            door = default;
            if (building.entranceDoors == null)
            {
                return false;
            }
            for (int i = 0; i < building.entranceDoors.Length; i++)
            {
                if (building.entranceDoors[i] != null && building.entranceDoors[i].doorTransform != null)
                {
                    door = building.entranceDoors[i].doorTransform.position;
                    return true;
                }
            }
            return false;
        }

        /// <summary>True while the player stands inside a building of one of these kinds.</summary>
        public static bool Inside(string[] kinds)
        {
            if (!BuildingManager.IsInsideBuilding)
            {
                return false;
            }
            var manager = InstanceBehavior<BuildingManager>.Instance;
            if (manager == null || manager.building == null)
            {
                return false;
            }
            string type = manager.building.SpecialService != null && !string.IsNullOrEmpty(manager.building.SpecialService.businessTypeName)
                ? manager.building.SpecialService.businessTypeName
                : manager.buildingRegistration != null ? manager.buildingRegistration.businessTypeName : null;
            return Matches(type, kinds);
        }

        /// <summary>True while the player is inside that very building.</summary>
        public static bool Inside(in Venue venue)
        {
            return BuildingManager.IsInsideBuilding && venue.Building != null
                   && ReferenceEquals(InstanceBehavior<BuildingManager>.Instance.building, venue.Building.building);
        }

        /// <summary>True while the player is on foot outside within a few steps of its door.</summary>
        public static bool AtDoor(in Venue venue, Vector3 player, float within = 7f)
        {
            if (!venue.Valid || BuildingManager.IsInsideBuilding || PlayerHelper.IsUsingVehicle)
            {
                return false;
            }
            Vector3 offset = venue.Door - player;
            return Mathf.Abs(offset.y) < 4f && offset.x * offset.x + offset.z * offset.z < within * within;
        }

        /// <summary>At its door or inside it (or inside any building of its kind: one branch of a clinic is as good as another).</summary>
        public static bool Reached(in Venue venue, string[] kinds, Vector3 player)
        {
            return AtDoor(venue, player) || Inside(venue) || (kinds != null && Inside(kinds));
        }

        /// <summary>Points the game's own destination marker at a venue. It can be dismissed like any destination.</summary>
        public static void Guide(in Venue venue)
        {
            try
            {
                if (venue.Valid && InstanceBehavior<GuidersManager>.Instance != null)
                {
                    GuidersManager.SetGuiderTarget(venue.Building.building.Address, DirectionGuiderType.Destination);
                    _guiding = true;
                }
            }
            catch (Exception e)
            {
                FamilyDirector.Logger?.Error(e);
            }
        }

        /// <summary>The same for a place that is not a building (a wedding in the open).</summary>
        public static void Guide(Vector3 place, string name)
        {
            try
            {
                if (InstanceBehavior<GuidersManager>.Instance != null)
                {
                    GuidersManager.SetGuiderTarget(place + Vector3.up * 3f, name, FamilyPhone.Icon, new Color(0.93f, 0.42f, 0.55f), DirectionGuiderType.Destination);
                    _guiding = true;
                }
            }
            catch (Exception e)
            {
                FamilyDirector.Logger?.Error(e);
            }
        }

        /// <summary>Takes the marker away again, if family life was the one that set it.</summary>
        public static void Unguide()
        {
            if (!_guiding)
            {
                return;
            }
            _guiding = false;
            try
            {
                GuidersManager.ResetGuider(DirectionGuiderType.Destination);
            }
            catch (Exception e)
            {
                FamilyDirector.Logger?.Error(e);
            }
        }

        public static void Reset()
        {
            _guiding = false;
        }

        /// <summary>The home the family lives in: the one named in the state while the player still rents it, else the first home they rent.</summary>
        public static BuildingRegistration Home()
        {
            try
            {
                List<BuildingRegistration> all = SaveGameManager.Current != null ? SaveGameManager.Current.BuildingRegistrations : null;
                BuildingRegistration first = null;
                FamilySave s = FamilyState.S;
                for (int i = 0; all != null && i < all.Count; i++)
                {
                    BuildingRegistration r = all[i];
                    if (r == null || !GoingPublic.Mine(r) || r.GetBuildingType() != Residential)
                    {
                        continue;
                    }
                    Building b = BuildingHelper.GetBuilding(r.Address);
                    if (b != null && !string.IsNullOrEmpty(s.HomeStreet) && b.StreetName == s.HomeStreet && b.StreetNumber == s.HomeNumber)
                    {
                        return r;
                    }
                    if (first == null)
                    {
                        first = r;
                    }
                }
                return first;
            }
            catch (Exception)
            {
                return null;   // a registration the game has no building for is nobody's home
            }
        }

        /// <summary>Floor area of a home in square metres; 0 if the game does not say.</summary>
        public static int Sqm(BuildingRegistration home)
        {
            try
            {
                Building b = home != null ? BuildingHelper.GetBuilding(home.Address) : null;
                return b != null ? b.totalSqm : 0;
            }
            catch (Exception)
            {
                return 0;
            }
        }

        /// <summary>Whether a home has room for the household plus so many more. A home whose size is unknown has room.</summary>
        public static bool HasRoom(BuildingRegistration home, int extraChildren)
        {
            if (home == null)
            {
                return false;
            }
            int sqm = Sqm(home);
            return sqm <= 0 || sqm >= FamilyBalance.SqmForTwo + FamilyBalance.SqmPerChild * (FamilyState.AtHome() + extraChildren);
        }

        public static void MakeHome(BuildingRegistration home)
        {
            Building b = home != null ? BuildingHelper.GetBuilding(home.Address) : null;
            if (b != null)
            {
                FamilyState.S.HomeStreet = b.StreetName;
                FamilyState.S.HomeNumber = b.StreetNumber;
            }
        }

        /// <summary>True while the player stands inside the family's home.</summary>
        public static bool AtHome()
        {
            if (!BuildingManager.IsInsideBuilding)
            {
                return false;
            }
            var manager = InstanceBehavior<BuildingManager>.Instance;
            if (manager == null || manager.building == null || !GoingPublic.Mine(manager.buildingRegistration) || manager.building.BuildingType != Residential)
            {
                return false;
            }
            FamilySave s = FamilyState.S;
            return string.IsNullOrEmpty(s.HomeStreet) || (manager.building.StreetName == s.HomeStreet && manager.building.StreetNumber == s.HomeNumber)
                   || ReferenceEquals(Home(), manager.buildingRegistration);
        }

        private static NavMeshPath _path;

        /// <summary>
        /// A spot on the floor near a point, on the player's storey: on the navigation mesh (the furniture is cut out of it), with
        /// so much room to its edge, and walkable to from the player. The agent type is the one whose mesh was found there.
        /// </summary>
        public static bool Floor(Vector3 near, Vector3 player, float room, float reach, out Vector3 spot, out int agentType)
        {
            spot = default;
            agentType = NavMeshHelperIds.Npc;
            for (int pass = 0; pass < 2; pass++)
            {
                int type = pass == 0 ? NavMeshHelperIds.Npc : NavMeshHelperIds.Player;
                var filter = new NavMeshQueryFilter { agentTypeID = type, areaMask = NavMesh.AllAreas };
                if (!NavMesh.SamplePosition(near, out NavMeshHit hit, reach, filter) || Mathf.Abs(hit.position.y - player.y) > 1.2f)
                {
                    continue;
                }
                if (NavMesh.FindClosestEdge(hit.position, out NavMeshHit edge, filter) && edge.distance < room)
                {
                    // Too close to the furniture or the wall: a step away from that edge.
                    Vector3 away = hit.position - edge.position;
                    away.y = 0f;
                    if (away.sqrMagnitude < 0.0001f)
                    {
                        away = hit.position - near;   // on the edge itself: out from what the point is beside
                        away.y = 0f;
                    }
                    if (away.sqrMagnitude < 0.0001f || !NavMesh.SamplePosition(hit.position + away.normalized * (room - edge.distance + 0.05f), out hit, 0.4f, filter)
                        || (NavMesh.FindClosestEdge(hit.position, out edge, filter) && edge.distance < room * 0.8f))
                    {
                        continue;
                    }
                }
                if (_path == null)
                {
                    _path = new NavMeshPath();
                }
                if (!NavMesh.SamplePosition(player, out NavMeshHit start, 1.5f, filter) || !NavMesh.CalculatePath(start.position, hit.position, filter, _path)
                    || _path.status != NavMeshPathStatus.PathComplete)
                {
                    continue;
                }
                spot = hit.position;
                agentType = type;
                return true;
            }
            return false;
        }
    }

    /// <summary>The game's two navigation agent types, under names of the mod's own.</summary>
    internal static class NavMeshHelperIds
    {
        public const int Npc = 0;
        public const int Player = 1479372276;
    }
}
