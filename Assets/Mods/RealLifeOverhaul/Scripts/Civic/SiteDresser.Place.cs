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
            if (site.Kind == SiteKind.Plot)
            {
                return ResolvePlot(dressed);   // ground given: no door, no pavement asked for
            }
            if (!CitySpots.Pavement(door, 8f, out Vector3 pavement))
            {
                return SiteRefusal.NoPavement;
            }
            if (InRoad(site.Kind))
            {
                return ResolveRoad(dressed, pavement);
            }
            SiteRefusal why = ResolveWall(dressed, pavement, door, site.Kind != SiteKind.Scaffold);
            return why == SiteRefusal.None && site.Kind == SiteKind.Rebuild ? ResolveWrap(dressed) : why;
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

        // The wall the building's door is in, and the bays that fit along it. The wall is the building's own: the face of its
        // street-level box nearest the door, read from its colliders (BuildingShell), never looked for along the line from
        // the pavement to the door. A door marker can stand on the pedestrians' walkable mesh (36 5th Avenue's is 0.30 m in
        // front of its wall; the mesh keeps 0.25 m off walls), which leaves that line no length, and a building faded for the
        // camera has its colliders off the layers a ray looks at.
        private SiteRefusal ResolveWall(Dressed dressed, Vector3 pavement, Vector3 door, bool hoarding)
        {
            SiteState site = dressed.Site;
            CityBuildingController building = CivicCity.Find(site.Street, site.Number);
            if (!BuildingShell.Read(building, _shell, out Vector3 origin, out Quaternion turn))
            {
                return SiteRefusal.NoWall;
            }
            Vector3 local = BuildingShell.Local(origin, turn, door);
            int side = WrapRules.SideOf(_shell, local.x, local.z);
            ShellSide face = _shell.Sides[side];
            WrapRules.Axes(side, out float outX, out float outZ, out float runX, out float runZ);
            Vector3 outward = BuildingShell.Direction(turn, outX, outZ);
            Vector3 along = BuildingShell.Direction(turn, runX, runZ);
            float doorAlong = WrapRules.Along(side, local.x, local.z);
            WrapRules.Point(side, face.Ground, doorAlong, out float footX, out float footZ);
            Vector3 foot = BuildingShell.World(origin, turn, footX, 0f, footZ);
            foot.y = pavement.y;
            float spanFrom = face.GroundMin - doorAlong, spanTo = face.GroundMax - doorAlong;   // the face's ends, from the foot

            // Bays either side of the first, kept in one run.
            float depth = hoarding ? 0.6f : BayDepth;
            if (!Bay(foot, outward, along, 0, depth, hoarding, spanFrom, spanTo))
            {
                // The first bay is where the door is, as often as not: start one bay along.
                if (Bay(foot + along * BayWidth, outward, along, 0, depth, hoarding, spanFrom - BayWidth, spanTo - BayWidth))
                {
                    foot += along * BayWidth;
                    spanFrom -= BayWidth;
                    spanTo -= BayWidth;
                }
                else if (Bay(foot - along * BayWidth, outward, along, 0, depth, hoarding, spanFrom + BayWidth, spanTo + BayWidth))
                {
                    foot -= along * BayWidth;
                    spanFrom += BayWidth;
                    spanTo += BayWidth;
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
                    growUp = Bay(foot, outward, along, max + 1, depth, hoarding, spanFrom, spanTo);
                    if (growUp)
                    {
                        max++;
                    }
                }
                if (growDown && max - min + 1 < MaxBays)
                {
                    growDown = Bay(foot, outward, along, min - 1, depth, hoarding, spanFrom, spanTo);
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
                // A lift and its guard rail stay below the roof and below any floor that stands out over the street-level wall.
                float ceiling = Mathf.Min(_shell.Top, face.ProudFrom);
                for (int k = 1; k <= MaxLifts && DeckHeight + (k - 1) * LiftHeight + 1f <= ceiling; k++)
                {
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

        // One bay of 2.4 m at an index along the wall: within the wall's own length, pavement under it, no door where a post
        // would stand, nothing in it. The wall itself is the building's box (BuildingShell): flat by what it is.
        private static bool Bay(Vector3 foot, Vector3 outward, Vector3 along, int index, float depth, bool hoarding, float spanFrom, float spanTo)
        {
            float middle = index * BayWidth;
            if (middle - BayWidth * 0.5f < spanFrom - 0.05f || middle + BayWidth * 0.5f > spanTo + 0.05f)
            {
                return false;   // past the end of the wall: the next house's front, or the corner
            }
            Vector3 centre = foot + along * middle;
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
