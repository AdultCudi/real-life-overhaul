using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using BAModAPI;
using Buildings.Outdoors;
using Helpers;
using RealLifeOverhaul.NewBuildings;
using RealLifeOverhaul.Uptown;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;

namespace RealLifeOverhaul.Corners
{
    /// <summary>One corner building as it stands: its model over the copy engine's building, its signs, and what it took over from that building.</summary>
    internal sealed class CornerHandle
    {
        public string Id;                        // the model's prop id
        public string Lot;                       // the district's lot it stands on
        public string Address;                   // as the game writes it, for the log
        public string Key;                       // its street and number, for the game's registration change event
        public MadeBuilding Made;
        public CornerModel Model;
        public CornerPose Pose;
        public GameObject Root;                  // the model's root: its mesh, its map box, its solid and carving boxes, its signs
        public MeshRenderer Drawn;
        public MeshRenderer Box;                 // the city map's stand-in
        public readonly List<CornerText> Texts = new List<CornerText>(4);
        public readonly List<Renderer> GameSigns = new List<Renderer>(8);   // the building's own door sign, carried to the entrance and drawn
        public int Triangles, Draws, GameMaterials, StandIns, Solids, Carves, SignsDrawn, SignsBlank;
        public float DoorMissed = -1f;           // m between the building's door and the model's entrance
        public double Ms;
        public bool Hidden;                      // hidden with the building's look (a demolition's wrap, the frame cost test)
        public bool SignCarried;                 // the building's own door sign stands at the entrance: where, in the model's frame
        public float SignX, SignY, SignZ, SignWidth, SignHeight;
    }

    /// <summary>
    /// The only part of the corner buildings that touches the game's buildings (plans/build-mode.md section 12): a model of the second
    /// props bundle (PropAssets/models/corner_*) stood at a block's corner over the copy engine's building of its type, which keeps
    /// its record, card, door, interior and click: its own look hidden for good (CopyGame.WearOver), its doors and its own door sign
    /// carried to the model's entrance, the model drawn in one mesh with a submesh for the bundle's atlas and one for each of the
    /// game's materials it names (brick, glass, found once a load behind the loading screen by name), solid and carved out of the
    /// walking meshes round the entrance, its hovers and clicks the building's, and its signs (CornerSigns). Stands as the building
    /// is made, at a load or in play; never per frame.
    /// </summary>
    internal static partial class CornerLook
    {
        private const string RootName = "RealLifeOverhaul corner building";
        private const string GhostName = "RealLifeOverhaul corner building ghost";
        public const string Waits = DistrictBuildings.LookWaits;            // the stand waits for the props bundles: tried again once a second
        private const float WaitSeconds = 30f;                                  // real seconds the props bundles are waited for before the copy's own look is kept

        public static IModLogger Logger;

        private static readonly Dictionary<string, CornerPose> Poses = new Dictionary<string, CornerPose>(8, StringComparer.Ordinal);       // lot -> where its model stands, worked as it was declared
        private static readonly Dictionary<string, CornerHandle> Stood = new Dictionary<string, CornerHandle>(8, StringComparer.Ordinal);   // lot -> standing
        private static readonly Dictionary<string, CornerHandle> ByAddress = new Dictionary<string, CornerHandle>(8, StringComparer.Ordinal);
        private static readonly List<MadeBuilding> Pending = new List<MadeBuilding>(4);
        private static readonly List<string> PendingLooks = new List<string>(4);
        private static float _waitUntil;

        private static readonly Dictionary<string, Material> Found = new Dictionary<string, Material>(2);   // kind -> the game's material
        private static readonly Dictionary<string, Material> Plain = new Dictionary<string, Material>(2);
        private static readonly List<CornerBox> Boxes = new List<CornerBox>(2);
        private static readonly List<BuildingSignController> Wide = new List<BuildingSignController>(4);
        private static readonly List<BuildingLogoSignController> Square = new List<BuildingLogoSignController>(4);
        private static readonly List<Renderer> SignParts = new List<Renderer>(8);
        private static readonly List<Light> SignLights = new List<Light>(4);
        private static bool _searched;
        private static string _foundWords = "not looked for";

        /// <summary>The corner buildings standing at this load.</summary>
        public static int Count => Stood.Count;

        /// <summary>
        /// At game start (Core/RealLifeOverhaulMod), before any save is read: the district's buildings that wear a corner building's model
        /// are stood with it (Uptown's DistrictBuildings asks while the city loads, before every other link is made), whatever the options
        /// say, the same on every machine.
        /// </summary>
        public static void Register(IModLogger logger)
        {
            Logger = logger;
            DistrictBuildings.LookGround = Ground;
            DistrictBuildings.LookStand = Stand;
            DistrictBuildings.LookGone = Gone;
            DistrictBuildings.LooksLoading = Loading;
            DistrictBuildings.LooksTick = Tick;
        }

        /// <summary>The line about which of the game's materials were found, for the tests.</summary>
        public static string FoundWords => _foundWords;

        /// <summary>Every corner building standing, into the caller's list (cleared), for the tests.</summary>
        public static void All(List<CornerHandle> into)
        {
            into.Clear();
            foreach (CornerHandle h in Stood.Values)
            {
                into.Add(h);
            }
        }

        /// <summary>The corner building on a lot, or null.</summary>
        public static CornerHandle On(string lot) => lot != null && Stood.TryGetValue(lot, out CornerHandle h) ? h : null;

        // ---------------------------------------------------------------- the load

        /// <summary>
        /// A load's buildings about to be set in place (DistrictBuildings.LooksLoading, behind the loading screen): the last city's
        /// corner buildings forgotten (their objects went with it) and the game's brick and glass found by name, once.
        /// </summary>
        public static void Loading()
        {
            Stood.Clear();
            ByAddress.Clear();
            Pending.Clear();
            PendingLooks.Clear();
            ForgetMaterials();
            FindMaterials();   // one search, here behind the loading screen, whether or not a corner building stands yet: one handed over in play finds them found
        }

        // The game's brick and glass by name: one search of the materials in memory, only behind the loading screen or the black screen.
        private static void FindMaterials()
        {
            if (_searched)
            {
                return;
            }
            _searched = true;
            Material[] all = SceneFinds.InMemory<Material>("corner buildings: the game's brick and glass by name");
            var said = new System.Text.StringBuilder(80);
            for (int k = 0; k < CornerRules.Kinds.Length; k++)
            {
                string kind = CornerRules.Kinds[k];
                string[] names = CornerRules.GameNames(kind);
                Material pick = null;
                for (int n = 0; n < names.Length && pick == null; n++)
                {
                    for (int i = 0; i < all.Length; i++)
                    {
                        if (all[i] != null && all[i].shader != null && string.Equals(all[i].name, names[n], StringComparison.Ordinal))
                        {
                            pick = all[i];
                            break;
                        }
                    }
                }
                if (pick != null)
                {
                    Found[kind] = pick;
                }
                said.Append(k > 0 ? ", " : string.Empty).Append(kind).Append(pick != null ? " '" + pick.name + "'" : " not found");
            }
            _foundWords = said.ToString();
        }

        private static void ForgetMaterials()
        {
            foreach (Material plain in Plain.Values)
            {
                if (plain != null)
                {
                    UnityEngine.Object.Destroy(plain);
                }
            }
            Plain.Clear();
            Found.Clear();
            _searched = false;
            _foundWords = "not looked for";
        }

        // The game's material of a part's kind, else a plain stand-in of the model's own colour for it; null only if the lit shader is gone.
        private static Material MaterialOf(PropLibrary.PropPart part, out bool fromGame)
        {
            fromGame = true;
            if (Found.TryGetValue(part.Game ?? string.Empty, out Material own) && own != null)
            {
                return own;
            }
            fromGame = false;
            string key = part.Game ?? "part";
            if (Plain.TryGetValue(key, out Material plain) && plain != null)
            {
                return plain;
            }
            Shader shader = Shader.Find("HDRP/Lit");
            if (shader == null)
            {
                return null;
            }
            plain = new Material(shader) { name = "RealLifeOverhaul corner building (plain " + key + ")" };
            plain.SetColor("_BaseColor", part.Fallback);
            plain.SetFloat("_Metallic", part.Metallic);
            plain.SetFloat("_Smoothness", part.Smoothness);
            Plain[key] = plain;
            return plain;
        }

        // ---------------------------------------------------------------- the ground and the pose

        /// <summary>
        /// The ground the copy engine's building beneath a corner building stands on (DistrictBuildings.LookGround), its model's pose
        /// kept for its stand: the site less the corner the entrance keeps. False (the building on its lot's own ground, in its own look)
        /// for a look that is no corner building, a lot at no corner, or a model the site cannot hold. A building recorded with its corner
        /// look wears it whatever the options say (the options choose what is built, never how a building of the save looks), the same
        /// on every machine.
        /// </summary>
        public static bool Ground(string look, string lotId, BuildingsLot ground, out MadeLot copy)
        {
            copy = default(MadeLot);
            Poses.Remove(lotId ?? string.Empty);
            if (!CornerRules.Find(look, out CornerModel m) || !GroundOf(lotId, ground, out CornerGround g))
            {
                return false;
            }
            CornerPose p = CornerRules.Pose(g);
            if (!CornerRules.Fits(m, p))
            {
                Logger?.Info("Corners: " + look + " does not fit lot " + lotId + " (" + p.Along.ToString("F1", CultureInfo.InvariantCulture) + " by "
                             + p.Deep.ToString("F1", CultureInfo.InvariantCulture) + " m from the corner); it stands in the copy engine's own look.");
                return false;
            }
            CornerRules.CopyGround(m, g, p, out float x, out float z, out float width, out float depth);
            copy = new MadeLot(x, z, g.Facing, width, depth);
            Poses[lotId] = p;
            return true;
        }

        /// <summary>A lot's ground at a block's corner, from the plan: the corner's end and the block's edge there; false for a lot at no corner.</summary>
        public static bool GroundOf(string lotId, BuildingsLot ground, out CornerGround g)
        {
            g = default(CornerGround);
            Lot lot = UptownLots.Get(lotId);
            int facing = Mathf.RoundToInt(ground.Facing);
            if (lot == null || !LayoutRules.StreetEnds(lot.Row, lot.Column, facing, out float low, out float high))
            {
                return false;
            }
            bool alongX = facing % 180 == 0;
            float mid = alongX ? ground.X : ground.Z;
            if (!CornerRules.End(mid - ground.Width * 0.5f, mid + ground.Width * 0.5f, true, low, true, high, out bool atLow, out float edge))
            {
                return false;
            }
            g = new CornerGround { X = ground.X, Z = ground.Z, Facing = ((facing % 360) + 360) % 360, Width = ground.Width, Depth = ground.Depth, Low = atLow, Edge = edge };
            return true;
        }

        // ---------------------------------------------------------------- standing

        /// <summary>
        /// A corner building stood over the copy engine's building made on its lot (DistrictBuildings.LookStand), before it joins the game's
        /// lists: null when it stands, <see cref="Waits"/> while the props bundles are read (tried once a second by <see cref="Tick"/>), else why not.
        /// </summary>
        public static string Stand(MadeBuilding made, string look)
        {
            if (made == null || made.Controller == null || made.Spec == null)
            {
                return "its building is not there";
            }
            if (!CornerRules.Find(look, out CornerModel model))
            {
                return "no corner building is called " + (look ?? "(none)");
            }
            if (!Poses.TryGetValue(made.Spec.Id, out CornerPose pose))
            {
                return "its lot is at no corner of its block the model fits";
            }
            if (!PropLibrary.Settled)
            {
                if (!Pending.Contains(made))
                {
                    Pending.Add(made);
                    PendingLooks.Add(look);
                    _waitUntil = Time.unscaledTime + WaitSeconds;
                }
                return Waits;
            }
            if (!PropLibrary.TryGetBuilding(look, out Mesh mesh, out Material atlas, out PropLibrary.PropPart[] parts))
            {
                return PropLibrary.Ready ? "the bundle reallife_props_2 has no model " + look + " (an older install)" : "the props bundles did not load";
            }
            if (!_searched && SceneFinds.Black)
            {
                FindMaterials();   // switched on in play: found now, behind the black screen
            }
            CornerHandle old = On(made.Spec.Id);
            if (old != null)
            {
                Take(old, false);
            }
            long began = Stopwatch.GetTimestamp();
            var h = new CornerHandle
            {
                Id = look, Lot = made.Spec.Id, Address = made.Address, Key = Key(made.Spec.Street, made.Spec.Number), Made = made, Model = model, Pose = pose
            };
            string why = Build(h, mesh, atlas, parts);
            if (why != null)
            {
                return why;
            }
            MoveDoor(h);
            CornerSigns.Make(h);
            h.Ms = (Stopwatch.GetTimestamp() - began) * 1000.0 / Stopwatch.Frequency;
            Stood[h.Lot] = h;
            ByAddress[h.Key] = h;
            CornerSigns.Listen();
            Logger?.Info(CornerRules.StoodLine(look, h.Address, h.Triangles, h.Draws, h.GameMaterials, h.StandIns, h.SignsDrawn, h.SignsBlank, h.DoorMissed)
                         + " Stood in " + h.Ms.ToString("F1", CultureInfo.InvariantCulture) + " ms.");
            return null;
        }

        /// <summary>
        /// Once a second while a corner building waits for the props bundles (DistrictBuildings.LooksTick): each stands once they are read,
        /// or keeps the copy's own look when they are not in time. True when nothing waits any more.
        /// </summary>
        public static bool Tick()
        {
            if (Pending.Count == 0)
            {
                return true;
            }
            if (!PropLibrary.Settled && Time.unscaledTime < _waitUntil)
            {
                return false;
            }
            for (int i = 0; i < Pending.Count; i++)
            {
                MadeBuilding made = Pending[i];
                string look = PendingLooks[i];
                string why = PropLibrary.Settled ? Stand(made, look) : "the props bundles were not read in " + WaitSeconds + " s";
                if (why != null)
                {
                    Logger?.Info(CornerRules.FallbackLine(look, made != null ? made.Address : null, why));
                }
            }
            Pending.Clear();
            PendingLooks.Clear();
            return true;
        }

        private static string Key(string street, int number) => (street ?? string.Empty) + "|" + number.ToString(CultureInfo.InvariantCulture);

        // The model's objects under the building's own root (so they go and come with it): its mesh with the atlas and the game's materials,
        // the city map's box, its solid boxes lent to the building, its carving boxes; the building's own look hidden under it.
        private static string Build(CornerHandle h, Mesh mesh, Material atlas, PropLibrary.PropPart[] parts)
        {
            CityBuildingController c = h.Made.Controller;
            var materials = new Material[parts.Length];
            materials[0] = atlas;
            for (int i = 1; i < parts.Length; i++)
            {
                materials[i] = MaterialOf(parts[i], out bool fromGame);
                if (materials[i] == null)
                {
                    return "the game's lit shader was not found for a stand-in material";
                }
                h.GameMaterials += fromGame ? 1 : 0;
                h.StandIns += fromGame ? 0 : 1;
            }
            // Its own look hidden for good first (the renderers under it now: never the model's), and hiding the building's look from now on
            // hides the model instead (a demolition's wrap, the frame cost test).
            CornerHandle wearer = h;
            CopyGame.WearOver(c, shown => Show(wearer, shown));
            int layer = LayerHelper.BuildingLayerIndex;
            var root = new GameObject(RootName + " " + h.Id);
            root.SetActive(false);
            root.layer = layer;
            root.transform.SetParent(c.transform, true);
            root.transform.SetPositionAndRotation(new Vector3(h.Pose.X, UptownLots.GroundY, h.Pose.Z), Quaternion.Euler(0f, h.Pose.Yaw, 0f));
            root.transform.localScale = Inverse(c.transform.lossyScale);
            var drawn = new GameObject("look") { layer = layer };
            drawn.transform.SetParent(root.transform, false);
            drawn.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = drawn.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = materials;
            Style(c, renderer, ShadowCastingMode.On);
            CornerModel m = h.Model;
            MeshRenderer box = null;
            Mesh cube = c.lodVersion != null && c.lodVersion.sharedMaterial != null ? Resources.GetBuiltinResource<Mesh>("Cube.fbx") : null;
            if (cube != null)
            {
                var boxObject = new GameObject("map box") { layer = layer };
                boxObject.transform.SetParent(root.transform, false);
                boxObject.transform.localPosition = new Vector3(m.Wide * 0.5f, m.Height * 0.5f, m.Deep * 0.5f);
                boxObject.transform.localScale = new Vector3(m.Wide, m.Height, m.Deep);
                boxObject.AddComponent<MeshFilter>().sharedMesh = cube;
                box = boxObject.AddComponent<MeshRenderer>();
                box.sharedMaterial = c.lodVersion.sharedMaterial;
                Style(c, box, ShadowCastingMode.ShadowsOnly);
            }
            CornerRules.Boxes(m, Boxes);
            var partsList = new List<ViewBlockingEntityPart>(Boxes.Count);
            for (int i = 0; i < Boxes.Count; i++)
            {
                CornerBox b = Boxes[i];
                var piece = new GameObject("body " + i) { layer = layer };
                piece.transform.SetParent(root.transform, false);
                piece.transform.localPosition = new Vector3((b.X1 + b.X2) * 0.5f, b.Height * 0.5f, (b.Z1 + b.Z2) * 0.5f);
                BoxCollider wall = piece.AddComponent<BoxCollider>();
                wall.size = new Vector3(b.X2 - b.X1, b.Height, b.Z2 - b.Z1);
                ViewBlockingEntityPart part = piece.AddComponent<ViewBlockingEntityPart>();
                partsList.Add(part);
                h.Solids++;
                CornerRules.Carve(b, out float sizeX, out float sizeZ);
                NavMeshObstacle carve = piece.AddComponent<NavMeshObstacle>();
                carve.shape = NavMeshObstacleShape.Box;
                carve.center = Vector3.zero;
                carve.size = new Vector3(sizeX, Mathf.Max(2f, b.Height), sizeZ);
                carve.carveOnlyStationary = true;
                carve.carving = true;
                h.Carves++;
            }
            h.Root = root;
            h.Drawn = renderer;
            h.Box = box;
            CornerSigns.Prepare(h);   // the signs' objects, one an anchor, so the fade and the map switch them with the model
            var hide = new List<Renderer>(1 + h.Texts.Count) { renderer };
            for (int i = 0; i < h.Texts.Count; i++)
            {
                if (h.Texts[i].Renderer != null)
                {
                    hide.Add(h.Texts[i].Renderer);
                }
            }
            CopyLook look = root.AddComponent<CopyLook>();
            look.Fill(box, new List<Renderer>(0), hide, partsList, new List<GameObject>(0));
            root.SetActive(true);
            look.Lend(c);   // a hover or a click on the model is one on the building
            if (c.renderersToHide != null && !c.renderersToHide.Contains(renderer))
            {
                c.renderersToHide.Add(renderer);   // the game's own fade of the building hides the model too
            }
            h.Draws = materials.Length;
            for (int i = 0; i < parts.Length; i++)
            {
                h.Triangles += parts[i].Triangles;
            }
            return null;
        }

        // The settings of the building's own renderers (light probes, reflections, shadows, layers): what lights the model as the city's buildings.
        private static void Style(CityBuildingController c, MeshRenderer renderer, ShadowCastingMode shadows)
        {
            renderer.shadowCastingMode = shadows;
            MeshRenderer[] own = c.GetComponentsInChildren<MeshRenderer>(true);   // once a stand
            for (int i = 0; i < own.Length; i++)
            {
                if (own[i] != c.lodVersion && own[i] != renderer)
                {
                    renderer.receiveShadows = own[i].receiveShadows;
                    renderer.lightProbeUsage = own[i].lightProbeUsage;
                    renderer.reflectionProbeUsage = own[i].reflectionProbeUsage;
                    renderer.motionVectorGenerationMode = own[i].motionVectorGenerationMode;
                    renderer.renderingLayerMask = own[i].renderingLayerMask;
                    return;
                }
            }
        }

        private static Vector3 Inverse(Vector3 s)
        {
            return new Vector3(1f / Mathf.Max(1e-4f, Mathf.Abs(s.x)), 1f / Mathf.Max(1e-4f, Mathf.Abs(s.y)), 1f / Mathf.Max(1e-4f, Mathf.Abs(s.z)));
        }

        // The building's doors moved to the model's entrance (where the game walks people to, puts them out and checks a save made inside),
        // and its own door sign (the game's sign of the business) carried with them, scaled to the entrance and shown.
        private static void MoveDoor(CornerHandle h)
        {
            MadeBuilding made = h.Made;
            CityBuildingController c = made.Controller;
            CornerRules.Door(h.Model, h.Pose, out float doorX, out float doorZ, out float facing);
            CornerRules.Face(h.Model, h.Pose, out float faceX, out float faceZ);
            Quaternion out1 = Quaternion.Euler(0f, facing, 0f);
            BuildingEntranceDoor first = c.entranceDoors != null && c.entranceDoors.Length > 0 ? c.entranceDoors[0] : null;
            Transform door0 = first != null ? first.doorTransform : null;
            if (door0 != null)
            {
                CarrySigns(h, door0, new Vector3(faceX, door0.position.y, faceZ), out1);
            }
            for (int i = 0; c.entranceDoors != null && i < c.entranceDoors.Length; i++)
            {
                Transform t = c.entranceDoors[i] != null ? c.entranceDoors[i].doorTransform : null;
                if (t != null && t != c.transform)
                {
                    t.SetPositionAndRotation(new Vector3(doorX, t.position.y, doorZ), out1);
                }
            }
            Vector3 at = door0 != null ? door0.position : new Vector3(doorX, made.DoorY, doorZ);
            made.DoorX = at.x;
            made.DoorZ = at.z;
            made.DoorFacing = facing;
            h.DoorMissed = Mathf.Sqrt((at.x - doorX) * (at.x - doorX) + (at.z - doorZ) * (at.z - doorZ));
            if (door0 != null)
            {
                c.poiOffsetOnStreet = door0.position - (c.upperFloor != null ? c.upperFloor.transform.position : c.transform.position) + 2f * Vector3.up;
            }
        }

        // The building's own door signs carried rigidly from its door to the entrance's face, turned to it, scaled to its width, and drawn again.
        private static void CarrySigns(CornerHandle h, Transform door, Vector3 face, Quaternion facing)
        {
            CityBuildingController c = h.Made.Controller;
            Wide.Clear();
            Square.Clear();
            c.GetComponentsInChildren(true, Wide);
            c.GetComponentsInChildren(true, Square);
            Vector3 from = door.position;
            Quaternion turn = facing * Quaternion.Inverse(Quaternion.LookRotation(Flat(door.forward), Vector3.up));
            Vector3 right = Flat(door.right);
            float lo = float.MaxValue, hi = float.MinValue, low = float.MaxValue, high = float.MinValue;
            for (int i = 0; i < Wide.Count + Square.Count; i++)
            {
                Reach(i < Wide.Count ? Wide[i].transform : Square[i - Wide.Count].transform, from, right, ref lo, ref hi, ref low, ref high);
            }
            float scale = hi > lo ? CornerRules.SignScale(h.Model, hi - lo) : 1f;
            if (hi > lo && high > low)
            {
                // Where it stands at the entrance, in the model's frame, so the model's own anchors it covers are left blank (CornerRules.Covered).
                float side = (lo + hi) * 0.5f * scale;
                h.SignCarried = true;
                h.SignX = h.Model.FaceX + -h.Model.OutZ * side;
                h.SignZ = h.Model.FaceZ + h.Model.OutX * side;
                h.SignY = from.y - UptownLots.GroundY + (low + high) * 0.5f * scale;
                h.SignWidth = (hi - lo) * scale;
                h.SignHeight = (high - low) * scale;
            }
            for (int i = 0; i < Wide.Count + Square.Count; i++)
            {
                Transform t = i < Wide.Count ? Wide[i].transform : Square[i - Wide.Count].transform;
                if (t == c.transform || door.IsChildOf(t))
                {
                    continue;   // a sign holder that carries the door or the building: never moved
                }
                Vector3 offset = turn * (t.position - from) * scale;
                t.SetPositionAndRotation(face + offset, turn * t.rotation);
                t.localScale *= scale;
                SignParts.Clear();
                t.GetComponentsInChildren(true, SignParts);
                for (int k = 0; k < SignParts.Count; k++)
                {
                    SignParts[k].forceRenderingOff = FarDistricts.LeaveAlone(SignParts[k], false);   // drawn again: the business's own sign
                    h.GameSigns.Add(SignParts[k]);
                }
                SignLights.Clear();
                t.GetComponentsInChildren(true, SignLights);
                for (int k = 0; k < SignLights.Count; k++)
                {
                    SignLights[k].enabled = true;   // its light, switched by the game's own hours by its object
                }
            }
            Wide.Clear();
            Square.Clear();
        }

        private static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v.sqrMagnitude > 1e-6f ? v.normalized : Vector3.forward;
        }

        private static readonly List<MeshFilter> SignMeshes = new List<MeshFilter>(8);

        // How far a sign's meshes reach from the door: along its right (lo, hi) and up (low, high), from each mesh's own corners, so a sign
        // the game keeps switched off (a unit with no business yet) is measured as well.
        private static void Reach(Transform sign, Vector3 from, Vector3 right, ref float lo, ref float hi, ref float low, ref float high)
        {
            SignMeshes.Clear();
            sign.GetComponentsInChildren(true, SignMeshes);
            for (int i = 0; i < SignMeshes.Count; i++)
            {
                Mesh mesh = SignMeshes[i].sharedMesh;
                if (mesh == null)
                {
                    continue;
                }
                Bounds b = mesh.bounds;
                Matrix4x4 m = SignMeshes[i].transform.localToWorldMatrix;
                for (int k = 0; k < 8; k++)
                {
                    Vector3 corner = new Vector3((k & 1) == 0 ? b.min.x : b.max.x, (k & 2) == 0 ? b.min.y : b.max.y, (k & 4) == 0 ? b.min.z : b.max.z);
                    Vector3 d = m.MultiplyPoint3x4(corner) - from;
                    float along = Vector3.Dot(d, right);
                    lo = Mathf.Min(lo, along);
                    hi = Mathf.Max(hi, along);
                    low = Mathf.Min(low, d.y);
                    high = Mathf.Max(high, d.y);
                }
            }
            SignMeshes.Clear();
        }

        // The model hidden or shown with the building's look (CopyGame's WearOver): a demolition's wrap, the frame cost test.
        private static void Show(CornerHandle h, bool shown)
        {
            if (h == null || h.Root == null)
            {
                return;
            }
            h.Hidden = !shown;
            if (h.Drawn != null)
            {
                h.Drawn.forceRenderingOff = !shown;
            }
            if (h.Box != null)
            {
                h.Box.forceRenderingOff = !shown;
            }
            for (int i = 0; i < h.GameSigns.Count; i++)
            {
                if (h.GameSigns[i] != null)
                {
                    h.GameSigns[i].forceRenderingOff = !shown;
                }
            }
            CornerSigns.Show(h, shown);
        }

        /// <summary>A corner building knocked down (DistrictBuildings.LookGone): its model, signs and carving taken away with it.</summary>
        public static void Gone(MadeBuilding made)
        {
            CornerHandle h = made != null && made.Spec != null ? On(made.Spec.Id) : null;
            if (h != null)
            {
                Take(h, false);
                Logger?.Info("Corners: " + h.Id + " at " + h.Address + " taken away with its building.");
            }
        }

        // A corner building taken away; its building's own look shown again only with showOwn (a test's way back).
        private static void Take(CornerHandle h, bool showOwn)
        {
            Stood.Remove(h.Lot);
            ByAddress.Remove(h.Key);
            CityBuildingController c = h.Made != null ? h.Made.Controller : null;
            if (c != null)
            {
                c.renderersToHide?.Remove(h.Drawn);
                CopyGame.TakeOff(c, showOwn);
            }
            CornerSigns.Drop(h);
            if (h.Root != null)
            {
                UnityEngine.Object.Destroy(h.Root);
            }
            h.Root = null;
        }

        /// <summary>True while a corner building stands: the registration change handler has nothing to look up before one does (no key is made).</summary>
        internal static bool Any => ByAddress.Count > 0;

        /// <summary>The corner building whose registration changed (the game's event), or null.</summary>
        internal static CornerHandle AtAddress(string street, int number) => ByAddress.TryGetValue(Key(street, number), out CornerHandle h) ? h : null;

        /// <summary>The figures of a standing corner building for a test's line: null when it stands whole, else why not.</summary>
        public static string Judge(CornerHandle h, out string said)
        {
            said = null;
            if (h == null || h.Root == null)
            {
                return "it does not stand";
            }
            Material[] materials = h.Drawn != null ? h.Drawn.sharedMaterials : null;
            if (materials == null || materials.Length != h.Draws || materials.Length < 1 || materials.Length > CornerRules.MostDraws)
            {
                return "its renderer has " + (materials?.Length ?? 0) + " material(s), not the " + h.Draws + " of its parts";
            }
            for (int i = 0; i < materials.Length; i++)
            {
                if (materials[i] == null)
                {
                    return "material " + i + " is missing";
                }
            }
            CityBuildingController c = h.Made != null ? h.Made.Controller : null;
            if (c == null || !CopyGame.Wears(c))
            {
                return "the building beneath does not wear the model (its own look is not hidden under it)";
            }
            if (h.DoorMissed > 0.05f)
            {
                return "its door is " + h.DoorMissed.ToString("F2", CultureInfo.InvariantCulture) + " m from the model's entrance";
            }
            CornerRules.Boxes(h.Model, Boxes);
            if (h.Carves != Boxes.Count || h.Solids != Boxes.Count)
            {
                return "it has " + h.Solids + " solid and " + h.Carves + " carved box(es), the model " + Boxes.Count;
            }
            for (int i = 0; i < Boxes.Count; i++)
            {
                if (CornerRules.InCarve(Boxes[i], h.Model.DoorX, h.Model.DoorZ))
                {
                    return "its entrance is inside its own carving";
                }
            }
            Vector3 door = new Vector3(h.Made.DoorX, h.Made.DoorY, h.Made.DoorZ);
            if (!NavMesh.SamplePosition(door, out NavMeshHit _, 1.5f, NavMesh.AllAreas))
            {
                return "its door is not within 1.5 m of the walking mesh";
            }
            said = h.Id + " at " + h.Address + ": " + h.Triangles + " triangles, " + h.Draws + " draw calls (" + h.GameMaterials + " of the game's materials"
                   + (h.StandIns > 0 ? ", " + h.StandIns + " stand-ins" : string.Empty) + "), turned " + h.Pose.Yaw + ", " + h.SignsDrawn + " sign(s) drawn"
                   + (h.SignsBlank > 0 ? " (" + h.SignsBlank + " blank)" : string.Empty) + ", the door on the walking mesh, the building's own look hidden";
            return null;
        }

        // ---------------------------------------------------------------- build mode's ghost

        /// <summary>
        /// Build mode's ghost of a corner building: its model alone at the street corner, turned to the two streets (no box, no signs,
        /// nothing solid); its root, or null while the props bundles are not read.
        /// </summary>
        public static Transform Ghost(string look, float x, float z, int yaw)
        {
            if (!PropLibrary.TryGetBuilding(look, out Mesh mesh, out Material atlas, out PropLibrary.PropPart[] parts))
            {
                return null;
            }
            var materials = new Material[parts.Length];
            materials[0] = atlas;
            for (int i = 1; i < parts.Length; i++)
            {
                materials[i] = MaterialOf(parts[i], out _);
                if (materials[i] == null)
                {
                    return null;
                }
            }
            var root = new GameObject(GhostName + " " + look) { layer = LayerHelper.BuildingLayerIndex };
            root.transform.SetPositionAndRotation(new Vector3(x, UptownLots.GroundY, z), Quaternion.Euler(0f, yaw, 0f));
            root.AddComponent<MeshFilter>().sharedMesh = mesh;
            root.AddComponent<MeshRenderer>().sharedMaterials = materials;
            return root.transform;
        }

        /// <summary>Build mode's ghost of a corner building taken away.</summary>
        public static void GhostGone(Transform root)
        {
            if (root != null && root.name.StartsWith(GhostName, StringComparison.Ordinal))
            {
                UnityEngine.Object.Destroy(root.gameObject);
            }
        }
    }
}
