using System.Collections.Generic;
using RealLifeOverhaul.Ui;

// The Company app's Rota, Marketing, Cover and Tax tabs. Samples' own names; the live source reads the player's businesses.
internal static partial class UiSamples
{
    // ------------------------------------------------------------------------------------------------- Rota

    private static void CompanyRota(CompanyFacts f, CompanyWords w, string state)
    {
        CompanyRotaFacts r = f.Rota;
        if (state == "rota_off")
        {
            r.PartOff = true;
            r.OffTitle = w.T("reallife_rota_tab", "Rota");
            r.OffBody = w.T("reallife_rota_off", "The rota planner is switched off in the mod's settings.");
            return;
        }
        bool one = state == "rota_one";
        var rows = new List<CompanyRow>();
        if (one)
        {
            rows.Add(new CompanyRow { Id = "p:Corner Cafe~12", Title = "Corner Cafe", Sub = w.F("reallife_rota_row_open", "Open {hours} hours", "hours", "84"), Icon = UiIcon.Calendar, Figure = w.F("reallife_co_row_gap_short", "{hours} h short", "hours", "14"), FigureTone = UiRole.WarnText, Note = w.F("reallife_co_tree_staff", "{count} staff", "count", "2") });
        }
        else
        {
            rows.Add(new CompanyRow { Id = "p:Speedy Bites~7", Title = "Speedy Bites", Sub = "Restaurant, " + w.F("reallife_rota_row_open", "Open {hours} hours", "hours", "112"), Icon = UiIcon.Calendar, Figure = w.T("reallife_rota_row_covered", "Covered"), FigureTone = UiRole.OkText, Note = w.F("reallife_co_tree_staff", "{count} staff", "count", "9") });
            rows.Add(new CompanyRow { Id = "pl:Speedy Bites~7:1", Title = "Standard week", Sub = w.T("reallife_rota_repeat_every", "Every week"), Icon = UiIcon.ClipboardText, Dim = true, Chip = w.T("reallife_rota_in_use", "In use"), ChipTone = UiRole.Ok });
            rows.Add(new CompanyRow { Id = "pl:Speedy Bites~7:2", Title = "Holiday week", Sub = w.F("reallife_rota_plan_range", "Days {from} to {to}", "from", "220", "to", "226"), Icon = UiIcon.ClipboardText, Dim = true });
            rows.Add(new CompanyRow { Id = "p:Bloom & Co~214", Title = "Bloom & Co", Sub = "Shop, " + w.F("reallife_rota_row_open", "Open {hours} hours", "hours", "72"), Icon = UiIcon.Calendar, Figure = w.F("reallife_co_row_gap_short", "{hours} h short", "hours", "6"), FigureTone = UiRole.WarnText, Note = w.F("reallife_co_tree_staff", "{count} staff", "count", "5") });
            rows.Add(new CompanyRow { Id = "p:Hudson Warehouse~4", Title = "Hudson Warehouse", Sub = "Warehouse", Icon = UiIcon.Calendar, Figure = w.T("reallife_rota_row_none", "No rota"), FigureTone = UiRole.Muted, Note = w.F("reallife_co_tree_staff", "{count} staff", "count", "4") });
        }
        r.Places = new CompanyList { Title = w.T("reallife_rota_tab", "Rota"), Count = one ? "1" : "4", Rows = rows.ToArray(), Chosen = 0 };
        var p = new CompanyRotaPlace { Id = one ? "Corner Cafe~12" : "Speedy Bites~7", Name = one ? "Corner Cafe" : "Speedy Bites", Sub = (one ? "Cafe, " : "Restaurant, ") + w.F("reallife_rota_row_open", "Open {hours} hours", "hours", one ? "84" : "112") };
        if (state == "rota_refused")
        {
            r.Places.Chosen = 3;
            p.Name = "Hudson Warehouse";
            p.Refused = SF("A warehouse has no rota: the game gives its drivers their vehicles itself.");
            r.Place = p;
            return;
        }
        p.Weekly = w.F("reallife_rota_weekly", "Week {week}, open {hours} hours", "week", "31", "hours", one ? "84" : "112");
        p.PlanNames = new[] { "Standard week", "Holiday week" };
        p.PlanChosen = 0;
        p.Ahead = one ? null : w.F("reallife_co_plan_ahead", "{plan} holds from day {from} to day {to}: up to {cap} each, and {floor} crew hours of floor a week against this plan", "plan", "Holiday week", "from", "220", "to", "226",
            "cap", w.F("reallife_rota_hours_n", "{hours} h", "hours", "32"), "floor", "+8");
        p.RepeatOptions = new[] { w.T("reallife_rota_repeat_every", "Every week"), w.T("reallife_rota_repeat_odd", "Odd weeks"), w.T("reallife_rota_repeat_even", "Even weeks") };
        p.Repeat = 0;
        p.RepeatActs = new[] { CA(CompanyDo.PlanChange, p.Id, 1, 3, 0), CA(CompanyDo.PlanChange, p.Id, 1, 3, 1), CA(CompanyDo.PlanChange, p.Id, 1, 3, 2) };
        p.HoursValue = w.F("reallife_rota_hours_n", "{hours} h", "hours", "40");
        p.HoursLess = true;
        p.HoursMore = true;
        p.HoursLessAct = CA(CompanyDo.PlanChange, p.Id, 1, 4, 36);
        p.HoursMoreAct = CA(CompanyDo.PlanChange, p.Id, 1, 4, 44);
        p.HoursTypeAct = CA(CompanyDo.PlanChange, p.Id, 1, 4);
        p.Overtime = false;
        p.OvertimeAct = CA(CompanyDo.PlanChange, p.Id, 1, 5, 1);
        p.Edit = CB(w.T("reallife_rota_edit", "Edit"), UiButtonKind.Secondary, UiAction.Count, CA(CompanyDo.Go, null, CompanyView.Rota, CompanyView.SubEdit), UiIcon.Sliders);
        p.Copy = CB(w.T("reallife_rota_copy", "Copy plan"), UiButtonKind.Secondary, UiAction.Count, CA(CompanyDo.PlanCopy, p.Id, 1), UiIcon.ClipboardText);
        p.Back = CB(w.T("reallife_rota_back", "Back to the last rota"), UiButtonKind.Secondary, UiAction.Count, CA(CompanyDo.RotaBack, p.Id), UiIcon.Back, false, null, true);
        p.Fill = CB(w.T("reallife_rota_fill", "Fill the gaps"), UiButtonKind.Primary, UiAction.Alt, CA(CompanyDo.RotaFill, p.Id, -1), UiIcon.Check);
        p.ModeOptions = new[] { w.F("reallife_rota_mode_auto", "Auto: {mode}", "mode", w.T("reallife_rota_mode_staff", "Staff first")), w.T("reallife_rota_mode_staff", "Staff first"), w.T("reallife_rota_mode_lean", "Lean") };
        p.ModeActs = new[] { CA(CompanyDo.RotaMode, p.Id, 0), CA(CompanyDo.RotaMode, p.Id, 1), CA(CompanyDo.RotaMode, p.Id, 2) };
        p.Mode = 0;
        p.DayNames = new[] { "Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun" };
        p.Day = state == "rota_day" ? 1 : 1;
        string[] names = one ? new[] { "Ana Costa", "Jin Park" } : new[] { "Dev Kapoor", "Lena Adler", "Maya Okafor", "Tomas Reyes", "Sana Malik", "Rui Tanaka" };
        string[] roles = one ? new[] { "Barista", "Cook" } : new[] { "Customer service", "Cooking", "Customer service", "Cleaning", "Cooking", "Customer service" };
        var people = new List<CompanyRotaPerson>();
        for (int i = 0; i < names.Length; i++)
        {
            var shifts = new string[7];
            var tones = new UiRole[7];
            for (int d = 0; d < 7; d++)
            {
                bool works = (d + i) % 7 != 6 && (d + i) % 4 != 3;
                shifts[d] = works ? (i % 2 == 0 ? "07-15" : "11-19") : null;
                tones[d] = works && i == 4 && d == 5 ? UiRole.WarnText : UiRole.None;
            }
            people.Add(new CompanyRotaPerson { Name = names[i], Role = roles[i], Hue = 0.1f + i * 0.14f, Ring = UiRole.Ok, Shifts = shifts, Tones = tones, Hours = (32 + i * 2) + "h", HoursTone = i == 4 ? UiRole.WarnText : UiRole.None });
        }
        p.People = people.ToArray();
        p.DayTotals = new[] { "40h", "40h", "32h", "32h", "40h", "48h", "24h" };
        p.TotalHours = "256h";
        p.SummaryLabels = new[] { w.T("reallife_rota_sum_served", "Served"), w.T("reallife_rota_sum_lost", "Lost"), w.T("reallife_rota_sum_wages_label", "Wages"), w.T("reallife_rota_sum_net_label", "Net") };
        p.SummaryValues = new[] { "1,420", "36", CM(4210), CM(2480) };
        p.SummaryTones = new[] { UiRole.None, UiRole.WarnText, UiRole.None, UiRole.OkText };
        p.DayTitle = w.F("reallife_rota_day_title_short", "{day}, hour by hour", "day", "Tuesday");
        p.DayShort = "Tuesday";
        p.DayNote = "07:00 - 23:00";
        p.HourLabels = new[] { "07", "08", "09", "10", "11", "12", "13", "14", "15", "16", "17", "18", "19", "20" };
        p.OnShift = new[] { 2, 2, 3, 3, 4, 5, 5, 4, 4, 3, 3, 4, 4, 2 };
        p.Needed = new[] { 2, 2, 3, 3, 4, 5, 5, 5, 4, 3, 3, 4, 4, 3 };
        p.Legend = new[] { w.T("reallife_rota_legend_on", "On shift"), w.T("reallife_rota_legend_short", "Short"), w.T("reallife_rota_legend_need", "Needed") };
        p.NeedsTitle = w.T("reallife_rota_needs", "Needs by time");
        p.NeedWhen = new[] { "07-11", "11-15", "15-23" };
        p.NeedWho = new[] { w.F("reallife_rota_needs_crew", "{count} on shift", "count", "2"), w.F("reallife_rota_needs_crew", "{count} on shift", "count", "4"), w.F("reallife_rota_needs_crew", "{count} on shift", "count", "3") };
        if (state == "rota_day")
        {
            var lanes = new List<UiBoardLane>();
            var shifts = new List<UiBoardShift>();
            for (int i = 0; i < names.Length; i++)
            {
                lanes.Add(new UiBoardLane { Name = names[i], Sub = roles[i], Hue = 0.1f + i * 0.14f });
                shifts.Add(new UiBoardShift { Lane = i, Start = (i % 2 == 0 ? 7 : 11) * 60, End = (i % 2 == 0 ? 15 : 19) * 60, Name = names[i], Hue = 0.1f + i * 0.14f, Ring = UiRole.Ok, Span = i % 2 == 0 ? "07:00 - 15:00" : "11:00 - 19:00" });
            }
            var cover = new string[24];
            var shortH = new bool[24];
            for (int h = 0; h < 24; h++)
            {
                int k = h - 7;
                cover[h] = k >= 0 && k < p.OnShift.Length ? p.OnShift[k] + "/" + p.Needed[k] : null;
                shortH[h] = k >= 0 && k < p.OnShift.Length && p.OnShift[k] < p.Needed[k];
            }
            var hours = new string[18];
            for (int h = 0; h < hours.Length; h++)
            {
                hours[h] = (6 + h).ToString("00") + ":00";
            }
            p.Board = new UiBoardModel
            {
                Days = p.DayNames, Day = 1, FirstHour = 6, LastHour = 23, Hours = hours, OpenFrom = 7 * 60, OpenTo = 23 * 60, Lanes = lanes.ToArray(), Shifts = shifts.ToArray(), Faces = true,
                CoverTitle = w.T("reallife_rota_cover_title", "Staff / needed"), Cover = cover, Short = shortH
            };
        }
        if (state == "rota_advice")
        {
            p.AdviceTitle = w.T("reallife_rota_advice_title", "The planner's advice");
            p.AdviceNote = w.F("reallife_rota_advice_hire", "{plan} would add {amount} a week.", "plan", "Hire 1 Customer service", "amount", "$310");
            p.Rungs = new[]
            {
                new CompanyRung { Name = w.T("reallife_rota_plan_now", "As it is"), Line = w.T("reallife_rota_needs_nothing", "Nothing to change"), Hours = w.F("reallife_rota_plan_hours", "{hours} hours, {manned} manned", "hours", "112", "manned", "104"), Net = CM(2480),
                    Use = CB(w.T("reallife_rota_use_plan", "Use this plan"), UiButtonKind.Secondary, UiAction.Count, CA(CompanyDo.RotaFill, p.Id, 0), UiIcon.Check) },
                new CompanyRung { Name = w.T("reallife_rota_plan_best", "Best with the staff"), Line = w.T("reallife_rota_needs_rota", "A new rota for the same hours"), Hours = w.F("reallife_rota_plan_hours_all", "{hours} hours", "hours", "112"), Net = CM(2640), Gain = "+" + CM(160),
                    Use = CB(w.T("reallife_rota_use_plan", "Use this plan"), UiButtonKind.Secondary, UiAction.Count, CA(CompanyDo.RotaFill, p.Id, 1), UiIcon.Check) },
                new CompanyRung { Name = w.F("reallife_rota_plan_hire", "Hire {count} {who}", "count", "1", "who", "Customer service"), Line = w.F("reallife_rota_needs_hire", "Hire a {who} at {wage} an hour", "who", "Customer service", "wage", "$18.00"), Hours = w.F("reallife_rota_plan_hours_all", "{hours} hours", "hours", "112"), Net = CM(2790), Gain = "+" + CM(150), Advised = true,
                    Use = CB(w.T("reallife_rota_use_after", "Use after hiring"), UiButtonKind.Secondary, UiAction.Count, CA(CompanyDo.RotaFill, p.Id, 2), UiIcon.Check, true, w.T("reallife_rota_hire_where", "Hire them first, from the place's job board.")) }
            };
            p.Beside = new[]
            {
                CSec(w.T("reallife_rota_head_your_people", "Your people")),
                CRow("Dev Kapoor, 20 to 40 hours a week", w.F("reallife_rota_person_hours", "{now} h now, {plan} h in the plan", "now", "34", "plan", "36"), UiRole.OkText),
                CRow("Sana Malik, no work after 18:00", w.F("reallife_rota_range_out", "Asks {asks}, plan gives {hours}", "asks", "20 to 32", "hours", "36"), UiRole.WarnText),
                CNote(w.F("reallife_rota_missing", "A second {station} station would pay for itself.", "station", "Grill") + " " + w.F("reallife_rota_missing_what", "{customers} customers a week are turned away.", "customers", "140"))
            };
        }
        if (state == "rota_edit" || state == "rota_edit_other")
        {
            bool other = state == "rota_edit_other";
            var c = new CompanyCard
            {
                Kicker = p.Name, Title = other ? "Quiet week" : "Standard week", Icon = UiIcon.Calendar, Chips = new[] { other ? w.T("reallife_rota_not_in_use", "Not in use") : w.T("reallife_rota_in_use", "In use") },
                ChipTones = new[] { other ? UiRole.Muted : UiRole.Ok }, Back = CBack(w, null, CompanyView.Rota)
            };
            c.Blocks = new[]
            {
                new CompanyBlock { Kind = CompanyBlockKind.Field, Text = w.T("reallife_rota_plan_name", "Name"), Value = other ? "Quiet week" : "Standard week", Sub = w.T("reallife_rota_plan_name_hint", "Up to 24 letters"), Act = CA(CompanyDo.PlanChange, p.Id, 1, 0, 0, "Standard week") },
                new CompanyBlock { Kind = CompanyBlockKind.Stepper, Text = w.T("reallife_rota_first_day", "First day it holds for"), Sub = w.T("reallife_rota_range_hint", "Leave both empty for every week"), Value = w.T("reallife_rota_always", "Always"), CanLess = false, CanMore = true,
                    Act = CA(CompanyDo.PlanChange, p.Id, 1, 1, 0), Act2 = CA(CompanyDo.PlanChange, p.Id, 1, 1, 214), Act3 = CA(CompanyDo.PlanChange, p.Id, 1, 1) },
                CSec(w.T("reallife_rota_needs", "Needs by time"), w.F("reallife_rota_blocks_count", "{count} of {most}", "count", "3", "most", "4")),
                CNote(w.T("reallife_rota_needs_what", "A floor of crew in a block of hours, whatever the customers say.")),
                new CompanyBlock { Kind = CompanyBlockKind.Stepper, Text = "07-11", Value = w.F("reallife_rota_needs_crew", "{count} on shift", "count", "2"), CanLess = true, CanMore = true, Act = CA(CompanyDo.PlanChange, p.Id, 1, 7, 1, "0"), Act2 = CA(CompanyDo.PlanChange, p.Id, 1, 7, 3, "0"), Act3 = CA(CompanyDo.PlanChange, p.Id, 1, 7, 0, "0") },
                new CompanyBlock { Kind = CompanyBlockKind.Stepper, Text = "11-15", Value = w.F("reallife_rota_needs_crew", "{count} on shift", "count", "4"), CanLess = true, CanMore = true, Act = CA(CompanyDo.PlanChange, p.Id, 1, 7, 3, "1"), Act2 = CA(CompanyDo.PlanChange, p.Id, 1, 7, 5, "1"), Act3 = CA(CompanyDo.PlanChange, p.Id, 1, 7, 0, "1") },
                CBtns(CB(w.T("reallife_rota_block_add", "Add a block"), UiButtonKind.Secondary, UiAction.Count, CA(CompanyDo.PlanChange, p.Id, 1, 6, 715), UiIcon.Plus),
                    CB(w.T("reallife_rota_block_remove", "Remove the last"), UiButtonKind.Secondary, UiAction.Count, CA(CompanyDo.PlanChange, p.Id, 1, 8, 0, "1"), UiIcon.Close, false, null, true)),
                other
                    ? CBtns(CB(w.T("reallife_rota_use_this", "Use this plan"), UiButtonKind.Primary, UiAction.Alt, CA(CompanyDo.PlanUse, p.Id, 2), UiIcon.Check),
                        CB(w.T("reallife_rota_plan_new", "New plan"), UiButtonKind.Secondary, UiAction.Count, CA(CompanyDo.PlanNew, p.Id), UiIcon.Plus),
                        CB(w.T("reallife_rota_plan_delete", "Delete this plan"), UiButtonKind.Secondary, UiAction.Count, CA(CompanyDo.PlanDelete, p.Id, 2), UiIcon.Close, false, null, true))
                    : CBtns(CB(w.T("reallife_rota_plan_new", "New plan"), UiButtonKind.Secondary, UiAction.Count, CA(CompanyDo.PlanNew, p.Id), UiIcon.Plus),
                        CB(w.T("reallife_rota_plan_delete", "Delete this plan"), UiButtonKind.Secondary, UiAction.Count, CA(CompanyDo.PlanDelete, p.Id, 1), UiIcon.Close, false, null, true))
            };
            r.Edit = c;
        }
        r.Place = p;
    }

    // ------------------------------------------------------------------------------------------------- Marketing and Cover

    private static void CompanyMarketing(CompanyFacts f, CompanyWords w, string state)
    {
        CompanyPage p = f.Marketing;
        if (state == "marketing_off")
        {
            p.PartOff = true;
            p.OffTitle = w.T("reallife_co_tab_marketing", "Marketing");
            p.OffBody = w.T("reallife_co_part_off", "Switched off in the options, or stopped after an error. Nothing of it runs and nothing is charged.");
            return;
        }
        bool one = state == "marketing_one";
        var rows = new List<CompanyRow>();
        if (one)
        {
            rows.Add(new CompanyRow { Id = "push:Corner Cafe~12", Title = "Corner Cafe", Sub = w.T("reallife_co_none", "None"), Icon = UiIcon.Store });
        }
        else
        {
            rows.Add(new CompanyRow { Id = "push:Speedy Bites~7", Title = "Speedy Bites", Sub = "Radio, Day 219", Icon = UiIcon.Megaphone, Figure = "+9%", FigureTone = UiRole.OkText, Note = CM(620), Chip = w.T("reallife_ibox_chip_running", "Running"), ChipTone = UiRole.Ok });
            rows.Add(new CompanyRow { Id = "push:Bloom & Co~214", Title = "Bloom & Co", Sub = w.T("reallife_co_none", "None"), Icon = UiIcon.Store });
            rows.Add(new CompanyRow { Id = "push:Pioneer Game Studio~9", Title = "Pioneer Game Studio", Sub = w.T("reallife_co_none", "None"), Icon = UiIcon.Store });
            rows.Add(new CompanyRow { Caption = true, Title = w.T("reallife_co_reports", "Reports") });
            rows.Add(new CompanyRow { Id = "rep:0", Title = "Flyers", Sub = "Bloom & Co, Day 201", Icon = UiIcon.ClipboardText, Figure = "+14%", FigureTone = UiRole.OkText, Note = w.F("reallife_co_row_extra", "{amount} extra", "amount", CM(210)) });
            rows.Add(new CompanyRow { Id = "rep:1", Title = "Discount week", Sub = "Speedy Bites, Day 188", Icon = UiIcon.ClipboardText, Figure = "+6%", FigureTone = UiRole.OkText, Note = w.F("reallife_co_row_extra", "{amount} extra", "amount", "-" + CM(40)) });
        }
        p.List = new CompanyList { Title = w.T("reallife_co_tab_marketing", "Marketing"), Count = one ? "1" : "3", Rows = rows.ToArray() };
        string pick = state == "marketing_running" ? "push:Speedy Bites~7" : state == "marketing_report" ? "rep:0" : state == "marketing_summary" ? null : one ? "push:Corner Cafe~12" : "push:Bloom & Co~214";
        for (int i = 0; i < rows.Count; i++)
        {
            p.List.Chosen = rows[i].Id == pick ? i : p.List.Chosen;
        }
        if (pick == null)
        {
            p.List.Chosen = -1;
            p.Top = new CompanyCard
            {
                Kicker = w.T("reallife_co_tab_marketing", "Marketing"), Title = w.T("reallife_co_marketing_title", "Campaigns"), Icon = UiIcon.Megaphone,
                Blocks = new[]
                {
                    CNote(w.T("reallife_co_push_intro", "A campaign draws more people to a business for some days.")), CSec(w.T("reallife_ibox_sec_how", "How it is measured")),
                    CNote(w.T("reallife_co_push_measure", "The customers and takings of the days before are set against the days during.")), CNote(w.T("reallife_co_push_game", "The game counts the effect in its own footfall.")),
                    CNote(w.T("reallife_co_push_pick", "Pick a business to book a campaign."))
                }
            };
            return;
        }
        var c = new CompanyCard { Kicker = w.T("reallife_co_tab_marketing", "Marketing"), Icon = UiIcon.Megaphone };
        if (pick == "rep:0")
        {
            c.Kicker = "Bloom & Co";
            c.Title = "Flyers";
            c.Icon = UiIcon.ClipboardText;
            c.Sub = w.F("reallife_co_report_days", "Days {from} to {to}, {days} days", "from", "198", "to", "201", "days", "4");
            c.Chips = new[] { "+34%" };
            c.ChipTones = new[] { UiRole.Ok };
            c.FactLabels = new[] { w.T("reallife_co_f_cost", "Cost"), w.T("reallife_co_f_extra", "Extra profit") };
            c.FactValues = new[] { CM(180), CM(210) };
            c.Blocks = new[]
            {
                CSec(w.T("reallife_ibox_sec_before_during", "Before and during")),
                new CompanyBlock { Kind = CompanyBlockKind.Compare, Text = w.T("reallife_co_f_customers", "Customers"), Value = "212", Sub = "242 (+14%)", Tone = UiRole.OkText, Options = new[] { w.T("reallife_co_report_before", "Before"), w.T("reallife_co_report_during", "During") } },
                new CompanyBlock { Kind = CompanyBlockKind.Compare, Text = w.T("reallife_co_f_takings", "Takings"), Value = CM(1480), Sub = CM(1690), Tone = UiRole.OkText },
                new CompanyBlock { Kind = CompanyBlockKind.Compare, Text = w.T("reallife_co_f_profit", "Profit"), Value = CM(240), Sub = CM(270), Tone = UiRole.OkText },
                new CompanyBlock { Kind = CompanyBlockKind.Break },
                CSec(w.T("reallife_ibox_sec_money", "The money")),
                CRow(w.T("reallife_co_f_extra", "Extra profit"), CM(210), UiRole.OkText), CRow(w.L("reallife_co_report_return", "Return on the cost"), "+34%", UiRole.OkText)
            };
        }
        else if (pick == "push:Speedy Bites~7")
        {
            c.Title = "Speedy Bites";
            c.Chips = new[] { w.T("reallife_ibox_chip_running", "Running") };
            c.ChipTones = new[] { UiRole.Ok };
            c.FactLabels = new[] { w.T("reallife_co_f_cost", "Cost"), w.T("reallife_co_f_effect", "Effect on customers") };
            c.FactValues = new[] { CM(620), "+9%" };
            c.Blocks = new[]
            {
                CSec(w.T("reallife_ibox_sec_campaign", "The campaign")), CRow(w.L("reallife_co_push_running", "Running"), "Radio"),
                new CompanyBlock { Kind = CompanyBlockKind.Meter, Text = w.F("reallife_co_push_progress", "Day {done} of {days}", "done", "2", "days", "4"), Value = w.F("reallife_co_push_left", "{count} days left", "count", "2"), Number = 50, Tone = UiRole.Ok },
                CNote(w.F("reallife_co_push_report_due", "A report comes on day {day}.", "day", "220")),
                CSec(w.T("reallife_co_push_earlier", "Earlier campaigns here")),
                new CompanyBlock { Kind = CompanyBlockKind.Items, Items = new[] { new CompanyItem { Title = "Discount week", Sub = "Day 188", Icon = UiIcon.ClipboardText, Figure = "+6%", FigureTone = UiRole.OkText, Note = w.F("reallife_co_row_extra", "{amount} extra", "amount", "-" + CM(40)), Grade = -1, Press = true, Act = CA(CompanyDo.Go, "rep:1", CompanyView.Marketing) } } }
            };
        }
        else
        {
            c.Title = one ? "Corner Cafe" : "Bloom & Co";
            c.FactLabels = new[] { w.T("reallife_co_f_cost", "Cost"), w.T("reallife_co_f_effect", "Effect on customers") };
            c.FactValues = new[] { CM(180), "+12%" };
            c.Blocks = new[]
            {
                CSec(w.T("reallife_ibox_sec_campaign", "The campaign")),
                new CompanyBlock { Kind = CompanyBlockKind.Choice, Text = w.T("reallife_co_push_kind", "Kind"), Options = new[] { "Flyers", "Radio", "Discount week" }, Chosen = 0, Acts = new[] { CA(CompanyDo.Go, "kind", 0), CA(CompanyDo.Go, "kind", 1), CA(CompanyDo.Go, "kind", 2) } },
                CNote(w.T("reallife_co_push_about_flyers", "Flyers through every door nearby.")),
                CRow(w.T("reallife_co_f_cost", "Cost"), CM(180)), CRow(w.L("reallife_co_push_length", "Length"), w.F("reallife_co_push_length_value", "{days} days from day {from}", "days", "4", "from", "215")),
                CRow(w.T("reallife_co_f_effect", "Effect on customers"), "+12%", UiRole.OkText),
                CNote(w.F("reallife_co_push_parts", "Its own {own}, your district {district}, the economy {economy}.", "own", "+10%", "district", "110%", "economy", "100%")),
                one ? CNote(string.Empty) : CSec(w.T("reallife_co_push_earlier", "Earlier campaigns here")),
                one ? CNote(string.Empty) : new CompanyBlock { Kind = CompanyBlockKind.Items, Items = new[] { new CompanyItem { Title = "Flyers", Sub = "Day 201", Icon = UiIcon.ClipboardText, Figure = "+14%", FigureTone = UiRole.OkText, Note = w.F("reallife_co_row_extra", "{amount} extra", "amount", CM(210)), Grade = -1, Press = true, Act = CA(CompanyDo.Go, "rep:0", CompanyView.Marketing) } } },
                CBtns(CB(w.T("reallife_co_push_book", "Book the campaign"), UiButtonKind.Primary, UiAction.Alt, CA(CompanyDo.CampaignBook, "Bloom & Co~214", 0), UiIcon.Check))
            };
        }
        p.Card = c;
    }

    private static void CompanyCover(CompanyFacts f, CompanyWords w, string state)
    {
        CompanyPage p = f.Cover;
        var rows = new List<CompanyRow>
        {
            new CompanyRow { Id = "claim:1", Title = "Speedy Bites", Sub = w.F("reallife_co_row_claim", "Claim, {cover}", "cover", "fire") + ", Safeguard", Icon = UiIcon.ShieldCheck, Figure = w.F("reallife_co_row_claim_by", "by day {day}", "day", "220"), Chip = w.T("reallife_ibox_chip_open", "Open"), ChipTone = UiRole.Warn },
            new CompanyRow { Id = "pol:Speedy Bites~7:0", Title = w.F("reallife_co_row_cover_title", "{cover} cover", "cover", "Fire"), Sub = "Speedy Bites, Safeguard", Icon = UiIcon.Fire, Figure = w.F("reallife_co_row_a_day", "{amount} a day", "amount", "$14") },
            new CompanyRow { Id = "pol:Speedy Bites~7:1", Title = w.F("reallife_co_row_cover_title", "{cover} cover", "cover", "Theft"), Sub = "Speedy Bites, " + w.T("reallife_co_none", "None"), Icon = UiIcon.Lock, Chip = w.T("reallife_co_row_uninsured", "Not covered"), ChipTone = UiRole.Warn },
            new CompanyRow { Id = "pol:Speedy Bites~7:2", Title = w.F("reallife_co_row_cover_title", "{cover} cover", "cover", "Liability"), Sub = "Speedy Bites, Safeguard", Icon = UiIcon.ShieldCheck, Figure = w.F("reallife_co_row_a_day", "{amount} a day", "amount", "$9") },
            new CompanyRow { Id = "pol:Bloom & Co~214:0", Title = w.F("reallife_co_row_cover_title", "{cover} cover", "cover", "Fire"), Sub = "Bloom & Co, " + w.T("reallife_co_none", "None"), Icon = UiIcon.Fire, Chip = w.T("reallife_co_row_uninsured", "Not covered"), ChipTone = UiRole.Warn }
        };
        p.List = new CompanyList { Title = w.T("reallife_co_tab_cover", "Cover"), Count = "5", Rows = rows.ToArray() };
        string pick = state == "cover_claim" ? "claim:1" : state == "cover_covered" ? "pol:Speedy Bites~7:0" : state == "cover_summary" ? null : "pol:Speedy Bites~7:1";
        for (int i = 0; i < rows.Count; i++)
        {
            p.List.Chosen = rows[i].Id == pick ? i : p.List.Chosen;
        }
        if (pick == null)
        {
            p.List.Chosen = -1;
            p.Top = new CompanyCard
            {
                Kicker = w.T("reallife_co_tab_cover", "Cover"), Title = w.T("reallife_co_cover_title", "Insurance"), Icon = UiIcon.ShieldCheck,
                FactLabels = new[] { w.T("reallife_co_f_premiums", "Premiums"), w.T("reallife_co_f_claims_open", "Claims to put in") }, FactValues = new[] { w.F("reallife_co_row_a_day", "{amount} a day", "amount", "$23"), "1" },
                Blocks = new[] { CSec(w.T("reallife_co_cover_what", "What cover is for")), CNote(w.T("reallife_co_cover_what_fire", "Fire cover pays for the damage a fire does.")), CNote(w.T("reallife_co_cover_what_theft", "Theft cover pays for what is stolen.")), CNote(w.T("reallife_co_cover_what_liability", "Liability cover pays when a customer is hurt.")), CNote(w.T("reallife_co_cover_pick", "Pick a business's cover to see its terms.")) }
            };
            return;
        }
        var c = new CompanyCard { Kicker = w.T("reallife_co_tab_cover", "Cover"), Icon = UiIcon.ShieldCheck };
        if (pick == "claim:1")
        {
            c.Title = w.F("reallife_co_claim_title", "{cover} claim, {business}", "cover", "Fire", "business", "Speedy Bites");
            c.Chips = new[] { w.T("reallife_ibox_chip_open", "Open") };
            c.ChipTones = new[] { UiRole.Warn };
            c.FactLabels = new[] { w.T("reallife_co_f_excess", "Excess"), w.T("reallife_co_f_pays", "Pays") };
            c.FactValues = new[] { CM(400), CM(3600) };
            c.Blocks = new[]
            {
                CNote(w.F("reallife_co_claim_open", "Put the claim in by day {day}; the insurer pays within {days} days.", "day", "220", "days", "5"), UiRole.WarnText),
                CNote(w.F("reallife_co_claim_what_fire", "A fire on day {day} cost {amount}.", "day", "214", "amount", CM(4000))),
                CSec(w.T("reallife_ibox_sec_money", "The money")), CRow(w.L("reallife_co_claim_insurer", "Insurer"), "Safeguard"), CRow(w.T("reallife_co_f_most", "Most it counts"), CM(10000)),
                CRow(w.T("reallife_co_f_excess", "Excess"), CM(400)), CRow(w.T("reallife_co_f_pays", "Pays"), CM(3600), UiRole.OkText),
                CNote(w.F("reallife_co_claim_counts", "Claims count against you after {most}, and the premium rises {loading}.", "most", "2", "loading", "20%")),
                CBtns(CB(w.T("reallife_co_claim_do", "Put the claim in"), UiButtonKind.Primary, UiAction.Alt, CA(CompanyDo.ClaimFile, null, 1), UiIcon.Check))
            };
        }
        else
        {
            bool covered = pick == "pol:Speedy Bites~7:0";
            c.Title = w.F("reallife_co_row_cover_title", "{cover} cover", "cover", covered ? "Fire" : "Theft");
            c.Sub = "Speedy Bites";
            c.Icon = covered ? UiIcon.Fire : UiIcon.Lock;
            c.Chips = new[] { covered ? w.T("reallife_ibox_chip_covered", "Covered") : w.T("reallife_ibox_chip_not_covered", "Not covered") };
            c.ChipTones = new[] { covered ? UiRole.Ok : UiRole.Warn };
            c.FactLabels = new[] { w.T("reallife_co_f_premium", "Premium"), w.T("reallife_co_f_most", "Most it counts") };
            c.FactValues = new[] { w.F("reallife_co_row_a_day", "{amount} a day", "amount", covered ? "$14" : "$11"), CM(covered ? 10000 : 5000) };
            var b = new List<CompanyBlock> { CSec(w.T("reallife_ibox_sec_terms", "Terms")) };
            if (covered)
            {
                b.Add(CRow(w.L("reallife_co_claim_insurer", "Insurer"), "Safeguard"));
            }
            else
            {
                b.Add(CNote(w.T("reallife_co_policy_none", "Speedy Bites has no cover against this. Choose an insurer.")));
                b.Add(new CompanyBlock { Kind = CompanyBlockKind.Choice, Text = w.T("reallife_co_claim_insurer", "Insurer"), Options = new[] { "Budget Mutual", "Safeguard", "Prime Assurance" }, Chosen = 1, Acts = new[] { CA(CompanyDo.Go, "insurer", 0), CA(CompanyDo.Go, "insurer", 1), CA(CompanyDo.Go, "insurer", 2) } });
            }
            b.Add(CNote(w.F("reallife_co_policy_excess", "The excess is {share} of a loss, at least {least}.", "share", "10%", "least", CM(200))));
            b.Add(CNote(w.F("reallife_co_policy_most", "It pays up to {most}, within {days} days.", "most", CM(covered ? 10000 : 5000), "days", "5")));
            b.Add(new CompanyBlock { Kind = CompanyBlockKind.Break });
            b.Add(CSec(w.T("reallife_co_policy_claims_sec", "Claim record")));
            b.Add(new CompanyBlock { Kind = CompanyBlockKind.Meter, Text = w.T("reallife_tab_co_claim", "Claims"), Value = w.F("reallife_rota_blocks_count", "{count} of {most}", "count", covered ? "1" : "0", "most", "3"), Number = covered ? 33 : 0, Tone = UiRole.Ok });
            b.Add(CNote(w.F("reallife_co_policy_claims", "After {most} claims the insurer refuses; each claim raises the premium by {loading}.", "most", "2", "loading", "20%")));
            if (covered)
            {
                b.Add(CNote(w.F("reallife_co_policy_term", "Runs to day {day}; {claims} claims so far.", "day", "280", "claims", "1")));
                b.Add(CBtns(CB(w.T("reallife_co_policy_cancel", "Cancel the cover"), UiButtonKind.Secondary, UiAction.Count, CA(CompanyDo.CoverCancel, "Speedy Bites~7", 0), UiIcon.Close, false, null, true)));
            }
            else
            {
                b.Add(CBtns(CB(w.T("reallife_co_policy_buy", "Buy the cover"), UiButtonKind.Primary, UiAction.Alt, CA(CompanyDo.CoverBuy, "Speedy Bites~7", 1, 1), UiIcon.Check)));
            }
            c.Blocks = b.ToArray();
        }
        p.Card = c;
    }

    // ------------------------------------------------------------------------------------------------- Tax and licences

    private static void CompanyTax(CompanyFacts f, CompanyWords w, string state)
    {
        CompanyTaxFacts t = f.Tax;
        if (state == "tax_off")
        {
            t.PartOff = true;
            t.OffTitle = w.T("reallife_co_nav_tax", "Tax and licences");
            t.OffBody = w.T("reallife_co_part_off", "Switched off in the options, or stopped after an error. Nothing of it runs and nothing is charged.");
            return;
        }
        bool one = state == "tax_one";
        t.LicencesTitle = w.T("reallife_co_licences", "Licences and permits");
        t.LicencesEmpty = w.T("reallife_co_lic_intro", "A business needs permits to trade.");
        var lic = new List<CompanyLicence>();
        lic.Add(new CompanyLicence { Id = "lic:Speedy Bites~7:0", Icon = UiIcon.Drop, Name = "Hygiene certificate", Place = "Speedy Bites", Manager = "Ray Walsh", ManagerHue = 0.55f, State = w.T("reallife_co_row_lic_auto", "Renews itself"), StateTone = UiRole.Ok, Day = "Day 241", Open = CA(CompanyDo.Go, "lic:Speedy Bites~7:0", CompanyView.Tax, CompanyView.SubLicence) });
        lic.Add(new CompanyLicence { Id = "lic:Speedy Bites~7:4", Icon = UiIcon.Fire, Name = "Fire safety", Place = "Speedy Bites", Manager = "Ray Walsh", ManagerHue = 0.55f, State = w.T("reallife_co_row_lic_soon", "Runs out soon"), StateTone = UiRole.Warn, Day = "Day 217", Open = CA(CompanyDo.Go, "lic:Speedy Bites~7:4", CompanyView.Tax, CompanyView.SubLicence),
            HasAction = true, Action = CB(w.T("reallife_co_lic_renew", "Renew"), UiButtonKind.Secondary, UiAction.Count, CA(CompanyDo.LicenceBuy, "Speedy Bites~7", 4), UiIcon.Check) });
        lic.Add(new CompanyLicence { Id = "lic:Bloom & Co~214:3", Icon = UiIcon.Tag, Name = "Signage permit", Place = "Bloom & Co", Manager = "Tessa Brandt", ManagerHue = 0.9f, State = w.T("reallife_co_row_lic_missing", "Missing"), StateTone = UiRole.Danger, Open = CA(CompanyDo.Go, "lic:Bloom & Co~214:3", CompanyView.Tax, CompanyView.SubLicence),
            HasAction = true, Action = CB(w.T("reallife_co_lic_buy", "Apply"), UiButtonKind.Secondary, UiAction.Count, CA(CompanyDo.LicenceBuy, "Bloom & Co~214", 3), UiIcon.Check) });
        lic.Add(new CompanyLicence { Id = "lic:Hudson Warehouse~4:7", Icon = UiIcon.Lock, Name = "Bonded storage", Place = "Hudson Warehouse", Manager = "Omar Haddad", ManagerHue = 0.75f, State = w.T("reallife_co_row_lic_pending", "Pending"), StateTone = UiRole.Warn, Day = "Day 216", Open = CA(CompanyDo.Go, "lic:Hudson Warehouse~4:7", CompanyView.Tax, CompanyView.SubLicence) });
        lic.Add(new CompanyLicence { Id = "lic:Bloom & Co~214:0", Icon = UiIcon.Drop, Name = "Hygiene certificate", Place = "Bloom & Co", Manager = "Tessa Brandt", ManagerHue = 0.9f, State = w.T("reallife_co_row_lic_valid", "Valid"), StateTone = UiRole.Ok, Day = "Day 250", Open = CA(CompanyDo.Go, "lic:Bloom & Co~214:0", CompanyView.Tax, CompanyView.SubLicence) });
        if (one)
        {
            lic.Clear();
            lic.Add(new CompanyLicence { Id = "lic:Corner Cafe~12:3", Icon = UiIcon.Tag, Name = "Signage permit", Place = "Corner Cafe", State = w.T("reallife_co_row_lic_missing", "Missing"), StateTone = UiRole.Danger, Open = CA(CompanyDo.Go, "lic:Corner Cafe~12:3", CompanyView.Tax, CompanyView.SubLicence),
                HasAction = true, Action = CB(w.T("reallife_co_lic_buy", "Apply"), UiButtonKind.Secondary, UiAction.Count, CA(CompanyDo.LicenceBuy, "Corner Cafe~12", 3), UiIcon.Check) });
        }
        t.Licences = lic.ToArray();
        t.LicencesCount = lic.Count.ToString();
        t.ComingUp = one ? new[] { new CompanyDone { When = "Day 90", Text = w.F("reallife_co_coming_quarter", "Quarter {quarter} closes", "quarter", "1"), Open = CA(CompanyDo.Go, "ret", CompanyView.Tax, CompanyView.SubReturn) } } : new[]
        {
            new CompanyDone { When = "Day 217", Text = w.F("reallife_co_coming_licence", "{licence} runs out at {business}", "licence", "Fire safety", "business", "Speedy Bites") },
            new CompanyDone { When = "Day 218", Text = w.F("reallife_co_coming_return", "Return for quarter {quarter} is due", "quarter", "2"), Open = CA(CompanyDo.Go, "ret", CompanyView.Tax, CompanyView.SubReturn) },
            new CompanyDone { When = "Day 218", Text = w.F("reallife_co_coming_topup", "{name} tops up the reserve", "name", "Noah Feldman"), Money = "-" + CM(1840), Tone = -1 },
            new CompanyDone { When = "Day 230", Text = w.F("reallife_co_coming_accountant", "{name}'s fee", "name", "Careful accountant"), Money = "-" + CM(620), Tone = -1 }
        };
        t.QuarterTitle = w.T("reallife_co_quarter_so_far", "This quarter so far");
        t.QuarterRange = w.F("reallife_co_quarter_range", "Q{quarter}, day {from} to {to}", "quarter", one ? "1" : "3", "from", one ? "1" : "181", "to", one ? "90" : "270");
        t.Quarter = one
            ? new[]
            {
                new CompanyStatementLine { Label = w.T("reallife_co_q_costs", "Costs that count"), Value = CM(0) }
            }
            : new[]
            {
                new CompanyStatementLine { Label = w.T("reallife_co_q_takings", "Takings"), Value = CM(74200) }, new CompanyStatementLine { Label = w.T("reallife_co_q_costs", "Costs that count"), Value = CM(52900) },
                new CompanyStatementLine { Label = w.T("reallife_co_q_taxable", "Taxable profit"), Value = CM(21300), Strong = true },
                new CompanyStatementLine { Label = w.F("reallife_co_q_tax", "Business tax at {percent}%", "percent", "20"), Value = CM(4260) },
                new CompanyStatementLine { Label = w.T("reallife_co_q_set_aside", "Set aside"), Value = CM(2420) }, new CompanyStatementLine { Label = w.T("reallife_co_q_still", "Still to set aside"), Value = CM(1840), Strong = true, Tone = UiRole.WarnText }
            };
        t.ReserveControls = one ? new CompanyBlock[0] : new[]
        {
            new CompanyBlock { Kind = CompanyBlockKind.Stepper, Text = w.T("reallife_co_reserve_amount", "Set aside"), Sub = w.F("reallife_co_reserve_cash", "{amount} in the cash", "amount", CM(48210)), Value = CM(1840), CanLess = true, CanMore = true,
                Act = CA(CompanyDo.Go, "amount", -100, 1840), Act2 = CA(CompanyDo.Go, "amount", 100, 1840), Act3 = CA(CompanyDo.Go, "amount", 0, 1840) },
            CBtns(CB(w.F("reallife_co_reserve_do", "Set aside {amount}", "amount", CM(1840)), UiButtonKind.Secondary, UiAction.Count, CA(CompanyDo.ReserveSet, null, 1840), UiIcon.Check),
                CB(w.T("reallife_co_reserve_release", "Release"), UiButtonKind.Secondary, UiAction.Count, CA(CompanyDo.ReserveRelease), UiIcon.Back, false, null, true)),
            new CompanyBlock { Kind = CompanyBlockKind.Switch, Text = w.T("reallife_co_reserve_auto", "Finance chief tops it up"), Sub = w.F("reallife_co_reserve_auto_on", "{name} does it when a quarter closes", "name", "Noah Feldman"), On = true, Value = w.T("reallife_co_on", "On"), Act = CA(CompanyDo.ReserveAuto, null, 0) }
        };
        t.PaidTitle = w.T("reallife_co_paid_year", "Paid this year");
        t.PaidEmpty = w.T("reallife_co_paid_none", "No return has been settled yet.");
        t.Paid = one ? new CompanyItem[0] : new[]
        {
            new CompanyItem { Title = w.F("reallife_co_row_quarter", "Quarter {quarter}", "quarter", "2"), Sub = "Day 182", Figure = CM(3780), Chip = w.T("reallife_co_paid_on_time", "On time"), ChipTone = UiRole.Ok },
            new CompanyItem { Title = w.F("reallife_co_row_quarter", "Quarter {quarter}", "quarter", "1"), Sub = "Day 96", Figure = CM(3120), Chip = w.T("reallife_ibox_chip_late", "Late"), ChipTone = UiRole.Warn }
        };
        t.ToolsTitle = w.T("reallife_co_tax_tools", "Return, accountant and books");
        t.Tools = new[]
        {
            new CompanyItem { Title = w.T("reallife_co_row_return", "Tax return"), Sub = w.F("reallife_co_row_quarter", "Quarter {quarter}", "quarter", one ? "1" : "2"), Chip = one ? w.T("reallife_co_row_return_running", "Running") : w.T("reallife_co_row_return_due", "Due"), ChipTone = one ? UiRole.Muted : UiRole.Warn, Press = true, Act = CA(CompanyDo.Go, "ret", CompanyView.Tax, CompanyView.SubReturn) },
            new CompanyItem { Title = w.T("reallife_co_row_accountant", "Accountant"), Sub = "Careful", Figure = w.F("reallife_co_row_a_month", "{amount} a month", "amount", CM(620)), Press = true, Act = CA(CompanyDo.Go, "acc", CompanyView.Tax, CompanyView.SubAccountant) },
            new CompanyItem { Title = "Speedy Bites", Sub = w.T("reallife_co_row_hidden", "Cash off the books"), Figure = "5%", FigureTone = UiRole.WarnText, Press = true, Act = CA(CompanyDo.Go, "hide:Speedy Bites~7", CompanyView.Tax, CompanyView.SubHide) }
        };
        t.PropertyTitle = w.T("reallife_co_property_tax", "Property tax");
        t.Property = one ? new CompanyStatementLine[0] : new[] { new CompanyStatementLine { Label = "Hudson Warehouse", Value = CM(2140) } };
        t.PropertyNote = one ? w.T("reallife_co_property_rented", "Rented premises: the property tax is inside the rent.") : w.T("reallife_co_property_note", "Counted so far this tax year.");
        if (state == "tax_lead" || state == "tax_big")
        {
            t.HasLead = true;
            t.LeadKicker = w.F("reallife_co_lead_chief", "Finance chief, {name}", "name", "Noah Feldman");
            t.LeadTitle = w.F("reallife_co_lead_reserve", "The year's tax so far is {tax}. {set} is set aside.", "tax", CM(4260), "set", CM(2420));
            t.LeadBody = w.F("reallife_co_lead_reserve_body", "Setting aside {amount} puts it out of reach of the cash until the bill comes.", "amount", CM(1840));
            t.LeadAsker = "Noah Feldman";
            t.LeadHue = 0.2f;
            t.LeadButtons = new[] { CB(w.F("reallife_co_reserve_let", "Let {name} do it", "name", "Noah"), UiButtonKind.Primary, UiAction.Alt, CA(CompanyDo.ReserveAuto, null, 1), UiIcon.Handshake) };
        }
        if (one)
        {
            t.HasLead = false;
        }
        var back = new[] { CB(w.T("reallife_co_back", "Back"), UiButtonKind.Secondary, UiAction.Count, CA(CompanyDo.Go, null, CompanyView.Tax, CompanyView.SubMain), UiIcon.Back) };
        if (state == "tax_return")
        {
            t.Card = new CompanyCard
            {
                Kicker = w.T("reallife_co_tab_tax", "Tax and licences"), Title = w.T("reallife_co_row_return", "Tax return"), Icon = UiIcon.Receipt, Back = back, Chips = new[] { w.T("reallife_ibox_chip_due", "Due") }, ChipTones = new[] { UiRole.Warn },
                Blocks = new[]
                {
                    CNote(w.F("reallife_co_return_due", "File by day {day}; after that a penalty of {amount} is charged, {count} so far.", "day", "218", "amount", CM(420), "count", "0"), UiRole.WarnText),
                    CSec(w.T("reallife_ibox_sec_return", "The return")), CRow(w.T("reallife_co_return_quarter", "Quarter"), "2"), CRow(w.T("reallife_co_return_takings", "Takings"), CM(68400)), CRow(w.T("reallife_co_return_deduct", "Costs that count"), CM(47100)),
                    new CompanyBlock { Kind = CompanyBlockKind.Break },
                    CNote(w.F("reallife_co_return_rebate", "{name} files it for you and takes back {amount}.", "name", "Careful", "amount", CM(210)), UiRole.OkText),
                    CBtns(CB(w.T("reallife_co_return_file", "File the return"), UiButtonKind.Primary, UiAction.Alt, CA(CompanyDo.ReturnFile), UiIcon.Check))
                }
            };
        }
        else if (state == "tax_accountant")
        {
            t.Card = new CompanyCard
            {
                Kicker = w.T("reallife_co_tab_tax", "Tax and licences"), Title = w.T("reallife_co_row_accountant", "Accountant"), Icon = UiIcon.Scales, Back = back,
                FactLabels = new[] { w.T("reallife_co_f_hired", "Hired now"), w.T("reallife_co_f_fee", "Fee") }, FactValues = new[] { "Careful", w.F("reallife_co_row_a_month", "{amount} a month", "amount", CM(620)) },
                Blocks = new[]
                {
                    CSec(w.T("reallife_ibox_sec_terms", "Terms")),
                    new CompanyBlock { Kind = CompanyBlockKind.Choice, Text = w.T("reallife_co_acc_tier", "Accountant"), Options = new[] { "Careless", "Careful", "Honest" }, Chosen = 2, Acts = new[] { CA(CompanyDo.Go, "kind", 0), CA(CompanyDo.Go, "kind", 1), CA(CompanyDo.Go, "kind", 2) } },
                    CNote(w.F("reallife_co_acc_files", "Files the return for you within {days} days.", "days", "7")), new CompanyBlock { Kind = CompanyBlockKind.Break },
                    CNote(w.F("reallife_co_acc_saves", "Saves {share} of the tax.", "share", "4%"), UiRole.OkText),
                    CNote(w.F("reallife_co_acc_risk", "Adds {share} to the audit risk.", "share", "0.2%")), CNote(w.T("reallife_co_acc_honest_stays", "Stays if you take cash off the books, and files an honest return.")),
                    CBtns(CB(w.T("reallife_co_acc_hire", "Hire"), UiButtonKind.Primary, UiAction.Alt, CA(CompanyDo.AccountantHire, null, 2), UiIcon.Check),
                        CB(w.T("reallife_co_acc_dismiss", "Let go"), UiButtonKind.Secondary, UiAction.Count, CA(CompanyDo.AccountantDismiss), UiIcon.Close, false, null, true))
                }
            };
        }
        else if (state == "tax_hide")
        {
            t.Card = new CompanyCard
            {
                Kicker = w.T("reallife_co_tab_tax", "Tax and licences"), Title = w.F("reallife_co_hide_title", "Cash off the books at {business}", "business", "Speedy Bites"), Icon = UiIcon.Percent, Back = back,
                FactLabels = new[] { w.T("reallife_co_f_saved", "Tax kept back"), w.T("reallife_co_f_risk", "Audit risk") }, FactValues = new[] { CM(310), "0.4%" },
                Blocks = new[]
                {
                    CNote(w.F("reallife_co_hide_what", "Cash you keep off the books pays no tax, at {percent}%.", "percent", "20")),
                    new CompanyBlock { Kind = CompanyBlockKind.Choice, Text = w.T("reallife_co_hide_label", "Cash off the books"), Options = new[] { "0%", "5%", "10%" }, Chosen = 1, Acts = new[] { CA(CompanyDo.HideSet, "Speedy Bites~7", 0), CA(CompanyDo.HideSet, "Speedy Bites~7", 5), CA(CompanyDo.HideSet, "Speedy Bites~7", 10) } },
                    new CompanyBlock { Kind = CompanyBlockKind.Break },
                    CSec(w.T("reallife_ibox_sec_risk", "The risk")), CNote(w.F("reallife_co_hide_risk", "The audit chance grows {grow} a day for each per cent and falls {fall} a day, to at most {most}.", "grow", "0.2", "fall", "0.1", "most", "30%")),
                    CNote(w.F("reallife_co_hide_audit", "An audit fines {fine} of what was hidden, and goes to court from {sum}.", "fine", "50%", "sum", CM(5000))), CNote(w.T("reallife_co_hide_threshold", "Nothing is checked while the sum stays small."))
                }
            };
        }
        else if (state == "licence_card")
        {
            t.Card = new CompanyCard
            {
                Kicker = "Bloom & Co", Title = "Signage permit", Icon = UiIcon.Tag, Back = back, Chips = new[] { w.T("reallife_ibox_chip_missing", "Missing") }, ChipTones = new[] { UiRole.Danger },
                FactLabels = new[] { w.T("reallife_co_f_fee", "Fee"), w.T("reallife_co_f_term", "Term") }, FactValues = new[] { CM(220), w.F("reallife_co_lic_term_value", "{days} days", "days", "90") },
                Blocks = new[]
                {
                    CNote(w.T("reallife_co_lic_missing", "Trading without it risks a fine."), UiRole.DangerText),
                    new CompanyBlock { Kind = CompanyBlockKind.Switch, Text = w.T("reallife_co_lic_auto", "Renew it by itself"), On = false, Value = w.T("reallife_co_off", "Off"), Act = CA(CompanyDo.LicenceAuto, "Bloom & Co~214", 3, 1) },
                    CBtns(CB(w.T("reallife_co_lic_buy", "Apply"), UiButtonKind.Primary, UiAction.Alt, CA(CompanyDo.LicenceBuy, "Bloom & Co~214", 3), UiIcon.Check)),
                    new CompanyBlock { Kind = CompanyBlockKind.Break },
                    CSec(w.T("reallife_co_lic_lapse_sec", "If it lapses")),
                    CNote(w.F("reallife_co_lic_fine", "An inspector may fine {amount}.", "amount", CM(600)), UiRole.WarnText),
                    CSec(w.T("reallife_co_lic_why_sec", "Why you need it")),
                    CNote(w.F("reallife_co_lic_why_signage", "A sign over the door needs a permit.", "hour", "22", "early", "6"))
                }
            };
        }
    }
}
