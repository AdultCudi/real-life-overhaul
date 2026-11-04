using System;
using System.Diagnostics;
using System.Globalization;
using System.Threading;
using BAModAPI;
using RealLifeOverhaul.Map25;
using UnityEngine;

namespace RealLifeOverhaul.Nav
{
    /// <summary>
    /// The 2.5D map's city, made once from what the minimap already measured (<see cref="NavBaseData"/>: every building's outline,
    /// its ground and height, the water grid) and the road graph: the blocks in two details (the full map's and the minimap's),
    /// the land and the roads, all in chunks. The arithmetic runs on a pool thread from arrays nothing else writes (about 30 ms
    /// outside the game); the meshes are made on the main thread a slice a frame (at most 0.6 ms). Nothing is made until the
    /// minimap's base is, and nothing at all with both options off. A tint is a write to the few vertices of one building in two
    /// meshes. Cost a frame once made: none (the picture is drawn by <see cref="NavMap25"/> and the panel, and only when the view moves).
    /// </summary>
    public sealed class NavScene
    {
        public static IModLogger Logger;

        private const double SliceMilliseconds = 0.6;   // of a frame, while the meshes are made
        private const float GroundY = -0.3f;            // the shadows' height under the scene's ground plane's zero

        public enum Stage { Idle, Building, Uploading, Ready, Failed }

        private sealed class Job
        {
            public NavBaseData Data;
            public NavGraph Graph;
            public Map25City City;
            public Map25Set Full, Small;
            public Map25Chunk[] Roads, Land;
            public float Y0;
            public volatile bool Done;
            public volatile string Fault;
            public double Milliseconds;

            public void Run(object state)
            {
                try
                {
                    Stopwatch watch = Stopwatch.StartNew();
                    City = Map25City.FromOutlines(Data.Hulls, Data.HullStart, Data.HullX, Data.HullZ, Data.HullKey, Data.HullFoot, Data.HullHeight, out Y0);
                    Full = Map25Set.Build(City, Map25Detail.Full, GroundY);
                    Small = Map25Set.Build(City, Map25Detail.Small, GroundY);
                    RouteSearch roads = Graph.Drive;
                    Roads = Map25Ground.Roads(roads.X, roads.Y, roads.Z, roads.EdgeStart, roads.EdgeTo, roads.EdgeExtra, Graph.Usable, Graph.Speed, roads.Count,
                                              Graph.OriginX, Graph.OriginZ, Graph.WorldWidth, Graph.WorldHeight, Y0);
                    Land = Map25Ground.Land(Data.Cells, Data.Columns, Data.Rows, Data.Cell, Data.OriginX, Data.OriginZ, GroundY * 0.5f);
                    Milliseconds = watch.Elapsed.TotalMilliseconds;
                }
                catch (Exception e)
                {
                    Fault = e.ToString();
                }
                Done = true;
            }
        }

        private Job _job;
        private readonly Map25Blocks _generator = new Map25Blocks();
        private readonly Map25Builder _scratch = new Map25Builder(4096);
        private double _uploadMilliseconds;
        private int _uploadFrames;

        public Stage State { get; private set; }
        public Map25Scene Scene { get; private set; }
        public Map25City City { get; private set; }
        public NavBaseData Data { get; private set; }
        public float Y0 { get; private set; }

        /// <summary>The chunks and vertices and triangles made, for the log and the checks.</summary>
        public string Summary { get; private set; }

        public bool Busy => State == Stage.Building || State == Stage.Uploading;
        public bool Ready => State == Stage.Ready && Scene != null;

        /// <summary>Starts the pool thread's part; the base's data and the graph must be there and stay as they are.</summary>
        public void Begin(NavBaseData data, NavGraph graph)
        {
            if (State != Stage.Idle || data == null || graph == null || !graph.Finished || data.Hulls == 0)
            {
                return;
            }
            string why;
            if (!Map25Scene.Available(out why))
            {
                Fail(why);
                return;
            }
            Data = data;
            _job = new Job { Data = data, Graph = graph };
            State = Stage.Building;
            ThreadPool.QueueUserWorkItem(_job.Run);
        }

        /// <summary>
        /// On the main thread once a frame while <see cref="Busy"/>: takes the pool thread's work when it is done, makes the scene, then
        /// its meshes for 0.6 ms a frame. Returns true while there is more.
        /// </summary>
        public bool Step()
        {
            if (State == Stage.Building)
            {
                Job job = _job;
                if (job == null || !job.Done)
                {
                    return true;
                }
                if (job.Fault != null)
                {
                    Fail(job.Fault);
                    return false;
                }
                string why;
                if (!Map25Scene.Calibrate(out why))
                {
                    Fail(why);
                    return false;
                }
                City = job.City;
                Y0 = job.Y0;
                Scene = new Map25Scene(new[] { job.Full, job.Small }, job.Land, job.Roads, City.MinX, City.MinZ, City.MaxX, City.MaxZ);
                Summary = City.Count + " buildings, " + job.Full.Blocks.Length + " + " + job.Small.Blocks.Length + " block chunks (" + job.Full.Vertices + " + " + job.Small.Vertices + " vertices, "
                          + job.Full.Triangles + " + " + job.Small.Triangles + " triangles), " + job.Land.Length + " land and " + job.Roads.Length + " road chunks, made in "
                          + job.Milliseconds.ToString("F0", CultureInfo.InvariantCulture) + " ms off the main thread";
                _job = null;
                State = Stage.Uploading;
                return true;
            }
            if (State == Stage.Uploading)
            {
                long began = Stopwatch.GetTimestamp();
                bool more = Scene.Upload(SliceMilliseconds);
                _uploadMilliseconds += (Stopwatch.GetTimestamp() - began) * 1000.0 / Stopwatch.Frequency;
                _uploadFrames++;
                if (!more)
                {
                    State = Stage.Ready;
                    Logger?.Info("Navigation: the 2.5D map is made: " + Summary + ", its meshes sent in " + _uploadFrames + " frames ("
                                 + _uploadMilliseconds.ToString("F1", CultureInfo.InvariantCulture) + " ms in all).");
                }
                return more;
            }
            return false;
        }

        /// <summary>Gives building <paramref name="hull"/> a tint (the game's highlight colour), or takes it away; the blocks' colours are written at once. False when it was already so.</summary>
        public bool Tint(int hull, bool on, Color32 colour)
        {
            if (!Ready || hull < 0 || hull >= City.Count)
            {
                return false;
            }
            float share = on ? (colour.r > 235 && colour.g > 235 && colour.b > 235 ? Map25Palette.HoverShare : Map25Palette.TintShare) : 0f;
            if (City.TintShare[hull] == share && (!on || (City.Tint[hull].R == colour.r / 255f && City.Tint[hull].G == colour.g / 255f && City.Tint[hull].B == colour.b / 255f)))
            {
                return false;
            }
            City.Tint[hull] = new Map25Colour(colour.r / 255f, colour.g / 255f, colour.b / 255f);
            City.TintShare[hull] = share;
            // the first set is the full map's, the second the minimap's: a tint reaches both
            Scene.Retint(City, hull, _generator, _scratch);
            return true;
        }

        private void Fail(string why)
        {
            State = Stage.Failed;
            _job = null;
            Logger?.Warn("Navigation: the 2.5D map is not made, the flat minimap and the game's own city map stay: " + why);
        }

        public void Dispose()
        {
            _job = null;
            if (Scene != null)
            {
                Scene.Dispose();
                Scene = null;
            }
            City = null;
            State = Stage.Idle;
        }
    }
}
