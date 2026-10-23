using System;
using System.Collections.Generic;
using RealLifeOverhaul.GameUi;
using UnityEngine;

namespace RealLifeOverhaul.Construction
{
    /// <summary>
    /// Renovations in the app: the player's businesses in the Land tab with a refit by the company or by the city's best
    /// renovation contractor at its quote, a contractor's refit as a project, and a rebuild's choice to refit the business inside
    /// it too. Written on a page's opening, a pick or a press.
    /// </summary>
    public sealed partial class ConstructionApp
    {
        private readonly List<BusinessView> _businesses = new List<BusinessView>(8);

        // The player's businesses in rented buildings, below their own buildings in the Land tab (an owned one is listed with those).
        private bool WriteBusinessRows(ref string selected, out BusinessView picked)
        {
            picked = default(BusinessView);
            bool found = false;
            if (!_director.RefitsOn)
            {
                return false;
            }
            _director.Refittable(_businesses);
            for (int i = 0; i < _businesses.Count; i++)
            {
                BusinessView b = _businesses[i];
                if (b.Owned)
                {
                    continue;   // its building's row is the player's own already
                }
                TableRow row = Pick("biz:" + ConstructionGame.Place(b.Street, b.Number), ref selected);
                row.Kind = ConstructionTables.LandBuilding;
                row.Set(0, b.Name);
                row.Set(1, ConstructionText.T("reallife_bld_value_area", "area", ConstructionText.N(b.Sqm)), b.Sqm);
                row.Set(2, ConstructionText.T(b.Office ? "reallife_bld_use_offices" : "reallife_bld_use_shops"));
                row.Set(3, string.Empty);
                row.Set(4, RefitState(b));
                if (row.Id == selected)
                {
                    picked = b;
                    found = true;
                }
            }
            return found;
        }

        private string RefitState(BusinessView b)
        {
            ProjectFile p = _director.State.AtBuilding(b.Street, b.Number);
            return p != null ? ConstructionText.StageWords(p) : ConstructionText.T("reallife_bld_land_state_rented");
        }

        private void WriteBusiness(InfoBox box, BusinessView b)
        {
            box.Title(b.Name);
            box.Subtitle(ConstructionGame.AddressText(b.Street, b.Number));
            box.Row("reallife_bld_row_area", ConstructionText.T("reallife_bld_value_area", "area", ConstructionText.N(b.Sqm)));
            box.Row("reallife_bld_row_district", ConstructionGame.DistrictWords(b.District));
            box.Note(ConstructionText.T("reallife_bld_refit_note_rented"));
            RefitButtons(box, b);
            BackButton(box);
        }

        /// <summary>The refit's two ways for a business of the player's, or the project already there.</summary>
        private void RefitButtons(InfoBox box, BusinessView b)
        {
            if (!_director.RefitsOn)
            {
                return;
            }
            box.Section("reallife_bld_sec_refit");
            ProjectFile live = _director.State.AtBuilding(b.Street, b.Number);
            if (live != null)
            {
                int project = live.Id;
                InfoBox.Button(box.ButtonRow(), InfoBox.Positive, "reallife_bld_button_to_project", () => Jump(ConstructionDirector.TabProjects, "prj:" + project));
                return;
            }
            RefitQuote q = _director.QuoteFor(b);
            box.Row("reallife_bld_row_refit_hours", ConstructionText.T("reallife_bld_value_refit_hours", "hours", q.Hours.ToString(), "days", TenderRules.StandardDays(q.Hours).ToString()));
            box.Row("reallife_bld_row_refit_quote", ConstructionText.T("reallife_bld_value_refit_quote", "contractor", ConstructionText.T(BuildCatalogue.RivalKeys[q.Rival]),
                "amount", ConstructionText.Money(q.Price), "days", q.Days.ToString()));
            Transform buttons = box.ButtonRow();
            string street = b.Street;
            int number = b.Number;
            if (_director.State.Firm.Founded > 0)
            {
                InfoBox.Button(buttons, InfoBox.Positive, "reallife_bld_button_refit_own", () =>
                {
                    int made = _director.NewRefit(street, number, out string why);
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
                Confirm("reallife_bld_confirm_refit", "reallife_bld_confirm_refit_app", b.Name, q.Price, () =>
                {
                    int made = _director.ContractorRefit(street, number, false, out string why);
                    return made > 0 ? "reallife_bld_answer_refit_ordered" : why;
                }, false));
            box.Note(ConstructionText.T("reallife_bld_refit_note"));
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
            }
            else if (p.Stage == ProjectStage.Finished)
            {
                box.RowPair("reallife_bld_row_handed", ConstructionText.Day(p.Handed), "reallife_bld_row_refit_grade", ConstructionText.T(RefitGradeKey(p.Grade)));
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
