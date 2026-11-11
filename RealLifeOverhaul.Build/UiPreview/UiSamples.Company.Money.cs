using System.Collections.Generic;
using System.Linq;
using RealLifeOverhaul.Ui;

// The Company app's Marketing, Cover and Tax tabs (the Rota's are in UiSamples.Company.Rota.cs). Samples' own names; the live source reads the player's businesses. Every campaign, policy,
// permit, quarter and day comes from the sample world (UiSamples.Company.World.cs).
internal static partial class UiSamples
{
    // ------------------------------------------------------------------------------------------------- Marketing and Cover

    private static string PlusPercent(int p) => (p >= 0 ? "+" : "-") + System.Math.Abs(p) + "%";

    private static int ReturnOf(CReport r) => r.Cost == 0 ? 0 : r.Extra * 100 / r.Cost;

    private static string ExtraNote(CompanyWords w, int extra) => extra >= 0 ? w.F("reallife_co_row_extra", "{amount} extra", "amount", CM(extra)) : w.F("reallife_co_row_less", "{amount} less", "amount", CM(-extra));

    private static int LastDayOf(CReport r) => r.From + r.Days - 1;

    // A report as a row of the campaigns list and as a line under a business.
    private static CompanyRow ReportRow(CompanyWords w, CReport r) => new CompanyRow
    {
        Id = "rep:" + r.Index, Title = Tr(w, r.Kind), Sub = Cw.Pl(r.Place).Name + ", " + Dy(w, LastDayOf(r)), Icon = UiIcon.ClipboardText, Figure = PlusPercent(r.Percent), FigureTone = UiRole.OkText, Note = ExtraNote(w, r.Extra)
    };

    private static CompanyItem EarlierItem(CompanyWords w, CReport r) => new CompanyItem
    {
        Grade = -1, Title = Tr(w, r.Kind), Sub = Dy(w, LastDayOf(r)), Icon = UiIcon.ClipboardText, Figure = PlusPercent(r.Percent), FigureTone = UiRole.OkText, Note = ExtraNote(w, r.Extra), Press = true,
        Act = CA(CompanyDo.Go, "rep:" + r.Index, CompanyView.Marketing)
    };

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
        bool one = Cw.Places.Count == 1;
        var rows = new List<CompanyRow>();
        foreach (CPlace pl in Cw.Places)
        {
            CCampaign camp = Cw.CampaignAt(pl.Key);
            rows.Add(camp == null
                ? new CompanyRow { Id = "push:" + pl.Key, Title = pl.Name, Sub = w.T("reallife_co_none", "None"), Icon = UiIcon.Store }
                : new CompanyRow { Id = "push:" + pl.Key, Title = pl.Name, Sub = Tr(w, camp.Kind) + ", " + Dy(w, Cw.Days(camp).Last), Icon = UiIcon.Megaphone, Figure = PlusPercent(camp.Percent), FigureTone = UiRole.OkText, Note = CM(camp.Cost),
                    Chip = w.T("reallife_ibox_chip_running", "Running"), ChipTone = UiRole.Ok });
        }
        if (Cw.Reports.Count > 0)
        {
            rows.Add(new CompanyRow { Caption = true, Title = w.T("reallife_co_reports", "Reports") });
            foreach (CReport r in Cw.Reports.OrderByDescending(x => x.Index))
            {
                rows.Add(ReportRow(w, r));
            }
        }
        p.List = new CompanyList { Title = w.T("reallife_co_tab_marketing", "Marketing"), Count = Cw.Places.Count.ToString(), Rows = rows.ToArray() };
        string pick = state == "marketing_running" ? "push:" + Cw.Pl("Speedy Bites").Key : state == "marketing_report" ? "rep:2" : state == "marketing_summary" ? null : one ? "push:" + Cw.Places[0].Key : "push:" + Cw.Pl("Bloom & Co").Key;
        for (int i = 0; i < rows.Count; i++)
        {
            p.List.Chosen = rows[i].Id == pick ? i : p.List.Chosen;
        }
        if (pick == null)
        {
            p.List.Chosen = -1;
            var kinds = new List<CompanyItem>();
            foreach (CKind k in CompanyWorld.CampaignKinds)
            {
                List<CReport> of = Cw.Reports.Where(x => x.Kind == k.Name).ToList();
                if (of.Count == 0)
                {
                    continue;
                }
                int average = of.Sum(x => ReturnOf(x)) / of.Count;
                kinds.Add(new CompanyItem { Grade = -1, Title = Tr(w, k.Name), Icon = UiIcon.Megaphone, Sub = w.F("reallife_co_push_n", "Campaigns: {count}", "count", of.Count.ToString()), Figure = PlusPercent(average),
                    FigureTone = average > 0 ? UiRole.OkText : UiRole.DangerText, Note = w.T("reallife_co_report_return_avg", "return") });
            }
            p.Top = new CompanyCard
            {
                Kicker = w.T("reallife_co_tab_marketing", "Marketing"), Title = w.T("reallife_co_marketing_title", "Campaigns"), Icon = UiIcon.Megaphone,
                Blocks = new[]
                {
                    CNote(w.T("reallife_co_push_intro", "A campaign draws more people to a business for some days.")),
                    new CompanyBlock { Kind = CompanyBlockKind.Figures, Options = new[] { w.T("reallife_co_f_spent", "Spent"), w.T("reallife_co_f_extra", "Extra profit"), w.T("reallife_co_f_campaigns", "Campaigns") },
                        Values = new[] { CM(Cw.Reports.Sum(x => x.Cost)), CM(Cw.Reports.Sum(x => x.Extra)), Cw.Reports.Count.ToString() } },
                    CSec(w.T("reallife_co_push_kinds_sec", "Which kind pays"), kinds.Count.ToString()),
                    new CompanyBlock { Kind = CompanyBlockKind.Items, Items = kinds.ToArray() },
                    CSec(w.T("reallife_ibox_sec_how", "How it is measured")),
                    CNote(w.T("reallife_co_push_measure", "The customers and takings of the days before are set against the days during.")), CNote(w.T("reallife_co_push_game", "The game counts the effect in its own footfall.")),
                    CNote(w.T("reallife_co_push_pick", "Pick a business to book a campaign."))
                }
            };
            return;
        }
        var c = new CompanyCard { Kicker = w.T("reallife_co_tab_marketing", "Marketing"), Icon = UiIcon.Megaphone };
        if (pick.StartsWith("rep:", System.StringComparison.Ordinal))
        {
            CReport r = Cw.Reports.First(x => "rep:" + x.Index == pick);
            CPlace at = Cw.Pl(r.Place);
            c.Kicker = at.Name;
            c.Title = Tr(w, r.Kind);
            c.Icon = UiIcon.ClipboardText;
            c.Sub = w.F("reallife_co_report_days", "Days {from} to {to}, {days} days", "from", Dn(r.From), "to", Dn(LastDayOf(r)), "days", r.Days.ToString());
            c.Chips = new[] { PlusPercent(ReturnOf(r)) };
            c.ChipTones = new[] { r.Extra >= 0 ? UiRole.Ok : UiRole.Danger };
            c.FactLabels = new[] { w.T("reallife_co_f_cost", "Cost"), w.T("reallife_co_f_extra", "Extra profit") };
            c.FactValues = new[] { CM(r.Cost), CM(r.Extra) };
            // The days before and during, from the business's own day: a quiet day sold less before the campaign, more during it by the footfall it drew.
            int perDay = at.Takings * 3 / 4, during = perDay * (100 + r.Percent) / 100, customers = perDay / 7, customersDuring = customers * (100 + r.Percent) / 100;
            int profitBefore = r.Extra < 0 ? 0 : 60, profitDuring = profitBefore + r.Extra / r.Days;
            c.Blocks = new[]
            {
                CSec(w.T("reallife_ibox_sec_before_during", "Before and during")),
                new CompanyBlock { Kind = CompanyBlockKind.Compare, Text = w.T("reallife_co_f_customers", "Customers"), Value = (customers * r.Days).ToString(), Sub = (customersDuring * r.Days) + " (" + PlusPercent(r.Percent) + ")", Tone = UiRole.OkText,
                    Options = new[] { w.T("reallife_co_report_before", "Before"), w.T("reallife_co_report_during", "During") } },
                new CompanyBlock { Kind = CompanyBlockKind.Compare, Text = w.T("reallife_co_f_takings", "Takings"), Value = CM(perDay * r.Days), Sub = CM(during * r.Days), Tone = UiRole.OkText },
                new CompanyBlock { Kind = CompanyBlockKind.Compare, Text = w.T("reallife_co_f_profit", "Profit"), Value = CM(profitBefore * r.Days), Sub = CM(profitDuring * r.Days), Tone = r.Extra >= 0 ? UiRole.OkText : UiRole.DangerText },
                CSec(w.T("reallife_co_report_per_day", "A day on average")),
                new CompanyBlock { Kind = CompanyBlockKind.Compare, Text = w.T("reallife_co_f_customers", "Customers"), Value = customers.ToString(), Sub = customersDuring.ToString(), Tone = UiRole.OkText, Options = new[] { w.T("reallife_co_report_before", "Before"), w.T("reallife_co_report_during", "During") } },
                new CompanyBlock { Kind = CompanyBlockKind.Compare, Text = w.T("reallife_co_f_takings", "Takings"), Value = CM(perDay), Sub = CM(during), Tone = UiRole.OkText },
                new CompanyBlock { Kind = CompanyBlockKind.Compare, Text = w.T("reallife_co_f_profit", "Profit"), Value = CM(profitBefore), Sub = CM(profitDuring), Tone = r.Extra >= 0 ? UiRole.OkText : UiRole.DangerText },
                new CompanyBlock { Kind = CompanyBlockKind.Break },
                CSec(w.T("reallife_ibox_sec_money", "The money")),
                CRow(w.T("reallife_co_f_extra", "Extra profit"), CM(r.Extra), r.Extra >= 0 ? UiRole.OkText : UiRole.DangerText), CRow(w.L("reallife_co_report_return", "Return on the cost"), PlusPercent(ReturnOf(r)), r.Extra >= 0 ? UiRole.OkText : UiRole.DangerText),
            };
            List<CReport> earlier = Cw.Reports.Where(x => x.Place == r.Place && x != r).OrderByDescending(x => x.Index).ToList();
            if (earlier.Count > 0)
            {
                c.Blocks = c.Blocks.Concat(new[] { CSec(w.T("reallife_co_push_earlier", "Earlier campaigns here")), new CompanyBlock { Kind = CompanyBlockKind.Items, Items = earlier.Select(x => EarlierItem(w, x)).ToArray() } }).ToArray();
            }
            p.Card = c;
            return;
        }
        CPlace shop = Cw.Pl(pick.Substring(5));
        CCampaign running = Cw.CampaignAt(shop.Key);
        List<CReport> before = Cw.Reports.Where(x => x.Place == shop.Key).OrderByDescending(x => x.Index).ToList();
        if (running != null)
        {
            CKind kind = CompanyWorld.KindOf(running.Kind);
            CompanyDays.Campaign days = Cw.Days(running);
            List<CReport> ofKind = Cw.Reports.Where(x => x.Kind == running.Kind).ToList();
            c.Title = shop.Name;
            c.Chips = new[] { w.T("reallife_ibox_chip_running", "Running") };
            c.ChipTones = new[] { UiRole.Ok };
            c.FactLabels = new[] { w.T("reallife_co_f_cost", "Cost"), w.T("reallife_co_f_effect", "Effect on customers") };
            c.FactValues = new[] { CM(running.Cost), PlusPercent(running.Percent) };
            var blocks = new List<CompanyBlock>
            {
                CSec(w.T("reallife_ibox_sec_campaign", "The campaign")), CRow(w.L("reallife_co_push_running", "Running"), Tr(w, running.Kind)),
                new CompanyBlock { Kind = CompanyBlockKind.Meter, Text = w.F("reallife_co_push_progress", "Day {done} of {days}", "done", days.Done.ToString(), "days", days.Length.ToString()), Value = w.F("reallife_co_push_left", "{count} days left", "count", days.Left.ToString()),
                    Number = days.Done * 100 / days.Length, Tone = UiRole.Ok },
                CNote(w.F("reallife_co_push_report_due", "A report comes on day {day}.", "day", Dn(days.Report))),
                CSec(w.T("reallife_co_push_parts_sec", "What makes up the effect")),
                CRow(w.T("reallife_co_push_part_own", "The campaign's own figure"), "+" + kind.Own + "%"), CRow(w.T("reallife_co_push_part_district", "The district counts for"), shop.Pct + "%"),
                CRow(w.T("reallife_co_push_part_economy", "The economy counts for"), "100%")
            };
            if (ofKind.Count > 0)
            {
                blocks.Add(CSec(w.F("reallife_co_push_kind_record", "{kind} so far", "kind", Tr(w, running.Kind))));
                blocks.Add(CRow(w.T("reallife_co_f_campaigns", "Campaigns"), ofKind.Count.ToString()));
                blocks.Add(CRow(w.L("reallife_co_report_return", "Return on the cost"), PlusPercent(ofKind.Sum(x => ReturnOf(x)) / ofKind.Count), UiRole.OkText));
                blocks.Add(CRow(w.T("reallife_co_f_extra", "Extra profit"), CM(ofKind.Sum(x => x.Extra))));
            }
            if (before.Count > 0)
            {
                blocks.Add(CSec(w.T("reallife_co_push_earlier", "Earlier campaigns here")));
                blocks.Add(new CompanyBlock { Kind = CompanyBlockKind.Items, Items = before.Select(x => EarlierItem(w, x)).ToArray() });
            }
            c.Blocks = blocks.ToArray();
            p.Card = c;
            return;
        }
        CKind first = CompanyWorld.CampaignKinds[0];
        c.Title = shop.Name;
        c.FactLabels = new[] { w.T("reallife_co_f_cost", "Cost"), w.T("reallife_co_f_effect", "Effect on customers") };
        c.FactValues = new[] { CM(first.Cost), PlusPercent(Cw.Effect(first, shop)) };
        var book = new List<CompanyBlock>
        {
            CSec(w.T("reallife_ibox_sec_campaign", "The campaign")),
            new CompanyBlock { Kind = CompanyBlockKind.Items, Items = CompanyWorld.CampaignKinds.Select((k, i) => new CompanyItem
            {
                Title = Tr(w, k.Name), Icon = UiIcon.Megaphone, Tint = UiRole.None, Press = true, Grade = -1, Sub = w.F("reallife_co_lic_term_value", "{days} days", "days", k.Days.ToString()), Figure = PlusPercent(Cw.Effect(k, shop)),
                FigureTone = UiRole.OkText, Note = CM(k.Cost), Chip = i == 0 ? w.T("reallife_ui_bizops_pick", "Picked") : null, ChipTone = UiRole.Ok, Act = CA(CompanyDo.Go, "kind", i)
            }).ToArray() },
            CNote(w.T("reallife_co_push_about_flyers", "Flyers through every door nearby.")),
            CRow(w.L("reallife_co_push_length", "Length"), w.F("reallife_co_push_length_value", "{days} days from day {from}", "days", first.Days.ToString(), "from", Dn(Cw.Today + 1))),
            CSec(w.T("reallife_co_push_parts_sec", "What makes up the effect")),
            CRow(w.T("reallife_co_push_part_own", "The campaign's own figure"), "+" + first.Own + "%"), CRow(w.T("reallife_co_push_part_district", "The district counts for"), shop.Pct + "%"),
            CRow(w.T("reallife_co_push_part_economy", "The economy counts for"), "100%")
        };
        if (before.Count > 0)
        {
            book.Add(CSec(w.T("reallife_co_push_earlier", "Earlier campaigns here")));
            book.Add(new CompanyBlock { Kind = CompanyBlockKind.Items, Items = before.Select(x => EarlierItem(w, x)).ToArray() });
        }
        book.Add(CSec(w.T("reallife_ibox_sec_how", "How it is measured")));
        book.Add(CNote(w.T("reallife_co_push_measure", "The customers and takings of the days before are set against the days during.")));
        book.Add(CBtns(CB(w.T("reallife_co_push_book", "Book the campaign"), UiButtonKind.Primary, UiAction.Alt, CA(CompanyDo.CampaignBook, shop.Key, 0), UiIcon.Check)));
        c.Blocks = book.ToArray();
        p.Card = c;
    }

    private static string CoverName(CompanyWords w, string cover) => w.T("reallife_co_cover_" + cover.ToLowerInvariant(), cover);

    private static UiIcon CoverIcon(string cover) => cover == "Fire" ? UiIcon.Fire : cover == "Theft" ? UiIcon.Lock : UiIcon.ShieldCheck;

    private static CCover CoverAt(string place, string cover) => Cw.Covers.FirstOrDefault(x => x.Place == place && x.Cover == cover);

    private static void CompanyCover(CompanyFacts f, CompanyWords w, string state)
    {
        CompanyPage p = f.Cover;
        var rows = new List<CompanyRow>();
        CClaim open = Cw.Claims.FirstOrDefault(x => x.Open);
        if (open != null)
        {
            rows.Add(new CompanyRow { Id = "claim:1", Title = Cw.Pl(open.Place).Name, Sub = w.F("reallife_co_row_claim", "Claim, {cover}", "cover", CoverName(w, open.Cover).ToLowerInvariant()) + ", " + CoverAt(open.Place, open.Cover).Insurer, Icon = UiIcon.ShieldCheck,
                Figure = w.F("reallife_co_row_claim_by", "by day {day}", "day", Dn(open.By)), Chip = w.T("reallife_ibox_chip_open", "Open"), ChipTone = UiRole.Warn });
        }
        foreach (CPlace pl in Cw.Places)
        {
            for (int k = 0; k < CompanyWorld.CoverKinds.Length; k++)
            {
                string cover = CompanyWorld.CoverKinds[k];
                CCover held = CoverAt(pl.Key, cover);
                rows.Add(new CompanyRow { Id = "pol:" + pl.Key + ":" + k, Title = w.F("reallife_co_row_cover_title", "{cover} cover", "cover", CompanyTextCapital(CoverName(w, cover))), Sub = pl.Name + ", " + (held != null ? held.Insurer : w.T("reallife_co_none", "None")),
                    Icon = CoverIcon(cover), Figure = held != null ? w.F("reallife_co_row_a_day", "{amount} a day", "amount", CM(held.Premium)) : null, Chip = held == null ? w.T("reallife_ibox_chip_not_covered", "Not covered") : null, ChipTone = UiRole.Warn });
            }
        }
        p.List = new CompanyList { Title = w.T("reallife_co_tab_cover", "Cover"), Count = rows.Count.ToString(), Rows = rows.ToArray() };
        string speedy = Cw.Pl("Speedy Bites").Key;
        string pick = state == "cover_claim" ? "claim:1" : state == "cover_covered" ? "pol:" + speedy + ":0" : state == "cover_summary" ? null : "pol:" + speedy + ":1";
        for (int i = 0; i < rows.Count; i++)
        {
            p.List.Chosen = rows[i].Id == pick ? i : p.List.Chosen;
        }
        if (pick == null)
        {
            p.List.Chosen = -1;
            var held = new List<CompanyItem>();
            for (int k = 0; k < CompanyWorld.CoverKinds.Length; k++)
            {
                string cover = CompanyWorld.CoverKinds[k];
                int n = Cw.Covers.Count(x => x.Cover == cover);
                held.Add(new CompanyItem { Title = w.F("reallife_co_row_cover_title", "{cover} cover", "cover", CompanyTextCapital(CoverName(w, cover))), Sub = w.F("reallife_co_covered_of", "{count} of {total} businesses", "count", n.ToString(), "total", Cw.Places.Count.ToString()),
                    Icon = CoverIcon(cover), Grade = n * 100 / Cw.Places.Count, Figure = null });
            }
            var recent = Cw.Claims.OrderByDescending(x => x.Day).ThenBy(x => x.Cover, System.StringComparer.Ordinal).Take(2).ToList();
            p.Top = new CompanyCard
            {
                Kicker = w.T("reallife_co_tab_cover", "Cover"), Title = w.T("reallife_co_cover_title", "Insurance"), Icon = UiIcon.ShieldCheck,
                FactLabels = new[] { w.T("reallife_co_f_premiums", "Premiums"), w.T("reallife_co_f_claims_open", "Claims to put in") }, FactValues = new[] { w.F("reallife_co_row_a_day", "{amount} a day", "amount", CM(Cw.Covers.Sum(x => x.Premium))), Cw.OpenClaims().ToString() },
                Blocks = new[] { CSec(w.T("reallife_co_cover_what", "What each kind covers")), CNote(w.T("reallife_co_cover_what_fire", "Fire cover pays for the damage a fire does.")), CNote(w.T("reallife_co_cover_what_theft", "Theft cover pays for what is stolen.")),
                    CNote(w.T("reallife_co_cover_what_liability", "Liability cover pays when a customer is hurt.")),
                    CSec(w.T("reallife_co_cover_held", "Held now")),
                    new CompanyBlock { Kind = CompanyBlockKind.Items, Items = held.ToArray() },
                    CSec(w.T("reallife_co_claims_recent", "Latest claims"), recent.Count.ToString()),
                    new CompanyBlock { Kind = CompanyBlockKind.Items, Items = recent.Select(x => new CompanyItem { Grade = -1, Title = w.F("reallife_co_claim_title", "{cover} claim, {business}", "cover", CompanyTextCapital(CoverName(w, x.Cover)), "business", Cw.Pl(x.Place).Name),
                        Sub = Dy(w, x.Day), Icon = UiIcon.ShieldCheck, Chip = x.Open ? w.T("reallife_ibox_chip_open", "Open") : w.T("reallife_hier_log_settled", "Settled"), ChipTone = x.Open ? UiRole.Warn : UiRole.Muted }).ToArray() },
                    CNote(w.T("reallife_co_cover_pick", "Pick a business's cover to see its terms.")) }
            };
            return;
        }
        var c = new CompanyCard { Kicker = w.T("reallife_co_tab_cover", "Cover"), Icon = UiIcon.ShieldCheck };
        if (pick == "claim:1")
        {
            CCover policy = CoverAt(open.Place, open.Cover);
            int loss = 4000, excess = 400;
            List<CClaim> earlier = Cw.Claims.Where(x => x.Place == open.Place && x != open).ToList();
            c.Title = w.F("reallife_co_claim_title", "{cover} claim, {business}", "cover", CompanyTextCapital(CoverName(w, open.Cover)), "business", Cw.Pl(open.Place).Name);
            c.Chips = new[] { w.T("reallife_ibox_chip_open", "Open") };
            c.ChipTones = new[] { UiRole.Warn };
            c.FactLabels = new[] { w.T("reallife_co_f_excess", "Excess"), w.T("reallife_co_f_pays", "Pays") };
            c.FactValues = new[] { CM(excess), CM(loss - excess) };
            var blocks = new List<CompanyBlock>
            {
                CNote(w.F("reallife_co_claim_open", "Put the claim in by day {day}; the insurer pays within {days} days.", "day", Dn(open.By), "days", "5"), UiRole.WarnText),
                CNote(w.F("reallife_co_claim_what_fire", "A fire on day {day} cost {amount}.", "day", Dn(open.Day), "amount", CM(loss))),
                CSec(w.T("reallife_ibox_sec_money", "The money")), CRow(w.L("reallife_co_claim_insurer", "Insurer"), policy.Insurer), CRow(w.T("reallife_co_f_most", "Most it counts"), CM(10000)),
                CRow(w.T("reallife_co_f_excess", "Excess"), CM(excess)), CRow(w.T("reallife_co_f_pays", "Pays"), CM(loss - excess), UiRole.OkText),
                CNote(w.F("reallife_co_claim_counts", "Claims count against you after {most}, and the premium rises {loading}.", "most", "2", "loading", "20%")),
                CBtns(CB(w.T("reallife_co_claim_do", "Put the claim in"), UiButtonKind.Primary, UiAction.Alt, CA(CompanyDo.ClaimFile, null, 1), UiIcon.Check)),
                CSec(w.T("reallife_co_policy_claims_sec", "Claim record")),
                new CompanyBlock { Kind = CompanyBlockKind.Meter, Text = w.T("reallife_tab_co_claim", "Claims"), Value = w.F("reallife_rota_blocks_count", "{count} of {most}", "count", "1", "most", "3"), Number = 33, Tone = UiRole.Ok }
            };
            if (earlier.Count > 0)
            {
                blocks.Add(CSec(w.T("reallife_co_claims_before", "Earlier claims here"), earlier.Count.ToString()));
                blocks.Add(new CompanyBlock { Kind = CompanyBlockKind.Items, Items = earlier.Select(x => new CompanyItem { Grade = -1, Title = w.F("reallife_co_claim_title", "{cover} claim, {business}", "cover", CompanyTextCapital(CoverName(w, x.Cover)), "business", Cw.Pl(x.Place).Name),
                    Sub = Dy(w, x.Day), Icon = UiIcon.ShieldCheck, Chip = w.T("reallife_hier_log_settled", "Settled"), ChipTone = UiRole.Muted }).ToArray() });
            }
            c.Blocks = blocks.ToArray();
        }
        else
        {
            string cover = CompanyWorld.CoverKinds[int.Parse(pick.Substring(pick.LastIndexOf(':') + 1))];
            CCover policy = CoverAt(speedy, cover);
            bool covered = policy != null;
            int most = cover == "Fire" ? 10000 : 5000;
            c.Title = w.F("reallife_co_row_cover_title", "{cover} cover", "cover", CompanyTextCapital(CoverName(w, cover)));
            c.Sub = Cw.Pl(speedy).Name;
            c.Icon = CoverIcon(cover);
            c.Chips = new[] { covered ? w.T("reallife_ibox_chip_covered", "Covered") : w.T("reallife_ibox_chip_not_covered", "Not covered") };
            c.ChipTones = new[] { covered ? UiRole.Ok : UiRole.Warn };
            int premium = covered ? policy.Premium : CompanyWorld.CoverPremium[System.Array.IndexOf(CompanyWorld.CoverKinds, cover)];
            c.FactLabels = new[] { w.T("reallife_co_f_premium", "Premium"), w.T("reallife_co_f_most", "Most it counts") };
            c.FactValues = new[] { w.F("reallife_co_row_a_day", "{amount} a day", "amount", CM(premium)), CM(most) };
            var b = new List<CompanyBlock> { CSec(w.T("reallife_ibox_sec_terms", "Terms")) };
            string[] insurers = { "Budget Mutual", "Safeguard", "Prime Assurance" }, shares = { "15%", "10%", "5%" }, least = { "$300", "$200", "$100" };
            int[] quotes = { premium - 2, premium, premium + 3 };
            if (covered)
            {
                b.Add(CRow(w.L("reallife_co_claim_insurer", "Insurer"), policy.Insurer));
                b.Add(CSec(w.T("reallife_co_policy_quotes", "What each insurer asks")));
                var held = new CompanyItem[3];
                for (int i = 0; i < held.Length; i++)
                {
                    held[i] = new CompanyItem { Title = insurers[i], Icon = UiIcon.Bank, Tint = UiRole.None, Grade = -1, Sub = w.F("reallife_co_policy_excess", "Excess: {share} of the loss, {least} at least.", "share", shares[i], "least", least[i]),
                        Figure = w.F("reallife_co_row_a_day", "{amount} a day", "amount", CM(quotes[i])), Chip = insurers[i] == policy.Insurer ? w.T("reallife_ibox_chip_covered", "Covered") : null, ChipTone = UiRole.Ok };
                }
                b.Add(new CompanyBlock { Kind = CompanyBlockKind.Items, Items = held });
            }
            else
            {
                b.Add(CNote(w.T("reallife_co_policy_pick", "No cover yet. Pick the insurer whose terms suit you.")));
                b.Add(CSec(w.T("reallife_co_policy_quotes", "What each insurer asks")));
                var asks = new CompanyItem[3];
                for (int i = 0; i < asks.Length; i++)
                {
                    asks[i] = new CompanyItem { Title = insurers[i], Icon = UiIcon.Bank, Tint = UiRole.None, Press = true, Grade = -1,
                        Sub = w.F("reallife_co_policy_excess", "Excess: {share} of the loss, {least} at least.", "share", shares[i], "least", least[i]), Figure = w.F("reallife_co_row_a_day", "{amount} a day", "amount", CM(quotes[i])),
                        Chip = i == 1 ? w.T("reallife_ui_bizops_pick", "Picked") : null, ChipTone = UiRole.Ok, Act = CA(CompanyDo.Go, "insurer", i) };
                }
                b.Add(new CompanyBlock { Kind = CompanyBlockKind.Items, Items = asks });
            }
            int claims = Cw.Claims.Count(x => x.Place == speedy && x.Cover == cover);
            b.Add(CNote(w.F("reallife_co_policy_most", "It pays up to {most}, within {days} days.", "most", CM(most), "days", "5")));
            b.Add(new CompanyBlock { Kind = CompanyBlockKind.Break });
            b.Add(CSec(w.T("reallife_co_policy_claims_sec", "Claim record")));
            b.Add(new CompanyBlock { Kind = CompanyBlockKind.Meter, Text = w.T("reallife_tab_co_claim", "Claims"), Value = w.F("reallife_rota_blocks_count", "{count} of {most}", "count", claims.ToString(), "most", "3"), Number = claims * 100 / 3, Tone = UiRole.Ok });
            b.Add(CNote(w.F("reallife_co_policy_claims", "After {most} claims the insurer refuses; each claim raises the premium by {loading}.", "most", "2", "loading", "20%")));
            if (covered)
            {
                b.Add(CNote(w.F("reallife_co_policy_term", "Runs to day {day}; {claims} claims so far.", "day", Dn(policy.Until), "claims", claims.ToString())));
                b.Add(CBtns(CB(w.T("reallife_co_policy_cancel", "Cancel the cover"), UiButtonKind.Secondary, UiAction.Count, CA(CompanyDo.CoverCancel, speedy, 0), UiIcon.Close, false, null, true)));
                // Another feature's quiet second choice under the card's own (CompanyLive.Quiet): a policy in force, the cover's kind.
                b.Add(CBtns(CB(w.T("reallife_scheme_quiet_claim", "A loss that never happened"), UiButtonKind.Quiet, UiAction.Count, CA(CompanyDo.Quiet, speedy, 0, System.Array.IndexOf(CompanyWorld.CoverKinds, cover)))));
            }
            else
            {
                b.Add(CBtns(CB(w.T("reallife_co_policy_buy", "Buy the cover"), UiButtonKind.Primary, UiAction.Alt, CA(CompanyDo.CoverBuy, speedy, 1, 1), UiIcon.Check)));
            }
            c.Blocks = b.ToArray();
        }
        p.Card = c;
    }

    // ------------------------------------------------------------------------------------------------- Tax and licences

    private static readonly int[] AccountantFees = { 300, 700, 1500 }, AccountantSaves = { 20, 40, 70 }, AccountantRisks = { 1000, 800, 600 };
    private const int AccountantHired = 1;   // the tier the company has: "Careful"

    private static CompanyItem[] AccountantTermRows(CompanyWords w)
    {
        var terms = new CompanyItem[3];
        for (int i = 0; i < 3; i++)
        {
            terms[i] = new CompanyItem { Title = w.T("reallife_co_accountant_" + i, i == 0 ? "Quay Street Bookkeeping" : i == 1 ? "Marlow & Finch" : "Ashdown Chartered"),
                Sub = w.F("reallife_co_acc_terms_line", "saves {saves} of the tax, audit risk {risk} of the usual", "saves", AccountantSaves[i] / 10f + "%", "risk", AccountantRisks[i] / 10 + "%"),
                Figure = w.F("reallife_co_row_a_month", "{amount} a month", "amount", CM(AccountantFees[i])), Press = true, Grade = -1, Act = CA(CompanyDo.Go, "acc", CompanyView.Tax, CompanyView.SubAccountant) };
        }
        return terms;
    }

    private static string CompanyTextCapital(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s.Substring(1);

    private static CompanyLicence LicenceRow(CompanyWords w, CLicence l)
    {
        CPlace pl = Cw.Pl(l.Place);
        CPerson mgr = Cw.ManagerOf(pl);
        string state = l.State == "auto" ? w.T("reallife_co_row_lic_auto", "Renews itself") : l.State == "soon" ? w.T("reallife_co_row_lic_soon", "Runs out soon") : l.State == "missing" ? w.T("reallife_co_row_lic_missing", "Missing")
            : l.State == "pending" ? w.T("reallife_co_row_lic_pending", "Pending") : w.T("reallife_co_row_lic_valid", "Valid");
        UiRole tone = l.State == "missing" ? UiRole.Danger : l.State == "soon" || l.State == "pending" ? UiRole.Warn : UiRole.Ok;
        UiIcon icon = l.Kind == "hygiene" ? UiIcon.Drop : l.Kind == "fire" ? UiIcon.Fire : l.Kind == "signage" ? UiIcon.Tag : l.Kind == "late" ? UiIcon.Clock : UiIcon.Lock;
        var row = new CompanyLicence
        {
            Id = "lic:" + l.Place + ":" + l.Code, Icon = icon, Name = Lic(w, l.Kind), Place = pl.Name, Manager = mgr != null ? mgr.Name : null, ManagerHue = mgr != null ? mgr.Hue : 0f, State = state, StateTone = tone,
            Day = l.Missing ? null : Dy(w, l.Until), Open = CA(CompanyDo.Go, "lic:" + l.Place + ":" + l.Code, CompanyView.Tax, CompanyView.SubLicence)
        };
        if (l.State == "soon" || l.State == "missing")
        {
            row.HasAction = true;
            row.Action = CB(l.State == "soon" ? w.T("reallife_co_lic_renew", "Renew") : w.T("reallife_co_lic_buy", "Apply"), UiButtonKind.Secondary, UiAction.Count, CA(CompanyDo.LicenceBuy, l.Place, l.Code), UiIcon.Check);
        }
        return row;
    }

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
        bool one = Cw.Places.Count == 1;
        CTax tax = Cw.Tax;
        CompanyDays.Quarter quarter = Cw.Quarter;
        CSeat chief = Cw.Seat(3);   // the finance chief, where there is one
        int still = Cw.Still();
        t.LicencesTitle = w.T("reallife_co_licences", "Licences and permits");
        t.LicencesEmpty = w.T("reallife_co_lic_intro", "A business needs permits to trade.");
        var lic = Cw.Licences.Select(l => LicenceRow(w, l)).ToArray();
        t.Licences = lic;
        t.LicencesCount = lic.Length.ToString();
        var coming = new List<KeyValuePair<int, CompanyDone>>();
        foreach (CLicence l in Cw.Licences.Where(x => (x.State == "soon" || x.State == "auto" || x.State == "valid") && x.Until - Cw.Today <= 14))
        {
            coming.Add(new KeyValuePair<int, CompanyDone>(l.Until, new CompanyDone { When = Dy(w, l.Until), Text = w.F("reallife_co_coming_licence", "{licence} runs out at {business}", "licence", Lic(w, l.Kind), "business", Cw.Pl(l.Place).Name) }));
        }
        if (tax.ReturnWaiting)
        {
            coming.Add(new KeyValuePair<int, CompanyDone>(tax.ReturnDue, new CompanyDone { When = Dy(w, tax.ReturnDue), Text = w.F("reallife_co_coming_return", "Return for quarter {quarter} is due", "quarter", tax.ReturnQuarter.ToString()),
                Open = CA(CompanyDo.Go, "ret", CompanyView.Tax, CompanyView.SubReturn) }));
        }
        else
        {
            coming.Add(new KeyValuePair<int, CompanyDone>(quarter.Close, new CompanyDone { When = Dy(w, quarter.Close), Text = w.F("reallife_co_coming_quarter", "Quarter {quarter} closes", "quarter", tax.Number.ToString()),
                Open = CA(CompanyDo.Go, "ret", CompanyView.Tax, CompanyView.SubReturn) }));
        }
        if (chief != null && tax.ReserveAuto && still > 0)
        {
            coming.Add(new KeyValuePair<int, CompanyDone>(quarter.Close, new CompanyDone { When = Dy(w, quarter.Close), Text = w.F("reallife_co_coming_topup", "{name} tops up the reserve", "name", chief.Name), Money = "-" + CM(still), Tone = -1 }));
        }
        if (!one)
        {
            int next = 230;   // the day the next month's fee is taken
            coming.Add(new KeyValuePair<int, CompanyDone>(next, new CompanyDone { When = Dy(w, next), Text = w.F("reallife_co_coming_accountant", "{name}'s fee", "name", w.T("reallife_co_accountant_" + AccountantHired, "Careful")),
                Money = "-" + CM(AccountantFees[AccountantHired]), Tone = -1 }));
        }
        coming.Sort((a, b) => a.Key.CompareTo(b.Key));
        t.ComingUp = coming.Select(x => x.Value).ToArray();
        t.QuarterTitle = w.T("reallife_co_quarter_so_far", "This quarter so far");
        t.QuarterRange = w.F("reallife_co_quarter_range", "Q{quarter}, day {from} to {to}", "quarter", tax.Number.ToString(), "from", Dn(tax.From), "to", Dn(quarter.Last));
        int takings = Cw.QuarterTakings(), costs = Cw.QuarterCosts(), taxable = CompanyWorld.Taxable(takings, costs), owed = CompanyWorld.QuarterTax(takings, costs, tax.Percent);
        var lines = new List<CompanyStatementLine>
        {
            new CompanyStatementLine { Label = w.T("reallife_co_q_takings", "Takings"), Value = CM(takings) }, new CompanyStatementLine { Label = w.T("reallife_co_q_costs", "Costs that count"), Value = CM(costs) },
            new CompanyStatementLine { Label = w.T("reallife_co_q_taxable", "Taxable profit"), Value = CM(taxable), Strong = true },
            new CompanyStatementLine { Label = w.F("reallife_co_q_tax", "Business tax at {percent}%", "percent", tax.Percent.ToString()), Value = CM(owed) }
        };
        if (tax.Reserve > 0 || still > 0)
        {
            lines.Add(new CompanyStatementLine { Label = w.T("reallife_co_q_set_aside", "Set aside"), Value = CM(tax.Reserve) });
            lines.Add(new CompanyStatementLine { Label = w.T("reallife_co_q_still", "Still to set aside"), Value = CM(still), Strong = true, Tone = still > 0 ? UiRole.WarnText : UiRole.OkText });
        }
        t.Quarter = lines.ToArray();
        t.QuarterLen = CompanyWorld.QuarterDays;
        t.QuarterDay = quarter.Day;
        t.QuarterDayText = w.F("reallife_co_quarter_day", "Day {day} of {days}", "day", quarter.Day.ToString(), "days", CompanyWorld.QuarterDays.ToString());
        var reserve = new List<CompanyBlock>
        {
            new CompanyBlock { Kind = CompanyBlockKind.Stepper, Text = w.T("reallife_co_reserve_amount", "Set aside"), Sub = w.F("reallife_co_reserve_cash", "{amount} in the cash", "amount", CM(Cw.Cash)), Value = CM(still), CanLess = true, CanMore = true,
                Act = CA(CompanyDo.Go, "amount", -100, still), Act2 = CA(CompanyDo.Go, "amount", 100, still), Act3 = CA(CompanyDo.Go, "amount", 0, still) },
            CBtns(CB(w.F("reallife_co_reserve_do", "Set aside {amount}", "amount", CM(still)), UiButtonKind.Secondary, UiAction.Count, CA(CompanyDo.ReserveSet, null, still), UiIcon.Check),
                CB(w.T("reallife_co_reserve_release", "Release"), UiButtonKind.Secondary, UiAction.Count, CA(CompanyDo.ReserveRelease), UiIcon.Back, tax.Reserve <= 0, tax.Reserve <= 0 ? w.T("reallife_co_reserve_empty", "Nothing is set aside") : null, true))
        };
        if (chief != null)
        {
            reserve.Add(new CompanyBlock { Kind = CompanyBlockKind.Switch, Text = w.T("reallife_co_reserve_auto", "Finance chief tops it up"), Sub = w.F("reallife_co_reserve_auto_on", "{name} does it when a quarter closes", "name", chief.Name),
                On = tax.ReserveAuto, Value = tax.ReserveAuto ? w.T("reallife_co_on", "On") : w.T("reallife_co_off", "Off"), Act = CA(CompanyDo.ReserveAuto, null, tax.ReserveAuto ? 0 : 1) });
        }
        t.ReserveControls = reserve.ToArray();
        t.PaidTitle = w.T("reallife_co_paid_year", "Paid this year");
        t.PaidEmpty = w.T("reallife_co_paid_none", "No return has been settled yet.");
        t.Paid = tax.Filed.OrderByDescending(x => x[1]).Select(x => new CompanyItem { Title = w.F("reallife_co_row_quarter", "Quarter {quarter}", "quarter", x[0].ToString()), Sub = Dy(w, x[1]), Figure = CM(x[2]),
            Chip = x[3] == 1 ? w.T("reallife_ibox_chip_late", "Late") : w.T("reallife_co_paid_on_time", "On time"), ChipTone = x[3] == 1 ? UiRole.Warn : UiRole.Ok }).ToArray();
        t.ToolsTitle = w.T("reallife_co_tax_tools", "Return, accountant and books");
        var tools = new List<CompanyItem>
        {
            new CompanyItem { Title = w.T("reallife_co_row_return", "Tax return"), Sub = w.F("reallife_co_row_quarter", "Quarter {quarter}", "quarter", (tax.ReturnWaiting ? tax.ReturnQuarter : tax.Number).ToString()),
                Chip = tax.ReturnWaiting ? w.T("reallife_co_row_return_due", "Due") : w.T("reallife_co_row_return_running", "Running"), ChipTone = tax.ReturnWaiting ? UiRole.Warn : UiRole.Muted, Press = true, Act = CA(CompanyDo.Go, "ret", CompanyView.Tax, CompanyView.SubReturn) },
            new CompanyItem { Title = w.T("reallife_co_row_accountant", "Accountant"), Sub = one ? w.T("reallife_co_none", "None") : w.T("reallife_co_accountant_" + AccountantHired, "Careful"),
                Figure = one ? null : w.F("reallife_co_row_a_month", "{amount} a month", "amount", CM(AccountantFees[AccountantHired])), Press = true, Act = CA(CompanyDo.Go, "acc", CompanyView.Tax, CompanyView.SubAccountant) }
        };
        foreach (CPlace pl in Cw.Places.Take(4))
        {
            tools.Add(new CompanyItem { Title = pl.Name, Sub = w.T("reallife_co_row_hidden", "Cash off the books"), Figure = pl.Hide + "%", FigureTone = pl.Hide > 0 ? UiRole.WarnText : UiRole.None, Press = true,
                Act = CA(CompanyDo.Go, "hide:" + pl.Key, CompanyView.Tax, CompanyView.SubHide) });
        }
        t.Tools = tools.ToArray();
        t.PropertyTitle = w.T("reallife_co_property_tax", "Property tax");
        t.Property = Cw.Places.Where(x => x.Property > 0).Select(x => new CompanyStatementLine { Label = x.Name, Value = CM(x.Property) }).ToArray();
        t.PropertyNote = t.Property.Length == 0 ? w.T("reallife_co_property_rented", "Rented premises: the property tax is inside the rent.") : w.T("reallife_co_property_note", "Counted so far this tax year.");
        if (one)
        {
            // A first quarter, a day in: how far through it the day is, what each accountant costs and does, and the permits the cafe does not need yet.
            t.TermsTitle = w.T("reallife_co_acc_terms_sec", "What an accountant costs");
            t.Terms = AccountantTermRows(w);
            t.ExtraTitle = w.T("reallife_co_lic_not_needed", "Permits this business does not need");
            string[] kinds = { "fire", "seating", "delivery", "bonded" }, names = { "fire safety certificate", "outdoor seating permit", "delivery vehicle permit", "bonded storage permit" };
            int[] codes = { 4, 5, 6, 7 }, permitFees = codes.Select(c => CompanyWorld.LicenceFee[c]).ToArray(), days = codes.Select(c => CompanyWorld.LicenceTerm[c]).ToArray();
            var extra = new CompanyItem[4];
            for (int i = 0; i < 4; i++)
            {
                extra[i] = new CompanyItem { Title = CompanyTextCapital(w.T("reallife_co_licence_" + kinds[i], names[i])), Sub = w.F("reallife_co_lic_term_value", "{days} days", "days", days[i].ToString()), Figure = CM(permitFees[i]), Grade = -1 };
            }
            t.Extra = extra;
        }
        // The lead: the return waiting for the owner. The company has no finance chief, so the reserve falling short has no banner of its own.
        if (tax.ReturnWaiting && (state == "tax_lead" || state == "tax_big"))
        {
            bool late = Cw.Today > tax.ReturnDue;
            int retTakings = Cw.ReturnTakingsOf(), retCosts = Cw.ReturnCostsOf();
            t.HasLead = true;
            t.LeadKicker = w.T("reallife_co_tab_tax", "Tax and licences");
            t.LeadTitle = w.F(late ? "reallife_co_return_late" : "reallife_co_return_due", late ? "Return for the quarter is late" : "File by day {day}; after that a penalty of {amount} is charged, {count} so far.",
                "day", Dn(tax.ReturnDue), "amount", CM(CompanyWorld.Penalty(retTakings)), "count", tax.ReturnPenalties.ToString());
            t.LeadBody = w.F("reallife_co_lead_return", "Quarter {quarter}: takings {takings}, costs that count {costs}.", "quarter", tax.ReturnQuarter.ToString(), "takings", CM(retTakings), "costs", CM(retCosts));
            t.LeadAsker = Cw.Owner;
            t.LeadHue = 0.05f;
            t.LeadButtons = new[]
            {
                CB(w.T("reallife_co_return_file", "File the return"), UiButtonKind.Primary, UiAction.Alt, CA(CompanyDo.ReturnFile), UiIcon.Check),
                CB(w.T("reallife_co_open_return", "Open the return"), UiButtonKind.Secondary, UiAction.Option, CA(CompanyDo.Go, "ret", CompanyView.Tax, CompanyView.SubReturn), UiIcon.Next)
            };
        }
        var back = new[] { CB(w.T("reallife_co_back", "Back"), UiButtonKind.Secondary, UiAction.Count, CA(CompanyDo.Go, null, CompanyView.Tax, CompanyView.SubMain), UiIcon.Back) };
        CPlace speedy = Cw.Places.Count > 1 ? Cw.Pl("Speedy Bites") : null;
        if (state == "tax_return")
        {
            int retTakings = Cw.ReturnTakingsOf(), retCosts = Cw.ReturnCostsOf();
            t.Card = new CompanyCard
            {
                Kicker = w.T("reallife_co_tab_tax", "Tax and licences"), Title = w.T("reallife_co_row_return", "Tax return"), Icon = UiIcon.Receipt, Back = back, Chips = new[] { w.T("reallife_ibox_chip_due", "Due") }, ChipTones = new[] { UiRole.Warn },
                Blocks = new[]
                {
                    CNote(w.F("reallife_co_return_due", "File by day {day}; after that a penalty of {amount} is charged, {count} so far.", "day", Dn(tax.ReturnDue), "amount", CM(CompanyWorld.Penalty(retTakings)), "count", tax.ReturnPenalties.ToString()), UiRole.WarnText),
                    CSec(w.T("reallife_ibox_sec_return", "The return")), CRow(w.L("reallife_co_return_quarter", "Quarter"), tax.ReturnQuarter.ToString()), CRow(w.L("reallife_co_return_takings", "Takings"), CM(retTakings)), CRow(w.L("reallife_co_return_deduct", "Costs that count"), CM(retCosts)),
                    new CompanyBlock { Kind = CompanyBlockKind.Break },
                    CNote(w.F("reallife_co_return_rebate", "{name} files it for you and takes back {amount}.", "name", w.T("reallife_co_accountant_" + AccountantHired, "Careful"), "amount", CM(Cw.ReturnTaxOf() * AccountantSaves[AccountantHired] / 1000)), UiRole.OkText),
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
                FactLabels = new[] { w.T("reallife_co_f_hired", "Hired now"), w.T("reallife_co_f_fee", "Fee") }, FactValues = new[] { w.T("reallife_co_accountant_" + AccountantHired, "Careful"), w.F("reallife_co_row_a_month", "{amount} a month", "amount", CM(AccountantFees[AccountantHired])) },
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
            int kept = speedy.Hide * Cw.QuarterTakings() / Cw.Places.Count / 100 * tax.Percent / 100;   // the share of the quarter's takings kept off the books, at the tax rate
            t.Card = new CompanyCard
            {
                Kicker = w.T("reallife_co_tab_tax", "Tax and licences"), Title = w.F("reallife_co_hide_title", "Cash off the books at {business}", "business", speedy.Name), Icon = UiIcon.Percent, Back = back,
                FactLabels = new[] { w.T("reallife_co_f_saved", "Tax kept back"), w.T("reallife_co_f_risk", "Audit risk") }, FactValues = new[] { CM(kept), "0.4%" },
                Blocks = new[]
                {
                    CNote(w.F("reallife_co_hide_what", "Cash you keep off the books pays no tax, at {percent}%.", "percent", tax.Percent.ToString())),
                    new CompanyBlock { Kind = CompanyBlockKind.Choice, Text = w.T("reallife_co_hide_label", "Cash off the books"), Options = new[] { "0%", "5%", "10%" }, Chosen = 1, Acts = new[] { CA(CompanyDo.HideSet, speedy.Key, 0), CA(CompanyDo.HideSet, speedy.Key, 5), CA(CompanyDo.HideSet, speedy.Key, 10) } },
                    new CompanyBlock { Kind = CompanyBlockKind.Break },
                    CSec(w.T("reallife_ibox_sec_risk", "The risk")), new CompanyBlock { Kind = CompanyBlockKind.Meter, Text = w.T("reallife_co_f_risk", "Audit risk"), Value = "0.4%", Number = 1, Tone = UiRole.Warn },
                    CNote(w.F("reallife_co_hide_risk", "The audit chance grows {grow} a day for each per cent and falls {fall} a day, to at most {most}.", "grow", "0.2", "fall", "0.1", "most", "30%")),
                    CNote(w.F("reallife_co_hide_audit", "An audit fines {fine} of what was hidden, and goes to court from {sum}.", "fine", "50%", "sum", CM(5000))), CNote(w.T("reallife_co_hide_threshold", "Nothing is checked while the sum stays small.")),
                    CSec(w.T("reallife_co_hide_every", "Every business"), Cw.Places.Count.ToString()),
                    new CompanyBlock { Kind = CompanyBlockKind.Items, Items = Cw.Places.Select(x => new CompanyItem { Grade = -1, Title = x.Name, Icon = UiIcon.Store, Sub = w.T("reallife_co_row_hidden", "Cash off the books"), Figure = x.Hide + "%",
                        Chip = x == speedy ? w.T("reallife_ui_bizops_pick", "Picked") : null, ChipTone = UiRole.Ok, Press = x != speedy, Act = x == speedy ? default(CompanyAct) : CA(CompanyDo.Go, "hide:" + x.Key, CompanyView.Tax, CompanyView.SubHide) }).ToArray() }
                }
            };
        }
        else if (state == "licence_card")
        {
            CPlace bloom = Cw.Pl("Bloom & Co");
            List<CLicence> others = Cw.Licences.Where(x => x.Place == bloom.Key && x.Kind != "signage").ToList();
            t.Card = new CompanyCard
            {
                Kicker = bloom.Name, Title = Lic(w, "signage"), Icon = UiIcon.Tag, Back = back, Chips = new[] { w.T("reallife_ibox_chip_missing", "Missing") }, ChipTones = new[] { UiRole.Danger },
                FactLabels = new[] { w.T("reallife_co_f_fee", "Fee"), w.T("reallife_co_f_term", "Term") }, FactValues = new[] { CM(CompanyWorld.LicenceFee[3]), w.F("reallife_co_lic_term_value", "{days} days", "days", CompanyWorld.LicenceTerm[3].ToString()) },
                Blocks = new[]
                {
                    CNote(w.T("reallife_co_lic_missing", "Trading without it risks a fine."), UiRole.DangerText),
                    new CompanyBlock { Kind = CompanyBlockKind.Switch, Text = w.T("reallife_co_lic_auto", "Renew it by itself"), On = false, Value = w.T("reallife_co_off", "Off"), Act = CA(CompanyDo.LicenceAuto, bloom.Key, 3, 1) },
                    CBtns(CB(w.T("reallife_co_lic_buy", "Apply"), UiButtonKind.Primary, UiAction.Alt, CA(CompanyDo.LicenceBuy, bloom.Key, 3), UiIcon.Check)),
                    new CompanyBlock { Kind = CompanyBlockKind.Break },
                    CSec(w.T("reallife_co_lic_lapse_sec", "If it lapses")),
                    CNote(w.F("reallife_co_lic_fine", "An inspector may fine {amount}.", "amount", CM(CompanyWorld.LicenceFee[3] * CompanyWorld.LicenceFineMultiple)), UiRole.WarnText),
                    CSec(w.T("reallife_co_lic_why_sec", "Why you need it")),
                    CNote(w.F("reallife_co_lic_why_signage", "A sign over the door needs a permit.", "hour", "22", "early", "6")),
                    CSec(w.F("reallife_co_lic_others", "Other permits at {business}", "business", bloom.Name), others.Count.ToString()),
                    new CompanyBlock { Kind = CompanyBlockKind.Items, Items = others.Select(x => new CompanyItem { Grade = -1, Title = Lic(w, x.Kind), Icon = x.Kind == "hygiene" ? UiIcon.Drop : UiIcon.Clock,
                        Sub = w.F("reallife_co_lic_term_value", "{days} days", "days", CompanyWorld.LicenceTerm[x.Code].ToString()), Figure = CM(CompanyWorld.LicenceFee[x.Code]),
                        Chip = x.State == "pending" ? w.T("reallife_co_row_lic_pending", "Pending") : w.T("reallife_ibox_chip_valid", "Valid"), ChipTone = x.State == "pending" ? UiRole.Warn : UiRole.Ok, Press = true,
                        Act = CA(CompanyDo.Go, "lic:" + x.Place + ":" + x.Code, CompanyView.Tax, CompanyView.SubLicence) }).ToArray() }
                }
            };
        }
    }
}
