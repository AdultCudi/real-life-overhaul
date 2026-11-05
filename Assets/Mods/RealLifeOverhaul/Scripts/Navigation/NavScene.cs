using System;
using System.Collections.Generic;
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
    /// its ground and height, the water grid), the road graph and the game's trees: the blocks in the minimap's detail,
    /// the land, the green ground of the parks, the roads with their lane marking and the trees, all in chunks. The arithmetic runs on a pool thread
    /// from arrays nothing else writes (about 50 ms outside the game); the meshes are made on the main thread a slice a frame (at most 0.6 ms).
    /// Nothing is made until the minimap's base is, and nothing at all with the option off. Cost a frame once made: none (the picture is drawn by the minimap's panel,
    /// and only when the view moves).
    /// </summary>
    public sealed class NavScene
    {
        public static IModLogger Logger;

        private const double SliceMilliseconds = 0.6;   // of a frame, while the meshes are made
        private const float GroundY = -0.3f;            // the contact patches' height under the scene's ground plane's zero
        private const int MostTrees = 14000;            // trees taken from the game: past this, every second one

        public enum Stage { Idle, Building, Uploading, Ready, Failed }

        private sealed class Job
        {
            public NavBaseData Data;
            public NavGraph Graph;
            public float[] TreeX, TreeZ;
            public int Trees;
            public Map25City City;
            public Map25Set Small;
            public Map25Under Under;
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
                    Small = Map25Set.Build(City, GroundY);
                    RouteSearch roads = Graph.Drive;
                    Under = Map25Under.Make(Data.Cells, Data.Columns, Data.Rows, Data.Cell, Data.OriginX, Data.OriginZ, GroundY * 0.5f,
                                            roads.X, roads.Y, roads.Z, roads.EdgeStart, roads.EdgeTo, roads.EdgeExtra, Graph.Usable, Graph.Speed, roads.Count,
                                            Graph.OriginX, Graph.OriginZ, Graph.WorldWidth, Graph.WorldHeight, Y0,
                                            TreeX, TreeZ, Trees, City.MinX - 100f, City.MinZ - 100f, City.MaxX + 100f, City.MaxZ + 100f);
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

        /// <summary>Starts the pool thread's part; the base's data and the graph must be there and stay as they are. The game's trees are read here, on the main thread.</summary>
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
            ReadTrees(_job);
            State = Stage.Building;
            ThreadPool.QueueUserWorkItem(_job.Run);
        }

        // Where the game's trees stand (TreeController.AllTrees, which every tree joins as it starts): two arrays of places, once, a few hundred microseconds.
        private static void ReadTrees(Job job)
        {
            List<TreeController> all = TreeController.AllTrees;
            int stride = all.Count > MostTrees ? (all.Count + MostTrees - 1) / MostTrees : 1;
            var x = new float[all.Count / stride + 1];
            var z = new float[x.Length];
            int n = 0;
            for (int i = 0; i < all.Count; i += stride)
            {
                TreeController tree = all[i];
                if (tree == null)
                {
                    continue;
                }
                Vector3 at = tree.transform.position;
                x[n] = at.x;
                z[n] = at.z;
                n++;
            }
            job.TreeX = x;
            job.TreeZ = z;
            job.Trees = n;
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
                Scene = new Map25Scene(job.Small, job.Under, City.MinX, City.MinZ, City.MaxX, City.MaxZ);
                CultureInfo plain = CultureInfo.InvariantCulture;
                Summary = City.Count + " buildings, " + job.Small.Vertices + " block vertices (" + job.Small.Triangles + " triangles), " + job.Under.Land.Length + " land, "
                          + job.Under.Roads.Length + " road and " + job.Under.Trees.Length + " tree chunks ("
                          + job.Trees + " trees, " + job.Under.Greens.Length + " green), made in " + job.Milliseconds.ToString("F0", plain) + " ms off the main thread";
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
                    Logger?.Info("Navigation: the minimap's 2.5D picture is made: " + Summary + ", its meshes sent in " + _uploadFrames + " frames ("
                                 + _uploadMilliseconds.ToString("F1", CultureInfo.InvariantCulture) + " ms in all).");
                }
                return more;
            }
            return false;
        }

        private void Fail(string why)
        {
            State = Stage.Failed;
            _job = null;
            Logger?.Warn("Navigation: the minimap's 2.5D picture is not made, the flat minimap stays: " + why);
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
