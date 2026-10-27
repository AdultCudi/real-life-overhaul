using RealLifeOverhaul.GameUi;
using UnityEngine;

namespace RealLifeOverhaul.Construction
{
    /// <summary>
    /// A car park's pages in the Projects tab (docs/features/construction.md, "Parking lots"): the plans page of a car park's project (no
    /// designs to choose: the lot, its bays, the works' hours and days, the estimate, the practice's fee), and the finished page, which
    /// points to the car park's pay machine and the build mode where it is run. The rest of a project's pages (planning, ready, site,
    /// money, hired builder) are the same as any project's.
    /// </summary>
    public sealed partial class ConstructionApp
    {
        private void WriteParkPlans(InfoBox box, BuildState state, ProjectFile p)
        {
            bool asked = p.PlansDay > 0 && !p.PlansDone;
            CarParkLayout plan = CarParkRules.LayoutOf(p.Width, p.Depth, p.Park);
            box.Section("reallife_park_sec_lot");
            box.RowPair("reallife_park_row_ground", ConstructionText.T("reallife_park_value_ground", "width", ConstructionText.N(p.Width), "depth", ConstructionText.N(p.Depth)),
                "reallife_park_row_bays", ConstructionText.T("reallife_park_value_bays", "bays", ConstructionText.N(plan.Bays), "blue", ConstructionText.N(plan.Accessible)));
            box.RowPair("reallife_park_row_works", ConstructionText.T("reallife_park_value_works", "hours", ConstructionText.N(p.Hours)),
                "reallife_park_row_use", ConstructionText.T(BuildCatalogue.UseKeys[(int)p.Use]));
            int days = CarParkRules.PlansDays + CarParkRules.PermitDays + CarParkRules.WorkingDays(p.Hours);
            box.Row("reallife_bld_sheet_days", ConstructionText.Fill("reallife_bld_sheet_days_value", "days", ConstructionText.N(days)));
            CostSheet sheet = _director.Preview(p, out _, out _, out _, out _);
            box.Row(p.PlansDone ? "reallife_bld_row_estimate" : "reallife_bld_row_estimate_about", ConstructionText.Money(p.PlansDone ? p.Estimate : sheet.Total));
            Transform buttons = box.ButtonRow();
            if (!p.PlansDone)
            {
                if (asked)
                {
                    box.Note(ConstructionText.T("reallife_bld_plans_coming", "day", ConstructionText.Day(p.PlansDay)));
                }
                else
                {
                    PlansQuote quote = _director.QuoteOf(p);
                    InfoBox.Button(buttons, InfoBox.Positive, "reallife_bld_button_draw", () =>
                        Confirm("reallife_bld_confirm_plans", "reallife_bld_confirm_plans_body", _director.ProjectName(p), quote.Fee, () => _director.DrawPlans(p), false));
                }
            }
            else
            {
                InfoBox.Button(buttons, InfoBox.Positive, "reallife_park_button_apply", () => Answer(_director.Apply(p)));
                box.Note(ConstructionText.T("reallife_park_apply_note", "fee", ConstructionText.Money(_director.PlanningFee(p))));
            }
            InfoBox.Button(buttons, InfoBox.Additional, "reallife_bld_back", Back);
            AbandonRow(box, p);
            Guide(box);
        }

        // A finished car park's project: it is open; run it at its pay machine, or in build mode.
        private void WriteParkFinished(InfoBox box, ProjectFile p)
        {
            CarParkFile c = _director.State != null ? _director.State.CarParkOfProject(p.Id) : null;
            if (c == null)
            {
                box.Note(ConstructionText.T("reallife_park_finished_closed"));
                return;
            }
            box.RowPair("reallife_park_row_bays", ConstructionText.N(c.Bays), "reallife_park_row_mode", ConstructionDirector.ParkModeWords(c));
            box.Note(ConstructionText.T("reallife_park_finished_note"));
        }
    }
}
