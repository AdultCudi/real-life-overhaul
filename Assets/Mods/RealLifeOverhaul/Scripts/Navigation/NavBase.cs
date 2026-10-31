using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;
using BAModAPI;
using UnityEngine;

namespace RealLifeOverhaul.Nav
{
    /// <summary>
    /// The minimap's base picture, the buildings and water under the roads, in the game's palette. The city is read
    /// from the mod's folder or measured once per version of the game, at most 0.8 ms a frame; the picture, at most
    /// 2048 pixels a side (9 MB on the graphics card, none in main memory), is drawn on a pool thread and kept for the
    /// session by a print of what it was drawn from. Nothing per frame once it is made.
    /// </summary>
    public sealed class NavBase
    {
        private const double SliceMilliseconds = 0.8;   // of one frame, while a city is measured
        private const int LookEvery = 8;                // the clock is looked at after this many buildings or squares
        private const float RoadReach = 8f;             // metres: a square this near a lane point is road, and is not asked
        private const float StreetReach = 150f;         // metres within which the nearest road gives the street's height
        private const float Sky = 400f;                 // metres above the highest road a square is looked down from

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
        private readonly List<long> _hullKey = new List<long>(2048);   // each outline's building by its address (NavBaseMath.BuildingKey)
        private readonly List<float> _hullX = new List<float>(8192);
        private readonly List<float> _hullZ = new List<float>(8192);
        private readonly float[] _cornerX = new float[8], _cornerZ = new float[8];
        private readonly float[] _outX = new float[17], _outZ = new float[17];
        private readonly List<MeshRenderer> _more = new List<MeshRenderer>(16);   // the mod's own buildings' boxes, asked for at each drawing
        private readonly List<float> _moreX = new List<float>(128), _moreZ = new List<float>(128);
        private readonly List<int> _moreStart = new List<int>(16);
        private int _moreStamp;     // NavBridges.MoreBuildingsStamp when the picture was drawn
        private int _moreDrawn;     // outlines of the mod's own on the picture

        // The session's picture and the print of what it was drawn from. It survives the city: a texture made by the mod is
        // no scene's, and it is marked to stay through the engine's sweep of unused assets.
        private static Texture2D _kept;
        private static ulong _keptPrint;

        /// <summary>The picture, once it is on the graphics card; null before, and if it could not be made.</summary>
        public Texture2D Picture { get; private set; }
        public int Width { get; private set; }
        public int Height { get; private set; }
        public float PerMetre { get; private set; }
        public float OriginX { get; private set; }
        public float OriginZ { get; private set; }

        /// <summary>The metres from the origin that the picture's pixels cover.</summary>
        public float WorldWidth { get; private set; }
        public float WorldHeight { get; private set; }

        /// <summary>What the picture was drawn from, kept for the checks and the tests.</summary>
        public NavBaseData Data => _stage == Stage.Done ? _data : null;

        /// <summary>True while there is work to do in <see cref="Step"/>.</summary>
        public bool Busy => _stage == Stage.Buildings || _stage == Stage.Water || _stage == Stage.Drawing;

        public bool Idle => _stage == Stage.Idle;
        public bool Drawing => _stage == Stage.Drawing;
        public bool Failed => _stage == Stage.Failed;

        /// <summary>True if the last picture came from the mod's folder and not from measuring the city.</summary>
        public bool FromFile => _fromFile;

        // What the thread works on and says when done; nothing of the engine is touched off the main thread.
        private sealed class Job
        {
            public NavBaseData Data;
            public NavBaseData Drawn;   // Data with the mod's own buildings on top, made on the main thread; Data itself with none
            public ulong Print;     // of what was drawn, made on the thread once the water is settled
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
            public int Outlined, WaterCells;

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
                    NavBaseData drawn = Drawn ?? Data;
                    Print = PrintOf(drawn, Width, Height, OriginX, OriginZ, PerMetre, Building, Water);
                    var cover = new byte[Width * Height];
                    Outlined = NavBaseRaster.Buildings(cover, Width, Height, OriginX, OriginZ, PerMetre, drawn);
                    Rgba = new byte[Width * Height * 4];
                    NavBaseRaster.Compose(Rgba, cover, Width, Height, OriginX, OriginZ, PerMetre, drawn,
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

        /// <summary>Begins from the mod's folder unless <paramref name="measureAgain"/>, else by measuring; waits for the game's palette.</summary>
        public void Begin(NavGraph graph, bool measureAgain)
        {
            if (_stage != Stage.Idle || graph == null || !graph.Sized)
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
                    if (_kept != null && _keptPrint == PrintOf(WithMore(kept), Width, Height, OriginX, OriginZ, PerMetre, _building, _water))
                    {
                        Picture = _kept;   // the same city drawn the same way as at an earlier load
                        _stage = Stage.Done;
                        Logger?.Info("Navigation: the minimap's buildings and water are the picture made at an earlier load (the same city, size and colours): "
                                     + Width + " x " + Height + " pixels, nothing drawn and nothing sent to the graphics card.");
                        return;
                    }
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
            _hullKey.Clear();
            _hullX.Clear();
            _hullZ.Clear();
            _cursor = 0;
            _asked = 0;
            _frames = 0;
            _measureMilliseconds = 0.0;
            _longestSlice = 0.0;
            _stage = Stage.Buildings;
        }

        /// <summary>At most 0.8 ms of work a frame while <see cref="Busy"/>; measures only <paramref name="inCity"/>, the loading screen gone.</summary>
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
                int corners = NotMeasured(buildings[_cursor]) ? 0 : NavBaseGame.Outline(buildings[_cursor], _cornerX, _cornerZ);
                int outline = corners >= 3 ? NavBaseMath.Hull(_cornerX, _cornerZ, corners, _outX, _outZ, 0) : 0;
                if (outline >= 3)
                {
                    _hullStart.Add(_hullX.Count);
                    _hullKey.Add(NavBaseGame.KeyOf(buildings[_cursor]));
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
            _data.HullKey = _hullKey.ToArray();
            _data.HullX = _hullX.ToArray();
            _data.HullZ = _hullZ.ToArray();
            using var piece = SessionWatch.Piece("the minimap's buildings");
            NavBaseWater.Prepare(_data);
            // from over every roof to under the lowest road
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

        /// <summary>Behind the loading screen, waits for the drawing thread; false if not done in time, the timer then sends it.</summary>
        public bool WaitForDrawing(int milliseconds)
        {
            Job job = _job;
            if (_stage != Stage.Drawing || job == null)
            {
                return true;
            }
            Stopwatch watch = Stopwatch.StartNew();
            while (!job.Done && watch.ElapsedMilliseconds < milliseconds)
            {
                Thread.Sleep(1);
            }
            return job.Done;
        }

        /// <summary>The print of everything a picture is drawn from: the same print, the same picture.</summary>
        internal static ulong PrintOf(NavBaseData data, int width, int height, float originX, float originZ, float perMetre, Color32 building, Color32 water)
        {
            var print = new InputPrint();
            print.Add(width);
            print.Add(height);
            print.Add(originX);
            print.Add(originZ);
            print.Add(perMetre);
            print.Add(building.r | building.g << 8 | building.b << 16 | building.a << 24);
            print.Add(water.r | water.g << 8 | water.b << 16 | water.a << 24);
            data.AddTo(ref print);
            return print.Value;
        }

        private void Count(long began)
        {
            double took = (Stopwatch.GetTimestamp() - began) * 1000.0 / Stopwatch.Frequency;
            _frames++;
            _measureMilliseconds += took;
            _longestSlice = Math.Max(_longestSlice, took);
        }

        // A building the mod made is left out of the measured city: the kept file is the game's city alone.
        private static bool NotMeasured(CityBuildingController building)
        {
            Func<CityBuildingController, bool> made = NavBridges.NotMeasured;
            return made != null && made(building);
        }

        /// <summary>True when the buildings the mod stands in the city have changed since the picture was drawn.</summary>
        public bool Stale => _stage == Stage.Done && _data != null && MoreStamp() != _moreStamp;

        /// <summary>The picture drawn again from the same city, with the mod's own buildings as they are now; nothing measured again.</summary>
        public void Redraw()
        {
            if (_stage == Stage.Done && _data != null)
            {
                Draw(false);
            }
        }

        private static int MoreStamp()
        {
            Func<int> stamp = NavBridges.MoreBuildingsStamp;
            return stamp != null ? stamp() : 0;
        }

        // The city with the outlines of the mod's own buildings on top, as they stand now (their boxes, read on the main thread).
        private NavBaseData WithMore(NavBaseData city)
        {
            _more.Clear();
            _moreX.Clear();
            _moreZ.Clear();
            _moreStart.Clear();
            NavBridges.MoreBuildings?.Invoke(_more);
            for (int i = 0; i < _more.Count; i++)
            {
                int corners = NavBaseGame.Outline(_more[i], _cornerX, _cornerZ);
                int outline = corners >= 3 ? NavBaseMath.Hull(_cornerX, _cornerZ, corners, _outX, _outZ, 0) : 0;
                if (outline < 3)
                {
                    continue;
                }
                _moreStart.Add(_moreX.Count);
                for (int k = 0; k < outline; k++)
                {
                    _moreX.Add(_outX[k]);
                    _moreZ.Add(_outZ[k]);
                }
            }
            _more.Clear();
            return city.With(_moreX, _moreZ, _moreStart);
        }

        private void Draw(bool settle)
        {
            _moreStamp = MoreStamp();
            NavBaseData drawn = WithMore(_data);
            _moreDrawn = _moreStart.Count;
            _job = new Job
            {
                Data = _data,
                Drawn = drawn,
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
                wrapMode = TextureWrapMode.Clamp,   // past the edge its outermost pixels go on: water stays water, no building is drawn out
                filterMode = FilterMode.Bilinear,
                anisoLevel = 0
            };
            picture.SetPixelData(job.Rgba, 0);
            picture.Apply(false, true);   // sent to the graphics card; the copy in main memory is let go
            picture.hideFlags = HideFlags.DontUnloadUnusedAsset;
            if (_kept != null && _kept != picture)
            {
                UnityEngine.Object.Destroy(_kept);   // another city's, or drawn from other figures
            }
            _kept = picture;
            _keptPrint = job.Print;
            Picture = picture;
            _stage = Stage.Done;
            Logger?.Info("Navigation: the minimap's buildings and water made: " + job.Width + " x " + job.Height + " pixels (" + (1f / job.PerMetre).ToString("F2", CultureInfo.InvariantCulture)
                         + " m a pixel, "
                         + (job.Rgba.Length / 1048576f).ToString("F1", CultureInfo.InvariantCulture) + " MB), " + job.Outlined + " buildings (" + _moreDrawn + " of them the mod's own) and "
                         + job.WaterCells + " squares of water of "
                         + _data.Cells.Length + ", "
                         + (_fromFile
                             ? "read from the mod's folder"
                             : "measured now (" + _asked + " looks down in " + _frames + " frames, " + _measureMilliseconds.ToString("F0") + " ms in all, longest "
                               + _longestSlice.ToString("F2", CultureInfo.InvariantCulture) + " ms) and " + (job.SaveFault == null ? "kept in the mod's folder" : "not kept (" + job.SaveFault + ")"))
                         + ", drawn in " + job.Milliseconds.ToString("F0") + " ms off the main thread, sent to the graphics card in "
                         + watch.Elapsed.TotalMilliseconds.ToString("F1", CultureInfo.InvariantCulture) + " ms.");
        }

        private void Fail(string why)
        {
            _stage = Stage.Failed;
            _job = null;
            Logger?.Warn("Navigation: the minimap shows roads only, as before: " + why);
        }

        /// <summary>Forgets everything for a fresh <see cref="Begin"/>; the picture is kept for the next load unless <paramref name="letGo"/>.</summary>
        public void Drop(bool letGo)
        {
            if (letGo && _kept != null)
            {
                UnityEngine.Object.Destroy(_kept);
                _kept = null;
                _keptPrint = 0;
            }
            Picture = null;
            _job = null;
            _data = null;
            _graph = null;
            _stage = Stage.Idle;
        }
    }
}
