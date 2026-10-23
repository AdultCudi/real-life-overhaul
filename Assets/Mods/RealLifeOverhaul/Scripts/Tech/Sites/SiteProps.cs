using System.Collections.Generic;
using Extensions;
using TMPro;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;

namespace RealLifeOverhaul.Tech
{
    /// <summary>The models of the tech sector's buildings from the prop bundle, or a box of the same size until it loads.</summary>
    internal static class SiteProps
    {
        public const float AiForward = 0.065f;      // metres dc_rack_ai's front stands out past a rack's, and its lights with it

        /// <summary>A model: its id, its size (x across, y up, z deep, metres), its box's colour, and whether it hangs on a wall by its back.</summary>
        public struct Spec
        {
            public string Id;
            public Vector3 Size;
            public Color Colour;
            public bool Wall;

            public Spec(string id, float x, float y, float z, Color colour, bool wall = false)
            {
                Id = id;
                Size = new Vector3(x, y, z);
                Colour = colour;
                Wall = wall;
            }
        }

        private static readonly Color Black = new Color(0.05f, 0.05f, 0.06f), White = new Color(0.86f, 0.87f, 0.88f), Grey = new Color(0.52f, 0.54f, 0.57f);
        private static readonly Color Wood = new Color(0.55f, 0.40f, 0.25f), Yellow = new Color(0.90f, 0.70f, 0.08f);

        public static readonly Spec Rack = new Spec("dc_rack", 0.60f, 2.00f, ServerRoomPlan.RackDepth, Black);
        public static readonly Spec AiRack = new Spec("dc_rack_ai", 0.60f, 2.00f, ServerRoomPlan.AiDepth, Black);
        public static readonly Spec NetworkRack = new Spec("dc_rack_network", 0.60f, 2.00f, ServerRoomPlan.RackDepth, Black);
        public static readonly Spec Cooling = new Spec("dc_cooling", ServerRoomPlan.CoolingWidth, 1.96f, ServerRoomPlan.CoolingDepth, White);
        public static readonly Spec Ups = new Spec("dc_ups", ServerRoomPlan.UpsWidth, 1.90f, ServerRoomPlan.UpsDepth, Grey);
        public static readonly Spec Tray = new Spec("dc_tray", ServerRoomPlan.TrayWidth, 0.08f, ServerRoomPlan.TrayMost, Grey);
        public static readonly Spec Floor = new Spec("dc_floor", ServerRoomPlan.Panel, 0.02f, ServerRoomPlan.Panel, new Color(0.62f, 0.63f, 0.64f));
        public static readonly Spec Vent = new Spec("dc_floor_vent", ServerRoomPlan.VentWidth, 0.024f, ServerRoomPlan.Panel, new Color(0.50f, 0.51f, 0.53f));
        public static readonly Spec OpsDesk = new Spec("dc_ops_desk", ServerRoomPlan.DeskWidth, 0.75f, ServerRoomPlan.DeskDepth, White);
        public static readonly Spec Reception = new Spec("hq_reception", LobbyPlan.DeskWidth, 1.10f, LobbyPlan.DeskDepth, White);
        public static readonly Spec LogoWall = new Spec("hq_logo_wall", LobbyPlan.LogoWidth, 2.60f, LobbyPlan.LogoDepth, Wood);
        public static readonly Spec Screen = new Spec("hq_screen", 1.60f, 0.95f, 0.06f, Black, true);
        public static readonly Spec ClosetRack = new Spec("tech_rack_small", 0.60f, 1.20f, 0.80f, Black);
        public static readonly Spec Bench = new Spec("tech_bench", 1.40f, 0.75f, 0.70f, Wood);
        public static readonly Spec Whiteboard = new Spec("tech_whiteboard", 1.80f, 1.20f, 0.05f, White, true);
        public static readonly Spec Sign = new Spec("dc_sign", 2.40f, 1.40f, 0.40f, Black);
        public static readonly Spec Booth = new Spec("dc_booth", 1.84f, 2.42f, 1.84f, White);
        public static readonly Spec Barrier = new Spec("dc_barrier", 0.40f, 1.05f, 0.40f, Yellow);
        public static readonly Spec Fence = new Spec("dc_fence", 2.50f, 2.20f, 0.08f, Black);

        // Where the lettering goes, from a model's pivot (props_tech.py): the logo wall's plate, the reception desk's band, the sign's glass.
        public static readonly Vector3 LogoPlate = new Vector3(0f, 1.60f, 0.080f);
        public static readonly Vector2 LogoPlateSize = new Vector2(2.40f, 1.00f);
        public static readonly Vector3 ReceptionBand = new Vector3(0f, 0.30f, 0.452f);
        public static readonly Vector3 SignGlass = new Vector3(0f, 0.95f, 0.126f);

        /// <summary>The model of a piece of a plan; null for one the game's own furniture stands for.</summary>
        public static bool Of(SitePieceKind kind, out Spec spec)
        {
            switch (kind)
            {
                case SitePieceKind.Rack: spec = Rack; return true;
                case SitePieceKind.AiRack: spec = AiRack; return true;
                case SitePieceKind.NetworkRack: spec = NetworkRack; return true;
                case SitePieceKind.Cooling: spec = Cooling; return true;
                case SitePieceKind.Ups: spec = Ups; return true;
                case SitePieceKind.Tray: spec = Tray; return true;
                case SitePieceKind.Floor: spec = Floor; return true;
                case SitePieceKind.Vent: spec = Vent; return true;
                case SitePieceKind.OpsDesk: spec = OpsDesk; return true;
                case SitePieceKind.Reception: spec = Reception; return true;
                case SitePieceKind.LogoWall: spec = LogoWall; return true;
                case SitePieceKind.Screen: spec = Screen; return true;
                case SitePieceKind.ClosetRack: spec = ClosetRack; return true;
                case SitePieceKind.TechBench: spec = Bench; return true;
                case SitePieceKind.Whiteboard: spec = Whiteboard; return true;
                default: spec = default; return false;
            }
        }

        /// <summary>A model under a parent at its origin: the bundle's, or a box of its size; <c>modelled</c> says which.</summary>
        public static Transform Make(Spec spec, Transform parent, out bool modelled)
        {
            var holder = new GameObject("RealLifeOverhaul " + spec.Id).transform;
            holder.SetParent(parent, false);
            modelled = Shapes.Prop(spec.Id, holder);
            if (!modelled)
            {
                Material paint = Shapes.Paint(spec.Colour);
                if (paint != null)
                {
                    Vector3 middle = spec.Wall ? new Vector3(0f, 0f, spec.Size.z * 0.5f) : new Vector3(0f, spec.Size.y * 0.5f, 0f);
                    Shapes.Piece("Stand-in", holder, Shapes.Cube, paint, middle, spec.Size);
                }
            }
            return holder;
        }

        /// <summary>A flat panel of one colour on a model (the company's on its logo wall and desk), facing +Z.</summary>
        public static void Panel(Transform parent, Color colour, Vector3 at, Vector3 size)
        {
            Material paint = Shapes.Paint(colour);
            if (paint != null)
            {
                Shapes.Piece("Panel", parent, Shapes.Cube, paint, at, size);
            }
        }

        /// <summary>Lettering in the game's font, read from +Z, as large as fits the box, on one line or two.</summary>
        public static void Letter(Transform parent, string text, Vector3 at, Vector2 box, Color colour, float most = 3f)
        {
            TMP_FontAsset font = GameUi.GameFont.Asset;
            if (font == null || string.IsNullOrEmpty(text))
            {
                return;
            }
            var label = new GameObject("Lettering");
            Transform place = label.transform;
            place.SetParent(parent, false);
            place.localPosition = at;
            place.localRotation = Quaternion.Euler(0f, 180f, 0f);
            var lettering = label.AddComponent<TextMeshPro>();
            lettering.font = font;
            lettering.text = "<noparse>" + text + "</noparse>";
            lettering.color = colour;
            lettering.fontStyle = FontStyles.Bold;
            lettering.alignment = TextAlignmentOptions.Center;
#pragma warning disable CS0618
            lettering.enableWordWrapping = true;
#pragma warning restore CS0618
            lettering.rectTransform.sizeDelta = box;
            lettering.enableAutoSizing = true;
            lettering.fontSizeMin = 0.05f;
            lettering.fontSizeMax = most;
            var renderer = label.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
        }

        /// <summary>Makes a piece solid on foot: a box cut out of the navigation mesh while it is on. The city's own buildings only.</summary>
        public static void Solid(Transform holder, Vector3 centre, Vector3 size)
        {
            var cut = holder.gameObject.AddComponent<NavMeshObstacle>();
            cut.shape = NavMeshObstacleShape.Box;
            cut.center = centre;
            cut.size = size;
            cut.carving = true;
            cut.carveOnlyStationary = true;
        }

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissiveColorId = Shader.PropertyToID("_EmissiveColor");
        private static Shader _lit;

        /// <summary>A material of the caller's own from the game's lit shader, which the caller changes and destroys. Null without the shader.</summary>
        public static Material Own(Color colour, float glow)
        {
            if (_lit == null)
            {
                _lit = Shader.Find("HDRP/Lit");
            }
            if (_lit == null)
            {
                return null;
            }
            var made = new Material(_lit) { name = "RealLifeOverhaul Status Lights", renderQueue = Shapes.TakesDecalsQueue, enableInstancing = true };
            Tint(made, colour, glow);
            return made;
        }

        /// <summary>The colour of such a material and how strongly it lights itself.</summary>
        public static void Tint(Material material, Color colour, float glow)
        {
            if (material == null)
            {
                return;
            }
            material.SetColor(BaseColorId, new Color(colour.r * 0.4f, colour.g * 0.4f, colour.b * 0.4f, 1f));
            material.SetColor(EmissiveColorId, new Color(colour.r, colour.g, colour.b, 1f) * glow);
        }
    }

    /// <summary>The floor of the room the player is in as a <see cref="RoomGrid"/>, sampled once under the fade.</summary>
    internal static class RoomMeasure
    {
        public const float CellLeast = 0.3f;
        public const int CellsMost = 30000;

        private static readonly List<Renderer> Renderers = new List<Renderer>(32);

        /// <summary>The door the player came in by (the nearest way out on foot) and the way into the room from it.</summary>
        public static bool Door(BuildingManager building, Vector3 player, Vector3 facing, out Vector3 at, out Vector3 inward)
        {
            at = player;
            inward = facing;
            float best = 36f;
            List<ExitZone> exits = building != null ? building.exitZones : null;
            for (int i = 0; exits != null && i < exits.Count; i++)
            {
                ExitZone exit = exits[i];
                if (exit == null || exit.playerSpawnPoint == null || exit.isDriveInBay)
                {
                    continue;
                }
                float sqr = (exit.playerSpawnPoint.position - player).sqrMagnitude;
                if (sqr < best)
                {
                    best = sqr;
                    at = exit.playerSpawnPoint.position;
                    inward = exit.playerSpawnPoint.forward;
                }
            }
            inward.y = 0f;
            if (inward.sqrMagnitude < 0.01f)
            {
                return false;
            }
            inward.Normalize();
            return true;
        }

        /// <summary>The room's floor as a grid; null if none could be found, as while the game is still putting the room together.</summary>
        public static RoomGrid Measure(BuildingManager building, Vector3 door, bool furniture, out float floor)
        {
            floor = door.y;
            NavMeshQueryFilter filter = NavMeshHelper.NpcNavMeshFilter;
            if (building == null || !NavMesh.SamplePosition(door, out NavMeshHit start, 1.5f, filter))
            {
                return null;
            }
            floor = start.position.y;
            Bounds bounds = building.interiorBounds;
            if (bounds.size.x < 2f || bounds.size.z < 2f)
            {
                bounds = new Bounds(door, new Vector3(50f, 4f, 50f));
            }
            bounds.Encapsulate(door + new Vector3(1f, 0f, 1f));
            bounds.Encapsulate(door - new Vector3(1f, 0f, 1f));
            float minX = bounds.min.x - 1f, maxX = bounds.max.x + 1f, minZ = bounds.min.z - 1f, maxZ = bounds.max.z + 1f;
            float cell = Mathf.Max(CellLeast, Mathf.Sqrt((maxX - minX) * (maxZ - minZ) / CellsMost));
            var grid = new RoomGrid(minX, minZ, maxX, maxZ, cell);
            int open = 0;
            float reach = Mathf.Min(0.35f, cell), within = cell * 0.5f;
            for (int r = 0; r < grid.Rows; r++)
            {
                for (int c = 0; c < grid.Cols; c++)
                {
                    grid.Centre(c, r, out float x, out float z);
                    var point = new Vector3(x, floor, z);
                    if (NavMesh.SamplePosition(point, out NavMeshHit hit, reach, filter) && Mathf.Abs(hit.position.y - floor) < 0.25f
                        && CitySpots.Flat(hit.position - point) < within * within)
                    {
                        grid[c, r] = RoomGrid.Open;
                        open++;
                    }
                }
            }
            if (furniture)
            {
                Furniture(building, grid, floor);
            }
            return open > 0 ? grid : null;
        }

        // the cells under everything of the player's that stands on the floor are theirs
        private static void Furniture(BuildingManager building, RoomGrid grid, float floor)
        {
            List<ItemController> items = building.allItemControllers;
            for (int i = 0; items != null && i < items.Count; i++)
            {
                ItemController item = items[i];
                if (item == null || !item.gameObject.activeInHierarchy)
                {
                    continue;
                }
                Renderers.Clear();
                item.GetComponentsInChildren(false, Renderers);
                bool any = false;
                Bounds all = default;
                for (int k = 0; k < Renderers.Count; k++)
                {
                    Renderer renderer = Renderers[k];
                    if (renderer == null || !renderer.enabled || renderer is ParticleSystemRenderer)
                    {
                        continue;
                    }
                    if (!any)
                    {
                        all = renderer.bounds;
                        any = true;
                    }
                    else
                    {
                        all.Encapsulate(renderer.bounds);
                    }
                }
                if (!any || all.min.y > floor + 1.0f || all.max.y < floor - 0.5f)
                {
                    continue;   // on a wall, the ceiling or another floor
                }
                grid.CellOf(all.min.x - 0.05f, all.min.z - 0.05f, out int c0, out int r0);
                grid.CellOf(all.max.x + 0.05f, all.max.z + 0.05f, out int c1, out int r1);
                for (int r = Mathf.Max(0, r0); r <= Mathf.Min(grid.Rows - 1, r1); r++)
                {
                    for (int c = Mathf.Max(0, c0); c <= Mathf.Min(grid.Cols - 1, c1); c++)
                    {
                        grid[c, r] = RoomGrid.Furniture;
                    }
                }
            }
            Renderers.Clear();
        }

        /// <summary>The count of the player's items in the room: what changes when they add or take away furniture.</summary>
        public static int Items(BuildingManager building)
        {
            List<ItemController> items = building != null ? building.allItemControllers : null;
            return items != null ? items.Count : 0;
        }

        /// <summary>A wall's real surface: a ray at a height from a point towards where the wall should be. False if nothing upright is there.</summary>
        public static bool Wall(Vector3 from, Vector3 toward, float reach, out Vector3 surface, out Vector3 normal)
        {
            toward.y = 0f;
            toward.Normalize();
            if (Physics.Raycast(from - toward * 0.3f, toward, out RaycastHit hit, reach + 0.3f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                && Vector3.Dot(hit.normal, toward) < -0.85f)
            {
                surface = hit.point;
                normal = hit.normal;
                normal.y = 0f;
                normal.Normalize();
                return true;
            }
            surface = default;
            normal = default;
            return false;
        }

        /// <summary>The ceiling over a point, where a ray up finds a flat one within reach.</summary>
        public static bool Ceiling(Vector3 from, out Vector3 at)
        {
            if (Physics.Raycast(from, Vector3.up, out RaycastHit hit, 6f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) && hit.normal.y < -0.9f)
            {
                at = hit.point;
                return true;
            }
            at = default;
            return false;
        }

        /// <summary>A piece of a plan stood in the room: at its place on the floor, turned to its yaw.</summary>
        public static void Stand(Transform piece, SitePiece p, float floor)
        {
            piece.SetPositionAndRotation(new Vector3(p.X, floor + p.Y, p.Z), Quaternion.Euler(0f, p.Yaw, 0f));
        }

        public static Vector3 Facing(SitePiece p)
        {
            return Quaternion.Euler(0f, p.Yaw, 0f) * Vector3.forward;
        }

        /// <summary>Somebody on the navigation mesh near a point, for a cast: false if there is no floor there.</summary>
        public static bool Spot(Vector3 near, out Vector3 spot)
        {
            if (NavMesh.SamplePosition(near, out NavMeshHit hit, 0.6f, NavMeshHelper.NpcNavMeshFilter))
            {
                spot = hit.position;
                return true;
            }
            spot = near;
            return false;
        }
    }
}
