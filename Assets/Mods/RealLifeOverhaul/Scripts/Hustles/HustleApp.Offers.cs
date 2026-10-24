using RealLifeOverhaul.Economy;
using RealLifeOverhaul.GameUi;
using UnityEngine;

namespace RealLifeOverhaul.Hustles
{
    /// <summary>The Offers tab: buyers' offers for the player's listed things, to take, counter or turn down, and the things sold lately.</summary>
    public sealed partial class HustleApp
    {
        public const string OfferRow = "offer:";

        private TableSpec WriteOffers(InfoBox box, HustleState state)
        {
            int pickedId = Picked(_picked, OfferRow);
            BuyerOffer picked = null;
            int now = HustleGame.Stamp;
            for (int i = 0; i < state.Offers.Count; i++)
            {
                BuyerOffer d = state.Offers[i];
                Owned o = state.Item(d.Item);
                if (o == null)
                {
                    continue;
                }
                TableRow row = Row(OfferRow + d.Id);
                row.Kind = HustleColumns.Waiting;
                row.Set(0, d.Buyer);
                row.Set(1, HustleText.Thing(o.Thing, o.Car));
                Money(row, 2, d.Offer);
                Money(row, 3, o.Asking);
                int margin = d.Offer - o.Paid - o.Spent;
                Money(row, 4, margin, margin >= 0 ? TableRow.Green : TableRow.Red);
                int left = Mathf.Max(0, d.Expires - now);
                row.Set(5, HustleText.T("reallife_hustle_hours_left", "hours", left.ToString()), left, left <= 1 ? TableRow.Red : TableRow.Plain);
                row.Set(6, HustleText.T("reallife_hustle_state_waiting"), TableRow.Yellow);
                picked = d.Id == pickedId ? d : picked;
            }
            for (int i = 0; i < state.Sales.Count; i++)
            {
                Sale s = state.Sales[i];
                TableRow row = Row("sale:" + i);
                row.Pick = false;
                row.Kind = HustleColumns.Sold;
                row.NoTotal = true;
                row.Set(0, s.Buyer);
                row.Set(1, HustleText.Thing(s.Thing, s.Car));
                Money(row, 2, s.Price);
                Money(row, 3, s.Cost);
                Money(row, 4, s.Price - s.Cost, s.Price >= s.Cost ? TableRow.Green : TableRow.Red);
                row.Set(5, FinanceText.Day(s.Day), s.Day);
                row.Set(6, HustleText.T("reallife_hustle_state_sold"), TableRow.Green);
            }
            if (picked != null)
            {
                WriteOffer(box, state, picked, now);
                return HustleColumns.Offers;
            }
            _picked = null;
            box.Title(HustleText.T("reallife_hustle_offers_title"));
            box.Section("reallife_ibox_sec_now");
            box.Row("reallife_hustle_row_waiting", state.Waiting.ToString(), state.Waiting > 0 ? CellTone.Yellow : CellTone.Plain);
            box.Row("reallife_hustle_row_profit", HustleText.Money(state.Profit), state.Profit >= 0 ? CellTone.Green : CellTone.Red);
            box.Note(HustleText.T("reallife_hustle_offers_about"));
            Meta.Guide.Point(box, GuideTitle);
            return HustleColumns.Offers;
        }

        private void WriteOffer(InfoBox box, HustleState state, BuyerOffer d, int now)
        {
            Owned o = state.Item(d.Item);
            int id = d.Id;
            box.Title(d.Buyer);
            box.Subtitle(HustleText.Thing(o.Thing, o.Car));
            box.Section("reallife_ibox_sec_money");
            box.Row("reallife_hustle_row_offer", HustleText.Money(d.Offer));
            box.Row("reallife_hustle_row_asking", HustleText.Money(o.Asking));
            int margin = d.Offer - o.Paid - o.Spent;
            box.Row("reallife_hustle_row_margin", HustleText.Money(margin), margin >= 0 ? CellTone.Green : CellTone.Red);
            box.Line(HustleText.T("reallife_hustle_offer_lapses", "hours", Mathf.Max(0, d.Expires - now).ToString(), "left",
                (HustleCatalogue.CountersMost - d.Counters).ToString()));
            if (o.Car)
            {
                box.Note(HustleText.T("reallife_hustle_offer_car"));
            }
            int steps = HustleRules.CounterSteps(d, o.Asking);
            int step = Choice(steps - 1);
            int counter = HustleRules.CounterAt(d, o.Asking, step);
            Choose(box, steps, step, i => HustleText.T("reallife_hustle_counter_at", "price", HustleText.Money(HustleRules.CounterAt(d, o.Asking, i))), SetChoice);
            Transform buttons = box.ButtonRow();
            InfoBox.Button(buttons, InfoBox.Positive, "reallife_hustle_button_accept", () => Confirm("reallife_hustle_q_sell_title",
                HustleText.T("reallife_hustle_q_sell", "thing", HustleText.Thing(o.Thing, o.Car), "price", HustleText.Money(d.Offer), "who", d.Buyer), () =>
                {
                    if (_director.Accept(id))
                    {
                        Pick(null);
                    }
                    Refresh();
                }, false));
            InfoBox.Button(buttons, InfoBox.Negative, "reallife_hustle_button_counter", () =>
            {
                bool taken = _director.Counter(id, counter, out bool gone);
                if (taken || gone)
                {
                    Pick(null);
                }
                Refresh();
            });
            Transform more = box.ButtonRow();
            InfoBox.Button(more, InfoBox.Negative, "reallife_hustle_button_decline", () =>
            {
                _director.Decline(id);
                Pick(null);
                Refresh();
            });
            InfoBox.Button(more, InfoBox.Additional, "common_close", ClosePick);
            Meta.Guide.Point(box, GuideTitle);
        }
    }
}
