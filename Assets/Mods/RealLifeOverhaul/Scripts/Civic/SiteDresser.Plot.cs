using System.Collections.Generic;
using Helpers;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// A new building going up on ground given (PlotRules): hoarding along the front and the game's fence wire round the rest,
    /// the dig, the slab with the tower crane standing and slewing, the frame a storey at a time in the game's own concrete,
    /// then the frame clad and wrapped in the game's scaffolding. Whatever of it stands between the camera and the player is
    /// hidden as the game hides its buildings. The building that stands there at the end is not this file's.
    /// </summary>
    public sealed partial class SiteDresser
    {
        private const float CraneTop = 48f;           // metres: the crane's tower top, for the box a plot fills
        private const string MastId = "tower_crane_mast";
        private const string JibId = "tower_crane_jib";

        private static readonly Color PlainConcrete = new Color(0.58f, 0.57f, 0.55f);   // where the game's concrete is missing
        private static readonly Color PlainMetal = new Color(0.42f, 0.44f, 0.46f);
        private static readonly Color PlainWire = new Color(0.55f, 0.57f, 0.58f);

        private readonly List<ShellBox> _concrete = new List<ShellBox>(256);
        private readonly List<ShellBox> _metal = new List<ShellBox>(256);
        private readonly List<PlotSpot> _spots = new List<PlotSpot>(16);
        private readonly List<FenceRun> _fence = new List<FenceRun>(4);

        // A plot's ground from its record: no door, no pavement; the land's front looks out along -Z.
        private SiteRefusal ResolvePlot(Dressed dressed)
        {
            SiteState site = dressed.Site;
            if (!PlotRules.TryRead(site.Street, out int width, out int depth, out int yaw, out int storeys) || PlotRules.Unfit(width, depth, storeys) != null)
            {
                return SiteRefusal.NoRoom;
            }
            dressed.PlotAt = new Vector3(site.X, site.Y, site.Z);
            dressed.PlotTurn = Quaternion.Euler(0f, yaw, 0f);
            dressed.PlotWidth = width;
            dressed.PlotDepth = depth;
            dressed.PlotYaw = yaw;
            dressed.PlotStoreys = storeys;
            dressed.Origin = dressed.PlotAt;
            dressed.Out = dressed.PlotTurn * Vector3.back;
            dressed.Along = dressed.PlotTurn * Vector3.right;
            dressed.Area = PlotArea(dressed.PlotAt, yaw, width, depth);
            PlotRules.Gate(width, out float gateFrom, out _);
            Vector3 board = PlotPoint(dressed, gateFrom - 1.6f, -depth * 0.5f - 0.9f);
            Notice(dressed, board, dressed.Out, PlotPoint(dressed, -width * 0.5f + 2f, -depth * 0.5f - 0.9f), dressed.Out);
            dressed.Wrap = dressed.Wrap ?? NewWrap();
            dressed.Wrap.Plan.Clear();
            Framed = true;
            return SiteRefusal.None;
        }

        // The box a plot fills, crane and all: the land turned by its yaw, to the top of the crane.
        private static Bounds PlotArea(Vector3 at, int yaw, int width, int depth)
        {
            var area = new Bounds(at, Vector3.zero);
            for (int corner = 0; corner < 4; corner++)
            {
                PlotRules.ToWorld(at.x, at.z, yaw, (corner % 2 == 0 ? -0.5f : 0.5f) * width, (corner < 2 ? -0.5f : 0.5f) * depth, out float x, out float z);
                area.Encapsulate(new Vector3(x, at.y, z));
            }
            area.Encapsulate(new Vector3(at.x, at.y + CraneTop, at.z));
            return area;
        }

        // A plot that is not set out yet: whether any of its ground is in the picture (its fence and slab stand at once).
        private bool PlotInView(SiteState site)
        {
            if (SceneFinds.Black || !PlotRules.TryRead(site.Street, out int width, out int depth, out int yaw, out _))
            {
                return false;
            }
            return BoxInView(PlotArea(new Vector3(site.X, site.Y, site.Z), yaw, width, depth));
        }

        private static Vector3 PlotPoint(Dressed dressed, float x, float z)
        {
            PlotRules.ToWorld(dressed.PlotAt.x, dressed.PlotAt.z, dressed.PlotYaw, x, z, out float worldX, out float worldZ);
            var at = new Vector3(worldX, dressed.PlotAt.y, worldZ);
            if (CitySpots.Ground(at, out float ground))
            {
                at.y = ground;
            }
            return at;
        }

        private static Vector3 PlotFacing(Dressed dressed, float yaw) => dressed.PlotTurn * Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;

        private void BuildPlot(Dressed dressed)
        {
            int phase = dressed.Phase, width = dressed.PlotWidth, depth = dressed.PlotDepth;
            SiteState site = dressed.Site;
            int standing = PlotRules.Standing(phase, site.Progress, dressed.PlotStoreys);
            dressed.Step = PlotRules.Step(phase, site.Progress, dressed.PlotStoreys);
            dressed.Hides.Clear();
            dressed.Hidden = false;

            // Every stage: the hoarding along the front (one panel carries the notice), the fence round the rest, the cabin.
            _spots.Clear();
            PlotRules.HoardingSpots(width, depth, _spots);
            for (int i = 0; i < _spots.Count; i++)
            {
                if (dressed.Unsafe && i == _spots.Count - 1)
                {
                    continue;   // the panel a careless contractor left out
                }
                Put(dressed, i == 0 ? "site_hoarding_notice" : "site_hoarding", PlotPoint(dressed, _spots[i].X, _spots[i].Z), PlotFacing(dressed, _spots[i].Yaw));
            }
            _fence.Clear();
            PlotRules.FenceRuns(width, depth, _fence);
            MakeMesh(dressed, PlotMeshes.Fence(_fence, "RealLifeOverhaul plot fence"), SiteKit.Wire, PlainWire, false);
            PlotRules.YardSpots(width, depth, out PlotSpot cabin, out PlotSpot toilet);
            Put(dressed, "site_cabin", PlotPoint(dressed, cabin.X, cabin.Z), PlotFacing(dressed, cabin.Yaw));
            Put(dressed, "site_toilet", PlotPoint(dressed, toilet.X, toilet.Z), PlotFacing(dressed, toilet.Yaw));

            if (phase == 1)
            {
                // The dig: the ground broken over the frame's footprint, the spoil heaped round it.
                PlotSpot earth = PlotRules.EarthSpot(width, depth);
                Transform works = Put(dressed, "site_earthworks", PlotPoint(dressed, earth.X, earth.Z), PlotFacing(dressed, earth.Yaw));
                if (works != null)
                {
                    works.localScale = new Vector3(earth.ScaleX, 1f, earth.ScaleZ);
                }
                _spots.Clear();
                PlotRules.SpoilSpots(width, depth, _spots);
                for (int i = 0; i < _spots.Count; i++)
                {
                    Put(dressed, "spoil_heap", PlotPoint(dressed, _spots[i].X, _spots[i].Z), PlotFacing(dressed, _spots[i].Yaw));
                }
            }
            if (phase >= 2)
            {
                _concrete.Clear();
                _metal.Clear();
                if (phase == 2)
                {
                    PlotRules.SlabBoxes(width, depth, _concrete, _metal);
                }
                else
                {
                    PlotRules.FrameBoxes(width, depth, standing, phase >= 4, _concrete, _metal);
                }
                MakeMesh(dressed, PlotMeshes.Boxes(_concrete, _metal, "RealLifeOverhaul plot frame"), SiteKit.Concrete, PlainConcrete, true);
                PlotSpot crane = PlotRules.CraneSpot(width, depth);
                StandCrane(dressed, PlotPoint(dressed, crane.X, crane.Z), PlotFacing(dressed, crane.Yaw), dressed.Site.Id * 13.7f);
                _spots.Clear();
                PlotRules.StackSpots(width, depth, _spots);
                for (int i = 0; i < _spots.Count; i++)
                {
                    Put(dressed, Stack(i), PlotPoint(dressed, _spots[i].X, _spots[i].Z), PlotFacing(dressed, _spots[i].Yaw));
                }
            }

            // The last stage: the clad frame in the game's scaffolding, every side, fabric on.
            SiteWrap wrap = dressed.Wrap;
            if (phase >= 4 && SiteKit.Ready)
            {
                if (wrap.Plan.Pieces.Count == 0)
                {
                    wrap.PrepareFrame(dressed.PlotAt, dressed.PlotTurn, PlotRules.Shell(width, depth, dressed.PlotStoreys), CivicWorks.PlaceText(site));
                }
                WrapRules.PlotStage(phase, wrap.Plan.Lifts, out int lifts, out bool fabric);
                wrap.Want(lifts, fabric);
            }
            else if (wrap.Placed > 0)
            {
                wrap.Want(0, false);
            }
            wrap.Hide(false);   // the stage's new meshes are shown: the wrap with them, until the next look says otherwise
            Framed = true;

            // People: at the gate, at the dig, on the slab, at the frame's foot.
            PlotRules.Frame(width, depth, out float minX, out float maxX, out float minZ, out _);
            PlotRules.Gate(width, out float gateFrom, out float gateTo);
            Vector3 gate = PlotPoint(dressed, (gateFrom + gateTo) * 0.5f, -depth * 0.5f + 1.2f);
            Vector3 edge = PlotPoint(dressed, (minX + maxX) * 0.5f, minZ - 1.0f), edgeLeft = PlotPoint(dressed, minX + 1.2f, minZ - 1.0f);
            Vector3 inward = dressed.PlotTurn * Vector3.forward;
            switch (phase)
            {
                case 0:
                    Plan(dressed, gate, gate + inward, CityLook.RoadWorker, "clipboard_write", null);
                    break;
                case 1:
                    Plan(dressed, edge, edge + inward, CityLook.RoadWorker, "shovel_dig", "shovel_rest");
                    Plan(dressed, edgeLeft, edgeLeft + inward, CityLook.RoadWorker, "shovel_dig", "shovel_rest");
                    Plan(dressed, gate, edge, CityLook.RoadWorker, "clipboard_write", null);
                    break;
                case 2:
                    Plan(dressed, edge, edge + inward, CityLook.RoadWorker, ClipLibrary.Has("rake_asphalt") ? "rake_asphalt" : "shovel_dig", null);
                    Plan(dressed, gate, edge, CityLook.RoadWorker, "clipboard_write", null);
                    break;
                case 3:
                    Plan(dressed, edge, edge + inward, CityLook.RoadWorker, ClipLibrary.Has("hammer_nail") ? "hammer_nail" : "kneel_work", "arms_crossed");
                    Plan(dressed, edgeLeft, edgeLeft + inward, CityLook.RoadWorker, "kneel_work", null);
                    Plan(dressed, gate, edge, CityLook.RoadWorker, "clipboard_write", null);
                    break;
                default:
                    Plan(dressed, gate, edge, CityLook.RoadWorker, "clipboard_write", null);
                    Plan(dressed, edgeLeft, edgeLeft + inward, CityLook.RoadWorker, "kneel_work", "arms_crossed");
                    break;
            }
        }

        private static string Stack(int index)
        {
            switch (index % 3)
            {
                case 0: return "site_stack_blocks";
                case 1: return "site_stack_timber";
                default: return "site_stack_rebar";
            }
        }

        // One mesh of a stage under the sites' root, at the land's middle and turn; the frame's renderers are hidden from the
        // camera with the wrap (FadePlots), the fence's are not (wire is seen through).
        private void MakeMesh(Dressed dressed, Mesh mesh, Material first, Color plainFirst, bool hides)
        {
            Material second = SiteKit.Metal != null ? SiteKit.Metal : Shapes.Paint(PlainMetal);
            Material own = first != null ? first : Shapes.Paint(plainFirst);
            var made = new GameObject(mesh.name);
            made.transform.SetParent(_root, false);
            made.transform.SetPositionAndRotation(dressed.PlotAt, dressed.PlotTurn);
            made.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = made.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = new[] { own, second };
            dressed.Made.Add(made);
            if (hides)
            {
                dressed.Hides.Add(renderer);
            }
        }

        // A plot's meshes go with what they were made for.
        private static void DropMade(Dressed dressed)
        {
            for (int i = 0; i < dressed.Made.Count; i++)
            {
                GameObject made = dressed.Made[i];
                if (made == null)
                {
                    continue;
                }
                if (made.TryGetComponent(out MeshFilter filter))
                {
                    PlotMeshes.Drop(filter.sharedMesh);
                }
                Object.Destroy(made);
            }
            dressed.Made.Clear();
            dressed.Hides.Clear();
        }

        // The crane: its mast on the pad, its jib on the slewing ring turning by the engine's animation. Part of the site's
        // props when it belongs to one (they go back together; the jib hangs under the mast), else the test's.
        private Transform StandCrane(Dressed dressed, Vector3 at, Vector3 forward, float offset)
        {
            Transform mast = _props.Stand(MastId, at, forward);
            if (mast == null)
            {
                return null;
            }
            Transform jib = _props.Take(JibId);
            if (dressed != null)
            {
                dressed.Props.Add(mast);
            }
            if (jib != null)
            {
                jib.SetParent(mast, false);
                jib.localPosition = new Vector3(0f, PlotRules.CraneRing, 0f);
                jib.localRotation = Quaternion.identity;
                jib.localScale = Vector3.one;
                CraneSlew.Play(jib.gameObject, offset);
                if (dressed != null)
                {
                    dressed.Props.Add(jib);
                }
            }
            return mast;
        }

        private void ReturnCrane(Transform mast)
        {
            Transform jib = mast != null ? mast.Find(JibId) : null;
            _props.Return(jib);
            _props.Return(mast);
        }

        /// <summary>Test: the tower crane alone, standing at a place and slewing. Null when it stands, else why not.</summary>
        public string CraneNow(Vector3 at, Vector3 forward)
        {
            if (_testCrane != null)
            {
                ReturnCrane(_testCrane);
            }
            _testCrane = StandCrane(null, at, forward, 0f);
            if (_testCrane == null)
            {
                return "the crane's mast could not be made";
            }
            Transform jib = _testCrane.Find(JibId);
            if (Meta.Dev.Verbose)
            {
                bool slewing = jib != null && CraneSlew.Playing(jib.gameObject);
                CivicDirector.Logger?.Info("Construction check (crane): mast " + (PropLibrary.Has(MastId) ? "modelled" : "as a stand-in") + " to the ring " + PlotRules.CraneRing
                                           + " m up, the jib " + (jib == null ? "missing" : (PropLibrary.Has(JibId) ? "modelled" : "as a stand-in") + ", slewing by the engine's own animation ("
                                               + CraneSlew.Round + " s a round): " + (slewing ? "PASS." : "FAIL (its Animation is not playing).")));
            }
            return jib == null ? "the crane's jib could not be made" : null;
        }

        // Five times a second: a plot standing between the camera and the player is hidden as the game hides its buildings
        // (shadows kept), its wrap with it; shown again once it is not.
        private void FadePlots()
        {
            Camera camera = GameManager.GetMainCamera();
            if (camera == null || PlayerHelper.PlayerController == null)
            {
                return;
            }
            Vector3 eye = camera.transform.position, player = PlayerHelper.GetPosition() + Vector3.up;
            Vector3 towards = player - eye;
            float distance = towards.magnitude;
            var ray = new Ray(eye, distance > 0.01f ? towards / distance : Vector3.forward);
            for (int i = 0; i < _dressed.Count; i++)
            {
                Dressed dressed = _dressed[i];
                if (dressed.Site == null || dressed.Site.Kind != SiteKind.Plot)
                {
                    continue;
                }
                PlotRules.Frame(dressed.PlotWidth, dressed.PlotDepth, out float minX, out float maxX, out float minZ, out float maxZ);
                Bounds frame = dressed.Wrap.Placed > 0 ? dressed.Wrap.Bounds : FrameBounds(dressed, minX, maxX, minZ, maxZ);
                bool blocks = frame.IntersectRay(ray, out float hit) && hit < distance - 1f && !frame.Contains(eye);
                if (blocks == dressed.Hidden)
                {
                    continue;
                }
                dressed.Hidden = blocks;
                for (int r = 0; r < dressed.Hides.Count; r++)
                {
                    if (dressed.Hides[r] != null)
                    {
                        ViewBlockingEntity.SetRendererToHideVisibility(dressed.Hides[r], blocks);
                    }
                }
                dressed.Wrap.Hide(blocks);
            }
        }

        private static Bounds FrameBounds(Dressed dressed, float minX, float maxX, float minZ, float maxZ)
        {
            var box = new Bounds(dressed.PlotAt, Vector3.zero);
            float top = PlotRules.GroundSlab + dressed.PlotStoreys * PlotRules.StoreyHeight + PlotRules.RailHeight;
            for (int corner = 0; corner < 4; corner++)
            {
                PlotRules.ToWorld(dressed.PlotAt.x, dressed.PlotAt.z, dressed.PlotYaw, corner % 2 == 0 ? minX : maxX, corner < 2 ? minZ : maxZ, out float x, out float z);
                box.Encapsulate(new Vector3(x, dressed.PlotAt.y, z));
                box.Encapsulate(new Vector3(x, dressed.PlotAt.y + top, z));
            }
            return box;
        }
    }
}
