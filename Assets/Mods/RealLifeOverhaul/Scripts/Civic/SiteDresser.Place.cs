using UnityEngine;
using UnityEngine.AI;

namespace RealLifeOverhaul
{
    /// <summary>Where a site goes: the kerb and the strip beside it for works in the road, the wall and its bays for scaffold and hoarding, the skip and the notice.</summary>
    public sealed partial class SiteDresser
    {
        private SiteRefusal Resolve(Dressed dressed)
        {
            SiteState site = dressed.Site;
            var door = new Vector3(site.X, site.Y, site.Z);
            if (!CitySpots.Pavement(door, 8f, out Vector3 pavement))
            {
                return SiteRefusal.NoPavement;
            }
            return InRoad(site.Kind) ? ResolveRoad(dressed, pavement) : ResolveWall(dressed, pavement, door, site.Kind == SiteKind.Refit);
        }

        // From the pavement towards the nearest lane: the edge of the pedestrians' mesh, the kerb (where the ground drops within
        // 0.8 m beyond that edge, else 0.3 m beyond it), the lane's direction and how far its middle is from the kerb.
        private bool Kerbside(RoadClosure road, Vector3 from, out Vector3 edge, out Vector3 kerb, out Vector3 outward, out Vector3 lane, out float laneFromKerb, ref SiteRefusal why)
        {
            edge = kerb = outward = lane = default;
            laneFromKerb = 0f;
            if (!road.Lane(from, 14f))
            {
                why = SiteRefusal.NoLane;
                return false;
            }
            lane = road.LaneDirection;
            Vector3 toLane = road.LaneCentre - from;
            toLane.y = 0f;
            float distance = toLane.magnitude;
            if (distance < 2f || distance > 13f)
            {
                why = SiteRefusal.NoLane;
                return false;
            }
            outward = toLane / distance;
            if (!NavMesh.Raycast(from, from + outward * distance, out NavMeshHit hit, _pavement))
            {
                why = SiteRefusal.NoKerb;   // the pavement runs on to the lane with no edge between: no kerb to work at
                return false;
            }
            edge = hit.position;
            float beyond = 0.3f;
            for (float step = 0.1f; step <= 0.8f; step += 0.1f)
            {
                if (CitySpots.Ground(edge + outward * step, out float ground) && ground < edge.y - 0.05f)
                {
                    beyond = step - 0.05f;
                    break;
                }
            }
            kerb = edge + outward * beyond;
            laneFromKerb = distance - hit.distance - beyond;
            if (laneFromKerb < 1.5f)
            {
                why = SiteRefusal.NoKerb;   // the lane's middle is on the kerb: the survey is wrong here
                return false;
            }
            return true;
        }

        private SiteRefusal ResolveRoad(Dressed dressed, Vector3 pavement)
        {
            if (!TrafficTools.Street)
            {
                return SiteRefusal.NoLane;
            }
            if (!CitySpots.Along(pavement, out Vector3 run))
            {
                return SiteRefusal.NoPavement;
            }
            float half = dressed.Site.Kind == SiteKind.Roadworks ? HalfTrench : 1.2f;
            SiteRefusal why = SiteRefusal.NoLane;
            for (int attempt = 0; attempt < 5; attempt++)
            {
                float shift = attempt == 0 ? 0f : (attempt % 2 == 1 ? 1f : -1f) * 7f * ((attempt + 1) / 2);
                if (!CitySpots.Pavement(pavement + run * shift, 2f, out Vector3 here)
                    || !Kerbside(dressed.Road, here, out Vector3 edge, out Vector3 kerb, out Vector3 outward, out Vector3 lane, out float laneFromKerb, ref why))
                {
                    continue;
                }
                Vector3 strip = kerb + outward * StripCentre;
                if (CitySpots.Ground(strip, out float ground))
                {
                    strip.y = ground;
                }
                if (!CivicSpots.BoxFree(strip + Vector3.up * 0.75f, new Vector3(0.8f, 0.5f, half + 0.6f), Quaternion.LookRotation(lane), CivicSpots.StandingVehicles))
                {
                    why = SiteRefusal.Vehicles;
                    continue;
                }
                if (!CitySpots.Stand(edge - outward * 0.08f - lane * Mathf.Min(1.7f, half), 0.38f, out _))
                {
                    why = SiteRefusal.NoRoom;
                    continue;
                }
                dressed.Origin = strip;
                dressed.Out = outward;
                dressed.Along = lane;
                dressed.Kerb = kerb;
                dressed.Edge = edge;
                dressed.LaneFromKerb = laneFromKerb;
                Notice(dressed, edge - outward * 0.9f + lane * (half + 2f), -lane, edge - outward * 0.9f - lane * (half + 2f), lane);
                return SiteRefusal.None;
            }
            return why;
        }

        // The wall the building's door is in, and the bays that fit along it.
        private SiteRefusal ResolveWall(Dressed dressed, Vector3 pavement, Vector3 door, bool hoarding)
        {
            Vector3 toDoor = door - pavement;
            toDoor.y = 0f;
            if (toDoor.sqrMagnitude < 0.04f)
            {
                return SiteRefusal.NoWall;
            }
            toDoor.Normalize();
            RaycastHit wall = default;
            bool found = false;
            Vector3 side = Vector3.Cross(Vector3.up, toDoor);
            for (int attempt = 0; attempt < 5 && !found; attempt++)
            {
                float shift = attempt == 0 ? 0f : (attempt % 2 == 1 ? 1f : -1f) * 1.6f * ((attempt + 1) / 2);
                found = CivicSpots.Wall(pavement + side * shift, toDoor, 1.3f, 10f, out wall) && Mathf.Abs(wall.normal.y) < 0.2f;
            }
            if (!found)
            {
                return SiteRefusal.NoWall;
            }
            Vector3 outward = wall.normal;
            outward.y = 0f;
            outward.Normalize();
            Vector3 along = Vector3.Cross(Vector3.up, outward);
            Vector3 foot = wall.point;
            foot.y = pavement.y;

            // Bays either side of the first, kept in one run.
            float depth = hoarding ? 0.6f : BayDepth;
            if (!Bay(foot, outward, along, 0, depth, hoarding))
            {
                // The first bay is where the door is, as often as not: start one bay along.
                if (Bay(foot + along * BayWidth, outward, along, 0, depth, hoarding))
                {
                    foot += along * BayWidth;
                }
                else if (Bay(foot - along * BayWidth, outward, along, 0, depth, hoarding))
                {
                    foot -= along * BayWidth;
                }
                else
                {
                    return SiteRefusal.NoRoom;
                }
            }
            int min = 0, max = 0;
            bool growUp = true, growDown = true;
            while (max - min + 1 < MaxBays && (growUp || growDown))
            {
                if (growUp)
                {
                    growUp = Bay(foot, outward, along, max + 1, depth, hoarding);
                    if (growUp)
                    {
                        max++;
                    }
                }
                if (growDown && max - min + 1 < MaxBays)
                {
                    growDown = Bay(foot, outward, along, min - 1, depth, hoarding);
                    if (growDown)
                    {
                        min--;
                    }
                }
            }
            if (!hoarding && max - min + 1 < 2)
            {
                return SiteRefusal.NoRoom;
            }
            int lifts = 0;
            if (!hoarding)
            {
                for (int k = 1; k <= MaxLifts; k++)
                {
                    float height = DeckHeight + (k - 1) * LiftHeight + 1f;
                    if (!CivicSpots.Wall(foot + outward * 2.2f, -outward, height, 3f, out RaycastHit high) || high.distance < 1.7f || high.distance > 2.8f)
                    {
                        break;
                    }
                    lifts = k;
                }
                if (lifts == 0)
                {
                    return SiteRefusal.NoWall;
                }
            }
            dressed.Origin = foot + along * ((min + max) * 0.5f * BayWidth) + outward * 0.9f;
            dressed.Kerb = foot;
            dressed.Out = outward;
            dressed.Along = along;
            dressed.BayMin = min;
            dressed.BayMax = max;
            dressed.Lifts = lifts;

            float low = min * BayWidth - BayWidth * 0.5f, top = max * BayWidth + BayWidth * 0.5f;
            Notice(dressed, foot + along * (low - 1.1f) + outward * 0.45f, outward, foot + along * (top + 1.1f) + outward * 0.45f, outward);
            Skip(dressed, foot + along * ((min + max) * 0.5f * BayWidth) + outward * 0.8f);
            return SiteRefusal.None;
        }

        // The skip: in the carriageway beside the kerb in front of the works, where there is a strip between the kerb and the
        // lane wide enough for it (1.7 m, and a car's width clear of the lane's middle) and nothing parked in it. Else there is none.
        private void Skip(Dressed dressed, Vector3 front)
        {
            dressed.HasSkip = false;
            SiteRefusal ignored = SiteRefusal.None;
            if (!TrafficTools.Ready || !CitySpots.Pavement(front, 1.5f, out Vector3 from)
                || !Kerbside(_probe, from, out _, out Vector3 kerb, out Vector3 outward, out Vector3 lane, out float laneFromKerb, ref ignored) || laneFromKerb < 3.1f)
            {
                return;
            }
            Vector3 centre = kerb + outward * 0.95f;
            if (CitySpots.Ground(centre, out float ground))
            {
                centre.y = ground;
            }
            if (CivicSpots.BoxFree(centre + Vector3.up * 0.8f, new Vector3(0.95f, 0.6f, 1.9f), Quaternion.LookRotation(lane), CivicSpots.Solid))
            {
                dressed.HasSkip = true;
                dressed.SkipAt = centre;
                dressed.SkipAlong = lane;
            }
        }

        // One bay of 2.4 m at an index along the wall: a flat wall behind it, pavement under it, no door where a post would stand, nothing in it.
        private static bool Bay(Vector3 foot, Vector3 outward, Vector3 along, int index, float depth, bool hoarding)
        {
            Vector3 centre = foot + along * (index * BayWidth);
            for (int ray = -1; ray <= 1; ray++)
            {
                if (!CivicSpots.Wall(centre + along * (ray * 1.0f) + outward * 1.2f, -outward, 1.3f, 1.7f, out RaycastHit hit)
                    || hit.distance < 0.95f || hit.distance > 1.5f || Vector3.Dot(hit.normal, outward) < 0.85f)
                {
                    return false;
                }
            }
            if (!CitySpots.OnPavement(centre + outward * (depth * 0.5f), out _) || !CitySpots.OnPavement(centre + outward * depth, out _))
            {
                return false;
            }
            if (hoarding)
            {
                if (CivicSpots.DoorNear(centre + outward * 0.4f, 1.7f))
                {
                    return false;   // a hoarding never stands across a door
                }
            }
            else if (CivicSpots.DoorNear(centre + along * 1.18f + outward * 0.3f, 0.9f) || CivicSpots.DoorNear(centre - along * 1.18f + outward * 0.3f, 0.9f))
            {
                return false;       // a standard would stand in a doorway
            }
            return CivicSpots.BoxFree(centre + outward * (WallGap + depth * 0.5f + 0.1f) + Vector3.up * 1.25f, new Vector3(1.15f, 0.9f, depth * 0.5f - 0.12f), Quaternion.LookRotation(outward),
                CivicSpots.Solid & ~CivicSpots.Walls);
        }

        // The notice board: at the first of two places that has room for it and for whoever reads it.
        private static void Notice(Dressed dressed, Vector3 first, Vector3 firstFace, Vector3 second, Vector3 secondFace)
        {
            dressed.HasNotice = false;
            for (int i = 0; i < 2; i++)
            {
                Vector3 at = i == 0 ? first : second;
                Vector3 face = i == 0 ? firstFace : secondFace;
                if (CitySpots.Stand(at, 0.5f, out Vector3 board) && CitySpots.Stand(at + face * 0.9f, 0.4f, out Vector3 reader))
                {
                    dressed.HasNotice = true;
                    dressed.NoticeAt = board;
                    dressed.NoticeFace = face;
                    dressed.Reader = reader;
                    return;
                }
            }
        }
    }
}
