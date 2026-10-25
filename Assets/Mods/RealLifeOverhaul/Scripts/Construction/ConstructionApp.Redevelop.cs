using System;
using RealLifeOverhaul.GameUi;
using UnityEngine;

namespace RealLifeOverhaul.Construction
{
    /// <summary>
    /// Redevelopment in the app (docs/features/construction.md, "Redevelopment"): on the Land tab's page of a building of the
    /// player's in Uptown, the ways to redevelop it (knock it down); a demolition's own page while its permit is asked for and
    /// before its works start (the notice to its tenants and business, the permit at City Hall, the company's crews or the city's
    /// demolition contractor at its quote). The site's own stages are the project's usual pages. Written on a page's opening, a
    /// pick or a press.
    /// </summary>
    public sealed partial class ConstructionApp
    {
        /// <summary>The ways to redevelop a building of the player's that is one of Uptown's; nothing for a building of the game's own districts.</summary>
        private void RedevelopButtons(InfoBox box, BuildingView b)
        {
            if (!ConstructionDirector.Standing(b.Street, b.Number, out StandingView s))
            {
                return;   // the game's own buildings are rebuilt, never knocked down
            }
            box.Section("reallife_bld_sec_redevelop");
            box.RowPair("reallife_bld_row_storeys", ConstructionText.N(s.Storeys), "reallife_bld_row_spaces_held", ConstructionText.N(1 + (s.Over != null ? s.Over.Length : 0)));
            string block = _director.DemolishBlock(b.Street, b.Number);
            if (block != null)
            {
                box.Line(ConstructionText.T(block), CellTone.Yellow);
            }
            else
            {
                string street = b.Street;
                int number = b.Number;
                InfoBox.Button(box.ButtonRow(), InfoBox.Negative, "reallife_bld_button_demolish", () =>
                    Confirm("reallife_bld_confirm_demolish", "reallife_bld_confirm_demolish_body", b.Name, 0L, () =>
                    {
                        int made = _director.NewDemolition(street, number, out string why);
                        if (made > 0)
                        {
                            Jump(ConstructionDirector.TabProjects, "prj:" + made);
                            return "reallife_bld_answer_demolition_planned";
                        }
                        return why;
                    }, true));
            }
            box.Note(ConstructionText.T("reallife_bld_demolish_note"));
        }

        /// <summary>A demolition's page before its works: the notice to its tenants and business, the permit, and who knocks it down.</summary>
        private void WriteDemolition(InfoBox box, BuildState state, ProjectFile p)
        {
            box.RowPair("reallife_bld_row_area", ConstructionText.T("reallife_bld_value_area", "area", ConstructionText.N(p.Area)), "reallife_bld_row_storeys", ConstructionText.N(p.Storeys));
            box.RowPair("reallife_bld_row_hours", ConstructionText.N(p.Hours), "reallife_bld_row_rubble", ConstructionText.N(RedevelopRules.RubbleLoads(p.Area)));
            box.Row("reallife_bld_row_estimate", ConstructionText.Money(p.Estimate));
            box.Row("reallife_bld_row_notice", NoticeWords(p), Emptied(p) ? CellTone.Green : CellTone.Plain);
            Transform buttons = box.ButtonRow();
            if (p.Stage == ProjectStage.Design)
            {
                box.Row("reallife_bld_row_permit_fee", ConstructionText.T("reallife_bld_value_permit", "amount", ConstructionText.Money(_director.PlanningFee(p)),
                    "days", RedevelopRules.PermitDays(p.Area).ToString()));
                InfoBox.Button(buttons, InfoBox.Positive, "reallife_bld_button_permit", () => Answer(_director.Apply(p)));
                box.Note(ConstructionText.T("reallife_bld_demolish_permit_note"));
            }
            else
            {
                string block = _director.StartBlock(p);
                if (_director.State.Firm.Founded > 0)
                {
                    if (block == null)
                    {
                        InfoBox.Button(buttons, InfoBox.Positive, "reallife_bld_button_start", () => Answer(_director.StartBuilding(p)));
                    }
                    else
                    {
                        box.Line(ConstructionText.T(block), CellTone.Yellow);
                    }
                }
                RefitQuote q = _director.DemolitionQuote(p);
                box.Row("reallife_bld_row_demolition_quote", ConstructionText.T("reallife_bld_value_refit_quote", "contractor", ConstructionText.T(BuildCatalogue.RivalKeys[q.Rival]),
                    "amount", ConstructionText.Money(q.Price), "days", q.Days.ToString()));
                if (ConstructionDirector.Emptied(p))
                {
                    InfoBox.Button(buttons, InfoBox.Negative, "reallife_bld_button_demolition_contractor", () =>
                        Confirm("reallife_bld_confirm_demolition_contractor", "reallife_bld_confirm_demolition_contractor_body", _director.ProjectName(p), q.Price,
                            () => _director.ContractorDemolition(p, false), false));
                }
                box.Note(ConstructionText.T("reallife_bld_demolish_ready_note"));
            }
            InfoBox.Button(box.ButtonRow(), InfoBox.Negative, "reallife_bld_button_abandon", () =>
                Confirm("reallife_bld_confirm_abandon", "reallife_bld_confirm_abandon_body", _director.ProjectName(p), p.Spent, () => _director.Abandon(p), true));
            BackButton(box);
        }

        // The notice to a building's tenants and business in words: not given yet, running to its day, or done and what it cost.
        private static string NoticeWords(ProjectFile p)
        {
            if (p.BuyOut > 0L)
            {
                return ConstructionText.T("reallife_bld_notice_state_bought", "amount", ConstructionText.Money(p.BuyOut));
            }
            if (p.BuyOut < 0L)
            {
                return ConstructionText.T("reallife_bld_notice_state_empty");
            }
            return p.NoticeUntil > 0 ? ConstructionText.T("reallife_bld_notice_state_running", "day", ConstructionText.Day(p.NoticeUntil))
                : ConstructionText.T("reallife_bld_notice_state_none");
        }

        private static bool Emptied(ProjectFile p) => ConstructionDirector.Emptied(p);
    }
}
