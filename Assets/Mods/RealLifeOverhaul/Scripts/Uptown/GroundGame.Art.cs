using System.Collections.Generic;
using Localizor;
using Localizor.LanguageChangeEvent;
using RealLifeOverhaul.NewGround;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RealLifeOverhaul.Uptown
{
    /// <summary>
    /// Uptown's street art, the game's own: its road modules copied along the district's streets (a street's 100 m, an
    /// avenue's 50 m, an avenue crossing a street), each copy made asleep and stripped before it wakes, so nothing of the
    /// template's own runs for it. A street's name over it is the district's; a crossing's light poles hang their red,
    /// yellow and green under the lamps the junction's lights switch, pole by pole to the approach whose light it is (the
    /// game hangs a light 5.7 m past the stop line); its crosswalks wait for the red of the road they cross, and those
    /// that lead onto no walking ground go; an avenue's sign, which names 1st Avenue, comes down. Parking along the kerbs
    /// and the pedestrians' zones come once the game is loaded, through new ground's own copies of the game's.
    /// </summary>
    public static partial class GroundGame
    {
        public const string ArtName = "RealLifeOverhaul Uptown streets";   // the copied modules, asleep until all are ready
        private const string StreetModule = "SM_RoadModule- 120-70";         // the game's 100 m of a street
        private const string AvenueModule = "SM_RoadModule 120-160-70";      // its 50 m of an avenue
        private const string CrossingModule = "SM_RoadModule Intersection_4-2 Lanes";   // its avenue crossing a street, with lights
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

        private static readonly Intersection[] Own = new Intersection[LayoutRules.FirstColumns + 1];   // each street's own crossing at 1st Avenue
        private static readonly List<ModulePlace> Places = new List<ModulePlace>(32);
        private static readonly List<GameObject> Copies = new List<GameObject>(32);
        private static readonly List<ParkingLaneGenerator> Lanes = new List<ParkingLaneGenerator>(32);
        private static readonly List<StationaryAiSpawner> Zones = new List<StationaryAiSpawner>(20);
        private static readonly List<PlanBox> ZoneBoxes = new List<PlanBox>(20);
        private static readonly float[] WayX = new float[4];      // one junction's ways: the mean heading of their stop points
        private static readonly float[] WayZ = new float[4];
        private static Road _street;
        private static Road _avenue;
        private static Intersection _anyCrossing;
        private static GameObject _art;

        /// <summary>The modules copied at this load.</summary>
        public static int Copied => Copies.Count;

        /// <summary>The light poles hung under the junctions' lamps, the crosswalks linked to a red, the signs taken down.</summary>
        public static int Lights { get; private set; }
        public static int Crossings { get; private set; }
        public static int SignsDown { get; private set; }

        /// <summary>The parking lanes and the pedestrians' zones made once the game was loaded.</summary>
        public static int ParkingLanes => Lanes.Count;
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
        /// The game's road modules the art is copied from, found once a load before anything changes: 9th Street's 100 m
        /// south of 1st Avenue, 1st Avenue's 50 m east of it, and each street's own crossing at 1st Avenue (a plain one;
        /// where the game has none of the plain kind, any of them). Each must be the size of its kind. Null, or why not.
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
            Intersection[] crossings = SceneFinds.All<Intersection>("uptown: the game's crossings to copy along its avenue (a search of the scene, behind the loading screen)");
            _anyCrossing = null;
            for (int k = 0; k <= LayoutRules.FirstColumns; k++)
            {
                Own[k] = NearestCrossing(crossings, LayoutRules.StreetX(k), LayoutRules.FirstAvenueZ);
                if (_anyCrossing == null)
                {
                    _anyCrossing = Own[k];
                }
            }
            return _anyCrossing == null ? "none of the game's crossings to copy (" + CrossingModule + ", of its size) was found on 1st Avenue" : null;
        }

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

        // A module of the size of its kind: its own mesh (a mesh the game batched with others would be the batch's) and unturned.
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
        /// Copies the game's modules along the district's streets once its streets are among the traffic's: each made asleep
        /// under one holder in the template's scene, its parking lanes taken out (new ground's copies come later), its name,
        /// lights, crosswalks and signs made the district's, then all woken together. Returns how many were copied.
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
            LayoutPlans.Modules(Places);
            for (int i = 0; i < Places.Count; i++)
            {
                ModulePlace place = Places[i];
                GameObject template = Template(place);
                GameObject copy = Object.Instantiate(template, _art.transform);
                copy.name = ArtName + " " + place.Kind + " " + place.Street;
                copy.transform.SetPositionAndRotation(new Vector3(place.X, template.transform.position.y, place.Z), template.transform.rotation);
                Strip(copy);
                if (place.Kind == ModuleKind.Crossing)
                {
                    Signs(copy, Own[place.Street] != null);
                    if (place.Junction >= 0 && place.Junction < build.Junctions.Count)
                    {
                        Wire(copy, build, build.Junctions[place.Junction], place.Junction);
                    }
                    else
                    {
                        Unlink(copy);
                    }
                }
                else
                {
                    LayoutStreet street = place.Kind == ModuleKind.Avenue ? LayoutStreets.Avenue(1) : LayoutStreets.Street(place.Street);
                    Name(copy, street != null ? street.NameKey.GetLocalization() : null);
                }
                Copies.Add(copy);
            }
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
                default:
                    Intersection own = place.Street >= 0 && place.Street < Own.Length ? Own[place.Street] : null;
                    return (own != null ? own : _anyCrossing).gameObject;
            }
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

        // A crossing's street signs: the avenue's names 1st Avenue, so it comes down; where the template is another
        // street's crossing, the street's comes down too.
        private static void Signs(GameObject copy, bool own)
        {
            Transform[] parts = copy.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < parts.Length; i++)
            {
                Transform sign = parts[i];
                if (sign.parent != null && sign.parent.name == SignsName && (!own || sign.name.Contains(AvenueSign)))
                {
                    sign.gameObject.SetActive(false);
                    SignsDown++;
                }
            }
        }

        // A crossing of the district's junction: each pole's lights go under the lamps of the way whose approach they
        // light, and each crosswalk waits for the red of the road it crosses; a crosswalk onto no walking ground goes.
        private static void Wire(GameObject copy, RoadBuild build, BuiltJunction junction, int index)
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
            int last = build.Junctions.Count - 1;
            CrosswalkToTrafficLightLink[] links = copy.GetComponentsInChildren<CrosswalkToTrafficLightLink>(true);
            for (int i = 0; i < links.Length; i++)
            {
                Vector3 local = copy.transform.InverseTransformPoint(links[i].transform.position);
                int way = -1;
                if (Mathf.Abs(local.x) < ArmFrom && local.z < -ArmFrom)
                {
                    way = street;   // over the street's arm south of the avenue
                }
                else if (Mathf.Abs(local.z) < ArmFrom && (local.x < -ArmFrom ? index > 0 : local.x > ArmFrom && index < last))
                {
                    way = avenue;   // over the avenue, on a side the avenue goes on
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
        /// Once the game is loaded: parking along the kerbs of the copied streets and avenue, each lane a copy of the
        /// template module's own (new ground's ParkingGame) where it stood in the template, and, with the pedestrians'
        /// ground laid, their zones on the pavements. Returns the lanes that could not be laid, said in <paramref name="why"/>.
        /// </summary>
        public static int Populate(bool people, out string why)
        {
            why = null;
            int refused = 0;
            for (int i = 0; i < Copies.Count && i < Places.Count; i++)
            {
                if (Places[i].Kind == ModuleKind.Crossing || Copies[i] == null)
                {
                    continue;
                }
                Transform template = Places[i].Kind == ModuleKind.Street ? _street.transform : _avenue.transform;
                ParkingLaneGenerator[] lanes = template.GetComponentsInChildren<ParkingLaneGenerator>(true);
                for (int l = 0; l < lanes.Length; l++)
                {
                    Vector3 at = Copies[i].transform.position + (lanes[l].transform.position - template.position);
                    Quaternion turn = lanes[l].transform.rotation;
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

        // The art goes with the city: the parking and zones first, so their cars and people go back to the game's pools.
        private static void DropArt()
        {
            Unpopulate();
            if (_art != null)
            {
                Object.Destroy(_art);
            }
            _art = null;
            Copies.Clear();
            Places.Clear();
            _street = null;
            _avenue = null;
            _anyCrossing = null;
            for (int k = 0; k < Own.Length; k++)
            {
                Own[k] = null;
            }
            Lights = 0;
            Crossings = 0;
            SignsDown = 0;
        }
    }
}
