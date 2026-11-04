using System;
using System.Collections.Generic;
using Localizor;
using RealLifeOverhaul.Careers;
using RealLifeOverhaul.Tech;

namespace RealLifeOverhaul.Ui
{
    using Job = RealLifeOverhaul.Careers.Job;

    // The parts of Career that are not the vacancies: the management ladder (decisions, policy, requests, staff and stores), the practice and
    // founding of Own work, and what the job lets the player answer (a matter, a conflict, resigning, walking out, retiring).
    public sealed partial class CareerLive
    {
        private const int GroupDecide = 0, GroupPolicy = 1, GroupAsk = 2, GroupStaff = 3, GroupStores = 4;

        private void ReadManagement(CareersDirector d, int day)
        {
            LadderFile f = d.Ladder;
            if (f == null)
            {
                _facts.ManagementLine = W.T("reallife_ladder_empty", "The management ladder opens when you work at one of the city's companies.");
                return;
            }
            CityCompany c = f.Company;
            _facts.ManagementChips = new[] { W.T("reallife_work_filter_everything", "Everything"), W.T("reallife_work_group_decide", "To decide"), W.T("reallife_work_group_policy", "Policy"),
                W.T("reallife_work_group_ask", "Requests"), W.T("reallife_work_group_staff", "Staff"), W.T("reallife_work_group_stores", "Stores") };
            for (int i = 0; i < f.Tasks.Count; i++)
            {
                LadderTask t = f.Tasks[i];
                var r = new CareerRow { Id = t.Id, Icon = UiIcon.People, Group = GroupDecide, Cells = new[] { TaskTitle(t), t.Store >= 0 && t.Store < c.Stores.Count ? c.Stores[t.Store].Name : "-",
                    W.T("reallife_work_decide", "Decide"), CareersText.Day(t.Lapse) }, Status = W.T("reallife_work_decide", "Decide"), Tone = UiRole.Warn, Detail = TaskTitle(t) };
                r.Acts.Add(CareerAct.TaskYes); r.Why.Add(string.Empty);
                r.Acts.Add(CareerAct.TaskNo); r.Why.Add(string.Empty);
                r.Acts.Add(CareerAct.TaskUp); r.Why.Add(string.Empty);
                _facts.Management.Add(r);
            }
            CompanyPolicy p = c.Policy;
            for (int i = 0; i <= (int)PolicyField.Budget; i++)
            {
                var field = (PolicyField)i;
                CareerLadderRules.Range(p, field, out int value, out int least, out int most, out int step);
                bool mine = (int)CareerLadderRules.SeatOf(field) == f.Seat;
                string name = CareersText.T(LadderRanks.Key("policy", field.ToString().ToLowerInvariant()));
                var r = new CareerRow { Id = i, Icon = UiIcon.Sliders, Group = GroupPolicy, Cells = new[] { name, c.Name, PolicyValue(field, value), mine ? W.T("reallife_work_yours", "Yours") : "-" },
                    Detail = name + ": " + PolicyValue(field, value) + ". " + CareersText.T("reallife_ladder_policy_about") };
                if (mine)
                {
                    r.Acts.Add(CareerAct.PolicyMore); r.Why.Add(value < most ? string.Empty : W.T("reallife_work_at_most", "It is at its most."));
                    r.Acts.Add(CareerAct.PolicyLess); r.Why.Add(value > least ? string.Empty : W.T("reallife_work_at_least", "It is at its least."));
                }
                _facts.Management.Add(r);
            }
            var ask = new CareerRow { Id = 0, Icon = UiIcon.Message, Group = GroupAsk, Cells = new[] { W.T("reallife_ladder_sec_requests", "Ask your manager"), CareersText.Employer(c.Name), "-", "-" },
                Detail = CareersText.T("reallife_ladder_sec_requests") };
            ask.Acts.Add(CareerAct.AskShiftEarlier); ask.Why.Add(f.Ask != 0 ? W.T("reallife_work_already_asked", "You have asked for something already.") : string.Empty);
            ask.Acts.Add(CareerAct.AskShiftLater); ask.Why.Add(f.Ask != 0 ? W.T("reallife_work_already_asked", "You have asked for something already.") : string.Empty);
            ask.Acts.Add(CareerAct.AskEquipment); ask.Why.Add(f.Equipped ? W.T("reallife_work_granted", "Already granted.") : f.Ask != 0 ? W.T("reallife_work_already_asked", "You have asked for something already.") : string.Empty);
            ask.Acts.Add(CareerAct.AskInsurance); ask.Why.Add(f.Insured ? W.T("reallife_work_granted", "Already granted.") : f.Ask != 0 ? W.T("reallife_work_already_asked", "You have asked for something already.") : string.Empty);
            ask.Acts.Add(CareerAct.Aiming); ask.Why.Add(string.Empty);
            _facts.Management.Add(ask);
            if (c.Stores.Count > 0 && LadderRanks.Has(f.Rung, RungPower.Rota) && f.Rung == (int)Rung.StoreManager)
            {
                var rota = new CareerRow { Id = 0, Icon = UiIcon.Clock, Group = GroupAsk, Cells = new[] { W.T("reallife_ladder_sec_rota", "The store's rota"), c.Stores[0].Name, CareersText.T("reallife_ladder_rota_" + c.Stores[0].Rota), "-" },
                    Detail = CareersText.T("reallife_ladder_sec_rota") };
                for (int k = 0; k < 3; k++)
                {
                    rota.Acts.Add(k == 0 ? CareerAct.RotaLean : k == 1 ? CareerAct.RotaStandard : CareerAct.RotaFull);
                    rota.Why.Add(c.Stores[0].Rota == k ? W.T("reallife_work_already_set", "It is set so.") : string.Empty);
                }
                _facts.Management.Add(rota);
            }
            for (int i = 0; i < c.People.Count && i < 40; i++)
            {
                Colleague pe = c.People[i];
                if (pe.Role != (int)Rung.Frontline)
                {
                    continue;
                }
                var r = new CareerRow { Id = pe.Id, Icon = UiIcon.Person, Group = GroupStaff, Grade = pe.Morale, Cells = new[] { pe.Name, pe.Store >= 0 && pe.Store < c.Stores.Count ? c.Stores[pe.Store].Name : "-",
                    CareersText.Rate(Prices.At(pe.Wage)), pe.Morale.ToString() }, Detail = pe.Name };
                r.Acts.Add(CareerAct.Raise); r.Why.Add(string.Empty);
                _facts.Management.Add(r);
            }
            for (int i = 0; i < c.Stores.Count; i++)
            {
                Outlet st = c.Stores[i];
                _facts.Management.Add(new CareerRow { Id = i, Icon = UiIcon.Store, Group = GroupStores, Grade = st.Results, Cells = new[] { st.Name, CareersText.T("reallife_ladder_rota_" + st.Rota),
                    CareerLadderRules.StaffAt(c, i).ToString() + " / " + st.Needed.ToString(), st.Results.ToString() }, Detail = st.Name });
            }
            if (_facts.Management.Count == 0)
            {
                _facts.ManagementLine = W.T("reallife_work_nothing_to_decide", "Nothing waits for your decision.");
            }
        }

        private static string PolicyValue(PolicyField field, int value)
        {
            switch (field)
            {
                case PolicyField.Staffing: return CareersText.T("reallife_ladder_rota_" + TalentCatalogue.Clamp(value, 0, 2));
                case PolicyField.Escalate: return CareersText.T("reallife_ladder_days", "days", value.ToString());
                case PolicyField.Floor: return value.ToString();
                case PolicyField.Band: return (value / 100f).ToString("0.#", Inv) + "%";
                case PolicyField.WageTop: return CareersText.Rate(Prices.At(value));
                default: return CareersText.Money(Prices.At(value));
            }
        }

        // What the player can do about the job: answer an office matter, declare a conflict, resign, walk out; and retire from the CV.
        private void ReadJobActs(CareersDirector d, WorkFile w, CareerJob cj, int day)
        {
            Job j = w.Job;
            if (j.Politics >= 0 && j.Politics < LifeRules.Matters.Length && j.PoliticsDay >= day)
            {
                Matter m = LifeRules.Matters[j.Politics];
                cj.MatterA = CareersText.T(CareersText.MatterKey(m, "_a"));
                cj.MatterB = CareersText.T(CareersText.MatterKey(m, "_b"));
                cj.MatterText = CareersText.T(CareersText.MatterKey(m));
            }
            cj.CanDeclare = !j.Declared && d.Conflicted(j);
        }

        private void ReadOwnWork(CareersDirector d, WorkFile w, int day)
        {
            Job j = w.Job;
            var row = new CareerRow { Id = 0, Icon = UiIcon.Briefcase, Group = 1, Cells = new[] { W.T("reallife_own_practice", "Your own practice"), "-", "-", "-" }, Detail = W.T("reallife_own_practice_about", "Work for yourself: a practice of your profession, or a company of your own.") };
            if (w.Practice.Open)
            {
                row.Cells[1] = CareersText.ProName(w.Practice.Prof);
                row.Cells[2] = w.Practice.Reputation.ToString(Inv);
                row.Acts.Add(CareerAct.PracticeClose); row.Why.Add(string.Empty);
            }
            else if (w.Job != null)
            {
                row.Acts.Add(CareerAct.PracticeOpen); row.Why.Add(string.Empty);
            }
            if (j != null)
            {
                row.Acts.Add(CareerAct.Found); row.Why.Add(d.CanFoundLeave(j) == null ? string.Empty : W.T(d.CanFoundLeave(j), "You cannot found a company from this job."));
                row.Acts.Add(CareerAct.FoundWith); row.Why.Add(d.CanFoundLeave(j) == null && d.ColleaguesFirm() != null ? string.Empty : W.T("reallife_work_no_colleagues", "No colleagues would come with you."));
                if (d.CanPracticeLeave(j) == null)
                {
                    row.Acts.Add(CareerAct.PracticeLeaveAct); row.Why.Add(string.Empty);
                }
            }
            if (d.ColleaguesFirm() != null)
            {
                row.Acts.Add(CareerAct.BringColleagues); row.Why.Add(string.Empty);
            }
            _facts.Own.Insert(0, row);
        }

        // Retiring is on the CV page, where the working life is told: a row of its own with the button, disabled with its reason until it can be done.
        private void ReadRetire(CareersDirector d)
        {
            string why = d.CanRetire();
            var r = new CareerRow { Id = 0, Icon = UiIcon.SignOut, Cells = new[] { W.T("reallife_life_button_retire", "Retire"), "-", "-", "-" }, Detail = W.T("reallife_work_retire_about", "Retire and the pension pays out from your pot.") };
            r.Acts.Add(CareerAct.Retire);
            r.Why.Add(why == null ? string.Empty : W.T(why, "You cannot retire yet."));
            _facts.Cv.Add(r);
        }

        private bool CarryMore(CareerAct act, int arg, CareersDirector d)
        {
            WorkFile w = d.Work;
            Job j = w != null ? w.Job : null;
            switch (act)
            {
                case CareerAct.Declare: return Said(d.Declare(), d);
                case CareerAct.AnswerA: return Said(d.AnswerMatter(true), d);
                case CareerAct.AnswerB: return Said(d.AnswerMatter(false), d);
                case CareerAct.WalkOut:
                    GameConfirm.Show("reallife_career_confirm_walk_title".Localize(), "reallife_career_confirm_walk_body".Localize(), () => Said(d.WalkOut(), d), null, null, null, true, true);
                    return true;
                case CareerAct.Retire:
                    GameConfirm.Show("reallife_life_confirm_retire_title".Localize(), "reallife_life_confirm_retire_body".Localize(), () => Said(d.Retire(), d), null, null, null, true, true);
                    return true;
                case CareerAct.PolicyMore: return Said(d.LadderPolicy((PolicyField)arg, 1), d);
                case CareerAct.PolicyLess: return Said(d.LadderPolicy((PolicyField)arg, -1), d);
                case CareerAct.AskShiftEarlier: return Said(d.LadderAsk(StaffWant.Shift, -1), d);
                case CareerAct.AskShiftLater: return Said(d.LadderAsk(StaffWant.Shift, 1), d);
                case CareerAct.AskEquipment: return Said(d.LadderAsk(StaffWant.Equipment, 0), d);
                case CareerAct.AskInsurance: return Said(d.LadderAsk(StaffWant.Insurance, 0), d);
                case CareerAct.Aiming:
                    return Said(d.LadderAiming(d.Ladder == null || !d.Ladder.Aiming), d);
                case CareerAct.RotaLean: return Said(d.LadderRota(0), d);
                case CareerAct.RotaStandard: return Said(d.LadderRota(1), d);
                case CareerAct.RotaFull: return Said(d.LadderRota(2), d);
                case CareerAct.Raise: return Said(d.LadderRaise(arg), d);
                case CareerAct.PracticeOpen: return Said(d.OpenPracticeNow(j != null ? j.Post.Prof : 0), d);
                case CareerAct.PracticeClose: return Said(d.ClosePractice(), d);
                case CareerAct.Found: return Said(d.FoundLeave(false), d);
                case CareerAct.FoundWith: return Said(d.FoundLeave(true), d);
                case CareerAct.PracticeLeaveAct: return Said(d.PracticeLeave(), d);
                case CareerAct.BringColleagues: return Said(d.BringColleagues(), d);
                default: return false;
            }
        }
    }
}
