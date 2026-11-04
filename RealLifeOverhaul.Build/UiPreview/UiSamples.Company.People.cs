using System.Collections.Generic;
using RealLifeOverhaul.Ui;

// The Company app's people tabs: Staff and Managers with their sub pages. Samples' own names; the live source reads the player's people.
internal static partial class UiSamples
{
    private static CompanyBlock CSec(string t, string right = null) => new CompanyBlock { Kind = CompanyBlockKind.Section, Text = t, Value = right };

    private static CompanyBlock CRow(string label, string value, UiRole tone = UiRole.None) => new CompanyBlock { Kind = CompanyBlockKind.Row, Text = label, Value = value, Tone = tone };

    private static CompanyBlock CNote(string text, UiRole tone = UiRole.None) => new CompanyBlock { Kind = CompanyBlockKind.Note, Text = text, Tone = tone };

    private static CompanyBlock CBtns(params CompanyButton[] b) => new CompanyBlock { Kind = CompanyBlockKind.Buttons, Buttons = b };

    private static CompanyButton[] CBack(CompanyWords w, string pick, int tab, int sub = 0) => new[] { CB(w.T("reallife_co_back", "Back"), UiButtonKind.Secondary, UiAction.Count, CA(CompanyDo.Go, pick, tab, sub), UiIcon.Back) };

    // ------------------------------------------------------------------------------------------------- Staff

    private static void CompanyStaff(CompanyFacts f, CompanyWords w, string state)
    {
        CompanyPage p = f.Staff;
        bool one = state == "staff_one";
        var rows = new List<CompanyRow>();
        if (!one)
        {
            rows.Add(new CompanyRow { Id = "strike:Speedy Bites~7", Title = w.T("reallife_co_row_strike", "Strike called"), Sub = "Speedy Bites", Icon = UiIcon.Megaphone, Figure = w.F("reallife_co_row_strike_from", "from day {day}", "day", "216"), Chip = w.T("reallife_co_row_answer", "Answer"), ChipTone = UiRole.Danger });
            rows.Add(new CompanyRow { Id = "ot:Hudson Warehouse~4", Title = w.T("reallife_co_row_overtime", "Overtime offered"), Sub = "Hudson Warehouse", Icon = UiIcon.Clock, Figure = w.F("reallife_co_row_a_day", "{amount} a day", "amount", "$96"), Chip = w.T("reallife_co_row_answer", "Answer"), ChipTone = UiRole.Warn });
            rows.Add(new CompanyRow { Id = "emp:dev", Title = "Dev Kapoor", Sub = "Speedy Bites", Face = true, Hue = 0.35f, Ring = UiRole.Warn, Figure = "$17.00 / h", Note = w.F("reallife_co_going", "going {wage}", "wage", "$19.00"), Chip = w.T("reallife_co_row_asks", "Asks for a raise"), ChipTone = UiRole.Warn });
            rows.Add(new CompanyRow { Id = "emp:lena", Title = "Lena Adler", Sub = "Speedy Bites", Face = true, Hue = 0.1f, Ring = UiRole.Warn, Figure = "$16.50 / h", Note = w.F("reallife_co_going", "going {wage}", "wage", "$16.50"), Chip = w.T("reallife_co_row_offer", "Rival's offer"), ChipTone = UiRole.Danger });
            rows.Add(new CompanyRow { Id = "emp:maya", Title = "Maya Okafor", Sub = "Bloom & Co", Face = true, Hue = 0.6f, Ring = UiRole.Ok, Figure = "$18.20 / h", Note = w.F("reallife_co_going", "going {wage}", "wage", "$18.20") });
            rows.Add(new CompanyRow { Id = "emp:tomas", Title = "Tomas Reyes", Sub = "Hudson Warehouse", Face = true, Hue = 0.8f, Ring = UiRole.Ok, Figure = "$15.40 / h", Note = w.F("reallife_co_going", "going {wage}", "wage", "$15.40") });
            rows.Add(new CompanyRow { Id = "emp:sana", Title = "Sana Malik", Sub = "Pioneer Game Studio", Face = true, Hue = 0.25f, Ring = UiRole.Danger, Figure = "$24.00 / h", Note = w.F("reallife_co_going", "going {wage}", "wage", "$26.00") });
        }
        else
        {
            rows.Add(new CompanyRow { Id = "emp:ana", Title = "Ana Costa", Sub = "Corner Cafe", Face = true, Hue = 0.45f, Ring = UiRole.Ok, Figure = "$13.00 / h", Note = w.F("reallife_co_going", "going {wage}", "wage", "$13.00") });
            rows.Add(new CompanyRow { Id = "emp:jin", Title = "Jin Park", Sub = "Corner Cafe", Face = true, Hue = 0.7f, Ring = UiRole.Ok, Figure = "$13.00 / h", Note = w.F("reallife_co_going", "going {wage}", "wage", "$13.00") });
        }
        p.List = new CompanyList { Title = w.T("reallife_co_staff_list", "Staff"), Count = one ? "2" : "31", Rows = rows.ToArray(), Empty = w.T("reallife_co_row_no_staff", "Nobody works for you yet.") };
        string pick = state == "staff_strike" ? "strike:Speedy Bites~7" : state == "staff_overtime" ? "ot:Hudson Warehouse~4" : state == "staff_offer" ? "emp:lena" : state == "staff_give" ? "emp:sana" : state == "staff_summary" || one ? null : "emp:dev";
        for (int i = 0; i < rows.Count; i++)
        {
            p.List.Chosen = rows[i].Id == pick ? i : p.List.Chosen;
        }
        string back = "";
        if (pick == null)
        {
            p.List.Chosen = -1;
            p.Top = StaffSummary(w, one);
        }
        else if (pick == "strike:Speedy Bites~7")
        {
            p.Card = StrikeCardSample(w, back);
        }
        else if (pick == "ot:Hudson Warehouse~4")
        {
            p.Card = OvertimeCardSample(w, back);
        }
        else
        {
            p.Card = pick == "emp:sana" ? GiveCardSample(w) : EmployeeCardSample(w, pick == "emp:lena");
        }
    }

    private static CompanyCard StaffSummary(CompanyWords w, bool one)
    {
        var c = new CompanyCard { Kicker = w.T("reallife_co_tab_staff", "Staff"), Title = w.T("reallife_co_staff_title", "Your staff"), Icon = UiIcon.People };
        c.FactLabels = new[] { w.T("reallife_co_f_staff", "Staff"), w.T("reallife_co_f_underpaid", "Underpaid"), w.T("reallife_co_f_low", "Standing low") };
        c.FactValues = one ? new[] { "2", "0", "0" } : new[] { "31", "4", "2" };
        var blocks = new List<CompanyBlock>();
        if (!one)
        {
            blocks.Add(CSec(w.T("reallife_co_staff_asks_sec", "Asking for a raise")));
            blocks.Add(CNote(w.F("reallife_co_staff_asks", "{count} people ask for a raise, {amount} an hour between them.", "count", "3", "amount", "$5.50"), UiRole.WarnText));
            blocks.Add(CBtns(CB(w.T("reallife_co_staff_agree_all", "Agree to all"), UiButtonKind.Primary, UiAction.Alt, CA(CompanyDo.RaisesAll, null, 0), UiIcon.Check),
                CB(w.T("reallife_co_staff_refuse_all", "Refuse all"), UiButtonKind.Secondary, UiAction.Count, CA(CompanyDo.RaisesAll, null, 1), UiIcon.Close, false, null, true)));
        }
        blocks.Add(CSec(w.T("reallife_co_staff_sick", "Sick pay")));
        blocks.Add(new CompanyBlock
        {
            Kind = CompanyBlockKind.Choice, Text = w.T("reallife_co_staff_sick", "Sick pay"), Options = new[] { w.T("reallife_co_sick_none", "None"), w.T("reallife_co_sick_statutory", "Statutory"), w.T("reallife_co_sick_generous", "Generous") },
            Chosen = 1, Acts = new[] { CA(CompanyDo.SickPolicy, null, 0), CA(CompanyDo.SickPolicy, null, 1), CA(CompanyDo.SickPolicy, null, 2) }
        });
        blocks.Add(CNote(w.F("reallife_co_staff_sick_what", "Somebody off sick is paid {share} of a day, and their standing moves {standing} a day.", "share", "50%", "standing", "0")));
        blocks.Add(CNote(w.T("reallife_co_staff_market", "A rival may offer your people more when they are paid under the going rate.")));
        blocks.Add(CNote(w.T("reallife_co_staff_picked", "Pick a person at the left to see what they earn, what they ask and how they feel.")));
        c.Blocks = blocks.ToArray();
        return c;
    }

    private static CompanyCard EmployeeCardSample(CompanyWords w, bool offer)
    {
        string name = offer ? "Lena Adler" : "Dev Kapoor";
        var c = new CompanyCard
        {
            Kicker = w.T("reallife_co_tab_staff", "Staff"), Title = name, Sub = w.F("reallife_co_emp_works", "{business}, {skill}", "business", "Speedy Bites", "skill", offer ? "Cooking" : "Customer service"), Face = true, Hue = offer ? 0.1f : 0.35f,
            Ring = UiRole.Warn, Chips = new[] { offer ? w.T("reallife_co_emp_offer", "A rival offers more") : w.T("reallife_co_emp_asks", "Asks for a raise") }, ChipTones = new[] { offer ? UiRole.Danger : UiRole.Warn },
            FactLabels = new[] { w.T("reallife_co_f_paid", "Paid"), w.T("reallife_co_f_going", "Going rate"), w.T("reallife_co_f_standing", "Standing"), w.T("reallife_co_f_mood", "Mood") },
            FactValues = offer ? new[] { "$16.50", "$16.50", "64", "58" } : new[] { "$17.00", "$19.00", "52", "47" }, Back = CBack(w, "", CompanyView.Staff)
        };
        var b = new List<CompanyBlock>();
        if (offer)
        {
            b.Add(CSec(w.T("reallife_co_emp_offer", "A rival offers more")));
            b.Add(CNote(w.F("reallife_co_emp_offer_what", "{rival} offers {wage} an hour. You can answer until day {day}.", "rival", "Harbour Grill", "wage", "$18.50", "day", "219")));
            b.Add(CNote(w.F("reallife_co_emp_offer_choices", "Matching adds {raise} an hour. Letting go means {days} days' notice.", "raise", "$2.00", "days", "3")));
            b.Add(CBtns(CB(w.T("reallife_co_emp_match", "Match the offer"), UiButtonKind.Primary, UiAction.Alt, CA(CompanyDo.OfferAnswer, "lena", 1), UiIcon.Check),
                CB(w.T("reallife_co_emp_let_go", "Let them go"), UiButtonKind.Secondary, UiAction.Count, CA(CompanyDo.OfferAnswer, "lena", 0), UiIcon.Close, false, null, true)));
        }
        else
        {
            b.Add(CSec(w.T("reallife_co_emp_asks", "Asks for a raise")));
            b.Add(CNote(w.F("reallife_co_emp_ask", "{name} asks for {wage} an hour. Answer by day {day}.", "name", "Dev", "wage", "$19.00", "day", "218")));
            b.Add(CNote(w.F("reallife_co_emp_ask_choices", "Agreeing pays {all}; offering less pays {less}.", "all", "$19.00", "less", "$18.00")));
            b.Add(CBtns(CB(w.T("reallife_co_emp_agree", "Agree"), UiButtonKind.Primary, UiAction.Alt, CA(CompanyDo.RaiseAnswer, "dev", 0), UiIcon.Check),
                CB(w.T("reallife_co_emp_less", "Offer less"), UiButtonKind.Secondary, UiAction.Option, CA(CompanyDo.RaiseAnswer, "dev", 1)),
                CB(w.F("reallife_co_emp_bonus", "Bonus {amount}", "amount", "$300"), UiButtonKind.Secondary, UiAction.Count, CA(CompanyDo.RaiseAnswer, "dev", 3)),
                CB(w.T("reallife_co_emp_refuse", "Refuse"), UiButtonKind.Secondary, UiAction.Count, CA(CompanyDo.RaiseAnswer, "dev", 2), UiIcon.Close, false, null, true)));
        }
        b.Add(CSec(w.T("reallife_ibox_sec_pay", "Pay")));
        b.Add(CRow(w.T("reallife_co_f_paid", "Paid"), offer ? "$16.50" : "$17.00"));
        b.Add(CRow(w.T("reallife_co_f_going", "Going rate"), offer ? "$16.50" : "$19.00", offer ? UiRole.None : UiRole.WarnText));
        b.Add(CSec(w.T("reallife_ibox_sec_feeling", "How they feel")));
        b.Add(new CompanyBlock { Kind = CompanyBlockKind.Meter, Text = w.T("reallife_co_f_standing", "Standing"), Value = offer ? "64" : "52", Number = offer ? 64 : 52, Tone = UiRole.Warn });
        b.Add(new CompanyBlock { Kind = CompanyBlockKind.Meter, Text = w.T("reallife_co_f_mood", "Mood"), Value = offer ? "58" : "47", Number = offer ? 58 : 47, Tone = UiRole.Warn });
        c.Blocks = b.ToArray();
        return c;
    }

    // Somebody paid under the going rate who has not asked: the owner may give the raise unasked.
    private static CompanyCard GiveCardSample(CompanyWords w)
    {
        var c = new CompanyCard
        {
            Kicker = w.T("reallife_co_tab_staff", "Staff"), Title = "Sana Malik", Sub = w.F("reallife_co_emp_works", "{business}, {skill}", "business", "Pioneer Game Studio", "skill", "Programming"), Face = true, Hue = 0.25f, Ring = UiRole.Danger,
            FactLabels = new[] { w.T("reallife_co_f_paid", "Paid"), w.T("reallife_co_f_going", "Going rate"), w.T("reallife_co_f_standing", "Standing"), w.T("reallife_co_f_mood", "Mood") },
            FactValues = new[] { "$24.00", "$26.00", "31", "44" }, Back = CBack(w, "", CompanyView.Staff)
        };
        c.Blocks = new[]
        {
            CNote(w.F("reallife_co_emp_give_what", "Paid {raise} an hour under the going rate. A raise now keeps them.", "raise", "$2.00")),
            CBtns(CB(w.T("reallife_co_emp_give", "Give the raise"), UiButtonKind.Primary, UiAction.Alt, CA(CompanyDo.RaiseGive, "sana"), UiIcon.Check)),
            CSec(w.T("reallife_ibox_sec_feeling", "How they feel")),
            new CompanyBlock { Kind = CompanyBlockKind.Meter, Text = w.T("reallife_co_f_standing", "Standing"), Value = "31", Number = 31, Tone = UiRole.Danger },
            new CompanyBlock { Kind = CompanyBlockKind.Meter, Text = w.T("reallife_co_f_mood", "Mood"), Value = "44", Number = 44, Tone = UiRole.Warn }
        };
        return c;
    }

    private static CompanyCard StrikeCardSample(CompanyWords w, string back)
    {
        var c = new CompanyCard
        {
            Kicker = w.T("reallife_co_tab_staff", "Staff"), Title = w.F("reallife_co_strike_title", "Strike at {business}", "business", "Speedy Bites"), Icon = UiIcon.Megaphone,
            Chips = new[] { w.T("reallife_ibox_chip_called", "Called") }, ChipTones = new[] { UiRole.Danger }, Back = CBack(w, "", CompanyView.Staff)
        };
        c.Blocks = new[]
        {
            CNote(w.F("reallife_co_strike_called", "A strike is called from day {from} to day {end}.", "from", "216", "end", "218"), UiRole.DangerText),
            CNote(w.F("reallife_co_strike_effect", "Footfall falls {percent}% and the shop is shut {hours} hours a day.", "percent", "30", "hours", "6")),
            CNote(w.F("reallife_co_strike_why", "{low} of {count} people stand low.", "low", "4", "count", "9")),
            CSec(w.T("reallife_co_strike_ways", "Ways to settle it")),
            CRow(w.T("reallife_co_strike_do_raise", "Raise everyone to the going rate"), "$3.40"),
            CRow(w.T("reallife_co_strike_do_payment", "A one-off payment to each"), "$540"),
            CNote(w.F("reallife_co_strike_sit", "Or sit it out until day {day}.", "day", "218")),
            CBtns(CB(w.T("reallife_co_strike_do_raise", "Raise everyone to the going rate"), UiButtonKind.Primary, UiAction.Alt, CA(CompanyDo.StrikeAnswer, "Speedy Bites~7", 0), UiIcon.Check),
                CB(w.T("reallife_co_strike_do_payment", "A one-off payment to each"), UiButtonKind.Secondary, UiAction.Count, CA(CompanyDo.StrikeAnswer, "Speedy Bites~7", 1)),
                CB(w.T("reallife_co_strike_do_sit", "Sit it out"), UiButtonKind.Secondary, UiAction.Count, CA(CompanyDo.Go, "", CompanyView.Staff), UiIcon.Close))
        };
        return c;
    }

    private static CompanyCard OvertimeCardSample(CompanyWords w, string back)
    {
        var c = new CompanyCard
        {
            Kicker = w.T("reallife_co_tab_staff", "Staff"), Title = w.F("reallife_co_overtime_title", "Overtime at {business}", "business", "Hudson Warehouse"), Icon = UiIcon.Clock, Back = CBack(w, "", CompanyView.Staff)
        };
        c.Blocks = new[]
        {
            CNote(w.F("reallife_co_overtime_by", "Answer by day {day}.", "day", "217"), UiRole.WarnText),
            CNote(w.F("reallife_co_overtime_what", "The crew offers {hours} extra hours a day for {days} days.", "hours", "2", "days", "5")),
            CSec(w.T("reallife_ibox_sec_terms", "Terms")),
            CRow(w.T("reallife_co_overtime_cost", "Cost"), w.F("reallife_co_row_a_day", "{amount} a day", "amount", "$96")),
            CRow(w.T("reallife_co_overtime_gain", "Footfall served"), "+12%"),
            CBtns(CB(w.T("reallife_co_overtime_agree", "Agree"), UiButtonKind.Primary, UiAction.Alt, CA(CompanyDo.OvertimeAnswer, "Hudson Warehouse~4", 1), UiIcon.Check),
                CB(w.T("reallife_co_overtime_refuse", "Refuse"), UiButtonKind.Secondary, UiAction.Count, CA(CompanyDo.OvertimeAnswer, "Hudson Warehouse~4", 0), UiIcon.Close, false, null, true))
        };
        return c;
    }

    // ------------------------------------------------------------------------------------------------- Managers

    private static UiOrgNode ONode(UiOrgKind kind, int level, string name, string sub = null, string right = null, bool kids = false, float hue = 0f, UiRole ring = UiRole.Ok) =>
        new UiOrgNode { Kind = kind, Level = level, Name = name, Sub = sub, Right = right, HasKids = kids, Hue = hue, Ring = ring };

    private static void CompanyManagers(CompanyFacts f, CompanyWords w, string state)
    {
        CompanyManagersFacts m = f.Managers;
        f.Deep = state != "managers_flat";
        m.Deep = f.Deep;
        if (state == "managers_off")
        {
            m.PartOff = true;
            m.OffTitle = w.T("reallife_co_tab_managers", "Managers");
            m.OffBody = w.T("reallife_mgr_off", "Managers are switched off in the options, or they stopped after an error. Nothing is decided by them and everything comes to you.");
            return;
        }
        var nodes = new List<UiOrgNode>();
        var ids = new List<string>();
        void Add(string id, UiOrgNode n)
        {
            ids.Add(id);
            nodes.Add(n);
        }
        Add("cap:owner", ONode(UiOrgKind.Caption, 0, w.T("reallife_co_tree_owner", "Owner")));
        Add("own", ONode(UiOrgKind.Person, 0, w.T("reallife_hier_you", "You"), w.F("reallife_co_tree_owner_sub", "Owner, all {count} businesses", "count", "4"), null, false, 0.05f));
        if (f.Deep)
        {
            Add("cap:exec", ONode(UiOrgKind.Caption, 0, w.T("reallife_co_tree_executive", "Executive")));
            Add("exe:1", ONode(UiOrgKind.Person, 1, "Ethan Brooks", "Chief operating officer", w.F("reallife_co_tree_managers", "{count} managers", "count", "3"), true, 0.15f));
            Add("mgr:Speedy Bites~7", ONode(UiOrgKind.Person, 2, "Ray Walsh", "Speedy Bites", w.F("reallife_co_tree_staff", "{count} staff", "count", "9"), true, 0.55f));
            Add("crew:dev", ONode(UiOrgKind.Person, 3, "Dev Kapoor", null, "Customer service", false, 0.35f, UiRole.None));
            Add("crew:lena", ONode(UiOrgKind.Person, 3, "Lena Adler", null, "Cooking", false, 0.1f, UiRole.None));
            Add("more:Speedy Bites~7", ONode(UiOrgKind.More, 3, w.F("reallife_co_and_more", "and {count} more", "count", "6")));
            Add("mgr:Bloom & Co~214", ONode(UiOrgKind.Person, 2, "Tessa Brandt", "Bloom & Co", w.F("reallife_co_tree_staff", "{count} staff", "count", "5"), true, 0.9f));
            Add("mgr:Hudson Warehouse~4", ONode(UiOrgKind.Person, 2, "Omar Haddad", "Hudson Warehouse", w.F("reallife_co_tree_staff", "{count} staff", "count", "4"), true, 0.75f, UiRole.Warn));
            Add("reg:Midtown", ONode(UiOrgKind.Person, 2, "Meera Iyer", w.F("reallife_co_tree_region", "Regional, {district}", "district", "Midtown"), null, false, 0.75f));
            Add("exe:2", ONode(UiOrgKind.Person, 1, "Priya Nair", "Chief human resources officer", null, false, 0.4f));
            Add("seat:3", ONode(UiOrgKind.Post, 1, "Chief financial officer", w.T("reallife_mgr_empty_post", "Empty post"), w.T("reallife_co_tree_hire", "Appoint")));
        }
        else
        {
            Add("mgr:Speedy Bites~7", ONode(UiOrgKind.Person, 1, "Ray Walsh", "Speedy Bites", w.F("reallife_co_tree_staff", "{count} staff", "count", "9"), true, 0.55f));
            Add("mgr:Bloom & Co~214", ONode(UiOrgKind.Person, 1, "Tessa Brandt", "Bloom & Co", w.F("reallife_co_tree_staff", "{count} staff", "count", "5"), true, 0.9f));
        }
        Add("cap:posts", ONode(UiOrgKind.Caption, 0, w.T("reallife_co_tree_open_posts", "Open posts")));
        Add("post:Pioneer Game Studio~9", ONode(UiOrgKind.Post, 1, w.F("reallife_co_tree_post", "Manager of {place}", "place", "Pioneer Game Studio"), "Office, Midtown", w.T("reallife_co_tree_hire", "Appoint")));
        m.Tree = nodes.ToArray();
        m.TreeIds = ids.ToArray();
        m.TreeTitle = w.T("reallife_co_who_decides", "Who decides what");
        m.TreeFoot = f.Deep ? w.T("reallife_co_open_policy", "Open the policy centre") : null;
        m.TreeFootAct = CA(CompanyDo.Go, null, CompanyView.Managers, CompanyView.SubPolicy);
        m.Empty = w.T("reallife_co_mgr_none_body", "Pick a person in the tree to see what they decide, what they may spend and how they are doing.");
        string tail = state.Substring(state.IndexOf('_') + 1);
        switch (tail)
        {
            case "owner":
                m.Chosen = "own";
                m.Profile = OwnerProfileSample(w);
                break;
            case "region":
                m.Chosen = "reg:Midtown";
                m.Profile = RegionProfileSample(w);
                break;
            case "post":
                m.Chosen = "post:Pioneer Game Studio~9";
                m.Profile = PostProfileSample(w);
                break;
            case "none":
                m.Chosen = "";
                m.Profile = null;
                break;
            default:
                m.Chosen = "mgr:Speedy Bites~7";
                m.Profile = ManagerProfileSample(w);
                break;
        }
        if (tail.StartsWith("sub_", System.StringComparison.Ordinal))
        {
            ManagerSubSample(f, m, w, tail.Substring(4));
        }
    }

    private static CompanyProfile ManagerProfileSample(CompanyWords w)
    {
        string place = "Speedy Bites~7";
        var p = new CompanyProfile
        {
            Id = "mgr:" + place, Name = "Ray Walsh", Role = w.F("reallife_co_role_manager", "Manager, {place}", "place", "Speedy Bites"), Hue = 0.55f, Ring = UiRole.Ok, Status = w.T("reallife_mgr_in_post", "In post"), StatusTone = UiRole.Ok,
            FactLabels = new[] { w.T("reallife_co_f_manager_since", "Manager since"), w.T("reallife_co_f_wage", "Wage"), w.T("reallife_co_f_this_month", "This month"), w.T("reallife_co_f_asked", "Asked you") },
            FactValues = new[] { "Day 41", "$23.50 / h", w.F("reallife_co_f_decisions", "{count} decisions", "count", "86"), "1" },
            WaitingText = w.T("reallife_co_one_waiting", "1 question waiting for you"), WaitingLine = w.F("reallife_hier_say_exception", "{name} asks for {asked} an hour; the limit is {ceiling}.", "name", "Dev Kapoor", "asked", "$19.00", "ceiling", "$18.00"),
            Waiting = CB(w.T("reallife_co_answer", "Answer"), UiButtonKind.Primary, UiAction.Alt, CA(CompanyDo.Go, "card", 1), UiIcon.Next),
            TasksTitle = w.F("reallife_co_decisions_owns", "Decisions {name} owns", "name", "Ray"), TasksHint = w.F("reallife_co_switch_hint", "Switch on = {name} decides", "name", "Ray")
        };
        string[] titles = { "Raises", "Rival offers", "Overtime", "Insurance claims", "Stock", "Prices", "Rota", "Hiring", "Marketing", "Leases" };
        string[] lines = { "Up to 110% of the going rate", "Up to 110% of the going rate", "A $400 budget a month", "Files within the window", "Keeps 3 days of stock", "Between 90% and 115%", "Written Sundays at 18:00", "Keeps the place at 9", "A $500 limit a month", "Up to a 5% rise" };
        bool[] on = { true, true, true, false, true, true, true, true, false, true };
        UiIcon[] icons = { UiIcon.Coin, UiIcon.Handshake, UiIcon.Clock, UiIcon.ShieldCheck, UiIcon.Package, UiIcon.Tag, UiIcon.Calendar, UiIcon.People, UiIcon.Megaphone, UiIcon.Key };
        var tasks = new List<CompanyTask>();
        for (int t = 0; t < titles.Length; t++)
        {
            tasks.Add(new CompanyTask
            {
                Id = place + ":" + t, Icon = icons[t], Title = titles[t], Line = lines[t], On = on[t], Who = on[t] ? "Ray" : w.T("reallife_hier_you", "You"),
                Act = CA(CompanyDo.TaskSet, place, t, on[t] ? 0 : 1)
            });
        }
        p.Tasks = tasks.ToArray();
        p.LimitsTitle = w.T("reallife_co_limits_title", "Limits and where questions go");
        p.LimitsAll = CB(w.T("reallife_co_all_limits", "All limits"), UiButtonKind.Quiet, UiAction.Count, CA(CompanyDo.Go, "mgr:" + place, CompanyView.Managers, CompanyView.SubTask, 0));
        p.Limits = new[]
        {
            new CompanyLimit { Title = w.T("reallife_co_lim_raise", "Raise limit"), Sub = w.T("reallife_co_lim_raise_sub", "Of the going rate, above the pay now"), Value = "110%", CanLess = true, CanMore = true,
                Less = CA(CompanyDo.LimitSet, place, 0, 1), More = CA(CompanyDo.LimitSet, place, 0, 3), Typed = CA(CompanyDo.LimitSet, place, 0, 2) },
            new CompanyLimit { Title = w.T("reallife_co_lim_budget", "Spend limit"), Sub = w.T("reallife_co_lim_budget_sub", "A month, for overtime and hires"), Value = "$400", CanLess = true, CanMore = true,
                Less = CA(CompanyDo.LimitSet, place, 4, 1), More = CA(CompanyDo.LimitSet, place, 4, 3), Typed = CA(CompanyDo.LimitSet, place, 4, 2) }
        };
        p.RouteTitle = w.F("reallife_co_route_title", "Questions {name} cannot decide go to", "name", "Ray");
        p.RouteSub = w.F("reallife_co_route_sub", "{name} picks up what you leave for a day", "name", "Ethan Brooks");
        p.RouteOptions = new[] { w.T("reallife_hier_you", "You"), "Ethan Brooks" };
        p.RouteChosen = 1;
        p.RouteActs = new[] { CA(CompanyDo.TaskSet, place, -1, 0), CA(CompanyDo.TaskSet, place, -1, 1) };
        p.GradesTitle = w.F("reallife_co_how_doing", "How {name} is doing", "name", "Ray");
        p.GradeLabels = new[] { w.T("reallife_co_g_judgement", "Judgement"), w.T("reallife_co_g_speed", "Speed"), w.T("reallife_co_g_thrift", "Thrift"), w.T("reallife_co_g_morale", "Crew morale") };
        p.GradeValues = new[] { 92, 74, 61, 72 };
        p.LogTitle = w.T("reallife_co_log", "Log");
        p.LogAll = w.F("reallife_co_all_count", "All {count}", "count", "86");
        p.LogOpen = CA(CompanyDo.Go, "mgr:" + place, CompanyView.Managers, CompanyView.SubDecision);
        p.Log = new[]
        {
            new CompanyDone { When = "Day 214", Text = w.F("reallife_mgr_did_stock", "Reordered stock for {count} products, a {day}-day cover", "count", "6", "day", "3"), Money = "-" + CM(410), Tone = -1 },
            new CompanyDone { When = "Day 214", Text = w.F("reallife_mgr_did_raise_agree", "Agreed a raise of {raise} for {name}", "name", "Lena Adler", "raise", "$0.50"), Money = "-" + CM(390), Tone = -1 },
            new CompanyDone { When = "Day 213", Text = w.F("reallife_mgr_did_prices", "Changed {count} prices", "count", "4"), Money = "+" + CM(85), Tone = 1 },
            new CompanyDone { When = "Day 212", Text = w.F("reallife_mgr_did_rota", "Wrote the rota: {shifts} shifts, {covered} of {wanted} hours covered", "shifts", "24", "covered", "112", "wanted", "112"), Money = "no cost" }
        };
        p.CrewTitle = w.F("reallife_co_crew_title", "{name}'s crew", "name", "Ray");
        p.CrewCount = w.F("reallife_co_staff_count_n", "{count} staff", "count", "9");
        p.Crew = new[]
        {
            new CompanyItem { Title = "Dev Kapoor", Sub = "Customer service", Face = true, Hue = 0.35f, Ring = UiRole.Warn, Grade = 52, Figure = "52", Press = true, Act = CA(CompanyDo.Go, "emp:dev", CompanyView.Staff) },
            new CompanyItem { Title = "Lena Adler", Sub = "Cooking", Face = true, Hue = 0.1f, Ring = UiRole.Ok, Grade = 64, Figure = "64", Press = true, Act = CA(CompanyDo.Go, "emp:lena", CompanyView.Staff) },
            new CompanyItem { Title = "Maya Okafor", Sub = "Customer service", Face = true, Hue = 0.6f, Ring = UiRole.Ok, Grade = 78, Figure = "78", Press = true, Act = CA(CompanyDo.Go, "emp:maya", CompanyView.Staff) }
        };
        p.Buttons = new[]
        {
            CB(w.T("reallife_mgr_replace", "Replace or stand down"), UiButtonKind.Secondary, UiAction.Count, CA(CompanyDo.Go, "mgr:" + place, CompanyView.Managers, CompanyView.SubAppoint), UiIcon.People),
            CB(w.T("reallife_hier_open_dashboard", "Open the dashboard"), UiButtonKind.Secondary, UiAction.Count, CA(CompanyDo.Go, "mgr:" + place, CompanyView.Managers, CompanyView.SubDashboard), UiIcon.ChartPie)
        };
        return p;
    }

    private static CompanyProfile OwnerProfileSample(CompanyWords w)
    {
        var p = new CompanyProfile
        {
            Id = "own", Name = "Alex Moreno", Role = w.T("reallife_co_role_owner", "Owner"), Hue = 0.05f, Ring = UiRole.Ok,
            FactLabels = new[] { w.T("reallife_co_f_businesses", "Businesses"), w.T("reallife_co_f_managers", "Managers"), w.T("reallife_co_f_asked", "Asked you") }, FactValues = new[] { "4", "6", "2" }
        };
        p.More = new[]
        {
            CSec(w.T("reallife_co_owner_month", "This month")),
            CRow(w.T("reallife_co_owner_decided", "Decisions made for you"), "212"),
            CRow(w.T("reallife_co_owner_cost", "Managers' allowances, a month"), CM(4210)),
            CSec(w.T("reallife_co_owner_levels", "Levels")),
            new CompanyBlock { Kind = CompanyBlockKind.Switch, Text = w.T("reallife_co_deep_mode", "Deep Mode"), Sub = w.T("reallife_co_deep_sub", "The executives, regions, the policy centre and every question's inbox"), On = true, Value = w.T("reallife_co_on", "On"), Act = CA(CompanyDo.DeepMode, null, 0) },
            new CompanyBlock { Kind = CompanyBlockKind.Link, Text = w.T("reallife_co_inbox", "Questions inbox"), Value = w.F("reallife_co_inbox_open", "{count} open", "count", "5"), Act = CA(CompanyDo.Go, null, CompanyView.Managers, CompanyView.SubInbox) },
            new CompanyBlock { Kind = CompanyBlockKind.Link, Text = w.T("reallife_co_policy_centre", "Policy centre"), Value = "", Act = CA(CompanyDo.Go, null, CompanyView.Managers, CompanyView.SubPolicy) },
            new CompanyBlock { Kind = CompanyBlockKind.Link, Text = w.T("reallife_co_templates", "Templates"), Value = "4", Act = CA(CompanyDo.Go, null, CompanyView.Managers, CompanyView.SubTemplates) }
        };
        return p;
    }

    private static CompanyProfile RegionProfileSample(CompanyWords w)
    {
        var p = new CompanyProfile
        {
            Id = "reg:Midtown", Name = "Meera Iyer", Role = w.F("reallife_co_tree_region", "Regional, {district}", "district", "Midtown"), Hue = 0.75f, Ring = UiRole.Ok, Status = w.T("reallife_mgr_in_post", "In post"), StatusTone = UiRole.Ok,
            FactLabels = new[] { w.T("reallife_co_f_since", "Since"), w.T("reallife_co_f_places", "Places"), w.T("reallife_co_f_standing_in", "Standing in"), w.T("reallife_co_f_decisions_n", "Decisions") }, FactValues = new[] { "Day 88", "3", "68", "41" }
        };
        p.More = new[]
        {
            CSec(w.T("reallife_mgr_sec_manager", "Manager")),
            CRow(w.T("reallife_mgr_skill", "Skill"), "81 / 64"),
            CRow(w.T("reallife_mgr_allowance", "Allowance"), w.F("reallife_co_row_a_day", "{amount} a day", "amount", "$84")),
            CRow(w.T("reallife_mgr_pay", "Pay"), "$31.00 / h"),
            CRow(w.T("reallife_mgr_mistakes", "Mistakes this month"), "2"),
            CNote(w.T("reallife_mgr_region_what", "A regional manager looks over the places of a district and stands in for a manager who is off sick."))
        };
        p.Buttons = new[]
        {
            CB(w.T("reallife_mgr_replace", "Replace or stand down"), UiButtonKind.Secondary, UiAction.Count, CA(CompanyDo.Go, "reg:Midtown", CompanyView.Managers, CompanyView.SubAppoint), UiIcon.People),
            CB(w.T("reallife_hier_open_dashboard", "Open the dashboard"), UiButtonKind.Secondary, UiAction.Count, CA(CompanyDo.Go, "reg:Midtown", CompanyView.Managers, CompanyView.SubRegion), UiIcon.ChartPie)
        };
        return p;
    }

    private static CompanyProfile PostProfileSample(CompanyWords w)
    {
        var p = new CompanyProfile
        {
            Id = "post:Pioneer Game Studio~9", Name = w.F("reallife_co_tree_post", "Manager of {place}", "place", "Pioneer Game Studio"), Role = "Office, Midtown", Hue = 0.3f, Ring = UiRole.None,
            Status = w.T("reallife_mgr_empty_post", "Empty post"), StatusTone = UiRole.Muted
        };
        p.More = new[]
        {
            CNote(w.T("reallife_mgr_place_none", "Nobody manages this place yet. A manager decides what you hand over and asks you what is past their limit.")),
            CBtns(CB(w.T("reallife_mgr_appoint", "Appoint"), UiButtonKind.Primary, UiAction.Alt, CA(CompanyDo.Go, "post:Pioneer Game Studio~9", CompanyView.Managers, CompanyView.SubAppoint), UiIcon.People))
        };
        return p;
    }

    // The sub pages of Managers: a list and a card, each from the subject the tree had picked.
    private static void ManagerSubSample(CompanyFacts f, CompanyManagersFacts m, CompanyWords w, string which)
    {
        var page = new CompanyPage();
        string place = "Speedy Bites~7";
        switch (which)
        {
            case "appoint":
            {
                page.List = new CompanyList
                {
                    Title = w.T("reallife_co_candidates", "Who could do it"), Count = "4", Chosen = 1,
                    Rows = new[]
                    {
                        new CompanyRow { Id = "mc:ray", Title = "Ray Walsh", Sub = "Customer service", Face = true, Hue = 0.55f, Ring = UiRole.None, Figure = "$23.50", Note = w.F("reallife_co_skill_n", "skill {n}", "n", "78"), Chip = w.T("reallife_mgr_in_post", "In post"), ChipTone = UiRole.Ok },
                        new CompanyRow { Id = "mc:lena", Title = "Lena Adler", Sub = "Cooking", Face = true, Hue = 0.1f, Ring = UiRole.None, Figure = "$16.50", Note = w.F("reallife_co_skill_n", "skill {n}", "n", "64") },
                        new CompanyRow { Id = "mc:maya", Title = "Maya Okafor", Sub = "Customer service", Face = true, Hue = 0.6f, Ring = UiRole.None, Figure = "$18.20", Note = w.F("reallife_co_skill_n", "skill {n}", "n", "71") },
                        new CompanyRow { Id = "mc:dev", Title = "Dev Kapoor", Sub = "Customer service", Face = true, Hue = 0.35f, Ring = UiRole.None, Figure = "$17.00", Note = w.F("reallife_co_skill_n", "skill {n}", "n", "59") }
                    }
                };
                var c = new CompanyCard
                {
                    Kicker = "Speedy Bites", Title = "Lena Adler", Sub = w.F("reallife_co_emp_works", "{business}, {skill}", "business", "Speedy Bites", "skill", "Cooking"), Face = true, Hue = 0.1f, Ring = UiRole.None,
                    Back = CBack(w, "mgr:" + place, CompanyView.Managers),
                    FactLabels = new[] { w.T("reallife_mgr_skill_label", "Skill"), w.T("reallife_co_f_paid", "Paid"), w.T("reallife_mgr_allowance_label", "Allowance") },
                    FactValues = new[] { "64", "$16.50", w.F("reallife_co_row_a_day", "{amount} a day", "amount", "$52") }
                };
                c.Blocks = new[]
                {
                    CRow(w.T("reallife_mgr_mistake_chance", "Chance of a mistake"), "7.2%"),
                    CNote(w.F("reallife_mgr_appoint_replaces", "This replaces {name}.", "name", "Ray Walsh")),
                    CBtns(CB(w.T("reallife_mgr_appoint", "Appoint"), UiButtonKind.Primary, UiAction.Alt, CA(CompanyDo.Appoint, "lena", 0, 0, 0, place), UiIcon.Check),
                        CB(w.T("reallife_mgr_stand_down", "Stand down"), UiButtonKind.Secondary, UiAction.Count, CA(CompanyDo.StandDown, place, 0), UiIcon.Close, false, null, true),
                        CB(w.T("reallife_mgr_hand_back", "Hand back"), UiButtonKind.Secondary, UiAction.Count, CA(CompanyDo.HandBack, place), UiIcon.Back))
                };
                page.Card = c;
                break;
            }
            case "decisions":
            {
                page.List = new CompanyList
                {
                    Title = w.F("reallife_co_decisions_of", "{name}'s decisions", "name", "Ray"), Count = "5", Chosen = 0,
                    Rows = new[]
                    {
                        new CompanyRow { Id = "md:1", Title = "Stock", Sub = w.F("reallife_mgr_did_stock", "Reordered stock for {count} products, a {day}-day cover", "count", "6", "day", "3"), Icon = UiIcon.Package, Figure = "Day 214", Note = "Ray Walsh", Chip = w.T("reallife_mgr_state_done", "Done"), ChipTone = UiRole.Ok },
                        new CompanyRow { Id = "md:2", Title = "Raises", Sub = w.F("reallife_mgr_did_raise_agree", "Agreed a raise of {raise} for {name}", "name", "Lena Adler", "raise", "$0.50"), Icon = UiIcon.Coin, Figure = "Day 214", Note = "Ray Walsh", Chip = w.T("reallife_mgr_state_done", "Done"), ChipTone = UiRole.Ok },
                        new CompanyRow { Id = "md:3", Title = "Prices", Sub = w.F("reallife_mgr_did_prices", "Changed {count} prices", "count", "4"), Icon = UiIcon.Tag, Figure = "Day 213", Note = "Ray Walsh", Chip = w.T("reallife_mgr_state_done", "Done"), ChipTone = UiRole.Ok },
                        new CompanyRow { Id = "md:4", Title = "Rota", Sub = w.F("reallife_mgr_did_rota", "Wrote the rota: {shifts} shifts, {covered} of {wanted} hours covered", "shifts", "24", "covered", "112", "wanted", "112"), Icon = UiIcon.Calendar, Figure = "Day 212", Note = "Ray Walsh", Chip = w.T("reallife_mgr_state_done", "Done"), ChipTone = UiRole.Muted },
                        new CompanyRow { Id = "md:5", Title = "Stock", Sub = w.F("reallife_mgr_did_stock", "Reordered stock for {count} products, a {day}-day cover", "count", "4", "day", "3"), Icon = UiIcon.Package, Figure = "Day 211", Note = "Ray Walsh", Chip = w.T("reallife_mgr_state_undone", "Undone"), ChipTone = UiRole.Muted }
                    }
                };
                var c = new CompanyCard
                {
                    Kicker = "Speedy Bites", Title = "Stock", Icon = UiIcon.Package, Sub = w.F("reallife_mgr_dec_when", "{place}, day {day} at {hour}:00, by {name}", "place", "Speedy Bites", "day", "214", "hour", "08", "name", "Ray Walsh"),
                    Chips = new[] { w.T("reallife_mgr_state_done", "Done") }, ChipTones = new[] { UiRole.Ok }, Back = CBack(w, "mgr:" + place, CompanyView.Managers)
                };
                c.Blocks = new[]
                {
                    CNote(w.F("reallife_mgr_did_stock", "Reordered stock for {count} products, a {day}-day cover", "count", "6", "day", "3")),
                    CRow("Buns", w.F("reallife_mgr_dec_from_to", "{before} to {after}", "before", "120", "after", "240")),
                    CRow("Patties", w.F("reallife_mgr_dec_from_to", "{before} to {after}", "before", "80", "after", "160")),
                    CRow("Fries", w.F("reallife_mgr_dec_from_to", "{before} to {after}", "before", "60", "after", "120")),
                    CNote(w.T("reallife_mgr_dec_undo_what", "Undoing cancels the order if it has not been delivered.")),
                    CBtns(CB(w.T("reallife_mgr_undo", "Undo"), UiButtonKind.Secondary, UiAction.Count, CA(CompanyDo.Undo, null, 1), UiIcon.Back, false, null, true))
                };
                page.Card = c;
                break;
            }
            case "task":
            {
                string[] titles = { "Raises", "Rival offers", "Overtime", "Insurance claims", "Stock", "Prices", "Rota", "Hiring", "Marketing", "Leases" };
                string[] lines = { "Up to 110% of the going rate", "Up to 110% of the going rate", "A $400 budget a month", "Files within the window", "Keeps 3 days of stock", "Between 90% and 115%", "Written Sundays at 18:00", "Keeps the place at 9", "A $500 limit a month", "Up to a 5% rise" };
                bool[] owns = { true, true, true, false, true, true, true, true, false, true };
                UiIcon[] glyphs = { UiIcon.Coin, UiIcon.Handshake, UiIcon.Clock, UiIcon.ShieldCheck, UiIcon.Package, UiIcon.Tag, UiIcon.Calendar, UiIcon.People, UiIcon.Megaphone, UiIcon.Key };
                var list = new List<CompanyRow>();
                for (int k = 0; k < titles.Length; k++)
                {
                    list.Add(new CompanyRow { Id = "mk:" + k, Title = titles[k], Sub = lines[k], Icon = glyphs[k], Chip = owns[k] ? "Ray" : w.T("reallife_hier_you", "You"), ChipTone = owns[k] ? UiRole.Ok : UiRole.Muted });
                }
                page.List = new CompanyList { Title = w.F("reallife_co_decisions_owns", "Decisions {name} owns", "name", "Ray"), Count = "10", Rows = list.ToArray(), Chosen = 0 };
                var c = new CompanyCard { Kicker = "Speedy Bites", Title = "Raises", Icon = UiIcon.Coin, Back = CBack(w, "mgr:" + place, CompanyView.Managers) };
                c.Blocks = new[]
                {
                    CNote("The manager answers a request for a raise inside the limit you set, and asks you about anything above it."),
                    new CompanyBlock { Kind = CompanyBlockKind.Switch, Text = w.T("reallife_mgr_task_given", "Ray decides this"), Sub = "Up to 110% of the going rate", On = true, Value = "Ray", Act = CA(CompanyDo.TaskSet, place, 0, 0) },
                    new CompanyBlock { Kind = CompanyBlockKind.Stepper, Text = w.T("reallife_co_lim_raise", "Raise limit"), Sub = w.T("reallife_co_lim_raise_sub", "Of the going rate, above the pay now"), Value = "110%", CanLess = true, CanMore = true,
                        Act = CA(CompanyDo.LimitSet, place, 0, 1), Act2 = CA(CompanyDo.LimitSet, place, 0, 3), Act3 = CA(CompanyDo.LimitSet, place, 0, 2) }
                };
                page.Card = c;
                break;
            }
            case "inbox":
            {
                page.List = new CompanyList
                {
                    Title = w.T("reallife_hier_dir_inbox", "Questions inbox"), Count = "3", Chosen = 0,
                    Rows = new[]
                    {
                        new CompanyRow { Id = "he:1", Title = "Exception", Sub = "Speedy Bites", Icon = UiIcon.Envelope, Figure = "Day 214", Note = w.T("reallife_hier_you", "You"), Chip = w.T("reallife_hier_state_open", "Open"), ChipTone = UiRole.Warn },
                        new CompanyRow { Id = "he:2", Title = "Lease", Sub = "Hudson Warehouse", Icon = UiIcon.Envelope, Figure = "Day 213", Note = "Meera Iyer", Chip = w.T("reallife_hier_state_open", "Open") },
                        new CompanyRow { Id = "he:3", Title = "Budget", Sub = "Bloom & Co", Icon = UiIcon.Envelope, Figure = "Day 211", Note = "Tessa Brandt", Chip = "Settled", ChipTone = UiRole.Muted }
                    }
                };
                var c = new CompanyCard
                {
                    Kicker = w.T("reallife_hier_dir_inbox", "Questions inbox"), Title = "Exception", Sub = "Speedy Bites", Icon = UiIcon.Envelope, Chips = new[] { w.T("reallife_hier_state_open", "Open"), w.F("reallife_hier_chip_with", "With {who}", "who", w.T("reallife_hier_you", "You")) },
                    ChipTones = new[] { UiRole.Warn, UiRole.None }, Back = CBack(w, null, CompanyView.Managers)
                };
                c.Blocks = new[]
                {
                    CNote(w.F("reallife_hier_say_exception", "{name} asks for {asked} an hour; the limit is {ceiling}.", "name", "Dev Kapoor", "asked", "$19.00", "ceiling", "$18.00")),
                    CBtns(CB(w.T("reallife_co_answer", "Answer"), UiButtonKind.Primary, UiAction.Alt, CA(CompanyDo.Go, "card", 1), UiIcon.Next)),
                    CBtns(CB(w.T("reallife_hier_open_staff", "Open the staff"), UiButtonKind.Secondary, UiAction.Count, CA(CompanyDo.Go, "emp:dev", CompanyView.Staff), UiIcon.People),
                        CB(w.T("reallife_hier_open_dashboard", "Open the dashboard"), UiButtonKind.Secondary, UiAction.Count, CA(CompanyDo.Go, "mgr:" + place, CompanyView.Managers, CompanyView.SubDashboard), UiIcon.ChartPie),
                        CB(w.T("reallife_hier_open_policy", "Open the policy"), UiButtonKind.Secondary, UiAction.Count, CA(CompanyDo.Go, "mgr:" + place, CompanyView.Managers, CompanyView.SubPolicy), UiIcon.Sliders)),
                    CSec(w.T("reallife_hier_sec_story", "The story")),
                    CRow(w.T("reallife_hier_raised", "Raised"), "Day 214"),
                    CRow(w.T("reallife_hier_moved", "Reached the owner"), "Day 214"),
                    new CompanyBlock { Kind = CompanyBlockKind.Lines, Rows = new[] { new CompanyDone { When = "Day 214", Text = "Raised: Exception, to the owner", Money = "Ray Walsh" } } },
                    CNote(w.T("reallife_hier_what_yours", "This one is yours to answer."))
                };
                page.Card = c;
                break;
            }
            case "policy":
            case "rule":
            {
                var rows = new List<CompanyRow>
                {
                    new CompanyRow { Caption = true, Title = w.T("reallife_pol_cat_hiring", "Hiring") },
                    new CompanyRow { Id = "pf:0", Title = w.T("reallife_pol_field_skill_floor", "Least skill hired"), Icon = UiIcon.Sliders, Figure = "None", Sub = w.T("reallife_pol_as_above", "As above") },
                    new CompanyRow { Id = "pf:1", Title = "Most a hire is paid", Icon = UiIcon.Sliders, Figure = "100%", Sub = w.T("reallife_pol_as_above", "As above") },
                    new CompanyRow { Caption = true, Title = w.T("reallife_pol_cat_pay", "Pay") },
                    new CompanyRow { Id = "pf:3", Title = w.T("reallife_pol_field_pay_band", "Pay band"), Icon = UiIcon.Sliders, Figure = "110%", Sub = w.T("reallife_pol_set_here_row", "Set here"), Chip = w.T("reallife_co_held", "Held"), ChipTone = UiRole.Warn },
                    new CompanyRow { Id = "pf:4", Title = w.T("reallife_pol_field_raise_most", "Largest raise"), Icon = UiIcon.Sliders, Figure = "No limit", Sub = w.T("reallife_pol_as_above", "As above") },
                    new CompanyRow { Caption = true, Title = w.T("reallife_pol_cat_escalation", "Escalation") },
                    new CompanyRow { Id = "pf:21", Title = w.T("reallife_pol_field_unmet_days", "Days a request may wait"), Icon = UiIcon.Sliders, Figure = "2 days", Sub = w.T("reallife_pol_as_above", "As above") }
                };
                page.List = new CompanyList
                {
                    Title = w.T("reallife_hier_dir_policy", "Policy centre"), Count = w.F("reallife_pol_scope_store", "{store}", "store", "Speedy Bites"), Rows = rows.ToArray(),
                    Filters = new[] { w.T("reallife_pol_scope_company", "Company"), "Midtown", "Speedy Bites" }, Filter = 2, Chosen = which == "rule" ? 4 : -1
                };
                m.FilterIds = new[] { "own", "reg:Midtown", "mgr:" + place };
                if (which == "rule")
                {
                    var c = new CompanyCard
                    {
                        Kicker = w.T("reallife_pol_cat_pay", "Pay") + ", " + w.F("reallife_pol_scope_store", "{store}", "store", "Speedy Bites"), Title = w.T("reallife_pol_field_pay_band", "Pay band"), Icon = UiIcon.Sliders,
                        Back = CBack(w, "mgr:" + place, CompanyView.Managers, CompanyView.SubPolicy)
                    };
                    c.Blocks = new[]
                    {
                        CNote(w.T("reallife_pol_field_pay_band_what", "The most anybody is paid after a raise or an offer matched, as a share of the going rate.")),
                        CSec(w.T("reallife_pol_set_here", "Set here")),
                        new CompanyBlock { Kind = CompanyBlockKind.Choice, Text = w.T("reallife_pol_field_pay_band", "Pay band"), Options = new[] { "Follow: 105%", "100%", "105%", "110%", "120%" }, Chosen = 3,
                            Acts = new[] { CA(CompanyDo.RuleSet, place, 3, -1, 2), CA(CompanyDo.RuleSet, place, 3, 0, 2), CA(CompanyDo.RuleSet, place, 3, 1, 2), CA(CompanyDo.RuleSet, place, 3, 2, 2), CA(CompanyDo.RuleSet, place, 3, 3, 2) } },
                        CSec(w.T("reallife_pol_sec_force", "In force")),
                        CRow(w.T("reallife_pol_scope_company", "The whole company"), "105%"),
                        CRow(w.T("reallife_pol_col_force", "In force here"), "105%", UiRole.WarnText),
                        CNote(w.F("reallife_pol_held_line", "A lower scope may only be stricter: this one is held to {value}.", "value", "105%"), UiRole.WarnText),
                        CNote(w.T("reallife_pol_way_up", "A higher step is looser."))
                    };
                    page.Card = c;
                }
                else
                {
                    var c = new CompanyCard { Kicker = w.T("reallife_hier_dir_policy", "Policy centre"), Title = w.F("reallife_pol_scope_store", "{store}", "store", "Speedy Bites"), Icon = UiIcon.Sliders, Back = CBack(w, "mgr:" + place, CompanyView.Managers) };
                    c.Blocks = new[]
                    {
                        CSec(w.T("reallife_pol_sec_scope", "This scope")),
                        CRow(w.T("reallife_pol_own", "Rules set here"), "3"),
                        CNote(w.T("reallife_pol_what", "A rule set here holds for this scope. A lower scope may only be stricter than the one above it.")),
                        CNote(w.T("reallife_pol_held_what", "A step past the scope above is held to it.")),
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
                page.List = new CompanyList
                {
                    Title = w.T("reallife_pol_templates", "Templates"), Count = "4", Chosen = own ? 2 : 1,
                    Rows = new[]
                    {
                        new CompanyRow { Id = "pk:b0", Title = w.T("reallife_pol_tpl_start", "The starting policy"), Sub = w.T("reallife_pol_tpl_built", "Built in"), Icon = UiIcon.ClipboardText },
                        new CompanyRow { Id = "pk:b1", Title = w.T("reallife_pol_tpl_careful", "Careful"), Sub = w.T("reallife_pol_tpl_built", "Built in"), Icon = UiIcon.ClipboardText },
                        new CompanyRow { Id = "pk:o1", Title = w.F("reallife_pol_tpl_own", "Yours, number {number}", "number", "1"), Sub = w.T("reallife_pol_tpl_yours", "Saved by you"), Icon = UiIcon.ClipboardText, Figure = "Day 190" }
                    }
                };
                var c = new CompanyCard
                {
                    Kicker = w.F("reallife_pol_scope_store", "{store}", "store", "Speedy Bites"), Title = own ? w.F("reallife_pol_tpl_own", "Yours, number {number}", "number", "1") : w.T("reallife_pol_tpl_careful", "Careful"),
                    Sub = own ? w.T("reallife_pol_tpl_yours", "Saved by you") : w.T("reallife_pol_tpl_built", "Built in"), Icon = UiIcon.ClipboardText, Back = CBack(w, "mgr:" + place, CompanyView.Managers, CompanyView.SubPolicy)
                };
                c.Blocks = new[]
                {
                    CNote(own ? w.T("reallife_pol_templates_what", "A template is a set of rules you can put on a scope in one step.") : w.T("reallife_pol_tpl_careful_what", "Small raises only, the owner approves every exception, and managers report daily.")),
                    CSec(w.T("reallife_pol_sec_template", "What it sets")),
                    CRow(w.T("reallife_pol_field_pay_band", "Pay band"), "100%"), CRow(w.T("reallife_pol_field_skill_floor", "Least skill hired"), "50"), CRow(w.T("reallife_pol_field_approver", "Who may agree an exception"), "Nobody"),
                    CRow(w.T("reallife_pol_field_budget", "Budget"), "$200"), CRow(w.T("reallife_pol_field_gap_days", "Days a gap may last"), "1 day"), CRow(w.T("reallife_pol_field_reports", "Reports"), "Every day"),
                    CNote(w.F("reallife_pol_tpl_onto", "Applying it sets these rules for {scope}.", "scope", "Speedy Bites")),
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
                page.List = new CompanyList
                {
                    Title = w.T("reallife_hier_dash_pending", "Questions here"), Count = "2", Chosen = -1,
                    Rows = new[]
                    {
                        new CompanyRow { Id = "he:1", Title = "Exception", Sub = w.T("reallife_hier_you", "You"), Icon = UiIcon.Envelope, Figure = "Day 214", Chip = w.T("reallife_hier_state_open", "Open"), ChipTone = UiRole.Warn },
                        new CompanyRow { Id = "he:7", Title = "Budget", Sub = "Ray Walsh", Icon = UiIcon.Envelope, Figure = "Day 201", Chip = "Settled", ChipTone = UiRole.Muted }
                    }
                };
                var c = new CompanyCard
                {
                    Kicker = "Speedy Bites, Ray Walsh", Title = w.T("reallife_hier_dash_title", "Store dashboard"), Icon = UiIcon.ChartPie, Back = CBack(w, "mgr:" + place, CompanyView.Managers),
                    Chips = new[] { w.F("reallife_hier_level_chip", "Level {level}", "level", "L1") }, ChipTones = new[] { UiRole.None },
                    FactLabels = new[] { w.T("reallife_co_f_staff", "Staff"), w.T("reallife_co_f_standing", "Standing"), w.T("reallife_co_f_score", "Score"), w.T("reallife_co_f_pending", "Pending") },
                    FactValues = new[] { "9 of 9", "72", "81", "1" }
                };
                c.Blocks = new[]
                {
                    CSec(w.T("reallife_hier_sec_figures", "Figures")),
                    CRow(w.T("reallife_hier_fig_budget", "Budget"), w.F("reallife_mgr_spent_value", "{spent} of {budget}", "spent", "$310", "budget", "$400")),
                    new CompanyBlock { Kind = CompanyBlockKind.Meter, Text = w.T("reallife_hier_fig_budget_used", "Budget used"), Value = "78%", Number = 78, Tone = UiRole.Ok },
                    CRow(w.T("reallife_ibox_profit", "Profit"), "+" + CM(1140)),
                    CSec(w.T("reallife_hier_sec_quick", "Do now")),
                    CBtns(CB(w.T("reallife_hier_quick_fill", "Fill the gaps"), UiButtonKind.Primary, UiAction.Alt, CA(CompanyDo.StoreFill, place), UiIcon.People),
                        CB(w.T("reallife_hier_quick_raise", "Raise the underpaid"), UiButtonKind.Secondary, UiAction.Option, CA(CompanyDo.StoreRaise, place), UiIcon.Coin),
                        CB(w.T("reallife_hier_open_policy", "Open the policy"), UiButtonKind.Secondary, UiAction.Count, CA(CompanyDo.Go, "mgr:" + place, CompanyView.Managers, CompanyView.SubPolicy), UiIcon.Sliders)),
                    new CompanyBlock { Kind = CompanyBlockKind.Choice, Text = w.T("reallife_hier_region_pick", "Region"), Options = new[] { w.F("reallife_hier_region_own", "{district}", "district", "Midtown"), "A", "B", "C" }, Chosen = 0,
                        Acts = new[] { CA(CompanyDo.StoreArea, place, 0), CA(CompanyDo.StoreArea, place, 1), CA(CompanyDo.StoreArea, place, 2), CA(CompanyDo.StoreArea, place, 3) } },
                    CNote(w.T("reallife_hier_dash_what", "A store dashboard shows what this manager holds and lets you act for them."))
                };
                page.Card = c;
                break;
            }
            case "regiondash":
            {
                page.List = new CompanyList
                {
                    Title = w.T("reallife_hier_dash_pending", "Questions here"), Count = "1", Chosen = -1,
                    Rows = new[] { new CompanyRow { Id = "he:2", Title = "Lease", Sub = "Hudson Warehouse", Icon = UiIcon.Envelope, Figure = "Day 213", Chip = w.T("reallife_hier_state_open", "Open"), ChipTone = UiRole.Warn } }
                };
                var c = new CompanyCard
                {
                    Kicker = w.F("reallife_mgr_row_region", "Region {district}", "district", "Midtown") + ", Meera Iyer", Title = w.T("reallife_hier_dash_title", "Store dashboard"), Icon = UiIcon.ChartPie,
                    Back = CBack(w, "reg:Midtown", CompanyView.Managers), Chips = new[] { w.F("reallife_hier_level_chip", "Level {level}", "level", "L2") }, ChipTones = new[] { UiRole.None },
                    FactLabels = new[] { w.T("reallife_co_f_places", "Places"), w.T("reallife_co_f_gaps", "Gaps"), w.T("reallife_co_f_standing", "Standing"), w.T("reallife_co_f_pending", "Pending") },
                    FactValues = new[] { "3/3", "1", "68", "1" }
                };
                c.Blocks = new[]
                {
                    CSec(w.T("reallife_hier_sec_figures", "Figures")), CRow(w.T("reallife_hier_fig_score", "Score"), "77"), CRow(w.T("reallife_hier_fig_profit", "Profit"), "+" + CM(1310)), CRow(w.T("reallife_pol_field_profit_target", "Profit target"), "$300"),
                    CSec(w.T("reallife_hier_sec_quick", "Do now")),
                    CBtns(CB(w.T("reallife_hier_quick_fill", "Fill the gaps"), UiButtonKind.Primary, UiAction.Alt, CA(CompanyDo.RegionFill, "Midtown"), UiIcon.People),
                        CB(w.T("reallife_hier_quick_raise", "Raise the underpaid"), UiButtonKind.Secondary, UiAction.Option, CA(CompanyDo.RegionRaise, "Midtown"), UiIcon.Coin)),
                    CBtns(CB(w.T("reallife_hier_quick_move", "Move crew"), UiButtonKind.Secondary, UiAction.Count, CA(CompanyDo.RegionMove, "Midtown"), UiIcon.Next),
                        CB(w.T("reallife_hier_quick_appoint", "Appoint managers"), UiButtonKind.Secondary, UiAction.Count, CA(CompanyDo.RegionAppoint, "Midtown"), UiIcon.People),
                        CB(w.T("reallife_hier_open_policy", "Open the policy"), UiButtonKind.Secondary, UiAction.Count, CA(CompanyDo.Go, "reg:Midtown", CompanyView.Managers, CompanyView.SubPolicy), UiIcon.Sliders)),
                    CNote(w.T("reallife_hier_region_dash_what", "A region dashboard shows what its manager holds and lets you act for them."))
                };
                page.Card = c;
                break;
            }
            default:   // seat
            {
                bool pick = which == "seat_pick", filled = which == "seat_filled";
                page.List = new CompanyList
                {
                    Title = w.T("reallife_co_candidates", "Who could do it"), Count = "2", Chosen = pick ? 0 : -1,
                    Rows = new[]
                    {
                        new CompanyRow { Id = "xc:pro1", Title = "Noah Feldman", Sub = "Finance, senior", Face = true, Hue = 0.2f, Ring = UiRole.None, Figure = "$42.00", Note = "Pioneer HQ" },
                        new CompanyRow { Id = "xc:pro2", Title = "Ines Duarte", Sub = "Finance, lead", Face = true, Hue = 0.5f, Ring = UiRole.None, Figure = "$55.00", Note = "Pioneer HQ" }
                    }
                };
                var c = new CompanyCard { Kicker = w.T("reallife_hier_seats", "Executive seats"), Title = "Chief financial officer", Icon = UiIcon.IdBadge, Back = CBack(w, "seat:3", CompanyView.Managers), Chips = new[] { w.T("reallife_mgr_empty_post", "Empty post") }, ChipTones = new[] { UiRole.Muted } };
                c.Blocks = new[]
                {
                    CNote(w.T("reallife_hier_seat_what_cfo", "The finance chief tops up the tax reserve when a quarter closes and watches the managers' budgets.")),
                    CNote(w.F("reallife_hier_seat_hire", "Choose one of your professionals: {roles}.", "roles", "Accountant, Financial analyst")),
                    CNote(w.T("reallife_hier_exec_pick", "Pick a professional at the left."))
                };
                if (pick)
                {
                    c = new CompanyCard
                    {
                        Kicker = "Chief financial officer", Title = "Noah Feldman", Sub = "Finance, senior, Pioneer HQ", Face = true, Hue = 0.2f, Ring = UiRole.None, Back = CBack(w, "seat:3", CompanyView.Managers)
                    };
                    c.Blocks = new[]
                    {
                        CRow(w.T("reallife_hier_going", "Going rate"), "$42.00"),
                        CNote(w.T("reallife_hier_seat_what_cfo", "The finance chief tops up the tax reserve when a quarter closes and watches the managers' budgets.")),
                        CBtns(CB(w.T("reallife_mgr_appoint", "Appoint"), UiButtonKind.Primary, UiAction.Alt, CA(CompanyDo.SeatAppoint, "pro1", 3), UiIcon.Check))
                    };
                }
                else if (filled)
                {
                    c = new CompanyCard
                    {
                        Kicker = w.T("reallife_hier_seats", "Executive seats"), Title = "Ethan Brooks", Sub = "Chief operating officer", Face = true, Hue = 0.15f, Ring = UiRole.Ok, Back = CBack(w, "exe:1", CompanyView.Managers),
                        Chips = new[] { w.T("reallife_mgr_in_post", "In post") }, ChipTones = new[] { UiRole.Ok }
                    };
                    c.Blocks = new[]
                    {
                        CNote(w.T("reallife_hier_seat_what_coo", "The operating chief answers what the managers pass up and watches their targets.")),
                        CRow(w.T("reallife_mgr_since", "Since"), "Day 60"), CRow(w.T("reallife_hier_col_role", "Role"), "Operations, senior"), CRow(w.T("reallife_hier_going", "Going rate"), "$48.00"),
                        CRow(w.T("reallife_mgr_allowance", "Allowance"), w.F("reallife_co_row_a_day", "{amount} a day", "amount", "$96")), CRow(w.T("reallife_hier_paid_month", "Paid this month"), CM(2880)),
                        CRow(w.T("reallife_hier_area_open", "Open in their area"), "2"), CRow(w.T("reallife_hier_area_settled", "Settled this week"), "7"), CRow(w.T("reallife_mgr_decided", "Decided"), "41 / 38"),
                        CBtns(CB(w.T("reallife_mgr_stand_down", "Stand down"), UiButtonKind.Secondary, UiAction.Count, CA(CompanyDo.SeatStandDown, null, 1), UiIcon.Close, false, null, true))
                    };
                }
                page.Card = c;
                break;
            }
        }
        m.Sub = page;
    }
}
