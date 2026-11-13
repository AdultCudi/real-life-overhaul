using System;
using System.Collections.Generic;
using System.Globalization;
using Localizor;
using RealLifeOverhaul.GameUi;
using RealLifeOverhaul.Ui;
using UI;
using UnityEngine;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// The Web shop app, part: what a site keeps beyond its way, promise and quality, the same controls the RLO interface offers: the delivery fee and the order that rides free, packing time,
    /// night orders, matching the rival, showing stock, the districts served, a picked order's refund and courier, a product's title and description, and the page of the shop as a customer
    /// sees it with the answers to its reviews.
    /// </summary>
    public sealed partial class OnlineApp
    {
        private const int PageStore = 4;
        private const int FeeStep = 50, FeeMost = 5000;   // cents

        // Free delivery over: never, then five by five up to a hundred, fifty by fifty up to a thousand, then by a hundred to ten thousand (the director's own ceiling).
        private static readonly int[] FreeSteps = BuildFreeSteps();

        private int _review;   // the review the customer page's button answers

        private static int[] BuildFreeSteps()
        {
            var steps = new List<int> { 0 };
            for (int c = 500; c <= 10000; c += 500)
            {
                steps.Add(c);
            }
            for (int c = 15000; c <= 100000; c += 5000)
            {
                steps.Add(c);
            }
            for (int c = 200000; c <= 1000000; c += 100000)
            {
                steps.Add(c);
            }
            return steps.ToArray();
        }

        private static int Nearest(int[] steps, int value)
        {
            int at = 0;
            for (int i = 1; i < steps.Length; i++)
            {
                at = Math.Abs(steps[i] - value) < Math.Abs(steps[at] - value) ? i : at;
            }
            return at;
        }

        // The settings customers see, as the RLO interface lists them; each control calls the director method the kit's button calls.
        private void WriteExtras(InfoBox box, WebPlace place, OnlineState state)
        {
            string key = place.Shop.Place;
            ShopExtra x = _director.Extra != null ? _director.Extra.Of(key, true) : null;
            if (x == null)
            {
                return;
            }
            box.SectionWords(OnlineText.T("reallife_ui_shop_settings"));
            int fee = x.FeeCents, free = x.FreeOverCents;
            if (!box.Slider("reallife_ui_shop_fee", FeeMost / FeeStep, fee / FeeStep, i => i == 0 ? OnlineText.T("reallife_ui_shop_free") : OnlineText.Price(i * FeeStep),
                    i => _director.SetFee(key, i * FeeStep)))
            {
                box.Row("reallife_ui_shop_fee", fee > 0 ? OnlineText.Price(fee) : OnlineText.T("reallife_ui_shop_free"));
            }
            if (!box.Slider("reallife_ui_shop_free_over", FreeSteps.Length - 1, Nearest(FreeSteps, free), i => i == 0 ? OnlineText.T("reallife_ui_shop_never") : OnlineText.Price(FreeSteps[i]),
                    i => _director.SetFreeOver(key, FreeSteps[i])))
            {
                box.Row("reallife_ui_shop_free_over", free > 0 ? OnlineText.Price(free) : OnlineText.T("reallife_ui_shop_never"));
            }
            string[] packing = { "reallife_ui_shop_pack_30", "reallife_ui_shop_pack_60", "reallife_ui_shop_pack_day" };
            if (!box.HasSelector || !box.Selector(packing.Length, x.Packing, i => OnlineText.T("reallife_ui_shop_packing") + ": " + OnlineText.T(packing[i]), i =>
                {
                    _director.SetPacking(key, i);
                    Refresh();   // a packing time under a day makes the promise same day, which the chooser above shows
                }))
            {
                box.Line(OnlineText.T("reallife_ui_shop_packing") + ": " + OnlineText.T(packing[Math.Max(0, Math.Min(2, x.Packing))]));
            }
            Switch(box, "reallife_ui_shop_night", x.Night, () => _director.Toggle(key, 0));
            Switch(box, "reallife_ui_shop_match", x.Match, () => _director.Toggle(key, 1));
            Switch(box, "reallife_ui_shop_stock", x.ShowStock, () => _director.Toggle(key, 2));
            box.Section("reallife_ui_shop_area");
            for (int k = 0; k < Math.Min(8, state.Districts.Count); k++)
            {
                int district = k;
                string name = OnlineText.District(state.Districts[k]);
                if (!box.ToggleWords(name, null, x.Serves(k), on => _director.ToggleDistrict(key, district)))
                {
                    box.Check(name, x.Serves(k));
                }
            }
        }

        private static void Switch(InfoBox box, string labelKey, bool on, Action flip)
        {
            Action<bool> set = value =>
            {
                if (value != on)
                {
                    on = value;
                    flip();
                }
            };
            if (!box.Toggle(labelKey, null, on, set) && !box.Switch(labelKey, on, set))
            {
                box.Check(OnlineText.T(labelKey), on);
            }
        }

        // An open order picked on the Orders tab: where it stands and the two things an owner can do with it now.
        private void WriteOrder(InfoBox box, WebPlace place, WebOrder order, OnlineState state, int day)
        {
            WebShop shop = place.Shop;
            string key = shop.Place;
            int id = order.Id;
            box.Title(OnlineText.T("reallife_ui_shop_order_n", "id", id.ToString(CultureInfo.InvariantCulture)));
            box.Subtitle(shop.Name);
            box.Section("reallife_ibox_sec_now");
            box.Row("reallife_ui_shop_col_district", OnlineText.District(order.District < state.Districts.Count ? state.Districts[order.District] : null));
            box.Row("reallife_ui_shop_col_stage", OnlineText.Stage(order.Stage));
            box.Row("reallife_ui_shop_col_total", OnlineText.Price(order.Stage == OrderStage.Waiting ? order.Asked : order.Total));
            box.Line(OnlineText.When(order.Due, day), CellTone.Plain);
            box.Note(Goods(shop, order));
            Transform buttons = box.ButtonRow();
            InfoBox.Button(buttons, InfoBox.Negative, "reallife_ui_shop_refund", () => GameConfirm.Show("reallife_ui_shop_refund_title".Localize(),
                "reallife_ui_shop_refund_body".Localize(new { id = id.ToString(CultureInfo.InvariantCulture) }), () => UiClone.Safe(() =>
                {
                    _director.RefundOrder(key, id);
                    _picked = null;
                    Refresh();
                }), null, null, null, true, true));
            if (order.Stage == OrderStage.Picked)
            {
                InfoBox.Button(buttons, InfoBox.Positive, "reallife_ui_shop_send", () => GameConfirm.Show("reallife_ui_shop_send_title".Localize(),
                    "reallife_ui_shop_send_body".Localize(new { id = id.ToString(CultureInfo.InvariantCulture) }), () => UiClone.Safe(() =>
                    {
                        _director.SendCourier(key, id);
                        Refresh();
                    }), null, null, null, true, false));
            }
            InfoBox.Button(buttons, InfoBox.Additional, "reallife_online_button_back", ClosePick);
            box.Note(OnlineText.T("reallife_ui_shop_order_pick"));
            Meta.Guide.Point(box, GuideTitle);
        }

        // The name and the description a product carries, and the buttons that set them (the game's own text box, empty for the game's words).
        private void WriteTitles(InfoBox box, WebPlace place, string item)
        {
            string key = place.Shop.Place;
            ShopExtra x = _director.Extra != null ? _director.Extra.Of(key, true) : null;
            LineExtra le = x != null ? x.LineOf(item, false) : null;
            string title = le != null ? le.Title : string.Empty, desc = le != null ? le.Desc : string.Empty;
            if (!string.IsNullOrEmpty(title))
            {
                box.Row("reallife_ui_shop_col_product", title);
            }
            if (!string.IsNullOrEmpty(desc))
            {
                box.Note(desc);
            }
            if (x == null)
            {
                return;
            }
            Transform buttons = box.ButtonRow();
            InfoBox.Button(buttons, InfoBox.Additional, "reallife_ui_shop_edit_title",
                () => Ask("reallife_ui_shop_title_placeholder", title, OnlineCatalogue.NameMost, text => _director.SetTitle(key, item, text)));
            InfoBox.Button(buttons, InfoBox.Additional, "reallife_ui_shop_edit_desc", () => Ask("reallife_ui_shop_desc_placeholder", desc, 200, text => _director.SetDescription(key, item, text)));
        }

        private void Ask(string placeholder, string initial, int most, Action<string> done)
        {
            GameUi.TextBox.Ask(placeholder, "common_confirm", text => UiClone.Safe(() =>
            {
                done(text);
                Refresh();
            }), null, initial ?? string.Empty, true, most);
        }

        // The shop as customers see it, read from the same facts the RLO interface draws it from: the promise and fee, each product with its price and rating, the reviews to answer, the latest
        // basket and the sellers beside this one.
        private void WriteStore(InfoBox box, WebPlace place)
        {
            WebShop shop = place.Shop;
            ShopSiteFact s = ShopLive.Site(shop.Place);
            string brand = OnlineText.T("reallife_ui_shop_brand");
            box.Title(OnlineText.T("reallife_ui_shop_store_line", "brand", brand, "name", shop.Name));
            if (s == null)
            {
                InfoBox.Button(box.ButtonRow(), InfoBox.Additional, "reallife_online_button_back", () => Page(PageFigures));
                return;
            }
            box.Chip(Stars(s.Halves, s.Reviews), CellTone.Plain);
            box.Section("reallife_ui_shop_delivery_sec");
            if (s.Products.Length > 0)
            {
                box.Row("reallife_ui_shop_arrives_label", s.Products[0].Arrives);
            }
            box.Row("reallife_ui_shop_fee", s.FeeCents > 0 ? OnlineText.Price(s.FeeCents) : OnlineText.T("reallife_ui_shop_free"));
            box.Row("reallife_ui_shop_free_over", s.FreeOverCents > 0 ? OnlineText.Price(s.FreeOverCents) : OnlineText.T("reallife_ui_shop_never"));
            box.Section("reallife_ui_shop_col_product");
            var reviews = new List<ShopReviewFact>(8);
            for (int i = 0; i < s.Products.Length; i++)
            {
                ShopProductFact p = s.Products[i];
                if (!p.On)
                {
                    continue;
                }
                string price = OnlineText.Price((int)p.Cents);
                if (p.WasCents > p.Cents)
                {
                    price += " (" + OnlineText.T("reallife_ui_shop_was", "price", OnlineText.Price((int)p.WasCents)) + ")";
                }
                string detail = Stars(p.Halves, p.Count) + (p.OnlyLeft ? ", " + OnlineText.T("reallife_ui_shop_only_left", "n", p.Stock.ToString(CultureInfo.InvariantCulture)) : string.Empty)
                    + (p.Express ? ", " + OnlineText.T("reallife_ui_shop_express") : string.Empty);
                box.Entry(string.IsNullOrEmpty(p.Title) ? p.Name : p.Title, price, detail, p.OnlyLeft ? CellTone.Yellow : CellTone.Plain);
                for (int r = 0; r < p.Reviews.Length && reviews.Count < 8; r++)
                {
                    reviews.Add(p.Reviews[r]);
                }
            }
            box.Section("reallife_ui_shop_reviews_sec");
            if (reviews.Count == 0)
            {
                box.Line(OnlineText.T("reallife_ui_shop_no_reviews"));
            }
            for (int i = 0; i < reviews.Count; i++)
            {
                ShopReviewFact r = reviews[i];
                box.Entry(r.Name + ", " + OnlineText.T("reallife_ui_shop_n_stars", "n", r.Stars.ToString(CultureInfo.InvariantCulture)), FinanceText.Day(r.Day),
                    string.IsNullOrEmpty(r.Reply) ? r.District : OnlineText.T("reallife_ui_shop_you_answered", "text", r.Reply));
            }
            if (reviews.Count > 0)
            {
                _review = Math.Max(0, Math.Min(reviews.Count - 1, _review));
                if (reviews.Count > 1 && box.HasSelector)
                {
                    box.Selector(reviews.Count, _review, i => reviews[i].Name + ", " + OnlineText.T("reallife_ui_shop_n_stars", "n", reviews[i].Stars.ToString(CultureInfo.InvariantCulture)),
                        i => _review = i);
                }
                ShopReviewFact answer = reviews[_review];
                InfoBox.Button(box.ButtonRow(), InfoBox.Additional, "reallife_ui_shop_reply_button", () => Ask("reallife_ui_shop_reply_placeholder", answer.Reply, 160,
                    text => _director.Reply(shop.Place, answer.Index, text)));
                box.Note(OnlineText.T("reallife_ui_shop_reviews_how"));
            }
            if (s.Basket != null)
            {
                box.SectionWords(OnlineText.T("reallife_ui_shop_basket_of", "customer", s.Basket.Customer));
                foreach (ShopBasketLine line in s.Basket.Lines)
                {
                    box.Entry(line.Name, OnlineText.Price((int)(line.Cents * line.Qty)), line.Qty + " x " + OnlineText.Price((int)line.Cents));
                }
                box.Row("reallife_ui_shop_subtotal", OnlineText.Price((int)s.Basket.Subtotal));
                box.Row("reallife_ui_shop_fee", s.Basket.Fee > 0 ? OnlineText.Price((int)s.Basket.Fee) : OnlineText.T("reallife_ui_shop_free"));
                box.Row("reallife_ui_shop_total", OnlineText.Price((int)s.Basket.Total), CellTone.Plain);
                if (s.Basket.ToFree > 0)
                {
                    box.Note(OnlineText.T("reallife_ui_shop_to_free", "amount", OnlineText.Price((int)s.Basket.ToFree)));
                }
            }
            else
            {
                box.Note(OnlineText.T("reallife_ui_shop_basket_none"));
            }
            box.Section("reallife_ui_shop_col_seller");
            if (s.Rivals.Length == 0)
            {
                box.Line(OnlineText.T("reallife_ui_shop_no_rivals"));
            }
            foreach (ShopRivalFact r in s.Rivals)
            {
                box.Entry(r.Name, Stars(r.Halves, r.Reviews), OnlineText.T("reallife_ui_shop_n_items", "count", r.Lines.Length.ToString(CultureInfo.InvariantCulture)));
            }
            InfoBox.Button(box.ButtonRow(), InfoBox.Additional, "reallife_online_button_back", () => Page(PageFigures));
            Meta.Guide.Point(box, GuideTitle);
        }

        private static string Stars(int halves, int count)
        {
            return halves < 0 ? OnlineText.T("reallife_ui_shop_no_reviews")
                : OnlineText.T("reallife_ui_shop_rated", "stars", (halves / 2f).ToString("0.0", CultureInfo.InvariantCulture), "count", count.ToString(CultureInfo.InvariantCulture));
        }
    }
}
