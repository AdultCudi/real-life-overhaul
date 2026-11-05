using System;
using RealLifeOverhaul.GameUi;
using RealLifeOverhaul.Ui;

namespace RealLifeOverhaul.Construction
{
    /// <summary>
    /// The fit-out finish and the height variance on a project's page: the finish as a step between basic, standard and premium with each tier's cost, days and rent side by side, its
    /// window, and the rent and payback of the one chosen; the variance as the planning line (floors over the code, pending until the council's sitting, allowed or refused) with the
    /// button that asks. Written on a page's opening or a press; nothing a frame.
    /// </summary>
    public sealed partial class ConstructionLive
    {
        // The finish on the plans, the ready page, the site and the finished building. A project with no finish to choose shows nothing; Let it grow's shows its own tier.
        private void FinishBox(KitBox box, ProjectFile p)
        {
            if (p.Grow > 0 || !ConstructionDirector.FinishKind(p))
            {
                return;
            }
            int tier = ConstructionDirector.FinishTier(p);
            bool open = ConstructionDirector.FinishOpen(p);
            box.Section("reallife_ui_cons_sec_finish");
            if (open)
            {
                box.Selector(FinishRules.Tiers, tier, i => ConstructionText.T(GrowRules.TierKeys[i]), i => Answer(Director.SetFinish(p, i)));
            }
            else
            {
                box.Row("reallife_ui_cons_row_finish", ConstructionText.T(GrowRules.TierKeys[tier]));
            }
            long month = RentMonth(p);
            for (int t = 0; t < FinishRules.Tiers && (open || t == tier); t++)
            {
                long price = p.Stage <= ProjectStage.Ready && p.Live ? Director.FinishEstimate(p, t) : p.Estimate;
                int days = Director.FinishDays(p, t);

                    string more = ConstructionText.T(days > 0 ? "reallife_ui_cons_finish_days_more" : days < 0 ? "reallife_ui_cons_finish_days_less"
                    : "reallife_ui_cons_finish_days_same", "count", Math.Abs(days).ToString());
                string rent = month > 0L ? ConstructionText.T("reallife_ui_cons_finish_rent", "amount", ConstructionText.Money(FinishRules.Rent(month, t))) : string.Empty;
                box.RowWords(ConstructionText.T(GrowRules.TierKeys[t]), ConstructionText.T("reallife_ui_cons_finish_line", "cost", ConstructionText.Money(price), "days", more, "rent", rent).Trim(),
                    t == tier ? CellTone.Green : CellTone.Plain);
            }
            if (month > 0L && p.Estimate > 0L)
            {
                int payback = FinishRules.PaybackMonths(p.Stage <= ProjectStage.Ready ? Director.FinishEstimate(p, tier) : p.Estimate, FinishRules.Rent(month, tier));
                box.Row("reallife_ui_cons_row_payback", ConstructionText.T("reallife_ui_cons_months_n", "count", payback.ToString()));
            }
            if (open && p.FinishBy > 0)
            {
                box.Line(ConstructionText.T("reallife_ui_cons_finish_by", "day", ConstructionText.Day(p.FinishBy)), CellTone.Yellow);
            }
            else if (open)
            {
                box.Note(ConstructionText.T("reallife_ui_cons_finish_before"));
            }
            else if (p.Live && p.Stage <= ProjectStage.Ready)
            {
                box.Note(ConstructionText.T("reallife_ui_cons_finish_closed"));
            }
        }

        // The height variance as the planning line: what the plans are over, the council's sitting, its answer; and the button that asks it.
        private void VarianceBox(KitBox box, ProjectFile p, KitRow buttons)
        {
            if (!ConstructionDirector.VarianceKind(p))
            {
                return;
            }
            int over = ConstructionDirector.VarianceOver(p);
            if (over <= 0 && p.Variance == VarianceState.None)
            {
                return;
            }
            box.Section("reallife_ui_cons_sec_variance");
            int limit = ConstructionDirector.HeightLimitOf(p);
            switch (p.Variance)
            {
                case VarianceState.Pending:
                    box.Row("reallife_ui_cons_row_variance", ConstructionText.T("reallife_ui_cons_variance_pending", "floors", p.VarianceFloors.ToString(), "day", ConstructionText.Day(p.VarianceDay)),
                        CellTone.Yellow);
                    box.Note(ConstructionText.T("reallife_ui_cons_variance_note"));
                    return;
                case VarianceState.Granted when ConstructionDirector.VarianceCovers(p):
                    box.Row("reallife_ui_cons_row_variance", ConstructionText.T("reallife_ui_cons_variance_granted", "floors", p.VarianceFloors.ToString(), "limit", limit.ToString()), CellTone.Green);
                    if (p.VarianceAyes >= 0)
                    {
                        box.Note(ConstructionText.T("reallife_ui_cons_variance_vote", "ayes", p.VarianceAyes.ToString(), "noes", p.VarianceNoes.ToString()));
                    }
                    return;
            }
            if (p.Variance == VarianceState.Refused)
            {
                box.Row("reallife_ui_cons_row_variance", ConstructionText.T("reallife_ui_cons_variance_refused", "floors", p.VarianceFloors.ToString()), CellTone.Red);
                if (p.VarianceAyes >= 0)
                {
                    box.Note(ConstructionText.T("reallife_ui_cons_variance_vote", "ayes", p.VarianceAyes.ToString(), "noes", p.VarianceNoes.ToString()));
                }
            }
            box.Line(ConstructionText.T("reallife_ui_cons_variance_over", "floors", p.Storeys.ToString(), "limit", limit.ToString(), "over", over.ToString()), CellTone.Yellow);
            string block = Director.VarianceBlock(p);
            if (block == null && buttons != null)
            {
                KitBox.Button(buttons, KitBox.Negative, "reallife_ui_cons_button_variance", () =>
                    Confirm("reallife_ui_cons_confirm_variance", "reallife_ui_cons_confirm_variance_body", Director.ProjectName(p),
                        ConstructionDirector.VarianceFee, () => Director.AskVariance(p), false));
            }
            else if (block != null && block != "reallife_ui_cons_answer_variance_none")
            {
                box.Line(ConstructionText.T(block), CellTone.Plain);
            }
        }
    }
}
