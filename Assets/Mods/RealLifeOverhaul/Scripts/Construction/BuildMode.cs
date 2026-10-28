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
    /// each is in the view chosen (the land's status, the zoning, land value, occupancy, what is under way). The map stays in full
    /// view (the owner, 26 Oct 2026: "when a new user sees it they must not feel where to click"): a hint bar at the top saying
    /// what to do next, the tools in their order at the foot with the views and the demand beside them, a small panel for the
    /// tool in use over the toolbar (BuildModeBar), the square under the pointer in the game's own tooltip, a window in the middle
    /// only to choose a design (BuildModePanel), a tour at the first opening (BuildModeTour), and a tinted ghost of a design on its
    /// footprint. Every action is the construction game's own call. A picked square pressed again, or right-clicked, lets it and
    /// those behind it go; a drag with shift lets a run go; Esc ends the tour, shuts the design window, lets a design or the
    /// squares go, and leaves only when nothing is left. The leave button, or anything else that shuts the map, leaves it and
    /// gives back the camera and the time; the player never moves.
    /// COST: nothing at all while closed (not enabled); while open, the mouse's buttons each frame, the cell under the pointer ten
    /// times a second (twenty while dragging); the layer coloured again and the window written only when something changes.
    /// It runs before the game's own key handling, so the Esc it takes to clear a selection never also shuts the map.
    /// </summary>
    [DefaultExecutionOrder(-31000)]
    public sealed partial class BuildMode : MonoBehaviour, CellRules.IRunKind
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("construction", "BuildMode", typeof(BuildMode));   // its Update
        private static readonly int HotUpdate = HotSpots.Slot("Construction: BuildMode.Update");

        public static IModLogger Logger;

        private const float DragEvery = 0.05f;   // seconds (real time) between looks at the cell under the pointer while dragging
        private const float HoverEvery = 0.1f;   // seconds (real time) between looks at the cell under the pointer otherwise
        private const float ClickPixels = 8f;    // a press and release closer than this is a click on one cell
        private const int MarksShown = 12;       // sites and developments listed and marked at once

        private ConstructionDirector _d;
        private MapPicker _map;                  // the map opened and its markers, its own window kept hidden
        private BuildModeKit _kit;               // the parts, copies of the game's interior designer's
        private BuildModeBar _bar;               // the interface over the map
        private BuildModePanel _catalogue;       // the design window
        private BuildModeTour _tour;
        private BuildModeGame.Layer _layer;
        private int[] _cellStart = new int[0];   // each column's first cell in the layer
        private byte[] _fills = new byte[0];
        private byte[] _lines = new byte[0];
        private int[] _depth = new int[0];       // each column's deepest row picked, -1 for none
        private int[] _before = new int[0];      // ... as it was when the press began
        private int[] _values = new int[0];      // each column's land value step, worked when the view asks
        private bool _valuesRead;
        private BuildModeRules.ColumnFacts[] _facts = new BuildModeRules.ColumnFacts[0];
        private int _heldStart;      // the layer's first held ground's square: after every cell of the grid
        private int _hoverHeld = -1; // the held ground under the pointer
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
        private bool _offStreet;                 // the pointer is off the street the drag began on (another street's squares, the road)
        private int _took;                       // what the last drag left out (CellRules.Took*), said under the tool panel's heading
        private readonly int[] _run = new int[RunMost];         // each space along a drag, from the press: what it is to the tool
        private readonly int[] _runColumn = new int[RunMost];   // ... and its column (-1 past the strip's end)
        private const int RunMost = 64;                         // spaces a drag reads along its street at most (a site takes seven)
        private LotFile _runSite;                               // the player's site a drag's run began on (Zone, Place, Join)
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
        private bool _ghostCorner;               // the ghost is a corner building's model, taken away by its own call
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
                if (_state.Tool == BuildTool.Zone && _zoneMarked > 0)
                {
                    return _zoneMarked;   // the Zone tool's squares, wherever they lie
                }
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
            GameSprites.Want(BuildModeKit.Names);   // the icons found in the one look behind the loading screen
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
            _catalogue?.Destroy();
            _catalogue = null;
            _bar?.Destroy();
            _bar = null;
            _tour = null;
            _kit?.Destroy();
            _kit = null;
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
            if (_bar == null || !_bar.Alive)
            {
                BuildModePanel.Logger = BuildModeBar.Logger = BuildModeKit.Logger = Logger;
                Transform canvas = BuildModeGame.WindowCanvas();
                _kit?.Destroy();
                _kit = canvas != null ? BuildModeKit.Take(UiClone.Dormant(canvas, "RealLife Build Mode parts (asleep)")) : null;
                _bar = _kit != null ? BuildModeBar.Make(canvas, _kit, ToolPicked, ViewPicked, Picked, Close) : null;
                if (_bar == null)
                {
                    return false;
                }
                _tour = new BuildModeTour(_kit, _bar);
                _catalogue = null;   // made at its first opening, on the same parts
            }
            if (_layer == null || _layer.Object == null || _layer.Cells != grid.LayerCells)
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
                _heldStart = at;
                at += grid.Held.Count;   // the held grounds' squares come after the grid's, drawn the one way in every view
                _fills = new byte[at];
                _lines = new byte[at];
                for (int h = _heldStart; h < at; h++)
                {
                    _fills[h] = BuildModeRules.HeldFill;
                    _lines[h] = BuildModeRules.LineHeld;
                }
                _facts = new BuildModeRules.ColumnFacts[grid.Columns.Count];
                _depth = new int[grid.Columns.Count];
                _before = new int[grid.Columns.Count];
                _values = new int[grid.Columns.Count];
            }
            _state = BuildModeRules.Next(_state, GoingPublic.InSession ? BuildInput.OpenInSession : BuildInput.Open, BuildTool.Land, false);
            _zoneBrush = -1;
            ClearPick();
            _playerAt = ConstructionGame.Player;
            BuildModeGame.CloseMenu();
            _valuesRead = false;
            ReadFacts();
            UptownRect(grid);
            _inUptown = true;   // it opens over Uptown; the map's middle is looked at with the pointer from then on
            MakePlots();
            _holdings = BuildModeHintRules.Holdings(_facts, _facts.Length);
            _state = BuildModeRules.Next(_state, BuildInput.Tool, BuildModeHintRules.FirstTool(_holdings, _state.ViewOnly), false);   // the next step not done yet
            _view = BuildModeRules.ViewFor(_state.Tool);
            WriteMarks();
            if (!_map.Open(Centre(grid), MarkPicked, Closed))   // the picker runs each pick and its close in its own guard (UiClone.Safe)
            {
                _state = BuildModeRules.Next(_state, BuildInput.MapClosed, BuildTool.Land, false);
                return false;
            }
            BuildModeGame.LockTime(true);
            BuildModeGame.HideFootLabels(true);   // the game's FPS, address and neighbourhood lines drew over the toolbar
            BuildModeGame.Show(_layer, true);
            BuildModeGame.ShowPlots(_plots, true);
            _bar.Show(true);
            Write(true);
            Colour();
            ColourPlots();
            enabled = true;
            if (BuildModeHintRules.TourWanted(BuildModeTour.Seen, _state.ViewOnly))
            {
                _tour.Start();   // the first opening: four tips, remembered once seen
            }
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
            BuildModeGame.HideFootLabels(false);
            BuildModeGame.Show(_layer, false);
            BuildModeGame.HideHover();
            _hoverHeld = -1;
            BuildModeGame.Pointer(BuildCursor.Normal);   // the game's own pointer back, on every way out
            _tour?.Hide();
            _catalogue?.Show(false);
            _bar?.Show(false);
            DropGhost();
            ClearPick();
            DropPlots();   // the plots are read again at the next opening
            _inUptown = true;
            _pressOff = false;
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
            float now = Time.unscaledTime;
            CursorTick(now);
            if (_cardPlot >= 0)
            {
                BuildModeGame.PutCardAway(_plots, _cardPlot);   // the game's card opened on the click build mode took: its tool panel is the plot's
                _cardPlot = -1;
            }
            if (_state.Step == BuildStep.Confirming)
            {
                return;
            }
            if (BuildModeGame.HoverShown && BuildModeGame.OverInterface)
            {
                BuildModeGame.HideHover();   // the pointer went onto the interface: the parts' own tooltips are the game's there
                _hoverColumn = _hoverRow = _hoverHeld = -1;
                _hoverPlot = -1;
            }
            bool tour = _tour != null && _tour.Running;
            bool window = CatalogueShown;
            if (BuildModeGame.CancelPressed && (tour || window || HasPicks || _state.Step == BuildStep.Ghost))
            {
                BuildModeGame.TakeCancel();   // Esc ends the tour, shuts the design window, then clears; the game's own Esc (shutting the map) waits for nothing left
                if (tour)
                {
                    _tour.Skip();
                }
                else if (window)
                {
                    CatalogueBack();
                }
                else
                {
                    Clear();
                }
                return;
            }
            if (tour || window)
            {
                return;   // the tour's tips and the design window hold the map
            }
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
                if (cell.None)
                {
                    PressOff();   // off Uptown's squares: a click picks the city's building under the pointer, a drag pans the map
                    return;
                }
                _state = BuildModeRules.Next(_state, BuildInput.Press, _state.Tool, !cell.None);
                if (!cell.None)
                {
                    BuildModeGame.HoldCamera(true);   // the map's left drag would pan it
                    BuildModeGame.HideHover();
                    if (_state.Tool == BuildTool.Zone)
                    {
                        ZonePressed(cell);   // on the player's own land the Zone tool's shapes pick (BuildMode.Zone); elsewhere as below
                    }
                    _down = true;
                    _downAt = BuildModeGame.Mouse;
                    _removing = BuildModeGame.Shift;
                    _letGo = !_removing && Picks(cell);   // a picked square pressed: a click lets it go
                    if (!_removing && !_letGo && !Touches(cell))
                    {
                        ClearDepths();   // a press away from what is picked (another street, or apart along this one) starts afresh
                    }
                    Array.Copy(_depth, _before, _depth.Length);
                    _from = _to = cell;
                    _offStreet = false;
                    Drag();
                    Changed(false);
                }
                return;
            }
            if (_pressOff && (BuildModeGame.Released || !BuildModeGame.Held))
            {
                ReleaseOff();
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
                    // The pointer read along the street the drag began on (CellGrid.Project): over the block's corner, onto the
                    // next street's squares or the road, the drag still picks along its own street (the owner, 26 Oct 2026).
                    CellAt under = _d.Cells.At(at.x, at.z);
                    CellAt cell = _state.Tool == BuildTool.Zone && _shape == ZoneShape.Brush && _zoneDragging && !under.None ? under : _d.Cells.Project(_from.Column, at.x, at.z);
                    bool off = under.None || _d.Cells.Columns[under.Column].Strip != _d.Cells.Columns[_from.Column].Strip;
                    if (!cell.None && (cell.Column != _to.Column || cell.Row != _to.Row || off != _offStreet))
                    {
                        _to = cell;
                        _offStreet = off;
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
                    _offStreet = false;
                    Drag();
                }
                _state = BuildModeRules.Next(_state, BuildInput.Release, _state.Tool, false);
                Changed(true);
            }
        }

        private bool HasPicks => _picked.Count > 0 || _plot >= 0;

        private bool Picks(CellAt cell) => !cell.None && cell.Column < _depth.Length && _depth[cell.Column] >= cell.Row;

        // The picks from the press: let go from the shallower row back (a shift drag, or a click on a picked square); else added
        // as Cities Skylines takes a drag (the owner, 26 Oct 2026: "selecting must be forgiving"): along the street the press began
        // on, from the street back to the deeper row, every space the tool can take (BuildModeRules.Takes) between the press and
        // the pointer, the spaces it cannot take left out (CellRules.Reach), a site's widest at most, one square alone taken a
        // square deeper; what was left out kept to say under the tool panel's heading (_took). Before, a press on another street
        // after a pick mixed both streets' squares (the reset read the picks of the drag's own first frame) and the whole pick was
        // refused ("These squares make no site", the owner's test). COST: a walk over the run's spaces, no allocation.
        private void Drag()
        {
            if (_state.Tool == BuildTool.Zone && ZoneDrag())
            {
                return;   // the Zone tool's shapes on the player's own land (BuildMode.Zone)
            }
            Array.Copy(_before, _depth, _depth.Length);
            CellGrid grid = _d.Cells;
            CellColumn a = grid.Columns[_from.Column], b = grid.Columns[_to.Column];
            if (_removing || _letGo)
            {
                int low = Math.Min(a.Space, b.Space), high = Math.Max(a.Space, b.Space);
                int shallow = Math.Min(_from.Row, _to.Row);
                for (int space = low; space <= high; space++)
                {
                    int column = grid.ColumnOf(a.Strip, space);
                    if (column >= 0)
                    {
                        BuildModeRules.Toggle(_depth, column, Math.Min(shallow, grid.Columns[column].Rows - 1), true);
                    }
                }
                _took = CellRules.TookAll;
                Gather();
                return;
            }
            _runSite = null;
            bool deepen = _state.Tool == BuildTool.Land || _state.Tool == BuildTool.Place;   // a site's footprint: one square alone is none
            int took = CellRules.DragPick(grid, _from, _to, this, BuildCatalogue.SiteSpacesMost, deepen, _depth, _run, _runColumn, out int first);
            if (CellRules.TrimWide(grid, _depth, first, BuildCatalogue.SiteSpacesMost))
            {
                took = CellRules.TookWide;
            }
            Gather();
            _took = took == CellRules.TookAll && _offStreet ? CellRules.TookStreet : took;
        }

        /// <summary>
        /// What a space along a drag is to the tool in use (CellRules.IRunKind, asked in order from the pressed space): one it may
        /// take (BuildModeRules.Takes), and for Zone, Place and Join of the one site of the player's the run began on.
        /// </summary>
        public int Kind(int column)
        {
            if (column < 0 || column >= _facts.Length || !BuildModeRules.Takes(_state.Tool, _facts[column]))
            {
                return CellRules.RunTaken;
            }
            if (_state.Tool == BuildTool.Zone || _state.Tool == BuildTool.Place || _state.Tool == BuildTool.Join)
            {
                _d.RowsHeld(column, out LotFile own);
                if (own == null || (_runSite != null && own != _runSite))
                {
                    return CellRules.RunTaken;
                }
                _runSite = own;
            }
            return CellRules.RunOk;
        }

        // True when a press on this cell joins what is picked: on the same street, at or beside the picked squares (nothing picked: true).
        private bool Touches(CellAt cell) => CellRules.Touches(_d.Cells, _depth, cell);

        private void ClearDepths()
        {
            for (int c = 0; c < _depth.Length; c++)
            {
                _depth[c] = -1;
            }
            _picked.Clear();
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
            _took = CellRules.TookAll;
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
                Colour();
                AfterPick();
                return;
            }
            _siteWhy = CellRules.Site(_d.Cells, _picked, BuildCatalogue.SiteSpacesMost, out _site);
            if (_bar != null)
            {
                WriteHint();   // while dragging: the line alone; the panel waits for the release
            }
            Colour();
        }

        // The square under the pointer said in the game's own tooltip beside it: who holds it, its zoning, its price or its zone.
        private void Hover()
        {
            Where();
            if (BuildModeGame.OverInterface || !BuildModeGame.Pointed(out Vector3 at))
            {
                if (_hoverColumn >= 0 || _hoverPlot >= 0 || _hoverHeld >= 0)
                {
                    _hoverColumn = _hoverRow = -1;
                    _hoverHeld = -1;
                    _hoverPlot = -1;
                    _hoverWords = null;
                    BuildModeGame.HideHover();
                }
                return;
            }
            if (HoverPlot())
            {
                return;   // a building of the city's: its card in the game's tooltip
            }
            CellAt cell = _d.Cells.At(at.x, at.z);
            int held = cell.None ? _d.Cells.HeldAt(at.x, at.z) : -1;   // bare ground no square covers: a special place's, a park's, the campus's, the district's edge
            if (cell.Column == _hoverColumn && cell.Row == _hoverRow && held == _hoverHeld)
            {
                return;
            }
            _hoverColumn = cell.Column;
            _hoverRow = cell.Row;
            _hoverHeld = held;
            _hoverWords = cell.None ? null : _d.CellWords(cell.Column, cell.Row);
            if (!cell.None && _d.HoverCard(cell.Column, cell.Row, out string head, out string zoning, out string more))
            {
                BuildModeGame.ShowHover(head, zoning, more);
            }
            else if (held >= 0 && _d.HeldCard(held, out string heldHead, out string heldWhy, out string heldMore))
            {
                _hoverWords = heldHead + ". " + heldWhy;
                BuildModeGame.ShowHover(heldHead, heldWhy, heldMore);
            }
            else
            {
                BuildModeGame.HideHover();
            }
        }

        /// <summary>The cell under the pointer in words, for the tests (null for none).</summary>
        public string HoverWords => _hoverWords;

        // What the cells picked are to the tool: a site to buy, the player's site, a footprint, a building.
        private void Pick()
        {
            _siteWhy = CellRules.Site(_d.Cells, _picked, BuildCatalogue.SiteSpacesMost, out _site);
            _targetSite = null;
            _hasTarget = false;
            _canBuy = _pickMine = false;
            _parkFacts = default(ParkFacts);
            _parkPicked = null;
            DropGhost();
            _design = -1;
            _designs.Clear();
            if (_picked.Count == 0)
            {
                return;
            }
            int column = _picked[0].Column;
            _canBuy = _siteWhy == null && _state.Tool == BuildTool.Land && _d.CellsBlock(_site, out _, out _, out _, out _) == null;
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
                    _pickMine = site != null;
                    for (int i = 1; i < _picked.Count && _pickMine; i++)
                    {
                        _d.RowsHeld(_picked[i].Column, out LotFile other);
                        _pickMine = other == site;   // every square the player's, of one site
                    }
                    if (_siteWhy == null && _pickMine && _d.ParkOfColumn(column) == null)
                    {
                        _d.FootprintDesigns(_site.FirstColumn, _site.Count, _site.MinRows, _use, _designs);
                        if (_designs.Count > BuildModeCatalogue.Most)
                        {
                            _designs.RemoveRange(BuildModeCatalogue.Most, _designs.Count - BuildModeCatalogue.Most);
                        }
                    }
                    ParkRead();   // BuildMode.Park: a car park on the squares, or what one would be
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
            if (_plot >= 0)
            {
                _plot = -1;
                ColourPlots();
            }
            _picked.Clear();
            for (int c = 0; c < _depth.Length; c++)
            {
                _depth[c] = -1;
            }
            ZoneClear();
            _growPanel = false;
            _parkMode = false;
            _parkPicked = null;
            _parkFacts = default(ParkFacts);
            _siteWhy = CellRules.NoCells;
            _site = default(CellSite);
            _took = CellRules.TookAll;
            _targetSite = null;
            _hasTarget = false;
            _canBuy = _pickMine = false;
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
            if (_state.Tool == BuildTool.Zone && _zoneMarked > 0)
            {
                ZoneColour();   // the squares marked, each on its own
                BuildModeGame.Colour(_layer, _fills, _lines);
                return;
            }
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

        /// <summary>The interface as laid out now, for the tests: null when nothing overlaps, runs off the screen or is cut short.</summary>
        public string WindowOverlap() => _bar != null ? _bar.Overlap() : "no interface";

        /// <summary>The design window as laid out now, for the tests: null when nothing is cut short or meets.</summary>
        public string CatalogueOverlap() => _catalogue != null ? _catalogue.Overlap(_bar != null ? _bar.ToolbarTop() : float.NaN) : "no design window";

        /// <summary>The design window's heading and the scale it is drawn at, for the tests.</summary>
        public string CatalogueHeading => _catalogue != null ? _catalogue.Heading : string.Empty;

        /// <summary>How many cards the design window shows, for the tests.</summary>
        public int CatalogueCards => _catalogue != null ? _catalogue.CardsShown : 0;

        /// <summary>The scale the design window is drawn at, for the tests.</summary>
        public float CatalogueScale => _catalogue != null ? _catalogue.Scale : 1f;

        /// <summary>True when a part of the interface covers the map's centre, with which, for the tests.</summary>
        public bool CoversCentre(out string what)
        {
            what = null;
            return _bar != null && _bar.CoversCentre(out what);
        }

        /// <summary>A tool's button greyed now, and the reason its tooltip gives, for the tests.</summary>
        public bool ToolGreyed(BuildTool tool, out string why)
        {
            why = null;
            return _bar != null && _bar.ToolGreyed(tool, out why);
        }

        /// <summary>The tools as greyed for these holdings, for the tests (Write puts the real ones back).</summary>
        public void ShowToolsFor(BuildHoldings holdings)
        {
            for (int t = 0; t < (int)BuildTool.Count; t++)
            {
                string why = BuildModeCityRules.ToolWhyAt((BuildTool)t, holdings, _state.ViewOnly, _inUptown);
                _toolNames[t] = T(BuildModeHintRules.ToolKeys[t]);
                _toolLines[t] = T(BuildModeHintRules.ToolTips[t]);
                _toolWhy[t] = why != null ? T(why) : null;
            }
            _bar?.Tools(_state.Tool, _toolNames, _toolLines, _toolWhy);
        }

        /// <summary>The interface written again as it stands, for the tests.</summary>
        public void Rewrite() => Write(false);

        /// <summary>What the player holds as the toolbar reads it now, for the tests.</summary>
        public BuildHoldings Holdings => _holdings;

        /// <summary>A tool chosen, as its button does (for the tests); a greyed one is not.</summary>
        public void UseTool(BuildTool tool) => ToolPicked((int)tool);

        /// <summary>A view chosen, as its toggle does (for the tests).</summary>
        public void UseView(BuildView view) => ViewPicked((int)view);

        /// <summary>Squares picked as a press and release on the map pick them: so many columns from a first, each so many rows (0: every row), for the tests.</summary>
        public void PickForTest(int first, int count, int rows)
        {
            ClearPick();
            CellGrid grid = _d.Cells;
            int at = first;
            for (int k = 0; k < count && at >= 0; k++, at = grid.Next(at))
            {
                int r = rows <= 0 ? grid.Columns[at].Rows : Math.Min(rows, grid.Columns[at].Rows);
                _depth[at] = r - 1;
            }
            Gather();
            _state = BuildModeRules.Next(BuildModeRules.Next(_state, BuildInput.Press, _state.Tool, true), BuildInput.Release, _state.Tool, false);
            Changed(true);
        }

        /// <summary>
        /// A drag as the pointer makes one (for the tests): pressed on a column's row, let go over a point on the ground (x, z), each
        /// read as OnUpdate reads them, what was picked before kept as a press keeps it; true when anything is picked.
        /// </summary>
        public bool DragForTest(int column, int row, float x, float z)
        {
            if (column < 0 || column >= _depth.Length)
            {
                return false;
            }
            var cell = new CellAt(column, Math.Min(row, _d.Cells.Columns[column].Rows - 1));
            _state = BuildModeRules.Next(_state, BuildInput.Press, _state.Tool, true);
            _removing = _letGo = false;
            if (!Touches(cell))
            {
                ClearDepths();   // as a press does: what is picked elsewhere let go first
            }
            Array.Copy(_depth, _before, _depth.Length);
            _from = cell;
            _to = _d.Cells.Project(column, x, z);
            CellAt under = _d.Cells.At(x, z);
            _offStreet = under.None || _d.Cells.Columns[under.Column].Strip != _d.Cells.Columns[column].Strip;
            Drag();
            _state = BuildModeRules.Next(_state, BuildInput.Release, _state.Tool, false);
            Changed(true);
            return _picked.Count > 0;
        }

        /// <summary>What the last drag left out (CellRules.Took*), for the tests.</summary>
        public int Took => _took;

        /// <summary>Everything picked let go, as Esc does (for the tests).</summary>
        public void ClearForTest() => Clear();

        /// <summary>The design window shut, as its Back to the map does (for the tests).</summary>
        public void ShutCatalogue() => CatalogueBack();

        /// <summary>The tour started again from its first tip, for the tests; true when a tip is up and placed well (why not in <paramref name="wrong"/>).</summary>
        public bool TourForTest(out string wrong)
        {
            wrong = "no tour";
            if (_tour == null)
            {
                return false;
            }
            _tour.Start();
            wrong = _tour.Check();
            return wrong == null;
        }

        /// <summary>The tour moved on (or ended after its last tip), for the tests; the step it shows then.</summary>
        public TourStep TourNextForTest(out string wrong)
        {
            wrong = null;
            if (_tour == null)
            {
                wrong = "no tour";
                return TourStep.Done;
            }
            _tour.Next();
            wrong = _tour.Running ? _tour.Check() : null;
            return _tour.Step;
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
                DesignChoice d = _designs[_design];
                _ghostCorner = !string.IsNullOrEmpty(d.Look);
                _ghost = _ghostCorner
                    ? BuildModeBridges.CornerGhost?.Invoke(d.Look, d.LookX, d.LookZ, d.LookYaw)   // the corner building's model, turned to the corner's two streets
                    : BuildModeBridges.GhostStand(d.Name, d.Size, x, z, facing, width, depth);
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
                Action<Transform> gone = _ghostCorner ? BuildModeBridges.CornerGhostGone : BuildModeBridges.GhostGone;
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
            _ghostCorner = false;
        }

        /// <summary>The ghost standing now, for the tests.</summary>
        public bool GhostStands => _ghost != null;
    }
}
