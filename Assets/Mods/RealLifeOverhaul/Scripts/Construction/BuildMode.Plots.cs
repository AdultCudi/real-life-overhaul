using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using UnityEngine;

namespace RealLifeOverhaul.Construction
{
    /// <summary>
    /// Build mode over the whole city (the owner, 26 Oct 2026: "Can you make the cursor a '+' like how it is in CS when it in build
    /// mode? Also can you make it grids in all district?", then "Grid + buildings"): outside Uptown every plot is one of the game's
    /// buildings, read once at each opening and drawn on the roof of the box the map shows it as, coloured by what it is to the
    /// player and by the game's building type colours in every view; the game's own tooltip says its address, type, size, owner,
    /// price and state; a click opens the tool panel for it (BizMan's buy and rent, a refit, a rebuild, the demolition's refusal),
    /// what cannot be done greyed with why. Zone, Place and Join stay Uptown's, greyed with the reason while the map looks over the
    /// rest of the city. The pointer is a crosshair over the map and the game's own over the interface, put back on leaving.
    /// COST: the plots read and their meshes made once an opening (timed in Player.log), destroyed at leaving; while open, a
    /// dictionary look when the pointer's building changes, the map's middle looked at with the pointer (ten times a second), the
    /// cursor looked at twenty times a second and set only when it changes; the plots coloured again only on a view or a pick.
    /// </summary>
    public sealed partial class BuildMode
    {
        private const float CursorEvery = 0.05f;   // seconds (real time) between looks at what the pointer is over, for the cursor
        private const float UptownSlack = 20f;     // m round Uptown's squares still counted as Uptown when the map's middle is there

        private BuildModeGame.Plots _plots;
        private byte[] _plotFills = new byte[0];
        private byte[] _plotLines = new byte[0];
        private int _plot = -1;                    // the plot picked: its tool panel is up
        private int _hoverPlot = -1;
        private int _pressPlot = -1;               // the plot a press began on (a click picks it; a drag pans the map)
        private bool _pressOff;                    // a press began off the squares (on a plot or bare ground)
        private Vector2 _pressAt;
        private int _cardPlot = -1;                // the game's building card to put away next frame (it opens after build mode's click)
        private bool _inUptown = true;             // the map's middle is over Uptown
        private float _nextCursor;
        private float _uMinX, _uMinZ, _uMaxX, _uMaxZ;
        private readonly HashSet<string> _uptownAddresses = new HashSet<string>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> _worksShare = new Dictionary<string, int>(64, StringComparer.Ordinal);
        private readonly HashSet<string> _worksMine = new HashSet<string>(StringComparer.Ordinal);
        private readonly string[] _plotWhy = new string[(int)PlotAction.Count];

        // The panel's buttons for a plot.
        private const int RowPlotBuy = 60, RowPlotRent = 61, RowPlotRefit = 62, RowPlotRebuild = 63, RowPlotBulldoze = 64;

        /// <summary>How many plots the layer has at this opening, for the tests.</summary>
        public int PlotCount => _plots != null ? _plots.Count : 0;

        /// <summary>How many squares of the city the plot layer is cut into, for the tests.</summary>
        public int PlotTiles => _plots != null ? _plots.Tiles.Count : 0;

        /// <summary>How long the plots took to read and make at this opening, in milliseconds, for the tests.</summary>
        public double PlotMilliseconds => _plots != null ? _plots.Milliseconds : -1.0;

        /// <summary>True while the map's middle is over Uptown, for the tests.</summary>
        public bool InUptown => _inUptown;

        /// <summary>The plot picked now (-1 for none), for the tests.</summary>
        public int PlotPicked => _plot;

        // ------------------------------------------------------------------ made at the opening, gone at leaving

        // Every building outside Uptown read as a plot and drawn, once an opening, timed.
        private void MakePlots()
        {
            DropPlots();
            if (_layer == null)
            {
                return;   // no material to draw them with: said by the land layer already
            }
            var clock = Stopwatch.StartNew();
            _d.UptownAddresses(_uptownAddresses);
            _plots = BuildModeGame.ReadPlots(_uptownAddresses, out string why);
            if (_plots == null)
            {
                Logger?.Warn("Construction: build mode has no plots over the city: " + why + "; Uptown's squares still work.");
                return;
            }
            _d.CityWorks(_worksShare, _worksMine);
            float least = float.MaxValue, most = 0f;
            for (int i = 0; i < _plots.Count; i++)
            {
                float price = _plots.PricePerSqm[i];
                if (price > 0f)
                {
                    least = Math.Min(least, price);
                    most = Math.Max(most, price);
                }
            }
            int works = 0;
            for (int i = 0; i < _plots.Count; i++)
            {
                PlotFacts f = _plots.Facts[i];
                f.ValueStep = BuildModeCityRules.ValueStep(_plots.PricePerSqm[i], least == float.MaxValue ? 0f : least, most);
                string key = _plots.Street[i] + "#" + _plots.Number[i].ToString(CultureInfo.InvariantCulture);
                if (_worksShare.TryGetValue(key, out int share))
                {
                    f.UnderWay = true;
                    f.Progress = share;
                    f.WorksMine = _worksMine.Contains(key);
                    works++;
                }
                _plots.Facts[i] = f;
            }
            BuildModeGame.MakePlots(_plots, _layer);
            _plotFills = new byte[_plots.Count];
            _plotLines = new byte[_plots.Count];
            clock.Stop();
            _plots.Milliseconds = clock.Elapsed.TotalMilliseconds;
            Logger?.Info("Construction: build mode's plots over the city: " + _plots.Count + " of the game's buildings outside Uptown, in " + _plots.Tiles.Count
                         + " squares of " + BuildModeCityRules.TileSize.ToString("0", CultureInfo.InvariantCulture) + " m the map's camera culls, " + works
                         + " with works under way; read and made in " + _plots.Milliseconds.ToString("0.0", CultureInfo.InvariantCulture) + " ms.");
        }

        private void DropPlots()
        {
            BuildModeGame.DestroyPlots(_plots);
            _plots = null;
            _plot = _hoverPlot = _pressPlot = _cardPlot = -1;
        }

        // Uptown's squares' rectangle, with its slack: the map's middle there is over Uptown.
        private void UptownRect(CellGrid grid)
        {
            _uMinX = _uMinZ = float.MaxValue;
            _uMaxX = _uMaxZ = float.MinValue;
            for (int c = 0; c < grid.Columns.Count; c++)
            {
                int rows = grid.Columns[c].Rows;
                for (int r = 0; r < rows; r += Math.Max(1, rows - 1))
                {
                    grid.Rect(c, r, 0f, out float x1, out float z1, out float x2, out float z2);
                    _uMinX = Math.Min(_uMinX, Math.Min(x1, x2));
                    _uMaxX = Math.Max(_uMaxX, Math.Max(x1, x2));
                    _uMinZ = Math.Min(_uMinZ, Math.Min(z1, z2));
                    _uMaxZ = Math.Max(_uMaxZ, Math.Max(z1, z2));
                }
            }
            _uMinX -= UptownSlack;
            _uMinZ -= UptownSlack;
            _uMaxX += UptownSlack;
            _uMaxZ += UptownSlack;
        }

        // Where the map looks: over Uptown or the rest of the city. A change writes the tools, the hint and the panel again, and a
        // tool that is Uptown's alone gives way to Buy land.
        private void Where()
        {
            if (!BuildModeGame.Looked(out Vector3 at))
            {
                return;
            }
            bool uptown = BuildModeCityRules.InRect(at.x, at.z, _uMinX, _uMinZ, _uMaxX, _uMaxZ);
            if (uptown == _inUptown)
            {
                return;
            }
            _inUptown = uptown;
            if (BuildModeCityRules.ToolWhyAt(_state.Tool, _holdings, _state.ViewOnly, _inUptown) != null)
            {
                _state = BuildModeRules.Next(_state, BuildInput.Tool, BuildTool.Land, false);
                _view = BuildModeRules.ViewFor(BuildTool.Land);
                _zoneBrush = -1;
                ClearPick();
                Colour();
                ColourPlots();
            }
            Write(false);
        }

        /// <summary>The map's middle looked at now, as the pointer's look does (for the tests); true over Uptown.</summary>
        public bool WhereForTest()
        {
            Where();
            return _inUptown;
        }

        /// <summary>The map glided over a point and the plot layer looked at there, for the tests.</summary>
        public void FlyForTest(Vector3 at) => BuildModeGame.FlyTo(at);

        // ------------------------------------------------------------------ the pointer

        // The pointer: a crosshair over the map, the game's own over the interface, a question box, the tour and the design window.
        private void CursorTick(float now)
        {
            if (now < _nextCursor)
            {
                return;
            }
            _nextCursor = now + CursorEvery;
            bool held = (_tour != null && _tour.Running) || CatalogueShown;
            bool question = BuildModeGame.QuestionUp || _state.Step == BuildStep.Confirming;
            BuildModeGame.Pointer(BuildModeCityRules.Cursor(_state.Open, BuildModeGame.OverInterface, question, held));
        }

        /// <summary>The pointer build mode shows now, for the tests.</summary>
        public BuildCursor PointerNow => BuildModeGame.PointerNow;

        /// <summary>The pointer as build mode chooses it for a target (over the interface or the map), set at once, for the tests.</summary>
        public BuildCursor PointerFor(bool overInterface)
        {
            BuildModeGame.Pointer(BuildModeCityRules.Cursor(_state.Open, overInterface, BuildModeGame.QuestionUp, (_tour != null && _tour.Running) || CatalogueShown));
            _nextCursor = Time.unscaledTime + 1f;   // held a moment, so the tick does not set it back before the test reads it
            return BuildModeGame.PointerNow;
        }

        // ------------------------------------------------------------------ a press and the pointer over a plot

        // A press off the squares: on a plot, or bare ground; a click picks the plot or lets the picked one go, a drag pans the map.
        private void PressOff()
        {
            _pressOff = true;
            _pressPlot = BuildModeGame.PlotPointed(_plots);
            _pressAt = BuildModeGame.Mouse;
        }

        private void ReleaseOff()
        {
            _pressOff = false;
            if ((BuildModeGame.Mouse - _pressAt).sqrMagnitude > ClickPixels * ClickPixels)
            {
                _pressPlot = -1;   // a drag: the map panned
                return;
            }
            if (_pressPlot >= 0)
            {
                PickPlot(_pressPlot);
                _cardPlot = _pressPlot;   // the game's card opens after this click: put away next frame
            }
            else if (_plot >= 0)
            {
                Clear();
            }
            _pressPlot = -1;
        }

        /// <summary>A plot picked as a click on it picks it (for the tests); false for none.</summary>
        public bool PickPlotForTest(int plot)
        {
            if (_plots == null || plot < 0 || plot >= _plots.Count)
            {
                return false;
            }
            PickPlot(plot);
            return _plot == plot;
        }

        /// <summary>The first plot whose facts pass a test, -1 for none (for the tests).</summary>
        public int FindPlot(Func<PlotFacts, bool> wanted)
        {
            for (int i = 0; _plots != null && i < _plots.Count; i++)
            {
                if (wanted(_plots.Facts[i]))
                {
                    return i;
                }
            }
            return -1;
        }

        /// <summary>A plot's facts, address and middle, for the tests.</summary>
        public bool PlotFactsOf(int plot, out PlotFacts f, out string street, out int number, out Vector3 middle)
        {
            f = default(PlotFacts);
            street = null;
            number = 0;
            middle = default(Vector3);
            if (_plots == null || plot < 0 || plot >= _plots.Count)
            {
                return false;
            }
            f = _plots.Facts[plot];
            street = _plots.Street[plot];
            number = _plots.Number[plot];
            middle = new Vector3((_plots.X[plot * 4] + _plots.X[plot * 4 + 2]) * 0.5f, _plots.Roof[plot], (_plots.Z[plot * 4] + _plots.Z[plot * 4 + 2]) * 0.5f);
            return true;
        }

        // A plot picked: the squares let go, its tool panel written, its outline drawn picked.
        private void PickPlot(int plot)
        {
            ClearPick();
            _plot = plot;
            _state = BuildModeRules.Next(BuildModeRules.Next(_state, BuildInput.Press, _state.Tool, true), BuildInput.Release, _state.Tool, false);
            BuildModeGame.HideHover();
            _hoverPlot = -1;
            Write(false);
            Colour();
            ColourPlots();
        }

        // The plot under the pointer in the game's tooltip; true when the pointer is on a plot (the squares then say nothing).
        private bool HoverPlot()
        {
            int plot = BuildModeGame.PlotPointed(_plots);
            if (plot < 0)
            {
                if (_hoverPlot >= 0)
                {
                    _hoverPlot = -1;
                    _hoverWords = null;
                    BuildModeGame.HideHover();
                }
                return false;
            }
            if (plot == _hoverPlot)
            {
                return true;
            }
            _hoverPlot = plot;
            _hoverColumn = _hoverRow = -1;
            PlotCard(plot, out string head, out string kind, out string owner, out string price, out string state);
            _hoverWords = head + "; " + kind + "; " + owner + "; " + price + "; " + state;
            BuildModeGame.ShowPlotHover(head, kind, owner, price, state);
            return true;
        }

        /// <summary>A plot as its card says it (the hover's words), for the tests; null for none.</summary>
        public string PlotWords(int plot)
        {
            if (_plots == null || plot < 0 || plot >= _plots.Count)
            {
                return null;
            }
            PlotCard(plot, out string head, out string kind, out string owner, out string price, out string state);
            return head + "; " + kind + "; " + owner + "; " + price + "; " + state;
        }

        // A plot's card: its address (and the business there), type and size, owner, price and state.
        private void PlotCard(int plot, out string head, out string kind, out string owner, out string price, out string state)
        {
            PlotFacts f = _plots.Facts[plot];
            string street = _plots.Street[plot];
            int number = _plots.Number[plot];
            string address = ConstructionGame.AddressText(street, number);
            string business = BuildModeGame.BusinessName(_plots, plot);
            head = business != null ? ConstructionText.Fill("reallife_bld_plot_head", "name", business, "address", address) : address;
            kind = ConstructionText.Fill("reallife_bld_plot_kind", "type", BuildModeGame.TypeWords(_plots.Type[plot]), "area", ConstructionText.N(_plots.Sqm[plot]));
            owner = ConstructionText.Fill("reallife_bld_plot_owner", "owner", OwnerWords(plot, f));
            price = PriceWords(plot, f);
            PlotStatus s = BuildModeCityRules.Status(f);
            state = f.UnderWay
                ? ConstructionText.Fill(f.WorksMine ? "reallife_bld_plot_works_yours" : "reallife_bld_plot_works", "share", ConstructionText.N(f.Progress))
                : ConstructionText.T(BuildModeCityRules.StatusKeys[(int)s]);
        }

        private string OwnerWords(int plot, PlotFacts f)
        {
            if (f.Owned)
            {
                return ConstructionText.T("reallife_bld_plot_owner_you");
            }
            if (f.Foreign)
            {
                return ConstructionText.T("reallife_bld_plot_owner_player");
            }
            string rival = f.Rival ? BuildModeGame.RivalName(_plots.Street[plot], _plots.Number[plot], _plots, plot) : null;
            if (rival != null)
            {
                return rival;
            }
            if (f.Kept)
            {
                return ConstructionText.T("reallife_bld_plot_owner_kept");
            }
            return ConstructionText.T(f.ForSale ? "reallife_bld_plot_owner_city" : "reallife_bld_plot_owner_landlord");
        }

        // The asking price where it is listed, else its value at the game's price a square metre, and its rent a day.
        private string PriceWords(int plot, PlotFacts f)
        {
            long value = (long)Math.Round(_plots.Sqm[plot] * (double)_plots.PricePerSqm[plot]);
            long rent = BuildModeGame.DailyRent(_plots, plot);
            if (f.ForSale && ConstructionGame.ForSale(_plots.Street[plot], _plots.Number[plot], out long asked) && asked > 0L)
            {
                return ConstructionText.Fill("reallife_bld_plot_price_asked", "price", ConstructionText.Money(asked), "rent", ConstructionText.Money(rent));
            }
            return ConstructionText.Fill("reallife_bld_plot_price_value", "price", ConstructionText.Money(value), "persqm",
                ConstructionText.Money((long)Math.Round(_plots.PricePerSqm[plot])), "rent", ConstructionText.Money(rent));
        }

        // ------------------------------------------------------------------ the colours

        // Every plot's colour in the view shown, the one picked drawn picked: on an opening, a view or a pick, never a frame.
        private void ColourPlots()
        {
            if (_plots == null || _plotFills.Length < _plots.Count)
            {
                return;
            }
            for (int i = 0; i < _plots.Count; i++)
            {
                BuildModeCityRules.Look(_plots.Facts[i], _view, out int fill, out int line);
                _plotFills[i] = (byte)(i == _plot ? BuildModeRules.Picked : fill);
                _plotLines[i] = (byte)(i == _plot ? BuildModeRules.LinePicked : line);
            }
            BuildModeGame.ColourPlots(_plots, _plotFills, _plotLines);
        }

        /// <summary>The palette entries a plot is drawn in now, for the tests.</summary>
        public bool PlotDrawn(int plot, out int fill, out int line)
        {
            fill = line = -1;
            if (_plots == null || plot < 0 || plot >= _plots.Count || plot >= _plotFills.Length)
            {
                return false;
            }
            fill = _plotFills[plot];
            line = _plotLines[plot];
            return true;
        }

        /// <summary>How many of the plot layer's squares are drawn and how many the map's camera sees now, for the tests.</summary>
        public int PlotTilesSeen(out int shown) => BuildModeGame.PlotTilesSeen(_plots, out shown);

        // ------------------------------------------------------------------ the tool panel for a plot

        // What can be done with the plot picked, each greyed with why when it cannot: the game's buy and rent on its BizMan page,
        // a refit by the city's best renovation contractor, a rebuild, and the demolition's own refusal of the city's buildings.
        private void WritePlot(BuildModeSheet s)
        {
            PlotCard(_plot, out string head, out string kind, out string owner, out string price, out string state);
            s.Begin(head, null);
            s.Line(kind, BuildModeBar.Soft);
            s.Line(owner, BuildModeBar.Text);
            s.Line(price, BuildModeBar.Text);
            s.Line(state, BuildModeBar.Soft);
            PlotWhys();
            for (int a = 0; a < (int)PlotAction.Count; a++)
            {
                if (_plotWhy[a] != null)
                {
                    s.Line(ConstructionText.Fill("reallife_bld_plot_why_line", "action", ConstructionText.T(BuildModeCityRules.ActionKeys[a]), "why", ConstructionText.T(_plotWhy[a])),
                        BuildModeBar.Amber);
                }
            }
            s.Buttons(RowPlotBuy, ConstructionText.T(BuildModeCityRules.ActionKeys[(int)PlotAction.Buy]), _plotWhy[(int)PlotAction.Buy] == null, RowPlotRent,
                ConstructionText.T(BuildModeCityRules.ActionKeys[(int)PlotAction.Rent]), _plotWhy[(int)PlotAction.Rent] == null);
            string refit = _plotWhy[(int)PlotAction.Refurbish] == null
                ? ConstructionText.Fill("reallife_bld_sheet_refit", "amount", ConstructionText.Money(_d.RefitPrice(_plots.Street[_plot], _plots.Number[_plot])))
                : ConstructionText.T(BuildModeCityRules.ActionKeys[(int)PlotAction.Refurbish]);
            s.Buttons(RowPlotRefit, refit, _plotWhy[(int)PlotAction.Refurbish] == null, RowPlotRebuild, ConstructionText.T(BuildModeCityRules.ActionKeys[(int)PlotAction.Redevelop]),
                _plotWhy[(int)PlotAction.Redevelop] == null);
            s.Buttons(0, null, false, RowPlotBulldoze, ConstructionText.T(BuildModeCityRules.ActionKeys[(int)PlotAction.Bulldoze]), _plotWhy[(int)PlotAction.Bulldoze] == null);
            s.Buttons(0, null, false, RowClear, T("reallife_bld_sheet_clear"), true);
        }

        // Why each action cannot be done with the plot picked (null where it can): the game-free rules first, then the construction game's own checks.
        private void PlotWhys()
        {
            PlotFacts f = _plots.Facts[_plot];
            string street = _plots.Street[_plot];
            int number = _plots.Number[_plot];
            bool session = _state.ViewOnly;
            _plotWhy[(int)PlotAction.Buy] = BuildModeCityRules.BuyWhy(f, session);
            _plotWhy[(int)PlotAction.Rent] = BuildModeCityRules.RentWhy(f, session);
            _plotWhy[(int)PlotAction.Refurbish] = BuildModeCityRules.RefurbishWhy(f, session) ?? _d.RefitBlock(street, number);
            _plotWhy[(int)PlotAction.Redevelop] = BuildModeCityRules.RedevelopWhy(f, session) ?? _d.RebuildBlock(street, number);
            _plotWhy[(int)PlotAction.Bulldoze] = session ? BuildModeHintRules.WhySession : _d.DemolishBlock(street, number);
        }

        /// <summary>Why each action cannot be done with the plot picked now (null where it can), for the tests.</summary>
        public string PlotWhy(PlotAction action)
        {
            if (_plots == null || _plot < 0 || _plot >= _plots.Count)
            {
                return "no plot picked";
            }
            PlotWhys();
            return _plotWhy[(int)action];
        }

        // A button of a plot's panel: each through the existing path, asked first where it costs.
        private bool ActPlot(int id)
        {
            if (_plots == null || _plot < 0 || _plot >= _plots.Count || id < RowPlotBuy || id > RowPlotBulldoze)
            {
                return false;
            }
            string street = _plots.Street[_plot];
            int number = _plots.Number[_plot];
            PlotWhys();
            switch (id)
            {
                case RowPlotBuy:
                case RowPlotRent:
                    if (_plotWhy[id == RowPlotBuy ? (int)PlotAction.Buy : (int)PlotAction.Rent] == null)
                    {
                        Close();   // the map shut: the game's BizMan opens on the building's page, its offer and rent there with the game's own checks
                        if (!BuildModeGame.OpenBizMan(street, number))
                        {
                            Refresh("reallife_bld_plot_no_bizman");
                        }
                    }
                    return true;
                case RowPlotRefit:
                    if (_plotWhy[(int)PlotAction.Refurbish] == null)
                    {
                        Ask("reallife_bld_mode_confirm_refit", "reallife_bld_mode_confirm_refit_body", _d.RefitPrice(street, number), () =>
                        {
                            int project = _d.ContractorRefit(street, number, false, out string why);
                            Refresh(project > 0 ? "reallife_bld_mode_refit_started" : why);
                        });
                    }
                    return true;
                case RowPlotRebuild:
                    if (_plotWhy[(int)PlotAction.Redevelop] == null)
                    {
                        Ask("reallife_bld_plot_confirm_rebuild", "reallife_bld_plot_confirm_rebuild_body", 0L, () =>
                        {
                            int project = _d.NewRebuild(street, number, out string why);
                            Refresh(project > 0 ? "reallife_bld_plot_rebuild_planned" : why);
                        });
                    }
                    return true;
                default:
                    if (_plotWhy[(int)PlotAction.Bulldoze] == null)
                    {
                        Ask("reallife_bld_mode_confirm_demolish", "reallife_bld_mode_confirm_demolish_body", 0L, () =>
                        {
                            int project = _d.NewDemolition(street, number, out string why);
                            Refresh(project > 0 ? "reallife_bld_mode_demolition_asked" : why);
                        });
                    }
                    return true;
            }
        }

        // The panel with nothing picked over the rest of the city: what a click on a building gives.
        private void WriteCityIdle(BuildModeSheet s)
        {
            s.Begin(T(ToolKey(_state.Tool)), null);
            s.Line(T("reallife_bld_plot_idle"), BuildModeBar.Soft);
            s.Line(T(BuildModeCityRules.WhyUptownOnly), BuildModeBar.Muted);
        }
    }
}
