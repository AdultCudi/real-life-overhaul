using System;
using System.Collections.Generic;
using System.Globalization;
using Localizor;
using Localizor.LanguageChangeEvent;
using RealLifeOverhaul.GameUi;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Construction
{
    /// <summary>
    /// What build mode's interface says (BuildModeBar over the map, BuildModePanel for the designs): the hint bar's line for where
    /// build mode stands (BuildModeHintRules), the tools with the reason each cannot be used yet, the views, the demand, the tool
    /// panel for the tool in use (what is picked with its figures and the one or two things to press), and the design window.
    /// Written again only after a pick or an action. Each action is the construction game's own call, a cost asked first in the
    /// game's question box.
    /// </summary>
    public sealed partial class BuildMode
    {
        // The panel's buttons.
        private const int RowBuy = 1, RowClear = 2, RowChoose = 3, RowPaint = 10, RowPaintNone = 14, RowParking = 20, RowBuild = 21, RowOther = 22, RowDemolish = 30, RowJoin = 40,
            RowSplit = 41, RowRefit = 50;
        private const int RowMark = 300;
        private const int BrushNone = (int)BuildUse.Count;   // the zone brush that takes a zone away

        private readonly int[] _keyFills = new int[BuildModeRules.LegendMost];
        private readonly int[] _keyLines = new int[BuildModeRules.LegendMost];
        private readonly string[] _keyWords = new string[BuildModeRules.LegendMost];
        private readonly string[] _toolNames = new string[(int)BuildTool.Count];
        private readonly string[] _toolLines = new string[(int)BuildTool.Count];
        private readonly string[] _toolWhy = new string[(int)BuildTool.Count];
        private readonly string[] _viewNames = new string[(int)BuildView.Count];
        private readonly string[] _viewLines = new string[(int)BuildView.Count];
        private readonly string[] _filterNames = new string[BuildModeCatalogue.FilterCount];
        private BuildHoldings _holdings;
        private int _zoneBrush = -1;          // the Zone tool's chosen zone: a use, BrushNone, or -1 for none chosen
        private bool _canBuy, _pickMine;
        private int _landCells;               // squares of the player's own land, for the Zone panel

        // The design window's facts for each design that fits the footprint's ground: its warnings, estimate, days, whether it can be built, a corner building.
        private readonly int[] _dWarn = new int[BuildModeCatalogue.Most];
        private readonly long[] _dCost = new long[BuildModeCatalogue.Most];
        private readonly int[] _dDays = new int[BuildModeCatalogue.Most];
        private readonly bool[] _dFits = new bool[BuildModeCatalogue.Most];
        private readonly bool[] _dCorner = new bool[BuildModeCatalogue.Most];
        private readonly int[] _dOrder = new int[BuildModeCatalogue.Most];
        private int _filter;
        private int _card = -1;               // the card picked in the window, not placed yet

        /// <summary>A tool's name key.</summary>
        internal static string ToolKey(BuildTool tool) => BuildModeHintRules.ToolKeys[(int)tool];

        private static string T(string key) => ConstructionText.T(key);

        private static string T(string key, string name, string value) => ConstructionText.T(key, name, value);

        private Color Colour(int entry)
        {
            if (_layer != null)
            {
                return BuildModeGame.Swatch(_layer, entry);
            }
            byte[] p = BuildModeRules.Palette[Math.Max(0, Math.Min(BuildModeRules.PaletteSize - 1, entry))];
            return new Color32(p[0], p[1], p[2], 255);
        }

        /// <summary>Everything written again: the tools, the views, the demand, the tool panel, the hint bar, the map's markers.</summary>
        private void Write(bool top)
        {
            if (_bar == null || !_bar.Alive)
            {
                return;
            }
            _holdings = BuildModeHintRules.Holdings(_facts, _facts.Length);
            _landCells = 0;
            for (int c = 0; c < _facts.Length; c++)
            {
                _landCells += _facts[c].Mine ? _facts[c].MineRows : 0;
            }
            WriteTools();
            WriteViews();
            WriteDemand();
            WriteSheet();
            WriteHint();
            WriteMarks();
            _bar.Place();
        }

        private void WriteTools()
        {
            for (int t = 0; t < (int)BuildTool.Count; t++)
            {
                _toolNames[t] = T(BuildModeHintRules.ToolKeys[t]);
                _toolLines[t] = T(BuildModeHintRules.ToolTips[t]);
                string why = BuildModeHintRules.ToolWhy((BuildTool)t, _holdings, _state.ViewOnly);
                _toolWhy[t] = why != null ? T(why) : null;
            }
            _bar.Tools(_state.Tool, _toolNames, _toolLines, _toolWhy);
        }

        private void WriteViews()
        {
            for (int v = 0; v < (int)BuildView.Count; v++)
            {
                _viewNames[v] = T(BuildModeRules.ViewKeys[v]);
                _viewLines[v] = T(BuildModeHintRules.ViewTips[v]);
            }
            _bar.Views(_view, _viewNames, _viewLines);
        }

        // The district's demand for each use as a short read-only bar in the use's colour (Cities Skylines' demand), from the district's own count.
        private void WriteDemand()
        {
            for (int u = 0; u < (int)BuildUse.Count; u++)
            {
                int demand = _d.DemandFor((BuildUse)u);
                string name = T(BuildCatalogue.UseKeys[u]);
                string title = demand < 0 ? name : ConstructionText.Fill("reallife_bld_tip_demand_title", "use", name, "value", ConstructionText.N(demand));
                _bar.Demand(u, demand, Colour(BuildModeRules.Homes + u), title, T(BuildModeHintRules.DemandWords(demand)));
            }
        }

        // ------------------------------------------------------------------ the hint bar

        private HintFacts Facts()
        {
            return new HintFacts
            {
                State = _state, Picked = _picked.Count > 0, SiteOk = _siteWhy == null && _picked.Count > 0, CanBuy = _canBuy,
                PickedMine = _state.Tool == BuildTool.Place ? _pickMine : _targetSite != null,
                PickedBuilding = _hasTarget, ZoneChosen = _zoneBrush >= 0, Designs = _designs.Count, MayBuild = BuildModeRules.MayBuild(_warnings)
            };
        }

        private void WriteHint()
        {
            HintFacts f = Facts();
            string key = BuildModeHintRules.Hint(f);
            string next = key == null ? string.Empty : ConstructionText.Fill(key, "cells", ConstructionText.N(PickedCells), "use", BrushName());
            string extra = BuildModeHintRules.HintExtra(f);
            string esc = BuildModeHintRules.Esc(_state, _picked.Count > 0);
            string more = extra != null && esc != null ? T(extra) + " · " + T(esc) : extra != null ? T(extra) : esc != null ? T(esc) : null;
            _bar.Hint(BuildModeHintRules.HintStep(f), next, more);
        }

        private string BrushName()
        {
            return _zoneBrush >= 0 && _zoneBrush < (int)BuildUse.Count ? T(BuildCatalogue.UseKeys[_zoneBrush]) : T("reallife_bld_mode_zone_none");
        }

        /// <summary>The hint bar's words now, for the tests.</summary>
        public string HintShown => _bar != null ? _bar.HintWords : null;

        /// <summary>The hint bar's words build mode's rules want now, for the tests.</summary>
        public string HintWanted
        {
            get
            {
                string key = BuildModeHintRules.Hint(Facts());
                return key == null ? null : ConstructionText.Fill(key, "cells", ConstructionText.N(PickedCells), "use", BrushName());
            }
        }

        // ------------------------------------------------------------------ the tool panel

        private void WriteSheet()
        {
            BuildModeSheet s = _bar.Sheet;
            switch (_state.Tool)
            {
                case BuildTool.Land: WriteLand(s); break;
                case BuildTool.Zone: WriteZone(s); break;
                case BuildTool.Place: WritePlace(s); break;
                case BuildTool.Bulldoze: WriteBuilding(s, RowDemolish); break;
                case BuildTool.Join: WriteJoin(s); break;
                default: WriteBuilding(s, RowRefit); break;
            }
            WriteLegend(s);
            s.End();
        }

        private string Count() => _picked.Count > 0 ? ConstructionText.Fill("reallife_bld_sheet_count", "cells", ConstructionText.N(PickedCells)) : null;

        private void WriteLegend(BuildModeSheet s)
        {
            int n = BuildModeRules.Legend(_view, _keyFills, _keyLines, _keyWords);
            for (int i = 0; i < n; i++)
            {
                s.Legend(_keyFills[i] >= 0 ? Colour(_keyFills[i]) : Color.clear, Colour(_keyLines[i]), _keyFills[i] < 0, T(_keyWords[i]));
            }
        }

        private void WriteLand(BuildModeSheet s)
        {
            s.Begin(T("reallife_bld_mode_tool_land"), Count());
            if (_picked.Count == 0)
            {
                s.Line(T("reallife_bld_sheet_land_none"), BuildModeBar.Soft);
                return;
            }
            if (_siteWhy != null)
            {
                s.Line(T(_siteWhy), BuildModeBar.Red);
                s.Buttons(0, null, false, RowClear, T("reallife_bld_sheet_clear"), true);
                return;
            }
            string block = _d.CellsBlock(_site, out long price, out _, out _, out LotFile mine);
            _d.LotOfColumn(_site.FirstColumn, out LotView first);
            bool corner = CellRules.Corner(_d.Cells, _site.FirstColumn, _site.Count, out _, out _);
            int zones = mine != null ? _d.ZonesNow(mine) : LandRules.ZonesOf(_site.Count >= 3 ? 3 : _site.Count == 2 ? 1 : 0);   // a site's class by its spaces, as Joined gives it
            long tax = price > 0L ? LandRules.LandTax(price, ConstructionGame.DaysPerYear, ConstructionGame.DaysPerYear, false, ConstructionDirector.Code) : 0L;   // a year's, as the Land tab says it
            s.Line(Where(first, corner), BuildModeBar.Soft);
            s.Fact(T("reallife_bld_sheet_price"), price > 0L ? ConstructionText.Money(price) : "-", BuildModeBar.Text);
            s.Fact(T("reallife_bld_sheet_tax"), ConstructionText.Money(tax), BuildModeBar.Text);
            s.Fact(T("reallife_bld_sheet_zoned"), ConstructionDirector.UsesWords(zones), ZonedColour(zones));
            s.Fact(T("reallife_bld_sheet_limit"), ConstructionText.Fill("reallife_bld_sheet_storeys", "storeys", ConstructionText.N(_d.UptownStoreys())), BuildModeBar.Text);
            s.Fact(T("reallife_bld_sheet_corner"), T(corner ? "reallife_bld_sheet_corner_yes" : "reallife_bld_sheet_corner_no"), BuildModeBar.Text);
            if (_state.ViewOnly)
            {
                s.Line(T(BuildModeHintRules.WhySession), BuildModeBar.Amber);
            }
            else if (block != null)
            {
                s.Line(T(block), BuildModeBar.Red);
            }
            s.Buttons(RowBuy, ConstructionText.Fill("reallife_bld_sheet_buy", "amount", price > 0L ? ConstructionText.Money(price) : "-"), block == null && !_state.ViewOnly && price > 0L,
                RowClear, T("reallife_bld_sheet_clear"), true);
        }

        // Where the squares picked are: their street, and whether they hold its corner.
        private string Where(LotView first, bool corner)
        {
            string street = string.IsNullOrEmpty(first.Street) ? _d.LotWords(first.Id) : first.Street;
            return ConstructionText.Fill(corner ? "reallife_bld_sheet_where_corner" : "reallife_bld_sheet_where", "street", street);
        }

        // A zoning's figure in its use's colour when it is one use; the panel's words otherwise.
        private Color ZonedColour(int zones)
        {
            for (int u = 0; u < (int)BuildUse.Count; u++)
            {
                if (zones == BuildCatalogue.Zone((BuildUse)u))
                {
                    Color c = Colour(BuildModeRules.Homes + u);
                    return Color.Lerp(c, Color.white, 0.25f);   // a little lighter, to read on the dark box
                }
            }
            return BuildModeBar.Text;
        }

        private void WriteZone(BuildModeSheet s)
        {
            s.Begin(T("reallife_bld_mode_tool_zone"), ConstructionText.Fill("reallife_bld_sheet_zone_count", "cells", ConstructionText.N(_landCells)));
            LotFile site = _targetSite != null ? _d.State.Lot(_targetSite) : null;
            if (_picked.Count > 0 && site == null)
            {
                s.Line(T("reallife_bld_hint_zone_not_yours"), BuildModeBar.Red);
            }
            else if (site != null)
            {
                s.Line(ConstructionText.Fill("reallife_bld_sheet_zone_site", "uses", ConstructionDirector.UsesWords(_d.ZonesNow(site))), BuildModeBar.Soft);
            }
            else
            {
                s.Line(T("reallife_bld_sheet_zone_line"), BuildModeBar.Soft);
            }
            for (int u = 0; u < (int)BuildUse.Count; u++)
            {
                string status = ZoneStatus(site, (BuildUse)u, out Color colour);
                string name = T(BuildCatalogue.UseKeys[u]);
                bool chosen = _zoneBrush == u;
                s.Swatch(RowPaint + u, Colour(BuildModeRules.Homes + u), chosen ? T("reallife_bld_sheet_zone_picked", "use", name) : name, status, colour, chosen, !_state.ViewOnly);
            }
            s.Swatch(RowPaintNone, Color.clear, T("reallife_bld_sheet_zone_remove"), null, BuildModeBar.Soft, _zoneBrush == BrushNone, !_state.ViewOnly);
        }

        // Whether a use is allowed on the site picked (or on the player's land when none is): allowed here, on some of it, or City Hall's with its fee.
        private string ZoneStatus(LotFile site, BuildUse use, out Color colour)
        {
            int allowed = 0, sites = 0;
            if (site != null)
            {
                sites = 1;
                allowed = LandRules.Zoned(_d.ZonesNow(site), use) ? 1 : 0;
            }
            else
            {
                for (int i = 0; i < _d.State.Lots.Count; i++)
                {
                    LotFile l = _d.State.Lots[i];
                    sites++;
                    allowed += LandRules.Zoned(_d.ZonesNow(l), use) ? 1 : 0;
                }
            }
            if (sites > 0 && allowed == sites)
            {
                colour = BuildModeBar.Good;
                return T("reallife_bld_sheet_zone_allowed");
            }
            if (allowed > 0)
            {
                colour = BuildModeBar.Good;
                return T("reallife_bld_sheet_zone_some");
            }
            colour = BuildModeBar.Amber;
            return T("reallife_bld_sheet_zone_ask", "fee", ConstructionText.Money(ConstructionDirector.ZoneFee));
        }

        private void WritePlace(BuildModeSheet s)
        {
            if (_state.Step == BuildStep.Ghost || _state.Step == BuildStep.Confirming)
            {
                WriteGhost(s);
                return;
            }
            s.Begin(T("reallife_bld_mode_tool_place"), Count());
            if (_picked.Count == 0)
            {
                s.Line(T("reallife_bld_sheet_place_none"), BuildModeBar.Soft);
                return;
            }
            if (_siteWhy != null || !_pickMine)
            {
                s.Line(T(_siteWhy ?? "reallife_bld_hint_place_not_yours"), BuildModeBar.Red);
                s.Buttons(0, null, false, RowClear, T("reallife_bld_sheet_clear"), true);
                return;
            }
            if (_designs.Count == 0)
            {
                s.Line(T("reallife_bld_mode_place_none"), BuildModeBar.Red);
                s.Buttons(0, null, false, RowClear, T("reallife_bld_sheet_clear"), true);
                return;
            }
            s.Line(ConstructionText.Fill("reallife_bld_sheet_place_fit", "count", ConstructionText.N(_designs.Count), "cells", ConstructionText.N(PickedCells), "use",
                T(BuildCatalogue.UseKeys[(int)_use])), BuildModeBar.Soft);
            s.Buttons(RowChoose, T("reallife_bld_cat_title"), !_state.ViewOnly, RowClear, T("reallife_bld_sheet_clear"), true);
        }

        private void WriteGhost(BuildModeSheet s)
        {
            if (_design < 0 || _design >= _designs.Count)
            {
                s.Begin(T("reallife_bld_mode_tool_place"), Count());
                return;
            }
            DesignChoice d = _designs[_design];
            s.Begin(SizeName(d), Count());
            s.Fact(T("reallife_bld_sheet_cost"), ConstructionText.Money(_estimate), BuildModeBar.Text);
            s.Fact(T("reallife_bld_sheet_days"), ConstructionText.Fill("reallife_bld_sheet_days_value", "days", ConstructionText.N(_d.PlaceDays(d, _use))), BuildModeBar.Text);
            int storeys = PlansRules.Storeys(d.Height);
            s.Fact(T("reallife_bld_sheet_storeys_name"), ConstructionText.Fill("reallife_bld_sheet_storeys_of", "storeys", ConstructionText.N(storeys), "limit", ConstructionText.N(_limit)),
                _limit > 0 && storeys > _limit ? BuildModeBar.Amber : BuildModeBar.Text);
            s.Fact(T("reallife_bld_sheet_area"), ConstructionText.Fill("reallife_bld_sheet_area_value", "area", ConstructionText.N(d.TotalSqm)), BuildModeBar.Text);
            for (int b = 0; b < BuildModeRules.WarnCount; b++)
            {
                int bit = 1 << b;
                if ((_warnings & bit) != 0)
                {
                    s.Line(T(BuildModeRules.WarnKeys[b]), (BuildModeRules.Stops & bit) != 0 ? BuildModeBar.Red : BuildModeBar.Amber);
                }
            }
            s.Buttons(0, null, false, RowParking, T(_parking ? "reallife_bld_sheet_parking_on" : "reallife_bld_sheet_parking_off"), !_state.ViewOnly);
            s.Buttons(RowBuild, T("reallife_bld_mode_button_build"), BuildModeRules.MayBuild(_warnings) && !_state.ViewOnly && _state.Step == BuildStep.Ghost, RowOther,
                T("reallife_bld_sheet_other"), _state.Step == BuildStep.Ghost);
        }

        // A design as the window names it: its size and version as the game's blueprints do ("C1"), and its type; a corner building by its name.
        private static string SizeName(DesignChoice d)
        {
            if (!string.IsNullOrEmpty(d.Look))
            {
                return string.IsNullOrEmpty(d.LookKey) ? d.Look : T(d.LookKey);
            }
            string size = string.IsNullOrEmpty(d.Size) ? "?" : char.ToUpperInvariant(d.Size[d.Size.Length - 1]).ToString() + d.Version.ToString(CultureInfo.InvariantCulture);
            return size + ", " + (string.IsNullOrEmpty(d.Type) ? string.Empty : d.Type.GetLocalization());
        }

        private void WriteBuilding(BuildModeSheet s, int row)
        {
            bool demolish = row == RowDemolish;
            if (_picked.Count == 0 || !_hasTarget)
            {
                s.Begin(T(demolish ? "reallife_bld_mode_tool_bulldoze" : "reallife_bld_mode_tool_refurbish"), null);
                s.Line(T(demolish ? "reallife_bld_hint_bulldoze_idle" : "reallife_bld_hint_refurbish_idle"), BuildModeBar.Soft);
                if (_picked.Count > 0)
                {
                    s.Line(T("reallife_bld_hint_building_not_yours"), BuildModeBar.Red);
                    s.Buttons(0, null, false, RowClear, T("reallife_bld_sheet_clear"), true);
                }
                return;
            }
            string block = demolish ? _d.DemolishBlock(_target.Street, _target.Number) : _d.RefitBlock(_target.Street, _target.Number);
            s.Begin(ConstructionGame.AddressText(_target.Street, _target.Number), null);
            if (block != null)
            {
                s.Line(T(block), BuildModeBar.Red);
            }
            string main = demolish ? T("reallife_bld_mode_row_demolish")
                : ConstructionText.Fill("reallife_bld_sheet_refit", "amount", ConstructionText.Money(_d.RefitPrice(_target.Street, _target.Number)));
            s.Buttons(row, main, block == null && !_state.ViewOnly, RowClear, T("reallife_bld_sheet_clear"), true);
        }

        private void WriteJoin(BuildModeSheet s)
        {
            s.Begin(T("reallife_bld_mode_tool_join"), Count());
            if (_picked.Count == 0)
            {
                s.Line(T("reallife_bld_hint_join_idle"), BuildModeBar.Soft);
                return;
            }
            if (_targetSite == null)
            {
                s.Line(T("reallife_bld_hint_zone_not_yours"), BuildModeBar.Red);
                s.Buttons(0, null, false, RowClear, T("reallife_bld_sheet_clear"), true);
                return;
            }
            string join = _d.JoinBlock(_targetSite, out _);
            string split = _d.SplitBlock(_targetSite);
            if (join != null)
            {
                s.Line(T(join), BuildModeBar.Amber);
            }
            if (split != null)
            {
                s.Line(T(split), BuildModeBar.Amber);
            }
            s.Buttons(RowJoin, T("reallife_bld_mode_row_join"), join == null && !_state.ViewOnly, 0, null, false);
            s.Buttons(0, null, false, RowSplit, T("reallife_bld_mode_row_split"), split == null && !_state.ViewOnly);
        }

        // The sites under way as the map's own markers (the picker's, its window hidden).
        private void WriteMarks()
        {
            _d.Marks(_marks);
            if (_map == null)
            {
                return;
            }
            _map.Begin(T("reallife_bld_mode_title"), ConstructionDirector.BuildingGlyph, new Color(1f, 0.6f, 0.15f));
            for (int i = 0; i < _marks.Count && i < MarksShown; i++)
            {
                BuildMark m = _marks[i];
                _map.Add(RowMark + i, m.Name, m.Detail, "reallife_bld_mode_button_look", true, true, new Vector3(m.X, 0f, m.Z));
            }
            if (_state.Open && _map.IsOpen)
            {
                _map.Open(Vector3.zero, MarkPicked, Closed);   // open already: the markers written again
            }
        }

        // ------------------------------------------------------------------ the design window

        // The designs that fit the footprint's ground looked at: each one's warnings, estimate and days; then the window opened on the first filter that shows any.
        private void OpenCatalogue()
        {
            if (_catalogue == null || !_catalogue.Alive)
            {
                _catalogue = BuildModePanel.Make(BuildModeGame.WindowCanvas(), _kit, FilterPicked, CardPicked, CatalogueBack, CataloguePlace);
                if (_catalogue == null)
                {
                    return;
                }
            }
            int n = Math.Min(_designs.Count, BuildModeCatalogue.Most);
            for (int i = 0; i < n; i++)
            {
                _dWarn[i] = _d.PlaceWarnings(_site.FirstColumn, _site.Count, _designs[i], true, _use, _parking, out _dCost[i], out _);
                _dFits[i] = BuildModeRules.MayBuild(_dWarn[i]);
                _dCorner[i] = !string.IsNullOrEmpty(_designs[i].Look);
                _dDays[i] = _d.PlaceDays(_designs[i], _use);
            }
            BuildModeCatalogue.Order(_dCost, _dFits, n, _dOrder);
            _filter = BuildModeCatalogue.First(_dCorner, _dFits, n);
            _card = _design >= 0 && _design < n ? _design : -1;
            BuildModeGame.HideHover();
            _catalogue.Show(true);
            WriteCatalogue(true);
        }

        private void WriteCatalogue(bool top)
        {
            if (_catalogue == null || !_catalogue.Shown)
            {
                return;
            }
            int n = Math.Min(_designs.Count, BuildModeCatalogue.Most);
            for (int f = 0; f < BuildModeCatalogue.FilterCount; f++)
            {
                _filterNames[f] = ConstructionText.Fill(BuildModeCatalogue.FilterKeys[f], "count", ConstructionText.N(BuildModeCatalogue.Count(f, _dCorner, _dFits, n)));
            }
            _d.LotOfColumn(_site.FirstColumn, out LotView first);
            bool corner = CellRules.Corner(_d.Cells, _site.FirstColumn, _site.Count, out _, out _);
            string street = string.IsNullOrEmpty(first.Street) ? _d.LotWords(first.Id) : first.Street;
            _catalogue.Begin(ConstructionText.Fill(corner ? "reallife_bld_cat_context_corner" : "reallife_bld_cat_context", "cells", ConstructionText.N(PickedCells), "street", street, "use",
                T(BuildCatalogue.UseKeys[(int)_use]), "storeys", ConstructionText.N(_d.UptownStoreys())), _filterNames, _filter);
            string use = T(BuildCatalogue.UseKeys[(int)_use]);
            Sprite picture = _kit.Icon(BuildModeCatalogue.Picture((int)_use), "Icon-Building");
            for (int k = 0; k < n; k++)
            {
                int i = _dOrder[k];
                if (!BuildModeCatalogue.Shows(_filter, _dCorner[i], _dFits[i]))
                {
                    continue;
                }
                DesignChoice d = _designs[i];
                string why = BuildModeCatalogue.Why(_dWarn[i]);
                string kind = why != null ? T(why) : ConstructionText.Fill(_dCorner[i] ? "reallife_bld_cat_kind_corner" : "reallife_bld_cat_kind_city", "use", use);
                string size = ConstructionText.Fill("reallife_bld_cat_size", "storeys", ConstructionText.N(PlansRules.Storeys(d.Height)), "area", ConstructionText.N(d.TotalSqm));
                _catalogue.Add(i, picture, SizeName(d), kind, size, ConstructionText.Money(_dCost[i]), why == null && !_state.ViewOnly, i == _card);
            }
            string summary = _card >= 0 && _card < n
                ? ConstructionText.Fill("reallife_bld_cat_summary", "name", SizeName(_designs[_card]), "cost", ConstructionText.Money(_dCost[_card]), "days", ConstructionText.N(_dDays[_card]))
                : T(BuildModeCatalogue.Count(_filter, _dCorner, _dFits, n) == 0 ? "reallife_bld_cat_none" : "reallife_bld_cat_pick");
            _catalogue.End(summary, _card >= 0 && _card < n && _dFits[_card] && !_state.ViewOnly, top);
        }

        private void FilterPicked(int filter)
        {
            if (filter >= 0 && filter < BuildModeCatalogue.FilterCount)
            {
                _filter = filter;
                WriteCatalogue(true);
            }
        }

        private void CardPicked(int index)
        {
            if (BuildModeCatalogue.Valid(index, _designs.Count) && _dFits[index])
            {
                _card = index;
                WriteCatalogue(false);
            }
        }

        // Back to the map: the window shut, the squares and any design placed before kept.
        private void CatalogueBack()
        {
            _catalogue?.Show(false);
            Write(false);
        }

        // The design picked put on the map: the window shut, its ghost on the footprint, Build here in the tool panel.
        private void CataloguePlace()
        {
            if (!BuildModeCatalogue.Valid(_card, _designs.Count) || !_dFits[_card])
            {
                return;
            }
            _catalogue?.Show(false);
            _design = _card;
            _state = BuildModeRules.Next(_state, BuildInput.Design, _state.Tool, false);
            Assess();
            StandGhost();
            Write(false);
            Colour();
        }

        /// <summary>True while the design window is up.</summary>
        public bool CatalogueShown => _catalogue != null && _catalogue.Shown;

        // ------------------------------------------------------------------ the toolbar, the views and the panel's buttons

        private void ToolPicked(int tool)
        {
            if (!_state.Open || tool < 0 || tool >= (int)BuildTool.Count)
            {
                return;
            }
            if (BuildModeHintRules.ToolWhy((BuildTool)tool, _holdings, _state.ViewOnly) != null)
            {
                return;   // greyed: its tooltip says why
            }
            _state = BuildModeRules.Next(_state, BuildInput.Tool, (BuildTool)tool, false);
            _view = BuildModeRules.ViewFor(_state.Tool);   // Buy land and Zone open on the zoning
            _zoneBrush = -1;
            _catalogue?.Show(false);
            ClearPick();
            ReadFacts();
            Write(true);
            Colour();
        }

        private void ViewPicked(int view)
        {
            if (!_state.Open || view < 0 || view >= (int)BuildView.Count)
            {
                return;
            }
            _view = (BuildView)view;
            if (_view == BuildView.Value && !_valuesRead)
            {
                ReadFacts();
            }
            Write(false);
            Colour();
        }

        // A marker on the map pressed: the map glides there.
        private void MarkPicked(int id)
        {
            int i = id - RowMark;
            if (i >= 0 && i < _marks.Count)
            {
                BuildModeGame.FlyTo(new Vector3(_marks[i].X, 0f, _marks[i].Z));
            }
        }

        private void Picked(int id)
        {
            if (!_state.Open)
            {
                return;
            }
            if (id >= RowMark)
            {
                MarkPicked(id);
                return;
            }
            Act(id);
        }

        // The warnings and estimate of the design picked on the footprint.
        private void Assess()
        {
            bool has = _design >= 0 && _design < _designs.Count;
            _warnings = _d.PlaceWarnings(_site.FirstColumn, _site.Count, has ? _designs[_design] : default(DesignChoice), has, _use, _parking, out _estimate, out _limit);
        }

        // After a release: a zone chosen goes on the land picked; squares of the player's own picked for a building open the design window.
        private void AfterPick()
        {
            if (_state.ViewOnly)
            {
                return;
            }
            if (_state.Tool == BuildTool.Zone && _zoneBrush >= 0 && _targetSite != null)
            {
                Zone(_targetSite, _zoneBrush);
            }
            else if (_state.Tool == BuildTool.Place && _siteWhy == null && _pickMine && _designs.Count > 0)
            {
                OpenCatalogue();
            }
        }

        // A zone put on a site of the player's: one its zoning allows at once; one City Hall must allow asked first in the game's question box, its fee named.
        private void Zone(string siteId, int brush)
        {
            LotFile site = _d.State.Lot(siteId);
            if (site == null)
            {
                return;
            }
            int use = brush >= (int)BuildUse.Count ? -1 : brush;
            if (use >= 0 && !LandRules.Zoned(_d.ZonesNow(site), (BuildUse)use))
            {
                Ask("reallife_bld_zone_confirm", "reallife_bld_zone_confirm_body", ConstructionDirector.ZoneFee, () => Refresh(_d.PaintSite(siteId, use)));
                return;
            }
            Refresh(_d.PaintSite(siteId, use));
        }

        private void Act(int id)
        {
            switch (id)
            {
                case RowClear:
                    Clear();
                    return;
                case RowChoose:
                    OpenCatalogue();
                    return;
                case RowOther:
                    DropGhost();
                    _design = -1;
                    _state = BuildModeRules.Next(_state, BuildInput.Clear, _state.Tool, false);
                    OpenCatalogue();
                    Colour();
                    return;
                case RowBuy:
                    if (_d.CellsBlock(_site, out long price, out _, out _, out _) == null)
                    {
                        Ask("reallife_bld_mode_confirm_buy", "reallife_bld_mode_confirm_buy_body", price, () =>
                        {
                            string answer = _d.BuyCells(_site);
                            if (answer == "reallife_bld_mode_bought")
                            {
                                // The next step: the land just bought stays picked, under Zone, where its zoning is shown and can be changed.
                                _state = BuildModeRules.Next(_state, BuildInput.Tool, BuildTool.Zone, false);
                                _state = BuildModeRules.Next(BuildModeRules.Next(_state, BuildInput.Press, BuildTool.Zone, true), BuildInput.Release, BuildTool.Zone, false);
                                _view = BuildModeRules.ViewFor(_state.Tool);
                                _zoneBrush = -1;
                            }
                            Refresh(answer);
                        });
                    }
                    return;
                case RowParking:
                    _parking = !_parking;
                    Assess();
                    StandGhost();
                    Refresh(null);
                    return;
                case RowBuild:
                    if (_design < 0 || _design >= _designs.Count)
                    {
                        return;
                    }
                    _state = BuildModeRules.Next(_state, BuildInput.Confirm, _state.Tool, false);
                    DesignChoice d = _designs[_design];
                    WriteHint();
                    Ask("reallife_bld_mode_confirm_build", "reallife_bld_mode_confirm_build_body", _estimate, () =>
                    {
                        int project = _d.PlaceProject(_site.FirstColumn, _site.Count, d, _use, _parking, out string why);
                        _state = BuildModeRules.Next(_state, BuildInput.Yes, _state.Tool, false);
                        _lastProject = project;
                        DropGhost();
                        ClearPick();
                        Refresh(project > 0 ? "reallife_bld_mode_built" : why);
                    }, () =>
                    {
                        _state = BuildModeRules.Next(_state, BuildInput.No, _state.Tool, false);
                        Write(false);
                    });
                    return;
                case RowDemolish:
                    if (_hasTarget)
                    {
                        LotView b = _target;
                        Ask("reallife_bld_mode_confirm_demolish", "reallife_bld_mode_confirm_demolish_body", 0L, () =>
                        {
                            int project = _d.NewDemolition(b.Street, b.Number, out string why);
                            Refresh(project > 0 ? "reallife_bld_mode_demolition_asked" : why);
                        });
                    }
                    return;
                case RowRefit:
                    if (_hasTarget)
                    {
                        LotView b = _target;
                        Ask("reallife_bld_mode_confirm_refit", "reallife_bld_mode_confirm_refit_body", _d.RefitPrice(b.Street, b.Number), () =>
                        {
                            int project = _d.ContractorRefit(b.Street, b.Number, false, out string why);
                            Refresh(project > 0 ? "reallife_bld_mode_refit_started" : why);
                        });
                    }
                    return;
                case RowJoin:
                    Refresh(_targetSite != null ? _d.Join(_targetSite) : null);
                    return;
                case RowSplit:
                    Refresh(_targetSite != null ? _d.Split(_targetSite) : null);
                    return;
                default:
                    if (id >= RowPaint && id <= RowPaintNone)
                    {
                        _zoneBrush = id - RowPaint;   // a use, or BrushNone
                        if (_targetSite != null)
                        {
                            Zone(_targetSite, _zoneBrush);   // land picked first: zoned at once
                        }
                        else
                        {
                            Write(false);
                        }
                    }
                    return;
            }
        }

        // The game's question box: the price named; yes runs the action.
        private static void Ask(string headerKey, string bodyKey, long amount, Action yes, Action no = null)
        {
            LanguageChangeEventDataHolder header = headerKey.Localize();
            LanguageChangeEventDataHolder body = bodyKey.Localize(new Dictionary<string, string> { { "amount", ConstructionText.Money(amount) } });
            GameConfirm.Show(header, body, () => UiClone.Safe(yes), no != null ? (Action)(() => UiClone.Safe(no)) : null, null, null, true, false);
        }

        // After an action: its answer in a notice, the facts read again, the picks looked at again, the interface and the layer written.
        private void Refresh(string key)
        {
            if (!string.IsNullOrEmpty(key))
            {
                Notifications.Show(NotificationType.Info, key, null, 5f, null, null, true, false);
            }
            if (!_state.Open)
            {
                return;
            }
            ReadFacts();
            if (_picked.Count > 0)
            {
                int design = _design;
                Pick();
                _design = design < _designs.Count ? design : -1;
                if (_design >= 0)
                {
                    Assess();
                    StandGhost();
                }
            }
            Write(false);
            Colour();
        }
    }
}
