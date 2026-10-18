using System;
using System.Globalization;
using GleyTrafficSystem;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>The bus network's part for a stop's bay: whether the strip by the kerb will do, and what its zone must clear.</summary>
    public static partial class TransitMap
    {
        private const float BayStraight = 0.35f;    // metres the lane's points may lie off its line along a bay
        private const float BayLevel = 0.30f;       // metres the strip may lie above or below the lane along a bay
        private const float KerbLookBehind = 33f;   // metres behind the pole the kerb's edge is looked along: all a bay may take up (BusBay.Lay)
        private const float KerbLookAhead = 26f;    // and ahead of it
        private const float BayAheadSlack = 1f;     // metres of strip ahead more than the bus needs to pull out: a vehicle's middle is seldom quite its origin
        private static readonly BusZone Zoning = new BusZone();   // what the bay being laid out would need done, kept for the stop that has one

        // Whether the strip beside a stop will do for a bay, and where the bus then stands; NoBay says why not. Arithmetic first,
        // then some forty rays and two boxes.
        private static void LayBay(TransitStop stop, TransitLine line, Vector3 right)
        {
            Vector3 lane = stop.Lane;
            Vector3 along = stop.Along;
            stop.BayAlong = along;
            float kerb = KerbAside(stop, right);
            stop.KerbAside = KerbLine(stop, right, kerb);
            if (!BusBay.Lay(stop.KerbAside, out float offset, out BusBay.Curve curveIn, out BusBay.Curve curveOut, out float behind, out float ahead))
            {
                stop.NoBay = offset > 0f ? "too wide to cross in the plain lane there is"
                    : stop.KerbAside < kerb - 0.01f ? "narrow with " + (_edgeWhat ?? "what stands") + " on the kerb's edge (the kerb " + kerb.ToString("F2", CultureInfo.InvariantCulture)
                                                      + " m out, the bus may come to " + stop.KerbAside.ToString("F2", CultureInfo.InvariantCulture) + ")"
                    : "narrow";
                return;
            }
            ahead += BayAheadSlack;
            float leaves = curveIn.Length + BusBay.Settle + BusBay.BusMiddleAhead + 1f;
            float rejoins = curveOut.Length - BusBay.BusMiddleAhead + 1f;
            Zoning.Reset();
            stop.NoBay = LaneBends(stop, line, right, leaves, rejoins) ?? StripBad(stop, right, offset, -behind, ahead, Zoning)
                         ?? BusZones.Plan(stop, right, -behind, ahead, Zoning);
            if (stop.NoBay != null)
            {
                return;
            }
            Vector3 bay = lane + right * offset;
            CitySpots.Ground(bay, out float ground);
            bay.y = ground;
            FindMeters(stop, right, -behind, ahead, Zoning);
            stop.HasBay = true;
            stop.Bay = bay;
            stop.BayOffset = offset;
            stop.BayBehind = behind;
            stop.BayAhead = ahead;
            stop.BayRoom = behind;   // one bus, the mod's own: what it needs is what is laid out
            stop.Zone = Zoning.Copy(-behind, ahead, stop.KerbAside);
            stop.Zone.Lanes = Zoning.Lanes;
            stop.Zone.Cut = Zoning.Cut;
            stop.Zone.Lost = Zoning.Lost;
        }

        // Why the lane along a bay is not straight and plain, with nothing of the road between it and the kerb, or null.
        private static string LaneBends(TransitStop stop, TransitLine line, Vector3 right, float back, float on)
        {
            int[] path = line.Path;
            for (int step = -1; step <= 1; step += 2)
            {
                float run = step < 0 ? back : on;
                for (int k = stop.PathIndex + step; k >= 0 && k < path.Length && Mathf.Abs(line.Metres[k] - stop.Metres) <= run; k += step)
                {
                    Waypoint point = _points[path[k]];
                    Vector3 off = point.position - stop.Lane;
                    if (!Plain(point))
                    {
                        return "a junction";
                    }
                    if (Mathf.Abs(Vector3.Dot(off, right)) > BayStraight || Mathf.Abs(off.y) > 1f)
                    {
                        return "the lane bends";
                    }
                    if (LaneToRight(point, right))
                    {
                        return "a lane beside";
                    }
                }
            }
            return null;
        }

        // Why the strip is not level road clear to the bus's door side, the kerb keeping its line, with nothing fixed in it the
        // zone cannot take away; or null, with what the zone must switch off put in it.
        private static readonly Collider[] StripHits = new Collider[24];

        private static string StripBad(TransitStop stop, Vector3 right, float offset, float from, float to, BusZone zone)
        {
            Vector3 lane = stop.Lane;
            Vector3 along = stop.Along;
            float inner = stop.KerbAside - BusBay.KerbGap * 0.5f;
            int steps = Mathf.Max(2, Mathf.CeilToInt((to - from) / 4f));
            for (int i = 0; i <= steps; i++)
            {
                Vector3 at = lane + along * (from + (to - from) * i / steps);
                if (!CitySpots.Ground(at + right * inner, out float edgeGround) || !CitySpots.Ground(at + right * offset, out float middleGround)
                    || Mathf.Abs(edgeGround - lane.y) > BayLevel || Mathf.Abs(middleGround - lane.y) > BayLevel)
                {
                    return "not level road";
                }
                // Pavement within a step of the bus's side: the kerb comes in, or a crossing runs over the strip.
                if (CitySpots.Pavement(at + right * (stop.KerbAside - 0.45f), 0.3f, out _))
                {
                    return "the kerb comes in";
                }
            }
            float near = BusBay.LaneHalf + 0.1f;
            // Up to the bus's side, not the kerb: a lamp post on the kerb's edge is not in its way.
            float width = stop.KerbAside - BusBay.KerbGap - near;
            Vector3 centre = lane + along * ((from + to) * 0.5f) + right * (near + width * 0.5f);
            int found = Physics.OverlapBoxNonAlloc(centre + Vector3.up * 1.15f, new Vector3(width * 0.5f, 0.8f, (to - from) * 0.5f), StripHits, Quaternion.LookRotation(along),
                _staticMask, QueryTriggerInteraction.Ignore);
            string bad = found >= StripHits.Length ? "more stands in the strip than can be counted" : null;
            for (int i = 0; i < found; i++)
            {
                Collider hit = StripHits[i];
                StripHits[i] = null;
                if (bad == null && hit != null)
                {
                    bad = Standing(stop, right, hit, zone);
                }
            }
            return bad;
        }

        // The game's parking meters by a bay's kerb stand for nothing once the parking is gone: they go with the zone.
        private static void FindMeters(TransitStop stop, Vector3 right, float from, float to, BusZone zone)
        {
            if (_propsLayer < 0)
            {
                return;
            }
            float inner = stop.KerbAside - 0.3f;
            float outer = stop.KerbAside + 1.2f;
            Vector3 centre = stop.Lane + stop.Along * ((from + to) * 0.5f) + right * ((inner + outer) * 0.5f) + Vector3.up * 1.1f;
            int found = Physics.OverlapBoxNonAlloc(centre, new Vector3((outer - inner) * 0.5f, 1.0f, (to - from) * 0.5f), StripHits, Quaternion.LookRotation(stop.Along),
                1 << _propsLayer, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < found; i++)
            {
                Collider hit = StripHits[i];
                StripHits[i] = null;
                if (hit == null || !IsMeter(hit.transform))
                {
                    continue;
                }
                GameObject piece = BoardingPiece(hit);
                if (piece != null && !zone.Meters.Contains(piece) && !zone.Clear.Contains(piece))
                {
                    zone.Meters.Add(piece);
                }
            }
        }

        // The game's parking meter (Prefab_ParkingMeter), by its name or a parent's.
        private static bool IsMeter(Transform part)
        {
            for (int up = 0; up < 4 && part != null; up++, part = part.parent)
            {
                if (part.name.IndexOf("ParkingMeter", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }
            return false;
        }

        // Something fixed in a bay's strip: null if the zone deals with it (furniture in the road; a street tree, whose collider is
        // twice its trunk), else why not, naming it.
        private static string Standing(TransitStop stop, Vector3 right, Collider hit, BusZone zone)
        {
            if (hit.attachedRigidbody != null)
            {
                return null;   // a thing that moves is looked at when the bay is used (TransitBays.Free)
            }
            string what = "'" + hit.name + "' on " + LayerMask.LayerToName(hit.gameObject.layer);
            if (hit.gameObject.layer != _propsLayer)
            {
                return "a building stands in the strip: " + what;
            }
            float middle = Vector3.Dot(Flat(hit.bounds.center - stop.Lane), right);
            if (middle >= stop.KerbAside - 0.05f)
            {
                if (hit.GetComponentInParent<TreeController>() == null)
                {
                    return "something on the kerb's edge reaches into the strip: " + what;
                }
                if (!zone.Known.Contains(hit))
                {
                    zone.Known.Add(hit);
                }
                return null;
            }
            GameObject prop = BusZones.PropOf(hit);
            if (prop == null)
            {
                return "something stands in the strip that is not plain street furniture: " + what;
            }
            if (!zone.Clear.Contains(prop))
            {
                zone.Clear.Add(prop);
            }
            return null;
        }

        // How near the lane's line a bus may come at the kerb: the kerb's edge, or nearer where furniture on the pavement reaches
        // over it. A tree is taken at its trunk; not the buildings, whose boxes are no guide to their walls.
        private const float SideClear = 0.10f;   // metres
        private const float TrunkHalf = 0.15f;   // metres, half a street tree's trunk, in a collider four times as broad
        private static string _edgeWhat;         // what brought the kerb's line in, for the log
        private static readonly Collider[] EdgeHits = new Collider[24];

        private static float KerbLine(TransitStop stop, Vector3 right, float kerb)
        {
            float inner = kerb - 0.6f;
            float outer = kerb + 0.4f;
            Vector3 centre = stop.Lane + stop.Along * ((KerbLookAhead - KerbLookBehind) * 0.5f) + right * ((inner + outer) * 0.5f) + Vector3.up * 1.15f;
            int found = _propsLayer < 0 ? 0 : Physics.OverlapBoxNonAlloc(centre, new Vector3((outer - inner) * 0.5f, 0.8f, (KerbLookAhead + KerbLookBehind) * 0.5f), EdgeHits,
                Quaternion.LookRotation(stop.Along), 1 << _propsLayer, QueryTriggerInteraction.Ignore);
            float line = kerb;
            _edgeWhat = null;
            for (int i = 0; i < found; i++)
            {
                Collider hit = EdgeHits[i];
                EdgeHits[i] = null;
                if (hit == null || hit.attachedRigidbody != null)
                {
                    continue;
                }
                float middle = Vector3.Dot(Flat(hit.bounds.center - stop.Lane), right);
                if (middle < kerb - 0.05f)
                {
                    continue;   // it stands in the road: the strip's own look deals with it
                }
                float reach = Reach(hit, stop.Lane, right, middle) + BusBay.KerbGap - SideClear;
                if (reach < line)
                {
                    line = reach;
                    _edgeWhat = "'" + hit.name + "'";
                }
            }
            return line;
        }

        // Metres aside of the lane's line the nearest a thing on the pavement comes.
        private static float Reach(Collider hit, Vector3 lane, Vector3 right, float middle)
        {
            if (hit.GetComponentInParent<TreeController>() != null)
            {
                return middle - TrunkHalf;
            }
            if (hit is BoxCollider || hit is SphereCollider || hit is CapsuleCollider || (hit is MeshCollider mesh && mesh.convex))
            {
                return Vector3.Dot(Flat(hit.ClosestPoint(hit.bounds.center - right * 8f) - lane), right);
            }
            Bounds box = hit.bounds;   // a mesh that is not convex: its box, which is never smaller than it
            float least = float.MaxValue;
            for (int corner = 0; corner < 4; corner++)
            {
                var at = new Vector3((corner & 1) == 0 ? box.min.x : box.max.x, box.center.y, (corner & 2) == 0 ? box.min.z : box.max.z);
                least = Mathf.Min(least, Vector3.Dot(Flat(at - lane), right));
            }
            return least;
        }

        // The kerb's distance from the lane's line: the mesh's pavement edge, or a step in the road surface before it.
        private static float KerbAside(TransitStop stop, Vector3 right)
        {
            Vector3 lane = stop.Lane;
            float meshEdge = Vector3.Dot(Flat(stop.Kerb - lane), right);
            if (meshEdge < BusBay.KerbNeeded(TransitBus.BodyHalfWidth))
            {
                return meshEdge;   // too near for a bay as it is: the step, if any, is nearer still
            }
            float from = meshEdge - 0.5f;
            if (!CitySpots.Ground(lane + right * from, out float road) || Mathf.Abs(road - lane.y) > BayLevel)
            {
                return meshEdge;
            }
            for (float d = from + 0.05f; d < meshEdge; d += 0.05f)
            {
                if (CitySpots.Ground(lane + right * d, out float height) && height - road > 0.05f)
                {
                    return d - 0.025f;
                }
            }
            return meshEdge;
        }
    }
}
