using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using BAModAPI;
using Helpers;
using Localizor;
using Newtonsoft.Json.Linq;
using NWH.Common.CoM;
using NWH.VehiclePhysics2.Modules.SpeedLimiter;
using NWH.WheelController3D;
using RealLifeOverhaul.DistrictSave;
using UI.Notification;
using UnityEngine;
using UnityEngine.AI;
using Vehicles.VehicleTypes;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The cars made for the mod (<see cref="ModCarCatalogue"/>), the only place they meet the game. Each is a vehicle type
    /// registered through the game's mod interface, so the game names, prices, sells on, taxes, tows and parks it as one of its
    /// own. Its prefab is made here, once a load, from the game car it is built on: a copy of that car, never switched on, with
    /// the mod's body, wheels, collision shape and textures (Bundles/reallife_cars; or, while that bundle is not built, the
    /// same meshes and pictures as plain files in Bundles/cars_newer, uncompressed) in place of the game's and its wheels,
    /// doors' approach points, lamps and helpers moved to fit; the copy is handed to the game's prefab lookup under the name the
    /// game asks for. Engine, gearbox, sounds, lights, damage, dirt and washing are therefore the game's own, untouched.
    /// In the save a car of the mod's is written as the game car it is built on and turned back at the next load
    /// (<see cref="OnSave"/>, <see cref="Ready"/>): without the mod, with an older mod, without the bundle, after a game update
    /// that moves what this stands on, or in a Going Public session, the player has that game car instead and nothing faults.
    /// COST: nothing per frame (<see cref="Waiting"/> is a field read, true for the frame after a save at most); a load builds
    /// one object a model; a save walks the save's vehicles once.
    /// </summary>
    public static class ModCars
    {
        private const float BayClear = 6f;          // metres: the game's own test of its dealers' bay (ShowcaseVehicleController.Purchase)
        private const float NewFuel = 0.97f;        // of a tank, as the game hands a new car over
        private const string VehicleShader = "Shader Graphs/SH_Vehicle";   // the game's, which every car of the mod's is drawn with (on a copy of a game car's material)
        private const float DoorStand = 0.25f;      // metres a door's approach point stands from the game car's flank (the Honza Mimic's driver side)

        public static IModLogger Logger;

        private static readonly Dictionary<string, VehicleType> Types = new Dictionary<string, VehicleType>(4);
        private static readonly Dictionary<string, GameObject> Templates = new Dictionary<string, GameObject>(4);
        private static readonly Dictionary<string, Material> Paints = new Dictionary<string, Material>(4);   // one a model for the whole run: every car of it in the world draws with it
        private static readonly List<CarStandIn> Kept = new List<CarStandIn>(4);          // cars left as their game car this session
        private static readonly List<VehicleInstance> Pending = new List<VehicleInstance>(4);   // written as their game car, to be turned back
        private static readonly List<string> PendingTypes = new List<string>(4);
        private static readonly List<CarStandIn> Line = new List<CarStandIn>(4);
        private static readonly Application.LogCallback Heard = OnLog;
        private static readonly Action SaveStep = OnSave;
        private static string _root;
        private static AssetBundle _bundle;
        private static string _loose;   // the folder of plain files used while there is no bundle, else null
        private static bool _bundleTried, _stoodDown, _listening;
        private static volatile bool _armed;
        private static int _mainThread = -1;
        private static string _prefix;
        private static GameObject _holder;

        /// <summary>True between a save's write and the cars' names going back: the garage's frame then calls <see cref="Frame"/>.</summary>
        public static bool Waiting => Pending.Count > 0;

        /// <summary>At the game's start, before any save is read: the types exist from then on, whatever the option says, so a car owned stays a car.</summary>
        public static void Start(ModContext context)
        {
            _root = context.ModRootPath;
            Logger = context.Logger;
            for (int i = 0; i < ModCarCatalogue.All.Length; i++)
            {
                Register(ModCarCatalogue.All[i]);
            }
            SaveHook.KeepEssential(SaveStep);
        }

        private static void Register(ModCarDef d)
        {
            string wrong = ModCarCatalogue.Wrong(d);
            if (wrong != null)
            {
                Logger?.Warn("Cars: " + d.Id + " is left out, " + wrong + ".");
                return;
            }
            var type = ScriptableObject.CreateInstance<VehicleType>();
            type.name = d.Id;
            type.vehicleTypeName = d.TypeName;
            type.price = d.Price;
            type.itemVersion = string.Empty;
            type.maxFuel = d.MaxFuel;
            type.maxCargoCapacity = d.Cargo;
            type.maxSpeed = d.MaxSpeed;
            type.enginePower = d.EnginePower;
            type.brakeForce = d.BrakeForce;
            type.turnRadius = d.TurnRadius;
            type.damageIntensity = d.DamageIntensity;
            type.fitsHandTruck = d.FitsHandTruck;
            type.isLuxuryCar = d.Luxury;
            type.fitsFlatbed = d.FitsFlatbed;
            type.taxDeductible = d.TaxDeductible;
            type.autoParkSupported = d.AutoPark;
            type.destinationsThatCanDeliver = d.Destinations;
            type.enclosed = true;
            if (ModdingAPI.RegisterModVehicleType(type))
            {
                Types[d.TypeName] = type;
            }
            else
            {
                Logger?.Warn("Cars: the game did not take the vehicle type " + d.TypeName + "; that car is not offered.");
            }
        }

        /// <summary>True when this model can be bought here and now.</summary>
        public static bool Offered(ModCarDef d, BikePlace place)
        {
            return Settings.GarageModCars && d != null && place != null && place.Bay != null && !GoingPublic.InSession && !_stoodDown
                   && Types.ContainsKey(d.TypeName) && Templates.TryGetValue(d.TypeName, out GameObject made) && made != null;
        }

        // ---------------------------------------------------------------- a load

        /// <summary>
        /// A save has been read. <paramref name="early"/>: before its city is built (the main menu's unload), where the cars are
        /// turned back in time for the game to make them itself. Otherwise the city stands already (a load that did not pass
        /// the main menu): a car turned back now is made again in place, but for the one the player sits in.
        /// </summary>
        public static void Ready(GameInstance save, bool early)
        {
            if (save == null || save.VehicleInstances == null)
            {
                return;
            }
            bool can = false;
            try
            {
                can = !GoingPublic.InSession && Prefabs();
            }
            catch (Exception e)
            {
                StandDown("their prefabs could not be made (" + e.GetType().Name + ": " + e.Message + ")");
            }
            Line.Clear();
            string text = null;
            if (save.modData != null)
            {
                save.modData.TryGetValue(ModCarCatalogue.Key, out text);
            }
            Line.AddRange(ModCarCatalogue.Read(text));
            Kept.Clear();
            int turned = 0, made = 0;
            for (int i = 0; i < save.VehicleInstances.Count && Line.Count > 0; i++)
            {
                VehicleInstance v = save.VehicleInstances[i];
                if (v == null)
                {
                    continue;
                }
                bool seated = !early && save.ActiveVehicleId == v.id;
                string back = ModCarCatalogue.Back(v.id, v.vehicleTypeName, Line, t => can && !seated && Templates.TryGetValue(t, out GameObject g) && g != null);
                if (back != v.vehicleTypeName)
                {
                    v.vehicleTypeName = back;
                    turned++;
                    if (!early && Remake(save, v))
                    {
                        made++;
                        i--;   // it went to the list's end: the vehicle now at this place is looked at next
                    }
                }
                else
                {
                    Keep(v);
                }
            }
            if (Line.Count > 0 || Meta.Dev.Verbose)
            {
                Logger?.Info("Cars check (load, " + (early ? "before the city" : "in the city") + "): the save names " + Line.Count + " car(s) of the mod's; "
                             + turned + " turned back" + (early ? string.Empty : " (" + made + " made again in place)") + ", " + Kept.Count
                             + " left as the game car they are saved as" + (GoingPublic.InSession ? " (a Going Public session)" : string.Empty)
                             + (can || Line.Count == 0 ? ": PASS." : ": the models cannot be made in this session."));
            }
        }

        private static void Keep(VehicleInstance v)
        {
            for (int k = 0; k < Line.Count; k++)
            {
                if (Line[k].Id == v.id && Line[k].Donor == v.vehicleTypeName)
                {
                    Kept.Add(Line[k]);
                    return;
                }
            }
        }

        // A car the game made as its game car before the mod could turn it back: taken out and made again where it stands.
        private static bool Remake(GameInstance save, VehicleInstance v)
        {
            VehicleController old = VehicleHelper.GetVehicleController(v);
            if (old == null)
            {
                return false;   // not in the world now (in a building): the game makes it from the type when it is
            }
            Vector3 at = old.transform.position;
            Quaternion facing = old.transform.rotation;
            old.gameObject.SetActive(false);
            UnityEngine.Object.Destroy(old.gameObject);
            save.VehicleInstances.Remove(v);
            VehicleHelper.CreateAndSpawnVehicle(v, at, facing);
            return true;
        }

        // ---------------------------------------------------------------- the prefabs

        // Every model made anew from its game car and handed to the game's lookup; true if any stands.
        private static bool Prefabs()
        {
            if (_stoodDown)
            {
                return false;
            }
            IDictionary cache = GameContract.PrefabCache.Field != null ? GameContract.PrefabCache.Field.GetValue(null) as IDictionary : null;
            if (cache == null)
            {
                StandDown("this build of the game keeps its prefabs somewhere else");
                return false;
            }
            if (!Open())
            {
                return false;
            }
            if (_holder == null)
            {
                _holder = new GameObject("RealLifeOverhaul.Cars");
                _holder.SetActive(false);   // what is made under it never wakes: it is only ever copied
                UnityEngine.Object.DontDestroyOnLoad(_holder);
            }
            bool any = false;
            for (int i = 0; i < ModCarCatalogue.All.Length; i++)
            {
                ModCarDef d = ModCarCatalogue.All[i];
                if (!Types.TryGetValue(d.TypeName, out VehicleType type))
                {
                    continue;
                }
                if (Templates.TryGetValue(d.TypeName, out GameObject old) && old != null)
                {
                    UnityEngine.Object.Destroy(old);   // the game car it was copied from may have been unloaded with the last city
                }
                Templates.Remove(d.TypeName);
                GameObject made = Build(d, type);
                if (made == null)
                {
                    continue;
                }
                Templates[d.TypeName] = made;
                cache[ModCarCatalogue.PrefabKey(d.TypeName)] = made;
                any = true;
            }
            return any;
        }

        private static bool Open()
        {
            if (_bundle != null || _loose != null)
            {
                return true;
            }
            if (_bundleTried)
            {
                return false;
            }
            _bundleTried = true;
            string file = Path.Combine(_root ?? string.Empty, ModCarCatalogue.Bundle);
            string folder = Path.Combine(_root ?? string.Empty, ModCarCatalogue.Loose);
            if (File.Exists(file))
            {
                _bundle = AssetBundle.LoadFromFile(file);
                if (_bundle != null)
                {
                    BundleShaders.Check(Logger, ModCarCatalogue.BundleName, _bundle, _bundle.LoadAllAssets(), VehicleShader);   // it holds no material: the line says so
                    return true;
                }
            }
            if (Directory.Exists(folder))
            {
                _loose = folder;
                Logger?.Info("Cars: read from the plain files of " + ModCarCatalogue.Loose + " (no bundle " + ModCarCatalogue.Bundle + " in this build): textures uncompressed.");
                return true;
            }
            StandDown("neither " + ModCarCatalogue.Bundle + " nor " + ModCarCatalogue.Loose + " is in the mod's folder");
            return false;
        }

        private static readonly Dictionary<string, Mesh> Meshes = new Dictionary<string, Mesh>(4);   // built from plain files once a run

        // A mesh of a model: the bundle's, or built from the plain file as GarageAssets' builder builds it.
        private static Mesh MeshOf(ModCarDef d, string part)
        {
            string name = ModCarCatalogue.MeshName(d, part);
            if (_bundle != null)
            {
                return _bundle.LoadAsset<Mesh>(name);
            }
            if (Meshes.TryGetValue(name, out Mesh kept) && kept != null)
            {
                return kept;
            }
            string file = Path.Combine(_loose, d.Id, "meshes", name + ".json");
            if (!File.Exists(file))
            {
                return null;
            }
            JObject read = JObject.Parse(File.ReadAllText(file));
            float[] at = read["vertices"]?.ToObject<float[]>(), up = read["normals"]?.ToObject<float[]>(), along = read["tangents"]?.ToObject<float[]>(), uvs = read["uv"]?.ToObject<float[]>();
            int[] corners = read["triangles"]?.ToObject<int[]>();
            int count = at != null ? at.Length / 3 : 0;
            if (count < 3 || up == null || up.Length != count * 3 || along == null || along.Length != count * 4 || uvs == null || uvs.Length != count * 2 || corners == null)
            {
                return null;
            }
            var vertices = new Vector3[count];
            var normals = new Vector3[count];
            var tangents = new Vector4[count];
            var uv = new Vector2[count];
            for (int i = 0; i < count; i++)
            {
                vertices[i] = new Vector3(at[i * 3], at[i * 3 + 1], at[i * 3 + 2]);
                normals[i] = new Vector3(up[i * 3], up[i * 3 + 1], up[i * 3 + 2]);
                tangents[i] = new Vector4(along[i * 4], along[i * 4 + 1], along[i * 4 + 2], along[i * 4 + 3]);
                uv[i] = new Vector2(uvs[i * 2], uvs[i * 2 + 1]);
            }
            var mesh = new Mesh { name = name };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetTangents(tangents);
            mesh.SetUVs(0, uv);
            mesh.SetTriangles(corners, 0, false);
            mesh.RecalculateBounds();
            Meshes[name] = mesh;   // kept readable and for the whole run: the game dents a body by its vertices, and every car of the model shares it
            return mesh;
        }

        private static readonly Dictionary<string, Texture2D> Pictures = new Dictionary<string, Texture2D>(8);   // plain files read once a run

        private static Texture2D TextureOf(ModCarDef d, int slot)
        {
            string name = ModCarCatalogue.TextureName(d, ModCarCatalogue.TextureParts[slot]);
            if (_bundle != null)
            {
                return _bundle.LoadAsset<Texture2D>(name);
            }
            if (Pictures.TryGetValue(name, out Texture2D kept) && kept != null)
            {
                return kept;
            }
            string file = Path.Combine(_loose, d.Id, "textures", name + ".png");
            if (!File.Exists(file))
            {
                return null;
            }
            var picture = new Texture2D(2, 2, TextureFormat.RGBA32, true, ModCarCatalogue.TextureLinear[slot]) { name = name };
            if (!picture.LoadImage(File.ReadAllBytes(file), true))
            {
                UnityEngine.Object.Destroy(picture);
                return null;
            }
            picture.wrapMode = TextureWrapMode.Clamp;
            picture.filterMode = FilterMode.Trilinear;
            picture.anisoLevel = 8;
            Pictures[name] = picture;
            return picture;
        }

        // Said once; from then on no car of the mod's is made or sold in this run of the game, and those owned stay their game car.
        private static void StandDown(string why)
        {
            if (!_stoodDown)
            {
                _stoodDown = true;
                Logger?.Warn("Cars: the mod's own cars stand down, " + why + ". A car already owned is the game car it is built on until this is put right.");
            }
        }

        private static GameObject Build(ModCarDef d, VehicleType type)
        {
            Mesh body = MeshOf(d, "body"), left = MeshOf(d, "wheel_left"), right = MeshOf(d, "wheel_right"), hull = MeshOf(d, "collider");
            GameObject donor = PrefabHelper.LoadPrefabAssetByName("Vehicles/PlayerVehicles/" + d.Donor.Substring(d.Donor.IndexOf('_') + 1));
            var donorCar = donor != null ? donor.GetComponent<CarController>() : null;
            var donorLooks = donor != null ? donor.GetComponent<CarFeatures>() : null;
            if (body == null || left == null || right == null || hull == null || donorCar == null || donorLooks == null
                || donorLooks.bodyMeshes == null || donorLooks.bodyMeshes.Length < 5)
            {
                Logger?.Warn("Cars: " + d.Id + " is not made: " + (donor == null ? "the game car it is built on was not found" : body == null || left == null || right == null || hull == null
                    ? "one of its meshes is missing" : "the game car it is built on is not laid out as it was") + ".");
                return null;
            }
            GameObject made = UnityEngine.Object.Instantiate(donor, _holder.transform);
            made.name = d.Id;
            var car = made.GetComponent<CarController>();
            var looks = made.GetComponent<CarFeatures>();
            car.vehicleType = type;

            // The body is the renderer with the largest mesh; the four others are the wheels.
            Renderer bodyDraw = null;
            float largest = 0f;
            for (int i = 0; i < looks.bodyMeshes.Length; i++)
            {
                var filter = looks.bodyMeshes[i] != null ? looks.bodyMeshes[i].GetComponent<MeshFilter>() : null;
                float size = filter != null && filter.sharedMesh != null ? filter.sharedMesh.bounds.size.sqrMagnitude : 0f;
                if (size > largest)
                {
                    largest = size;
                    bodyDraw = looks.bodyMeshes[i];
                }
            }
            if (bodyDraw == null || bodyDraw.sharedMaterial == null)
            {
                UnityEngine.Object.Destroy(made);
                Logger?.Warn("Cars: " + d.Id + " is not made: the game car it is built on has no body to stand in for.");
                return null;
            }
            Bounds was = bodyDraw.GetComponent<MeshFilter>().sharedMesh.bounds;   // the game car's own measures, to move its helpers by
            float wasHalf = was.extents.x, wasFront = was.center.z + was.extents.z, wasRear = was.center.z - was.extents.z;
            float wasRadius = d.WheelRadius;

            if (!Paints.TryGetValue(d.TypeName, out Material paint) || paint == null)
            {
                paint = new Material(bodyDraw.sharedMaterial) { name = d.Id };
                // The textures' paint mask is drawn for these two edges of the shader's own step (ModCarCatalogue.PaintFrom): the game car's may differ.
                paint.SetFloat("_Min", ModCarCatalogue.PaintFrom);
                paint.SetFloat("_Max", ModCarCatalogue.PaintTo);
                for (int i = 0; i < ModCarCatalogue.TextureSlots.Length; i++)
                {
                    Texture2D picture = TextureOf(d, i);
                    if (picture == null)
                    {
                        UnityEngine.Object.Destroy(paint);
                        UnityEngine.Object.Destroy(made);
                        Logger?.Warn("Cars: " + d.Id + " is not made: its texture is missing, " + ModCarCatalogue.TextureParts[i] + ".");
                        return null;
                    }
                    paint.SetTexture(ModCarCatalogue.TextureSlots[i], picture);
                }
                Paints[d.TypeName] = paint;
            }
            for (int i = 0; i < looks.bodyMeshes.Length; i++)
            {
                Renderer draw = looks.bodyMeshes[i];
                var filter = draw != null ? draw.GetComponent<MeshFilter>() : null;
                if (filter == null)
                {
                    continue;
                }
                // One material, whatever the game car had: a mod car's mesh is one part, and Unity draws the last part again with every
                // material left over (the UMC Desert's body has a second, clear one for its glass, which would lie over the whole car).
                draw.sharedMaterials = new[] { paint };
                if (draw == bodyDraw)
                {
                    filter.sharedMesh = body;
                    continue;
                }
                Transform wheel = draw.transform;
                Vector3 p = wheel.localPosition;
                wasRadius = filter.sharedMesh != null ? filter.sharedMesh.bounds.extents.y : wasRadius;
                float wasWidth = filter.sharedMesh != null ? filter.sharedMesh.bounds.extents.x * 2f : d.WheelWidth;
                filter.sharedMesh = p.x < 0f ? left : right;
                wheel.localPosition = new Vector3(p.x < 0f ? -d.TrackHalf : d.TrackHalf, d.WheelRadius, p.z > 0f ? d.AxleFront : d.AxleRear);
                for (int c = 0; c < wheel.childCount; c++)   // the round shadow caster under each wheel
                {
                    Transform shade = wheel.GetChild(c);
                    Vector3 s = shade.localScale;
                    shade.localScale = new Vector3(s.x * d.WheelWidth / Mathf.Max(wasWidth, 0.01f), s.y * d.WheelRadius / wasRadius, s.z * d.WheelRadius / wasRadius);
                }
            }
            WheelController[] wheels = made.GetComponentsInChildren<WheelController>(true);
            for (int i = 0; i < wheels.Length; i++)
            {
                Transform at = wheels[i].transform;
                Vector3 p = at.localPosition;
                at.localPosition = new Vector3(p.x < 0f ? -d.TrackHalf : d.TrackHalf, p.y - wasRadius + d.WheelRadius, p.z > 0f ? d.AxleFront : d.AxleRear);
                wheels[i].wheel.radius = d.WheelRadius;
                wheels[i].wheel.width = d.WheelWidth;
            }
            var nwh = made.GetComponent<NWH.VehiclePhysics2.VehicleController>();
            if (nwh != null)
            {
                nwh.wheelbase = d.Wheelbase;
                nwh.powertrain.engine.maxPower = d.EnginePower;
                nwh.brakes.maxTorque = d.BrakeForce;
            }
            var limiter = made.GetComponent<SpeedLimiterModuleWrapper>();
            if (limiter != null && limiter.module != null)
            {
                limiter.module.speedLimit = d.MaxSpeed;
            }
            var centre = made.GetComponent<VariableCenterOfMass>();
            if (centre != null)
            {
                centre.dimensions = new Vector3(d.HalfWidth * 2f, d.Height - 0.1f, d.Length);
            }
            Transform shape = made.transform.Find(VehicleHelper.BodyColliderPath);
            var meshShape = shape != null ? shape.GetComponent<MeshCollider>() : null;
            if (meshShape != null)
            {
                meshShape.sharedMesh = hull;
            }
            Bounds now = body.bounds;
            if (car.navMeshObstacle != null)
            {
                car.navMeshObstacle.center = new Vector3(0f, now.center.y, now.center.z);
                car.navMeshObstacle.size = new Vector3(d.HalfWidth * 2f, now.size.y, now.size.z);
            }
            float lengthBy = d.Length / Mathf.Max(wasFront - wasRear, 1f), widthBy = d.HalfWidth / Mathf.Max(wasHalf, 0.5f);
            for (int i = 0; i < made.transform.childCount; i++)
            {
                Transform child = made.transform.GetChild(i);
                Vector3 p = child.localPosition;
                if (child.name.StartsWith("NavMeshTarget", StringComparison.Ordinal))   // where the player walks to, to get in: one a side
                {
                    child.localPosition = new Vector3(Mathf.Sign(p.x) * (Mathf.Abs(p.x) - wasHalf + d.HalfWidth), p.y, p.z * lengthBy);
                    continue;
                }
                switch (child.name)
                {
                    case "LoadingPosition":
                        child.localPosition = new Vector3(p.x, p.y, d.Rear - (wasRear - p.z));
                        break;
                    case "RefuelingPosition":
                        child.localPosition = new Vector3(Mathf.Sign(p.x) * (d.HalfWidth + DoorStand), p.y, p.z * lengthBy);
                        break;
                    case "Spotlights":
                        child.localPosition = new Vector3(p.x, p.y, d.Front - (wasFront - p.z));
                        break;
                    case "SmokeBrokenCar":
                        child.localPosition = new Vector3(p.x, d.BonnetHeight, d.Front - (wasFront - p.z));
                        break;
                    case "CarWashVFX":
                        for (int c = 0; c < child.childCount; c++)
                        {
                            Transform spray = child.GetChild(c);
                            Vector3 q = spray.localPosition;
                            spray.localPosition = Mathf.Abs(q.x) > 0.1f ? new Vector3(q.x * widthBy, q.y, q.z * lengthBy) : new Vector3(q.x, d.Height, q.z * lengthBy);
                        }
                        break;
                    case "VehicleParkingHelper":
                        var behind = child.GetComponentInChildren<BoxCollider>(true);
                        if (behind != null)
                        {
                            behind.size = new Vector3(behind.size.x * widthBy, behind.size.y, behind.size.z * lengthBy);
                            behind.center = new Vector3(behind.center.x, behind.center.y, behind.center.z * lengthBy);
                        }
                        break;
                }
            }
            if (Meta.Dev.Verbose)
            {
                Logger?.Info("Cars check (prefab): " + d.Id + " made on " + donor.name + ": body " + body.triangles.Length / 3 + " triangles, " + wheels.Length
                             + " wheels at track " + (d.TrackHalf * 2f).ToString("F2") + " m and wheelbase " + d.Wheelbase.ToString("F2") + " m, collision shape "
                             + (meshShape != null ? "set" : "NOT FOUND") + ", the game car was " + (wasFront - wasRear).ToString("F2") + " m long: "
                             + (wheels.Length == 4 && meshShape != null && nwh != null ? "PASS." : "FAIL."));
            }
            return made;
        }

        // ---------------------------------------------------------------- a sale

        /// <summary>Buys a model at a dealer's stand: paid as the game's own cars are, stood in the dealer's bay as the game stands its own.</summary>
        public static bool Buy(ModCarDef d, BikePlace place)
        {
            GameInstance save = SaveGameManager.Current;
            if (save == null || !Offered(d, place))
            {
                Notifications.Show(NotificationType.Warning, "reallife_cars_unready", null, 7f);
                return false;
            }
            Vector3 bay = place.Bay.position;
            var room = new Bounds(bay, Vector3.one * BayClear);
            for (int i = 0; i < save.VehicleInstances.Count; i++)
            {
                if (save.VehicleInstances[i] != null && room.Contains(save.VehicleInstances[i].position))
                {
                    Notifications.Show(NotificationType.Warning, "reallife_cars_no_bay", null, 8f);
                    return false;
                }
            }
            IDictionary cache = GameContract.PrefabCache.Field != null ? GameContract.PrefabCache.Field.GetValue(null) as IDictionary : null;
            if (cache == null)
            {
                Notifications.Show(NotificationType.Warning, "reallife_cars_unready", null, 7f);
                return false;
            }
            cache[ModCarCatalogue.PrefabKey(d.TypeName)] = Templates[d.TypeName];   // put again: the game empties its lookup now and then
            var bill = new TransactionInfo("ba:transaction_vehiclebought", new Dictionary<string, string>(1) { { "vehicleName", d.TypeName } });
            if (!GameManager.ChangeMoneySafe(-d.Price, bill, null, null, force: false, showNotification: true))
            {
                return false;   // the game has said the account is short
            }
            var colours = InstanceBehavior<GlobalReferences>.Instance.vehicleColors;
            string colour = colours != null && colours.Length > 0 ? colours[UnityEngine.Random.Range(0, colours.Length)].name : null;
            VehicleController car = VehicleHelper.CreateAndSpawnVehicle(new VehicleInstance(d.TypeName) { vehicleColorName = colour, fuel = d.MaxFuel * NewFuel },
                bay, place.Bay.rotation);
            Notifications.Show(NotificationType.Success, "reallife_cars_bought", new Dictionary<string, string>(1) { { "name", d.TypeName.GetLocalization() } }, 10f);
            Logger?.Info("Cars: a " + d.Id + " was bought for " + d.Price + " and stands in the dealer's bay at " + bay.ToString("F1") + (car != null ? "." : ", but the game returned no car."));
            return true;
        }

        // ---------------------------------------------------------------- a save

        // In the game's save event: every car of the mod's is written as its game car, with the line that turns it back.
        private static void OnSave()
        {
            GameInstance save = SaveGameManager.Current;
            if (save == null || save.VehicleInstances == null)
            {
                return;
            }
            if (Pending.Count > 0)
            {
                PutBack();
            }
            Line.Clear();
            for (int i = 0; i < save.VehicleInstances.Count; i++)
            {
                VehicleInstance v = save.VehicleInstances[i];
                if (v == null)
                {
                    continue;
                }
                string disk = ModCarCatalogue.OnDisk(v.vehicleTypeName);
                if (disk != v.vehicleTypeName)
                {
                    Line.Add(new CarStandIn(v.id, v.vehicleTypeName, disk));
                    Pending.Add(v);
                    PendingTypes.Add(v.vehicleTypeName);
                    v.vehicleTypeName = disk;
                    continue;
                }
                for (int k = 0; k < Kept.Count; k++)
                {
                    if (Kept[k].Id == v.id && Kept[k].Donor == v.vehicleTypeName)
                    {
                        Line.Add(Kept[k]);
                        break;
                    }
                }
            }
            if (save.modData == null)
            {
                save.modData = new Dictionary<string, string>();
            }
            if (Line.Count > 0)
            {
                save.modData[ModCarCatalogue.Key] = ModCarCatalogue.Write(Line);
            }
            else
            {
                save.modData.Remove(ModCarCatalogue.Key);
            }
            if (Pending.Count > 0)
            {
                _mainThread = Thread.CurrentThread.ManagedThreadId;
                _prefix = DistrictSaveRules.WrittenPrefix(SaveGamePathHelper.GetTempSavePath());
                if (!_listening)
                {
                    _listening = true;
                    Application.logMessageReceivedThreaded += Heard;
                }
                _armed = true;
            }
        }

        // From any thread, for every line logged: the game's own line that its file is written turns the names back while its save
        // call still runs. Nothing here may log (it would come back here).
        private static void OnLog(string text, string stack, LogType type)
        {
            try
            {
                if (_armed && Thread.CurrentThread.ManagedThreadId == _mainThread && DistrictSaveRules.IsWrittenLine(text, _prefix))
                {
                    PutBack();
                }
            }
            catch (Exception)
            {
                // the frame after puts back what is left
            }
        }

        /// <summary>The frame after a save, if the game's line did not come.</summary>
        public static void Frame()
        {
            if (Pending.Count > 0)
            {
                PutBack();
            }
        }

        private static void PutBack()
        {
            _armed = false;
            for (int i = 0; i < Pending.Count; i++)
            {
                Pending[i].vehicleTypeName = PendingTypes[i];
            }
            Pending.Clear();
            PendingTypes.Clear();
        }
    }
}
