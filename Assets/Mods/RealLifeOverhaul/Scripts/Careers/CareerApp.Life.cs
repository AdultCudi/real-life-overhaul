using Localizor;
using RealLifeOverhaul.GameUi;
using RealLifeOverhaul.Tech;
using UI;
using UnityEngine;

namespace RealLifeOverhaul.Careers
{
    /// <summary>
    /// The working life on the My job tab: the last review and the next, the boss's manner and the player's standing with
    /// colleagues, burnout, an office matter to answer, share options, the pension, the clauses of the contract and a
    /// conflict of interest; the buttons for overtime, asking for a rise, answering a matter, declaring a conflict and
    /// retiring.
    /// </summary>
    public sealed partial class CareerApp
    {
        partial void JobMore(InfoBox box, WorkFile w, StudyFile f, Job j, int day)
        {
            int next = j.ReviewDay + LifeRules.ReviewEvery(GameCalendar.DaysPerYear);
            box.Line(j.Rating > 0
                ? CareersText.Fill("reallife_life_job_review", "rating", j.Rating.ToString(), "day", CareersText.Day(j.ReviewDay), "next", CareersText.Day(next))
                : CareersText.T("reallife_life_job_review_none", "next", CareersText.Day(next)));
            box.Line(CareersText.Fill("reallife_life_job_feel", "boss", CareersText.Boss((BossKind)j.Boss),
                "favour", j.Favour.ToString(), "burnout", CareersText.Mille(j.Burnout), "overtime", CareersText.Minutes(j.Overtime)));
            if (j.Politics >= 0)
            {
                Matter m = LifeRules.Matters[TalentCatalogue.Clamp(j.Politics, 0, LifeRules.Matters.Length - 1)];
                box.Line(CareersText.T(CareersText.MatterKey(m), "employer", CareersText.Employer(j.Post.Employer)));
            }
            if (j.Options > 0)
            {
                box.Line(CareersText.T("reallife_life_job_options", "count", j.Options.ToString(), "day", CareersText.Day(j.VestDay)));
            }
            int share = LifeRules.ProfitShareBp(j);
            if (share > 0)
            {
                box.Line(CareersText.T("reallife_life_job_share", "share", (share / 100f).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + "%"));
            }
            if (j.NonCompete || j.Exclusive)
            {
                box.Line(CareersText.T(j.NonCompete && j.Exclusive ? "reallife_life_job_clauses_both" : j.NonCompete ? "reallife_life_job_clause_restraint" : "reallife_life_job_clause_exclusive"));
            }
            if (j.Leaving == (int)LeaveReason.Redundant && j.NoticeEnd > 0)
            {
                box.Line(CareersText.T("reallife_life_job_redundant", "day", CareersText.Day(j.NoticeEnd)));
            }
            if (_director.Conflicted(j))
            {
                box.Line(CareersText.T(j.Declared ? "reallife_life_job_declared" : "reallife_life_job_conflict"));
            }
            box.Line(CareersText.Pair("reallife_life_sum_pension", CareersText.Cents(w.Pension)));
        }

        partial void JobButtons(Transform buttons, Job j, int day)
        {
            string late = _director.CanWorkLate();
            if (late == null)
            {
                InfoBox.Button(buttons, InfoBox.Additional, "reallife_life_button_late", () => Do(_director.WorkLate));
            }
            if (day - j.AskDay >= LifeRules.AskGapDays && j.NoticeEnd <= 0)
            {
                InfoBox.Button(buttons, InfoBox.Additional, "reallife_life_button_rise", () => Do(_director.AskRise));
            }
            InfoBox box = _shell.Box;
            if (j.Politics >= 0)
            {
                Matter m = LifeRules.Matters[TalentCatalogue.Clamp(j.Politics, 0, LifeRules.Matters.Length - 1)];
                Transform answers = box.ButtonRow();
                InfoBox.Button(answers, InfoBox.Positive, CareersText.MatterKey(m, "_a"), () => Do(() => _director.AnswerMatter(true)));
                InfoBox.Button(answers, InfoBox.Additional, CareersText.MatterKey(m, "_b"), () => Do(() => _director.AnswerMatter(false)));
            }
            if (!j.Declared && _director.Conflicted(j))
            {
                InfoBox.Button(box.ButtonRow(), InfoBox.Additional, "reallife_life_button_declare", () => Do(_director.Declare));
            }
        }

        partial void NoJobMore(InfoBox box, WorkFile w, int day)
        {
            if (w.BenefitUntil >= day && w.BenefitWeek > 0)
            {
                box.Line(CareersText.T("reallife_life_benefit_line", "amount", CareersText.Money(Prices.At(w.BenefitWeek)), "day", CareersText.Day(w.BenefitUntil)));
            }
            box.Line(CareersText.Pair("reallife_life_sum_pension", CareersText.Cents(w.Pension)));
            if (w.Retired <= 0)
            {
                RetireButton(box, w);
            }
        }

        partial void CvMore(InfoBox box, WorkFile w, StudyFile f, int day)
        {
            box.Line(CareersText.Pair("reallife_life_sum_pension", CareersText.Cents(w.Pension)));
            if (w.Retired > 0)
            {
                box.Line(CareersText.T("reallife_life_retired_on", "day", CareersText.Day(w.Retired)));
            }
            else if (w.Job != null)
            {
                RetireButton(box, w);
            }
        }

        private void RetireButton(InfoBox box, WorkFile w)
        {
            if (_director.CanRetire() != null)
            {
                box.Line(CareersText.Fill("reallife_life_retire_from", "early", LifeRules.RetireEarlyAge.ToString(), "age", LifeRules.RetireAge.ToString(),
                    "cut", (LifeRules.EarlyCutBp / 100).ToString() + "%"));
                return;
            }
            InfoBox.Button(box.ButtonRow(), InfoBox.Negative, "reallife_life_button_retire", () => HudConfirm.Show(
                "reallife_life_confirm_retire_title".Localize(), "reallife_life_confirm_retire_body".Localize(new { age = LifeRules.RetireAge.ToString(), cut = (LifeRules.EarlyCutBp / 100).ToString() + "%" }),
                () => UiClone.Safe(() => Do(_director.Retire)), null, null, null, true, true));
        }
    }
}
