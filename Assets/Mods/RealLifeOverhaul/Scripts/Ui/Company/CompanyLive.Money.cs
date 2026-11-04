using System;
using System.Collections.Generic;
using RealLifeOverhaul.Economy;

namespace RealLifeOverhaul.Ui
{
    internal sealed partial class CompanyLive
    {
        private static readonly string[] CampaignAboutKeys =
        {
            "reallife_co_push_about_flyers", "reallife_co_push_about_radio", "reallife_co_push_about_discount", "reallife_co_push_about_refit",
            "reallife_co_push_about_sponsored", "reallife_co_push_about_influencer", "reallife_co_push_about_influencer", "reallife_co_push_about_influencer",
            "reallife_co_push_about_influencer", "reallife_co_push_about_influencer", "reallife_co_push_about_giveaway"
        };

        private readonly List<int> _kinds = new List<int>(CompanyCatalogue.CampaignKinds);

        private static CompanyBlock Chips(params string[] words) => new CompanyBlock { Kind = CompanyBlockKind.Chips, Options = words };

        // ------------------------------------------------------------------------------------------------- Marketing

        // Each business with its campaign (running, booked or none), then the reports of the ones that ended; at the right the campaign to book or the report.
        private void ReadMarketing(CompanyFacts f, CompanyDirector d, CompanyView v, Words w)
        {
            CompanyPage page = f.Marketing;
            if (!d.On(CompanyPart.Marketing))
            {
                PartOff(page, w.T("reallife_co_tab_marketing", "Marketing"));
                return;
            }
            CompanyState s = d.State;
            int day = d.Today;
            IReadOnlyList<CompanyShop> shops = d.Shops;
            var rows = new List<CompanyRow>(shops.Count + s.Reports.Count + 2);
            for (int i = 0; i < shops.Count; i++)
            {
                ShopFile file = shops[i].File;
                bool on = file.Push >= 0, running = on && file.Pushing(day);
                rows.Add(new CompanyRow
                {
                    Id = "push:" + file.Place, Title = shops[i].Name, Sub = on ? CompanyText.Campaign(file.Push) + ", " + (running ? CompanyText.Day(file.PushEnd - 1)
                        : CompanyText.T("reallife_co_row_from", "day", file.PushFrom.ToString()))
                        : CompanyText.T("reallife_co_none"), Icon = on ? UiIcon.Megaphone : UiIcon.Store, Figure = on ? CompanyText.Percent(file.PushPercent) : null, FigureTone = UiRole.OkText,
                    Note = on ? CompanyText.Money(file.PushCost) : null, Chip = on ? CompanyText.T(running ? "reallife_ibox_chip_running" : "reallife_ibox_chip_booked") : null, ChipTone = running
                        ? UiRole.Ok : UiRole.Muted
                });
            }
            if (s.Reports.Count > 0)
            {
                rows.Add(new CompanyRow { Caption = true, Title = w.T("reallife_co_reports", "Reports") });
            }
            for (int i = s.Reports.Count - 1; i >= 0; i--)
            {
                CampaignReport r = s.Reports[i];
                rows.Add(new CompanyRow
                {
                    Id = "rep:" + i, Title = CompanyText.Campaign(r.Kind), Sub = r.Name + ", " + CompanyText.Day(r.From
                        + r.Days - 1), Icon = UiIcon.ClipboardText, Figure = CompanyText.Percent(r.Footfall),
                    FigureTone = r.Footfall > 0 ? UiRole.OkText : UiRole.None, Note = ExtraText(r.Extra)
                });
            }
            page.List = new CompanyList { Title = w.T("reallife_co_tab_marketing", "Marketing"), Count = shops.Count.ToString(), Rows = rows.ToArray(),
                Empty = CompanyText.T("reallife_co_row_no_business") };
            string pick = v.Pick[CompanyView.Marketing] ?? string.Empty;
            page.List.Chosen = ChosenRow(page.List.Rows, pick);
            CompanyShop shop = pick.StartsWith("push:", StringComparison.Ordinal) ? d.ShopAt(pick.Substring(5)) : null;
            int index;
            CampaignReport report = pick.StartsWith("rep:", StringComparison.Ordinal) && int.TryParse(pick.Substring(4), out index) && index >= 0 && index < s.Reports.Count ? s.Reports[index] : null;
            if (shop != null)
            {
                page.Card = CampaignCard(d, shop, v, w, day);
            }
            else if (report != null)
            {
                page.Card = ReportCard(report, w);
            }
            else
            {
                page.List.Chosen = -1;
                page.Top = new CompanyCard
                {
                    Kicker = w.T("reallife_co_tab_marketing", "Marketing"), Title = w.T("reallife_co_marketing_title", "Campaigns"), Icon = UiIcon.Megaphone,
                    Blocks = new[]
                    {
                        Note(CompanyText.T("reallife_co_push_intro")), Sec(w.T("reallife_ibox_sec_how", "How it is measured")), Note(CompanyText.T("reallife_co_push_measure")),
                        Note(CompanyText.T("reallife_co_push_game")), Note(CompanyText.T("reallife_co_push_pick"))
                    }
                };
            }
        }

        // A part that is off says so under its own name, with what that means.
        private static void PartOff(CompanyPage page, string title)
        {
            page.PartOff = true;
            page.OffTitle = title;
            page.OffBody = CompanyText.T("reallife_co_part_off");
        }

        private CompanyCard CampaignCard(CompanyDirector d, CompanyShop shop, CompanyView v, Words w, int day)
        {
            ShopFile file = shop.File;
            var card = new CompanyCard { Kicker = w.T("reallife_co_tab_marketing", "Marketing"), Title = shop.Name, Icon = UiIcon.Megaphone };
            var blocks = new List<CompanyBlock>(12);
            if (file.Push >= 0)
            {
                bool running = file.Pushing(day);
                card.Chips = new[] { CompanyText.T(running ? "reallife_ibox_chip_running" : "reallife_ibox_chip_booked") };
                card.ChipTones = new[] { UiRole.Ok };
                card.FactLabels = new[] { w.T("reallife_co_f_cost", "Cost"), w.T("reallife_co_f_effect", "Effect on customers") };
                card.FactValues = new[] { CompanyText.Money(file.PushCost), CompanyText.Percent(file.PushPercent) };
                blocks.Add(Sec(w.T("reallife_ibox_sec_campaign", "The campaign")));
                blocks.Add(Row(CompanyText.Label("reallife_co_push_running"), CompanyText.Campaign(file.Push)));
                int length = Math.Max(1, file.PushEnd - file.PushFrom), done = Math.Max(0, Math.Min(length, day - file.PushFrom + 1));
                blocks.Add(new CompanyBlock { Kind = CompanyBlockKind.Meter, Text = running ? w.F("reallife_co_push_progress", "Day {done} of {days}", "done", done.ToString(), "days",
                    length.ToString())
                    : CompanyText.T("reallife_co_push_days", "from", file.PushFrom.ToString(), "to", (file.PushEnd - 1).ToString()),
                    Value = running ? w.F("reallife_co_push_left", "{count} days left", "count", Math.Max(0, length - done).ToString()) : null, Number = running ? done * 100 / length : 0,
                        Tone = UiRole.Ok });
                blocks.Add(Note(CompanyText.T("reallife_co_push_report_due", "day", file.PushEnd.ToString())));
                Earlier(blocks, d, shop, w);
                card.Blocks = blocks.ToArray();
                return card;
            }
            if (!d.Promoted(shop))
            {
                blocks.Add(Note(CompanyText.T(EmpireMotors.IsDealership(shop.Reg) ? "reallife_co_push_empire" : "reallife_co_push_no_figure")));
                Earlier(blocks, d, shop, w);
                card.Blocks = blocks.ToArray();
                return card;
            }
            int offered = d.OfferedKinds(shop, _kinds);
            int at = CompanyCatalogue.Clamp(v.Kind, 0, Math.Max(0, offered - 1));
            int kind = _kinds[at];
            var kinds = new CompanyItem[offered];
            for (int i = 0; i < offered; i++)
            {
                int c1, p1, s1, r1;
                d.CampaignQuote(shop, _kinds[i], out c1, out p1, out s1, out r1);
                kinds[i] = new CompanyItem
                {
                    Title = CompanyText.Campaign(_kinds[i]), Icon = UiIcon.Megaphone, Tint = UiRole.None, Press = true, Grade = -1,
                    Sub = CompanyText.T("reallife_co_lic_term_value", "days", CompanyCatalogue.CampaignDays[_kinds[i]].ToString()), Figure = CompanyText.Percent(p1), FigureTone = UiRole.OkText,
                    Note = CompanyText.Money(c1), Chip = i == at ? CompanyText.T("reallife_ui_bizops_pick") : null, ChipTone = UiRole.Ok, Act = CompanyAct.Of(CompanyDo.Go, "kind", i,
                        CompanyView.SubMain, 0, null)
                };
            }
            int cost, percent, suit, repeat;
            d.CampaignQuote(shop, kind, out cost, out percent, out suit, out repeat);
            card.FactLabels = new[] { w.T("reallife_co_f_cost", "Cost"), w.T("reallife_co_f_effect", "Effect on customers") };
            card.FactValues = new[] { CompanyText.Money(cost), CompanyText.Percent(percent) };
            blocks.Add(Sec(w.T("reallife_ibox_sec_campaign", "The campaign")));
            blocks.Add(new CompanyBlock { Kind = CompanyBlockKind.Items, Items = kinds });
            blocks.Add(Note(CompanyText.T(CampaignAboutKeys[kind], "share", CompanyText.Share(CompanyCatalogue.DiscountShare))));
            blocks.Add(Row(w.T("reallife_co_f_cost", "Cost"), CompanyText.Money(cost)));
            blocks.Add(Row(CompanyText.Label("reallife_co_push_length"), CompanyText.T("reallife_co_push_length_value", "days", CompanyCatalogue.CampaignDays[kind].ToString(), "from", (day
                + 1).ToString())));
            blocks.Add(Row(w.T("reallife_co_f_effect", "Effect on customers"), CompanyText.Percent(percent), UiRole.OkText));
            string economy = CompanyText.Share(CompanyCatalogue.CampaignCycle[CompanyCatalogue.Clamp((int)d.Cycle, 0, 3)]);
            blocks.Add(Note(CompanyText.T("reallife_co_push_parts", "own", CompanyText.Percent(CompanyCatalogue.CampaignFootfall[kind]), "district", CompanyText.Share(suit), "economy", economy)
                + (repeat < 1000 ? " " + CompanyText.T("reallife_co_push_repeat", "share", CompanyText.Share(repeat)) : string.Empty)));
            Earlier(blocks, d, shop, w);
            blocks.Add(Buttons(Btn(CompanyText.T("reallife_co_push_book"), UiButtonKind.Primary, CompanyAct.Of(CompanyDo.CampaignBook, shop.File.Place, kind), UiAction.Alt, UiIcon.Check)));
            card.Blocks = blocks.ToArray();
            return card;
        }

        // The profit a campaign left over its cost; one that cost more than it brought says "less", not a negative "extra".
        private static string ExtraText(int extra)
        {
            return extra >= 0 ? CompanyText.T("reallife_co_row_extra", "amount", CompanyText.Money(extra)) : CompanyText.T("reallife_co_row_less", "amount", CompanyText.Money(-extra));
        }

        // What the business's earlier campaigns did, newest first: the kind, the day it ended, the customers it brought and the profit it left; a row opens its report.
        private static void Earlier(List<CompanyBlock> blocks, CompanyDirector d, CompanyShop shop, Words w)
        {
            CompanyState s = d.State;
            var items = new List<CompanyItem>(3);
            for (int i = s.Reports.Count - 1; i >= 0 && items.Count < 3; i--)
            {
                CampaignReport r = s.Reports[i];
                if (r.Name != shop.Name)
                {
                    continue;
                }
                items.Add(new CompanyItem
                {
                    Title = CompanyText.Campaign(r.Kind), Sub = CompanyText.Day(r.From + r.Days - 1), Icon = UiIcon.ClipboardText, Figure = CompanyText.Percent(r.Footfall),
                    FigureTone = r.Footfall > 0 ? UiRole.OkText : UiRole.None, Note = ExtraText(r.Extra), Grade = -1, Press = true,
                    Act = CompanyAct.Of(CompanyDo.Go, "rep:" + i, CompanyView.Marketing)
                });
            }
            if (items.Count > 0)
            {
                blocks.Add(Sec(w.T("reallife_co_push_earlier", "Earlier campaigns here")));
                blocks.Add(new CompanyBlock { Kind = CompanyBlockKind.Items, Items = items.ToArray() });
            }
        }

        private static CompanyCard ReportCard(CampaignReport r, Words w)
        {
            var card = new CompanyCard
            {
                Kicker = r.Name, Title = CompanyText.Campaign(r.Kind), Icon = UiIcon.ClipboardText,
                Sub = CompanyText.T("reallife_co_report_days", "from", r.From.ToString(), "to", (r.From + r.Days - 1).ToString(), "days", r.Days.ToString())
            };
            if (r.Cost > 0)
            {
                card.Chips = new[] { CompanyText.Percent(r.Return) };
                card.ChipTones = new[] { r.Return > 0 ? UiRole.Ok : UiRole.Danger };
            }
            card.FactLabels = new[] { w.T("reallife_co_f_cost", "Cost"), w.T("reallife_co_f_extra", "Extra profit") };
            card.FactValues = new[] { CompanyText.Money(r.Cost), CompanyText.Money(r.Extra) };
            var blocks = new List<CompanyBlock>(10);
            if (r.DaysBefore < r.Days || r.DaysDuring < r.Days)
            {
                blocks.Add(Note(CompanyText.T("reallife_co_report_short", "before", r.DaysBefore.ToString(), "during", r.DaysDuring.ToString()), UiRole.WarnText));
            }
            blocks.Add(Sec(w.T("reallife_ibox_sec_before_during", "Before and during")));
            string[] heads = { w.T("reallife_co_report_before", "Before"), w.T("reallife_co_report_during", "During") };
            blocks.Add(new CompanyBlock { Kind = CompanyBlockKind.Compare, Text = w.T("reallife_co_f_customers", "Customers"), Value = r.CustomersBefore.ToString(),
                Sub = r.CustomersDuring + " (" + CompanyText.Percent(r.Footfall) + ")", Tone = r.Footfall > 0 ? UiRole.OkText : r.Footfall < 0 ? UiRole.DangerText : UiRole.None, Options = heads });
            blocks.Add(new CompanyBlock { Kind = CompanyBlockKind.Compare, Text = w.T("reallife_co_f_takings", "Takings"), Value = CompanyText.Money(r.TakingsBefore),
                Sub = CompanyText.Money(r.TakingsDuring),
                Tone = r.TakingsDuring > r.TakingsBefore ? UiRole.OkText : r.TakingsDuring < r.TakingsBefore ? UiRole.DangerText : UiRole.None });
            blocks.Add(new CompanyBlock { Kind = CompanyBlockKind.Compare, Text = w.T("reallife_co_f_profit", "Profit"), Value = CompanyText.Money(r.ProfitBefore),
                Sub = CompanyText.Money(r.ProfitDuring),
                Tone = r.ProfitDuring > r.ProfitBefore ? UiRole.OkText : r.ProfitDuring < r.ProfitBefore ? UiRole.DangerText : UiRole.None });
            blocks.Add(new CompanyBlock { Kind = CompanyBlockKind.Break });
            blocks.Add(Sec(w.T("reallife_ibox_sec_money", "The money")));
            blocks.Add(Row(w.T("reallife_co_f_extra", "Extra profit"), CompanyText.Money(r.Extra), r.Extra > 0 ? UiRole.OkText : UiRole.None));
            if (r.Cost > 0)
            {
                blocks.Add(Row(CompanyText.Label("reallife_co_report_return"), CompanyText.Percent(r.Return), r.Return > 0 ? UiRole.OkText : UiRole.DangerText));
            }
            else
            {
                blocks.Add(Note(CompanyText.T("reallife_co_report_no_return")));
            }
            card.Blocks = blocks.ToArray();
            return card;
        }

        // ------------------------------------------------------------------------------------------------- Cover

        // The claims that wait or were filed, then each business's three covers; at the right the claim or the policy, with the insurers to choose from while uncovered.
        private void ReadCover(CompanyFacts f, CompanyDirector d, CompanyView v, Words w)
        {
            CompanyPage page = f.Cover;
            if (!d.On(CompanyPart.Cover))
            {
                PartOff(page, w.T("reallife_co_tab_cover", "Cover"));
                return;
            }
            CompanyState s = d.State;
            var rows = new List<CompanyRow>(16);
            int open = 0, premiums = 0;
            for (int i = s.Claims.Count - 1; i >= 0; i--)
            {
                Claim c = s.Claims[i];
                if (c.Stage != ClaimStage.Open && c.Stage != ClaimStage.Filed)
                {
                    continue;
                }
                open += c.Stage == ClaimStage.Open ? 1 : 0;
                int until = c.Stage == ClaimStage.Open ? c.Day + CompanyCatalogue.ClaimWindowDays : c.PayDay;
                rows.Add(new CompanyRow
                {
                    Id = "claim:" + c.Id, Title = c.Name, Sub = CompanyText.T("reallife_co_row_claim", "cover", CompanyText.Cover(c.Kind)) + ", "
                        + CompanyText.Insurer(c.Insurer), Icon = UiIcon.ShieldCheck,
                    Figure = c.Stage == ClaimStage.Open ? CompanyText.T("reallife_co_row_claim_by", "day", until.ToString()) : CompanyText.T("reallife_co_row_claim_paid", "day", until.ToString()),
                    Chip = CompanyText.T(c.Stage == ClaimStage.Open ? "reallife_ibox_chip_open" : "reallife_ibox_chip_filed"), ChipTone = c.Stage == ClaimStage.Open ? UiRole.Warn : UiRole.Muted
                });
            }
            IReadOnlyList<CompanyShop> shops = d.Shops;
            for (int i = 0; i < shops.Count; i++)
            {
                for (int k = 0; k < shops[i].File.Cover.Length; k++)
                {
                    Policy p = shops[i].File.Cover[k];
                    premiums += p.Active ? p.Premium : 0;
                    rows.Add(new CompanyRow
                    {
                        Id = "pol:" + shops[i].File.Place + ":" + k, Title = w.F("reallife_co_row_cover_title", "{cover} cover", "cover", CompanyText.Capital(CompanyText.Cover(k))),
                        Sub = shops[i].Name + ", " + (p.Active ? CompanyText.Insurer(p.Insurer) : CompanyText.T("reallife_co_none")),
                        Icon = k == (int)CoverKind.Fire ? UiIcon.Fire : k == (int)CoverKind.Theft ? UiIcon.Lock : UiIcon.ShieldCheck,
                        Figure = p.Active ? CompanyText.T("reallife_co_row_a_day", "amount", CompanyText.Money(p.Premium)) : null,
                        Chip = p.Active ? null : CompanyText.T("reallife_ibox_chip_not_covered"), ChipTone = UiRole.Warn
                    });
                }
            }
            page.List = new CompanyList { Title = w.T("reallife_co_tab_cover", "Cover"), Count = rows.Count.ToString(), Rows = rows.ToArray(), Empty = CompanyText.T("reallife_co_row_no_business") };
            string pick = v.Pick[CompanyView.Cover] ?? string.Empty;
            page.List.Chosen = ChosenRow(page.List.Rows, pick);
            Claim claim = null;
            int number;
            if (pick.StartsWith("claim:", StringComparison.Ordinal) && int.TryParse(pick.Substring(6), out number))
            {
                claim = s.ClaimOf(number);
            }
            CompanyShop shop = null;
            int kind = -1;
            if (pick.StartsWith("pol:", StringComparison.Ordinal))
            {
                int last = pick.LastIndexOf(':');
                if (last > 4 && int.TryParse(pick.Substring(last + 1), out kind) && kind >= 0 && kind < CompanyCatalogue.CoverKinds)
                {
                    shop = d.ShopAt(pick.Substring(4, last - 4));
                }
            }
            if (claim != null)
            {
                page.Card = ClaimCard(claim, w);
            }
            else if (shop != null)
            {
                page.Card = PolicyCard(d, shop, kind, v, w);
            }
            else
            {
                page.List.Chosen = -1;
                page.Top = new CompanyCard
                {
                    Kicker = w.T("reallife_co_tab_cover", "Cover"), Title = w.T("reallife_co_cover_title", "Insurance"), Icon = UiIcon.ShieldCheck,
                    FactLabels = new[] { w.T("reallife_co_f_premiums", "Premiums"), w.T("reallife_co_f_claims_open", "Claims to put in") },
                    FactValues = new[] { CompanyText.T("reallife_co_row_a_day", "amount", CompanyText.Money(premiums)), open.ToString() },
                    Blocks = new[]
                    {
                        Sec(CompanyText.T("reallife_co_cover_what")), Note(CompanyText.T("reallife_co_cover_what_fire")), Note(CompanyText.T("reallife_co_cover_what_theft")),
                        Note(CompanyText.T("reallife_co_cover_what_liability")), Note(CompanyText.T("reallife_co_cover_pick"))
                    }
                };
            }
        }

        private static CompanyCard ClaimCard(Claim c, Words w)
        {
            var card = new CompanyCard
            {
                Kicker = w.T("reallife_co_tab_cover", "Cover"), Title = CompanyText.T("reallife_co_claim_title", "cover", CompanyText.Cover(c.Kind), "business", c.Name), Icon = UiIcon.ShieldCheck,
                Chips = new[] { CompanyText.T(c.Stage == ClaimStage.Open ? "reallife_ibox_chip_open" : c.Stage == ClaimStage.Filed ? "reallife_ibox_chip_filed" : c.Stage == ClaimStage.Paid
                    ? "reallife_ibox_chip_paid" : "reallife_ibox_chip_lapsed") },
                ChipTones = new[] { c.Stage == ClaimStage.Open ? UiRole.Warn : c.Stage == ClaimStage.Paid ? UiRole.Ok : c.Stage == ClaimStage.Filed ? UiRole.Muted : UiRole.Danger },
                FactLabels = new[] { w.T("reallife_co_f_excess", "Excess"), w.T("reallife_co_f_pays", "Pays") }, FactValues = new[] { CompanyText.Money(c.Excess), CompanyText.Money(c.Payout) }
            };
            var blocks = new List<CompanyBlock>(10);
            switch (c.Stage)
            {
                case ClaimStage.Open:
                    blocks.Add(Note(CompanyText.T("reallife_co_claim_open", "day", (c.Day + CompanyCatalogue.ClaimWindowDays).ToString(), "days",
                        CompanyCatalogue.InsurerPayDays[c.Insurer].ToString()), UiRole.WarnText));
                    break;
                case ClaimStage.Filed:
                    blocks.Add(Note(CompanyText.T("reallife_co_claim_filed", "day", c.PayDay.ToString())));
                    break;
                case ClaimStage.Paid:
                    blocks.Add(Note(CompanyText.T("reallife_co_claim_paid"), UiRole.OkText));
                    break;
                default:
                    blocks.Add(Note(CompanyText.T("reallife_co_claim_lapsed"), UiRole.DangerText));
                    break;
            }
            blocks.Add(Note(CompanyText.T(c.Kind == (int)CoverKind.Fire ? "reallife_co_claim_what_fire" : c.Kind == (int)CoverKind.Theft ? "reallife_co_claim_what_theft"
                : "reallife_co_claim_what_liability",
                "day", c.Day.ToString(), "amount", CompanyText.Money(c.Loss))));
            blocks.Add(Sec(w.T("reallife_ibox_sec_money", "The money")));
            blocks.Add(Row(CompanyText.Label("reallife_co_claim_insurer"), CompanyText.Insurer(c.Insurer)));
            blocks.Add(Row(w.T("reallife_co_f_most", "Most it counts"), CompanyText.Money(CompanyCatalogue.CoverMost[c.Kind])));
            blocks.Add(Row(w.T("reallife_co_f_excess", "Excess"), CompanyText.Money(c.Excess)));
            blocks.Add(Row(w.T("reallife_co_f_pays", "Pays"), CompanyText.Money(c.Payout), UiRole.OkText));
            if (c.Stage == ClaimStage.Open)
            {
                blocks.Add(Note(CompanyText.T("reallife_co_claim_counts", "most", (CompanyCatalogue.InsurerClaimsMost[c.Insurer] - 1).ToString(), "loading",
                    CompanyText.Share(CompanyCatalogue.ClaimLoading))));
                blocks.Add(Buttons(Btn(CompanyText.T("reallife_co_claim_do"), UiButtonKind.Primary, CompanyAct.Of(CompanyDo.ClaimFile, null, c.Id), UiAction.Alt, UiIcon.Check)));
            }
            card.Blocks = blocks.ToArray();
            return card;
        }

        private CompanyCard PolicyCard(CompanyDirector d, CompanyShop shop, int kind, CompanyView v, Words w)
        {
            Policy p = shop.File.Cover[kind];
            string place = shop.File.Place;
            int insurer = p.Active ? p.Insurer : CompanyCatalogue.Clamp(v.Insurer, 0, CompanyCatalogue.Insurers - 1);
            var card = new CompanyCard
            {
                Kicker = w.T("reallife_co_tab_cover", "Cover"), Title = w.F("reallife_co_row_cover_title", "{cover} cover", "cover", CompanyText.Capital(CompanyText.Cover(kind))),
                Icon = kind == (int)CoverKind.Fire ? UiIcon.Fire : kind == (int)CoverKind.Theft ? UiIcon.Lock : UiIcon.ShieldCheck,
                Chips = new[] { CompanyText.T(p.Active ? "reallife_ibox_chip_covered" : "reallife_ibox_chip_not_covered") }, ChipTones = new[] { p.Active ? UiRole.Ok : UiRole.Warn }
            };
            int premium = p.Active ? p.Premium : d.Quote(shop, kind, insurer);
            card.FactLabels = new[] { w.T("reallife_co_f_premium", "Premium"), w.T("reallife_co_f_most", "Most it counts") };
            card.FactValues = new[] { CompanyText.T("reallife_co_row_a_day", "amount", CompanyText.Money(premium)), CompanyText.Money(CompanyCatalogue.CoverMost[kind]) };
            card.Sub = shop.Name;
            var blocks = new List<CompanyBlock>(12) { Sec(w.T("reallife_ibox_sec_terms", "Terms")) };
            if (!p.Active)
            {
                blocks.Add(Note(w.T("reallife_co_policy_pick", "No cover yet. Pick the insurer whose terms suit you.")));
                var quotes = new CompanyItem[CompanyCatalogue.Insurers];
                for (int i = 0; i < quotes.Length; i++)
                {
                    quotes[i] = new CompanyItem
                    {
                        Title = CompanyText.Insurer(i), Icon = UiIcon.Bank, Tint = UiRole.None, Press = true, Grade = -1,
                        Sub = CompanyText.T("reallife_co_policy_excess", "share", CompanyText.Share(CompanyCatalogue.InsurerExcessShare[i]), "least",
                            CompanyText.Money(CompanyCatalogue.InsurerExcessLeast[i])),
                        Figure = CompanyText.T("reallife_co_row_a_day", "amount", CompanyText.Money(d.Quote(shop, kind, i))),
                        Chip = i == insurer ? CompanyText.T("reallife_ui_bizops_pick") : null, ChipTone = UiRole.Ok, Act = CompanyAct.Of(CompanyDo.Go, "insurer", i)
                    };
                }
                blocks.Add(Sec(w.T("reallife_co_policy_quotes", "What each insurer asks")));
                blocks.Add(new CompanyBlock { Kind = CompanyBlockKind.Items, Items = quotes });
            }
            else
            {
                blocks.Add(Row(CompanyText.Label("reallife_co_claim_insurer"), CompanyText.Insurer(insurer)));
            }
            blocks.Add(Note(CompanyText.T("reallife_co_policy_excess", "share", CompanyText.Share(CompanyCatalogue.InsurerExcessShare[insurer]), "least",
                CompanyText.Money(CompanyCatalogue.InsurerExcessLeast[insurer]))));
            blocks.Add(Note(CompanyText.T("reallife_co_policy_most", "most", CompanyText.Money(CompanyCatalogue.CoverMost[kind]), "days", CompanyCatalogue.InsurerPayDays[insurer].ToString())));
            bool writes = CoverRules.WillWrite(p, insurer);
            int carried = CoverRules.Carried(p);
            blocks.Add(new CompanyBlock { Kind = CompanyBlockKind.Break });
            int most = Math.Max(1, CompanyCatalogue.InsurerClaimsMost[insurer]), claimed = p.Active ? p.Claims : carried;
            blocks.Add(Sec(w.T("reallife_co_policy_claims_sec", "Claim record")));
            blocks.Add(new CompanyBlock { Kind = CompanyBlockKind.Meter, Text = CompanyText.T("reallife_tab_co_claim"), Value = w.F("reallife_rota_blocks_count", "{count} of {most}", "count",
                claimed.ToString(),
                "most", most.ToString()), Number = Math.Min(100, claimed * 100 / most), Tone = claimed >= most - 1 ? UiRole.Warn : UiRole.Ok });
            if (p.Active)
            {
                blocks.Add(Note(CompanyText.T("reallife_co_policy_term", "day", p.Until.ToString(), "claims", p.Claims.ToString())));
            }
            else if (CoverRules.Declines(p, insurer))
            {
                blocks.Add(Note(CompanyText.T("reallife_co_policy_declined", "insurer", CompanyText.Insurer(insurer)), UiRole.DangerText));
            }
            else if (!writes)
            {
                blocks.Add(Note(CompanyText.T("reallife_co_policy_refused", "insurer", CompanyText.Insurer(insurer), "claims", carried.ToString()), UiRole.DangerText));
            }
            else if (carried > 0)
            {
                blocks.Add(Note(CompanyText.T("reallife_co_policy_carried", "claims", carried.ToString()), UiRole.WarnText));
            }
            blocks.Add(Note(CompanyText.T("reallife_co_policy_claims", "most", (CompanyCatalogue.InsurerClaimsMost[insurer] - 1).ToString(), "loading",
                CompanyText.Share(CompanyCatalogue.ClaimLoading))));
            if (p.Active)
            {
                blocks.Add(Buttons(Btn(CompanyText.T("reallife_co_policy_cancel"), UiButtonKind.Secondary, CompanyAct.Of(CompanyDo.CoverCancel, place, kind), UiAction.Count, UiIcon.Close, true)));
            }
            else
            {
                blocks.Add(Buttons(Btn(CompanyText.T("reallife_co_policy_buy"), UiButtonKind.Primary, CompanyAct.Of(CompanyDo.CoverBuy, place, kind, insurer), UiAction.Alt, UiIcon.Check, false,
                    !writes,
                    !writes ? CompanyText.T("reallife_co_policy_refused", "insurer", CompanyText.Insurer(insurer), "claims", carried.ToString()) : null)));
            }
            card.Blocks = blocks.ToArray();
            return card;
        }
    }
}
