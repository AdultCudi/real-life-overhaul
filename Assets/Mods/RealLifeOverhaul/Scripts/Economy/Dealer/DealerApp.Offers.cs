using RealLifeOverhaul.GameUi;
using UnityEngine;

namespace RealLifeOverhaul.Economy.Dealership
{
    /// <summary>The Offers tab (customers' offers and the cars sold lately) and the Finance tab (plans and warranties).</summary>
    public sealed partial class DealerApp
    {
        private const string DealRow = "deal:";

        private TableSpec WriteOffers(InfoBox box, DealerState state, int now)
        {
            int pickedId = Picked(_picked, DealRow);
            DealerOffer picked = null;
            DealerYard pickedYard = null;
            for (int y = 0; y < state.Yards.Count; y++)
            {
                DealerYard yard = state.Yards[y];
                for (int d = 0; d < yard.Deals.Count; d++)
                {
                    DealerOffer deal = yard.Deals[d];
                    StockCar car = yard.Car(deal.Car);
                    if (car == null)
                    {
                        continue;
                    }
                    TableRow row = Row(DealRow + deal.Id);
                    row.Kind = DealerColumns.Waiting;
                    row.Set(0, deal.Customer);
                    row.Set(1, DealerGame.CarName(car.Type));
                    row.Set(2, yard.Name);
                    Money(row, 3, deal.Offer);
                    Money(row, 4, car.Asking);
                    row.Set(5, Extras(deal));
                    int left = Mathf.Max(0, deal.Expires - now);
                    row.Set(6, DealerText.Hours(left), left, left <= 3 ? TableRow.Red : TableRow.Plain);
                    row.Set(7, deal.Stage == OfferStage.Driving ? DealerText.T("reallife_dealer_state_driving") : DealerText.T("reallife_dealer_state_waiting"), TableRow.Yellow);
                    if (deal.Id == pickedId)
                    {
                        picked = deal;
                        pickedYard = yard;
                    }
                }
            }
            for (int i = 0; i < state.Sales.Count; i++)
            {
                DealerSale s = state.Sales[i];
                TableRow row = Row("sale:" + i);
                row.Pick = false;
                row.Kind = DealerColumns.Sold;
                row.NoTotal = true;
                row.Set(0, s.Trade ? DealerText.T("reallife_dealer_to_trade") : s.Customer);
                row.Set(1, DealerGame.CarName(s.Type));
                DealerYard yard = state.Yard(s.Place);
                row.Set(2, yard != null ? yard.Name : string.Empty);
                Money(row, 3, s.Price);
                Money(row, 4, s.Cost);
                row.Set(5, (s.Financed ? DealerText.T("reallife_dealer_extra_finance") + " " : string.Empty) + (s.Tier >= 0 ? DealerText.Tier(s.Tier) : string.Empty)
                           + (s.Extras != 0 ? (s.Financed || s.Tier >= 0 ? ", " : string.Empty) + DealerText.Extras(s.Extras) : string.Empty));
                row.Set(6, FinanceText.Day(s.Day), s.Day);
                row.Set(7, DealerText.T("reallife_dealer_state_sold", "margin", DealerText.Money(s.Price - s.Cost)), s.Price < s.Cost ? TableRow.Red : TableRow.Green);
            }
            if (picked != null)
            {
                WriteDeal(box, state, pickedYard, picked, now);
                return DealerColumns.Offers;
            }
            _picked = null;
            box.Title(DealerText.T("reallife_dealer_offers_title"));
            box.Section("reallife_ibox_sec_now");
            box.Row("reallife_dealer_sum_waiting", _director.Waiting().ToString(), _director.Waiting() > 0 ? CellTone.Yellow : CellTone.Plain);
            int sold = 0, margin = 0;
            for (int y = 0; y < state.Yards.Count; y++)
            {
                sold += state.Yards[y].Today.Sold;
                margin += state.Yards[y].Today.Margin;
            }
            box.Line(DealerText.Fill("reallife_dealer_offers_today", "sold", sold.ToString(), "margin", DealerText.Money(margin)));
            box.Note(DealerText.T("reallife_dealer_offers_about"));
            Meta.Guide.Point(box, GuideTitle);
            return DealerColumns.Offers;
        }

        private static string Extras(DealerOffer d)
        {
            var t = new System.Text.StringBuilder(48);
            if (d.WantsDrive && !d.Drove)
            {
                t.Append(DealerText.T("reallife_dealer_extra_drive"));
            }
            if (d.WantsFinance)
            {
                t.Append(t.Length > 0 ? ", " : string.Empty).Append(DealerText.T("reallife_dealer_extra_finance"));
            }
            if (d.TradeType != null)
            {
                t.Append(t.Length > 0 ? ", " : string.Empty).Append(DealerText.T("reallife_dealer_extra_trade"));
            }
            if (d.Tier >= 0)
            {
                t.Append(t.Length > 0 ? ", " : string.Empty).Append(DealerText.Tier(d.Tier));
            }
            if (d.Extras != 0)
            {
                t.Append(t.Length > 0 ? ", " : string.Empty).Append(DealerText.Extras(d.Extras));
            }
            return t.ToString();
        }

        private static int ExtrasPrice(int extras, int price, out int cost)
        {
            int sell = 0;
            cost = 0;
            for (int e = 0; e < DealerCatalogue.ExtraKinds; e++)
            {
                if ((extras & (1 << e)) != 0)
                {
                    int one = DealerRules.ExtraPrice(price, e, Prices.At(DealerCatalogue.ExtraLeast[e]));
                    sell += one;
                    cost += DealerRules.ExtraCost(one, e);
                }
            }
            return sell;
        }

        private void WriteDeal(InfoBox box, DealerState state, DealerYard yard, DealerOffer d, int now)
        {
            StockCar car = yard.Car(d.Car);
            box.Title(d.Customer);
            box.Subtitle(DealerText.Profile(d.Profile));
            box.Subtitle(DealerGame.CarName(car.Type) + ", " + yard.Name);
            if (d.Stage == OfferStage.Driving)
            {
                box.Chip(DealerText.T("reallife_ibox_chip_driving"), CellTone.Plain);
                box.Line(DealerText.T("reallife_dealer_deal_driving"));
            }
            else if (d.WantsDrive && !d.Drove)
            {
                box.Line(DealerText.T("reallife_dealer_deal_wants_drive"), CellTone.Yellow);
            }
            box.Section("reallife_ibox_sec_offer");
            box.RowPair("reallife_ibox_offer", DealerText.Money(d.Offer), "reallife_ibox_asking", DealerText.Money(car.Asking));
            box.Note(DealerText.Fill("reallife_dealer_deal_what", "car", DealerGame.CarName(car.Type), "name", yard.Name, "offer", DealerText.Money(d.Offer), "asking", DealerText.Money(car.Asking),
                "left", DealerText.Hours(Mathf.Max(0, d.Expires - now))));
            if (d.WantsFinance)
            {
                bool lends = yard.Finance && DealerRules.Lends(d.Score);
                box.Line(DealerText.Fill(lends ? "reallife_dealer_deal_finance" : yard.Finance ? "reallife_dealer_deal_finance_refused" : "reallife_dealer_deal_finance_off",
                    "deposit", d.Deposit.ToString(), "weeks", ((d.Term + DealerCatalogue.WeekDays - 1) / DealerCatalogue.WeekDays).ToString(),
                    "grade", FinanceText.Grade(CreditCatalogue.GradeOf(d.Score))));
            }
            if (d.TradeType != null)
            {
                box.Line(DealerText.Fill("reallife_dealer_deal_trade", "car", DealerGame.CarName(d.TradeType), "km", DealerText.Km(d.TradeKm), "allowance", DealerText.Money(d.TradeAllowance)));
            }
            if (DealerRules.Tier(yard.Warranty, d.Tier, state) >= 0)   // the warranty the sale would take: none when no more can be kept
            {
                int premium = DealerRules.Premium(d.Offer, d.Tier, Prices.At(DealerCatalogue.PremiumLeast));
                box.Line(DealerText.T("reallife_dealer_deal_warranty", "tier", DealerText.Tier(d.Tier), "premium", DealerText.Money(premium)));
            }
            if (d.Extras != 0 && yard.Extras)
            {
                int sell = ExtrasPrice(d.Extras, d.Offer, out int cost);
                box.Line(DealerText.Fill("reallife_dealer_deal_extras", "extras", DealerText.Extras(d.Extras), "price", DealerText.Money(sell), "margin", DealerText.Money(sell - cost)));
            }
            if (d.Lift > 0)
            {
                box.Line(DealerText.T("reallife_dealer_deal_lift"), CellTone.Green);
            }
            int steps = DealerRules.CounterSteps(d, car.Asking);
            int step = Choice(steps - 1);
            int counter = DealerRules.CounterAt(d, car.Asking, step);
            if (d.Stage == OfferStage.Open && steps > 1)
            {
                Choose(box, steps, step, i => DealerText.T("reallife_dealer_deal_counter", "price", DealerText.Money(DealerRules.CounterAt(d, car.Asking, i)),
                    "left", (DealerCatalogue.CountersMost - d.Counters).ToString()), SetChoice);
            }
            Transform buttons = box.ButtonRow();
            if (d.Stage == OfferStage.Open)
            {
                InfoBox.Button(buttons, InfoBox.Positive, "reallife_dealer_button_accept", () =>
                {
                    if (_director.Accept(d.Id))
                    {
                        Pick(null);
                    }
                });
                if (steps > 1)
                {
                    InfoBox.Button(buttons, InfoBox.Additional, "reallife_dealer_button_counter", () =>
                    {
                        bool taken = _director.Counter(d.Id, counter, out bool gone);
                        if (taken || gone)
                        {
                            Pick(null);
                        }
                        else
                        {
                            UI.Notification.Notifications.Show(UI.Notification.NotificationType.Info, "reallife_dealer_note_counter_refused", null, 6f);
                        }
                        Refresh();
                    });
                }
                InfoBox.Button(buttons, InfoBox.Negative, "reallife_dealer_button_decline", () =>
                {
                    _director.Decline(d.Id);
                    Pick(null);
                    Refresh();
                });
            }
            Transform more = box.ButtonRow();
            if (d.Stage == OfferStage.Open && d.WantsDrive && !d.Drove)
            {
                InfoBox.Button(more, InfoBox.Positive, "reallife_dealer_button_drive", () => _director.Drive(d.Id));
            }
            InfoBox.Button(more, InfoBox.Additional, "common_close", ClosePick);
            Meta.Guide.Point(box, GuideTitle);
        }

        private TableSpec WriteFinance(InfoBox box, DealerState state)
        {
            int owed = 0, weekly = 0, missed = 0, paidOut = 0;
            for (int i = 0; i < state.Plans.Count; i++)
            {
                DealerPlan p = state.Plans[i];
                TableRow row = Row("plan:" + p.Id);
                row.Pick = false;
                row.Kind = DealerColumns.Plans;
                row.Set(0, p.Customer);
                row.Set(1, DealerGame.CarName(p.Type));
                DealerYard yard = state.Yard(p.Place);
                row.Set(2, yard != null ? yard.Name : string.Empty);
                Money(row, 3, p.Owed);
                Money(row, 4, p.Weekly);
                row.Set(5, FinanceText.Day(p.Due), p.Due);
                row.Set(6, p.Missed > 0
                        ? DealerText.T("reallife_dealer_plan_missed", "weeks", p.Missed.ToString())
                        : DealerText.T("reallife_dealer_plan_paying", "grade", FinanceText.Grade(CreditCatalogue.GradeOf(p.Score))),
                    p.Missed > 0 ? TableRow.Red : TableRow.Plain);
                owed += p.Owed;
                weekly += p.Weekly;
                missed += p.Missed > 0 ? 1 : 0;
            }
            for (int i = 0; i < state.Covers.Count; i++)
            {
                DealerCover c = state.Covers[i];
                TableRow row = Row("cover:" + c.Id);
                row.Pick = false;
                row.Kind = DealerColumns.Covers;
                row.NoTotal = true;
                row.Set(0, c.Customer);
                row.Set(1, DealerGame.CarName(c.Type));
                DealerYard yard = state.Yard(c.Place);
                row.Set(2, yard != null ? yard.Name : string.Empty);
                Money(row, 3, c.Paid, c.Paid > 0 ? TableRow.Yellow : TableRow.Plain);
                row.Set(4, c.Claims.ToString(), c.Claims);
                row.Set(5, FinanceText.Day(c.Until), c.Until);
                row.Set(6, DealerText.Tier(c.Tier));
                paidOut += c.Paid;
            }
            box.Title(DealerText.T("reallife_dealer_finance_title"));
            box.Section("reallife_ibox_sec_now");
            box.Line(DealerText.Fill("reallife_dealer_finance_plans", "plans", state.Plans.Count.ToString(), "owed", DealerText.Money(owed), "weekly", DealerText.Money(weekly),
                "missed", missed.ToString()));
            box.Line(DealerText.Fill("reallife_dealer_finance_covers", "covers", state.Covers.Count.ToString(), "paid", DealerText.Money(paidOut)));
            box.Note(DealerText.T("reallife_dealer_finance_about"));
            Meta.Guide.Point(box, GuideTitle);
            _picked = null;
            return DealerColumns.Finance;
        }
    }
}
