using System;
using System.Collections.Generic;
using System.Reflection;
using BAModAPI;
using Entities;
using Localizor;
using UI;
using UI.Notification;
using UI.Smartphone.Apps.MyEmployees;
using UI.Smartphone.Apps.MyEmployees.Types;
using UnityEngine;
using UnityEngine.Events;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// "Give a raise" in the game's own My Employees app, among its actions for several picked employees (beside paying
    /// bonuses and training): the game's tick boxes, its filter by business, its panel of choices with its confirm button and
    /// its question box. The raise itself is the Company app's (<see cref="CompanyDirector.GivePicked"/>), so the game's employee
    /// is never written. The action is put in the game's two lists of mass actions by name (GameContract.MassActions,
    /// MassActionTabs) while the staff part runs, and taken out again when it stops. Going Public: the picked employees are the
    /// player's own, and the raise lives in this player's save only; nothing is sent.
    /// COST: one dictionary look a second while it stands; everything else on the player's clicks.
    /// </summary>
    internal static class PickedRaiseGame
    {
        public const string Type = "reallife_co_mass_raise";   // the action's name in the game's lists, and its words in the drop-down
        private const string EmployeesTab = "Employees";        // My Employees' tab of hired people, as the game names it

        private static IModLogger Logger => CompanyDirector.Logger;

        private static PickedRaiseAction _action;
        private static string[] _tabs = new string[0];   // the tabs it was added to
        private static bool _off, _toldOff;
        private static readonly List<string> Ids = new List<string>(16);
        private static readonly UnityAction<int> OnChosen = Guard.Listener<int>(Chosen);

        /// <summary>The action stands in the game's lists while <paramref name="on"/>, and not otherwise. Called each second.</summary>
        public static void Keep(bool on)
        {
            if (_off)
            {
                return;
            }
            try
            {
                Dictionary<string, EmployeeMassAction> actions = Actions();
                Dictionary<string, string[]> tabs = Tabs();
                if (actions == null || tabs == null)
                {
                    Off("the game's lists of actions for several picked employees are not there");
                    return;
                }
                bool stands = _action != null && actions.TryGetValue(Type, out EmployeeMassAction held) && ReferenceEquals(held, _action);
                if (on && !stands && actions.Count > 0)
                {
                    Put(actions, tabs);
                }
                else if (!on && stands)
                {
                    Take(actions, tabs);
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                Off("an error above");
            }
        }

        private static Dictionary<string, EmployeeMassAction> Actions() => GameContract.MassActions.Field?.GetValue(null) as Dictionary<string, EmployeeMassAction>;

        private static Dictionary<string, string[]> Tabs() => GameContract.MassActionTabs.Field?.GetValue(null) as Dictionary<string, string[]>;

        // Added to every tab that offers paying bonuses, which only hired people can be given; the employees' tab if none does.
        private static void Put(Dictionary<string, EmployeeMassAction> actions, Dictionary<string, string[]> tabs)
        {
            var found = new List<string>(2);
            foreach (KeyValuePair<string, string[]> tab in tabs)
            {
                string[] types = tab.Value;
                for (int i = 0; types != null && i < types.Length; i++)
                {
                    if (actions.TryGetValue(types[i], out EmployeeMassAction a) && a is PayBonusesMassAction && !found.Contains(tab.Key))
                    {
                        found.Add(tab.Key);
                    }
                }
            }
            if (found.Count == 0 && tabs.ContainsKey(EmployeesTab))
            {
                found.Add(EmployeesTab);
            }
            if (found.Count == 0)
            {
                Off("My Employees has no tab of hired people to add it to");
                return;
            }
            if (_action == null)
            {
                _action = ScriptableObject.CreateInstance<PickedRaiseAction>();
                _action.hideFlags = HideFlags.HideAndDontSave;   // the mod's own, never unloaded with a scene
                _action.name = Type;
            }
            _action.type = Type;
            _action.supportedTabs = found;
            actions[Type] = _action;
            for (int i = 0; i < found.Count; i++)
            {
                string[] types = tabs[found[i]] ?? new string[0];
                if (Array.IndexOf(types, Type) >= 0)
                {
                    continue;
                }
                var with = new string[types.Length + 1];
                Array.Copy(types, with, types.Length);
                with[types.Length] = Type;
                tabs[found[i]] = with;
            }
            _tabs = found.ToArray();
            Logger?.Info("Company: 'Give a raise' added to the game's actions for several picked employees in My Employees (" + string.Join(", ", _tabs) + ").");
        }

        private static void Take(Dictionary<string, EmployeeMassAction> actions, Dictionary<string, string[]> tabs)
        {
            actions.Remove(Type);
            for (int i = 0; i < _tabs.Length; i++)
            {
                if (tabs.TryGetValue(_tabs[i], out string[] types) && types != null && Array.IndexOf(types, Type) >= 0)
                {
                    var without = new List<string>(types);
                    without.Remove(Type);
                    tabs[_tabs[i]] = without.ToArray();
                }
            }
            UnityEngine.Object.Destroy(_action);   // made by code, so destroyed by code; made again if the part comes back
            _action = null;
            Logger?.Info("Company: 'Give a raise' taken out of My Employees' actions: the staff part is off.");
        }

        private static void Off(string why)
        {
            _off = true;
            if (!_toldOff)
            {
                _toldOff = true;
                Logger?.Warn("Company: no raise for several picked employees in My Employees this session: " + why + ". A raise for one is still given in the Company app.");
            }
        }

        /// <summary>The game's Perform: its panel of choices, as its own "assign a business" opens one.</summary>
        internal static void Perform()
        {
            MyEmployeesMassActionsUI ui = Ui();
            CompanyDirector company = CompanyDirector.Instance;
            if (ui == null || company == null || !company.PickedRaisesOn)
            {
                Notifications.ShowError("reallife_co_mass_raise_off");
                return;
            }
            var choices = new List<string>(StaffRules.PickedChoices);
            for (int i = 0; i < StaffRules.PickedChoices; i++)
            {
                choices.Add(CompanyDirector.PickedChoiceText(i));
            }
            ui.ShowMassActionOptionsPanel(Type + "_title", choices, OnChosen);
        }

        // A choice confirmed in the game's panel: what it comes to, asked in the game's question box, then given.
        private static void Chosen(int choice)
        {
            CompanyDirector company = CompanyDirector.Instance;
            if (choice < 0)
            {
                Notifications.ShowError("common_no_option_selected");
                return;
            }
            if (company == null || !company.PickedRaisesOn || !ReadPicked())
            {
                Notifications.ShowError(company == null || !company.PickedRaisesOn ? "reallife_co_mass_raise_off" : "myemployees_mass_action_no_employees_selected");
                return;
            }
            PickedPlan plan = company.PlanPicked(Ids, choice);
            if (plan.Raised == 0)
            {
                Notifications.Show(NotificationType.Error, "reallife_co_mass_raise_none", Words(plan), 9f, null, null, true, false);
                return;
            }
            var ids = new List<string>(Ids);
            GameConfirm.Show(Type.Localize(), "reallife_co_mass_raise_confirm".Localize(new
            {
                count = plan.Raised.ToString(), picked = plan.Picked.ToString(), what = CompanyDirector.PickedChoiceText(choice),
                before = CompanyText.Wage((int)Math.Min(int.MaxValue, plan.Before)), after = CompanyText.Wage((int)Math.Min(int.MaxValue, plan.After)),
                daybefore = CompanyText.Money(plan.DayBefore / 100f), dayafter = CompanyText.Money(plan.DayAfter / 100f), left = Left(plan)
            }), () => Give(ids, choice), null, "reallife_co_mass_raise_give", "common_cancel", false, false);
        }

        private static void Give(List<string> ids, int choice)
        {
            CompanyDirector company = CompanyDirector.Instance;
            int sum = 0;
            int count = company != null ? company.GivePicked(ids, choice, out sum) : 0;
            if (count > 0)
            {
                Notifications.Show(NotificationType.Success, "reallife_co_mass_raise_done", new Dictionary<string, string>
                {
                    { "count", count.ToString() }, { "raise", CompanyText.Wage(sum) }
                }, 9f, null, null, true, false);
            }
            MyEmployeesMassActionsUI ui = Ui();
            ui?.OnMassActionPerformed(false, true);   // as the game's own actions end: the panel shut, the ticks cleared, the rows drawn again
        }

        // The ids of the picked employees the game holds now; false with none.
        private static bool ReadPicked()
        {
            Ids.Clear();
            List<EmployeeInstance> picked = MyEmployeesMassActionsUI.massActionSelectedEmployees;
            for (int i = 0; picked != null && i < picked.Count; i++)
            {
                if (picked[i] != null && !picked[i].IsCandidate && !string.IsNullOrEmpty(picked[i].id))
                {
                    Ids.Add(picked[i].id);
                }
            }
            return Ids.Count > 0;
        }

        // Who is left out and why, for the question: empty when nobody is.
        private static string Left(PickedPlan plan)
        {
            return plan.Left == 0 ? string.Empty : " " + CompanyText.Fill("reallife_co_mass_raise_left", "count", plan.Left.ToString(), "days", CompanyCatalogue.RaiseRestDays.ToString());
        }

        private static Dictionary<string, string> Words(PickedPlan plan)
        {
            return new Dictionary<string, string> { { "picked", plan.Picked.ToString() }, { "days", CompanyCatalogue.RaiseRestDays.ToString() } };
        }

        private static MyEmployeesMassActionsUI Ui()
        {
            UIs uis = InstanceBehavior<UIs>.Instance;
            return uis != null && uis.fullMenu != null && uis.fullMenu.myEmployees != null ? uis.fullMenu.myEmployees.massActionsUI : null;
        }
    }

    /// <summary>The game's mass action for "Give a raise": its Perform is the game's call into the mod, behind the guard.</summary>
    internal sealed class PickedRaiseAction : EmployeeMassAction
    {
        private static readonly FeatureGuard Guarded = Guard.For("economy_company", "PickedRaiseAction", typeof(PickedRaiseAction));

        public override void Perform()
        {
            if (Guarded.Off)
            {
                return;
            }
            try
            {
                PickedRaiseGame.Perform();
            }
            catch (Exception e)
            {
                Guard.Fail(Guarded, e);
            }
        }
    }
}
