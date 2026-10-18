using Localizor;
using RealLifeOverhaul.GameUi;
using RealLifeOverhaul.Tech;
using UnityEngine;

namespace RealLifeOverhaul.Careers
{
    /// <summary>The working life on the My job tab: reviews, burnout, office matters, options, the pension, the clauses and a conflict of interest.</summary>
    public sealed partial class CareerApp
    {
        private void JobMore(InfoBox box, WorkFile w, Job j)
        {
            box.Section("reallife_meta_title_careers_life");
            int next = j.ReviewDay + LifeRules.ReviewEvery(GameCalendar.DaysPerYear);
            if (j.Rating > 0)
            {
                box.Row("reallife_ibox_review", CareersText.Fill("reallife_ibox_review_value", "rating", j.Rating.ToString(), "day", CareersText.Day(j.ReviewDay)),
                    j.Rating >= 4 ? CellTone.Green : j.Rating <= 2 ? CellTone.Yellow : CellTone.Plain);
            }
            box.Row("reallife_ibox_next_review", CareersText.Day(next));
            box.Row("reallife_ibox_boss", CareersText.Boss((BossKind)j.Boss));
            box.Row("reallife_ibox_colleagues", j.Favour.ToString(), j.Favour > 0 ? CellTone.Green : j.Favour < 0 ? CellTone.Yellow : CellTone.Plain);
            box.Meter("reallife_ibox_burnout", j.Burnout / 1000f, CareersText.Mille(j.Burnout), j.Burnout >= 700 ? CellTone.Red : j.Burnout >= 400 ? CellTone.Yellow : CellTone.Green);
            box.Row("reallife_ibox_overtime", CareersText.Minutes(j.Overtime));
            if (j.Politics >= 0)
            {
                Matter m = LifeRules.Matters[TalentCatalogue.Clamp(j.Politics, 0, LifeRules.Matters.Length - 1)];
                box.Line(CareersText.T(CareersText.MatterKey(m), "employer", CareersText.Employer(j.Post.Employer)), CellTone.Yellow);
            }
            if (j.Options > 0)
            {
                box.Line(CareersText.T("reallife_life_job_options", "count", j.Options.ToString(), "day", CareersText.Day(j.VestDay)), CellTone.Green);
            }
            int share = LifeRules.ProfitShareBp(j);
            if (share > 0)
            {
                box.Line(CareersText.T("reallife_life_job_share", "share", (share / 100f).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + "%"), CellTone.Green);
            }
            if (j.NonCompete || j.Exclusive)
            {
                box.Note(CareersText.T(j.NonCompete && j.Exclusive ? "reallife_life_job_clauses_both" : j.NonCompete ? "reallife_life_job_clause_restraint" : "reallife_life_job_clause_exclusive"));
            }
            if (_director.Conflicted(j))
            {
                box.Line(CareersText.T(j.Declared ? "reallife_life_job_declared" : "reallife_life_job_conflict"), j.Declared ? CellTone.Plain : CellTone.Red);
            }
            box.Row("reallife_life_sum_pension", CareersText.Cents(w.Pension));
        }

        private void JobButtons(Transform buttons, Job j, int day)
        {
            string late = _director.CanWorkLate();
            if (late == null && _director.DeskFallback(j))   // overtime is worked at the desk, as the day is
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

        private void NoJobMore(InfoBox box, WorkFile w, int day)
        {
            box.Section("reallife_ibox_sec_money");
            if (w.BenefitUntil >= day && w.BenefitWeek > 0)
            {
                box.Line(CareersText.T("reallife_life_benefit_line", "amount", CareersText.Money(Prices.At(w.BenefitWeek)), "day", CareersText.Day(w.BenefitUntil)), CellTone.Green);
            }
            box.Row("reallife_life_sum_pension", CareersText.Cents(w.Pension));
            if (w.Retired <= 0)
            {
                RetireButton(box);
            }
        }

        private void CvMore(InfoBox box, WorkFile w)
        {
            box.Row("reallife_life_sum_pension", CareersText.Cents(w.Pension));
            if (w.Retired > 0)
            {
                box.Note(CareersText.T("reallife_life_retired_on", "day", CareersText.Day(w.Retired)));
            }
            else if (w.Job != null)
            {
                RetireButton(box);
            }
        }

        private void RetireButton(InfoBox box)
        {
            if (_director.CanRetire() != null)
            {
                box.Note(CareersText.Fill("reallife_life_retire_from", "early", LifeRules.RetireEarlyAge.ToString(), "age", LifeRules.RetireAge.ToString(),
                    "cut", (LifeRules.EarlyCutBp / 100).ToString() + "%"));
                return;
            }
            InfoBox.Button(box.ButtonRow(), InfoBox.Negative, "reallife_life_button_retire", () => GameConfirm.Show(
                "reallife_life_confirm_retire_title".Localize(),
                "reallife_life_confirm_retire_body".Localize(new { age = LifeRules.RetireAge.ToString(), cut = (LifeRules.EarlyCutBp / 100).ToString() + "%" }),
                () => UiClone.Safe(() => Do(_director.Retire)), null, null, null, true, true));
        }
    }
}
