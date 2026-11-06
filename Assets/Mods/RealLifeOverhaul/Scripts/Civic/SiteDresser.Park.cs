using System.Collections.Generic;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// A car park's works on ground given to it (ParkSiteRules): the hoarding along the street with a gate at the way in and the
    /// works sign, the dig with its spoil, the base course of the game's concrete, the surface laid on it in dark paint, the lines
    /// being painted and the cones round the gate, a crew with the shovel, the rake and the kneeling work at each. Only the props
    /// of the city's programme and the plots' own meshes: nothing new is drawn. What stands there at the end is the construction
    /// game's (the car park's own ground, lanes and lamps), stood when the works are handed over.
    /// </summary>
    public sealed partial class SiteDresser
    {
        private static readonly Color PlainAsphalt = new Color(0.07f, 0.07f, 0.075f);   // the surface laid, where the game's own is missing

        private readonly List<ShellBox> _parkBase = new List<ShellBox>(4);
        private readonly List<ShellBox> _parkSurface = new List<ShellBox>(4);
        private readonly List<float> _parkXs = new List<float>(4);
        private readonly List<float> _parkZs = new List<float>(4);

        // A car park's ground from its record: no door, no pavement; the land's front looks out along -Z, as a plot's.
        private SiteRefusal ResolvePark(Dressed dressed)
        {
            SiteState site = dressed.Site;
            if (!ParkSiteRules.TryRead(site.Street, out int width, out int depth, out int yaw))
            {
                return SiteRefusal.NoRoom;
            }
            dressed.PlotAt = new Vector3(site.X, site.Y, site.Z);
            dressed.PlotTurn = Quaternion.Euler(0f, yaw, 0f);
            dressed.PlotWidth = width;
            dressed.PlotDepth = depth;
            dressed.PlotYaw = yaw;
            dressed.PlotStoreys = 1;
            dressed.Origin = dressed.PlotAt;
            dressed.Out = dressed.PlotTurn * Vector3.back;
            dressed.Along = dressed.PlotTurn * Vector3.right;
            dressed.Area = ParkArea(dressed.PlotAt, yaw, width, depth);
            float front = -depth * 0.5f - 0.9f;
            Vector3 board = PlotPoint(dressed, -width * 0.5f + ParkSiteRules.GateWidth + 1.6f, front);
            Notice(dressed, board, dressed.Out, PlotPoint(dressed, -width * 0.5f + ParkSiteRules.GateWidth + 3.6f, front), dressed.Out);
            return SiteRefusal.None;
        }

        // The box a car park's works fill, to the height of the cones and the digger.
        private Bounds ParkArea(Vector3 at, int yaw, int width, int depth)
        {
            ParkSiteRules.Corners(at.x, at.z, yaw, width, depth, _parkXs, _parkZs);
            var area = new Bounds(at, Vector3.zero);
            for (int i = 0; i < _parkXs.Count; i++)
            {
                area.Encapsulate(new Vector3(_parkXs[i], at.y, _parkZs[i]));
            }
            area.Encapsulate(new Vector3(at.x, at.y + 4f, at.z));
            return area;
        }

        // A car park's works not yet set out: whether any of its ground is in the picture (its hoarding and base stand at once).
        private bool ParkInView(SiteState site)
        {
            if (SceneFinds.Black || !ParkSiteRules.TryRead(site.Street, out int width, out int depth, out int yaw))
            {
                return false;
            }
            return BoxInView(ParkArea(new Vector3(site.X, site.Y, site.Z), yaw, width, depth));
        }

        private void BuildPark(Dressed dressed)
        {
            int phase = dressed.Phase, width = dressed.PlotWidth, depth = dressed.PlotDepth;
            dressed.Hides.Clear();
            dressed.Hidden = false;

            // Every stage: the hoarding along the street (one panel carries the notice), the cones at the gate, the works sign.
            _spots.Clear();
            ParkSiteRules.HoardingSpots(width, depth, _spots);
            for (int i = 0; i < _spots.Count; i++)
            {
                if (dressed.Unsafe && i == _spots.Count - 1)
                {
                    continue;   // the panel a careless contractor left out
                }
                Put(dressed, i == 0 ? "site_hoarding_notice" : "site_hoarding", PlotPoint(dressed, _spots[i].X, _spots[i].Z), PlotFacing(dressed, _spots[i].Yaw));
            }
            float gate = -width * 0.5f + ParkSiteRules.GateWidth * 0.5f, front = -depth * 0.5f;
            Put(dressed, "cone", PlotPoint(dressed, gate - ParkSiteRules.GateWidth * 0.5f + 0.3f, front + 0.4f), PlotFacing(dressed, 0f));
            Put(dressed, "cone", PlotPoint(dressed, gate + ParkSiteRules.GateWidth * 0.5f - 0.3f, front + 0.4f), PlotFacing(dressed, 0f));
            Put(dressed, "road_sign_works", PlotPoint(dressed, gate, front + 1.8f), PlotFacing(dressed, 180f));

            float inside = ParkSiteRules.Margin + 1f;
            if (phase == 1)
            {
                // The dig: the ground broken over the works, the spoil heaped, the skip at the gate.
                var earth = new Vector3(0f, 0f, 0f);
                Transform works = Put(dressed, "site_earthworks", PlotPoint(dressed, earth.x, earth.z), PlotFacing(dressed, 0f));
                if (works != null)
                {
                    works.localScale = new Vector3((width - 2f * inside) / PlotRules.EarthSize, 1f, (depth - 2f * inside) / PlotRules.EarthSize);
                }
                for (int i = 0; i < 3; i++)
                {
                    Put(dressed, "spoil_heap", PlotPoint(dressed, -width * 0.5f + inside + (width - 2f * inside) * (i + 0.5f) / 3f, depth * 0.5f - inside - 1f), PlotFacing(dressed, i * 70f));
                }
                Put(dressed, "skip_bin", PlotPoint(dressed, gate + ParkSiteRules.GateWidth * 0.5f + 2.5f, front + 2.2f), PlotFacing(dressed, 90f));
            }
            _parkBase.Clear();
            _parkSurface.Clear();
            if (ParkSiteRules.BaseLaid(phase))
            {
                _parkBase.Add(ParkSiteRules.Base(width, depth, ParkSiteRules.BaseHeight));
                MakeMesh(dressed, PlotMeshes.Boxes(_parkBase, _parkSurface, "RealLifeOverhaul car park base"), SiteKit.Concrete, PlainConcrete, false);
            }
            if (ParkSiteRules.SurfaceLaid(phase))
            {
                // The surface laid on the base: dark paint a little higher, and lines being painted on it in the last stage.
                _parkBase.Clear();
                float reach = phase >= 4 ? 1f : 0.55f;   // the share of the base the surface covers: the rest is still being laid
                float x = (width * 0.5f - ParkSiteRules.Margin) * reach, z = depth * 0.5f - ParkSiteRules.Margin;
                _parkBase.Add(new ShellBox(-x, x, ParkSiteRules.BaseHeight, ParkSiteRules.SurfaceHeight, -z, z));
                MakeMesh(dressed, PlotMeshes.Boxes(_parkBase, _parkSurface, "RealLifeOverhaul car park surface"), null, PlainAsphalt, false);
                if (phase >= 4)
                {
                    for (int i = 0; i < 4; i++)
                    {
                        Put(dressed, "road_marking", PlotPoint(dressed, -width * 0.5f + inside + 3f + i * 2.5f, 0f), PlotFacing(dressed, 90f));
                    }
                }
            }

            // People: at the gate, at the dig, raking the base, painting the lines.
            Vector3 gateAt = PlotPoint(dressed, gate, front + 1.2f), middle = PlotPoint(dressed, 0f, -depth * 0.25f), side = PlotPoint(dressed, width * 0.25f, -depth * 0.25f);
            Vector3 inward = dressed.PlotTurn * Vector3.forward;
            switch (phase)
            {
                case 0:
                    Plan(dressed, gateAt, gateAt + inward, CityLook.RoadWorker, "clipboard_write", null);
                    break;
                case 1:
                    Plan(dressed, middle, middle + inward, CityLook.RoadWorker, "shovel_dig", "shovel_rest");
                    Plan(dressed, side, side + inward, CityLook.RoadWorker, "shovel_dig", "shovel_rest");
                    Plan(dressed, gateAt, middle, CityLook.RoadWorker, "clipboard_write", null);
                    break;
                case 2:
                    Plan(dressed, middle, middle + inward, CityLook.RoadWorker, ClipLibrary.Has("rake_asphalt") ? "rake_asphalt" : "shovel_dig", null);
                    Plan(dressed, gateAt, middle, CityLook.RoadWorker, "clipboard_write", null);
                    break;
                case 3:
                    Plan(dressed, middle, middle + inward, CityLook.RoadWorker, ClipLibrary.Has("rake_asphalt") ? "rake_asphalt" : "kneel_work", "arms_crossed");
                    Plan(dressed, side, side + inward, CityLook.RoadWorker, "kneel_work", null);
                    break;
                default:
                    Plan(dressed, middle, middle + inward, CityLook.RoadWorker, "kneel_work", "arms_crossed");
                    Plan(dressed, gateAt, middle, CityLook.RoadWorker, "clipboard_write", null);
                    break;
            }
        }
    }
}
