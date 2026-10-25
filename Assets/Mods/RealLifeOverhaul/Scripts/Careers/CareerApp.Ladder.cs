using System.Collections.Generic;
using RealLifeOverhaul.GameUi;
using RealLifeOverhaul.Tech;
using UnityEngine;

namespace RealLifeOverhaul.Careers
{
    /// <summary>
    /// The Career app's Management tab: the player's place on the ladder at the city company their job is with, the exact
    /// powers of it, the performance score and what the next rung asks; the tasks waiting, the staff and the stores of the
    /// player's unit, each a row whose box holds the decisions the rung may take. Written only when the app opens or changes.
    /// </summary>
    public sealed partial class CareerApp
    {
        public const string TaskRow = "task:", PersonRow = "who:", StoreRow = "store:";
        private const string LadderGuide = "reallife_meta_title_careers_ladder";
        private static readonly string[] RotaKeys = { "reallife_ladder_rota_0", "reallife_ladder_rota_1", "reallife_ladder_rota_2" };

        private int _ladderPick, _ladderTo, _ladderPickOf = -1;
        private readonly List<Colleague> _picks = new List<Colleague>(CareerLadderRules.StaffMost * 2);

        private TableSpec WriteLadder(InfoBox box, WorkFile w, int day)
        {
            LadderFile f = _director.Ladder;
            if (f == null)
            {
                _picked = null;
                box.Title(CareersText.T("reallife_ladder_title"));
                box.Note(CareersText.T(!Settings.CareersLadder ? "reallife_ladder_off" : w.Job == null ? "reallife_ladder_no_job" : "reallife_ladder_not_here"));
                box.Note(CareersText.T("reallife_ladder_about"));
                Meta.Guide.Point(box, LadderGuide);
                return CareerColumns.Ladder;
            }
            CityCompany c = f.Company;
            LadderTask pickedTask = null;
            Colleague pickedPerson = null;
            int pickedStore = -1;
            int taskId = PickedId(_picked, TaskRow), personId = PickedId(_picked, PersonRow), storeId = PickedId(_picked, StoreRow);
            for (int i = 0; i < f.Tasks.Count; i++)
            {
                LadderTask t = f.Tasks[i];
                TableRow row = Row(TaskRow + t.Id);
                row.Kind = CareerColumns.LadderTasks;
                row.Set(0, TaskTitle(f, t));
                row.Set(1, StoreName(c, t.Store));
                Colleague who = c.PersonOf(t.Person);
                row.Set(2, who != null ? who.Name : "-");
                row.Set(3, CareersText.T("reallife_ladder_kind_" + ((TaskKind)t.Kind).ToString().ToLowerInvariant()));
                row.Set(4, CareersText.Day(t.Lapse), t.Lapse);
                row.Set(5, CareersText.T(t.Lapse <= day ? "reallife_ladder_state_today" : "reallife_ladder_state_waiting"), t.Lapse <= day ? TableRow.Yellow : TableRow.Plain);
                pickedTask = t.Id == taskId ? t : pickedTask;
            }
            for (int i = 0; i < c.People.Count; i++)
            {
                Colleague p = c.People[i];
                if (!CareerLadderRules.InUnit(f, p.Store))
                {
                    continue;
                }
                TableRow row = Row(PersonRow + p.Id);
                row.Kind = CareerColumns.LadderStaff;
                row.Set(0, p.Name);
                row.Set(1, StoreName(c, p.Store));
                row.Set(2, CareersText.T(LadderRanks.TitleKey(p.Role, (ExecSeat)p.Seat)));
                row.Set(3, CareersText.T("reallife_ladder_row_skill", "skill", p.Skill.ToString()), p.Skill);
                row.Set(4, CareersText.Rate(Prices.At(p.Wage)), p.Wage);
                row.Set(5, CareersText.T("reallife_ladder_row_morale", "morale", p.Morale.ToString()), p.Morale < CareerLadderRules.LowMorale ? TableRow.Red : p.Morale >= 70 ? TableRow.Green : TableRow.Plain);
                pickedPerson = p.Id == personId ? p : pickedPerson;
            }
            for (int s = 0; s < c.Stores.Count && LadderRanks.Has(f.Rung, RungPower.Stores); s++)
            {
                if (!CareerLadderRules.InUnit(f, s))
                {
                    continue;
                }
                Outlet st = c.Stores[s];
                TableRow row = Row(StoreRow + s);
                row.Kind = CareerColumns.LadderStores;
                row.Set(0, st.Name);
                row.Set(1, WorkGame.Address(st.Street, st.Number));
                Colleague manager = CareerLadderRules.Holder(c, s, (int)Rung.StoreManager);
                row.Set(2, manager != null ? manager.Name : s == 0 && f.Rung == (int)Rung.StoreManager ? CareersText.T("reallife_ladder_you") : "-");
                row.Set(3, CareersText.T("reallife_ladder_row_staff", "staff", CareerLadderRules.StaffAt(c, s).ToString(), "wanted", CareerLadderRules.Wanted(st).ToString()), CareerLadderRules.StaffAt(c, s));
                row.Set(4, CareersText.T("reallife_ladder_row_morale", "morale", CareerLadderRules.MoraleAt(c, s).ToString()), CareerLadderRules.MoraleAt(c, s));
                row.Set(5, CareersText.T("reallife_ladder_row_results", "results", st.Results.ToString()), st.Results >= 70 ? TableRow.Green : st.Results < 45 ? TableRow.Red : TableRow.Plain);
                pickedStore = s == storeId ? s : pickedStore;
            }
            if (pickedTask != null)
            {
                WriteTask(box, f, pickedTask, day);
            }
            else if (pickedPerson != null)
            {
                WritePerson(box, f, pickedPerson);
            }
            else if (pickedStore >= 0)
            {
                WriteStore(box, f, pickedStore, day);
            }
            else
            {
                _picked = null;
                WritePosition(box, w, f, day);
            }
            return CareerColumns.Ladder;
        }

        private static string StoreName(CityCompany c, int s)
        {
            return s >= 0 && s < c.Stores.Count ? CareersText.Fill("reallife_ladder_store_at", "name", c.Stores[s].Name, "place", WorkGame.Address(c.Stores[s].Street, c.Stores[s].Number))
                : CareersText.T("reallife_ladder_head_office");
        }

        private static string TaskTitle(LadderFile f, LadderTask t)
        {
            Colleague p = f.Company.PersonOf(t.Person);
            string who = p != null ? p.Name : string.Empty;
            switch ((TaskKind)t.Kind)
            {
                case TaskKind.Demand:
                    return CareersText.Fill("reallife_ladder_task_demand", "name", who, "what", CareersText.T("reallife_ladder_want_" + ((StaffWant)t.About).ToString().ToLowerInvariant()));
                case TaskKind.Escalation:
                    return CareersText.T("reallife_ladder_task_up_" + ((Concern)t.About).ToString().ToLowerInvariant(), "name", who);
                default:
                    return CareersText.T("reallife_ladder_task_" + ((TaskKind)t.Kind).ToString().ToLowerInvariant());
            }
        }

        // The box with nothing picked: the position, its powers, the score, the next rung, the requests and the rung's own tools.
        private void WritePosition(InfoBox box, WorkFile w, LadderFile f, int day)
        {
            CityCompany c = f.Company;
            Job j = w.Job;
            box.Title(CareersDirector.LadderTitle(f));
            box.Subtitle(CareersText.Fill("reallife_ladder_sub", "company", CareersText.Employer(c.Name), "stores", c.Stores.Count.ToString()));
            box.Chip(CareersText.T("reallife_ladder_chip_score", "score", CareerLadderRules.ScoreOf(f).ToString()), CareerLadderRules.ScoreOf(f) >= 70 ? CellTone.Green : CareerLadderRules.ScoreOf(f) < 50 ? CellTone.Yellow : CellTone.Plain);
            box.Section("reallife_ladder_sec_position");
            box.Row("reallife_ladder_row_since", CareersText.Day(f.RungDay));
            box.Row("reallife_ladder_row_reports", _director.LadderBoss(f));
            box.Row("reallife_career_col_pay", CareersText.T("reallife_career_row_rate", "rate", CareersText.Rate(Prices.At(j.Pay))));
            box.Row("reallife_career_col_hours", CareersText.Week(j.Def));
            box.Row("reallife_ladder_row_unit", CareersText.T(f.Rung <= (int)Rung.StoreManager ? "reallife_ladder_unit_store" : f.Rung == (int)Rung.Regional ? "reallife_ladder_unit_region" : "reallife_ladder_unit_company"));
            box.Section("reallife_ladder_sec_powers");
            for (int i = 0; i < LadderRanks.Listed.Length; i++)
            {
                RungPower p = LadderRanks.Listed[i];
                box.Check(CareersText.T(LadderRanks.PowerKey(p)), LadderRanks.Has(f.Rung, p));
            }
            box.Section("reallife_ladder_sec_score");
            CareerLadderRules.UnitFigures(f, day, out int staffing, out int morale, out int stores);
            box.Meter("reallife_ladder_row_score", CareerLadderRules.ScoreOf(f) / 100f, CareersText.T("reallife_ibox_of", "value", CareerLadderRules.ScoreOf(f).ToString(), "most", "100"),
                CareerLadderRules.ScoreOf(f) >= 70 ? CellTone.Green : CareerLadderRules.ScoreOf(f) < 50 ? CellTone.Yellow : CellTone.Plain);
            box.Row("reallife_ladder_row_staffing", CareersText.Percent(staffing), staffing < 100 ? CellTone.Yellow : CellTone.Plain);
            box.Row("reallife_ladder_row_morale_avg", morale.ToString(), morale < CareerLadderRules.LowMorale ? CellTone.Red : CellTone.Plain);
            box.Row("reallife_ladder_row_decided", CareersText.T("reallife_ladder_decided", "done", f.Handled.ToString(), "lapsed", f.Lapsed.ToString()), f.Lapsed > 0 ? CellTone.Yellow : CellTone.Plain);
            int bonus = CareerLadderRules.ReviewBonus(f);
            box.Note(CareersText.T(f.Rung == (int)Rung.Frontline ? "reallife_ladder_score_floor" : "reallife_ladder_score_manager", "bonus", (bonus > 0 ? "+" : string.Empty) + bonus));
            WriteNext(box, f, day);
            WriteRequests(box, f);
            if (LadderRanks.Has(f.Rung, RungPower.Rota) && f.Rung == (int)Rung.StoreManager)
            {
                box.Section("reallife_ladder_sec_rota");
                Outlet st = c.Stores[0];
                box.Selector(3, st.Rota, i => CareersText.T(RotaKeys[i]), i => Do(() => _director.LadderRota(i)));
                box.Note(CareersText.T("reallife_ladder_rota_about", "most", CareersText.T(RotaKeys[c.Policy.Staffing])));
            }
            if (f.Rung == (int)Rung.Executive)
            {
                WritePolicy(box, f);
            }
            Meta.Guide.Point(box, LadderGuide);
        }

        // What the next rung asks for, each a tick or not, and the player's name put forward or not.
        private void WriteNext(InfoBox box, LadderFile f, int day)
        {
            if (f.Rung >= LadderRanks.CareerTop)
            {
                box.Section("reallife_ladder_sec_next");
                box.Line(CareersText.T("reallife_ladder_top"), CellTone.Green);
                return;
            }
            RungDef next = LadderRanks.Of(f.Rung + 1);
            ExecSeat seat = _director.LadderSeatAimed(f);
            box.SectionWords(CareersText.T("reallife_ladder_sec_next_to", "title", CareersText.T(LadderRanks.TitleKey(f.Rung + 1, seat))));
            int need = _director.LadderDaysNeeded(f), had = day - f.RungDay;
            box.Check(CareersText.T("reallife_ladder_need_days", "have", System.Math.Max(0, had).ToString(), "need", need.ToString()), had >= need);
            box.Check(CareersText.T("reallife_ladder_need_score", "have", CareerLadderRules.ScoreOf(f).ToString(), "need", next.Score.ToString()), CareerLadderRules.ScoreOf(f) >= next.Score);
            _director.LadderSkills(f, out int have, out int skills);
            LadderRanks.SkillsOf(f.Rung + 1, seat, out int a, out int b);
            box.Check(CareersText.Fill("reallife_ladder_need_skills", "a", CareersText.Skill(a), "b", CareersText.Skill(b), "have", have.ToString(), "need", skills.ToString()), have >= skills);
            box.Check(CareersText.T(next.Recommend ? "reallife_ladder_need_recommend" : "reallife_ladder_need_recommend_optional", "boss", _director.LadderBoss(f)), f.Recommended || !next.Recommend);
            box.Check(CareersText.T("reallife_ladder_need_stores", "have", f.Company.Stores.Count.ToString(), "need", next.Stores.ToString()), f.Company.Stores.Count >= next.Stores);
            string why = _director.LadderWhyNot(f);
            box.Line(CareersText.T(why == null ? "reallife_ladder_next_ready" : why), why == null ? CellTone.Green : CellTone.Plain);
            box.Switch("reallife_ladder_aiming", f.Aiming, on => Do(() => _director.LadderAiming(on)));
            if (next.Level == Rung.Executive)
            {
                box.Selector(3, (int)seat - 1, i => CareersText.T(LadderRanks.TitleKey((int)Rung.Executive, (ExecSeat)(i + 1))), i => Do(() => _director.LadderAimSeat((ExecSeat)(i + 1))));
            }
        }

        // The player's own requests to their superior: one waiting at a time, each granted once.
        private void WriteRequests(InfoBox box, LadderFile f)
        {
            box.Section("reallife_ladder_sec_requests");
            if (f.Ask != (int)StaffWant.None)
            {
                box.Line(CareersText.Fill("reallife_ladder_asked_line", "what", CareersText.T("reallife_ladder_want_" + ((StaffWant)f.Ask).ToString().ToLowerInvariant()), "boss", _director.LadderBoss(f)),
                    CellTone.Yellow);
            }
            if (f.Prefer != 0)
            {
                box.Line(CareersText.T(f.Prefer < 0 ? "reallife_ladder_granted_earlier" : "reallife_ladder_granted_later", "hours", System.Math.Abs(f.Prefer).ToString()), CellTone.Green);
            }
            if (f.Equipped)
            {
                box.Line(CareersText.T("reallife_ladder_granted_equipment"), CellTone.Green);
            }
            if (f.Insured)
            {
                box.Line(CareersText.T("reallife_ladder_granted_insurance"), CellTone.Green);
            }
            if (f.Ask != (int)StaffWant.None || !LadderRanks.Has(f.Rung, RungPower.Requests))
            {
                return;
            }
            box.Note(CareersText.T("reallife_ladder_ask_about", "chance", (CareerLadderRules.AskChance(f) / 10).ToString() + "%"));
            Transform row = box.ButtonRow();
            if (f.Prefer > -2)
            {
                InfoBox.Button(row, InfoBox.Additional, "reallife_ladder_button_earlier", () => Do(() => _director.LadderAsk(StaffWant.Shift, -1)));
            }
            if (f.Prefer < 2)
            {
                InfoBox.Button(row, InfoBox.Additional, "reallife_ladder_button_later", () => Do(() => _director.LadderAsk(StaffWant.Shift, 1)));
            }
            if (!f.Equipped)
            {
                InfoBox.Button(row, InfoBox.Additional, "reallife_ladder_button_equipment", () => Do(() => _director.LadderAsk(StaffWant.Equipment, 0)));
            }
            if (!f.Insured)
            {
                InfoBox.Button(row, InfoBox.Additional, "reallife_ladder_button_insurance", () => Do(() => _director.LadderAsk(StaffWant.Insurance, 0)));
            }
        }

        // The executive's own area of the company's policy: each field with its value and a step either way.
        private void WritePolicy(InfoBox box, LadderFile f)
        {
            CompanyPolicy p = f.Company.Policy;
            box.Section("reallife_ladder_sec_policy");
            box.Note(CareersText.T("reallife_ladder_policy_about"));
            for (int i = 0; i <= (int)PolicyField.Budget; i++)
            {
                var field = (PolicyField)i;
                CareerLadderRules.Range(p, field, out int value, out int least, out int most, out int step);
                bool mine = (int)CareerLadderRules.SeatOf(field) == f.Seat;
                box.RowWords(CareersText.T("reallife_ladder_policy_" + field.ToString().ToLowerInvariant()), PolicyValue(field, value));
                if (!mine)
                {
                    continue;
                }
                Transform row = box.ButtonRow();
                if (value > least)
                {
                    InfoBox.Button(row, InfoBox.Additional, "reallife_ladder_button_less", () => Do(() => _director.LadderPolicy(field, -1)));
                }
                if (value < most)
                {
                    InfoBox.Button(row, InfoBox.Additional, "reallife_ladder_button_more", () => Do(() => _director.LadderPolicy(field, 1)));
                }
            }
        }

        private static string PolicyValue(PolicyField field, int value)
        {
            switch (field)
            {
                case PolicyField.Staffing: return CareersText.T(RotaKeys[TalentCatalogue.Clamp(value, 0, 2)]);
                case PolicyField.Escalate: return CareersText.T("reallife_ladder_days", "days", value.ToString());
                case PolicyField.Floor: return value.ToString();
                case PolicyField.Band: return (value / 100f).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + "%";
                case PolicyField.WageTop: return CareersText.Rate(Prices.At(value));
                default: return CareersText.Money(Prices.At(value));
            }
        }

        // A task picked: what it is, the choice of person or candidate where it has one, and the decisions the rung may take.
        private void WriteTask(InfoBox box, LadderFile f, LadderTask t, int day)
        {
            CityCompany c = f.Company;
            int id = t.Id;
            if (_ladderPickOf != id)
            {
                _ladderPickOf = id;
                _ladderPick = 0;
            }
            box.Title(TaskTitle(f, t));
            box.Subtitle(StoreName(c, t.Store));
            box.Chip(CareersText.T("reallife_ladder_kind_" + ((TaskKind)t.Kind).ToString().ToLowerInvariant()));
            box.Row("reallife_ladder_row_raised", CareersText.Day(t.Day));
            box.Row("reallife_ladder_row_decide_by", CareersText.Day(t.Lapse), t.Lapse <= day ? CellTone.Yellow : CellTone.Plain);
            if (t.Store >= 0 && t.Store < c.Stores.Count)
            {
                box.Row("reallife_ladder_row_staff_now", CareersText.T("reallife_ladder_row_staff", "staff", CareerLadderRules.StaffAt(c, t.Store).ToString(), "wanted", CareerLadderRules.Wanted(c.Stores[t.Store]).ToString()));
                box.Row("reallife_ladder_row_budget", CareersText.Money(Prices.At(CareerLadderRules.BudgetLeft(c, t.Store))));
            }
            string key = "reallife_ladder_do_" + ((TaskKind)t.Kind).ToString().ToLowerInvariant() + (t.Kind == (int)TaskKind.Escalation ? "_" + ((Concern)t.About).ToString().ToLowerInvariant() : string.Empty);
            box.Note(CareersText.T(key + "_about"));
            int pick = -1;
            switch ((TaskKind)t.Kind)
            {
                case TaskKind.Hire:
                    {
                        int least = CareerLadderRules.CandidatesEach;
                        box.Selector(least, _ladderPick, i =>
                        {
                            CareerLadderRules.Candidate(_director.LadderSeed, id, i, out int skill, out int wage);
                            return CareersText.Fill("reallife_ladder_candidate", "name", _director.LadderCandidateName(id, i), "skill", skill.ToString(), "pay", CareersText.Rate(Prices.At(wage)));
                        }, i =>
                        {
                            _ladderPick = i;
                            Refresh();
                        });
                        CareerLadderRules.Candidate(_director.LadderSeed, id, _ladderPick, out int s, out int w);
                        bool within = CareerLadderRules.WithinPolicy(c.Policy, s, w);
                        box.Line(CareersText.T(within ? "reallife_ladder_within_policy" : "reallife_ladder_outside_policy", "floor", c.Policy.Floor.ToString(), "top", CareersText.Rate(Prices.At(c.Policy.WageTop))),
                            within ? CellTone.Green : CellTone.Red);
                        pick = _ladderPick;
                        break;
                    }
                case TaskKind.Cover:
                case TaskKind.Appoint:
                case TaskKind.Escalation when t.About == (int)Concern.Gap:
                    {
                        _picks.Clear();
                        for (int i = 0; i < c.People.Count; i++)
                        {
                            Colleague p = c.People[i];
                            bool fits = t.Kind == (int)TaskKind.Escalation ? p.Store != t.Store && CareerLadderRules.InUnit(f, p.Store) && p.Role == (int)Rung.Frontline
                                : p.Store == t.Store && (t.Kind == (int)TaskKind.Cover || p.Role != (int)Rung.StoreManager);
                            if (fits)
                            {
                                _picks.Add(p);
                            }
                        }
                        if (_picks.Count > 0)
                        {
                            int at = TalentCatalogue.Clamp(_ladderPick, 0, _picks.Count - 1);
                            box.Selector(_picks.Count, at, i => CareersText.Fill("reallife_ladder_pick_person", "name", _picks[i].Name, "store", c.Stores[_picks[i].Store].Name,
                                "skill", _picks[i].Skill.ToString(), "morale", _picks[i].Morale.ToString()), i =>
                            {
                                _ladderPick = i;
                                Refresh();
                            });
                            pick = _picks[at].Id;
                        }
                        else
                        {
                            box.Line(CareersText.T("reallife_ladder_nobody"), CellTone.Yellow);
                        }
                        break;
                    }
            }
            int chosen = pick;
            Transform row = box.ButtonRow();
            if (pick >= 0 || (t.Kind != (int)TaskKind.Hire && t.Kind != (int)TaskKind.Cover && t.Kind != (int)TaskKind.Appoint && !(t.Kind == (int)TaskKind.Escalation && t.About == (int)Concern.Gap)))
            {
                InfoBox.Button(row, InfoBox.Positive, key + "_yes", () => Do(() => _director.LadderAnswer(id, Verdict.Yes, chosen)));
            }
            InfoBox.Button(row, InfoBox.Negative, key + "_no", () => Do(() => _director.LadderAnswer(id, Verdict.No, chosen)));
            if (LadderRanks.Has(f.Rung, RungPower.PassUp) && f.Rung < (int)Rung.Executive && t.Kind != (int)TaskKind.Appoint && !(t.Kind == (int)TaskKind.Cover && f.Rung >= (int)Rung.StoreManager))
            {
                InfoBox.Button(row, InfoBox.Additional, "reallife_ladder_button_up", () => Do(() => _director.LadderAnswer(id, Verdict.Up, chosen)));
            }
            InfoBox.Button(box.ButtonRow(), InfoBox.Additional, "common_close", ClosePick);
            Meta.Guide.Point(box, LadderGuide);
        }

        // A person picked: who they are and what they ask for; a raise and a move where the rung may.
        private void WritePerson(InfoBox box, LadderFile f, Colleague p)
        {
            CityCompany c = f.Company;
            int id = p.Id;
            box.Title(p.Name);
            box.Subtitle(StoreName(c, p.Store));
            box.Chip(CareersText.T(LadderRanks.TitleKey(p.Role, (ExecSeat)p.Seat)));
            box.Row("reallife_ladder_row_skill_label", p.Skill.ToString());
            box.Row("reallife_career_col_pay", CareersText.Rate(Prices.At(p.Wage)));
            box.Meter("reallife_ladder_row_morale_label", p.Morale / 100f, p.Morale.ToString(), p.Morale < CareerLadderRules.LowMorale ? CellTone.Red : p.Morale >= 70 ? CellTone.Green : CellTone.Plain);
            box.Row("reallife_ladder_row_wants", p.Want == (int)StaffWant.None ? "-" : CareersText.T("reallife_ladder_want_" + ((StaffWant)p.Want).ToString().ToLowerInvariant()));
            if (p.GameId.Length == 0)
            {
                box.Note(CareersText.T("reallife_ladder_person_ours"));
            }
            Transform row = box.ButtonRow();
            if (LadderRanks.Has(f.Rung, RungPower.Raises) && p.Store >= 0)
            {
                box.Note(CareersText.T("reallife_ladder_raise_about", "band", (c.Policy.Band / 100f).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + "%",
                    "days", CareerLadderRules.RaiseGapDays.ToString()));
                InfoBox.Button(row, InfoBox.Positive, "reallife_ladder_button_raise", () => Do(() => _director.LadderRaise(id)));
            }
            if (LadderRanks.Has(f.Rung, RungPower.Transfers) && p.Role == (int)Rung.Frontline && p.Store >= 0)
            {
                var stores = new List<int>(c.Stores.Count);
                for (int s = 0; s < c.Stores.Count; s++)
                {
                    if (s != p.Store && CareerLadderRules.InUnit(f, s))
                    {
                        stores.Add(s);
                    }
                }
                if (stores.Count > 0)
                {
                    int at = TalentCatalogue.Clamp(_ladderTo, 0, stores.Count - 1);
                    box.Selector(stores.Count, at, i => StoreName(c, stores[i]), i =>
                    {
                        _ladderTo = i;
                        Refresh();
                    });
                    int to = stores[at];
                    InfoBox.Button(box.ButtonRow(), InfoBox.Additional, "reallife_ladder_button_move", () => Do(() => _director.LadderMove(id, to)));
                }
            }
            InfoBox.Button(box.ButtonRow(), InfoBox.Additional, "common_close", ClosePick);
        }

        // A store picked (a regional manager's or an executive's): its figures and who runs it.
        private void WriteStore(InfoBox box, LadderFile f, int s, int day)
        {
            CityCompany c = f.Company;
            Outlet st = c.Stores[s];
            box.Title(st.Name);
            box.Subtitle(WorkGame.Address(st.Street, st.Number));
            Colleague manager = CareerLadderRules.Holder(c, s, (int)Rung.StoreManager);
            box.Row("reallife_ladder_row_manager", manager != null ? manager.Name : CareersText.T("reallife_ladder_none"), manager != null ? CellTone.Plain : CellTone.Red);
            box.Row("reallife_ladder_row_staff_now", CareersText.T("reallife_ladder_row_staff", "staff", CareerLadderRules.StaffAt(c, s).ToString(), "wanted", CareerLadderRules.Wanted(st).ToString()));
            box.Row("reallife_ladder_row_staffing", CareersText.Percent(CareerLadderRules.Staffing(c, s, day)));
            box.Row("reallife_ladder_row_morale_avg", CareerLadderRules.MoraleAt(c, s).ToString());
            box.Row("reallife_ladder_row_rota", CareersText.T(RotaKeys[TalentCatalogue.Clamp(st.Rota, 0, 2)]));
            box.Row("reallife_ladder_row_budget", CareersText.Money(Prices.At(CareerLadderRules.BudgetLeft(c, s))));
            box.Meter("reallife_ladder_row_results_label", st.Results / 100f, st.Results.ToString(), st.Results >= 70 ? CellTone.Green : st.Results < 45 ? CellTone.Red : CellTone.Plain);
            if (st.GapDays > 0)
            {
                box.Line(CareersText.T("reallife_ladder_gap_days", "days", st.GapDays.ToString()), CellTone.Yellow);
            }
            InfoBox.Button(box.ButtonRow(), InfoBox.Additional, "common_close", ClosePick);
        }
    }
}
