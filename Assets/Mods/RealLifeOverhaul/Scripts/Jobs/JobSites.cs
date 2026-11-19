using System;
using BAModAPI;
using BigAmbitions.Tags;
using Extensions;
using Helpers;
using Streets;
using UnityEngine;
using UnityEngine.AI;

namespace RealLifeOverhaul
{
    /// <summary>Where indoor jobs are worked: a client's floor, every place a reachable point on its navigation mesh with room round it.</summary>
    public static class JobSites
    {
        private const float Room = 0.6f;         // metres of clear floor wanted round a place somebody works at
        private const int Attempts = 24;
        private const int ClosingMargin = 15;   // game minutes clear of closing time

        /// <summary>Any business that is somebody else's and open; kept as a field so asking allocates nothing.</summary>
        public static readonly Predicate<CityBuildingController> AnyClient = IsClient;
        /// <summary>The same, and a shop with shelves and customers.</summary>
        public static readonly Predicate<CityBuildingController> ShopClient = IsShop;

        private static readonly System.Collections.Generic.List<CityBuildingController> Candidates = new System.Collections.Generic.List<CityBuildingController>(64);
        private static NavMeshPath _path;

        /// <summary>
        /// A nearby client open long enough for the walk and the floor, by hand or by the clock; or the key of the real reason there is none.
        /// The Gig Line asks this to offer the gig (<see cref="Probe"/>) and the gig asks it to begin, so the two never disagree. Asked only
        /// when a job is offered or begins.
        /// </summary>
        public static bool RandomClient(JobContext ctx, JobDef def, int units, float min, float max, float seconds, float secondsPerMetre,
            Predicate<CityBuildingController> only, out DoorSpot spot, out string why, Predicate<DoorSpot> place = null)
        {
            return Find(ctx.PlayerPosition, def, units, min, max, seconds, secondsPerMetre, only, place, ctx.Logger, out spot, out why);
        }

        /// <summary>Whether a gig could begin here and now as far as its client goes: the same search the gig makes, and the reason when not.</summary>
        public static bool Probe(Vector3 from, JobDef def, int units, float min, float max, float seconds, float secondsPerMetre,
            Predicate<CityBuildingController> only, Predicate<DoorSpot> place, out string why)
        {
            return Find(from, def, units, min, max, seconds, secondsPerMetre, only, place, null, out _, out why);
        }

        // Every building in reach is looked at once, nearest checks first, so a gig is refused only when no client fits; one is then
        // taken at random among those that do.
        private static bool Find(Vector3 from, JobDef def, int units, float min, float max, float seconds, float secondsPerMetre,
            Predicate<CityBuildingController> only, Predicate<DoorSpot> place, IModLogger log, out DoorSpot spot, out string why)
        {
            spot = default;
            why = ClientRules.NoneNear;
            CityBuildingController[] buildings = GameDlc.Open(InstanceBehavior<CityManager>.Instance);
            if (buildings == null)
            {
                return false;
            }
            float minSqr = min * min;
            float maxSqr = max * max;
            float speed = Mathf.Max(0.25f, PlayerPrefSettings.GameSpeed);
            Candidates.Clear();
            int near = 0;
            for (int i = 0; i < buildings.Length; i++)
            {
                CityBuildingController building = buildings[i];
                if (building == null || building.building == null || GameMembers.Fenced(building.building) || !JobSpots.DoorOf(building, out Vector3 door))
                {
                    continue;
                }
                Vector3 offset = door - from;
                offset.y = 0f;
                float sqr = offset.sqrMagnitude;
                if (sqr < minSqr || sqr > maxSqr || Mathf.Abs(door.y - from.y) > 6f || (only != null && !only(building)))
                {
                    continue;
                }
                near++;
                float needed = ClientRules.Needed(seconds, speed, Mathf.Sqrt(sqr), secondsPerMetre, units, ShiftRates.Minutes(def), ClosingMargin);
                float open = OpenMinutes(building.buildingRegistration);
                if (open < needed)
                {
                    if (log != null && Meta.Dev.Verbose)
                    {
                        log.Info("Jobs: '" + def.Id + "' client refused: " + NameOf(building) + " stays open for " + open.ToString("F0")
                                 + " game minutes; the walk, work, clock and margin need " + needed.ToString("F0") + ".");
                    }
                    continue;
                }
                Candidates.Add(building);
            }
            int openCount = Candidates.Count;
            while (Candidates.Count > 0)
            {
                int pick = UnityEngine.Random.Range(0, Candidates.Count);
                CityBuildingController building = Candidates[pick];
                Candidates[pick] = Candidates[Candidates.Count - 1];
                Candidates.RemoveAt(Candidates.Count - 1);
                if (!JobSpots.Front(building, out Vector3 front) || !JobSpots.DoorOf(building, out Vector3 door))
                {
                    continue;
                }
                var found = new DoorSpot { Front = front, Door = door, Building = building, Name = building.building.Address.ToFormattedString() };
                if (place != null && !place(found))
                {
                    continue;
                }
                Candidates.Clear();
                spot = found;
                why = null;
                return true;
            }
            why = ClientRules.Reason(near, openCount);
            if (log != null && Meta.Dev.Verbose)
            {
                log.Info("Jobs: '" + def.Id + "' no client: " + near + " of the kind within " + max.ToString("F0") + " m, " + openCount + " open long enough: " + why + ".");
            }
            return false;
        }

        private static string NameOf(CityBuildingController building)
        {
            BuildingRegistration registration = building.buildingRegistration;
            return registration != null && !string.IsNullOrEmpty(registration.BusinessName) ? registration.BusinessName : building.building.Address.ToFormattedString();
        }

        // Consecutive open hours from the game's schedule, including adjoining slots and days, stopping at the first gap.
        private static float OpenMinutes(BuildingRegistration registration)
        {
            if (registration == null || registration.temporarilyClosed || registration.scheduleDays == null)
            {
                return 0f;
            }
            int day = TimeHelper.CurrentDay;
            int hour = TimeHelper.CurrentHour;
            float minutes = 0f;
            for (int i = 0; i < 168; i++)   // at most a week, even for a business open all the time
            {
                ScheduleDay schedule = null;
                var weekday = TimeHelper.GetDayOfWeek(day);
                for (int d = 0; d < registration.scheduleDays.Count; d++)
                {
                    if (registration.scheduleDays[d] != null && registration.scheduleDays[d].day == weekday)
                    {
                        schedule = registration.scheduleDays[d];
                        break;
                    }
                }
                if (schedule == null || schedule.openingHourSlots == null || !schedule.IsOpenAtHour(hour))
                {
                    break;
                }
                minutes += i == 0 ? 60f - TimeHelper.CurrentMinute : 60f;
                if (++hour == 24)
                {
                    hour = 0;
                    day++;
                }
            }
            return minutes;
        }

        /// <summary>The client's current opening state and the game's entry restrictions, also checked while the player works inside.</summary>
        public static bool Available(in DoorSpot spot) => IsClient(spot.Building);

        /// <summary>Whole game minutes the client stays open from now by the game's schedule; 0 if it is shut or unknown. Asked when the clock starts.</summary>
        public static int MinutesOpen(in DoorSpot spot)
        {
            try
            {
                return spot.Building != null ? Mathf.FloorToInt(OpenMinutes(spot.Building.buildingRegistration)) : 0;
            }
            catch (Exception)
            {
                return 0;   // a building the game has no data for is nobody's client
            }
        }

        /// <summary>Uses the game's door and verifies that entry began; Interact's return value also means a refused door.</summary>
        public static bool Enter(in DoorSpot spot)
        {
            var uis = InstanceBehavior<UI.UIs>.Instance;
            if (uis == null || uis.gameSpeed.Paused || Inside(spot))
            {
                return true;
            }
            spot.Building.Interact();
            var premises = InstanceBehavior<BuildingManager>.Instance;
            return Inside(spot) || (premises != null && premises.enteringBuilding);
        }

        private static bool IsClient(CityBuildingController building)
        {
            try
            {
                BuildingRegistration registration = building != null ? building.buildingRegistration : null;
                return registration != null && building.building != null && !registration.RentedByPlayer
                       && registration.businessTypeName != "ba:businesstype_empty" && registration.HasEstablishedBusiness
                       && !GameMembers.Fenced(building.building)
                       && BuildingHelper.CanEnterBuilding(building.building.Address)
                       && registration.GetEntranceFeeForPlayer() <= 0f          // nobody pays to get in to work
                       && !GameDlc.Gated(building)
                       && !BuildingManager.IsBuildingBlockedByAnyService(building.building.Address);
            }
            catch (Exception)
            {
                return false;   // a building the game has no data for is nobody's client
            }
        }

        private static bool IsShop(CityBuildingController building)
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

        /// <summary>A clear place on the floor of the building the player is in, on their storey, walkable to and <paramref name="apart"/> from those taken.</summary>
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

        /// <summary>A standing place at a named shelf to face: as <see cref="FloorSpot"/>, less the room round it.</summary>
        public static bool ShelfSpot(Vector3 from, Vector3[] taken, int count, float apart, out Vector3 spot, out Vector3 shelf, out string itemName)
        {
            spot = default;
            shelf = default;
            itemName = null;
            if (!BuildingManager.IsInsideBuilding)
            {
                return false;
            }
            for (int i = 0; i < Attempts; i++)
            {
                if (!ShopFloor.RandomShelf(out Vector3 standAt, out Vector3 at, out _, out string name) || Mathf.Abs(standAt.y - from.y) > 1.5f
                    || !Apart(standAt, taken, count, apart) || !Reachable(from, standAt))
                {
                    continue;
                }
                spot = standAt;
                shelf = at;
                itemName = name;
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

        // True if someone on foot indoors can walk the whole way from one point to the other.
        private static bool Reachable(Vector3 from, Vector3 to)
        {
            if (_path == null)
            {
                _path = new NavMeshPath();   // one, reused
            }
            return NavMesh.SamplePosition(from, out NavMeshHit start, 2f, NavMeshHelper.NpcNavMeshFilter)
                   && NavMesh.CalculatePath(start.position, to, NavMeshHelper.NpcNavMeshFilter, _path)
                   && _path.status == NavMeshPathStatus.PathComplete;
        }
    }
}
