using System;
using System.Collections.Generic;
using Localizor;
using RealLifeOverhaul.Careers;
using RealLifeOverhaul.Tech;

namespace RealLifeOverhaul.Ui
{
    using Job = RealLifeOverhaul.Careers.Job;

    // The parts of Career that are not the vacancies: the management ladder (decisions with their candidates, the position, policy, requests, staff and
    // stores), the practice and founding of Own work, the CV and what the job lets the player answer (a matter, a conflict, resigning, walking out,
    // retiring). Built for the view each time (the choices a player stepped are in it), from the director's own records.
    public sealed partial class CareerLive
    {
        private static readonly string[] RotaKeys = { "reallife_ladder_rota_0", "reallife_ladder_rota_1", "reallife_ladder_rota_2" };
        private const int TasksShown = 3;
        private readonly List<int> _practices = new List<int>(8);

        private WorkTab ManagementTab(CareersDirector d, WorkView view, int day)
        {
            UtilitiesWords words = W;
            var t = new WorkTab { Shape = WorkShape.Form };
            LadderFile f = d.Ladder;
            var form = new WorkFormModel();
            t.Form = form;
            if (f == null)
            {
                form.Lead = words.T("reallife_ladder_empty", "Nothing here yet.");
                return t;
            }
            CityCompany c = f.Company;
            form.Lead = CareersDirector.LadderTitle(f) + ". " + CareersText.Fill("reallife_ladder_sub", "company", CareersText.Employer(c.Name), "stores", c.Stores.Count.ToString());
            // to decide: each task a tile, the pick it needs and its decisions
            var decide = new WorkFormBand { Title = words.T("reallife_work_group_decide", "To decide"), Count = f.Tasks.Count.ToString(Inv) };
            for (int i = 0; i < f.Tasks.Count && i < TasksShown; i++)
            {
                LadderTask task = f.Tasks[i];
                decide.Rows.Add(new WorkFormRow { Field = WorkField.Tile, Label = TaskTitle(f, task), Hint = StoreName(c, task.Store), Value = CareersText.Day(task.Lapse), Icon = UiIcon.People });
                int pick = TaskPick(d, f, task, i, view, t, decide);
                decide.Rows.Add(TaskButtons(f, task, words, pick >= 0 || pick == -2 ? i : 15, pick));
            }
            if (f.Tasks.Count == 0)
            {
                decide.Rows.Add(new WorkFormRow { Field = WorkField.Text, Value = words.T("reallife_work_nothing_to_decide",
                    "Nothing waits for your decision. Tasks come from the stores as their staff ask.") });
            }
            form.Left.Add(decide);
            // where you stand
            var stand = new WorkFormBand { Title = words.T("reallife_ladder_sec_position", "Your position") };
            CareerLadderRules.UnitFigures(f, day, out int staffing, out int morale, out int stores);
            stand.Rows.Add(new WorkFormRow { Field = WorkField.Label, Label = words.T("reallife_ladder_row_reports", "Reports to"), Value = d.LadderBoss(f) });
            stand.Rows.Add(new WorkFormRow { Field = WorkField.Bar, Bar = new WorkBar { Label = words.T("reallife_ladder_row_score", "Performance score"), Value = CareerLadderRules.ScoreOf(f) } });
            stand.Rows.Add(new WorkFormRow { Field = WorkField.Bar, Bar = new WorkBar { Label = words.T("reallife_ladder_row_staffing", "Staffing"), Value = Math.Min(100, staffing) } });
            stand.Rows.Add(new WorkFormRow { Field = WorkField.Bar, Bar = new WorkBar { Label = words.T("reallife_ladder_row_morale_avg", "Staff morale"), Value = morale } });
            for (int i = 0; i < LadderRanks.Listed.Length; i++)
            {
                RungPower power = LadderRanks.Listed[i];
                stand.Rows.Add(new WorkFormRow { Field = WorkField.Check, Label = CareersText.T(LadderRanks.PowerKey(power)), On = LadderRanks.Has(f.Rung, power) });
            }
            form.Left.Add(stand);
            NextRungBand(d, f, form, words, day);
            RequestsBand(d, f, form, words);
            if (LadderRanks.Has(f.Rung, RungPower.Rota) && f.Rung == (int)Rung.StoreManager && c.Stores.Count > 0)
            {
                var rota = new WorkFormBand { Title = words.T("reallife_ladder_sec_rota", "The store's schedule") };
                for (int level = 0; level < 3; level++)
                {
                    rota.Rows.Add(new WorkFormRow { Field = WorkField.Button, Label = string.Empty,
                        Act = Act(level == 0 ? CareerAct.RotaLean : level == 1 ? CareerAct.RotaStandard : CareerAct.RotaFull, 0,
                        CareersText.T(RotaKeys[level]), UiButtonKind.Secondary, UiIcon.Clock, false, c.Stores[0].Rota == level ? words.T("reallife_work_already_set", "It is set so.") : null) });
                }
                form.Right.Add(rota);
            }
            if (f.Rung == (int)Rung.Executive)
            {
                form.Right.Add(PolicyBand(f, words));
            }
            var staff = new WorkFormBand { Title = words.T("reallife_work_group_staff", "Staff") };
            int people = 0;
            for (int i = 0; i < c.People.Count && people < 4; i++)
            {
                Colleague p = c.People[i];
                if (p.Role != (int)Rung.Frontline)
                {
                    continue;
                }
                people++;
                staff.Rows.Add(new WorkFormRow { Field = WorkField.Person, Person = new WorkPerson { Name = p.Name, Role = StoreName(c, p.Store) + ", " + CareersText.Rate(Prices.At(p.Wage)),
                    Right = words.F("reallife_work_morale_n", "Morale {n}", "n", p.Morale.ToString(Inv)), Ring = p.Morale < CareerLadderRules.LowMorale ? UiRole.Warn : UiRole.Ok },
                    Act = Act(CareerAct.Raise, p.Id, words.T("reallife_work_raise_their_pay", "Raise their pay"), UiButtonKind.Secondary, UiIcon.TrendUp, false,
                        LadderRanks.Has(f.Rung, RungPower.Raises) ? null : words.T("reallife_work_no_power_raises", "Your rung cannot give raises.")) });
            }
            staff.Count = people.ToString(Inv);
            if (people > 0)
            {
                form.Right.Add(staff);
            }
            var shops = new WorkFormBand { Title = words.T("reallife_work_group_stores", "Stores"), Count = c.Stores.Count.ToString(Inv) };
            for (int i = 0; i < c.Stores.Count && i < 4; i++)
            {
                Outlet st = c.Stores[i];
                shops.Rows.Add(new WorkFormRow { Field = WorkField.Tile, Label = st.Name, Hint = CareersText.T(RotaKeys[TalentCatalogue.Clamp(st.Rota,
                    0, 2)]) + ", " + CareerLadderRules.StaffAt(c, i).ToString(Inv)
                    + " / " + st.Needed.ToString(Inv), Value = words.F("reallife_work_results_n", "Results {n}", "n", st.Results.ToString(Inv)), Icon = UiIcon.Store });
            }
            if (c.Stores.Count > 0)
            {
                form.Right.Add(shops);
            }
            return t;
        }

        private static string TaskTitle(LadderFile f, LadderTask t)
        {
            Colleague p = f.Company.PersonOf(t.Person);
            string who = p != null ? p.Name : string.Empty;
            switch ((TaskKind)t.Kind)
            {
                case TaskKind.Demand:
                    return CareersText.Fill("reallife_ladder_task_demand", "name", who, "what", CareersDirector.WantText(t.About));
                case TaskKind.Escalation:
                    return CareersText.T(LadderRanks.Key("task_up", ((Concern)t.About).ToString().ToLowerInvariant()), "name", who);
                default:
                    return CareersText.T(LadderRanks.Key("task", ((TaskKind)t.Kind).ToString().ToLowerInvariant()));
            }
        }

        private static string StoreName(CityCompany c, int s)
        {
            return s >= 0 && s < c.Stores.Count ? CareersText.Fill("reallife_ladder_store_at", "name", c.Stores[s].Name, "place", WorkGame.Address(c.Stores[s].Street, c.Stores[s].Number))
                : CareersText.T("reallife_ladder_head_office");
        }

        // The people or candidates a task can be answered with, in the order the page lists them: the candidate's place, or the person's id.
        private static bool AsksPick(LadderTask t) => t.Kind == (int)TaskKind.Hire || t.Kind == (int)TaskKind.Cover || t.Kind == (int)TaskKind.Appoint
            || (t.Kind == (int)TaskKind.Escalation && t.About == (int)Concern.Gap);

        private static void PickList(CareersDirector d, LadderFile f, LadderTask t, List<string> words, List<int> ids)
        {
            CityCompany c = f.Company;
            if (t.Kind == (int)TaskKind.Hire)
            {
                for (int i = 0; i < CareerLadderRules.CandidatesEach; i++)
                {
                    CareerLadderRules.Candidate(d.LadderSeed, t.Id, i, out int skill, out int wage);
                    words.Add(CareersText.Fill("reallife_ladder_candidate", "name", d.LadderCandidateName(t.Id, i), "skill", skill.ToString(), "pay", CareersText.Rate(Prices.At(wage))));
                    ids.Add(i);
                }
                return;
            }
            for (int i = 0; i < c.People.Count && AsksPick(t); i++)
            {
                Colleague p = c.People[i];
                bool fits = t.Kind == (int)TaskKind.Escalation ? p.Store != t.Store && CareerLadderRules.InUnit(f, p.Store) && p.Role == (int)Rung.Frontline
                    : p.Store == t.Store && (t.Kind == (int)TaskKind.Cover || p.Role != (int)Rung.StoreManager);
                if (fits && words.Count < 12)
                {
                    words.Add(CareersText.Fill("reallife_ladder_pick_person", "name", p.Name, "store", c.Stores[p.Store].Name, "skill", p.Skill.ToString(), "morale", p.Morale.ToString()));
                    ids.Add(p.Id);
                }
            }
        }

        // A task that needs a person or a candidate picked: the choice is a selector the player steps, its place in the page's amounts the task's. Returns the
        // pick, -1 when the task asks none, -2 when it asks one and nobody can be picked.
        private int TaskPick(CareersDirector d, LadderFile f, LadderTask t, int slot, WorkView view, WorkTab tab, WorkFormBand band)
        {
            var words = new List<string>(8);
            var ids = new List<int>(8);
            PickList(d, f, t, words, ids);
            if (words.Count == 0)
            {
                return AsksPick(t) ? -2 : -1;
            }
            int at = Math.Max(0, Math.Min(words.Count - 1, view.Get(slot, 0)));
            tab.Amounts.Add(new WorkAmount { Id = slot, Value = at, Min = 0, Max = words.Count - 1, Words = words.ToArray(), Figure = words[at] });
            band.Rows.Add(new WorkFormRow { Field = WorkField.Stepper, Label = W.T("reallife_work_pick", "Pick"), Stepper = tab.Amounts.Count - 1 });
            return ids[at];
        }

        private WorkFormRow TaskButtons(LadderFile f, LadderTask t, UtilitiesWords words, int slot, int pick)
        {
            // the decision's own words (yes, no) come with the kind of task; a pick of -2 says nobody can be picked, so it cannot be agreed to
            string kind = ((TaskKind)t.Kind).ToString().ToLowerInvariant();
            string key = LadderRanks.Key("do", t.Kind == (int)TaskKind.Escalation ? kind + "_" + ((Concern)t.About).ToString().ToLowerInvariant() : kind);
            int arg = t.Id * 16 + (slot & 15);
            var row = new WorkFormRow { Field = WorkField.Button, Label = string.Empty, Act = Act(CareerAct.TaskYes, arg, CareersText.T(key + "_yes"), UiButtonKind.Primary, UiIcon.Check, false,
                pick == -2 ? words.T("reallife_ladder_nobody", "Nobody can be spared for this.") : null) };
            row.More.Add(Act(CareerAct.TaskNo, arg, CareersText.T(key + "_no"), UiButtonKind.Secondary, UiIcon.Close));
            if (LadderRanks.Has(f.Rung, RungPower.PassUp) && f.Rung < (int)Rung.Executive && t.Kind != (int)TaskKind.Appoint && !(t.Kind == (int)TaskKind.Cover && f.Rung >= (int)Rung.StoreManager))
            {
                row.More.Add(Act(CareerAct.TaskUp, arg, words.T("reallife_ladder_button_up", "Pass it up"), UiButtonKind.Secondary, UiIcon.Up));
            }
            return row;
        }

        // What the next rung asks, each a tick or not, and the player's name put forward or not.
        private void NextRungBand(CareersDirector d, LadderFile f, WorkFormModel form, UtilitiesWords words, int day)
        {
            if (f.Rung >= LadderRanks.CareerTop)
            {
                var top = new WorkFormBand { Title = words.T("reallife_ladder_sec_next", "The next rank") };
                top.Rows.Add(new WorkFormRow { Field = WorkField.Text, Value = CareersText.T("reallife_ladder_top") });
                form.Left.Add(top);
                return;
            }
            RungDef next = LadderRanks.Of(f.Rung + 1);
            ExecSeat seat = d.LadderSeatAimed(f);
            var band = new WorkFormBand { Title = CareersText.T("reallife_ladder_sec_next_to", "title", CareersText.T(LadderRanks.TitleKey(f.Rung + 1, seat))) };
            int need = d.LadderDaysNeeded(f), had = day - f.RungDay;
            band.Rows.Add(new WorkFormRow { Field = WorkField.Check, Label = CareersText.T("reallife_ladder_need_days", "have", Math.Max(0,
                had).ToString(), "need", need.ToString()), On = had >= need });
            band.Rows.Add(new WorkFormRow { Field = WorkField.Check, Label = CareersText.T("reallife_ladder_need_score", "have",
                CareerLadderRules.ScoreOf(f).ToString(), "need", next.Score.ToString()),
                On = CareerLadderRules.ScoreOf(f) >= next.Score });
            d.LadderSkills(f, out int have, out int skills);
            LadderRanks.SkillsOf(f.Rung + 1, seat, out int a, out int b);
            band.Rows.Add(new WorkFormRow { Field = WorkField.Check, Label = CareersText.Fill("reallife_ladder_need_skills", "a",
                CareersText.Skill(a), "b", CareersText.Skill(b), "have", have.ToString(),
                "need", skills.ToString()), On = have >= skills });
            band.Rows.Add(new WorkFormRow { Field = WorkField.Check,
                Label = CareersText.T(next.Recommend ? "reallife_ladder_need_recommend" : "reallife_ladder_need_recommend_optional", "boss", d.LadderBoss(f)),
                On = f.Recommended || !next.Recommend });
            band.Rows.Add(new WorkFormRow { Field = WorkField.Check, Label = CareersText.T("reallife_ladder_need_stores", "have", f.Company.Stores.Count.ToString(), "need", next.Stores.ToString()),
                On = f.Company.Stores.Count >= next.Stores });
            band.Rows.Add(new WorkFormRow { Field = WorkField.Switch, Label = words.T("reallife_ladder_aiming", "Put my name forward"), On = f.Aiming,
                Act = Act(CareerAct.Aiming, f.Aiming ? 0 : 1, words.T("reallife_ladder_aiming", "Put my name forward"), UiButtonKind.Secondary, UiIcon.Check) });
            form.Left.Add(band);
        }

        private void RequestsBand(CareersDirector d, LadderFile f, WorkFormModel form, UtilitiesWords words)
        {
            var band = new WorkFormBand { Title = words.T("reallife_ladder_sec_requests", "Your requests") };
            if (f.Ask != (int)StaffWant.None)
            {
                band.Rows.Add(new WorkFormRow { Field = WorkField.Text, Value = CareersText.Fill("reallife_ladder_asked_line", "what", CareersDirector.WantText(f.Ask), "boss", d.LadderBoss(f)) });
            }
            string waiting = f.Ask != 0 ? words.T("reallife_work_already_asked", "You have asked for something already.") : null;
            if (!LadderRanks.Has(f.Rung, RungPower.Requests))
            {
                waiting = words.T("reallife_work_no_power_requests", "Your rung makes no requests.");
            }
            string granted = words.T("reallife_work_granted", "Already granted.");
            var shift = new WorkFormRow { Field = WorkField.Button, Label = words.T("reallife_work_ask_shift", "Your shift"), Act = Act(CareerAct.AskShiftEarlier, 0,
                words.T("reallife_work_ask_shift_earlier", "Ask for it earlier"), UiButtonKind.Secondary, UiIcon.Back, false, f.Prefer <= -2 ? granted : waiting) };
            shift.More.Add(Act(CareerAct.AskShiftLater, 0, words.T("reallife_work_ask_shift_later", "Ask for it later"), UiButtonKind.Secondary,
                UiIcon.Next, false, f.Prefer >= 2 ? granted : waiting));
            band.Rows.Add(shift);
            band.Rows.Add(new WorkFormRow { Field = WorkField.Button, Label = words.T("reallife_work_ask_equipment_label", "Equipment"), Act = Act(CareerAct.AskEquipment, 0,
                words.T("reallife_work_ask_equipment", "Ask for equipment"), UiButtonKind.Secondary, UiIcon.Wrench, false, f.Equipped ? granted : waiting) });
            band.Rows.Add(new WorkFormRow { Field = WorkField.Button, Label = words.T("reallife_work_ask_insurance_label", "Insurance"), Act = Act(CareerAct.AskInsurance, 0,
                words.T("reallife_work_ask_insurance", "Ask for insurance"), UiButtonKind.Secondary, UiIcon.ShieldCheck, false, f.Insured ? granted : waiting) });
            form.Right.Add(band);
        }

        private static WorkFormBand PolicyBand(LadderFile f, UtilitiesWords words)
        {
            CompanyPolicy p = f.Company.Policy;
            var band = new WorkFormBand { Title = words.T("reallife_ladder_sec_policy", "The company's policy"), Note = CareersText.T("reallife_ladder_policy_about") };
            for (int i = 0; i <= (int)PolicyField.Budget; i++)
            {
                var field = (PolicyField)i;
                CareerLadderRules.Range(p, field, out int value, out int least, out int most, out int step);
                bool mine = (int)CareerLadderRules.SeatOf(field) == f.Seat;
                string name = CareersText.T(LadderRanks.Key("policy", field.ToString().ToLowerInvariant()));
                if (!mine)
                {
                    band.Rows.Add(new WorkFormRow { Field = WorkField.Label, Label = name, Value = PolicyValue(field, value) });
                    continue;
                }
                band.Rows.Add(new WorkFormRow { Field = WorkField.Nudge, Label = name, Value = PolicyValue(field, value),
                    Act = Act(CareerAct.PolicyLess, i, words.T("reallife_ladder_button_less", "Lower"), UiButtonKind.Secondary, UiIcon.Minus, false,
                        value > least ? null : words.T("reallife_work_at_least", "It is at its least.")),
                    Act2 = Act(CareerAct.PolicyMore, i, words.T("reallife_ladder_button_more", "Raise"), UiButtonKind.Secondary, UiIcon.Plus, false,
                        value < most ? null : words.T("reallife_work_at_most", "It is at its most.")) });
            }
            return band;
        }

        private static string PolicyValue(PolicyField field, int value)
        {
            switch (field)
            {
                case PolicyField.Staffing: return CareersText.T("reallife" + "_ladder_rota_" + TalentCatalogue.Clamp(value, 0, 2));
                case PolicyField.Escalate: return CareersText.T("reallife_ladder_days", "days", value.ToString());
                case PolicyField.Floor: return value.ToString();
                case PolicyField.Band: return (value / 100f).ToString("0.#", Inv) + "%";
                case PolicyField.WageTop: return CareersText.Rate(Prices.At(value));
                default: return CareersText.Money(Prices.At(value));
            }
        }

        // What the job lets the player answer: a matter and a conflict.
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

        // ------------------------------------------------------------------------------------------------- Own work

        private WorkTab OwnTab(CareersDirector d, WorkFile w, StudyFile f, WorkView view, int day)
        {
            UtilitiesWords words = W;
            var t = new WorkTab { Shape = WorkShape.Form };
            var form = new WorkFormModel { Lead = words.T("reallife_own_practice_about", "Work for yourself: a practice of your profession, or a company of your own.") };
            t.Form = form;
            if (!CareersDirector.OwnOn)
            {
                form.Lead = words.T("reallife_own_off", "Working for yourself is switched off on the options page.");
                return t;
            }
            Job j = w.Job;
            var practice = new WorkFormBand { Title = words.T("reallife_own_practice", "Your own practice") };
            if (w.Practice.Open)
            {
                practice.Rows.Add(new WorkFormRow { Field = WorkField.Label, Label = words.T("reallife_career_col_profession", "Profession"), Value = CareersText.ProName(w.Practice.Prof) });
                practice.Rows.Add(new WorkFormRow { Field = WorkField.Label, Label = words.T("reallife_ibox_since", "Since"), Value = CareersText.Day(w.Practice.Opened) });
                practice.Rows.Add(new WorkFormRow { Field = WorkField.Bar, Bar = new WorkBar { Label = words.T("reallife_ibox_name", "Name"),
                    Value = Math.Max(0, Math.Min(100, w.Practice.Reputation)) } });
                practice.Foot = Act(CareerAct.PracticeClose, 0, words.T("reallife_own_button_close_practice", "Close the practice"), UiButtonKind.Destructive, UiIcon.Close);
            }
            else
            {
                _practices.Clear();
                for (int i = 0; i < ProCatalogue.All.Length; i++)
                {
                    if (OwnRules.CanPractise(ProCatalogue.All[i], f))
                    {
                        _practices.Add(i);
                    }
                }
                if (_practices.Count == 0)
                {
                    practice.Rows.Add(new WorkFormRow { Field = WorkField.Text, Value = CareersText.T("reallife_own_practice_none") });
                }
                else
                {
                    int at = Math.Max(0, Math.Min(_practices.Count - 1, view.Get(0, 0)));
                    var names = new string[_practices.Count];
                    for (int i = 0; i < names.Length; i++)
                    {
                        names[i] = CareersText.ProName(_practices[i]);
                    }
                    t.Amounts.Add(new WorkAmount { Id = 0, Value = at, Min = 0, Max = names.Length - 1, Words = names, Figure = names[at] });
                    practice.Rows.Add(new WorkFormRow { Field = WorkField.Stepper, Label = words.T("reallife_career_col_profession", "Profession"), Stepper = 0 });
                    practice.Rows.Add(new WorkFormRow { Field = WorkField.Label, Label = words.T("reallife_own_registration", "Registration"),
                        Value = CareersText.Money(Prices.At(OwnRules.RegisterFee(ProCatalogue.Find(_practices[at])))) });
                    practice.Foot = Act(CareerAct.PracticeOpen, _practices[at], words.T("reallife_own_button_open_practice", "Open the practice"), UiButtonKind.Primary, UiIcon.Check);
                }
            }
            form.Left.Add(practice);
            var leave = new WorkFormBand { Title = words.T("reallife_own_found_title", "Found a company") };
            if (w.FounderDay > 0)
            {
                leave.Rows.Add(new WorkFormRow { Field = WorkField.Text, Value = CareersText.Fill("reallife_own_founder_line", "employer",
                    CareersText.Employer(w.FounderFrom), "day", CareersText.Day(w.FounderDay),
                    "bonus", (d.FounderBonus() / 10).ToString() + "%") });
            }
            if (w.ColleaguesCount > 0 && w.ColleaguesUntil >= day)
            {
                leave.Rows.Add(new WorkFormRow { Field = WorkField.Text, Value = CareersText.T("reallife_own_colleagues_line", "count",
                    w.ColleaguesCount.ToString(), "day", CareersText.Day(w.ColleaguesUntil)) });
                leave.Rows.Add(new WorkFormRow { Field = WorkField.Button, Label = words.T("reallife_own_colleagues_label", "Colleagues waiting"), Act = Act(CareerAct.BringColleagues, 0,
                    words.T("reallife_own_button_colleagues", "Bring your former colleagues"), UiButtonKind.Secondary, UiIcon.People, false,
                    d.ColleaguesFirm() != null ? null : CareersText.T("reallife_own_colleagues_no_firm")) });
            }
            if (w.RestraintUntil >= day)
            {
                leave.Rows.Add(new WorkFormRow { Field = WorkField.Text, Value = CareersText.T(w.ClaimNumber > 0 ? "reallife_own_restraint_claimed" : "reallife_own_restraint_line",
                    "employer", CareersText.Employer(w.RestraintEmployer), "day", CareersText.Day(w.RestraintUntil)) });
            }
            if (j != null && j.NoticeEnd <= 0)
            {
                string why = d.CanFoundLeave(j);
                leave.Rows.Add(new WorkFormRow { Field = WorkField.Button, Label = words.T("reallife_own_alone_label", "On your own"),
                    Act = Act(CareerAct.Found, 0, words.T("reallife_own_button_found", "Leave to found a company"),
                    UiButtonKind.Secondary, UiIcon.Building, false, why == null ? null : words.T(why, "You cannot found a company from this job.")) });
                leave.Rows.Add(new WorkFormRow { Field = WorkField.Button, Label = words.T("reallife_own_with_label", "With colleagues"), Act = Act(CareerAct.FoundWith, 0,
                    words.T("reallife_own_button_found_colleagues", "Leave to found one, taking colleagues"), UiButtonKind.Secondary, UiIcon.People,
                    false, why == null && d.ColleaguesFirm() != null ? null : words.T("reallife_work_no_colleagues", "No colleagues would come with you.")) });
                if (d.CanPracticeLeave(j) == null)
                {
                    leave.Rows.Add(new WorkFormRow { Field = WorkField.Button, Label = words.T("reallife_own_practice_leave_label", "Leave for your practice"), Act = Act(CareerAct.PracticeLeaveAct, 0,
                        words.T("reallife_own_button_practice_leave", "Leave to open a practice"), UiButtonKind.Secondary, UiIcon.SignOut) });
                }
            }
            form.Left.Add(leave);
            var contracts = new WorkFormBand { Title = words.T("reallife_own_contracts", "Contracts"), Count = w.Contracts.Count.ToString(Inv) };
            for (int i = w.Contracts.Count - 1; i >= 0 && contracts.Rows.Count < 8; i--)
            {
                RealLifeOverhaul.Careers.Contract c = w.Contracts[i];
                contracts.Rows.Add(new WorkFormRow { Field = WorkField.Tile, Label = c.Client,
                    Hint = CareersText.ProName(c.Prof) + ", " + CareersText.ContractState(c.At), Value = CareersText.Money(Prices.At((int)c.Fee)),
                    Icon = UiIcon.Wrench });
                if (c.Stage == (int)ContractStep.Offered)
                {
                    var row = new WorkFormRow { Field = WorkField.Button, Label = words.F("reallife_work_due_day", "Due {day}", "day", CareersText.Day(c.Due)), Act = Act(CareerAct.TakeContract, c.Id,
                        words.T("reallife_own_button_take", "Take it on"), UiButtonKind.Primary, UiIcon.Check) };
                    row.More.Add(Act(CareerAct.DropContract, c.Id, words.T("reallife_career_button_decline", "Turn it down"), UiButtonKind.Secondary, UiIcon.Close));
                    contracts.Rows.Add(row);
                }
                else if (c.Stage == (int)ContractStep.Taken)
                {
                    contracts.Rows.Add(new WorkFormRow { Field = WorkField.Button, Label = words.F("reallife_work_due_day", "Due {day}", "day", CareersText.Day(c.Due)),
                        Act = Act(CareerAct.DropContract, c.Id, words.T("reallife_own_button_drop", "Drop it"), UiButtonKind.Destructive, UiIcon.Close) });
                }
            }
            if (w.Contracts.Count == 0)
            {
                contracts.Rows.Add(new WorkFormRow { Field = WorkField.Text, Value = words.T("reallife_work_no_contracts",
                    "No contract is on offer. Clients ask once you have a profession to offer.") });
            }
            form.Right.Add(contracts);
            OwnFoot(w, form);
            return t;
        }

        // Own work's record: the contracts finished, what they earned and the practice's name, and the last three finished.
        private static void OwnFoot(WorkFile w, WorkFormModel form)
        {
            var clients = new List<string>(3);
            var fees = new List<string>(3);
            var days = new List<string>(3);
            int finished = 0;
            long earned = 0;
            for (int i = w.Contracts.Count - 1; i >= 0; i--)
            {
                RealLifeOverhaul.Careers.Contract c = w.Contracts[i];
                if (c.At != ContractStep.Done)
                {
                    continue;
                }
                finished++;
                earned += Prices.At((int)c.Fee);
                if (clients.Count < 3)
                {
                    clients.Add(c.Client);
                    fees.Add(CareersText.Money(Prices.At((int)c.Fee)));
                    days.Add(CareersText.Day(c.Due));
                }
            }
            if (finished > 0 || w.Practice.Open)
            {
                CareerPages.OwnBands(Words(), form, finished, (int)Math.Min(int.MaxValue, earned), w.Practice.Open ? Math.Max(0, Math.Min(100, w.Practice.Reputation)) : 0,
                    clients.ToArray(), fees.ToArray(), days.ToArray());
            }
        }

        // ------------------------------------------------------------------------------------------------- the CV

        private WorkTab CvTab(CareersDirector d, WorkFile w, StudyFile study)
        {
            UtilitiesWords words = W;
            var t = new WorkTab { Shape = WorkShape.Form };
            var spells = new List<CareerSpell>(6);
            if (w.Job != null)
            {
                spells.Add(new CareerSpell { Title = CareersText.Title(w.Job.Post.Prof, w.Job.Post.Rank), Employer = CareersText.Employer(w.Job.Post.Employer),
                    Dates = CareersText.Day(w.Job.RankDay > 0 ? w.Job.RankDay : w.Job.Hired) + " " + words.T("reallife_work_cv_to_now", "to now"), Icon = WorkGlyphs.Profession(w.Job.Post.Prof), Current = true });
            }
            int good = 0;
            for (int i = w.History.Count - 1; i >= 0 && spells.Count < 6; i--)
            {
                Spell s = w.History[i];
                good += s.Reference >= 3 ? 1 : 0;
                spells.Add(new CareerSpell { Title = CareersText.Title(s.Prof, s.Top), Employer = CareersText.Employer(s.Employer),
                    Dates = CareersText.Day(s.From) + " " + words.T("reallife_work_cv_to", "to") + " " + CareersText.Day(s.To),
                    Note = CareersText.Fill("reallife_career_row_spell", "leaving", CareersText.Leaving(s.Leaving), "reference", CareersText.Reference(s.Reference)),
                    Icon = WorkGlyphs.Profession(s.Prof), Tone = s.Reference >= 3 ? UiRole.Ok : UiRole.Warn });
            }
            var quals = new List<CareerSpell>(5);
            for (int i = 0; i < study.Results.Count && quals.Count < 5; i++)
            {
                Enrolment e = study.Results[i];
                if (e.Def != null && e.Stage == (int)EnrolStage.Passed)
                {
                    quals.Add(new CareerSpell { Title = CareersText.Course(e.Course), Employer = CareersText.Day(e.Ended), Dates = CareersText.Grade(e.Def, e.Grade), Icon = WorkGlyphs.Course(e.Def) });
                }
            }
            var skills = new List<string>(4);
            var values = new List<int>(4);
            var order = new List<int>(Skills.Count);
            for (int s = 0; s < Skills.Count; s++)
            {
                if (StudyRules.Points(study, s) > 0)
                {
                    order.Add(s);
                }
            }
            order.Sort((a, b) => StudyRules.Points(study, b).CompareTo(StudyRules.Points(study, a)));
            for (int i = 0; i < order.Count && i < 4; i++)
            {
                skills.Add(CareersText.Skill(order[i]));
                values.Add(Math.Min(100, StudyRules.Points(study, order[i])));
            }
            int jobs = (w.Job != null ? 1 : 0) + w.History.Count;
            long earned = w.Job != null ? w.Job.Earned : 0;
            int days = w.Job != null ? Math.Max(0, SaveGameManager.Current.Day - w.Job.Hired) : 0;
            for (int i = 0; i < w.History.Count; i++)
            {
                earned += w.History[i].Earned;
                days += Math.Max(0, w.History[i].To - w.History[i].From);
            }
            string why = d.CanRetire();
            _facts.CanRetire = why == null;
            CareerPages.CvPage(words, t, _facts.Player, w.Job != null ? CareersText.Title(w.Job.Post.Prof, w.Job.Post.Rank) : null, w.Job != null ? CareersText.Employer(w.Job.Post.Employer) : null,
                spells, quals, jobs, days / 30, CareersText.Cents(earned), good, CareersText.Cents(w.Pension), CareersGame.AgeYears(), LifeRules.RetireEarlyAge, skills.ToArray(), values.ToArray(),
                why == null ? null : words.T(why, "You cannot retire yet."), w.Retired > 0);
            return t;
        }

        // ------------------------------------------------------------------------------------------------- doing

        private bool CarryMore(CareerAct act, int arg, CareersDirector d, WorkView view)
        {
            switch (act)
            {
                case CareerAct.Declare: return Said(d.Declare(), d);
                case CareerAct.AnswerA: return Said(d.AnswerMatter(true), d);
                case CareerAct.AnswerB: return Said(d.AnswerMatter(false), d);
                case CareerAct.WalkOut:
                    GameConfirm.Show("reallife_career_confirm_walk_title".Localize(), "reallife_career_confirm_walk_body".Localize(),
                        () => Said(d.WalkOut(), d), null, null, null, true, true);
                    return true;
                case CareerAct.Retire:
                    GameConfirm.Show("reallife_life_confirm_retire_title".Localize(), "reallife_life_confirm_retire_body".Localize(),
                        () => Said(d.Retire(), d), null, null, null, true, true);
                    return true;
                case CareerAct.PolicyMore: return Said(d.LadderPolicy((PolicyField)arg, 1), d);
                case CareerAct.PolicyLess: return Said(d.LadderPolicy((PolicyField)arg, -1), d);
                case CareerAct.AskShiftEarlier: return Said(d.LadderAsk(StaffWant.Shift, -1), d);
                case CareerAct.AskShiftLater: return Said(d.LadderAsk(StaffWant.Shift, 1), d);
                case CareerAct.AskEquipment: return Said(d.LadderAsk(StaffWant.Equipment, 0), d);
                case CareerAct.AskInsurance: return Said(d.LadderAsk(StaffWant.Insurance, 0), d);
                case CareerAct.Aiming: return Said(d.LadderAiming(arg == 1), d);
                case CareerAct.RotaLean: return Said(d.LadderRota(0), d);
                case CareerAct.RotaStandard: return Said(d.LadderRota(1), d);
                case CareerAct.RotaFull: return Said(d.LadderRota(2), d);
                case CareerAct.Raise: return Said(d.LadderRaise(arg), d);
                case CareerAct.TaskYes: return Said(d.LadderAnswer(arg >> 4, Verdict.Yes, PickOf(d, arg, view)), d);
                case CareerAct.TaskNo: return Said(d.LadderAnswer(arg >> 4, Verdict.No, PickOf(d, arg, view)), d);
                case CareerAct.TaskUp: return Said(d.LadderAnswer(arg >> 4, Verdict.Up, PickOf(d, arg, view)), d);
                case CareerAct.PracticeOpen: return Said(d.OpenPracticeNow(arg), d);
                case CareerAct.PracticeClose: return Said(d.ClosePractice(), d);
                case CareerAct.Found: Confirm(d, "reallife_own_confirm_found_body", () => d.FoundLeave(false)); return true;
                case CareerAct.FoundWith: Confirm(d, "reallife_own_confirm_colleagues_body", () => d.FoundLeave(true)); return true;
                case CareerAct.PracticeLeaveAct: Confirm(d, "reallife_own_confirm_practice_body", d.PracticeLeave); return true;
                case CareerAct.BringColleagues: return Said(d.BringColleagues(), d);
                default: return false;
            }
        }

        // The decision of a task with the pick the player stepped: the candidate's place or the person's id, in the order the page listed them.
        private int PickOf(CareersDirector d, int arg, WorkView view)
        {
            LadderFile f = d.Ladder;
            LadderTask t = f != null ? f.Tasks.Find(x => x.Id == (arg >> 4)) : null;
            if (t == null)
            {
                return -1;
            }
            var words = new List<string>(8);
            var ids = new List<int>(8);
            PickList(d, f, t, words, ids);
            if (ids.Count == 0)
            {
                return -1;
            }
            int slot = arg & 15, at = Math.Max(0, Math.Min(ids.Count - 1, view.Get(slot, 0)));
            return ids[at];
        }

        // Leaving a job to work for oneself is asked first, in the game's own question box, as the old page asked it.
        private void Confirm(CareersDirector d, string bodyKey, Func<string> act)
        {
            Job j = d.Work != null ? d.Work.Job : null;
            string employer = j != null ? CareersText.Employer(j.Post.Employer) : string.Empty;
            GameConfirm.Show("reallife_own_confirm_title".Localize(new { employer }), bodyKey.Localize(new { days = j != null ? ProRules.NoticeDays(j.Rank, j.Post.Rank).ToString() : "0" }),
                () => Said(act(), d), null, null, null, true, true);
        }
    }
}
