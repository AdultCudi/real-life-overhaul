using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using BAModAPI;
using UnityEngine;

namespace RealLifeOverhaul.Nav
{
    /// <summary>
    /// The minimap's base picture: the city's buildings and its water, under the roads. One texture, made once per
    /// city load and shown by one picture on the minimap whose window on it moves with the player (NavHud).
    ///
    /// The game's own city map is the city itself under a camera, not a picture (see NavBaseGame), so this is drawn
    /// from what the game keeps, in the game's palette:
    ///
    ///   1  the measured city is read from the mod's folder if this version of the game has been measured before
    ///      (about 150 KB, one read);
    ///   2  otherwise every building's outline is read, a few a frame, and
    ///   3  the city is asked where its water is, square by square of 8 m, a few a frame (one ray each);
    ///      steps 2 and 3 take 0.8 ms of a frame at most, for some seconds, once per version of the game;
    ///   4  the picture is drawn from that on a thread of the pool (and the measured city written to the mod's folder);
    ///   5  the picture goes to the graphics card: one call, the same as the road picture's.
    ///
    /// The size. The picture is at most 2048 pixels on its long side, four bytes a pixel and no smaller copies: for
    /// the game's city (3.6 km by 2 km) 2048 x 1154, 9.0 MB on the graphics card and nothing in main memory once it
    /// is there. That is 1.77 m a pixel. The minimap's view is at most 676 units of the HUD wide (338 pixels on a
    /// 1920 screen) and shows 260 m at the usual zoom: a pixel of the picture is 2.3 pixels of such a screen (4.6 at
    /// 3840), smoothed by the graphics card. A building is 10 to 30 pixels of the picture, so its shape holds; only
    /// its edge is soft, by under a metre, where the roads above it are sharp (their picture has 4096). More would
    /// buy a sharper edge on a flat shape and cost four times the memory.
    ///
    /// Cost. Nothing per frame once the picture is made (<see cref="Busy"/> is false: the director's one test). While
    /// a city is measured: 0.8 ms a frame. Per city load: one read of the file, one thread job of some tens of
    /// milliseconds off the main thread, one upload.
    /// </summary>
    public sealed class NavBase
    {
        private const double SliceMilliseconds = 0.8;   // of one frame, while a city is measured
        private const int LookEvery = 8;                // the clock is looked at after this many buildings or squares
        private const float RoadReach = 8f;             // metres: a square this near a lane point is road, and is not asked
        private const float StreetReach = 150f;         // metres within which the nearest road gives the street's height
        private const float Sky = 400f;                 // metres above the highest road from which a square is looked down on (over every roof)

        public static IModLogger Logger;

        private enum Stage { Idle, Buildings, Water, Drawing, Done, Failed }

        private Stage _stage;
        private NavGraph _graph;
        private NavBaseData _data;
        private Job _job;
        private string _file;
        private Color32 _building, _water;
        private bool _fromFile;
        private int _cursor;
        private float _top, _depth;
        private int _asked, _frames;
        private double _measureMilliseconds, _longestSlice;
        private readonly List<int> _hullStart = new List<int>(2048);
        private readonly List<int> _hullOf = new List<int>(2048);
        private readonly List<float> _hullX = new List<float>(8192);
        private readonly List<float> _hullZ = new List<float>(8192);
        private readonly float[] _cornerX = new float[8], _cornerZ = new float[8];
        private readonly float[] _outX = new float[17], _outZ = new float[17];

        /// <summary>The picture, once it is on the graphics card; null before, and if it could not be made.</summary>
        public Texture2D Picture { get; private set; }
        public int Width { get; private set; }
        public int Height { get; private set; }
        public float PerMetre { get; private set; }
        public float OriginX { get; private set; }
        public float OriginZ { get; private set; }

        /// <summary>The piece of the world the picture covers, in metres from its origin: exactly what its pixels cover.</summary>
        public float WorldWidth { get; private set; }
        public float WorldHeight { get; private set; }

        /// <summary>What the picture was drawn from (kept for the checks and the tests: some hundreds of outlines and a byte per square).</summary>
        public NavBaseData Data => _stage == Stage.Done ? _data : null;

        /// <summary>True while there is work to do in <see cref="Step"/>.</summary>
        public bool Busy => _stage == Stage.Buildings || _stage == Stage.Water || _stage == Stage.Drawing;

        public bool Idle => _stage == Stage.Idle;
        public bool Failed => _stage == Stage.Failed;

        /// <summary>True if the last picture came from the mod's folder and not from measuring the city.</summary>
        public bool FromFile => _fromFile;

        // What the thread works on, and what it says when it is done. Nothing of the engine is touched off the main thread.
        private sealed class Job
        {
            public NavBaseData Data;
            public bool Settle;
            public string SaveTo;
            public byte[] Rgba;
            public int Width, Height;
            public float OriginX, OriginZ, PerMetre;
            public Color32 Building, Water;
            public volatile bool Done;
            public volatile string Fault;
            public string SaveFault;
            public double Milliseconds;
            public int Drawn, WaterCells;

            public void Run(object state)
            {
                try
                {
                    Stopwatch watch = Stopwatch.StartNew();
                    if (Settle)
                    {
                        NavBaseWater.Settle(Data.Cells, Data.Columns, Data.Rows);
                        try
                        {
                            Directory.CreateDirectory(Path.GetDirectoryName(SaveTo));
                            File.WriteAllBytes(SaveTo, Data.Encode());
                        }
                        catch (Exception e)
                        {
                            SaveFault = e.Message;   // the picture is still made; the city is measured again next time
                        }
                    }
                    for (int i = 0; i < Data.Cells.Length; i++)
                    {
                        WaterCells += Data.Cells[i] == NavBaseWater.Water ? 1 : 0;
                    }
                    var cover = new byte[Width * Height];
                    Drawn = NavBaseRaster.Buildings(cover, Width, Height, OriginX, OriginZ, PerMetre, Data);
                    Rgba = new byte[Width * Height * 4];
                    NavBaseRaster.Compose(Rgba, cover, Width, Height, OriginX, OriginZ, PerMetre, Data,
                                          Building.r, Building.g, Building.b, Building.a, Water.r, Water.g, Water.b, Water.a);
                    Milliseconds = watch.Elapsed.TotalMilliseconds;
                }
                catch (Exception e)
                {
                    Fault = e.ToString();
                }
                Done = true;
            }
        }

        /// <summary>
        /// Begins: from the mod's folder if this city has been measured (and <paramref name="measureAgain"/> is not
        /// asked for), by measuring it otherwise. Does nothing while the game's palette is not there yet (it is
        /// asked again on the director's next tick).
        /// </summary>
        public void Begin(NavGraph graph, bool measureAgain)
        {
            if (_stage != Stage.Idle || graph == null || !graph.Finished)
            {
                return;
            }
            if (graph.Places == null || graph.Places.Indexed == 0 || graph.WorldWidth <= 0f || graph.WorldHeight <= 0f)
            {
                Fail("the city's roads were not found, so there is nothing to lay it on");
                return;
            }
            if (!NavBaseGame.Colours(out _building, out _water))
            {
                return;
            }
            _graph = graph;
            int build = NavBaseGame.Build();
            _file = NavBaseGame.FilePath(build);
            OriginX = graph.OriginX;
            OriginZ = graph.OriginZ;
            NavRaster.Size(graph.WorldWidth, graph.WorldHeight, Mathf.Min(NavBaseMath.LongestSide, SystemInfo.maxTextureSize), NavBaseMath.MostPerMetre,
                           out int width, out int height, out float perMetre);
            Width = width;
            Height = height;
            PerMetre = perMetre;
            WorldWidth = width / perMetre;
            WorldHeight = height / perMetre;
            if (measureAgain)
            {
                NavBaseGame.Delete(_file);
            }
            else
            {
                NavBaseData kept = NavBaseData.Decode(NavBaseGame.Read(_file), build, OriginX, OriginZ, graph.WorldWidth, graph.WorldHeight);
                if (kept != null)
                {
                    _data = kept;
                    _fromFile = true;
                    Draw(false);
                    return;
                }
            }
            _fromFile = false;
            _data = new NavBaseData
            {
                Build = build,
                OriginX = OriginX,
                OriginZ = OriginZ,
                WorldWidth = graph.WorldWidth,
                WorldHeight = graph.WorldHeight,
                Cell = NavBaseWater.CellMetres,
                Columns = Mathf.Max(1, Mathf.CeilToInt(WorldWidth / NavBaseWater.CellMetres)),
                Rows = Mathf.Max(1, Mathf.CeilToInt(WorldHeight / NavBaseWater.CellMetres))
            };
            _data.Cells = new byte[_data.Columns * _data.Rows];
            _hullStart.Clear();
            _hullOf.Clear();
            _hullX.Clear();
            _hullZ.Clear();
            _cursor = 0;
            _asked = 0;
            _frames = 0;
            _measureMilliseconds = 0.0;
            _longestSlice = 0.0;
            _stage = Stage.Buildings;
        }

        /// <summary>
        /// The next piece of work, 0.8 ms of it at most. Called once a frame while <see cref="Busy"/>.
        /// <paramref name="inCity"/>: a save is loaded and the loading screen is gone (the city is measured only
        /// then: under the loading screen not everything of it need be there).
        /// </summary>
        public void Step(bool inCity)
        {
            switch (_stage)
            {
                case Stage.Buildings:
                    if (inCity)
                    {
                        Outlines();
                    }
                    break;
                case Stage.Water:
                    if (inCity)
                    {
                        Squares();
                    }
                    break;
                case Stage.Drawing:
                    if (_job == null || _job.Done)
                    {
                        Finish();
                    }
                    break;
            }
        }

        private void Outlines()
        {
            CityBuildingController[] buildings = NavBaseGame.Buildings();
            if (buildings == null)
            {
                return;   // no city yet
            }
            long began = Stopwatch.GetTimestamp();
            double took = 0.0;
            while (_cursor < buildings.Length)
            {
                int corners = NavBaseGame.Outline(buildings[_cursor], _cornerX, _cornerZ);
                int outline = corners >= 3 ? NavBaseMath.Hull(_cornerX, _cornerZ, corners, _outX, _outZ, 0) : 0;
                if (outline >= 3)
                {
                    _hullStart.Add(_hullX.Count);
                    _hullOf.Add(_cursor);
                    for (int i = 0; i < outline; i++)
                    {
                        _hullX.Add(_outX[i]);
                        _hullZ.Add(_outZ[i]);
                    }
                }
                _cursor++;
                if (_cursor % LookEvery == 0)
                {
                    took = (Stopwatch.GetTimestamp() - began) * 1000.0 / Stopwatch.Frequency;
                    if (took >= SliceMilliseconds)
                    {
                        break;
                    }
                }
            }
            Count(began);
            if (_cursor < buildings.Length)
            {
                return;
            }
            if (_hullStart.Count == 0)
            {
                Fail("the game's list of buildings is empty or none of them could be measured");
                return;
            }
            _data.Hulls = _hullStart.Count;
            _hullStart.Add(_hullX.Count);
            _data.HullStart = _hullStart.ToArray();
            _data.HullOf = _hullOf.ToArray();
            _data.HullX = _hullX.ToArray();
            _data.HullZ = _hullZ.ToArray();
            using var piece = SessionWatch.Piece("the minimap's buildings");
            NavBaseWater.Prepare(_data);
            // From over every roof down to under the lowest road.
            float[] y = _graph.Drive.Y;
            bool[] usable = _graph.Usable;
            float lowest = float.MaxValue, highest = float.MinValue;
            for (int i = 0; i < _graph.Drive.Count; i++)
            {
                if (usable[i])
                {
                    lowest = Mathf.Min(lowest, y[i]);
                    highest = Mathf.Max(highest, y[i]);
                }
            }
            _top = highest + Sky;
            _depth = highest - lowest + Sky + 60f;
            _cursor = 0;
            _stage = Stage.Water;
        }

        private void Squares()
        {
            long began = Stopwatch.GetTimestamp();
            NavIndex places = _graph.Places;
            float[] y = _graph.Drive.Y;
            byte[] cells = _data.Cells;
            int columns = _data.Columns;
            float cell = _data.Cell;
            int looked = 0;
            while (_cursor < cells.Length)
            {
                if (cells[_cursor] == NavBaseWater.Unasked)
                {
                    float x = OriginX + (_cursor % columns + 0.5f) * cell;
                    float z = OriginZ + (_cursor / columns + 0.5f) * cell;
                    if (places.Nearest(x, z, RoadReach) >= 0)
                    {
                        cells[_cursor] = NavBaseWater.Land;
                    }
                    else
                    {
                        int street = places.Nearest(x, z, StreetReach);
                        cells[_cursor] = NavBaseGame.Ask(x, z, _top, _depth, street >= 0 ? y[street] : float.NaN);
                        _asked++;
                    }
                    looked++;
                    if (looked % LookEvery == 0 && (Stopwatch.GetTimestamp() - began) * 1000.0 / Stopwatch.Frequency >= SliceMilliseconds)
                    {
                        _cursor++;
                        break;
                    }
                }
                _cursor++;
            }
            Count(began);
            if (_cursor >= cells.Length)
            {
                Draw(true);
            }
        }

        private void Count(long began)
        {
            double took = (Stopwatch.GetTimestamp() - began) * 1000.0 / Stopwatch.Frequency;
            _frames++;
            _measureMilliseconds += took;
            _longestSlice = Math.Max(_longestSlice, took);
        }

        private void Draw(bool settle)
        {
            _job = new Job
            {
                Data = _data,
                Settle = settle,
                SaveTo = _file,
                Width = Width,
                Height = Height,
                OriginX = OriginX,
                OriginZ = OriginZ,
                PerMetre = PerMetre,
                Building = _building,
                Water = _water
            };
            _stage = Stage.Drawing;
            ThreadPool.QueueUserWorkItem(_job.Run);
        }

        private void Finish()
        {
            Job job = _job;
            _job = null;
            if (job == null || job.Fault != null || job.Rgba == null)
            {
                Fail("its picture could not be drawn. " + (job != null ? job.Fault : string.Empty));
                return;
            }
            using var piece = SessionWatch.Piece("the minimap's base picture");
            Stopwatch watch = Stopwatch.StartNew();
            var picture = new Texture2D(job.Width, job.Height, TextureFormat.RGBA32, false, false)
            {
                name = "RealLife Minimap Base",
                wrapMode = TextureWrapMode.Clamp,   // past the picture's edge its outermost pixels go on: open water stays water, and no building is drawn there
                filterMode = FilterMode.Bilinear,
                anisoLevel = 0
            };
            picture.SetPixelData(job.Rgba, 0);
            picture.Apply(false, true);   // sent to the graphics card; the copy in main memory is let go
            Picture = picture;
            _stage = Stage.Done;
            Logger?.Info("Navigation: the minimap's buildings and water made: " + job.Width + " x " + job.Height + " pixels (" + (1f / job.PerMetre).ToString("F2") + " m a pixel, "
                         + (job.Rgba.Length / 1048576f).ToString("F1") + " MB), " + job.Drawn + " buildings and " + job.WaterCells + " squares of water of " + _data.Cells.Length + ", "
                         + (_fromFile
                             ? "read from the mod's folder"
                             : "measured now (" + _asked + " looks down in " + _frames + " frames, " + _measureMilliseconds.ToString("F0") + " ms in all, longest "
                               + _longestSlice.ToString("F2") + " ms) and " + (job.SaveFault == null ? "kept in the mod's folder" : "not kept (" + job.SaveFault + ")"))
                         + ", drawn in " + job.Milliseconds.ToString("F0") + " ms off the main thread, sent to the graphics card in " + watch.Elapsed.TotalMilliseconds.ToString("F1") + " ms.");
        }

        private void Fail(string why)
        {
            _stage = Stage.Failed;
            _job = null;
            Logger?.Warn("Navigation: the minimap shows roads only, as before: " + why);
        }

        /// <summary>Lets the picture go and forgets everything, so that <see cref="Begin"/> starts afresh. A thread still drawing finishes into an array nobody reads.</summary>
        public void Drop()
        {
            if (Picture != null)
            {
                UnityEngine.Object.Destroy(Picture);
                Picture = null;
            }
            _job = null;
            _data = null;
            _graph = null;
            _stage = Stage.Idle;
        }
    }
}
