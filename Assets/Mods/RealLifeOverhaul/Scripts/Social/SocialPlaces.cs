using System;
using Extensions;
using Helpers;
using Localizor;
using Streets;
using UnityEngine;
using UnityEngine.AI;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul
{
    /// <summary>Somewhere to meet: a business of the city's, the pavement in front of its door, what it is called.</summary>
    public struct Venue
    {
        public CityBuildingController Building;
        public Vector3 Front;      // where somebody waits: pavement, at street level, clear of vehicles
        public Vector3 Door;
        public string Name;        // the business's name, or its address
        public string Street;      // the game's own street name (not localised: it identifies the address in the save)
        public int Number;
        public bool Anywhere;      // no building at all: a stretch of pavement (a walk agreed "here", from the test button)

        public bool Valid => Building != null || Anywhere;
    }

    /// <summary>
    /// Where the social life happens: which of the city's businesses will do for an outing, the
    /// name of the street a thing happened on, the places on the pavement people wait at, walk
    /// in from and walk off to. Everything here is asked when a plan is made, a scene is staged
    /// or the paper is written; nothing is asked on a timer.
    /// </summary>
    public static class SocialPlaces
    {
        private const int Nearest = 3;

        private static readonly CityBuildingController[] Candidates = new CityBuildingController[Nearest];
        private static readonly float[] CandidateSqr = new float[Nearest];

        public static int DaysPerYear
        {
            get
            {
                GameInstance save = SaveGameManager.Current;
                int days = GameCalendar.DaysPerYear;
                return days < 8 ? 60 : days;
            }
        }

        public static int Districts => NeighborhoodHelper.Neighborhoods != null ? Mathf.Max(1, NeighborhoodHelper.Neighborhoods.Count) : 1;

        /// <summary>What a district of the cast's is called.</summary>
        public static string District(int index)
        {
            try
            {
                var all = NeighborhoodHelper.Neighborhoods;
                if (all != null && all.Count > 0)
                {
                    return all[Mathf.Abs(index) % all.Count].GetLocalization();
                }
            }
            catch (Exception)
            {
                // the game's list was not there: the plain word below
            }
            return "reallife_social_district_unknown".GetLocalization();
        }

        /// <summary>
        /// A place as the street knows it: indoors, the entrance of the building the player is in (an interior is a place of
        /// its own, and its coordinates say nothing about where in the city it is); out of doors, the place itself.
        /// </summary>
        public static Vector3 OnStreet(Vector3 where)
        {
            if (!BuildingManager.IsInsideBuilding)
            {
                return where;
            }
            try
            {
                BuildingManager manager = InstanceBehavior<BuildingManager>.Instance;
                Transform door = manager != null && manager.building != null ? BuildingHelper.GetAddressEntranceTransform(manager.building.Address) : null;
                return door != null ? door.position : where;
            }
            catch (Exception)
            {
                return where;
            }
        }

        /// <summary>The street a point is on, as the game writes it: that of the nearest building's address. A scan of the city: once per line of the paper.</summary>
        public static string StreetAt(Vector3 where)
        {
            try
            {
                CityManager city = InstanceBehavior<CityManager>.Instance;
                CityBuildingController[] buildings = city != null ? city.cityBuildingControllers : null;
                CityBuildingController best = null;
                float bestSqr = float.MaxValue;
                for (int i = 0; buildings != null && i < buildings.Length; i++)
                {
                    CityBuildingController building = buildings[i];
                    if (building == null || building.building == null)
                    {
                        continue;
                    }
                    Vector3 offset = building.transform.position - where;
                    float sqr = offset.x * offset.x + offset.z * offset.z;
                    if (sqr < bestSqr)
                    {
                        bestSqr = sqr;
                        best = building;
                    }
                }
                if (best != null && best.building.Address != null)
                {
                    string street = best.building.Address.GetStreetNameLocalized();
                    if (!string.IsNullOrEmpty(street))
                    {
                        return street;
                    }
                }
            }
            catch (Exception)
            {
                // a building without an address: the plain words below
            }
            return "reallife_news_street_unknown".GetLocalization();
        }

        /// <summary>
        /// A place for a kind of outing between two distances from a point: one of the three
        /// nearest that will do, so that it is not always the same one. Outings that need no
        /// particular business are agreed for "outside" any business with a name.
        /// </summary>
        public static bool Find(PlanKind kind, Vector3 around, float minDistance, float maxDistance, out Venue venue, CityBuildingController not = null)
        {
            venue = default;
            CityManager city = InstanceBehavior<CityManager>.Instance;
            CityBuildingController[] buildings = city != null ? city.cityBuildingControllers : null;
            if (buildings == null)
            {
                return false;
            }
            OutingDef def = SocialCatalogue.Outing(kind);
            int found = 0;
            float minSqr = minDistance * minDistance;
            float maxSqr = maxDistance * maxDistance;
            for (int i = 0; i < buildings.Length; i++)
            {
                CityBuildingController building = buildings[i];
                if (building == null || building == not || !Suits(building, kind, def))
                {
                    continue;
                }
                Transform door = DoorOf(building);
                if (door == null)
                {
                    continue;
                }
                Vector3 offset = door.position - around;
                float sqr = offset.x * offset.x + offset.z * offset.z;
                if (sqr < minSqr || sqr > maxSqr || Mathf.Abs(offset.y) > 8f)
                {
                    continue;
                }
                // Kept in order of distance, nearest first.
                int at = found < Nearest ? found : Nearest - 1;
                if (found >= Nearest && sqr >= CandidateSqr[at])
                {
                    continue;
                }
                while (at > 0 && CandidateSqr[at - 1] > sqr)
                {
                    Candidates[at] = Candidates[at - 1];
                    CandidateSqr[at] = CandidateSqr[at - 1];
                    at--;
                }
                Candidates[at] = building;
                CandidateSqr[at] = sqr;
                if (found < Nearest)
                {
                    found++;
                }
            }
            bool made = false;
            int first = found > 0 ? Random.Range(0, found) : 0;
            for (int i = 0; i < found && !made; i++)
            {
                made = Make(Candidates[(first + i) % found], out venue);
            }
            for (int i = 0; i < Nearest; i++)
            {
                Candidates[i] = null;
            }
            return made;
        }

        /// <summary>The venue of a plan read from the save. False if the building is no longer what it was.</summary>
        public static bool Resolve(string street, int number, out Venue venue)
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
                if (building == null || building.building == null)
                {
                    continue;
                }
                Address address = building.building.Address;
                if (address != null && address.streetNumber == number && address.streetName == street)
                {
                    return Make(building, out venue);
                }
            }
            return false;
        }

        /// <summary>True while the player could walk into the venue: it is open, and nothing has it shut.</summary>
        public static bool Open(in Venue venue)
        {
            try
            {
                return venue.Valid && venue.Building.building != null && BuildingHelper.CanEnterBuilding(venue.Building.building.Address)
                       && !BuildingManager.IsBuildingBlockedByAnyService(venue.Building.building.Address);
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// A place to stand near a point: the point itself if somebody can stand there with room,
        /// else pavement within a few metres that has it.
        /// </summary>
        public static bool Standing(Vector3 near, out Vector3 spot)
        {
            if (CitySpots.Pavement(near, 1.5f, out spot) && CitySpots.ClearQuietly(spot, 0.45f))
            {
                return true;
            }
            for (int i = 0; i < 10; i++)
            {
                if (CitySpots.PavementRing(near, 1.2f, 5f, false, out spot) && CitySpots.ClearQuietly(spot, 0.45f))
                {
                    return true;
                }
            }
            spot = default;
            return false;
        }

        /// <summary>
        /// Pavement the camera is not looking at from which the walk to a goal is short: the whole
        /// way can be walked, and the route (not the straight line: a goal across the road may be
        /// a block's walk away) is no longer than <paramref name="most"/> metres. The nearer ring
        /// (14 to 26 m) is asked first, then 22 to 40 m. Never beside the player.
        /// </summary>
        public static bool WalkIn(Vector3 goal, Vector3 player, float most, out Vector3 from, out float length)
        {
            for (int i = 0; i < 8; i++)
            {
                bool near = i < 5;
                if (CitySpots.PavementRing(goal, near ? 14f : 22f, near ? 26f : 40f, true, out from) && JobSpots.Flat(from, player) > 9f
                    && CitySpots.ClearQuietly(from, 0.4f) && Route(from, goal, out length) && length <= most)
                {
                    return true;
                }
            }
            from = default;
            length = 0f;
            return false;
        }

        /// <summary>
        /// The walk from one point of pavement to another, as the game's pedestrians would make it.
        /// False if it cannot be walked the whole way. No allocation after the first call.
        /// </summary>
        public static bool Route(Vector3 from, Vector3 to, out float length)
        {
            length = 0f;
            if (_route == null)
            {
                _route = new NavMeshPath();
                _walk = new NavMeshQueryFilter { agentTypeID = NavMeshHelper.NpcAgentTypeId, areaMask = NavMesh.AllAreas & ~NavMeshHelper.SwimmingAreaMask };
            }
            if (!NavMesh.CalculatePath(from, to, _walk, _route) || _route.status != NavMeshPathStatus.PathComplete)
            {
                return false;
            }
            length = Length(_route);
            return true;
        }

        /// <summary>The length of a route; one with more turns than are counted is "very long".</summary>
        public static float Length(NavMeshPath path)
        {
            int count = path.GetCornersNonAlloc(Corners);
            float length = count >= Corners.Length ? 1000f : 0f;
            for (int i = 1; i < count; i++)
            {
                length += Vector3.Distance(Corners[i - 1], Corners[i]);
            }
            return length;
        }

        /// <summary>How far a point is from the pedestrians' navigation mesh; negative if there is none within a metre.</summary>
        public static float OffMesh(Vector3 point)
        {
            if (_route == null)
            {
                Route(point, point, out _);
            }
            return NavMesh.SamplePosition(point, out NavMeshHit hit, 1f, _walk) ? Vector3.Distance(hit.position, point) : -1f;
        }

        /// <summary>True while the player is inside the venue itself (the one place indoors where a plan is being kept, not walked out on).</summary>
        public static bool InsideVenue(in Venue venue)
        {
            try
            {
                BuildingManager manager = InstanceBehavior<BuildingManager>.Instance;
                return BuildingManager.IsInsideBuilding && manager != null && venue.Building != null && ReferenceEquals(manager.building, venue.Building.building);
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// "Here": the pavement beside a point, with no business to it, called by the name of the
        /// street. For a walk agreed on the spot (the test button), wherever there is pavement.
        /// </summary>
        public static bool Here(Vector3 around, out Venue venue)
        {
            venue = default;
            if (!CitySpots.Pavement(around, 8f, out Vector3 pavement))
            {
                return false;
            }
            venue.Anywhere = true;
            venue.Front = pavement;
            venue.Door = pavement;
            venue.Name = StreetAt(around);
            return true;
        }

        private static readonly Vector3[] Corners = new Vector3[48];
        private static NavMeshPath _route;
        private static NavMeshQueryFilter _walk;

        /// <summary>Somewhere to walk off to: pavement 30 to 55 m away, out of view if there is any such, that can be walked to.</summary>
        public static bool WalkOff(Vector3 from, out Vector3 to)
        {
            for (int i = 0; i < 6; i++)
            {
                if (CitySpots.PavementRing(from, 30f, 55f, i < 4, out to) && CitySpots.Reachable(from, to))
                {
                    return true;
                }
            }
            to = default;
            return false;
        }

        private static bool Suits(CityBuildingController building, PlanKind kind, OutingDef def)
        {
            try
            {
                BuildingRegistration registration = building.buildingRegistration;
                if (registration == null || building.building == null || !registration.HasEstablishedBusiness
                    || building.building.locatedInsidePrivateFenceIndex >= 0 || registration.businessTypeName == null
                    || registration.businessTypeName == "ba:businesstype_empty" || !building.building.requiredDLC.DlcIsOwned())
                {
                    return false;
                }
                if (kind == PlanKind.Business)
                {
                    return GoingPublic.Mine(registration);
                }
                if (GoingPublic.Mine(registration))
                {
                    return false;
                }
                if (def.Match == null)
                {
                    return true;
                }
                for (int i = 0; i < def.Match.Length; i++)
                {
                    if (registration.businessTypeName.IndexOf(def.Match[i], StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return true;
                    }
                }
                return false;
            }
            catch (Exception)
            {
                return false;   // a building the game has no data for is no venue
            }
        }

        private static bool Make(CityBuildingController building, out Venue venue)
        {
            venue = default;
            Transform door = DoorOf(building);
            if (door == null || building.building == null || building.building.Address == null || !JobSpots.Front(building, out Vector3 front))
            {
                return false;
            }
            BuildingRegistration registration = building.buildingRegistration;
            venue.Building = building;
            venue.Front = front;
            venue.Door = door.position;
            venue.Street = building.building.Address.streetName;
            venue.Number = building.building.Address.streetNumber;
            venue.Name = registration != null && !string.IsNullOrEmpty(registration.BusinessName)
                ? registration.BusinessName
                : building.building.Address.ToFormattedString();
            return true;
        }

        private static Transform DoorOf(CityBuildingController building)
        {
            if (building.entranceDoors == null)
            {
                return null;
            }
            for (int i = 0; i < building.entranceDoors.Length; i++)
            {
                if (building.entranceDoors[i] != null && building.entranceDoors[i].doorTransform != null)
                {
                    return building.entranceDoors[i].doorTransform;
                }
            }
            return null;
        }
    }
}
