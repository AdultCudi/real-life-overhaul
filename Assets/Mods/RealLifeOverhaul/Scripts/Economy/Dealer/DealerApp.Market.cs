using RealLifeOverhaul.GameUi;
using UnityEngine;

namespace RealLifeOverhaul.Economy.Dealership
{
    /// <summary>
    /// The Market tab: the day's new cars from the wholesaler, used cars offered by their owners and the lots of the trade
    /// auction, filtered by kind. A new car is bought at its price; a used one can be looked over first (its faults and its
    /// true condition are then known, and the owner's least comes down by what is found) and haggled over; a lot of the
    /// auction can be looked over and bid on, a step at a time, against the other dealers of the room until it is knocked
    /// down. A car bought goes onto the lot of the dealership chosen, which needs a free place.
    /// </summary>
    public sealed partial class DealerApp
    {
        private const string ListRow = "list:";

        private TableSpec WriteMarket(InfoBox box, DealerState state, int now)
        {
            int pickedId = Picked(_picked, ListRow);
            DealerListing picked = null;
            int news = 0, used = 0, lots = 0;
            for (int i = 0; i < state.Market.Count; i++)
            {
                DealerListing l = state.Market[i];
                if (l.Gone && l.Kind != ListingKind.Auction)
                {
                    continue;
                }
                TableRow row = Row(ListRow + l.Id);
                row.Kind = (int)l.Kind;
                row.Set(0, DealerGame.CarName(l.Type));
                row.Set(1, DealerText.Kind(l.Kind));
                row.Set(2, DealerText.Km(l.Km), l.Km);
                int shown = l.Inspected ? l.Condition : l.Shown;
                row.Set(3, l.Inspected ? DealerText.Condition(shown) : DealerText.T("reallife_dealer_said", "condition", DealerText.Condition(shown)), shown);
                Money(row, 4, l.Kind == ListingKind.Used && l.Countered > 0 ? l.Countered : l.Asking);
                row.Set(5, DealerGame.ColourName(l.Colour));
                if (l.Kind == ListingKind.Auction)
                {
                    int left = Mathf.Max(0, l.Ends - now);
                    row.Set(6, l.Gone ? DealerText.T("reallife_dealer_ended") : DealerText.Hours(left), l.Gone ? 0 : left);
                    row.Set(7, AuctionState(l), l.Leader == 0 ? TableRow.Green : l.Leader > 0 ? TableRow.Yellow : TableRow.Plain);
                    lots++;
                }
                else
                {
                    row.Set(6, DealerText.T("reallife_dealer_today"));
                    row.Set(7, l.Kind == ListingKind.Used ? (l.Inspected ? DealerText.T("reallife_dealer_state_inspected") : DealerText.T("reallife_dealer_state_not_inspected")) : DealerText.T("reallife_dealer_state_new"));
                    news += l.Kind == ListingKind.Wholesale ? 1 : 0;
                    used += l.Kind == ListingKind.Used ? 1 : 0;
                }
                picked = l.Id == pickedId ? l : picked;
            }
            if (picked != null)
            {
                switch (picked.Kind)
                {
                    case ListingKind.Used: WriteUsed(box, state, picked); break;
                    case ListingKind.Auction: WriteLot(box, state, picked, now); break;
                    default: WriteNew(box, state, picked); break;
                }
                return DealerColumns.Market;
            }
            _picked = null;
            box.Title(DealerText.T("reallife_dealer_market_title"));
            box.Line(DealerText.T("reallife_dealer_market_about"));
            box.Line(DealerText.Fill("reallife_dealer_market_sum", "new", news.ToString(), "used", used.ToString(), "lots", lots.ToString()));
            box.Line(DealerText.Pair("reallife_dealer_cash", DealerText.Money(DealerGame.Cash)));
            if (state.NoBidsUntil >= SaveGameManager.Current.Day)
            {
                box.Line(DealerText.T("reallife_dealer_market_no_bids", "day", FinanceText.Day(state.NoBidsUntil + 1)));
            }
            return DealerColumns.Market;
        }

        private static string AuctionState(DealerListing l)
        {
            if (l.Gone)
            {
                return l.Leader == 0 ? DealerText.T("reallife_dealer_state_won") : DealerText.T("reallife_dealer_state_sold_on");
            }
            if (l.Leader == 0)
            {
                return DealerText.T("reallife_dealer_state_leading");
            }
            return l.Leader > 0 ? DealerText.T("reallife_dealer_state_rival", "rival", DealerCatalogue.RivalNames[(l.Leader - 1) % DealerCatalogue.RivalNames.Length])
                : DealerText.T("reallife_dealer_state_no_bid");
        }

        private void WriteNew(InfoBox box, DealerState state, DealerListing l)
        {
            int worth = _director.Worth(l);
            box.Title(DealerGame.CarName(l.Type) + "  " + DealerGame.ColourName(l.Colour));
            box.Line(DealerText.T("reallife_dealer_new_what", "km", DealerText.Km(l.Km)));
            box.Line(DealerText.Fill("reallife_dealer_new_money", "price", DealerText.Money(l.Asking), "worth", DealerText.Money(worth), "asking", DealerText.Money(DealerRules.Asking(worth))));
            string yard = ChooseYard(box, state);
            Transform buttons = box.ButtonRow();
            if (yard != null)
            {
                InfoBox.Button(buttons, InfoBox.Positive, "reallife_dealer_button_buy", () => Confirm("reallife_dealer_q_buy_title",
                    DealerText.T("reallife_dealer_q_buy", "car", DealerGame.CarName(l.Type), "price", DealerText.Money(l.Asking)), () =>
                    {
                        if (_director.Buy(l.Id, yard, l.Asking))
                        {
                            Pick(null);
                        }
                    }, false));
            }
            else
            {
                box.Line(DealerText.T(state.Yards.Count == 0 ? "reallife_dealer_refuse_off" : "reallife_dealer_no_room"));
            }
            InfoBox.Button(buttons, InfoBox.Additional, "common_close", ClosePick);
        }

        private void WriteUsed(InfoBox box, DealerState state, DealerListing l)
        {
            box.Title(DealerGame.CarName(l.Type) + "  " + DealerGame.ColourName(l.Colour));
            if (l.Inspected)
            {
                int worth = _director.Worth(l);
                box.Line(DealerText.Fill("reallife_dealer_used_inspected", "km", DealerText.Km(l.Km), "condition", DealerText.Condition(l.Condition), "faults", DealerText.Faults(l.Faults)));
                box.Line(DealerText.Pair("reallife_dealer_used_worth", DealerText.Money(worth)));
            }
            else
            {
                box.Line(DealerText.Fill("reallife_dealer_used_said", "km", DealerText.Km(l.Km), "condition", DealerText.Condition(l.Shown)));
            }
            box.Line(DealerText.Fill("reallife_dealer_used_asking", "price", DealerText.Money(l.Asking), "counter", l.Countered > 0 ? DealerText.Money(l.Countered) : DealerText.T("reallife_dealer_none"),
                "left", (DealerCatalogue.HagglesMost - l.Haggles).ToString()));
            string yard = ChooseYard(box, state);
            int step = Choice(DealerRules.HaggleSteps - 3);
            int offer = DealerRules.HaggleAt(l.Asking, step);
            Choose(box, DealerRules.HaggleSteps, step, i => DealerText.T("reallife_dealer_used_offer", "price", DealerText.Money(DealerRules.HaggleAt(l.Asking, i))), SetChoice);
            Transform buttons = box.ButtonRow();
            if (yard != null)
            {
                InfoBox.Button(buttons, InfoBox.Positive, "reallife_dealer_button_offer", () =>
                {
                    HaggleAnswer a = _director.Haggle(l.Id, yard, offer, out int counter);
                    if (a == HaggleAnswer.Taken)
                    {
                        Pick(null);
                    }
                    else if (a == HaggleAnswer.WalkedAway)
                    {
                        UI.Notification.Notifications.Show(UI.Notification.NotificationType.Info, "reallife_dealer_note_owner_gone", null, 7f);
                        Pick(null);
                    }
                    Refresh();
                });
                int price = l.Countered > 0 ? l.Countered : l.Asking;
                InfoBox.Button(buttons, InfoBox.Negative, "reallife_dealer_button_buy", () => Confirm("reallife_dealer_q_buy_title",
                    DealerText.T("reallife_dealer_q_buy", "car", DealerGame.CarName(l.Type), "price", DealerText.Money(price)), () =>
                    {
                        if (_director.Buy(l.Id, yard, price))
                        {
                            Pick(null);
                        }
                    }, false));
            }
            else
            {
                box.Line(DealerText.T(state.Yards.Count == 0 ? "reallife_dealer_refuse_off" : "reallife_dealer_no_room"));
            }
            Transform more = box.ButtonRow();
            if (!l.Inspected)
            {
                int fee = _director.InspectionFee(l);
                InfoBox.Button(more, InfoBox.Positive, "reallife_dealer_button_inspect", () => Confirm("reallife_dealer_q_inspect_title",
                    DealerText.T("reallife_dealer_q_inspect", "car", DealerGame.CarName(l.Type), "fee", DealerText.Money(fee)), () => _director.Inspect(l.Id, yard), false));
            }
            InfoBox.Button(more, InfoBox.Additional, "common_close", ClosePick);
        }

        private void WriteLot(InfoBox box, DealerState state, DealerListing l, int now)
        {
            int worth = _director.Worth(l);
            box.Title(DealerGame.CarName(l.Type) + "  " + DealerGame.ColourName(l.Colour));
            box.Line(l.Inspected
                ? DealerText.Fill("reallife_dealer_used_inspected", "km", DealerText.Km(l.Km), "condition", DealerText.Condition(l.Condition), "faults", DealerText.Faults(l.Faults))
                : DealerText.Fill("reallife_dealer_lot_catalogue", "km", DealerText.Km(l.Km), "condition", DealerText.Condition(l.Shown)));
            box.Line(DealerText.Fill("reallife_dealer_lot_bid", "bid", DealerText.Money(l.Asking), "state", AuctionState(l), "bids", l.Bids.ToString()));
            if (l.Gone)
            {
                box.Line(DealerText.T("reallife_dealer_lot_over"));
                InfoBox.Button(box.ButtonRow(), InfoBox.Additional, "common_close", ClosePick);
                return;
            }
            int next = DealerRules.NextBid(l, worth), fee = DealerRules.BuyersFee(next, Prices.At(DealerCatalogue.BuyersFeeLeast));
            box.Line(DealerText.Fill("reallife_dealer_lot_next", "bid", DealerText.Money(next), "fee", DealerText.Money(fee), "ends", DealerText.When(l.Ends), "left", DealerText.Hours(Mathf.Max(0, l.Ends - now))));
            box.Line(DealerText.T("reallife_dealer_lot_note"));
            Transform buttons = box.ButtonRow();
            if (l.Leader != 0)
            {
                InfoBox.Button(buttons, InfoBox.Positive, "reallife_dealer_button_bid", () => _director.Bid(l.Id));
            }
            if (!l.Inspected)
            {
                int inspection = _director.InspectionFee(l);
                InfoBox.Button(buttons, InfoBox.Negative, "reallife_dealer_button_inspect", () => Confirm("reallife_dealer_q_inspect_title",
                    DealerText.T("reallife_dealer_q_inspect", "car", DealerGame.CarName(l.Type), "fee", DealerText.Money(inspection)), () => _director.Inspect(l.Id, null), false));
            }
            InfoBox.Button(buttons, InfoBox.Additional, "common_close", ClosePick);
        }
    }
}
