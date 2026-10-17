using System.Collections.Generic;
using RealLifeOverhaul.GameUi;

namespace RealLifeOverhaul.Economy
{
    /// <summary>The Marketing tab: each business's campaign, the campaigns to book, and the reports.</summary>
    public sealed partial class CompanyApp
    {
        private static readonly string[] CampaignAbout =
        {
            "reallife_co_push_about_flyers", "reallife_co_push_about_radio", "reallife_co_push_about_discount", "reallife_co_push_about_refit",
            "reallife_co_push_about_sponsored", "reallife_co_push_about_influencer", "reallife_co_push_about_influencer", "reallife_co_push_about_influencer",
            "reallife_co_push_about_influencer", "reallife_co_push_about_influencer", "reallife_co_push_about_giveaway"
        };

        private readonly List<int> _kinds = new List<int>(CompanyCatalogue.CampaignKinds);
        private int _campaign;   // which of the kinds offered at the business has its quote showing

        partial void WriteMarketing(InfoBox box, CompanyState state, int day, ref string selected)
        {
            IReadOnlyList<CompanyShop> shops = _director.Shops;
            for (int i = 0; i < shops.Count; i++)
            {
                ShopFile file = shops[i].File;
                bool on = file.Push >= 0;
                Pick("push:" + file.Place, shops[i].Name, on ? CompanyText.Campaign(file.Push) : CompanyText.T("reallife_co_none"),
                    !on ? string.Empty : file.Pushing(day) ? CompanyText.Day(file.PushEnd - 1) : CompanyText.T("reallife_co_row_from", "day", file.PushFrom.ToString()),
                    on ? CompanyText.Percent(file.PushPercent) : string.Empty, 0, ref selected);
                TableRow row = Last;
                row.Kind = !on ? CompanyTables.PushNone : file.Pushing(day) ? CompanyTables.PushRunning : CompanyTables.PushBooked;
                if (on)
                {
                    row.Key[2] = file.Pushing(day) ? file.PushEnd - 1 : file.PushFrom;
                    row.Key[3] = file.PushPercent;
                    row.Set(4, CompanyText.Money(file.PushCost), file.PushCost);
                }
            }
            for (int i = state.Reports.Count - 1; i >= 0; i--)
            {
                CampaignReport r = state.Reports[i];
                Pick("rep:" + i, CompanyText.T("reallife_co_row_report", "business", r.Name), CompanyText.Campaign(r.Kind), CompanyText.Day(r.From + r.Days - 1),
                    CompanyText.Percent(r.Footfall), 0, ref selected);
                TableRow row = Last;
                row.Kind = CompanyTables.PushEnded;
                row.Key[2] = r.From + r.Days - 1;
                row.Key[3] = r.Footfall;
                row.Set(4, CompanyText.Money(r.Cost), r.Cost);
                row.Set(5, CompanyText.T("reallife_co_row_extra", "amount", CompanyText.Money(r.Extra)), r.Extra,
                    r.Extra < 0 ? TableRow.Red : r.Extra > 0 ? TableRow.Green : TableRow.Plain);
            }
            if (_rows.Count == 0)
            {
                Say("none", CompanyText.T("reallife_co_row_no_business"));
            }

            string place = Picked("push:");
            CompanyShop shop = place != null ? _director.ShopAt(place) : null;
            string index = Picked("rep:");
            int at;
            CampaignReport report = index != null && int.TryParse(index, out at) && at >= 0 && at < state.Reports.Count ? state.Reports[at] : null;
            if (shop != null)
            {
                WriteCampaign(box, shop, day);
            }
            else if (report != null)
            {
                WriteReport(box, report);
            }
            else
            {
                box.Title(CompanyText.T("reallife_co_tab_marketing"));
                box.Note(CompanyText.T("reallife_co_push_intro"));
                box.Section("reallife_ibox_sec_how");
                box.Note(CompanyText.T("reallife_co_push_measure"));
                box.Note(CompanyText.T("reallife_co_push_game"));
                box.Note(CompanyText.T("reallife_co_push_pick"));
                Guide(box);
            }
        }

        private void WriteCampaign(InfoBox box, CompanyShop shop, int day)
        {
            ShopFile file = shop.File;
            string place = file.Place;
            box.Title(CompanyText.T("reallife_co_push_title", "business", shop.Name));
            if (file.Push >= 0)
            {
                box.Chip(CompanyText.T(file.Pushing(day) ? "reallife_ibox_chip_running" : "reallife_ibox_chip_booked"), CellTone.Green);
                box.Section("reallife_ibox_sec_campaign");
                box.Row("reallife_co_push_running", CompanyText.Campaign(file.Push));
                box.Line(CompanyText.T("reallife_co_push_days", "from", file.PushFrom.ToString(), "to", (file.PushEnd - 1).ToString()));
                box.Row("reallife_co_push_cost", CompanyText.Money(file.PushCost));
                box.Row("reallife_co_push_effect", CompanyText.Percent(file.PushPercent), CellTone.Green);
                box.Note(CompanyText.T("reallife_co_push_report_due", "day", file.PushEnd.ToString()));
                BackButton(box);
                return;
            }
            if (!_director.Promoted(shop))
            {
                // An Empire Motors dealership's customers are that mod's (Core/EmpireMotors).
                box.Note(CompanyText.T(EmpireMotors.IsDealership(shop.Reg) ? "reallife_co_push_empire" : "reallife_co_push_no_figure"));
                BackButton(box);
                return;
            }
            int offered = _director.OfferedKinds(shop, _kinds);
            int at = CompanyCatalogue.Clamp(_campaign, 0, offered - 1);
            box.Section("reallife_ibox_sec_campaign");
            int kind = _kinds[at];
            if (!box.HasSelector || !box.Selector(offered, at, i => CompanyText.Campaign(_kinds[i]), i =>
                {
                    _campaign = i;
                    Refresh();
                }))
            {
                box.Line(CompanyText.Campaign(kind));
            }
            int cost, percent, suit, repeat;
            _director.CampaignQuote(shop, kind, out cost, out percent, out suit, out repeat);
            box.Note(CompanyText.T(CampaignAbout[kind], "share", CompanyText.Share(CompanyCatalogue.DiscountShare)));
            box.Row("reallife_co_push_cost", CompanyText.Money(cost));
            box.Row("reallife_co_push_length", CompanyText.T("reallife_co_push_length_value", "days", CompanyCatalogue.CampaignDays[kind].ToString(), "from", (day + 1).ToString()));
            box.Row("reallife_co_push_effect", CompanyText.Percent(percent), CellTone.Green);
            string economy = CompanyText.Share(CompanyCatalogue.CampaignCycle[CompanyCatalogue.Clamp((int)_director.Cycle, 0, 3)]);
            box.Note(CompanyText.T("reallife_co_push_parts", "own", CompanyText.Percent(CompanyCatalogue.CampaignFootfall[kind]), "district", CompanyText.Share(suit),
                "economy", economy) + (repeat < 1000 ? " " + CompanyText.T("reallife_co_push_repeat", "share", CompanyText.Share(repeat)) : string.Empty));
            var buttons = box.ButtonRow();
            int chosen = kind;
            InfoBox.Button(buttons, InfoBox.Positive, "reallife_co_push_book", () => _director.BookCampaign(place, chosen));
            InfoBox.Button(buttons, InfoBox.Additional, "reallife_co_back", Back);
            Guide(box);
        }

        private void WriteReport(InfoBox box, CampaignReport r)
        {
            box.Title(CompanyText.Campaign(r.Kind));
            box.Subtitle(r.Name);
            box.Subtitle(CompanyText.T("reallife_co_report_days", "from", r.From.ToString(), "to", (r.From + r.Days - 1).ToString(), "days", r.Days.ToString()));
            if (r.Cost > 0)
            {
                box.Chip(CompanyText.Percent(r.Return), r.Return > 0 ? CellTone.Green : CellTone.Red);
            }
            if (r.DaysBefore < r.Days || r.DaysDuring < r.Days)
            {
                box.Note(CompanyText.T("reallife_co_report_short", "before", r.DaysBefore.ToString(), "during", r.DaysDuring.ToString()));
            }
            box.Section("reallife_ibox_sec_before_during");
            box.Line(CompanyText.T("reallife_co_report_customers", "before", r.CustomersBefore.ToString(), "during", r.CustomersDuring.ToString(),
                "change", CompanyText.Percent(r.Footfall)));
            box.Line(CompanyText.T("reallife_co_report_takings", "before", CompanyText.Money(r.TakingsBefore), "during", CompanyText.Money(r.TakingsDuring)));
            box.Line(CompanyText.T("reallife_co_report_profit", "before", CompanyText.Money(r.ProfitBefore), "during", CompanyText.Money(r.ProfitDuring)));
            box.Section("reallife_ibox_sec_money");
            box.Row("reallife_co_push_cost", CompanyText.Money(r.Cost));
            box.Row("reallife_co_report_extra", CompanyText.Money(r.Extra), r.Extra > 0 ? CellTone.Green : CellTone.Plain);
            if (r.Cost > 0)
            {
                box.Row("reallife_co_report_return", CompanyText.Percent(r.Return), r.Return > 0 ? CellTone.Green : CellTone.Red);
            }
            else
            {
                box.Note(CompanyText.T("reallife_co_report_no_return"));
            }
            BackButton(box);
        }
    }
}
