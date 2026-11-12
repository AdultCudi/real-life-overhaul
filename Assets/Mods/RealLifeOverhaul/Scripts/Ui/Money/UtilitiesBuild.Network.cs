using System;
using System.Collections.Generic;
using System.Globalization;

namespace RealLifeOverhaul.Ui
{
    public static partial class UtilitiesBuild
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        // ------------------------------------------------------------------------------------------------- Your network

        private static void Network(UtilitiesFaceModel m, UtilitiesFacts f, UtilitiesView v, UtilitiesWords w)
        {
            UtilityNetworkFact n = f.Network;
            var net = new UtilitiesNetworkModel { State = n.State };
            m.Network = net;
            net.ProvidersTitle = w.T("reallife_ui_util_providers", "Other providers in the city");
            net.Providers = Providers(n, w);
            if (n.State == 2)
            {
                Sold(net, n, w);
                return;
            }
            Districts(net, f, n, w);
            Plans(net, n, w);
            if (n.State == 0)
            {
                Pitch(net, n, w);
                return;
            }
            Hero(net, f, n, w);
            net.Chart = Chart(n, w);
        }

        private static void Hero(UtilitiesNetworkModel net, UtilitiesFacts f, UtilityNetworkFact n, UtilitiesWords w)
        {
            net.Name = w.T("reallife_bb_box_title", "Your broadband company");
            int live = 0, digging = 0;
            for (int i = 0; i < n.Districts.Length; i++)
            {
                live += n.Districts[i].State == 2 ? 1 : 0;
                digging += n.Districts[i].State == 1 ? 1 : 0;
            }
            string state = live == 0 && digging == 0 ? w.T("reallife_ui_util_state_none", "No district dug yet")
                : live == 0 ? w.T("reallife_ui_util_state_first", "Digging the first district")
                : digging > 0 ? w.F("reallife_ui_util_state_live_dig", "{live} live, {dig} digging", "live", live.ToString(Inv), "dig", digging.ToString(Inv))
                : w.F("reallife_ui_util_state_live", "{live} of {all} districts live", "live", live.ToString(Inv), "all", n.Districts.Length.ToString(Inv));
            net.Chips = new[] { w.T("reallife_ui_util_fibre", "Fibre network"), w.F("reallife_ui_util_founded", "Founded {day}", "day", w.Day(n.FoundedDay)), state };
            net.ChipTones = new[] { UiRole.Accent, UiRole.Muted, live == 0 ? UiRole.Warn : UiRole.Ok };
            long profit = (long)n.TakingsDay - n.UpkeepDay;
            net.FigureLabels = new[]
            {
                w.T("reallife_ui_util_fig_customers", "Customers"), w.T("reallife_ui_util_fig_takings", "Takings a day"),
                w.T("reallife_ui_util_fig_upkeep", "Upkeep a day"), w.T("reallife_ui_util_fig_profit", "Profit a day")
            };
            net.FigureValues = new[] { w.N(n.Customers), w.Money(n.TakingsDay), w.Money(n.UpkeepDay), w.Money(profit) };
            net.FigureInks = new[] { UiRole.Text, UiRole.Text, UiRole.Text, profit >= 0 ? UiRole.OkText : UiRole.DangerText };
            net.Actions = new[]
            {
                new UiActionModel
                {
                    Label = w.T("reallife_ui_util_dig_district", "Dig in a district"), Kind = UiButtonKind.Primary, Icon = UiIcon.Wrench, Key = UiAction.Alt,
                    Command = UtilitiesFace.HeroAction, Arg = 0, Disabled = digging + live >= n.Districts.Length
                },
                new UiActionModel
                {
                    Label = w.T("reallife_ui_util_set_plans", "Set the plans"), Kind = UiButtonKind.Secondary, Icon = UiIcon.Gear, Key = UiAction.Option,
                    Command = UtilitiesFace.HeroAction, Arg = 1
                }
            };
            net.PlansLink = w.T("reallife_ui_util_set_prices", "Set prices");
        }

        // The company not founded: what it costs, what it gets, and the button that founds it.
        private static void Pitch(UtilitiesNetworkModel net, UtilityNetworkFact n, UtilitiesWords w)
        {
            net.Name = w.T("reallife_ui_util_found_title", "Found your broadband company");
            net.Chips = new[] { n.HasHeadquarters ? w.F("reallife_bb_box_hq", "Office and billing: {name}", "name", n.HeadquartersName) : w.T("reallife_ui_util_no_hq", "No headquarters") };
            net.ChipTones = new[] { n.HasHeadquarters ? UiRole.Ok : UiRole.Warn };
            net.FigureLabels = new[]
            {
                w.T("reallife_ui_util_fig_licence", "Operator's licence"), w.T("reallife_ui_util_fig_district", "Each district"), w.T("reallife_ui_util_fig_digging", "Digging takes")
            };
            net.FigureValues = new[]
            {
                w.Money(n.Licence), w.F("reallife_ui_util_range", "{from} to {to}", "from", w.Money(n.DigLeast), "to", w.Money(n.DigMost)),
                w.F("reallife_ui_util_months", "{n} months", "n", n.BuildMonths.ToString(Inv))
            };
            net.FigureInks = new[] { UiRole.Text, UiRole.Text, UiRole.Text };
            net.Actions = new[]
            {
                new UiActionModel
                {
                    Label = w.T("reallife_bb_box_found", "Found the company"), Kind = UiButtonKind.Primary, Icon = UiIcon.Check, Key = UiAction.Alt,
                    Command = UtilitiesFace.HeroAction, Arg = 2, Disabled = !n.HasHeadquarters
                }
            };
            net.Gets = new UtilitiesCard
            {
                Title = w.T("reallife_ui_util_gets", "What you get"),
                Heading = true,
                Lines = new[]
                {
                    new UtilitiesLine
                    {
                        Value = w.F("reallife_bb_box_intro", "Become an internet provider yourself. Operator's licence: {fee}, then each district's fibre costs a fortune and takes two months to dig.",
                            "fee", w.Money(n.Licence))
                    },
                    new UtilitiesLine
                    {
                        Value = n.HasHeadquarters ? w.F("reallife_bb_box_hq", "Office and billing: {name}", "name", n.HeadquartersName)
                            : w.T("reallife_bb_box_need_hq", "You need a headquarters first: it is the company's office and bills its customers."),
                        Check = true, Met = n.HasHeadquarters
                    },
                    new UtilitiesLine { Value = w.T("reallife_ui_util_get_fibre", "Your own fibre, dug district by district"), Check = true, Met = true },
                    new UtilitiesLine { Value = w.T("reallife_ui_util_get_choice", "Customers choose you or the city's providers, by price and reliability"), Check = true, Met = true },
                    new UtilitiesLine { Value = w.T("reallife_ui_util_get_own", "Your own homes and businesses connect free on your network"), Check = true, Met = true },
                    new UtilitiesLine { Value = w.T("reallife_ui_util_get_sell", "Set prices, upkeep and marketing, or sell it for years of profit"), Check = true, Met = true }
                }
            };
        }

        private static void Sold(UtilitiesNetworkModel net, UtilityNetworkFact n, UtilitiesWords w)
        {
            net.Name = w.T("reallife_bb_box_title", "Your broadband company");
            net.Line = w.F("reallife_bb_box_sold", "Sold on {day} for {price}. Harbour Fibre runs the network now.", "day", w.Day(n.SoldDay), "price", w.Money(n.SalePrice));
            net.Chips = new[] { w.F("reallife_ui_util_sold_day", "Sold {day}", "day", w.Day(n.SoldDay)) };
            net.ChipTones = new[] { UiRole.Muted };
            net.EmptyTitle = w.T("reallife_ui_util_sold_title", "The company is sold");
            net.EmptyBody = w.T("reallife_bb_sold_out", "You sold your broadband company: the city's licence went with it.");
        }

        private static void Districts(UtilitiesNetworkModel net, UtilitiesFacts f, UtilityNetworkFact n, UtilitiesWords w)
        {
            net.DistrictsTitle = w.T("reallife_ui_util_districts", "Districts");
            net.Heads = new[]
            {
                w.T("reallife_bb_col_district", "District"), w.T("reallife_ui_util_col_reached", "Homes reached"), w.T("reallife_ui_util_col_customers", "Customers"),
                w.T("reallife_ui_util_col_takeup", "Take-up of homes reached"), w.T("reallife_util_col_state", "State"), string.Empty
            };
            var rows = new UtilitiesDistrictRow[n.Districts.Length];
            int live = 0;
            long homes = 0, customers = 0;
            for (int i = 0; i < rows.Length; i++)
            {
                UtilityDistrictFact d = n.Districts[i];
                var row = new UtilitiesDistrictRow { Name = d.Name, Hue = i % 8, Homes = w.N(d.Homes), TakeUp = -1, ActionDistrict = i };
                if (n.State == 1 && d.State == 2)
                {
                    live++;
                    homes += d.Homes;
                    customers += d.Customers;
                    row.Customers = w.N(d.Customers);
                    row.TakeUp = d.Homes > 0 ? (int)Math.Min(100L, (long)d.Customers * 100L / d.Homes) : 0;
                    row.TakeUpWords = row.TakeUp.ToString(Inv) + "%";
                    row.Bar = true;
                    row.State = w.T("reallife_money_live", "Live");
                    row.StateTone = UiRole.Ok;
                    row.Action = d.ExtendCost > 0 ? w.T("reallife_ui_util_extend", "Extend") : null;
                }
                else if (n.State == 1 && d.State == 1)
                {
                    int left = Math.Max(1, d.ReadyDay - f.Day);
                    row.TakeUpWords = left == 1 ? w.T("reallife_ui_util_day_left", "1 day left") : w.F("reallife_ui_util_days_left", "{n} days left", "n", left.ToString(Inv));
                    row.State = w.T("reallife_ui_util_digging", "Digging");
                    row.StateTone = UiRole.Warn;
                }
                else
                {
                    row.TakeUpWords = w.F("reallife_ui_util_to_dig", "{cost} to dig", "cost", w.Money(d.DigCost));
                    row.State = w.T("reallife_ui_util_not_started", "Not started");
                    row.StateTone = UiRole.Muted;
                    row.Action = n.State == 1 ? w.T("reallife_money_dig", "Dig") : null;
                }
                rows[i] = row;
            }
            net.Districts = rows;
            net.TotalName = live > 0 ? w.F("reallife_ui_util_total_live", "Districts live: {count}", "count", live.ToString(Inv)) : string.Empty;
            net.TotalHomes = live > 0 ? w.N((int)Math.Min(int.MaxValue, homes)) : string.Empty;
            net.TotalCustomers = live > 0 ? w.N((int)Math.Min(int.MaxValue, customers)) : string.Empty;
            net.TotalTakeUp = live > 0 && homes > 0 ? (customers * 100L / homes).ToString(Inv) + "%" : string.Empty;
        }

        private static void Plans(UtilitiesNetworkModel net, UtilityNetworkFact n, UtilitiesWords w)
        {
            net.PlansTitle = w.T("reallife_ui_util_your_plans", "Your plans");
            string[] names = { w.T("reallife_ui_util_plan_basic", "Basic"), w.T("reallife_ui_util_plan_fast", "Fast"), w.T("reallife_ui_util_plan_gigabit", "Gigabit") };
            string per = w.T("reallife_ui_util_a_month", "a month");
            net.Plans = new UtilitiesSideRow[n.Plans.Length];
            for (int i = 0; i < n.Plans.Length; i++)
            {
                UtilityPlanFact p = n.Plans[i];
                net.Plans[i] = new UtilitiesSideRow
                {
                    Name = names[Math.Min(i, names.Length - 1)],
                    Sub = n.State == 1 ? w.F("reallife_ui_util_plan_line", "{speed}, customers: {count}", "speed", w.Speed(p.Mbps), "count", w.N(p.Customers)) : w.Speed(p.Mbps),
                    Price = w.Cents(p.PriceCents), Per = per, Icon = UiIcon.Globe
                };
            }
        }

        private static UtilitiesSideRow[] Providers(UtilityNetworkFact n, UtilitiesWords w)
        {
            var rows = new UtilitiesSideRow[n.Rivals.Length];
            for (int i = 0; i < rows.Length; i++)
            {
                UtilityRivalFact r = n.Rivals[i];
                rows[i] = new UtilitiesSideRow
                {
                    Name = r.Name,
                    Sub = w.F("reallife_ui_util_rival_line", "{districts} districts, {from} to {to} Mbit, {kind}", "districts", r.Districts.ToString(Inv), "from", w.N(r.FromMbps),
                        "to", w.N(r.ToMbps), "kind", r.Kind),
                    Price = w.F("reallife_ui_util_from_price", "from {price}", "price", w.Cents(r.FromCents)), Per = w.T("reallife_ui_util_a_month", "a month")
                };
            }
            return rows;
        }

        // The customers over the days the company has had, up to sixty: a line of as many points as there are days of history.
        private static UiChartModel Chart(UtilityNetworkFact n, UtilitiesWords w)
        {
            string title = w.T("reallife_ui_util_chart", "Customers, 60 days");
            int count = n.Series.Length;
            var chart = new UiChartModel
            {
                Kind = UiChartKind.Line, Title = string.Empty,
                EmptyTitle = w.T("reallife_ui_util_chart_empty", "No history yet"), EmptyBody = w.T("reallife_ui_util_chart_empty_body", "The line fills in a day at a time.")
            };
            chart.Title = title;
            if (count < 2)
            {
                return chart;
            }
            var values = new float[count];
            var points = new string[count];
            for (int i = 0; i < count; i++)
            {
                values[i] = n.Series[i];
                points[i] = (n.SeriesFirstDay + i).ToString(Inv);
            }
            chart.Points = points;
            chart.Series = new[] { new UiSeries { Label = title, Values = values } };
            chart.Format = value => ((int)value).ToString("N0", Inv);
            return chart;
        }

        // ------------------------------------------------------------------------------------------------- the sheet over the page

        private static void Sheet(UtilitiesFaceModel m, UtilitiesFacts f, UtilitiesView v, UtilitiesWords w)
        {
            if (m.Tab != 2 || v.Sheet == UtilitiesAsk.None)
            {
                return;
            }
            UtilityNetworkFact n = f.Network;
            var sheet = new UtilitiesSheetModel();
            UtilityDistrictFact d = v.District >= 0 && v.District < n.Districts.Length ? n.Districts[v.District] : null;
            string cancel = w.T("reallife_net_box_cancel", "Cancel");
            switch (v.Sheet)
            {
                case UtilitiesAsk.Found:
                    Question(sheet, w.T("reallife_bb_confirm_found_title", "Found a broadband company"),
                        w.F("reallife_bb_confirm_found_body",
                            "The city's operator's licence costs {fee}, paid now and not given back. Each district's network is then built on its own, from this tab.",
                            "fee", w.Money(n.Licence)), cancel, w.T("reallife_bb_box_found", "Found the company"), false);
                    break;
                case UtilitiesAsk.Build when d != null:
                    Question(sheet, w.F("reallife_bb_confirm_build_title", "Build your network in {district}", "district", d.Name),
                        w.F("reallife_bb_confirm_build_body",
                            "Digging the fibre costs {cost}, paid now and not given back, and takes {months} months. Its upkeep is about {upkeep} a month at the usual maintenance.",
                            "cost", w.Money(d.DigCost), "months", n.BuildMonths.ToString(Inv), "upkeep", w.Money(d.Upkeep)), cancel, w.T("reallife_bb_box_build", "Build here"), false);
                    break;
                case UtilitiesAsk.Extend when d != null:
                    Question(sheet, w.F("reallife_endscale_bb_confirm_extend_title", "Extend the network in {district}?", "district", d.Name),
                        w.F("reallife_endscale_bb_confirm_extend_body", "Extending the network to every home and business of the district costs {cost}, paid now.", "cost", w.Money(d.ExtendCost)),
                        cancel, w.T("reallife_endscale_bb_extend", "Extend the network"), false);
                    break;
                case UtilitiesAsk.Sell:
                    Question(sheet, w.T("reallife_bb_confirm_sell_title", "Sell your broadband company"),
                        w.F("reallife_bb_confirm_sell_body",
                            "{buyer} offers {price}: four years of the last year's profit, or three tenths of what the network cost if that is more. "
                            + "It runs the network on under its own name, and your places on it are billed like any customer's. The city's licence goes with the company: you cannot found another.",
                            "buyer", n.BuyerName, "price", w.Money(n.SaleValue)), cancel, w.T("reallife_bb_box_sell", "Sell the company"), true);
                    break;
                case UtilitiesAsk.DigList:
                    DigList(sheet, n, w);
                    break;
                case UtilitiesAsk.Settings:
                    Settings(sheet, n, w);
                    break;
                default:
                    return;
            }
            m.Sheet = sheet;
        }

        private static void Question(UtilitiesSheetModel sheet, string title, string body, string cancel, string confirm, bool destructive)
        {
            sheet.Kind = UtilitiesSheetKind.Question;
            sheet.Title = title;
            sheet.Body = body;
            sheet.Answers = new[]
            {
                new UiActionModel { Label = cancel, Kind = UiButtonKind.Secondary, Icon = UiIcon.None, Key = UiAction.Count, Command = UtilitiesFace.SheetAnswer, Arg = 0 },
                new UiActionModel
                {
                    Label = confirm, Kind = destructive ? UiButtonKind.Destructive : UiButtonKind.Primary, Icon = UiIcon.Check, Key = UiAction.Count,
                    Command = UtilitiesFace.SheetAnswer, Arg = 1
                }
            };
        }

        // The districts not started, each with what it costs, and a button that asks before it digs.
        private static void DigList(UtilitiesSheetModel sheet, UtilityNetworkFact n, UtilitiesWords w)
        {
            sheet.Kind = UtilitiesSheetKind.Dig;
            sheet.Title = w.T("reallife_ui_util_dig_district", "Dig in a district");
            sheet.Close = w.T("reallife_ui_util_done", "Done");
            var rows = new List<UtilitiesDistrictRow>(n.Districts.Length);
            for (int i = 0; i < n.Districts.Length; i++)
            {
                UtilityDistrictFact d = n.Districts[i];
                if (d.State != 0)
                {
                    continue;
                }
                rows.Add(new UtilitiesDistrictRow
                {
                    Name = d.Name, Hue = i % 8, Homes = w.F("reallife_ui_util_homes", "{n} homes and businesses", "n", w.N(d.Homes)),
                    TakeUpWords = w.F("reallife_ui_util_to_dig", "{cost} to dig", "cost", w.Money(d.DigCost)), Action = w.T("reallife_money_dig", "Dig"), ActionDistrict = i
                });
            }
            sheet.Districts = rows.ToArray();
        }

        // How the company is run: prices, upkeep and marketing in steps, and the sale.
        private static void Settings(UtilitiesSheetModel sheet, UtilityNetworkFact n, UtilitiesWords w)
        {
            sheet.Kind = UtilitiesSheetKind.Settings;
            sheet.Title = w.T("reallife_ui_util_set_plans", "Set the plans");
            sheet.Close = w.T("reallife_ui_util_done", "Done");
            string a = n.Plans.Length > 0 ? w.Cents(n.Plans[0].PriceCents) : string.Empty;
            string b = n.Plans.Length > 1 ? w.Cents(n.Plans[1].PriceCents) : string.Empty;
            string c = n.Plans.Length > 2 ? w.Cents(n.Plans[2].PriceCents) : string.Empty;
            int budget = n.Marketing >= 0 && n.Marketing < n.MarketingBudgets.Length ? n.MarketingBudgets[n.Marketing] : 0;
            var card = new UtilitiesCard();
            card.Steps = new[]
            {
                new UtilitiesStep
                {
                    Label = w.T("reallife_ui_util_prices", "Prices"), Value = w.F("reallife_ui_util_of_usual", "{share} of the usual", "share", w.Percent(n.PriceShare)),
                    Hint = w.F("reallife_ui_util_prices_hint", "{p0}, {p1} and {p2} a month", "p0", a, "p1", b, "p2", c),
                    CanLess = n.PriceShare > n.PriceLeast, CanMore = n.PriceShare < n.PriceMost
                },
                new UtilitiesStep
                {
                    Label = w.T("reallife_ui_util_maintenance", "Maintenance"), Value = w.F("reallife_ui_util_of_usual", "{share} of the usual", "share", w.Percent(n.Maintenance)),
                    Hint = w.F("reallife_ui_util_outage_hint", "Outages on about {outages} days in a thousand", "outages", n.OutagePerMille.ToString(Inv)),
                    CanLess = n.Maintenance > n.MaintenanceLeast, CanMore = n.Maintenance < n.MaintenanceMost
                },
                new UtilitiesStep
                {
                    Label = w.T("reallife_ui_util_marketing", "Marketing"),
                    Value = budget <= 0 ? w.T("reallife_ui_util_none", "None") : w.F("reallife_ui_util_budget", "{amount} a month", "amount", w.Money(budget)),
                    Hint = w.T("reallife_ui_util_marketing_hint", "Brings in customers, and is paid every day"),
                    CanLess = n.Marketing > 0, CanMore = n.Marketing < n.MarketingBudgets.Length - 1
                }
            };
            var lines = new List<UtilitiesLine>(2)
            {
                new UtilitiesLine { Label = w.T("reallife_ui_util_sale_value", "A buyer would pay"), Value = w.Money(n.SaleValue) }
            };
            if (n.Owed > 0)
            {
                lines.Add(new UtilitiesLine { Value = w.F("reallife_endscale_owed_line", "Owed from costs the cash could not meet: {owed}", "owed", w.Money(n.Owed)), Ink = UiRole.WarnText });
            }
            card.Lines = lines.ToArray();
            card.Actions = new[]
            {
                new UiActionModel
                {
                    Label = w.T("reallife_bb_box_sell", "Sell the company"), Kind = UiButtonKind.Destructive, Icon = UiIcon.Coin, Key = UiAction.Count,
                    Command = UtilitiesFace.SheetAction, Arg = 0
                }
            };
            sheet.Card = card;
        }
    }
}
