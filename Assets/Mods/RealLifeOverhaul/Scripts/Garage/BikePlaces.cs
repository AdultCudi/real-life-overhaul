using System;
using System.Collections.Generic;
using BAModAPI;
using Localizor;
using Streets;
using UnityEngine;

namespace RealLifeOverhaul
{
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
    /// Where two-wheelers come from. Worked out once per city load, by rule and in a fixed order, so the
    /// same save always has them in the same spots:
    /// - bike-share docks beside the game's own scooter stands (every other one, eight at most);
    /// - a bicycle rack on the pavement outside supermarkets (five at most);
    /// - a motorbike stand outside car dealerships (four at most), or at garages with a repair bay where
    ///   the city has no dealership open.
    /// Each stands on pavement that <see cref="CitySpots"/> found clear. A place is a handful of renderers
    /// (display bikes of the shared meshes, a rail or a plinth), built when the player first comes within
    /// 90 m, hidden beyond that, with no collider: dressing, like the mod's other street props.
    /// </summary>
    public sealed class BikePlaces
    {
        private const int MaxDocks = 8, MaxShops = 5, MaxStands = 4;
        private const float ShowWithin = 90f;
        public const float Reach = 3.5f;
        private const int ShareColour = 0x1E88E5;

        // Half sizes of what stands at a place, for the room test (the height is from 0.35 m up).
        private static readonly Vector3 DockHalf = new Vector3(1.55f, 0.55f, 1.05f);
        private static readonly Vector3 RackHalf = new Vector3(1.05f, 0.55f, 0.95f);
        private static readonly Vector3 StandHalf = new Vector3(1.35f, 0.55f, 1.35f);
        public static readonly Vector3 BikeHalf = new Vector3(0.40f, 0.50f, 1.00f);

        private static int _solidMask;

        public static IModLogger Logger;

        private readonly Transform _owner;
        private readonly List<BikePlace> _places = new List<BikePlace>(18);
        private List<Transform> _stands;   // the game's scooter stands, found behind the loading screen (PrewarmStands); null once Find has them
        private bool _found;

        public BikePlaces(Transform owner)
        {
            _owner = owner;
        }

        public int Count => _places.Count;

        /// <summary>Every place, for the city map (World/MapLinks). Read only.</summary>
        public IReadOnlyList<BikePlace> All => _places;

        public bool Found => _found;

        /// <summary>
        /// For ScenePrewarm: the game's scooter stands, which only a search of the whole scene finds (a frame of 84 ms the
        /// first time the player was on the street after a load), looked for behind the loading screen and kept for
        /// <see cref="Find"/>. False while the player is indoors, as Find waits for the street: tried again at the walk
        /// out of a door, behind the game's black screen.
        /// </summary>
        public bool PrewarmStands()
        {
            if (_found || _stands != null || !Settings.Garage || !Settings.TwoWheelers)
            {
                return true;   // with the option off, Find looks for itself if it is switched on later, as before
            }
            if (BuildingManager.IsInsideBuilding)
            {
                return false;
            }
            _stands = Stands();
            return true;
        }

        // The stands the game has put out in the street, of its electric scooters.
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

        /// <summary>Finds the places. One-off per city load (the search of the scene it needs is made behind the loading screen).</summary>
        public void Find()
        {
            _found = true;
            var city = InstanceBehavior<CityManager>.Instance;
            if (city == null || city.cityBuildingControllers == null)
            {
                return;
            }
            try
            {
                List<Transform> stands = _stands ?? Stands();
                _stands = null;
                stands.RemoveAll(stand => stand == null);   // (one taken away since it was found)
                stands.Sort(ByPlace);
                int step = Mathf.Max(2, Mathf.CeilToInt(stands.Count / (float)MaxDocks));
                int docks = 0;
                for (int i = 0; i < stands.Count && docks < MaxDocks; i += step)
                {
                    Transform stand = stands[i];
                    Quaternion facing = Quaternion.Euler(0f, stand.eulerAngles.y, 0f);
                    if (Beside(stand.position, facing, out Vector3 spot))
                    {
                        Add(BikePlaceKind.Dock, spot, facing);
                        docks++;
                    }
                }

                var buildings = new List<CityBuildingController>(city.cityBuildingControllers);
                buildings.RemoveAll(b => b == null || b.buildingRegistration == null);
                buildings.Sort((a, b) => ByPlace(a.transform, b.transform));
                int shops = 0, motor = 0;
                foreach (CityBuildingController building in buildings)
                {
                    string type = building.buildingRegistration.businessTypeName;
                    if (type == "ba:businesstype_supermarket" && shops < MaxShops && building.buildingRegistration.HasEstablishedBusiness
                        && Outside(building, RackHalf, out Vector3 spot, out Quaternion facing))
                    {
                        Add(BikePlaceKind.BicycleShop, spot, facing);
                        shops++;
                    }
                    else if (type == "ba:businesstype_cardealership" && motor < MaxStands && Outside(building, StandHalf, out spot, out facing))
                    {
                        Add(BikePlaceKind.MotorbikeStand, spot, facing);
                        motor++;
                    }
                }
                if (motor == 0)
                {
                    // No dealership in this city (yet): the stand goes to garages that repair, where a motorbike is at home too.
                    foreach (CityBuildingController building in buildings)
                    {
                        var station = building.GetComponent<GasStationController>();
                        if (station != null && station.repairStationTriggers != null && station.repairStationTriggers.Length > 0 && motor < 3
                            && Outside(building, StandHalf, out Vector3 spot, out Quaternion facing))
                        {
                            Add(BikePlaceKind.MotorbikeStand, spot, facing);
                            motor++;
                        }
                    }
                }
                Logger?.Info("Two-wheelers: " + docks + " bike-share docks (of " + stands.Count + " scooter stands), " + shops + " bicycle racks outside supermarkets, "
                             + motor + " motorbike stands.");
            }
            catch (Exception e)
            {
                Logger?.Warn("Two-wheelers: the places to hire and buy could not be worked out; the test buttons still make bicycles and motorbikes. " + e);
            }
        }

        private static int ByPlace(Transform a, Transform b)
        {
            Vector3 p = a.position;
            Vector3 q = b.position;
            int byX = Mathf.RoundToInt(p.x).CompareTo(Mathf.RoundToInt(q.x));
            return byX != 0 ? byX : Mathf.RoundToInt(p.z).CompareTo(Mathf.RoundToInt(q.z));
        }

        // A dock beside one of the game's scooter stands: the first of eight places round it that is pavement and
        // has room for the whole dock, clear of the stand itself.
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
                    if (CitySpots.Pavement(from + way * out_, 1.2f, out spot) && Fits(spot, facing, DockHalf, 0.05f)
                        && !Physics.CheckSphere(spot + Vector3.up, 1.6f, UsableMask, QueryTriggerInteraction.Collide))
                    {
                        return true;
                    }
                }
            }
            spot = default;
            return false;
        }

        // A rack or a stand on the pavement outside a building: along the pavement from its door, far enough from
        // anything the player uses (CitySpots keeps 2.4 m from doors, benches and machines), with room for all of it.
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
                if (CitySpots.Pavement(front + along * out_, 1.5f, out spot) && CitySpots.ClearQuietly(spot, 1.2f) && Fits(spot, facing, half, 0f))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// True if a box of this half size, standing on the ground at a spot (its middle moved forwards by an
        /// offset), touches nothing solid: no wall, vehicle, lamp post, bench or person. Starts 0.35 m up, so
        /// kerbs and the ground do not count. For what is about to be put on the street.
        /// </summary>
        public static bool Fits(Vector3 spot, Quaternion facing, Vector3 half, float forwards)
        {
            Vector3 centre = spot + facing * new Vector3(0f, 0.35f + half.y, forwards);
            return !Physics.CheckBox(centre, half, facing, SolidMask, QueryTriggerInteraction.Ignore);
        }

        // Everything something could be standing in: all layers but the ground itself and the ones that are only zones (as CitySpots).
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

        /// <summary>
        /// True if nothing solid stands between two points of the street, looked along at a height above each.
        /// This is what keeps a two-wheeler out of the inside of a building: <see cref="Fits"/> asks whether a box
        /// touches anything, and a box that lies wholly behind a shopfront touches nothing (a wall is a skin of
        /// triangles, not a filled shape), so a place 2 m "beside" something on the pavement can be indoors and
        /// still fit. A line from where the player or the stand is to that place crosses the wall, and says so.
        /// </summary>
        public static bool Seen(Vector3 from, Vector3 spot, float height)
        {
            return !Physics.Linecast(from + Vector3.up * height, spot + Vector3.up * height, SolidMask, QueryTriggerInteraction.Ignore);
        }

        /// <summary>
        /// The height a two-wheeler stands at over a point: the ground the game's rays find there, or the
        /// pavement's own walkable surface where that is the higher of the two (a kerb the ground ray went
        /// under). <paramref name="pavement"/> says whether the point is pavement at all. False: no ground there.
        /// </summary>
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

        /// <summary>
        /// A place for a two-wheeler in front of somebody on foot: 2.2 m straight ahead on the ground under that
        /// point, side on to them (so they see what it is), in the open and in their sight. If that exact place
        /// is taken, a little to either side, further or nearer. False if none of them will do.
        /// </summary>
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

        /// <summary>
        /// True if a two-wheeler stands where nobody can walk up to it: there is pavement near it, and a building's wall
        /// between that pavement and it. For one found standing like that when a save loads (a version of the mod that
        /// handed bikes over to the side of the stand could put one behind the shopfront). Two queries, once per bike per load.
        /// </summary>
        public static bool Walled(Vector3 at)
        {
            if (!CitySpots.Pavement(at, 8f, out Vector3 walk) || CitySpots.Flat(walk - at) < 0.3f * 0.3f)
            {
                return false;   // no pavement to judge from, or it stands on pavement
            }
            int walls = LayerMask.GetMask("Buildings", "BuildingsOutlined", "BuildingWalls");
            return walls != 0 && Physics.Linecast(walk + Vector3.up * 1.5f, new Vector3(at.x, walk.y + 1.5f, at.z), walls, QueryTriggerInteraction.Ignore);
        }

        private static readonly float[] AheadTurn = { 0f, 35f, -35f, 0f, 0f, 70f, -70f };
        private static readonly float[] AheadReach = { 2.2f, 2.2f, 2.2f, 3.0f, 1.7f, 2.2f, 2.2f };

        /// <summary>What a place on the street is called: the address of the nearest building and its district. Empty if the city cannot say. A scan of the city's buildings: for a panel or a toast, never per frame.</summary>
        public static string Whereabouts(Vector3 where)
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

        /// <summary>The place within reach of a point, or null. A loop over at most seventeen positions.</summary>
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

        /// <summary>The nearest place of a kind and how far it is; null if the city has none.</summary>
        public BikePlace Nearest(Vector3 from, BikePlaceKind kind, out float metres)
        {
            BikePlace best = null;
            metres = float.MaxValue;
            for (int i = 0; i < _places.Count; i++)
            {
                float distance = Vector3.Distance(_places[i].Position, from);
                if (_places[i].Kind == kind && distance < metres)
                {
                    metres = distance;
                    best = _places[i];
                }
            }
            return best;
        }

        /// <summary>Once a second: shows what is near the player, hides the rest. Builds at most one place per call.</summary>
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

        // Where a handed-over bike may stand, in the place's own frame (x across the pavement, z along it): along the
        // pavement first, since a place faces along its pavement and "to its side" is the shopfront or the kerb.
        private static readonly Vector2[] HandOver =
        {
            new Vector2(0f, 2.6f), new Vector2(0f, -2.6f), new Vector2(0f, 3.4f), new Vector2(0f, -3.4f),
            new Vector2(1.1f, 2.9f), new Vector2(-1.1f, 2.9f), new Vector2(1.1f, -2.9f), new Vector2(-1.1f, -2.9f),
            new Vector2(2.1f, 0f), new Vector2(-2.1f, 0f)
        };

        /// <summary>
        /// Where a bike handed over at a place is stood: beside it along the pavement, on pavement, clear of
        /// everything solid and in plain sight of the place (so never behind a wall). Where no pavement beside
        /// it is free, on any ground level with the place that is still in its sight. False if there is no room.
        /// </summary>
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
                    // Looked along above head height: whoever stands at the stand is not a wall.
                    if (Fits(spot, place.Rotation, BikeHalf, 0f) && Seen(place.Position, spot, 2.1f))
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
            _found = false;
        }

        // ---------------------------------------------------------------- what stands there

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
                        // a rail along the ground with three wheel hoops ahead of the forks, and the pay post at its end
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
