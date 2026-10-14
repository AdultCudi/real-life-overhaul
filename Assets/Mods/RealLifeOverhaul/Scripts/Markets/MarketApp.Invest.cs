using System.Collections.Generic;
using RealLifeOverhaul.GameUi;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Markets
{
    /// <summary>
    /// A large investor's parts of the Stocks app (InvestRules, MarketEngine.Invest.cs): on a company's page, an order at
    /// the market for more than one order may be is offered as a worked order (what it would take, its days, the button
    /// that places it), and a holding chooses the share of each dividend it puts back; on the Orders tab a worked order
    /// shows what is done, what is left and today's part, and a dividend put back its money; the History tab marks a
    /// worked order's day of slices and a purchase made with a dividend. Written when the app opens and when the player
    /// does something in it, never per frame.
    /// </summary>
    public sealed partial class MarketApp
    {
        // ---------------------------------------------------------------- a worked order on the ticket

        // An order at the market too large for one order, with worked orders on: it is worked instead of refused.
        private static bool Worked(in Estimate e, bool closing)
        {
            return e.Why == OrderWhy.TooLarge && !closing && Settings.MarketsLarge;
        }

        // What working it would take: its slices, its days, and what it comes to at today's price (or why it cannot be placed).
        private void WorkedLines(InfoBox box, MarketState s, Stock k, in Quote q)
        {
            if (_type != OrderType.Market)
            {
                box.Line(MarketText.Why(OrderWhy.TooLarge), CellTone.Red);   // (only an order at the market is worked: a limit or a stop is one order)
                return;
            }
            long most = MarketTrading.MostPerOrder(k);
            box.Line(MarketText.Fill("reallife_mkt_inv_ticket_worked", "most", Count(most), "days", InvestRules.MarketDays(_qty, k.Adv).ToString()), CellTone.Yellow);
            OrderWhy why = _director.WorkedWhy(k.Id, _side, _qty);
            if (why != OrderWhy.None)
            {
                box.Line(MarketText.Why(why), CellTone.Red);
                return;
            }
            bool buying = _side == OrderSide.Buy;
            int cents = buying ? q.Ask : q.Bid;
            box.Line(MarketText.Fill("reallife_mkt_inv_ticket_about", "price", MarketText.Price(cents), "total", MarketText.Money(MarketTrading.Value(_qty, cents, buying))));
        }

        // The worked order, as it will be placed, put to the player before it is.
        private void ConfirmWorked(Stock k)
        {
            OrderSide side = _side;
            long qty = _qty;
            OrderWhy why = _type == OrderType.Market ? _director.WorkedWhy(k.Id, side, qty) : OrderWhy.TooLarge;
            MarketState s = _director.State;
            if (why != OrderWhy.None || s == null)
            {
                Error(why, k);
                return;
            }
            bool buying = side == OrderSide.Buy;
            Quote q = MarketTrading.QuoteOf(s, k, s.Phase);
            GameConfirm.Show(
                MarketText.Fill(buying ? "reallife_mkt_inv_confirm_buy" : "reallife_mkt_inv_confirm_sell", "name", k.Name),
                MarketText.Fill("reallife_mkt_inv_confirm_body", "count", Count(qty), "name", k.Name, "most", Count(MarketTrading.MostPerOrder(k)),
                    "days", InvestRules.MarketDays(qty, k.Adv).ToString(), "total", MarketText.Money(MarketTrading.Value(qty, buying ? q.Ask : q.Bid, buying))),
                () => UiClone.Safe(() => PlaceWorkedOrder(k, side, qty)), null, "reallife_mkt_inv_work", "common_cancel", false, !buying);
        }

        private void PlaceWorkedOrder(Stock k, OrderSide side, long qty)
        {
            Order order = _director.PlaceWorked(k.Id, side, qty, out OrderWhy why);
            if (order == null)
            {
                Error(why, k);
            }
            else
            {
                Notifications.Show(NotificationType.Info, "reallife_mkt_inv_note_placed", new Dictionary<string, string> { { "name", k.Name }, { "count", Count(qty) } }, 6f, null, null, true, false);
            }
            Refresh();
        }

        // ---------------------------------------------------------------- a holding's dividends put back

        // On a company's page, with shares of it held: the share of each dividend put back into it at the next open.
        private void PutBackChoice(InfoBox box, Stock k, Position p)
        {
            if (!Settings.MarketsReinvest || p == null || p.Shares <= 0)
            {
                return;
            }
            int[] choices = InvestRules.ReinvestChoices;
            box.Selector(choices.Length, Mathf.Max(0, System.Array.IndexOf(choices, InvestRules.ReinvestOf(p.Reinvest))), i => InvestText.Reinvest(choices[i]), i => UiClone.Safe(() =>
            {
                if (!_director.SetReinvest(k.Id, choices[i], out OrderWhy why))
                {
                    Error(why, k);
                }
                Refresh();
            }));
        }

        // ---------------------------------------------------------------- the Orders tab

        // A worked order's shares: what is done of them; a dividend put back's before the open: none yet.
        private static string SharesWords(Order o)
        {
            if (o.Back && o.Qty <= 0)
            {
                return "-";
            }
            return o.Worked || (o.Filled > 0 && o.Filled < o.Qty) ? Count(o.Filled) + " / " + Count(o.Qty) : Count(o.Qty);
        }

        private static string LastsWords(Order o)
        {
            if (o.Worked)
            {
                return MarketText.T("reallife_mkt_inv_life_done");
            }
            return o.Life == OrderLife.Day ? MarketText.T(LifeKeys[0]) : MarketText.Fill("reallife_mkt_until_day", "day", o.ExpireDay.ToString());
        }

        // An order's state: a worked order open is working, and one stopped before it was done (not by the player) is stopped.
        private static string StateWords(Order o)
        {
            if (o.Worked && o.IsOpen)
            {
                return MarketText.T("reallife_mkt_inv_state_working");
            }
            return Stopped(o) ? MarketText.T("reallife_mkt_inv_state_stopped") : MarketText.T(StateKeys[Mathf.Clamp((int)o.State, 0, StateKeys.Length - 1)]);
        }

        private static bool Stopped(Order o)
        {
            return o.Worked && !o.IsOpen && o.State != OrderState.Filled && o.Why != OrderWhy.Cancelled;
        }

        // A worked order's progress (done, today, left) or a dividend put back's money, in the order's box.
        private void WriteInvestOrder(InfoBox box, MarketState s, Stock k, Order o)
        {
            box.Note(MarketText.Fill("reallife_mkt_order_what", "side", MarketText.T(SideKeys[(int)o.Side]), "count", o.Back && o.Qty <= 0 ? "-" : Count(o.Qty), "kind", KindWords(o)));
            if (o.Back)
            {
                box.Row("reallife_mkt_inv_row_money", MarketText.Money(o.Budget));
                if (o.IsOpen)
                {
                    box.Note(MarketText.T("reallife_mkt_inv_order_back_help"));
                }
                return;
            }
            box.Meter("reallife_ibox_done", LookRules.Fill(o.Filled, o.Qty), MarketText.Fill("reallife_ibox_of", "value", Count(o.Filled), "most", Count(o.Qty)), CellTone.Plain);
            if (o.Filled > 0)
            {
                box.Row("reallife_mkt_inv_row_average", MarketText.Price((int)System.Math.Min(int.MaxValue, o.FilledValue * 100 / o.Filled)));
            }
            if (o.DayOf == s.Day && o.DayQty > 0)
            {
                box.Row("reallife_ibox_today", MarketText.Fill("reallife_mkt_inv_today_value", "count", Count(o.DayQty), "price",
                    MarketText.Price((int)System.Math.Min(int.MaxValue, o.DayValue * 100 / o.DayQty))));
            }
            if (o.IsOpen)
            {
                box.Row("reallife_mkt_inv_row_left", MarketText.Fill("reallife_mkt_inv_left_value", "count", Count(o.Left), "days", InvestRules.MarketDays(o.Left, k != null ? k.Adv : 1.0).ToString()));
                box.Note(MarketText.Fill("reallife_mkt_inv_order_help", "share", MarketCatalogue.OrderShareOfVolumePct + "%"));
            }
        }

        // ---------------------------------------------------------------- the History tab

        // A line's mark: a worked order's day of slices, or a purchase made with a dividend put back.
        private static string InvestMark(Trade t)
        {
            if (t.Order > 0)
            {
                return InvestText.Mark(MarketText.T("reallife_mkt_inv_mark_worked"));
            }
            return t.Back ? InvestText.Mark(MarketText.T("reallife_mkt_inv_mark_put_back")) : string.Empty;
        }
    }
}
