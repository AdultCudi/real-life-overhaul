using System.Collections.Generic;
using System.Linq;
using RealLifeOverhaul.Ui;

// The Company app's Managers tab with its sub pages. Samples' own names; the live source reads the player's people. Every name, wage, count and day comes from the sample world
// (UiSamples.Company.World.cs), so that this tab says what the Staff, Rota and Tax tabs say.
internal static partial class UiSamples
{
    // ------------------------------------------------------------------------------------------------- Managers

    private static UiOrgNode ONode(UiOrgKind kind, int level, string name, string sub = null, string right = null, bool kids = false, float hue = 0f, UiRole ring = UiRole.Ok) =>
        new UiOrgNode { Kind = kind, Level = level, Name = name, Sub = sub, Right = right, HasKids = kids, Hue = hue, Ring = ring };

    private static string RoleKey(string role) => role == "business_analyst" ? "Business analyst" : role == "accountant" ? "Accountant" : "Financial analyst";

    private static string RoleOf(CompanyWords w, CPro p) => w.T("reallife_talent_role_" + p.Role, RoleKey(p.Role)) + ", " + w.T("reallife_talent_level_" + p.Level, p.Level);

    private static string SeatName(CompanyWords w, CSeat s) => Tr(w, s.Title);

    private static string[] ManagerTasks = { "Raises", "Rival offers", "Overtime", "Insurance claims", "Stock", "Prices", "Rota", "Hiring", "Marketing", "Leases" };

    private static readonly UiIcon[] ManagerGlyphs = { UiIcon.Coin, UiIcon.Handshake, UiIcon.Clock, UiIcon.ShieldCheck, UiIcon.Package, UiIcon.Tag, UiIcon.Calendar, UiIcon.People, UiIcon.Megaphone, UiIcon.Key };

    // Which of the ten kinds of decision each manager owns, by the manager's id: the profile's switches, the owner's count of who holds what and the lists of the same decision elsewhere all read it.
    private static bool[] Owns(string manager, bool handover)
    {
        if (handover)
        {
            switch (manager)
            {
                case "ray": return new[] { true, false, true, false, true, false, true, false, false, false };
                case "tessa": return new[] { false, false, false, false, true, true, true, false, false, false };
                case "omar": return new[] { false, false, true, false, true, false, true, false, false, false };
                default: return new[] { false, false, false, false, false, false, true, false, false, false };
            }
        }
        switch (manager)
        {
            case "ray": return new[] { true, true, true, false, true, true, true, true, false, true };
            case "tessa": return new[] { true, true, false, true, true, true, true, false, true, false };
            case "omar": return new[] { true, false, true, false, true, false, false, true, false, true };
            default: return new bool[10];
        }
    }

    private static int Held(int task, bool handover)
    {
        int n = 0;
        foreach (CPerson m in Cw.Managers())
        {
            n += Owns(m.Id, handover)[task] ? 1 : 0;
        }
        return n;
    }

    private static int YearCost(int deltaHundredths) => deltaHundredths * CompanyWorld.DayHours * 247 / 100;   // a year's wages for an hourly change, over the working days of a year

    private static void CompanyManagers(CompanyFacts f, CompanyWords w, string state)
    {
        CompanyManagersFacts m = f.Managers;
        bool handover = state == "managers_handover";
        f.Deep = state != "managers_flat" && state != "managers_none";
        m.Deep = f.Deep;
        if (state == "managers_off")
        {
            m.PartOff = true;
            m.OffTitle = w.T("reallife_co_tab_managers", "Managers");
            m.OffBody = w.T("reallife_mgr_off", "Managers are switched off in the options, or they stopped after an error. Nothing is decided by them and everything comes to you.");
            return;
        }
        if (handover)
        {
            Cw.HandOver = true;
        }
        bool bare = !f.Deep;   // a company with a few managers, or none yet, and the businesses that wait for one
        var nodes = new List<UiOrgNode>();
        var ids = new List<string>();
        var where = new List<string>();
        var needs = new List<bool>();
        void Add(string id, UiOrgNode n, string district = null, bool waits = false)
        {
            ids.Add(id);
            nodes.Add(n);
            where.Add(district);
            needs.Add(waits);
        }
        // A place's manager with the first of their crew under them, and how many more there are: the staff of the place, the manager among them, as its page counts.
        void AddPlaceManager(CPlace pl, int level, bool expand)
        {
            CPerson mgr = Cw.ManagerOf(pl);
            List<CPerson> crew = Cw.StaffAt(pl.Key).Where(p => p != mgr).ToList();
            Add("mgr:" + pl.Key, ONode(UiOrgKind.Person, level, mgr.Name, pl.Name, w.F("reallife_co_tree_staff", "{count} staff", "count", (crew.Count + 1).ToString()), true, mgr.Hue), pl.District, Cw.OpenAsksOf(pl.Key) > 0);
            if (!expand)
            {
                return;
            }
            for (int k = 0; k < 3 && k < crew.Count; k++)
            {
                Add("crew:" + crew[k].Id, ONode(UiOrgKind.Person, level + 1, crew[k].Name, null, Tr(w, crew[k].Skill), false, crew[k].Hue, UiRole.None));
            }
            if (crew.Count > 3)
            {
                Add("more:" + pl.Key, ONode(UiOrgKind.More, level + 1, w.F("reallife_co_and_more", "and {count} more", "count", (crew.Count - 3).ToString())));
            }
        }
        Add("cap:owner", ONode(UiOrgKind.Caption, 0, w.T("reallife_co_tree_owner", "Owner")));
        Add("own", ONode(UiOrgKind.Person, 0, Cw.Owner, w.F("reallife_co_tree_owner_sub", "Owner, all {count} businesses", "count", Cw.Places.Count.ToString()), null, false, 0.05f));
        if (f.Deep)
        {
            CSeat coo = Cw.Seat(1), chro = Cw.Seat(2), cfo = Cw.Seat(3);   // the operating chief and the finance chief in post, the people chief's seat empty
            Add("cap:exec", ONode(UiOrgKind.Caption, 0, w.T("reallife_co_tree_executive", "Executive")));
            Add("exe:1", ONode(UiOrgKind.Person, 1, coo.Name, SeatName(w, coo), w.F("reallife_co_tree_managers", "{count} managers", "count", Cw.Managers().Count.ToString()), true, coo.Hue100 / 100f));
            foreach (CPlace pl in Cw.Places)
            {
                if (pl.ManagerId != null)
                {
                    AddPlaceManager(pl, 2, pl.Name == "Speedy Bites");
                }
            }
            foreach (CPerson r in Cw.Managers().Where(p => p.Post == 2))
            {
                Add("reg:" + Cw.Region, ONode(UiOrgKind.Person, 2, r.Name, w.F("reallife_co_tree_region", "Regional, {district}", "district", Cw.Region), null, false, r.Hue), Cw.Region);
            }
            Add("seat:2", ONode(UiOrgKind.Post, 1, SeatName(w, chro), w.T("reallife_mgr_empty_post", "Empty post"), w.T("reallife_co_tree_hire", "Appoint")));
            Add("exe:3", ONode(UiOrgKind.Person, 1, cfo.Name, SeatName(w, cfo), null, false, cfo.Hue100 / 100f));
        }
        else
        {
            foreach (CPlace pl in Cw.Places)
            {
                if (pl.ManagerId != null)
                {
                    AddPlaceManager(pl, 1, pl.Name == "Speedy Bites");
                }
            }
        }
        List<CPlace> posts = Cw.OpenPosts();
        if (posts.Count > 0)
        {
            Add("cap:posts", ONode(UiOrgKind.Caption, 0, w.T("reallife_co_tree_open_posts", "Open posts")));
        }
        foreach (CPlace pl in posts)
        {
            Add("post:" + pl.Key, ONode(UiOrgKind.Post, 1, pl.Name, Kind(w, pl.Kind) + ", " + pl.District, w.T("reallife_co_tree_hire", "Appoint")), pl.District);
        }
        m.Tree = nodes.ToArray();
        m.TreeIds = ids.ToArray();
        m.TreeDistricts = where.ToArray();
        m.TreeNeeds = needs.ToArray();
        var districts = new List<string>();
        foreach (string d in where)
        {
            if (d != null && !districts.Contains(d))
            {
                districts.Add(d);
            }
        }
        m.Districts = districts.ToArray();
        m.TreeTitle = w.T("reallife_co_who_decides", "Who decides what");
        m.TreeFoot = f.Deep ? w.T("reallife_co_open_policy", "Open the policy centre") : null;
        m.Empty = w.T("reallife_co_mgr_none_body", "Pick a person in the tree to see what they decide, what they may spend and how they are doing.");
        string tail = state.Substring(state.IndexOf('_') + 1);
        string aPost = posts.Count > 0 ? posts[0].Key : null;
        switch (tail)
        {
            case "owner":
            case "handover":
            case "none":
                m.Chosen = "own";
                m.Profile = OwnerProfileSample(w, tail == "none", tail == "handover");
                break;
            case "region":
                m.Chosen = "reg:" + Cw.Region;
                m.Profile = RegionProfileSample(w);
                break;
            case "post":
                m.Chosen = "post:" + Cw.Pl("Pioneer Game Studio").Key;
                m.Profile = PostProfileSample(w);
                break;
            default:
                m.Chosen = "mgr:" + Cw.Pl("Speedy Bites").Key;
                m.Profile = ManagerProfileSample(w, bare);
                break;
        }
        if (tail.StartsWith("sub_", System.StringComparison.Ordinal))
        {
            ManagerSubSample(f, m, w, tail.Substring(4));
        }
    }

    // The word for a task as the sample writes it: its English name through the key.
    private static string TaskName(CompanyWords w, int task) => Tr(w, ManagerTasks[task]);

    // What each of the ten kinds of decision is limited to, for a manager, in the screen's own words (the limit's key, filled in); null where a kind has no limit line.
    private static string[] TaskLines(CompanyWords w, CPerson manager = null, CPlace place = null)
    {
        int band = manager != null ? manager.Band : 110;
        int budget = manager != null ? manager.Budget : 400;
        int keep = place != null ? place.Keep : 9;
        return new string[]
        {
            w.F("reallife_mgr_limit_wage_value", "{share} of the going rate", "share", band + "%"), w.F("reallife_mgr_limit_wage_value", "{share} of the going rate", "share", band + "%"),
            w.F("reallife_mgr_limit_budget_value", "{amount} a month", "amount", "$" + budget), null, w.F("reallife_mgr_limit_stock_value", "{days} days on top of the week", "days", "3"),
            w.F("reallife_mgr_limit_price_value", "{low} to {high} of the market price", "low", "90%", "high", "115%"),
            w.F("reallife_mgr_task_rota_when", "Sundays at {hour}:00, only to fill a gap, never taking anybody off the rota.", "hour", "18"),
            w.F("reallife_mgr_limit_keep_value", "{count} staff", "count", keep.ToString()), w.F("reallife_mgr_limit_marketing_value", "{amount} a month", "amount", "$500"),
            w.F("reallife_mgr_limit_lease_value", "up to {share} more than the rent now", "share", "5%")
        };
    }

    private static CompanyProfile ManagerProfileSample(CompanyWords w, bool noExecutive = false)
    {
        CPlace pl = Cw.Pl("Speedy Bites");
        CPerson ray = Cw.ManagerOf(pl), dev = Cw.P("dev");
        string place = pl.Key, first = FirstName(ray.Name);
        List<CPerson> staff = Cw.StaffAt(place);
        List<CPerson> crew = staff.Where(x => x != ray).ToList();
        int ceiling = dev.Going * ray.Band / 100;
        int used = (ray.Spent * 100 + ray.Budget / 2) / ray.Budget;
        var p = new CompanyProfile
        {
            Id = "mgr:" + place, Name = ray.Name, Role = w.F("reallife_co_role_manager", "Manager, {place}", "place", pl.Name), Hue = ray.Hue, Ring = UiRole.Ok, Status = w.T("reallife_mgr_in_post", "In post"), StatusTone = UiRole.Ok,
            FactLabels = new[] { w.T("reallife_co_f_manager_since", "Manager since"), w.T("reallife_co_f_wage", "Wage"), w.T("reallife_co_f_this_month", "This month"), w.T("reallife_co_f_asked", "Asked you") },
            FactValues = new[] { Dy(w, ray.Since), CompanyWorld.PerHour(ray.Paid), w.F("reallife_co_f_decisions", "{count} decisions", "count", ray.Decided.ToString()), ray.AskedYou.ToString() },
            WaitingText = w.T("reallife_co_one_waiting", "1 question waiting for you"), WaitingLine = w.F("reallife_hier_say_exception", "{name} asks for {asked} an hour; the limit is {ceiling}.", "name", dev.Name, "asked", CompanyWorld.Wage(dev.Asked), "ceiling", CompanyWorld.Wage(ceiling)),
            Waiting = CB(w.T("reallife_co_answer", "Answer"), UiButtonKind.Primary, UiAction.Alt, CA(CompanyDo.Go, "card", 1), UiIcon.Next),
            TasksTitle = w.F("reallife_co_decisions_owns", "Decisions {name} owns", "name", first), TasksHint = w.F("reallife_co_switch_hint", "Switch on = {name} decides", "name", first)
        };
        string[] lines = TaskLines(w, ray, pl);
        bool[] on = Owns(ray.Id, Cw.HandOver);
        var tasks = new List<CompanyTask>();
        for (int t = 0; t < ManagerTasks.Length; t++)
        {
            tasks.Add(new CompanyTask
            {
                Id = place + ":" + t, Icon = ManagerGlyphs[t], Title = TaskName(w, t), Line = lines[t], On = on[t], Who = on[t] ? first : w.T("reallife_hier_you", "You"),
                Act = CA(CompanyDo.TaskSet, place, t, on[t] ? 0 : 1)
            });
        }
        p.Tasks = tasks.ToArray();
        p.LimitsTitle = w.T("reallife_co_limits_title", "Limits and where questions go");
        p.LimitsAll = CB(w.T("reallife_co_all_limits", "All limits"), UiButtonKind.Quiet, UiAction.Count, CA(CompanyDo.Go, "mgr:" + place, CompanyView.Managers, CompanyView.SubTask, 0));
        p.Limits = new[]
        {
            new CompanyLimit { Title = w.T("reallife_co_lim_raise", "Raise limit"), Sub = w.T("reallife_co_lim_raise_sub", "Of the going rate, above the pay now"), Value = ray.Band + "%", CanLess = true, CanMore = true,
                Less = CA(CompanyDo.LimitSet, place, 0, 1), More = CA(CompanyDo.LimitSet, place, 0, 3), Typed = CA(CompanyDo.LimitSet, place, 0, 2) },
            new CompanyLimit { Title = w.T("reallife_co_lim_budget", "Spend limit"), Sub = w.T("reallife_co_lim_budget_sub", "A month, for overtime and hires"), Value = "$" + ray.Budget, CanLess = true, CanMore = true,
                Less = CA(CompanyDo.LimitSet, place, 4, 1), More = CA(CompanyDo.LimitSet, place, 4, 3), Typed = CA(CompanyDo.LimitSet, place, 4, 2) }
        };
        CSeat coo = Cw.Seat(1);
        p.RouteTitle = w.F("reallife_co_route_title", "Questions {name} cannot decide go to", "name", first);
        p.RouteSub = noExecutive ? null : w.F("reallife_co_route_sub", "{name} picks up what you leave for a day", "name", coo.Name);
        p.RouteOptions = noExecutive ? new[] { w.T("reallife_hier_you", "You") } : new[] { w.T("reallife_hier_you", "You"), coo.Name };
        p.RouteChosen = noExecutive ? 0 : 1;
        p.RouteActs = noExecutive ? new[] { CA(CompanyDo.TaskSet, place, -1, 0) } : new[] { CA(CompanyDo.TaskSet, place, -1, 0), CA(CompanyDo.TaskSet, place, -1, 1) };
        p.GradesTitle = w.F("reallife_co_how_doing", "How {name} is doing", "name", first);
        p.GradeLabels = new[] { w.T("reallife_co_g_judgement", "Judgement"), w.T("reallife_co_g_speed", "Speed"), w.T("reallife_co_g_thrift", "Thrift"), w.T("reallife_co_g_morale", "Crew morale") };
        p.GradeValues = new[] { 92, 74, 100 - used, Cw.CrewStanding(place) };
        p.LogTitle = w.T("reallife_co_log", "Log");
        p.LogAll = w.F("reallife_co_all_count", "All {count}", "count", ray.Decided.ToString());
        p.LogOpen = CA(CompanyDo.Go, "mgr:" + place, CompanyView.Managers, CompanyView.SubDecision);
        CPerson lena = Cw.P("lena");
        p.Log = new[]
        {
            new CompanyDone { When = Dy(w, 214), Text = w.F("reallife_mgr_did_stock", "Reordered stock for {count} products, a {day}-day cover", "count", "6", "day", Dn(215)), Money = "-" + CM(410), Tone = -1 },
            new CompanyDone { When = Dy(w, 214), Text = w.F("reallife_mgr_did_raise_agree", "Agreed a raise of {raise} for {name}", "name", lena.Name, "raise", CompanyWorld.Wage(lena.RaiseSizes[0])), Money = "-" + CM(YearCost(lena.RaiseSizes[0])), Tone = -1 },
            new CompanyDone { When = Dy(w, 213), Text = w.F("reallife_mgr_did_prices", "Changed {count} prices", "count", "4"), Money = "+" + CM(85), Tone = 1 },
            new CompanyDone { When = Dy(w, 212), Text = w.F("reallife_mgr_did_rota", "Wrote the rota: {shifts} shifts, {covered} of {wanted} hours covered", "shifts", Cw.RotaOf(pl).ShiftCount.ToString(), "covered", (Cw.RotaOf(pl).Open - Cw.RotaOf(pl).Short).ToString(), "wanted", Cw.RotaOf(pl).Open.ToString()), Money = Tr(w, "no cost") }
        };
        p.CrewTitle = w.F("reallife_co_crew_title", "{name}'s crew", "name", first);
        p.CrewCount = w.F("reallife_co_staff_count_n", "{count} staff", "count", crew.Count.ToString());
        p.CrewTotal = crew.Count;
        p.Crew = crew.Take(3).Select(x => CrewItem(w, x, true)).ToArray();   // the skill of each, as the live page grades them
        p.Buttons = new[]
        {
            CB(w.T("reallife_mgr_replace", "Replace or stand down"), UiButtonKind.Secondary, UiAction.Count, CA(CompanyDo.Go, "mgr:" + place, CompanyView.Managers, CompanyView.SubAppoint), UiIcon.People),
            CB(w.T("reallife_hier_open_dashboard", "Open the dashboard"), UiButtonKind.Secondary, UiAction.Count, CA(CompanyDo.Go, "mgr:" + place, CompanyView.Managers, CompanyView.SubDashboard), UiIcon.ChartPie)
        };
        return p;
    }

    // A manager as a row of the owner's list of managers: the place they run (or the district), how its crew stands, and a press that opens them.
    private static CompanyItem ManagerRow(CompanyWords w, CPerson m)
    {
        bool region = m.Post == 2;
        CPlace pl = region ? null : Cw.Pl(m.Place);
        int standing = region ? Cw.StandingIn(Cw.Region) : Cw.CrewStanding(m.Place);
        return new CompanyItem
        {
            Title = m.Name, Sub = region ? w.F("reallife_co_tree_region", "Regional, {district}", "district", Cw.Region) : pl.Name, Face = true, Hue = m.Hue, Ring = Cw.OpenAsksOf(m.Place) > 0 ? UiRole.Warn : UiRole.Ok,
            Grade = standing, Figure = standing.ToString(), Note = w.T("reallife_co_crew_standing", "crew standing"), Press = true,
            Act = CA(CompanyDo.Go, region ? "reg:" + Cw.Region : "mgr:" + pl.Key, CompanyView.Managers)
        };
    }

    private static CompanyItem AskItem(CompanyWords w, CAsk a)
    {
        CPerson asker = Cw.P(a.Asker);
        return new CompanyItem
        {
            Grade = -1, Title = AskTitle(w, a), FaceName = asker.Name, Sub = Cw.Pl(a.Place).Name, Face = true, Hue = asker.Hue, Ring = a.Open ? UiRole.Warn : UiRole.Ok,
            Figure = Dy(w, a.Day), Press = true, Act = CA(CompanyDo.Go, "card", a.Id)
        };
    }

    private static string AskTitle(CompanyWords w, CAsk a) => a.Kind == "raise" ? w.T("reallife_co_q_raise_title", "A raise above my limit") : a.Kind == "lease" ? w.T("reallife_co_q_lease_title", "A lease is up for renewal") : w.T("reallife_co_q_budget", "Budget");

    private static CompanyProfile OwnerProfileSample(CompanyWords w, bool nobody = false, bool handover = false)
    {
        List<CPerson> managers = Cw.Managers();
        var p = new CompanyProfile
        {
            Id = "own", Name = Cw.Owner, Role = w.T("reallife_co_role_owner", "Owner"), Hue = 0.05f, Ring = UiRole.Ok,
            FactLabels = nobody ? new[] { w.T("reallife_co_f_businesses", "Businesses") } : new[] { w.T("reallife_co_f_businesses", "Businesses"), w.T("reallife_co_f_managers", "Managers"), w.T("reallife_co_f_asked", "Asked you") },
            FactValues = nobody ? new[] { Cw.Places.Count.ToString() } : new[] { Cw.Places.Count.ToString(), managers.Count.ToString(), Cw.Asks.Count(a => a.Open && a.Holder == "you").ToString() }
        };
        var more = new List<CompanyBlock>();
        CompanyBlock[] levels =
        {
            CSec(w.T("reallife_co_owner_levels", "Levels")),
            new CompanyBlock { Kind = CompanyBlockKind.Switch, Text = w.T("reallife_co_deep_mode", "Deep Mode"), Sub = w.T("reallife_co_deep_sub", "The executives, regions, the policy centre and every question's inbox"),
                On = !nobody, Value = w.T(nobody ? "reallife_co_off" : "reallife_co_on", nobody ? "Off" : "On"), Act = CA(CompanyDo.DeepMode, null, nobody ? 1 : 0) }
        };
        if (!nobody)
        {
            List<CAsk> mine = Cw.Asks.Where(a => a.Open && a.Holder == "you").ToList();
            more.Add(CSec(w.T("reallife_co_owner_waiting", "Waiting for you"), mine.Count.ToString()));
            more.Add(new CompanyBlock { Kind = CompanyBlockKind.Items, Items = mine.Select(a => AskItem(w, a)).ToArray() });
            more.Add(CSec(w.T("reallife_mgr_sec_directory", "Your managers"), managers.Count.ToString()));
            more.Add(new CompanyBlock { Kind = CompanyBlockKind.Items, Items = managers.Select(m => ManagerRow(w, m)).ToArray() });
            more.Add(new CompanyBlock { Kind = CompanyBlockKind.Break });
            int decided = 0, cost = 0;
            foreach (CPerson m in managers)
            {
                decided += m.Decided;
                cost += CompanyWorld.Allowance(m.MSkill, m.Post == 2) * 30;   // a month of the day's allowance
            }
            more.Add(CSec(w.T("reallife_co_owner_month", "This month")));
            more.Add(CRow(w.T("reallife_co_owner_decided", "Decisions made for you"), decided.ToString()));
            more.Add(CRow(w.T("reallife_co_owner_cost", "Managers' allowances, a month"), CM(cost)));
        }
        else
        {
            more.Add(CNote(w.T("reallife_co_no_managers", "No manager yet. A manager decides what you hand over, and asks you what is past their limit.")));
            CompanyItem[] ten = HandOverRows(w);
            more.Add(CSec(w.T("reallife_co_hand_over", "What you can hand over"), ten.Length.ToString()));
            more.Add(new CompanyBlock { Kind = CompanyBlockKind.Items, Items = ten.Take((ten.Length + 1) / 2).ToArray() });
            more.AddRange(levels);   // the levels stand under the first five, where that column would end short
            more.Add(new CompanyBlock { Kind = CompanyBlockKind.Break });
            more.Add(CSec(w.T("reallife_co_hand_over_more", "And these too")));
            more.Add(new CompanyBlock { Kind = CompanyBlockKind.Items, Items = ten.Skip((ten.Length + 1) / 2).ToArray() });
            // Who could take a post: the company's own people who stand best.
            List<CPerson> could = Cw.People.OrderByDescending(x => x.Standing).ThenBy(x => x.Name, System.StringComparer.Ordinal).Take(5).ToList();
            more.Add(CSec(w.T("reallife_co_could_manage", "Who could take a post")));
            more.Add(new CompanyBlock { Kind = CompanyBlockKind.Items, Items = could.Select(x => new CompanyItem { Title = x.Name, Sub = Pn(w, x) + ", " + Cw.Pl(x.Place).Name, Face = true, Hue = x.Hue, Ring = UiRole.Ok, Grade = x.Standing,
                Figure = x.Standing.ToString(), Press = true, Act = CA(CompanyDo.Go, "emp:" + x.Id, CompanyView.Staff) }).ToArray() });
        }
        if (!nobody)
        {
            more.AddRange(levels);
            int open = Cw.Asks.Count(a => a.Open);
            more.Add(new CompanyBlock
            {
                Kind = CompanyBlockKind.Items, Items = new[]
                {
                    new CompanyItem { Grade = -1, Title = w.T("reallife_co_inbox", "Questions inbox"), Sub = w.T("reallife_co_level_inbox", "Every question your managers pass up"), Icon = UiIcon.Envelope,
                        Figure = w.F("reallife_co_inbox_open", "{count} open", "count", open.ToString()), Press = true, Act = CA(CompanyDo.Go, null, CompanyView.Managers, CompanyView.SubInbox) },
                    new CompanyItem { Grade = -1, Title = w.T("reallife_co_policy_centre", "Policy centre"), Sub = w.T("reallife_co_level_policy", "Rules for the company, a region or a place"), Icon = UiIcon.Sliders,
                        Press = true, Act = CA(CompanyDo.Go, null, CompanyView.Managers, CompanyView.SubPolicy) },
                    new CompanyItem { Grade = -1, Title = w.T("reallife_co_templates", "Templates"), Sub = w.T("reallife_co_level_templates", "Sets of rules to put on a scope in one step"), Icon = UiIcon.ClipboardText,
                        Figure = "3", Press = true, Act = CA(CompanyDo.Go, null, CompanyView.Managers, CompanyView.SubTemplates) }
                }
            });
            // What no manager holds yet, from the ten kinds: the owner's own page lists what is still theirs to hand over, with how many of the managers have each.
            CompanyItem[] all = HandOverRows(w);
            var still = new List<CompanyItem>();
            for (int i = 0; i < all.Length; i++)
            {
                int n = Held(i, handover);
                if (n < managers.Count)
                {
                    all[i].Figure = w.F("reallife_co_held_by", "{count} of {total}", "count", n.ToString(), "total", managers.Count.ToString());
                    still.Add(all[i]);
                }
            }
            more.Add(CSec(w.T("reallife_co_hand_over", "What you can hand over"), still.Count.ToString()));
            more.Add(new CompanyBlock { Kind = CompanyBlockKind.Items, Items = still.ToArray() });
        }
        p.More = more.ToArray();
        return p;
    }

    // The ten decisions a manager can own, as rows with their glyph: what a post hands over.
    private static CompanyItem[] HandOverRows(CompanyWords w)
    {
        string[] keys = { "raises", "offers", "overtime", "claims", "stock", "prices", "rota", "hiring", "marketing", "leases" };
        string[] lines = TaskLines(w);
        var rows = new CompanyItem[keys.Length];
        for (int i = 0; i < rows.Length; i++)
        {
            rows[i] = new CompanyItem { Grade = -1, Title = w.T("reallife_mgr_task_" + keys[i], TaskName(w, i)), Sub = lines[i], Icon = ManagerGlyphs[i] };
        }
        return rows;
    }

    private static CompanyProfile RegionProfileSample(CompanyWords w)
    {
        CPerson meera = Cw.Managers().First(x => x.Post == 2);
        string district = Cw.Region, first = FirstName(meera.Name);
        List<CPlace> places = Cw.Places.Where(x => x.District == district).ToList();
        var p = new CompanyProfile
        {
            Id = "reg:" + district, Name = meera.Name, Role = w.F("reallife_co_tree_region", "Regional, {district}", "district", district), Hue = meera.Hue, Ring = UiRole.Ok, Status = w.T("reallife_mgr_in_post", "In post"), StatusTone = UiRole.Ok,
            FactLabels = new[] { w.T("reallife_co_f_since", "Since"), w.T("reallife_co_f_places", "Places"), w.T("reallife_co_f_standing_in", "Standing in"), w.T("reallife_co_f_decisions_n", "Decisions") },
            FactValues = new[] { Dy(w, meera.Since), places.Count.ToString(), Cw.StandingIn(district).ToString(), meera.Decided.ToString() }
        };
        p.GradesTitle = w.F("reallife_co_how_doing", "How {name} is doing", "name", first);
        p.GradeLabels = new[] { w.T("reallife_co_g_judgement", "Judgement"), w.T("reallife_co_g_speed", "Speed"), w.T("reallife_co_g_morale", "Crew morale"), w.T("reallife_hier_fig_score", "Score") };
        p.GradeValues = new[] { 88, 71, Cw.StandingIn(district), 77 };
        List<CAsk> area = Cw.Asks.Where(a => a.Open && places.Any(x => x.Key == a.Place)).ToList();
        int profit = places.Sum(x => Cw.Result(x)), gaps = places.Count(x => Cw.StaffAt(x.Key).Count < x.Keep);
        var more = new List<CompanyBlock>
        {
            // Yesterday in the district, from the same figures the district's dashboard reads: its profit, the places short of crew, the questions waiting.
            new CompanyBlock { Kind = CompanyBlockKind.Figures, Options = new[] { w.T("reallife_hier_fig_profit", "Profit"), w.T("reallife_co_f_gaps", "Gaps"), w.T("reallife_co_f_pending", "Pending") },
                Values = new[] { CoSigned(profit), gaps.ToString(), area.Count.ToString() } },
            CSec(w.T("reallife_mgr_region_their_places", "Their places"), places.Count.ToString()),
            new CompanyBlock { Kind = CompanyBlockKind.Items, Items = places.Select(x => PlaceRow(w, x)).ToArray() },
            CSec(w.T("reallife_hier_fig_budget", "Budget"), places.Count(x => x.ManagerId != null).ToString()),
            new CompanyBlock { Kind = CompanyBlockKind.Items, Items = BudgetItems(w, places.Where(x => x.ManagerId != null).Select(x => Cw.ManagerOf(x)).ToList()) },
            CSec(w.T("reallife_co_asked_in_area", "Asked in their area"), area.Count.ToString()),
            new CompanyBlock { Kind = CompanyBlockKind.Items, Items = area.Select(a => AskItem(w, a)).ToArray() }
        };
        more.AddRange(new[]
        {
            new CompanyBlock { Kind = CompanyBlockKind.Break },
            CSec(w.T("reallife_mgr_sec_manager", "Manager")),
            CRow(w.T("reallife_mgr_skill", "Skill"), meera.MSkill.ToString()),
            CRow(w.T("reallife_mgr_skill_far", "Skill standing in for a place"), meera.FarSkill.ToString()),
            CRow(w.T("reallife_mgr_allowance", "Allowance"), w.F("reallife_co_row_a_day", "{amount} a day", "amount", CM(CompanyWorld.Allowance(meera.MSkill, true)))),
            CRow(w.T("reallife_mgr_pay", "Pay"), CompanyWorld.PerHour(meera.Paid)),
            CRow(w.T("reallife_mgr_mistakes", "Mistakes this month"), "2"),
            CSec(w.T("reallife_co_log", "Log"), meera.Decided.ToString()),
            new CompanyBlock
            {
                Kind = CompanyBlockKind.Lines, Rows = new[]
                {
                    new CompanyDone { When = Dy(w, 214), Text = w.F("reallife_mgr_did_stock", "Reordered stock for {count} products, a {day}-day cover", "count", "6", "day", Dn(215)), Money = "-" + CM(410), Tone = -1 },
                    new CompanyDone { When = Dy(w, 213), Text = w.F("reallife_mgr_did_prices", "Changed {count} prices", "count", "4"), Money = "+" + CM(85), Tone = 1 },
                    new CompanyDone { When = Dy(w, 211), Text = w.F("reallife_mgr_did_rota", "Wrote the rota: {shifts} shifts, {covered} of {wanted} hours covered", "shifts", Cw.RotaOf(Cw.Pl("Speedy Bites")).ShiftCount.ToString(), "covered", (Cw.RotaOf(Cw.Pl("Speedy Bites")).Open - Cw.RotaOf(Cw.Pl("Speedy Bites")).Short).ToString(), "wanted", Cw.RotaOf(Cw.Pl("Speedy Bites")).Open.ToString()) },
                    new CompanyDone { When = Dy(w, 210), Text = w.F("reallife_mgr_did_prices", "Changed {count} prices", "count", "2"), Money = "+" + CM(40), Tone = 1 },
                    new CompanyDone { When = Dy(w, 208), Text = w.F("reallife_mgr_did_stock", "Reordered stock for {count} products, a {day}-day cover", "count", "3", "day", Dn(209)), Money = "-" + CM(220), Tone = -1 }
                }
            },
            CNote(w.T("reallife_mgr_region_what", "A regional manager looks over the places of a district and stands in for a manager who is off sick."))
        });
        p.More = more.ToArray();
        p.Buttons = new[]
        {
            CB(w.T("reallife_mgr_replace", "Replace or stand down"), UiButtonKind.Secondary, UiAction.Count, CA(CompanyDo.Go, "reg:" + district, CompanyView.Managers, CompanyView.SubAppoint), UiIcon.People),
            CB(w.T("reallife_hier_open_dashboard", "Open the dashboard"), UiButtonKind.Secondary, UiAction.Count, CA(CompanyDo.Go, "reg:" + district, CompanyView.Managers, CompanyView.SubRegion), UiIcon.ChartPie)
        };
        return p;
    }

    // Managers with what each has spent of the month's budget: a grade for the share used, the sum, and the budget under it.
    private static CompanyItem[] BudgetItems(CompanyWords w, List<CPerson> managers) => managers.Select(m => new CompanyItem
    {
        Grade = m.Spent * 100 / m.Budget, Title = m.Name, Sub = Cw.Pl(m.Place).Name, Face = true, Hue = m.Hue, Ring = UiRole.Ok, Figure = CM(m.Spent), Note = w.F("reallife_co_of_budget", "of {amount}", "amount", CM(m.Budget)),
        Press = true, Act = CA(CompanyDo.Go, "mgr:" + m.Place, CompanyView.Managers)
    }).ToArray();

    // A place as a row: its manager's face and how its crew stands, or the empty post.
    private static CompanyItem PlaceRow(CompanyWords w, CPlace pl)
    {
        CPerson mgr = Cw.ManagerOf(pl);
        int standing = Cw.CrewStanding(pl.Key);
        var item = new CompanyItem { Grade = standing, Figure = standing.ToString(), Note = w.T("reallife_co_crew_standing", "crew standing"), Press = true };
        if (mgr != null)
        {
            item.Title = mgr.Name;
            item.Sub = pl.Name;
            item.Face = true;
            item.Hue = mgr.Hue;
            item.Ring = UiRole.Ok;
            item.Act = CA(CompanyDo.Go, "mgr:" + pl.Key, CompanyView.Managers);
        }
        else
        {
            item.Title = pl.Name;
            item.Sub = w.T("reallife_mgr_empty_post", "Empty post");
            item.Icon = UiIcon.Store;
            item.Act = CA(CompanyDo.Go, "post:" + pl.Key, CompanyView.Managers);
        }
        return item;
    }

    private static CompanyProfile PostProfileSample(CompanyWords w)
    {
        CPlace pl = Cw.Pl("Pioneer Game Studio");
        List<CPerson> staff = Cw.StaffAt(pl.Key);
        List<CPerson> could = staff.Where(x => x.MSkill > 0).OrderByDescending(x => x.MSkill).Take(4).ToList();
        var p = new CompanyProfile
        {
            Id = "post:" + pl.Key, Name = w.F("reallife_co_tree_post", "Manager of {place}", "place", pl.Name), Role = Kind(w, pl.Kind) + ", " + pl.District, Glyph = UiIcon.IdBadge, GlyphTint = UiRole.Muted,
            Status = w.T("reallife_mgr_empty_post", "Empty post"), StatusTone = UiRole.Muted,
            FactLabels = new[] { w.T("reallife_co_f_staff", "Staff"), w.T("reallife_co_f_best_skill", "Best skill"), w.T("reallife_co_f_wage_ask", "Wages from") },
            FactValues = new[] { staff.Count.ToString(), staff.Max(x => x.MSkill).ToString(), CompanyWorld.PerHour(staff.Min(x => x.Paid)) }
        };
        var more = new List<CompanyBlock>
        {
            CSec(w.T("reallife_co_candidates", "Who could do it"), could.Count.ToString()),
            new CompanyBlock
            {
                Kind = CompanyBlockKind.Items, Items = could.Select(x => new CompanyItem { Title = x.Name, Sub = Pn(w, x), Face = true, Hue = x.Hue, Ring = UiRole.None, Grade = x.MSkill, Figure = CompanyWorld.Wage(x.Paid),
                    Note = w.F("reallife_co_skill_n", "skill {n}", "n", x.MSkill.ToString()), Press = true, Act = CA(CompanyDo.Go, "post:" + pl.Key, CompanyView.Managers, CompanyView.SubAppoint, 0, "mc:" + x.Id) }).ToArray()
            },
            CSec(w.T("reallife_mgr_sec_post", "The post")),
            CNote(w.T("reallife_mgr_place_none", "Nobody manages this place yet. A manager decides what you hand over and asks you what is past their limit.")),
            CRow(w.T("reallife_mgr_row_place", "Place"), pl.Name),
            CRow(w.T("reallife_mgr_row_district", "District"), pl.District),
            CRow(w.T("reallife_co_f_standing", "Standing"), Cw.CrewStanding(pl.Key).ToString()),
            new CompanyBlock { Kind = CompanyBlockKind.Break },
            CSec(w.T("reallife_co_post_cost", "What a manager here costs")),
            CRow(w.T("reallife_mgr_allowance", "Allowance"), w.F("reallife_co_allowance_range", "from {least} to {most} a day", "least", CM(CompanyWorld.Allowance(could.Min(x => x.MSkill))), "most", CM(CompanyWorld.Allowance(could.Max(x => x.MSkill))))),
        };
        // What the place did yesterday and how its crew stands: the reasons to put somebody in the post.
        more.Add(CSec(w.T("reallife_co_post_state", "How it goes without one")));
        more.Add(CRow(w.T("reallife_co_f_takings", "Takings"), CM(pl.Takings)));
        more.Add(CRow(w.T("reallife_co_result_label", "Yesterday's result"), CoSigned(Cw.Result(pl)), Cw.Result(pl) >= 0 ? UiRole.OkText : UiRole.DangerText));
        more.Add(CRow(w.T("reallife_co_post_questions", "Questions waiting here"), Cw.OpenAsksOf(pl.Key).ToString()));
        string[] lines = TaskLines(w, null, pl);
        int[] owned = { 0, 2, 4, 5, 6, 7 };
        more.Add(CSec(w.T("reallife_mgr_sec_would", "What a manager here decides"), owned.Length.ToString()));
        more.Add(new CompanyBlock { Kind = CompanyBlockKind.Items, Items = owned.Select(i => new CompanyItem { Grade = -1, Title = TaskName(w, i), Sub = lines[i], Icon = ManagerGlyphs[i] }).ToArray() });
        p.More = more.ToArray();
        p.Buttons = new[] { CB(w.T("reallife_mgr_appoint", "Appoint"), UiButtonKind.Primary, UiAction.Alt, CA(CompanyDo.Go, "post:" + pl.Key, CompanyView.Managers, CompanyView.SubAppoint), UiIcon.People) };
        return p;
    }

    // The sub pages of Managers: a list and a card, each from the subject the tree had picked.
    private static void ManagerSubSample(CompanyFacts f, CompanyManagersFacts m, CompanyWords w, string which)
    {
        var page = new CompanyPage();
        CPlace speedy = Cw.Pl("Speedy Bites");
        CPerson ray = Cw.P("ray"), dev = Cw.P("dev");
        string place = speedy.Key, first = FirstName(ray.Name);
        switch (which)
        {
            case "appoint":
            {
                List<CPerson> could = Cw.StaffAt(place).OrderByDescending(x => x.MSkill).Take(4).ToList();
                int chosen = could.FindIndex(x => x.Id == "lena");
                page.List = new CompanyList
                {
                    Title = w.T("reallife_co_candidates", "Who could do it"), Count = could.Count.ToString(), Chosen = chosen,
                    Rows = could.Select(x => new CompanyRow { Id = "mc:" + x.Id, Title = x.Name, Sub = Pn(w, x), Face = true, Hue = x.Hue, Ring = UiRole.None, Figure = CompanyWorld.Wage(x.Paid), Note = w.F("reallife_co_skill_n", "skill {n}", "n", x.MSkill.ToString()),
                        Chip = x == ray ? w.T("reallife_mgr_in_post", "In post") : null, ChipTone = UiRole.Ok }).ToArray()
                };
                CPerson lena = Cw.P("lena");
                var c = new CompanyCard
                {
                    Kicker = speedy.Name, Title = lena.Name, Sub = w.F("reallife_co_emp_works", "{business}, {skill}", "business", speedy.Name, "skill", Pn(w, lena)), Face = true, Hue = lena.Hue, Ring = UiRole.None,
                    Back = CBack(w, "mgr:" + place, CompanyView.Managers),
                    FactLabels = new[] { w.T("reallife_mgr_skill_label", "Skill"), w.T("reallife_co_f_paid", "Paid"), w.T("reallife_mgr_allowance_label", "Allowance") },
                    FactValues = new[] { lena.MSkill.ToString(), CompanyWorld.Wage(lena.Paid), w.F("reallife_co_row_a_day", "{amount} a day", "amount", CM(CompanyWorld.Allowance(lena.MSkill))) }
                };
                c.Blocks = new[]
                {
                    new CompanyBlock { Kind = CompanyBlockKind.Meter, Text = w.T("reallife_mgr_mistake_chance", "Chance of a mistake"), Value = "7.2%", Number = 14, Tone = UiRole.Warn },
                    CNote(w.F("reallife_mgr_appoint_replaces", "This replaces {name}.", "name", ray.Name)),
                    CBtns(CB(w.T("reallife_mgr_appoint", "Appoint"), UiButtonKind.Primary, UiAction.Alt, CA(CompanyDo.Appoint, lena.Id, 0, 0, 0, place), UiIcon.Check),
                        CB(w.T("reallife_mgr_stand_down", "Stand down"), UiButtonKind.Secondary, UiAction.Count, CA(CompanyDo.StandDown, place, 0), UiIcon.Close, false, null, true),
                        CB(w.T("reallife_mgr_hand_back", "Hand back"), UiButtonKind.Secondary, UiAction.Count, CA(CompanyDo.HandBack, place), UiIcon.Back)),
                    CSec(w.T("reallife_mgr_sec_would_own", "What this person would own"), "10"),
                    new CompanyBlock { Kind = CompanyBlockKind.Items, Items = HandOverRows(w) }
                };
                page.Card = c;
                break;
            }
            case "decisions":
            {
                CPerson lena = Cw.P("lena");
                var decisions = new[]
                {
                    new { Id = "md:1", Task = 4, Day = 214, Text = w.F("reallife_mgr_did_stock", "Reordered stock for {count} products, a {day}-day cover", "count", "6", "day", Dn(215)), Note = "-$410", State = true },
                    new { Id = "md:2", Task = 0, Day = 214, Text = w.F("reallife_mgr_did_raise_agree", "Agreed a raise of {raise} for {name}", "name", lena.Name, "raise", CompanyWorld.Wage(lena.RaiseSizes[0])), Note = "-" + CM(YearCost(lena.RaiseSizes[0])), State = true },
                    new { Id = "md:3", Task = 5, Day = 213, Text = w.F("reallife_mgr_did_prices", "Changed {count} prices", "count", "4"), Note = "+$85", State = true },
                    new { Id = "md:4", Task = 6, Day = 212, Text = w.F("reallife_mgr_did_rota", "Wrote the rota: {shifts} shifts, {covered} of {wanted} hours covered", "shifts", Cw.RotaOf(speedy).ShiftCount.ToString(), "covered", (Cw.RotaOf(speedy).Open - Cw.RotaOf(speedy).Short).ToString(), "wanted", Cw.RotaOf(speedy).Open.ToString()), Note = ray.Name, State = true },
                    new { Id = "md:5", Task = 4, Day = 211, Text = w.F("reallife_mgr_did_stock", "Reordered stock for {count} products, a {day}-day cover", "count", "4", "day", Dn(212)), Note = "-$300", State = false }
                };
                page.List = new CompanyList
                {
                    Title = w.F("reallife_co_decisions_of", "{name}'s decisions", "name", first), Count = decisions.Length.ToString(), Chosen = 0,
                    Rows = decisions.Select(d => new CompanyRow { Id = d.Id, Title = TaskName(w, d.Task), Sub = d.Text, Icon = ManagerGlyphs[d.Task], Figure = Dy(w, d.Day), Note = d.Note,
                        Chip = d.State ? w.T("reallife_mgr_state_done", "Done") : w.T("reallife_mgr_state_undone", "Undone"), ChipTone = d.State ? (d.Id == "md:4" ? UiRole.Muted : UiRole.Ok) : UiRole.Muted }).ToArray()
                };
                var c = new CompanyCard
                {
                    Kicker = speedy.Name, Title = TaskName(w, 4), Icon = UiIcon.Package, Sub = w.F("reallife_ui_mgr_dec_when", "{place}, day {day} at {time}, by {name}", "place", speedy.Name, "day", Dn(214), "time", "08:00", "name", ray.Name),
                    Chips = new[] { w.T("reallife_mgr_state_done", "Done") }, ChipTones = new[] { UiRole.Ok }, Back = CBack(w, "mgr:" + place, CompanyView.Managers)
                };
                c.Blocks = new[]
                {
                    CNote(w.F("reallife_mgr_did_stock", "Reordered stock for {count} products, a {day}-day cover", "count", "6", "day", Dn(215))),
                    CRow("Buns", w.F("reallife_mgr_dec_from_to", "{before} to {after}", "before", "120", "after", "240")),
                    CRow("Patties", w.F("reallife_mgr_dec_from_to", "{before} to {after}", "before", "80", "after", "160")),
                    CRow("Fries", w.F("reallife_mgr_dec_from_to", "{before} to {after}", "before", "60", "after", "120")),
                    CRow("Cola", w.F("reallife_mgr_dec_from_to", "{before} to {after}", "before", "48", "after", "96")),
                    CRow("Salad", w.F("reallife_mgr_dec_from_to", "{before} to {after}", "before", "20", "after", "40")),
                    CRow("Napkins", w.F("reallife_mgr_dec_from_to", "{before} to {after}", "before", "300", "after", "600")),
                    CSec(w.T("reallife_mgr_sec_before", "Before it")),
                    new CompanyBlock { Kind = CompanyBlockKind.Lines, Rows = new[]
                    {
                        new CompanyDone { When = w.F("reallife_co_day_hour", "Day {day}, {hour}", "day", Dn(211), "hour", "08:00"), Text = ray.Name + ": " + w.F("reallife_mgr_did_stock", "Reordered stock for {count} products, a {day}-day cover", "count", "4", "day", Dn(212)), Money = "-$300", Tone = -1 },
                        new CompanyDone { When = w.F("reallife_co_day_hour", "Day {day}, {hour}", "day", Dn(208), "hour", "08:00"), Text = ray.Name + ": " + w.F("reallife_mgr_did_stock", "Reordered stock for {count} products, a {day}-day cover", "count", "5", "day", Dn(209)), Money = "-$355", Tone = -1 }
                    } },
                    CNote(w.T("reallife_mgr_dec_undo_what", "Undoing cancels the order if it has not been delivered.")),
                    new CompanyBlock { Kind = CompanyBlockKind.Meter, Text = w.T("reallife_hier_fig_budget_used", "Budget used"), Value = (ray.Spent * 100 / ray.Budget) + "%", Number = ray.Spent * 100 / ray.Budget, Tone = UiRole.Ok },
                    CBtns(CB(w.T("reallife_mgr_undo", "Undo"), UiButtonKind.Secondary, UiAction.Count, CA(CompanyDo.Undo, null, 1), UiIcon.Back, false, null, true))
                };
                page.Card = c;
                break;
            }
            case "task":
            {
                string[] lines = TaskLines(w, ray, speedy);
                bool[] owns = Owns(ray.Id, Cw.HandOver);
                var list = new List<CompanyRow>();
                for (int k = 0; k < ManagerTasks.Length; k++)
                {
                    list.Add(new CompanyRow { Id = "mk:" + k, Title = TaskName(w, k), Sub = lines[k], Icon = ManagerGlyphs[k], Chip = owns[k] ? first : w.T("reallife_hier_you", "You"), ChipTone = owns[k] ? UiRole.Ok : UiRole.Muted });
                }
                page.List = new CompanyList { Title = w.F("reallife_co_decisions_owns", "Decisions {name} owns", "name", first), Count = list.Count.ToString(), Rows = list.ToArray(), Chosen = 0 };
                var c = new CompanyCard { Kicker = speedy.Name, Title = TaskName(w, 0), Icon = UiIcon.Coin, Back = CBack(w, "mgr:" + place, CompanyView.Managers) };
                // The raises Ray agreed, from the days they were given: the cost of each over a year, the newest first.
                var given = new List<KeyValuePair<int, CompanyDone>>();
                foreach (CPerson x in Cw.StaffAt(place))
                {
                    for (int i = 0; i < x.RaiseDays.Length; i++)
                    {
                        given.Add(new KeyValuePair<int, CompanyDone>(x.RaiseDays[i], new CompanyDone { When = w.F("reallife_co_day_hour", "Day {day}, {hour}", "day", Dn(x.RaiseDays[i]), "hour", "08:00"),
                            Text = ray.Name + ": " + w.F("reallife_mgr_did_raise_agree", "Agreed a raise of {raise} for {name}", "name", x.Name, "raise", CompanyWorld.Wage(x.RaiseSizes[i])), Money = "-" + CM(YearCost(x.RaiseSizes[i])), Tone = -1 }));
                    }
                }
                given.Sort((l, r) => r.Key != l.Key ? r.Key.CompareTo(l.Key) : string.CompareOrdinal(l.Value.Text, r.Value.Text));
                c.Blocks = new[]
                {
                    CNote(w.T("reallife_mgr_task_raises_what", "Requests for a raise here are answered in the morning: the whole raise if the pay asked for is inside the wage band, half if only that fits, otherwise no.")),
                    new CompanyBlock { Kind = CompanyBlockKind.Switch, Text = w.T("reallife_mgr_task_given", "Ray decides this"), Sub = lines[0], On = owns[0], Value = first, Act = CA(CompanyDo.TaskSet, place, 0, 0) },
                    new CompanyBlock { Kind = CompanyBlockKind.Stepper, Text = w.T("reallife_co_lim_raise", "Raise limit"), Sub = w.T("reallife_co_lim_raise_sub", "Of the going rate, above the pay now"), Value = ray.Band + "%", CanLess = true, CanMore = true,
                        Act = CA(CompanyDo.LimitSet, place, 0, 1), Act2 = CA(CompanyDo.LimitSet, place, 0, 3), Act3 = CA(CompanyDo.LimitSet, place, 0, 2) }
                };
                var after = new List<CompanyBlock>
                {
                    CSec(w.T("reallife_co_log", "Log"), given.Count.ToString()),
                    new CompanyBlock { Kind = CompanyBlockKind.Lines, Rows = given.Select(g => g.Value).ToArray() }
                };
                var others = Cw.Managers().Where(x => x != ray && x.Post == 1).ToList();
                after.Add(CSec(w.T("reallife_co_task_elsewhere", "The same decision elsewhere"), others.Count.ToString()));
                after.Add(new CompanyBlock
                {
                    Kind = CompanyBlockKind.Items, Items = others.Select(x => new CompanyItem
                    {
                        Title = x.Name, Sub = Cw.Pl(x.Place).Name + ", " + w.F("reallife_mgr_limit_wage_value", "{share} of the going rate", "share", x.Band + "%"), Face = true, Hue = x.Hue, Ring = Cw.OpenAsksOf(x.Place) > 0 ? UiRole.Warn : UiRole.Ok, Grade = -1,
                        Chip = Owns(x.Id, Cw.HandOver)[0] ? FirstName(x.Name) : w.T("reallife_hier_you", "You"), ChipTone = Owns(x.Id, Cw.HandOver)[0] ? UiRole.Ok : UiRole.Muted, Press = true,
                        Act = CA(CompanyDo.Go, "mgr:" + x.Place, CompanyView.Managers, CompanyView.SubTask, 0)
                    }).ToArray()
                });
                c.Blocks = c.Blocks.Concat(after).ToArray();
                page.Card = c;
                break;
            }
            case "inbox":
            {
                string open = w.T("reallife_hier_state_open", "Open");
                page.List = new CompanyList
                {
                    Title = w.T("reallife_co_inbox", "Questions inbox"), Count = Cw.Asks.Count(a => a.Id <= 3).ToString(), Chosen = 0,
                    Rows = Cw.Asks.Where(a => a.Id <= 3).Select(a => new CompanyRow
                    {
                        Id = "he:" + a.Id, Title = AskTitle(w, a), FaceName = Cw.P(a.Asker).Name, Face = true, Hue = Cw.P(a.Asker).Hue, Ring = a.Open ? UiRole.Warn : UiRole.Ok, Sub = Cw.Pl(a.Place).Name, Figure = Dy(w, a.Day),
                        Note = a.Holder == "you" ? w.T("reallife_hier_you", "You") : Cw.P(a.Holder).Name, Chip = a.Open ? open : w.T("reallife_hier_log_settled", "Settled"), ChipTone = a.Open ? UiRole.Warn : UiRole.Muted
                    }).ToArray()
                };
                var c = new CompanyCard
                {
                    Kicker = w.T("reallife_co_inbox", "Questions inbox"), Title = w.T("reallife_co_q_raise_title", "A raise above my limit"), Sub = speedy.Name, Icon = UiIcon.Envelope,
                    Chips = new[] { open, w.F("reallife_hier_chip_with", "With {who}", "who", w.T("reallife_hier_you", "You")) },
                    ChipTones = new[] { UiRole.Warn, UiRole.Accent }, Back = CBack(w, null, CompanyView.Managers)
                };
                c.Blocks = new[]
                {
                    new CompanyBlock { Kind = CompanyBlockKind.Items, Items = new[] { new CompanyItem { Grade = -1, Title = ray.Name, Sub = w.T("reallife_co_q_manager_asks", "Manager"), Face = true, Hue = ray.Hue, Ring = UiRole.Warn, Press = true, Act = CA(CompanyDo.Go, "mgr:" + place, CompanyView.Managers) } } },
                    CNote(w.F("reallife_hier_say_exception", "{name} asks for {asked} an hour; the limit is {ceiling}.", "name", dev.Name, "asked", CompanyWorld.Wage(dev.Asked), "ceiling", CompanyWorld.Wage(dev.Going * ray.Band / 100))),
                    CompanyBlocks.Facts(RaiseQuestion(w)),
                    CBtns(CB(w.T("reallife_co_answer", "Answer"), UiButtonKind.Primary, UiAction.Alt, CA(CompanyDo.Go, "card", 1), UiIcon.Next)),
                    CBtns(CB(w.T("reallife_hier_open_staff", "Open the staff"), UiButtonKind.Secondary, UiAction.Count, CA(CompanyDo.Go, "emp:" + dev.Id, CompanyView.Staff), UiIcon.People),
                        CB(w.T("reallife_hier_open_dashboard", "Open the dashboard"), UiButtonKind.Secondary, UiAction.Count, CA(CompanyDo.Go, "mgr:" + place, CompanyView.Managers, CompanyView.SubDashboard), UiIcon.ChartPie),
                        CB(w.T("reallife_hier_open_policy", "Open the policy"), UiButtonKind.Secondary, UiAction.Count, CA(CompanyDo.Go, "mgr:" + place, CompanyView.Managers, CompanyView.SubPolicy), UiIcon.Sliders)),
                    CSec(w.T("reallife_hier_sec_answers", "What you could do"), "4"),
                    CompanyBlocks.Answers(RaiseQuestion(w)),
                    CSec(w.T("reallife_hier_sec_story", "The story")),
                    CNote(w.F("reallife_hier_raised_by", "Raised on day {day} by {name}.", "day", Dn(214), "name", ray.Name))
                };
                page.Card = c;
                break;
            }
            case "policy":
            case "rule":
            {
                bool store = which == "rule";   // the rule's page stands on a store; the scope's own page on the company, whose list says no scope above
                string setHere = w.T("reallife_pol_set_here_row", "Set here"), asAbove = w.T("reallife_pol_as_above", "As above");
                var rows = new List<CompanyRow>
                {
                    new CompanyRow { Caption = true, Title = w.T("reallife_pol_cat_hiring", "Hiring") },
                    new CompanyRow { Id = "pf:0", Title = w.T("reallife_pol_field_skill_floor", "Least skill hired"), Icon = CompanyGlyphs.Policy(0), Figure = w.T("reallife_co_sick_none", "None"), Sub = store ? asAbove : null },
                    new CompanyRow { Id = "pf:1", Title = w.T("reallife_pol_field_hire_cap", "Most a hire is paid"), Icon = CompanyGlyphs.Policy(1), Figure = "100%", Sub = store ? asAbove : null },
                    new CompanyRow { Caption = true, Title = w.T("reallife_pol_cat_pay", "Pay") },
                    new CompanyRow { Id = "pf:3", Title = w.T("reallife_pol_field_pay_band", "Pay band"), Icon = CompanyGlyphs.Policy(3), Figure = store ? "110%" : "105%", Sub = store ? setHere : null, Chip = store ? w.T("reallife_co_held", "Held") : null, ChipTone = UiRole.Warn },
                    new CompanyRow { Id = "pf:4", Title = w.T("reallife_pol_field_raise_most", "Largest raise"), Icon = CompanyGlyphs.Policy(4), Figure = "5%", Sub = store ? setHere : null },
                    new CompanyRow { Caption = true, Title = w.T("reallife_pol_cat_escalation", "Escalation") },
                    new CompanyRow { Id = "pf:21", Title = w.T("reallife_pol_field_unmet_days", "Days a request may wait"), Icon = CompanyGlyphs.Policy(21), Figure = w.F("reallife_econ_row_days", "{days} days", "days", "2"), Sub = store ? setHere : null }
                };
                page.List = new CompanyList
                {
                    Title = w.T("reallife_hier_dir_policy", "Policy centre"), Rows = rows.ToArray(),
                    Filters = new[] { w.T("reallife_pol_scope_company", "Company"), Cw.Region, speedy.Name }, Filter = store ? 2 : 0, Chosen = store ? 4 : -1
                };
                m.FilterIds = new[] { "own", "reg:" + Cw.Region, "mgr:" + place };
                if (which == "rule")
                {
                    var c = new CompanyCard
                    {
                        Kicker = w.T("reallife_pol_cat_pay", "Pay") + ", " + w.F("reallife_pol_scope_store", "{store}", "store", speedy.Name), Title = w.T("reallife_pol_field_pay_band", "Pay band"), Icon = CompanyGlyphs.Policy(3),
                        Back = CBack(w, "mgr:" + place, CompanyView.Managers, CompanyView.SubPolicy)
                    };
                    c.Blocks = new[]
                    {
                        CNote(w.T("reallife_pol_field_pay_band_what", "The most anybody is paid after a raise or an offer matched, as a share of the going rate.")),
                        CSec(w.T("reallife_pol_set_here", "Set here")),
                        new CompanyBlock { Kind = CompanyBlockKind.Choice, Text = w.T("reallife_pol_field_pay_band", "Pay band"), Options = new[] { w.F("reallife_pol_follow_one", "Follow: {value}", "value", "105%"), "100%", "105%", "110%", "120%" }, Chosen = 3,
                            Acts = new[] { CA(CompanyDo.RuleSet, place, 3, -1, 2), CA(CompanyDo.RuleSet, place, 3, 0, 2), CA(CompanyDo.RuleSet, place, 3, 1, 2), CA(CompanyDo.RuleSet, place, 3, 2, 2), CA(CompanyDo.RuleSet, place, 3, 3, 2) } },
                        CSec(w.T("reallife_pol_sec_binds", "Where it holds"), "4"),
                        new CompanyBlock
                        {
                            Kind = CompanyBlockKind.Items, Items = new[]
                            {
                                new CompanyItem { Grade = -1, Title = w.T("reallife_pol_scope_company", "The whole company"), Icon = UiIcon.Briefcase, Sub = w.T("reallife_pol_set_here_row", "Set here"), Figure = "105%" },
                                new CompanyItem { Grade = -1, Title = Cw.Region, Icon = UiIcon.MapTrifold, Sub = w.T("reallife_pol_as_above", "As above"), Figure = "105%" },
                                new CompanyItem { Grade = -1, Title = speedy.Name, Icon = UiIcon.Store, Sub = w.T("reallife_pol_set_here_row", "Set here"), Figure = "110%", Chip = w.T("reallife_co_held", "Held"), ChipTone = UiRole.Warn },
                                new CompanyItem { Grade = -1, Title = Cw.Pl("Pioneer Game Studio").Name, Icon = UiIcon.Store, Sub = w.T("reallife_pol_set_here_row", "Set here"), Figure = "100%" }
                            }
                        },
                        CRow(w.T("reallife_pol_col_force", "In force here"), "105%", UiRole.WarnText),
                        CNote(w.F("reallife_pol_held_line", "A lower scope may only be stricter: this one is held to {value}.", "value", "105%"), UiRole.WarnText),
                        CNote(w.T("reallife_pol_way_up", "A higher step is looser."))
                    };
                    page.Card = c;
                }
                else
                {
                    var c = new CompanyCard { Kicker = w.T("reallife_pol_scope_company", "Company"), Title = w.T("reallife_pol_sec_scope", "This scope"), Icon = UiIcon.Sliders, Back = CBack(w, "own", CompanyView.Managers) };
                    var under = new List<CompanyItem>();
                    foreach (CPlace pl in Cw.Places)
                    {
                        CPerson mg = Cw.ManagerOf(pl);
                        int setN = mg == ray ? 3 : mg != null && mg.Id == "omar" ? 1 : 0;
                        under.Add(mg != null
                            ? new CompanyItem { Grade = -1, Title = mg.Name, Sub = pl.Name, Face = true, Hue = mg.Hue, Ring = UiRole.Ok, Figure = setN > 0 ? setN.ToString() : null, Note = setN > 0 ? setHere : null, Press = true,
                                Act = CA(CompanyDo.Go, "mgr:" + pl.Key, CompanyView.Managers, CompanyView.SubPolicy) }
                            : new CompanyItem { Grade = -1, Title = pl.Name, Sub = w.T("reallife_mgr_empty_post", "Empty post"), Icon = UiIcon.Store, Press = true, Act = CA(CompanyDo.Go, "post:" + pl.Key, CompanyView.Managers, CompanyView.SubPolicy) });
                    }
                    c.Blocks = new[]
                    {
                        CNote(w.T("reallife_pol_what", "A rule set here holds for this scope. A lower scope may only be stricter than the one above it.")),
                        CRow(w.T("reallife_pol_own", "Rules set here"), "3"),
                        CRow(w.T("reallife_pol_held_rules", "Rules held to the scope above"), "1", UiRole.WarnText),
                        CSec(w.T("reallife_pol_sec_under", "Places under it"), under.Count.ToString()),
                        new CompanyBlock { Kind = CompanyBlockKind.Items, Items = under.ToArray() },
                        CBtns(CB(w.T("reallife_pol_templates", "Templates"), UiButtonKind.Secondary, UiAction.Count, CA(CompanyDo.Go, "mgr:" + place, CompanyView.Managers, CompanyView.SubTemplates), UiIcon.ClipboardText),
                            CB(w.T("reallife_pol_save_template", "Save as a template"), UiButtonKind.Secondary, UiAction.Count, CA(CompanyDo.TemplateSave, place, 0, 0, 2), UiIcon.FloppyDisk)),
                        CBtns(CB(w.T("reallife_pol_follow", "Follow the scope above"), UiButtonKind.Secondary, UiAction.Count, CA(CompanyDo.RuleClear, place, 0, 0, 2), UiIcon.Back, false, null, true))
                    };
                    page.Card = c;
                }
                break;
            }
            case "templates":
            case "tpl_own":
            {
                bool own = which == "tpl_own";
                string saved = w.F("reallife_pol_tpl_saved", "Saved on day {day}", "day", Dn(190));
                page.List = new CompanyList
                {
                    Title = w.T("reallife_pol_templates", "Templates"), Count = "3", Chosen = own ? 2 : 1,
                    Rows = new[]
                    {
                        new CompanyRow { Id = "pk:b0", Title = w.T("reallife_pol_tpl_start", "The starting policy"), Sub = w.T("reallife_pol_tpl_built", "Built in"), Icon = UiIcon.ClipboardText },
                        new CompanyRow { Id = "pk:b1", Title = w.T("reallife_pol_tpl_careful", "Careful"), Sub = w.T("reallife_pol_tpl_built", "Built in"), Icon = UiIcon.ClipboardText },
                        new CompanyRow { Id = "pk:o1", Title = saved, Sub = w.T("reallife_pol_tpl_yours", "Saved by you"), Icon = UiIcon.ClipboardText }
                    }
                };
                var c = new CompanyCard
                {
                    Kicker = w.F("reallife_pol_scope_store", "{store}", "store", speedy.Name), Title = own ? saved : w.T("reallife_pol_tpl_careful", "Careful"),
                    Sub = own ? w.T("reallife_pol_tpl_yours", "Saved by you") : w.T("reallife_pol_tpl_built", "Built in"), Icon = UiIcon.ClipboardText, Back = CBack(w, "mgr:" + place, CompanyView.Managers, CompanyView.SubPolicy)
                };
                CompanyItem Rule(int field, string name, string value) => new CompanyItem { Grade = -1, Title = name, Icon = CompanyGlyphs.Policy(field), Figure = value };
                c.Blocks = new[]
                {
                    CNote(own ? w.T("reallife_pol_templates_what", "A template is a set of rules you can put on a scope in one step.") : w.T("reallife_pol_tpl_careful_what", "Small raises only, the owner approves every exception, and managers report daily.")),
                    CNote(w.F("reallife_pol_tpl_onto", "Applying it sets these rules for {scope}.", "scope", speedy.Name)),
                    CSec(w.T("reallife_pol_sec_template", "What it sets")),
                    new CompanyBlock
                    {
                        Kind = CompanyBlockKind.Items, Items = new[]
                        {
                            Rule(3, w.T("reallife_pol_field_pay_band", "Pay band"), "100%"), Rule(0, w.T("reallife_pol_field_skill_floor", "Least skill hired"), "50"),
                            Rule(6, w.T("reallife_pol_field_approver", "Who may agree an exception"), w.T("reallife_hier_you", "You")), Rule(12, w.T("reallife_pol_field_budget", "Budget"), "$200"),
                            Rule(19, w.T("reallife_pol_field_gap_days", "Days a gap may last"), w.T("reallife_pol_unit_day", "1 day")), Rule(24, w.T("reallife_pol_field_reports", "Reports"), Tr(w, "Every day"))
                        }
                    },
                    own
                        ? CBtns(CB(w.T("reallife_pol_tpl_apply", "Apply"), UiButtonKind.Primary, UiAction.Alt, CA(CompanyDo.TemplateApply, place, 0, 1, 2), UiIcon.Check),
                            CB(w.T("reallife_pol_tpl_delete", "Delete"), UiButtonKind.Secondary, UiAction.Count, CA(CompanyDo.TemplateDelete, null, 1), UiIcon.Close, false, null, true))
                        : CBtns(CB(w.T("reallife_pol_tpl_apply", "Apply"), UiButtonKind.Primary, UiAction.Alt, CA(CompanyDo.TemplateApply, place, 1, 0, 2), UiIcon.Check))
                };
                page.Card = c;
                break;
            }
            case "dashboard":
            {
                string open = w.T("reallife_hier_state_open", "Open");
                List<CAsk> here = Cw.Asks.Where(a => a.Place == place).OrderByDescending(a => a.Day).ToList();
                List<CPerson> crew = Cw.StaffAt(place).Where(x => x != ray).ToList();
                int used = (ray.Spent * 100 + ray.Budget / 2) / ray.Budget;
                page.List = new CompanyList
                {
                    Title = w.T("reallife_hier_dash_pending", "Questions here"), Count = here.Count.ToString(), Chosen = -1,
                    Rows = here.Select(a => new CompanyRow
                    {
                        Id = "he:" + a.Id, Title = AskTitle(w, a), FaceName = ray.Name, Face = true, Hue = ray.Hue, Ring = a.Open ? UiRole.Warn : UiRole.Ok, Sub = a.Holder == "you" ? w.T("reallife_hier_you", "You") : ray.Name, Figure = Dy(w, a.Day),
                        Chip = a.Open ? open : w.T("reallife_hier_log_settled", "Settled"), ChipTone = a.Open ? UiRole.Warn : UiRole.Muted
                    }).ToArray()
                };
                var c = new CompanyCard
                {
                    Kicker = speedy.Name + ", " + ray.Name, Title = w.T("reallife_hier_dash_title", "Store dashboard"), Icon = UiIcon.ChartPie, Back = CBack(w, "mgr:" + place, CompanyView.Managers),
                    FactLabels = new[] { w.T("reallife_co_f_staff", "Staff"), w.T("reallife_co_f_standing", "Standing"), w.T("reallife_co_f_score", "Score"), w.T("reallife_co_f_pending", "Pending") },
                    FactValues = new[] { w.F("reallife_hier_fig_staff_value", "{staff} of {keep} kept", "staff", Cw.StaffAt(place).Count.ToString(), "keep", speedy.Keep.ToString()), Cw.CrewStanding(place).ToString(), "81", Cw.OpenAsksOf(place).ToString() }
                };
                c.Blocks = new[]
                {
                    new CompanyBlock { Kind = CompanyBlockKind.Figures, Options = new[] { w.T("reallife_hier_fig_budget", "Budget"), w.T("reallife_ibox_profit", "Profit") },
                        Values = new[] { w.F("reallife_mgr_spent_value", "{spent} of {budget}", "spent", CM(ray.Spent), "budget", CM(ray.Budget)), CoSigned(Cw.Result(speedy)) } },
                    new CompanyBlock { Kind = CompanyBlockKind.Meter, Text = w.T("reallife_hier_fig_budget_used", "Budget used"), Value = used + "%", Number = used, Tone = UiRole.Ok },
                    CSec(w.T("reallife_hier_sec_quick", "Do now")),
                    CBtns(CB(w.T("reallife_hier_quick_fill", "Fill the gaps"), UiButtonKind.Primary, UiAction.Alt, CA(CompanyDo.StoreFill, place), UiIcon.People),
                        CB(w.T("reallife_hier_quick_raise", "Raise the underpaid"), UiButtonKind.Secondary, UiAction.Option, CA(CompanyDo.StoreRaise, place), UiIcon.Coin),
                        CB(w.T("reallife_hier_open_policy", "Open the policy"), UiButtonKind.Secondary, UiAction.Count, CA(CompanyDo.Go, "mgr:" + place, CompanyView.Managers, CompanyView.SubPolicy), UiIcon.Sliders)),
                    CSec(w.F("reallife_co_crew_title", "{name}'s crew", "name", first), CountOf(w, System.Math.Min(3, crew.Count), crew.Count)),
                    new CompanyBlock { Kind = CompanyBlockKind.Items, Items = crew.Take(3).Select(x => CrewItem(w, x, true)).ToArray() },
                    new CompanyBlock { Kind = CompanyBlockKind.Choice, Text = w.T("reallife_hier_region_pick", "Region"), Options = new[] { w.F("reallife_hier_region_own", "{district}", "district", speedy.District), w.F("reallife_hier_region_custom", "Region {letter}", "letter", "A"), w.F("reallife_hier_region_custom", "Region {letter}", "letter", "B") }, Chosen = 0,
                        Acts = new[] { CA(CompanyDo.StoreArea, place, 0), CA(CompanyDo.StoreArea, place, 1), CA(CompanyDo.StoreArea, place, 2) } },
                    CNote(w.T("reallife_hier_dash_what", "A store dashboard shows what this manager holds and lets you act for them."))
                };
                page.Card = c;
                break;
            }
            case "regiondash":
            {
                CPerson meera = Cw.Managers().First(x => x.Post == 2);
                string district = Cw.Region;
                List<CPlace> places = Cw.Places.Where(x => x.District == district).ToList();
                List<CAsk> here = Cw.Asks.Where(a => places.Any(x => x.Key == a.Place)).OrderByDescending(a => a.Day).ToList();
                int managed = places.Count(x => x.ManagerId != null), gaps = places.Count(x => Cw.StaffAt(x.Key).Count < x.Keep), profit = places.Sum(x => Cw.Result(x));
                page.List = new CompanyList
                {
                    Title = w.T("reallife_hier_dash_pending", "Questions here"), Count = here.Count.ToString(), Chosen = -1,
                    Rows = here.Select(a => new CompanyRow
                    {
                        Id = "he:" + a.Id, Title = AskTitle(w, a), FaceName = Cw.P(a.Asker).Name, Face = true, Hue = Cw.P(a.Asker).Hue, Ring = a.Open ? UiRole.Warn : UiRole.Ok, Sub = Cw.Pl(a.Place).Name, Figure = Dy(w, a.Day),
                        Chip = a.Open ? w.T("reallife_hier_state_open", "Open") : w.T("reallife_hier_log_settled", "Settled"), ChipTone = a.Open ? UiRole.Warn : UiRole.Muted
                    }).ToArray()
                };
                var c = new CompanyCard
                {
                    Kicker = w.F("reallife_mgr_row_region", "Region {district}", "district", district), Sub = meera.Name, Title = w.T("reallife_hier_dash_title", "Store dashboard"), Icon = UiIcon.ChartPie,
                    Back = CBack(w, "reg:" + district, CompanyView.Managers),
                    FactLabels = new[] { w.T("reallife_co_f_places", "Places"), w.T("reallife_co_f_gaps", "Gaps"), w.T("reallife_co_f_standing", "Standing"), w.T("reallife_co_f_pending", "Pending") },
                    FactValues = new[] { managed + "/" + places.Count, gaps.ToString(), Cw.StandingIn(district).ToString(), here.Count(a => a.Open).ToString() }
                };
                c.Blocks = new[]
                {
                    new CompanyBlock { Kind = CompanyBlockKind.Figures, Options = new[] { w.T("reallife_hier_fig_score", "Score"), w.T("reallife_hier_fig_profit", "Profit"), w.T("reallife_pol_field_profit_target", "Profit target") },
                        Values = new[] { "77", CoSigned(profit), "$300" } },
                    CSec(w.T("reallife_hier_sec_quick", "Do now")),
                    CBtns(CB(w.T("reallife_hier_quick_fill", "Fill the gaps"), UiButtonKind.Primary, UiAction.Alt, CA(CompanyDo.RegionFill, district), UiIcon.People),
                        CB(w.T("reallife_hier_quick_raise", "Raise the underpaid"), UiButtonKind.Secondary, UiAction.Option, CA(CompanyDo.RegionRaise, district), UiIcon.Coin),
                        CB(w.T("reallife_hier_quick_move", "Move crew"), UiButtonKind.Secondary, UiAction.Count, CA(CompanyDo.RegionMove, district), UiIcon.Next)),
                    CBtns(CB(w.T("reallife_hier_quick_appoint", "Appoint managers"), UiButtonKind.Secondary, UiAction.Count, CA(CompanyDo.RegionAppoint, district), UiIcon.People),
                        CB(w.T("reallife_hier_open_policy", "Open the policy"), UiButtonKind.Secondary, UiAction.Count, CA(CompanyDo.Go, "reg:" + district, CompanyView.Managers, CompanyView.SubPolicy), UiIcon.Sliders)),
                    CSec(w.T("reallife_mgr_region_their_places", "Their places"), places.Count.ToString()),
                    new CompanyBlock { Kind = CompanyBlockKind.Items, Items = places.Select(x => PlaceRow(w, x)).ToArray() },
                    CSec(w.T("reallife_hier_fig_budget", "Budget"), places.Count(x => x.ManagerId != null).ToString()),
                    new CompanyBlock { Kind = CompanyBlockKind.Items, Items = BudgetItems(w, places.Where(x => x.ManagerId != null).Select(x => Cw.ManagerOf(x)).ToList()) },
                    CNote(w.T("reallife_hier_region_dash_what", "A region dashboard shows what its manager holds and lets you act for them."))
                };
                page.Card = c;
                break;
            }
            default:   // seat
            {
                bool pick = which == "seat_pick", filled = which == "seat_filled";
                CSeat cfo = Cw.Seat(2), coo = Cw.Seat(1);   // the seat nobody holds: the people chief's
                // What the seat would watch, place by place from the place managers' own figures: the operating chief how many decisions each made, the people chief how the crew stands.
                List<CPerson> watched = Cw.Managers().Where(x => x.Post == 1).ToList();
                var watch = new CompanyItem[watched.Count];
                for (int i = 0; i < watch.Length; i++)
                {
                    CPerson mg = watched[i];
                    int crewStanding = Cw.CrewStanding(mg.Place);
                    watch[i] = new CompanyItem { Grade = filled ? -1 : crewStanding, Title = mg.Name, Sub = Cw.Pl(mg.Place).Name, Face = true, Hue = mg.Hue, Ring = UiRole.Ok,
                        Figure = filled ? w.F("reallife_co_f_decisions", "{count} decisions", "count", mg.Decided.ToString()) : crewStanding.ToString(), Note = filled ? null : w.T("reallife_co_crew_standing", "crew standing") };
                }
                int openInArea = filled ? Cw.Asks.Count(a => a.Open) : Cw.Asks.Count(a => a.Open && a.Kind == "raise");
                CompanyBlock[] watching =
                {
                    CSec(w.T("reallife_hier_sec_watch", "What they would watch"), watch.Length.ToString()), new CompanyBlock { Kind = CompanyBlockKind.Items, Items = watch },
                    CSec(w.T("reallife_hier_sec_area", "Area")), CRow(w.T("reallife_hier_area_open", "Open in their area"), openInArea.ToString()), CRow(w.T("reallife_co_f_managers", "Managers"), Cw.Managers().Count.ToString())
                };
                CPro pro1 = Cw.Pro(filled ? "pro3" : "pro1"), pro2 = Cw.Pro(filled ? "pro4" : "pro2");
                page.List = new CompanyList
                {
                    Title = w.T("reallife_co_candidates", "Who could do it"), Count = "2", Chosen = pick ? 0 : -1,
                    Rows = new[]
                    {
                        new CompanyRow { Id = "xc:pro1", Title = pro1.Name, Sub = RoleOf(w, pro1), Face = true, Hue = pro1.Hue100 / 100f, Ring = UiRole.None, Figure = CompanyWorld.Wage(pro1.Going), Note = "Pioneer HQ" },
                        new CompanyRow { Id = "xc:pro2", Title = pro2.Name, Sub = RoleOf(w, pro2), Face = true, Hue = pro2.Hue100 / 100f, Ring = UiRole.None, Figure = CompanyWorld.Wage(pro2.Going), Note = "Pioneer HQ" }
                    }
                };
                var c = new CompanyCard { Kicker = w.T("reallife_hier_seats", "Executive seats"), Title = SeatName(w, cfo), Icon = UiIcon.IdBadge, Back = CBack(w, "seat:2", CompanyView.Managers), Chips = new[] { w.T("reallife_mgr_empty_post", "Empty post") }, ChipTones = new[] { UiRole.Muted } };
                c.Blocks = new[]
                {
                    CNote(w.T("reallife_hier_seat_what_chro", "The people chief answers the pay questions that wait inside the band and raises staff who stand low or are leaving.")),
                    CNote(w.F("reallife_hier_seat_hire", "Choose one of your professionals: {roles}.", "roles", w.T("reallife_talent_role_hr_manager", "HR manager") + ", " + w.T("reallife_talent_role_recruiter", "Recruiter"))),
                    CNote(w.T("reallife_hier_exec_pick", "Pick a professional at the left.")),
                    watching[0], watching[1], watching[2], watching[3], watching[4]
                };
                if (pick)
                {
                    c = new CompanyCard
                    {
                        Kicker = SeatName(w, cfo), Title = pro1.Name, Sub = RoleOf(w, pro1) + ", Pioneer HQ", Face = true, Hue = pro1.Hue100 / 100f, Ring = UiRole.None, Back = CBack(w, "seat:2", CompanyView.Managers)
                    };
                    c.Blocks = new[]
                    {
                        CRow(w.T("reallife_hier_going", "Going rate"), CompanyWorld.Wage(pro1.Going)),
                        CNote(w.T("reallife_hier_seat_what_chro", "The people chief answers the pay questions that wait inside the band and raises staff who stand low or are leaving.")),
                        CBtns(CB(w.T("reallife_mgr_appoint", "Appoint"), UiButtonKind.Primary, UiAction.Alt, CA(CompanyDo.SeatAppoint, "pro1", 2), UiIcon.Check)),
                        watching[0], watching[1], watching[2], watching[3], watching[4]
                    };
                }
                else if (filled)
                {
                    c = new CompanyCard
                    {
                        Kicker = w.T("reallife_hier_seats", "Executive seats"), Title = coo.Name, Sub = SeatName(w, coo), Face = true, Hue = coo.Hue100 / 100f, Ring = UiRole.Ok, Back = CBack(w, "exe:1", CompanyView.Managers),
                        Chips = new[] { w.T("reallife_mgr_in_post", "In post") }, ChipTones = new[] { UiRole.Ok }
                    };
                    int open = Cw.Asks.Count(a => a.Open);
                    c.Blocks = new[]
                    {
                        CNote(w.T("reallife_hier_seat_what_coo", "The operating chief answers what the managers pass up and watches their targets.")),
                        CRow(w.T("reallife_mgr_since", "Since"), Dy(w, coo.Since)), CRow(w.T("reallife_hier_col_role", "Role"), w.T("reallife_talent_role_" + coo.Role, RoleKey(coo.Role)) + ", " + w.T("reallife_talent_level_" + coo.Level, coo.Level)),
                        CRow(w.T("reallife_hier_going", "Going rate"), CompanyWorld.Wage(coo.Going)),
                        CRow(w.T("reallife_mgr_allowance", "Allowance"), w.F("reallife_co_row_a_day", "{amount} a day", "amount", CM(coo.Allowance / 100))), CRow(w.T("reallife_hier_paid_month", "Paid this month"), CM(coo.Allowance / 100 * 30)),
                        CRow(w.T("reallife_hier_area_open", "Open in their area"), open.ToString()), CRow(w.T("reallife_hier_area_settled", "Settled this week"), "7"), CRow(w.T("reallife_mgr_decided", "Decisions"), "41"), CRow(w.T("reallife_hier_last_month", "Last month"), "38"),
                        CBtns(CB(w.T("reallife_mgr_stand_down", "Stand down"), UiButtonKind.Secondary, UiAction.Count, CA(CompanyDo.SeatStandDown, null, 1), UiIcon.Close, false, null, true)),
                        watching[0], watching[1], watching[2], watching[3], watching[4]
                    };
                }
                page.Card = c;
                break;
            }
        }
        m.Sub = page;
    }
}
