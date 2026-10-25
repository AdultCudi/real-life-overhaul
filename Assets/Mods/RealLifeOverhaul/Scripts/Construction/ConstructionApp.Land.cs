using System;
using System.Collections.Generic;
using RealLifeOverhaul.GameUi;
using UnityEngine;

namespace RealLifeOverhaul.Construction
{
    /// <summary>The Land tab: the district's lots for sale, the player's own lots, and the player's buildings that can be rebuilt.</summary>
    public sealed partial class ConstructionApp
    {
        private readonly List<BuildingView> _owned = new List<BuildingView>(8);
        private readonly List<LandRun> _runs = new List<LandRun>(32);
        private int _runSpaces = 1;   // spaces of a run the player means to buy, as the Land tab's selector last stood

        partial void WriteLand(InfoBox box, BuildState state, ref string selected)
        {
            IReadOnlyList<LotView> lots = _director.Lots;
            string pickedLot = null;
            for (int i = 0; i < lots.Count; i++)
            {
                LotView lot = lots[i];
                LotFile owned = state.Lot(lot.Id);
                if (owned == null && (ConstructionDirector.OnSpaces(lot) || !lot.Open || ConstructionDirector.LotTaken(lot.Id) || _director.DeveloperOn(lot.Id)))
                {
                    continue;   // built on, the developers', or a free space of the grid (its run's row sells it): not land to buy here
                }
                if (owned != null && _director.Lot(lot.Id, out LotView site))
                {
                    lot = site;   // a site of several spaces as one lot
                }
                TableRow row = Pick((owned != null ? "own:" : "lot:") + lot.Id, ref selected);
                row.Kind = owned != null ? ConstructionTables.LandOwned : ConstructionTables.LandForSale;
                row.Set(0, ConstructionGame.AddressText(lot.Street, lot.Number));
                row.Set(1, ConstructionText.T("reallife_bld_value_size", "width", lot.Width.ToString(), "depth", lot.Depth.ToString()), lot.Width * lot.Depth);
                row.Set(2, ConstructionText.ZonesWords(owned != null ? _director.ZonesNow(owned) : _director.ClassZones(lot.Class)));
                long price = owned != null ? owned.Paid : _director.LotPrice(lot);
                row.Set(3, price > 0 ? ConstructionText.Money(price) : "-", price);
                row.Set(4, LotState(state, lot, owned), owned != null ? CellTone.Green : CellTone.Plain);
                if (row.Id == selected)
                {
                    pickedLot = lot.Id;
                }
            }
            // A block's free spaces in runs side by side: each run one row, bought a space or more at a time.
            _director.Runs(_runs);
            LandRun pickedRun = default(LandRun);
            bool run = false;
            for (int i = 0; i < _runs.Count; i++)
            {
                LandRun r = _runs[i];
                TableRow row = Pick("run:" + r.First.Id, ref selected);
                row.Kind = ConstructionTables.LandForSale;
                row.Set(0, ConstructionGame.AddressText(r.First.Street, r.First.Number));
                row.Set(1, ConstructionText.T("reallife_bld_value_spaces", "count", r.Spaces.ToString(), "width", r.Width.ToString(), "depth", r.First.Depth.ToString()), r.Width * r.First.Depth);
                row.Set(2, ConstructionText.ZonesWords(_director.ClassZones(r.First.Class)));
                long each = _director.SitePrice(r.First.Id, 1);
                row.Set(3, each > 0 ? ConstructionText.T("reallife_bld_value_a_space", "amount", ConstructionText.Money(each)) : "-", each);
                row.Set(4, ConstructionText.T(GoingPublic.InSession ? "reallife_bld_land_state_session" : "reallife_bld_land_state_sale"));
                if (row.Id == selected)
                {
                    pickedRun = r;
                    run = true;
                }
            }
            ConstructionGame.Owned(_owned);
            BuildingView pickedBuilding = default(BuildingView);
            bool building = false;
            for (int i = 0; i < _owned.Count; i++)
            {
                BuildingView b = _owned[i];
                if (BuildCatalogue.UseOf(b.Type) < 0)
                {
                    continue;   // a type the company does not build
                }
                TableRow row = Pick("bld:" + ConstructionGame.Place(b.Street, b.Number), ref selected);
                row.Kind = ConstructionTables.LandBuilding;
                row.Set(0, b.Name);
                row.Set(1, ConstructionText.T("reallife_bld_value_area", "area", ConstructionText.N(b.TotalSqm)), b.TotalSqm);
                row.Set(2, ConstructionText.T(BuildCatalogue.UseKeys[BuildCatalogue.UseOf(b.Type)]));
                row.Set(3, string.Empty);
                row.Set(4, RebuildState(state, b));
                if (row.Id == selected)
                {
                    pickedBuilding = b;
                    building = true;
                }
            }
            // The landmarks' sites: their lots held for the city's megaprojects, shown as one row each.
            IReadOnlyList<LotView> landmarks = _director.LandmarkSites;
            string pickedLandmark = null;
            for (int i = 0; i < landmarks.Count; i++)
            {
                LotView site = landmarks[i];
                TableRow row = Pick("lmk:" + site.Id, ref selected);
                row.Kind = ConstructionTables.LandLandmark;
                row.Set(0, ConstructionDirector.LandmarkName(site.Id) ?? ConstructionText.T("reallife_lmk_tender_unnamed"));
                row.Set(1, ConstructionText.T("reallife_bld_value_size", "width", site.Width.ToString(), "depth", site.Depth.ToString()), site.Width * site.Depth);
                row.Set(2, ConstructionText.T("reallife_lmk_land_zoned"));
                row.Set(3, "-", 0L);
                row.Set(4, ConstructionDirector.LandmarkWords(site.Id) ?? string.Empty);
                if (row.Id == selected)
                {
                    pickedLandmark = site.Id;
                }
            }
            bool business = WriteUnitRows(ref selected, out UnitView pickedUnit);
            if (pickedLot != null && _director.Lot(pickedLot, out LotView picked))
            {
                WriteLot(box, state, picked);
                return;
            }
            if (run)
            {
                WriteRun(box, pickedRun);
                return;
            }
            if (pickedLandmark != null && _director.Lot(pickedLandmark, out LotView landmark))
            {
                WriteLandmark(box, state, landmark);
                return;
            }
            if (building)
            {
                WriteBuilding(box, state, pickedBuilding);
                return;
            }
            if (business)
            {
                WriteUnit(box, pickedUnit);
                return;
            }
            box.Title(ConstructionText.T("reallife_bld_tab_land"));
            box.Note(ConstructionText.T(!ConstructionDirector.DistrictLives() ? "reallife_bld_land_no_district" : GoingPublic.InSession ? "reallife_bld_land_session" : "reallife_bld_land_pick"));
            Guide(box);
        }

        private string LotState(BuildState state, LotView lot, LotFile owned)
        {
            if (owned == null)
            {
                return ConstructionText.T(GoingPublic.InSession ? "reallife_bld_land_state_session" : "reallife_bld_land_state_sale");
            }
            ProjectFile p = state.OnLot(lot.Id);
            if (p != null)
            {
                return ConstructionText.StageWords(p);
            }
            return ConstructionText.T(owned.ZoneAsk != 0 ? "reallife_bld_land_state_zoning" : "reallife_bld_land_state_empty");
        }

        private static string RebuildState(BuildState state, BuildingView b)
        {
            for (int i = 0; i < state.Projects.Count; i++)
            {
                ProjectFile p = state.Projects[i];
                if (p.Live && p.Street == b.Street && p.Number == b.Number)
                {
                    return ConstructionText.StageWords(p);
                }
            }
            return ConstructionText.T("reallife_bld_land_state_yours");
        }

        // A run of a block's free spaces: what a space costs, and a button for each site it holds from its first space, one
        // space to all of them side by side, each building one that fills it.
        private void WriteRun(InfoBox box, LandRun run)
        {
            LotView first = run.First;
            box.Title(ConstructionGame.AddressText(first.Street, first.Number));
            box.Subtitle(ConstructionText.T("reallife_bld_land_run_sub", "count", run.Spaces.ToString()));
            box.RowPair("reallife_bld_row_spaces", ConstructionText.T("reallife_bld_value_spaces", "count", run.Spaces.ToString(), "width", run.Width.ToString(), "depth",
                first.Depth.ToString()), "reallife_bld_row_traffic", ConstructionText.N(first.Traffic));
            box.Row("reallife_bld_row_zoned", ConstructionText.ZonesWords(_director.ClassZones(first.Class)));
            box.Note(ConstructionText.T("reallife_bld_land_run_note"));
            // How many of its spaces side by side to buy, from its first: one site, one building that fills it.
            int spaces = Mathf.Clamp(_runSpaces, 1, run.Spaces);
            box.Selector(run.Spaces, spaces - 1, i => ConstructionText.T(i == 0 ? "reallife_bld_set_one_space" : "reallife_bld_set_spaces", "count", (i + 1).ToString()),
                i => UiClone.Safe(() =>
                {
                    _runSpaces = i + 1;
                    Refresh();
                }));
            string id = first.Id;
            long price = _director.SitePrice(id, spaces);
            box.Row("reallife_bld_row_price", price > 0 ? ConstructionText.Money(price) : ConstructionText.T("reallife_bld_value_not_known"));
            Transform buttons = box.ButtonRow();
            string block = _director.BuySiteBlock(id, spaces);
            if (block != null)
            {
                box.Line(ConstructionText.T(block), CellTone.Yellow);
            }
            else
            {
                InfoBox.Button(buttons, InfoBox.Positive, "reallife_bld_button_buy_site", () =>
                    Confirm("reallife_bld_confirm_site", "reallife_bld_confirm_site_body", _director.LotWords(id), price, () => _director.BuySite(id, spaces), false));
            }
            InfoBox.Button(buttons, InfoBox.Additional, "reallife_bld_back", Back);
            Guide(box);
        }

        private void WriteLot(InfoBox box, BuildState state, LotView lot)
        {
            LotFile owned = state.Lot(lot.Id);
            box.Title(ConstructionGame.AddressText(lot.Street, lot.Number));
            box.Subtitle(ConstructionText.T(BuildCatalogue.ClassKeys[BuildCatalogue.Clamp(lot.Class, 0, BuildCatalogue.ClassCount - 1)]));
            box.RowPair("reallife_bld_row_size", ConstructionText.T("reallife_bld_value_size", "width", lot.Width.ToString(), "depth", lot.Depth.ToString()),
                "reallife_bld_row_traffic", ConstructionText.N(lot.Traffic));
            if (ConstructionDirector.OnSpaces(lot))
            {
                box.Row("reallife_bld_row_spaces", ConstructionText.T("reallife_bld_value_spaces", "count", lot.Spaces.ToString(), "width", lot.Width.ToString(), "depth", lot.Depth.ToString()));
            }
            int zones = owned != null ? _director.ZonesNow(owned) : _director.ClassZones(lot.Class);
            box.Row("reallife_bld_row_zoned", ConstructionText.ZonesWords(zones));
            box.Row("reallife_bld_row_usual", ConstructionText.T("reallife_bld_value_area", "area", ConstructionText.N(LandRules.UsualArea(lot.Width, lot.Depth, lot.Class))));
            if (owned == null)
            {
                long price = _director.LotPrice(lot);
                box.Row("reallife_bld_row_price", price > 0 ? ConstructionText.Money(price) : ConstructionText.T("reallife_bld_value_not_known"));
                if (price > 0)
                {
                    box.Row("reallife_bld_row_land_tax", ConstructionText.T("reallife_bld_value_a_year", "amount",
                        ConstructionText.Money(LandRules.LandTax(price, ConstructionGame.DaysPerYear, ConstructionGame.DaysPerYear, false, ConstructionDirector.Code))));
                }
                string block = _director.BuyBlock(lot);
                if (block != null)
                {
                    box.Line(ConstructionText.T(block), CellTone.Yellow);
                }
                else
                {
                    string id = lot.Id;
                    InfoBox.Button(box.ButtonRow(), InfoBox.Positive, "reallife_bld_button_buy_lot", () =>
                        Confirm("reallife_bld_confirm_lot", "reallife_bld_confirm_lot_body", _director.LotWords(id), price, () => _director.BuyLot(id), false));
                }
                BackButton(box);
                return;
            }
            box.RowPair("reallife_bld_row_bought", ConstructionText.Day(owned.Bought), "reallife_bld_row_paid", ConstructionText.Money(owned.Paid));
            bool empty = LandRules.Empty(owned.EmptySince, ConstructionGame.Day);
            box.Row("reallife_bld_row_land_tax", ConstructionText.T("reallife_bld_value_a_year", "amount",
                ConstructionText.Money(LandRules.LandTax(owned.Paid, ConstructionGame.DaysPerYear, ConstructionGame.DaysPerYear, empty, ConstructionDirector.Code))),
                empty ? CellTone.Yellow : CellTone.Plain);
            if (empty)
            {
                box.Line(ConstructionText.T("reallife_bld_land_vacant"), CellTone.Yellow);
            }
            if (owned.ZoneAsk != 0)
            {
                box.Line(ConstructionText.T("reallife_bld_land_zone_heard", "use", ConstructionText.ZonesWords(owned.ZoneAsk), "day", ConstructionText.Day(owned.ZoneDay)), CellTone.Plain);
            }
            ProjectFile p = state.OnLot(lot.Id);
            Transform buttons = box.ButtonRow();
            if (p != null)
            {
                int project = p.Id;
                InfoBox.Button(buttons, InfoBox.Positive, "reallife_bld_button_to_project", () => Jump(ConstructionDirector.TabProjects, "prj:" + project));
            }
            else
            {
                string id = lot.Id;
                InfoBox.Button(buttons, InfoBox.Positive, "reallife_bld_button_plan", () =>
                {
                    int made = _director.NewPlot(id, out string why);
                    if (made > 0)
                    {
                        Jump(ConstructionDirector.TabProjects, "prj:" + made);
                    }
                    else
                    {
                        Answer(why);
                    }
                });
            }
            if (owned.ZoneAsk == 0 && zones != (1 << (int)BuildUse.Count) - 1)
            {
                InfoBox.Button(buttons, InfoBox.Negative, "reallife_bld_button_rezone", () => PlanningCall.Open(0));
            }
            InfoBox.Button(buttons, InfoBox.Additional, "reallife_bld_back", Back);
            Guide(box);
        }

        // A landmark's site: what it is, how far it has come, and its tender if one is open.
        private void WriteLandmark(InfoBox box, BuildState state, LotView site)
        {
            box.Title(ConstructionDirector.LandmarkName(site.Id) ?? ConstructionText.T("reallife_lmk_tender_unnamed"));
            box.Subtitle(ConstructionText.T("reallife_lmk_source"));
            box.RowPair("reallife_bld_row_size", ConstructionText.T("reallife_bld_value_size", "width", site.Width.ToString(), "depth", site.Depth.ToString()),
                "reallife_bld_row_traffic", ConstructionText.N(site.Traffic));
            box.Row("reallife_bld_col_state", ConstructionDirector.LandmarkWords(site.Id) ?? string.Empty);
            box.Note(ConstructionText.T("reallife_lmk_land_note"));
            Transform buttons = box.ButtonRow();
            for (int i = state.Tenders.Count - 1; i >= 0; i--)
            {
                TenderFile t = state.Tenders[i];
                if (t.Source == TenderSource.Landmark && t.Lot == site.Id && t.State == TenderState.Open)
                {
                    int tender = t.Id;
                    InfoBox.Button(buttons, InfoBox.Positive, "reallife_lmk_button_tender", () => Jump(ConstructionDirector.TabTenders, "tnd:" + tender));
                    break;
                }
            }
            InfoBox.Button(buttons, InfoBox.Additional, "reallife_bld_back", Back);
            Guide(box);
        }

        private void WriteBuilding(InfoBox box, BuildState state, BuildingView b)
        {
            box.Title(b.Name);
            box.Subtitle(ConstructionGame.AddressText(b.Street, b.Number));
            box.RowPair("reallife_bld_row_area", ConstructionText.T("reallife_bld_value_area", "area", ConstructionText.N(b.TotalSqm)), "reallife_bld_row_traffic", ConstructionText.N(b.Traffic));
            box.Row("reallife_bld_row_district", ConstructionGame.DistrictWords(b.District));
            box.Note(ConstructionText.T("reallife_bld_rebuild_note"));
            ProjectFile live = null;
            for (int i = 0; i < state.Projects.Count && live == null; i++)
            {
                ProjectFile p = state.Projects[i];
                live = p.Live && p.Street == b.Street && p.Number == b.Number ? p : null;
            }
            Transform buttons = box.ButtonRow();
            if (live != null)
            {
                int project = live.Id;
                InfoBox.Button(buttons, InfoBox.Positive, "reallife_bld_button_to_project", () => Jump(ConstructionDirector.TabProjects, "prj:" + project));
            }
            else
            {
                string street = b.Street;
                int number = b.Number;
                InfoBox.Button(buttons, InfoBox.Positive, "reallife_bld_button_rebuild", () =>
                {
                    int made = _director.NewRebuild(street, number, out string why);
                    if (made > 0)
                    {
                        Jump(ConstructionDirector.TabProjects, "prj:" + made);
                    }
                    else
                    {
                        Answer(why);
                    }
                });
            }
            InfoBox.Button(buttons, InfoBox.Additional, "reallife_bld_back", Back);
            if (live == null)
            {
                RedevelopButtons(box, b);   // a building of Uptown's: knocked down to its ground (ConstructionApp.Redevelop.cs)
            }
            if (live == null && ConstructionGame.Unit(b.Street, b.Number, out UnitView inside))
            {
                RefitButtons(box, inside);
            }
            Guide(box);
        }

        // Another tab with a row of it picked.
        private void Jump(int tab, string pick)
        {
            _tab = Mathf.Clamp(tab, 0, TabKeys.Length - 1);
            _pick = pick;
            _page = 0;
            Refresh();
        }
    }
}
