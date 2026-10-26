using System;
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
        private const string LayerName = "RealLife build mode cells";
        private const float LayerUp = 0.12f;   // m over the district's ground: above the block grounds and kerbs the map draws, under any building
        private static readonly int UnlitColour = Shader.PropertyToID("_UnlitColor");
        private static readonly int UnlitColourMap = Shader.PropertyToID("_UnlitColorMap");
        private static readonly int BaseColour = Shader.PropertyToID("_BaseColor");
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
        public static Vector2 Mouse => Input.mousePosition;

        // ------------------------------------------------------------------ the land layer

        /// <summary>The land layer: one mesh of every cell as a square, its colours from a palette texture by each cell's UVs.</summary>
        internal sealed class Layer
        {
            public GameObject Object;
            public Mesh Mesh;
            public Vector2[] Uvs;
            public UnityEngine.Material Material;
            public Texture2D Palette;
            public int Cells;
        }

        /// <summary>The land layer made over the grid, hidden; null, with why, when the map's own zone material is not there to copy.</summary>
        public static Layer MakeLayer(CellGrid grid, out string why)
        {
            why = null;
            MeshRenderer zone = ZoneRenderer();
            if (zone == null || zone.sharedMaterial == null)
            {
                why = "the city map's neighbourhood zones are not there to copy";
                return null;
            }
            int cells = grid.CellCount;
            var vertices = new Vector3[cells * 4];
            var uvs = new Vector2[cells * 4];
            var triangles = new int[cells * 6];
            int v = 0, t = 0;
            for (int c = 0; c < grid.Columns.Count; c++)
            {
                for (int r = 0; r < grid.Columns[c].Rows; r++)
                {
                    grid.Rect(c, r, CellRules.CellGutter, out float x1, out float z1, out float x2, out float z2);
                    vertices[v] = new Vector3(x1, LayerUp, z1);
                    vertices[v + 1] = new Vector3(x1, LayerUp, z2);
                    vertices[v + 2] = new Vector3(x2, LayerUp, z2);
                    vertices[v + 3] = new Vector3(x2, LayerUp, z1);
                    triangles[t] = v;
                    triangles[t + 1] = v + 1;
                    triangles[t + 2] = v + 2;
                    triangles[t + 3] = v;
                    triangles[t + 4] = v + 2;
                    triangles[t + 5] = v + 3;
                    v += 4;
                    t += 6;
                }
            }
            var mesh = new Mesh { name = LayerName, indexFormat = IndexFormat.UInt32 };
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            var palette = new Texture2D(BuildModeRules.PaletteSize, 1, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = LayerName };
            for (int i = 0; i < BuildModeRules.PaletteSize; i++)
            {
                byte[] p = BuildModeRules.Palette[i];
                palette.SetPixel(i, 0, new Color32(p[0], p[1], p[2], p[3]));
            }
            palette.Apply(false, true);
            var material = new UnityEngine.Material(zone.sharedMaterial) { name = LayerName };
            material.SetColor(UnlitColour, Color.white);
            material.SetTexture(UnlitColourMap, palette);
            var go = new GameObject(LayerName);
            go.layer = zone.gameObject.layer;   // the layer the map's camera draws its zones on
            CityManager city = InstanceBehavior<CityManager>.Instance;
            if (city != null && city.gameObject.scene.IsValid())
            {
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, city.gameObject.scene);   // it goes with the city
            }
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            go.SetActive(false);
            return new Layer { Object = go, Mesh = mesh, Uvs = uvs, Material = material, Palette = palette, Cells = cells };
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

        /// <summary>Every cell's palette entry written into the layer's UVs (the arrays kept: nothing new made).</summary>
        public static void Colour(Layer layer, byte[] entries)
        {
            if (layer == null || layer.Mesh == null)
            {
                return;
            }
            for (int i = 0; i < layer.Cells && i < entries.Length; i++)
            {
                var uv = new Vector2((entries[i] + 0.5f) / BuildModeRules.PaletteSize, 0.5f);
                int v = i * 4;
                layer.Uvs[v] = uv;
                layer.Uvs[v + 1] = uv;
                layer.Uvs[v + 2] = uv;
                layer.Uvs[v + 3] = uv;
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
