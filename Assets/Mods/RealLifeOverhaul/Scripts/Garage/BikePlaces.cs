using System;
using System.Collections.Generic;
using System.Globalization;
using BAModAPI;
using Localizor;
using Streets;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>What a two-wheeler place on the street is for.</summary>
    public enum BikePlaceKind
    {
        Dock,            // bike share: hire a city bicycle, bring one back
        BicycleShop,     // a rack outside a supermarket: bicycles for sale
        MotorbikeStand   // outside a car dealership: motorbikes and helmets for sale
    }

    /// <summary>One place on the street where two-wheelers are hired or sold.</summary>
    public sealed class BikePlace
    {
        public BikePlaceKind Kind;
        public Vector3 Position;
        public Quaternion Rotation;
        public GameObject Dressing;     // built the first time the player comes near, then kept
        public bool Shown;
    }

    /// <summary>
    /// Where two-wheelers are hired and sold, worked out once a city load by rule and in a fixed order, so a save always
    /// has them in the same spots: docks beside the game's scooter stands, racks outside supermarkets, motorbike stands
    /// outside car dealerships (repairing garages where the city has none), each wholly on level pavement off crossings
    /// (<see cref="Grounded"/>). A place is a few renderers with no collider, built within 90 m; a second's tick.
    /// </summary>
    public sealed class BikePlaces
    {
        private const int MaxDocks = 8, MaxShops = 5, MaxStands = 4;
        private const float ShowWithin = 90f;   // metres
        private const int RefusalsSaid = 3;   // places refused said one by one in the log, with Developer diagnostics on
        public const float Reach = 3.5f;   // metres: a place's panel
        private const int ShareColour = 0x1E88E5;   // 0xRRGGBB

        // Half sizes of what stands at a place, for the room test (the height from 0.35 m up).
        private static readonly Vector3 DockHalf = new Vector3(1.55f, 0.55f, 1.05f);
        private static readonly Vector3 RackHalf = new Vector3(1.05f, 0.55f, 0.95f);
        private static readonly Vector3 StandHalf = new Vector3(1.35f, 0.55f, 1.35f);
        public static readonly Vector3 BikeHalf = new Vector3(0.40f, 0.50f, 1.00f);

        private static int _solidMask;

        public static IModLogger Logger;

        private readonly Transform _owner;
        private readonly List<BikePlace> _places = new List<BikePlace>(18);
        private List<Transform> _stands;   // found behind the loading screen (PrewarmStands); null once Find has them
        private bool _found;
        private Search _search;   // the places being found, a slice a frame
        private const double SliceMs = 1.5;   // milliseconds of the search a frame: a stand's docks or a building's rack is one item
        private static int _refused;   // refused by BikeSpotRules this city load, for the log

        public BikePlaces(Transform owner)
        {
            _owner = owner;
        }

        public int Count => _places.Count;

        /// <summary>Every place, for the city map (World/MapLinks). Read only.</summary>
        public IReadOnlyList<BikePlace> All => _places;

        public bool Found => _found;

        /// <summary>For ScenePrewarm: the scooter stands, found by a search of the scene (84 ms). False indoors: tried again.</summary>
        public bool PrewarmStands()
        {
            if (_found || _stands != null || !Settings.Garage || !Settings.TwoWheelers)
            {
                return true;   // with the option off, Find looks for itself if it is switched on later
            }
            if (BuildingManager.IsInsideBuilding)
            {
                return false;
            }
            _stands = Stands();
            return true;
        }

        private static List<Transform> Stands()
        {
            var stands = new List<Transform>();
            foreach (VehicleSpawnerController spawner in SceneFinds.All<VehicleSpawnerController>("garage: the city's scooter stands being looked for (a search of the whole scene, once a city)"))
            {
                if (spawner != null && spawner.vehicleType == TwoWheelers.ScooterType && spawner.gameObject.activeInHierarchy)
                {
                    stands.Add(spawner.transform);
                }
            }
            return stands;
        }

        /// <summary>
        /// Begins finding the places, once a city load, from the stands found behind the loading screen. The search goes on a slice a
        /// frame (<see cref="Step"/>, about 1.5 ms): all of it in one frame took 37 ms in a player's log (27 Oct 2026), the garage's
        /// share of a 149 ms frame. The places appear as they are found.
        /// </summary>
        public void Find()
        {
            _found = true;
            _refused = 0;
            _search = null;
            var city = InstanceBehavior<CityManager>.Instance;
            if (city == null || city.cityBuildingControllers == null)
            {
                return;
            }
            try
            {
                List<Transform> stands = _stands ?? Stands();
                _stands = null;
                stands.RemoveAll(stand => stand == null);   // one taken away since it was found
                var search = new Search();
                search.Stands = Keyed(stands);
                search.Stands.Sort(ByKey);
                search.Step = Mathf.Max(2, Mathf.CeilToInt(search.Stands.Count / (float)MaxDocks));
                var buildings = new List<CityBuildingController>(GameDlc.Open(city));
                buildings.RemoveAll(b => b == null || b.buildingRegistration == null);
                search.Buildings = Keyed(buildings);
                search.Buildings.Sort(ByKey);
                _search = search;
            }
            catch (Exception e)
            {
                _search = null;
                Logger?.Warn("Two-wheelers: the places to hire and buy could not be worked out; the test buttons still make bicycles and motorbikes. " + e);
            }
        }

        /// <summary>True while the places are being found a slice a frame.</summary>
        public bool Finding => _search != null;

        // What the search keeps between its slices: the stands and the buildings in the city's fixed order, and how far each pass has got.
        private sealed class Search
        {
            public List<AtPlace<Transform>> Stands;
            public List<AtPlace<CityBuildingController>> Buildings;
            public int Step;        // stands between two docks tried
            public int Stage;       // 0 the docks, 1 the racks and the stands, 2 garages that repair (only if no dealership), 3 said
            public int Next;        // the next stand or building of the stage
            public int Docks, Shops, Motor;
        }

        // A thing and where it stands in whole metres, so that sorting them asks the engine for nothing (x first, then z).
        private struct AtPlace<T>
        {
            public T Item;
            public int X, Z;
        }

        private static List<AtPlace<Transform>> Keyed(List<Transform> items)
        {
            var keyed = new List<AtPlace<Transform>>(items.Count);
            for (int i = 0; i < items.Count; i++)
            {
                Vector3 p = items[i].position;
                keyed.Add(new AtPlace<Transform> { Item = items[i], X = Mathf.RoundToInt(p.x), Z = Mathf.RoundToInt(p.z) });
            }
            return keyed;
        }

        private static List<AtPlace<CityBuildingController>> Keyed(List<CityBuildingController> items)
        {
            var keyed = new List<AtPlace<CityBuildingController>>(items.Count);
            for (int i = 0; i < items.Count; i++)
            {
                Vector3 p = items[i].transform.position;
                keyed.Add(new AtPlace<CityBuildingController> { Item = items[i], X = Mathf.RoundToInt(p.x), Z = Mathf.RoundToInt(p.z) });
            }
            return keyed;
        }

        private static int ByKey<T>(AtPlace<T> a, AtPlace<T> b)
        {
            int byX = a.X.CompareTo(b.X);
            return byX != 0 ? byX : a.Z.CompareTo(b.Z);
        }

        /// <summary>The next slice of the search, once a frame while <see cref="Finding"/>; nothing otherwise.</summary>
        public void Step()
        {
            Search s = _search;
            if (s == null)
            {
                return;
            }
            try
            {
                WorkSlice slice = WorkSlice.Of(SliceMs, 1);
                while (s.Stage == 0)
                {
                    if (s.Next >= s.Stands.Count || s.Docks >= MaxDocks)
                    {
                        s.Stage = 1;
                        s.Next = 0;
                        break;
                    }
                    if (!slice.More())
                    {
                        return;
                    }
                    Transform stand = s.Stands[s.Next].Item;
                    s.Next += s.Step;
                    if (stand == null)
                    {
                        continue;
                    }
                    Quaternion facing = Quaternion.Euler(0f, stand.eulerAngles.y, 0f);
                    if (Beside(stand.position, facing, out Vector3 spot))
                    {
                        Add(BikePlaceKind.Dock, spot, facing);
                        s.Docks++;
                    }
                }
                while (s.Stage == 1)
                {
                    if (s.Next >= s.Buildings.Count)
                    {
                        s.Stage = s.Motor == 0 ? 2 : 3;
                        s.Next = 0;
                        break;
                    }
                    if (!slice.More())
                    {
                        return;
                    }
                    CityBuildingController building = s.Buildings[s.Next++].Item;
                    if (building == null || building.buildingRegistration == null)
                    {
                        continue;
                    }
                    string type = building.buildingRegistration.businessTypeName;
                    if (type == "ba:businesstype_supermarket" && s.Shops < MaxShops && building.buildingRegistration.HasEstablishedBusiness
                        && Outside(building, RackHalf, out Vector3 spot, out Quaternion facing))
                    {
                        Add(BikePlaceKind.BicycleShop, spot, facing);
                        s.Shops++;
                    }
                    else if (type == "ba:businesstype_cardealership" && s.Motor < MaxStands && Outside(building, StandHalf, out spot, out facing))
                    {
                        Add(BikePlaceKind.MotorbikeStand, spot, facing);
                        s.Motor++;
                    }
                }
                // No dealership in this city yet: the stand goes to garages that repair.
                while (s.Stage == 2)
                {
                    if (s.Next >= s.Buildings.Count)
                    {
                        s.Stage = 3;
                        break;
                    }
                    if (!slice.More())
                    {
                        return;
                    }
                    CityBuildingController building = s.Buildings[s.Next++].Item;
                    var station = building != null ? building.GetComponent<GasStationController>() : null;
                    if (station != null && station.repairStationTriggers != null && station.repairStationTriggers.Length > 0 && s.Motor < 3
                        && Outside(building, StandHalf, out Vector3 spot, out Quaternion facing))
                    {
                        Add(BikePlaceKind.MotorbikeStand, spot, facing);
                        s.Motor++;
                    }
                }
                _search = null;
                Logger?.Info("Two-wheelers: " + s.Docks + " bike-share docks (of " + s.Stands.Count + " scooter stands), " + s.Shops + " bicycle racks outside supermarkets, "
                             + s.Motor + " motorbike stands; " + _refused + " place(s) tried were refused as not wholly on level pavement or on a crossing.");
            }
            catch (Exception e)
            {
                _search = null;
                Logger?.Warn("Two-wheelers: the places to hire and buy could not be worked out; the test buttons still make bicycles and motorbikes. " + e);
            }
        }

        // The first of eight places round a scooter stand on pavement with room for the whole dock.
        private static bool Beside(Vector3 from, Quaternion facing, out Vector3 spot)
        {
            Vector3 right = facing * Vector3.right;
            Vector3 forward = facing * Vector3.forward;
            for (int ring = 0; ring < 2; ring++)
            {
                float out_ = ring == 0 ? 3.4f : 5.2f;
                for (int side = 0; side < 4; side++)
                {
                    Vector3 way = side == 0 ? right : side == 1 ? -right : side == 2 ? forward : -forward;
                    if (CitySpots.Pavement(from + way * out_, 1.2f, out spot) && Grounded(ref spot, facing, DockHalf) && Fits(spot, facing, DockHalf, 0.05f)
                        && !Physics.CheckSphere(spot + Vector3.up, 1.6f, UsableMask, QueryTriggerInteraction.Collide))
                    {
                        return true;
                    }
                }
            }
            spot = default;
            return false;
        }

        // On the pavement along from a building's door, clear of what the player uses, with room for all of it.
        private static bool Outside(CityBuildingController building, Vector3 half, out Vector3 spot, out Quaternion facing)
        {
            spot = default;
            facing = Quaternion.identity;
            if (!JobSpots.Front(building, out Vector3 front))
            {
                return false;
            }
            Vector3 along = CitySpots.Along(front, out Vector3 way) ? way : building.transform.right;
            along.y = 0f;
            if (along.sqrMagnitude < 0.01f)
            {
                return false;
            }
            along.Normalize();
            facing = Quaternion.LookRotation(along);
            for (int i = 0; i < 6; i++)
            {
                float out_ = (4f + i / 2 * 2.5f) * (i % 2 == 0 ? 1f : -1f);
                if (CitySpots.Pavement(front + along * out_, 1.5f, out spot) && Grounded(ref spot, facing, half) && CitySpots.ClearQuietly(spot, 1.2f) && Fits(spot, facing, half, 0f))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>True if the whole place stands on level pavement off crossings; the spot then goes on the ground.</summary>
        public static bool Grounded(ref Vector3 spot, Quaternion facing, Vector3 half)
        {
            int off = 0;
            float lowest = float.MaxValue, highest = float.MinValue, middle = spot.y;
            for (int i = 0; i < BikeSpotRules.Samples; i++)
            {
                BikeSpotRules.Sample(i, half.x, half.z, out float across, out float along);
                Vector3 at = spot + facing * new Vector3(across, 0f, along);
                bool ground = StandHeight(at, out float height, out _);
                bool mesh = CitySpots.Pavement(new Vector3(at.x, height, at.z), BikeSpotRules.OnMesh + 0.3f, out Vector3 walk);
                if (!ground || !BikeSpotRules.OnPavement(mesh, CitySpots.Flat(walk - at)))
                {
                    off++;
                    continue;
                }
                lowest = Mathf.Min(lowest, height);
                highest = Mathf.Max(highest, height);
                if (i == 0)
                {
                    middle = height;
                }
            }
            var room = new Vector3(half.x + BikeSpotRules.CrossingMargin, 1f, half.z + BikeSpotRules.CrossingMargin);
            bool crossing = !CitySpots.ClearOfCrossings(spot + Vector3.up * 0.5f, room, facing, out _);
            BikeSpotRules.Refusal why = BikeSpotRules.Judge(off, lowest, highest, crossing);
            if (why != BikeSpotRules.Refusal.None)
            {
                _refused++;
                if (Meta.Dev.Verbose && _refused <= RefusalsSaid)   // the rest are counted in the line after the search: 170 lines in one frame before
                {
                    Logger?.Info("Two-wheelers check (place): a place at (" + spot.x.ToString("F1", CultureInfo.InvariantCulture) + ", "
                                 + spot.z.ToString("F1", CultureInfo.InvariantCulture) + ") refused: " + RefusalWords(why, off, highest - lowest) + ": PASS.");
                }
                return false;
            }
            spot.y = middle;
            return true;
        }

        private static string RefusalWords(BikeSpotRules.Refusal why, int off, float rise)
        {
            switch (why)
            {
                case BikeSpotRules.Refusal.Crossing: return "a marked crossing under it or within " + BikeSpotRules.CrossingMargin.ToString("F1", CultureInfo.InvariantCulture) + " m";
                case BikeSpotRules.Refusal.OffPavement:
                    return off + " of its " + BikeSpotRules.Samples + " points (its footprint and " + BikeSpotRules.Margin.ToString("F2", CultureInfo.InvariantCulture)
                           + " m round it) not on pavement";
                default: return "the ground under it rises " + rise.ToString("F2", CultureInfo.InvariantCulture) + " m, over a kerb";
            }
        }

        /// <summary>True if a box of this half size at a spot, moved forwards, touches nothing solid from 0.35 m up (over kerbs).</summary>
        public static bool Fits(Vector3 spot, Quaternion facing, Vector3 half, float forwards)
        {
            Vector3 centre = spot + facing * new Vector3(0f, 0.35f + half.y, forwards);
            return !Physics.CheckBox(centre, half, facing, SolidMask, QueryTriggerInteraction.Ignore);
        }

        // All layers but the ground and the zone-only ones.
        private static int SolidMask
        {
            get
            {
                if (_solidMask == 0)
                {
                    _solidMask = ~LayerMask.GetMask("Roads", "Ground", "GroundUnplacable", "UI", "Ignore Raycast", "AutoParkSpots", "ParkingArea", "TransparentFX", "Water");
                }
                return _solidMask;
            }
        }

        private static int _pinMask, _crowdMask, _wallMask;
        private static bool _masksRead;

        /// <summary>The layers a bike must never be left in (<see cref="BikeClearRules.Pins"/>), read once.</summary>
        public static int PinMask
        {
            get
            {
                ReadMasks();
                return _pinMask;
            }
        }

        /// <summary>What a place a bike is moved to must be clear of: those layers and people's.</summary>
        public static int RoomMask
        {
            get
            {
                ReadMasks();
                return _pinMask | _crowdMask;
            }
        }

        private static void ReadMasks()
        {
            if (_masksRead)
            {
                return;
            }
            _masksRead = true;
            _wallMask = LayerMask.GetMask("Buildings", "BuildingsOutlined", "BuildingWalls");
            for (int layer = 0; layer < 32; layer++)
            {
                string name = LayerMask.LayerToName(layer);
                if (BikeClearRules.Pins(name))
                {
                    _pinMask |= 1 << layer;
                }
                if (BikeClearRules.Crowds(name))
                {
                    _crowdMask |= 1 << layer;
                }
            }
        }

        /// <summary>True if no building's wall stands between two points at a height; the post a bike stood in does not count.</summary>
        public static bool Unwalled(Vector3 from, Vector3 to, float height)
        {
            ReadMasks();
            return _wallMask == 0 || !Physics.Linecast(from + Vector3.up * height, to + Vector3.up * height, _wallMask, QueryTriggerInteraction.Ignore);
        }

        /// <summary>True if nothing solid stands between two points: a wall is a skin, which <see cref="Fits"/> alone misses.</summary>
        public static bool Seen(Vector3 from, Vector3 spot, float height)
        {
            return !Physics.Linecast(from + Vector3.up * height, spot + Vector3.up * height, SolidMask, QueryTriggerInteraction.Ignore);
        }

        /// <summary>The height a two-wheeler stands at: the ground, or the pavement where higher (a kerb). False: no ground.</summary>
        public static bool StandHeight(Vector3 point, out float height, out bool pavement)
        {
            bool ground = CitySpots.Ground(point, out height);
            pavement = false;
            var probe = new Vector3(point.x, height, point.z);
            if (CitySpots.Pavement(probe, 0.5f, out Vector3 walk) && CitySpots.Flat(walk - probe) < 0.5f * 0.5f)
            {
                pavement = true;
                if (!ground || (walk.y > height && walk.y - height < 0.45f))
                {
                    height = walk.y;
                }
                ground = true;
            }
            return ground;
        }

        /// <summary>A place for a two-wheeler 2.2 m ahead of somebody on foot, side on, in the open; else close by. False: none.</summary>
        public static bool SpotAhead(Transform player, out Vector3 spot, out Quaternion facing)
        {
            Vector3 from = player.position;
            float yaw = player.eulerAngles.y;
            facing = Quaternion.Euler(0f, yaw + 90f, 0f);
            for (int i = 0; i < AheadTurn.Length; i++)
            {
                Vector3 at = from + Quaternion.Euler(0f, yaw + AheadTurn[i], 0f) * Vector3.forward * AheadReach[i];
                if (!StandHeight(at, out float height, out _) || Mathf.Abs(height - from.y) > 1.2f)
                {
                    continue;   // no ground there, or another level (under a bridge, a deck above)
                }
                spot = new Vector3(at.x, height, at.z);
                if (Fits(spot, facing, BikeHalf, 0f) && Seen(from, spot, 1.2f))
                {
                    return true;
                }
            }
            spot = default;
            return false;
        }

        /// <summary>True if a two-wheeler stands where nobody can walk up to it, a wall between it and the pavement; two queries.</summary>
        public static bool Walled(Vector3 at)
        {
            if (!CitySpots.Pavement(at, 8f, out Vector3 walk) || CitySpots.Flat(walk - at) < 0.3f * 0.3f)
            {
                return false;   // no pavement to judge from, or it stands on pavement
            }
            ReadMasks();
            return _wallMask != 0 && Physics.Linecast(walk + Vector3.up * 1.5f, new Vector3(at.x, walk.y + 1.5f, at.z), _wallMask, QueryTriggerInteraction.Ignore);
        }

        private static readonly float[] AheadTurn = { 0f, 35f, -35f, 0f, 0f, 70f, -70f };
        private static readonly float[] AheadReach = { 2.2f, 2.2f, 2.2f, 3.0f, 1.7f, 2.2f, 2.2f };

        /// <summary>The nearest building's address and district, or empty. A scan of the city's buildings: never per frame.</summary>
        public static string Whereabouts(Vector3 where)
        {
            try
            {
                CityManager city = InstanceBehavior<CityManager>.Instance;
                CityBuildingController[] buildings = GameDlc.Open(city);
                CityBuildingController best = null;
                float bestSqr = float.MaxValue;
                for (int i = 0; buildings != null && i < buildings.Length; i++)
                {
                    CityBuildingController building = buildings[i];
                    if (building == null || building.building == null || building.building.Address == null || building.building.Address.IsUndefined())
                    {
                        continue;
                    }
                    float sqr = CitySpots.Flat(building.transform.position - where);
                    if (sqr < bestSqr)
                    {
                        bestSqr = sqr;
                        best = building;
                    }
                }
                if (best == null)
                {
                    return string.Empty;
                }
                string street = best.building.Address.ToFormattedString();
                string district = best.building.Neighbourhood;
                district = string.IsNullOrEmpty(district) ? null : district.GetLocalization();
                return string.IsNullOrEmpty(district) ? street : street + ", " + district;
            }
            catch (Exception)
            {
                return string.Empty;   // a building without an address or a district without a name
            }
        }

        private static int UsableMask => LayerMask.GetMask("InteractiveItems", "InteractiveItemsOutlined");

        private void Add(BikePlaceKind kind, Vector3 position, Quaternion rotation)
        {
            _places.Add(new BikePlace { Kind = kind, Position = position, Rotation = rotation });
        }

        /// <summary>The place within reach of a point, or null. A loop over at most seventeen places.</summary>
        public BikePlace Near(Vector3 from, float reach = Reach)
        {
            BikePlace best = null;
            float bestSqr = reach * reach;
            for (int i = 0; i < _places.Count; i++)
            {
                float sqr = (_places[i].Position - from).sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    best = _places[i];
                }
            }
            return best;
        }

        /// <summary>Once a second: shows what is near the player and hides the rest, building at most one place a call.</summary>
        public void Tick(Vector3 player, bool on)
        {
            bool built = false;
            for (int i = 0; i < _places.Count; i++)
            {
                BikePlace place = _places[i];
                bool show = on && (place.Position - player).sqrMagnitude < ShowWithin * ShowWithin;
                if (show && place.Dressing == null)
                {
                    if (built)
                    {
                        continue;
                    }
                    built = true;
                    Dress(place);
                }
                if (place.Dressing != null && place.Shown != show)
                {
                    place.Shown = show;
                    place.Dressing.SetActive(show);
                }
            }
        }

        // In the place's frame (x across the pavement, z along it): along first, as across is the shopfront or the kerb.
        private static readonly Vector2[] HandOver =
        {
            new Vector2(0f, 2.6f), new Vector2(0f, -2.6f), new Vector2(0f, 3.4f), new Vector2(0f, -3.4f),
            new Vector2(1.1f, 2.9f), new Vector2(-1.1f, 2.9f), new Vector2(1.1f, -2.9f), new Vector2(-1.1f, -2.9f),
            new Vector2(2.1f, 0f), new Vector2(-2.1f, 0f)
        };

        /// <summary>Where a bike handed over is stood: beside the place along the pavement, in its sight. False: no room.</summary>
        public static bool HandOverSpot(BikePlace place, out Vector3 spot)
        {
            for (int pass = 0; pass < 2; pass++)
            {
                for (int i = 0; i < HandOver.Length; i++)
                {
                    Vector3 at = place.Position + place.Rotation * new Vector3(HandOver[i].x, 0f, HandOver[i].y);
                    if (!StandHeight(at, out float height, out bool pavement) || (pass == 0 && !pavement)
                        || Mathf.Abs(height - place.Position.y) > (pass == 0 ? 0.6f : 0.3f))
                    {
                        continue;
                    }
                    spot = new Vector3(at.x, height, at.z);
                    // Looked along above head height, so whoever stands there is not a wall. Never on a crossing.
                    if (Fits(spot, place.Rotation, BikeHalf, 0f) && Seen(place.Position, spot, 2.1f)
                        && CitySpots.ClearOfCrossings(spot + Vector3.up * 0.5f, new Vector3(BikeHalf.x, 1f, BikeHalf.z), place.Rotation, out _))
                    {
                        return true;
                    }
                }
            }
            spot = default;
            return false;
        }

        public void Dispose()
        {
            for (int i = 0; i < _places.Count; i++)
            {
                if (_places[i].Dressing != null)
                {
                    UnityEngine.Object.Destroy(_places[i].Dressing);
                }
            }
            _places.Clear();
            _stands = null;
            _search = null;
            _found = false;
        }

        private void Dress(BikePlace place)
        {
            using var piece = SessionWatch.Piece("garage: a two-wheeler stand being put out");
            if (!Shapes.Available)
            {
                return;
            }
            var go = new GameObject("RealLifeOverhaul " + place.Kind);
            Transform root = go.transform;
            root.SetParent(_owner, false);
            root.SetPositionAndRotation(place.Position, place.Rotation);
            place.Dressing = go;
            place.Shown = true;
            Material dark = Shapes.Paint(new Color(0.10f, 0.11f, 0.12f));
            Material steel = Shapes.Paint(new Color(0.62f, 0.64f, 0.66f));
            switch (place.Kind)
            {
                case BikePlaceKind.Dock:
                    if (!Shapes.Prop("bike_dock", root))
                    {
                        // a rail with three wheel hoops ahead of the forks, and the pay post at its end
                        Shapes.Piece("Rail", root, Shapes.Cube, dark, new Vector3(0f, 0.04f, 0.80f), new Vector3(2.5f, 0.08f, 0.12f));
                        for (int i = -1; i <= 1; i++)
                        {
                            Shapes.Piece("Hoop", root, Shapes.Cube, steel, new Vector3(i * 0.8f - 0.06f, 0.38f, 0.80f), new Vector3(0.025f, 0.68f, 0.05f));
                            Shapes.Piece("Hoop", root, Shapes.Cube, steel, new Vector3(i * 0.8f + 0.06f, 0.38f, 0.80f), new Vector3(0.025f, 0.68f, 0.05f));
                            Shapes.Piece("Hoop", root, Shapes.Cube, steel, new Vector3(i * 0.8f, 0.72f, 0.80f), new Vector3(0.145f, 0.025f, 0.05f));
                        }
                        Shapes.Piece("Post", root, Shapes.Cube, dark, new Vector3(1.38f, 0.62f, 0.80f), new Vector3(0.16f, 1.24f, 0.12f));
                        Shapes.Piece("Panel", root, Shapes.Cube, Shapes.Paint(GarageCar.FromRgb(ShareColour), 1f), new Vector3(1.38f, 1.08f, 0.738f), new Vector3(0.13f, 0.22f, 0.012f));
                    }
                    Display(BikeKind.CityBicycle, root, new Vector3(-0.8f, 0f, 0f), ShareColour);
                    Display(BikeKind.CityBicycle, root, new Vector3(0f, 0f, 0f), ShareColour);
                    break;
                case BikePlaceKind.BicycleShop:
                    Shapes.Piece("Rail", root, Shapes.Cube, steel, new Vector3(0f, 0.72f, 0.62f), new Vector3(1.7f, 0.03f, 0.03f));
                    Shapes.Piece("Leg", root, Shapes.Cube, steel, new Vector3(-0.85f, 0.35f, 0.62f), new Vector3(0.03f, 0.70f, 0.03f));
                    Shapes.Piece("Leg", root, Shapes.Cube, steel, new Vector3(0.85f, 0.35f, 0.62f), new Vector3(0.03f, 0.70f, 0.03f));
                    Shapes.Piece("Sign", root, Shapes.Cube, Shapes.Paint(new Color(0.95f, 0.78f, 0.10f), 0.5f), new Vector3(0.85f, 0.95f, 0.62f), new Vector3(0.30f, 0.20f, 0.02f));
                    Display(BikeKind.CityBicycle, root, new Vector3(-0.45f, 0f, 0f), -1);
                    Display(BikeKind.RoadBicycle, root, new Vector3(0.35f, 0f, 0f), -1);
                    break;
                default:
                    Shapes.Piece("Plinth", root, Shapes.Cube, dark, new Vector3(0f, 0.03f, 0f), new Vector3(2.6f, 0.06f, 2.6f));
                    Shapes.Piece("Sign", root, Shapes.Cube, Shapes.Paint(new Color(0.85f, 0.12f, 0.12f), 0.5f), new Vector3(1.2f, 0.75f, 1.2f), new Vector3(0.36f, 0.24f, 0.02f));
                    Shapes.Piece("Sign Post", root, Shapes.Cube, steel, new Vector3(1.2f, 0.34f, 1.2f), new Vector3(0.03f, 0.62f, 0.03f));
                    Display(BikeKind.StandardMotorbike, root, new Vector3(-0.55f, 0.06f, 0f), -1);
                    Display(BikeKind.SportMotorbike, root, new Vector3(0.55f, 0.06f, 0f), -1);
                    break;
            }
        }

        private static void Display(BikeKind kind, Transform root, Vector3 at, int colour)
        {
            BikeBody body = BikeBody.Build(BikeCatalogue.Of(kind), root, colour, still: true);
            if (body != null)
            {
                body.Root.transform.localPosition = at;
            }
        }
    }
}
