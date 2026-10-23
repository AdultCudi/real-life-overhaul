using System;
using System.Collections.Generic;
using System.Globalization;

namespace RealLifeOverhaul.NewGround
{
    /// <summary>
    /// New streets as the game's traffic system lays its own (read from its baked lanes in the Streets scene): traffic
    /// keeps right; a lane's points are named Road_n-Lane_k-Waypoint_i, the last before a junction ...-Out and the first
    /// after one ...-In; the turn through a junction is ...-CConnect, ...-Connector, ...-CConnect points that keep the road
    /// they come from, so the traffic's blinkers (DrivingAI.ClassifyTurn) see the road change at the In point; a lane
    /// change is a point a step ahead on the lane beside, the same way (otherLanes); a car may be put on the road on any
    /// plain point that is not a junction's stop or exit; points stand about 4.8 m apart. The plan's new points come after
    /// the game's own (indices it has are never changed), with road ids above the game's, inside the road grid as the game
    /// has it at this load. Every build is checked (<see cref="Check"/>) before the game's side appends it, and refused if
    /// the game has put a road of its own there since (<see cref="Clear"/>). No Unity or game types: checked offline
    /// (NavCheck, NewGroundRoadCheck.cs). Runs once at a load; nothing per frame.
    /// </summary>
    public static class RoadRules
    {
        public const float Step = 4.8f;          // m between two lane points, the game's own median (4.81 m)
        public const float EdgeMargin = 1f;      // m every new point keeps from the road grid's edges
        public const float ClearReach = 6f;      // m: a lane point of the game's this near a new one (a join apart) means a road of the game's is there now
        public const float JoinReach = 8f;       // m from its place a join's lane point of the game's may be found
        public const float JoinRoom = 14f;       // m round a join where the game's own lanes are expected beside the new one
        public const float LongestLink = 16f;    // m: two linked points further apart are a fault (the game's longest plain step is 27 m, on bends rarely over 13)
        public const float TurnAngle = 30f;      // degrees: a change of heading through a junction under this is going straight on
        public const int MostPriorityWays = 4;   // roads into a priority junction: the game's weighs four at most (PriorityIntersection.IsPathFree)
        public const int SlowestSpeed = 10;      // km/h: the range a lane's speed is held to
        public const int FastestSpeed = 130;

        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        // One lane of a road while it is built: where its points are in the build, and how it meets its junctions.
        private sealed class Lane
        {
            public int Road;
            public int Index;          // its number on the road (the k of Lane_k)
            public bool Forward;       // the way the road's line runs
            public int Rank;           // 0 nearest the middle, Count - 1 at the kerb
            public int Count;          // lanes this way on its road
            public int First;          // local indices of its first and last point
            public int Last;
            public int StartJunction;  // -1 when it starts at an open end
            public int EndJunction;
            public float StartDx, StartDz;   // its heading at its first and last point
            public float EndDx, EndDz;
            public int Connectors;     // turn points named after it so far
        }

        /// <summary>The name of a lane point, the game's way: <c>Road_7-Lane_0-Waypoint_3</c>, <c>-In</c>, <c>-Out</c>, <c>-CConnect2</c>, <c>-Connector5</c>.</summary>
        public static string Name(int road, int lane, string what)
        {
            return "Road_" + road.ToString(Inv) + "-Lane_" + lane.ToString(Inv) + "-" + what;
        }

        /// <summary>
        /// The plan's lanes, turns and junctions as lane points after the game's <paramref name="first"/> own, with road
        /// ids from <paramref name="firstRoadId"/>. A join's lane point of the game's must have been found at this load.
        /// </summary>
        public static RoadBuild Build(RoadPlan plan, RoadGrid grid, int first, int firstRoadId)
        {
            var build = new RoadBuild(first, firstRoadId);
            if (plan == null || plan.Roads.Count == 0)
            {
                build.Problem = "the plan has no road";
                return build;
            }
            if (!grid.Valid)
            {
                build.Problem = "the game's road grid is not there";
                return build;
            }
            var lanes = new List<Lane>(plan.Roads.Count * 2);
            for (int r = 0; r < plan.Roads.Count && build.Problem == null; r++)
            {
                LayRoad(plan, r, build, lanes);
            }
            for (int j = 0; j < plan.Junctions.Count && build.Problem == null; j++)
            {
                LayJunction(plan, j, build, lanes);
            }
            for (int i = 0; i < plan.Joins.Count && build.Problem == null; i++)
            {
                Join(plan, plan.Joins[i], build, lanes);
            }
            build.Roads = plan.Roads.Count;
            if (build.Problem == null)
            {
                Finish(build, grid);
            }
            return build;
        }

        /// <summary>
        /// The test road: a copy of one of the game's own lanes as a road of its own, joined to it by a fork at the
        /// chain's first point and a merge at its last. <paramref name="chain"/> is the game's lane points in their order
        /// (at least three, the inner ones copied where they stand), <paramref name="x"/>, <paramref name="y"/>,
        /// <paramref name="z"/> their places. No car is put on the road on the copy, so every car on it chose the fork.
        /// </summary>
        public static RoadBuild Copy(int[] chain, float[] x, float[] y, float[] z, int speed, bool cars, bool trucks, RoadGrid grid, int first, int roadId)
        {
            var build = new RoadBuild(first, roadId);
            if (chain == null || x == null || y == null || z == null || chain.Length < 3 || x.Length != chain.Length || y.Length != chain.Length || z.Length != chain.Length)
            {
                build.Problem = "the lane to copy has fewer than three points";
                return build;
            }
            if (!grid.Valid)
            {
                build.Problem = "the game's road grid is not there";
                return build;
            }
            for (int i = 1; i < chain.Length - 1; i++)
            {
                var point = new LanePoint
                {
                    Name = Name(roadId, 0, "Waypoint_" + (i - 1).ToString(Inv)),
                    X = x[i],
                    Y = y[i],
                    Z = z[i],
                    Cars = cars,
                    Trucks = trucks,
                    Speed = speed,
                    RoadId = roadId
                };
                build.Points.Add(point);
            }
            for (int i = 0; i < build.Points.Count; i++)
            {
                if (i > 0)
                {
                    build.Points[i].Prev.Add(first + i - 1);
                }
                if (i < build.Points.Count - 1)
                {
                    build.Points[i].Next.Add(first + i + 1);
                }
            }
            build.Points[0].Prev.Add(chain[0]);
            build.Points[build.Points.Count - 1].Next.Add(chain[chain.Length - 1]);
            build.Edits.Add(new LinkEdit(chain[0], first, true));
            build.Edits.Add(new LinkEdit(chain[chain.Length - 1], first + build.Points.Count - 1, false));
            build.Roads = 1;
            Finish(build, grid);
            for (int i = 0; i < build.Points.Count; i++)
            {
                build.Points[i].Spawn = false;
            }
            return build;
        }

        // A road's lanes: each lane's points evenly along the middle line, offset to its own right, stopped short of its junctions.
        private static void LayRoad(RoadPlan plan, int r, RoadBuild build, List<Lane> lanes)
        {
            PlanRoad road = plan.Roads[r];
            int id = build.FirstRoadId + r;
            if (road.X.Count < 2 || road.Y.Count != road.X.Count || road.Z.Count != road.X.Count)
            {
                build.Problem = "road " + r + " has fewer than two points of its middle line";
                return;
            }
            if (road.Ahead < 0 || road.Back < 0 || road.Ahead + road.Back == 0)
            {
                build.Problem = "road " + r + " has no lane";
                return;
            }
            if (road.LaneWidth < 2f || road.LaneWidth > 6f)
            {
                build.Problem = "road " + r + " has lanes " + road.LaneWidth.ToString("0.0", Inv) + " m wide (2 to 6 m wanted)";
                return;
            }
            if (road.Speed < SlowestSpeed || road.Speed > FastestSpeed || (!road.Cars && !road.Trucks))
            {
                build.Problem = "road " + r + " has a speed of " + road.Speed + " km/h or no vehicle that may drive it";
                return;
            }
            if (!Junction(plan, road.Start, true) || !Junction(plan, road.End, true))
            {
                build.Problem = "road " + r + " names a junction the plan does not have";
                return;
            }
            float length = Length(road);
            float from = road.Start >= 0 ? plan.Junctions[road.Start].Inset : 0f;
            float to = length - (road.End >= 0 ? plan.Junctions[road.End].Inset : 0f);
            if (to - from < Step)
            {
                build.Problem = "road " + r + " is " + length.ToString("0.0", Inv) + " m long, too short for its junctions";
                return;
            }
            int count = Math.Max(2, (int)Math.Round((to - from) / Step) + 1);
            for (int lane = 0; lane < road.Ahead + road.Back; lane++)
            {
                bool forward = lane < road.Ahead;
                int rank = forward ? lane : lane - road.Ahead;
                float offset = (rank + 0.5f) * road.LaneWidth;
                var made = new Lane
                {
                    Road = r,
                    Index = lane,
                    Forward = forward,
                    Rank = rank,
                    Count = forward ? road.Ahead : road.Back,
                    First = build.Points.Count,
                    StartJunction = forward ? road.Start : road.End,
                    EndJunction = forward ? road.End : road.Start
                };
                for (int i = 0; i < count; i++)
                {
                    float along = forward ? from + (to - from) * i / (count - 1) : to - (to - from) * i / (count - 1);
                    At(road, along, out float cx, out float cy, out float cz, out float dx, out float dz);
                    if (!forward)
                    {
                        dx = -dx;
                        dz = -dz;
                    }
                    // The lane's own right: (dz, -dx) with x east and z north.
                    string what = i == 0 && made.StartJunction >= 0 ? "In"
                        : i == count - 1 && made.EndJunction >= 0 ? "Out"
                        : "Waypoint_" + i.ToString(Inv);
                    var point = new LanePoint
                    {
                        Name = Name(id, lane, what),
                        X = cx + dz * offset,
                        Y = cy,
                        Z = cz - dx * offset,
                        Cars = road.Cars,
                        Trucks = road.Trucks,
                        Speed = road.Speed,
                        RoadId = id
                    };
                    int local = build.Points.Count;
                    if (i > 0)
                    {
                        point.Prev.Add(build.First + local - 1);
                        build.Points[local - 1].Next.Add(build.First + local);
                    }
                    build.Points.Add(point);
                    if (i == 0)
                    {
                        made.StartDx = dx;
                        made.StartDz = dz;
                    }
                    if (i == count - 1)
                    {
                        made.EndDx = dx;
                        made.EndDz = dz;
                    }
                }
                made.Last = build.Points.Count - 1;
                lanes.Add(made);
            }
            // Lane changes, as the game has them: a point to the one a step ahead on the lane beside, both ways.
            for (int a = lanes.Count - road.Ahead - road.Back; a < lanes.Count; a++)
            {
                for (int b = a + 1; b < lanes.Count; b++)
                {
                    Lane one = lanes[a], other = lanes[b];
                    if (one.Forward != other.Forward || Math.Abs(one.Rank - other.Rank) != 1)
                    {
                        continue;
                    }
                    for (int i = 0; i < count - 2; i++)
                    {
                        Beside(build, one.First + i, other.First + i + 1);
                        Beside(build, other.First + i, one.First + i + 1);
                    }
                }
            }
        }

        // A lane change between two plain points (never a junction's stop or exit).
        private static void Beside(RoadBuild build, int from, int to)
        {
            LanePoint a = build.Points[from], b = build.Points[to];
            if (a.Name.IndexOf("Waypoint_", StringComparison.Ordinal) < 0 || b.Name.IndexOf("Waypoint_", StringComparison.Ordinal) < 0)
            {
                return;
            }
            a.Beside.Add(build.First + to);
        }

        // A junction's turns: from each lane coming in to the lanes of every other road leaving, the kerb lane turning
        // right into the kerb lane, the middle lane left into the middle lane, straight on lane by lane.
        private static void LayJunction(RoadPlan plan, int j, RoadBuild build, List<Lane> lanes)
        {
            PlanJunction junction = plan.Junctions[j];
            var built = new BuiltJunction
            {
                Name = "Intersection_" + (plan.Name ?? "plan") + "_" + j.ToString(Inv),
                Lights = junction.Lights,
                Green = junction.Green,
                Yellow = junction.Yellow
            };
            var ins = new List<Lane>(8);
            var outs = new List<Lane>(8);
            for (int i = 0; i < lanes.Count; i++)
            {
                if (lanes[i].EndJunction == j)
                {
                    ins.Add(lanes[i]);
                }
                if (lanes[i].StartJunction == j)
                {
                    outs.Add(lanes[i]);
                }
            }
            if (ins.Count == 0 || outs.Count == 0)
            {
                build.Problem = "junction " + j + " has " + ins.Count + " lanes in and " + outs.Count + " out";
                return;
            }
            var roadsIn = new List<int>(4);
            for (int i = 0; i < ins.Count; i++)
            {
                int way = roadsIn.IndexOf(ins[i].Road);
                if (way < 0)
                {
                    roadsIn.Add(ins[i].Road);
                    built.Ways.Add(new List<int>(2));
                    way = built.Ways.Count - 1;
                }
                built.Ways[way].Add(build.First + ins[i].Last);
            }
            if (!junction.Lights && built.Ways.Count > MostPriorityWays)
            {
                build.Problem = "priority junction " + j + " has " + built.Ways.Count + " roads in (the game's weighs " + MostPriorityWays + " at most)";
                return;
            }
            for (int i = 0; i < outs.Count; i++)
            {
                built.Exits.Add(build.First + outs[i].First);
            }
            var reached = new bool[outs.Count];
            for (int i = 0; i < ins.Count; i++)
            {
                Lane lane = ins[i];
                int turns = 0;
                for (int o = 0; o < outs.Count; o++)
                {
                    Lane leave = outs[o];
                    if (leave.Road == lane.Road || !Turns(lane, leave))
                    {
                        continue;
                    }
                    LanePoint a = build.Points[lane.Last], b = build.Points[leave.First];
                    bool cars = a.Cars && b.Cars, trucks = a.Trucks && b.Trucks;
                    if (!cars && !trucks)
                    {
                        continue;
                    }
                    Connect(build, lane, leave, cars, trucks);
                    turns++;
                    reached[o] = true;
                }
                if (turns == 0)
                {
                    build.Problem = "a lane of road " + lane.Road + " into junction " + j + " leads nowhere";
                    return;
                }
            }
            for (int o = 0; o < outs.Count; o++)
            {
                if (!reached[o])
                {
                    build.Problem = "a lane of road " + outs[o].Road + " out of junction " + j + " cannot be reached";
                    return;
                }
            }
            build.Junctions.Add(built);
        }

        // Which lane of the road coming in may take which lane of the road leaving.
        private static bool Turns(Lane lane, Lane leave)
        {
            float angle = Angle(lane.EndDx, lane.EndDz, leave.StartDx, leave.StartDz);
            if (angle > TurnAngle)
            {
                return lane.Rank == 0 && leave.Rank == 0;   // a left turn: the middle lane into the middle lane
            }
            if (angle < -TurnAngle)
            {
                return lane.Rank == lane.Count - 1 && leave.Rank == leave.Count - 1;   // right: kerb lane into kerb lane
            }
            // Straight on lane by lane; where the road beyond has more lanes, the kerb lane also feeds the extra ones.
            return leave.Rank == Math.Min(lane.Rank, leave.Count - 1) || (lane.Rank == lane.Count - 1 && leave.Rank > lane.Rank);
        }

        // The turn through the junction: a curve from the stop point to the exit point, its own points named after the lane coming in.
        private static void Connect(RoadBuild build, Lane lane, Lane leave, bool cars, bool trucks)
        {
            LanePoint a = build.Points[lane.Last], b = build.Points[leave.First];
            float cx, cz;
            if (!Meet(a.X, a.Z, lane.EndDx, lane.EndDz, b.X, b.Z, -leave.StartDx, -leave.StartDz, out cx, out cz))
            {
                cx = (a.X + b.X) * 0.5f;
                cz = (a.Z + b.Z) * 0.5f;
            }
            float span = Distance(a.X, a.Z, cx, cz) + Distance(cx, cz, b.X, b.Z);
            int inner = Math.Max(0, (int)Math.Round(span / Step) - 1);
            int previous = build.First + lane.Last;
            for (int i = 0; i < inner; i++)
            {
                float t = (i + 1f) / (inner + 1f);
                float u = 1f - t;
                lane.Connectors++;
                string what = (i == 0 || i == inner - 1 ? "CConnect" : "Connector") + lane.Connectors.ToString(Inv);
                var point = new LanePoint
                {
                    Name = Name(a.RoadId, lane.Index, what),
                    X = u * u * a.X + 2f * u * t * cx + t * t * b.X,
                    Y = u * a.Y + t * b.Y,
                    Z = u * u * a.Z + 2f * u * t * cz + t * t * b.Z,
                    Cars = cars,
                    Trucks = trucks,
                    Speed = a.Speed,
                    RoadId = a.RoadId
                };
                int index = build.First + build.Points.Count;
                point.Prev.Add(previous);
                Point(build, previous).Next.Add(index);
                build.Points.Add(point);
                previous = index;
            }
            int exit = build.First + leave.First;
            Point(build, previous).Next.Add(exit);
            b.Prev.Add(previous);
        }

        // A join: the lane's open end linked into one of the game's own lanes (found at this load).
        private static void Join(RoadPlan plan, PlanJoin join, RoadBuild build, List<Lane> lanes)
        {
            Lane lane = null;
            for (int i = 0; i < lanes.Count; i++)
            {
                if (lanes[i].Road == join.Road && lanes[i].Index == join.Lane)
                {
                    lane = lanes[i];
                }
            }
            if (lane == null)
            {
                build.Problem = "a join names lane " + join.Lane + " of road " + join.Road + ", which the plan does not have";
                return;
            }
            if (join.Existing < 0 || join.Existing >= build.First)
            {
                build.Problem = "a join of road " + join.Road + " found no lane of the game's within " + JoinReach.ToString("0", Inv) + " m of its place";
                return;
            }
            if (join.Fork ? lane.StartJunction >= 0 : lane.EndJunction >= 0)
            {
                build.Problem = "a join of road " + join.Road + " is at an end that meets a junction";
                return;
            }
            int point = build.First + (join.Fork ? lane.First : lane.Last);
            LanePoint own = Point(build, point);
            if (join.Fork)
            {
                own.Prev.Add(join.Existing);
                build.Edits.Add(new LinkEdit(join.Existing, point, true));
            }
            else
            {
                own.Next.Add(join.Existing);
                build.Edits.Add(new LinkEdit(join.Existing, point, false));
            }
        }

        // Cells and spawn places, once every point is laid.
        private static void Finish(RoadBuild build, RoadGrid grid)
        {
            var stopsAndExits = new HashSet<int>();
            for (int j = 0; j < build.Junctions.Count; j++)
            {
                BuiltJunction junction = build.Junctions[j];
                for (int w = 0; w < junction.Ways.Count; w++)
                {
                    stopsAndExits.UnionWith(junction.Ways[w]);
                }
                stopsAndExits.UnionWith(junction.Exits);
            }
            for (int i = 0; i < build.Points.Count; i++)
            {
                LanePoint point = build.Points[i];
                point.Row = grid.Row(point.Z);
                point.Column = grid.Column(point.X);
                point.Spawn = point.Name.IndexOf("Connect", StringComparison.Ordinal) < 0 && !stopsAndExits.Contains(build.First + i);
            }
            for (int j = 0; j < build.Junctions.Count; j++)
            {
                BuiltJunction junction = build.Junctions[j];
                for (int w = 0; w < junction.Ways.Count; w++)
                {
                    for (int k = 0; k < junction.Ways[w].Count; k++)
                    {
                        AddCell(junction, build.At(junction.Ways[w][k]), grid);
                    }
                }
                for (int k = 0; k < junction.Exits.Count; k++)
                {
                    AddCell(junction, build.At(junction.Exits[k]), grid);
                }
            }
        }

        private static void AddCell(BuiltJunction junction, LanePoint point, RoadGrid grid)
        {
            if (point == null)
            {
                return;
            }
            int cell = point.Row * grid.Columns + point.Column;
            if (!junction.Cells.Contains(cell))
            {
                junction.Cells.Add(cell);
            }
        }

        /// <summary>
        /// Null when the build may be appended after the game's <paramref name="existing"/> lane points; otherwise what is
        /// wrong. Every point on the grid, every link in range and both ways, nothing that leads nowhere, names as the game
        /// reads them, road ids above the game's highest (<paramref name="highestRoadId"/>), sound junctions and joins.
        /// </summary>
        public static string Check(RoadBuild build, RoadGrid grid, int existing, int highestRoadId)
        {
            if (build == null)
            {
                return "no build";
            }
            if (build.Problem != null)
            {
                return build.Problem;
            }
            if (build.First != existing)
            {
                return "the build was made after " + build.First + " lane points, the game has " + existing + " now";
            }
            if (build.Points.Count == 0)
            {
                return "the build has no lane point";
            }
            if (!grid.Valid)
            {
                return "the game's road grid is not there";
            }
            int total = existing + build.Points.Count;
            for (int i = 0; i < build.Points.Count; i++)
            {
                LanePoint point = build.Points[i];
                int index = existing + i;
                if (!grid.Inside(point.X, point.Z, EdgeMargin))
                {
                    return "the lane point " + point.Name + " at (" + point.X.ToString("0.0", Inv) + ", " + point.Z.ToString("0.0", Inv)
                           + ") is off the game's road grid (x " + grid.CornerX.ToString("0.0", Inv) + " to " + grid.EastEdge.ToString("0.0", Inv)
                           + ", z " + grid.CornerZ.ToString("0.0", Inv) + " to " + grid.NorthEdge.ToString("0.0", Inv) + ")";
                }
                if (point.Row != grid.Row(point.Z) || point.Column != grid.Column(point.X))
                {
                    return "the lane point " + point.Name + " is filed in the wrong cell";
                }
                if (point.Next.Count == 0)
                {
                    return "the lane point " + point.Name + " leads nowhere";
                }
                if (point.Prev.Count == 0)
                {
                    return "nothing leads into the lane point " + point.Name;
                }
                if (!point.Cars && !point.Trucks)
                {
                    return "no vehicle may drive the lane point " + point.Name;
                }
                if (point.Speed < SlowestSpeed || point.Speed > FastestSpeed)
                {
                    return "the lane point " + point.Name + " has a speed of " + point.Speed + " km/h";
                }
                if (point.RoadId <= highestRoadId)
                {
                    return "the lane point " + point.Name + " has road id " + point.RoadId + ", which the game's roads may use (its highest is " + highestRoadId + ")";
                }
                bool connector = point.Name.IndexOf("Connect", StringComparison.Ordinal) >= 0;
                bool plain = point.Name.IndexOf("Waypoint_", StringComparison.Ordinal) >= 0;
                bool end = point.Name.EndsWith("-In", StringComparison.Ordinal) || point.Name.EndsWith("-Out", StringComparison.Ordinal);
                if (!point.Name.StartsWith("Road_", StringComparison.Ordinal) || point.Name.IndexOf("-Lane_", StringComparison.Ordinal) < 0
                    || (connector ? 1 : 0) + (plain ? 1 : 0) + (end ? 1 : 0) != 1)
                {
                    return "the lane point " + point.Name + " is not named as the game reads it";
                }
                if (connector && point.Spawn)
                {
                    return "a car would be put on the road at the turn point " + point.Name;
                }
                string link = Links(build, point, index, point.Next, true, total);
                if (link != null)
                {
                    return link;
                }
                link = Links(build, point, index, point.Prev, false, total);
                if (link != null)
                {
                    return link;
                }
                for (int k = 0; k < point.Beside.Count; k++)
                {
                    LanePoint other = build.At(point.Beside[k]);
                    if (other == null || !plain || other.Name.IndexOf("Waypoint_", StringComparison.Ordinal) < 0
                        || Distance(point.X, point.Z, other.X, other.Z) > LongestLink)
                    {
                        return "the lane change from " + point.Name + " is not to a plain point nearby";
                    }
                }
            }
            for (int e = 0; e < build.Edits.Count; e++)
            {
                LinkEdit edit = build.Edits[e];
                LanePoint added = build.At(edit.Added);
                if (edit.Existing < 0 || edit.Existing >= existing || added == null)
                {
                    return "a join links lane point " + edit.Existing + " of the game's, which it does not have";
                }
                if (edit.AsNext ? !added.Prev.Contains(edit.Existing) : !added.Next.Contains(edit.Existing))
                {
                    return "the join at " + added.Name + " is one way only";
                }
            }
            for (int j = 0; j < build.Junctions.Count; j++)
            {
                BuiltJunction junction = build.Junctions[j];
                if (junction.Ways.Count == 0 || junction.Exits.Count == 0)
                {
                    return "the junction " + junction.Name + " has no way in or out";
                }
                if (!junction.Lights && junction.Ways.Count > MostPriorityWays)
                {
                    return "the priority junction " + junction.Name + " has " + junction.Ways.Count + " roads in";
                }
                if (junction.Lights && (junction.Green <= 0f || junction.Yellow <= 0f))
                {
                    return "the lights of " + junction.Name + " have no green or yellow time";
                }
            }
            return null;
        }

        // Every link of a point in range, the other end linked back, and not too long.
        private static string Links(RoadBuild build, LanePoint point, int index, List<int> links, bool next, int total)
        {
            for (int k = 0; k < links.Count; k++)
            {
                int to = links[k];
                if (to < 0 || to >= total || to == index)
                {
                    return "the lane point " + point.Name + " links to " + to + ", outside the lane points";
                }
                LanePoint other = build.At(to);
                if (other == null)
                {
                    bool edited = false;
                    for (int e = 0; e < build.Edits.Count; e++)
                    {
                        edited |= build.Edits[e].Existing == to && build.Edits[e].Added == index && build.Edits[e].AsNext != next;
                    }
                    if (!edited)
                    {
                        return "the lane point " + point.Name + " links to the game's lane point " + to + " without a join";
                    }
                    continue;
                }
                if (next ? !other.Prev.Contains(index) : !other.Next.Contains(index))
                {
                    return "the link between " + point.Name + " and " + other.Name + " is one way only";
                }
                if (Distance(point.X, point.Z, other.X, other.Z) > LongestLink)
                {
                    return "the lane points " + point.Name + " and " + other.Name + " are " + Distance(point.X, point.Z, other.X, other.Z).ToString("0.0", Inv) + " m apart";
                }
            }
            return null;
        }

        /// <summary>
        /// Null when no lane point of the game's (<paramref name="x"/>, <paramref name="z"/>, the game's own indices) stands
        /// within <see cref="ClearReach"/> of a new one, apart from round the build's joins; else where the game has a road
        /// now, which a game update may have put there since the plan was made.
        /// </summary>
        public static string Clear(RoadBuild build, int[] indices, float[] x, float[] z)
        {
            if (build == null || indices == null || x == null || z == null)
            {
                return null;
            }
            float reach = ClearReach * ClearReach;
            float room = JoinRoom * JoinRoom;
            for (int i = 0; i < build.Points.Count; i++)
            {
                LanePoint point = build.Points[i];
                bool nearJoin = false;
                for (int e = 0; e < build.Edits.Count && !nearJoin; e++)
                {
                    LanePoint joined = build.At(build.Edits[e].Added);
                    nearJoin = joined != null && Square(point.X - joined.X, point.Z - joined.Z) <= room;
                }
                if (nearJoin)
                {
                    continue;
                }
                for (int k = 0; k < indices.Length; k++)
                {
                    if (Square(point.X - x[k], point.Z - z[k]) <= reach)
                    {
                        return "the game has a lane point (" + indices[k] + ") at (" + x[k].ToString("0.0", Inv) + ", " + z[k].ToString("0.0", Inv)
                               + "), within " + ClearReach.ToString("0", Inv) + " m of the new " + point.Name;
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// The game's lane point a join is made to: the nearest within <see cref="JoinReach"/> of its place that runs the
        /// right way (a fork's leads into the new lane, a merge's takes it), or -1.
        /// </summary>
        public static int FindJoin(PlanJoin join, float laneDx, float laneDz, int[] indices, float[] x, float[] z, float[] dx, float[] dz)
        {
            int best = -1;
            float nearest = JoinReach * JoinReach;
            for (int k = 0; k < indices.Length; k++)
            {
                float d = Square(x[k] - join.X, z[k] - join.Z);
                if (d > nearest)
                {
                    continue;
                }
                float dot = dx[k] * laneDx + dz[k] * laneDz;
                if (dot < 0.5f * (float)Math.Sqrt((dx[k] * dx[k] + dz[k] * dz[k]) * (laneDx * laneDx + laneDz * laneDz)))
                {
                    continue;   // more than 60 degrees off the new lane's way
                }
                nearest = d;
                best = indices[k];
            }
            return best;
        }

        /// <summary>The heading of a plan's lane at its open end, for <see cref="FindJoin"/>.</summary>
        public static void JoinHeading(PlanRoad road, PlanJoin join, out float dx, out float dz)
        {
            bool forward = join.Lane < road.Ahead;
            bool atStart = forward == join.Fork;
            float length = Length(road);
            At(road, atStart ? 0f : length, out _, out _, out _, out dx, out dz);
            if (!forward)
            {
                dx = -dx;
                dz = -dz;
            }
        }

        /// <summary>A short text of what a build adds, for the log.</summary>
        public static string Words(RoadBuild build)
        {
            if (build == null)
            {
                return "nothing";
            }
            int connectors = 0, spawns = 0;
            for (int i = 0; i < build.Points.Count; i++)
            {
                connectors += build.Points[i].Name.IndexOf("Connect", StringComparison.Ordinal) >= 0 ? 1 : 0;
                spawns += build.Points[i].Spawn ? 1 : 0;
            }
            int lights = 0;
            for (int j = 0; j < build.Junctions.Count; j++)
            {
                lights += build.Junctions[j].Lights ? 1 : 0;
            }
            return build.Points.Count + " lane points (" + connectors + " in junctions, " + spawns + " where cars may start) on " + build.Roads + " road(s), "
                   + build.Junctions.Count + " junction(s) (" + lights + " with lights), " + build.Edits.Count + " join(s) to the game's lanes";
        }

        private static LanePoint Point(RoadBuild build, int index)
        {
            return build.At(index);
        }

        private static bool Junction(RoadPlan plan, int index, bool optional)
        {
            return (optional && index == -1) || (index >= 0 && index < plan.Junctions.Count);
        }

        private static float Length(PlanRoad road)
        {
            float length = 0f;
            for (int i = 1; i < road.X.Count; i++)
            {
                length += Distance(road.X[i - 1], road.Z[i - 1], road.X[i], road.Z[i]);
            }
            return length;
        }

        // The middle line's point and heading at a distance along it.
        private static void At(PlanRoad road, float along, out float x, out float y, out float z, out float dx, out float dz)
        {
            int last = road.X.Count - 1;
            for (int i = 1; i <= last; i++)
            {
                float piece = Distance(road.X[i - 1], road.Z[i - 1], road.X[i], road.Z[i]);
                if (along <= piece || i == last)
                {
                    float t = piece > 0.0001f ? Math.Max(0f, Math.Min(1f, along / piece)) : 0f;
                    x = road.X[i - 1] + (road.X[i] - road.X[i - 1]) * t;
                    y = road.Y[i - 1] + (road.Y[i] - road.Y[i - 1]) * t;
                    z = road.Z[i - 1] + (road.Z[i] - road.Z[i - 1]) * t;
                    dx = piece > 0.0001f ? (road.X[i] - road.X[i - 1]) / piece : 0f;
                    dz = piece > 0.0001f ? (road.Z[i] - road.Z[i - 1]) / piece : 1f;
                    return;
                }
                along -= piece;
            }
            x = road.X[0];
            y = road.Y[0];
            z = road.Z[0];
            dx = 0f;
            dz = 1f;
        }

        // Where two rays meet ahead of both, if they do.
        private static bool Meet(float ax, float az, float adx, float adz, float bx, float bz, float bdx, float bdz, out float x, out float z)
        {
            x = 0f;
            z = 0f;
            float cross = adx * bdz - adz * bdx;
            if (Math.Abs(cross) < 0.05f)
            {
                return false;   // near parallel: going straight on
            }
            float t = ((bx - ax) * bdz - (bz - az) * bdx) / cross;
            float u = ((bx - ax) * adz - (bz - az) * adx) / cross;
            if (t <= 0f || u <= 0f)
            {
                return false;
            }
            x = ax + adx * t;
            z = az + adz * t;
            return true;
        }

        /// <summary>Degrees from one heading to the next, positive to the left (x east, z north, seen from above).</summary>
        public static float Angle(float ax, float az, float bx, float bz)
        {
            double angle = Math.Atan2(ax * bz - az * bx, ax * bx + az * bz);
            return (float)(angle * 180.0 / Math.PI);
        }

        private static float Distance(float ax, float az, float bx, float bz)
        {
            return (float)Math.Sqrt(Square(ax - bx, az - bz));
        }

        private static float Square(float dx, float dz)
        {
            return dx * dx + dz * dz;
        }
    }
}
