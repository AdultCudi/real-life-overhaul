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
        public Vector3 Front;      // where somebody waits: pavement at street level, clear of vehicles
        public Vector3 Door;
        public string Name;        // the business's name, or its address
        public string Street;      // the game's own street name, not localised: it identifies the address in the save
        public int Number;
        public bool Anywhere;      // no building: a stretch of pavement (a walk agreed "here", from the test button)

        public bool Valid => Building != null || Anywhere;
    }

    /// <summary>Where the social life happens: venues, streets, and pavement to wait at, walk in from and walk off to. Nothing on a timer.</summary>
    public static class SocialPlaces
    {
        private const int Nearest = 3;
        private const int SpotTries = 12;          // places to stand looked at round a door for one clear of other panels' places
        private const int BusyMost = 32;           // other panels' places counted round a door at most
        private const float BusyWithin = 16f;      // metres round the door within which they are counted: the widest reach (7.5 m) beyond the widest ring
        private const float WideRing = 8f;         // metres from the door the wider half of the tries reaches, where the street allows

        private static readonly float[] BusyX = new float[BusyMost];
        private static readonly float[] BusyZ = new float[BusyMost];
        private static readonly float[] BusyReach = new float[BusyMost];
        private static readonly Vector3[] Spots = new Vector3[SpotTries + 1];
        private static readonly float[] SpotX = new float[SpotTries + 1];
        private static readonly float[] SpotZ = new float[SpotTries + 1];

        private static readonly CityBuildingController[] Candidates = new CityBuildingController[Nearest];
        private static readonly float[] CandidateSqr = new float[Nearest];
        private static readonly Vector3[] Corners = new Vector3[48];
        private static NavMeshPath _route;
        private static NavMeshQueryFilter _walk;

        /// <summary>The game's year in days; 60 while its calendar gives fewer than eight.</summary>
        public static int DaysPerYear
        {
            get
            {
                int days = GameCalendar.DaysPerYear;
                return days < 8 ? 60 : days;
            }
        }

        public static int Districts => NeighborhoodHelper.Neighborhoods != null ? Mathf.Max(1, NeighborhoodHelper.Neighborhoods.Count) : 1;

        /// <summary>The game's neighbourhood at an index, wrapped round its list.</summary>
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
            }
            return "reallife_social_district_unknown".GetLocalization();
        }

        /// <summary>Indoors, the building's entrance: an interior's coordinates say nothing of where it is in the city.</summary>
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

        /// <summary>The street of the nearest building's address, as the game writes it. A scan of the city's buildings.</summary>
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
            }
            return "reallife_news_street_unknown".GetLocalization();
        }

        /// <summary>One of the three nearest places that will do for an outing between two distances, so that it is not always the same.</summary>
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

        /// <summary>The venue of a plan read from the save. False if the building is no longer a venue.</summary>
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

        /// <summary>Open, and nothing has it shut.</summary>
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

        /// <summary>Pavement with room to stand near a point: within 1.5 m of it, else in a ring of 1.2 to 5 m.</summary>
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
        /// As <see cref="Standing"/>, but clear of the places where the mod's other panels are offered (a subway entrance, a
        /// stop, a bike dock, a kiosk: Core/GameUi/PanelPlaces) where the street allows: the place nearest the door with room
        /// outside all their reaches, else the roomiest found (<see cref="GameUi.PanelPickRules.Pick"/>). A person met
        /// there then has the game's one box to themselves. <paramref name="room"/>: metres outside the nearest reach;
        /// <paramref name="busy"/>: how many such places are near. A few looks at the pavement, on an event.
        /// </summary>
        public static bool StandingClear(Vector3 near, out Vector3 spot, out float room, out int busy)
        {
            busy = GameUi.PanelPlaces.Near(near, BusyWithin, BusyX, BusyZ, BusyReach);
            room = float.MaxValue;
            if (busy == 0)
            {
                return Standing(near, out spot);
            }
            int count = 0;
            if (CitySpots.Pavement(near, 1.5f, out Vector3 at) && CitySpots.ClearQuietly(at, 0.45f))
            {
                count = Keep(near, at, count);
            }
            for (int i = 0; i < SpotTries; i++)
            {
                if (CitySpots.PavementRing(near, 1.2f, i < SpotTries / 2 ? 5f : WideRing, false, out at) && CitySpots.ClearQuietly(at, 0.45f))
                {
                    count = Keep(near, at, count);
                }
            }
            int best = GameUi.PanelPickRules.Pick(SpotX, SpotZ, count, BusyX, BusyZ, BusyReach, busy);
            if (best < 0)
            {
                spot = default;
                return false;
            }
            spot = Spots[best];
            room = GameUi.PanelPickRules.Room(SpotX[best], SpotZ[best], BusyX, BusyZ, BusyReach, busy);
            return true;
        }

        // Keeps a place to stand in order of its distance from the door, nearest first: the order of preference for the rule.
        private static int Keep(Vector3 door, Vector3 at, int count)
        {
            if (count >= Spots.Length)
            {
                return count;
            }
            float far = JobSpots.Flat(at, door);
            int i = count;
            while (i > 0 && JobSpots.Flat(Spots[i - 1], door) > far)
            {
                Spots[i] = Spots[i - 1];
                SpotX[i] = SpotX[i - 1];
                SpotZ[i] = SpotZ[i - 1];
                i--;
            }
            Spots[i] = at;
            SpotX[i] = at.x;
            SpotZ[i] = at.z;
            return count + 1;
        }

        /// <summary>Pavement out of view 14 to 40 m off from which the walk to a goal is at most <paramref name="most"/> metres of route.</summary>
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

        /// <summary>As the game's pedestrians would walk it; false unless it goes the whole way. No allocation after the first call.</summary>
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

        /// <summary>The length of a route in metres; one with more corners than are kept counts 1000 more.</summary>
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

        /// <summary>Metres to the pedestrians' navigation mesh; negative if none is within a metre.</summary>
        public static float OffMesh(Vector3 point)
        {
            if (_route == null)
            {
                Route(point, point, out _);
            }
            return NavMesh.SamplePosition(point, out NavMeshHit hit, 1f, _walk) ? Vector3.Distance(hit.position, point) : -1f;
        }

        /// <summary>Pavement within 8 m of a point, called by its street's name: a walk agreed on the spot.</summary>
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

        /// <summary>Pavement 30 to 55 m away that can be walked to, out of view if there is any.</summary>
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
                    || GameMembers.Fenced(building.building) || registration.businessTypeName == null
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

        internal static bool Make(CityBuildingController building, out Venue venue)
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
