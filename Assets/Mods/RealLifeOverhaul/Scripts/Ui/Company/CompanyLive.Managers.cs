using System;
using System.Collections.Generic;
using RealLifeOverhaul.Careers;
using RealLifeOverhaul.Economy;

namespace RealLifeOverhaul.Ui
{
    internal sealed partial class CompanyLive
    {
        private const int CrewShown = 4, LogShown = 5;

        // Each kind of manager's decision has its own glyph (one glyph, one role).
        private static readonly UiIcon[] TaskIcons =
        {
            UiIcon.Coin, UiIcon.Handshake, UiIcon.Clock, UiIcon.ShieldCheck, UiIcon.Package, UiIcon.Tag, UiIcon.Calendar, UiIcon.People, UiIcon.Megaphone, UiIcon.Key
        };

        private void ReadManagers(CompanyFacts f, CompanyDirector d, CompanyView v, Words w)
        {
            CompanyManagersFacts m = f.Managers;
            if (!d.ManagersOn || d.ManagerData == null)
            {
                m.PartOff = true;
                m.OffTitle = w.T("reallife_co_tab_managers", "Managers");
                m.OffBody = CompanyText.T("reallife_mgr_off");
                return;
            }
            m.Deep = f.Deep;
            if (v.Sub[CompanyView.Managers] != CompanyView.SubMain && ReadManagersSub(m, d, v, w))
            {
                return;
            }
            BuildTree(m, d, w);
            string pick = v.Pick[CompanyView.Managers] ?? string.Empty;
            bool known = false;
            for (int i = 0; i < m.TreeIds.Length; i++)
            {
                known |= m.TreeIds[i] == pick && m.Tree[i].Kind != UiOrgKind.Caption;
            }
            if (!known)
            {
                pick = FirstPickable(m);   // the first manager, else the owner: a page is always in view
                v.Pick[CompanyView.Managers] = pick;
            }
            m.Chosen = pick;
            m.Profile = ProfileOf(d, pick, w);
            m.TreeFoot = d.HierarchyOn ? w.T("reallife_co_open_policy", "Open the policy centre") : null;
            m.TreeTitle = w.T("reallife_co_who_decides", "Who decides what");
            m.Empty = w.T("reallife_co_mgr_none_body", "Pick a person in the tree to see what they decide, what they may spend and how they are doing.");
        }

        private static string FirstPickable(CompanyManagersFacts m)
        {
            for (int i = 0; i < m.Tree.Length; i++)
            {
                if (m.TreeIds[i].StartsWith("mgr:", StringComparison.Ordinal))
                {
                    return m.TreeIds[i];
                }
            }
            for (int i = 0; i < m.Tree.Length; i++)
            {
                if (m.Tree[i].Kind != UiOrgKind.Caption)
                {
                    return m.TreeIds[i];
                }
            }
            return string.Empty;
        }

        // ------------------------------------------------------------------------------------------------- the tree

        // Who decides what: the owner, the executives in their seats (Deep Mode), the managers of each place with their crew under them, and the posts nobody holds.
        private void BuildTree(CompanyManagersFacts m, CompanyDirector d, Words w)
        {
            var nodes = new List<UiOrgNode>(48);
            var ids = new List<string>(48);
            HierarchyState h = d.HierarchyOn ? d.HierarchyData : null;
            bool exec = h != null && h.Deep;
            string owner = CompanyGame.OwnerName() ?? w.T("reallife_hier_you", "You");
            nodes.Add(Caption(w.T("reallife_co_tree_owner", "Owner")));
            ids.Add("cap:owner");
            FaceSources.Player(owner);
            nodes.Add(new UiOrgNode
            {
                Kind = UiOrgKind.Person, Level = 0, Name = w.T("reallife_hier_you", "You"), Sub = w.F("reallife_co_tree_owner_sub", "Owner, all {count} businesses", "count", d.Shops.Count.ToString()),
                Hue = UiColour.HueOf(owner), Ring = UiRole.Ok, HasKids = false
            });
            ids.Add("own");
            ExecutiveFile coo = null;
            if (exec)
            {
                nodes.Add(Caption(w.T("reallife_co_tree_executive", "Executive")));
                ids.Add("cap:exec");
                foreach (ExecSeat seat in ExecutiveHiring.Seats)
                {
                    ExecutiveFile x = h.Executive(seat);
                    coo = seat == ExecSeat.Coo ? x : coo;
                    if (x == null)
                    {
                        nodes.Add(new UiOrgNode { Kind = UiOrgKind.Post, Level = 1, Name = CompanyDirector.SeatName(seat), Sub = w.T("reallife_mgr_empty_post", "Empty post"), Right = w.T("reallife_co_tree_hire", "Appoint") });
                        ids.Add("seat:" + (int)seat);
                        continue;
                    }
                    int managersUnder = seat == ExecSeat.Coo ? CountManagers(d) : 0;
                    nodes.Add(new UiOrgNode
                    {
                        Kind = UiOrgKind.Person, Level = 1, Name = x.Name, Sub = CompanyDirector.SeatName(seat), Hue = UiColour.HueOf(x.Name), Ring = x.Seen ? UiRole.Ok : UiRole.Warn,
                        Right = seat == ExecSeat.Coo ? w.F("reallife_co_tree_managers", "{count} managers", "count", managersUnder.ToString()) : null, HasKids = seat == ExecSeat.Coo && managersUnder > 0
                    });
                    ids.Add("exe:" + (int)seat);
                    Face(x.Name, x.Id);
                    if (seat == ExecSeat.Coo)
                    {
                        AddManagers(nodes, ids, d, w, 2);
                    }
                }
            }
            if (!exec || coo == null)
            {
                if (exec)
                {
                    // Without a chief operating officer the managers stand under the owner as without Deep Mode.
                    RemoveManagersAfterExecutives(nodes, ids);
                }
                AddManagers(nodes, ids, d, w, 1);
            }
            var posts = new List<string>(4);
            for (int i = 0; i < d.Managed.Count; i++)
            {
                if (d.Managed[i].Own == null)
                {
                    posts.Add(d.Managed[i].Place.Place);
                }
            }
            if (posts.Count > 0)
            {
                nodes.Add(Caption(w.T("reallife_co_tree_open_posts", "Open posts")));
                ids.Add("cap:posts");
                for (int i = 0; i < posts.Count; i++)
                {
                    ManagedPlace mp = d.ManagedAt(posts[i]);
                    nodes.Add(new UiOrgNode
                    {
                        Kind = UiOrgKind.Post, Level = 1, Name = w.F("reallife_co_tree_post", "Manager of {place}", "place", mp.Place.Name), Sub = CompanyApp.PlaceKindName(mp.Kind) + ", " + CompanyText.District(mp.District),
                        Right = w.T("reallife_co_tree_hire", "Appoint")
                    });
                    ids.Add("post:" + posts[i]);
                }
            }
            m.Tree = nodes.ToArray();
            m.TreeIds = ids.ToArray();
        }

        private static UiOrgNode Caption(string name) => new UiOrgNode { Kind = UiOrgKind.Caption, Name = name };

        private static int CountManagers(CompanyDirector d)
        {
            int n = 0;
            for (int i = 0; i < d.Managed.Count; i++)
            {
                n += d.Managed[i].Own != null ? 1 : 0;
            }
            return n;
        }

        // Rows of the executives were added before the managers only when a COO stood; without one the managers go straight under the owner.
        private static void RemoveManagersAfterExecutives(List<UiOrgNode> nodes, List<string> ids)
        {
            for (int i = nodes.Count - 1; i >= 0; i--)
            {
                if (ids[i].StartsWith("mgr:", StringComparison.Ordinal) || ids[i].StartsWith("crew:", StringComparison.Ordinal) || ids[i].StartsWith("more:", StringComparison.Ordinal))
                {
                    nodes.RemoveAt(i);
                    ids.RemoveAt(i);
                }
            }
        }

        // A manager for each place that has one, with up to a few of their crew under them.
        private void AddManagers(List<UiOrgNode> nodes, List<string> ids, CompanyDirector d, Words w, int level)
        {
            CompanyState s = d.State;
            for (int i = 0; i < d.Managed.Count; i++)
            {
                ManagedPlace mp = d.Managed[i];
                if (mp.Own == null)
                {
                    continue;
                }
                ManagerFile own = mp.Own;
                int crew = 0;
                for (int k = 0; k < s.Staff.Count; k++)
                {
                    crew += s.Staff[k].Place == mp.Place.Place && s.Staff[k].Id != own.Id ? 1 : 0;
                }
                Face(own.Name, own.Id);
                PostState post = d.PostOf(mp);
                nodes.Add(new UiOrgNode
                {
                    Kind = UiOrgKind.Person, Level = level, Name = own.Name, Sub = mp.Place.Name, Hue = UiColour.HueOf(own.Name), Ring = post == PostState.InPost ? UiRole.Ok : UiRole.Warn,
                    Right = w.F("reallife_co_tree_staff", "{count} staff", "count", crew.ToString()), HasKids = crew > 0
                });
                ids.Add("mgr:" + mp.Place.Place);
                int shown = 0;
                for (int k = 0; k < s.Staff.Count && shown < 3; k++)
                {
                    StaffFile p = s.Staff[k];
                    if (p.Place != mp.Place.Place || p.Id == own.Id)
                    {
                        continue;
                    }
                    Face(p.Name, p.Id);
                    nodes.Add(new UiOrgNode { Kind = UiOrgKind.Person, Level = level + 1, Name = p.Name, Hue = UiColour.HueOf(p.Name), Ring = UiRole.None, Right = CompanyGame.SkillName(p.Id) });
                    ids.Add("crew:" + p.Id);
                    shown++;
                }
                if (crew > shown)
                {
                    nodes.Add(new UiOrgNode { Kind = UiOrgKind.More, Level = level + 1, Name = w.F("reallife_co_and_more", "and {count} more", "count", (crew - shown).ToString()) });
                    ids.Add("more:" + mp.Place.Place);
                }
            }
            // Regional managers: one a district.
            ManagerState ms = d.ManagerData;
            for (int i = 0; ms != null && i < ms.Managers.Count; i++)
            {
                ManagerFile r = ms.Managers[i];
                if (!r.Region)
                {
                    continue;
                }
                Face(r.Name, r.Id);
                nodes.Add(new UiOrgNode
                {
                    Kind = UiOrgKind.Person, Level = level, Name = r.Name, Sub = w.F("reallife_co_tree_region", "Regional, {district}", "district", CompanyDirector.AreaName(r.Place)),
                    Hue = UiColour.HueOf(r.Name), Ring = CompanyDirector.Sick(r) ? UiRole.Warn : UiRole.Ok
                });
                ids.Add("reg:" + r.Place);
            }
        }

        // ------------------------------------------------------------------------------------------------- a profile

        private CompanyProfile ProfileOf(CompanyDirector d, string id, Words w)
        {
            if (id.StartsWith("mgr:", StringComparison.Ordinal))
            {
                ManagedPlace mp = d.ManagedAt(id.Substring(4));
                return mp != null && mp.Own != null ? ManagerProfile(d, mp, w) : null;
            }
            if (id.StartsWith("exe:", StringComparison.Ordinal))
            {
                int seat;
                int.TryParse(id.Substring(4), out seat);
                ExecutiveFile x = d.HierarchyOn ? d.HierarchyData.Executive((ExecSeat)seat) : null;
                return x != null ? ExecutiveProfile(d, x, w) : null;
            }
            if (id.StartsWith("reg:", StringComparison.Ordinal))
            {
                ManagerFile r = d.RegionOf(id.Substring(4));
                return r != null ? RegionProfile(d, r, w) : null;
            }
            if (id.StartsWith("crew:", StringComparison.Ordinal))
            {
                StaffFile p = d.State.Employee(id.Substring(5));
                return p != null ? CrewProfile(d, p, w) : null;
            }
            if (id.StartsWith("post:", StringComparison.Ordinal))
            {
                ManagedPlace mp = d.ManagedAt(id.Substring(5));
                return mp != null ? PostProfile(d, mp, w) : null;
            }
            if (id.StartsWith("seat:", StringComparison.Ordinal))
            {
                int seat;
                int.TryParse(id.Substring(5), out seat);
                return SeatProfile(d, (ExecSeat)seat, w);
            }
            return OwnerProfile(d, w);
        }

        // The manager of a place, as the mockup has it: their facts, the question waiting, what they decide, their limits, how they do, their log and their crew.
        private CompanyProfile ManagerProfile(CompanyDirector d, ManagedPlace mp, Words w)
        {
            ManagerFile m = mp.Own;
            string place = mp.Place.Place;
            PostState post = d.PostOf(mp);
            int asked = d.HierarchyOn ? OpenAsksOf(d.HierarchyData, place) : 0;
            var p = new CompanyProfile
            {
                Id = "mgr:" + place, Name = m.Name, Hue = UiColour.HueOf(m.Name), Ring = post == PostState.InPost ? UiRole.Ok : UiRole.Warn,
                Role = w.F("reallife_co_role_manager", "Manager, {place}", "place", mp.Place.Name),
                Status = CompanyText.T(post == PostState.InPost ? "reallife_mgr_in_post" : "reallife_mgr_off_sick"), StatusTone = post == PostState.InPost ? UiRole.Ok : UiRole.Warn
            };
            Face(m.Name, m.Id);
            p.FactLabels = new[] { w.T("reallife_co_f_manager_since", "Manager since"), w.T("reallife_co_f_wage", "Wage"), w.T("reallife_co_f_this_month", "This month"), w.T("reallife_co_f_asked", "Asked you") };
            p.FactValues = new[] { CompanyText.Day(m.From), w.PerHour(d.PayOf(m)), w.F("reallife_co_f_decisions", "{count} decisions", "count", m.Decided.ToString()), asked.ToString() };
            // The question waiting for the owner, if there is one.
            if (asked > 0)
            {
                Escalation e = OwnerEscalationAt(d, place);
                if (e != null)
                {
                    p.WaitingText = asked == 1 ? w.T("reallife_co_one_waiting", "1 question waiting for you") : w.F("reallife_co_n_waiting", "{count} questions waiting for you", "count", asked.ToString());
                    p.WaitingLine = CompanyApp.EscalationSays(d, e);
                    p.Waiting = Btn(w.T("reallife_co_answer", "Answer"), UiButtonKind.Primary, CompanyAct.Of(CompanyDo.Go, "card", e.Id), UiAction.Alt);
                }
            }
            // What they decide: a switch for every kind the place has.
            p.TasksTitle = w.F("reallife_co_decisions_owns", "Decisions {name} owns", "name", FirstName(m.Name));
            p.TasksHint = w.F("reallife_co_switch_hint", "Switch on = {name} decides", "name", FirstName(m.Name));
            int kinds = ManagerRules.Kinds(mp.Kind), blocked = d.ManagerBlocked(mp);
            var tasks = new List<CompanyTask>(ManagerTaskCount);
            for (int t = 0; t < (int)ManagerTask.Count; t++)
            {
                ManagerTask task = (ManagerTask)t;
                if (!ManagerRules.Has(kinds, task))
                {
                    continue;
                }
                bool on = ManagerRules.Has(m.Tasks, task);
                string wait = on && (blocked & (1 << t)) != 0 ? CompanyApp.TaskWaits(t) : null;
                tasks.Add(new CompanyTask
                {
                    Id = place + ":" + t, Icon = TaskIcons[Math.Min(t, TaskIcons.Length - 1)], Title = CompanyApp.TaskTitle(t), Line = wait ?? CompanyApp.TaskLine(m, task), On = on,
                    Who = on ? FirstName(m.Name) : w.T("reallife_hier_you", "You"), Act = CompanyAct.Of(CompanyDo.TaskSet, place, t, on ? 0 : 1)
                });
            }
            p.Tasks = tasks.ToArray();
            // Limits: the ones that matter for what is switched on; the rest are on the kind's own page.
            var limits = new List<CompanyLimit>(3);
            if (ManagerRules.Has(m.Tasks, ManagerTask.Raises) || ManagerRules.Has(m.Tasks, ManagerTask.Offers))
            {
                limits.Add(LimitOf(d, place, CompanyDirector.LimitWage, w));
            }
            if (ManagerRules.Has(m.Tasks, ManagerTask.Overtime) || ManagerRules.Has(m.Tasks, ManagerTask.Hiring))
            {
                limits.Add(LimitOf(d, place, CompanyDirector.LimitBudget, w));
            }
            p.Limits = limits.ToArray();
            p.LimitsTitle = w.T("reallife_co_limits_title", "Limits and where questions go");
            p.LimitsAll = Btn(w.T("reallife_co_all_limits", "All limits"), UiButtonKind.Quiet, CompanyAct.Of(CompanyDo.Go, "mgr:" + place, CompanyView.Managers, CompanyView.SubTask, 0));
            // Where a question they cannot decide goes: to you, or after a day to the executive of its area.
            if (d.HierarchyOn && d.HierarchyData.Deep)
            {
                ExecutiveFile any = d.HierarchyData.Executives.Count > 0 ? d.HierarchyData.Executives[0] : null;
                if (any != null)
                {
                    p.RouteTitle = w.F("reallife_co_route_title", "Questions {name} cannot decide go to", "name", FirstName(m.Name));
                    p.RouteSub = w.F("reallife_co_route_sub", "{name} picks up what you leave for a day", "name", any.Name);
                    p.RouteOptions = new[] { w.T("reallife_hier_you", "You"), any.Name };
                    p.RouteChosen = m.HandDown ? 1 : 0;
                    p.RouteActs = new[] { CompanyAct.Of(CompanyDo.TaskSet, place, -1, 0), CompanyAct.Of(CompanyDo.TaskSet, place, -1, 1) };
                }
            }
            // How they are doing, as grades; a grade with nothing behind it yet is left out, never shown as a zero.
            var labels = new List<string>(4);
            var values = new List<int>(4);
            int review = mp.Region != null && !CompanyDirector.Sick(mp.Region) ? mp.Region.Skill : 0;
            Grade(labels, values, w.T("reallife_co_g_judgement", "Judgement"), ManagerRules.Judgement(m.Skill, review, m.Decided, m.Mistakes));
            Grade(labels, values, w.T("reallife_co_g_speed", "Speed"), ManagerRules.Speed(m.Speeds));
            Grade(labels, values, w.T("reallife_co_g_thrift", "Thrift"), ManagerRules.Thrift(m.Spent, d.BudgetOf(m)));
            int count, standing = d.StandingAt(place, out count);
            Grade(labels, values, w.T("reallife_co_g_morale", "Crew morale"), ManagerRules.Morale(standing, count));
            p.GradesTitle = w.F("reallife_co_how_doing", "How {name} is doing", "name", FirstName(m.Name));
            p.GradeLabels = labels.ToArray();
            p.GradeValues = values.ToArray();
            // Their log: what they decided here, newest first.
            ManagerState ms = d.ManagerData;
            var log = new List<CompanyDone>(LogShown);
            int total = 0;
            for (int i = ms.Decisions.Count - 1; i >= 0; i--)
            {
                if (ms.Decisions[i].Place != place)
                {
                    continue;
                }
                total++;
                if (log.Count < LogShown)
                {
                    log.Add(DoneOf(ms.Decisions[i], d, w));
                }
            }
            p.LogTitle = w.T("reallife_co_log", "Log");
            p.Log = log.ToArray();
            p.LogAll = total > LogShown ? w.F("reallife_co_all_count", "All {count}", "count", total.ToString()) : null;
            p.LogOpen = CompanyAct.Of(CompanyDo.Go, "mgr:" + place, CompanyView.Managers, CompanyView.SubDecision);
            // Their crew.
            var crew = new List<CompanyItem>(CrewShown);
            int crewTotal = 0;
            CompanyState s = d.State;
            for (int i = 0; i < s.Staff.Count; i++)
            {
                StaffFile c = s.Staff[i];
                if (c.Place != place || c.Id == m.Id)
                {
                    continue;
                }
                crewTotal++;
                if (crew.Count < CrewShown)
                {
                    Face(c.Name, c.Id);
                    crew.Add(new CompanyItem
                    {
                        Title = c.Name, Sub = CompanyGame.SkillName(c.Id), Face = true, Hue = UiColour.HueOf(c.Name), Ring = StaffRules.StandsLow(c) ? UiRole.Danger : UiRole.Ok, Grade = c.Skill,
                        Figure = c.Skill.ToString(), FigureTone = UiRole.None, Press = true, Act = CompanyAct.Of(CompanyDo.Go, "emp:" + c.Id, CompanyView.Staff)
                    });
                }
            }
            p.CrewTitle = w.F("reallife_co_crew_title", "{name}'s crew", "name", FirstName(m.Name));
            p.CrewCount = crewTotal > 0 ? w.F("reallife_co_staff_count_n", "{count} staff", "count", crewTotal.ToString()) : null;
            p.Crew = crew.ToArray();
            // What can be done with the post.
            var buttons = new List<CompanyButton>(2)
            {
                Btn(w.T("reallife_co_replace_or_stand", "Replace or stand down"), UiButtonKind.Secondary, CompanyAct.Of(CompanyDo.Go, "mgr:" + place, CompanyView.Managers, CompanyView.SubAppoint), UiAction.Count, UiIcon.People)
            };
            if (d.HierarchyOn && d.HierarchyData.Deep)
            {
                buttons.Add(Btn(w.T("reallife_hier_open_dashboard", "Open the dashboard"), UiButtonKind.Secondary, CompanyAct.Of(CompanyDo.Go, "mgr:" + place, CompanyView.Managers, CompanyView.SubDashboard), UiAction.Count, UiIcon.ChartPie));
            }
            p.Buttons = buttons.ToArray();
            return p;
        }

        private const int ManagerTaskCount = 10;

        private static void Grade(List<string> labels, List<int> values, string label, int value)
        {
            if (value >= 0)
            {
                labels.Add(label);
                values.Add(value);
            }
        }

        private static Escalation OwnerEscalationAt(CompanyDirector d, string place)
        {
            HierarchyState h = d.HierarchyData;
            for (int i = 0; i < h.Escalations.Count; i++)
            {
                Escalation e = h.Escalations[i];
                if (e.Open && e.Level >= EscalationRules.LevelOwner && e.Place == place)
                {
                    return e;
                }
            }
            return null;
        }

        // A limit as a stepper: its words, its value now and the step before and after.
        private CompanyLimit LimitOf(CompanyDirector d, string place, int limit, Words w)
        {
            ManagedPlace mp = d.ManagedAt(place);
            ManagerFile m = mp != null ? mp.Own : null;
            var l = new CompanyLimit();
            if (m == null)
            {
                return l;
            }
            int step, count;
            string title, sub;
            LimitState(d, m, limit, w, out step, out count, out title, out sub, out l.Value);
            l.Title = title;
            l.Sub = sub;
            l.CanLess = step > 0;
            l.CanMore = step < count - 1;
            l.Less = CompanyAct.Of(CompanyDo.LimitSet, place, limit, Math.Max(0, step - 1));
            l.More = CompanyAct.Of(CompanyDo.LimitSet, place, limit, Math.Min(count - 1, step + 1));
            l.Typed = CompanyAct.Of(CompanyDo.LimitSet, place, limit, step);
            return l;
        }

        // A limit's step, how many there are, its words and its value in words.
        internal static void LimitState(CompanyDirector d, ManagerFile m, int limit, Words w, out int step, out int count, out string title, out string sub, out string value)
        {
            int index = d.Index;
            switch (limit)
            {
                case CompanyDirector.LimitWage:
                    step = m.WageBand; count = ManagerCatalogue.WageBands.Length; title = w.T("reallife_co_lim_raise", "Raise limit"); sub = w.T("reallife_co_lim_raise_sub", "Of the going rate, above the pay now");
                    value = CompanyText.Share(ManagerCatalogue.WageBands[Math.Max(0, Math.Min(count - 1, step))]);
                    return;
                case CompanyDirector.LimitBudget:
                    step = m.Budget; count = ManagerCatalogue.Budgets.Length; title = w.T("reallife_co_lim_budget", "Spend limit"); sub = w.T("reallife_co_lim_budget_sub", "A month, for overtime and hires");
                    value = CompanyText.Money(CompanyCatalogue.AtPrices(ManagerCatalogue.Budgets[Math.Max(0, Math.Min(count - 1, step))], index));
                    return;
                case CompanyDirector.LimitMarketing:
                    step = m.MarketingStep; count = ManagerCatalogue.MarketingSteps.Length; title = w.T("reallife_co_lim_marketing", "Marketing limit"); sub = w.T("reallife_co_lim_marketing_sub", "A month, for campaigns");
                    value = CompanyText.Money(CompanyCatalogue.AtPrices(ManagerCatalogue.MarketingSteps[Math.Max(0, Math.Min(count - 1, step))], index));
                    return;
                case CompanyDirector.LimitLease:
                    step = m.LeaseStep; count = ManagerCatalogue.LeaseRises.Length; title = w.T("reallife_co_lim_lease", "Lease rise limit"); sub = w.T("reallife_co_lim_lease_sub", "The most a renewal may raise the rent without asking you");
                    value = CompanyText.Share(ManagerCatalogue.LeaseRises[Math.Max(0, Math.Min(count - 1, step))]);
                    return;
                case CompanyDirector.LimitKeep:
                    step = Math.Max(0, m.Keep); count = ManagerCatalogue.KeepMost + 1; title = w.T("reallife_co_lim_keep", "Crew to keep"); sub = w.T("reallife_co_lim_keep_sub", "Hiring keeps the place at this many");
                    value = CompanyText.T("reallife_mgr_limit_keep_value", "count", step.ToString());
                    return;
                case CompanyDirector.LimitStock:
                    step = m.StockDays; count = ManagerCatalogue.StockDays.Length; title = w.T("reallife_co_lim_stock", "Stock days"); sub = w.T("reallife_co_lim_stock_sub", "Orders keep this many days of stock");
                    value = CompanyText.T("reallife_mgr_limit_stock_value", "days", ManagerCatalogue.StockDays[Math.Max(0, Math.Min(count - 1, step))].ToString());
                    return;
                case CompanyDirector.LimitPriceLow:
                    step = m.PriceLow; count = ManagerCatalogue.PriceLows.Length; title = w.T("reallife_co_lim_price_low", "Lowest price"); sub = w.T("reallife_co_lim_price_low_sub", "Of the market price");
                    value = CompanyText.Share(ManagerCatalogue.PriceLows[Math.Max(0, Math.Min(count - 1, step))]);
                    return;
                case CompanyDirector.LimitPriceHigh:
                    step = m.PriceHigh; count = ManagerCatalogue.PriceHighs.Length; title = w.T("reallife_co_lim_price_high", "Highest price"); sub = w.T("reallife_co_lim_price_high_sub", "Of the market price");
                    value = CompanyText.Share(ManagerCatalogue.PriceHighs[Math.Max(0, Math.Min(count - 1, step))]);
                    return;
                default:
                    step = 0; count = 1; title = string.Empty; sub = null; value = string.Empty;
                    return;
            }
        }

        private CompanyProfile OwnerProfile(CompanyDirector d, Words w)
        {
            string owner = CompanyGame.OwnerName() ?? w.T("reallife_hier_you", "You");
            HierarchyState h = d.HierarchyOn ? d.HierarchyData : null;
            int mine = 0;
            for (int i = 0; h != null && i < h.Escalations.Count; i++)
            {
                mine += h.Escalations[i].Open && h.Escalations[i].Level >= EscalationRules.LevelOwner ? 1 : 0;
            }
            var p = new CompanyProfile
            {
                Id = "own", Name = owner, Role = w.T("reallife_co_role_owner", "Owner"), Hue = UiColour.HueOf(owner), Ring = UiRole.Ok, Status = null,
                FactLabels = new[] { w.T("reallife_co_f_businesses", "Businesses"), w.T("reallife_co_f_managers", "Managers"), w.T("reallife_co_f_asked", "Asked you") },
                FactValues = new[] { d.Shops.Count.ToString(), d.ManagerData.Managers.Count.ToString(), mine.ToString() }
            };
            FaceSources.Player(owner);
            var more = new List<CompanyBlock>(12);
            more.Add(Sec(w.T("reallife_co_owner_month", "This month")));
            int decided = 0, cost = 0;
            for (int i = 0; i < d.ManagerData.Managers.Count; i++)
            {
                decided += d.ManagerData.Managers[i].Decided;
                cost += d.MonthCostOf(d.ManagerData.Managers[i]);
            }
            more.Add(Row(w.T("reallife_co_owner_decided", "Decisions made for you"), decided.ToString()));
            more.Add(Row(w.T("reallife_co_owner_cost", "Managers' allowances, a month"), CompanyText.Money(cost)));
            if (h != null)
            {
                more.Add(Sec(w.T("reallife_co_owner_levels", "Levels")));
                more.Add(new CompanyBlock
                {
                    Kind = CompanyBlockKind.Switch, Text = w.T("reallife_co_deep_mode", "Deep Mode"), Sub = w.T("reallife_co_deep_sub", "The executives, regions, the policy centre and every question's inbox"),
                    On = h.Deep, Value = w.T(h.Deep ? "reallife_co_on" : "reallife_co_off", h.Deep ? "On" : "Off"), Act = CompanyAct.Of(CompanyDo.DeepMode, null, h.Deep ? 0 : 1)
                });
                if (h.Deep)
                {
                    more.Add(Link(w.T("reallife_co_inbox", "Questions inbox"), w.F("reallife_co_inbox_open", "{count} open", "count", OpenCount(h).ToString()), CompanyAct.Of(CompanyDo.Go, null, CompanyView.Managers, CompanyView.SubInbox)));
                    more.Add(Link(w.T("reallife_co_policy_centre", "Policy centre"), string.Empty, CompanyAct.Of(CompanyDo.Go, null, CompanyView.Managers, CompanyView.SubPolicy)));
                    more.Add(Link(w.T("reallife_co_templates", "Templates"), (PolicyCatalogue.TemplateKeys.Length + h.Templates.Count).ToString(), CompanyAct.Of(CompanyDo.Go, null, CompanyView.Managers, CompanyView.SubTemplates)));
                }
            }
            p.More = more.ToArray();
            return p;
        }

        private static int OpenCount(HierarchyState h)
        {
            int n = 0;
            for (int i = 0; i < h.Escalations.Count; i++)
            {
                n += h.Escalations[i].Open ? 1 : 0;
            }
            return n;
        }

        private CompanyProfile ExecutiveProfile(CompanyDirector d, ExecutiveFile x, Words w)
        {
            ExecSeat seat = (ExecSeat)x.Area;
            var p = new CompanyProfile
            {
                Id = "exe:" + x.Area, Name = x.Name, Role = CompanyDirector.SeatName(seat), Hue = UiColour.HueOf(x.Name), Ring = x.Seen ? UiRole.Ok : UiRole.Warn,
                Status = CompanyText.T(x.Seen ? "reallife_mgr_in_post" : "reallife_hier_seat_waits"), StatusTone = x.Seen ? UiRole.Ok : UiRole.Warn,
                FactLabels = new[] { w.T("reallife_co_f_since", "Since"), w.T("reallife_co_f_allowance", "Allowance"), w.T("reallife_co_f_this_month", "This month"), w.T("reallife_co_f_open_area", "Open in their area") },
                FactValues = new[] { CompanyText.Day(x.From), CompanyText.T("reallife_co_row_a_day", "amount", CompanyText.Money(CompanyDirector.AllowanceOf(x))), w.F("reallife_co_f_decisions", "{count} decisions", "count", x.Decided.ToString()),
                    d.OpenOfSeat(seat).ToString() }
            };
            Face(x.Name, x.Id);
            var more = new List<CompanyBlock>(8);
            more.Add(Sec(w.T("reallife_hier_sec_area", "Their area")));
            more.Add(Note(CompanyText.T(PolicyCatalogue.Mod + "_hier_seat_what_" + seat.ToString().ToLowerInvariant())));
            more.Add(Row(CompanyText.T("reallife_hier_area_settled"), d.SettledSince(null, x.Name, d.Today - 7).ToString()));
            more.Add(Row(CompanyText.T("reallife_hier_last_month"), x.LastDecided.ToString()));
            if (!x.Seen)
            {
                more.Add(Note(CompanyText.T("reallife_hier_seat_waits_line"), UiRole.WarnText));
            }
            p.More = more.ToArray();
            p.Buttons = new[]
            {
                Btn(w.T("reallife_mgr_replace", "Replace"), UiButtonKind.Secondary, CompanyAct.Of(CompanyDo.Go, "exe:" + x.Area, CompanyView.Managers, CompanyView.SubSeat), UiAction.Count, UiIcon.People),
                Btn(w.T("reallife_mgr_stand_down", "Stand down"), UiButtonKind.Secondary, CompanyAct.Of(CompanyDo.SeatStandDown, null, x.Area), UiAction.Count, UiIcon.Close, true)
            };
            return p;
        }

        private CompanyProfile RegionProfile(CompanyDirector d, ManagerFile r, Words w)
        {
            string area = r.Place;
            bool sick = CompanyDirector.Sick(r);
            int places = 0;
            for (int i = 0; i < d.Managed.Count; i++)
            {
                places += d.Managed[i].Area == area && d.Managed[i].Own != null ? 1 : 0;
            }
            var p = new CompanyProfile
            {
                Id = "reg:" + area, Name = r.Name, Role = w.F("reallife_co_tree_region", "Regional, {district}", "district", CompanyDirector.AreaName(area)), Hue = UiColour.HueOf(r.Name),
                Ring = sick ? UiRole.Warn : UiRole.Ok, Status = CompanyText.T(sick ? "reallife_mgr_off_sick" : "reallife_mgr_in_post"), StatusTone = sick ? UiRole.Warn : UiRole.Ok,
                FactLabels = new[] { w.T("reallife_co_f_since", "Since"), w.T("reallife_co_f_places", "Places"), w.T("reallife_co_f_standing_in", "Standing in"), w.T("reallife_co_f_decisions_n", "Decisions") },
                FactValues = new[] { CompanyText.Day(r.From), places.ToString(), d.StandingIn(area).ToString(), r.Decided.ToString() }
            };
            Face(r.Name, r.Id);
            var more = new List<CompanyBlock>(8)
            {
                Sec(w.T("reallife_mgr_sec_manager", "Manager")),
                Row(CompanyText.T("reallife_mgr_skill"), r.Skill.ToString() + " / " + ManagerRules.FromAfar(r.Skill).ToString()),
                Row(CompanyText.T("reallife_mgr_allowance"), CompanyText.T("reallife_co_row_a_day", "amount", CompanyText.Money(d.AllowanceOf(r)))),
                Row(CompanyText.T("reallife_mgr_pay"), CompanyText.Wage(d.PayOf(r))),
                Row(CompanyText.T("reallife_mgr_mistakes"), r.Mistakes.ToString())
            };
            if (sick)
            {
                more.Add(Note(CompanyText.T("reallife_mgr_region_sick"), UiRole.WarnText));
            }
            more.Add(Note(CompanyText.T("reallife_mgr_region_what")));
            p.More = more.ToArray();
            var buttons = new List<CompanyButton>(4)
            {
                Btn(w.T("reallife_co_replace_or_stand", "Replace or stand down"), UiButtonKind.Secondary, CompanyAct.Of(CompanyDo.Go, "reg:" + area, CompanyView.Managers, CompanyView.SubAppoint), UiAction.Count, UiIcon.People)
            };
            if (d.HierarchyOn)
            {
                buttons.Add(Btn(w.T("reallife_hier_open_dashboard", "Open the dashboard"), UiButtonKind.Secondary, CompanyAct.Of(CompanyDo.Go, "reg:" + area, CompanyView.Managers, CompanyView.SubRegion), UiAction.Count, UiIcon.ChartPie));
            }
            p.Buttons = buttons.ToArray();
            return p;
        }

        private CompanyProfile CrewProfile(CompanyDirector d, StaffFile c, Words w)
        {
            var p = new CompanyProfile
            {
                Id = "crew:" + c.Id, Name = c.Name, Role = CompanyGame.SkillName(c.Id) + ", " + d.ShopName(c.Place), Hue = UiColour.HueOf(c.Name), Ring = StaffRules.StandsLow(c) ? UiRole.Danger : UiRole.Ok,
                FactLabels = new[] { w.T("reallife_co_f_paid", "Paid"), w.T("reallife_co_f_standing", "Standing"), w.T("reallife_co_f_mood", "Mood") },
                FactValues = new[] { CompanyText.Wage(c.Paid), c.Standing.ToString(), c.Mood.ToString() }
            };
            Face(c.Name, c.Id);
            p.Buttons = new[] { Btn(w.T("reallife_co_open_staff", "Open in Staff"), UiButtonKind.Primary, CompanyAct.Of(CompanyDo.Go, "emp:" + c.Id, CompanyView.Staff), UiAction.Alt, UiIcon.Next) };
            return p;
        }

        private CompanyProfile PostProfile(CompanyDirector d, ManagedPlace mp, Words w)
        {
            var p = new CompanyProfile
            {
                Id = "post:" + mp.Place.Place, Name = w.F("reallife_co_tree_post", "Manager of {place}", "place", mp.Place.Name), Role = CompanyApp.PlaceKindName(mp.Kind) + ", " + CompanyText.District(mp.District),
                Hue = UiColour.HueOf(mp.Place.Name), Ring = UiRole.None, Status = CompanyText.T("reallife_mgr_empty_post"), StatusTone = UiRole.Muted
            };
            p.More = new[]
            {
                Note(CompanyText.T("reallife_mgr_place_none")),
                Buttons(Btn(w.T("reallife_mgr_appoint", "Appoint"), UiButtonKind.Primary, CompanyAct.Of(CompanyDo.Go, "post:" + mp.Place.Place, CompanyView.Managers, CompanyView.SubAppoint), UiAction.Alt, UiIcon.People))
            };
            return p;
        }

        private CompanyProfile SeatProfile(CompanyDirector d, ExecSeat seat, Words w)
        {
            string name = CompanyDirector.SeatName(seat);
            var p = new CompanyProfile
            {
                Id = "seat:" + (int)seat, Name = name, Role = w.T("reallife_co_role_executive", "Executive seat"), Hue = UiColour.HueOf(name), Ring = UiRole.None,
                Status = CompanyText.T("reallife_mgr_empty_post"), StatusTone = UiRole.Muted
            };
            p.More = new[]
            {
                Note(CompanyText.T(PolicyCatalogue.Mod + "_hier_seat_what_" + seat.ToString().ToLowerInvariant())),
                Buttons(Btn(w.T("reallife_mgr_appoint", "Appoint"), UiButtonKind.Primary, CompanyAct.Of(CompanyDo.Go, "seat:" + (int)seat, CompanyView.Managers, CompanyView.SubSeat), UiAction.Alt, UiIcon.People))
            };
            return p;
        }
    }
}
