using System;
using System.Collections.Generic;
using System.Globalization;
using BigAmbitions.Rivals;
using Buildings;
using Helpers;
using Localizor;
using UI;
using UnityEngine;
using UnityEngine.Rendering;

namespace RealLifeOverhaul.Construction
{
    /// <summary>
    /// Build mode over the city's own districts (the owner, 26 Oct 2026: "make it grids in all district"), where it touches the
    /// game: every one of the city's buildings outside Uptown read once at each opening as a plot (its address, type, size, floor
    /// area, the game's price a square metre, who holds it, from the game's own registration and lists, its outline from the box
    /// the map draws it as), the plot layer drawn as one mesh a square of the city on the land layer's material so the map's camera
    /// culls what it does not see, the building under the pointer as the game's own map finds it (MouseController), the game's
    /// building card on the map put away when build mode takes the click, BizMan opened on a building's page (the game's own buy and
    /// rent), and the pointer: a crosshair made once in code (the game's cursors have none), the game's own put back.
    /// COST: nothing while closed (everything here is made at an opening and destroyed at leaving); while open, nothing a frame of its own.
    /// </summary>
    internal static partial class BuildModeGame
    {
        private const string PlotsName = "RealLife build mode plots";
        private const float RoofUp = 0.3f;        // m over the top of a building's box: the plot is drawn on its roof, which the map shows from above
        private const string EmptyType = "ba:businesstype_empty";
        private const string ModStreet = "reallife:";   // the mod's own districts' streets: Uptown's buildings are its squares, not plots

        /// <summary>The plots of one opening: each of the game's buildings outside Uptown, its facts and its outline, and the meshes that draw them.</summary>
        internal sealed class Plots
        {
            public int Count;
            public CityBuildingController[] Buildings;
            public string[] Street;
            public int[] Number;
            public string[] Type;       // the game's building type (ba:buildingtype_...)
            public string[] Size;       // the game's building size (ba:buildingsize_...)
            public int[] Sqm;
            public float[] PricePerSqm; // the game's market price a square metre now
            public float[] X, Z;        // four corners a plot, round it
            public float[] Roof;        // the height its outline is drawn at
            public PlotFacts[] Facts;
            public readonly Dictionary<EntityController, int> Of = new Dictionary<EntityController, int>(4096);   // a building and its parts: its plot
            public readonly List<PlotTile> Tiles = new List<PlotTile>(64);
            public UnityEngine.Material Material;
            public int Layer;
            public double Milliseconds;  // the reading and the meshes, for the log line and the test
        }

        /// <summary>One square of the city's plot layer: its own mesh, so the map's camera culls it as the game's map culls its buildings.</summary>
        internal sealed class PlotTile
        {
            public GameObject Object;
            public Mesh Mesh;
            public Vector2[] Uvs;
            public int[] Plots;
        }

        private static readonly float[] BoxX = new float[4], BoxZ = new float[4];

        /// <summary>
        /// Every building of the city outside Uptown read as a plot (CityManager's list through GameDlc.Open, so nothing a DLC keeps):
        /// its address, type, size, floor area, price a square metre and what it is to the player, its outline and its roof's height.
        /// <paramref name="uptown"/> holds Uptown's lot addresses (street + '#' + number), left to its squares. Null with why when the
        /// city has no list. Runs once at an opening.
        /// </summary>
        public static Plots ReadPlots(HashSet<string> uptown, out string why)
        {
            why = null;
            CityManager city = InstanceBehavior<CityManager>.Instance;
            CityBuildingController[] all = GameDlc.Open(city);
            if (all == null || all.Length == 0)
            {
                why = "the city has no list of its buildings";
                return null;
            }
            var sale = new HashSet<string>(StringComparer.Ordinal);
            List<BuildingForSale> listed = SaveGameManager.Current != null ? SaveGameManager.Current.buildingsForSale : null;
            for (int i = 0; listed != null && i < listed.Count; i++)
            {
                Address a = listed[i] != null ? listed[i].address : null;
                if (a != null)
                {
                    sale.Add(a.streetName + "#" + a.streetNumber.ToString(CultureInfo.InvariantCulture));
                }
            }
            int n = all.Length;
            var p = new Plots
            {
                Buildings = new CityBuildingController[n], Street = new string[n], Number = new int[n], Type = new string[n], Size = new string[n], Sqm = new int[n],
                PricePerSqm = new float[n], X = new float[n * 4], Z = new float[n * 4], Roof = new float[n], Facts = new PlotFacts[n]
            };
            int failed = 0;
            for (int i = 0; i < all.Length; i++)
            {
                CityBuildingController c = all[i];
                Building b = c != null ? c.building : null;
                if (b == null || string.IsNullOrEmpty(b.StreetName) || b.StreetName.StartsWith(ModStreet, StringComparison.Ordinal))
                {
                    continue;
                }
                string key = b.StreetName + "#" + b.StreetNumber.ToString(CultureInfo.InvariantCulture);
                if (uptown != null && uptown.Contains(key))
                {
                    continue;
                }
                try
                {
                    if (!Ground(c, out float roof))
                    {
                        continue;
                    }
                    int at = p.Count;
                    for (int k = 0; k < 4; k++)
                    {
                        p.X[at * 4 + k] = BoxX[k];
                        p.Z[at * 4 + k] = BoxZ[k];
                    }
                    p.Roof[at] = roof + RoofUp;
                    p.Buildings[at] = c;
                    p.Street[at] = b.StreetName;
                    p.Number[at] = b.StreetNumber;
                    p.Type[at] = b.BuildingType;
                    p.Size[at] = b.BuildingSize;
                    p.Sqm[at] = b.totalSqm;
                    p.PricePerSqm[at] = Price(b);
                    p.Facts[at] = FactsOf(c, b, sale.Contains(key));
                    p.Of[c] = at;
                    for (int k = 0; c.cityBuildingParts != null && k < c.cityBuildingParts.Count; k++)
                    {
                        if (c.cityBuildingParts[k] != null)
                        {
                            p.Of[c.cityBuildingParts[k]] = at;
                        }
                    }
                    p.Count++;
                }
                catch (Exception e)
                {
                    if (failed++ == 0)
                    {
                        Logger?.Warn("Construction: build mode could not read the building at " + key + " as a plot (" + e.GetType().Name + ": " + e.Message + "); it is left out.");
                    }
                }
            }
            if (failed > 1)
            {
                Logger?.Warn("Construction: build mode left out " + failed + " of the city's buildings it could not read.");
            }
            return p;
        }

        private static float Price(Building b)
        {
            try
            {
                return b.GetMarketPricePerSqm();
            }
            catch (Exception)
            {
                return 0f;   // a building type or district the game has no figures for: the lowest step
            }
        }

        // What a building is to the player, from the game's registration the controller keeps.
        private static PlotFacts FactsOf(CityBuildingController c, Building b, bool forSale)
        {
            var f = new PlotFacts { ForSale = forSale, Use = BuildCatalogue.UseOf(b.BuildingType) };
            BuildingRegistration reg = c.buildingRegistration;
            f.Kept = b.SpecialService != null;
            if (reg == null)
            {
                f.Kept = true;   // nothing the game lets or sells here
                return f;
            }
            f.Owned = reg.BuildingOwnedByPlayer;
            f.Rented = reg.RentedByPlayer;
            f.Foreign = GoingPublic.Foreign(reg);
            f.Rival = !string.IsNullOrEmpty(reg.buildingOwnerRivalId);
            f.ToLet = reg.AvailableForRent && !reg.RentedByPlayer && (string.IsNullOrEmpty(reg.businessTypeName) || reg.businessTypeName == EmptyType);
            f.Kept |= SpecialBuildings.TakenBy(reg) != null;   // the mod's own places, kept off the market in the interface
            return f;
        }

        // A building's outline (into BoxX, BoxZ) and the top of its box: the box the map draws it as (its LOD box), its own mesh's
        // box where it is not batched, else its upright box turned back by the building's own turn.
        private static bool Ground(CityBuildingController c, out float roof)
        {
            roof = 0f;
            MeshRenderer box = c.lodVersion;
            MeshFilter filter = box != null && !box.isPartOfStaticBatch ? box.GetComponent<MeshFilter>() : null;
            Mesh mesh = filter != null ? filter.sharedMesh : null;
            if (mesh != null && mesh.bounds.size.sqrMagnitude > 0.01f)
            {
                Bounds local = mesh.bounds;
                Matrix4x4 toWorld = box.transform.localToWorldMatrix;
                Vector3 low = local.min, high = local.max;
                Vector3 a = toWorld.MultiplyPoint3x4(new Vector3(low.x, low.y, low.z)), b = toWorld.MultiplyPoint3x4(new Vector3(high.x, low.y, low.z));
                Vector3 d = toWorld.MultiplyPoint3x4(new Vector3(high.x, low.y, high.z)), e = toWorld.MultiplyPoint3x4(new Vector3(low.x, low.y, high.z));
                float w = Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z)), h = Vector2.Distance(new Vector2(a.x, a.z), new Vector2(e.x, e.z));
                if (w >= BuildModeCityRules.NarrowestPlot && h >= BuildModeCityRules.NarrowestPlot && w <= BuildModeCityRules.WidestPlot && h <= BuildModeCityRules.WidestPlot)
                {
                    BoxX[0] = a.x; BoxZ[0] = a.z;
                    BoxX[1] = b.x; BoxZ[1] = b.z;
                    BoxX[2] = d.x; BoxZ[2] = d.z;
                    BoxX[3] = e.x; BoxZ[3] = e.z;
                    roof = Mathf.Max(toWorld.MultiplyPoint3x4(high).y, toWorld.MultiplyPoint3x4(low).y);
                    return true;
                }
            }
            bool any = false;
            Bounds whole = default;
            if (box != null && box.bounds.size.sqrMagnitude > 0.01f)
            {
                whole = box.bounds;
                any = true;
            }
            else
            {
                Renderer[] parts = c.Renderers;
                for (int i = 0; parts != null && i < parts.Length; i++)
                {
                    if (parts[i] == null || parts[i].bounds.size.sqrMagnitude < 0.01f)
                    {
                        continue;
                    }
                    if (!any)
                    {
                        whole = parts[i].bounds;
                        any = true;
                    }
                    else
                    {
                        whole.Encapsulate(parts[i].bounds);
                    }
                }
            }
            if (!any)
            {
                return false;
            }
            roof = whole.max.y;
            return BuildModeCityRules.Outline(whole.min.x, whole.min.z, whole.max.x, whole.max.z, c.transform.eulerAngles.y, BoxX, BoxZ);
        }

        /// <summary>
        /// The plot layer's meshes made over the plots read, one a square of the city (BuildModeCityRules.TileSize), each in the land
        /// layer's material on its layer, hidden; false (nothing made) without the land layer. Each plot is an inside and an edge, as a square is.
        /// </summary>
        public static bool MakePlots(Plots p, Layer layer)
        {
            if (p == null || layer == null || layer.Material == null || layer.Object == null)
            {
                return false;
            }
            p.Material = layer.Material;
            p.Layer = layer.Object.layer;
            var byTile = new Dictionary<int, List<int>>(128);
            for (int i = 0; i < p.Count; i++)
            {
                float cx = (p.X[i * 4] + p.X[i * 4 + 2]) * 0.5f, cz = (p.Z[i * 4] + p.Z[i * 4 + 2]) * 0.5f;
                int tile = BuildModeCityRules.Tile(cx, cz);
                if (!byTile.TryGetValue(tile, out List<int> list))
                {
                    list = new List<int>(32);
                    byTile[tile] = list;
                }
                list.Add(i);
            }
            CityManager city = InstanceBehavior<CityManager>.Instance;
            foreach (KeyValuePair<int, List<int>> pair in byTile)
            {
                List<int> plots = pair.Value;
                int cells = plots.Count;
                var vertices = new Vector3[cells * CellVertices];
                var uvs = new Vector2[cells * CellVertices];
                var triangles = new int[cells * CellIndices];
                for (int k = 0; k < cells; k++)
                {
                    PlotQuad(p, plots[k], vertices, triangles, k);
                }
                var mesh = new Mesh { name = PlotsName, indexFormat = vertices.Length > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
                mesh.vertices = vertices;
                mesh.uv = uvs;
                mesh.triangles = triangles;
                mesh.RecalculateBounds();
                var go = new GameObject(PlotsName) { layer = p.Layer };
                if (city != null && city.gameObject.scene.IsValid())
                {
                    UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, city.gameObject.scene);
                }
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = p.Material;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.allowOcclusionWhenDynamic = false;
                go.SetActive(false);
                p.Tiles.Add(new PlotTile { Object = go, Mesh = mesh, Uvs = uvs, Plots = plots.ToArray() });
            }
            return true;
        }

        // One plot's inside and edge, at its roof, the edge's inner corners each moved in along both its sides.
        private static void PlotQuad(Plots p, int plot, Vector3[] vertices, int[] triangles, int k)
        {
            int v = k * CellVertices, t = k * CellIndices, at = plot * 4;
            float y = p.Roof[plot];
            float ux = p.X[at + 1] - p.X[at], uz = p.Z[at + 1] - p.Z[at], wx = p.X[at + 3] - p.X[at], wz = p.Z[at + 3] - p.Z[at];
            float ul = Mathf.Sqrt(ux * ux + uz * uz), wl = Mathf.Sqrt(wx * wx + wz * wz);
            float edge = Mathf.Min(BuildModeCityRules.EdgeWidth, Mathf.Min(ul, wl) * 0.25f);
            float iux = ul > 0f ? ux / ul * edge : 0f, iuz = ul > 0f ? uz / ul * edge : 0f, iwx = wl > 0f ? wx / wl * edge : 0f, iwz = wl > 0f ? wz / wl * edge : 0f;
            for (int c = 0; c < 4; c++)
            {
                float su = c == 1 || c == 2 ? -1f : 1f, sw = c >= 2 ? -1f : 1f;   // inward along each side
                var outer = new Vector3(p.X[at + c], y, p.Z[at + c]);
                var inner = new Vector3(p.X[at + c] + su * iux + sw * iwx, y, p.Z[at + c] + su * iuz + sw * iwz);
                vertices[v + c] = inner;
                vertices[v + 4 + c] = outer;
                vertices[v + 8 + c] = inner;
            }
            triangles[t++] = v;
            triangles[t++] = v + 1;
            triangles[t++] = v + 2;
            triangles[t++] = v;
            triangles[t++] = v + 2;
            triangles[t++] = v + 3;
            for (int c = 0; c < 4; c++)
            {
                int o = v + 4 + c, o2 = v + 4 + (c + 1) % 4, i = v + 8 + c, i2 = v + 8 + (c + 1) % 4;
                triangles[t++] = o;
                triangles[t++] = o2;
                triangles[t++] = i2;
                triangles[t++] = o;
                triangles[t++] = i2;
                triangles[t++] = i;
            }
        }

        /// <summary>Every plot's inside and edge written into its square's UVs (each square's array kept), from each plot's palette entries.</summary>
        public static void ColourPlots(Plots p, byte[] fills, byte[] lines)
        {
            if (p == null)
            {
                return;
            }
            for (int t = 0; t < p.Tiles.Count; t++)
            {
                PlotTile tile = p.Tiles[t];
                if (tile.Mesh == null)
                {
                    continue;
                }
                for (int k = 0; k < tile.Plots.Length; k++)
                {
                    int i = tile.Plots[k];
                    var fill = new Vector2((fills[i] + 0.5f) / BuildModeRules.PaletteSize, 0.5f);
                    var line = new Vector2((lines[i] + 0.5f) / BuildModeRules.PaletteSize, 0.5f);
                    int v = k * CellVertices;
                    for (int c = 0; c < 4; c++)
                    {
                        tile.Uvs[v + c] = fill;
                    }
                    for (int c = 4; c < CellVertices; c++)
                    {
                        tile.Uvs[v + c] = line;
                    }
                }
                tile.Mesh.uv = tile.Uvs;
            }
        }

        public static void ShowPlots(Plots p, bool shown)
        {
            for (int t = 0; p != null && t < p.Tiles.Count; t++)
            {
                GameObject go = p.Tiles[t].Object;
                if (go != null && go.activeSelf != shown)
                {
                    go.SetActive(shown);
                }
            }
        }

        /// <summary>How many of the plot layer's squares are drawn now and how many the map's camera sees, for the test's line.</summary>
        public static int PlotTilesSeen(Plots p, out int shown)
        {
            shown = 0;
            int seen = 0;
            for (int t = 0; p != null && t < p.Tiles.Count; t++)
            {
                GameObject go = p.Tiles[t].Object;
                Renderer r = go != null ? go.GetComponent<Renderer>() : null;
                if (r != null && go.activeInHierarchy)
                {
                    shown++;
                    seen += r.isVisible ? 1 : 0;
                }
            }
            return seen;
        }

        /// <summary>The plot layer's meshes destroyed (the material is the land layer's, kept).</summary>
        public static void DestroyPlots(Plots p)
        {
            for (int t = 0; p != null && t < p.Tiles.Count; t++)
            {
                if (p.Tiles[t].Object != null)
                {
                    UnityEngine.Object.Destroy(p.Tiles[t].Object);
                }
                if (p.Tiles[t].Mesh != null)
                {
                    UnityEngine.Object.Destroy(p.Tiles[t].Mesh);
                }
            }
            p?.Tiles.Clear();
            p?.Of.Clear();
        }

        /// <summary>The plot under the pointer as the game's own map finds the building there (MouseController's target), or -1.</summary>
        public static int PlotPointed(Plots p)
        {
            EntityController target = MouseController.currentTargetEntity;
            return p != null && target != null && p.Of.TryGetValue(target, out int plot) ? plot : -1;
        }

        /// <summary>The point of the ground under the middle of the map's view; false if the camera cannot say.</summary>
        public static bool Looked(out Vector3 point)
        {
            point = default(Vector3);
            Camera camera = GameManager.GetMainCamera();
            if (camera == null)
            {
                return false;
            }
            Ray ray = camera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            var ground = new Plane(Vector3.up, Vector3.zero);
            if (!ground.Raycast(ray, out float along))
            {
                return false;
            }
            point = ray.GetPoint(along);
            return true;
        }

        /// <summary>The game's building card on the map put away when it shows this plot's building: build mode's tool panel took the click.</summary>
        public static void PutCardAway(Plots p, int plot)
        {
            UIs uis = InstanceBehavior<UIs>.Instance;
            if (p == null || plot < 0 || plot >= p.Count || uis == null || uis.buildingResume == null)
            {
                return;
            }
            if (uis.buildingResume.CityBuildingController == p.Buildings[plot])
            {
                uis.buildingResume.Close();
            }
        }

        /// <summary>
        /// The game's BizMan opened on a building's own page, as the map's building card does (its buy offer and its rent are
        /// there, with the game's checks). False if it could not be.
        /// </summary>
        public static bool OpenBizMan(string street, int number)
        {
            UIs uis = InstanceBehavior<UIs>.Instance;
            if (string.IsNullOrEmpty(street) || uis == null || uis.fullMenu == null || uis.fullMenu.bizMan == null)
            {
                return false;
            }
            uis.fullMenu.bizMan.Open(new Address(street, number));
            return true;
        }

        /// <summary>The game's words for a building type, a size and an owner.</summary>
        public static string TypeWords(string type) => string.IsNullOrEmpty(type) ? "-" : type.GetLocalization();

        public static string RivalName(string street, int number, Plots p, int plot)
        {
            BuildingRegistration reg = p != null && plot >= 0 && plot < p.Count && p.Buildings[plot] != null ? p.Buildings[plot].buildingRegistration : null;
            if (reg == null || string.IsNullOrEmpty(reg.buildingOwnerRivalId))
            {
                return null;
            }
            try
            {
                return reg.buildingOwnerRivalId.GetRivalName();
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>The name of the business trading in a plot's building, as the game writes it, or null.</summary>
        public static string BusinessName(Plots p, int plot)
        {
            BuildingRegistration reg = p != null && plot >= 0 && plot < p.Count && p.Buildings[plot] != null ? p.Buildings[plot].buildingRegistration : null;
            return reg != null && !string.IsNullOrEmpty(reg.BusinessName) ? reg.BusinessName : null;
        }

        /// <summary>A plot said in the game's own tooltip beside the pointer, as a square is (ShowHover): a header and up to four lines.</summary>
        public static void ShowPlotHover(string head, string line1, string line2, string line3, string line4)
        {
            ShowHover(head, line1, line2);
            Color soft = new Color(0.79f, 0.82f, 0.84f, 1f);
            if (!string.IsNullOrEmpty(line3))
            {
                Tooltip.TooltipSystem.AddLabel(line3, soft);
            }
            if (!string.IsNullOrEmpty(line4))
            {
                Tooltip.TooltipSystem.AddLabel(line4, soft);
            }
        }

        /// <summary>A plot's building's market rent a day as the game prices it (its rent flow's own figure), or 0.</summary>
        public static long DailyRent(Plots p, int plot)
        {
            Building b = p != null && plot >= 0 && plot < p.Count && p.Buildings[plot] != null ? p.Buildings[plot].building : null;
            try
            {
                return b != null ? b.GetBuildingDailyMarketRent() : 0L;
            }
            catch (Exception)
            {
                return 0L;
            }
        }

        /// <summary>True when the game's question box is up.</summary>
        public static bool QuestionUp => HudConfirm.isOpen;

        // ------------------------------------------------------------------ the pointer

        private static Texture2D _cross;
        private static readonly Vector2 CrossSpot = new Vector2(15f, 15f);
        private static BuildCursor _cursor = BuildCursor.Normal;

        /// <summary>The pointer as build mode wants it: a crosshair (made once, kept) or the game's own default cursor put back. Set only when it changes.</summary>
        public static void Pointer(BuildCursor wanted)
        {
            if (wanted == _cursor)
            {
                return;
            }
            _cursor = wanted;
            if (wanted == BuildCursor.Cross)
            {
                Cursor.SetCursor(Cross(), CrossSpot, CursorMode.Auto);
                return;
            }
            GameCursor(out Texture2D texture, out Vector2 spot);
            Cursor.SetCursor(texture, spot, CursorMode.Auto);
        }

        /// <summary>The pointer build mode set now, for the tests.</summary>
        public static BuildCursor PointerNow => _cursor;

        // The game's default cursor (GameManager.mouseSettings, the one MouseController sets); none: the system's.
        private static void GameCursor(out Texture2D texture, out Vector2 spot)
        {
            texture = null;
            spot = Vector2.zero;
            GameManager game = InstanceBehavior<GameManager>.Instance;
            List<CursorData> cursors = game != null && game.mouseSettings != null ? game.mouseSettings.cursors : null;
            for (int i = 0; cursors != null && i < cursors.Count; i++)
            {
                if (cursors[i] != null && cursors[i].type == CursorType.Default)
                {
                    texture = cursors[i].cursorTexture;
                    spot = cursors[i].hotspot;
                    return;
                }
            }
        }

        // A "+" of 32 pixels: white arms two pixels wide, a dark rim round them, a gap at the middle; made once and kept (readable, as the cursor needs).
        private static Texture2D Cross()
        {
            if (_cross != null)
            {
                return _cross;
            }
            const int size = 32, mid = 15;
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "RealLife build mode cursor", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    pixels[y * size + x] = new Color32(0, 0, 0, 0);
                }
            }
            for (int pass = 0; pass < 2; pass++)
            {
                int grow = pass == 0 ? 1 : 0;
                var colour = pass == 0 ? new Color32(20, 20, 24, 230) : new Color32(255, 255, 255, 255);
                for (int a = 2 - grow; a <= 29 + grow; a++)
                {
                    if (a >= 13 + grow && a <= 17 - grow)
                    {
                        continue;   // the gap at the middle, so the point under it shows
                    }
                    for (int w = mid - grow; w <= mid + 1 + grow; w++)
                    {
                        pixels[w * size + a] = colour;   // across
                        pixels[a * size + w] = colour;   // up and down
                    }
                }
            }
            t.SetPixels32(pixels);
            t.Apply(false, false);
            _cross = t;
            return t;
        }
    }
}
