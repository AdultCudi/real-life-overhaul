using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using Helpers;
using RealLifeOverhaul.NewBuildings;
using RealLifeOverhaul.Uptown;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;

namespace RealLifeOverhaul.Schools
{
    /// <summary>One of the campus's models as it stands: its objects, its figures, and what it took over from the copy engine's building.</summary>
    internal sealed class LookHandle
    {
        public string Id;
        public string Name;
        public GameObject Root;                 // the model's own root: its mesh, its map box, its solid boxes and its carving boxes
        public MeshRenderer Drawn;              // the one renderer that draws the model's submeshes
        public MeshRenderer Box;                // the city map's stand-in (null where the game's box material was not found)
        public int Triangles, Draws, GameMaterials, StandIns, Solids, Carves;
        public double Ms;                       // milliseconds it took to stand
        public CityBuildingController Over;     // the main school's building, whose own look is hidden under this one
        public bool HidOwn;                     // that look was hidden by this handle (and is shown again as it goes)
        public int ReplacedTriangles, ReplacedDraws;   // what the look hidden under it drew; 0 where nothing was hidden
        public float DoorMissed = -1f;          // m between the model's door and the building's own door (the main school's); -1 where it has none
        public string Why;                      // why it was not stood
    }

    /// <summary>
    /// The only part of Birchwood School's own look (docs/features/schools.md, "The look") that touches the game: the three models
    /// of the second props bundle stood in the city, each one mesh with a submesh for the bundle's atlas and one for each of the
    /// game's own materials it names (brick, roof, glass, the rooftop fence), those found once a load behind the loading screen
    /// by name; drawn as the city's own copies are (their renderers' lighting settings copied, the see-through of the game's fade,
    /// the city map's stand-in box: <see cref="CopyLook"/>), the hall and the junior block solid and carved out of the walking
    /// meshes, the main school over the copy engine's building (its look hidden by the engine's own path, its door, floors,
    /// walls and click kept). Stands at a load, or again from a test; never per frame.
    /// </summary>
    internal static class CampusLook
    {
        private const string RootName = "RealLifeOverhaul Schools campus look";

        // How the copy engine's own renderers are lit, read from the main school's building at a load and given to the models'.
        private static bool _styled;
        private static LightProbeUsage _probes = LightProbeUsage.BlendProbes;
        private static ReflectionProbeUsage _reflections = ReflectionProbeUsage.BlendProbes;
        private static bool _receive = true;
        private static MotionVectorGenerationMode _motion = MotionVectorGenerationMode.ForceNoMotion;
        private static uint _layerMask = 1u;
        private static Material _boxMaterial;                // the game's building box on the city map, read from the main school's building

        private static readonly Dictionary<string, Material> Found = new Dictionary<string, Material>(4);     // kind -> the game's material
        private static readonly Dictionary<string, Material> Plain = new Dictionary<string, Material>(4);     // kind -> a plain stand-in of ours
        private static readonly List<MeshRenderer> Scratch = new List<MeshRenderer>(64);
        private static readonly List<LookBox> Boxes = new List<LookBox>(2);
        private static bool _searched;
        private static string _foundWords = "not looked for";
        private static double _searchMs;

        /// <summary>The line about which of the game's materials were found by name, for Player.log.</summary>
        public static string FoundWords => _foundWords;

        /// <summary>True once the copy engine's own renderers' lighting settings were read for the models.</summary>
        public static bool Styled => _styled;

        /// <summary>Milliseconds the one search of memory for the game's materials took at this load.</summary>
        public static double SearchMs => _searchMs;

        /// <summary>True once the game's materials were looked for at this load.</summary>
        public static bool Searched => _searched;

        /// <summary>Whether the second bundle's model of that id is loaded; else why not.</summary>
        public static bool Has(string id, out string why)
        {
            why = null;
            if (!PropLibrary.Ready)
            {
                why = PropLibrary.Settled ? "the props bundles did not load" : "the props bundles are not loaded yet";
                return false;
            }
            if (!PropLibrary.TryGetBuilding(id, out _, out _, out _))
            {
                why = "the bundle reallife_props_2 has no model '" + id + "' (an older install, or the bundle is missing)";
                return false;
            }
            return true;
        }

        // ---------------------------------------------------------------- the game's materials

        /// <summary>
        /// The game's brick, roof, glass and chain-link materials found by name in memory (a search of the game's loaded materials, so only
        /// behind the loading screen or a pause), once a load. A kind the game has none of is stood in for by a plain colour.
        /// </summary>
        public static void FindMaterials()
        {
            if (_searched)
            {
                return;
            }
            _searched = true;
            long began = Stopwatch.GetTimestamp();
            Material[] all = SceneFinds.InMemory<Material>("campus look: the game's brick, roof, glass and chain-link materials by name");
            var said = new System.Text.StringBuilder(160);
            for (int k = 0; k < CampusLookRules.Kinds.Length; k++)
            {
                string kind = CampusLookRules.Kinds[k];
                Material pick = null;
                string[] names = CampusLookRules.GameNames(kind);
                for (int n = 0; n < names.Length && pick == null; n++)
                {
                    for (int i = 0; i < all.Length; i++)
                    {
                        Material m = all[i];
                        if (m != null && m.shader != null && string.Equals(m.name, names[n], StringComparison.Ordinal))
                        {
                            pick = m;
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
            _searchMs = (Stopwatch.GetTimestamp() - began) * 1000.0 / Stopwatch.Frequency;
            _foundWords = said.ToString();
        }

        // The game's material of a kind, or for a fence the glass's; a plain stand-in of the part's colour where the game has none. Null only if the lit shader is gone.
        private static Material MaterialOf(PropLibrary.PropPart part, out bool fromGame)
        {
            fromGame = true;
            string kind = part.Game;
            if (Found.TryGetValue(kind, out Material own) && own != null)
            {
                return own;
            }
            if (kind == "fence" && Found.TryGetValue("glass", out Material glass) && glass != null)
            {
                return glass;
            }
            fromGame = false;
            string key = CampusLookRules.StandsInFor(kind);
            if (Plain.TryGetValue(key, out Material plain) && plain != null)
            {
                return plain;
            }
            Shader shader = Shader.Find("HDRP/Lit");
            if (shader == null)
            {
                return null;
            }
            plain = new Material(shader) { name = "RealLifeOverhaul campus look (plain " + key + ")" };
            plain.SetColor("_BaseColor", part.Fallback);
            plain.SetFloat("_Metallic", part.Metallic);
            plain.SetFloat("_Smoothness", part.Smoothness);
            Plain[key] = plain;
            return plain;
        }

        /// <summary>The materials found and the stand-ins made are let go (the campus going, the option off, the city unloading).</summary>
        public static void Forget()
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
            _styled = false;
            _boxMaterial = null;
            _foundWords = "not looked for";
            _searchMs = 0;
        }

        // ---------------------------------------------------------------- how the copy engine's buildings are lit

        /// <summary>Reads the lighting settings of the copy engine's building's own renderers (and its map box's material), to give the models the same.</summary>
        public static void Capture(CityBuildingController building)
        {
            if (building == null)
            {
                return;
            }
            Scratch.Clear();
            building.GetComponentsInChildren(true, Scratch);
            for (int i = 0; i < Scratch.Count; i++)
            {
                if (Scratch[i] != building.lodVersion)
                {
                    MeshRenderer r = Scratch[i];
                    _probes = r.lightProbeUsage;
                    _reflections = r.reflectionProbeUsage;
                    _receive = r.receiveShadows;
                    _motion = r.motionVectorGenerationMode;
                    _layerMask = r.renderingLayerMask;
                    _styled = true;
                    break;
                }
            }
            Scratch.Clear();
            if (building.lodVersion != null && building.lodVersion.sharedMaterial != null)
            {
                _boxMaterial = building.lodVersion.sharedMaterial;
            }
        }

        // ---------------------------------------------------------------- standing

        /// <summary>
        /// A block's look (the hall, the junior block) on its plot: solid (a box collider each, joined to the game's see-through fade) and
        /// carved out of the walking meshes. Null with <see cref="LookHandle.Why"/> when it cannot be stood.
        /// </summary>
        public static LookHandle StandBlock(string id, string name, CampusBlockPlot plot, Transform parent)
        {
            if (!CampusLookRules.Find(id, out LookModel model))
            {
                return Refused(id, name, "no such model");
            }
            CampusLookRules.RootOnPlot(model, plot, out float x, out float z, out float yaw);
            return Stand(model, name, new Vector3(x, UptownLots.GroundY, z), yaw, parent, true);
        }

        /// <summary>
        /// The main school's look over the copy engine's building: its door where the building's own door is. Its own look is hidden by
        /// the engine's path (CopyGame.HideLook), its fade lists given the model, its walking meshes cut to the model's body. Null with why when it cannot.
        /// </summary>
        public static LookHandle StandMain(MadeBuilding made, string name, Transform parent)
        {
            if (made == null || made.Controller == null || made.Spec == null || made.Lot < 0 || made.Lot >= made.Spec.Lots.Length)
            {
                return Refused(CampusLookRules.MainModel, name, "the main school's building is not there");
            }
            CampusLookRules.Find(CampusLookRules.MainModel, out LookModel model);
            CityBuildingController c = made.Controller;
            Vector3 door = c.entranceDoors != null && c.entranceDoors.Length > 0 && c.entranceDoors[0] != null && c.entranceDoors[0].doorTransform != null
                ? c.entranceDoors[0].doorTransform.position : new Vector3(made.DoorX, made.DoorY, made.DoorZ);
            CampusLookRules.RootOnDoor(model, door.x, door.z, made.DoorFacing, out float x, out float z, out float yaw);
            // How far that puts the look from where the copy engine stood its footprint on the lot: across the lot, and along its front.
            MadeLot lot = made.Spec.Lots[made.Lot];
            double f = lot.Facing * Math.PI / 180.0;
            float fx = (float)Math.Sin(f), fz = (float)Math.Cos(f), rx = (float)Math.Cos(f), rz = -(float)Math.Sin(f);
            double a = yaw * Math.PI / 180.0;
            float backX = (float)Math.Sin(a), backZ = (float)Math.Cos(a);
            float centreX = x + backX * model.Depth * 0.5f, centreZ = z + backZ * model.Depth * 0.5f;
            float aside = (centreX - lot.X) * rx + (centreZ - lot.Z) * rz;
            float expectedWall = lot.Depth * 0.5f - lot.Inset;
            float forward = (x - lot.X) * fx + (z - lot.Z) * fz - expectedWall;
            string why = CampusLookRules.MainWhy(aside, forward);
            if (why != null)
            {
                return Refused(CampusLookRules.MainModel, name, why);
            }
            int replacedTriangles = 0, replacedDraws = 0;
            Measure(c, ref replacedTriangles, ref replacedDraws);
            LookHandle h = Stand(model, name, new Vector3(x, c.transform.position.y, z), yaw, parent, false);
            if (h.Root == null)
            {
                return h;
            }
            h.Over = c;
            h.ReplacedTriangles = replacedTriangles;
            h.ReplacedDraws = replacedDraws;
            // The door of the model is the building's own: its front face, 1.55 m in front of the wall along the way the door faces.
            float outX = (float)Math.Sin(made.DoorFacing * Math.PI / 180.0), outZ = (float)Math.Cos(made.DoorFacing * Math.PI / 180.0);
            Vector3 modelDoor = h.Root.transform.TransformPoint(new Vector3(0f, 0f, -model.DoorOut));
            h.DoorMissed = (float)Math.Sqrt((modelDoor.x - door.x) * (modelDoor.x - door.x) + (modelDoor.z - door.z) * (modelDoor.z - door.z));
            if (c.renderersToHide != null && !c.renderersToHide.Contains(h.Drawn))
            {
                c.renderersToHide.Add(h.Drawn);      // the game's own fade of the building (the pointer's hover and click are the building's already)
            }
            h.HidOwn = CopyGame.HideLook(c);
            return h;
        }

        private static LookHandle Stand(LookModel model, string name, Vector3 at, float yaw, Transform parent, bool solid)
        {
            long began = Stopwatch.GetTimestamp();
            var h = new LookHandle { Id = model.Id, Name = name };
            if (!PropLibrary.TryGetBuilding(model.Id, out Mesh mesh, out Material atlas, out PropLibrary.PropPart[] parts))
            {
                return Refused(model.Id, name, "the model is not in the bundle");
            }
            if (!_searched)
            {
                // The game's materials are found once, behind the loading screen (FindMaterials); a search in play would stutter.
                return Refused(model.Id, name, "the game's materials were not looked for behind the loading screen");
            }
            var materials = new Material[parts.Length];
            materials[0] = atlas;
            for (int i = 1; i < parts.Length; i++)
            {
                materials[i] = MaterialOf(parts[i], out bool fromGame);
                if (materials[i] == null)
                {
                    return Refused(model.Id, name, "the game's lit shader was not found for a stand-in material");
                }
                h.GameMaterials += fromGame ? 1 : 0;
                h.StandIns += fromGame ? 0 : 1;
            }
            int layer = LayerHelper.BuildingLayerIndex;
            var root = new GameObject(RootName + " " + name);
            root.SetActive(false);
            root.layer = layer;
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(at, Quaternion.Euler(0f, yaw, 0f));
            var drawn = new GameObject("look") { layer = layer };
            drawn.transform.SetParent(root.transform, false);
            drawn.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = drawn.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = materials;
            Style(renderer, ShadowCastingMode.On);
            CampusLookRules.Boxes(model, Boxes);
            LookBox body = Boxes[0];
            // The city map's stand-in: the game's building box over the body, drawn only as a shadow in the street, whole with the map open.
            MeshRenderer box = null;
            Mesh cube = _boxMaterial != null ? Resources.GetBuiltinResource<Mesh>("Cube.fbx") : null;
            if (cube != null)
            {
                var boxObject = new GameObject("map box") { layer = layer };
                boxObject.transform.SetParent(root.transform, false);
                boxObject.transform.localPosition = new Vector3(body.X, body.Height * 0.5f, body.Z);
                boxObject.transform.localScale = new Vector3(body.HalfX * 2f, body.Height, body.HalfZ * 2f);
                boxObject.AddComponent<MeshFilter>().sharedMesh = cube;
                box = boxObject.AddComponent<MeshRenderer>();
                box.sharedMaterial = _boxMaterial;
                Style(box, ShadowCastingMode.ShadowsOnly);
            }
            var partsList = new List<ViewBlockingEntityPart>(2);
            CopyLook look = root.AddComponent<CopyLook>();
            for (int i = 0; i < Boxes.Count; i++)
            {
                LookBox b = Boxes[i];
                var piece = new GameObject("body " + i) { layer = layer };
                piece.transform.SetParent(root.transform, false);
                piece.transform.localPosition = new Vector3(b.X, b.Height * 0.5f, b.Z);
                if (solid)
                {
                    BoxCollider wall = piece.AddComponent<BoxCollider>();
                    wall.size = new Vector3(b.HalfX * 2f, b.Height, b.HalfZ * 2f);
                    ViewBlockingEntityPart part = piece.AddComponent<ViewBlockingEntityPart>();
                    part.cityBuildingController = look;
                    partsList.Add(part);
                    h.Solids++;
                }
                CampusLookRules.CarveSize(b, out float sizeX, out float sizeZ);
                NavMeshObstacle carve = piece.AddComponent<NavMeshObstacle>();
                carve.shape = NavMeshObstacleShape.Box;
                carve.center = Vector3.zero;
                carve.size = new Vector3(sizeX, Mathf.Max(2f, b.Height), sizeZ);
                carve.carveOnlyStationary = true;
                carve.carving = true;
                h.Carves++;
            }
            look.Fill(box, new List<Renderer>(0), new List<Renderer>(1) { renderer }, partsList, new List<GameObject>(0));
            root.SetActive(true);
            h.Root = root;
            h.Drawn = renderer;
            h.Box = box;
            h.Draws = materials.Length;
            for (int i = 0; i < parts.Length; i++)
            {
                h.Triangles += parts[i].Triangles;
            }
            h.Ms = (Stopwatch.GetTimestamp() - began) * 1000.0 / Stopwatch.Frequency;
            return h;
        }

        // The settings of the copy engine's own renderers: what lights the model as the city's buildings.
        private static void Style(MeshRenderer renderer, ShadowCastingMode shadows)
        {
            renderer.shadowCastingMode = shadows;
            renderer.receiveShadows = _receive;
            renderer.lightProbeUsage = _probes;
            renderer.reflectionProbeUsage = _reflections;
            renderer.motionVectorGenerationMode = _motion;
            renderer.renderingLayerMask = _layerMask;
        }

        private static LookHandle Refused(string id, string name, string why)
        {
            return new LookHandle { Id = id, Name = name, Why = why };
        }

        /// <summary>A look taken away: its objects, its place in the building's fade lists, and the look it hid shown again. Safe on null.</summary>
        public static void Take(ref LookHandle h)
        {
            if (h == null)
            {
                return;
            }
            if (h.Over != null)
            {
                h.Over.renderersToHide?.Remove(h.Drawn);
                if (h.HidOwn)
                {
                    CopyGame.ShowLook(h.Over);
                }
            }
            if (h.Root != null)
            {
                UnityEngine.Object.Destroy(h.Root);
            }
            h = null;
        }

        /// <summary>The triangles and draw calls a building's own parts draw: its meshes' triangles and its renderers' materials, its further levels of detail left out.</summary>
        public static void Measure(Component root, ref int triangles, ref int draws)
        {
            if (root == null)
            {
                return;
            }
            var detail = new HashSet<Renderer>();
            LODGroup[] groups = root.GetComponentsInChildren<LODGroup>(true);
            for (int g = 0; g < groups.Length; g++)
            {
                LOD[] levels = groups[g].GetLODs();
                for (int l = 1; l < levels.Length; l++)
                {
                    for (int r = 0; levels[l].renderers != null && r < levels[l].renderers.Length; r++)
                    {
                        if (levels[l].renderers[r] != null)
                        {
                            detail.Add(levels[l].renderers[r]);
                        }
                    }
                }
            }
            MeshRenderer[] all = root.GetComponentsInChildren<MeshRenderer>(true);
            CityBuildingController own = root as CityBuildingController;
            for (int i = 0; i < all.Length; i++)
            {
                MeshRenderer r = all[i];
                if (detail.Contains(r) || (own != null && r == own.lodVersion) || !r.TryGetComponent(out MeshFilter filter) || filter.sharedMesh == null)
                {
                    continue;
                }
                Mesh m = filter.sharedMesh;
                int sub = Mathf.Min(m.subMeshCount, Mathf.Max(1, r.sharedMaterials.Length));
                for (int s = 0; s < sub; s++)
                {
                    triangles += (int)m.GetIndexCount(s) / 3;
                }
                draws += r.sharedMaterials.Length;
            }
        }

        /// <summary>The figures of a stood look for a test's line: whether it is whole as it should be, else why not.</summary>
        public static string Judge(LookHandle h, out string said)
        {
            said = "not stood";
            if (h == null || h.Root == null)
            {
                return h?.Why ?? "it was not stood";
            }
            Material[] materials = h.Drawn != null ? h.Drawn.sharedMaterials : null;
            if (materials == null || materials.Length != h.Draws || materials.Length < 2)
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
            CampusLookRules.Find(h.Id, out LookModel model);
            CampusLookRules.Boxes(model, Boxes);
            if (h.Carves != Boxes.Count)
            {
                return "it carves " + h.Carves + " box(es), the model has " + Boxes.Count;
            }
            if (h.Over == null && h.Solids != Boxes.Count)
            {
                return "it has " + h.Solids + " solid box(es), a block of the model's shape has " + Boxes.Count;
            }
            if (h.Over != null && h.DoorMissed > 0.05f)
            {
                return "its door is " + h.DoorMissed.ToString("F2", CultureInfo.InvariantCulture) + " m from the building's own";
            }
            said = h.Name + " " + h.Triangles + " triangles, " + h.Draws + " draw calls, " + h.GameMaterials + " game materials"
                   + (h.StandIns > 0 ? ", " + h.StandIns + " stand-ins" : string.Empty) + ", " + h.Solids + " solid and " + h.Carves + " carved box(es), stood in "
                   + h.Ms.ToString("F1", CultureInfo.InvariantCulture) + " ms"
                   + (h.Over != null ? ", its door " + h.DoorMissed.ToString("F2", CultureInfo.InvariantCulture) + " m from the building's" : string.Empty);
            return null;
        }
    }
}
