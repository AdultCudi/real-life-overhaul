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
    /// Build mode's window (a copy of the game's subway station window on the map, MapPicker): its headline the tool, then the
    /// district's demand, the tools, what is picked with its figures and the actions, and the sites under way with their markers.
    /// Written again only after a pick or an action. Each action is the construction game's own call, a cost asked first in the
    /// game's question box.
    /// </summary>
    public sealed partial class BuildMode
    {
        // Row ids.
        private const int RowBuy = 1, RowClear = 2, RowPaint = 10, RowPaintNone = 14, RowParking = 20, RowBuild = 21, RowDemolish = 30, RowJoin = 40, RowSplit = 41, RowRefit = 50;
        private const int RowTool = 100, RowDesign = 200, RowMark = 300;

        private const string Red = "<color=#FF6B6B>";
        private const string Shut = "</color>";

        private static readonly string[] ToolKeys =
        {
            "reallife_bld_mode_tool_land", "reallife_bld_mode_tool_zone", "reallife_bld_mode_tool_place", "reallife_bld_mode_tool_bulldoze", "reallife_bld_mode_tool_join", "reallife_bld_mode_tool_refurbish"
        };

        private static readonly string[] ToolHints =
        {
            "reallife_bld_mode_hint_land", "reallife_bld_mode_hint_zone", "reallife_bld_mode_hint_place", "reallife_bld_mode_hint_bulldoze", "reallife_bld_mode_hint_join", "reallife_bld_mode_hint_refurbish"
        };

        private static string T(string key) => ConstructionText.T(key);

        private static string T(string key, string name, string value) => ConstructionText.T(key, name, value);

        private void Write()
        {
            if (_window == null)
            {
                return;
            }
            Sprite icon = GameSprites.Get("Icon-Building");
            _window.Begin(T("reallife_bld_mode_title") + ": " + T(ToolKeys[(int)_state.Tool]), icon, new Color(1f, 0.6f, 0.15f));
            _window.Note(_state.ViewOnly ? T("reallife_bld_mode_view_only") : T(ToolHints[(int)_state.Tool]));
            WriteDemand();
            for (int t = 0; t < (int)BuildTool.Count; t++)
            {
                _window.Add(RowTool + t, T(ToolKeys[t]), null, "reallife_bld_mode_button_use", t != (int)_state.Tool, false, Vector3.zero);
            }
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
                _window.Add(RowClear, T("reallife_bld_mode_row_clear"), null, "reallife_bld_mode_button_clear", true, false, Vector3.zero);
            }
            _d.Marks(_marks);
            for (int i = 0; i < _marks.Count && i < MarksShown; i++)
            {
                BuildMark m = _marks[i];
                _window.Add(RowMark + i, m.Name, m.Detail, "reallife_bld_mode_button_look", true, true, new Vector3(m.X, 0f, m.Z));
            }
            if (_state.Open)
            {
                _window.Open(Vector3.zero, Picked, Closed);   // open already: the rows written again
            }
        }

        // The district's demand for each use as the game's bars (Cities Skylines' demand), from the district's own count.
        private void WriteDemand()
        {
            var lines = new List<string>(4);
            for (int u = 0; u < (int)BuildUse.Count; u++)
            {
                int demand = _d.DemandFor((BuildUse)u);
                if (demand < 0)
                {
                    continue;
                }
                lines.Add(T(BuildCatalogue.UseKeys[u]) + "  " + DemandBar(demand) + "  " + demand.ToString(CultureInfo.InvariantCulture));
            }
            if (lines.Count > 0)
            {
                _window.Note(T("reallife_bld_mode_demand") + "\n" + string.Join("\n", lines.ToArray()));
            }
        }

        // Twenty strokes in the game's lettering, those up to the demand in its green.
        private static string DemandBar(int demand)
        {
            int filled = Mathf.Clamp((demand + 2) / 5, 0, 20);
            return "<color=#5FD35F>" + new string('|', filled) + "</color><color=#5A5E64>" + new string('|', 20 - filled) + "</color>";
        }

        private void WriteLand()
        {
            if (_picked.Count == 0)
            {
                return;
            }
            if (_siteWhy != null)
            {
                _window.Note(Red + T(_siteWhy) + Shut);
                return;
            }
            string block = _d.CellsBlock(_site, out long price, out _, out _, out LotFile mine);
            _d.LotOfColumn(_site.FirstColumn, out LotView first);
            _window.Note(ConstructionText.T("reallife_bld_mode_land_picked", "cells", ConstructionText.N(_site.Cells), "spaces", ConstructionText.N(_site.Count), "name", _d.LotWords(first.Id)));
            if (price > 0L)
            {
                long tax = LandRules.LandTax(price, ConstructionGame.DaysPerYear, ConstructionGame.DaysPerYear, false, ConstructionDirector.Code);   // a year's, as the Land tab says it
                _window.Note(ConstructionText.T("reallife_bld_mode_land_price", "price", ConstructionText.Money(price), "tax", ConstructionText.Money(tax)));
            }
            int zones = mine != null ? _d.ZonesNow(mine) : LandRules.ZonesOf(_site.Count >= 3 ? 3 : _site.Count == 2 ? 1 : 0);   // a site's class by its spaces, as Joined gives it
            _window.Note(T("reallife_bld_mode_land_zoned", "uses", ZonesWords(zones)) + "\n" + T("reallife_bld_mode_land_limit", "storeys", ConstructionText.N(_d.UptownStoreys())));
            if (block != null)
            {
                _window.Note(Red + T(block) + Shut);
            }
            _window.Add(RowBuy, T("reallife_bld_mode_row_buy"), price > 0L ? ConstructionText.Money(price) : null, "reallife_bld_mode_button_buy", block == null && !_state.ViewOnly, false, Vector3.zero);
        }

        private static string ZonesWords(int zones)
        {
            var words = new List<string>(4);
            for (int u = 0; u < (int)BuildUse.Count; u++)
            {
                if (LandRules.Zoned(zones, (BuildUse)u))
                {
                    words.Add(T(BuildCatalogue.UseKeys[u]));
                }
            }
            return words.Count == 0 ? "-" : string.Join(", ", words.ToArray());
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
                _window.Note(Red + T("reallife_bld_mode_zone_not_yours") + Shut);
                return;
            }
            int zones = _d.ZonesNow(site);
            _window.Note(T("reallife_bld_mode_land_zoned", "uses", ZonesWords(zones)));
            for (int u = 0; u < (int)BuildUse.Count; u++)
            {
                bool allowed = LandRules.Zoned(zones, (BuildUse)u);
                string detail = allowed ? T("reallife_bld_mode_zone_allowed") : T("reallife_bld_mode_zone_ask", "fee", ConstructionText.Money(ConstructionDirector.ZoneFee));
                _window.Add(RowPaint + u, T(BuildCatalogue.UseKeys[u]), detail, "reallife_bld_mode_button_paint", !_state.ViewOnly && site.Paint != u + 1, false, Vector3.zero);
            }
            _window.Add(RowPaintNone, T("reallife_bld_mode_zone_none"), null, "reallife_bld_mode_button_paint", !_state.ViewOnly && site.Paint != 0, false, Vector3.zero);
        }

        private void WritePlace()
        {
            if (_picked.Count == 0)
            {
                return;
            }
            if (_siteWhy != null)
            {
                _window.Note(Red + T(_siteWhy) + Shut);
                return;
            }
            _window.Note(ConstructionText.T("reallife_bld_mode_place_footprint", "wide", ConstructionText.N(_site.Count), "deep", ConstructionText.N(_site.MinRows), "use", T(BuildCatalogue.UseKeys[(int)_use])));
            if (_designs.Count == 0)
            {
                _window.Note(Red + T("reallife_bld_mode_place_none") + Shut);
            }
            for (int i = 0; i < _designs.Count; i++)
            {
                DesignChoice d = _designs[i];
                string detail = ConstructionText.T("reallife_bld_mode_place_design", "storeys", ConstructionText.N(PlansRules.Storeys(d.Height)), "area", ConstructionText.N(d.TotalSqm));
                _window.Add(RowDesign + i, SizeName(d), detail, "reallife_bld_mode_button_pick", i != _design, false, Vector3.zero);
            }
            if (_design < 0)
            {
                return;
            }
            _window.Note(T("reallife_bld_mode_place_estimate", "amount", ConstructionText.Money(_estimate)) + "\n" + T("reallife_bld_mode_land_limit", "storeys", ConstructionText.N(_limit)));
            for (int b = 0; b < BuildModeRules.WarnCount; b++)
            {
                if ((_warnings & (1 << b)) != 0)
                {
                    _window.Note(Red + T(BuildModeRules.WarnKeys[b]) + Shut);
                }
            }
            _window.Add(RowParking, T("reallife_bld_mode_row_parking"), T(_parking ? "reallife_bld_mode_yes" : "reallife_bld_mode_no"), "reallife_bld_mode_button_change", !_state.ViewOnly, false, Vector3.zero);
            _window.Add(RowBuild, T("reallife_bld_mode_row_build"), null, "reallife_bld_mode_button_build", BuildModeRules.MayBuild(_warnings) && !_state.ViewOnly, false, Vector3.zero);
        }

        // A design as the window names it: its size and version as the game's blueprints do ("C1"), and its type.
        private static string SizeName(DesignChoice d)
        {
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
                _window.Note(Red + T("reallife_bld_mode_building_not_yours") + Shut);
                return;
            }
            string block = row == RowDemolish ? _d.DemolishBlock(_target.Street, _target.Number) : _d.RefitBlock(_target.Street, _target.Number);
            _window.Note(ConstructionGame.AddressText(_target.Street, _target.Number));
            if (block != null)
            {
                _window.Note(Red + T(block) + Shut);
            }
            string detail = row == RowRefit ? ConstructionText.Money(_d.RefitPrice(_target.Street, _target.Number)) : null;
            _window.Add(row, T(key), detail, "reallife_bld_mode_button_go", block == null && !_state.ViewOnly, false, Vector3.zero);
        }

        private void WriteJoin()
        {
            if (_picked.Count == 0)
            {
                return;
            }
            if (_targetSite == null)
            {
                _window.Note(Red + T("reallife_bld_mode_zone_not_yours") + Shut);
                return;
            }
            string join = _d.JoinBlock(_targetSite, out _);
            string split = _d.SplitBlock(_targetSite);
            _window.Add(RowJoin, T("reallife_bld_mode_row_join"), join != null ? T(join) : null, "reallife_bld_mode_button_go", join == null && !_state.ViewOnly, false, Vector3.zero);
            _window.Add(RowSplit, T("reallife_bld_mode_row_split"), split != null ? T(split) : null, "reallife_bld_mode_button_go", split == null && !_state.ViewOnly, false, Vector3.zero);
        }

        // ------------------------------------------------------------------ a row pressed

        private void Picked(int id)
        {
            if (!_state.Open)
            {
                return;
            }
            if (id >= RowTool && id < RowTool + (int)BuildTool.Count)
            {
                _state = BuildModeRules.Next(_state, BuildInput.Tool, (BuildTool)(id - RowTool), false);
                ClearPick();
            }
            else if (id >= RowMark)
            {
                int i = id - RowMark;
                if (i < _marks.Count)
                {
                    BuildModeGame.FlyTo(new Vector3(_marks[i].X, 0f, _marks[i].Z));
                }
                return;
            }
            else if (id >= RowDesign)
            {
                _design = id - RowDesign;
                _state = BuildModeRules.Next(_state, BuildInput.Design, _state.Tool, false);
                Assess();
                StandGhost();
            }
            else
            {
                Act(id);
                return;
            }
            Write();
            Colour();
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
                case RowClear:
                    _state = BuildModeRules.Next(_state, BuildInput.Clear, _state.Tool, false);
                    if (_state.Step != BuildStep.Ghost)
                    {
                        ClearPick();
                    }
                    else
                    {
                        _design = -1;
                        DropGhost();
                    }
                    Refresh(null);
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
            Write();
            Colour();
        }
    }
}
