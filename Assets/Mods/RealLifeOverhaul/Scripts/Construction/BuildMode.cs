using System;
using System.Collections.Generic;
using System.Globalization;
using BAModAPI;
using RealLifeOverhaul.GameUi;
using UnityEngine;

namespace RealLifeOverhaul.Construction
{
    /// <summary>
    /// Build mode (plans/build-mode.md; the owner, 26 Oct 2026: "can we make buildings like zoning in Cities Skylines? You go into
    /// build mode after you want to buy some land, then building on it"): the game's city map opened over Uptown from the
    /// Construction app, the time paused and its controls locked, the district's zoning cells drawn as one mesh coloured by what
    /// each is, a window of the game's own (a copy of its subway station window, MapPicker) with the tools, what is picked and the
    /// actions, and a tinted ghost of a design on its footprint. Every action is the construction game's own call. Esc, Cancel or
    /// anything else that shuts the map leaves it and gives back the camera and the time; the player never moves.
    /// COST: nothing at all while closed (not enabled); while open, the mouse's buttons each frame and the cell under it twenty
    /// times a second while dragging; the layer coloured again only when something changes.
    /// </summary>
    public sealed partial class BuildMode : MonoBehaviour
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("construction", "BuildMode", typeof(BuildMode));   // its Update
        private static readonly int HotUpdate = HotSpots.Slot("Construction: BuildMode.Update");

        public static IModLogger Logger;

        private const float DragEvery = 0.05f;   // seconds (real time) between looks at the cell under the pointer while dragging
        private const float ClickPixels = 8f;    // a press and release closer than this is a click on one cell
        private const int MarksShown = 12;       // sites and developments listed and marked at once
        private const int DesignsListed = 8;

        private ConstructionDirector _d;
        private MapPicker _window;
        private BuildModeGame.Layer _layer;
        private int[] _cellStart = new int[0];   // each column's first cell in the layer
        private byte[] _colours = new byte[0];
        private BuildModeRules.ColumnFacts[] _facts = new BuildModeRules.ColumnFacts[0];
        private BuildModeState _state;
        private readonly List<CellAt> _picked = new List<CellAt>(64);
        private readonly List<DesignChoice> _designs = new List<DesignChoice>(32);
        private readonly List<BuildMark> _marks = new List<BuildMark>(16);
        private CellSite _site;
        private string _siteWhy;
        private CellAt _from, _to;
        private Vector2 _downAt;
        private bool _down;
        private float _nextDrag;
        private string _targetSite;          // the Zone and Join tools: the player's site picked
        private LotView _target;             // the Bulldoze and Refurbish tools: the building picked
        private bool _hasTarget;
        private int _design = -1;
        private BuildUse _use = BuildUse.Shops;
        private bool _parking;
        private int _warnings;
        private long _estimate;
        private int _limit;
        private Transform _ghost;
        private int _lastProject;
        private Vector3 _playerAt;

        /// <summary>True while build mode is open.</summary>
        public bool IsOpen => _state.Open;

        /// <summary>Where build mode stands, for the tests.</summary>
        public BuildModeState State => _state;

        /// <summary>The layer is drawn now, for the tests.</summary>
        public bool LayerShown => _layer != null && _layer.Object != null && _layer.Object.activeSelf;

        /// <summary>The site the cells picked make now (Count 0 for none), for the tests.</summary>
        public CellSite Site => _siteWhy == null ? _site : default(CellSite);

        /// <summary>The project the last Build here made, for the tests.</summary>
        public int LastProject => _lastProject;

        private void Awake()
        {
            enabled = false;   // runs only while open
        }

        private void Update()
        {
            using var hotSpot = HotSpots.Time(HotUpdate);
            if (FrameGuard.Off)
            {
                enabled = false;   // its feature was switched off after its faults
                return;
            }
            try
            {
                OnUpdate();
            }
            catch (Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
                Leave("a fault");
            }
        }

        private void OnDestroy()
        {
            Leave("the city went");
            BuildModeGame.Destroy(_layer);
            _layer = null;
            if (_window != null)
            {
                _window.Destroy();
                _window = null;
            }
        }

        // ------------------------------------------------------------------ opening and leaving

        /// <summary>Opens build mode over the district: false if the map or its window could not open.</summary>
        public bool Open(ConstructionDirector d)
        {
            if (_state.Open)
            {
                return true;
            }
            if (MapPicker.AnyOpen)
            {
                return false;   // another choice of places is up on the map
            }
            _d = d;
            CellGrid grid = d.Cells;
            if (_window == null || !_window.Alive)
            {
                _window = MapPicker.Make("RealLife Build Mode");
                if (_window == null)
                {
                    return false;
                }
            }
            if (_layer == null || _layer.Object == null || _layer.Cells != grid.CellCount)
            {
                BuildModeGame.Destroy(_layer);
                _layer = BuildModeGame.MakeLayer(grid, out string why);
                if (_layer == null)
                {
                    Logger?.Warn("Construction: build mode has no land layer: " + why + "; its window and tools still work.");
                }
                _cellStart = new int[grid.Columns.Count];
                int at = 0;
                for (int c = 0; c < grid.Columns.Count; c++)
                {
                    _cellStart[c] = at;
                    at += grid.Columns[c].Rows;
                }
                _colours = new byte[at];
                _facts = new BuildModeRules.ColumnFacts[grid.Columns.Count];
            }
            _state = BuildModeRules.Next(_state, GoingPublic.InSession ? BuildInput.OpenInSession : BuildInput.Open, BuildTool.Land, false);
            ClearPick();
            _playerAt = ConstructionGame.Player;
            BuildModeGame.CloseMenu();
            Write();
            if (!_window.Open(Centre(grid), Picked, Closed))   // the window runs each pick and its close in its own guard (UiClone.Safe)
            {
                _state = BuildModeRules.Next(_state, BuildInput.MapClosed, BuildTool.Land, false);
                return false;
            }
            BuildModeGame.LockTime(true);
            BuildModeGame.Show(_layer, true);
            ReadFacts();
            Colour();
            enabled = true;
            Logger?.Info("Construction: build mode opened over Uptown (" + grid.Columns.Count + " spaces, " + grid.CellCount + " cells)" + (_state.ViewOnly ? ", view only in this session" : string.Empty)
                         + "; the time paused " + (BuildModeGame.Paused ? "YES" : "not yet") + ".");
            return true;
        }

        /// <summary>Leaves build mode: the map shut if it is still open, the camera and the time given back, the ghost taken away.</summary>
        public void Close()
        {
            if (_state.Open && _window != null)
            {
                _window.Close();   // calls Closed
            }
            Leave("closed");
        }

        private void Closed()
        {
            Leave("the map shut");
        }

        private void Leave(string why)
        {
            if (!_state.Open)
            {
                return;
            }
            _state = BuildModeRules.Next(_state, BuildInput.MapClosed, BuildTool.Land, false);
            _down = false;
            BuildModeGame.HoldCamera(false);
            BuildModeGame.LockTime(false);
            BuildModeGame.Show(_layer, false);
            DropGhost();
            ClearPick();
            enabled = false;
            Vector3 now = ConstructionGame.Player;
            float moved = Vector3.Distance(now, _playerAt);
            Logger?.Info("Construction: build mode left (" + why + "); the map's camera free " + (BuildModeGame.CameraFree ? "YES" : "NO") + ", the player " + moved.ToString("0.00", CultureInfo.InvariantCulture)
                         + " m from where they were.");
        }

        // Where the map opens: over the player's nearest cell of their own, else the nearest cell for sale, else the district's first.
        private Vector3 Centre(CellGrid grid)
        {
            Vector3 player = ConstructionGame.Player;
            int best = -1;
            float near = float.MaxValue;
            for (int c = 0; c < grid.Columns.Count; c++)
            {
                BuildModeRules.ColumnFacts f = _d.FactsOf(c);
                if (!f.Mine && !f.Free)
                {
                    continue;
                }
                CellColumn col = grid.Columns[c];
                Middle(grid, c, 0, out float x, out float z);
                float d = (x - player.x) * (x - player.x) + (z - player.z) * (z - player.z) - (f.Mine ? 1e7f : 0f);
                if (d < near)
                {
                    near = d;
                    best = c;
                }
            }
            if (best < 0)
            {
                best = 0;
            }
            Middle(grid, best, 0, out float bx, out float bz);
            return new Vector3(bx, 0f, bz);
        }

        private static void Middle(CellGrid grid, int column, int row, out float x, out float z)
        {
            grid.Rect(column, row, 0f, out float x1, out float z1, out float x2, out float z2);
            x = (x1 + x2) * 0.5f;
            z = (z1 + z2) * 0.5f;
        }

        // ------------------------------------------------------------------ the pointer

        private void OnUpdate()
        {
            if (!_state.Open)
            {
                enabled = false;
                return;
            }
            if (!BuildModeGame.MapOpen)
            {
                Leave("the map shut");
                return;
            }
            if (_state.Step == BuildStep.Confirming)
            {
                return;
            }
            if (!_down && BuildModeGame.Pressed && !BuildModeGame.OverInterface && BuildModeGame.Pointed(out Vector3 at))
            {
                CellAt cell = _d.Cells.At(at.x, at.z);
                _state = BuildModeRules.Next(_state, BuildInput.Press, _state.Tool, !cell.None);
                if (!cell.None)
                {
                    BuildModeGame.HoldCamera(true);   // the map's left drag would pan it
                    _down = true;
                    _downAt = BuildModeGame.Mouse;
                    _from = _to = cell;
                    PickRectangle();
                    Colour();
                }
                return;
            }
            if (!_down)
            {
                return;
            }
            if (BuildModeGame.Held && Time.unscaledTime >= _nextDrag)
            {
                _nextDrag = Time.unscaledTime + DragEvery;
                if (BuildModeGame.Pointed(out Vector3 point))
                {
                    CellAt cell = _d.Cells.At(point.x, point.z);
                    if (!cell.None && (cell.Column != _to.Column || cell.Row != _to.Row) && _d.Cells.Columns[cell.Column].Strip == _d.Cells.Columns[_from.Column].Strip)
                    {
                        _to = cell;
                        PickRectangle();
                        Colour();
                    }
                }
            }
            if (BuildModeGame.Released || !BuildModeGame.Held)
            {
                _down = false;
                BuildModeGame.HoldCamera(false);
                if ((BuildModeGame.Mouse - _downAt).sqrMagnitude <= ClickPixels * ClickPixels)
                {
                    _to = _from;   // a click: the one cell (and those between it and the street)
                    PickRectangle();
                }
                _state = BuildModeRules.Next(_state, BuildInput.Release, _state.Tool, false);
                Pick();
                Write();
                Colour();
            }
        }

        // The cells between the press and the pointer along one strip, each column from the street back to the deeper row.
        private void PickRectangle()
        {
            _picked.Clear();
            CellGrid grid = _d.Cells;
            CellColumn a = grid.Columns[_from.Column], b = grid.Columns[_to.Column];
            int low = Math.Min(a.Space, b.Space), high = Math.Max(a.Space, b.Space), row = Math.Max(_from.Row, _to.Row);
            for (int space = low; space <= high; space++)
            {
                int column = grid.ColumnOf(a.Strip, space);
                if (column >= 0)
                {
                    _picked.Add(new CellAt(column, Math.Min(row, grid.Columns[column].Rows - 1)));
                }
            }
        }

        // What the cells picked are to the tool: a site to buy, the player's site, a footprint, a building.
        private void Pick()
        {
            _siteWhy = CellRules.Site(_d.Cells, _picked, BuildCatalogue.SiteSpacesMost, out _site);
            _targetSite = null;
            _hasTarget = false;
            DropGhost();
            _design = -1;
            if (_picked.Count == 0)
            {
                return;
            }
            int column = _picked[0].Column;
            switch (_state.Tool)
            {
                case BuildTool.Zone:
                case BuildTool.Join:
                    _d.RowsHeld(column, out LotFile own);
                    _targetSite = own != null ? own.Id : null;
                    break;
                case BuildTool.Bulldoze:
                case BuildTool.Refurbish:
                    _hasTarget = _d.LotOfColumn(column, out _target) && _d.FactsOf(column).MyBuilding;
                    break;
                case BuildTool.Place:
                    _d.RowsHeld(column, out LotFile site);
                    _use = site != null && site.Paint > 0 ? (BuildUse)(site.Paint - 1) : FirstZoned(site);
                    if (_siteWhy == null)
                    {
                        _d.FootprintDesigns(_site.FirstColumn, _site.Count, _site.MinRows, _use, _designs);
                        if (_designs.Count > DesignsListed)
                        {
                            _designs.RemoveRange(DesignsListed, _designs.Count - DesignsListed);
                        }
                    }
                    break;
            }
        }

        private BuildUse FirstZoned(LotFile site)
        {
            int zones = site != null ? _d.ZonesNow(site) : 0;
            for (int u = 0; u < (int)BuildUse.Count; u++)
            {
                if (LandRules.Zoned(zones, (BuildUse)u))
                {
                    return (BuildUse)u;
                }
            }
            return BuildUse.Shops;
        }

        private void ClearPick()
        {
            _picked.Clear();
            _siteWhy = CellRules.NoCells;
            _site = default(CellSite);
            _targetSite = null;
            _hasTarget = false;
            _design = -1;
            _designs.Clear();
            DropGhost();
        }

        // ------------------------------------------------------------------ the layer and the ghost

        private void ReadFacts()
        {
            for (int c = 0; c < _facts.Length; c++)
            {
                _facts[c] = _d.FactsOf(c);
            }
        }

        private void Colour()
        {
            if (_layer == null)
            {
                return;
            }
            CellGrid grid = _d.Cells;
            for (int c = 0; c < _facts.Length && c < grid.Columns.Count; c++)
            {
                for (int r = 0; r < grid.Columns[c].Rows; r++)
                {
                    _colours[_cellStart[c] + r] = (byte)BuildModeRules.Status(_facts[c], r, _state.Tool);
                }
            }
            byte mark = (byte)(_state.Step == BuildStep.Ghost ? BuildModeRules.Footprint : _siteWhy == null || _state.Tool != BuildTool.Land ? BuildModeRules.Picked : BuildModeRules.PickedBad);
            for (int i = 0; i < _picked.Count; i++)
            {
                int c = _picked[i].Column;
                for (int r = 0; r <= _picked[i].Row && c < _cellStart.Length; r++)
                {
                    _colours[_cellStart[c] + r] = mark;
                }
            }
            BuildModeGame.Colour(_layer, _colours);
        }

        private void StandGhost()
        {
            DropGhost();
            if (_design < 0 || _design >= _designs.Count || _siteWhy != null || BuildModeBridges.GhostStand == null)
            {
                return;
            }
            CellRules.FootprintLot(_d.Cells, _site.FirstColumn, _site.Count, out float x, out float z, out float facing, out float width, out float depth);
            try
            {
                _ghost = BuildModeBridges.GhostStand(_designs[_design].Name, _designs[_design].Size, x, z, facing, width, depth);
                BuildModeGame.Tint(_ghost, _layer, _warnings == 0);
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                _ghost = null;
            }
        }

        private void DropGhost()
        {
            if (_ghost != null)
            {
                Action<Transform> gone = BuildModeBridges.GhostGone;
                try
                {
                    gone?.Invoke(_ghost);
                }
                catch (Exception e)
                {
                    Logger?.Error(e);
                }
            }
            _ghost = null;
        }

        /// <summary>The ghost standing now, for the tests.</summary>
        public bool GhostStands => _ghost != null;
    }
}
