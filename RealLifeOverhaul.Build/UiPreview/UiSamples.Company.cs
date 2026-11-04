using System;
using System.Collections.Generic;
using RealLifeOverhaul.Ui;

// The Company app's samples: every state of every tab with realistic data (long names, empty, full, day 5, day 214), so the offline checks lay each out in every language
// and frame and the previews draw them over the game's streets. The names are samples' own and never ship: the live source reads the player's businesses and people.
internal static partial class UiSamples
{
    public static readonly string[] CompanyStates =
    {
        "overview_big", "overview_tall", "overview_one", "overview_shop", "overview_quiet", "question", "lease_question", "gap_question", "deep_question", "company_off",
        "staff_big", "staff_summary", "staff_offer", "staff_give", "staff_strike", "staff_overtime", "staff_one",
        "managers_big", "managers_owner", "managers_region", "managers_post", "managers_none", "managers_flat", "managers_off",
        "managers_sub_appoint", "managers_sub_decisions", "managers_sub_task", "managers_sub_inbox", "managers_sub_policy", "managers_sub_rule", "managers_sub_templates",
        "managers_sub_dashboard", "managers_sub_regiondash", "managers_sub_seat", "managers_sub_tpl_own", "managers_sub_seat_pick", "managers_sub_seat_filled",
        "rota_big", "rota_day", "rota_advice", "rota_look", "rota_look_hire", "rota_edit", "rota_edit_other", "rota_one", "rota_refused", "rota_off",
        "marketing_big", "marketing_running", "marketing_report", "marketing_summary", "marketing_one", "marketing_off",
        "cover_big", "cover_claim", "cover_covered", "cover_summary",
        "tax_big", "tax_return", "tax_accountant", "tax_hide", "licence_card", "tax_one", "tax_off"
    };

    public static CompanyWords CompanyWordsOf(UiWords t) => new CompanyWords(t, UtilitiesMoney);

    private static string CM(float money) => UtilitiesMoney(money);

    // A sample's own words (an alert's, a decision's, a name): data the director writes, never a label of the app, so no key is asked for and none ships. Names filled in as the app's are.
    private static string SF(string english, params string[] pairs)
    {
        for (int i = 0; i + 1 < pairs.Length; i += 2)
        {
            english = english.Replace("{" + pairs[i] + "}", pairs[i + 1] ?? string.Empty);
        }
        return english;
    }

    private static CompanyAct CA(CompanyDo d, string s = null, int a = 0, int b = 0, int c = 0, string s2 = null) => CompanyAct.Of(d, s, a, b, c, s2);

    private static CompanyButton CB(string label, UiButtonKind kind, UiAction key, CompanyAct act, UiIcon icon = UiIcon.None, bool disabled = false, string why = null, bool arms = false) =>
        new CompanyButton { Label = label, Kind = kind, Key = key, Act = act, Icon = icon, Disabled = disabled, Why = why, Arms = arms };

    public static CompanyView CompanyViewOf(string state)
    {
        var v = new CompanyView();
        string tab = state.Split('_')[0];
        v.Tab = tab == "staff" ? CompanyView.Staff : tab == "managers" ? CompanyView.Managers : tab == "rota" ? CompanyView.Rota : tab == "marketing" ? CompanyView.Marketing
            : tab == "cover" ? CompanyView.Cover : tab == "tax" || tab == "licence" ? CompanyView.Tax : CompanyView.Overview;
        if (tab == "managers")
        {
            string place = "mgr:Speedy Bites~7";
            v.Pick[CompanyView.Managers] = place;
            v.Pick[CompanyView.Managers] = state == "managers_owner" ? "own" : state == "managers_region" || state == "managers_sub_regiondash" ? "reg:Midtown" : state == "managers_post" ? "post:Pioneer Game Studio~9" : state == "managers_sub_seat" ? "seat:3" : place;
            v.SubPick[CompanyView.Managers] = state == "managers_sub_tpl_own" ? "pk:o1" : state == "managers_sub_seat_pick" ? "xc:pro1" : string.Empty;
            v.Pick[CompanyView.Managers] = state == "managers_sub_tpl_own" ? place : state == "managers_sub_seat_filled" ? "exe:1" : v.Pick[CompanyView.Managers];
            v.Sub[CompanyView.Managers] = state == "managers_sub_appoint" ? CompanyView.SubAppoint : state == "managers_sub_decisions" ? CompanyView.SubDecision : state == "managers_sub_task" ? CompanyView.SubTask
                : state == "managers_sub_inbox" ? CompanyView.SubInbox : state == "managers_sub_policy" || state == "managers_sub_rule" ? CompanyView.SubPolicy : state == "managers_sub_templates" || state == "managers_sub_tpl_own" ? CompanyView.SubTemplates
                : state == "managers_sub_dashboard" ? CompanyView.SubDashboard : state == "managers_sub_regiondash" ? CompanyView.SubRegion : state.StartsWith("managers_sub_seat", System.StringComparison.Ordinal) ? CompanyView.SubSeat : CompanyView.SubMain;
        }
        if (tab == "rota")
        {
            v.Pick[CompanyView.Rota] = RotaPickOf(state);
            v.Sub[CompanyView.Rota] = state == "rota_day" ? CompanyView.SubDay : state == "rota_advice" || state.StartsWith("rota_look", System.StringComparison.Ordinal) ? CompanyView.SubAdvice
                : state == "rota_edit" || state == "rota_edit_other" ? CompanyView.SubEdit : CompanyView.SubMain;
            v.SubArg[CompanyView.Rota] = state == "rota_look" ? 2 : state == "rota_look_hire" ? 3 : 0;   // the rung read, counted from one
            v.Day = 1;
        }
        if (tab == "tax" || tab == "licence")
        {
            v.Sub[CompanyView.Tax] = state == "tax_return" ? CompanyView.SubReturn : state == "tax_accountant" ? CompanyView.SubAccountant : state == "tax_hide" ? CompanyView.SubHide : state == "licence_card" ? CompanyView.SubLicence : CompanyView.SubMain;
            v.Pick[CompanyView.Tax] = state == "tax_hide" ? "hide:Speedy Bites~7" : state == "licence_card" ? "lic:Bloom & Co~214:3" : state == "tax_return" ? "ret" : state == "tax_accountant" ? "acc" : string.Empty;
        }
        if (state == "overview_shop")
        {
            v.Pick[CompanyView.Overview] = "Speedy Bites~7";
            v.Sub[CompanyView.Overview] = CompanyView.SubBusiness;
        }
        if (state == "question" || state == "lease_question" || state == "gap_question" || state == "deep_question")
        {
            v.Question = state == "lease_question" ? 2 : state == "gap_question" ? 3 : state == "deep_question" ? 4 : 1;
        }
        return v;
    }

    public static CompanyFacts CompanyFactsOf(UiWords t, string state)
    {
        CompanyWords w = CompanyWordsOf(t);
        bool one = state.EndsWith("_one", StringComparison.Ordinal);
        one = one || state == "staff_one" || state == "rota_one" || state == "marketing_one" || state == "tax_one";
        var f = new CompanyFacts { Ready = state != "company_off", Name = one ? "Corner Cafe" : "Pioneer Holdings", Mark = one ? "CC" : "PH" };
        f.Counts = state == "company_off" ? new string[7] : one ? new string[] { null, "2", null, "1", null, null, "1" } : new[] { null, "31", "6", null, "2", null, "1" };
        f.Counts[2] = state == "managers_off" || state == "managers_none" ? null : f.Counts[2];   // a part that is off counts nothing in the sidebar
        f.Counts[3] = state == "rota_off" ? null : f.Counts[3];
        f.Counts[4] = state == "marketing_off" ? null : f.Counts[4];
        f.Counts[6] = state == "tax_off" ? null : f.Counts[6];
        if (state == "company_off")
        {
            f.OffTitle = w.T("reallife_co_off_title", "The company is not running");
            f.OffBody = w.T("reallife_co_off_body", "Start a business, and its staff, rota, cover and tax are run from here.");
            return f;
        }
        string tab = state.Split('_')[0];
        if (tab == "overview" || state.EndsWith("question", StringComparison.Ordinal))
        {
            CompanyOverview(f, w, state);
        }
        else if (tab == "staff")
        {
            CompanyStaff(f, w, state);
        }
        else if (tab == "managers")
        {
            CompanyManagers(f, w, state);
        }
        else if (tab == "rota")
        {
            CompanyRota(f, w, state);
        }
        else if (tab == "marketing")
        {
            CompanyMarketing(f, w, state);
        }
        else if (tab == "cover")
        {
            CompanyCover(f, w, state);
        }
        else
        {
            CompanyTax(f, w, state);
        }
        return f;
    }

    // ------------------------------------------------------------------------------------------------- the overview

    private static CompanyQuestion RaiseQuestion(CompanyWords w, bool threeAnswers = false)
    {
        var q = new CompanyQuestion
        {
            Id = 1, Kicker = w.F("reallife_co_q_kicker_manager", "A manager's question, {place}", "place", "Speedy Bites, 7 5th Avenue"), Title = w.T("reallife_co_q_raise_title", "A raise above my limit"),
            Headline = SF("{name} asks for {pay} an hour. {manager} can approve up to {limit}.", "name", "Dev Kapoor", "pay", "$19", "manager", "Ray", "limit", "$18"),
            Body = SF("{name} has worked both registers for 41 days and covered the lunch rush all week. Another raise this month goes over {manager}'s limit.", "name", "Dev", "manager", "Ray"),
            Asker = "Ray Walsh", AskerRole = w.T("reallife_co_q_manager_asks", "Manager"), AskerHue = 0.55f, AskerRing = UiRole.Warn,
            Subject = "Dev Kapoor", SubjectRole = w.F("reallife_co_q_cashier_days", "{skill}, {days} days", "skill", "Customer service", "days", "41"), SubjectRing = UiRole.Ok,
            FactLabels = new[] { w.T("reallife_co_q_now", "Now"), w.T("reallife_co_q_asks_for", "Asks for"), w.T("reallife_co_q_limit", "Limit"), "Covered this week" },
            FactValues = new[] { "$17 / h", "$19 / h", "$18 / h", "38 h" },
            OpenLabel = w.F("reallife_co_open_page", "Open {name}'s page", "name", "Ray"), Open = CA(CompanyDo.Go, "mgr:Speedy Bites~7", CompanyView.Managers),
            Selected = 0
        };
        q.Choices = new[]
        {
            new CompanyChoice
            {
                Name = w.F("reallife_co_q_c_raise", "Raise {name} to {pay} an hour", "name", "Dev", "pay", "$19"), Button = w.F("reallife_co_q_raise_do", "Raise {name} to {pay}", "name", "Dev", "pay", "$19"),
                Note = w.T("reallife_co_q_now_note", "now"), Act = CA(CompanyDo.Settle, null, 1, 0),
                Lines = new[]
                {
                    new CompanyConsequence { Text = w.F("reallife_co_q_year_more", "About {amount} a year more in wages", "amount", "$3,950"), Tone = UiConsequence.Loss, Change = 1 },
                    new CompanyConsequence { Text = w.F("reallife_co_q_stays", "{name} stays and the registers' morale rises", "name", "Dev"), Tone = 1, Change = 1 }
                }
            },
            new CompanyChoice
            {
                Name = w.F("reallife_co_q_c_limit", "Raise {manager}'s limit to {limit}, and let him decide", "manager", "Ray", "limit", "$19"), Button = w.T("reallife_co_q_limit_do", "Raise the limit"),
                Note = w.T("reallife_co_q_standing_note", "standing"), Act = CA(CompanyDo.Settle, null, 1, 3),
                Lines = new[]
                {
                    new CompanyConsequence { Text = w.F("reallife_co_q_limit_line", "{manager} approves raises up to {limit} without asking you", "manager", "Ray", "limit", "$19"), Tone = -1, Change = 1 },
                    new CompanyConsequence { Text = w.T("reallife_co_q_fewer", "Fewer questions at your desk"), Tone = 1, Change = -1 }
                }
            },
            new CompanyChoice
            {
                Name = w.F("reallife_co_q_c_bonus", "Give {name} a {amount} bonus instead", "name", "Dev", "amount", "$300"), Button = w.T("reallife_co_q_bonus_do", "Give the bonus"),
                Note = w.T("reallife_co_q_once_note", "once"), Act = CA(CompanyDo.Settle, null, 1, 2),
                Lines = new[]
                {
                    new CompanyConsequence { Text = w.F("reallife_co_q_today", "{amount} today", "amount", "$300"), Tone = UiConsequence.Loss, Change = -1 },
                    new CompanyConsequence { Text = w.F("reallife_co_q_ask_again", "{name} may ask again next month", "name", "Dev"), Tone = -1, Change = 1 }
                }
            },
            new CompanyChoice
            {
                Name = w.T("reallife_co_q_c_no", "Say no"), Button = w.T("reallife_co_q_no_do", "Say no"), Note = w.T("reallife_co_q_nocost_note", "no cost"), Act = CA(CompanyDo.Settle, null, 1, 1),
                Lines = new[] { new CompanyConsequence { Text = w.F("reallife_co_q_may_leave", "{name} may leave; hiring costs about {amount}", "name", "Dev", "amount", "$1,800"), Tone = -1, Change = -1 } }
            }
        };
        if (threeAnswers)
        {
            q.Choices = new[] { q.Choices[0], q.Choices[2], q.Choices[3] };   // the limit stays where it is: the mockup's overview asks three things
        }
        return q;
    }

    // A question with no figure of its own to weigh: a gap in the crew, which the owner fills or dismisses; held below the owner in Deep Mode, it is tried or passed up.
    private static CompanyQuestion PlainQuestion(CompanyWords w, bool deep)
    {
        var q = new CompanyQuestion
        {
            Id = deep ? 4 : 3, Kicker = w.F("reallife_co_q_kicker_manager", "A manager's question, {place}", "place", "Speedy Bites, 7 5th Avenue"), Title = "The crew is short",
            Headline = w.F("reallife_hier_say_gap", "{staff} staff, {keep} wanted since day {day}", "staff", "7", "keep", "9", "day", "209"),
            Body = SF("Two hours a day have nobody on the grill. {manager} cannot hire past the budget this month.", "manager", "Ray"),
            Asker = "Ray Walsh", AskerRole = w.T("reallife_co_q_manager_asks", "Manager"), AskerHue = 0.55f, AskerRing = UiRole.Warn,
            FactLabels = new[] { "Staff", "Wanted", w.T("reallife_co_q_budget", "Budget left") },
            FactValues = new[] { "7", "9", "$90" }, OpenLabel = w.F("reallife_co_open_page", "Open {name}'s page", "name", "Ray"), Open = CA(CompanyDo.Go, "mgr:Speedy Bites~7", CompanyView.Managers), Deep = deep, Selected = 0
        };
        q.Choices = deep ? new CompanyChoice[0] : new[]
        {
            new CompanyChoice
            {
                Name = w.T("reallife_hier_do_fill", "Fill the gaps now"), Button = w.T("reallife_hier_do_fill", "Fill the gaps now"), Note = w.T("reallife_co_q_now_note", "now"), Act = CA(CompanyDo.Settle, null, 3, 0),
                Lines = new[] { new CompanyConsequence { Text = SF("Hires {count} from the job board, about {amount} a week", "count", "2", "amount", "$1,150"), Tone = UiConsequence.Loss, Change = 1 } }
            },
            new CompanyChoice
            {
                Name = w.T("reallife_hier_do_dismiss", "Dismiss"), Button = w.T("reallife_hier_do_dismiss", "Dismiss"), Note = w.T("reallife_co_q_nocost_note", "no cost"), Act = CA(CompanyDo.Dismiss, null, 3),
                Lines = new[] { new CompanyConsequence { Text = "The gap stays; the question comes back in a week", Tone = -1, Change = 0 } }
            }
        };
        return q;
    }

    private static CompanyQuestion LeaseQuestion(CompanyWords w)
    {
        var q = new CompanyQuestion
        {
            Id = 2, Kicker = w.F("reallife_co_q_kicker_manager", "A manager's question, {place}", "place", "Hudson Warehouse, Hell's Kitchen"), Title = w.T("reallife_co_q_lease_title", "A lease is up for renewal"),
            Headline = w.F("reallife_co_q_lease_head", "The lease at {place} renews in {days} days: {rent} a month, up {rise}.", "place", "Hudson Warehouse", "days", "9", "rent", "$6,800", "rise", "8%"),
            Body = SF("{manager} may agree a rise of up to {limit}. This one is past it, so it is yours. The landlord holds the premises at the new rent from the day the lease ends.", "manager", "Omar", "limit", "5%"),
            Asker = "Omar Haddad", AskerRole = w.T("reallife_co_q_manager_asks", "Manager"), AskerHue = 0.75f, AskerRing = UiRole.Warn,
            FactLabels = new[] { w.T("reallife_co_q_rent_now", "Rent now"), w.T("reallife_co_q_rent_offer", "Offered"), w.T("reallife_co_q_term", "New term"), w.T("reallife_co_q_rise_limit", "Rise") },
            FactValues = new[] { "$6,300", "$6,800", "24 months", "7.9%" },
            OpenLabel = w.F("reallife_co_open_page", "Open {name}'s page", "name", "Omar"), Open = CA(CompanyDo.Go, "mgr:Hudson Warehouse~4", CompanyView.Managers),
            Selected = 0
        };
        q.Choices = new[]
        {
            new CompanyChoice
            {
                Name = w.T("reallife_co_q_c_renew", "Renew at the offered rent"), Button = w.T("reallife_co_q_renew_do", "Renew the lease"), Note = w.T("reallife_co_q_standing_note", "standing"), Act = CA(CompanyDo.LeaseAnswer, "Hudson Warehouse~4", 0),
                Lines = new[] { new CompanyConsequence { Text = w.F("reallife_co_q_rent_year", "About {amount} a year more in rent", "amount", "$6,000"), Tone = UiConsequence.Loss, Change = 1 } }
            },
            new CompanyChoice
            {
                Name = w.T("reallife_co_q_c_haggle", "Haggle for a smaller rise"), Button = w.T("reallife_co_q_haggle_do", "Haggle"), Note = w.T("reallife_co_q_once_note", "once"), Act = CA(CompanyDo.LeaseAnswer, "Hudson Warehouse~4", 4),
                Lines = new[] { new CompanyConsequence { Text = w.T("reallife_co_q_haggle_line", "The landlord may meet you half way, or hold firm"), Tone = 0, Change = 0 } }
            },
            new CompanyChoice
            {
                Name = w.T("reallife_co_q_c_hold", "Hold over, month to month"), Button = w.T("reallife_co_q_hold_do", "Hold over"), Note = w.T("reallife_co_q_nocost_note", "no cost"), Act = CA(CompanyDo.LeaseAnswer, "Hudson Warehouse~4", 1),
                Lines = new[] { new CompanyConsequence { Text = w.T("reallife_co_q_hold_line", "A premium on the rent, and no term to rely on"), Tone = -1, Change = 1 } }
            }
        };
        return q;
    }

    private static void CompanyOverview(CompanyFacts f, CompanyWords w, string state)
    {
        CompanyOverviewFacts o = f.Overview;
        bool one = state == "overview_one";
        bool quiet = state == "overview_quiet";
        if (state == "question" || state == "lease_question" || state == "gap_question" || state == "deep_question")
        {
            f.Question = state == "lease_question" ? LeaseQuestion(w) : state == "gap_question" ? PlainQuestion(w, false) : state == "deep_question" ? PlainQuestion(w, true) : RaiseQuestion(w);
        }
        o.Line = w.F("reallife_co_head_line", "Head office {place}, {staff} staff, {managers} managers, run since Day {day}", "place", one ? "12 Elm Street, Brooklyn" : "7 5th Avenue, Midtown",
            "staff", one ? "2" : "31", "managers", one ? "0" : "6", "day", "1");
        o.ResultLabel = w.T("reallife_co_result_label", "Today's result");
        if (one)
        {
            o.Businesses = new[] { new CompanyBusiness { Place = "Corner Cafe~12", Name = "Corner Cafe", Mark = "CC", HasBooks = false, Waiting = true } };
            o.Result = null;
            o.ResultNote = w.F("reallife_co_after_day", "after day {day}", "day", "7");
            o.Waiting = new[]
            {
                new CompanyWaiting { Name = "Signage permit is missing", Line = SF("{business} trades without it; an inspector may call", "business", "Corner Cafe"), Icon = UiIcon.Receipt, Open = CA(CompanyDo.Go, null, CompanyView.Tax), OpenLabel = w.T("reallife_co_open", "Open") },
                new CompanyWaiting { Name = "Four hours with nobody on shift", Line = SF("{business}, Tuesday and Thursday", "business", "Corner Cafe"), Icon = UiIcon.Calendar, Open = CA(CompanyDo.Go, null, CompanyView.Rota), OpenLabel = w.T("reallife_co_open", "Open") }
            };
            o.WaitingCount = "2";
            o.Next = new CompanyWaiting
            {
                Name = w.T("reallife_co_next_title", "Next: hire a manager"), Line = w.T("reallife_co_next_line", "A manager answers what you hand over, and asks you what is past their limit."),
                Icon = UiIcon.People, OpenLabel = w.T("reallife_co_open_managers", "Open Managers"), Open = CA(CompanyDo.Go, null, CompanyView.Managers)
            };
            o.Shop = ShopCard(w, true);
            o.Tools = new[] { CB(w.T("reallife_co_open_bank", "Open your bank"), UiButtonKind.Secondary, UiAction.Count, CA(CompanyDo.OpenBank), UiIcon.Bank) };
            return;
        }
        o.Businesses = new[]
        {
            new CompanyBusiness { Place = "Speedy Bites~7", Name = "Speedy Bites", Mark = "SB", Takings = CM(4860), Costs = CM(3720), Result = "+" + CM(1140), ResultTone = 1, HasBooks = true },
            new CompanyBusiness { Place = "Bloom & Co~214", Name = "Bloom & Co", Mark = "BC", Takings = CM(1980), Costs = CM(1760), Result = "+" + CM(220), ResultTone = 1, HasBooks = true },
            new CompanyBusiness { Place = "Pioneer Game Studio~9", Name = "Pioneer Game Studio", Mark = "PG", Takings = CM(2240), Costs = CM(2310), Result = "-" + CM(70), ResultTone = -1, HasBooks = true },
            new CompanyBusiness { Place = "Hudson Warehouse~4", Name = "Hudson Warehouse", Mark = "HW", Takings = CM(720), Costs = CM(770), Result = "-" + CM(50), ResultTone = -1, HasBooks = true }
        };
        o.Result = "+" + CM(1240);
        o.ResultTone = 1;
        o.Spark = new float[] { 600, 820, 740, 1010, 900, 1100, 980, 1180, 1050, 1240 };
        o.TotalTakings = CM(9800);
        o.TotalCosts = CM(8560);
        o.TotalResult = "+" + CM(1240);
        o.TotalTone = 1;
        o.CostLabels = new[] { w.F("reallife_co_cost_wages_staff", "Wages, {count} staff", "count", "31"), w.T("reallife_co_cost_stock", "Stock and ingredients"), w.T("reallife_co_cost_rent", "Rent and bills"), w.T("reallife_co_cost_marketing", "Marketing") };
        o.CostValues = new[] { CM(4930), CM(2360), CM(960), CM(310) };
        o.CostTotal = CM(8560);
        o.Managers = new[]
        {
            new CompanyManagerToday { Name = "Ray Walsh", Place = "Speedy Bites", Figures = w.F("reallife_co_decided_asked", "{decided} decided, {asked} asked you", "decided", "14", "asked", "1"), Hue = 0.55f, Ring = UiRole.Ok, Open = CA(CompanyDo.Go, "mgr:Speedy Bites~7", CompanyView.Managers) },
            new CompanyManagerToday { Name = "Tessa Brandt", Place = "Bloom & Co", Figures = w.F("reallife_co_decided_asked", "{decided} decided, {asked} asked you", "decided", "6", "asked", "1"), Hue = 0.9f, Ring = UiRole.Ok, Open = CA(CompanyDo.Go, "mgr:Bloom & Co~214", CompanyView.Managers) },
            new CompanyManagerToday { Name = "Omar Haddad", Place = "Hudson Warehouse", Figures = w.F("reallife_co_decided_asked", "{decided} decided, {asked} asked you", "decided", "9", "asked", "1"), Hue = 0.75f, Ring = UiRole.Ok, Open = CA(CompanyDo.Go, "exe:2", CompanyView.Managers) }
        };
        o.Tools = new[]
        {
            CB(w.T("reallife_co_open_cleaning", "Open cleaning"), UiButtonKind.Secondary, UiAction.Count, CA(CompanyDo.OpenCleaning), UiIcon.Package),
            CB(w.T("reallife_co_open_bank", "Open your bank"), UiButtonKind.Secondary, UiAction.Count, CA(CompanyDo.OpenBank), UiIcon.Bank),
            CB(w.T("reallife_co_open_stocks", "Open in Stocks"), UiButtonKind.Secondary, UiAction.Count, CA(CompanyDo.OpenStocks), UiIcon.ChartLineUp)
        };
        o.Note = SF("Listed as {code}: {price} a share, {change} today", "code", "PIO", "price", "$41.20", "change", "+1.8%");
        o.Districts = new CompanyFilterBar { Options = new[] { w.T("reallife_co_f_all_districts", "All districts"), "Midtown", "Hell's Kitchen" }, Chosen = 0, Shift = 0, Bits = 6 };
        o.DistrictFilter = 0;
        o.TotalLabel = w.T("reallife_co_all_businesses", "All businesses");
        o.Done = new[]
        {
            new CompanyDone { When = "08:10", Text = SF("{name} reordered buns and patties for {business}", "name", "Ray Walsh", "business", "Speedy Bites"), Money = "-" + CM(410), Tone = -1 },
            new CompanyDone { When = "09:30", Text = SF("{name} lowered rose prices {percent} on slow stock", "name", "Tessa Brandt", "percent", "6%"), Money = "+" + CM(85), Tone = 1 },
            new CompanyDone { When = "10:45", Text = SF("{name} booked a courier from Harbourside Freight HSF", "name", "Meera Iyer"), Money = "-" + CM(240), Tone = -1 },
            new CompanyDone { When = "11:20", Text = SF("{name} moved {person} from the fryer to the grill", "name", "Ray Walsh", "person", "Lena Adler"), Money = "no cost", Tone = 0 },
            new CompanyDone { When = "11:55", Text = SF("{name} turned away a supplier who asked {percent} more", "name", "Tessa Brandt", "percent", "12%"), Money = "+" + CM(140), Tone = 1 },
            new CompanyDone { When = "13:05", Text = SF("{name} gave {person} the whole raise, {amount} an hour", "name", "Ray Walsh", "person", "Lena Adler", "amount", "$0.50"), Money = "-" + CM(4), Tone = -1 },
            new CompanyDone { When = "14:20", Text = SF("{name} booked a flyer week for {business}", "name", "Tessa Brandt", "business", "Bloom & Co"), Money = "-" + CM(180), Tone = -1 },
            new CompanyDone { When = "15:40", Text = SF("{name} wrote the week's rota for {business}", "name", "Ray Walsh", "business", "Speedy Bites"), Money = "no cost", Tone = 0 }
        };
        if (quiet)
        {
            o.Waiting = new CompanyWaiting[0];
            return;
        }
        o.Lead = RaiseQuestion(w, state == "overview_big");
        o.Lead.Kicker = w.F("reallife_co_needs_you", "Needs you now, {manager}, {place}", "manager", "Ray Walsh", "place", "Speedy Bites");
        o.Waiting = new[]
        {
            new CompanyWaiting { Name = "Meera Iyer, " + "Operations", Line = SF("{business} lease renews in {days} days: {rent} a month, up {rise}", "business", "Hudson Warehouse", "days", "9", "rent", "$6,800", "rise", "8%"),
                Hue = 0.75f, Ring = UiRole.Warn, Open = CA(CompanyDo.Go, "card", 2), OpenLabel = w.T("reallife_co_open", "Open") },
            new CompanyWaiting { Name = "Tessa Brandt, Bloom & Co", Line = SF("Order {count} more tulips for Saturday, {amount}", "count", "400", "amount", "$620"), Hue = 0.9f, Ring = UiRole.Warn,
                Open = CA(CompanyDo.Go, "mgr:Bloom & Co~214", CompanyView.Managers), OpenLabel = w.T("reallife_co_open", "Open") }
        };
        o.WaitingCount = "2";
        if (state == "overview_shop")
        {
            f.Business = ShopCard(w);
        }
    }

    // A business as a card, the way CompanyLive.BusinessCard makes it: as its own page (a head with the figures beside the name, two columns) or, for a company with one business, as a panel.
    private static CompanyCard ShopCard(CompanyWords w, bool panel = false)
    {
        var c = new CompanyCard();
        var blocks = new List<CompanyBlock>();
        if (!panel)
        {
            c.Kicker = w.T("reallife_co_business", "Business");
            c.Title = "Speedy Bites";
            c.Sub = SF("7 5th Avenue, {district}", "district", "Midtown");
            c.Icon = UiIcon.ForkKnife;
            c.Chips = new[] { "Waiting for you", "Open" };
            c.ChipTones = new[] { UiRole.Warn, UiRole.Ok };
            c.FactLabels = new[] { w.T("reallife_co_f_staff", "Staff"), w.T("reallife_co_f_takings", "Takings"), w.T("reallife_co_f_profit", "Profit"), w.T("reallife_co_f_standing", "Standing") };
            c.FactValues = new[] { "9", CM(4860), "+" + CM(1140), "72" };
            c.Back = new[] { CB(w.T("reallife_co_back", "Back"), UiButtonKind.Secondary, UiAction.Count, CA(CompanyDo.Go, "", CompanyView.Overview), UiIcon.Back) };
        }
        else
        {
            blocks.Add(new CompanyBlock { Kind = CompanyBlockKind.Figures, Options = new[] { w.T("reallife_co_f_staff", "Staff"), w.T("reallife_co_f_standing", "Standing") }, Values = new[] { "2", "52" } });
        }
        blocks.Add(new CompanyBlock { Kind = CompanyBlockKind.Section, Text = w.T("reallife_co_sec_state", "How it stands") });
        blocks.Add(new CompanyBlock { Kind = CompanyBlockKind.Row, Text = w.T("reallife_co_r_manager", "Manager"), Value = panel ? w.T("reallife_co_none", "None") : "Ray Walsh" });
        blocks.Add(new CompanyBlock { Kind = CompanyBlockKind.Row, Text = w.T("reallife_co_r_district_income", "District income"), Value = panel ? "$96" : "$113" });
        blocks.Add(new CompanyBlock { Kind = CompanyBlockKind.Row, Text = w.T("reallife_co_r_lease", "Lease"), Value = w.F("reallife_co_lease_renews", "Day {day}, {rent} a month", "day", panel ? "301" : "238", "rent", panel ? "$1,980" : "$6,300") });
        blocks.Add(new CompanyBlock { Kind = CompanyBlockKind.Row, Text = w.T("reallife_co_r_rival", "Nearest rival"), Value = w.F("reallife_co_rival_line", "{name}, about {amount} a day (the game's estimate)", "name", panel ? "Brooklyn Brew" : "Midtown Grill", "amount", panel ? "$460" : "$4,100") });
        if (panel)
        {
            blocks.Add(new CompanyBlock { Kind = CompanyBlockKind.Note, Text = SF("The game has no statement for this business yet. Figures come after its first full day.") });
        }
        else
        {
            blocks.Add(new CompanyBlock { Kind = CompanyBlockKind.Row, Text = w.T("reallife_co_r_hidden", "Cash off the books"), Value = "0%" });
        }
        blocks.Add(new CompanyBlock { Kind = CompanyBlockKind.Section, Text = w.T("reallife_co_r_licences", "Licences") });
        blocks.Add(new CompanyBlock
        {
            Kind = CompanyBlockKind.Items, Items = panel
                ? new[]
                {
                    new CompanyItem { Title = "Hygiene certificate", Icon = UiIcon.Drop, Sub = "Day 140", Chip = w.T("reallife_co_row_lic_valid", "Valid"), ChipTone = UiRole.Ok, Grade = -1, Press = true, Act = CA(CompanyDo.Go, "lic:Corner Cafe~12:0", CompanyView.Tax, CompanyView.SubLicence) },
                    new CompanyItem { Title = "Signage permit", Icon = UiIcon.Tag, Chip = w.T("reallife_co_row_lic_missing", "Missing"), ChipTone = UiRole.Danger, Grade = -1, Press = true, Act = CA(CompanyDo.Go, "lic:Corner Cafe~12:3", CompanyView.Tax, CompanyView.SubLicence) }
                }
                : new[]
                {
                    new CompanyItem { Title = "Hygiene certificate", Icon = UiIcon.Drop, Sub = "Day 260", Chip = w.T("reallife_co_row_lic_auto", "Renews itself"), ChipTone = UiRole.Ok, Grade = -1, Press = true, Act = CA(CompanyDo.Go, "lic:Speedy Bites~7:0", CompanyView.Tax, CompanyView.SubLicence) },
                    new CompanyItem { Title = "Fire safety", Icon = UiIcon.Fire, Sub = "Day 217", Chip = w.T("reallife_co_row_lic_soon", "Runs out soon"), ChipTone = UiRole.Warn, Grade = -1, Press = true, Act = CA(CompanyDo.Go, "lic:Speedy Bites~7:4", CompanyView.Tax, CompanyView.SubLicence) },
                    new CompanyItem { Title = "Signage permit", Icon = UiIcon.Tag, Chip = w.T("reallife_co_row_lic_missing", "Missing"), ChipTone = UiRole.Danger, Grade = -1, Press = true, Act = CA(CompanyDo.Go, "lic:Speedy Bites~7:3", CompanyView.Tax, CompanyView.SubLicence) }
                }
        });
        if (!panel)
        {
            blocks.Add(new CompanyBlock { Kind = CompanyBlockKind.Break });
        }
        blocks.Add(new CompanyBlock { Kind = CompanyBlockKind.Section, Text = w.T("reallife_co_r_cover", "Cover held") });
        blocks.Add(new CompanyBlock
        {
            Kind = CompanyBlockKind.Items, Items = new[]
            {
                new CompanyItem { Title = "Fire", Icon = UiIcon.Fire, Chip = panel ? w.T("reallife_ibox_chip_not_covered", "Not covered") : w.T("reallife_ibox_chip_covered", "Covered"), ChipTone = panel ? UiRole.Warn : UiRole.Ok, Grade = -1, Press = true,
                    Act = CA(CompanyDo.Go, panel ? "pol:Corner Cafe~12:0" : "pol:Speedy Bites~7:0", CompanyView.Cover) },
                new CompanyItem { Title = "Theft", Icon = UiIcon.Lock, Chip = w.T("reallife_ibox_chip_not_covered", "Not covered"), ChipTone = UiRole.Warn, Grade = -1, Press = true,
                    Act = CA(CompanyDo.Go, panel ? "pol:Corner Cafe~12:1" : "pol:Speedy Bites~7:1", CompanyView.Cover) }
            }
        });
        blocks.Add(new CompanyBlock { Kind = CompanyBlockKind.Link, Text = w.T("reallife_co_r_campaign", "Campaign"), Value = w.T("reallife_co_none", "None"), Act = CA(CompanyDo.Go, panel ? "biz:Corner Cafe~12" : "biz:Speedy Bites~7", CompanyView.Marketing) });
        blocks.Add(new CompanyBlock { Kind = CompanyBlockKind.Section, Text = w.T("reallife_co_sec_do", "What you can do here") });
        blocks.Add(new CompanyBlock
        {
            Kind = CompanyBlockKind.Buttons, Buttons = new[]
            {
                CB(w.T("reallife_co_open_bizman", "Open in BizMan"), UiButtonKind.Secondary, UiAction.Count, CA(CompanyDo.BizMan, panel ? "Corner Cafe~12" : "Speedy Bites~7"), UiIcon.Locate),
                CB(w.T("reallife_co_sell", "Sell this business"), UiButtonKind.Secondary, UiAction.Count, CA(CompanyDo.SellBusiness, panel ? "Corner Cafe~12" : "Speedy Bites~7"), UiIcon.Handshake)
            }
        });
        if (!panel)
        {
            string[][] crew =
            {
                new[] { "Dev Kapoor", "Customer service", "$17.00", "0.35" }, new[] { "Lena Adler", "Cooking", "$16.50", "0.1" }, new[] { "Rui Tanaka", "Customer service", "$15.90", "0.5" },
                new[] { "Ines Duarte", "Cooking", "$16.20", "0.15" }, new[] { "Hana Sato", "Cleaning", "$15.60", "0.8" }, new[] { "Leo Martin", "Cooking", "$16.00", "0.3" },
                new[] { "Maya Okafor", "Customer service", "$18.20", "0.6" }, new[] { "Tomas Reyes", "Cleaning", "$15.40", "0.8" }, new[] { "Sana Malik", "Cooking", "$24.00", "0.25" }
            };
            var rows = new CompanyItem[crew.Length];
            for (int i = 0; i < crew.Length; i++)
            {
                rows[i] = new CompanyItem { Title = crew[i][0], Sub = crew[i][1], Face = true, Hue = float.Parse(crew[i][3], System.Globalization.CultureInfo.InvariantCulture), Ring = UiRole.Ok,
                    Figure = w.F("reallife_rota_hours_n", "{hours} h", "hours", "8"), Note = crew[i][2] + " / h", Grade = -1, Press = true, Act = CA(CompanyDo.Go, "emp:" + i, CompanyView.Staff) };
            }
            blocks.Add(new CompanyBlock { Kind = CompanyBlockKind.Section, Text = w.T("reallife_co_staff_people", "Your people"), Value = "9" });
            blocks.Add(new CompanyBlock { Kind = CompanyBlockKind.Items, Items = rows });
        }
        if (panel)
        {
            CompanyFace.AsideSections(blocks, w.T("reallife_co_r_licences", "Licences"), w.T("reallife_co_r_cover", "Cover held"));
        }
        c.Blocks = blocks.ToArray();
        return c;
    }
}
