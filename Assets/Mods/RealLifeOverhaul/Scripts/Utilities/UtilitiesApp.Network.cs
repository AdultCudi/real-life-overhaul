using System;
using System.Collections.Generic;
using Localizor;
using Localizor.LanguageChangeEvent;
using RealLifeOverhaul.GameUi;
using Streets;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Utilities
{
    /// <summary>The Utilities app's Your network tab: the broadband company's districts, founded, dug, set and sold in the box.</summary>
    public sealed partial class UtilitiesApp
    {
        private void WriteNetwork(InfoBox box, UtilitiesState state, ref string selected)
        {
            Network n = state.Network;
            int today = UtilitiesGame.Today, level = Prices.Level, picked = -1, open = 0;
            int scale = _director.Company != null ? _director.Company.ScaleNow() : 0, buy = EndScaleRules.Buying(Settings.EndScale);
            for (int d = 0; d < Network.Districts; d++)
            {
                string id = "bb|" + d;
                TableRow row = Row(id);
                row.Pick = true;
                if (id == _pick)
                {
                    selected = id;
                    picked = d;
                }
                bool isOpen = n.Open(d, today), digging = n.Digging(d);
                open += isOpen ? 1 : 0;
                int connections = BroadbandRules.Connections(n, d), share = n.Exists ? n.Share[d, Network.Own] : 0, rivals = Rivals(d);
                int cost = isOpen || digging ? n.Spent[d] : BroadbandRules.BuildCost(d, level, buy);
                int market = MarketShown(n, d, buy, isOpen || digging);
                row.Set(0, BroadbandCompany.District(d));
                row.Set(1, isOpen ? UtilitiesText.T("reallife_bb_state_open", "day", UtilitiesText.Day(n.Built[d]))
                    : digging ? UtilitiesText.T("reallife_bb_state_digging", "day", UtilitiesText.Day(n.ReadyDay(d))) : UtilitiesText.T("reallife_bb_state_none"),
                    isOpen ? TableRow.Green : digging ? TableRow.Yellow : TableRow.Plain);
                row.Set(2, n.Exists ? connections.ToString("N0", UtilitiesText.Culture) : "-", connections);
                row.Set(3, n.Exists ? UtilitiesText.T("reallife_bb_percent", "share", UtilitiesText.Percent(share)) : "-", share);
                row.Set(4, market.ToString("N0", UtilitiesText.Culture), market);
                row.Set(5, rivals.ToString(UtilitiesText.Culture), rivals);
                row.Set(6, UtilitiesText.Money(cost), cost);
            }
            UiClone.Words(_shell.Header, n.Running ? UtilitiesText.T("reallife_bb_headline", "count", BroadbandRules.Connections(n).ToString("N0", UtilitiesText.Culture), "districts", open.ToString())
                : UtilitiesText.T(n.State == CompanyState.Sold ? "reallife_bb_headline_sold" : "reallife_bb_headline_none"));
            if (picked >= 0)
            {
                WriteDistrict(box, n, picked, today, level, scale, buy);
                return;
            }
            _pick = null;
            WriteCompany(box, n, open, level, scale, today, buy);
        }

        // a district's connections the company reaches once dug, or would if dug now (at scale, the whole district)
        private static int MarketShown(Network n, int d, int scale, bool built)
        {
            return built ? BroadbandRules.MarketOf(n, d) : EndScaleRules.Mix(BroadbandCatalogue.Market[d], BroadbandRules.MarketAtScale(d), scale);
        }

        private void WriteCompany(InfoBox box, Network n, int open, int level, int scale, int today, int buy)
        {
            BroadbandCompany company = _director.Company;
            BuildingRegistration hq = UtilitiesGame.Headquarters();
            box.Title(UtilitiesText.T("reallife_bb_box_title"));
            if (n.State == CompanyState.Sold)
            {
                box.Line(UtilitiesText.T("reallife_bb_box_sold", "day", UtilitiesText.Day(n.SoldDay), "price", UtilitiesText.Money(n.SalePrice)));
                Meta.Guide.Point(box, GuideTitle);
                return;
            }
            if (!n.Running)
            {
                int fee = BroadbandRules.LicenceFee(level, buy);
                box.Line(hq != null ? UtilitiesText.T("reallife_bb_box_hq", "name", HqName(hq)) : UtilitiesText.T("reallife_bb_box_need_hq"));
                box.Note(UtilitiesText.T("reallife_bb_box_intro", "fee", UtilitiesText.Money(fee)));
                if (hq != null)
                {
                    InfoBox.Button(box.ButtonRow(), InfoBox.Positive, "reallife_bb_box_found", () => Ask("reallife_bb_confirm_found_title".Localize(),
                        "reallife_bb_confirm_found_body".Localize(new { fee = UtilitiesText.Money(fee) }), "reallife_bb_box_found", false,
                        () => company.Found(out string why) ? null : why));
                }
                Meta.Guide.Point(box, GuideTitle);
                return;
            }
            if (hq == null)
            {
                box.Line(UtilitiesText.T("reallife_bb_box_no_hq"));
            }
            int connections = BroadbandRules.Connections(n);
            box.Row("reallife_bb_box_connections", UtilitiesText.T("reallife_bb_box_connections_value", "count", connections.ToString("N0", UtilitiesText.Culture), "districts", open.ToString()));
            if (scale > 0)
            {
                box.Row("reallife_endscale_bb_crews", UtilitiesText.T("reallife_endscale_bb_crews_value",
                    "field", n.Crew[BroadbandCatalogue.Field].ToString(), "field_need", BroadbandRules.CrewNeed(n, BroadbandCatalogue.Field, today).ToString(),
                    "calls", n.Crew[BroadbandCatalogue.Calls].ToString(), "calls_need", BroadbandRules.CrewNeed(n, BroadbandCatalogue.Calls, today).ToString(),
                    "ops", n.Crew[BroadbandCatalogue.Operations].ToString(), "ops_need", BroadbandRules.CrewNeed(n, BroadbandCatalogue.Operations, today).ToString(),
                    "wages", UtilitiesText.Money(BroadbandRules.CrewCents(n) * BroadbandCatalogue.WageMonthDays / GameCalendar.MonthDays * level / PriceRules.Start / 100f)));
            }
            if (n.Owed > 0)
            {
                box.Line(UtilitiesText.T("reallife_endscale_owed_line", "owed", UtilitiesText.Money(n.Owed)));
            }
            if (n.HistoryCount > 0)
            {
                box.Row("reallife_bb_box_last", UtilitiesText.T("reallife_bb_box_last_value", "takings", UtilitiesText.Money(n.LastTakings), "costs", UtilitiesText.Money(n.LastCosts),
                    "profit", UtilitiesText.Money(n.LastTakings - n.LastCosts)));
            }
            box.Row("reallife_bb_box_month", UtilitiesText.T("reallife_bb_box_month_value", "takings", UtilitiesText.Money(n.MonthTakings), "costs", UtilitiesText.Money(n.MonthCosts)));
            box.Row("reallife_bb_box_year", UtilitiesText.Money(BroadbandRules.YearProfit(n)));
            long value = BroadbandRules.SaleNet(n);   // what a buyer pays less what the company owes, as Sell pays it
            box.Row("reallife_bb_box_value", UtilitiesText.Money(value));
            box.Headline("reallife_bb_box_settings");
            int least = BroadbandCatalogue.PriceLeast, step = BroadbandCatalogue.PriceStep;
            box.Selector((BroadbandCatalogue.PriceMost - least) / step + 1, (n.PriceShare - least) / step, i => PricesText(least + i * step, level), i => UiClone.Safe(() =>
            {
                company.SetPrices(least + i * step);
                Refresh();
            }));
            int careLeast = BroadbandCatalogue.MaintenanceLeast, careStep = BroadbandCatalogue.MaintenanceStep;
            box.Selector((BroadbandCatalogue.MaintenanceMost - careLeast) / careStep + 1, (n.Maintenance - careLeast) / careStep, i => UtilitiesText.T("reallife_bb_maintenance_option",
                "share", ((careLeast + i * careStep) / 10).ToString(), "outages", BroadbandRules.OutagePerMille(careLeast + i * careStep).ToString()), i => UiClone.Safe(() =>
            {
                company.SetMaintenance(careLeast + i * careStep);
                Refresh();
            }));
            box.Selector(BroadbandCatalogue.Marketing.Length, n.Marketing, i => i == 0 ? UtilitiesText.T("reallife_bb_marketing_none")
                : UtilitiesText.T("reallife_bb_marketing_option", "amount", UtilitiesText.Money(BroadbandRules.MarketingMoney(i, level))), i => UiClone.Safe(() =>
            {
                company.SetMarketing(i);
                Refresh();
            }));
            InfoBox.Button(box.ButtonRow(), InfoBox.Negative, "reallife_bb_box_sell", () => Ask("reallife_bb_confirm_sell_title".Localize(),
                "reallife_bb_confirm_sell_body".Localize(new { price = UtilitiesText.Money(value), buyer = UtilitiesText.T(BroadbandCatalogue.SoldNameKey) }), "reallife_bb_box_sell", true,
                () => company.Sell(out string why) ? null : why));
            Meta.Guide.Point(box, GuideTitle);
        }

        private void WriteDistrict(InfoBox box, Network n, int d, int today, int level, int scale, int buy)
        {
            BroadbandCompany company = _director.Company;
            box.Title(BroadbandCompany.District(d));
            int cost = BroadbandRules.BuildCost(d, level, buy), extend = BroadbandRules.ExtendCost(n, d, level, today, buy);
            if (n.Open(d, today))
            {
                box.Line(UtilitiesText.T("reallife_bb_box_district_open", "day", UtilitiesText.Day(n.Built[d]), "count", BroadbandRules.Connections(n, d).ToString("N0", UtilitiesText.Culture),
                    "share", UtilitiesText.Percent(n.Share[d, Network.Own]), "trust", UtilitiesText.Percent(BroadbandRules.Trust(n, d, today, GameCalendar.MonthDays))));
            }
            else if (n.Digging(d))
            {
                box.Line(UtilitiesText.T("reallife_bb_box_district_digging", "day", UtilitiesText.Day(n.ReadyDay(d))));
            }
            else
            {
                box.Line(UtilitiesText.T("reallife_bb_box_district_cost", "cost", UtilitiesText.Money(cost), "months", BroadbandCatalogue.BuildMonths.ToString()));
            }
            box.Row("reallife_bb_box_district_market", MarketShown(n, d, buy, n.Open(d, today) || n.Digging(d)).ToString("N0", UtilitiesText.Culture));
            if (extend > 0)
            {
                box.Line(UtilitiesText.T("reallife_endscale_bb_district_old", "reach", BroadbandCatalogue.Market[d].ToString("N0", UtilitiesText.Culture),
                    "all", BroadbandRules.MarketAtScale(d).ToString("N0", UtilitiesText.Culture), "cost", UtilitiesText.Money(extend)));
            }
            box.Row("reallife_bb_box_district_shares", Shares(n.Exists ? n : CityAsItIs(), d));
            Transform buttons = box.ButtonRow();
            if (extend > 0)
            {
                InfoBox.Button(buttons, InfoBox.Positive, "reallife_endscale_bb_extend", () => Ask(
                    "reallife_endscale_bb_confirm_extend_title".Localize(new { district = BroadbandCompany.District(d) }),
                    "reallife_endscale_bb_confirm_extend_body".Localize(new { cost = UtilitiesText.Money(extend) }), "reallife_endscale_bb_extend", false,
                    () => company.Extend(d, out string why) ? null : why));
            }
            if (n.Running && !n.Open(d, today) && !n.Digging(d))
            {
                int upkeep = (int)EndScaleRules.Mix((long)BroadbandRules.BuildCost(d, level) * BroadbandCatalogue.UpkeepPerMille / 1000,
                    (long)BroadbandRules.BuildCost(d, level, EndScaleRules.Full) * BroadbandCatalogue.UpkeepPerMilleScale / 1000, buy);
                InfoBox.Button(buttons, InfoBox.Positive, "reallife_bb_box_build", () => Ask("reallife_bb_confirm_build_title".Localize(new { district = BroadbandCompany.District(d) }),
                    "reallife_bb_confirm_build_body".Localize(new { cost = UtilitiesText.Money(cost), months = BroadbandCatalogue.BuildMonths.ToString(), upkeep = UtilitiesText.Money(upkeep) }),
                    "reallife_bb_box_build", false, () => company.Build(d, out string why) ? null : why));
            }
            InfoBox.Button(buttons, InfoBox.Additional, "reallife_util_box_back", Back);
            Meta.Guide.Point(box, GuideTitle);
        }

        // the game's own question before a large sum is spent or the company sold; on yes it is done, a refusal said
        private void Ask(LanguageChangeEventDataHolder title, LanguageChangeEventDataHolder body, string confirmKey, bool negative, Func<string> act)
        {
            UiClone.Safe(() => GameConfirm.Show(title, body, () => UiClone.Safe(() =>
            {
                string why = act();
                if (why != null)
                {
                    UtilitiesText.Toast(NotificationType.Warning, UtilitiesText.T(why), 8f);
                }
                Refresh();
            }), null, confirmKey, "common_cancel", false, negative));
        }

        private static string PricesText(int share, int level)
        {
            return UtilitiesText.T("reallife_bb_price_option", "share", (share / 10).ToString(), "p0", UtilitiesText.Money(BroadbandRules.PlanCents(share, 0, level) / 100f),
                "p1", UtilitiesText.Money(BroadbandRules.PlanCents(share, 1, level) / 100f), "p2", UtilitiesText.Money(BroadbandRules.PlanCents(share, 2, level) / 100f));
        }

        // a district's providers by share, the largest first: "Kestrel Cable 45%, Brightvein Fibre 30%, ..."
        private static string Shares(Network n, int d)
        {
            var order = new List<int>(Network.Providers);
            for (int p = 0; p < Network.Providers; p++)
            {
                if (n.Share[d, p] > 0)
                {
                    order.Add(p);
                }
            }
            order.Sort((x, y) => n.Share[d, y].CompareTo(n.Share[d, x]));
            var words = new System.Text.StringBuilder(96);
            for (int i = 0; i < order.Count; i++)
            {
                int p = order[i];
                string name = p == Network.Own ? UtilitiesText.T(n.State == CompanyState.Sold ? BroadbandCatalogue.SoldNameKey : BroadbandCatalogue.OwnNameKey)
                    : UtilitiesText.T(InternetCatalogue.Providers[p].NameKey);
                words.Append(i == 0 ? string.Empty : ", ").Append(UtilitiesText.T("reallife_bb_share_item", "provider", name, "share", UtilitiesText.Percent(n.Share[d, p])));
            }
            return words.ToString();
        }

        private static Network CityAsItIs()
        {
            if (_cityAsItIs == null)
            {
                _cityAsItIs = new Network();
                BroadbandRules.Found(_cityAsItIs, 0);
                _cityAsItIs.State = CompanyState.None;
            }
            return _cityAsItIs;
        }

        private static int Rivals(int d)
        {
            int count = 0;
            for (int p = 0; p < InternetCatalogue.Providers.Length; p++)
            {
                count += InternetRules.Reaches(p, BroadbandCatalogue.Districts[d]) ? 1 : 0;
            }
            return count;
        }

        private static string HqName(BuildingRegistration hq)
        {
            return string.IsNullOrEmpty(hq.BusinessName) ? hq.Address.ToFormattedString() : hq.BusinessName;
        }
    }
}
