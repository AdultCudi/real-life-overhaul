using System.Collections.Generic;
using System.Text;
using RealLifeOverhaul.GameUi;
using UnityEngine;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// The Orders tab and the Catalogue tab.
    ///
    /// Orders: the queue of every site, an order to a row: its number, its site and district, its goods, what it is
    /// worth, the hour it is promised for, and where it stands (waiting to be picked, packed and waiting for a van,
    /// on its way), in the game's red once its promise has passed; and under them the returns on their way back. It
    /// opens with the soonest promise first, and filters by state and by site. An order that has reached its door is
    /// in the day's book and no longer a row. The white box adds the queue up and says who is picking.
    ///
    /// Catalogue: one site's products with stock, the three prices and the week's sales, and under them what else the
    /// place has in stock; it filters by in stock or not and by on the site or not. A product that is picked has its page: the going price, the shop's price, the online price
    /// on a selector from thirty per cent under the shop's price to thirty over, what that price does to its sales,
    /// and the button that puts it on or takes it off the site.
    /// </summary>
    public sealed partial class OnlineApp
    {
        private const string OrderRow = "ord:", ReturnRow = "ret:", LineRow = "line:", AddRow = "add:";
        private const int RowsMost = 120;           // orders the table lists
        private const int ShopPriceStep = 6;        // the selector's middle choice: the shop's own price

        // The online price on the selector, in per cent over or under the shop's own price.
        private static readonly int[] PriceSteps = { -30, -25, -20, -15, -10, -5, 0, 5, 10, 15, 20, 25, 30 };

        private readonly StringBuilder _text = new StringBuilder(96);
        private readonly List<string> _stocked = new List<string>(32);
        private string _site;       // the site whose catalogue shows

        // ---------------------------------------------------------------- the queue

        private TableSpec WriteOrders(InfoBox box, OnlineState state, int day, int now)
        {
            IReadOnlyList<WebPlace> places = _director.Places;
            int waiting = 0, packed = 0, sent = 0, late = 0, pickers = 0, vans = 0;
            List<string> sites = OnlineColumns.Orders.Filters[1].Names;   // the second filter's kinds: the sites, by name
            sites.Clear();
            for (int i = 0; i < places.Count; i++)
            {
                WebShop shop = places[i].Shop;
                pickers += places[i].Pickers;
                sites.Add(shop.Name);
                for (int o = 0; o < shop.Orders.Count; o++)
                {
                    WebOrder order = shop.Orders[o];
                    bool overdue = order.Due < now;
                    waiting += order.Stage == OrderStage.Waiting ? 1 : 0;
                    packed += order.Stage == OrderStage.Picked ? 1 : 0;
                    sent += order.Stage == OrderStage.Out ? 1 : 0;
                    late += overdue ? 1 : 0;
                    if (_rows.Count >= RowsMost)
                    {
                        continue;
                    }
                    TableRow row = Row(OrderRow + order.Id);
                    row.Pick = false;
                    row.Kind = overdue ? OnlineColumns.Late : order.Stage == OrderStage.Waiting ? OnlineColumns.Waiting : order.Stage == OrderStage.Picked ? OnlineColumns.Packed : OnlineColumns.Out;
                    row.Kind2 = i;
                    Count(row, 0, order.Id);
                    row.Set(1, shop.Name);
                    row.Set(2, OnlineText.District(order.District < state.Districts.Count ? state.Districts[order.District] : null));
                    row.Set(3, Goods(shop, order));
                    Cents(row, 4, order.Stage == OrderStage.Waiting ? order.Asked : order.Total);
                    row.Set(5, OnlineText.When(order.Due, day), order.Due);
                    row.Set(6, overdue ? OnlineText.T("reallife_online_stage_late", "stage", OnlineText.Stage(order.Stage)) : OnlineText.Stage(order.Stage), order.Due - now,
                        overdue ? TableRow.Red : order.Stage == OrderStage.Waiting ? TableRow.Yellow : TableRow.Plain);
                }
                for (int r = 0; r < shop.Returns.Count && _rows.Count < RowsMost; r++)
                {
                    WebReturn back = shop.Returns[r];
                    TableRow row = Row(ReturnRow + i + ":" + r);
                    row.Pick = false;
                    row.NoTotal = true;   // (a refund to come, not an order's worth)
                    row.Kind = OnlineColumns.ComingBack;
                    row.Kind2 = i;
                    row.Set(1, shop.Name);
                    row.Set(3, OnlineText.T("reallife_online_ord_goods", "units", back.Qty.ToString(), "product", _director.ItemName(back.Item)));
                    Cents(row, 4, back.Refund);
                    row.Set(5, FinanceText.Day(back.Day), back.Day * 24 + OnlineCatalogue.ReturnHour);
                    row.Set(6, OnlineText.T("reallife_online_kind_back"), back.Day * 24 + OnlineCatalogue.ReturnHour - now);
                }
            }
            for (int v = 0; v < state.Vans.Count; v++)
            {
                vans += state.Vans[v].Ready ? 1 : 0;
            }
            if (_rows.Count == 0)
            {
                Say(OnlineText.T(places.Count == 0 ? "reallife_online_row_no_site" : "reallife_online_row_no_orders"));
            }
            _picked = null;
            box.Title(OnlineText.T("reallife_online_tab_orders"));
            box.Section("reallife_look_sec_now");
            box.Row("reallife_online_que_late", late.ToString());
            box.Row("reallife_online_que_waiting", waiting.ToString());
            box.Row("reallife_online_que_packed", packed.ToString());
            box.Row("reallife_online_que_out", sent.ToString());
            box.Row("reallife_online_que_pickers", OnlineText.T("reallife_online_que_pickers_value", "count", pickers.ToString(), "each", OnlineCatalogue.PickPerHour.ToString()));
            box.Row("reallife_online_que_vans", vans.ToString());
            box.Note(OnlineText.Fill("reallife_online_que_note", "hours", OnlineCatalogue.WaitMost.ToString(), "share", OnlineText.Share(OnlineCatalogue.LateRefund)));
            Meta.Guide.Point(box, GuideTitle);
            return OnlineColumns.Orders;
        }

        // "2 x Cola, 1 x Crisps": what was asked for, by the game's names.
        private string Goods(WebShop shop, WebOrder order)
        {
            _text.Length = 0;
            for (int j = 0; j < order.Count; j++)
            {
                string item = order.Line[j] >= 0 && order.Line[j] < shop.Lines.Count ? shop.Lines[order.Line[j]].Item : null;
                _text.Append(j > 0 ? ", " : string.Empty).Append(order.Qty[j]).Append(" x ").Append(_director.ItemName(item));
            }
            return _text.ToString();
        }

        // ---------------------------------------------------------------- the catalogue

        private TableSpec WriteCatalogue(InfoBox box, OnlineState state, int day)
        {
            IReadOnlyList<WebPlace> places = _director.Places;
            if (places.Count == 0)
            {
                Say(OnlineText.T("reallife_online_row_no_site"));
                _picked = null;
                box.Title(OnlineText.T("reallife_online_tab_catalogue"));
                box.Line(OnlineText.T("reallife_online_cat_none"));
                Meta.Guide.Point(box, GuideTitle);
                return OnlineColumns.Catalogue;
            }
            int at = 0;
            for (int i = 0; i < places.Count; i++)
            {
                at = places[i].Shop.Place == _site ? i : at;
            }
            WebPlace place = places[at];
            WebShop shop = place.Shop;
            Site site = place.Site;
            _site = shop.Place;
            string pickedLine = Picked(_picked, LineRow), pickedAdd = Picked(_picked, AddRow);
            int line = -1, on = 0;
            for (int l = 0; l < shop.Lines.Count; l++)
            {
                WebLine w = shop.Lines[l];
                int stock = l < site.Stock.Length ? site.Stock[l] : 0;
                TableRow row = Row(LineRow + w.Item);
                row.Kind = stock > 0 ? OnlineColumns.InStock : OnlineColumns.OutOfStock;
                row.Kind2 = w.On ? OnlineColumns.Offered : OnlineColumns.NotOffered;
                row.Set(0, _director.ItemName(w.Item));
                Count(row, 1, stock, w.On && stock == 0 ? TableRow.Red : TableRow.Plain);
                Price(row, 2, _director.ShopPrice(place, w.Item));
                Price(row, 3, l < site.Ask.Length ? site.Ask[l] : 0);
                Price(row, 4, l < site.Going.Length ? site.Going[l] : 0);
                Count(row, 5, w.SoldWeek);
                Count(row, 6, w.MissedWeek, w.MissedWeek > 0 ? TableRow.Yellow : TableRow.Plain);
                row.Set(7, OnlineText.T(w.On ? "reallife_online_state_offered" : "reallife_online_state_not_offered"));
                on += w.On ? 1 : 0;
                line = w.Item == pickedLine ? l : line;
            }
            _director.Stocked(place, _stocked);
            string adding = null;
            for (int i = 0; i < _stocked.Count; i++)
            {
                if (shop.Line(_stocked[i]) != null)
                {
                    continue;
                }
                int stock = _director.StockOf(place, _stocked[i]);
                TableRow row = Row(AddRow + _stocked[i]);
                row.Kind = stock > 0 ? OnlineColumns.InStock : OnlineColumns.OutOfStock;
                row.Kind2 = OnlineColumns.NotOffered;
                row.Set(0, _director.ItemName(_stocked[i]));
                Count(row, 1, stock);
                Price(row, 2, _director.ShopPrice(place, _stocked[i]));
                row.Set(7, OnlineText.T("reallife_online_cat_not_on"));
                adding = _stocked[i] == pickedAdd ? _stocked[i] : adding;
            }
            _stocked.Clear();
            if (_rows.Count == 0)
            {
                Say(OnlineText.T("reallife_online_row_no_stock"));
            }
            if (line >= 0)
            {
                WriteLine(box, place, line);
                return OnlineColumns.Catalogue;
            }
            if (adding != null)
            {
                WriteAdd(box, place, adding);
                return OnlineColumns.Catalogue;
            }
            _picked = null;
            box.Title(OnlineText.T("reallife_online_cat_title", "name", shop.Name));
            if (places.Count > 1 && box.HasSelector)
            {
                box.Selector(places.Count, at, i => places[i].Shop.Name, i =>
                {
                    _site = places[i].Shop.Place;
                    Refresh();
                });
            }
            box.Row("reallife_online_cat_on", OnlineText.T("reallife_online_cat_on_value", "count", on.ToString(), "most", OnlineCatalogue.LinesMost.ToString()));
            box.Row("reallife_online_cat_expected", (site.PerDay / 1000f).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture));
            box.Line(OnlineText.Fill("reallife_online_cat_parts", "need", site.Need.ToString(), "price", OnlineText.Share(site.Price), "stars", OnlineText.Share(OnlineRules.StarsFactor(shop.Stars)),
                "marketing", OnlineText.Share(OnlineRules.MarketingFactor(site.Ads, site.Campaign))));
            box.Note(OnlineText.T("reallife_online_cat_note"));
            Meta.Guide.Point(box, GuideTitle);
            return OnlineColumns.Catalogue;
        }

        // A product of the site: its prices, the online price on a selector, what the price does, and taking it off or putting it back.
        private void WriteLine(InfoBox box, WebPlace place, int index)
        {
            WebShop shop = place.Shop;
            Site site = place.Site;
            WebLine line = shop.Lines[index];
            string key = shop.Place, item = line.Item;
            int own = _director.ShopPrice(place, item), going = index < site.Going.Length ? site.Going[index] : 0, ask = line.Price > 0 ? line.Price : own;
            box.Title(_director.ItemName(item));
            box.Section("reallife_look_sec_now");
            box.Row("reallife_online_line_going", OnlineText.Price(going));
            box.Row("reallife_online_line_shop", OnlineText.Price(own));
            int step = ShopPriceStep;
            if (line.Price > 0 && own > 0)
            {
                int percent = Mathf.RoundToInt(line.Price * 100f / own) - 100, nearest = int.MaxValue;
                for (int i = 0; i < PriceSteps.Length; i++)
                {
                    if (Mathf.Abs(PriceSteps[i] - percent) < nearest)
                    {
                        nearest = Mathf.Abs(PriceSteps[i] - percent);
                        step = i;
                    }
                }
            }
            if (own <= 0 || !box.HasSelector || !box.Selector(PriceSteps.Length, step,
                    i => i == ShopPriceStep
                        ? OnlineText.T("reallife_online_line_follow", "price", OnlineText.Price(own))
                        : OnlineText.T("reallife_online_line_step", "price", OnlineText.Price(StepPrice(own, i)), "percent", (PriceSteps[i] > 0 ? "+" : string.Empty) + PriceSteps[i]),
                    i => _director.SetPrice(key, item, i == ShopPriceStep ? 0 : StepPrice(own, i))))
            {
                box.Row("reallife_online_line_online", OnlineText.Price(ask));
            }
            int factor = going > 0 && ask > 0 ? OnlineRules.PriceFactor((int)((long)ask * 1000 / going)) : 0;
            box.Line(factor > 0
                ? OnlineText.T("reallife_online_line_effect", "share", OnlineText.Share(factor))
                : OnlineText.T("reallife_online_line_too_dear", "share", OnlineText.Share(OnlineCatalogue.PriceCeiling - 1000)));
            box.Row("reallife_online_line_stock", (index < site.Stock.Length ? site.Stock[index] : 0).ToString());
            box.Row("reallife_online_line_week", OnlineText.T("reallife_online_line_week_value", "sold", line.SoldWeek.ToString(), "missed", line.MissedWeek.ToString()));
            Transform buttons = box.ButtonRow();
            if (line.On)
            {
                InfoBox.Button(buttons, InfoBox.Negative, "reallife_online_button_off", () =>
                {
                    _picked = null;
                    _director.Offer(key, item, false);
                });
            }
            else
            {
                InfoBox.Button(buttons, InfoBox.Positive, "reallife_online_button_on", () => _director.Offer(key, item, true));
            }
            InfoBox.Button(buttons, InfoBox.Additional, "reallife_online_button_back", ClosePick);
            Meta.Guide.Point(box, GuideTitle);
        }

        // A cell of a price kept in cents: written with its cents, ordered by its amount.
        private static void Price(TableRow row, int column, int cents)
        {
            row.Set(column, OnlineText.Price(cents), cents / 100.0);
        }

        private static int StepPrice(int own, int step)
        {
            return Mathf.Max(1, Mathf.RoundToInt(own * (100 + PriceSteps[step]) / 100f));
        }

        // Something the place stocks that is not on the site: putting it on.
        private void WriteAdd(InfoBox box, WebPlace place, string item)
        {
            string key = place.Shop.Place;
            box.Title(_director.ItemName(item));
            box.Section("reallife_look_sec_now");
            box.Row("reallife_online_line_shop", OnlineText.Price(_director.ShopPrice(place, item)));
            box.Row("reallife_online_line_stock", _director.StockOf(place, item).ToString());
            Transform buttons = box.ButtonRow();
            InfoBox.Button(buttons, InfoBox.Positive, "reallife_online_button_on", () =>
            {
                _picked = LineRow + item;
                _director.Offer(key, item, true);
            });
            InfoBox.Button(buttons, InfoBox.Additional, "reallife_online_button_back", ClosePick);
            box.Note(OnlineText.T("reallife_online_line_add_note", "most", OnlineCatalogue.LinesMost.ToString()));
            Meta.Guide.Point(box, GuideTitle);
        }
    }
}
