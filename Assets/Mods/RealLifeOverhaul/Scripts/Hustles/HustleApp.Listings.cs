using System.Globalization;
using RealLifeOverhaul.Economy.Dealership;
using RealLifeOverhaul.GameUi;
using UnityEngine;

namespace RealLifeOverhaul.Hustles
{
    /// <summary>The Listings tab: what the city's people sell today, to haggle over and buy, collected or brought home.</summary>
    public sealed partial class HustleApp
    {
        public const string ListingRow = "list:";

        private TableSpec WriteListings(InfoBox box, HustleState state)
        {
            int pickedId = Picked(_picked, ListingRow);
            Listing picked = null;
            int level = HustleGame.Level;
            for (int i = 0; i < state.Listings.Count; i++)
            {
                Listing l = state.Listings[i];
                TableRow row = Row(ListingRow + l.Id);
                ThingDef def = l.Car ? null : HustleCatalogue.Thing(l.Thing);
                row.Kind = l.Car ? (int)ThingKind.Car : def != null ? (int)def.Kind : 0;
                row.Pick = !l.Gone;
                row.Set(0, HustleText.Thing(l.Thing, l.Car));
                row.Set(1, HustleText.Kind(l.Thing, l.Car));
                row.Set(2, HustleText.Condition(l.Shown), l.Shown);
                Money(row, 3, l.Countered > 0 ? l.Countered : l.Asking);
                Money(row, 4, HustleRules.SaidWorth(l, level));
                row.Set(5, l.Seller);
                row.Set(6, Where(l.Street, l.Number));
                row.Set(7, l.Gone ? HustleText.T("reallife_hustle_state_gone") : l.Countered > 0 ? HustleText.T("reallife_hustle_state_countered") : HustleText.T("reallife_hustle_state_for_sale"),
                    l.Gone ? TableRow.Red : l.Countered > 0 ? TableRow.Yellow : TableRow.Plain);
                picked = l.Id == pickedId && !l.Gone ? l : picked;
            }
            if (picked != null)
            {
                WriteListing(box, picked, level);
                return HustleColumns.Listings;
            }
            _picked = null;
            box.Title(HustleText.T("reallife_hustle_listings_title"));
            box.Section("reallife_ibox_sec_now");
            box.Row("reallife_hustle_cash", HustleText.Money(HustleGame.Cash));
            box.Row("reallife_hustle_row_negotiation", HustleDirector.Negotiation.ToString(CultureInfo.InvariantCulture));
            box.Note(HustleText.T("reallife_hustle_listings_about"));
            Meta.Guide.Point(box, GuideTitle);
            return HustleColumns.Listings;
        }

        private void WriteListing(InfoBox box, Listing l, int level)
        {
            box.Title(HustleText.Thing(l.Thing, l.Car));
            box.Subtitle(HustleText.T("reallife_hustle_listing_seller", "seller", l.Seller, "place", Where(l.Street, l.Number)));
            if (l.Car)
            {
                box.Subtitle(HustleGame.ColourName(l.Colour));
            }
            box.Chip(HustleText.T("reallife_hustle_chip_said", "condition", HustleText.Condition(l.Shown)), CellTone.Plain);
            box.Section("reallife_ibox_sec_money");
            int price = l.Countered > 0 ? l.Countered : l.Asking;
            box.Line(HustleText.T("reallife_hustle_listing_money", "asking", HustleText.Money(l.Asking), "counter",
                l.Countered > 0 ? HustleText.Money(l.Countered) : HustleText.T("reallife_hustle_none"), "left", (HustleCatalogue.HagglesMost - l.Haggles).ToString(CultureInfo.InvariantCulture)));
            box.Row("reallife_hustle_row_worth_said", HustleText.Money(HustleRules.SaidWorth(l, level)));
            if (l.Car)
            {
                box.Line(HustleText.T("reallife_hustle_listing_car", "km", l.Km.ToString("N0", CultureInfo.CurrentCulture)));
                _deliver = false;
            }
            else
            {
                int fee = HustleRules.DeliveryFee(price, level);
                Choose(box, 2, _deliver ? 1 : 0, i => i == 0 ? HustleText.T("reallife_hustle_get_collect") : HustleText.T("reallife_hustle_get_deliver", "fee", HustleText.Money(fee)),
                    i => _deliver = i == 1);
            }
            box.Note(HustleText.T(l.Car ? "reallife_hustle_listing_car_note" : "reallife_hustle_listing_note"));
            int step = Choice(HustleRules.HaggleSteps - 5);
            int offer = HustleRules.HaggleAt(l.Asking, step);
            Choose(box, HustleRules.HaggleSteps, step, i => HustleText.T("reallife_hustle_offer_at", "price", HustleText.Money(HustleRules.HaggleAt(l.Asking, i))), SetChoice);
            int id = l.Id;
            bool deliver = _deliver && !l.Car;
            Transform buttons = box.ButtonRow();
            InfoBox.Button(buttons, InfoBox.Positive, "reallife_hustle_button_offer", () =>
            {
                HaggleAnswer a = _director.Haggle(id, offer, deliver, out int counter);
                if (a != HaggleAnswer.Countered)
                {
                    Pick(null);
                }
                Refresh();
            });
            InfoBox.Button(buttons, InfoBox.Negative, "reallife_hustle_button_buy", () => Confirm("reallife_hustle_q_buy_title",
                HustleText.T(deliver ? "reallife_hustle_q_buy_deliver" : "reallife_hustle_q_buy", "thing", HustleText.Thing(l.Thing, l.Car), "price", HustleText.Money(price),
                    "place", Where(l.Street, l.Number), "fee", HustleText.Money(HustleRules.DeliveryFee(price, level))), () =>
                {
                    if (_director.Buy(id, price, deliver))
                    {
                        Pick(null);
                    }
                    Refresh();
                }, false));
            Transform more = box.ButtonRow();
            InfoBox.Button(more, InfoBox.Positive, "reallife_hustle_button_way", () => _director.ShowWay(l.Street, l.Number));
            InfoBox.Button(more, InfoBox.Additional, "common_close", ClosePick);
            Meta.Guide.Point(box, GuideTitle);
        }
    }
}
