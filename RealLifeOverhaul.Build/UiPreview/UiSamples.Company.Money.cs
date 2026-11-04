using System.Collections.Generic;
using RealLifeOverhaul.Ui;

// The Company app's Marketing, Cover and Tax tabs (the Rota's are in UiSamples.Company.Rota.cs). Samples' own names; the live source reads the player's businesses.
internal static partial class UiSamples
{
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
            rows.Add(new CompanyRow { Id = "push:Speedy Bites~7", Title = "Speedy Bites", Sub = Tr(w, "Radio") + ", " + w.F("reallife_mgr_row_day", "Day {day}", "day", "219"), Icon = UiIcon.Megaphone, Figure = "+9%", FigureTone = UiRole.OkText, Note = CM(620), Chip = w.T("reallife_ibox_chip_running", "Running"), ChipTone = UiRole.Ok });
            rows.Add(new CompanyRow { Id = "push:Bloom & Co~214", Title = "Bloom & Co", Sub = w.T("reallife_co_none", "None"), Icon = UiIcon.Store });
            rows.Add(new CompanyRow { Id = "push:Pioneer Game Studio~9", Title = "Pioneer Game Studio", Sub = w.T("reallife_co_none", "None"), Icon = UiIcon.Store });
            rows.Add(new CompanyRow { Caption = true, Title = w.T("reallife_co_reports", "Reports") });
            rows.Add(new CompanyRow { Id = "rep:0", Title = w.T("reallife_co_campaign_flyers", "Flyers"), Sub = "Bloom & Co, " + w.F("reallife_mgr_row_day", "Day {day}", "day", "201"), Icon = UiIcon.ClipboardText, Figure = "+24%", FigureTone = UiRole.OkText, Note = w.F("reallife_co_row_extra", "{amount} extra", "amount", CM(180)) });
            rows.Add(new CompanyRow { Id = "rep:2", Title = w.T("reallife_co_campaign_radio", "Radio spot"), Sub = "Bloom & Co, " + w.F("reallife_mgr_row_day", "Day {day}", "day", "176"), Icon = UiIcon.ClipboardText, Figure = "+9%", FigureTone = UiRole.OkText, Note = w.F("reallife_co_row_extra", "{amount} extra", "amount", CM(90)) });
            rows.Add(new CompanyRow { Id = "rep:1", Title = Tr(w, "Discount week"), Sub = "Speedy Bites, " + w.F("reallife_mgr_row_day", "Day {day}", "day", "188"), Icon = UiIcon.ClipboardText, Figure = "+6%", FigureTone = UiRole.OkText, Note = w.F("reallife_co_row_less", "{amount} less", "amount", CM(40)) });
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
                    CNote(w.T("reallife_co_push_intro", "A campaign draws more people to a business for some days.")),
                    new CompanyBlock { Kind = CompanyBlockKind.Figures, Options = new[] { w.T("reallife_co_f_spent", "Spent"), w.T("reallife_co_f_extra", "Extra profit"), w.T("reallife_co_f_campaigns", "Campaigns") }, Values = new[] { CM(310), CM(230), "3" } },
                    CSec(w.T("reallife_co_push_kinds_sec", "Which kind pays"), "3"),
                    new CompanyBlock { Kind = CompanyBlockKind.Items, Items = new[]
                    {
                        new CompanyItem { Grade = -1, Title = w.T("reallife_co_campaign_flyers", "Flyers"), Icon = UiIcon.Megaphone, Sub = w.F("reallife_co_push_n", "Campaigns: {count}", "count", "1"), Figure = "+100%", FigureTone = UiRole.OkText, Note = w.T("reallife_co_report_return_avg", "return") },
                        new CompanyItem { Grade = -1, Title = w.T("reallife_co_campaign_radio", "Radio spot"), Icon = UiIcon.Megaphone, Sub = w.F("reallife_co_push_n", "Campaigns: {count}", "count", "1"), Figure = "+45%", FigureTone = UiRole.OkText, Note = w.T("reallife_co_report_return_avg", "return") },
                        new CompanyItem { Grade = -1, Title = w.T("reallife_co_campaign_discount", "Discount day"), Icon = UiIcon.Megaphone, Sub = w.F("reallife_co_push_n", "Campaigns: {count}", "count", "1"), Figure = "-18%", FigureTone = UiRole.DangerText, Note = w.T("reallife_co_report_return_avg", "return") }
                    } },
                    CSec(w.T("reallife_ibox_sec_how", "How it is measured")),
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
            c.Title = w.T("reallife_co_campaign_flyers", "Flyers");
            c.Icon = UiIcon.ClipboardText;
            c.Sub = w.F("reallife_co_report_days", "Days {from} to {to}, {days} days", "from", "198", "to", "201", "days", "4");
            c.Chips = new[] { "+100%" };
            c.ChipTones = new[] { UiRole.Ok };
            c.FactLabels = new[] { w.T("reallife_co_f_cost", "Cost"), w.T("reallife_co_f_extra", "Extra profit") };
            c.FactValues = new[] { CM(180), CM(180) };
            c.Blocks = new[]
            {
                CSec(w.T("reallife_ibox_sec_before_during", "Before and during")),
                new CompanyBlock { Kind = CompanyBlockKind.Compare, Text = w.T("reallife_co_f_customers", "Customers"), Value = "212", Sub = "262 (+24%)", Tone = UiRole.OkText, Options = new[] { w.T("reallife_co_report_before", "Before"), w.T("reallife_co_report_during", "During") } },
                new CompanyBlock { Kind = CompanyBlockKind.Compare, Text = w.T("reallife_co_f_takings", "Takings"), Value = CM(1480), Sub = CM(2010), Tone = UiRole.OkText },
                new CompanyBlock { Kind = CompanyBlockKind.Compare, Text = w.T("reallife_co_f_profit", "Profit"), Value = CM(240), Sub = CM(600), Tone = UiRole.OkText },
                CSec(w.T("reallife_co_report_per_day", "A day on average")),
                new CompanyBlock { Kind = CompanyBlockKind.Compare, Text = w.T("reallife_co_f_customers", "Customers"), Value = "53", Sub = "65", Tone = UiRole.OkText, Options = new[] { w.T("reallife_co_report_before", "Before"), w.T("reallife_co_report_during", "During") } },
                new CompanyBlock { Kind = CompanyBlockKind.Compare, Text = w.T("reallife_co_f_takings", "Takings"), Value = CM(370), Sub = CM(502), Tone = UiRole.OkText },
                new CompanyBlock { Kind = CompanyBlockKind.Compare, Text = w.T("reallife_co_f_profit", "Profit"), Value = CM(60), Sub = CM(150), Tone = UiRole.OkText },
                new CompanyBlock { Kind = CompanyBlockKind.Break },
                CSec(w.T("reallife_ibox_sec_money", "The money")),
                CRow(w.T("reallife_co_f_extra", "Extra profit"), CM(180), UiRole.OkText), CRow(w.L("reallife_co_report_return", "Return on the cost"), "+100%", UiRole.OkText),
                CSec(w.T("reallife_co_push_earlier", "Earlier campaigns here")),
                new CompanyBlock { Kind = CompanyBlockKind.Items, Items = new[] { new CompanyItem { Grade = -1, Title = w.T("reallife_co_campaign_radio", "Radio spot"), Sub = w.F("reallife_mgr_row_day", "Day {day}", "day", "176"), Icon = UiIcon.ClipboardText, Figure = "+9%", FigureTone = UiRole.OkText, Note = w.F("reallife_co_row_extra", "{amount} extra", "amount", CM(90)), Press = true, Act = CA(CompanyDo.Go, "rep:2", CompanyView.Marketing) } } }
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
                CSec(w.T("reallife_ibox_sec_campaign", "The campaign")), CRow(w.L("reallife_co_push_running", "Running"), Tr(w, "Radio")),
                new CompanyBlock { Kind = CompanyBlockKind.Meter, Text = w.F("reallife_co_push_progress", "Day {done} of {days}", "done", "2", "days", "4"), Value = w.F("reallife_co_push_left", "{count} days left", "count", "2"), Number = 50, Tone = UiRole.Ok },
                CNote(w.F("reallife_co_push_report_due", "A report comes on day {day}.", "day", "220")),
                CSec(w.T("reallife_co_push_parts_sec", "What makes up the effect"), "+9%"),
                CRow(w.T("reallife_co_push_part_own", "The campaign's own figure"), "+8%"), CRow(w.T("reallife_co_push_part_district", "The district counts for"), "110%"),
                CRow(w.T("reallife_co_push_part_economy", "The economy counts for"), "100%"),
                CSec(w.T("reallife_co_push_earlier", "Earlier campaigns here")),
                new CompanyBlock { Kind = CompanyBlockKind.Items, Items = new[] { new CompanyItem { Title = Tr(w, "Discount week"), Sub = w.F("reallife_mgr_row_day", "Day {day}", "day", "188"), Icon = UiIcon.ClipboardText, Figure = "+6%", FigureTone = UiRole.OkText, Note = w.F("reallife_co_row_less", "{amount} less", "amount", CM(40)), Grade = -1, Press = true, Act = CA(CompanyDo.Go, "rep:1", CompanyView.Marketing) } } }
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
                new CompanyBlock { Kind = CompanyBlockKind.Items, Items = new[]
                {
                    new CompanyItem { Title = w.T("reallife_co_campaign_flyers", "Flyers"), Icon = UiIcon.Megaphone, Tint = UiRole.None, Press = true, Grade = -1, Sub = w.F("reallife_co_lic_term_value", "{days} days", "days", "4"), Figure = "+12%", FigureTone = UiRole.OkText, Note = CM(180), Chip = w.T("reallife_ui_bizops_pick", "Picked"), ChipTone = UiRole.Ok, Act = CA(CompanyDo.Go, "kind", 0) },
                    new CompanyItem { Title = Tr(w, "Radio"), Icon = UiIcon.Megaphone, Tint = UiRole.None, Press = true, Grade = -1, Sub = w.F("reallife_co_lic_term_value", "{days} days", "days", "4"), Figure = "+9%", FigureTone = UiRole.OkText, Note = CM(620), Act = CA(CompanyDo.Go, "kind", 1) },
                    new CompanyItem { Title = Tr(w, "Discount week"), Icon = UiIcon.Megaphone, Tint = UiRole.None, Press = true, Grade = -1, Sub = w.F("reallife_co_lic_term_value", "{days} days", "days", "7"), Figure = "+6%", FigureTone = UiRole.OkText, Note = CM(40), Act = CA(CompanyDo.Go, "kind", 2) }
                } },
                CNote(w.T("reallife_co_push_about_flyers", "Flyers through every door nearby.")),
                CRow(w.T("reallife_co_f_cost", "Cost"), CM(180)), CRow(w.L("reallife_co_push_length", "Length"), w.F("reallife_co_push_length_value", "{days} days from day {from}", "days", "4", "from", "215")),
                CRow(w.T("reallife_co_f_effect", "Effect on customers"), "+12%", UiRole.OkText),
                CNote(w.F("reallife_co_push_parts", "Its own {own}, your district {district}, the economy {economy}.", "own", "+10%", "district", "110%", "economy", "100%")),
                one ? CNote(string.Empty) : CSec(w.T("reallife_co_push_earlier", "Earlier campaigns here")),
                one ? CNote(string.Empty) : new CompanyBlock { Kind = CompanyBlockKind.Items, Items = new[] { new CompanyItem { Title = w.T("reallife_co_campaign_flyers", "Flyers"), Sub = w.F("reallife_mgr_row_day", "Day {day}", "day", "201"), Icon = UiIcon.ClipboardText, Figure = "+24%", FigureTone = UiRole.OkText, Note = w.F("reallife_co_row_extra", "{amount} extra", "amount", CM(180)), Grade = -1, Press = true, Act = CA(CompanyDo.Go, "rep:0", CompanyView.Marketing) } } },
                CSec(w.T("reallife_ibox_sec_how", "How it is measured")),
                CNote(w.T("reallife_co_push_measure", "The customers and takings of the days before are set against the days during.")),
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
            new CompanyRow { Id = "claim:1", Title = "Speedy Bites", Sub = w.F("reallife_co_row_claim", "Claim, {cover}", "cover", w.T("reallife_co_cover_fire", "Fire").ToLowerInvariant()) + ", Safeguard", Icon = UiIcon.ShieldCheck, Figure = w.F("reallife_co_row_claim_by", "by day {day}", "day", "220"), Chip = w.T("reallife_ibox_chip_open", "Open"), ChipTone = UiRole.Warn },
            new CompanyRow { Id = "pol:Speedy Bites~7:0", Title = w.F("reallife_co_row_cover_title", "{cover} cover", "cover", CompanyTextCapital(w.T("reallife_co_cover_fire", "Fire"))), Sub = "Speedy Bites, Safeguard", Icon = UiIcon.Fire, Figure = w.F("reallife_co_row_a_day", "{amount} a day", "amount", "$14") },
            new CompanyRow { Id = "pol:Speedy Bites~7:1", Title = w.F("reallife_co_row_cover_title", "{cover} cover", "cover", CompanyTextCapital(w.T("reallife_co_cover_theft", "Theft"))), Sub = "Speedy Bites, " + w.T("reallife_co_none", "None"), Icon = UiIcon.Lock, Chip = w.T("reallife_ibox_chip_not_covered", "Not covered"), ChipTone = UiRole.Warn },
            new CompanyRow { Id = "pol:Speedy Bites~7:2", Title = w.F("reallife_co_row_cover_title", "{cover} cover", "cover", CompanyTextCapital(w.T("reallife_co_cover_liability", "Liability"))), Sub = "Speedy Bites, Safeguard", Icon = UiIcon.ShieldCheck, Figure = w.F("reallife_co_row_a_day", "{amount} a day", "amount", "$9") },
            new CompanyRow { Id = "pol:Bloom & Co~214:0", Title = w.F("reallife_co_row_cover_title", "{cover} cover", "cover", CompanyTextCapital(w.T("reallife_co_cover_fire", "Fire"))), Sub = "Bloom & Co, " + w.T("reallife_co_none", "None"), Icon = UiIcon.Fire, Chip = w.T("reallife_ibox_chip_not_covered", "Not covered"), ChipTone = UiRole.Warn }
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
                Blocks = new[] { CSec(w.T("reallife_co_cover_what", "What each kind covers")), CNote(w.T("reallife_co_cover_what_fire", "Fire cover pays for the damage a fire does.")), CNote(w.T("reallife_co_cover_what_theft", "Theft cover pays for what is stolen.")), CNote(w.T("reallife_co_cover_what_liability", "Liability cover pays when a customer is hurt.")),
                    CSec(w.T("reallife_co_cover_held", "Held now")),
                    new CompanyBlock { Kind = CompanyBlockKind.Items, Items = new[]
                    {
                        new CompanyItem { Title = w.F("reallife_co_row_cover_title", "{cover} cover", "cover", CompanyTextCapital(w.T("reallife_co_cover_fire", "Fire"))), Sub = w.F("reallife_co_covered_of", "{count} of {total} businesses", "count", "1", "total", "2"), Icon = UiIcon.Fire, Grade = 50, Figure = w.F("reallife_co_row_a_day", "{amount} a day", "amount", "$14") },
                        new CompanyItem { Title = w.F("reallife_co_row_cover_title", "{cover} cover", "cover", CompanyTextCapital(w.T("reallife_co_cover_theft", "Theft"))), Sub = w.F("reallife_co_covered_of", "{count} of {total} businesses", "count", "0", "total", "2"), Icon = UiIcon.Lock, Grade = 0 },
                        new CompanyItem { Title = w.F("reallife_co_row_cover_title", "{cover} cover", "cover", CompanyTextCapital(w.T("reallife_co_cover_liability", "Liability"))), Sub = w.F("reallife_co_covered_of", "{count} of {total} businesses", "count", "1", "total", "2"), Icon = UiIcon.ShieldCheck, Grade = 50, Figure = w.F("reallife_co_row_a_day", "{amount} a day", "amount", "$9") }
                    } },
                    CSec(w.T("reallife_co_claims_recent", "Latest claims"), "2"),
                    new CompanyBlock { Kind = CompanyBlockKind.Items, Items = new[]
                    {
                        new CompanyItem { Grade = -1, Title = w.F("reallife_co_claim_title", "{cover} claim, {business}", "cover", CompanyTextCapital(w.T("reallife_co_cover_fire", "Fire")), "business", "Speedy Bites"), Sub = w.F("reallife_mgr_row_day", "Day {day}", "day", "214"), Icon = UiIcon.ShieldCheck, Figure = CM(3600), Chip = w.T("reallife_ibox_chip_open", "Open"), ChipTone = UiRole.Warn, Press = true, Act = CA(CompanyDo.Go, "claim:1", CompanyView.Cover) },
                        new CompanyItem { Grade = -1, Title = w.F("reallife_co_claim_title", "{cover} claim, {business}", "cover", CompanyTextCapital(w.T("reallife_co_cover_theft", "Theft")), "business", "Speedy Bites"), Sub = w.F("reallife_mgr_row_day", "Day {day}", "day", "188"), Icon = UiIcon.ShieldCheck, Figure = CM(1800), Chip = w.T("reallife_ibox_chip_paid", "Paid"), ChipTone = UiRole.Ok, Press = true, Act = CA(CompanyDo.Go, "claim:2", CompanyView.Cover) }
                    } },
                    CNote(w.T("reallife_co_cover_pick", "Pick a business's cover to see its terms.")) }
            };
            return;
        }
        var c = new CompanyCard { Kicker = w.T("reallife_co_tab_cover", "Cover"), Icon = UiIcon.ShieldCheck };
        if (pick == "claim:1")
        {
            c.Title = w.F("reallife_co_claim_title", "{cover} claim, {business}", "cover", CompanyTextCapital(w.T("reallife_co_cover_fire", "Fire")), "business", "Speedy Bites");
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
                CBtns(CB(w.T("reallife_co_claim_do", "Put the claim in"), UiButtonKind.Primary, UiAction.Alt, CA(CompanyDo.ClaimFile, null, 1), UiIcon.Check)),
                CSec(w.T("reallife_co_policy_claims_sec", "Claim record")),
                new CompanyBlock { Kind = CompanyBlockKind.Meter, Text = w.T("reallife_tab_co_claim", "Claims"), Value = w.F("reallife_rota_blocks_count", "{count} of {most}", "count", "1", "most", "3"), Number = 33, Tone = UiRole.Ok },
                CSec(w.T("reallife_co_claims_before", "Earlier claims here"), "1"),
                new CompanyBlock { Kind = CompanyBlockKind.Items, Items = new[] { new CompanyItem { Grade = -1, Title = w.F("reallife_co_claim_title", "{cover} claim, {business}", "cover", CompanyTextCapital(w.T("reallife_co_cover_theft", "Theft")), "business", "Speedy Bites"), Sub = w.F("reallife_mgr_row_day", "Day {day}", "day", "188"), Icon = UiIcon.ShieldCheck, Figure = CM(1800), Chip = w.T("reallife_ibox_chip_paid", "Paid"), ChipTone = UiRole.Ok, Press = true, Act = CA(CompanyDo.Go, "claim:2", CompanyView.Cover) } } }
            };
        }
        else
        {
            bool covered = pick == "pol:Speedy Bites~7:0";
            c.Title = w.F("reallife_co_row_cover_title", "{cover} cover", "cover", CompanyTextCapital(covered ? w.T("reallife_co_cover_fire", "Fire") : w.T("reallife_co_cover_theft", "Theft")));
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
                b.Add(CSec(w.T("reallife_co_policy_quotes", "What each insurer asks")));
                string[] others = { "Budget Mutual", "Safeguard", "Prime Assurance" }, cuts = { "15%", "10%", "5%" }, floors = { "$300", "$200", "$100" }, costs = { "$12", "$14", "$17" };
                var held = new CompanyItem[3];
                for (int i = 0; i < held.Length; i++)
                {
                    held[i] = new CompanyItem { Title = others[i], Icon = UiIcon.Bank, Tint = UiRole.None, Grade = -1, Sub = w.F("reallife_co_policy_excess", "Excess: {share} of the loss, {least} at least.", "share", cuts[i], "least", floors[i]),
                        Figure = w.F("reallife_co_row_a_day", "{amount} a day", "amount", costs[i]), Chip = i == 1 ? w.T("reallife_ibox_chip_covered", "Covered") : null, ChipTone = UiRole.Ok };
                }
                b.Add(new CompanyBlock { Kind = CompanyBlockKind.Items, Items = held });
            }
            else
            {
                b.Add(CNote(w.T("reallife_co_policy_pick", "No cover yet. Pick the insurer whose terms suit you.")));
                b.Add(CSec(w.T("reallife_co_policy_quotes", "What each insurer asks")));
                string[] insurers = { "Budget Mutual", "Safeguard", "Prime Assurance" }, shares = { "15%", "10%", "5%" }, least = { "$300", "$200", "$100" }, quotes = { "$9", "$11", "$14" };
                var asks = new CompanyItem[3];
                for (int i = 0; i < asks.Length; i++)
                {
                    asks[i] = new CompanyItem { Title = insurers[i], Icon = UiIcon.Bank, Tint = UiRole.None, Press = true, Grade = -1,
                        Sub = w.F("reallife_co_policy_excess", "Excess: {share} of the loss, {least} at least.", "share", shares[i], "least", least[i]), Figure = w.F("reallife_co_row_a_day", "{amount} a day", "amount", quotes[i]),
                        Chip = i == 1 ? w.T("reallife_ui_bizops_pick", "Picked") : null, ChipTone = UiRole.Ok, Act = CA(CompanyDo.Go, "insurer", i) };
                }
                b.Add(new CompanyBlock { Kind = CompanyBlockKind.Items, Items = asks });
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

    private static CompanyItem[] AccountantTermRows(CompanyWords w)
    {
        int[] fees = { 300, 700, 1500 }, saves = { 20, 40, 70 }, risks = { 1000, 800, 600 };
        var terms = new CompanyItem[3];
        for (int i = 0; i < 3; i++)
        {
            terms[i] = new CompanyItem { Title = w.T("reallife_co_accountant_" + i, i == 0 ? "Quay Street Bookkeeping" : i == 1 ? "Marlow & Finch" : "Ashdown Chartered"),
                Sub = w.F("reallife_co_acc_terms_line", "saves {saves} of the tax, audit risk {risk} of the usual", "saves", saves[i] / 10f + "%", "risk", risks[i] / 10 + "%"),
                Figure = w.F("reallife_co_row_a_month", "{amount} a month", "amount", CM(fees[i])), Press = true, Grade = -1, Act = CA(CompanyDo.Go, "acc", CompanyView.Tax, CompanyView.SubAccountant) };
        }
        return terms;
    }

    private static string CompanyTextCapital(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s.Substring(1);

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
        lic.Add(new CompanyLicence { Id = "lic:Speedy Bites~7:0", Icon = UiIcon.Drop, Name = Lic(w, "hygiene"), Place = "Speedy Bites", Manager = "Ray Walsh", ManagerHue = 0.55f, State = w.T("reallife_co_row_lic_auto", "Renews itself"), StateTone = UiRole.Ok, Day = w.F("reallife_mgr_row_day", "Day {day}", "day", "241"), Open = CA(CompanyDo.Go, "lic:Speedy Bites~7:0", CompanyView.Tax, CompanyView.SubLicence) });
        lic.Add(new CompanyLicence { Id = "lic:Speedy Bites~7:4", Icon = UiIcon.Fire, Name = Lic(w, "fire"), Place = "Speedy Bites", Manager = "Ray Walsh", ManagerHue = 0.55f, State = w.T("reallife_co_row_lic_soon", "Runs out soon"), StateTone = UiRole.Warn, Day = w.F("reallife_mgr_row_day", "Day {day}", "day", "217"), Open = CA(CompanyDo.Go, "lic:Speedy Bites~7:4", CompanyView.Tax, CompanyView.SubLicence),
            HasAction = true, Action = CB(w.T("reallife_co_lic_renew", "Renew"), UiButtonKind.Secondary, UiAction.Count, CA(CompanyDo.LicenceBuy, "Speedy Bites~7", 4), UiIcon.Check) });
        lic.Add(new CompanyLicence { Id = "lic:Bloom & Co~214:3", Icon = UiIcon.Tag, Name = Lic(w, "signage"), Place = "Bloom & Co", Manager = "Tessa Brandt", ManagerHue = 0.9f, State = w.T("reallife_co_row_lic_missing", "Missing"), StateTone = UiRole.Danger, Open = CA(CompanyDo.Go, "lic:Bloom & Co~214:3", CompanyView.Tax, CompanyView.SubLicence),
            HasAction = true, Action = CB(w.T("reallife_co_lic_buy", "Apply"), UiButtonKind.Secondary, UiAction.Count, CA(CompanyDo.LicenceBuy, "Bloom & Co~214", 3), UiIcon.Check) });
        lic.Add(new CompanyLicence { Id = "lic:Hudson Warehouse~4:7", Icon = UiIcon.Lock, Name = Lic(w, "bonded"), Place = "Hudson Warehouse", Manager = "Omar Haddad", ManagerHue = 0.75f, State = w.T("reallife_co_row_lic_pending", "Pending"), StateTone = UiRole.Warn, Day = w.F("reallife_mgr_row_day", "Day {day}", "day", "216"), Open = CA(CompanyDo.Go, "lic:Hudson Warehouse~4:7", CompanyView.Tax, CompanyView.SubLicence) });
        lic.Add(new CompanyLicence { Id = "lic:Bloom & Co~214:0", Icon = UiIcon.Drop, Name = Lic(w, "hygiene"), Place = "Bloom & Co", Manager = "Tessa Brandt", ManagerHue = 0.9f, State = w.T("reallife_co_row_lic_valid", "Valid"), StateTone = UiRole.Ok, Day = w.F("reallife_mgr_row_day", "Day {day}", "day", "250"), Open = CA(CompanyDo.Go, "lic:Bloom & Co~214:0", CompanyView.Tax, CompanyView.SubLicence) });
        if (one)
        {
            lic.Clear();
            lic.Add(new CompanyLicence { Id = "lic:Corner Cafe~12:0", Icon = UiIcon.Drop, Name = CompanyTextCapital(w.T("reallife_co_licence_hygiene", "food hygiene")), Place = "Corner Cafe", State = w.T("reallife_co_row_lic_valid", "Valid"), StateTone = UiRole.Ok, Day = w.F("reallife_mgr_row_day", "Day {day}", "day", "140"),
                Open = CA(CompanyDo.Go, "lic:Corner Cafe~12:0", CompanyView.Tax, CompanyView.SubLicence) });
            lic.Add(new CompanyLicence { Id = "lic:Corner Cafe~12:3", Icon = UiIcon.Tag, Name = Lic(w, "signage"), Place = "Corner Cafe", State = w.T("reallife_co_row_lic_missing", "Missing"), StateTone = UiRole.Danger, Open = CA(CompanyDo.Go, "lic:Corner Cafe~12:3", CompanyView.Tax, CompanyView.SubLicence),
                HasAction = true, Action = CB(w.T("reallife_co_lic_buy", "Apply"), UiButtonKind.Secondary, UiAction.Count, CA(CompanyDo.LicenceBuy, "Corner Cafe~12", 3), UiIcon.Check) });
        }
        t.Licences = lic.ToArray();
        t.LicencesCount = lic.Count.ToString();
        t.ComingUp = one ? new[] { new CompanyDone { When = w.F("reallife_mgr_row_day", "Day {day}", "day", "90"), Text = w.F("reallife_co_coming_quarter", "Quarter {quarter} closes", "quarter", "1"), Open = CA(CompanyDo.Go, "ret", CompanyView.Tax, CompanyView.SubReturn) } } : new[]
        {
            new CompanyDone { When = w.F("reallife_mgr_row_day", "Day {day}", "day", "217"), Text = w.F("reallife_co_coming_licence", "{licence} runs out at {business}", "licence", Lic(w, "fire"), "business", "Speedy Bites") },
            new CompanyDone { When = w.F("reallife_mgr_row_day", "Day {day}", "day", "218"), Text = w.F("reallife_co_coming_return", "Return for quarter {quarter} is due", "quarter", "2"), Open = CA(CompanyDo.Go, "ret", CompanyView.Tax, CompanyView.SubReturn) },
            new CompanyDone { When = w.F("reallife_mgr_row_day", "Day {day}", "day", "218"), Text = w.F("reallife_co_coming_topup", "{name} tops up the reserve", "name", "Noah Feldman"), Money = "-" + CM(1840), Tone = -1 },
            new CompanyDone { When = w.F("reallife_mgr_row_day", "Day {day}", "day", "230"), Text = w.F("reallife_co_coming_accountant", "{name}'s fee", "name", Tr(w, "Careful")), Money = "-" + CM(620), Tone = -1 }
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
            new CompanyItem { Title = w.F("reallife_co_row_quarter", "Quarter {quarter}", "quarter", "1"), Sub = w.F("reallife_mgr_row_day", "Day {day}", "day", "96"), Figure = CM(3120), Chip = w.T("reallife_ibox_chip_late", "Late"), ChipTone = UiRole.Warn },
            new CompanyItem { Title = w.F("reallife_co_row_quarter", "Quarter {quarter}", "quarter", "4"), Sub = w.F("reallife_mgr_row_day", "Day {day}", "day", "7"), Figure = CM(2980), Chip = w.T("reallife_co_paid_on_time", "On time"), ChipTone = UiRole.Ok }
        };
        t.ToolsTitle = w.T("reallife_co_tax_tools", "Return, accountant and books");
        t.Tools = new[]
        {
            new CompanyItem { Title = w.T("reallife_co_row_return", "Tax return"), Sub = w.F("reallife_co_row_quarter", "Quarter {quarter}", "quarter", one ? "1" : "2"), Chip = one ? w.T("reallife_co_row_return_running", "Running") : w.T("reallife_co_row_return_due", "Due"), ChipTone = one ? UiRole.Muted : UiRole.Warn, Press = true, Act = CA(CompanyDo.Go, "ret", CompanyView.Tax, CompanyView.SubReturn) },
            new CompanyItem { Title = w.T("reallife_co_row_accountant", "Accountant"), Sub = one ? w.T("reallife_co_none", "None") : Tr(w, "Careful"), Figure = one ? null : w.F("reallife_co_row_a_month", "{amount} a month", "amount", CM(620)), Press = true, Act = CA(CompanyDo.Go, "acc", CompanyView.Tax, CompanyView.SubAccountant) },
            new CompanyItem { Title = one ? "Corner Cafe" : "Speedy Bites", Sub = w.T("reallife_co_row_hidden", "Cash off the books"), Figure = one ? "0%" : "5%", FigureTone = one ? UiRole.None : UiRole.WarnText, Press = true, Act = CA(CompanyDo.Go, one ? "hide:Corner Cafe~12" : "hide:Speedy Bites~7", CompanyView.Tax, CompanyView.SubHide) }
        };
        t.PropertyTitle = w.T("reallife_co_property_tax", "Property tax");
        t.Property = one ? new CompanyStatementLine[0] : new[] { new CompanyStatementLine { Label = "Hudson Warehouse", Value = CM(2140) } };
        t.PropertyNote = one ? w.T("reallife_co_property_rented", "Rented premises: the property tax is inside the rent.") : w.T("reallife_co_property_note", "Counted so far this tax year.");
        if (one)
        {
            // A first quarter, seven days in: how far through it the day is, what each accountant costs and does, and the permits the cafe does not need yet.
            t.QuarterLen = 90;
            t.QuarterDay = 7;
            t.QuarterDayText = w.F("reallife_co_quarter_day", "Day {day} of {days}", "day", "7", "days", "90");
            t.TermsTitle = w.T("reallife_co_acc_terms_sec", "What an accountant costs");
            t.Terms = AccountantTermRows(w);
            t.ExtraTitle = w.T("reallife_co_lic_not_needed", "Permits this business does not need");
            string[] kinds = { "fire", "seating", "delivery", "bonded" }, names = { "fire safety certificate", "outdoor seating permit", "delivery vehicle permit", "bonded storage permit" };
            int[] permitFees = { 350, 250, 200, 600 }, days = { 60, 30, 30, 60 };
            var extra = new CompanyItem[4];
            for (int i = 0; i < 4; i++)
            {
                extra[i] = new CompanyItem { Title = CompanyTextCapital(w.T("reallife_co_licence_" + kinds[i], names[i])), Sub = w.F("reallife_co_lic_term_value", "{days} days", "days", days[i].ToString()), Figure = CM(permitFees[i]), Grade = -1 };
            }
            t.Extra = extra;
        }
        if (state == "tax_lead" || state == "tax_big")
        {
            t.HasLead = true;
            t.LeadKicker = w.F("reallife_co_lead_chief", "Finance chief, {name}", "name", "Noah Feldman");
            t.LeadTitle = w.F("reallife_co_lead_reserve", "The year's tax so far is {tax}. {set} is set aside.", "tax", CM(11160), "set", CM(2420));
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
                    CSec(w.T("reallife_ibox_sec_return", "The return")), CRow(w.L("reallife_co_return_quarter", "Quarter"), "2"), CRow(w.L("reallife_co_return_takings", "Takings"), CM(68400)), CRow(w.L("reallife_co_return_deduct", "Costs that count"), CM(47100)),
                    new CompanyBlock { Kind = CompanyBlockKind.Break },
                    CNote(w.F("reallife_co_return_rebate", "{name} files it for you and takes back {amount}.", "name", Tr(w, "Careful"), "amount", CM(210)), UiRole.OkText),
                    CBtns(CB(w.T("reallife_co_return_file", "File the return"), UiButtonKind.Primary, UiAction.Alt, CA(CompanyDo.ReturnFile), UiIcon.Check)),
                    CSec(w.T("reallife_co_acc_terms_sec", "What an accountant costs")), new CompanyBlock { Kind = CompanyBlockKind.Items, Items = AccountantTermRows(w) }
                }
            };
        }
        else if (state == "tax_accountant")
        {
            t.Card = new CompanyCard
            {
                Kicker = w.T("reallife_co_tab_tax", "Tax and licences"), Title = w.T("reallife_co_row_accountant", "Accountant"), Icon = UiIcon.Scales, Back = back,
                FactLabels = new[] { w.T("reallife_co_f_hired", "Hired now"), w.T("reallife_co_f_fee", "Fee") }, FactValues = new[] { Tr(w, "Careful"), w.F("reallife_co_row_a_month", "{amount} a month", "amount", CM(620)) },
                Blocks = new[]
                {
                    CSec(w.T("reallife_ibox_sec_terms", "Terms")),
                    new CompanyBlock { Kind = CompanyBlockKind.Choice, Text = w.T("reallife_co_acc_tier", "Accountant"), Options = new[] { Tr(w, "Careless"), Tr(w, "Careful"), Tr(w, "Honest") }, Chosen = 2, Acts = new[] { CA(CompanyDo.Go, "kind", 0), CA(CompanyDo.Go, "kind", 1), CA(CompanyDo.Go, "kind", 2) } },
                    CNote(w.F("reallife_co_acc_files", "Files the return for you within {days} days.", "days", "7")), new CompanyBlock { Kind = CompanyBlockKind.Break },
                    CNote(w.F("reallife_co_acc_saves", "Saves {share} of the tax.", "share", "4%"), UiRole.OkText),
                    CNote(w.F("reallife_co_acc_risk", "Adds {share} to the audit risk.", "share", "0.2%")), CNote(w.T("reallife_co_acc_honest_stays", "Stays if you take cash off the books, and files an honest return.")),
                    CSec(w.T("reallife_co_acc_terms_sec", "What an accountant costs")), new CompanyBlock { Kind = CompanyBlockKind.Items, Items = AccountantTermRows(w) },
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
                    CSec(w.T("reallife_ibox_sec_risk", "The risk")), new CompanyBlock { Kind = CompanyBlockKind.Meter, Text = w.T("reallife_co_f_risk", "Audit risk"), Value = "0.4%", Number = 1, Tone = UiRole.Warn },
                    CNote(w.F("reallife_co_hide_risk", "The audit chance grows {grow} a day for each per cent and falls {fall} a day, to at most {most}.", "grow", "0.2", "fall", "0.1", "most", "30%")),
                    CNote(w.F("reallife_co_hide_audit", "An audit fines {fine} of what was hidden, and goes to court from {sum}.", "fine", "50%", "sum", CM(5000))), CNote(w.T("reallife_co_hide_threshold", "Nothing is checked while the sum stays small.")),
                    CSec(w.T("reallife_co_hide_every", "Every business"), "3"),
                    new CompanyBlock { Kind = CompanyBlockKind.Items, Items = new[]
                    {
                        new CompanyItem { Grade = -1, Title = "Speedy Bites", Icon = UiIcon.Store, Sub = w.T("reallife_co_row_hidden", "Cash off the books"), Figure = "5%", Chip = w.T("reallife_ui_bizops_pick", "Picked"), ChipTone = UiRole.Ok },
                        new CompanyItem { Grade = -1, Title = "Bloom & Co", Icon = UiIcon.Store, Sub = w.T("reallife_co_row_hidden", "Cash off the books"), Figure = "0%", Press = true, Act = CA(CompanyDo.Go, "hide:Bloom & Co~214", CompanyView.Tax, CompanyView.SubHide) },
                        new CompanyItem { Grade = -1, Title = "Hudson Warehouse", Icon = UiIcon.Store, Sub = w.T("reallife_co_row_hidden", "Cash off the books"), Figure = "10%", Press = true, Act = CA(CompanyDo.Go, "hide:Hudson Warehouse~3", CompanyView.Tax, CompanyView.SubHide) }
                    } }
                }
            };
        }
        else if (state == "licence_card")
        {
            t.Card = new CompanyCard
            {
                Kicker = "Bloom & Co", Title = Lic(w, "signage"), Icon = UiIcon.Tag, Back = back, Chips = new[] { w.T("reallife_ibox_chip_missing", "Missing") }, ChipTones = new[] { UiRole.Danger },
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
                    CNote(w.F("reallife_co_lic_why_signage", "A sign over the door needs a permit.", "hour", "22", "early", "6")),
                    CSec(w.F("reallife_co_lic_others", "Other permits at {business}", "business", "Bloom & Co"), "2"),
                    new CompanyBlock { Kind = CompanyBlockKind.Items, Items = new[]
                    {
                        new CompanyItem { Grade = -1, Title = CompanyTextCapital(w.T("reallife_co_licence_hygiene", "food hygiene")), Icon = UiIcon.Drop, Sub = w.F("reallife_co_lic_term_value", "{days} days", "days", "90"), Figure = CM(180), Chip = w.T("reallife_ibox_chip_valid", "Valid"), ChipTone = UiRole.Ok, Press = true, Act = CA(CompanyDo.Go, "lic:Bloom & Co~214:0", CompanyView.Tax, CompanyView.SubLicence) },
                        new CompanyItem { Grade = -1, Title = CompanyTextCapital(w.T("reallife_co_licence_late", "late opening")), Icon = UiIcon.Clock, Sub = w.F("reallife_co_lic_term_value", "{days} days", "days", "60"), Figure = CM(260), Chip = w.T("reallife_co_row_lic_pending", "Pending"), ChipTone = UiRole.Warn, Press = true, Act = CA(CompanyDo.Go, "lic:Bloom & Co~214:2", CompanyView.Tax, CompanyView.SubLicence) }
                    } }
                }
            };
        }
    }
}
