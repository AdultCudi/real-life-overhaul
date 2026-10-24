using System.Collections.Generic;
using Localizor;
using Localizor.LanguageChangeEvent;
using RealLifeOverhaul.NewGround;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RealLifeOverhaul.Uptown
{
    /// <summary>
    /// Uptown's street art, the game's own: its road modules copied along the band's streets and avenues (a street's 100 m,
    /// an avenue's 50 m, an avenue crossing a street) and a block's ground on every block the backdrop keeps none (its ring of
    /// slabs laid as its middle, as the backdrop's own over the band: GroundGame.Paving.cs), each copy
    /// made asleep and stripped before it wakes, so nothing of the template's own runs for it; past the band's walls the
    /// streets and avenues go on one length more and the blocks round it get their ground, as scenery, so the camera never
    /// looks out on nothing. A street's name over it is the district's; a crossing's light poles hang their red, yellow and
    /// green under the lamps the junction's lights switch, pole by pole to the approach whose light it is (the game hangs a
    /// light 5.7 m past the stop line); its crosswalks wait for the red of the road they cross, and those over an arm with no
    /// lanes go; an avenue's sign, which names 1st Avenue, comes down but on 1st Avenue's own continuations. Parking along
    /// the kerbs and the pedestrians' zones come once the game is loaded, through new ground's own copies of the game's.
    /// </summary>
    public static partial class GroundGame
    {
        public const string ArtName = "RealLifeOverhaul Uptown streets";   // the copied modules, asleep until all are ready
        private const string StreetModule = "SM_RoadModule- 120-70";         // the game's 100 m of a street
        private const string AvenueModule = "SM_RoadModule 120-160-70";      // its 50 m of an avenue
        private const string CrossingModule = "SM_RoadModule Intersection_4-2 Lanes";   // its avenue crossing a street, with lights
        private const string GroundPlanesRoot = "AdditionalGroundPlanes";    // Manhattan's own block grounds, not in a static batch
        private const string GroundPlane = "SM_120-70 GroundPlane";          // a block's ground, 70 by 120 m
        private const string TrafficName = "Traffic";                        // a crossing's light poles
        private const string RedName = "RedLight";                           // a pole's lights, as the game names them
        private const string YellowName = "YellowLight";
        private const string GreenName = "Greenlight";
        private const string SignsName = "StreetNameSigns";                  // a pole's street signs, the name in each sign's mesh
        private const string AvenueSign = "Avenue";                          // in the name of an avenue's sign
        private const float SizeNear = 1f;        // m a template's mesh may differ from the module's size, either way
        private const float OwnNear = 1f;         // m from a street's middle its own crossing at 1st Avenue is looked for
        private const float LampReach = 4f;       // m: a pole's red light this near where an approach's light hangs is that approach's
        private const float LampAhead = 5.7f;     // m past its stop point the game hangs an approach's light
        private const float ArmFrom = 5f;         // m from a crossing's middle a crosswalk stands on one of its arms
        private const float BlockWide = 70f;      // m: a block's ground across, west to east
        private const float BlockDeep = 120f;     // m: and north to south
        private const string LitterName = "TrashDecoration";   // a module's litter: bags, cans, papers; the bags are solid

        private static readonly Intersection[] Own = new Intersection[LayoutRules.FirstColumns + 1];   // each Manhattan street's own crossing at 1st Avenue
        private static readonly List<ModulePlace> Places = new List<ModulePlace>(512);
        private static readonly List<GameObject> Copies = new List<GameObject>(512);
        private static readonly List<int> CopyPlace = new List<int>(512);        // each copy's place among Places
        private static readonly List<ParkingLaneGenerator> Lanes = new List<ParkingLaneGenerator>(256);
        private static readonly List<StationaryAiSpawner> Zones = new List<StationaryAiSpawner>(128);
        private static readonly List<PlanBox> ZoneBoxes = new List<PlanBox>(128);
        private static readonly List<Bounds> KeptGrounds = new List<Bounds>(64);   // the backdrop's own block grounds, which stay
        private static readonly List<GroundRect> Mouths = new List<GroundRect>(4);  // where a car crosses into a big special place (BigPlacesRules.Mouths)
        private static readonly float[] WayX = new float[4];      // one junction's ways: the summed heading of their stop points
        private static readonly float[] WayZ = new float[4];
        private static Road _street;
        private static Road _avenue;
        private static Intersection _anyCrossing;
        private static GameObject _groundPlane;
        private static GameObject _art;

        /// <summary>The pieces copied at this load, and of them the blocks' grounds and the scenery past the walls.</summary>
        public static int Copied => Copies.Count;
        public static int GroundsLaid { get; private set; }
        public static int SceneryLaid { get; private set; }

        /// <summary>The light poles hung under the junctions' lamps, the crosswalks linked to a red, the signs taken down.</summary>
        public static int Lights { get; private set; }
        public static int Crossings { get; private set; }
        public static int SignsDown { get; private set; }

        /// <summary>The parking lanes and the pedestrians' zones made once the game was loaded.</summary>
        public static int ParkingLanes => Lanes.Count;

        /// <summary>The parking lanes not laid at this load as they meet a big special place's mouth, and the bags of litter put away from them.</summary>
        public static int LanesKeptOff { get; private set; }
        public static int LitterAway { get; private set; }
        public static int PeopleZones => Zones.Count;

        /// <summary>The game's cars parked along Uptown's kerbs now.</summary>
        public static int Parked
        {
            get
            {
                int parked = 0;
                for (int i = 0; i < Lanes.Count; i++)
                {
                    parked += ParkingGame.Parked(Lanes[i]);
                }
                return parked;
            }
        }

        /// <summary>
        /// The game's pieces the art is copied from, found once a load before anything changes: 9th Street's 100 m south
        /// of 1st Avenue, 1st Avenue's 50 m east of it, each street's own crossing at 1st Avenue (a plain one; where the game
        /// has none of the plain kind, any of them), and a block's ground among Manhattan's own (one in no static batch).
        /// Each must be the size of its kind. Null, or why not. A block's ground missing leaves the blocks the backdrop has
        /// none on bare, said in the log; the district stands all the same.
        /// </summary>
        public static string FindTemplates()
        {
            Road[] roads = SceneFinds.All<Road>("uptown: the game's road modules to copy along its streets (a search of the scene, behind the loading screen)");
            float streetZ = LayoutRules.FirstAvenueZ - LayoutRules.CrossingHalfZ - LayoutRules.StreetModule * 0.5f;
            _street = NearestRoad(roads, StreetModule, LayoutRules.StreetX(0), streetZ, LayoutRules.StreetHalf * 2f, LayoutRules.StreetModule);
            _avenue = NearestRoad(roads, AvenueModule, LayoutRules.StreetX(0) + LayoutRules.StreetPitch * 0.5f, LayoutRules.FirstAvenueZ,
                LayoutRules.AvenueModule, LayoutRules.AvenueHalf * 2f);
            if (_street == null || _avenue == null)
            {
                return "the game's " + (_street == null ? "street" : "avenue") + " module to copy (" + (_street == null ? StreetModule : AvenueModule)
                       + ", of its size) was not found at Manhattan's north edge";
            }
            Intersection[] crossings = SceneFinds.All<Intersection>("uptown: the game's crossings to copy along its avenues (a search of the scene, behind the loading screen)");
            _anyCrossing = null;
            for (int k = 0; k <= LayoutRules.FirstColumns; k++)
            {
                Own[k] = NearestCrossing(crossings, LayoutRules.StreetX(k), LayoutRules.FirstAvenueZ);
                if (_anyCrossing == null)
                {
                    _anyCrossing = Own[k];
                }
            }
            if (_anyCrossing == null)
            {
                return "none of the game's crossings to copy (" + CrossingModule + ", of its size) was found on 1st Avenue";
            }
            _groundPlane = GroundTemplate();
            KeptGrounds.Clear();
            KeptPlanes.Clear();
            Transform kept = _backdrop != null ? _backdrop.Find(KeptName) : null;
            Renderer[] planes = kept != null ? kept.GetComponentsInChildren<Renderer>(false) : new Renderer[0];
            for (int i = 0; i < planes.Length; i++)
            {
                if (planes[i] != null)
                {
                    KeptGrounds.Add(planes[i].bounds);
                    KeptPlanes.Add(planes[i]);
                }
            }
            return null;
        }

        // Manhattan's own block ground nearest 9th Street's block south of 1st Avenue, of a block's size and in no static batch.
        private static GameObject GroundTemplate()
        {
            GameObject best = null;
            float nearest = float.MaxValue, x = LayoutRules.StreetX(0) + LayoutRules.StreetPitch * 0.5f, z = LayoutRules.FirstAvenueZ - LayoutRules.AvenuePitch * 0.5f;
            for (int s = 0; s < SceneManager.sceneCount; s++)
            {
                Scene scene = SceneManager.GetSceneAt(s);
                if (!scene.isLoaded)
                {
                    continue;
                }
                SceneRoots.Clear();
                scene.GetRootGameObjects(SceneRoots);
                for (int i = 0; i < SceneRoots.Count; i++)
                {
                    if (SceneRoots[i] == null || SceneRoots[i].name != GroundPlanesRoot)
                    {
                        continue;
                    }
                    Transform root = SceneRoots[i].transform;
                    for (int c = 0; c < root.childCount; c++)
                    {
                        GameObject plane = root.GetChild(c).gameObject;
                        if (!plane.activeInHierarchy || !plane.name.StartsWith(GroundPlane, System.StringComparison.Ordinal) || !Sized(plane, BlockWide, BlockDeep))
                        {
                            continue;
                        }
                        Vector3 at = plane.transform.position;
                        float d = (at.x - x) * (at.x - x) + (at.z - z) * (at.z - z);
                        if (d < nearest)
                        {
                            nearest = d;
                            best = plane;
                        }
                    }
                }
            }
            SceneRoots.Clear();
            return best;
        }

        /// <summary>True when a block's ground template was found at this load.</summary>
        public static bool HasGroundTemplate => _groundPlane != null;

        private static Road NearestRoad(Road[] roads, string name, float x, float z, float sizeX, float sizeZ)
        {
            Road best = null;
            float nearest = float.MaxValue;
            for (int i = 0; i < roads.Length; i++)
            {
                Road road = roads[i];
                if (road == null || !road.name.StartsWith(name, System.StringComparison.Ordinal) || !Sized(road.gameObject, sizeX, sizeZ))
                {
                    continue;
                }
                Vector3 at = road.transform.position;
                float d = (at.x - x) * (at.x - x) + (at.z - z) * (at.z - z);
                if (d < nearest)
                {
                    nearest = d;
                    best = road;
                }
            }
            return best;
        }

        private static Intersection NearestCrossing(Intersection[] crossings, float x, float z)
        {
            for (int i = 0; i < crossings.Length; i++)
            {
                Intersection crossing = crossings[i];
                if (crossing == null || !crossing.name.StartsWith(CrossingModule, System.StringComparison.Ordinal)
                    || !Sized(crossing.gameObject, LayoutRules.CrossingHalfX * 2f, LayoutRules.CrossingHalfZ * 2f))
                {
                    continue;
                }
                Vector3 at = crossing.transform.position;
                if (Mathf.Abs(at.x - x) <= OwnNear && Mathf.Abs(at.z - z) <= OwnNear)
                {
                    return crossing;
                }
            }
            return null;
        }

        // A piece of the size of its kind: its own mesh (a mesh the game batched with others would be the batch's) and unturned.
        private static bool Sized(GameObject module, float sizeX, float sizeZ)
        {
            if (!module.TryGetComponent(out MeshFilter filter) || filter.sharedMesh == null || Quaternion.Angle(module.transform.rotation, Quaternion.identity) > 1f)
            {
                return false;
            }
            Vector3 size = Vector3.Scale(filter.sharedMesh.bounds.size, module.transform.lossyScale);
            return Mathf.Abs(size.x - sizeX) <= SizeNear && Mathf.Abs(size.z - sizeZ) <= SizeNear;
        }

        /// <summary>
        /// Copies the game's pieces along the band once its streets are among the traffic's: each made asleep under one
        /// holder in the template's scene, its parking lanes taken out (new ground's copies come later), its name, lights,
        /// crosswalks and signs made the district's, a block's ground only where the backdrop keeps none; then all woken
        /// together. Returns how many were copied.
        /// </summary>
        public static int CopyArt(RoadBuild build)
        {
            if (_art != null || _street == null || _avenue == null || _anyCrossing == null)
            {
                return Copies.Count;
            }
            _art = new GameObject(ArtName);
            _art.SetActive(false);
            SceneManager.MoveGameObjectToScene(_art, _street.gameObject.scene);
            FindMouths();
            LayoutPlans.Modules(Places);
            for (int i = 0; i < Places.Count; i++)
            {
                ModulePlace place = Places[i];
                GameObject template = Template(place);
                if (template == null || (place.Kind == ModuleKind.Ground && Kept(place.X, place.Z)))
                {
                    continue;
                }
                GameObject copy = Object.Instantiate(template, _art.transform);
                copy.name = ArtName + " " + place.Kind + " " + place.Street + " " + place.Row;
                Vector3 offset = place.Kind == ModuleKind.Ground ? template.transform.position - Middle(template) : Vector3.zero;   // a road module's pivot is its middle
                copy.transform.SetPositionAndRotation(new Vector3(place.X + offset.x, template.transform.position.y, place.Z + offset.z), template.transform.rotation);
                Strip(copy);
                ClearMouths(copy);
                switch (place.Kind)
                {
                    case ModuleKind.Crossing:
                        Signs(copy, place.Street >= 0 && place.Street < Own.Length && Own[place.Street] != null, place.Row == 0);
                        if (place.Junction >= 0 && place.Junction < build.Junctions.Count)
                        {
                            Wire(copy, build, build.Junctions[place.Junction], place);
                        }
                        else
                        {
                            Unlink(copy);
                        }
                        break;
                    case ModuleKind.Ground:
                        GroundsLaid++;
                        Unpave(copy.GetComponent<Renderer>(), false);   // a copy goes with the art: nothing to give back
                        break;
                    default:
                        if (place.Kind == ModuleKind.Street || place.Row > 0)
                        {
                            LayoutStreet street = place.Scenery ? null : place.Kind == ModuleKind.Avenue ? LayoutStreets.Avenue(place.Row) : LayoutStreets.Street(place.Street);
                            Name(copy, street != null ? street.NameKey.GetLocalization() : null);   // 1st Avenue's continuations keep its own name
                        }
                        break;
                }
                SceneryLaid += place.Scenery ? 1 : 0;
                Copies.Add(copy);
                CopyPlace.Add(i);
            }
            UnpaveKept();
            _art.SetActive(true);
            return Copies.Count;
        }

        private static GameObject Template(ModulePlace place)
        {
            switch (place.Kind)
            {
                case ModuleKind.Street:
                    return _street.gameObject;
                case ModuleKind.Avenue:
                    return _avenue.gameObject;
                case ModuleKind.Ground:
                    return _groundPlane;
                default:
                    Intersection own = place.Street >= 0 && place.Street < Own.Length ? Own[place.Street] : null;
                    return (own != null ? own : _anyCrossing).gameObject;
            }
        }

        // The middle of a piece as it is drawn: its own mesh's, where its pivot may stand elsewhere.
        private static Vector3 Middle(GameObject template)
        {
            return template.TryGetComponent(out Renderer shown) ? shown.bounds.center : template.transform.position;
        }

        // True where one of the backdrop's own block grounds, which stay, already covers a block's middle.
        private static bool Kept(float x, float z)
        {
            for (int i = 0; i < KeptGrounds.Count; i++)
            {
                Bounds b = KeptGrounds[i];
                if (x >= b.min.x && x <= b.max.x && z >= b.min.z && z <= b.max.z)
                {
                    return true;
                }
            }
            return false;
        }

        // The big special places' mouths, from fixed data once a load: whether or not a place stands at this load (the
        // district decides that later, from the save), its mouth is kept clear, which costs a lane of parking at most.
        private static void FindMouths()
        {
            Mouths.Clear();
            LitterAway = 0;
            var plan = new List<BuildingsLot>(512);
            LifeGame.Lots(plan);
            for (int i = 0; i < BigPlacesRules.All.Length; i++)
            {
                if (BigPlacesRules.Ground(BigPlacesRules.All[i], plan, out BuildingsLot ground))
                {
                    BigPlacesRules.Mouths(BigPlacesRules.All[i], ground, Mouths);
                }
            }
        }

        // A module's bags of litter lying in a big place's mouth go before the copy wakes, so the ground grows over that
        // pavement whole and a car crosses it into the place; the lamps, signs and lights stay.
        private static void ClearMouths(GameObject copy)
        {
            Transform litter = Mouths.Count > 0 ? copy.transform.Find(LitterName) : null;
            if (litter == null)
            {
                return;
            }
            Collider[] solid = litter.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < solid.Length; i++)
            {
                Vector3 at = solid[i].transform.position;
                if (BigPlacesRules.In(Mouths, at.x, at.z))
                {
                    solid[i].gameObject.SetActive(false);
                    LitterAway++;
                }
            }
        }

        // True when a parking lane laid there would meet a big place's mouth: its box's reach along the world's axes.
        private static bool InMouth(ParkingLaneGenerator lane, Vector3 at, Quaternion turn)
        {
            BoxCollider box = Mouths.Count > 0 ? lane.GetComponent<BoxCollider>() : null;
            if (box == null)
            {
                return false;
            }
            Vector3 scale = lane.transform.lossyScale;
            Vector3 centre = at + turn * Vector3.Scale(box.center, scale);
            Vector3 across = turn * Vector3.right * (box.size.x * Mathf.Abs(scale.x) * 0.5f);
            Vector3 along = turn * Vector3.forward * (box.size.z * Mathf.Abs(scale.z) * 0.5f);
            float reachX = Mathf.Abs(across.x) + Mathf.Abs(along.x), reachZ = Mathf.Abs(across.z) + Mathf.Abs(along.z);
            return BigPlacesRules.Meets(Mouths, centre.x - reachX, centre.z - reachZ, centre.x + reachX, centre.z + reachZ);
        }

        // The template's parking lanes go before the copy wakes: their own copies, placed by new ground, park the cars.
        private static void Strip(GameObject copy)
        {
            ParkingLaneGenerator[] lanes = copy.GetComponentsInChildren<ParkingLaneGenerator>(true);
            for (int i = 0; i < lanes.Length; i++)
            {
                Object.DestroyImmediate(lanes[i].gameObject);
            }
        }

        // The name a street's module shows over it on the city map: the game writes its label from the module's street
        // when it wakes. Without the field the label would name the template's street, so it is put away.
        private static void Name(GameObject copy, string name)
        {
            Road road = copy.GetComponent<Road>();
            if (road != null && name != null && GameContract.RoadStreetName.Ok)
            {
                GameContract.RoadStreetName.Field.SetValue(road, name);
                return;
            }
            TextLocalizationComponent label = copy.GetComponentInChildren<TextLocalizationComponent>(true);
            if (label != null)
            {
                label.gameObject.SetActive(false);
            }
        }

        // A crossing's street signs: the avenue's names 1st Avenue, so it comes down but on 1st Avenue's continuations; where
        // the template is another street's crossing, the street's comes down too.
        private static void Signs(GameObject copy, bool own, bool firstAvenue)
        {
            Transform[] parts = copy.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < parts.Length; i++)
            {
                Transform sign = parts[i];
                if (sign.parent == null || sign.parent.name != SignsName)
                {
                    continue;
                }
                bool avenue = sign.name.Contains(AvenueSign);
                if (avenue ? !firstAvenue : !own)
                {
                    sign.gameObject.SetActive(false);
                    SignsDown++;
                }
            }
        }

        // A crossing of the district's junction: each pole's lights go under the lamps of the way whose approach they
        // light, and each crosswalk waits for the red of the road it crosses; a crosswalk over an arm with no lanes goes.
        private static void Wire(GameObject copy, RoadBuild build, BuiltJunction junction, ModulePlace place)
        {
            int ways = Mathf.Min(junction.Ways.Count, WayX.Length);
            int street = -1, avenue = -1;
            for (int w = 0; w < ways; w++)
            {
                Heading(build, junction.Ways[w], out WayX[w], out WayZ[w]);
                if (Mathf.Abs(WayZ[w]) > Mathf.Abs(WayX[w]))
                {
                    street = w;
                }
                else
                {
                    avenue = w;
                }
            }
            Transform traffic = copy.transform.Find(TrafficName);
            Transform[] parts = traffic != null ? traffic.GetComponentsInChildren<Transform>(true) : new Transform[0];
            for (int i = 0; i < parts.Length; i++)
            {
                Transform red = parts[i];
                if (red.name != RedName)
                {
                    continue;
                }
                int way = LitBy(build, junction, red.position);
                Transform yellow = way >= 0 ? Sibling(red, YellowName) : null;
                Transform green = way >= 0 ? Sibling(red, GreenName) : null;
                GameObject redLamp = way >= 0 ? RoadsGame.LampOf(junction, way, "red") : null;
                GameObject yellowLamp = way >= 0 ? RoadsGame.LampOf(junction, way, "yellow") : null;
                GameObject greenLamp = way >= 0 ? RoadsGame.LampOf(junction, way, "green") : null;
                if (yellow == null || green == null || redLamp == null || yellowLamp == null || greenLamp == null)
                {
                    continue;   // a pole no approach of the district's comes to stays dark, as the game's own at its T junctions
                }
                Hang(red, redLamp);
                Hang(yellow, yellowLamp);
                Hang(green, greenLamp);
                Lights++;
            }
            bool north = LayoutPlans.HasLanes(place.Street, place.Row + 1), south = LayoutPlans.HasLanes(place.Street, place.Row);
            bool west = LayoutPlans.AvenueArm(place.Street, place.Row, false), east = LayoutPlans.AvenueArm(place.Street, place.Row, true);
            CrosswalkToTrafficLightLink[] links = copy.GetComponentsInChildren<CrosswalkToTrafficLightLink>(true);
            for (int i = 0; i < links.Length; i++)
            {
                Vector3 local = copy.transform.InverseTransformPoint(links[i].transform.position);
                int way = -1;
                if (Mathf.Abs(local.x) < ArmFrom && (local.z < -ArmFrom ? south : local.z > ArmFrom && north))
                {
                    way = street;   // over the street's arm north or south of the avenue, where its lanes are
                }
                else if (Mathf.Abs(local.z) < ArmFrom && (local.x < -ArmFrom ? west : local.x > ArmFrom && east))
                {
                    way = avenue;   // over the avenue, on a side the avenue's lanes go on
                }
                GameObject red = way >= 0 ? RoadsGame.RedLamp(junction, way) : null;
                if (red == null)
                {
                    Object.DestroyImmediate(links[i].gameObject);
                    continue;
                }
                links[i].redTrafficLight = red;
                Crossings++;
            }
        }

        // A crossing where no lanes meet: its crosswalks would wait for a red no light ever lights, so they go.
        private static void Unlink(GameObject copy)
        {
            CrosswalkToTrafficLightLink[] links = copy.GetComponentsInChildren<CrosswalkToTrafficLightLink>(true);
            for (int i = 0; i < links.Length; i++)
            {
                Object.DestroyImmediate(links[i].gameObject);
            }
        }

        // The way of a junction whose approach a light at this place lights: the nearest of its approaches' lights, if near.
        private static int LitBy(RoadBuild build, BuiltJunction junction, Vector3 at)
        {
            int best = -1;
            float nearest = LampReach * LampReach;
            for (int w = 0; w < junction.Ways.Count; w++)
            {
                List<int> stops = junction.Ways[w];
                for (int s = 0; s < stops.Count; s++)
                {
                    LanePoint stop = build.At(stops[s]);
                    if (stop == null || !Ahead(build, stop, out float dx, out float dz))
                    {
                        continue;
                    }
                    float lx = stop.X + dx * LampAhead - at.x, lz = stop.Z + dz * LampAhead - at.z;
                    if (lx * lx + lz * lz < nearest)
                    {
                        nearest = lx * lx + lz * lz;
                        best = w;
                    }
                }
            }
            return best;
        }

        // The axis a way's lanes come in along, summed over its stop points: an avenue's two ways on one green come in from
        // both ends, so it is the size of each part that counts, not its sign.
        private static void Heading(RoadBuild build, List<int> stops, out float x, out float z)
        {
            x = 0f;
            z = 0f;
            for (int s = 0; s < stops.Count; s++)
            {
                LanePoint stop = build.At(stops[s]);
                if (stop != null && Ahead(build, stop, out float dx, out float dz))
                {
                    x += Mathf.Abs(dx);
                    z += Mathf.Abs(dz);
                }
            }
        }

        // The way a lane comes into its stop point, from the point before it.
        private static bool Ahead(RoadBuild build, LanePoint stop, out float dx, out float dz)
        {
            dx = 0f;
            dz = 0f;
            LanePoint before = stop.Prev.Count > 0 ? build.At(stop.Prev[0]) : null;
            if (before == null)
            {
                return false;
            }
            float x = stop.X - before.X, z = stop.Z - before.Z, length = Mathf.Sqrt(x * x + z * z);
            if (length < 0.01f)
            {
                return false;
            }
            dx = x / length;
            dz = z / length;
            return true;
        }

        private static Transform Sibling(Transform light, string name)
        {
            return light.parent != null ? light.parent.Find(name) : null;
        }

        // A light under the lamp its junction switches: it shows while the lamp is on, where it hangs on its pole.
        private static void Hang(Transform light, GameObject lamp)
        {
            light.SetParent(lamp.transform, true);
            light.gameObject.SetActive(true);
        }

        /// <summary>
        /// Once the game is loaded: parking along the kerbs of the band's copied streets and avenues (never the scenery's),
        /// each lane a copy of the template module's own (new ground's ParkingGame) where it stood in the template, and, with
        /// the pedestrians' ground laid, their zones on the pavements. Returns the lanes that could not be laid, said in
        /// <paramref name="why"/>.
        /// </summary>
        public static int Populate(bool people, out string why)
        {
            why = null;
            int refused = 0;
            LanesKeptOff = 0;
            for (int i = 0; i < Copies.Count; i++)
            {
                ModulePlace place = Places[CopyPlace[i]];
                if (place.Scenery || (place.Kind != ModuleKind.Street && place.Kind != ModuleKind.Avenue) || Copies[i] == null)
                {
                    continue;
                }
                Transform template = place.Kind == ModuleKind.Street ? _street.transform : _avenue.transform;
                ParkingLaneGenerator[] lanes = template.GetComponentsInChildren<ParkingLaneGenerator>(true);
                for (int l = 0; l < lanes.Length; l++)
                {
                    Vector3 at = Copies[i].transform.position + (lanes[l].transform.position - template.position);
                    Quaternion turn = lanes[l].transform.rotation;
                    if (InMouth(lanes[l], at, turn))
                    {
                        LanesKeptOff++;   // a car crosses that kerb into a big special place
                        continue;
                    }
                    string refusal = null;
                    ParkingLaneGenerator made = ParkingGame.Room(lanes[l], at, turn, out refusal) ? ParkingGame.Copy(lanes[l], at, turn, out refusal) : null;
                    if (made != null)
                    {
                        Lanes.Add(made);
                        continue;
                    }
                    refused++;
                    why = why ?? Copies[i].name + ": " + refusal;
                }
            }
            if (people)
            {
                LayoutPlans.Zones(ZoneBoxes);
                for (int i = 0; i < ZoneBoxes.Count; i++)
                {
                    PlanBox box = ZoneBoxes[i];
                    StationaryAiSpawner zone = PeopleGame.Zone(new Vector3(box.X, box.Top, box.Z), 0f, new Vector3(box.HalfX * 2f, 0f, box.HalfZ * 2f), out string none);
                    if (zone == null)
                    {
                        why = why ?? none;
                        break;
                    }
                    Zones.Add(zone);
                }
            }
            return refused;
        }

        /// <summary>Takes the parking and the zones away: the cars and the people go back to the game's pools.</summary>
        public static void Unpopulate()
        {
            for (int i = 0; i < Lanes.Count; i++)
            {
                ParkingLaneGenerator lane = Lanes[i];
                ParkingGame.Remove(ref lane);
            }
            Lanes.Clear();
            for (int i = 0; i < Zones.Count; i++)
            {
                StationaryAiSpawner zone = Zones[i];
                PeopleGame.Unzone(ref zone);
            }
            Zones.Clear();
        }

        // The art goes with the city: the parking and zones first, so their cars and people go back to the game's pools; the
        // backdrop's block grounds get their own materials back.
        private static void DropArt()
        {
            Unpopulate();
            Repaved();
            if (_art != null)
            {
                Object.Destroy(_art);
            }
            _art = null;
            Copies.Clear();
            CopyPlace.Clear();
            Places.Clear();
            KeptGrounds.Clear();
            _street = null;
            _avenue = null;
            _anyCrossing = null;
            _groundPlane = null;
            for (int k = 0; k < Own.Length; k++)
            {
                Own[k] = null;
            }
            Lights = 0;
            Crossings = 0;
            SignsDown = 0;
            GroundsLaid = 0;
            SceneryLaid = 0;
        }
    }
}
