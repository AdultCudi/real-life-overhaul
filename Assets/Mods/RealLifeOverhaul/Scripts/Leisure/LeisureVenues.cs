using System;
using System.Collections.Generic;
using Buildings;
using Controllers;
using Enums;
using Helpers;
using PlayerActivity;
using PlayerActivity.Tennis;
using Streets;
using UnityEngine;
using UnityEngine.AI;

namespace RealLifeOverhaul.Leisure
{
    /// <summary>One place worth seeing: found once per city load from the game's own scene objects.</summary>
    public sealed class Landmark
    {
        public string Id;
        public string NameKey;
        public Vector3 Position;
        public float Radius;
        public bool Waterside;    // a harbour, a pier, the shore: a line can be cast near here
    }

    /// <summary>
    /// What the game's city has that leisure builds on, found once and kept: the things inside
    /// the building the player is in (the cocktail bars and dance floors of a nightclub, the
    /// wardrobes of a home, a clothing store's racks), the landmarks of the city, and whether
    /// the player stands at the water's edge. Nothing here runs by itself: the director asks on
    /// its one-second timer, and the costly parts (a pass over the building's items, the scene
    /// search for landmarks) happen once per building entered and once per city load.
    /// </summary>
    public static class LeisureVenues
    {
        public const string ClothingStore = "ba:businesstype_clothingstore";
        public const string Nightclub = "ba:businesstype_nightclub";
        public const string Cinema = "ba:businesstype_cinema";
        public const string Theater = "ba:businesstype_theater";
        public const string Residential = "ba:buildingtype_residential";

        public static readonly List<ItemController> Bars = new List<ItemController>(8);
        public static readonly List<ItemController> DanceFloors = new List<ItemController>(8);
        public static readonly List<ItemController> Wardrobes = new List<ItemController>(8);
        public static readonly List<ItemController> Racks = new List<ItemController>(16);
        public static readonly List<Landmark> Landmarks = new List<Landmark>(20);

        private static Building _scanned;
        private static int _scannedCount = -1;
        private static bool _landmarksTried;
        private static int _waterMask;

        public static void Reset()
        {
            Bars.Clear();
            DanceFloors.Clear();
            Wardrobes.Clear();
            Racks.Clear();
            Landmarks.Clear();
            _scanned = null;
            _scannedCount = -1;
            _landmarksTried = false;
        }

        // ---------------------------------------------------------------- indoors

        /// <summary>The business type of the building the player is in, or null out of doors.</summary>
        public static string BusinessIn(BuildingManager manager)
        {
            return manager != null && manager.building != null && manager.buildingRegistration != null ? manager.buildingRegistration.businessTypeName : null;
        }

        /// <summary>A home: a residential building, which the player can only be inside if it is theirs.</summary>
        public static bool IsHome(BuildingManager manager)
        {
            return manager != null && manager.building != null && manager.building.BuildingType == Residential;
        }

        /// <summary>
        /// Sorts the items of the building the player is in into the lists above. One pass, made again only when the
        /// building or the number of its items changes (an interior fills in over the first moments inside).
        /// </summary>
        public static void ScanIndoors(BuildingManager manager)
        {
            if (manager == null || manager.building == null)
            {
                if (_scanned != null)
                {
                    Bars.Clear();
                    DanceFloors.Clear();
                    Wardrobes.Clear();
                    Racks.Clear();
                    _scanned = null;
                    _scannedCount = -1;
                }
                return;
            }
            List<ItemController> items = manager.allItemControllers;
            int count = items != null ? items.Count : 0;
            if (ReferenceEquals(manager.building, _scanned) && count == _scannedCount)
            {
                return;
            }
            _scanned = manager.building;
            _scannedCount = count;
            Bars.Clear();
            DanceFloors.Clear();
            Wardrobes.Clear();
            Racks.Clear();
            for (int i = 0; i < count; i++)
            {
                ItemController item = items[i];
                string name = item != null ? item.itemName : null;
                if (name == null)
                {
                    continue;
                }
                if (name.IndexOf("cocktailbar", StringComparison.Ordinal) >= 0)
                {
                    Bars.Add(item);
                }
                else if (name.IndexOf("dancefloor", StringComparison.Ordinal) >= 0)
                {
                    DanceFloors.Add(item);
                }
                else if (name.IndexOf("itemname_wardrobe", StringComparison.Ordinal) >= 0 || name.IndexOf("itemname_dresser", StringComparison.Ordinal) >= 0)
                {
                    Wardrobes.Add(item);
                }
                else if (name.IndexOf("clothingrack", StringComparison.Ordinal) >= 0)
                {
                    Racks.Add(item);
                }
            }
        }

        /// <summary>The nearest of a list to a point, within a distance on the flat. Null if none.</summary>
        public static ItemController Nearest(List<ItemController> items, Vector3 from, float within)
        {
            ItemController best = null;
            float bestSqr = within * within;
            for (int i = 0; i < items.Count; i++)
            {
                ItemController item = items[i];
                if (item == null || !item.gameObject.activeInHierarchy)
                {
                    continue;
                }
                Vector3 to = item.transform.position - from;
                to.y = 0f;
                if (to.sqrMagnitude < bestSqr)
                {
                    bestSqr = to.sqrMagnitude;
                    best = item;
                }
            }
            return best;
        }

        // ---------------------------------------------------------------- landmarks

        /// <summary>
        /// Finds the city's landmarks from the scene, once per city load (a second call does nothing). The scene
        /// searches here are the one-off setup the performance rules allow; whatever the city does not have is
        /// simply not on the list.
        /// </summary>
        private const string LandmarkSearch = "leisure: one of the city's landmarks being looked for (a search of the whole scene, six of them once a city)";

        public static void FindLandmarks()
        {
            if (_landmarksTried)
            {
                return;
            }
            _landmarksTried = true;
            try
            {
                Scene<FerrisWheel>("ferris_wheel", "reallife_leisure_landmark_ferris_wheel", 55f, true);
                Scene<GolfCourse>("golf_course", "reallife_leisure_landmark_golf_course", 70f, false);
                Scene<TennisCourtManager>("tennis_courts", "reallife_leisure_landmark_tennis_courts", 45f, false);
                Scene<SwimmingPoolBuildingController>("swimming_pool", "reallife_leisure_landmark_swimming_pool", 40f, false);
                Scene<BridgeController>("bridge", "reallife_leisure_landmark_bridge", 70f, true);

                CasinoBuildingController[] casinos = SceneFinds.All<CasinoBuildingController>(LandmarkSearch, FindObjectsInactive.Include);
                if (casinos.Length > 0 && casinos[0] != null)
                {
                    Transform at = casinos[0].ticketHouse != null ? casinos[0].ticketHouse.transform : casinos[0].transform;
                    Add("casino_pier", "reallife_leisure_landmark_casino_pier", at.position, 45f, true);
                }

                CityManager city = InstanceBehavior<CityManager>.Instance;
                if (city != null && city.subwayStations != null)
                {
                    foreach (SubwayStation station in city.subwayStations)
                    {
                        if (station == null)
                        {
                            continue;
                        }
                        switch (station.stationName)
                        {
                            case SubwayStationName.MidtownTimesSquareStation: Add("times_square", "reallife_leisure_landmark_times_square", station.transform.position, 45f, false); break;
                            case SubwayStationName.MidtownStPatrickStation: Add("st_patrick", "reallife_leisure_landmark_st_patrick", station.transform.position, 45f, false); break;
                            case SubwayStationName.MidtownMadisonParkStation: Add("madison_park", "reallife_leisure_landmark_madison_park", station.transform.position, 45f, false); break;
                            case SubwayStationName.IndustryCitySunsetParkStation: Add("sunset_park", "reallife_leisure_landmark_sunset_park", station.transform.position, 45f, false); break;
                            case SubwayStationName.GarmentDistrictHarborStation: Add("garment_harbor", "reallife_leisure_landmark_garment_harbor", station.transform.position, 50f, true); break;
                            case SubwayStationName.IndustryCityHarborStation: Add("industry_harbor", "reallife_leisure_landmark_industry_harbor", station.transform.position, 50f, true); break;
                            case SubwayStationName.TheHamptonsStation: Add("hamptons", "reallife_leisure_landmark_hamptons", station.transform.position, 60f, true); break;
                            case SubwayStationName.LowerManhattanCenterStation: Add("lower_manhattan", "reallife_leisure_landmark_lower_manhattan", station.transform.position, 45f, false); break;
                            case SubwayStationName.HellsKitchenNorthStation: Add("hells_kitchen", "reallife_leisure_landmark_hells_kitchen", station.transform.position, 45f, false); break;
                            case SubwayStationName.MurrayHillNorthStation: Add("murray_hill", "reallife_leisure_landmark_murray_hill", station.transform.position, 45f, false); break;
                        }
                    }
                }

                Transform hospital = BuildingHelper.GetAddressEntranceTransform(GameManager.hospitalAddress);
                if (hospital != null)
                {
                    Add("hospital", "reallife_leisure_landmark_hospital", hospital.position, 35f, false);
                }
            }
            catch (Exception e)
            {
                LeisureDirector.Logger?.Error(e);
            }
            LeisureDirector.Logger?.Info("Leisure: " + Landmarks.Count + " landmark(s) found in the city.");
        }

        private static void Scene<T>(string id, string nameKey, float radius, bool waterside) where T : Component
        {
            T[] found = SceneFinds.All<T>(LandmarkSearch, FindObjectsInactive.Include);
            // The same one every time, whatever order the engine hands them over in: the most westerly.
            T pick = null;
            for (int i = 0; i < found.Length; i++)
            {
                if (found[i] != null && (pick == null || found[i].transform.position.x < pick.transform.position.x))
                {
                    pick = found[i];
                }
            }
            if (pick != null)
            {
                Add(id, nameKey, pick.transform.position, radius, waterside);
            }
        }

        private static void Add(string id, string nameKey, Vector3 position, float radius, bool waterside)
        {
            for (int i = 0; i < Landmarks.Count; i++)
            {
                if (Landmarks[i].Id == id)
                {
                    return;
                }
            }
            Landmarks.Add(new Landmark { Id = id, NameKey = nameKey, Position = position, Radius = radius, Waterside = waterside });
        }

        /// <summary>The landmark whose ground the player stands on, or null.</summary>
        public static Landmark LandmarkAt(Vector3 position)
        {
            Landmark best = null;
            float bestSqr = float.MaxValue;
            for (int i = 0; i < Landmarks.Count; i++)
            {
                Landmark mark = Landmarks[i];
                Vector3 to = mark.Position - position;
                to.y = 0f;
                if (to.sqrMagnitude <= mark.Radius * mark.Radius && to.sqrMagnitude < bestSqr)
                {
                    bestSqr = to.sqrMagnitude;
                    best = mark;
                }
            }
            return best;
        }

        // ---------------------------------------------------------------- the water's edge

        /// <summary>How far the nearest waterside landmark is, on the flat; a large number if there is none.</summary>
        public static float WatersideDistance(Vector3 position)
        {
            float best = float.MaxValue;
            for (int i = 0; i < Landmarks.Count; i++)
            {
                if (Landmarks[i].Waterside)
                {
                    best = Mathf.Min(best, CitySpots.Flat(Landmarks[i].Position - position));
                }
            }
            return best;
        }

        /// <summary>
        /// Whether somebody standing here and facing this way has open water in front of them. The game has no
        /// water object to ask, so this measures: the player stands within three metres of the edge of the
        /// walkable ground, and 5.5 and 7.5 metres ahead there is either something called water, or nothing at
        /// all under the sky, or a surface more than 0.8 m below their feet that nobody can walk or drive on.
        /// Three rays; called only while the player stands still near a waterside landmark.
        /// </summary>
        /// <param name="water">Where a float would land, 6 m ahead.</param>
        /// <param name="surfaceKnown">False if no surface was hit there (the float is then not shown).</param>
        public static bool WaterAhead(Vector3 position, Vector3 forward, out Vector3 water, out bool surfaceKnown)
        {
            water = position;
            surfaceKnown = false;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.01f)
            {
                return false;
            }
            forward.Normalize();
            if (!NavMesh.FindClosestEdge(position, out NavMeshHit edge, NavMesh.AllAreas) || edge.distance > 3f)
            {
                return false;
            }
            if (_waterMask == 0)
            {
                _waterMask = ~((1 << LayerHelper.PlayerLayerIndex) | (1 << LayerHelper.HumanLayerIndex) | (1 << 2));
            }
            float surface = float.NaN;
            for (int i = 0; i < 3; i++)
            {
                float ahead = 4f + i * 1.75f;   // 4, 5.75, 7.5
                Vector3 point = position + forward * ahead;
                bool open;
                if (!Physics.Raycast(point + Vector3.up * 2.5f, Vector3.down, out RaycastHit hit, 16f, _waterMask, QueryTriggerInteraction.Ignore))
                {
                    open = true;
                }
                else if (NamedWater(hit.collider))
                {
                    open = true;
                    surface = hit.point.y;
                }
                else if (hit.point.y > position.y - 0.8f)
                {
                    open = false;   // ground, a quay wall, a boat: something at their own level
                }
                else
                {
                    open = hit.collider.gameObject.layer != LayerMask.NameToLayer("Roads")
                           && !NavMesh.SamplePosition(hit.point, out _, 1.2f, NavMesh.AllAreas);
                    if (open && float.IsNaN(surface))
                    {
                        surface = hit.point.y;
                    }
                }
                if (!open && i > 0)
                {
                    return false;   // the first may still be quay; the two beyond it must be water
                }
            }
            surfaceKnown = !float.IsNaN(surface);
            water = position + forward * 6f;
            water.y = surfaceKnown ? surface : position.y - 1f;
            return true;
        }

        /// <summary>
        /// A place to fish from at the waterside landmark nearest to <paramref name="from"/>, then the next nearest:
        /// a spot a little in from the edge of the walkable ground, and the way to face there for open water
        /// (<see cref="WaterAhead"/>). For the test button only: it looks at up to 33 spots a landmark and eight ways
        /// to face at each, three rays a look, so it is never called on a timer.
        /// </summary>
        public static bool WaterEdgeNear(Vector3 from, out Vector3 stand, out Vector3 facing, out float metres)
        {
            stand = from;
            facing = Vector3.forward;
            metres = 0f;
            FindLandmarks();
            float beyond = -1f;
            for (int tried = 0; tried < Landmarks.Count; tried++)
            {
                // The nearest waterside landmark not looked at yet.
                Landmark next = null;
                float nearest = float.MaxValue;
                for (int i = 0; i < Landmarks.Count; i++)
                {
                    float away = CitySpots.Flat(Landmarks[i].Position - from);
                    if (Landmarks[i].Waterside && away > beyond && away < nearest)
                    {
                        nearest = away;
                        next = Landmarks[i];
                    }
                }
                if (next == null)
                {
                    return false;
                }
                beyond = nearest;
                for (int ring = 0; ring <= 4; ring++)
                {
                    int bearings = ring == 0 ? 1 : 8;
                    for (int b = 0; b < bearings; b++)
                    {
                        float turn = b * Mathf.PI * 0.25f;
                        Vector3 probe = next.Position + new Vector3(Mathf.Sin(turn), 0f, Mathf.Cos(turn)) * (ring * 12f);
                        if (!NavMesh.SamplePosition(probe, out NavMeshHit ground, 6f, NavMesh.AllAreas)
                            || !NavMesh.FindClosestEdge(ground.position, out NavMeshHit edge, NavMesh.AllAreas) || edge.distance > 40f)
                        {
                            continue;
                        }
                        Vector3 inward = ground.position - edge.position;
                        inward.y = 0f;
                        if (inward.sqrMagnitude < 0.01f)
                        {
                            inward = edge.normal;
                            inward.y = 0f;
                        }
                        if (inward.sqrMagnitude < 0.0001f
                            || !NavMesh.SamplePosition(edge.position + inward.normalized * 1.2f, out NavMeshHit spot, 1.5f, NavMesh.AllAreas))
                        {
                            continue;
                        }
                        for (int f = 0; f < 8; f++)
                        {
                            Vector3 way = Quaternion.Euler(0f, f * 45f, 0f) * -inward.normalized;   // straight out over the edge first
                            if (WaterAhead(spot.position, way, out _, out _))
                            {
                                stand = spot.position;
                                facing = way;
                                metres = Mathf.Sqrt(CitySpots.Flat(spot.position - from));
                                return true;
                            }
                        }
                    }
                }
            }
            return false;
        }

        private static bool NamedWater(Collider collider)
        {
            string name = collider.name;
            return name.IndexOf("water", StringComparison.OrdinalIgnoreCase) >= 0 || name.IndexOf("ocean", StringComparison.OrdinalIgnoreCase) >= 0
                   || name.IndexOf("river", StringComparison.OrdinalIgnoreCase) >= 0 || name.IndexOf("sea", StringComparison.OrdinalIgnoreCase) == 0;
        }
    }
}
