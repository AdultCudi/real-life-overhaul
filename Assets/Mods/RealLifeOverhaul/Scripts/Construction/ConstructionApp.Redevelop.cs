using System;
using RealLifeOverhaul.GameUi;
using UnityEngine;

namespace RealLifeOverhaul.Construction
{
    /// <summary>
    /// Redevelopment in the app (docs/features/construction.md, "Redevelopment"): on the Land tab's page of a building of the
    /// player's in Uptown, the ways to redevelop it (knock it down, put storeys on it, make it another use); the own page of a
    /// demolition or a change of use while its permission is asked for and before its works start (the notice to its tenants and
    /// business, the permission at City Hall, the company's crews or the city's contractor at its quote); an extension's storeys
    /// on its plans page. The site's own stages are the project's usual pages. Written on a page's opening, a pick or a press.
    /// </summary>
    public sealed partial class ConstructionApp
    {
        private readonly System.Collections.Generic.List<BuildUse> _uses = new System.Collections.Generic.List<BuildUse>(4);

        /// <summary>The ways to redevelop a building of the player's that is one of Uptown's; nothing for a building of the game's own districts.</summary>
        private void RedevelopButtons(InfoBox box, BuildingView b)
        {
            if (!ConstructionDirector.Standing(b.Street, b.Number, out StandingView s))
            {
                return;   // the game's own buildings are rebuilt, never knocked down
            }
            box.Section("reallife_bld_sec_redevelop");
            box.RowPair("reallife_bld_row_storeys", ConstructionText.N(s.Storeys), "reallife_bld_row_spaces_held", ConstructionText.N(1 + (s.Over != null ? s.Over.Length : 0)));
            string street = b.Street;
            int number = b.Number;
            string block = _director.DemolishBlock(street, number), extend = _director.ExtendBlock(street, number), convert = _director.ConvertBlock(street, number);
            if (block != null)
            {
                box.Line(ConstructionText.T(block), CellTone.Yellow);   // why it cannot come down: the commonest reason, and the player's own things inside
            }
            if (extend == "reallife_bld_answer_extension_no_room" && block == null)
            {
                box.Line(ConstructionText.T(extend), CellTone.Yellow);
            }
            Transform buttons = box.ButtonRow();
            if (extend == null)
            {
                InfoBox.Button(buttons, InfoBox.Positive, "reallife_bld_button_extend", () =>
                    Confirm("reallife_bld_confirm_extend", "reallife_bld_confirm_extend_body", b.Name, 0L, () => Planned(_director.NewExtension(street, number, out string why), why,
                        "reallife_bld_answer_extension_planned"), false));
            }
            if (convert == null)
            {
                InfoBox.Button(buttons, InfoBox.Positive, "reallife_bld_button_convert", () =>
                    Confirm("reallife_bld_confirm_convert", "reallife_bld_confirm_convert_body", b.Name, 0L, () => Planned(_director.NewConversion(street, number, out string why), why,
                        "reallife_bld_answer_conversion_planned"), false));
            }
            if (block == null)
            {
                InfoBox.Button(buttons, InfoBox.Negative, "reallife_bld_button_demolish", () =>
                    Confirm("reallife_bld_confirm_demolish", "reallife_bld_confirm_demolish_body", b.Name, 0L, () => Planned(_director.NewDemolition(street, number, out string why), why,
                        "reallife_bld_answer_demolition_planned"), true));
            }
            box.Note(ConstructionText.T("reallife_bld_demolish_note"));
            box.Note(ConstructionText.T("reallife_bld_extend_note", "limit", _director.UptownStoreys().ToString()));
        }

        // A project of redevelopment made: its page opened, and what to do next; else why it was not made.
        private string Planned(int made, string why, string next)
        {
            if (made <= 0)
            {
                return why;
            }
            Jump(ConstructionDirector.TabProjects, "prj:" + made);
            return next;
        }

        /// <summary>An extension's storeys on top, chosen before its plans are asked for: up to the building code's limit in force.</summary>
        private void StoreysSelector(InfoBox box, ProjectFile p, bool asked)
        {
            int room = _director.ExtensionRoomOf(p);
            if (!asked && !p.PlansDone && room > 0)
            {
                int most = Math.Max(room, p.Added);
                box.Selector(most, BuildCatalogue.Clamp(p.Added - 1, 0, most - 1), i => ConstructionText.T("reallife_bld_sel_added", "storeys", (i + 1).ToString()),
                    i => Answer(_director.SetAdded(p, i + 1)));
            }
            else
            {
                box.Row("reallife_bld_row_added", ConstructionText.N(p.Added));
            }
            box.Note(ConstructionText.T("reallife_bld_extension_plans_note"));
        }

        /// <summary>
        /// The page of works that need their building empty, before they start (a demolition, a change of use): the notice to its
        /// tenants and business, the permission, and who does the works.
        /// </summary>
        private void WriteVacant(InfoBox box, BuildState state, ProjectFile p)
        {
            bool down = p.Kind == ProjectKind.Demolition;
            if (!down)
            {
                WriteUseChoice(box, p);
            }
            box.RowPair("reallife_bld_row_area", ConstructionText.T("reallife_bld_value_area", "area", ConstructionText.N(p.Area)), "reallife_bld_row_storeys", ConstructionText.N(p.Storeys));
            if (down)
            {
                box.RowPair("reallife_bld_row_hours", ConstructionText.N(p.Hours), "reallife_bld_row_rubble", ConstructionText.N(RedevelopRules.RubbleLoads(p.Area)));
            }
            else
            {
                box.Row("reallife_bld_row_hours", ConstructionText.N(p.Hours));
            }
            box.Row("reallife_bld_row_estimate", ConstructionText.Money(p.Estimate));
            box.Row("reallife_bld_row_notice", NoticeWords(p), Emptied(p) ? CellTone.Green : CellTone.Plain);
            Transform buttons = box.ButtonRow();
            if (p.Stage == ProjectStage.Design)
            {
                int days = down ? RedevelopRules.PermitDays(p.Area) : PlanningRules.WorkingDays(p.Area);
                box.Row(down ? "reallife_bld_row_permit_fee" : "reallife_bld_row_change_fee",
                    ConstructionText.T("reallife_bld_value_permit", "amount", ConstructionText.Money(_director.PlanningFee(p)), "days", days.ToString()));
                InfoBox.Button(buttons, InfoBox.Positive, down ? "reallife_bld_button_permit" : "reallife_bld_button_change_permit", () => Answer(_director.Apply(p)));
                box.Note(ConstructionText.T(down ? "reallife_bld_demolish_permit_note" : "reallife_bld_convert_permit_note"));
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
                RefitQuote q = _director.VacantQuote(p);
                box.Row(down ? "reallife_bld_row_demolition_quote" : "reallife_bld_row_conversion_quote", ConstructionText.T("reallife_bld_value_refit_quote", "contractor",
                    ConstructionText.T(BuildCatalogue.RivalKeys[q.Rival]), "amount", ConstructionText.Money(q.Price), "days", q.Days.ToString()));
                if (ConstructionDirector.Emptied(p))
                {
                    InfoBox.Button(buttons, InfoBox.Negative, down ? "reallife_bld_button_demolition_contractor" : "reallife_bld_button_conversion_contractor", () =>
                        Confirm(down ? "reallife_bld_confirm_demolition_contractor" : "reallife_bld_confirm_conversion_contractor",
                            down ? "reallife_bld_confirm_demolition_contractor_body" : "reallife_bld_confirm_conversion_contractor_body", _director.ProjectName(p), q.Price,
                            () => _director.ContractorVacant(p, false), false));
                }
                box.Note(ConstructionText.T(down ? "reallife_bld_demolish_ready_note" : "reallife_bld_convert_ready_note"));
            }
            InfoBox.Button(box.ButtonRow(), InfoBox.Negative, "reallife_bld_button_abandon", () =>
                Confirm("reallife_bld_confirm_abandon", "reallife_bld_confirm_abandon_body", _director.ProjectName(p), p.Spent, () => _director.Abandon(p), true));
            BackButton(box);
        }

        /// <summary>A site of the player's joined with the next along its block's side (bought first if it is a free space), or split back into its lots.</summary>
        private void JoinButtons(InfoBox box, string firstId)
        {
            string join = _director.JoinBlock(firstId, out string nextId);
            string split = _director.SplitBlock(firstId);
            if (join != null && join != "reallife_bld_answer_join_buy_first" && join != "reallife_bld_answer_join_too_wide" && split != null)
            {
                return;   // nothing beside it to join, and one lot already
            }
            box.Section("reallife_bld_sec_join");
            Transform buttons = box.ButtonRow();
            if (join == null)
            {
                InfoBox.Button(buttons, InfoBox.Positive, "reallife_bld_button_join", () => Answer(_director.Join(firstId)));
            }
            else if (join == "reallife_bld_answer_join_buy_first")
            {
                long price = _director.NextSpacePrice(firstId);
                InfoBox.Button(buttons, InfoBox.Positive, "reallife_bld_button_buy_join", () =>
                    Confirm("reallife_bld_confirm_buy_join", "reallife_bld_confirm_buy_join_body", _director.LotWords(nextId), price, () => _director.BuyAndJoin(firstId), false));
            }
            else if (join == "reallife_bld_answer_join_too_wide")
            {
                box.Line(ConstructionText.T(join), CellTone.Yellow);
            }
            if (split == null)
            {
                InfoBox.Button(buttons, InfoBox.Negative, "reallife_bld_button_split", () => Answer(_director.Split(firstId)));
            }
            box.Note(ConstructionText.T("reallife_bld_join_note"));
        }

        // A change of use's new use: chosen among those its structure takes before its permission is asked for, shown after.
        private void WriteUseChoice(InfoBox box, ProjectFile p)
        {
            if (p.Stage != ProjectStage.Design)
            {
                box.Row("reallife_bld_row_new_use", ConstructionText.T(BuildCatalogue.UseKeys[(int)p.Use]));
                return;
            }
            _director.ConversionUsesOf(p, _uses);
            if (_uses.Count == 0)
            {
                box.Line(ConstructionText.T("reallife_bld_answer_conversion_none"), CellTone.Red);
                return;
            }
            int at = Math.Max(0, _uses.IndexOf(p.Use));
            box.Selector(_uses.Count, at, i => ConstructionText.T("reallife_bld_sel_new_use", "use", ConstructionText.T(BuildCatalogue.UseKeys[(int)_uses[i]])),
                i => Answer(_director.SetConversionUse(p, _uses[i])));
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
