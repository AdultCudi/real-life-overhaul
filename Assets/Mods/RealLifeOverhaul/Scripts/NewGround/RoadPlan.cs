using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.NewGround
{
    /// <summary>
    /// The traffic system's road grid as the game has it at this load (GleyUrbanAssets.CurrentSceneData: gridCorner,
    /// gridCellSize, grid). Read from the game at every load and never kept from an earlier one, so a game update that
    /// moves or grows the grid is followed. The game's own lookup (CurrentSceneData.GetCell) has no bounds check: a point
    /// north or east of the grid throws in the traffic's every frame, one south or west of its corner is mirrored into the
    /// wrong cell, so a new lane point is refused unless it is <see cref="Inside"/>. No Unity or game types.
    /// </summary>
    public struct RoadGrid
    {
        public float CornerX;   // m: the grid's south-west corner, the game's gridCorner
        public float CornerZ;
        public float Cell;      // m: the side of a cell, the game's gridCellSize
        public int Rows;        // cells along z, the game's grid.Length
        public int Columns;     // cells along x, the length of the game's first row

        public RoadGrid(float cornerX, float cornerZ, float cell, int rows, int columns)
        {
            CornerX = cornerX;
            CornerZ = cornerZ;
            Cell = cell;
            Rows = rows;
            Columns = columns;
        }

        public bool Valid => Cell > 0f && Rows > 0 && Columns > 0;

        public float EastEdge => CornerX + Columns * Cell;

        public float NorthEdge => CornerZ + Rows * Cell;

        /// <summary>The row the game looks a point up in, for a point on the grid.</summary>
        public int Row(float z)
        {
            return (int)Math.Floor((z - CornerZ) / Cell);
        }

        /// <summary>The column the game looks a point up in, for a point on the grid.</summary>
        public int Column(float x)
        {
            return (int)Math.Floor((x - CornerX) / Cell);
        }

        /// <summary>True for a point the game's lookup takes safely: on the grid and at least <paramref name="margin"/> m from each edge.</summary>
        public bool Inside(float x, float z, float margin)
        {
            return Valid && x >= CornerX + margin && z >= CornerZ + margin && x < EastEdge - margin && z < NorthEdge - margin;
        }

        /// <summary>Metres from a point to the nearest edge of the grid; negative off it.</summary>
        public float Room(float x, float z)
        {
            return Math.Min(Math.Min(x - CornerX, EastEdge - x), Math.Min(z - CornerZ, NorthEdge - z));
        }
    }

    /// <summary>One road of a plan: its middle line, its lanes each way, its speed and who may drive it.</summary>
    public sealed class PlanRoad
    {
        public readonly List<float> X = new List<float>(8);   // m: the middle line, from the road's start to its end
        public readonly List<float> Y = new List<float>(8);
        public readonly List<float> Z = new List<float>(8);
        public int Ahead = 1;              // lanes the way the line runs
        public int Back = 1;               // lanes the other way
        public float LaneWidth = 3.5f;     // m: one lane; the game's lanes side by side stand 3.5 to 4 m apart
        public int Speed = 50;             // km/h: the game's own lanes have 30, 50 or 80
        public bool Cars = true;
        public bool Trucks = true;
        public int Start = -1;             // the junction the road starts at (an index into the plan's junctions), or -1
        public int End = -1;               // the junction it ends at, or -1
        public float StartInset = -1f;     // m its lanes stop short of the junction at its start; below zero, that junction's own Inset
        public float EndInset = -1f;       // m, the same at its end: a street's lanes stop further back than an avenue's, as at the game's crossings

        /// <summary>Adds a point of the middle line; returns the road, so a plan reads as one statement.</summary>
        public PlanRoad Point(float x, float y, float z)
        {
            X.Add(x);
            Y.Add(y);
            Z.Add(z);
            return this;
        }
    }

    /// <summary>Where roads of a plan meet: with traffic lights, or by priority (the game's priority junction weighs four roads at most).</summary>
    public sealed class PlanJunction
    {
        public float X;                    // m: its middle
        public float Y;
        public float Z;
        public bool Lights = true;
        public float Inset = 9f;           // m: the lanes stop this far short of its middle, where the turns begin
        public float Green = 10f;          // s: green for one road, the game's own junctions' time
        public float Yellow = 2f;          // s: yellow, the same
        public readonly List<int> Together = new List<int>(2);   // the plan's roads whose lanes in share one way of the lights, as an avenue's two directions at the game's own crossings
        public readonly List<int> Across = new List<int>(2);     // the plan's roads sharing a second way: a street's two arms across an avenue, as at the game's crossings
    }

    /// <summary>
    /// An open end of a road of the plan that meets one of the game's junctions with lights, as a street of a district
    /// continues one of the game's across a T: the road's lanes become the junction's own, as the game lays its four-way
    /// crossings. A lane leaving the junction is one of its exits, reached from its stop points by turns of the game's kind;
    /// a lane coming into it stops in the way whose lanes run against it (both directions of a street go on one green, as
    /// at the game's own crossings) and turns into its exits. The junction is found again at every load near
    /// (<see cref="X"/>, <see cref="Z"/>) and read by the game's side into <see cref="Stops"/> and <see cref="Exits"/>:
    /// nothing of it is kept by index.
    /// </summary>
    public sealed class PlanThrough
    {
        public int Road;                   // the plan's road
        public bool AtStart = true;        // its start meets the junction; false: its end
        public float X;                    // m: where the junction is looked for, near the middle of its stop points
        public float Z;
        public int Existing = -1;          // the game's junction with lights at this load (its index among them), set by the game's side
        public readonly List<GamePoint> Stops = new List<GamePoint>(8);   // its stop points at this load, set by the game's side
        public readonly List<GamePoint> Exits = new List<GamePoint>(8);   // its exits at this load, the same
    }

    /// <summary>One of the game's own lane points at a junction, as the game's side reads it at a load: never kept.</summary>
    public struct GamePoint
    {
        public int Index;                  // in the game's array at this load
        public float X;                    // m
        public float Y;
        public float Z;
        public float Dx;                   // the way its lane runs there: a stop point's way in, an exit's way out
        public float Dz;
        public int Way;                    // a stop point's way of the junction's lights; -1 for an exit
        public int RoadId;                 // the game's road id
        public string Lane;                // its lane's name as the game writes it ("Road_69-Lane_0"), for the turns named after it
        public int Speed;                  // km/h
        public bool Cars;
        public bool Trucks;
    }

    /// <summary>
    /// An open end of a new lane joined to one of the game's own lanes. The game's lane point is found again at every load
    /// near (<see cref="X"/>, <see cref="Z"/>): its index is never kept, since a game update renumbers the lanes.
    /// </summary>
    public sealed class PlanJoin
    {
        public int Road;                   // the plan's road
        public int Lane;                   // 0 to Ahead-1 the way the road runs (0 nearest the middle), then the lanes the other way
        public bool Fork;                  // true: the game's point leads into the lane's first point; false: the lane's last point leads into the game's
        public float X;                    // m: where the game's lane point is looked for
        public float Z;
        public int Existing = -1;          // the game's lane point at this load, set by the game's side before the build
    }

    /// <summary>A plan of new streets: roads, the junctions where they meet, their joins to the game's own lanes, and their ends through the game's junctions.</summary>
    public sealed class RoadPlan
    {
        public string Name;
        public readonly List<PlanRoad> Roads = new List<PlanRoad>(8);
        public readonly List<PlanJunction> Junctions = new List<PlanJunction>(4);
        public readonly List<PlanJoin> Joins = new List<PlanJoin>(4);
        public readonly List<PlanThrough> Throughs = new List<PlanThrough>(4);
    }

    /// <summary>One new lane point, ready to be made a lane point of the game's (GleyTrafficSystem.Waypoint).</summary>
    public sealed class LanePoint
    {
        public string Name;
        public float X;
        public float Y;
        public float Z;
        public readonly List<int> Next = new List<int>(2);     // indices in the whole array, the game's and the new together
        public readonly List<int> Prev = new List<int>(2);
        public readonly List<int> Beside = new List<int>(1);   // the game's otherLanes: a step ahead on the lane beside, the same way
        public bool Cars;
        public bool Trucks;
        public int Speed;                                       // km/h
        public int RoadId;
        public int Row;
        public int Column;
        public bool Spawn;                                      // the traffic may put a car on the road here
        public bool Through;                                    // a turn through one of the game's junctions: inside it, beside its own lanes by design
        public int Keeps = -1;                                  // such a turn from one of the game's stop points keeps that lane's road id; -1 otherwise
    }

    /// <summary>A new junction, ready to be made one of the game's (TrafficLightsIntersection or PriorityIntersection).</summary>
    public sealed class BuiltJunction
    {
        public string Name;
        public bool Lights;
        public float Green;
        public float Yellow;
        public readonly List<List<int>> Ways = new List<List<int>>(4);   // per road coming in: its lanes' last points, where cars stop or give way
        public readonly List<int> Exits = new List<int>(8);               // the first points of the lanes leaving it
        public readonly List<int> Cells = new List<int>(4);               // row * columns + column of each cell its points are in
    }

    /// <summary>A link from one of the game's lane points to a new one: added to its neighbours (a fork) or to its previous points (a merge).</summary>
    public struct LinkEdit
    {
        public int Existing;
        public int Added;
        public bool AsNext;

        public LinkEdit(int existing, int added, bool asNext)
        {
            Existing = existing;
            Added = added;
            AsNext = asNext;
        }
    }

    /// <summary>
    /// A new lane point made one of a game junction's own: a stop point in one of its ways (with the place the way's lamps
    /// for that approach are looked for, a little ahead of it, where the game hangs a light), or an exit.
    /// </summary>
    public struct ThroughEdit
    {
        public int Junction;    // the game's junction with lights (its index among them)
        public int Way;         // the way a stop point joins; -1 for an exit
        public int Point;       // the new lane point, in the whole array
        public float LampX;     // m: where a stop point's lamps are looked for
        public float LampZ;

        public ThroughEdit(int junction, int way, int point, float lampX, float lampZ)
        {
            Junction = junction;
            Way = way;
            Point = point;
            LampX = lampX;
            LampZ = lampZ;
        }
    }

    /// <summary>What a plan comes to: the lane points to append after the game's own, the junctions, the links into the game's lanes.</summary>
    public sealed class RoadBuild
    {
        public readonly int First;          // the index of the first new point: the game's count of lane points at this load
        public readonly int FirstRoadId;    // the first new road id, above every id the game uses at this load
        public readonly List<LanePoint> Points = new List<LanePoint>(64);
        public readonly List<BuiltJunction> Junctions = new List<BuiltJunction>(4);
        public readonly List<LinkEdit> Edits = new List<LinkEdit>(4);
        public readonly List<ThroughEdit> Through = new List<ThroughEdit>(4);   // new points made stop points and exits of the game's junctions
        public int Roads;                   // road ids used, from FirstRoadId on
        public string Problem;              // null when the build may be appended

        public RoadBuild(int first, int firstRoadId)
        {
            First = first;
            FirstRoadId = firstRoadId;
        }

        public int Count => Points.Count;

        /// <summary>The point at an index of the whole array, or null for one of the game's own.</summary>
        public LanePoint At(int index)
        {
            int local = index - First;
            return local >= 0 && local < Points.Count ? Points[local] : null;
        }
    }
}
