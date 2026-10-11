using System.Collections.Generic;
using Localizor;
using RealLifeOverhaul.GameUi;
using UI;
using UnityEngine;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// The Shops tab. The table lists every site and, under them, the businesses and warehouses that have none. A
    /// site that is picked has three pages in the white box: its figures for today and the week (orders, takings,
    /// cost of goods, delivery, refunds, what is left, the queue), its rating with what is moving it and what sold
    /// best and ran out, and its settings (who delivers, what is promised, a better site, advertising, hosting, its
    /// name, closing it). A business that is picked has the page that opens a site for it.
    /// </summary>
    public sealed partial class OnlineApp
    {
        private const string ShopRow = "shop:", NewRow = "new:";
        private const int PageFigures = 0, PageRating = 1, PageSettings = 2;

        private readonly List<BuildingRegistration> _candidates = new List<BuildingRegistration>(16);
        private int _quality = 1, _way = OnlineCatalogue.Courier, _promise = 1;   // what the page that opens a site is set to

        private string[] WriteShops(InfoBox box, OnlineState state, int day, int now)
        {
            IReadOnlyList<WebPlace> places = _director.Places;
            int orders = 0, takings = 0, paid = 0;
            WebPlace picked = null;
            string pickedShop = Picked(_picked, ShopRow), pickedNew = Picked(_picked, NewRow);
            for (int i = 0; i < places.Count; i++)
            {
                WebPlace place = places[i];
                WebShop shop = place.Shop;
                FinanceRow row = Row(ShopRow + shop.Place);
                row.A = shop.Name;
                row.B = place.Business;
                row.C = OnlineText.T("reallife_online_row_orders", "today", shop.Today.Orders.ToString(), "open", shop.Orders.Count.ToString());
                row.D = shop.Offline ? OnlineText.T("reallife_online_row_offline") : OnlineText.T("reallife_online_row_stars", "stars", OnlineText.Stars(shop.Stars));
                row.Tone = shop.Offline ? 1 : shop.Stars < OnlineCatalogue.PoorStars ? 2 : 0;
                orders += shop.Today.Orders;
                takings += shop.Today.Takings;
                paid += shop.Today.PaidIn;
                picked = shop.Place == pickedShop ? place : picked;
            }
            _director.Candidates(_candidates);
            BuildingRegistration opening = null;
            for (int i = 0; i < _candidates.Count; i++)
            {
                string key = _director.KeyOf(_candidates[i]);
                FinanceRow row = Row(NewRow + key);
                row.A = _director.NameOf(_candidates[i]);
                row.B = _director.TypeOf(_candidates[i]);
                row.D = OnlineText.T("reallife_online_row_none");
                opening = key == pickedNew ? _candidates[i] : opening;
            }
            if (_rows.Count == 0)
            {
                Say(OnlineText.T("reallife_online_row_no_business"));
            }
            if (picked != null)
            {
                switch (_page)
                {
                    case PageRating: WriteRating(box, picked, day); break;
                    case PageSettings: WriteSettings(box, picked, state, day); break;
                    default: WriteFigures(box, picked, day, now); break;
                }
                return OnlineColumns.Shops;
            }
            if (opening != null)
            {
                WriteOpen(box, opening, state);
                return OnlineColumns.Shops;
            }
            _picked = null;
            box.Title(OnlineText.T("reallife_online_app"));
            box.Line(OnlineText.T("reallife_online_about"));
            box.Line(OnlineText.Pair("reallife_online_sum_sites", OnlineText.T("reallife_online_sum_sites_value", "count", state.Shops.Count.ToString(), "most", OnlineCatalogue.ShopsMost.ToString())));
            box.Line(OnlineText.Pair("reallife_online_sum_orders", orders.ToString()));
            box.Line(OnlineText.Pair("reallife_online_sum_takings", OnlineText.Money(takings)));
            box.Line(OnlineText.Pair("reallife_online_sum_paid", OnlineText.Money(paid)));
            box.Line(OnlineText.T("reallife_online_sum_note"));
            return OnlineColumns.Shops;
        }

        // ---------------------------------------------------------------- a site: today and the week

        private void WriteFigures(InfoBox box, WebPlace place, int day, int now)
        {
            WebShop shop = place.Shop;
            DayBook today = shop.Today, week = _week;
            shop.WeekInto(week, day);
            box.Title(shop.Name);
            box.Line(OnlineText.Fill("reallife_online_fig_what", "business", place.Business, "quality", OnlineText.Quality(shop.Quality), "way", OnlineText.Way(shop.Way), "promise", OnlineText.Promise(shop.Promise)));
            if (shop.Offline)
            {
                box.Line(OnlineText.T("reallife_online_fig_offline"));
            }
            box.Line(Both("reallife_online_fig_orders", today.Orders.ToString(), week.Orders.ToString()));
            box.Line(Both("reallife_online_fig_takings", OnlineText.Money(today.Takings), OnlineText.Money(week.Takings)));
            box.Line(Both("reallife_online_fig_goods", OnlineText.Money(today.Goods), OnlineText.Money(week.Goods)));
            box.Line(Both("reallife_online_fig_delivery", OnlineText.Money(today.Delivery), OnlineText.Money(week.Delivery)));
            box.Line(Both("reallife_online_fig_refunds", OnlineText.Money(today.Refunds), OnlineText.Money(week.Refunds)));
            box.Line(Both("reallife_online_fig_profit", OnlineText.Money(today.Profit), OnlineText.Money(week.Profit)));
            box.Line(OnlineText.Fill("reallife_online_fig_queue", "waiting", shop.Count(OrderStage.Waiting).ToString(), "packed", shop.Count(OrderStage.Picked).ToString(), "out", shop.Count(OrderStage.Out).ToString(),
                "late", shop.Overdue(now).ToString(), "pickers", place.Pickers.ToString()));
            Transform buttons = box.ButtonRow();
            InfoBox.Button(buttons, InfoBox.Positive, "reallife_online_button_rating", () => Page(PageRating));
            InfoBox.Button(buttons, InfoBox.Additional, "bizman_menu_settings", () => Page(PageSettings));
            InfoBox.Button(box.ButtonRow(), InfoBox.Additional, "common_close", ClosePick);
        }

        private static string Both(string labelKey, string today, string week)
        {
            return OnlineText.Pair(labelKey, OnlineText.T("reallife_online_fig_both", "today", today, "week", week));
        }

        // ---------------------------------------------------------------- a site: its rating and what moves it

        private void WriteRating(InfoBox box, WebPlace place, int day)
        {
            WebShop shop = place.Shop;
            DayBook week = _week;
            shop.WeekInto(week, day);
            int before = shop.StarsBefore(day);
            box.Title(OnlineText.T("reallife_online_rate_title", "name", shop.Name));
            box.Line(OnlineText.Pair("reallife_online_rate_stars", before > 0
                ? OnlineText.T("reallife_online_rate_stars_value", "stars", OnlineText.Stars(shop.Stars), "was", OnlineText.Stars(before))
                : OnlineText.T("reallife_online_row_stars", "stars", OnlineText.Stars(shop.Stars))));
            box.Line(OnlineText.Fill("reallife_online_rate_week", "good", week.OnTime.ToString(), "late", week.Late.ToString(), "short", week.Short.ToString(), "lost", (week.Cancelled + week.Missing).ToString(),
                "back", week.Returned.ToString()));
            // (the site's price factor: 826 is a tenth over the going price, where an order is marked down; 1108 a twentieth under it, where it is marked up)
            box.Line(OnlineText.T(place.Site.Price < 830 ? "reallife_online_rate_dear" : place.Site.Price > 1100 ? "reallife_online_rate_cheap" : "reallife_online_rate_fair"));
            box.Line(OnlineText.Pair("reallife_online_rate_effect", OnlineText.Share(OnlineRules.StarsFactor(shop.Stars))));
            WebLine best = null, gone = null;
            for (int l = 0; l < shop.Lines.Count; l++)
            {
                WebLine line = shop.Lines[l];
                best = line.SoldWeek > 0 && (best == null || line.SoldWeek > best.SoldWeek) ? line : best;
                gone = line.MissedWeek > 0 && (gone == null || line.MissedWeek > gone.MissedWeek) ? line : gone;
            }
            box.Line(OnlineText.Pair("reallife_online_rate_best", best != null
                ? OnlineText.T("reallife_online_rate_units", "product", _director.ItemName(best.Item), "units", best.SoldWeek.ToString()) : OnlineText.T("reallife_online_rate_nothing")));
            box.Line(OnlineText.Pair("reallife_online_rate_out", gone != null
                ? OnlineText.T("reallife_online_rate_units", "product", _director.ItemName(gone.Item), "units", gone.MissedWeek.ToString()) : OnlineText.T("reallife_online_rate_nothing")));
            InfoBox.Button(box.ButtonRow(), InfoBox.Additional, "reallife_online_button_back", () => Page(PageFigures));
        }

        // ---------------------------------------------------------------- a site: its settings

        private void WriteSettings(InfoBox box, WebPlace place, OnlineState state, int day)
        {
            WebShop shop = place.Shop;
            string key = shop.Place;
            box.Title(OnlineText.T("reallife_online_set_title", "name", shop.Name));
            if (!box.HasSelector || !box.Selector(OnlineCatalogue.Ways, shop.Way, OnlineText.Way, i => _director.SetWay(key, i)))
            {
                box.Line(OnlineText.Way(shop.Way));
            }
            int ready = 0;
            for (int v = 0; v < state.Vans.Count; v++)
            {
                ready += state.Vans[v].Ready ? 1 : 0;
            }
            box.Line(shop.Way == OnlineCatalogue.OwnVans
                ? OnlineText.Fill(ready > 0 ? "reallife_online_set_vans" : "reallife_online_set_no_vans", "count", ready.ToString(), "from", OnlineCatalogue.DriveFrom.ToString("00"), "until", OnlineCatalogue.DriveUntil.ToString("00"))
                : OnlineText.T("reallife_online_set_courier", "base", OnlineText.Price(OnlineCatalogue.CourierBase), "km", OnlineText.Price(OnlineCatalogue.CourierPerKm)));
            if (!box.HasSelector || !box.Selector(2, shop.Promise, OnlineText.Promise, i => _director.SetPromise(key, i)))
            {
                box.Line(OnlineText.Promise(shop.Promise));
            }
            int setUp, hosting;
            bool inHouse;
            _director.Quote(shop.Quality, out setUp, out hosting, out inHouse);
            box.Line(OnlineText.Pair("reallife_online_set_hosting", OnlineText.Fill("reallife_online_set_hosting_value", "quality", OnlineText.Quality(shop.Quality), "amount", OnlineText.Money(hosting * 100), "day", shop.HostDue.ToString())));
            Transform first = box.ButtonRow();
            if (shop.Offline)
            {
                InfoBox.Button(first, InfoBox.Positive, "reallife_online_button_pay", () => _director.PayHosting(key));
            }
            else if (shop.Quality < OnlineCatalogue.Qualities - 1)
            {
                int next = shop.Quality + 1, nextSetUp, nextHosting;
                _director.Quote(next, out nextSetUp, out nextHosting, out inHouse);
                box.Line(OnlineText.Fill("reallife_online_set_better", "quality", OnlineText.Quality(next), "amount", OnlineText.Money(Mathf.Max(0, nextSetUp - setUp) * 100), "hosting", OnlineText.Money(nextHosting * 100)));
                InfoBox.Button(first, InfoBox.Positive, "reallife_online_button_better", () => HudConfirm.Show(
                    "reallife_online_confirm_better_title".Localize(new { quality = OnlineText.Quality(next) }),
                    "reallife_online_confirm_better_body".Localize(new { amount = OnlineText.Money(Mathf.Max(0, nextSetUp - setUp) * 100), hosting = OnlineText.Money(nextHosting * 100) }),
                    () => UiClone.Safe(() => _director.Upgrade(key, next))));
            }
            if (shop.AdEnd > 0)
            {
                box.Line(OnlineText.Fill("reallife_online_set_ads_on", "percent", shop.AdPercent.ToString(), "from", shop.AdFrom.ToString(), "to", (shop.AdEnd - 1).ToString()));
            }
            else
            {
                int cost, percent;
                _director.AdQuote(place, out cost, out percent);
                box.Line(OnlineText.Fill("reallife_online_set_ads", "amount", OnlineText.Money(cost * 100), "days", OnlineCatalogue.AdDays.ToString(), "percent", percent.ToString()));
                InfoBox.Button(first, InfoBox.Additional, "reallife_online_button_ads", () => _director.Advertise(key));
            }
            Transform second = box.ButtonRow();
            UIs uis = InstanceBehavior<UIs>.Instance;
            if (uis != null && uis.textInputUi != null)
            {
                // The game's own text box, as BizMan names a business with it.
                InfoBox.Button(second, InfoBox.Additional, "reallife_online_button_rename", () =>
                    uis.textInputUi.Show("reallife_online_rename_placeholder", "common_confirm", text => UiClone.Safe(() => _director.Rename(key, text)), null, shop.Name, false, OnlineCatalogue.NameMost));
            }
            InfoBox.Button(second, InfoBox.Negative, "reallife_online_button_shut", () => HudConfirm.Show(
                "reallife_online_confirm_shut_title".Localize(new { name = shop.Name }), "reallife_online_confirm_shut_body".Localize(new { orders = shop.Orders.Count.ToString() }),
                () => UiClone.Safe(() =>
                {
                    _picked = null;
                    _director.CloseShop(key);
                }), null, null, null, true, true));
            InfoBox.Button(box.ButtonRow(), InfoBox.Additional, "reallife_online_button_back", () => Page(PageFigures));
        }

        // ---------------------------------------------------------------- a business without a site: opening one

        private void WriteOpen(InfoBox box, BuildingRegistration reg, OnlineState state)
        {
            string key = _director.KeyOf(reg), name = _director.NameOf(reg);
            int quality = OnlineCatalogue.Quality(_quality), setUp, hosting;
            bool inHouse;
            _director.Quote(quality, out setUp, out hosting, out inHouse);
            box.Title(OnlineText.T("reallife_online_open_title", "business", name));
            if (!box.HasSelector || !box.Selector(OnlineCatalogue.Qualities, quality, OnlineText.Quality, i =>
                {
                    _quality = i;
                    Refresh();
                }))
            {
                box.Line(OnlineText.Quality(quality));
            }
            box.Line(OnlineText.Pair("reallife_online_open_setup", OnlineText.Money(setUp * 100)) + (inHouse ? " " + OnlineText.T("reallife_online_open_agency", "share", OnlineText.Share(OnlineCatalogue.AgencyDiscount)) : string.Empty));
            box.Line(OnlineText.Pair("reallife_online_open_hosting", OnlineText.T("reallife_online_open_hosting_value", "amount", OnlineText.Money(hosting * 100), "days", _director.MonthDays.ToString())));
            box.Line(OnlineText.Pair("reallife_online_open_orders", OnlineText.T("reallife_online_open_orders_value", "orders", OnlineCatalogue.OrdersADay[quality].ToString(), "most", OnlineCatalogue.OrdersMost[quality].ToString())));
            if (!box.HasSelector || !box.Selector(OnlineCatalogue.Ways, _way, OnlineText.Way, i => _way = i))
            {
                box.Line(OnlineText.Way(_way));
            }
            if (!box.HasSelector || !box.Selector(2, _promise, OnlineText.Promise, i => _promise = i))
            {
                box.Line(OnlineText.Promise(_promise));
            }
            box.Line(OnlineText.T(state.Shops.Count >= OnlineCatalogue.ShopsMost ? "reallife_online_open_full" : "reallife_online_open_note"));
            Transform buttons = box.ButtonRow();
            if (state.Shops.Count < OnlineCatalogue.ShopsMost)
            {
                InfoBox.Button(buttons, InfoBox.Positive, "reallife_online_button_open", () => HudConfirm.Show(
                    "reallife_online_confirm_open_title".Localize(new { business = name }),
                    "reallife_online_confirm_open_body".Localize(new { quality = OnlineText.Quality(quality), amount = OnlineText.Money(setUp * 100), hosting = OnlineText.Money(hosting * 100) }),
                    () => UiClone.Safe(() =>
                    {
                        if (_director.OpenShop(key, quality, _way, _promise, false) != null)
                        {
                            _picked = ShopRow + key;
                            _page = PageFigures;
                            Refresh();
                        }
                    })));
            }
            InfoBox.Button(buttons, InfoBox.Additional, "common_close", ClosePick);
        }
    }
}
