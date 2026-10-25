using System;
using System.Collections.Generic;
using RealLifeOverhaul.Careers;
using UI.Notification;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// The company's hierarchy (docs/features/economy-hierarchy.md): the policy every automatic decision obeys, set for the
    /// company, a region and a store, a lower scope never past the one above; the escalations that carry what a manager
    /// cannot settle up to whoever may; and the regional managers and executives above the managers. It runs on the
    /// managers' hour and day and costs nothing per frame. Switched off, every manager decides by their own limits exactly
    /// as before and nothing is escalated; what was set is kept for when it is on again.
    /// </summary>
    public sealed partial class CompanyDirector
    {
        private HierarchyState _hier;
        private bool _hierBroken;

        /// <summary>Whether the hierarchy runs: the managers do, its option is on and no fault has stopped it.</summary>
        public bool HierarchyOn => ManagersOn && Settings.EconomyHierarchy && _hier != null && !_hierBroken;

        internal HierarchyState HierarchyData => _running ? _hier : null;

        private void HierarchyFault(string where, Exception e)
        {
            _hierBroken = true;
            Logger?.Error(e);
            Logger?.Warn("Company: the hierarchy switched off for this session after an error in " + where
                         + ". The managers decide by their own limits again and nothing is escalated; the policy and the open escalations are kept.");
        }

        partial void HierarchyLoad(GameInstance save)
        {
            int version;
            _hier = HierarchyStore.Load(save.modData, out version);
            bool fresh = _hier == null;
            _hier = _hier ?? new HierarchyState { WeekFrom = save.Day };
            if (fresh)
            {
                KeepOwnLimits();
            }
            if (!fresh || Verbose)
            {
                Logger?.Info("Company: hierarchy " + (fresh ? "opened for this save at the starting policy" : "loaded (version " + version + ")") + ": "
                             + _hier.Regions.Count + " region(s) and " + _hier.Stores.Count + " store(s) with rules of their own, " + _hier.Templates.Count
                             + " template(s), " + _hier.OpenAt(EscalationRules.LevelOwner) + " escalation(s) waiting for you, " + _hier.Executives.Count + " executive(s).");
            }
        }

        // A save from before the policy: a manager's own pay band or budget that is not the starting one is that store's own rule.
        private void KeepOwnLimits()
        {
            if (_mgr == null)
            {
                return;
            }
            int kept = 0;
            for (int i = 0; i < _mgr.Managers.Count; i++)
            {
                ManagerFile m = _mgr.Managers[i];
                if (m.Region || (m.WageBand == ManagerCatalogue.WageBandDefault && m.Budget == ManagerCatalogue.BudgetDefault))
                {
                    continue;
                }
                PolicySet store = _hier.Want(false, m.Place);
                if (store == null)
                {
                    continue;
                }
                store.Steps[(int)PolicyField.PayBand] = m.WageBand != ManagerCatalogue.WageBandDefault ? m.WageBand : -1;
                store.Steps[(int)PolicyField.Budget] = m.Budget != ManagerCatalogue.BudgetDefault ? m.Budget : -1;
                kept++;
            }
            if (kept > 0)
            {
                Logger?.Info("Company: " + kept + " manager(s) had their own pay band or budget; each is kept as that store's own rule.");
            }
        }

        partial void HierarchySave(GameInstance save)
        {
            if (_hier != null)
            {
                save.modData[HierarchyStore.Key] = HierarchyStore.Write(_hier);
            }
        }

        private void HierarchyForget()
        {
            _hier = null;
        }

        // --- The policy in force ---

        /// <summary>The region a place is in for its regional manager and its rules: the region the player put it in, else its district.</summary>
        internal string AreaOf(ManagedPlace mp)
        {
            if (mp == null)
            {
                return null;
            }
            string picked = _hier != null ? _hier.AreaPicked(mp.Place.Place) : null;
            return picked ?? mp.District;
        }

        /// <summary>
        /// A rule's step in force at a place: the company's, its region's and its own (PolicyRules.Effective). With the
        /// hierarchy off, the manager's own limit where it has one and the starting step for the rest: what was decided before.
        /// </summary>
        internal int Rule(ManagedPlace mp, PolicyField field)
        {
            if (!HierarchyOn)
            {
                ManagerFile m = mp != null ? mp.Own : null;
                return field == PolicyField.PayBand && m != null ? m.WageBand : field == PolicyField.Budget && m != null ? m.Budget : PolicyCatalogue.Def(field).Default;
            }
            return PolicyRules.Effective(_hier.Company, mp != null ? _hier.Region(AreaOf(mp)) : null, mp != null ? _hier.Store(mp.Place.Place) : null, field);
        }

        /// <summary>A rule's step in force for a region as a whole: the company's and the region's.</summary>
        internal int RegionRule(string area, PolicyField field)
        {
            return HierarchyOn ? PolicyRules.Effective(_hier.Company, _hier.Region(area), null, field) : PolicyCatalogue.Def(field).Default;
        }

        /// <summary>Whether the policy lets the managers answer this kind by themselves; true for kinds no switch of it covers.</summary>
        private bool PolicyAllows(ManagedPlace mp, ManagerTask task)
        {
            int field = PolicyRules.SwitchOf(task);
            return field < 0 || Rule(mp, (PolicyField)field) == 1;
        }

        /// <summary>
        /// The pay band and the budget in force written into each manager's file, so their panel and their month say what
        /// binds them, and so they keep deciding by it if the hierarchy is switched off later.
        /// </summary>
        internal void PolicyMirror()
        {
            if (!HierarchyOn)
            {
                return;
            }
            for (int i = 0; i < _managed.Count; i++)
            {
                ManagedPlace mp = _managed[i];
                if (mp.Own != null)
                {
                    mp.Own.WageBand = Rule(mp, PolicyField.PayBand);
                    mp.Own.Budget = Rule(mp, PolicyField.Budget);
                }
            }
        }

        /// <summary>
        /// One rule of a scope set by the owner: the company's (scope null), a region's or a store's; -1 takes a lower
        /// scope's own rule away. A lower scope's step past the one above is kept as the owner chose it and held to the one
        /// above while that stands. Applied at once.
        /// </summary>
        public void PolicySetRule(int scopeKind, string scope, PolicyField field, int step)
        {
            if (!HierarchyOn)
            {
                return;
            }
            PolicyDef d = PolicyCatalogue.Def(field);
            PolicySet set = scopeKind == ScopeCompany ? _hier.Company : _hier.Want(scopeKind == ScopeRegion, scope);
            if (set == null || (scopeKind != ScopeCompany && d.Way == PolicyWay.CompanyOnly))
            {
                Notify(NotificationType.Warning, "reallife_pol_note_cannot", null, 7f);
                return;
            }
            set.Steps[(int)field] = scopeKind == ScopeCompany ? CompanyCatalogue.Clamp(step, 0, d.Steps.Length - 1) : step < 0 ? -1 : CompanyCatalogue.Clamp(step, 0, d.Steps.Length - 1);
            ManagedPlace mp = scopeKind == ScopeStore ? ManagedAt(scope) : null;
            if (mp != null && mp.Own != null && field == PolicyField.PayBand && step >= 0)
            {
                mp.Own.WageBand = set.Steps[(int)field];   // the store's own, as the manager's panel has always set it
            }
            if (mp != null && mp.Own != null && field == PolicyField.Budget && step >= 0)
            {
                mp.Own.Budget = set.Steps[(int)field];
            }
            PolicyMirror();
            Logger?.Info("Company: policy " + d.Key + " of " + ScopeName(scopeKind, scope)
                         + (step < 0 ? " now follows the scope above" : " set to step " + step + " (" + PolicyCatalogue.Value(field, step) + ")") + ".");
            Changed();
        }

        public const int ScopeCompany = 0, ScopeRegion = 1, ScopeStore = 2;

        /// <summary>
        /// A manager's own pay band or budget, set on their panel, kept as their store's rule, whether the hierarchy is on or
        /// not, so switching it on later keeps what the player chose. With it on, the rule in force is written back at once.
        /// </summary>
        private void StoreOwn(string place, PolicyField field, int step)
        {
            PolicySet store = _hier != null ? _hier.Want(false, place) : null;
            if (store == null)
            {
                return;
            }
            store.Steps[(int)field] = step;
            PolicyMirror();
        }

        internal string ScopeName(int scopeKind, string scope)
        {
            return scopeKind == ScopeCompany ? "the company" : scopeKind == ScopeRegion ? "region " + AreaName(scope) : ManagedName(scope);
        }

        /// <summary>A region's name for the player: its district's, or a region they made.</summary>
        internal static string AreaName(string area)
        {
            int n = HierarchyRules.CustomNumber(area);
            return n > 0 ? CompanyText.T("reallife_hier_region_custom", "letter", HierarchyRules.Letter(n)) : CompanyText.District(area);
        }

        /// <summary>A template's steps put on a scope (built in by number, or one of the owner's); every rule at once.</summary>
        public void PolicyApplyTemplate(int scopeKind, string scope, int builtIn, int own)
        {
            if (!HierarchyOn)
            {
                return;
            }
            int[] steps = null;
            if (own > 0)
            {
                for (int i = 0; i < _hier.Templates.Count && steps == null; i++)
                {
                    steps = _hier.Templates[i].Number == own ? _hier.Templates[i].Set.Steps : null;
                }
            }
            else if (builtIn >= 0 && builtIn < PolicyCatalogue.Templates.Length)
            {
                steps = PolicyCatalogue.Templates[builtIn];   // the starting policy is none: PolicyRules.Apply puts every rule at its start
            }
            PolicySet set = scopeKind == ScopeCompany ? _hier.Company : _hier.Want(scopeKind == ScopeRegion, scope);
            if (set == null)
            {
                Notify(NotificationType.Warning, "reallife_pol_note_cannot", null, 7f);
                return;
            }
            PolicySet above = AboveOf(scopeKind, scope);
            PolicyRules.Apply(steps, set, above, scopeKind == ScopeCompany);
            ManagedPlace mp = scopeKind == ScopeStore ? ManagedAt(scope) : null;
            if (mp != null && mp.Own != null)
            {
                mp.Own.WageBand = set.Sets(PolicyField.PayBand) ? set.Of(PolicyField.PayBand) : mp.Own.WageBand;
                mp.Own.Budget = set.Sets(PolicyField.Budget) ? set.Of(PolicyField.Budget) : mp.Own.Budget;
            }
            PolicyMirror();
            Logger?.Info("Company: a template (" + (own > 0 ? "yours, number " + own : "built in, number " + builtIn) + ") put on " + ScopeName(scopeKind, scope) + ".");
            Notify(NotificationType.Success, "reallife_pol_note_applied", new Dictionary<string, string> { { "scope", ScopeWords(scopeKind, scope) } }, 7f);
            Changed();
        }

        /// <summary>A lower scope's own rules all taken away: it follows the scope above in everything.</summary>
        public void PolicyClear(int scopeKind, string scope)
        {
            PolicySet set = !HierarchyOn || scopeKind == ScopeCompany ? null : scopeKind == ScopeRegion ? _hier.Region(scope) : _hier.Store(scope);
            if (set == null)
            {
                return;
            }
            set.Clear();
            PolicyMirror();
            Logger?.Info("Company: " + ScopeName(scopeKind, scope) + " now follows the scope above in every rule.");
            Changed();
        }

        /// <summary>The scope's rules in force kept as a template of the owner's; the oldest goes past the most kept.</summary>
        public void PolicySaveTemplate(int scopeKind, string scope)
        {
            if (!HierarchyOn)
            {
                return;
            }
            if (_hier.Templates.Count >= PolicyCatalogue.TemplatesMost)
            {
                _hier.Templates.RemoveAt(0);
            }
            var tpl = new PolicyTemplate { Number = _hier.NextTemplate++, Day = Save.Day };
            for (int f = 0; f < (int)PolicyField.Count; f++)
            {
                tpl.Set.Steps[f] = ScopeRule(scopeKind, scope, (PolicyField)f);
            }
            _hier.Templates.Add(tpl);
            Logger?.Info("Company: the rules in force for " + ScopeName(scopeKind, scope) + " kept as template " + tpl.Number + ".");
            Notify(NotificationType.Success, "reallife_pol_note_saved", new Dictionary<string, string> { { "number", tpl.Number.ToString() } }, 7f);
            Changed();
        }

        public void PolicyDeleteTemplate(int number)
        {
            for (int i = _hier != null ? _hier.Templates.Count - 1 : -1; i >= 0; i--)
            {
                if (_hier.Templates[i].Number == number)
                {
                    _hier.Templates.RemoveAt(i);
                    Changed();
                }
            }
        }

        /// <summary>A rule's step in force for a scope: the company's own, a region's with the company's above it, a store's with both.</summary>
        internal int ScopeRule(int scopeKind, string scope, PolicyField field)
        {
            if (_hier == null)
            {
                return PolicyCatalogue.Def(field).Default;
            }
            if (scopeKind == ScopeCompany)
            {
                return PolicyRules.Effective(_hier.Company, null, null, field);
            }
            if (scopeKind == ScopeRegion)
            {
                return PolicyRules.Effective(_hier.Company, _hier.Region(scope), null, field);
            }
            ManagedPlace mp = ManagedAt(scope);
            return PolicyRules.Effective(_hier.Company, mp != null ? _hier.Region(AreaOf(mp)) : null, _hier.Store(scope), field);
        }

        // The rules in force above a scope, as one set: nothing above the company, the company above a region, both above a store.
        private PolicySet AboveOf(int scopeKind, string scope)
        {
            if (scopeKind == ScopeCompany)
            {
                return null;
            }
            var above = new PolicySet();
            ManagedPlace mp = scopeKind == ScopeStore ? ManagedAt(scope) : null;
            string area = mp != null ? AreaOf(mp) : null;
            for (int f = 0; f < (int)PolicyField.Count; f++)
            {
                above.Steps[f] = scopeKind == ScopeRegion || area == null ? ScopeRule(ScopeCompany, null, (PolicyField)f) : ScopeRule(ScopeRegion, area, (PolicyField)f);
            }
            return above;
        }

        /// <summary>A scope in a few words for a notice: the company, a region, a store.</summary>
        internal string ScopeWords(int scopeKind, string scope)
        {
            return scopeKind == ScopeCompany ? CompanyText.T("reallife_pol_scope_company") : scopeKind == ScopeRegion ? AreaName(scope) : ManagedName(scope);
        }

        /// <summary>Whether the player has places in a region: a district of theirs, or a region they made with a store in it.</summary>
        internal bool HasArea(string area)
        {
            return HasDistrict(area);
        }

        /// <summary>A place's gap filled now from its dashboard, as its manager and the policy would: its own board, then the region's boards and moves the policy allows.</summary>
        public void HierarchyFillNow(string place)
        {
            ManagedPlace mp = HierarchyOn ? ManagedAt(place) : null;
            if (mp == null || mp.Own == null || mp.Own.Keep <= mp.Place.Staff)
            {
                Notify(NotificationType.Info, "reallife_hier_note_no_gap", null, 7f);
                return;
            }
            int before = mp.Place.Staff;
            try
            {
                HireOwnBoard(mp, false);
                if (mp.Place.Staff < mp.Own.Keep)
                {
                    FillGap(mp, (int)Rung.StoreManager, Rule(mp, PolicyField.Sources), mp.Own.Name, Save.Day, Save.Hour, false);
                }
            }
            catch (Exception e)
            {
                HierarchyFault("filling a gap", e);
                return;
            }
            int came = mp.Place.Staff - before;
            Notify(came > 0 ? NotificationType.Success : NotificationType.Warning, came > 0 ? "reallife_hier_note_filled" : "reallife_hier_note_gap_left",
                new Dictionary<string, string> { { "place", mp.Place.Name }, { "count", came.ToString() } }, 8f);
            Changed();
        }

        // The place's own job board, inside its policy, as its manager hires at 11: what a manager would pick, without a
        // mistake; the owner's own press is not held to the manager's budget. Returns how many were hired.
        private int HireOwnBoard(ManagedPlace mp, bool owner)
        {
            ManagerFile m = mp.Own;
            int hired = 0;
            for (int guard = 0; guard < 8 && mp.Place.Staff < m.Keep; guard++)
            {
                ManagerGame.Candidates(mp.Place.Reg, _candidates);
                int count = _candidates.Count;
                if (count == 0)
                {
                    break;
                }
                if (_hireWage.Length < count)
                {
                    _hireWage = new int[count * 2];
                    _hireGoing = new int[count * 2];
                    _hireSkill = new int[count * 2];
                }
                int income = mp.Shop != null ? mp.Shop.Income : UnityEngine.Mathf.RoundToInt(CityEconomy.DistrictIncome(mp.District));
                for (int i = 0; i < count; i++)
                {
                    _hireWage[i] = ManagerGame.WageNow(_candidates[i], _index);
                    _hireGoing[i] = ManagerGame.Going(_candidates[i], _index, _cycle, income);
                    _hireSkill[i] = ManagerGame.SkillOf(_candidates[i]);
                }
                int pick = PolicyRules.Pick(_hireWage, _hireGoing, _hireSkill, count, Rule(mp, PolicyField.PayBand), Rule(mp, PolicyField.HirePayCap), Rule(mp, PolicyField.SkillFloor),
                    owner ? int.MaxValue / 4 : ManagerRules.Left(BudgetOf(m), m.Spent), false);
                Entities.EmployeeInstance e = pick >= 0 ? _candidates[pick] : null;
                string name = e != null ? ManagerGame.NameOf(e) : null;
                if (e == null || !ManagerGame.Hire(e, mp.Place.Reg))
                {
                    break;
                }
                int cost = ManagerRules.HireCost(_hireWage[pick]);
                m.Spent += cost;
                mp.Place.Staff++;
                hired++;
                HierarchyEntry entry = _hier.Log(HierarchyLog.Hired, owner ? (int)Rung.Owner : (int)Rung.StoreManager, owner ? CompanyText.T("reallife_hier_you") : m.Name,
                    mp.Place.Place, Save.Day, Save.Hour);
                entry.A = _hireWage[pick];
                entry.B = _hireSkill[pick];
                entry.C = cost;
                entry.Detail = HierarchyStore.Clean(name);
            }
            _candidates.Clear();
            return hired;
        }

        /// <summary>A place's underpaid staff raised now from its dashboard, inside the band and as often as the policy allows.</summary>
        public void HierarchyRaiseNow(string place)
        {
            ManagedPlace mp = HierarchyOn ? ManagedAt(place) : null;
            if (mp == null)
            {
                return;
            }
            int count;
            try
            {
                count = BatchRaise(mp, (int)Rung.StoreManager, mp.Own != null ? mp.Own.Name : CompanyText.T("reallife_hier_you"), Save.Day, Save.Hour, false);
            }
            catch (Exception e)
            {
                HierarchyFault("a batch of raises", e);
                return;
            }
            Notify(count > 0 ? NotificationType.Success : NotificationType.Info, count > 0 ? "reallife_hier_note_raised" : "reallife_hier_note_none_underpaid",
                new Dictionary<string, string> { { "place", mp.Place.Name }, { "count", count.ToString() } }, 8f);
            Changed();
        }

        /// <summary>The owner's Deep Mode: every level's pages, not only what needs them.</summary>
        public void HierarchyDeep(bool on)
        {
            if (_hier == null)
            {
                return;
            }
            _hier.Deep = on;
            Changed();
        }
    }
}
