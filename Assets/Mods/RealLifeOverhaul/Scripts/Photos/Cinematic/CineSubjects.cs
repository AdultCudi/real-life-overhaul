using System;
using System.Collections.Generic;
using GleyTrafficSystem;
using Helpers;
using RealLifeOverhaul.Economy;
using Streets.Pedestrians;
using UnityEngine;

namespace RealLifeOverhaul.Photos
{
    /// <summary>A subject looked for: whether it is there, why not, its place and size for the camera's sums, and its name for the list.</summary>
    internal struct CineFound
    {
        public bool There;
        public string Why;        // for a subject not there
        public CineBox Box;
        public string Name;       // what it is, in plain words, for shots.txt
        public int Roofs;         // the skyline's roofs to stand on, the best first (CineSubjects.Roof)
    }

    /// <summary>
    /// What a cinematic shot is of, found in the city as it stands when the shot comes up, never at a place written down:
    /// the player and their car, the walkers and the trees the game lists, the mod's places on the map rows and the
    /// buildings that stand at them, the bus, a stall, a scene that is on, the nearest shopfront, the street by the traffic
    /// system's own lanes (the middle of its carriageway) and the roofs by rays cast from above. One look is a pass over a
    /// list of the game's and some tens of rays, on a press with the game paused; nothing per frame.
    /// </summary>
    internal static class CineSubjects
    {
        private const float FrontageReach = 55f;      // metres: a shop's sign is lit within the game's first band, 60 m of the player
        private const float FrontHalf = 7f;           // metres of a front either side of its door
        private const float LookOver = 3f;            // metres over the street the runs are measured at: over the cars, under the signs
        private const float SideMost = 45f;           // metres a street's wall is looked for to either side
        private const float RoofFrom = 260f;          // metres over the street a roof is looked for from
        private const float RoofStep = 2f;            // metres walked at a time toward a roof's edge
        private const int RoofSteps = 24;
        private const float RoofFlat = 1.2f;          // metres a roof may rise or fall and still be the same roof
        private const float EdgeBack = 0.8f;          // metres the tripod stands back from the edge
        private const int RenderersMost = 240;        // of a building, to measure it by
        private const int BuildingsMost = 4096;       // of the city, to find where most of it stands
        private const float LaneReach = 45f;          // metres a lane is looked for from the player
        private const float LaneAlong = 5f;           // metres along a street within which its lanes are one cross section
        private const float LaneAcross = 24f;         // metres to either side a carriageway's lanes are looked for
        private const float PlaceRadius = 14f;        // metres round a place on the map its building is looked for

        private static readonly string[] Landmarks = { "landmarks", "sights", "courthouse", "university", "tech_hq", "schools", "utilities_office" };
        private static readonly string[] Stations = { "police", "hotels" };
        private static readonly float[] Stations25 = { 0f, 25f, -25f, 50f, -50f };   // metres along a street its cross sections are tried at

        private static readonly float[] Xs = new float[BuildingsMost], Zs = new float[BuildingsMost];
        private static readonly Vector3[] Points = new Vector3[CineRules.ClusterMost];
        private static readonly Vector3[] Ways = new Vector3[CineRules.ClusterMost];
        private static readonly float[] Runs = new float[CineRules.Bearings];
        private static readonly float[] Across = new float[64];
        private static readonly Vector3[] RoofAt = new Vector3[CineRules.Stands];
        private static readonly float[] RoofWorth = new float[CineRules.Stands];
        private static readonly List<Renderer> Renderers = new List<Renderer>(RenderersMost);
        private static readonly Collider[] Near = new Collider[32];
        private static readonly Predicate<Waypoint> PlainLane = RoadGraph.Plain;
        private static int _trees;   // how many of Points are the trees of the park found last

        /// <summary>Looks for a subject now. A fault in one look is that subject not found, said in the log; it never ends the run.</summary>
        public static CineFound Find(CineSubject subject)
        {
            var found = default(CineFound);
            try
            {
                PlayerController player = PlayerHelper.PlayerController;
                if (player == null)
                {
                    found.Why = "the player is not there";
                    return found;
                }
                Vector3 from = player.transform.position;
                switch (subject)
                {
                    case CineSubject.Player: Player(ref found); break;
                    case CineSubject.Car: Car(from, ref found); break;
                    case CineSubject.Walkers: Walkers(from, ref found); break;
                    case CineSubject.Trees: Trees(from, ref found); break;
                    case CineSubject.Avenue: Avenue(from, ref found); break;
                    case CineSubject.Skyline: Skyline(from, ref found); break;
                    case CineSubject.Landmark: OnMap(from, Landmarks, "no landmark or sight of the mod's map rows with a building to it within " + Metres(CineRules.Reach), ref found); break;
                    case CineSubject.Bus: Bus(from, ref found); break;
                    case CineSubject.Stall: Stall(from, ref found); break;
                    case CineSubject.Incident: Incident(from, ref found); break;
                    case CineSubject.Frontage: Frontage(from, ref found); break;
                    case CineSubject.Station: OnMap(from, Stations, "no police station or hotel of the mod's within " + Metres(CineRules.Reach), ref found); break;
                    default: found.Why = "not a subject"; break;
                }
            }
            catch (Exception e)
            {
                CineGame.Logger?.Error(e);
                found.There = false;
                found.Why = "a fault while looking (" + e.GetType().Name + ")";
            }
            return found;
        }

        /// <summary>A roof of the skyline found last, the best first: where the tripod stands at its edge.</summary>
        public static Vector3 Roof(int index)
        {
            return index >= 0 && index < RoofAt.Length ? RoofAt[index] : Vector3.zero;
        }

        /// <summary>
        /// How many trees of the park found last the lens sees from a place: before it, inside the frame, between 4 m and
        /// 90 m off, with no building between. A ray a tree, on a press.
        /// </summary>
        public static int TreesSeen(Vector3 eye, Quaternion turn, float tanWide, float tanHigh)
        {
            Quaternion back = Quaternion.Inverse(turn);
            int seen = 0;
            for (int i = 0; i < _trees; i++)
            {
                Vector3 crown = Points[i] + Vector3.up * 4f;
                Vector3 local = back * (crown - eye);
                if (local.z < 4f || local.z > 90f || Mathf.Abs(local.x) > local.z * tanWide * 0.95f || Mathf.Abs(local.y) > local.z * tanHigh * 0.95f)
                {
                    continue;
                }
                seen += Physics.Linecast(eye, crown, CineGame.Walls, QueryTriggerInteraction.Ignore) ? 0 : 1;
            }
            return seen;
        }

        private static string Metres(float metres)
        {
            return metres.ToString("0") + " m";
        }

        private static Vector3 Flat(Vector3 way)
        {
            way.y = 0f;
            return way.sqrMagnitude > 0.0001f ? way.normalized : Vector3.forward;
        }

        private static bool Near2(Vector3 from, Vector3 at, float within)
        {
            float dx = at.x - from.x, dz = at.z - from.z;
            return dx * dx + dz * dz <= within * within;
        }

        private static void Set(ref CineFound found, Vector3 centre, Vector3 facing, float halfLength, float halfWidth, float height, string name)
        {
            Vector3 way = Flat(facing);
            found.There = true;
            found.Why = null;
            found.Name = name;
            found.Box = new CineBox { X = centre.x, Y = centre.y, Z = centre.z, Fx = way.x, Fz = way.z, HalfLength = halfLength, HalfWidth = halfWidth, Height = height };
        }

        // ------------------------------------------------------------------ the player and their car

        private static void Player(ref CineFound found)
        {
            if (PlayerHelper.IsUsingVehicle)
            {
                found.Why = "the player is in a vehicle";
                return;
            }
            if (!PhotosGame.Body(out Transform root, out Transform head))
            {
                found.Why = "the player's body or head was not found";
                return;
            }
            Set(ref found, root.position, root.forward, 0.3f, 0.3f, Mathf.Clamp(head.position.y - root.position.y + 0.14f, 1.2f, 2.2f), "the player");
        }

        // The car being driven, else the nearest of the player's own that stands in the city, parked with its engine off or not.
        private static void Car(Vector3 from, ref CineFound found)
        {
            GameManager manager = InstanceBehavior<GameManager>.Instance;
            CarController car = manager != null ? manager.selectedVehicle as CarController : null;
            int cars = 0;
            float nearest = float.MaxValue;
            if (car == null)
            {
                float best = CineRules.Reach * CineRules.Reach;
                List<VehicleController> own = VehicleHelper.AllPlayerVehicles;
                for (int i = 0; own != null && i < own.Count; i++)
                {
                    if (!(own[i] is CarController candidate) || !candidate.gameObject.activeInHierarchy || !GoingPublic.MineVehicle(candidate))
                    {
                        continue;
                    }
                    cars++;
                    float apart = (candidate.transform.position - from).sqrMagnitude;
                    nearest = Mathf.Min(nearest, apart);
                    if (apart < best)
                    {
                        best = apart;
                        car = candidate;
                    }
                }
            }
            if (car == null)
            {
                found.Why = cars == 0
                    ? "no car of the player's stands in the city (the game lists none out of a building)"
                    : "no car of the player's within " + Metres(CineRules.Reach) + ": the nearest of " + cars + " is " + Metres(Mathf.Sqrt(nearest)) + " away";
                return;
            }
            Transform body = car.transform;
            Vector3 centre = body.position;
            float halfLength = 2.2f, halfWidth = 0.9f, top = 1.45f;   // a saloon, where the game's body collider cannot be read
            if (VehicleHelper.TryGetBodyColliderBounds(body, out Bounds local, out Collider shell) && shell != null)
            {
                Vector3 scale = shell.transform.lossyScale;
                halfLength = Mathf.Clamp(local.extents.z * Mathf.Abs(scale.z), 1.2f, 5f);
                halfWidth = Mathf.Clamp(local.extents.x * Mathf.Abs(scale.x), 0.6f, 1.6f);
                centre = shell.transform.TransformPoint(local.center);
                top = shell.transform.TransformPoint(local.center + Vector3.up * local.extents.y).y;
            }
            float ground = CineGame.Ground(centre, out float height) ? height : body.position.y;
            top = top > ground + 0.5f ? Mathf.Clamp(top - ground, 1f, 3.2f) : 1.45f;
            centre.y = ground;
            bool driven = car == (manager != null ? manager.selectedVehicle : null);
            Set(ref found, centre, body.forward, halfLength, halfWidth, top, driven ? "the car the player drives" : "a car of the player's, parked");
        }

        // ------------------------------------------------------------------ what the game lists: walkers and trees

        private static void Walkers(Vector3 from, ref CineFound found)
        {
            CityManager city = InstanceBehavior<CityManager>.Instance;
            List<Pedestrian> walkers = city != null && city.pedestrianSpawner != null ? city.pedestrianSpawner.activePedestrians : null;
            int count = 0;
            for (int i = 0; walkers != null && i < walkers.Count && count < CineRules.ClusterMost; i++)
            {
                Pedestrian walker = walkers[i];
                if (walker == null || !walker.gameObject.activeInHierarchy || !Near2(from, walker.transform.position, CineRules.Reach))
                {
                    continue;
                }
                Points[count] = walker.transform.position;
                Ways[count] = walker.transform.forward;
                Xs[count] = Points[count].x;
                Zs[count] = Points[count].z;
                count++;
            }
            _trees = 0;   // Points are walkers now
            if (!CineRules.Densest(Xs, Zs, count, CineRules.WalkerRadius, CineRules.WalkersLeast, out int centre, out int size))
            {
                found.Why = "fewer than " + CineRules.WalkersLeast + " walkers within " + Metres(CineRules.WalkerRadius) + " of each other (" + count + " walkers within "
                            + Metres(CineRules.Reach) + ")";
                return;
            }
            Set(ref found, Points[centre], Ways[centre], 0.3f, 0.3f, 1.8f, size + " walkers on one stretch of pavement");
        }

        private static void Trees(Vector3 from, ref CineFound found)
        {
            List<TreeController> trees = TreeController.AllTrees;
            int count = 0;
            for (int i = 0; trees != null && i < trees.Count && count < CineRules.ClusterMost; i++)
            {
                TreeController tree = trees[i];
                if (tree == null || !tree.gameObject.activeInHierarchy || !Near2(from, tree.transform.position, CineRules.Reach))
                {
                    continue;
                }
                Points[count] = tree.transform.position;
                Xs[count] = Points[count].x;
                Zs[count] = Points[count].z;
                count++;
            }
            _trees = count;
            if (!CineRules.Densest(Xs, Zs, count, CineRules.TreeRadius, CineRules.TreesLeast, out int centre, out int size))
            {
                found.Why = "fewer than " + CineRules.TreesLeast + " trees within " + Metres(CineRules.TreeRadius) + " of each other (" + count + " trees within "
                            + Metres(CineRules.Reach) + ")";
                return;
            }
            Vector3 middle = Points[centre];
            if (CineGame.Ground(middle, out float ground))
            {
                middle.y = ground;
            }
            // First from the player's side, where there is a way in; the camera then goes round them for open ground.
            Set(ref found, middle, Near2(from, middle, 3f) ? Vector3.forward : from - middle, 9f, 9f, 9f, size + " trees together");
        }

        // ------------------------------------------------------------------ the street: the middle of its carriageway

        private static Vector3 Bearing(int index)
        {
            float turn = index * 2f * Mathf.PI / CineRules.Bearings;
            return new Vector3(Mathf.Sin(turn), 0f, Mathf.Cos(turn));
        }

        private static float Run(Vector3 over, Vector3 way)
        {
            return Physics.Raycast(over, way, out RaycastHit hit, CineRules.AvenueMost, CineGame.Walls, QueryTriggerInteraction.Ignore) ? hit.distance : CineRules.AvenueMost;
        }

        // A cross section of a street as a place for the camera.
        private struct Street
        {
            public float Worth, Run, Half;
            public Vector3 Middle, Way;
            public int Walls;   // how many of its two sides have a wall within reach
        }

        // The street nearest the player, by the traffic system's own lanes, and at a corner the street that crosses it: at a
        // few cross sections along each, the middle of the carriageway is half way between its outermost lanes. The one
        // taken has walls on both sides if any has, then the longest clear run, and the lens looks down that run. Where the
        // city has no lanes to ask, the longest run between walls from where the player stands.
        private static void Avenue(Vector3 from, ref CineFound found)
        {
            Waypoint[] lanes = TrafficManager.HasInstance ? RoadData.Points : null;
            Waypoint lane = lanes != null ? TrafficTools.ClosestWaypoint(from, LaneReach, PlainLane) : null;
            if (lane == null || !Events.EventRoad.Direction(lane, out Vector3 along))
            {
                AvenueByWalls(from, ref found);
                return;
            }
            var best = default(Street);
            Sections(lanes, lane.position, along, ref best);
            Waypoint crossing = Crossing(lanes, from, along);
            if (crossing != null && Events.EventRoad.Direction(crossing, out Vector3 across))
            {
                Sections(lanes, crossing.position, across, ref best);
            }
            if (best.Worth <= 0f)
            {
                found.Why = "no street near the player has a clear run of " + Metres(CineRules.AvenueLeast) + " from the middle of its carriageway";
                return;
            }
            Set(ref found, best.Middle, best.Way, 0f, best.Half, 0f, "a street " + Metres(best.Run) + " long, from the middle of its carriageway (" + Metres(best.Half * 2f) + " wide), "
                + (best.Walls == 2 ? "walls on both sides" : best.Walls == 1 ? "a wall on one side only" : "open on both sides"));
        }

        // The cross sections of one street, each judged: two walled sides first (300 each, more than any run is worth), then the run.
        private static void Sections(Waypoint[] lanes, Vector3 start, Vector3 along, ref Street best)
        {
            var right = new Vector3(along.z, 0f, -along.x);
            for (int s = 0; s < Stations25.Length; s++)
            {
                Vector3 station = start + along * Stations25[s];
                int count = 0;
                for (int i = 0; i < lanes.Length && count < Across.Length; i++)
                {
                    Waypoint point = lanes[i];
                    Vector3 apart = point.position - station;
                    float ahead = apart.x * along.x + apart.z * along.z, beside = apart.x * right.x + apart.z * right.z;
                    if (Mathf.Abs(ahead) > LaneAlong || Mathf.Abs(beside) > LaneAcross || Mathf.Abs(apart.y) > 4f || !RoadGraph.Plain(point))
                    {
                        continue;
                    }
                    Across[count++] = beside;
                }
                if (!CineJudge.Carriageway(Across, count, out float mid, out float half))
                {
                    continue;   // a junction, or the street has ended
                }
                Vector3 middle = station + right * mid;
                if (CineGame.Ground(middle, out float ground))
                {
                    middle.y = ground;
                }
                Vector3 over = middle + Vector3.up * LookOver;
                float forward = Run(over, along), backward = Run(over, -along);
                bool onward = forward >= backward;
                float run = onward ? forward : backward, behind = onward ? backward : forward;
                if (run < CineRules.AvenueLeast)
                {
                    continue;
                }
                int walls = (Physics.Raycast(over, -right, SideMost, CineGame.Walls, QueryTriggerInteraction.Ignore) ? 1 : 0)
                            + (Physics.Raycast(over, right, SideMost, CineGame.Walls, QueryTriggerInteraction.Ignore) ? 1 : 0);
                float worth = walls * 300f + Mathf.Min(run, CineRules.AvenueMost) + 0.25f * Mathf.Min(behind, CineRules.AvenueMost);
                if (worth > best.Worth)
                {
                    best = new Street { Worth = worth, Run = run, Half = half, Middle = middle, Way = onward ? along : -along, Walls = walls };
                }
            }
        }

        // At a corner: the nearest lane of a road proper that runs across the first street's.
        private static Waypoint Crossing(Waypoint[] lanes, Vector3 from, Vector3 along)
        {
            Waypoint best = null;
            float bestApart = LaneReach * LaneReach;
            for (int i = 0; i < lanes.Length; i++)
            {
                Waypoint point = lanes[i];
                float dx = point.position.x - from.x, dz = point.position.z - from.z;
                float apart = dx * dx + dz * dz;
                if (apart >= bestApart || !RoadGraph.Plain(point) || !Events.EventRoad.Direction(point, out Vector3 way) || Mathf.Abs(Vector3.Dot(way, along)) > 0.5f)
                {
                    continue;
                }
                bestApart = apart;
                best = point;
            }
            return best;
        }

        private static void AvenueByWalls(Vector3 from, ref CineFound found)
        {
            Vector3 over = from + Vector3.up * LookOver;
            float longest = 0f;
            for (int b = 0; b < CineRules.Bearings; b++)
            {
                Runs[b] = Run(over, Bearing(b));
                longest = Mathf.Max(longest, Runs[b]);
            }
            if (!CineRules.Avenue(Runs, CineRules.Bearings, CineRules.AvenueLeast, out int best))
            {
                found.Why = "no lane of the traffic system near the player, and no straight street of " + Metres(CineRules.AvenueLeast) + " between walls (the longest run is "
                            + Metres(longest) + ")";
                return;
            }
            Vector3 along = Bearing(best);
            var right = new Vector3(along.z, 0f, -along.x);
            bool wallLeft = Physics.Raycast(over, -right, out RaycastHit left, SideMost, CineGame.Walls, QueryTriggerInteraction.Ignore);
            bool wallRight = Physics.Raycast(over, right, out RaycastHit rightHit, SideMost, CineGame.Walls, QueryTriggerInteraction.Ignore);
            bool both = wallLeft && wallRight;
            Vector3 middle = both ? from + right * ((rightHit.distance - left.distance) * 0.5f) : from;
            if (CineGame.Ground(middle, out float ground))
            {
                middle.y = ground;
            }
            float half = both ? (left.distance + rightHit.distance) * 0.5f : 8f;
            Set(ref found, middle, along, 0f, half, 0f, "a street " + Metres(Runs[best]) + " long and " + Metres(half * 2f) + " wide between its walls (no lanes to find its middle by)");
        }

        // ------------------------------------------------------------------ the skyline: toward the city, from a roof's edge

        // The way with the most buildings in view, so the lens never looks off the map's edge; then roofs behind the player,
        // each walked to its edge on that side, so nothing of the roof stands before the lens.
        private static void Skyline(Vector3 from, ref CineFound found)
        {
            CityManager city = InstanceBehavior<CityManager>.Instance;
            CityBuildingController[] all = city != null ? GameDlc.Open(city) : null;
            int count = 0;
            for (int i = 0; all != null && i < all.Length && count < BuildingsMost; i++)
            {
                if (all[i] != null)
                {
                    Vector3 apart = all[i].transform.position - from;
                    Xs[count] = apart.x;
                    Zs[count] = apart.z;
                    count++;
                }
            }
            if (!CineJudge.DensestBearing(Xs, Zs, count, CineRules.Bearings, out int best, out int seen))
            {
                found.Why = "no way to look with a dozen buildings in view (" + count + " buildings in the city's list)";
                return;
            }
            Vector3 toward = Bearing(best);
            var right = new Vector3(toward.z, 0f, -toward.x);
            float street = CineGame.Ground(from, out float ground) ? ground : from.y;
            int roofs = 0;
            for (int i = 0; i < CineRules.Stands; i++)
            {
                CineRules.Stand(i, out float back, out float sideways);
                Vector3 stand = from - toward * back + right * sideways;
                stand.y = street + RoofFrom;
                if (!Physics.Raycast(stand, Vector3.down, out RaycastHit roof, RoofFrom + 40f, CineGame.Walls | CineGame.GroundMask, QueryTriggerInteraction.Ignore))
                {
                    continue;
                }
                float worth = CineRules.RoofWorth(roof.point.y - street);
                if (worth <= 0f)
                {
                    continue;
                }
                Vector3 edge = Edge(roof.point, toward);
                Vector3 tripod = edge + Vector3.up * CineRules.EyeOverRoof;
                if (Physics.Raycast(tripod, toward, CineRules.RoofView, CineGame.Walls, QueryTriggerInteraction.Ignore))
                {
                    continue;   // a taller wall before it: no view
                }
                int at = roofs;
                while (at > 0 && RoofWorth[at - 1] < worth)   // the best first
                {
                    RoofAt[at] = RoofAt[at - 1];
                    RoofWorth[at] = RoofWorth[at - 1];
                    at--;
                }
                RoofAt[at] = edge;
                RoofWorth[at] = worth;
                roofs++;
            }
            if (roofs == 0)
            {
                found.Why = "no roof between " + Metres(CineRules.RoofLeast) + " and " + Metres(CineRules.RoofMost) + " high behind the player with " + Metres(CineRules.RoofView)
                            + " of open air toward the city";
                return;
            }
            Set(ref found, new Vector3(from.x, street, from.z), toward, 0f, 0f, 0f,
                "the city from a roof's edge " + Metres(RoofAt[0].y - street) + " over the street, toward " + seen + " buildings");
            found.Roofs = roofs;
        }

        // A roof walked toward the view until it ends: the last place still on it, a step back from the edge.
        private static Vector3 Edge(Vector3 onRoof, Vector3 toward)
        {
            Vector3 last = onRoof;
            for (int step = 1; step <= RoofSteps; step++)
            {
                Vector3 over = onRoof + toward * (RoofStep * step) + Vector3.up * 3f;
                if (!Physics.Raycast(over, Vector3.down, out RaycastHit under, 3f + RoofFlat, CineGame.Walls | CineGame.GroundMask, QueryTriggerInteraction.Ignore)
                    || Mathf.Abs(under.point.y - onRoof.y) > RoofFlat)
                {
                    break;
                }
                last = under.point;
            }
            Vector3 edge = last - toward * EdgeBack;
            edge.y = onRoof.y;
            return edge;
        }

        // ------------------------------------------------------------------ the mod's places and the city's buildings

        // The nearest place of the rows asked that has a building to hold in the frame: the place's own, or the one standing at it.
        private static void OnMap(Vector3 from, string[] rows, string none, ref CineFound found)
        {
            IReadOnlyList<MapPlaces.Category> all = MapPlaces.All;
            int bestRow = int.MaxValue;
            float bestApart = float.MaxValue;
            bool any = false;
            var best = default(CineFound);
            for (int c = 0; all != null && c < all.Count; c++)
            {
                MapPlaces.Category category = all[c];
                int row = category != null ? Array.IndexOf(rows, category.Id) : -1;
                if (row < 0 || category.Places == null)
                {
                    continue;
                }
                for (int p = 0; p < category.Places.Count; p++)
                {
                    MapPlace place = category.Places[p];
                    if (place == null || !Near2(from, place.Position, CineRules.Reach))
                    {
                        continue;
                    }
                    float apart = (place.Position - from).sqrMagnitude;
                    if (!(row < bestRow || (row == bestRow && apart < bestApart)))   // the rows are in the order a photographer would pick them
                    {
                        continue;
                    }
                    CityBuildingController building = place.Building != null ? place.Building : BuildingAt(place.Position);
                    var one = default(CineFound);
                    if (building == null || !Measure(building, place.Position, string.IsNullOrEmpty(place.Name) ? rows[row] : place.Name, ref one))
                    {
                        continue;   // a marker with nothing to it is no picture
                    }
                    best = one;
                    bestRow = row;
                    bestApart = apart;
                    any = true;
                }
            }
            if (!any)
            {
                found.Why = none;
                return;
            }
            found = best;
        }

        // The building that stands at a place on the map: the nearest of the city's whose walls are within a few metres of it.
        private static CityBuildingController BuildingAt(Vector3 place)
        {
            int count = Physics.OverlapSphereNonAlloc(place + Vector3.up * 2f, PlaceRadius, Near, CineGame.Walls, QueryTriggerInteraction.Ignore);
            CityBuildingController best = null;
            float bestApart = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                CityBuildingController building = Near[i] != null ? Near[i].GetComponentInParent<CityBuildingController>() : null;
                if (building == null)
                {
                    continue;
                }
                float apart = (Near[i].bounds.ClosestPoint(place) - place).sqrMagnitude;
                if (apart < bestApart)
                {
                    bestApart = apart;
                    best = building;
                }
            }
            return best;
        }

        // A building as a box: its renderers' bounds, facing out through the side its door (or its place on the map) is on.
        private static bool Measure(CityBuildingController building, Vector3 face, string name, ref CineFound found)
        {
            Renderers.Clear();
            building.GetComponentsInChildren(false, Renderers);
            bool any = false;
            var bounds = default(Bounds);
            for (int i = 0; i < Renderers.Count && i < RenderersMost; i++)
            {
                Renderer part = Renderers[i];
                if (part == null || !part.enabled || part is ParticleSystemRenderer || part.bounds.size.sqrMagnitude > 600f * 600f)
                {
                    continue;
                }
                if (any)
                {
                    bounds.Encapsulate(part.bounds);
                }
                else
                {
                    bounds = part.bounds;
                    any = true;
                }
            }
            Renderers.Clear();
            if (!any)
            {
                return false;
            }
            Vector3 centre = bounds.center;
            Vector3 out0 = face - centre;
            Transform frame = building.transform;
            Vector3 along = Flat(frame.forward), across = Flat(frame.right);
            float byAlong = Vector3.Dot(out0, along), byAcross = Vector3.Dot(out0, across);
            Vector3 outward = Mathf.Abs(byAlong) >= Mathf.Abs(byAcross) ? along * (byAlong < 0f ? -1f : 1f) : across * (byAcross < 0f ? -1f : 1f);
            float deep = Mathf.Abs(outward.x) * bounds.extents.x + Mathf.Abs(outward.z) * bounds.extents.z;
            float wide = Mathf.Abs(outward.z) * bounds.extents.x + Mathf.Abs(outward.x) * bounds.extents.z;
            float ground = CineGame.Ground(face, out float height) ? height : bounds.min.y;
            centre.y = ground;
            Set(ref found, centre, outward, Mathf.Max(1f, deep), Mathf.Max(1f, wide), Mathf.Clamp(bounds.max.y - ground, 3f, 400f), name);
            return true;
        }

        private static void Frontage(Vector3 from, ref CineFound found)
        {
            CityManager city = InstanceBehavior<CityManager>.Instance;
            CityBuildingController[] all = city != null ? GameDlc.Open(city) : null;
            CityBuildingController best = null;
            Vector3 bestDoor = default(Vector3);
            float bestApart = FrontageReach * FrontageReach;
            for (int i = 0; all != null && i < all.Length; i++)
            {
                CityBuildingController building = all[i];
                BuildingRegistration registration = building != null ? building.buildingRegistration : null;
                if (registration == null || string.IsNullOrEmpty(registration.BusinessName) || !Books.IsBusiness(registration) || !JobSpots.DoorOf(building, out Vector3 door))
                {
                    continue;
                }
                float apart = (door - from).sqrMagnitude;
                if (apart < bestApart)
                {
                    bestApart = apart;
                    best = building;
                    bestDoor = door;
                }
            }
            if (best == null)
            {
                found.Why = "no business with a door within " + Metres(FrontageReach);
                return;
            }
            string name = best.buildingRegistration.BusinessName;
            if (!Measure(best, bestDoor, name, ref found))
            {
                found.Why = "the nearest business's building could not be measured";
                found.There = false;
                return;
            }
            // The front, not the block: the door on the ground, a few metres of the wall either side, the sign's height.
            if (CineGame.Ground(bestDoor, out float ground))
            {
                bestDoor.y = ground;
            }
            found.Box.X = bestDoor.x;
            found.Box.Y = bestDoor.y;
            found.Box.Z = bestDoor.z;
            found.Box.HalfLength = 0f;
            found.Box.HalfWidth = FrontHalf;
            found.Box.Height = Mathf.Min(found.Box.Height, 6f);
        }

        // ------------------------------------------------------------------ the mod's own in the street

        private static void Bus(Vector3 from, ref CineFound found)
        {
            if (!TransitDirector.BusOut(out Vector3 at, out Vector3 facing, out bool standing) || !Near2(from, at, CineRules.Reach))
            {
                found.Why = "no bus on the street within " + Metres(CineRules.Reach) + " (one comes when the player waits at a stop)";
                return;
            }
            if (CineGame.Ground(at, out float ground))
            {
                at.y = ground;
            }
            Set(ref found, at, facing, BusVehicle.Length * 0.5f, BusVehicle.HalfWidth, 3f, standing ? "a bus standing at its stop" : "a bus on its way");
        }

        private static void Stall(Vector3 from, ref CineFound found)
        {
            CityLifeDirector life = CityLifeDirector.Instance;
            Vector3 at = default(Vector3);
            bool stall = life != null && life.SiteOut(CityFamily.Stalls, out at);
            if ((!stall && !(life != null && life.SiteOut(CityFamily.Everyday, out at))) || !Near2(from, at, CineRules.Reach))
            {
                found.Why = "no stall, cart or street cafe of the living city is out near the player";
                return;
            }
            if (CineGame.Ground(at, out float ground))
            {
                at.y = ground;
            }
            // A person at a stall is the subject, seen from the player's side of it.
            Set(ref found, at, Near2(from, at, 2f) ? Vector3.forward : from - at, 0.5f, 0.5f, 1.8f, stall ? "a stall of the living city" : "people of the living city about their day");
        }

        private static void Incident(Vector3 from, ref CineFound found)
        {
            Vector3 at = default(Vector3);
            string what = null;
            if (TrafficAccidents.IsActive)
            {
                TrafficAccidents.Needing(out at);   // where the scene is, whoever it needs
                what = "a traffic accident";
            }
            else if (Events.EventDirector.Burning(out at))
            {
                what = "a fire";
            }
            else if (Events.EventDirector.StreetEvent(out at, out _))
            {
                what = "a street event";
            }
            else if (CrimeDirector.IsActive)
            {
                CrimeDirector.Victim(out at);   // where the scene is, whatever its kind
                what = "a crime scene";
            }
            if (what == null || at == Vector3.zero || !Near2(from, at, CineRules.Reach))
            {
                found.Why = what == null ? "no accident, fire, crime or street event is on" : what + " is on, but not within " + Metres(CineRules.Reach);
                return;
            }
            if (CineGame.Ground(at, out float ground))
            {
                at.y = ground;
            }
            Set(ref found, at, Near2(from, at, 2f) ? Vector3.forward : from - at, 7f, 4.5f, 2.4f, what);
        }
    }
}
