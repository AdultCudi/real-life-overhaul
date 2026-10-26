using System;
using System.Collections.Generic;
using RealLifeOverhaul.GameUi;
using UnityEngine;

namespace RealLifeOverhaul.Construction
{
    /// <summary>
    /// Renovations in the app: the player's units (businesses, homes and empty units) in the Land tab with a refit by the company
    /// or by the city's best renovation contractor at its quote, the furnishing kept or one of the game's own interior designs
    /// for the unit laid at the handover by the game's installers, a contractor's refit as a project, and a rebuild's choice to
    /// refit the business inside it too. Written on a page's opening, a pick or a press.
    /// </summary>
    public sealed partial class ConstructionApp
    {
        private readonly List<UnitView> _units = new List<UnitView>(8);
        private readonly List<string> _fitDesigns = new List<string>(16);
        private string _fitPlace;   // the unit the design selector last stood for
        private int _fitPick;       // 0 keeps the furniture, 1 and on the game's designs for it

        // The player's units in rented buildings, below their own buildings in the Land tab (an owned one is listed with those).
        private bool WriteUnitRows(ref string selected, out UnitView picked)
        {
            picked = default(UnitView);
            bool found = false;
            if (!_director.RefitsOn)
            {
                return false;
            }
            _director.RefitUnits(_units);
            for (int i = 0; i < _units.Count; i++)
            {
                UnitView u = _units[i];
                if (u.Owned)
                {
                    continue;   // its building's row is the player's own already
                }
                TableRow row = Pick("biz:" + ConstructionGame.Place(u.Street, u.Number), ref selected);
                row.Kind = ConstructionTables.LandBuilding;
                row.Set(0, u.Name);
                row.Set(1, ConstructionText.T("reallife_bld_value_area", "area", ConstructionText.N(u.Sqm)), u.Sqm);
                row.Set(2, ConstructionText.T(BuildCatalogue.UseKeys[(int)UnitUse(u)]));
                row.Set(3, string.Empty);
                row.Set(4, RefitState(u));
                if (row.Id == selected)
                {
                    picked = u;
                    found = true;
                }
            }
            return found;
        }

        private static BuildUse UnitUse(UnitView u)
        {
            int use = BuildCatalogue.UseOf(u.Type);
            return use >= 0 ? (BuildUse)use : BuildUse.Shops;
        }

        private string RefitState(UnitView u)
        {
            ProjectFile p = _director.State.AtBuilding(u.Street, u.Number);
            return p != null ? ConstructionText.StageWords(p) : ConstructionText.T("reallife_bld_land_state_rented");
        }

        private void WriteUnit(InfoBox box, UnitView u)
        {
            box.Title(u.Name);
            box.Subtitle(ConstructionGame.AddressText(u.Street, u.Number));
            string where = u.Street;
            int at = u.Number;
            MapRow(box, () => _director.ShowBuilding(where, at));
            box.Row("reallife_bld_row_area", ConstructionText.T("reallife_bld_value_area", "area", ConstructionText.N(u.Sqm)));
            box.Row("reallife_bld_row_district", ConstructionGame.DistrictWords(u.District));
            box.Note(ConstructionText.T(u.Trading ? "reallife_bld_refit_note_rented" : "reallife_bld_refit_note_home"));
            RefitButtons(box, u);
            BackButton(box);
        }

        /// <summary>The refit's two ways for a unit of the player's, its furnishing kept or one of the game's designs, or the project already there.</summary>
        private void RefitButtons(InfoBox box, UnitView u)
        {
            if (!_director.RefitsOn)
            {
                return;
            }
            box.Section("reallife_bld_sec_refit");
            ProjectFile live = _director.State.AtBuilding(u.Street, u.Number);
            if (live != null)
            {
                int project = live.Id;
                InfoBox.Button(box.ButtonRow(), InfoBox.Positive, "reallife_bld_button_to_project", () => Jump(ConstructionDirector.TabProjects, "prj:" + project));
                return;
            }
            string design = DesignSelector(box, u);
            RefitQuote q = _director.QuoteFor(u);
            box.Row("reallife_bld_row_refit_hours", ConstructionText.T("reallife_bld_value_refit_hours", "hours", q.Hours.ToString(), "days", TenderRules.StandardDays(q.Hours).ToString()));
            box.Row("reallife_bld_row_refit_quote", ConstructionText.T("reallife_bld_value_refit_quote", "contractor", ConstructionText.T(BuildCatalogue.RivalKeys[q.Rival]),
                "amount", ConstructionText.Money(q.Price), "days", q.Days.ToString()));
            FurnishRow(box, u, design);
            Transform buttons = box.ButtonRow();
            string street = u.Street;
            int number = u.Number;
            if (_director.State.Firm.Founded > 0)
            {
                InfoBox.Button(buttons, InfoBox.Positive, "reallife_bld_button_refit_own", () =>
                {
                    int made = _director.NewRefit(street, number, design, out string why);
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
            InfoBox.Button(buttons, InfoBox.Negative, "reallife_bld_button_refit_contractor", () =>
                Confirm("reallife_bld_confirm_refit", "reallife_bld_confirm_refit_app", u.Name, q.Price, () =>
                {
                    int made = _director.ContractorRefit(street, number, false, design, out string why);
                    return made > 0 ? "reallife_bld_answer_refit_ordered" : why;
                }, false));
            box.Note(ConstructionText.T(design != null ? "reallife_bld_refit_furnish_note" : "reallife_bld_refit_note"));
        }

        // The furnishing: the unit's furniture kept, or one of the game's designs for it, as a selector; the design picked, or null.
        private string DesignSelector(InfoBox box, UnitView u)
        {
            _director.DesignsFor(u, _fitDesigns);
            string place = ConstructionGame.Place(u.Street, u.Number);
            if (place != _fitPlace)
            {
                _fitPlace = place;
                _fitPick = 0;
            }
            if (_fitDesigns.Count == 0)
            {
                box.Line(ConstructionText.T("reallife_bld_refit_no_designs"), CellTone.Plain);
                return null;
            }
            int pick = Mathf.Clamp(_fitPick, 0, _fitDesigns.Count);
            box.Selector(_fitDesigns.Count + 1, pick, DesignWords, i => UiClone.Safe(() =>
            {
                _fitPick = i;
                Refresh();
            }));
            int at = RefitRules.DesignAt(pick, _fitDesigns.Count);
            return at >= 0 ? _fitDesigns[at] : null;
        }

        // A place of the furnishing's selector in words: the furniture kept, or a design of the game's by its own name.
        private string DesignWords(int i)
        {
            int at = RefitRules.DesignAt(i, _fitDesigns.Count);
            return at < 0 ? ConstructionText.T("reallife_bld_sel_keep_furniture") : ConstructionText.T("reallife_bld_sel_fit_design", "name", _fitDesigns[at]);
        }

        // What the game's installers ask for the design at the handover, as the game's own contract works it out.
        private void FurnishRow(InfoBox box, UnitView u, string design)
        {
            if (design == null)
            {
                return;
            }
            if (!_director.FurnishCost(u, design, out long price, out long fee, out long pieces, out long sold))
            {
                box.Line(ConstructionText.T("reallife_bld_refit_design_unread"), CellTone.Yellow);
                return;
            }
            box.Row("reallife_bld_row_furnish", ConstructionText.Fill("reallife_bld_value_furnish", "amount", ConstructionText.Money(price), "fee", ConstructionText.Money(fee),
                "pieces", ConstructionText.Money(pieces), "sold", ConstructionText.Money(sold)));
        }

        // A refit's furnishing on its project's page while it can still be changed: kept, or one of the game's designs.
        private void FitSelector(InfoBox box, ProjectFile p)
        {
            if (p.Kind != ProjectKind.Refit || !p.Live || !_director.RefitsOn || !ConstructionGame.Unit(p.Street, p.Number, out UnitView u))
            {
                return;
            }
            _director.DesignsFor(u, _fitDesigns);
            if (_fitDesigns.Count == 0)
            {
                return;
            }
            int now = string.IsNullOrEmpty(p.FitDesign) ? 0 : _fitDesigns.IndexOf(p.FitDesign) + 1;
            box.Selector(_fitDesigns.Count + 1, Mathf.Max(0, now), DesignWords, i =>
            {
                int at = RefitRules.DesignAt(i, _fitDesigns.Count);
                Answer(_director.SetFitDesign(p, at >= 0 ? _fitDesigns[at] : null));
            });
            FurnishRow(box, u, string.IsNullOrEmpty(p.FitDesign) ? null : p.FitDesign);
        }

        // A refit the city's contractor does: what was paid, how far it is, when it is due.
        private void WriteContracted(InfoBox box, ProjectFile p)
        {
            box.Section("reallife_bld_sec_refit");
            box.Row("reallife_bld_row_contractor", ConstructionText.T(BuildCatalogue.RivalKeys[BuildCatalogue.Clamp(p.Contractor, 0, BuildCatalogue.RivalCount - 1)]));
            box.RowPair("reallife_bld_row_paid", ConstructionText.Money(p.ContractorPrice), "reallife_bld_row_due", ConstructionText.Day(p.DueDay));
            if (p.Stage == ProjectStage.Building)
            {
                double progress = Progress(p);
                box.Meter("reallife_bld_row_progress", (float)progress, Mathf.RoundToInt((float)progress * 100f) + "%", CellTone.Plain);
                box.Note(ConstructionText.T("reallife_bld_refit_closed_note"));
                FitSelector(box, p);
            }
            else if (p.Stage == ProjectStage.Finished)
            {
                box.RowPair("reallife_bld_row_handed", ConstructionText.Day(p.Handed), "reallife_bld_row_refit_grade", ConstructionText.T(RefitGradeKey(p.Grade)));
                if (!string.IsNullOrEmpty(p.FitDesign))
                {
                    box.Row("reallife_bld_row_design", p.FitDesign);
                }
            }
            BackButton(box);
        }

        private static string RefitGradeKey(int grade)
        {
            return grade >= 2 ? "reallife_bld_refit_grade_fine" : grade == 1 ? "reallife_bld_refit_grade_good" : "reallife_bld_refit_grade_plain";
        }

        // A rebuild of a building with a business of the player's in it may refit that business too.
        private void WithRefitSelector(InfoBox box, ProjectFile p)
        {
            if (p.Kind != ProjectKind.Rebuild || !_director.RefitsOn || !ConstructionGame.Business(p.Street, p.Number, out _))
            {
                return;
            }
            box.Selector(2, p.WithRefit ? 1 : 0, i => ConstructionText.T(i == 1 ? "reallife_bld_sel_with_refit" : "reallife_bld_sel_outside_only"),
                i => Answer(_director.SetWithRefit(p, i == 1)));
        }
    }
}
