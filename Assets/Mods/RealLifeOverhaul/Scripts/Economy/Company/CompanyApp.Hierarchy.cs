using System;
using System.Collections.Generic;
using RealLifeOverhaul.Careers;
using RealLifeOverhaul.GameUi;
using UnityEngine;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// The hierarchy's pages on the Managers tab, in the table and the white box as the tab's others: what is waiting for
    /// the owner (the exceptions), one escalation with its story and the owner's answers, the policy centre for the
    /// company, a region or a store with each rule's steps, the templates, and a place's dashboard (its key figures, what is
    /// pending, and its quick actions). A page is written when it is shown or something changes, never per frame. The picks:
    ///   "hx:" the exceptions; "he:id" one; "pc:" the policy centre; "pf:field" one rule; "pt:" the templates;
    ///   "pk:b n" or "pk:o n" one template; "db:place" a place's dashboard.
    /// </summary>
    public sealed partial class CompanyApp
    {
        private const int EscalationsShown = 60;   // escalations in the exceptions' table, the newest first

        private static readonly string[] EscalationKeys =
        {
            "reallife_hier_kind_gap", "reallife_hier_kind_budget", "reallife_hier_kind_demand", "reallife_hier_kind_mood", "reallife_hier_kind_wave",
            "reallife_hier_kind_kpi", "reallife_hier_kind_exception", "reallife_hier_kind_proposal"
        };
        private static readonly string[] EscalationStateKeys =
        {
            "reallife_hier_state_open", "reallife_hier_state_resolved", "reallife_hier_state_cleared", "reallife_hier_state_dismissed", "reallife_hier_state_lapsed"
        };

        private readonly int[] _kindCounts = new int[(int)EscalationKind.Count];
        private int _polKind = CompanyDirector.ScopeCompany;
        private string _polScope;
        private string _escBack;   // where an escalation's Back goes: the exceptions, or the dashboard it was opened from

        // The hierarchy's own pages; false for a pick that is not one of them.
        private bool WriteHierarchy(InfoBox box, ManagerState ms, ref string selected)
        {
            HierarchyState hs = _director.HierarchyData;
            if (_pick == null || hs == null || !_director.HierarchyOn)
            {
                return false;
            }
            if (_pick == "hx:")
            {
                _escBack = "hx:";
                WriteInbox(box, hs, ref selected);
                return true;
            }
            string rest = Picked("he:");
            int id;
            Escalation e = rest != null && int.TryParse(rest, out id) ? hs.Find(id) : null;
            if (e != null)
            {
                WriteEscalationStory(hs, e, ref selected);
                WriteEscalation(box, e);
                return true;
            }
            if (_pick == "pc:")
            {
                WritePolicies(box, ref selected);
                return true;
            }
            rest = Picked("pf:");
            int field;
            if (rest != null && int.TryParse(rest, out field) && field >= 0 && field < (int)PolicyField.Count)
            {
                WritePolicies(null, ref selected);
                WriteRule(box, (PolicyField)field);
                return true;
            }
            if (_pick == "pt:")
            {
                WriteTemplates(hs, ref selected);
                WriteTemplatesBox(box);
                return true;
            }
            rest = Picked("pk:");
            if (rest != null && rest.Length > 1 && int.TryParse(rest.Substring(1), out id))
            {
                WriteTemplates(hs, ref selected);
                WriteTemplate(box, hs, rest[0] == 'o', id);
                return true;
            }
            rest = Picked("db:");
            ManagedPlace mp = rest != null ? _director.ManagedAt(rest) : null;
            if (mp != null)
            {
                _escBack = _pick;
                WriteDashboardRows(hs, mp, ref selected);
                WriteDashboard(box, mp);
                return true;
            }
            return false;
        }

        // The directory's first rows: what waits for the owner, and the company's policy.
        private void HierarchyRows(ref string selected)
        {
            HierarchyState hs = _director.HierarchyData;
            if (hs == null || !_director.HierarchyOn)
            {
                return;
            }
            int yours = hs.OpenAt(EscalationRules.LevelOwner), others = hs.OpenAt(EscalationRules.LevelRegion) + hs.OpenAt(EscalationRules.LevelExecutive);
            Pick("hx:", CompanyText.T("reallife_hier_dir_inbox"), yours.ToString(), string.Empty, others.ToString(), 0, ref selected);
            Last.Set(4, yours > 0 ? CompanyText.T("reallife_mgr_dir_look") : CompanyText.T("reallife_co_state_in_order"), yours > 0 ? CellTone.Yellow : CellTone.Plain);
            int own = hs.Regions.Count + hs.Stores.Count;
            Pick("pc:", CompanyText.T("reallife_hier_dir_policy"), CompanyText.T("reallife_pol_dir_own", "count", own.ToString()), string.Empty, string.Empty, 0, ref selected);
            Last.Set(4, CompanyText.T("reallife_pol_dir_open"));
        }

        private string KindWords(int kind)
        {
            return CompanyText.T(EscalationKeys[CompanyCatalogue.Clamp(kind, 0, EscalationKeys.Length - 1)]);
        }

        // Who holds an escalation now, in a few words.
        private string HolderWords(Escalation e)
        {
            if (!e.Open)
            {
                return e.By.Length > 0 ? e.By : CompanyText.T("reallife_mgr_none");
            }
            if (e.Level >= EscalationRules.LevelOwner)
            {
                return CompanyText.T("reallife_hier_you");
            }
            return CompanyText.T(e.Level == EscalationRules.LevelRegion ? "reallife_hier_held_region" : "reallife_hier_held_executive");
        }

        private string PlaceWords(Escalation e)
        {
            if (e.Place.Length == 0)
            {
                return CompanyText.T("reallife_pol_scope_company");
            }
            ManagedPlace mp = _director.ManagedAt(e.Place);
            return mp != null ? mp.Place.Name : e.Detail;
        }

        // --- What waits ---

        private void WriteInbox(InfoBox box, HierarchyState hs, ref string selected)
        {
            _spec = CompanyTables.Escalations;
            int shown = 0, yours = 0, others = 0;
            int[] kinds = _kindCounts;
            Array.Clear(kinds, 0, kinds.Length);
            for (int i = hs.Escalations.Count - 1; i >= 0 && shown < EscalationsShown; i--)
            {
                Escalation e = hs.Escalations[i];
                shown++;
                Pick("he:" + e.Id, KindWords(e.Kind), PlaceWords(e), HolderWords(e), CompanyText.T("reallife_mgr_row_day", "day", e.Day.ToString()), 0, ref selected);
                TableRow row = Last;
                row.Kind = !e.Open ? CompanyTables.HeldClosed : e.Level >= EscalationRules.LevelOwner ? CompanyTables.HeldOwner
                    : e.Level == EscalationRules.LevelRegion ? CompanyTables.HeldRegion : CompanyTables.HeldExecutive;
                row.Set(3, CompanyText.T("reallife_mgr_row_day", "day", e.Day.ToString()), e.Day);
                row.Set(4, CompanyText.T(EscalationStateKeys[CompanyCatalogue.Clamp(e.State, 0, EscalationStateKeys.Length - 1)]),
                    e.Open && e.Level >= EscalationRules.LevelOwner ? CellTone.Yellow : CellTone.Plain);
                if (e.Open)
                {
                    yours += e.Level >= EscalationRules.LevelOwner ? 1 : 0;
                    others += e.Level < EscalationRules.LevelOwner ? 1 : 0;
                    kinds[CompanyCatalogue.Clamp(e.Kind, 0, kinds.Length - 1)]++;
                }
            }
            if (shown == 0)
            {
                Say("none", CompanyText.T("reallife_hier_inbox_none"));
            }
            box.Title(CompanyText.T("reallife_hier_dir_inbox"));
            box.Chip(CompanyText.T("reallife_hier_chip_yours", "count", yours.ToString()), yours > 0 ? CellTone.Yellow : CellTone.Green);
            InfoBox.Button(box.ButtonRow(), InfoBox.Additional, "reallife_co_back", Back);
            box.Section("reallife_hier_sec_waiting");
            box.RowPair("reallife_hier_held_you", yours.ToString(), "reallife_hier_with_others", others.ToString());
            for (int k = 0; k < kinds.Length; k++)
            {
                if (kinds[k] > 0)
                {
                    box.RowWords(KindWords(k), kinds[k].ToString());
                }
            }
            box.Note(CompanyText.T("reallife_hier_inbox_what"));
            Meta.Guide.Point(box, "reallife_meta_title_economy_hierarchy");
        }

        // An escalation's story: when it was raised, who passed it on and who settled it, from the hierarchy's log.
        private void WriteEscalationStory(HierarchyState hs, Escalation e, ref string selected)
        {
            _spec = CompanyTables.EscalationLog;
            int rows = 0;
            for (int i = hs.Entries.Count - 1; i >= 0; i--)
            {
                HierarchyEntry entry = hs.Entries[i];
                if ((entry.Kind != (int)HierarchyLog.Raised && entry.Kind != (int)HierarchyLog.Passed && entry.Kind != (int)HierarchyLog.Settled) || entry.A != e.Id)
                {
                    continue;
                }
                rows++;
                Pick("le:" + entry.Id, EntryWhat(entry), EntryDetail(entry), CompanyText.T("reallife_mgr_row_day", "day", entry.Day.ToString()),
                    entry.Who.Length > 0 ? entry.Who : CompanyText.T("reallife_hier_you"), 0, ref selected);
                Last.Pick = false;
            }
            if (rows == 0)
            {
                Say("none", CompanyText.T("reallife_hier_story_none"));
            }
        }

        private void WriteEscalation(InfoBox box, Escalation e)
        {
            int id = e.Id;
            ManagedPlace mp = _director.ManagedAt(e.Place);
            string place = e.Place;
            box.Title(KindWords(e.Kind));
            box.Subtitle(PlaceWords(e));
            box.Chip(CompanyText.T(EscalationStateKeys[CompanyCatalogue.Clamp(e.State, 0, EscalationStateKeys.Length - 1)]), e.Open ? CellTone.Yellow : CellTone.Green);
            if (e.Open)
            {
                box.Chip(CompanyText.T("reallife_hier_chip_with", "who", HolderWords(e)));
            }
            box.Line(EscalationWords(e));
            bool yours = e.Open && e.Level >= EscalationRules.LevelOwner;
            Transform buttons = box.ButtonRow();
            if (yours)
            {
                switch ((EscalationKind)e.Kind)
                {
                    case EscalationKind.Gap:
                        InfoBox.Button(buttons, InfoBox.Positive, "reallife_hier_do_fill", () => _director.HierarchySettle(id, 0));
                        break;
                    case EscalationKind.Budget:
                        InfoBox.Button(buttons, InfoBox.Positive, "reallife_hier_do_budget", () => _director.HierarchySettle(id, 0));
                        break;
                    case EscalationKind.Mood:
                    case EscalationKind.Wave:
                        InfoBox.Button(buttons, InfoBox.Positive, "reallife_hier_do_raise", () => _director.HierarchySettle(id, 0));
                        break;
                    case EscalationKind.Exception:
                    case EscalationKind.Proposal:
                        InfoBox.Button(buttons, InfoBox.Positive, e.Kind == (int)EscalationKind.Proposal ? "reallife_hier_do_approve" : "reallife_hier_do_agree",
                            () => _director.HierarchySettle(id, 0));
                        InfoBox.Button(buttons, InfoBox.Negative, e.Kind == (int)EscalationKind.Proposal ? "reallife_hier_do_reject" : "reallife_hier_do_refuse",
                            () => _director.HierarchySettle(id, 1));
                        break;
                }
                if (e.Kind != (int)EscalationKind.Exception && e.Kind != (int)EscalationKind.Proposal)
                {
                    InfoBox.Button(buttons, InfoBox.Additional, "reallife_hier_do_dismiss", () => _director.HierarchyDismiss(id));
                }
            }
            else if (e.Open && _director.HierarchyData.Deep)
            {
                // Deep Mode: the owner steps in at the level that holds it, as its holder would.
                InfoBox.Button(buttons, InfoBox.Positive, "reallife_hier_do_try", () => _director.HierarchyTryNow(id));
                InfoBox.Button(buttons, InfoBox.Additional, "reallife_hier_do_pass", () => _director.HierarchyPass(id));
            }
            InfoBox.Button(buttons, InfoBox.Additional, "reallife_co_back", () => Go(_escBack ?? "hx:"));
            Transform links = mp != null ? box.ButtonRow() : null;
            if (mp != null)
            {
                if (e.Kind == (int)EscalationKind.Demand || e.Kind == (int)EscalationKind.Mood || e.Kind == (int)EscalationKind.Wave || e.Kind == (int)EscalationKind.Exception)
                {
                    InfoBox.Button(links, InfoBox.Additional, "reallife_hier_open_staff", () => OpenStaff(e.Kind == (int)EscalationKind.Exception ? "emp:" + e.Subject : null));
                }
                InfoBox.Button(links, InfoBox.Additional, "reallife_hier_open_dashboard", () => Go("db:" + place));
                InfoBox.Button(links, InfoBox.Additional, "reallife_hier_open_policy", () => OpenPolicy(CompanyDirector.ScopeStore, place));
            }
            box.Section("reallife_hier_sec_story");
            box.RowPair("reallife_hier_raised", CompanyText.Day(e.Day), "reallife_hier_moved", CompanyText.Day(e.Moved));
            if (e.By.Length > 0)
            {
                box.Row(e.Open ? "reallife_hier_last_by" : "reallife_hier_settled_by", e.By);
            }
            box.Note(CompanyText.T(yours ? "reallife_hier_what_yours" : e.Open ? "reallife_hier_what_others" : "reallife_hier_what_closed"));
            Meta.Guide.Point(box, "reallife_meta_title_economy_hierarchy");
        }

        // The Staff tab, with one of the staff picked.
        private void OpenStaff(string pick)
        {
            _tab = CompanyDirector.TabStaff;
            _pick = pick;
            _table.ClearSelection();
            Refresh();
        }

        // What an escalation says, by kind, from its figures (CompanyDirector.Escalations raises each).
        private string EscalationWords(Escalation e)
        {
            switch ((EscalationKind)e.Kind)
            {
                case EscalationKind.Gap:
                    return CompanyText.T("reallife_hier_say_gap", "staff", e.A.ToString(), "keep", e.B.ToString(), "day", e.C.ToString());
                case EscalationKind.Budget:
                    return CompanyText.T("reallife_hier_say_budget", "spent", CompanyText.Money(e.A), "budget", CompanyText.Money(e.B));
                case EscalationKind.Demand:
                    return CompanyText.T(e.C == 1 ? "reallife_hier_say_demand_strike" : "reallife_hier_say_demand", "count", e.A.ToString(), "day", e.B.ToString());
                case EscalationKind.Mood:
                    return CompanyText.T("reallife_hier_say_mood", "standing", e.A.ToString(), "floor", e.B.ToString(), "count", e.C.ToString());
                case EscalationKind.Wave:
                    return CompanyText.T("reallife_hier_say_wave", "count", e.A.ToString(), "gone", e.B.ToString(), "leaving", e.C.ToString());
                case EscalationKind.Kpi:
                    return CompanyText.T("reallife_hier_say_kpi", "profit", CompanyText.Money(e.A), "target", CompanyText.Money(e.B), "days", e.C.ToString());
                case EscalationKind.Exception:
                    return CompanyText.T(e.C == 1 ? "reallife_hier_say_exception_offer" : "reallife_hier_say_exception", "name", e.Detail, "asked", CompanyText.Wage(e.A),
                        "ceiling", CompanyText.Wage(e.B));
                case EscalationKind.Proposal:
                    return ProposalWords(e);
                default:
                    return string.Empty;
            }
        }

        // A proposal in words: the rule, from the step in force to the one proposed, and why (its figure C, by area).
        private string ProposalWords(Escalation e)
        {
            int field = CompanyCatalogue.Clamp(e.A, 0, (int)PolicyField.Count - 1);
            return CompanyText.Fill("reallife_hier_say_proposal", "who", e.Detail, "rule", RuleName((PolicyField)field), "from", StepWords((PolicyField)field, e.C),
                "to", StepWords((PolicyField)field, e.B));
        }

        private string EntryWhat(HierarchyEntry entry)
        {
            return CompanyText.T(PolicyCatalogue.Mod + "_hier_log_" + ((HierarchyLog)CompanyCatalogue.Clamp(entry.Kind, 0, (int)HierarchyLog.Policy)).ToString().ToLowerInvariant());
        }

        // A log entry's detail in words, by kind (HierarchyLog says what each figure is).
        private string EntryDetail(HierarchyEntry entry)
        {
            switch ((HierarchyLog)entry.Kind)
            {
                case HierarchyLog.Raised:
                    return CompanyText.T("reallife_hier_logd_raised", "what", KindWords(entry.B), "to", LevelWords(entry.C));
                case HierarchyLog.Passed:
                    return CompanyText.T("reallife_hier_logd_passed", "what", KindWords(entry.C), "to", LevelWords(entry.B));
                case HierarchyLog.Settled:
                    return KindWords(entry.B);
                case HierarchyLog.Moved:
                {
                    string name = entry.Detail, from = string.Empty;
                    int star = entry.Detail.IndexOf('/');
                    if (star >= 0)
                    {
                        name = entry.Detail.Substring(0, star);
                        from = entry.Detail.Substring(star + 1);
                    }
                    return CompanyText.T("reallife_hier_logd_moved", "name", name, "from", from, "to", PlaceName(entry.Place));
                }
                case HierarchyLog.Approved:
                    return CompanyText.T("reallife_hier_logd_approved", "name", entry.Detail, "raise", CompanyText.Wage(entry.A));
                case HierarchyLog.TopUp:
                    return CompanyText.T("reallife_hier_logd_topup", "amount", CompanyText.Money(entry.A), "place", PlaceName(entry.Place));
                case HierarchyLog.Raises:
                    return CompanyText.T("reallife_hier_logd_raises", "count", entry.A.ToString(), "raise", CompanyText.Wage(entry.B), "place", PlaceName(entry.Place));
                case HierarchyLog.Hired:
                    return CompanyText.T("reallife_hier_logd_hired", "name", entry.Detail, "skill", entry.B.ToString(), "place", PlaceName(entry.Place));
                case HierarchyLog.Report:
                    return CompanyText.T("reallife_hier_logd_report", "open", entry.A.ToString(), "settled", entry.B.ToString(), "score", entry.C.ToString());
                case HierarchyLog.Paid:
                    return CompanyText.T("reallife_hier_logd_paid", "amount", CompanyText.Money(entry.A));
                case HierarchyLog.Proposed:
                case HierarchyLog.Policy:
                {
                    var field = (PolicyField)CompanyCatalogue.Clamp(entry.A, 0, (int)PolicyField.Count - 1);
                    return CompanyText.T("reallife_hier_logd_policy", "rule", RuleName(field), "to", StepWords(field, entry.B));
                }
                default:
                    return entry.Detail;
            }
        }

        private string PlaceName(string place)
        {
            ManagedPlace mp = place != null && place.Length > 0 ? _director.ManagedAt(place) : null;
            return mp != null ? mp.Place.Name : CompanyText.T("reallife_pol_scope_company");
        }

        private static string LevelWords(int level)
        {
            int l = CompanyCatalogue.Clamp(level, 0, (int)Rung.Owner);
            return l >= EscalationRules.LevelOwner ? CompanyText.T("reallife_hier_you") : CompanyText.T(LadderRanks.TitleKey(l, ExecSeat.None));
        }

        // --- The policy centre ---

        /// <summary>The policy centre opened on a scope: the company (null), a region or a store.</summary>
        private void OpenPolicy(int kind, string scope)
        {
            _polKind = kind;
            _polScope = scope;
            Go("pc:");
        }

        // The scope the centre shows, checked against what the company has now: a store or region gone falls back to the company.
        private void PolicyScopeNow()
        {
            if (_polKind == CompanyDirector.ScopeStore && _director.ManagedAt(_polScope) == null
                || _polKind == CompanyDirector.ScopeRegion && !_director.HasArea(_polScope))
            {
                _polKind = CompanyDirector.ScopeCompany;
                _polScope = null;
            }
        }

        // The store's region, or the region itself, for the region column; null for the company.
        private string PolicyArea()
        {
            if (_polKind == CompanyDirector.ScopeRegion)
            {
                return _polScope;
            }
            ManagedPlace mp = _polKind == CompanyDirector.ScopeStore ? _director.ManagedAt(_polScope) : null;
            return mp != null ? _director.AreaOf(mp) : null;
        }

        private void WritePolicies(InfoBox box, ref string selected)
        {
            PolicyScopeNow();
            HierarchyState hs = _director.HierarchyData;
            _spec = CompanyTables.Policies;
            string area = PolicyArea();
            PolicySet region = area != null ? hs.Region(area) : null, store = _polKind == CompanyDirector.ScopeStore ? hs.Store(_polScope) : null;
            for (int c = 0; c < (int)PolicyCategory.Count; c++)
            {
                Say("cat" + c, "<b>" + CompanyText.T(PolicyCatalogue.CategoryKeys[c]) + "</b>");
                for (int f = 0; f < PolicyCatalogue.Defs.Length; f++)
                {
                    PolicyDef d = PolicyCatalogue.Defs[f];
                    if ((int)d.Category != c)
                    {
                        continue;
                    }
                    int force = _director.ScopeRule(_polKind, _polScope, d.Field);
                    bool held = Held(d, region, store);
                    Pick("pf:" + f, RuleName(d.Field), StepWords(d.Field, hs.Company.Of(d.Field)), region != null && region.Sets(d.Field) ? StepWords(d.Field, region.Of(d.Field)) : Above(d, area),
                        store != null && store.Sets(d.Field) ? StepWords(d.Field, store.Of(d.Field)) : _polKind == CompanyDirector.ScopeStore ? Above(d, "store") : string.Empty,
                        held ? 1 : 0, ref selected);
                    Last.Set(4, StepWords(d.Field, force), held ? CellTone.Yellow : CellTone.Plain);
                }
            }
            if (box == null)
            {
                return;
            }
            box.Title(CompanyText.T("reallife_hier_dir_policy"));
            box.Subtitle(ScopeTitle());
            InfoBox.Button(box.ButtonRow(), InfoBox.Additional, "reallife_co_back", Back);
            box.Headline("reallife_pol_scope");
            int index, count = ScopeList(out index);
            if (!box.HasSelector || !box.Selector(count, index, ScopeItem, PickScope))
            {
                box.Line(ScopeTitle());
            }
            PolicySet own = _polKind == CompanyDirector.ScopeCompany ? null : _polKind == CompanyDirector.ScopeRegion ? region : store;
            box.Section("reallife_pol_sec_scope");
            box.Row("reallife_pol_own", _polKind == CompanyDirector.ScopeCompany ? CompanyText.T("reallife_pol_own_all") : (own != null ? own.Count : 0).ToString());
            Transform buttons = box.ButtonRow();
            InfoBox.Button(buttons, InfoBox.Additional, "reallife_pol_templates", () => Go("pt:"));
            int kind = _polKind;
            string scope = _polScope;
            InfoBox.Button(buttons, InfoBox.Additional, "reallife_pol_save_template", () => _director.PolicySaveTemplate(kind, scope));
            if (own != null && own.Count > 0)
            {
                InfoBox.Button(box.ButtonRow(), InfoBox.Negative, "reallife_pol_follow", () => _director.PolicyClear(kind, scope));
            }
            box.Note(CompanyText.T("reallife_pol_what"));
            box.Note(CompanyText.T("reallife_pol_held_what"));
            Meta.Guide.Point(box, "reallife_meta_title_economy_hierarchy");
        }

        // Whether a lower scope's own step is past the one above it and so held to it.
        private bool Held(PolicyDef d, PolicySet region, PolicySet store)
        {
            int company = PolicyRules.Effective(_director.HierarchyData.Company, null, null, d.Field);
            if (region != null && region.Sets(d.Field) && PolicyRules.Past(d.Way, company, region.Of(d.Field)))
            {
                return true;
            }
            int above = PolicyRules.Effective(_director.HierarchyData.Company, region, null, d.Field);
            return store != null && store.Sets(d.Field) && PolicyRules.Past(d.Way, above, store.Of(d.Field));
        }

        // An empty cell of a scope that sets nothing for the rule: "as above", or "company only".
        private static string Above(PolicyDef d, string scope)
        {
            return scope == null ? string.Empty : d.Way == PolicyWay.CompanyOnly ? CompanyText.T("reallife_pol_company_only") : CompanyText.T("reallife_pol_as_above");
        }

        private string ScopeTitle()
        {
            return _polKind == CompanyDirector.ScopeCompany ? CompanyText.T("reallife_pol_scope_company")
                : _polKind == CompanyDirector.ScopeRegion ? CompanyText.T("reallife_pol_scope_region", "region", CompanyDirector.AreaName(_polScope))
                : CompanyText.T("reallife_pol_scope_store", "store", _director.ManagedName(_polScope));
        }

        private readonly List<string> _scopeKeys = new List<string>(24);
        private readonly List<int> _scopeKinds = new List<int>(24);

        // The scopes a player may set, the company first, then the regions, then the stores with a manager; the current one's place.
        private int ScopeList(out int index)
        {
            _scopeKeys.Clear();
            _scopeKinds.Clear();
            _scopeKeys.Add(null);
            _scopeKinds.Add(CompanyDirector.ScopeCompany);
            IReadOnlyList<string> areas = _director.ManagerDistricts;
            for (int i = 0; i < areas.Count; i++)
            {
                _scopeKeys.Add(areas[i]);
                _scopeKinds.Add(CompanyDirector.ScopeRegion);
            }
            IReadOnlyList<ManagedPlace> places = _director.Managed;
            for (int i = 0; i < places.Count; i++)
            {
                if (places[i].Own != null)
                {
                    _scopeKeys.Add(places[i].Place.Place);
                    _scopeKinds.Add(CompanyDirector.ScopeStore);
                }
            }
            index = 0;
            for (int i = 0; i < _scopeKeys.Count; i++)
            {
                index = _scopeKinds[i] == _polKind && string.Equals(_scopeKeys[i], _polScope, StringComparison.Ordinal) ? i : index;
            }
            return _scopeKeys.Count;
        }

        private string ScopeItem(int i)
        {
            if (i < 0 || i >= _scopeKeys.Count)
            {
                return string.Empty;
            }
            return _scopeKinds[i] == CompanyDirector.ScopeCompany ? CompanyText.T("reallife_pol_scope_company")
                : _scopeKinds[i] == CompanyDirector.ScopeRegion ? CompanyText.T("reallife_pol_scope_region", "region", CompanyDirector.AreaName(_scopeKeys[i]))
                : CompanyText.T("reallife_pol_scope_store", "store", _director.ManagedName(_scopeKeys[i]));
        }

        private void PickScope(int i)
        {
            if (i < 0 || i >= _scopeKeys.Count)
            {
                return;
            }
            _polKind = _scopeKinds[i];
            _polScope = _scopeKeys[i];
            Refresh();
        }

        private void WriteRule(InfoBox box, PolicyField field)
        {
            PolicyDef d = PolicyCatalogue.Def(field);
            HierarchyState hs = _director.HierarchyData;
            int kind = _polKind;
            string scope = _polScope;
            box.Title(RuleName(field));
            box.Subtitle(CompanyText.T(PolicyCatalogue.CategoryKeys[(int)d.Category]) + ", " + ScopeTitle());
            InfoBox.Button(box.ButtonRow(), InfoBox.Additional, "reallife_co_back", () => Go("pc:"));
            box.Note(CompanyText.T(d.TextKey + "_what"));
            bool company = kind == CompanyDirector.ScopeCompany;
            if (!company && d.Way == PolicyWay.CompanyOnly)
            {
                box.Line(CompanyText.T("reallife_pol_company_only_line", "value", StepWords(field, hs.Company.Of(field))), CellTone.Yellow);
                return;
            }
            PolicySet own = company ? hs.Company : kind == CompanyDirector.ScopeRegion ? hs.Region(scope) : hs.Store(scope);
            int above = company ? -1 : kind == CompanyDirector.ScopeRegion ? _director.ScopeRule(CompanyDirector.ScopeCompany, null, field)
                : PolicyArea() != null ? _director.ScopeRule(CompanyDirector.ScopeRegion, PolicyArea(), field) : _director.ScopeRule(CompanyDirector.ScopeCompany, null, field);
            box.Headline("reallife_pol_set_here");
            int offset = company ? 0 : 1;   // a lower scope's first choice: follow the scope above
            int current = own != null && own.Sets(field) ? own.Of(field) + offset : 0;
            Func<int, string> text = i => !company && i == 0 ? CompanyText.T("reallife_pol_follow_one", "value", StepWords(field, above))
                : !company && PolicyRules.Past(d.Way, above, i - offset) ? CompanyText.T("reallife_pol_held_mark", "value", StepWords(field, i - offset)) : StepWords(field, i - offset);
            if (!box.HasSelector || !box.Selector(d.Steps.Length + offset, current, text, i => _director.PolicySetRule(kind, scope, field, i - offset)))
            {
                box.Line(text(current));
            }
            box.Section("reallife_pol_sec_force");
            box.RowPair("reallife_pol_col_company", StepWords(field, hs.Company.Of(field)), "reallife_pol_col_force", StepWords(field, _director.ScopeRule(kind, scope, field)));
            if (!company && own != null && own.Sets(field) && PolicyRules.Past(d.Way, above, own.Of(field)))
            {
                box.Line(CompanyText.T("reallife_pol_held_line", "value", StepWords(field, above)), CellTone.Yellow);
            }
            box.Note(CompanyText.T(d.Way == PolicyWay.LooseUp ? "reallife_pol_way_up" : d.Way == PolicyWay.LooseDown ? "reallife_pol_way_down" : "reallife_pol_way_company"));
        }

        private static string RuleName(PolicyField field)
        {
            return CompanyText.T(PolicyCatalogue.Def(field).TextKey);
        }

        /// <summary>A rule's step in words, by what its number is (PolicyUnit).</summary>
        internal string StepWords(PolicyField field, int step)
        {
            PolicyDef d = PolicyCatalogue.Def(field);
            if (step < 0)
            {
                return CompanyText.T("reallife_pol_as_above");
            }
            int value = PolicyCatalogue.Value(field, step);
            switch (d.Unit)
            {
                case PolicyUnit.Skill:
                    return value <= 0 ? CompanyText.T("reallife_pol_none") : CompanyText.T("reallife_pol_unit_skill", "n", value.ToString());
                case PolicyUnit.Share:
                    return field == PolicyField.RaiseMost && value >= PolicyCatalogue.RaiseAny ? CompanyText.T("reallife_pol_no_limit") : CompanyText.Share(value);
                case PolicyUnit.Days:
                    return value <= 0 ? CompanyText.T("reallife_pol_none") : value == 1 ? CompanyText.T("reallife_pol_unit_day")
                        : CompanyText.T("reallife_pol_unit_days", "days", value.ToString());
                case PolicyUnit.Money:
                    return field == PolicyField.ProfitTarget && value == PolicyCatalogue.ProfitNone ? CompanyText.T("reallife_pol_none")
                        : CompanyText.Money(CompanyCatalogue.AtPrices(value, _director.Index));
                case PolicyUnit.Choice:
                    return CompanyText.T(d.TextKey + "_" + step);
                case PolicyUnit.OnOff:
                    return CompanyText.T(value == 1 ? "reallife_pol_on" : "reallife_pol_off");
                case PolicyUnit.Count:
                    return value <= 0 && (field == PolicyField.MoodTarget || field == PolicyField.MoodFloor) ? CompanyText.T("reallife_pol_none") : value.ToString();
                case PolicyUnit.Percent:
                    return CompanyText.Percent(value);
                case PolicyUnit.BudgetStep:
                    return CompanyText.Money(CompanyCatalogue.AtPrices(ManagerCatalogue.Step(ManagerCatalogue.Budgets, value), _director.Index));
                default:
                    return value.ToString();
            }
        }

        // --- Templates ---

        private void WriteTemplates(HierarchyState hs, ref string selected)
        {
            _spec = CompanyTables.Templates;
            for (int i = 0; i < PolicyCatalogue.Templates.Length; i++)
            {
                Pick("pk:b" + i, CompanyText.T(PolicyCatalogue.TemplateKeys[i]), CompanyText.T("reallife_pol_tpl_built"), string.Empty, string.Empty, 0, ref selected);
            }
            for (int i = 0; i < hs.Templates.Count; i++)
            {
                PolicyTemplate t = hs.Templates[i];
                Pick("pk:o" + t.Number, CompanyText.T("reallife_pol_tpl_own", "number", t.Number.ToString()), CompanyText.T("reallife_pol_tpl_yours"),
                    CompanyText.T("reallife_mgr_row_day", "day", t.Day.ToString()), string.Empty, 0, ref selected);
            }
        }

        private void WriteTemplatesBox(InfoBox box)
        {
            box.Title(CompanyText.T("reallife_pol_templates"));
            box.Subtitle(ScopeTitle());
            InfoBox.Button(box.ButtonRow(), InfoBox.Additional, "reallife_co_back", () => Go("pc:"));
            box.Note(CompanyText.T("reallife_pol_templates_what"));
        }

        private void WriteTemplate(InfoBox box, HierarchyState hs, bool own, int number)
        {
            int[] steps = null;
            if (own)
            {
                for (int i = 0; i < hs.Templates.Count && steps == null; i++)
                {
                    steps = hs.Templates[i].Number == number ? hs.Templates[i].Set.Steps : null;
                }
            }
            bool known = own ? steps != null : number >= 0 && number < PolicyCatalogue.Templates.Length;
            if (!known)
            {
                WriteTemplatesBox(box);
                return;
            }
            steps = own ? steps : PolicyCatalogue.Templates[number];   // the starting policy has none: every rule at its start
            int kind = _polKind;
            string scope = _polScope;
            box.Title(own ? CompanyText.T("reallife_pol_tpl_own", "number", number.ToString()) : CompanyText.T(PolicyCatalogue.TemplateKeys[number]));
            box.Subtitle(own ? CompanyText.T("reallife_pol_tpl_yours") : CompanyText.T("reallife_pol_tpl_built"));
            Transform buttons = box.ButtonRow();
            InfoBox.Button(buttons, InfoBox.Positive, "reallife_pol_tpl_apply", () =>
            {
                _director.PolicyApplyTemplate(kind, scope, own ? 0 : number, own ? number : 0);
                Go("pc:");
            });
            if (own)
            {
                InfoBox.Button(buttons, InfoBox.Negative, "reallife_pol_tpl_delete", () =>
                {
                    _director.PolicyDeleteTemplate(number);
                    Go("pt:");
                });
            }
            InfoBox.Button(buttons, InfoBox.Additional, "reallife_co_back", () => Go("pt:"));
            if (!own)
            {
                box.Note(CompanyText.T(PolicyCatalogue.TemplateKeys[number] + "_what"));
            }
            box.Section("reallife_pol_sec_template");
            PolicyField[] shown = { PolicyField.PayBand, PolicyField.SkillFloor, PolicyField.Approver, PolicyField.Budget, PolicyField.GapDays, PolicyField.Reports };
            for (int i = 0; i < shown.Length; i++)
            {
                int f = (int)shown[i];
                box.RowWords(RuleName(shown[i]), StepWords(shown[i], steps != null && steps[f] >= 0 ? steps[f] : PolicyCatalogue.Defs[f].Default));
            }
            box.Line(CompanyText.T("reallife_pol_tpl_onto", "scope", ScopeTitle()));
        }

        // --- A place's dashboard ---

        // What is pending at a place first, then what was settled there lately, each a click from its escalation.
        private void WriteDashboardRows(HierarchyState hs, ManagedPlace mp, ref string selected)
        {
            _spec = CompanyTables.Escalations;
            int rows = 0;
            for (int pass = 0; pass < 2; pass++)
            {
                for (int i = hs.Escalations.Count - 1; i >= 0 && rows < EscalationsShown; i--)
                {
                    Escalation e = hs.Escalations[i];
                    if (e.Open != (pass == 0) || !string.Equals(e.Place, mp.Place.Place, StringComparison.Ordinal))
                    {
                        continue;
                    }
                    rows++;
                    Pick("he:" + e.Id, KindWords(e.Kind), PlaceWords(e), HolderWords(e), CompanyText.T("reallife_mgr_row_day", "day", e.Day.ToString()), 0, ref selected);
                    TableRow row = Last;
                    row.Kind = !e.Open ? CompanyTables.HeldClosed : e.Level >= EscalationRules.LevelOwner ? CompanyTables.HeldOwner
                        : e.Level == EscalationRules.LevelRegion ? CompanyTables.HeldRegion : CompanyTables.HeldExecutive;
                    row.Set(3, CompanyText.T("reallife_mgr_row_day", "day", e.Day.ToString()), e.Day);
                    row.Set(4, CompanyText.T(EscalationStateKeys[CompanyCatalogue.Clamp(e.State, 0, EscalationStateKeys.Length - 1)]), e.Open ? CellTone.Yellow : CellTone.Plain);
                }
            }
            if (rows == 0)
            {
                Say("none", CompanyText.T("reallife_hier_dash_none"));
            }
        }

        private void WriteDashboard(InfoBox box, ManagedPlace mp)
        {
            string place = mp.Place.Place;
            ManagerFile m = mp.Own;
            box.Title(CompanyText.T("reallife_hier_dash_title"));
            box.Subtitle(mp.Place.Name + (m != null ? ", " + m.Name : string.Empty));
            box.Chip(CompanyText.T("reallife_hier_level_chip", "level", ExecutiveHiring.Code(Rung.StoreManager)));
            Transform top = box.ButtonRow();
            InfoBox.Button(top, InfoBox.Additional, "reallife_co_back", () => Go("mp:" + place));
            if (m == null)
            {
                box.Note(CompanyText.T("reallife_mgr_place_none"));
                return;
            }
            box.Section("reallife_hier_sec_figures");
            int count, standing = _director.StandingAt(place, out count);
            int budget = _director.BudgetOf(m);
            box.RowPair("reallife_hier_fig_staff", CompanyText.T("reallife_hier_fig_staff_value", "staff", mp.Place.Staff.ToString(), "keep", Math.Max(0, m.Keep).ToString()),
                "reallife_hier_fig_standing", count > 0 ? standing.ToString() : CompanyText.T("reallife_mgr_none"));
            box.RowPair("reallife_hier_fig_budget", CompanyText.T("reallife_mgr_spent_value", "spent", CompanyText.Money(m.Spent), "budget", CompanyText.Money(budget)),
                "reallife_hier_fig_score", _director.ScoreOf(mp).ToString());
            float used = budget > 0 ? Mathf.Clamp01(m.Spent / (float)budget) : 0f;
            box.Meter("reallife_hier_fig_budget_used", used, CompanyText.Share((int)(used * 1000f)), used >= 0.8f ? CellTone.Yellow : CellTone.Plain);
            if (mp.Shop != null && mp.Shop.HasBooks)
            {
                box.Row("reallife_ibox_profit", CompanyText.Money(mp.Shop.Profit), mp.Shop.Profit < 0 ? CellTone.Red : CellTone.Plain);
            }
            int open = 0;
            HierarchyState hs = _director.HierarchyData;
            for (int i = 0; i < hs.Escalations.Count; i++)
            {
                open += hs.Escalations[i].Open && hs.Escalations[i].Place == place ? 1 : 0;
            }
            box.Section("reallife_hier_sec_quick");
            box.Row("reallife_hier_pending", open.ToString(), open > 0 ? CellTone.Yellow : CellTone.Plain);
            Transform quick = box.ButtonRow();
            InfoBox.Button(quick, InfoBox.Positive, "reallife_hier_quick_fill", () => _director.HierarchyFillNow(place));
            InfoBox.Button(quick, InfoBox.Additional, "reallife_hier_quick_raise", () => _director.HierarchyRaiseNow(place));
            InfoBox.Button(box.ButtonRow(), InfoBox.Additional, "reallife_hier_open_policy", () => OpenPolicy(CompanyDirector.ScopeStore, place));
            box.Note(CompanyText.T("reallife_hier_dash_what"));
            Meta.Guide.Point(box, "reallife_meta_title_economy_hierarchy");
        }
    }
}
