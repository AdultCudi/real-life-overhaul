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
    /// What build mode's window says (BuildModePanel, a copy of the game's help screen in the middle): the tool and the view in
    /// the list, the tool's page (a short help, what is picked as a card of figures with its actions, the zones as buttons in
    /// their colours, the designs with their size and cost), the district's demand as bars, the view's legend and the sites under
    /// way, each with its marker on the map. Written again only after a pick or an action. Each action is the construction game's
    /// own call, a cost asked first in the game's question box.
    /// </summary>
    public sealed partial class BuildMode
    {
        // Row ids.
        private const int RowBuy = 1, RowClear = 2, RowFold = 3, RowPaint = 10, RowPaintNone = 14, RowParking = 20, RowBuild = 21, RowDemolish = 30, RowJoin = 40, RowSplit = 41, RowRefit = 50;
        private const int RowDesign = 200, RowMark = 300;

        private const string Red = "<color=#FF6B6B>";
        private const string Shut = "</color>";

        private static readonly string[] ToolKeys =
        {
            "reallife_bld_mode_tool_land", "reallife_bld_mode_tool_zone", "reallife_bld_mode_tool_place", "reallife_bld_mode_tool_bulldoze", "reallife_bld_mode_tool_join",
                "reallife_bld_mode_tool_refurbish"
        };

        private static readonly string[] ToolHelp =
        {
            "reallife_bld_mode_help_land", "reallife_bld_mode_help_zone", "reallife_bld_mode_help_place", "reallife_bld_mode_help_bulldoze", "reallife_bld_mode_help_join",
                "reallife_bld_mode_help_refurbish"
        };

        // Each tool's square in the header: the game's own colours where it has one for the thing (for sale, the player's, a use).
        private static readonly int[] ToolSquare =
        {
            BuildModeRules.ForSale, BuildModeRules.LineZoned, BuildModeRules.Offices, BuildModeRules.LineDemolished, BuildModeRules.LineYours, BuildModeRules.UnderWayDone
        };

        private readonly int[] _keyFills = new int[BuildModeRules.LegendMost];
        private readonly int[] _keyLines = new int[BuildModeRules.LegendMost];
        private readonly string[] _keyWords = new string[BuildModeRules.LegendMost];

        /// <summary>A tool's name key.</summary>
        internal static string ToolKey(BuildTool tool) => ToolKeys[(int)tool];

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

        private void Write(bool top)
        {
            if (_panel == null || !_panel.Alive)
            {
                return;
            }
            _panel.Begin(_state.Tool, _view, ConstructionDirector.BuildingGlyph, Colour(ToolSquare[(int)_state.Tool]), T(BuildModeRules.ViewKeys[(int)_view]), T(ToolKeys[(int)_state.Tool]),
                _picked.Count > 0 ? Summary() : null);
            _panel.Paragraph(_state.ViewOnly ? T("reallife_bld_mode_view_only") : T(ToolHelp[(int)_state.Tool]));
            _panel.Button(RowFold, T("reallife_bld_mode_row_pick"), T("reallife_bld_mode_row_pick_note"), T("reallife_bld_mode_button_pick_map"), true);
            switch (_state.Tool)
            {
                case BuildTool.Land: WriteLand(); break;
                case BuildTool.Zone: WriteZone(); break;
                case BuildTool.Place: WritePlace(); break;
                case BuildTool.Bulldoze: WriteBuilding(RowDemolish, "reallife_bld_mode_row_demolish"); break;
                case BuildTool.Join: WriteJoin(); break;
                default: WriteBuilding(RowRefit, "reallife_bld_mode_row_refit"); break;
            }
            if (_picked.Count > 0)
            {
                _panel.Button(RowClear, T("reallife_bld_mode_row_clear"), T("reallife_bld_mode_row_clear_note"), T("reallife_bld_mode_button_clear"), true);
            }
            WriteDemand();
            WriteLegend();
            WriteMarks();
            for (int i = 0; i < _marks.Count && i < MarksShown; i++)
            {
                if (i == 0)
                {
                    _panel.Heading(T("reallife_bld_mode_heading_underway"));
                }
                _panel.Button(RowMark + i, _marks[i].Name, _marks[i].Detail, T("reallife_bld_mode_button_look"), true);
            }
            _panel.End(top);
            _panel.Status(_hoverWords ?? Summary());
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

        // The district's demand for each use as the game's bars in the use's colour (Cities Skylines' demand), from the district's own count.
        private void WriteDemand()
        {
            bool any = false;
            for (int u = 0; u < (int)BuildUse.Count; u++)
            {
                int demand = _d.DemandFor((BuildUse)u);
                if (demand < 0)
                {
                    continue;
                }
                if (!any)
                {
                    _panel.Heading(T("reallife_bld_mode_demand"));
                    any = true;
                }
                _panel.Demand(u, T(BuildCatalogue.UseKeys[u]), demand, Colour(BuildModeRules.Homes + u));
            }
        }

        private void WriteLegend()
        {
            int n = BuildModeRules.Legend(_view, _keyFills, _keyLines, _keyWords);
            _panel.Heading(T("reallife_bld_mode_heading_legend"));
            for (int i = 0; i < n; i++)
            {
                _panel.Key(_keyFills[i] >= 0 ? Colour(_keyFills[i]) : Color.clear, Colour(_keyLines[i]), _keyFills[i] < 0, T(_keyWords[i]));
            }
        }

        private void WriteLand()
        {
            if (_picked.Count == 0)
            {
                return;
            }
            _panel.Heading(T("reallife_bld_mode_heading_picked"));
            if (_siteWhy != null)
            {
                _panel.Paragraph(Red + T(_siteWhy) + Shut);
                return;
            }
            string block = _d.CellsBlock(_site, out long price, out _, out _, out LotFile mine);
            _d.LotOfColumn(_site.FirstColumn, out LotView first);
            int zones = mine != null ? _d.ZonesNow(mine) : LandRules.ZonesOf(_site.Count >= 3 ? 3 : _site.Count == 2 ? 1 : 0);   // a site's class by its spaces, as Joined gives it
            long tax = price > 0L ? LandRules.LandTax(price, ConstructionGame.DaysPerYear, ConstructionGame.DaysPerYear, false, ConstructionDirector.Code) : 0L;   // a year's, as the Land tab says it
            string card = ConstructionText.Fill("reallife_bld_mode_land_card", "cells", ConstructionText.N(_site.Cells), "spaces", ConstructionText.N(_site.Count), "name", _d.LotWords(first.Id),
                "uses", ConstructionDirector.UsesWords(zones), "storeys", ConstructionText.N(_d.UptownStoreys()), "price", price > 0L ? ConstructionText.Money(price) : "-",
                "tax", ConstructionText.Money(tax));
            _panel.Paragraph(card);
            if (block != null)
            {
                _panel.Paragraph(Red + T(block) + Shut);
            }
            _panel.Button(RowBuy, T("reallife_bld_mode_row_buy"), price > 0L ? ConstructionText.Money(price) : null, T("reallife_bld_mode_button_buy"), block == null && !_state.ViewOnly);
        }

        private void WriteZone()
        {
            if (_picked.Count == 0)
            {
                return;
            }
            LotFile site = _targetSite != null ? _d.State.Lot(_targetSite) : null;
            if (site == null)
            {
                _panel.Paragraph(Red + T("reallife_bld_mode_zone_not_yours") + Shut);
                return;
            }
            int zones = _d.ZonesNow(site);
            _panel.Heading(T("reallife_bld_mode_heading_zone"));
            _panel.Paragraph(T("reallife_bld_mode_land_zoned", "uses", ConstructionDirector.UsesWords(zones)));
            for (int u = 0; u < (int)BuildUse.Count; u++)
            {
                bool allowed = LandRules.Zoned(zones, (BuildUse)u);
                string detail = allowed ? T("reallife_bld_mode_zone_allowed") : T("reallife_bld_mode_zone_ask", "fee", ConstructionText.Money(ConstructionDirector.ZoneFee));
                _panel.Button(RowPaint + u, T(BuildCatalogue.UseKeys[u]), detail, T("reallife_bld_mode_button_paint"), !_state.ViewOnly && site.Paint != u + 1, Colour(BuildModeRules.Homes + u));
            }
            _panel.Button(RowPaintNone, T("reallife_bld_mode_zone_none"), null, T("reallife_bld_mode_button_paint"), !_state.ViewOnly && site.Paint != 0, Colour(BuildModeRules.NoZone));
        }

        private void WritePlace()
        {
            if (_picked.Count == 0)
            {
                return;
            }
            if (_siteWhy != null)
            {
                _panel.Paragraph(Red + T(_siteWhy) + Shut);
                return;
            }
            _panel.Heading(T("reallife_bld_mode_heading_designs"));
            _panel.Paragraph(ConstructionText.T("reallife_bld_mode_place_footprint", "wide", ConstructionText.N(_site.Count), "deep", ConstructionText.N(_site.MinRows), "use",
                T(BuildCatalogue.UseKeys[(int)_use])));
            if (_designs.Count == 0)
            {
                _panel.Paragraph(Red + T("reallife_bld_mode_place_none") + Shut);
            }
            for (int i = 0; i < _designs.Count; i++)
            {
                DesignChoice d = _designs[i];
                string detail = ConstructionText.T("reallife_bld_mode_place_design", "storeys", ConstructionText.N(PlansRules.Storeys(d.Height)), "area", ConstructionText.N(d.TotalSqm));
                _panel.Button(RowDesign + i, SizeName(d), detail, T(i == _design ? "reallife_bld_mode_button_picked" : "reallife_bld_mode_button_pick"), i != _design);
            }
            if (_design < 0)
            {
                return;
            }
            _panel.Paragraph(T("reallife_bld_mode_place_estimate", "amount", ConstructionText.Money(_estimate)) + "\n" + T("reallife_bld_mode_land_limit", "storeys", ConstructionText.N(_limit)));
            for (int b = 0; b < BuildModeRules.WarnCount; b++)
            {
                if ((_warnings & (1 << b)) != 0)
                {
                    _panel.Paragraph(Red + T(BuildModeRules.WarnKeys[b]) + Shut);
                }
            }
            _panel.Switch(RowParking, T("reallife_bld_mode_row_parking"), null, _parking, !_state.ViewOnly);
            _panel.Button(RowBuild, T("reallife_bld_mode_row_build"), ConstructionText.Money(_estimate), T("reallife_bld_mode_button_build"), BuildModeRules.MayBuild(_warnings) && !_state.ViewOnly);
        }

        // A design as the window names it: its size and version as the game's blueprints do ("C1"), and its type; a corner building by its name.
        private static string SizeName(DesignChoice d)
        {
            if (!string.IsNullOrEmpty(d.Look))
            {
                return (string.IsNullOrEmpty(d.LookKey) ? d.Look : T(d.LookKey)) + ", " + (string.IsNullOrEmpty(d.Type) ? string.Empty : d.Type.GetLocalization());
            }
            string size = string.IsNullOrEmpty(d.Size) ? "?" : char.ToUpperInvariant(d.Size[d.Size.Length - 1]).ToString() + d.Version.ToString(CultureInfo.InvariantCulture);
            return size + ", " + (string.IsNullOrEmpty(d.Type) ? string.Empty : d.Type.GetLocalization());
        }

        private void WriteBuilding(int row, string key)
        {
            if (_picked.Count == 0)
            {
                return;
            }
            if (!_hasTarget)
            {
                _panel.Paragraph(Red + T("reallife_bld_mode_building_not_yours") + Shut);
                return;
            }
            string block = row == RowDemolish ? _d.DemolishBlock(_target.Street, _target.Number) : _d.RefitBlock(_target.Street, _target.Number);
            _panel.Heading(ConstructionGame.AddressText(_target.Street, _target.Number));
            if (block != null)
            {
                _panel.Paragraph(Red + T(block) + Shut);
            }
            string detail = row == RowRefit ? ConstructionText.Money(_d.RefitPrice(_target.Street, _target.Number)) : null;
            _panel.Button(row, T(key), detail, T("reallife_bld_mode_button_go"), block == null && !_state.ViewOnly);
        }

        private void WriteJoin()
        {
            if (_picked.Count == 0)
            {
                return;
            }
            if (_targetSite == null)
            {
                _panel.Paragraph(Red + T("reallife_bld_mode_zone_not_yours") + Shut);
                return;
            }
            string join = _d.JoinBlock(_targetSite, out _);
            string split = _d.SplitBlock(_targetSite);
            _panel.Button(RowJoin, T("reallife_bld_mode_row_join"), join != null ? T(join) : null, T("reallife_bld_mode_button_go"), join == null && !_state.ViewOnly);
            _panel.Button(RowSplit, T("reallife_bld_mode_row_split"), split != null ? T(split) : null, T("reallife_bld_mode_button_go"), split == null && !_state.ViewOnly);
        }

        // ------------------------------------------------------------------ the list and the rows

        private void ToolPicked(int tool)
        {
            if (!_state.Open || tool < 0 || tool >= (int)BuildTool.Count)
            {
                return;
            }
            _state = BuildModeRules.Next(_state, BuildInput.Tool, (BuildTool)tool, false);
            _view = BuildModeRules.ViewFor(_state.Tool);   // Buy land and Zone open on the zoning
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

        // The arrow in the window's top bar: aside to pick on the map, or back to the middle.
        private void FoldPressed()
        {
            if (_panel != null)
            {
                _panel.Fold(!_panel.Folded);
            }
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
                _panel.Fold(true);   // aside, so the place is seen
                return;
            }
            if (id >= RowDesign)
            {
                _design = id - RowDesign;
                _state = BuildModeRules.Next(_state, BuildInput.Design, _state.Tool, false);
                Assess();
                StandGhost();
                Write(false);
                Colour();
                _panel.Fold(true);   // aside, so the ghost is seen; the arrow brings Build here back
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

        private void Act(int id)
        {
            switch (id)
            {
                case RowFold:
                    _panel.Fold(true);
                    return;
                case RowClear:
                    Clear();
                    return;
                case RowBuy:
                    if (_d.CellsBlock(_site, out long price, out _, out _, out _) == null)
                    {
                        Ask("reallife_bld_mode_confirm_buy", "reallife_bld_mode_confirm_buy_body", price, () => Refresh(_d.BuyCells(_site)));
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
                    if (id >= RowPaint && id <= RowPaintNone && _targetSite != null)
                    {
                        Refresh(_d.PaintSite(_targetSite, id == RowPaintNone ? -1 : id - RowPaint));
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

        // After an action: its answer in a notice, the facts read again, the picks looked at again, the window and the layer written.
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
