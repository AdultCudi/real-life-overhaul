using System;
using System.Collections.Generic;
using System.Globalization;
using Extensions;
using Localizor;
using RealLifeOverhaul.Uptown;
using TMPro;
using UI;
using UI.InGameUI;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;

namespace RealLifeOverhaul.Subway
{
    /// <summary>
    /// The only file of the districts' subway stations that touches the game (docs/features/subway-uptown.md): one of the
    /// game's own entrances copied asleep, its station replaced by the mod's (<see cref="StationEntrance"/>), stood where the
    /// plan says once nothing solid stands there and its riders come up on ground both walking meshes hold, cut into those
    /// meshes, added to the game's list of stations (CityManager.subwayStations: the map's marker, outline and filter and
    /// every travel follow from it) and given an entry in the game's own window of stations at its name's place; and all of
    /// it taken away again as it came. Nothing is searched for in the scene but the game's own list and window. COST:
    /// called at the load, on the option's press and by the tests; nothing a frame.
    /// </summary>
    internal static class StationsGame
    {
        private sealed class Stood
        {
            public StationSite Site;
            public StationEntrance Station;
            public CityMapSubwayStationEntry Entry;
        }

        private const float ShapeSlack = 0.3f;       // m the game's entrance may differ from the plan's measures before it is taken for another model
        private const float SolidFrom = 0.4f;        // m above the ground the look for something solid begins: kerbs and the ground do not count
        private const float CutLook = 0.3f;          // m round the entrance's middle no walking ground is left once the cut is made

        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private static readonly List<Stood> Standing = new List<Stood>(4);
        private static readonly Collider[] Overlaps = new Collider[8];
        private static readonly List<string> ListNames = new List<string>(32);
        private static GameObject _root;
        private static TMP_InputField _search;
        private static UnityAction<string> _onSearch;   // made once, hung and taken off by this field
        private static StationListWatch _watch;
        private static int _solidMask;

        /// <summary>How many of the districts' stations stand now.</summary>
        public static int Count => Standing.Count;

        public static StationEntrance StationAt(int index) => index >= 0 && index < Standing.Count ? Standing[index].Station : null;

        public static StationSite SiteAt(int index) => index >= 0 && index < Standing.Count ? Standing[index].Site : null;

        /// <summary>The game's stations as its city has them now (the mod's own among them once they stand); 0 with no city.</summary>
        public static int GameStations()
        {
            CityManager city = InstanceBehavior<CityManager>.Instance;
            return city != null && city.subwayStations != null ? city.subwayStations.Count - OwnStations.Count : 0;
        }

        /// <summary>A station's name as the player reads it.</summary>
        public static string NameOf(StationSite site) => site.NameKey.GetLocalization();

        private static Transform Root()
        {
            if (_root == null)
            {
                _root = new GameObject(StationsDirector.RootName);
            }
            return _root.transform;
        }

        private static List<CityMapSubwayStationEntry> Entries(CityMapSubwayStations list)
        {
            return list != null ? GameContract.MapStationEntries.Field?.GetValue(list) as List<CityMapSubwayStationEntry> : null;
        }

        // ---------------------------------------------------------------- standing one

        /// <summary>
        /// Stands a station of the plan and joins it to the game's network: null when it stands, else why not, with nothing of it left. Refused
        /// before anything is made: no city or list of the game's, no entrance of the game's that can be copied, an entrance of another shape,
        /// something solid on its ground, or riders that would come up off the walking ground.
        /// </summary>
        public static string Stand(StationSite site, out StationEntrance stood)
        {
            stood = null;
            CityManager city = InstanceBehavior<CityManager>.Instance;
            UIs uis = InstanceBehavior<UIs>.Instance;
            if (city == null || city.subwayStations == null || city.subwaySystem == null || city.cityMap == null)
            {
                return "the game's subway is not there";
            }
            CityMapSubwayStations list = uis != null ? uis.cityMapSubwayStations : null;
            List<CityMapSubwayStationEntry> entries = Entries(list);
            if (list == null || list.entryTemplate == null || entries == null)
            {
                return "the game's window of stations is not as the mod knows it";
            }
            if (Find(site) != null)
            {
                return "it stands already";
            }
            SubwayStation template = Template(city);
            if (template == null)
            {
                return "none of the game's entrances can be copied (its own mesh on its own object, never a static batch, and a solid box)";
            }
            string shape = Shape(template);
            if (shape != null)
            {
                return shape;
            }
            var at = new Vector3(site.X, StationsRules.GroundOf(site), site.Z);
            Quaternion turn = Quaternion.Euler(0f, site.Yaw, 0f);
            string solid = Solid(at, turn);
            if (solid != null)
            {
                return solid;
            }
            string walk = ComesUp(site, out Vector3 arrive);
            if (walk != null)
            {
                return walk;
            }
            string district = DistrictSplit.IdOf(site.District);
            var asleep = new GameObject("RLO subway station (asleep)");
            asleep.SetActive(false);   // nothing of the copy wakes until it is whole: the game's own station on it never does
            StationEntrance station = null;
            GameObject copy = null;
            try
            {
                copy = UnityEngine.Object.Instantiate(template.gameObject, asleep.transform);
                copy.name = "RLO subway station " + site.Id;
                SubwayStation copied = copy.GetComponent<SubwayStation>();
                if (copied == null)
                {
                    return "the copy of the game's entrance has no station on it";
                }
                Renderer[] pieces = copied.Renderers;
                copy.transform.SetPositionAndRotation(at, turn);
                Transform arrival = new GameObject("Arrival").transform;
                arrival.SetParent(copy.transform, false);
                arrival.position = arrive;
                var stop = new GameObject("TaxiStop");
                stop.transform.SetParent(copy.transform, false);
                StationTaxiStop taxiStop = stop.AddComponent<StationTaxiStop>();
                taxiStop.Fit(arrival, site.NameKey, district);
                station = copy.AddComponent<StationEntrance>();
                station.Fit(site, copied, pieces, arrival, taxiStop, district);
                UnityEngine.Object.DestroyImmediate(copied);   // asleep, it never woke: nothing of the game's heard of it
                Cut(copy);
                copy.transform.SetParent(Root(), true);        // it wakes, whole
            }
            catch (Exception)
            {
                SceneryTrees.Destroy(copy);   // null is safe; asleep, its lamp never listed itself
                throw;
            }
            finally
            {
                SceneryTrees.Destroy(asleep);   // with a copy that was refused still under it, or empty
            }
            var entry = new Stood { Site = site, Station = station };
            Standing.Add(entry);
            city.subwayStations.Add(station);          // last, after the game's own: whoever looks one up by its name's value finds the game's first
            OwnStations.Add(station, site.NameKey, site.Id + " (the districts' own)");
            entry.Entry = AddEntry(entry, list, entries);
            Hang(list);
            stood = station;
            return null;
        }

        private static Stood Find(StationSite site)
        {
            for (int i = 0; i < Standing.Count; i++)
            {
                if (ReferenceEquals(Standing[i].Site, site))
                {
                    return Standing[i];
                }
            }
            return null;
        }

        // The game's entrance nearest the districts that can be copied: Midtown North West's, at 9th Street and 1st Avenue, in Build 3682.
        private static SubwayStation Template(CityManager city)
        {
            SubwayStation best = null;
            float bestAt = float.MaxValue;
            var near = new Vector3(StreetsRules.NorthWestStationX, 0f, StreetsRules.NorthWestStationZ);
            for (int i = 0; i < city.subwayStations.Count; i++)
            {
                SubwayStation station = city.subwayStations[i];
                if (station == null || OwnStations.Is(station) || station is StationEntrance || !Copyable(station))
                {
                    continue;
                }
                float sqr = CitySpots.Flat(station.transform.position - near);
                if (sqr < bestAt)
                {
                    bestAt = sqr;
                    best = station;
                }
            }
            return best;
        }

        // An entrance whose look is its own object's mesh (a copy of a piece drawn in a static batch would draw the batch), with the game's solid box.
        private static bool Copyable(SubwayStation station)
        {
            MeshFilter mesh = station.GetComponent<MeshFilter>();
            MeshRenderer drawn = station.GetComponent<MeshRenderer>();
            Vector3 scale = station.transform.lossyScale;
            return mesh != null && mesh.sharedMesh != null && drawn != null && !drawn.isPartOfStaticBatch && station.GetComponent<BoxCollider>() != null
                   && station.Renderers != null && station.Renderers.Length > 0 && Mathf.Abs(scale.x - 1f) < 0.01f && Mathf.Abs(scale.z - 1f) < 0.01f;
        }

        // The entrance against the plan's measures (StationsRules: Build 3682's): another model after a game update is not stood on ground measured for this one.
        private static string Shape(SubwayStation template)
        {
            Bounds bounds = template.GetComponent<MeshFilter>().sharedMesh.bounds;
            float half = bounds.extents.x, front = bounds.center.z + bounds.extents.z, back = bounds.extents.z - bounds.center.z;
            if (Mathf.Abs(half - StationsRules.HalfWidth) > ShapeSlack || Mathf.Abs(front - StationsRules.Front) > ShapeSlack || Mathf.Abs(back - StationsRules.Back) > ShapeSlack)
            {
                return "the game's entrance is another shape than the plan's (" + (half * 2f).ToString("F2", Inv) + " m wide, " + back.ToString("F2", Inv) + " m behind and "
                       + front.ToString("F2", Inv) + " m ahead of its origin; the plan's " + (StationsRules.HalfWidth * 2f).ToString("F2", Inv) + ", " + StationsRules.Back.ToString("F2", Inv)
                       + " and " + StationsRules.Front.ToString("F2", Inv) + ")";
            }
            return null;
        }

        // Anything solid standing where the entrance would: a lamp, a tree, a shelter, a wall. One box, as it is stood.
        private static string Solid(Vector3 at, Quaternion turn)
        {
            if (_solidMask == 0)
            {
                _solidMask = ~LayerMask.GetMask("Roads", "Ground", "GroundUnplacable", "UI", "Ignore Raycast", "AutoParkSpots", "ParkingArea", "TransparentFX", "Water");
            }
            Physics.SyncTransforms();   // what the load stood in this same call is where it stands
            float height = StationsRules.Height - SolidFrom;
            Vector3 centre = at + turn * new Vector3(0f, StationsRules.Sunk + SolidFrom + height * 0.5f, (StationsRules.Front - StationsRules.Back) * 0.5f);
            var half = new Vector3(StationsRules.HalfWidth - 0.05f, height * 0.5f, (StationsRules.Front + StationsRules.Back) * 0.5f - 0.05f);
            int found = Physics.OverlapBoxNonAlloc(centre, half, Overlaps, turn, _solidMask, QueryTriggerInteraction.Ignore);
            if (found <= 0)
            {
                return null;
            }
            Collider first = Overlaps[0];
            string what = first != null ? first.name + (first.transform.parent != null ? " of " + first.transform.parent.name : string.Empty) : "something";
            Array.Clear(Overlaps, 0, Overlaps.Length);
            return "something solid stands on its ground (" + what + (found > 1 ? " and " + (found - 1) + " more" : string.Empty) + ")";
        }

        // Where a rider comes up, on the player's walking ground and the pedestrians' both: the game brings its rider up on the nearest of any.
        private static string ComesUp(StationSite site, out Vector3 arrive)
        {
            StationsRules.Arrival(site, out float x, out float z);
            var wanted = new Vector3(x, StationsRules.GroundOf(site) + StationsRules.Sunk, z);
            arrive = wanted;
            bool player = Sample(wanted, PlayerController.navMeshQueryFilter, StationsRules.ArriveRoom, out Vector3 onPlayers);
            bool people = Sample(wanted, NavMeshHelper.NpcNavMeshFilter, StationsRules.ArriveRoom, out _);
            if (!player || !people)
            {
                return "its riders would come up off the walking ground (the player's " + (player ? "holds" : "does not hold") + " the place, the pedestrians' "
                       + (people ? "holds" : "does not hold") + " it)";
            }
            arrive = onPlayers;
            return null;
        }

        private static bool Sample(Vector3 at, NavMeshQueryFilter filter, float within, out Vector3 found)
        {
            if (NavMesh.SamplePosition(at, out NavMeshHit hit, within + 0.5f, filter) && CitySpots.Flat(hit.position - at) <= within * within)
            {
                found = hit.position;
                return true;
            }
            found = at;
            return false;
        }

        // Walkers move on the walking meshes and colliders stop nobody: one carving box cuts the entrance out of both, and goes with it.
        private static void Cut(GameObject copy)
        {
            StationsRules.Carve(out float ahead, out float across, out float along);
            NavMeshObstacle cut = copy.AddComponent<NavMeshObstacle>();
            cut.shape = NavMeshObstacleShape.Box;
            cut.center = new Vector3(0f, StationsRules.Sunk + StationsRules.Height * 0.5f, ahead);
            cut.size = new Vector3(across, StationsRules.Height, along);
            cut.carveOnlyStationary = true;
            cut.carving = true;
        }

        // ---------------------------------------------------------------- the game's window of stations

        private static int Order(string a, string b) => string.Compare(a, b, StringComparison.CurrentCulture);   // as the game sorts its own

        // An entry of the game's own for the station, at its name's place among the game's, which are in the order of their names.
        private static CityMapSubwayStationEntry AddEntry(Stood stood, CityMapSubwayStations list, List<CityMapSubwayStationEntry> entries)
        {
            CityMapSubwayStationEntry entry = UnityEngine.Object.Instantiate(list.entryTemplate, list.entryTemplate.transform.parent);
            entry.name = "RLO " + stood.Site.Id;
            entry.Setup(stood.Station);   // the game's own: its station, its button, and a label of the copied entrance's name until the next line
            if (entry.stationNameLabel != null)
            {
                entry.stationNameLabel.Key = stood.Site.NameKey;
            }
            ListNames.Clear();
            for (int i = 0; i < entries.Count; i++)
            {
                SubwayStation other = entries[i] != null ? entries[i].Station : null;
                ListNames.Add(other != null ? GameMembers.NameKey(other).GetLocalization() : string.Empty);
            }
            int at = StationsRules.InsertAt(ListNames, NameOf(stood.Site), Order);
            if (at < entries.Count && entries[at] != null)
            {
                entry.transform.SetSiblingIndex(entries[at].transform.GetSiblingIndex());
            }
            entries.Insert(at, entry);
            return entry;
        }

        // The window's search: the game's own reads a station's name from its name's value, so after it the districts' entries are shown by their own.
        private static void Hang(CityMapSubwayStations list)
        {
            if (list == null)
            {
                return;
            }
            if (_search == null)
            {
                _search = list.GetComponentInChildren<TMP_InputField>(true);
            }
            if (_watch == null)
            {
                _watch = list.gameObject.AddComponent<StationListWatch>();   // hangs it again each time the window is shown, after the game's own listener
            }
            Rehang();
        }

        /// <summary>The districts' listener put last on the window's search field (the game adds its own the first time its window wakes).</summary>
        internal static void Rehang()
        {
            if (_search == null || Standing.Count == 0)
            {
                return;
            }
            _onSearch = _onSearch ?? Guard.Listener<string>(OnSearch);
            _search.onValueChanged.RemoveListener(_onSearch);
            _search.onValueChanged.AddListener(_onSearch);
            OnSearch(_search.text);
        }

        private static void OnSearch(string typed)
        {
            for (int i = 0; i < Standing.Count; i++)
            {
                CityMapSubwayStationEntry entry = Standing[i].Entry;
                if (entry != null)
                {
                    bool shown = StationsRules.Shown(NameOf(Standing[i].Site), typed, StringComparison.CurrentCultureIgnoreCase);
                    if (entry.gameObject.activeSelf != shown)
                    {
                        entry.gameObject.SetActive(shown);
                    }
                }
            }
        }

        // ---------------------------------------------------------------- taking them away

        /// <summary>True when a ride of the game's begins or ends at one of the districts' stations.</summary>
        public static bool RideTouches()
        {
            CityManager city = InstanceBehavior<CityManager>.Instance;
            SubwaySystem system = city != null ? city.subwaySystem : null;
            return system != null && (OwnStations.Is(system.lastSubwayStation) || OwnStations.Is(system.destinationSubwayStation));
        }

        /// <summary>True when the game's map is open on its list of stations from one of the districts'.</summary>
        public static bool PickingFromOwn()
        {
            CityManager city = InstanceBehavior<CityManager>.Instance;
            return city != null && city.cityMap != null && city.subwaySystem != null && CityMap.IsOpen && city.cityMap.isSubwayMode && OwnStations.Is(city.subwaySystem.lastSubwayStation);
        }

        /// <summary>The game's map shut, as its own close does it: its list of stations put away and the station the rider stood at let go.</summary>
        public static void ShutMap()
        {
            CityManager city = InstanceBehavior<CityManager>.Instance;
            if (city != null && city.cityMap != null && CityMap.IsOpen)
            {
                city.cityMap.Close();
            }
        }

        /// <summary>
        /// Every station of the districts' own taken away as it came: out of the game's list first (the game walks that list at every toggle of its
        /// map), its entry out of the window, its marker off the map, the game's card let go of its stop, then the entrance itself with its cut.
        /// Returns how many went. Safe with none standing and with a city that is going.
        /// </summary>
        public static int RemoveAll()
        {
            int gone = Standing.Count;
            CityManager city = InstanceBehavior<CityManager>.Instance;
            UIs uis = InstanceBehavior<UIs>.Instance;
            CityMapSubwayStations list = uis != null ? uis.cityMapSubwayStations : null;
            List<CityMapSubwayStationEntry> entries = Entries(list);
            for (int i = Standing.Count - 1; i >= 0; i--)
            {
                Stood stood = Standing[i];
                StationEntrance station = stood.Station;
                if (city != null && city.subwayStations != null && !ReferenceEquals(station, null))
                {
                    city.subwayStations.Remove(station);
                }
                OwnStations.Remove(station);
                if (stood.Entry != null)
                {
                    entries?.Remove(stood.Entry);
                    UnityEngine.Object.Destroy(stood.Entry.gameObject);
                }
                if (station != null)
                {
                    Unmark(city, station.transform);
                    Release(uis, station);
                    SceneryTrees.Destroy(station.gameObject);   // its lamp out of the game's culling lists first (RLO-PERF-023), then the entrance with its cut
                }
            }
            Standing.Clear();
            if (_search != null && _onSearch != null)
            {
                _search.onValueChanged.RemoveListener(_onSearch);
            }
            if (_watch != null)
            {
                UnityEngine.Object.Destroy(_watch);
            }
            _watch = null;
            _search = null;
            return gone;
        }

        // The station's marker on the game's map, which the game makes the first time its stations are shown there and keeps in its list of markers.
        private static void Unmark(CityManager city, Transform station)
        {
            CityMap map = city != null ? city.cityMap : null;
            if (map == null || map.pois == null)
            {
                return;
            }
            bool any = false;
            for (int i = map.pois.Count - 1; i >= 0; i--)
            {
                PointOfInterest marker = map.pois[i];
                if (marker != null && marker.target == station)
                {
                    map.pois.RemoveAt(i);
                    UnityEngine.Object.Destroy(marker.gameObject);
                    any = true;
                }
            }
            if (any)
            {
                map.UpdateNonePermanentPointOfInterests();   // the map's own list of the markers it moves is made again before it next walks it
            }
        }

        // The game's card about a place, if it is about this station's stop: let go, as its own close does.
        private static void Release(UIs uis, StationEntrance station)
        {
            BuildingResume card = uis != null ? uis.buildingResume : null;
            if (card != null && station.TaxiStop != null && ReferenceEquals(GameMembers.TargetOf(card), station.TaxiStop))
            {
                card.Close();
            }
        }

        /// <summary>The city is gone, or goes: what stood went with its scene, and nothing of it is kept.</summary>
        public static void Forget()
        {
            Standing.Clear();
            OwnStations.Clear();
            SceneryTrees.Destroy(_root);   // null is safe; what still stands under it has its lamps taken out of the game's culling lists first
            _root = null;
            _search = null;
            _watch = null;
        }

        // ---------------------------------------------------------------- the check

        /// <summary>
        /// Each standing station against what was wanted, counted from what stands: once in the game's list, an entry of its own in the window with
        /// its own name, its riders coming up on both walking meshes, and the entrance cut out of the player's. One line; true when all pass.
        /// </summary>
        public static bool Check(out string line)
        {
            CityManager city = InstanceBehavior<CityManager>.Instance;
            UIs uis = InstanceBehavior<UIs>.Instance;
            List<CityMapSubwayStationEntry> entries = Entries(uis != null ? uis.cityMapSubwayStations : null);
            var text = new System.Text.StringBuilder(256);
            bool pass = true;
            int listed = 0;
            for (int i = 0; city != null && city.subwayStations != null && i < city.subwayStations.Count; i++)
            {
                listed += city.subwayStations[i] is StationEntrance ? 1 : 0;
            }
            for (int i = 0; i < Standing.Count; i++)
            {
                Stood stood = Standing[i];
                StationEntrance station = stood.Station;
                if (station == null)
                {
                    text.Append(stood.Site.Id).Append(" gone; ");
                    pass = false;
                    continue;
                }
                int inList = 0, inWindow = 0;
                for (int k = 0; city != null && city.subwayStations != null && k < city.subwayStations.Count; k++)
                {
                    inList += ReferenceEquals(city.subwayStations[k], station) ? 1 : 0;
                }
                for (int k = 0; entries != null && k < entries.Count; k++)
                {
                    inWindow += entries[k] != null && ReferenceEquals(entries[k].Station, station) ? 1 : 0;
                }
                bool named = stood.Entry != null && stood.Entry.stationNameLabel != null && stood.Entry.stationNameLabel.Key == stood.Site.NameKey
                             && GameMembers.NameKey(station) == stood.Site.NameKey && station.customOverlayHeaderKey == stood.Site.NameKey;
                Vector3 arrive = station.Arrival != null ? station.Arrival.position : station.transform.position;
                bool player = Sample(arrive, PlayerController.navMeshQueryFilter, StationsRules.ArriveRoom, out _);
                bool people = Sample(arrive, NavMeshHelper.NpcNavMeshFilter, StationsRules.ArriveRoom, out _);
                Vector3 middle = station.transform.TransformPoint(new Vector3(0f, StationsRules.Sunk, (StationsRules.Front - StationsRules.Back) * 0.5f));
                bool cut = !Sample(middle, PlayerController.navMeshQueryFilter, CutLook, out _);
                bool good = inList == 1 && inWindow == 1 && named && player && people && cut;
                pass &= good;
                text.Append(stood.Site.Id).Append(": in the game's list ").Append(inList).Append(", in its window ").Append(inWindow).Append(named ? ", named its own" : ", NOT named its own")
                    .Append(", riders come up on the player's ground ").Append(player ? "yes" : "NO").Append(" and the pedestrians' ").Append(people ? "yes" : "NO")
                    .Append(", cut out of the walking ground ").Append(cut ? "yes" : "NO").Append("; ");
            }
            pass &= listed == Standing.Count;
            line = Standing.Count + " standing, " + listed + " of the districts' own in the game's list of " + (city != null && city.subwayStations != null ? city.subwayStations.Count : 0)
                   + "; " + text + (pass ? "PASS" : "FAIL");
            return pass;
        }
    }

    /// <summary>
    /// On the game's window of stations while the districts' stations stand: each time the window is shown, the districts'
    /// search listener is put after the game's own (which the game adds when its window first wakes), so the districts'
    /// entries are shown by their own names. COST: nothing a frame; one call as the window opens.
    /// </summary>
    public sealed class StationListWatch : MonoBehaviour
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("uptown_subway", "StationListWatch", typeof(StationListWatch));

        private void OnEnable()
        {
            if (FrameGuard.Off)
            {
                return;
            }
            try
            {
                StationsGame.Rehang();
            }
            catch (Exception fault)
            {
                Guard.Fail(FrameGuard, fault, this);
            }
        }
    }
}
