using System.Collections.Generic;
using System.Globalization;
using Localizor;
using RealLifeOverhaul.GameUi;
using UnityEngine;

namespace RealLifeOverhaul.Tech
{
    /// <summary>
    /// The Team tab's role change for everyone shown (the roles one at a time are on a person's career page), and the firms' managers: who is named, what they have done, and a
    /// person's page for naming one, setting their targets and switching their duties. Written with the page, never a frame.
    /// </summary>
    public sealed partial class RolesApp
    {
        private int _moveFrom, _moveTo, _moveCount;               // the steppers' places: the role they move from and to (in the lists below) and how many (0: all)
        private int _targetAt, _targetCount = 1;                  // a manager's target being set: the role (in the firm's list) and the count
        private readonly int[] _heldBy = new int[TalentCatalogue.RoleCount];
        private readonly int[] _fromRoles = new int[TalentCatalogue.RoleCount];
        private readonly int[] _toRoles = new int[TalentCatalogue.RoleCount];
        private readonly int[] _atRoles = new int[TalentCatalogue.RoleCount];

        // A move of some of the people shown from one role to another: the roles and the count chosen, who goes (best fit first, then the newest) and what it comes to before it is asked.
        private void WriteMoveRoles(InfoBox box)
        {
            box.Section("reallife_roles_sec_move");
            _director.RolesHeld(_raiseIds, _heldBy);
            int nFrom = 0;
            for (int r = 0; r < _heldBy.Length; r++)
            {
                if (_heldBy[r] > 0)
                {
                    _fromRoles[nFrom++] = r;
                }
            }
            if (nFrom == 0)
            {
                box.Note(TechText.T("reallife_roles_move_nobody"));
                return;
            }
            _moveFrom = System.Math.Max(0, System.Math.Min(nFrom - 1, _moveFrom));
            int from = _fromRoles[_moveFrom];
            int nTo = _director.RolesTo(_raiseIds, from, _toRoles);
            if (nTo == 0)
            {
                box.Note(TechText.T("reallife_roles_move_none_to", "role", TechText.Role(from)));
                return;
            }
            _moveTo = System.Math.Max(0, System.Math.Min(nTo - 1, _moveTo));
            int to = _toRoles[_moveTo];
            RoleMovePlan all = _director.PlanRoleMoves(_raiseIds, from, to, 0, out RoleMoveFigures figures);
            int able = all.Able;
            _moveCount = System.Math.Max(0, System.Math.Min(able, _moveCount));
            int asked = RoleMoveRules.Asked(_moveCount, able);
            RoleMovePlan plan = asked == 0 ? all : _director.PlanRoleMoves(_raiseIds, from, to, asked, out figures);
            if (box.HasSelector)
            {
                box.Selector(nFrom, _moveFrom, i => TechText.Fill("reallife_roles_move_from_pick", "role", TechText.Role(_fromRoles[i]), "count",
                    _heldBy[_fromRoles[i]].ToString(CultureInfo.InvariantCulture)), i =>
                {
                    _moveFrom = i;
                    _moveTo = 0;
                    _moveCount = 0;
                    Refresh();
                });
                box.Selector(nTo, _moveTo, i => TechText.Fill("reallife_roles_move_to_pick", "role", TechText.Role(_toRoles[i])), i =>
                {
                    _moveTo = i;
                    _moveCount = 0;
                    Refresh();
                });
                box.Selector(able + 1, _moveCount, i => i == 0 ? TechText.Fill("reallife_roles_move_all", "count", able.ToString(CultureInfo.InvariantCulture))
                    : i.ToString(CultureInfo.InvariantCulture), i =>
                {
                    _moveCount = i;
                    Refresh();
                });
            }
            int month = GameCalendar.MonthDays;
            string going = Signed(figures.GoingMonthly(month));
            if (plan.Moved > 0)
            {
                box.Line(TechText.Fill("reallife_roles_move_plan", "count", plan.Moved.ToString(CultureInfo.InvariantCulture), "from", TechText.Role(from), "to", TechText.Role(to), "going", going));
            }
            else
            {
                box.Note(TechText.T("reallife_roles_move_none"));
            }
            box.Note(TechText.Fill(StudioFeeds.RoleKey(StudioFeeds.Of(TalentCatalogue.Role(to))), "parts", PartsLine(StudioFeeds.Of(TalentCatalogue.Role(to))), "role", TechText.Role(to)));
            for (int w = 0; w < RoleMovePlan.Whys; w++)
            {
                if (plan.Why[w] > 0)
                {
                    box.Note(TechText.Fill("reallife_roles_move_refused", "count", plan.Why[w].ToString(CultureInfo.InvariantCulture),
                        "why", TechText.Fill(RoleMoveRules.WhyKey(w), "firm", plan.WhyPlace[w] ?? string.Empty)));
                }
            }
            if (plan.Away + plan.CannotHold > 0)
            {
                box.Note(TechText.Fill("reallife_roles_move_left_out", "away", plan.Away.ToString(CultureInfo.InvariantCulture), "cannot", plan.CannotHold.ToString(CultureInfo.InvariantCulture)));
            }
            box.Note(TechText.T("reallife_roles_move_rule"));
            if (plan.Moved > 0)
            {
                var ids = new List<string>(_raiseIds);
                int count = plan.Moved;
                InfoBox.Button(box.ButtonRow(), InfoBox.Positive, "reallife_roles_button_move", () => ConfirmMove(ids, from, to, asked, count, going));
            }
        }

        // The question the move asks first, with the figures the page showed; the people are chosen again when it is answered.
        private void ConfirmMove(List<string> ids, int from, int to, int asked, int moved, string going)
        {
            string count = moved.ToString(CultureInfo.InvariantCulture), fromRole = TechText.Role(from), toRole = TechText.Role(to);
            GameConfirm.Show("reallife_roles_confirm_move_title".Localize(new { count, to = toRole }),
                "reallife_roles_confirm_move_body".Localize(new { count, from = fromRole, to = toRole, going }),
                () => UiClone.Safe(() => Do(() => _director.MoveRoles(ids, from, to, asked))), null, null, null, true, false);
        }

        private static string Signed(long cents) => (cents > 0 ? "+" : string.Empty) + TechText.Cents(cents);

        /// <summary>The parts a kind of work builds as the role's line says them, with the shares of the catalogue's split ("Tech 40%, Content 60%").</summary>
        internal static string PartsLine(Feed feed)
        {
            return StudioFeeds.PartsLine(feed, p => TechText.T(StudioViews.PartKey(p)), s => TechText.Mille(s));
        }

        // The firms' named managers, one line each, on the Team tab with nothing picked.
        private void WriteManagers(InfoBox box)
        {
            if (!_director.ManagersOn)
            {
                return;
            }
            bool any = false;
            for (int i = 0; i < _director.FirmPlaces.Count; i++)
            {
                string place = _director.FirmPlaces[i].File.Place;
                string line = _director.ManagerSummary(place);
                if (line.Length == 0)
                {
                    continue;
                }
                if (!any)
                {
                    box.Section("reallife_tech_mgr_title");
                    any = true;
                }
                box.Line(TechText.Fill("reallife_tech_mgr_firm_line", "firm", _director.FirmPlaces[i].File.Name, "line", line));
            }
            if (!any)
            {
                box.Section("reallife_tech_mgr_title");
                box.Note(TechText.T("reallife_tech_mgr_about"));
            }
        }

        // A person's page: naming them the firm's manager (with what it costs), or, if they are the manager, what they do, their duties, targets and the post's end.
        private void WriteManagerPage(InfoBox box, Person person)
        {
            if (!_director.ManagersOn)
            {
                return;
            }
            string id = person.File.Id, place = person.File.Place;
            ManagerPost post = _director.PostOf(id);
            box.Section("reallife_tech_mgr_title");
            if (post == null)
            {
                string why = _director.ManagerRefusal(id);
                ManagerPost there = _director.ManagerOf(place);
                if (there != null && there.Id != id)
                {
                    box.Note(TechText.Fill("reallife_tech_mgr_firm_has", "name", there.Name));
                }
                if (why != null)
                {
                    box.Note(TechText.T(why));
                    return;
                }
                InfoBox.Button(box.ButtonRow(), InfoBox.Additional, "reallife_tech_mgr_button_name", () => ConfirmName(person));
                return;
            }
            box.Line(_director.ManagerSummary(place));
            if (post.Last.Length > 0)
            {
                box.Note(post.Last);
            }
            if (post.Worn.Count > 0)
            {
                box.Note(TechText.Fill("reallife_tech_mgr_worn_names", "names", string.Join(", ", post.Worn)));
            }
            if (post.Idle.Count > 0)
            {
                box.Note(TechText.Fill("reallife_tech_mgr_idle_names", "names", string.Join(", ", post.Idle)));
            }
            if (box.HasSwitch)
            {
                Duty(box, post, place, FirmManagerCatalogue.DutyBalance, "reallife_tech_mgr_duty_balance");
                Duty(box, post, place, FirmManagerCatalogue.DutyHire, "reallife_tech_mgr_duty_hire");
                Duty(box, post, place, FirmManagerCatalogue.DutyRaises, "reallife_tech_mgr_duty_raises");
                Duty(box, post, place, FirmManagerCatalogue.DutyWatch, "reallife_tech_mgr_duty_watch");
            }
            for (int i = 0; i < post.Targets.Count; i++)
            {
                box.Line(TechText.Fill("reallife_tech_mgr_target_line", "role", TechText.Role(post.Targets[i].Role), "count", post.Targets[i].Count.ToString(CultureInfo.InvariantCulture)));
            }
            if (post.Targets.Count == 0)
            {
                box.Note(TechText.T("reallife_tech_mgr_target_none"));
            }
            int n = _director.RolesAt(place, _atRoles);
            if (n > 0 && box.HasSelector)
            {
                _targetAt = System.Math.Max(0, System.Math.Min(n - 1, _targetAt));
                box.Selector(n, _targetAt, i => TechText.Role(_atRoles[i]), i =>
                {
                    _targetAt = i;
                    Refresh();
                });
                box.Selector(20, System.Math.Max(0, System.Math.Min(19, _targetCount - 1)), i => TechText.Fill("reallife_tech_mgr_target_count", "count", (i
                    + 1).ToString(CultureInfo.InvariantCulture)), i =>
                {
                    _targetCount = i + 1;
                    Refresh();
                });
                Transform row = box.ButtonRow();
                int role = _atRoles[_targetAt], count = _targetCount;
                InfoBox.Button(row, InfoBox.Positive, "reallife_tech_mgr_button_target", () => Do(() => _director.SetManagerTarget(place, role, count)));
                InfoBox.Button(row, InfoBox.Additional, "reallife_tech_mgr_button_clear", () => Do(() => _director.ClearManagerTargets(place)));
            }
            InfoBox.Button(box.ButtonRow(), InfoBox.Negative, "reallife_tech_mgr_button_remove", () => GameConfirm.Show("reallife_tech_mgr_confirm_remove_title".Localize(new { name = person.Name }),
                "reallife_tech_mgr_confirm_remove_body".Localize(new { name = person.Name, firm = person.Business }), () => UiClone.Safe(() => Do(() => _director.RemoveManager(place))),
                    null, null, null, true, true));
        }

        private void Duty(InfoBox box, ManagerPost post, string place, int duty, string key)
        {
            box.Switch(key, post.Has(duty), on =>
            {
                _director.SetManagerDuty(place, duty, on);
                Refresh();
            });
        }

        private void ConfirmName(Person person)
        {
            if (!_director.PlanManager(person.File.Id, out ManagerPlan plan))
            {
                return;
            }
            string id = person.File.Id, name = plan.Name, firm = plan.Firm;
            string body = _director.NameBody(plan);
            GameConfirm.Show("reallife_tech_mgr_confirm_name_title".Localize(new { name, firm }), "reallife_tech_mgr_confirm_name_text".Localize(new { body }),
                () => UiClone.Safe(() => Do(() => _director.NameManager(id))), null, null, null, true, false);
        }
    }
}
