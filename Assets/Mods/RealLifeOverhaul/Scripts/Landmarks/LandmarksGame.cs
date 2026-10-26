using System.Collections.Generic;
using Helpers;
using RealLifeOverhaul.NewBuildings;
using RealLifeOverhaul.NewGround;
using UI.Load;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;

namespace RealLifeOverhaul.Landmarks
{
    /// <summary>
    /// The only part of the landmarks that reads or changes the game's scene: the clock, the player and the way they are
    /// taken to a landmark, the pieces a landmark stands in the street (the props bundles' models, plain shapes of the same
    /// size while the building sites' bundle is not installed), what stands in the way and cuts the walking meshes, the
    /// camera's view, the crowds' squares of the game's own pedestrians and a district building's traffic index. Nothing
    /// here keeps state but the camera's planes and the scratch lists. Called on a second's look, a day's pass or a press,
    /// never per frame.
    /// </summary>
    internal static class LandmarksGame
    {
        private const float FarPlane = 400f;       // m: a landmark further from the camera than this is out of its picture
        private const string RootName = "RealLifeOverhaul Landmark";

        private static readonly Plane[] Planes = new Plane[6];
        private static readonly Color Concrete = new Color(0.62f, 0.62f, 0.60f);
        private static readonly Color Seats = new Color(0.10f, 0.42f, 0.46f);    // Uptown's green blue, the district's own on the city map
        private static readonly Color Roof = new Color(0.82f, 0.83f, 0.84f);
        private static readonly Color Grass = new Color(0.20f, 0.45f, 0.18f);
        private static readonly Color Steel = new Color(0.30f, 0.31f, 0.33f);

        internal static bool Loading => LoadScene.isLoading || !ModState.Loaded || SaveGameManager.Current == null;

        internal static int Day => TimeHelper.CurrentDay;

        internal static int Hour => TimeHelper.CurrentHour;

        internal static bool Outdoors => PlayerHelper.PlayerController != null && !BuildingManager.IsInsideBuilding;

        internal static Vector3 Player => PlayerHelper.PlayerController != null ? PlayerHelper.GetPosition() : Vector3.zero;

        internal static bool InVehicle => VehicleHelper.IsInsideVehicle();

        internal static bool MapOpen => CityMap.IsOpen;

        // ------------------------------------------------------------------ taking the player there

        /// <summary>
        /// The player put at a spot, facing a way, the game's own way: out of a building first by its own door, out of a vehicle
        /// by its own exit. False while the game is still taking them out (ask again in a moment).
        /// </summary>
        internal static bool TakeTo(Vector3 at, float heading)
        {
            BuildingManager manager = InstanceBehavior<BuildingManager>.IsInitialized ? InstanceBehavior<BuildingManager>.Instance : null;
            if (BuildingManager.IsInsideBuilding || (manager != null && (manager.enteringBuilding || manager.exitingBuilding)))
            {
                if (BuildingManager.IsInsideBuilding && manager != null && !manager.exitingBuilding)
                {
                    manager.ExitFromBuilding(0);
                }
                return false;
            }
            if (VehicleHelper.IsInsideVehicle())
            {
                GameManager game = InstanceBehavior<GameManager>.IsInitialized ? InstanceBehavior<GameManager>.Instance : null;
                game?.selectedVehicle?.ExitVehicle();
                return false;
            }
            WalkGame.Warp(at);
            if (PlayerHelper.PlayerController != null)
            {
                PlayerHelper.PlayerController.transform.rotation = Quaternion.Euler(0f, heading, 0f);
            }
            return true;
        }

        // ------------------------------------------------------------------ the camera

        /// <summary>Whether any of a landmark's ground, to a height, is in the camera's picture or on the open city map: a change there waits until it is not.</summary>
        internal static bool InView(SiteRect ground, float height)
        {
            if (MapOpen)
            {
                return true;
            }
            Camera camera = GameManager.GetMainCamera();
            if (camera == null)
            {
                return false;
            }
            var box = new Bounds(new Vector3(ground.MidX, height * 0.5f, ground.MidZ), new Vector3(ground.Width + 4f, height + 2f, ground.Depth + 4f));
            if (box.SqrDistance(camera.transform.position) > FarPlane * FarPlane)
            {
                return false;
            }
            GeometryUtility.CalculateFrustumPlanes(camera, Planes);
            return GeometryUtility.TestPlanesAABB(Planes, box);
        }

        // ------------------------------------------------------------------ the pieces

        /// <summary>
        /// The pieces of a landmark stood under a root of their own on the ground at <paramref name="ground"/>: each the props
        /// bundle's model when it has one, else (the stadium's own pieces only) plain shapes of the same size; a solid piece also
        /// a box the player walks round and a cut in the walking meshes. Returns the root, or null when nothing stood.
        /// </summary>
        internal static GameObject Stand(string name, List<LandmarkPiece> pieces, float ground, out int modelled, out int plain)
        {
            modelled = plain = 0;
            if (pieces == null || pieces.Count == 0)
            {
                return null;
            }
            var root = new GameObject(RootName + " " + name);
            for (int i = 0; i < pieces.Count; i++)
            {
                LandmarkPiece p = pieces[i];
                var place = new Vector3(p.X, ground, p.Z);
                Quaternion turn = Quaternion.Euler(0f, p.Yaw, 0f);
                Vector3 stretch = new Vector3(p.StretchX, 1f, p.StretchZ);
                if (PropLibrary.Has(p.Id) && PropLibrary.TryGet(p.Id, out Mesh mesh, out Material material) && mesh != null && material != null)
                {
                    var piece = new GameObject(p.Id);
                    piece.transform.SetParent(root.transform, false);
                    piece.transform.SetPositionAndRotation(place, turn);
                    piece.transform.localScale = stretch;
                    piece.AddComponent<MeshFilter>().sharedMesh = mesh;
                    var renderer = piece.AddComponent<MeshRenderer>();
                    renderer.sharedMaterial = material;
                    renderer.lightProbeUsage = LightProbeUsage.Off;
                    renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                    modelled++;
                    if (p.Solid)
                    {
                        Block(piece, p.Id);
                    }
                    continue;
                }
                GameObject shape = PlainShape(p.Id, root.transform, place, turn, stretch);
                if (shape != null)
                {
                    plain++;
                    if (p.Solid)
                    {
                        Block(shape, p.Id);
                    }
                }
            }
            if (modelled + plain == 0)
            {
                Object.Destroy(root);
                return null;
            }
            return root;
        }

        /// <summary>A landmark's pieces taken away: their boxes and cuts go with them. Safe on null.</summary>
        internal static void Take(ref GameObject root)
        {
            if (root != null)
            {
                Object.Destroy(root);
            }
            root = null;
        }

        // A piece's own size (m: across, up, along), as the bundle's models are made (PropAssets/scripts/props_landmarks.py).
        private static Vector3 SizeOf(string id)
        {
            switch (id)
            {
                case LandmarkRules.Stand: return new Vector3(LandmarkRules.Section, LandmarkRules.StandHeight, LandmarkRules.StandDepth);
                case LandmarkRules.End: return new Vector3(LandmarkRules.Section, LandmarkRules.EndHeight, LandmarkRules.EndDepth);
                case LandmarkRules.Corner: return new Vector3(LandmarkRules.EndDepth, LandmarkRules.CornerHeight, LandmarkRules.StandDepth);
                case LandmarkRules.Floodlight: return new Vector3(1.6f, LandmarkRules.FloodlightHeight, 1.6f);
                case LandmarkRules.Lamps: return Vector3.zero;   // drawn by the mast's plain shape while the bundle has neither
                case LandmarkRules.Pitch: return new Vector3(LandmarkRules.PitchWidth, 0.06f, LandmarkRules.PitchDepth);
                default: return Vector3.zero;
            }
        }

        // What stands in the way: a box the player's body meets, and the same box cut out of both walking meshes.
        private static void Block(GameObject piece, string id)
        {
            Vector3 size = SizeOf(id);
            if (size == Vector3.zero)
            {
                return;
            }
            var box = piece.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, size.y * 0.5f, 0f);
            box.size = size;
            var cut = piece.AddComponent<NavMeshObstacle>();
            cut.shape = NavMeshObstacleShape.Box;
            cut.center = box.center;
            cut.size = new Vector3(Mathf.Max(0.5f, size.x - 0.4f), size.y, Mathf.Max(0.5f, size.z - 0.4f));
            cut.carveOnlyStationary = true;
            cut.carving = true;
        }

        // The stadium's own pieces drawn as plain shapes while the building sites' bundle does not have them: the same
        // footprint and height, terraces as steps. Other ids (the first bundle's dressing) have none: they are left out.
        private static GameObject PlainShape(string id, Transform parent, Vector3 place, Quaternion turn, Vector3 stretch)
        {
            if (!Shapes.Available)
            {
                return null;
            }
            Vector3 size = SizeOf(id);
            if (size == Vector3.zero)
            {
                return null;
            }
            var piece = new GameObject(id + " (plain)");
            Transform t = piece.transform;
            t.SetParent(parent, false);
            t.SetPositionAndRotation(place, turn);
            t.localScale = stretch;
            Mesh cube = Shapes.Cube;
            switch (id)
            {
                case LandmarkRules.Pitch:
                    Shapes.Piece("field", t, cube, Shapes.Paint(Grass), new Vector3(0f, 0.03f, 0f), size);
                    break;
                case LandmarkRules.Floodlight:
                    Shapes.Piece("mast", t, cube, Shapes.Paint(Steel), new Vector3(0f, size.y * 0.5f, 0f), new Vector3(0.6f, size.y, 0.6f));
                    Shapes.Piece("lamps", t, cube, Shapes.Paint(Roof, 2f), new Vector3(0f, size.y - 1.2f, 0.3f), new Vector3(4f, 2.4f, 0.4f));
                    break;
                default:
                    // Terraces rising away from the pitch (its seats face +Z), a back wall, and a roof on the main stands.
                    const int Steps = 6;
                    for (int s = 0; s < Steps; s++)
                    {
                        float depth = size.z / Steps, rise = (size.y - 2f) * (s + 1) / Steps;
                        Shapes.Piece("terrace", t, cube, Shapes.Paint(s % 2 == 0 ? Seats : Concrete),
                            new Vector3(0f, rise * 0.5f, size.z * 0.5f - depth * (s + 0.5f)), new Vector3(size.x, rise, depth));
                    }
                    if (id == LandmarkRules.Stand)
                    {
                        Shapes.Piece("roof", t, cube, Shapes.Paint(Roof), new Vector3(0f, size.y - 0.2f, 0f), new Vector3(size.x, 0.4f, size.z));
                    }
                    break;
            }
            return piece;
        }

        // ------------------------------------------------------------------ crowds and traffic

        /// <summary>A square of pavement the game's own pedestrians come to and stand in (a crowd), or null with why.</summary>
        internal static StationaryAiSpawner Crowd(float x, float z, float yaw, float width, float depth, out string why)
        {
            return PeopleGame.Zone(new Vector3(x, UptownGround, z), yaw, new Vector3(width, PeopleGame.ZoneHeight, depth), out why);
        }

        /// <summary>A crowd's square taken away, its people back to the game's pools. Safe on null.</summary>
        internal static void NoCrowd(ref StationaryAiSpawner zone)
        {
            PeopleGame.Unzone(ref zone);
        }

        private const float UptownGround = 0f;   // m: the district's ground (UptownLots.GroundY)

        /// <summary>
        /// A district building's traffic index as the game reads it for its customers (Building.trafficIndex): the game's record
        /// is made at each load and never saved, so a day's lift goes with the next load. False for a building with no record.
        /// </summary>
        internal static bool Traffic(MadeBuilding made, int index)
        {
            if (made == null || made.Building == null)
            {
                return false;
            }
            made.Building.trafficIndex = Mathf.Clamp(index, 0, 100);
            return true;
        }

        /// <summary>A district building's traffic index now; -1 for one with no record.</summary>
        internal static int TrafficNow(MadeBuilding made)
        {
            return made != null && made.Building != null ? made.Building.trafficIndex : -1;
        }
    }
}
