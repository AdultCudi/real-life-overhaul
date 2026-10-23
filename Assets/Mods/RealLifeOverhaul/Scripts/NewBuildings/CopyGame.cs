using System;
using System.Collections.Generic;
using BAModAPI;
using Buildings;
using Parking.UndergroundParking;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RealLifeOverhaul.NewBuildings
{
    /// <summary>
    /// The copy engine's side of the game, the only place it touches the game. The city's buildings are whole designs placed
    /// in the scenes, and every city scene is loaded at once, so any of them can be copied with Instantiate: the copy
    /// shares the game's meshes, materials and textures (nothing more on the graphics card), and as the game's light is
    /// all real time a copy has the night windows, the shop glass, the rain and the see-through fade of the game's own.
    /// A copy is made from a stripped template kept asleep under a holder that is switched off, as Core/GameFurniture
    /// keeps the game's furniture: one Instantiate a copy. A building drawn in a static batch is never copied, as the
    /// copy would draw at the original's place. Two kinds of copy: a working one keeps the game's controller (a made
    /// building, which is never destroyed in a session) and a decorative one has it taken off (<see cref="CopyLook"/>).
    /// The ground a copy is stood on is checked in <c>CopyGame.Ground.cs</c>. Nothing here runs by itself: the catalogue
    /// is measured behind the loading screen at a load with made buildings, or a slice a frame in play when first
    /// wanted; a template is made on first use.
    /// </summary>
    internal static partial class CopyGame
    {
        private const string SpecialType = NewBuildingsRules.SpecialType;
        private const string HamptonsHouseSize = "ba:buildingsize_t";   // the Hamptons' houses: outside and inside are one prefab
        private const double SliceMs = 1.0;                              // ms of a frame the catalogue may take in play
        private const int LookEvery = 4;                                 // buildings measured between two looks at the clock

        public static IModLogger Logger;

        private static readonly List<DesignEntry> Designs = new List<DesignEntry>(512);
        private static readonly List<CityBuildingController> Sources = new List<CityBuildingController>(512);   // by DesignEntry.Source
        private static readonly Dictionary<string, int> ByKey = new Dictionary<string, int>(512, StringComparer.Ordinal);   // "name|size": its design
        private static readonly Dictionary<int, Transform> Templates = new Dictionary<int, Transform>(32);   // a design's template, working ones at -1 - index
        private static readonly List<Renderer> Drawn = new List<Renderer>(128);
        private static readonly List<Component> Parts = new List<Component>(256);
        private static readonly List<MeshFilter> Meshes = new List<MeshFilter>(64);

        private static Transform _asleep;      // switched off: what is under it never wakes
        private static Transform _standing;    // the copies stand under it
        private static CityBuildingController[] _measuring;   // the city's list being measured a slice at a time in play
        private static int _cursor;
        private static int _batched, _dlc, _skipped, _measured;
        private static bool _ready;

        /// <summary>True once the catalogue has been measured in this city.</summary>
        public static bool Ready => _ready;

        /// <summary>True while the catalogue is being measured a slice at a time.</summary>
        public static bool Measuring => _measuring != null;

        public static int DesignCount => Designs.Count;

        public static IList<DesignEntry> Catalogue => Designs;

        /// <summary>The floor area (m2) of the game's building a design is measured and copied from, or 0 when it is gone.</summary>
        public static int SourceArea(int design)
        {
            CityBuildingController source = design >= 0 && design < Designs.Count ? Sources[Designs[design].Source] : null;
            return source != null && source.building != null ? source.building.totalSqm : 0;
        }

        /// <summary>The copies stand under <paramref name="root"/>, the templates sleep under a holder of it switched off; both go with the city.</summary>
        public static void Begin(Transform root)
        {
            Forget();
            var asleep = new GameObject("RealLife Building Templates (asleep)");
            asleep.SetActive(false);
            asleep.transform.SetParent(root, false);
            _asleep = asleep.transform;
            var standing = new GameObject("RealLife New Buildings");
            standing.transform.SetParent(root, false);
            _standing = standing.transform;
        }

        /// <summary>The city is going: the catalogue, the templates and the ground's record are let go (the objects go with the mod's root).</summary>
        public static void Forget()
        {
            Designs.Clear();
            Sources.Clear();
            ByKey.Clear();
            Templates.Clear();
            ForgetGround();
            _measuring = null;
            _cursor = 0;
            _ready = false;
            _asleep = null;
            _standing = null;
        }

        // ---------------------------------------------------------------- the catalogue

        /// <summary>
        /// Behind the loading screen: every building of the city measured at once from <paramref name="all"/> (the scene's,
        /// inactive ones too, as a load with made buildings has them), and every game building's ground kept for the checks.
        /// </summary>
        public static void MeasureAll(CityBuildingController[] all)
        {
            Designs.Clear();
            Sources.Clear();
            ByKey.Clear();
            _batched = _dlc = _skipped = _measured = 0;
            for (int i = 0; all != null && i < all.Length; i++)
            {
                Measure(all[i]);
            }
            Finish();
        }

        /// <summary>In play: begins measuring the city's own list of its buildings (no search of the scene), a slice a frame.</summary>
        public static bool BeginMeasuring()
        {
            if (_ready || _measuring != null)
            {
                return true;
            }
            CityBuildingController[] open = GameDlc.Open(InstanceBehavior<CityManager>.Instance);   // without what a DLC keeps
            if (open == null || open.Length == 0)
            {
                return false;
            }
            Designs.Clear();
            Sources.Clear();
            ByKey.Clear();
            BeginGround();
            _batched = _dlc = _skipped = _measured = 0;
            _measuring = open;
            _cursor = 0;
            return true;
        }

        /// <summary>One slice of the measuring in play (about a millisecond); true once the catalogue is ready.</summary>
        public static bool MeasureSlice()
        {
            if (_measuring == null)
            {
                return _ready;
            }
            WorkSlice slice = WorkSlice.Of(SliceMs, LookEvery);
            while (_cursor < _measuring.Length && slice.More())
            {
                Measure(_measuring[_cursor++]);
            }
            if (_cursor < _measuring.Length)
            {
                return false;
            }
            _measuring = null;
            Finish();
            return true;
        }

        private static void Finish()
        {
            Designs.Sort(NewBuildingsRules.CompareDesigns);
            ByKey.Clear();
            _batched = 0;
            for (int i = 0; i < Designs.Count; i++)
            {
                ByKey[Designs[i].Name + "|" + Designs[i].Size] = i;
                _batched += Designs[i].Batched ? 1 : 0;
            }
            _ready = true;
            Logger?.Info("Copy check (catalogue): " + _measured + " of the city's buildings measured, " + Designs.Count + " designs by size (" + _batched
                         + " of them drawn in a static batch, never copied); " + _dlc + " buildings for a DLC left out, " + _skipped + " of a kind never copied (special, a Hamptons house, "
                         + "another controller, no door, leaning): " + (Designs.Count > _batched ? "PASS" : "FAIL, no design can be copied"));
        }

        // One building: its ground for the checks, and a design entry if it can be copied.
        private static void Measure(CityBuildingController c)
        {
            if (c == null || c.building == null)
            {
                return;
            }
            _measured++;
            if (MadeBuildings.Of(c) != null)
            {
                return;   // the mod's own: neither a design nor the game's ground
            }
            Building b = c.building;
            bool batched = Batched(c, out _, out _);
            KeepGround(c);
            if (b.BuildingType == SpecialType || b.BuildingSize == HamptonsHouseSize || c.GetType() != typeof(CityBuildingController) || c.entranceDoors == null
                || c.entranceDoors.Length == 0 || c.entranceDoors[0] == null || c.entranceDoors[0].doorTransform == null || TakenElsewhere(c))
            {
                _skipped++;
                return;
            }
            if (OfDlc(c))
            {
                _dlc++;   // never a pick a player without the DLC could not have (the shared helper for this rule is another part's)
                return;
            }
            string name = NewBuildingsRules.DesignName(c.name);
            string key = name + "|" + b.BuildingSize;
            if (ByKey.TryGetValue(key, out int known))
            {
                DesignEntry was = Designs[known];
                was.Uses++;
                Building from = Sources[was.Source] != null ? Sources[was.Source].building : null;
                if ((from == null || NewBuildingsRules.MeasuredFrom(batched, b.StreetName, b.StreetNumber, was.Batched, from.StreetName, from.StreetNumber)) && Describe(c, was))
                {
                    Sources[was.Source] = c;   // the scene lists its buildings in no fixed order: the rule picks the same one everywhere
                    was.Batched = batched;
                    was.Version = b.BuildingVersion;
                    was.Type = b.BuildingType;
                }
                return;
            }
            var entry = new DesignEntry { Name = name, Size = b.BuildingSize, Version = b.BuildingVersion, Type = b.BuildingType, Uses = 1, Batched = batched };
            if (!Describe(c, entry))
            {
                _skipped++;
                return;
            }
            entry.Source = Sources.Count;
            Sources.Add(c);
            ByKey[key] = Designs.Count;
            Designs.Add(entry);
        }

        // A building of a DLC, or with content of one in it or round it (shown or hidden by ownership): never a design, so that
        // what a pick may be is the same for players who own different DLCs. Its flag is read, never whether a DLC is owned.
        private static bool OfDlc(CityBuildingController c)
        {
            return GameDlc.Gated(c) || c.GetComponentInChildren<ToggleObjectDLC>(true) != null || c.GetComponentInParent<ToggleObjectDLC>(true) != null;
        }

        // A building whose look is the game's own business: a taxi destination or a gas station part under it.
        private static bool TakenElsewhere(CityBuildingController c)
        {
            return c.GetComponentInChildren<TaxiDestinationData>(true) != null || c.GetComponentInChildren<GasStationController>(true) != null;
        }

        /// <summary>True if any of the building's renderers is drawn in a static batch; how many of how many.</summary>
        public static bool Batched(CityBuildingController c, out int batched, out int renderers)
        {
            batched = renderers = 0;
            if (c == null)
            {
                return false;
            }
            Drawn.Clear();
            c.GetComponentsInChildren(true, Drawn);
            renderers = Drawn.Count;
            for (int i = 0; i < Drawn.Count; i++)
            {
                batched += Drawn[i] != null && Drawn[i].isPartOfStaticBatch ? 1 : 0;
            }
            Drawn.Clear();
            return batched > 0;
        }

        /// <summary>
        /// A building's measures round its root, in its root's own turn: the footprint and height from its box (the game's
        /// shadow and map look), else its renderers; its first door and the way that door faces out. False for a leaning one.
        /// </summary>
        public static bool Describe(CityBuildingController c, DesignEntry into)
        {
            Transform root = c.transform;
            if (!NewBuildingsRules.Upright(root.up.y))
            {
                return false;
            }
            float yaw = root.eulerAngles.y;
            Quaternion unturn = Quaternion.Euler(0f, -yaw, 0f);
            Vector3 origin = root.position;
            float minX = float.MaxValue, minZ = float.MaxValue, maxX = float.MinValue, maxZ = float.MinValue, top = float.MinValue;
            bool any = false;
            MeshRenderer box = c.lodVersion;
            MeshFilter boxMesh = box != null && !box.isPartOfStaticBatch ? box.GetComponent<MeshFilter>() : null;
            if (boxMesh != null && boxMesh.sharedMesh != null)
            {
                Bounds b = boxMesh.sharedMesh.bounds;
                Matrix4x4 m = boxMesh.transform.localToWorldMatrix;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 p = unturn * (m.MultiplyPoint3x4(new Vector3((i & 1) == 0 ? b.min.x : b.max.x, (i & 2) == 0 ? b.min.y : b.max.y, (i & 4) == 0 ? b.min.z : b.max.z)) - origin);
                    Grow(p, ref minX, ref minZ, ref maxX, ref maxZ, ref top);
                }
                any = true;
            }
            else
            {
                Drawn.Clear();
                c.GetComponentsInChildren(false, Drawn);
                for (int r = 0; r < Drawn.Count; r++)
                {
                    Bounds b = Drawn[r].bounds;
                    if (b.size.sqrMagnitude < 0.01f)
                    {
                        continue;
                    }
                    for (int i = 0; i < 8; i++)
                    {
                        Vector3 p = unturn * (new Vector3((i & 1) == 0 ? b.min.x : b.max.x, (i & 2) == 0 ? b.min.y : b.max.y, (i & 4) == 0 ? b.min.z : b.max.z) - origin);
                        Grow(p, ref minX, ref minZ, ref maxX, ref maxZ, ref top);
                    }
                    any = true;
                }
                Drawn.Clear();
            }
            if (!any || maxX - minX < 1f || maxZ - minZ < 1f)
            {
                return false;
            }
            Transform door = c.entranceDoors[0].doorTransform;
            Vector3 at = unturn * (door.position - origin);
            Vector3 outward = Vector3.ProjectOnPlane(door.forward, Vector3.up);
            into.MinX = minX;
            into.MaxX = maxX;
            into.MinZ = minZ;
            into.MaxZ = maxZ;
            into.Height = Mathf.Max(1f, top);
            into.DoorX = at.x;
            into.DoorZ = at.z;
            into.DoorY = at.y;
            into.DoorFacing = NewBuildingsRules.Turn((outward.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(outward).eulerAngles.y : yaw) - yaw);
            return true;
        }

        private static void Grow(Vector3 p, ref float minX, ref float minZ, ref float maxX, ref float maxZ, ref float top)
        {
            minX = Mathf.Min(minX, p.x);
            maxX = Mathf.Max(maxX, p.x);
            minZ = Mathf.Min(minZ, p.z);
            maxZ = Mathf.Max(maxZ, p.z);
            top = Mathf.Max(top, p.y);
        }

        /// <summary>The design of that name and size, or -1.</summary>
        public static int Find(string name, string size)
        {
            return name != null && size != null && ByKey.TryGetValue(name + "|" + size, out int at) ? at : -1;
        }

        /// <summary>The first design of that name, whatever its size, or -1.</summary>
        public static int Find(string name)
        {
            for (int i = 0; name != null && i < Designs.Count; i++)
            {
                if (string.Equals(Designs[i].Name, name, StringComparison.Ordinal))
                {
                    return i;
                }
            }
            return -1;
        }

        // ---------------------------------------------------------------- templates and copies

        /// <summary>
        /// A design's template, made on first asking from the building it was measured on, stripped and asleep; null if that
        /// building is gone. A working one keeps the game's controller; a decorative one has it taken off.
        /// </summary>
        private static Transform Template(int design, bool working)
        {
            int key = working ? -1 - design : design;
            if (Templates.TryGetValue(key, out Transform made) && made != null)
            {
                return made;
            }
            CityBuildingController source = design >= 0 && design < Designs.Count ? Sources[Designs[design].Source] : null;
            made = source != null ? MakeTemplate(source, working) : null;
            Templates[key] = made;
            return made;
        }

        private static Transform MakeTemplate(CityBuildingController source, bool working)
        {
            if (_asleep == null)
            {
                return null;
            }
            GameObject copy = UnityEngine.Object.Instantiate(source.gameObject, _asleep, false);
            copy.name = "Template " + NewBuildingsRules.DesignName(source.name) + (working ? " (working)" : " (look)");
            copy.SetActive(false);   // a copy of it is made switched off: nothing wakes before it stands where it should
            Transform t = copy.transform;
            t.localPosition = Vector3.zero;
            t.localRotation = Quaternion.identity;
            t.localScale = source.transform.lossyScale;
            if (working)
            {
                StripWorking(copy.GetComponent<CityBuildingController>());
            }
            else
            {
                StripLook(copy);
            }
            return t;
        }

        // A working copy keeps the game's controller and all it needs; no car may drive in and no underground car park is
        // a second one of the original's (the game finds a car park by its number), so those go.
        private static void StripWorking(CityBuildingController c)
        {
            Parts.Clear();
            c.GetComponentsInChildren(true, Parts);
            for (int i = Parts.Count - 1; i >= 0; i--)
            {
                Component part = Parts[i];
                if (part is DriveInEntrance || part is UndergroundParkingEntrance)
                {
                    UnityEngine.Object.DestroyImmediate(part);
                }
            }
            Parts.Clear();
            c.driveInEntrances = new DriveInEntrance[0];
            c.undergroundParkingEntrance = null;
        }

        // A decorative copy keeps what is drawn, what is solid and what the game's fade finds; its signs are put away (as
        // the game's are on a building with no business) and every script goes, the game's controller first.
        private static void StripLook(GameObject copy)
        {
            CityBuildingController c = copy.GetComponent<CityBuildingController>();
            if (c == null)
            {
                return;
            }
            CopyLook look = copy.AddComponent<CopyLook>();
            look.Fill(c.lodVersion, c.renderersToFade, c.renderersToHide, c.cityBuildingParts, c.objectsToDisable);
            for (int i = 0; i < c.cityBuildingParts.Count; i++)
            {
                if (c.cityBuildingParts[i] != null)
                {
                    c.cityBuildingParts[i].cityBuildingController = look;
                }
            }
            Parts.Clear();
            copy.GetComponentsInChildren(true, Parts);
            for (int i = 0; i < Parts.Count; i++)
            {
                if (Parts[i] is BuildingSignController || Parts[i] is BuildingLogoSignController)
                {
                    Parts[i].gameObject.SetActive(false);
                }
            }
            UnityEngine.Object.DestroyImmediate(c);
            for (int pass = 0; pass < 4; pass++)
            {
                Parts.Clear();
                copy.GetComponentsInChildren(true, Parts);
                int removed = 0;
                for (int i = Parts.Count - 1; i >= 0; i--)
                {
                    Component part = Parts[i];
                    if (part != null && Unwanted(part))
                    {
                        UnityEngine.Object.DestroyImmediate(part);
                        removed++;
                    }
                }
                if (removed == 0)
                {
                    break;
                }
            }
            Parts.Clear();
        }

        private static bool Unwanted(Component c)
        {
            if (c is Transform || c is MeshFilter || c is MeshRenderer || c is LODGroup || c is CopyLook || c is ViewBlockingEntityPart || c is UnityEngine.AI.NavMeshObstacle)
            {
                return false;
            }
            if (c is Collider collider)
            {
                return collider.isTrigger;   // the walls stay solid; the game's triggers are its scripts' business
            }
            return true;   // every other script, light, sound, animator, probe, canvas and particle system
        }

        /// <summary>
        /// A working copy of a design for a made building, standing at (x, y, z) turned by <paramref name="yaw"/> degrees,
        /// with the game's record and registration set before it wakes (the city's list can reach it before its Start);
        /// null if the design's building is gone. It stands in the scene of the building it copies, which goes with the city.
        /// </summary>
        public static CityBuildingController MakeWorking(int design, Vector3 at, float yaw, Building building, BuildingRegistration registration, string name)
        {
            Transform template = Template(design, true);
            CityBuildingController source = design >= 0 && design < Designs.Count ? Sources[Designs[design].Source] : null;
            if (template == null || source == null)
            {
                return null;
            }
            // Never under the mod's own objects: the save tools' removal destroys those in a city that stays, and a made
            // building's controller must outlive the session (its handlers on the game's events stay until the next load).
            Transform copy = UnityEngine.Object.Instantiate(template, at, Quaternion.Euler(0f, yaw, 0f));
            SceneManager.MoveGameObjectToScene(copy.gameObject, source.gameObject.scene);
            copy.name = name;
            CityBuildingController c = copy.GetComponent<CityBuildingController>();
            c.building = building;
            c.buildingRegistration = registration;
            copy.gameObject.SetActive(true);
            FarDistricts.Include(copy);
            return c;
        }

        /// <summary>A decorative copy of a design standing at (x, y, z) turned by <paramref name="yaw"/> degrees; null if the design's building is gone.</summary>
        public static CopyLook MakeLook(int design, Vector3 at, float yaw, string name)
        {
            Transform template = Template(design, false);
            if (template == null || _standing == null)
            {
                return null;
            }
            Transform copy = UnityEngine.Object.Instantiate(template, _standing, false);
            copy.name = name;
            copy.SetPositionAndRotation(at, Quaternion.Euler(0f, yaw, 0f));
            copy.gameObject.SetActive(true);
            FarDistricts.Include(copy);
            return copy.GetComponent<CopyLook>();
        }

        /// <summary>Takes a decorative copy away (it has no controller of the game's, so nothing of it is left in the game's events).</summary>
        public static void Remove(CopyLook look)
        {
            if (look != null)
            {
                UnityEngine.Object.Destroy(look.gameObject);
            }
        }

        /// <summary>A made building hidden or shown: the whole copy switched off, so nothing of it is drawn, solid or clicked; its controller stays.</summary>
        public static void Show(CityBuildingController c, bool shown)
        {
            if (c != null && c.gameObject.activeSelf != shown)
            {
                c.gameObject.SetActive(shown);
            }
        }

        /// <summary>The look's root, for the log and the tests; null once gone.</summary>
        public static Transform RootOf(CopyLook look) => look != null ? look.transform : null;

        // ---------------------------------------------------------------- a building's own look hidden

        private static readonly Dictionary<CityBuildingController, List<Renderer>> Hidden = new Dictionary<CityBuildingController, List<Renderer>>(8);
        private static readonly Dictionary<CityBuildingController, List<Light>> HiddenLights = new Dictionary<CityBuildingController, List<Light>>(8);

        /// <summary>
        /// Hides one of the game's buildings' look: every renderer under it and its box (the shadow in the street, the whole
        /// building on the city map) kept from drawing, and the lights of its signs off; its controller, colliders, door and
        /// card left working: what a construction site's finished look stands over. The game's own switches (the map's,
        /// the fade's, the signs' and the hourly lights') leave a renderer kept from drawing as it is, and switch a sign's
        /// light by its object, not by the light itself. Kept from the far districts' saving, which would otherwise draw it
        /// again when it gives its square back. False if already hidden or no building.
        /// </summary>
        public static bool HideLook(CityBuildingController c)
        {
            if (c == null || Hidden.ContainsKey(c))
            {
                return false;
            }
            var list = new List<Renderer>(32);
            c.GetComponentsInChildren(true, list);
            if (c.lodVersion != null && !list.Contains(c.lodVersion))
            {
                list.Add(c.lodVersion);
            }
            for (int i = 0; i < list.Count; i++)
            {
                FarDistricts.LeaveAlone(list[i], true);
                list[i].forceRenderingOff = true;
            }
            Hidden[c] = list;
            var lights = new List<Light>(4);
            c.GetComponentsInChildren(true, lights);
            for (int i = lights.Count - 1; i >= 0; i--)
            {
                if (lights[i].enabled)
                {
                    lights[i].enabled = false;
                }
                else
                {
                    lights.RemoveAt(i);   // off already: not the mod's to switch on again
                }
            }
            HiddenLights[c] = lights;
            return true;
        }

        /// <summary>Shows a look hidden by <see cref="HideLook"/> again, as the far districts' saving has its square now. False if it was not hidden.</summary>
        public static bool ShowLook(CityBuildingController c)
        {
            if (c == null || !Hidden.TryGetValue(c, out List<Renderer> list))
            {
                return false;
            }
            Hidden.Remove(c);
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] != null)
                {
                    list[i].forceRenderingOff = FarDistricts.LeaveAlone(list[i], false);
                }
            }
            if (HiddenLights.TryGetValue(c, out List<Light> lights))
            {
                HiddenLights.Remove(c);
                for (int i = 0; i < lights.Count; i++)
                {
                    if (lights[i] != null)
                    {
                        lights[i].enabled = true;
                    }
                }
            }
            return true;
        }

        /// <summary>Every hidden look shown again (the option switched off, the city going); how many.</summary>
        public static int ShowAllLooks()
        {
            int shown = 0;
            while (Hidden.Count > 0)
            {
                CityBuildingController first = null;
                foreach (CityBuildingController c in Hidden.Keys)
                {
                    first = c;
                    break;
                }
                if (ShowLook(first))
                {
                    shown++;
                }
                else
                {
                    Hidden.Remove(first);
                }
            }
            HiddenLights.Clear();
            return shown;
        }

        // ---------------------------------------------------------------- out of view

        private static readonly Plane[] Frustum = new Plane[6];
        private static int _frustumFrame = -1;

        /// <summary>
        /// True when any of the box is in the main camera's view, or the city map is open (it shows the whole city): a look
        /// changes only out of view, as the construction sites' scaffold does. The camera's planes are made once a frame.
        /// </summary>
        public static bool InView(Bounds box)
        {
            if (CityMap.IsOpen)
            {
                return true;
            }
            Camera main = GameManager.GetMainCamera();
            if (main == null)
            {
                return false;
            }
            if (_frustumFrame != Time.frameCount)
            {
                GeometryUtility.CalculateFrustumPlanes(main, Frustum);
                _frustumFrame = Time.frameCount;
            }
            return GeometryUtility.TestPlanesAABB(Frustum, box);
        }

        /// <summary>The box round a lot grown by the gap kept, from a metre under its ground to <paramref name="height"/> above it.</summary>
        public static Bounds BoxOf(MadeLot lot, float ground, float height)
        {
            float f = lot.Facing * Mathf.Deg2Rad;
            float hw = lot.Width * 0.5f + NewBuildingsRules.Gap, hd = lot.Depth * 0.5f + NewBuildingsRules.Gap;
            float reachX = Mathf.Abs(Mathf.Cos(f)) * hw + Mathf.Abs(Mathf.Sin(f)) * hd;
            float reachZ = Mathf.Abs(Mathf.Sin(f)) * hw + Mathf.Abs(Mathf.Cos(f)) * hd;
            float top = Mathf.Max(2f, height) + 2f;
            return new Bounds(new Vector3(lot.X, ground + (top - 1f) * 0.5f, lot.Z), new Vector3(reachX * 2f, top + 1f, reachZ * 2f));
        }

        /// <summary>The box round one of the game's buildings, from its own box (the game's shadow and map look), grown by the gap kept.</summary>
        public static Bounds BoxOf(CityBuildingController c)
        {
            if (c == null)
            {
                return default;
            }
            Bounds b = c.lodVersion != null ? c.lodVersion.bounds : default;
            if (b.size.sqrMagnitude < 1f)
            {
                b = new Bounds(c.transform.position, new Vector3(40f, 40f, 40f));   // no box to read: a block's worth round its root
            }
            b.Expand(NewBuildingsRules.Gap * 2f);
            return b;
        }

        /// <summary>A copy's hovers and clicks handed to the building whose look it wears, or given back with null.</summary>
        public static void Lend(CopyLook look, CityBuildingController building)
        {
            if (look != null)
            {
                look.Lend(building);
            }
        }

        /// <summary>True while that building's look is hidden.</summary>
        public static bool LookHidden(CityBuildingController c) => c != null && Hidden.ContainsKey(c);

        // ---------------------------------------------------------------- the city's buildings, for the tests

        /// <summary>The game's building whose door is nearest the point, the mod's own left out; null in no city.</summary>
        public static CityBuildingController Nearest(Vector3 from, out float metres)
        {
            metres = float.MaxValue;
            CityBuildingController[] all = GameDlc.Open(InstanceBehavior<CityManager>.Instance);
            CityBuildingController best = null;
            for (int i = 0; all != null && i < all.Length; i++)
            {
                CityBuildingController c = all[i];
                if (c == null || c.entranceDoors == null || c.entranceDoors.Length == 0 || c.entranceDoors[0] == null || c.entranceDoors[0].doorTransform == null
                    || MadeBuildings.Of(c) != null)
                {
                    continue;
                }
                float d = (c.entranceDoors[0].doorTransform.position - from).sqrMagnitude;
                if (d < metres)
                {
                    metres = d;
                    best = c;
                }
            }
            metres = best != null ? Mathf.Sqrt(metres) : 0f;
            return best;
        }

        /// <summary>The design entry of one building, measured now (the copy test's: one building, no catalogue); null if it cannot be copied.</summary>
        public static DesignEntry DescribeOne(CityBuildingController c, out string why)
        {
            why = null;
            if (c == null || c.building == null)
            {
                why = "there is no building";
                return null;
            }
            if (c.building.BuildingType == SpecialType || c.building.BuildingSize == HamptonsHouseSize || c.GetType() != typeof(CityBuildingController) || TakenElsewhere(c))
            {
                why = "it is a building of a kind never copied (special, a Hamptons house, a taxi destination or a gas station)";
                return null;
            }
            if (OfDlc(c))
            {
                why = "it needs a DLC, and a pick may never depend on one";
                return null;
            }
            var entry = new DesignEntry { Name = NewBuildingsRules.DesignName(c.name), Size = c.building.BuildingSize, Version = c.building.BuildingVersion, Type = c.building.BuildingType, Uses = 1 };
            entry.Batched = Batched(c, out _, out _);
            if (!Describe(c, entry))
            {
                why = "it leans or could not be measured";
                return null;
            }
            entry.Source = -1;
            return entry;
        }

        /// <summary>A stripped, sleeping template of one building of the city (the frame cost test's ten copies); null if it cannot be made.</summary>
        public static Transform LookTemplate(CityBuildingController source)
        {
            return source != null ? MakeTemplate(source, false) : null;
        }

        /// <summary>One decorative copy from a template of <see cref="LookTemplate"/>, standing at once.</summary>
        public static CopyLook Stand(Transform template, Vector3 at, float yaw, string name)
        {
            if (template == null || _standing == null)
            {
                return null;
            }
            Transform copy = UnityEngine.Object.Instantiate(template, _standing, false);
            copy.name = name;
            copy.SetPositionAndRotation(at, Quaternion.Euler(0f, yaw, 0f));
            copy.gameObject.SetActive(true);
            FarDistricts.Include(copy);
            return copy.GetComponent<CopyLook>();
        }

        /// <summary>A template of <see cref="LookTemplate"/> no longer wanted.</summary>
        public static void Drop(Transform template)
        {
            if (template != null)
            {
                UnityEngine.Object.Destroy(template.gameObject);
            }
        }

        private const int NearestKept = 24;   // the doors nearest the camera looked at for a building that can be copied
        private static readonly CityBuildingController[] Near = new CityBuildingController[NearestKept];
        private static readonly float[] NearDistance = new float[NearestKept];

        /// <summary>
        /// The game's building nearest the point that can be copied (not in a static batch, of no DLC, of a kind copied), its
        /// measures into <paramref name="design"/>: of the 24 nearest doors. Null if none of them can be.
        /// </summary>
        public static CityBuildingController NearestCopyable(Vector3 from, out DesignEntry design)
        {
            design = null;
            CityBuildingController[] all = GameDlc.Open(InstanceBehavior<CityManager>.Instance);
            if (all == null)
            {
                return null;
            }
            for (int i = 0; i < NearestKept; i++)
            {
                Near[i] = null;
                NearDistance[i] = float.MaxValue;
            }
            for (int i = 0; i < all.Length; i++)
            {
                CityBuildingController c = all[i];
                if (c == null || c.entranceDoors == null || c.entranceDoors.Length == 0 || c.entranceDoors[0] == null || c.entranceDoors[0].doorTransform == null
                    || MadeBuildings.Of(c) != null)
                {
                    continue;
                }
                float d = (c.entranceDoors[0].doorTransform.position - from).sqrMagnitude;
                for (int k = 0; k < NearestKept; k++)
                {
                    if (d < NearDistance[k])
                    {
                        for (int m = NearestKept - 1; m > k; m--)
                        {
                            Near[m] = Near[m - 1];
                            NearDistance[m] = NearDistance[m - 1];
                        }
                        Near[k] = c;
                        NearDistance[k] = d;
                        break;
                    }
                }
            }
            for (int k = 0; k < NearestKept; k++)
            {
                DesignEntry entry = Near[k] != null ? DescribeOne(Near[k], out _) : null;
                if (entry != null && !entry.Batched)
                {
                    design = entry;
                    return Near[k];
                }
            }
            return null;
        }

        /// <summary>A decorative copy of one building of the city, made now (the copy test's); null if it cannot be.</summary>
        public static CopyLook MakeLookOf(CityBuildingController source, Vector3 at, float yaw, string name)
        {
            if (source == null || _asleep == null || _standing == null)
            {
                return null;
            }
            Transform copy = MakeTemplate(source, false);   // one copy of one building: the stripped copy itself stands, no template kept
            if (copy == null)
            {
                return null;
            }
            copy.SetParent(_standing, false);
            copy.name = name;
            copy.SetPositionAndRotation(at, Quaternion.Euler(0f, yaw, 0f));
            copy.gameObject.SetActive(true);
            FarDistricts.Include(copy);
            return copy.GetComponent<CopyLook>();
        }
    }
}
