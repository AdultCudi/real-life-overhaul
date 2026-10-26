using System;
using System.Globalization;
using BAModAPI;
using BigAmbitions.InputSystem;
using Buildings;
using Helpers;
using UI;
using UI.Smartphone;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;

namespace RealLifeOverhaul.Construction
{
    /// <summary>
    /// The one file build mode touches the game from (plans/build-mode.md section 1): the city map opened and its camera held
    /// while cells are dragged, the time controls locked as the interior designer locks them, the ground under the pointer, the
    /// mouse, the land layer (one mesh in a copy of the map's own neighbourhood zone material, on the layer the map's camera
    /// draws, coloured from a palette texture) and the ghost's tint. Nothing here runs a frame of its own: BuildMode asks while
    /// it is open.
    /// </summary>
    internal static class BuildModeGame
    {
        public static IModLogger Logger;

        private const string LayerName = "RealLife build mode cells";
        private const float LayerUp = 0.25f;     // m over the district's ground: over a block ground's grass (0.055) and the parks' paving (0.09), under any building's floor
        private const float LineWidth = 0.3f;    // m of each cell's edge drawn as its line: two cells side by side make a line 0.6 m wide, a pixel or two at the map's farthest
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private static readonly int UnlitColour = Shader.PropertyToID("_UnlitColor");
        private static readonly int UnlitColourMap = Shader.PropertyToID("_UnlitColorMap");
        private static readonly int BaseColour = Shader.PropertyToID("_BaseColor");
        private static readonly int ZTestTransparent = Shader.PropertyToID("_ZTestTransparent");          // HDRP's depth test of a transparent surface
        private static readonly int ZTestForward = Shader.PropertyToID("_ZTestDepthEqualForOpaque");      // ... which its Unlit forward pass reads
        private static readonly int CullAll = Shader.PropertyToID("_CullMode");
        private static readonly int CullModeForward = Shader.PropertyToID("_CullModeForward");
        private static readonly int TransparentCullMode = Shader.PropertyToID("_TransparentCullMode");
        private static MaterialPropertyBlock _tint;
        private static bool _timeLocked;

        /// <summary>True while the game's city map is open.</summary>
        public static bool MapOpen => CityMap.IsOpen;

        /// <summary>The game's full menu (the phone's apps) closed, so the map opens over the city.</summary>
        public static void CloseMenu()
        {
            UIs uis = InstanceBehavior<UIs>.Instance;
            if (FullMenu.IsOpen && uis != null && uis.fullMenu != null)
            {
                uis.fullMenu.Toggle(false);
            }
        }

        /// <summary>The time controls locked (or let go) while build mode is open, as the interior designer locks them; the map keeps the time paused.</summary>
        public static void LockTime(bool locked)
        {
            UIs uis = InstanceBehavior<UIs>.Instance;
            if (uis == null || uis.gameSpeed == null || _timeLocked == locked)
            {
                return;
            }
            uis.gameSpeed.DisableTimeControl(locked);
            _timeLocked = locked;
        }

        /// <summary>True while the game's time is paused.</summary>
        public static bool Paused
        {
            get
            {
                UIs uis = InstanceBehavior<UIs>.Instance;
                return uis != null && uis.gameSpeed != null && uis.gameSpeed.Paused;
            }
        }

        /// <summary>The map's camera held still (or let go) while cells are dragged: its left drag would pan the map.</summary>
        public static void HoldCamera(bool held)
        {
            CityMapCam cam = MapCam();
            if (cam != null && cam.enabled == held)
            {
                cam.enabled = !held;
            }
        }

        /// <summary>True while the map's camera takes the player's input (not held).</summary>
        public static bool CameraFree
        {
            get
            {
                CityMapCam cam = MapCam();
                return cam == null || cam.enabled;
            }
        }

        private static CityMapCam MapCam()
        {
            CityManager city = InstanceBehavior<CityManager>.Instance;
            return city != null && city.cityMap != null ? city.cityMap.cityMapCam : null;
        }

        /// <summary>The open map's camera glides over a place.</summary>
        public static void FlyTo(Vector3 place)
        {
            CityMapCam cam = MapCam();
            if (CityMap.IsOpen && cam != null)
            {
                cam.MoveCameraToTarget(place);
            }
        }

        /// <summary>The point on the district's ground under the mouse (a ray to the ground's plane, no physics); false if the camera cannot say.</summary>
        public static bool Pointed(out Vector3 point)
        {
            point = default(Vector3);
            Camera camera = GameManager.GetMainCamera();
            if (camera == null)
            {
                return false;
            }
            Ray ray = camera.ScreenPointToRay(Input.mousePosition);
            var ground = new Plane(Vector3.up, Vector3.zero);
            if (!ground.Raycast(ray, out float along))
            {
                return false;
            }
            point = ray.GetPoint(along);
            return true;
        }

        /// <summary>True while the pointer is over the game's interface (a window, a button).</summary>
        public static bool OverInterface => EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

        public static bool Pressed => Input.GetMouseButtonDown(0);
        public static bool Held => Input.GetMouseButton(0);
        public static bool Released => Input.GetMouseButtonUp(0);
        public static bool RightPressed => Input.GetMouseButtonDown(1);
        public static bool RightReleased => Input.GetMouseButtonUp(1);
        public static Vector2 Mouse => Input.mousePosition;

        /// <summary>True while a shift key is held: a drag then lets cells go instead of picking them.</summary>
        public static bool Shift => Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

        /// <summary>True on the frame the player's cancel key (Esc) is pressed with no question box of the game's up (Esc is the box's then).</summary>
        public static bool CancelPressed => !HudConfirm.isOpen && PlayerAction.Cancel.Pressed();

        /// <summary>The cancel key taken this frame, so the game's own handling (which would shut the map) does not see it; BuildMode runs before the game's key handling.</summary>
        public static void TakeCancel() => PlayerAction.Cancel.Reset();

        /// <summary>The canvas the game's help screen stands on: build mode's window, a copy of it, stands there too, over the map and its markers.</summary>
        public static Transform WindowCanvas()
        {
            HelpSystem help = InstanceBehavior<HelpSystem>.Instance;
            return help != null && help.container != null ? help.container.transform.parent : null;
        }

        // ------------------------------------------------------------------ the land layer

        /// <summary>The land layer: one mesh of every cell as an inside and an edge, their colours from a palette texture by UVs.</summary>
        internal sealed class Layer
        {
            public GameObject Object;
            public MeshRenderer Renderer;
            public Mesh Mesh;
            public Vector2[] Uvs;
            public UnityEngine.Material Material;
            public Texture2D Palette;
            public int Cells;
        }

        private const int CellVertices = 12;   // an inside of 4 corners and an edge of 8 (its outer and inner corners)
        private const int CellIndices = 30;    // the inside's 2 triangles and the edge's 8

        /// <summary>The land layer made over the grid, hidden, in a copy of the map's zone material with the ordinary depth test; null, with why, without the material.</summary>
        public static Layer MakeLayer(CellGrid grid, out string why)
        {
            // The map's neighbourhood zones lie 8.3 m under the ground (CityManager/NeighborhoodZones at y -2.5, each zone at -5.8,
            // Build 3682's level3) in an HDRP Unlit material whose depth test is Greater (_ZTestTransparent and
            // _ZTestDepthEqualForOpaque 5): drawn only where something stands in front of them, so a zone tints the district through
            // its ground. The first build mode copied that test, so its cells, laid over the ground, showed only where a building hid
            // them ("grid squares on existing buildings but not on empty spaces", the owner, 26 Oct 2026). The copy's test is LessEqual.
            why = null;
            MeshRenderer zone = ZoneRenderer();
            if (zone == null || zone.sharedMaterial == null)
            {
                why = "the city map's neighbourhood zones are not there to copy";
                return null;
            }
            int cells = grid.CellCount;
            var vertices = new Vector3[cells * CellVertices];
            var uvs = new Vector2[cells * CellVertices];
            var triangles = new int[cells * CellIndices];
            int v = 0, t = 0;
            for (int c = 0; c < grid.Columns.Count; c++)
            {
                for (int r = 0; r < grid.Columns[c].Rows; r++)
                {
                    grid.Rect(c, r, 0f, out float x1, out float z1, out float x2, out float z2);
                    float w = Math.Min(LineWidth, Math.Min(x2 - x1, z2 - z1) * 0.25f);
                    // The inside, then the edge's outer and inner corners, each clockwise seen from above.
                    Corners(vertices, v, x1 + w, z1 + w, x2 - w, z2 - w);
                    Corners(vertices, v + 4, x1, z1, x2, z2);
                    Corners(vertices, v + 8, x1 + w, z1 + w, x2 - w, z2 - w);
                    triangles[t++] = v;
                    triangles[t++] = v + 1;
                    triangles[t++] = v + 2;
                    triangles[t++] = v;
                    triangles[t++] = v + 2;
                    triangles[t++] = v + 3;
                    for (int k = 0; k < 4; k++)
                    {
                        int o = v + 4 + k, o2 = v + 4 + (k + 1) % 4, i = v + 8 + k, i2 = v + 8 + (k + 1) % 4;
                        triangles[t++] = o;
                        triangles[t++] = o2;
                        triangles[t++] = i2;
                        triangles[t++] = o;
                        triangles[t++] = i2;
                        triangles[t++] = i;
                    }
                    v += CellVertices;
                }
            }
            var mesh = new Mesh { name = LayerName, indexFormat = IndexFormat.UInt32 };
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            var palette = new Texture2D(BuildModeRules.PaletteSize, 1, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = LayerName
            };
            byte[][] colours = GameColours();
            for (int i = 0; i < BuildModeRules.PaletteSize; i++)
            {
                byte[] p = colours[i];
                palette.SetPixel(i, 0, new Color32(p[0], p[1], p[2], p[3]));
            }
            palette.Apply(false, true);
            UnityEngine.Material copied = zone.sharedMaterial;
            var material = new UnityEngine.Material(copied) { name = LayerName };
            float zTest = material.HasProperty(ZTestTransparent) ? material.GetFloat(ZTestTransparent) : -1f;
            material.SetColor(UnlitColour, Color.white);
            material.SetTexture(UnlitColourMap, palette);
            material.SetFloat(ZTestTransparent, (float)CompareFunction.LessEqual);       // over the ground, under the buildings
            material.SetFloat(ZTestForward, (float)CompareFunction.LessEqual);          // HDRP's forward pass reads this one for a transparent surface
            material.SetFloat(CullAll, (float)UnityEngine.Rendering.CullMode.Off);     // both faces: seen from any angle the map turns to
            material.SetFloat(CullModeForward, (float)UnityEngine.Rendering.CullMode.Off);
            material.SetFloat(TransparentCullMode, (float)UnityEngine.Rendering.CullMode.Off);
            var go = new GameObject(LayerName);
            go.layer = zone.gameObject.layer;   // the layer the map's camera draws its zones on
            CityManager city = InstanceBehavior<CityManager>.Instance;
            if (city != null && city.gameObject.scene.IsValid())
            {
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, city.gameObject.scene);   // it goes with the city
            }
            go.transform.position = new Vector3(0f, LayerUp, 0f);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.allowOcclusionWhenDynamic = false;
            go.SetActive(false);
            Logger?.Info("Construction: build mode's layer: " + cells + " cells, one mesh of " + vertices.Length + " corners, on layer '" + LayerMask.LayerToName(go.layer) + "' "
                         + LayerUp.ToString("0.00", Inv) + " m over the ground; the map's zone material '" + copied.name + "' ("
                         + (copied.shader != null ? copied.shader.name : "no shader") + ", queue " + copied.renderQueue + ") tests depth by " + (CompareFunction)(int)zTest
                         + ", the layer's copy by LessEqual.");
            return new Layer { Object = go, Renderer = renderer, Mesh = mesh, Uvs = uvs, Material = material, Palette = palette, Cells = cells };
        }

        private static void Corners(Vector3[] into, int at, float x1, float z1, float x2, float z2)
        {
            into[at] = new Vector3(x1, 0f, z1);
            into[at + 1] = new Vector3(x1, 0f, z2);
            into[at + 2] = new Vector3(x2, 0f, z2);
            into[at + 3] = new Vector3(x2, 0f, z1);
        }

        // The first of the map's neighbourhood zones with a renderer: its material and layer are the land layer's.
        private static MeshRenderer ZoneRenderer()
        {
            CityMapNeighborhoodZones.CityMapNeighborhoodZone[] zones = CityMapNeighborhoodZones.Zones;
            for (int i = 0; zones != null && i < zones.Length; i++)
            {
                if (zones[i] != null && zones[i].zoneRenderer != null)
                {
                    return zones[i].zoneRenderer;
                }
            }
            return null;
        }

        /// <summary>
        /// The palette with the game's own colours in it: each use its building type's colour on the city map's filters
        /// (BuildingTypeData.mapFilterColor: office is blue in the game), land for sale the map's for-sale colour and the player's
        /// land and buildings the map's own colour for the player (CityMapFilters.forSaleFilterColor, playerFilterColor), each at
        /// the layer's own alpha; the mixed zoning a colour none of those is near. Said once in Player.log.
        /// </summary>
        public static byte[][] GameColours()
        {
            var palette = new byte[BuildModeRules.PaletteSize][];
            for (int i = 0; i < palette.Length; i++)
            {
                palette[i] = (byte[])BuildModeRules.Palette[i].Clone();
            }
            var taken = new byte[6][];
            var said = new System.Text.StringBuilder(256);
            for (int u = 0; u < 4; u++)
            {
                BuildingTypeData data = BuildingTypeHelper.GetData(BuildCatalogue.UseTypes[u]);
                if (data != null && data.hasCityMapFilter)
                {
                    Put(palette[BuildModeRules.Homes + u], data.mapFilterColor);
                }
                taken[u] = palette[BuildModeRules.Homes + u];
                said.Append(u > 0 ? ", " : string.Empty).Append(BuildCatalogue.UseTypes[u]).Append(' ').Append(Hex(palette[BuildModeRules.Homes + u]))
                    .Append(data != null && data.hasCityMapFilter ? string.Empty : " (not the game's)");
            }
            UIs uis = InstanceBehavior<UIs>.Instance;
            CityMapFilters filters = uis != null ? uis.mapFilters : null;
            if (filters != null)
            {
                Put(palette[BuildModeRules.ForSale], filters.forSaleFilterColor);
                Put(palette[BuildModeRules.YourLand], filters.playerFilterColor);
                Put(palette[BuildModeRules.YourBuilding], filters.playerFilterColor);
                Put(palette[BuildModeRules.LineYours], filters.playerFilterColor);
            }
            taken[4] = palette[BuildModeRules.ForSale];
            taken[5] = palette[BuildModeRules.YourLand];
            byte[] mixed = BuildModeRules.MixedColour(taken);
            palette[BuildModeRules.Mixed][0] = mixed[0];
            palette[BuildModeRules.Mixed][1] = mixed[1];
            palette[BuildModeRules.Mixed][2] = mixed[2];
            if (!_coloursSaid)
            {
                _coloursSaid = true;
                Logger?.Info("Construction: build mode's colours from the game: " + said + "; for sale " + Hex(palette[BuildModeRules.ForSale]) + ", the player's "
                             + Hex(palette[BuildModeRules.YourLand]) + (filters != null ? string.Empty : " (the map's filters not found: the layer's own)") + "; mixed zoning "
                             + Hex(palette[BuildModeRules.Mixed]) + ", none of the game's.");
            }
            return palette;
        }

        private static bool _coloursSaid;

        // The game's colour into a palette entry, its alpha kept (the layer's own).
        private static void Put(byte[] entry, Color colour)
        {
            Color32 c = colour;
            entry[0] = c.r;
            entry[1] = c.g;
            entry[2] = c.b;
        }

        /// <summary>A palette entry as #RRGGBB.</summary>
        public static string Hex(byte[] p) => "#" + p[0].ToString("X2", Inv) + p[1].ToString("X2", Inv) + p[2].ToString("X2", Inv);

        /// <summary>A building's occupancy as the game keeps it: room to let (for rent, nobody in) and who is there, as the game writes its name; false with no registration.</summary>
        public static bool Occupant(string street, int number, out bool vacant, out string who)
        {
            vacant = false;
            who = null;
            BuildingRegistration reg = string.IsNullOrEmpty(street) ? null : BuildingHelper.GetBuildingRegistration(new Address(street, number));
            if (reg == null)
            {
                return false;
            }
            vacant = reg.AvailableForRent && !reg.RentedByPlayer && (string.IsNullOrEmpty(reg.businessTypeName) || reg.businessTypeName == EmptyBusiness);
            who = ConstructionGame.Words(reg);
            return true;
        }

        private const string EmptyBusiness = "ba:businesstype_empty";

        /// <summary>The colour of a palette entry, for the legend's swatches (the game's colours read in).</summary>
        public static Color32 Swatch(Layer layer, int entry)
        {
            if (layer != null && layer.Palette != null && entry >= 0 && entry < BuildModeRules.PaletteSize)
            {
                Color32 c = layer.Palette.GetPixel(entry, 0);
                return new Color32(c.r, c.g, c.b, (byte)Math.Max((int)c.a, 200));
            }
            return new Color32(0, 0, 0, 0);
        }

        /// <summary>Every cell's inside and edge written into the layer's UVs (the arrays kept: nothing new made).</summary>
        public static void Colour(Layer layer, byte[] fills, byte[] lines)
        {
            if (layer == null || layer.Mesh == null)
            {
                return;
            }
            for (int i = 0; i < layer.Cells && i < fills.Length && i < lines.Length; i++)
            {
                var fill = new Vector2((fills[i] + 0.5f) / BuildModeRules.PaletteSize, 0.5f);
                var line = new Vector2((lines[i] + 0.5f) / BuildModeRules.PaletteSize, 0.5f);
                int v = i * CellVertices;
                for (int k = 0; k < 4; k++)
                {
                    layer.Uvs[v + k] = fill;
                }
                for (int k = 4; k < CellVertices; k++)
                {
                    layer.Uvs[v + k] = line;
                }
            }
            layer.Mesh.uv = layer.Uvs;
        }

        public static void Show(Layer layer, bool shown)
        {
            if (layer != null && layer.Object != null && layer.Object.activeSelf != shown)
            {
                layer.Object.SetActive(shown);
            }
        }

        /// <summary>
        /// Whether the map's camera draws the layer now: its renderer on, on a layer the camera draws, not held back by a
        /// draw distance (the mod's view_radius and far_props tweaks give their own back while the map is open), within the
        /// camera's view and far plane, and its depth test the ordinary one; false with why.
        /// </summary>
        public static bool LayerSeen(Layer layer, out string why)
        {
            why = null;
            Camera camera = GameManager.GetMainCamera();
            if (layer == null || layer.Renderer == null || layer.Material == null)
            {
                why = "no layer";
                return false;
            }
            if (camera == null)
            {
                why = "no camera";
                return false;
            }
            int bit = layer.Object.layer;
            Bounds bounds = layer.Renderer.bounds;
            float[] distances = camera.layerCullDistances;
            float limit = distances != null && bit < distances.Length ? distances[bit] : 0f;
            float far = Mathf.Sqrt(bounds.SqrDistance(camera.transform.position));
            if (!layer.Object.activeInHierarchy || !layer.Renderer.enabled || layer.Renderer.forceRenderingOff)
            {
                why = "its renderer is off";
            }
            else if ((camera.cullingMask & (1 << bit)) == 0)
            {
                why = "the camera does not draw layer '" + LayerMask.LayerToName(bit) + "'";
            }
            else if (limit > 0f && far > limit)
            {
                why = "a draw distance of " + limit.ToString("0", Inv) + " m holds it back at " + far.ToString("0", Inv) + " m";
            }
            else if (far > camera.farClipPlane)
            {
                why = "past the far plane";
            }
            else if (!GeometryUtility.TestPlanesAABB(GeometryUtility.CalculateFrustumPlanes(camera), bounds))
            {
                why = "out of the camera's view";
            }
            else if ((int)layer.Material.GetFloat(ZTestTransparent) != (int)CompareFunction.LessEqual || (int)layer.Material.GetFloat(ZTestForward) != (int)CompareFunction.LessEqual)
            {
                why = "its depth test is not the ordinary one";
            }
            return why == null;
        }

        /// <summary>How far the map's camera is from the ground under it, for the test's line.</summary>
        public static float CameraHeight
        {
            get
            {
                Camera camera = GameManager.GetMainCamera();
                return camera != null ? camera.transform.position.y : 0f;
            }
        }

        public static void Destroy(Layer layer)
        {
            if (layer == null)
            {
                return;
            }
            if (layer.Object != null)
            {
                UnityEngine.Object.Destroy(layer.Object);
            }
            Mesh mesh = layer.Mesh;   // what MakeLayer made, by the names it made them under
            UnityEngine.Material material = layer.Material;
            Texture2D palette = layer.Palette;
            if (mesh != null)
            {
                UnityEngine.Object.Destroy(mesh);
            }
            if (material != null)
            {
                UnityEngine.Object.Destroy(material);
            }
            if (palette != null)
            {
                UnityEngine.Object.Destroy(palette);
            }
        }

        // ------------------------------------------------------------------ the ghost

        /// <summary>The ghost on the map's layer and tinted: green with no warning, red with one.</summary>
        public static void Tint(Transform root, Layer layer, bool ok)
        {
            if (root == null)
            {
                return;
            }
            int on = layer != null && layer.Object != null ? layer.Object.layer : root.gameObject.layer;
            _tint = _tint ?? new MaterialPropertyBlock();
            Color colour = ok ? new Color(0.55f, 1f, 0.55f, 1f) : new Color(1f, 0.45f, 0.45f, 1f);
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);   // once a ghost: one design's parts
            for (int i = 0; i < renderers.Length; i++)
            {
                renderers[i].gameObject.layer = on;
                renderers[i].GetPropertyBlock(_tint);
                _tint.SetColor(BaseColour, colour);
                renderers[i].SetPropertyBlock(_tint);
                renderers[i].shadowCastingMode = ShadowCastingMode.Off;
            }
            Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                colliders[i].enabled = false;   // nothing of the ghost is solid or clicked
            }
        }
    }
}
