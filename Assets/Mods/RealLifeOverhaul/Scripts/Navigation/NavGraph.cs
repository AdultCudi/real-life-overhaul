using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using BAModAPI;
using GleyTrafficSystem;
using GleyUrbanAssets;
using Streets;
using UnityEngine;

namespace RealLifeOverhaul.Nav
{
    /// <summary>
    /// The city's roads as the minimap and the GPS use them, made once per city in four short steps
    /// (<see cref="Step"/>, one per call, each named for the session record):
    ///
    ///   1  the road graph for driving (Core/RoadGraph, the builder the bus network uses), and for each lane point
    ///      its road's number, whether it is a junction's, and its speed;
    ///   2  the same roads for walking (every edge both ways), and the grid that answers "which lane points are near";
    ///   3  the map picture, drawn from the graph on a thread of the pool (it reads these arrays and writes its own);
    ///   4  the picture handed to the graphics card, once the thread is done.
    ///
    /// After that nothing here runs unless it is asked: a route search is begun and advanced by the director, and a
    /// street's name is worked out the first time a turn onto it is announced.
    /// </summary>
    public sealed class NavGraph
    {
        public const float LaneChangeCost = 30f;   // metres a change of lane counts for (as the bus network has it)
        private const float Margin = 24f;          // metres of empty picture round the outermost road
        private const int LongestSide = 4096;      // pixels
        private const float MostPerMetre = 1.25f;
        private const float LaneHalfWidth = 2.3f;  // metres either side of a lane's middle that are drawn as road

        public static IModLogger Logger;

        private Waypoint[] _points;
        private int _step;
        private PictureJob _job;
        private float[] _doorX, _doorY, _doorZ;    // every building's door, and the street of its address
        private string[] _doorStreet;
        private string[] _streetOfRoad;            // by road number; "" = looked for and not found
        private readonly Dictionary<string, int> _votes = new Dictionary<string, int>(8);

        public RouteSearch Drive { get; private set; }
        public RouteSearch Walk { get; private set; }
        public NavIndex Places { get; private set; }
        public int[] Road { get; private set; }
        public bool[] Junction { get; private set; }
        public bool[] Usable { get; private set; }
        public float[] Speed { get; private set; }
        public int Roads { get; private set; }

        /// <summary>The map picture: one byte a pixel, how much road is there. Null until step 4 (or if it could not be made).</summary>
        public Texture2D Picture { get; private set; }
        public float OriginX { get; private set; }
        public float OriginZ { get; private set; }
        public float WorldWidth { get; private set; }
        public float WorldHeight { get; private set; }

        /// <summary>True once routes can be searched for (after step 2).</summary>
        public bool Ready => _step >= 3;

        /// <summary>True once every step is done, with or without a picture.</summary>
        public bool Finished => _step >= 5;

        // What the thread draws into, and what it says when it is done. Nothing of the engine is touched off the main thread.
        private sealed class PictureJob
        {
            public byte[] Pixels;
            public int Width, Height, Count;
            public float OriginX, OriginZ, PerMetre;
            public float[] X, Z, Extra;
            public int[] Start, To;
            public bool[] Usable;
            public volatile bool Done;
            public volatile string Fault;
            public double Milliseconds;
            public int Bands;

            public void Run(object state)
            {
                try
                {
                    Stopwatch watch = Stopwatch.StartNew();
                    Bands = NavRaster.Draw(Pixels, Width, Height, OriginX, OriginZ, PerMetre, LaneHalfWidth, X, Z, Start, To, Extra, Usable, Count);
                    Milliseconds = watch.Elapsed.TotalMilliseconds;
                }
                catch (Exception e)
                {
                    Fault = e.ToString();
                }
                Done = true;
            }
        }

        /// <summary>Does the next step if it can be done now. Call from a timer until <see cref="Finished"/>.</summary>
        public void Step()
        {
            switch (_step)
            {
                case 0:
                    if (Begin())
                    {
                        _step = 1;
                    }
                    break;
                case 1:
                    OnFootAndIndex();
                    _step = 3;
                    break;
                case 3:
                    StartPicture();
                    _step = 4;
                    break;
                case 4:
                    if (_job == null || _job.Done)
                    {
                        FinishPicture();
                        _step = 5;
                    }
                    break;
            }
        }

        private bool Begin()
        {
            CurrentSceneData data = CurrentSceneData.GetSceneInstance();
            if (data == null || data.allWaypoints == null || data.allWaypoints.Length < 100)
            {
                return false;   // the city's roads are not there yet
            }
            SessionWatch.Mark("the GPS's road graph");
            Stopwatch watch = Stopwatch.StartNew();
            _points = data.allWaypoints;
            int count = _points.Length;
            Drive = RoadGraph.Build(_points, LaneChangeCost, out int laneChanges);
            Road = new int[count];
            Junction = new bool[count];
            Usable = new bool[count];
            Speed = new float[count];
            int[] start = Drive.EdgeStart;
            int[] to = Drive.EdgeTo;
            int roads = 0;
            for (int i = 0; i < count; i++)
            {
                Waypoint point = _points[i];
                Road[i] = -1;
                if (point == null)
                {
                    continue;
                }
                Road[i] = NavMath.RoadOf(point.name);
                Junction[i] = NavMath.IsConnector(point.name);
                Speed[i] = point.maxSpeed > 0 ? point.maxSpeed / 3.6f : 13.9f;
                roads = Mathf.Max(roads, Road[i] + 1);
                // A point is part of the roads if a lane leads from it or to it.
                for (int e = start[i]; e < start[i + 1]; e++)
                {
                    Usable[i] = true;
                    Usable[to[e]] = true;
                }
            }
            Roads = roads;
            _streetOfRoad = new string[roads];
            Logger?.Info("Navigation: road graph made: " + count + " lane points, " + to.Length + " edges (" + laneChanges + " of them changes of lane), "
                         + roads + " roads, " + watch.Elapsed.TotalMilliseconds.ToString("F1") + " ms.");
            return true;
        }

        private void OnFootAndIndex()
        {
            SessionWatch.Mark("the GPS's walking graph");
            Stopwatch watch = Stopwatch.StartNew();
            NavEdges.TwoWay(Drive.Count, Drive.EdgeStart, Drive.EdgeTo, out int[] start, out int[] to, out float[] extra);
            Walk = new RouteSearch(Drive);
            Walk.SetEdges(start, to, extra);
            Places = new NavIndex(Drive.X, Drive.Z, Usable, Drive.Count);
            Logger?.Info("Navigation: walking graph and index made: " + to.Length + " edges, " + Places.Indexed + " lane points in the index, "
                         + watch.Elapsed.TotalMilliseconds.ToString("F1") + " ms.");
        }

        private void StartPicture()
        {
            if (Places.Indexed == 0)
            {
                return;
            }
            OriginX = Places.MinX - Margin;
            OriginZ = Places.MinZ - Margin;
            WorldWidth = Places.MaxX - Places.MinX + Margin * 2f;
            WorldHeight = Places.MaxZ - Places.MinZ + Margin * 2f;
            NavRaster.Size(WorldWidth, WorldHeight, Mathf.Min(LongestSide, SystemInfo.maxTextureSize), MostPerMetre, out int width, out int height, out float perMetre);
            _job = new PictureJob
            {
                Pixels = new byte[width * height],
                Width = width,
                Height = height,
                Count = Drive.Count,
                OriginX = OriginX,
                OriginZ = OriginZ,
                PerMetre = perMetre,
                X = Drive.X,
                Z = Drive.Z,
                Start = Drive.EdgeStart,
                To = Drive.EdgeTo,
                Extra = Drive.EdgeExtra,
                Usable = Usable
            };
            // The picture's world size is exactly what its pixels cover, so a metre is the same number of pixels both ways.
            WorldWidth = width / perMetre;
            WorldHeight = height / perMetre;
            ThreadPool.QueueUserWorkItem(_job.Run);
        }

        private void FinishPicture()
        {
            PictureJob job = _job;
            _job = null;
            if (job == null)
            {
                return;
            }
            if (job.Fault != null)
            {
                Logger?.Warn("Navigation: the map picture could not be drawn, so the minimap shows no roads. " + job.Fault);
                return;
            }
            SessionWatch.Mark("the minimap's picture");
            Stopwatch watch = Stopwatch.StartNew();
            var picture = new Texture2D(job.Width, job.Height, TextureFormat.Alpha8, false, true)
            {
                name = "RealLife Minimap Roads",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                anisoLevel = 0
            };
            picture.SetPixelData(job.Pixels, 0);
            picture.Apply(false, true);   // sent to the graphics card; the copy in main memory is let go
            Picture = picture;
            Logger?.Info("Navigation: map made: " + job.Width + " x " + job.Height + " pixels (" + job.PerMetre.ToString("F2") + " a metre, "
                         + (job.Pixels.Length / 1048576f).ToString("F1") + " MB), " + job.Bands + " lanes drawn in " + job.Milliseconds.ToString("F0")
                         + " ms off the main thread, sent to the graphics card in " + watch.Elapsed.TotalMilliseconds.ToString("F1") + " ms.");
        }

        public void Dispose()
        {
            if (Picture != null)
            {
                UnityEngine.Object.Destroy(Picture);
                Picture = null;
            }
            _job = null;   // a thread still drawing finishes into an array nobody reads
            _points = null;
        }

        // ---------------------------------------------------------------- street names

        /// <summary>
        /// What the street a road belongs to is called, or null. The road graph has no names; buildings have
        /// addresses. So a road is named after the street most of the buildings along it give as their address:
        /// up to seven places along the road each ask for the building whose door is nearest (within 45 m), and a
        /// name needs two of them (or the only one, on a short road). Worked out once per road, when first asked for; the doors are read once.
        /// </summary>
        public string StreetOf(int road)
        {
            if (road < 0 || _streetOfRoad == null || road >= _streetOfRoad.Length)
            {
                return null;
            }
            if (_streetOfRoad[road] == null)
            {
                _streetOfRoad[road] = Name(road) ?? string.Empty;
            }
            return _streetOfRoad[road].Length > 0 ? _streetOfRoad[road] : null;
        }

        private string Name(int road)
        {
            if (_doorStreet == null)
            {
                Doors();
            }
            if (_doorStreet.Length == 0)
            {
                return null;
            }
            int points = 0;
            for (int i = 0; i < Road.Length; i++)
            {
                points += Road[i] == road && !Junction[i] ? 1 : 0;
            }
            if (points == 0)
            {
                return null;
            }
            _votes.Clear();
            int every = Mathf.Max(1, points / 7);
            int seen = 0, asked = 0;
            for (int i = 0; i < Road.Length && asked < 7; i++)
            {
                if (Road[i] != road || Junction[i] || seen++ % every != 0)
                {
                    continue;
                }
                asked++;
                string street = StreetAt(Drive.X[i], Drive.Y[i], Drive.Z[i]);
                if (street != null)
                {
                    _votes.TryGetValue(street, out int votes);
                    _votes[street] = votes + 1;
                }
            }
            string best = null;
            int most = 0;
            foreach (KeyValuePair<string, int> vote in _votes)
            {
                if (vote.Value > most)
                {
                    most = vote.Value;
                    best = vote.Key;
                }
            }
            _votes.Clear();
            if (best == null || (most < 2 && asked > 2))
            {
                return null;
            }
            string name = AddressHelper.GetStreetNameLocalized(best);
            return string.IsNullOrEmpty(name) ? null : name;
        }

        /// <summary>The street of the building whose door is nearest a place (within 45 m), as the game writes it; null if there is none.</summary>
        public string StreetNear(float x, float y, float z)
        {
            if (_doorStreet == null)
            {
                Doors();
            }
            string street = StreetAt(x, y, z);
            if (street == null)
            {
                return null;
            }
            string name = AddressHelper.GetStreetNameLocalized(street);
            return string.IsNullOrEmpty(name) ? null : name;
        }

        // Every building's door and the street in its address, read once (the first time a name is wanted).
        private void Doors()
        {
            CityManager city = InstanceBehavior<CityManager>.Instance;
            CityBuildingController[] buildings = city != null ? city.cityBuildingControllers : null;
            int count = buildings != null ? buildings.Length : 0;
            var x = new List<float>(count);
            var y = new List<float>(count);
            var z = new List<float>(count);
            var street = new List<string>(count);
            for (int i = 0; i < count; i++)
            {
                CityBuildingController building = buildings[i];
                if (building == null || building.building == null)
                {
                    continue;
                }
                Address address = building.building.Address;
                if (address == null || string.IsNullOrEmpty(address.streetName))
                {
                    continue;
                }
                Vector3 door = building.entranceDoors != null && building.entranceDoors.Length > 0 && building.entranceDoors[0] != null && building.entranceDoors[0].doorTransform != null
                    ? building.entranceDoors[0].doorTransform.position
                    : building.transform.position;
                x.Add(door.x);
                y.Add(door.y);
                z.Add(door.z);
                street.Add(address.streetName);
            }
            _doorX = x.ToArray();
            _doorY = y.ToArray();
            _doorZ = z.ToArray();
            _doorStreet = street.ToArray();
        }

        // The street in the address of the building whose door is nearest a place, within 45 m and on the same level.
        private string StreetAt(float x, float y, float z)
        {
            string street = null;
            float best = 45f * 45f;
            for (int i = 0; i < _doorStreet.Length; i++)
            {
                float dx = _doorX[i] - x;
                float dz = _doorZ[i] - z;
                float sqr = dx * dx + dz * dz;
                if (sqr < best && Mathf.Abs(_doorY[i] - y) <= 10f)
                {
                    best = sqr;
                    street = _doorStreet[i];
                }
            }
            return street;
        }
    }
}
