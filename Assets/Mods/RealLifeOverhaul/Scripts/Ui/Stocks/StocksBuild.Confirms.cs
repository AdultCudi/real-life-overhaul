using System;
using System.Collections.Generic;
using RealLifeOverhaul.Markets;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The order's own question. The old Stocks app asked it in the game's question box before every order ("Buy shares of X", with
    /// the count, the kind, the price, the cost, the commission, the total and how long it lasts) and before a worked order; the kit's
    /// panel only placed it. This sheet asks the same, with the same amounts, as they stand when it is asked, and the order goes only when
    /// the player confirms. Built when the sheet changes, never per frame.
    /// </summary>
    public static partial class StocksBuild
    {
        private static readonly string[] TicketKindKeys = { "reallife_mkt_type_market", "reallife_mkt_type_limit", "reallife_mkt_type_stop_loss", "reallife_mkt_type_take_profit" };
        private static readonly string[] TicketLifeKeys = { "reallife_mkt_life_day", "reallife_mkt_life_until" };

        /// <summary>The order the ticket sheet holds, read back out of its slots.</summary>
        public static void TicketSlots(StocksSheetState st, out int stockId, out bool buy, out int kind, out int price, out OrderLife life, out long qty)
        {
            long[] v = st.V;
            stockId = (int)v[StocksSheets.TStock];
            buy = v[StocksSheets.TBuy] != 0;
            kind = (int)Math.Max(0, Math.Min(3, v[StocksSheets.TKind]));
            price = (int)Math.Max(0, Math.Min(int.MaxValue, v[StocksSheets.TPrice]));
            life = v[StocksSheets.TLife] == 1 ? OrderLife.UntilCancelled : OrderLife.Day;
            qty = Math.Max(1L, v[StocksSheets.TQty]);
        }

        /// <summary>Whether an order the engine says is too large for one order is to be worked in slices (the market's large-order rule), as the old app asked.</summary>
        public static bool WorkedOrder(Estimate e, OrderType type, bool closing) => e.Why == OrderWhy.TooLarge && type == OrderType.Market && !closing && Settings.MarketsLarge;

        public static StocksSheetModel TicketSheet(MarketDirector d, StocksSheetState st, UiWords w)
        {
            MarketState s = d.State;
            TicketSlots(st, out int stockId, out bool buy, out int kind, out int typed, out OrderLife life, out long qty);
            Stock k = s.Stock(stockId);
            StocksSheetModel m = Frame(w, w("reallife_mkt_tab_orders", "Orders"), string.Empty, UiIcon.ChartLineUp);
            if (k == null)
            {
                m.Name = w("reallife_stk_none", "None");
                return m;
            }
            bool closing = kind >= 2;
            if (closing)
            {
                buy = s.Held(k.Id) < 0;
            }
            OrderType type = TypeOf(kind);
            int price = TicketPrice(s, k, !buy, kind, typed);
            OrderSide side = buy ? OrderSide.Buy : OrderSide.Sell;
            Estimate e = d.Estimate(k.Id, side, type, qty, type == OrderType.Limit ? price : 0, type == OrderType.Stop ? price : 0, closing);   // as it is now, not as it was drawn
            bool worked = WorkedOrder(e, type, closing);
            m.Name = k.Name;
            m.Sub = k.Ticker;
            if (worked)
            {
                Quote q = MarketTrading.QuoteOf(s, k, s.Phase);
                long total = MarketTrading.Value(qty, buy ? q.Ask : q.Bid, buy);
                m.Title = MarketText.Fill(buy ? "reallife_mkt_inv_confirm_buy" : "reallife_mkt_inv_confirm_sell", "name", k.Name);
                m.Big = Money(total);
                m.About = Plain(MarketText.Fill("reallife_mkt_inv_confirm_body", "count", qty.ToString("N0"), "name", k.Name, "most", MarketTrading.MostPerOrder(k).ToString("N0"),
                    "days", InvestRules.MarketDays(qty, k.Adv).ToString(), "total", Money(total)) + (s.Account.Margin ? string.Empty : "\n" + MarketText.T(buy ? "reallife_mkt_inv_ticket_hold" : "reallife_mkt_inv_ticket_due")));
                m.SideLabels = new[] { w("reallife_mkt_col_shares", "Shares"), w("reallife_stk_sh_per_day", "A day, about"), w("reallife_stk_sh_days", "Days it takes") };
                m.SideValues = new[] { qty.ToString("N0"), MarketTrading.MostPerOrder(k).ToString("N0"), InvestRules.MarketDays(qty, k.Adv).ToString() };
                m.Confirm = w("reallife_mkt_inv_work", "Work it");
                m.Reason = d.WorkedWhy(k.Id, side, qty) == OrderWhy.None ? null : MarketText.Why(d.WorkedWhy(k.Id, side, qty), k);
                m.ConfirmOff = m.Reason != null;
                return m;
            }
            m.Title = MarketText.Fill(buy ? "reallife_mkt_confirm_buy" : "reallife_mkt_confirm_sell", "name", k.Name);
            m.Big = Money(e.Total);
            string kindWords = MarketText.T(TicketKindKeys[kind]);
            string lasts = MarketText.T(TicketLifeKeys[life == OrderLife.UntilCancelled ? 1 : 0]);
            m.About = Plain(MarketText.Fill("reallife_mkt_confirm_body", "count", qty.ToString("N0"), "name", k.Name, "kind", kindWords, "price", MarketText.Price(e.PriceCents),
                "value", Money(e.Value), "fee", Money(e.Fee), "total", Money(e.Total), "lasts", lasts));
            m.SideLabels = new[]
            {
                w("reallife_mkt_col_shares", "Shares"), w("reallife_mkt_col_kind", "Kind"), w("reallife_mkt_col_price", "Price"), w("reallife_stk_row_cost", "Cost"),
                w("reallife_stk_row_fee", "Fee"), w("reallife_stk_row_total", "Total"), w("reallife_mkt_col_lasts", "Lasts")
            };
            m.SideValues = new[] { qty.ToString("N0"), kindWords, MarketText.Price(e.PriceCents), Money(e.Value), Money(e.Fee), Money(e.Total), lasts };
            m.Confirm = w(buy ? "reallife_mkt_buy" : "reallife_mkt_sell", buy ? "Buy" : "Sell");
            m.Reason = e.Why == OrderWhy.None ? null : MarketText.Why(e.Why, k);
            m.ConfirmOff = e.Why != OrderWhy.None;
            return m;
        }

        /// <summary>A question the old app put in the game's box, with its answer's amounts: the title, then the body, as one text.</summary>
        private static string Asked(string title, string body) => Plain(title) + "\n" + Plain(body);
    }
}
