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
    /// each is in the view chosen (the land's status, the zoning, land value, occupancy, what is under way), a big window in the
    /// middle in the game's own style (BuildModePanel) with the tools, the views, what is picked with its figures and the actions,
    /// which steps aside while the player picks on the map, and a tinted ghost of a design on its footprint. Every action is the
    /// construction game's own call. A picked square pressed again, or right-clicked, lets it and those behind it go; a drag with
    /// shift lets a run go; Esc clears what is picked first and leaves only when nothing is. Esc with nothing picked, the window's
    /// close button, or anything else that shuts the map leaves it and gives back the camera and the time; the player never moves.
    /// COST: nothing at all while closed (not enabled); while open, the mouse's buttons each frame, the cell under the pointer ten
    /// times a second (twenty while dragging); the layer coloured again and the window written only when something changes.
    /// It runs before the game's own key handling, so the Esc it takes to clear a selection never also shuts the map.
    /// </summary>
    [DefaultExecutionOrder(-31000)]
    public sealed partial class BuildMode : MonoBehaviour
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("construction", "BuildMode", typeof(BuildMode));   // its Update
        private static readonly int HotUpdate = HotSpots.Slot("Construction: BuildMode.Update");

        public static IModLogger Logger;

        private const float DragEvery = 0.05f;   // seconds (real time) between looks at the cell under the pointer while dragging
        private const float HoverEvery = 0.1f;   // seconds (real time) between looks at the cell under the pointer otherwise
        private const float ClickPixels = 8f;    // a press and release closer than this is a click on one cell
        private const int MarksShown = 12;       // sites and developments listed and marked at once
        private const int DesignsListed = 8;

        private ConstructionDirector _d;
        private MapPicker _map;                  // the map opened and its markers, its own window kept hidden
        private BuildModePanel _panel;
        private BuildModeGame.Layer _layer;
        private int[] _cellStart = new int[0];   // each column's first cell in the layer
        private byte[] _fills = new byte[0];
        private byte[] _lines = new byte[0];
        private int[] _depth = new int[0];       // each column's deepest row picked, -1 for none
        private int[] _before = new int[0];      // ... as it was when the press began
        private int[] _values = new int[0];      // each column's land value step, worked when the view asks
        private bool _valuesRead;
        private BuildModeRules.ColumnFacts[] _facts = new BuildModeRules.ColumnFacts[0];
        private BuildModeState _state;
        private BuildView _view = BuildView.Zoning;
        private readonly List<CellAt> _picked = new List<CellAt>(64);
        private readonly List<DesignChoice> _designs = new List<DesignChoice>(32);
        private readonly List<BuildMark> _marks = new List<BuildMark>(16);
        private CellSite _site;
        private string _siteWhy;
        private CellAt _from, _to;
        private Vector2 _downAt, _rightAt;
        private bool _down, _rightDown, _removing, _letGo;
        private float _nextDrag, _nextHover;
        private int _hoverColumn = -1, _hoverRow = -1;
        private string _hoverWords;
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

        /// <summary>The view the layer shows now.</summary>
        public BuildView View => _view;

        /// <summary>The layer is drawn now, for the tests.</summary>
        public bool LayerShown => _layer != null && _layer.Object != null && _layer.Object.activeSelf;

        /// <summary>The site the cells picked make now (Count 0 for none), for the tests.</summary>
        public CellSite Site => _siteWhy == null ? _site : default(CellSite);

        /// <summary>The project the last Build here made, for the tests.</summary>
        public int LastProject => _lastProject;

        /// <summary>Cells picked now, for the tests.</summary>
        public int PickedCells
        {
            get
            {
                int n = 0;
                for (int i = 0; i < _picked.Count; i++)
                {
                    n += _picked[i].Row + 1;
                }
                return n;
            }
        }

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
            if (_map != null)
            {
                _map.Destroy();
                _map = null;
            }
            if (_panel != null)
            {
                _panel.Destroy();
                _panel = null;
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
            if (_map == null || !_map.Alive)
            {
                _map = MapPicker.Make("RealLife Build Mode");
                if (_map == null)
                {
                    return false;
                }
                _map.Windowless = true;   // the map and the markers only: the window is the panel
            }
            if (_panel == null || !_panel.Alive)
            {
                BuildModePanel.Logger = Logger;
                _panel = BuildModePanel.Make(BuildModeGame.WindowCanvas(), ToolPicked, ViewPicked, Picked, FoldPressed, Close);
                if (_panel == null)
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
                _fills = new byte[at];
                _lines = new byte[at];
                _facts = new BuildModeRules.ColumnFacts[grid.Columns.Count];
                _depth = new int[grid.Columns.Count];
                _before = new int[grid.Columns.Count];
                _values = new int[grid.Columns.Count];
            }
            _state = BuildModeRules.Next(_state, GoingPublic.InSession ? BuildInput.OpenInSession : BuildInput.Open, BuildTool.Land, false);
            _view = BuildModeRules.ViewFor(_state.Tool);
            ClearPick();
            _playerAt = ConstructionGame.Player;
            BuildModeGame.CloseMenu();
            _valuesRead = false;
            ReadFacts();
            WriteMarks();
            if (!_map.Open(Centre(grid), MarkPicked, Closed))   // the picker runs each pick and its close in its own guard (UiClone.Safe)
            {
                _state = BuildModeRules.Next(_state, BuildInput.MapClosed, BuildTool.Land, false);
                return false;
            }
            BuildModeGame.LockTime(true);
            BuildModeGame.Show(_layer, true);
            _panel.Fold(false);
            _panel.Show(true);
            Write(true);
            Colour();
            enabled = true;
            Logger?.Info("Construction: build mode opened over Uptown (" + grid.Columns.Count + " spaces, " + grid.CellCount + " cells)" + (_state.ViewOnly
                ? ", view only in this session" : string.Empty)
                         + "; the time paused " + (BuildModeGame.Paused ? "YES" : "not yet") + "; the view " + _view + ".");
            return true;
        }

        /// <summary>Leaves build mode: the map shut if it is still open, the camera and the time given back, the ghost taken away.</summary>
        public void Close()
        {
            if (_state.Open && _map != null)
            {
                _map.Close();   // calls Closed
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
            if (_map != null && _map.IsOpen)
            {
                _map.Close();   // a fault or the option: the map shut too, and the picker's markers put away (its Closed comes back here and finds it left)
            }
            _down = _rightDown = false;
            BuildModeGame.HoldCamera(false);
            BuildModeGame.LockTime(false);
            BuildModeGame.Show(_layer, false);
            _panel?.Show(false);
            DropGhost();
            ClearPick();
            enabled = false;
            Vector3 now = ConstructionGame.Player;
            float moved = Vector3.Distance(now, _playerAt);
            Logger?.Info("Construction: build mode left (" + why + "); the map's camera free " + (BuildModeGame.CameraFree ? "YES" : "NO") + ", the player "
                + moved.ToString("0.00", CultureInfo.InvariantCulture)
                         + " m from where they were.");
        }

        // Where the map opens: over the player's nearest cell of their own, else the nearest cell for sale, else the district's first.
        private Vector3 Centre(CellGrid grid)
        {
            Vector3 player = ConstructionGame.Player;
            int best = -1;
            float near = float.MaxValue;
            for (int c = 0; c < grid.Columns.Count && c < _facts.Length; c++)
            {
                BuildModeRules.ColumnFacts f = _facts[c];
                if (!f.Mine && !f.Free)
                {
                    continue;
                }
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
            if (BuildModeGame.CancelPressed && (HasPicks || _state.Step == BuildStep.Ghost))
            {
                BuildModeGame.TakeCancel();   // Esc clears what is picked; the game's own Esc (shutting the map) waits for nothing picked
                Clear();
                return;
            }
            float now = Time.unscaledTime;
            if (!_down && now >= _nextHover)
            {
                _nextHover = now + HoverEvery;
                Hover();
            }
            if (BuildModeGame.RightPressed && !BuildModeGame.OverInterface)
            {
                _rightDown = true;
                _rightAt = BuildModeGame.Mouse;
            }
            if (_rightDown && BuildModeGame.RightReleased)
            {
                _rightDown = false;   // a right click lets the square under it go; a right drag is the map's turn
                if ((BuildModeGame.Mouse - _rightAt).sqrMagnitude <= ClickPixels * ClickPixels && BuildModeGame.Pointed(out Vector3 at) && LetGo(_d.Cells.At(at.x, at.z)))
                {
                    Changed(true);
                }
            }
            if (!_down && BuildModeGame.Pressed && !BuildModeGame.OverInterface && BuildModeGame.Pointed(out Vector3 point))
            {
                CellAt cell = _d.Cells.At(point.x, point.z);
                _state = BuildModeRules.Next(_state, BuildInput.Press, _state.Tool, !cell.None);
                if (!cell.None)
                {
                    BuildModeGame.HoldCamera(true);   // the map's left drag would pan it
                    if (_panel != null && !_panel.Folded)
                    {
                        _panel.Fold(true);            // the window steps aside while squares are picked
                    }
                    _down = true;
                    _downAt = BuildModeGame.Mouse;
                    _removing = BuildModeGame.Shift;
                    _letGo = !_removing && Picks(cell);   // a picked square pressed: a click lets it go
                    Array.Copy(_depth, _before, _depth.Length);
                    _from = _to = cell;
                    Drag();
                    Changed(false);
                }
                return;
            }
            if (!_down)
            {
                return;
            }
            if (BuildModeGame.Held && now >= _nextDrag)
            {
                _nextDrag = now + DragEvery;
                if (BuildModeGame.Pointed(out Vector3 at))
                {
                    CellAt cell = _d.Cells.At(at.x, at.z);
                    if (!cell.None && (cell.Column != _to.Column || cell.Row != _to.Row) && _d.Cells.Columns[cell.Column].Strip == _d.Cells.Columns[_from.Column].Strip)
                    {
                        _to = cell;
                        _letGo = false;   // a drag adds (or with shift lets a run go)
                        Drag();
                        Changed(false);
                    }
                }
            }
            if (BuildModeGame.Released || !BuildModeGame.Held)
            {
                _down = false;
                BuildModeGame.HoldCamera(false);
                if ((BuildModeGame.Mouse - _downAt).sqrMagnitude <= ClickPixels * ClickPixels)
                {
                    _to = _from;   // a click: the one cell (and those between it and the street), or let go if it was picked
                    Drag();
                }
                _state = BuildModeRules.Next(_state, BuildInput.Release, _state.Tool, false);
                Changed(true);
            }
        }

        private bool HasPicks => _picked.Count > 0;

        private bool Picks(CellAt cell) => !cell.None && cell.Column < _depth.Length && _depth[cell.Column] >= cell.Row;

        // The picks from the press: added from the street back to the deeper row along the strip, or let go from the shallower
        // row back (a shift drag, or a click on a picked square); a press on another strip starts afresh.
        private void Drag()
        {
            Array.Copy(_before, _depth, _depth.Length);
            CellGrid grid = _d.Cells;
            CellColumn a = grid.Columns[_from.Column], b = grid.Columns[_to.Column];
            bool remove = _removing || _letGo;
            if (!remove && _picked.Count > 0 && grid.Columns[_picked[0].Column].Strip != a.Strip)
            {
                for (int c = 0; c < _depth.Length; c++)
                {
                    _depth[c] = -1;   // a new strip: a new selection
                }
            }
            int low = Math.Min(a.Space, b.Space), high = Math.Max(a.Space, b.Space);
            int row = remove ? Math.Min(_from.Row, _to.Row) : Math.Max(_from.Row, _to.Row);
            for (int space = low; space <= high; space++)
            {
                int column = grid.ColumnOf(a.Strip, space);
                if (column >= 0)
                {
                    BuildModeRules.Toggle(_depth, column, Math.Min(row, grid.Columns[column].Rows - 1), remove);
                }
            }
            Gather();
        }

        // A right click: the square under the pointer and those behind it let go. True when anything changed.
        private bool LetGo(CellAt cell)
        {
            if (!Picks(cell))
            {
                return false;
            }
            BuildModeRules.Toggle(_depth, cell.Column, cell.Row, true);
            Gather();
            return true;
        }

        // The picks as the site rule reads them: each column's deepest row, along the strip in order.
        private void Gather()
        {
            _picked.Clear();
            CellGrid grid = _d.Cells;
            for (int c = 0; c < _depth.Length; c++)
            {
                if (_depth[c] < 0)
                {
                    continue;
                }
                var at = new CellAt(c, _depth[c]);
                int i = _picked.Count;
                _picked.Add(at);
                while (i > 0 && grid.Columns[_picked[i - 1].Column].Space > grid.Columns[c].Space)
                {
                    _picked[i] = _picked[i - 1];   // kept in order along the strip: a handful at most
                    i--;
                }
                _picked[i] = at;
            }
        }

        // What is picked changed: the layer coloured; on a release the picks are read for the tool and the window written; while
        // dragging only the top bar's count and price.
        private void Changed(bool released)
        {
            if (_picked.Count == 0 && _state.Step == BuildStep.Picked)
            {
                _state = BuildModeRules.Next(_state, BuildInput.Clear, _state.Tool, false);
            }
            if (released)
            {
                Pick();
                Write(false);
            }
            else
            {
                _siteWhy = CellRules.Site(_d.Cells, _picked, BuildCatalogue.SiteSpacesMost, out _site);
                _panel?.Status(Summary());
            }
            Colour();
        }

        // The square under the pointer said in the top bar: its zoning, height limit, price and who holds it.
        private void Hover()
        {
            if (BuildModeGame.OverInterface || !BuildModeGame.Pointed(out Vector3 at))
            {
                if (_hoverColumn >= 0)
                {
                    _hoverColumn = _hoverRow = -1;
                    _hoverWords = null;
                    _panel?.Status(Summary());
                }
                return;
            }
            CellAt cell = _d.Cells.At(at.x, at.z);
            if (cell.Column == _hoverColumn && cell.Row == _hoverRow)
            {
                return;
            }
            _hoverColumn = cell.Column;
            _hoverRow = cell.Row;
            _hoverWords = cell.None ? null : _d.CellWords(cell.Column, cell.Row);
            _panel?.Status(_hoverWords ?? Summary());
        }

        /// <summary>The cell under the pointer in words, for the tests (null for none).</summary>
        public string HoverWords => _hoverWords;

        // What is picked in a line: its squares and spaces and, for land, its price; or why it makes no site.
        private string Summary()
        {
            if (_picked.Count == 0)
            {
                return T(_state.ViewOnly ? "reallife_bld_mode_view_only" : "reallife_bld_mode_status_none");
            }
            if (_siteWhy != null)
            {
                return T(_siteWhy);
            }
            string line = ConstructionText.Fill("reallife_bld_mode_status_picked", "cells", ConstructionText.N(_site.Cells), "spaces", ConstructionText.N(_site.Count));
            if (_state.Tool == BuildTool.Land && _d.CellsBlock(_site, out long price, out _, out _, out _) == null && price > 0L)
            {
                line += ", " + ConstructionText.Money(price);
            }
            return line;
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
            for (int c = 0; c < _depth.Length; c++)
            {
                _depth[c] = -1;
            }
            _siteWhy = CellRules.NoCells;
            _site = default(CellSite);
            _targetSite = null;
            _hasTarget = false;
            _design = -1;
            _designs.Clear();
            DropGhost();
        }

        // Esc, or the window's Clear selection: a design picked goes first, then the squares.
        private void Clear()
        {
            _state = BuildModeRules.Next(_state, BuildInput.Clear, _state.Tool, false);
            if (_state.Step == BuildStep.Picked && _design >= 0)
            {
                _design = -1;
                DropGhost();
            }
            else
            {
                ClearPick();
            }
            Write(false);
            Colour();
        }

        // ------------------------------------------------------------------ the layer and the ghost

        private void ReadFacts()
        {
            // One lot the game cannot answer for must not keep the window shut: it is drawn as unknown and logged once.
            Exception first = null;
            int failed = 0;
            for (int c = 0; c < _facts.Length; c++)
            {
                try
                {
                    _facts[c] = _d.FactsOf(c);
                }
                catch (Exception e)
                {
                    _facts[c] = default;
                    first = first ?? e;
                    failed++;
                }
            }
            if (first != null)
            {
                Logger?.Warn("Construction: build mode could not read " + failed + " column(s) of land; they are drawn as unknown. The first: " + first.GetType().Name + ": " + first.Message);
            }
            if (_view == BuildView.Value || _valuesRead)
            {
                _d.ValueSteps(_values);   // worked only once the land value view is asked for, then kept up with the facts
                _valuesRead = true;
            }
            for (int c = 0; c < _facts.Length && _valuesRead; c++)
            {
                _facts[c].ValueStep = _values[c];
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
                int rows = grid.Columns[c].Rows;
                for (int r = 0; r < rows; r++)
                {
                    BuildModeRules.Look(_facts[c], r, rows, _view, out int fill, out int line);
                    _fills[_cellStart[c] + r] = (byte)fill;
                    _lines[_cellStart[c] + r] = (byte)line;
                }
            }
            bool bad = _siteWhy != null && _state.Tool == BuildTool.Land;
            byte mark = (byte)(_state.Step == BuildStep.Ghost ? BuildModeRules.Footprint : bad ? BuildModeRules.PickedBad : BuildModeRules.Picked);
            for (int i = 0; i < _picked.Count; i++)
            {
                int c = _picked[i].Column;
                for (int r = 0; r <= _picked[i].Row && c < _cellStart.Length; r++)
                {
                    _fills[_cellStart[c] + r] = mark;
                    _lines[_cellStart[c] + r] = BuildModeRules.LinePicked;
                }
            }
            BuildModeGame.Colour(_layer, _fills, _lines);
        }

        /// <summary>The palette entries a column's cells are drawn in now (fill and edge of its street row), for the tests.</summary>
        public bool Drawn(int column, out int fill, out int line)
        {
            fill = line = -1;
            if (column < 0 || column >= _cellStart.Length || _layer == null)
            {
                return false;
            }
            fill = _fills[_cellStart[column]];
            line = _lines[_cellStart[column]];
            return true;
        }

        /// <summary>Whether the map's camera draws the layer now, and why not.</summary>
        public bool LayerSeen(out string why) => BuildModeGame.LayerSeen(_layer, out why);

        /// <summary>The window as laid out now, for the tests: null when nothing overlaps or is cut short.</summary>
        public string WindowOverlap() => _panel != null ? _panel.Overlap() : "no window";

        /// <summary>A tool chosen, as its link in the window does (for the tests).</summary>
        public void UseTool(BuildTool tool) => ToolPicked((int)tool);

        /// <summary>A view chosen, as its link in the window does (for the tests).</summary>
        public void UseView(BuildView view) => ViewPicked((int)view);

        /// <summary>The window brought back to the middle, as its arrow does (for the tests).</summary>
        public void Unfold() => _panel?.Fold(false);

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
