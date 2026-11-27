using System;
using System.Collections.Generic;
using System.Globalization;
using Localizor;
using RealLifeOverhaul.Tech;

namespace RealLifeOverhaul.Ui
{
    // Open Roles' team on the kit, the part that changes roles together and names the firms' managers: the steppers, the preview with who goes and who is left out and why,
    // the question, and a person's page for naming a manager or for a manager's duties and targets. Every action goes to the director that owns it.
    public sealed partial class RolesLive
    {
        private const int MoveFrom = 10, MoveTo = 11, MoveCount = 12, TargetRole = 13, TargetCount = 14;   // the amounts these steppers keep (the view's 0 to 8 are the new ad's, 9 the raise's)

        private static string Signed(long cents) => (cents > 0 ? "+" : string.Empty) + TechText.Cents(cents);

        // Some of the people the page lists moved from one role to another: the roles and the count are stepped in each person's detail (the table's detail is where the page keeps its
        // steppers), and the preview says who goes first, who is left out and why, and what the going rate does. The button asks first.
        private void AddMoveRoles(TechDirector d, WorkView view, WorkTab t, UtilitiesWords w)
        {
            var ids = new List<string>(TeamRows);
            ShownTeam(d, view, ids);
            var held = new int[TalentCatalogue.RoleCount];
            d.RolesHeld(ids, held);
            var fromRoles = new List<int>(8);
            for (int r = 0; r < held.Length; r++)
            {
                if (held[r] > 0)
                {
                    fromRoles.Add(r);
                }
            }
            if (fromRoles.Count == 0)
            {
                return;
            }
            int fromAt = Math.Max(0, Math.Min(fromRoles.Count - 1, view != null ? view.Get(MoveFrom, 0) : 0)), from = fromRoles[fromAt];
            var toRoles = new int[TalentCatalogue.RoleCount];
            int nTo = d.RolesTo(ids, from, toRoles);
            if (nTo == 0)
            {
                return;
            }
            int toAt = Math.Max(0, Math.Min(nTo - 1, view != null ? view.Get(MoveTo, 0) : 0)), to = toRoles[toAt];
            RoleMovePlan all = d.PlanRoleMoves(ids, from, to, 0, out RoleMoveFigures figures);
            int able = all.Able;
            int countAt = Math.Max(0, Math.Min(able, view != null ? view.Get(MoveCount, 0) : 0)), asked = RoleMoveRules.Asked(countAt, able);
            RoleMovePlan plan = asked == 0 ? all : d.PlanRoleMoves(ids, from, to, asked, out figures);
            var fromWords = new string[fromRoles.Count];
            for (int i = 0; i < fromWords.Length; i++)
            {
                fromWords[i] = TechText.Fill("reallife_roles_move_from_pick", "role", TechText.Role(fromRoles[i]), "count", held[fromRoles[i]].ToString(Inv));
            }
            var toWords = new string[nTo];
            for (int i = 0; i < nTo; i++)
            {
                toWords[i] = TechText.Fill("reallife_roles_move_to_pick", "role", TechText.Role(toRoles[i]));
            }
            var countWords = new string[able + 1];
            countWords[0] = TechText.Fill("reallife_roles_move_all", "count", able.ToString(Inv));
            for (int i = 1; i <= able; i++)
            {
                countWords[i] = i.ToString(Inv);
            }
            int first = t.Amounts.Count;
            t.Amounts.Add(Choice(MoveFrom, fromAt, fromWords));
            t.Amounts.Add(Choice(MoveTo, toAt, toWords));
            t.Amounts.Add(Choice(MoveCount, countAt, countWords));
            string[] titles = { TechText.T("reallife_roles_move_step_from"), TechText.T("reallife_roles_move_step_to"), TechText.T("reallife_roles_move_step_count") };
            foreach (WorkRow row in t.Rows)
            {
                for (int s = 0; s < 3; s++)
                {
                    row.Sections.Add(new WorkSection { Part = WorkPart.Amount, Title = titles[s], Stepper = first + s });
                }
            }
            int month = GameCalendar.MonthDays;
            string going = Signed(figures.GoingMonthly(month)), fromName = TechText.Role(from), toName = TechText.Role(to);
            var labels = new List<string>(6);
            var values = new List<string>(6);
            labels.Add(TechText.T("reallife_roles_move_lbl_plan"));
            values.Add(plan.Moved > 0 ? TechText.Fill("reallife_roles_move_plan", "count", plan.Moved.ToString(Inv), "from", fromName, "to", toName, "going", going)
                : TechText.T("reallife_roles_move_none"));
            labels.Add(TechText.T("reallife_roles_move_lbl_game"));
            Feed feed = StudioFeeds.Of(TalentCatalogue.Role(to));
            values.Add(TechText.Fill(StudioFeeds.RoleKey(feed), "parts", RolesApp.PartsLine(feed), "role", toName));
            for (int k = 0; k < RoleMovePlan.Whys; k++)
            {
                if (plan.Why[k] > 0)
                {
                    labels.Add(TechText.T("reallife_roles_move_lbl_left"));
                    values.Add(TechText.Fill("reallife_roles_move_refused", "count", plan.Why[k].ToString(Inv), "why", TechText.Fill(RoleMoveRules.WhyKey(k), "firm",
                        plan.WhyPlace[k] ?? string.Empty)));
                }
            }
            if (plan.Away + plan.CannotHold > 0)
            {
                labels.Add(TechText.T("reallife_roles_move_lbl_left"));
                values.Add(TechText.Fill("reallife_roles_move_left_out", "away", plan.Away.ToString(Inv), "cannot", plan.CannotHold.ToString(Inv)));
            }
            labels.Add(TechText.T("reallife_roles_move_lbl_rule"));
            values.Add(TechText.T("reallife_roles_move_rule"));
            t.Below.Add(WorkBands.Pairs(TechText.T("reallife_roles_sec_move"), labels.ToArray(), values.ToArray()));
            if (plan.Moved > 0)
            {
                t.Stats.Add(new KeyValuePair<string, string>(TechText.Fill("reallife_roles_move_stat", "count", plan.Moved.ToString(Inv), "to", toName), going));
            }
            t.Acts.Add(Act(MoveRoles, 0, TechText.Fill("reallife_roles_move_button", "count", plan.Moved.ToString(Inv), "to", toName), UiButtonKind.Secondary, UiIcon.Check, false,
                plan.Moved > 0 ? null : TechText.T("reallife_roles_move_none")));
        }

        // The question the move asks first, with the figures the page showed: the people are chosen again when it is answered.
        private bool AskMoveRoles(TechDirector d, WorkView view)
        {
            var ids = new List<string>(TeamRows);
            ShownTeam(d, view, ids);
            var held = new int[TalentCatalogue.RoleCount];
            d.RolesHeld(ids, held);
            var fromRoles = new List<int>(8);
            for (int r = 0; r < held.Length; r++)
            {
                if (held[r] > 0)
                {
                    fromRoles.Add(r);
                }
            }
            if (fromRoles.Count == 0)
            {
                return false;
            }
            int from = fromRoles[Math.Max(0, Math.Min(fromRoles.Count - 1, view.Get(MoveFrom, 0)))];
            var toRoles = new int[TalentCatalogue.RoleCount];
            int nTo = d.RolesTo(ids, from, toRoles);
            if (nTo == 0)
            {
                return false;
            }
            int to = toRoles[Math.Max(0, Math.Min(nTo - 1, view.Get(MoveTo, 0)))];
            RoleMovePlan all = d.PlanRoleMoves(ids, from, to, 0, out RoleMoveFigures figures);
            int able = all.Able, asked = RoleMoveRules.Asked(Math.Max(0, Math.Min(able, view.Get(MoveCount, 0))), able);
            RoleMovePlan plan = asked == 0 ? all : d.PlanRoleMoves(ids, from, to, asked, out figures);
            if (plan.Moved == 0)
            {
                return false;
            }
            string count = plan.Moved.ToString(Inv), going = Signed(figures.GoingMonthly(GameCalendar.MonthDays)), fromName = TechText.Role(from), toName = TechText.Role(to);
            GameConfirm.Show("reallife_roles_confirm_move_title".Localize(new { count, to = toName }),
                "reallife_roles_confirm_move_body".Localize(new { count, from = fromName, to = toName, going }),
                () =>
                {
                    d.MoveRoles(ids, from, to, asked);
                    _local++;
                }, null, null, null, true, false);
            return true;
        }

        // The firms' managers on the team page: one line a firm that has one.
        private void AddManagerLines(TechDirector d, WorkTab t)
        {
            if (!d.ManagersOn)
            {
                return;
            }
            var firms = new List<string>(4);
            var lines = new List<string>(4);
            for (int i = 0; i < d.FirmPlaces.Count; i++)
            {
                string line = d.ManagerSummary(d.FirmPlaces[i].File.Place);
                if (line.Length > 0)
                {
                    firms.Add(d.FirmPlaces[i].File.Name);
                    lines.Add(line);
                }
            }
            t.Below.Add(WorkBands.Pairs(TechText.T("reallife_tech_mgr_title"), firms.Count > 0 ? firms.ToArray() : new[] { TechText.T("reallife_tech_mgr_none_label") },
                lines.Count > 0 ? lines.ToArray() : new[] { TechText.T("reallife_tech_mgr_about") }));
        }

        // A person's career page: naming them the firm's manager (what it costs is in the question), or as the manager their duties, their targets and the end of the post.
        private void AddManagerBand(TechDirector d, WorkView view, WorkTab t, WorkFormModel form, Person person)
        {
            if (!d.ManagersOn)
            {
                return;
            }
            string id = person.File.Id, place = person.File.Place;
            ManagerPost post = d.PostOf(id);
            var band = new WorkFormBand { Title = TechText.T("reallife_tech_mgr_title") };
            if (post == null)
            {
                ManagerPost there = d.ManagerOf(place);
                string why = d.ManagerRefusal(id);
                if (there != null && there.Id != id)
                {
                    band.Rows.Add(new WorkFormRow { Field = WorkField.Text, Value = TechText.Fill("reallife_tech_mgr_firm_has", "name", there.Name) });
                }
                band.Rows.Add(new WorkFormRow { Field = WorkField.Button, Label = TechText.T("reallife_tech_mgr_label_post"),
                    Act = Act(NameManager, 0, TechText.T("reallife_tech_mgr_button_name"), UiButtonKind.Secondary, UiIcon.Star, false, why != null ? TechText.T(why) : null) });
                form.Right.Add(band);
                return;
            }
            band.Rows.Add(new WorkFormRow { Field = WorkField.Text, Value = d.ManagerSummary(place) });
            if (post.Last.Length > 0)
            {
                band.Rows.Add(new WorkFormRow { Field = WorkField.Text, Value = post.Last });
            }
            if (post.Worn.Count > 0)
            {
                band.Rows.Add(new WorkFormRow { Field = WorkField.Text, Value = TechText.Fill("reallife_tech_mgr_worn_names", "names", string.Join(", ", post.Worn)) });
            }
            if (post.Idle.Count > 0)
            {
                band.Rows.Add(new WorkFormRow { Field = WorkField.Text, Value = TechText.Fill("reallife_tech_mgr_idle_names", "names", string.Join(", ", post.Idle)) });
            }
            int[] duties = { FirmManagerCatalogue.DutyBalance, FirmManagerCatalogue.DutyHire, FirmManagerCatalogue.DutyRaises, FirmManagerCatalogue.DutyWatch };
            string[] dutyOn = { "reallife_tech_mgr_duty_balance_on", "reallife_tech_mgr_duty_hire_on", "reallife_tech_mgr_duty_raises_on", "reallife_tech_mgr_duty_watch_on" };
            string[] dutyOff = { "reallife_tech_mgr_duty_balance_off", "reallife_tech_mgr_duty_hire_off", "reallife_tech_mgr_duty_raises_off", "reallife_tech_mgr_duty_watch_off" };
            for (int i = 0; i < duties.Length; i++)
            {
                bool on = post.Has(duties[i]);
                band.Rows.Add(new WorkFormRow { Field = WorkField.Button, Label = string.Empty, Act = Act(ToggleDuty, duties[i], TechText.T(on ? dutyOn[i] : dutyOff[i]),
                    on ? UiButtonKind.Primary : UiButtonKind.Secondary, on ? UiIcon.Check : UiIcon.Close) });
            }
            for (int i = 0; i < post.Targets.Count; i++)
            {
                band.Rows.Add(new WorkFormRow { Field = WorkField.Label, Label = TechText.Role(post.Targets[i].Role), Value = post.Targets[i].Count.ToString(Inv) });
            }
            if (post.Targets.Count == 0)
            {
                band.Rows.Add(new WorkFormRow { Field = WorkField.Text, Value = TechText.T("reallife_tech_mgr_target_none") });
            }
            var roles = new int[TalentCatalogue.RoleCount];
            int n = d.RolesAt(place, roles);
            if (n > 0)
            {
                int at = Math.Max(0, Math.Min(n - 1, view.Get(TargetRole, 0)));
                var names = new string[n];
                for (int i = 0; i < n; i++)
                {
                    names[i] = TechText.Role(roles[i]);
                }
                var counts = new string[20];
                for (int i = 0; i < counts.Length; i++)
                {
                    counts[i] = TechText.Fill("reallife_tech_mgr_target_count", "count", (i + 1).ToString(Inv));
                }
                int countAt = Math.Max(0, Math.Min(counts.Length - 1, view.Get(TargetCount, 0)));
                t.Amounts.Add(Choice(TargetRole, at, names));
                band.Rows.Add(new WorkFormRow { Field = WorkField.Stepper, Label = TechText.T("reallife_tech_mgr_target_role"), Stepper = t.Amounts.Count - 1 });
                t.Amounts.Add(Choice(TargetCount, countAt, counts));
                band.Rows.Add(new WorkFormRow { Field = WorkField.Stepper, Label = TechText.T("reallife_tech_mgr_target_howmany"), Stepper = t.Amounts.Count - 1 });
                var set = new WorkFormRow { Field = WorkField.Button, Label = string.Empty, Act = Act(SetTarget, roles[at] * 100 + countAt + 1,
                    TechText.T("reallife_tech_mgr_button_target"), UiButtonKind.Secondary, UiIcon.Check) };
                set.More.Add(Act(ClearTargets, 0, TechText.T("reallife_tech_mgr_button_clear"), UiButtonKind.Secondary, UiIcon.Close));
                band.Rows.Add(set);
            }
            band.Rows.Add(new WorkFormRow { Field = WorkField.Button, Label = string.Empty, Act = Act(RemoveManager, 0, TechText.T("reallife_tech_mgr_button_remove"),
                UiButtonKind.Destructive, UiIcon.SignOut) });
            form.Right.Add(band);
        }

        // Naming a manager: the question first, with the rise, what it takes from their own work and who they relieve.
        private bool AskNameManager(TechDirector d, Person person)
        {
            if (person == null || !d.PlanManager(person.File.Id, out ManagerPlan plan))
            {
                return false;
            }
            string id = person.File.Id, name = plan.Name, firm = plan.Firm;
            string body = d.NameBody(plan);
            GameConfirm.Show("reallife_tech_mgr_confirm_name_title".Localize(new { name, firm }), "reallife_tech_mgr_confirm_name_text".Localize(new { body }), () =>
            {
                d.NameManager(id);
                _local++;
            }, null, null, null, true, false);
            return true;
        }

        private bool AskRemoveManager(TechDirector d, Person person)
        {
            if (person == null || d.PostOf(person.File.Id) == null)
            {
                return false;
            }
            string place = person.File.Place, name = person.Name, firm = person.Business;
            GameConfirm.Show("reallife_tech_mgr_confirm_remove_title".Localize(new { name }), "reallife_tech_mgr_confirm_remove_body".Localize(new { name, firm }), () =>
            {
                d.RemoveManager(place);
                _local++;
            }, null, null, null, true, true);
            return true;
        }
    }
}
