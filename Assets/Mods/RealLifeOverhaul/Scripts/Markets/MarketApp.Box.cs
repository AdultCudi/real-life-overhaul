using System;
using System.Collections.Generic;
using System.Globalization;
using Localizor;
using RealLifeOverhaul.GameUi;
using UI;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Markets
{
    public sealed partial class MarketApp
    {
        private const string CompanyRow = "co:", HoldingRow = "po:", SectorRow = "ix:", OrderRow = "or:", TradeRow = "tr:";
        private const int NewsInBox = 3, TypedLength = 12;   // the news in the box: the three newest (the Ledger's markets page has the rest)
        private const int QtyDefault = 10;

        private static readonly string[] SideKeys = { "reallife_mkt_side_buy", "reallife_mkt_side_sell" };
        private static readonly string[] TypeKeys = { "reallife_mkt_type_market", "reallife_mkt_type_limit", "reallife_mkt_type_stop" };
        // The ticket's kinds: at the market, a limit (to buy or to sell), and on what is held a stop loss and a take profit.
        private const int KindMarket = 0, KindLimit = 1, KindStopLoss = 2, KindTakeProfit = 3;
        private static readonly string[] TicketKeys = { "reallife_mkt_type_market", "reallife_mkt_type_limit", "reallife_mkt_type_stop_loss", "reallife_mkt_type_take_profit" };
        private static readonly string[] LifeKeys = { "reallife_mkt_life_day", "reallife_mkt_life_until" };
        private static readonly string[] StateKeys =
        {
            "reallife_mkt_state_open", "reallife_mkt_state_filled", "reallife_mkt_state_cancelled", "reallife_mkt_state_expired", "reallife_mkt_state_refused"
        };
        private static readonly string[] KindKeys =
        {
            "reallife_mkt_kind_bought", "reallife_mkt_kind_sold", "reallife_mkt_kind_dividend", "reallife_mkt_kind_dividend_paid", "reallife_mkt_kind_payout", "reallife_mkt_kind_interest",
            "reallife_mkt_kind_borrow_fee", "reallife_mkt_kind_deposit", "reallife_mkt_kind_withdrawal", "reallife_mkt_kind_shortfall"
        };

        // The order being written on a company's page.
        private OrderSide _side;
        private OrderType _type;
        private int _kind;           // KindMarket...: _type and whether it only closes follow from it
        private OrderLife _life;
        private long _qty = QtyDefault;
        private int _priceCents;     // a limit's or a stop's price; 0 until one is typed (the quote is then taken)

        private void ResetTicket()
        {
            _side = OrderSide.Buy;
            _type = OrderType.Market;
            _kind = KindMarket;
            _life = OrderLife.Day;
            _qty = QtyDefault;
            _priceCents = 0;
        }

        // ---------------------------------------------------------------- the market's state, the same on every page

        private void StatusLine(InfoBox box)
        {
            string key;
            if (_director.Behind)
            {
                key = "reallife_mkt_status_behind";
            }
            else if (_director.Engine != null && _director.Engine.Held)
            {
                key = "reallife_mkt_status_held";
            }
            else if (_director.Trading)
            {
                key = "reallife_mkt_status_open";
            }
            else
            {
                key = "reallife_mkt_status_shut";
            }
            box.Note(MarketText.Fill(key, "open", Clock(MarketCatalogue.OpenMinute), "close", Clock(MarketCatalogue.CloseMinute)));
        }

        private static string Clock(int minute)
        {
            return (minute / 60).GetFormattedTime(minute % 60);
        }

        private static string IndexWords(int hundredths)
        {
            return (hundredths / 100.0).ToString("N2", CultureInfo.CurrentCulture);
        }

        // ---------------------------------------------------------------- Market

        private TableSpec WriteMarket(InfoBox box, MarketState s)
        {
            int today = MarketHistory.IndexDayMoveBp(s);
            box.Title(MarketText.T("reallife_mkt_index"));
            box.Chip(MarketText.Change(today), today > 0 ? CellTone.Green : today < 0 ? CellTone.Red : CellTone.Plain);
            StatusLine(box);
            box.Section("reallife_look_sec_now");
            box.Line(MarketText.Fill("reallife_mkt_box_index", "level", IndexWords(s.IndexLast), "change", MarketText.Change(today)));
            box.Line(MarketText.Fill("reallife_mkt_box_rate", "rate", (s.Env.RateBp / 100f).ToString("0.00", CultureInfo.CurrentCulture) + "%",
                "inflation", (s.Env.InflationBp / 100f).ToString("0.0", CultureInfo.CurrentCulture) + "%"));
            box.Headline("reallife_mkt_box_news");
            int said = 0;
            for (int i = s.News.Count - 1; i >= 0 && said < NewsInBox; i--)
            {
                MarketNews n = s.News[i];
                if (n.Kind == MarketNewsKind.Close || n.Kind == MarketNewsKind.Mover)
                {
                    continue;
                }
                string news = MarketText.News(n, s);
                if (news.Length == 0)
                {
                    continue;   // (a player's company's refusals and the like are no news)
                }
                box.Line(news);
                said++;
            }
            if (said == 0)
            {
                box.Note(MarketText.T("reallife_mkt_box_no_news"));
            }
            OwnButton(box);
            Meta.Guide.Point(box, GuideTitle);

            TableRow index = Row(SectorRow, -1);
            WriteIndex(index, MarketText.T("reallife_mkt_index"), s.IndexLast, MarketHistory.IndexDayMoveBp(s), s.IndexDaily, null);
            for (int sec = 0; sec < (int)MarketSector.Count; sec++)
            {
                if (s.SectorLast[sec] <= 0)
                {
                    continue;
                }
                TableRow row = Row(SectorRow, sec);
                WriteIndex(row, MarketText.Sector((MarketSector)sec), s.SectorLast[sec], MarketHistory.SectorDayMoveBp(s, sec), null, s.SectorDaily[sec]);
            }
            return MarketTables.Overview;
        }

        private static readonly int[] Backs = { 5, 21, 63, 100 };

        private static void WriteIndex(TableRow row, string name, int last, int today, List<Candle> daily, List<int> closes)
        {
            row.Set(0, name);
            row.Set(1, IndexWords(last), last);
            row.Set(2, MarketText.Change(today), today, Tone(today));
            int count = daily != null ? daily.Count : closes.Count;
            for (int b = 0; b < Backs.Length; b++)
            {
                int at = count - Backs[b];
                if (at < 0)
                {
                    row.Set(3 + b, "-", double.NaN);
                    continue;
                }
                int then = daily != null ? daily[at].C : closes[at];
                int bp = Bp(last, then);
                row.Set(3 + b, MarketText.Change(bp), bp, Tone(bp));
            }
        }

        // The city index's page: its box.
        private void WriteIndexBox(InfoBox box, MarketState s)
        {
            box.Title(MarketText.T("reallife_mkt_index"));
            int today = MarketHistory.IndexDayMoveBp(s);
            box.Chip(MarketText.Change(today), today > 0 ? CellTone.Green : today < 0 ? CellTone.Red : CellTone.Plain);
            StatusLine(box);
            box.Section("reallife_look_sec_now");
            box.Line(MarketText.Fill("reallife_mkt_box_index", "level", IndexWords(s.IndexLast), "change", MarketText.Change(today)));
            box.Line(MarketText.Fill("reallife_mkt_box_range", "low", IndexWords(s.IndexLow), "high", IndexWords(s.IndexHigh)));
            Transform buttons = box.ButtonRow();
            InfoBox.Button(buttons, InfoBox.Negative, "reallife_mkt_back", () => UiClone.Safe(ClosePage));
            box.Note(MarketText.T("reallife_mkt_box_index_about"));
            Meta.Guide.Point(box, GuideTitle);
        }

        // ---------------------------------------------------------------- Companies and the watch list

        private TableSpec WriteCompanies(InfoBox box, MarketState s, bool watched)
        {
            int listed = 0, up = 0, down = 0;
            for (int i = 0; i < s.Stocks.Count; i++)
            {
                Stock k = s.Stocks[i];
                if (!k.Listed || (watched && !s.Watching(k.Id)))
                {
                    continue;
                }
                listed++;
                TableRow row = Row(CompanyRow, k.Id);
                row.Kind = (int)k.Sector;
                row.Kind2 = (int)k.Size;
                row.Set(0, k.Name + "  <size=80%>" + k.Ticker + "</size>");
                row.Set(1, MarketText.Sector(k.Sector));
                row.Set(2, MarketText.Price(k.Last), k.Last / 100.0);
                int bp = MarketHistory.DayMoveBp(k);
                up += bp > 0 ? 1 : 0;
                down += bp < 0 ? 1 : 0;
                row.Set(3, MarketText.Change(bp), bp, Tone(bp));
                row.Set(4, Count(k.DayVolume), k.DayVolume);
                double worth = k.Shares * (k.Last / 100.0);
                row.Set(5, MarketText.Large(worth), worth);
                double pe = Pe(k);
                row.Set(6, double.IsNaN(pe) ? "-" : pe.ToString("0.0", CultureInfo.CurrentCulture), double.IsNaN(pe) ? double.NaN : pe);
                int yield = YieldBp(k);
                row.Set(7, yield > 0 ? MarketText.Percent(yield) : "-", yield);
                long held = HeldOrFounder(s, k);
                row.Set(8, held != 0 ? Count(held) : string.Empty, held);
            }
            box.Title(MarketText.T(watched ? "reallife_mkt_tab_watch" : "reallife_mkt_tab_companies"));
            StatusLine(box);
            box.Line(MarketText.Fill(watched ? "reallife_mkt_box_watched" : "reallife_mkt_box_listed", "count", listed.ToString(), "up", up.ToString(), "down", down.ToString()));
            box.Line(MarketText.T(watched ? "reallife_mkt_box_watch_help" : "reallife_mkt_box_companies_help"));
            Meta.Guide.Point(box, GuideTitle);
            return watched ? MarketTables.Watch : MarketTables.Companies;
        }

        // ---------------------------------------------------------------- Orders

        private TableSpec WriteOrders(InfoBox box, MarketState s)
        {
            int open = 0;
            for (int i = s.Orders.Count - 1; i >= 0; i--)
            {
                Order o = s.Orders[i];
                Stock k = s.Stock(o.StockId);
                TableRow row = Row(OrderRow, o.Id);
                row.Kind = o.IsOpen ? MarketTables.IsOpen : MarketTables.IsDone;
                open += o.IsOpen ? 1 : 0;
                row.Set(0, k != null ? k.Name : string.Empty);
                row.Set(1, MarketText.T(SideKeys[(int)o.Side]));
                row.Set(2, KindWords(o) + (o.Forced ? " *" : string.Empty));
                row.Set(3, o.Filled > 0 && o.Filled < o.Qty ? Count(o.Filled) + " / " + Count(o.Qty) : Count(o.Qty), o.Qty);
                int cents = o.Type == OrderType.Limit ? o.LimitCents : o.Type == OrderType.Stop ? o.StopCents : o.Filled > 0 ? (int)(o.FilledValue * 100 / o.Filled) : 0;
                row.Set(4, cents > 0 ? MarketText.Price(cents) : "-", cents / 100.0);
                row.Set(5, o.Life == OrderLife.Day ? MarketText.T(LifeKeys[0]) : MarketText.Fill("reallife_mkt_until_day", "day", o.ExpireDay.ToString()));
                row.Set(6, MarketText.T(StateKeys[(int)o.State]), o.State == OrderState.Filled ? TableRow.Green : o.State == OrderState.Refused ? TableRow.Red : TableRow.Plain);
                row.Set(7, MarketText.Fill("reallife_mkt_day", "day", o.PlacedDay.ToString()), o.PlacedDay * 100.0 + o.PlacedPhase);
            }
            int id = Picked(_picked, OrderRow);
            Order picked = id != int.MinValue ? s.Order(id) : null;
            if (picked != null)
            {
                WriteOrderBox(box, s, picked);
            }
            else
            {
                box.Title(MarketText.T("reallife_mkt_tab_orders"));
                StatusLine(box);
                box.Line(MarketText.Fill("reallife_mkt_box_orders", "count", open.ToString(), "most", MarketCatalogue.OrdersMost.ToString()));
                box.Note(MarketText.T("reallife_mkt_box_orders_help"));
                Meta.Guide.Point(box, GuideTitle);
            }
            return MarketTables.Orders;
        }

        private void WriteOrderBox(InfoBox box, MarketState s, Order o)
        {
            Stock k = s.Stock(o.StockId);
            box.Title(k != null ? k.Name : string.Empty);
            box.Chip(MarketText.T(StateKeys[(int)o.State]), o.IsOpen ? CellTone.Plain : o.Filled > 0 ? CellTone.Green : CellTone.Yellow);
            box.Section("reallife_look_sec_order");
            box.Line(MarketText.Fill("reallife_mkt_order_state", "state", MarketText.T(StateKeys[(int)o.State]),
                "lasts", o.Life == OrderLife.Day ? MarketText.T(LifeKeys[0]) : MarketText.Fill("reallife_mkt_until_day", "day", o.ExpireDay.ToString())));
            if (o.Why != OrderWhy.None && !o.IsOpen)
            {
                box.Line(MarketText.Why(o.Why), CellTone.Red);
            }
            box.Note(MarketText.Fill("reallife_mkt_order_what", "side", MarketText.T(SideKeys[(int)o.Side]), "count", Count(o.Qty), "kind", KindWords(o)));
            if (o.Type == OrderType.Limit)
            {
                box.Line(MarketText.Fill("reallife_mkt_order_limit", "price", MarketText.Price(o.LimitCents)));
            }
            else if (o.Type == OrderType.Stop)
            {
                box.Line(MarketText.Fill(o.Triggered ? "reallife_mkt_order_stop_reached" : "reallife_mkt_order_stop", "price", MarketText.Price(o.StopCents)));
            }
            if (o.Filled > 0)
            {
                box.Line(MarketText.Fill("reallife_mkt_order_filled", "count", Count(o.Filled), "price", MarketText.Price((int)(o.FilledValue * 100 / o.Filled))));
            }
            if (o.Closing && o.IsOpen)
            {
                box.Note(MarketText.T(o.Type == OrderType.Stop ? "reallife_mkt_order_stop_loss_help" : "reallife_mkt_order_take_profit_help"));
            }
            Transform buttons = box.ButtonRow();
            if (o.IsOpen && !o.Forced)
            {
                InfoBox.Button(buttons, InfoBox.Negative, "reallife_mkt_cancel_order", () => UiClone.Safe(() =>
                {
                    if (!_director.Cancel(o.Id))
                    {
                        Error(_director.Behind ? OrderWhy.Behind : OrderWhy.Held, k);
                    }
                    Refresh();
                }));
            }
            if (k != null)
            {
                InfoBox.Button(buttons, InfoBox.Additional, "reallife_mkt_open_company", () => UiClone.Safe(() =>
                {
                    OpenPage(k.Id);
                    Refresh();
                }));
            }
            Meta.Guide.Point(box, GuideTitle);
        }

        // ---------------------------------------------------------------- Portfolio

        private TableSpec WritePortfolio(InfoBox box, MarketState s)
        {
            long worth = 0, cost = 0, dividends = 0;
            FounderRow(s);
            WriteBotHoldings(s);   // (the bots' holdings, marked with their firm: MarketApp.Bots.cs)
            for (int i = 0; i < s.Positions.Count; i++)
            {
                Position p = s.Positions[i];
                Stock k = s.Stock(p.StockId);
                if (k == null)
                {
                    continue;
                }
                Quote q = MarketTrading.QuoteOf(s, k, s.Phase);
                long value = p.Shares > 0 ? MarketTrading.Value(p.Shares, q.Bid, false) : -MarketTrading.Value(-p.Shares, q.Ask, true);
                long paid = p.Shares > 0 ? p.Cost : -p.Cost;
                long gain = value - paid;
                worth += value;
                cost += paid;
                dividends += p.Dividends;
                TableRow row = Row(HoldingRow, k.Id);
                row.Kind = (int)k.Sector;
                row.Set(0, k.Name + (p.Test ? "  <size=80%>" + MarketText.T("reallife_mkt_test_mark") + "</size>" : string.Empty));
                row.Set(1, Count(p.Shares), p.Shares);
                long shares = Math.Abs(p.Shares);
                double each = shares > 0 ? p.Cost / (double)shares : 0.0;
                row.Set(2, MarketText.Price((int)Math.Round(each * 100.0)), each);
                row.Set(3, MarketText.Price(k.Last), k.Last / 100.0);
                row.Set(4, MarketText.Money(value), value);
                row.Set(5, MarketText.Money(gain), gain, Tone(Math.Sign(gain)));
                int pct = paid != 0 ? (int)(gain * 10000L / Math.Abs(paid)) : 0;
                row.Set(6, MarketText.Change(pct), pct, Tone(pct));
                row.Set(7, MarketText.Money(p.Dividends), p.Dividends);
                string next = NextPayment(s, k, p, out double nextDay);
                row.Set(8, next, nextDay);
            }
            WriteAccount(box, s, worth, cost, dividends);
            BondsLine(box, s);   // (the bonds held: MarketApp.Bonds.cs)
            OwnButton(box);
            Meta.Guide.Point(box, GuideTitle);
            return MarketTables.Portfolio;
        }

        private void WriteAccount(InfoBox box, MarketState s, long worth, long cost, long dividends)
        {
            Account a = s.Account;
            box.Title(MarketText.T(a.Margin ? "reallife_mkt_account_margin" : "reallife_mkt_account_cash"));
            if (a.Margin && a.CallDay > 0)
            {
                box.Chip(MarketText.T("reallife_look_chip_margin_call"), CellTone.Red);
                box.Line(MarketText.Fill("reallife_mkt_account_call", "amount", MarketText.Money(a.CallAmount), "day", a.CallDay.ToString()), CellTone.Red);
            }
            StatusLine(box);
            box.Section("reallife_look_sec_now");
            box.Row("reallife_look_worth", MarketText.Money(worth));
            box.Row("reallife_look_gain", MarketText.Money(worth - cost), worth > cost ? CellTone.Green : worth < cost ? CellTone.Red : CellTone.Plain);
            box.Note(MarketText.Fill("reallife_mkt_account_worth", "worth", MarketText.Money(worth), "cost", MarketText.Money(cost), "gain", MarketText.Money(worth - cost)));
            box.Line(MarketText.Fill("reallife_mkt_account_dividends", "dividends", MarketText.Money(a.Dividends), "commission", MarketText.Money(a.Commission)));
            Transform buttons = null;
            if (a.Margin)
            {
                long equity = MarketTrading.Equity(s, s.Phase), need = MarketTrading.Maintenance(s, s.Phase), gross = MarketTrading.GrossExposure(s, s.Phase);
                long power = Math.Max(0, equity * 100 / MarketCatalogue.InitialMarginPct - gross);
                box.Section("reallife_mkt_account_margin");
                box.Line(MarketText.Fill("reallife_mkt_account_cash_line", "cash", MarketText.Money(a.Cash), "equity", MarketText.Money(equity)));
                box.Line(MarketText.Fill("reallife_mkt_account_margin_line", "need", MarketText.Money(need), "power", MarketText.Money(power)));
                box.Line(MarketText.Fill("reallife_mkt_account_withdrawable", "amount", MarketText.Money(_director.MostWithdrawable())));
                if (a.Interest + a.BorrowFees > 0)
                {
                    box.Line(MarketText.Fill("reallife_mkt_account_costs", "interest", MarketText.Money(a.Interest), "fees", MarketText.Money(a.BorrowFees)));
                }
                buttons = box.ButtonRow();
                if (Settings.MarketsMargin)
                {
                    AskMoney(buttons, InfoBox.Positive, "reallife_mkt_deposit", "reallife_mkt_deposit_placeholder", amount =>
                    {
                        if (!_director.Deposit(amount))
                        {
                            Notifications.ShowError("reallife_mkt_note_deposit_refused", null, false);
                        }
                    });
                }
                AskMoney(buttons, InfoBox.Additional, "reallife_mkt_withdraw", "reallife_mkt_withdraw_placeholder", amount =>
                {
                    if (!_director.Withdraw(amount))
                    {
                        Notifications.ShowError("reallife_mkt_note_withdraw_refused", null, false);
                    }
                });
            }
            else if (Settings.MarketsMargin)
            {
                box.Line(MarketText.Fill("reallife_mkt_account_margin_offer", "least", MarketText.Money(Prices.At(MarketCatalogue.MarginOpenLeastAtStart))));
                buttons = box.ButtonRow();
                AskMoney(buttons, InfoBox.Additional, "reallife_mkt_open_margin", "reallife_mkt_deposit_placeholder", amount =>
                {
                    if (!_director.Deposit(amount))
                    {
                        Notifications.ShowError("reallife_mkt_note_deposit_refused", null, false);
                    }
                });
            }
        }

        // The game's own text box for a sum of money; what is typed is checked by what it is for.
        private void AskMoney(Transform row, int kind, string buttonKey, string placeholderKey, Action<long> then)
        {
            UIs uis = InstanceBehavior<UIs>.Instance;
            if (uis == null || uis.textInputUi == null)
            {
                return;
            }
            InfoBox.Button(row, kind, buttonKey, () => uis.textInputUi.Show(placeholderKey, "common_confirm", text => UiClone.Safe(() =>
            {
                long amount;
                if (!long.TryParse((text ?? string.Empty).Trim().Replace(",", string.Empty).Replace(".", string.Empty), NumberStyles.Integer, CultureInfo.InvariantCulture, out amount)
                    || amount <= 0)
                {
                    Notifications.ShowError("common_notification_invalid_amount", null, false);
                    return;
                }
                then(amount);
                Refresh();
            }), null, string.Empty, false, TypedLength));
        }

        // ---------------------------------------------------------------- History

        private TableSpec WriteHistory(InfoBox box, MarketState s)
        {
            for (int i = s.Trades.Count - 1; i >= 0; i--)
            {
                Trade t = s.Trades[i];
                Stock k = t.StockId > 0 ? s.Stock(t.StockId) : null;
                TableRow row = Row(TradeRow, i);
                row.Pick = false;
                row.Set(0, MarketText.Fill("reallife_mkt_day", "day", t.Day.ToString()), t.Day * 100.0 + t.Phase);
                row.Set(1, k != null ? k.Name : string.Empty);
                int kind = Mathf.Clamp((int)t.Kind, 0, KindKeys.Length - 1);
                row.Set(2, MarketText.T(KindKeys[kind]) + (t.Test ? "  <size=80%>" + MarketText.T("reallife_mkt_test_mark") + "</size>" : string.Empty) + BotMark(t.Bot));
                row.Set(3, t.Qty != 0 ? Count(t.Qty) : string.Empty, t.Qty);
                row.Set(4, t.PriceCents > 0 ? MarketText.Price(t.PriceCents) : string.Empty, t.PriceCents / 100.0);
                row.Set(5, MarketText.Money(t.Value), t.Value, Tone(Math.Sign(t.Value)));
                row.Set(6, t.Fee > 0 ? MarketText.Money(t.Fee) : string.Empty, t.Fee);
            }
            Account a = s.Account;
            box.Title(MarketText.T("reallife_mkt_tab_history"));
            box.Line(MarketText.Fill("reallife_mkt_history_totals", "commission", MarketText.Money(a.Commission), "dividends", MarketText.Money(a.Dividends),
                "realised", MarketText.Money(a.Realised)));
            if (a.DividendsPaid + a.Interest + a.BorrowFees > 0)
            {
                box.Line(MarketText.Fill("reallife_mkt_history_costs", "paid", MarketText.Money(a.DividendsPaid), "interest", MarketText.Money(a.Interest), "fees", MarketText.Money(a.BorrowFees)));
            }
            box.Note(MarketText.Fill("reallife_mkt_history_kept", "count", MarketCatalogue.TradesKept.ToString()));
            Meta.Guide.Point(box, GuideTitle);
            return MarketTables.History;
        }

        // ---------------------------------------------------------------- a company's quote and the order ticket

        private void WriteTicket(InfoBox box, MarketState s, Stock k)
        {
            if (k.Own != null && _director.Engine != null && _director.Engine.Mine(k))
            {
                WriteOwnTicket(box, s, k);   // the founder deals by the rules of their own (MarketApp.Own.cs)
                return;
            }
            box.Title(k.Name);
            box.Subtitle(k.Ticker);
            Quote q = MarketTrading.QuoteOf(s, k, s.Phase);
            int bp = MarketHistory.DayMoveBp(k);
            box.Chip(MarketText.Change(bp), bp > 0 ? CellTone.Green : bp < 0 ? CellTone.Red : CellTone.Plain);
            box.Section("reallife_look_sec_quote");
            box.RowPair("reallife_look_bid", MarketText.Price(q.Bid), "reallife_look_ask", MarketText.Price(q.Ask));
            box.Note(MarketText.Fill("reallife_mkt_ticket_quote", "bid", MarketText.Price(q.Bid), "ask", MarketText.Price(q.Ask), "last", MarketText.Price(k.Last), "change", MarketText.Change(bp)));
            long held = s.Held(k.Id);
            Position p = s.Position(k.Id);
            if (held != 0 && p != null)
            {
                long value = held > 0 ? MarketTrading.Value(held, q.Bid, false) : -MarketTrading.Value(-held, q.Ask, true);
                long paid = held > 0 ? p.Cost : -p.Cost;
                box.Line(MarketText.Fill("reallife_mkt_ticket_held", "count", Count(held), "worth", MarketText.Money(value), "gain", MarketText.Money(value - paid)),
                    value > paid ? CellTone.Green : value < paid ? CellTone.Red : CellTone.Plain);
            }
            else
            {
                box.Line(MarketText.Fill("reallife_mkt_ticket_status", "status", Status(s, k)));
            }
            TrustLine(box, k);   // a property trust: what it pays and when (MarketApp.Bonds.cs)
            bool closing = _kind >= KindStopLoss;
            box.Section("reallife_look_sec_order");
            if (closing)
            {
                // A stop loss or a take profit is on what is held: it sells a holding, or buys back shares sold short.
                _side = held < 0 ? OrderSide.Buy : OrderSide.Sell;
            }
            else
            {
                box.Selector(SideKeys.Length, (int)_side, i => MarketText.T(SideKeys[i]), i => UiClone.Safe(() =>
                {
                    _side = (OrderSide)i;
                    _priceCents = 0;
                    Refresh();
                }));
            }
            box.Selector(TicketKeys.Length, _kind, i => MarketText.T(TicketKeys[i]), i => UiClone.Safe(() =>
            {
                _kind = i;
                _type = i == KindMarket ? OrderType.Market : i == KindStopLoss ? OrderType.Stop : OrderType.Limit;
                _priceCents = 0;
                long holding = Math.Abs(s.Held(k.Id));
                if (i >= KindStopLoss && holding > 0)
                {
                    _qty = holding;   // (all of it, to begin with)
                }
                Refresh();
            }));
            box.Selector(LifeKeys.Length, (int)_life, i => MarketText.T(LifeKeys[i]), i => UiClone.Safe(() =>
            {
                _life = (OrderLife)i;
                Refresh();
            }));
            int price = TicketPrice(q);
            Transform typed = box.ButtonRow();
            AskNumber(typed, InfoBox.Additional, MarketText.Fill("reallife_mkt_ticket_shares", "count", Count(_qty)), "reallife_mkt_shares_placeholder", false, value =>
            {
                _qty = Math.Max(1, value);
            });
            if (_type != OrderType.Market)
            {
                AskNumber(typed, InfoBox.Positive, MarketText.Fill(_kind == KindTakeProfit ? "reallife_mkt_ticket_take_profit" : _type == OrderType.Limit ? "reallife_mkt_ticket_limit" : "reallife_mkt_ticket_stop",
                        "price", MarketText.Price(price)),
                    "reallife_mkt_price_placeholder", true, value =>
                    {
                        _priceCents = (int)Math.Max(1, Math.Min(int.MaxValue, value));
                    });
            }
            Estimate e = _director.Estimate(k.Id, _side, _type, _qty, _type == OrderType.Limit ? price : 0, _type == OrderType.Stop ? price : 0, closing);
            if (e.Why == OrderWhy.None)
            {
                box.Line(MarketText.Fill(_side == OrderSide.Buy ? "reallife_mkt_ticket_cost" : "reallife_mkt_ticket_brings", "price", MarketText.Price(e.PriceCents), "value", MarketText.Money(e.Value),
                    "fee", MarketText.Money(e.Fee), "total", MarketText.Money(e.Total)));
                if (e.ImpactBp >= 5)
                {
                    box.Line(MarketText.Fill("reallife_mkt_ticket_impact", "move", MarketText.Percent(e.ImpactBp)), CellTone.Yellow);
                }
            }
            else
            {
                box.Line(MarketText.Why(e.Why), CellTone.Red);
            }
            Transform buttons = box.ButtonRow();
            Stock company = k;
            // (always lit: an order that cannot be placed has its reason in the line above, and pressed, in the game's notification)
            InfoBox.Button(buttons, InfoBox.Positive, _side == OrderSide.Buy ? "reallife_mkt_buy" : "reallife_mkt_sell", () => UiClone.Safe(() => Confirm(company, price)));
            bool watching = s.Watching(k.Id);
            InfoBox.Button(buttons, InfoBox.Additional, watching ? "reallife_mkt_unwatch" : "reallife_mkt_watch", () => UiClone.Safe(() =>
            {
                if (!_director.Watch(company.Id, !watching))
                {
                    Notifications.ShowError("reallife_mkt_note_watch_full", null, false);
                }
                Refresh();
            }));
            InfoBox.Button(buttons, InfoBox.Negative, "reallife_mkt_back", () => UiClone.Safe(ClosePage));
            Meta.Guide.Point(box, GuideTitle);
        }

        // A limit's or a stop's price as typed, or one to begin with: a limit to buy at the offer and to sell at the bid, a
        // stop loss a twentieth beyond the price, a take profit a tenth.
        private int TicketPrice(in Quote q)
        {
            if (_priceCents > 0)
            {
                return _priceCents;
            }
            if (_type == OrderType.Stop)
            {
                return _side == OrderSide.Sell ? Math.Max(1, q.Bid * 95 / 100) : q.Ask * 105 / 100;
            }
            if (_kind == KindTakeProfit)
            {
                return _side == OrderSide.Sell ? q.Bid * 110 / 100 : Math.Max(1, q.Ask * 90 / 100);
            }
            return _side == OrderSide.Buy ? q.Ask : q.Bid;
        }

        // An order's kind in words: a stop loss and a take profit by their own names.
        private static string KindWords(Order o)
        {
            if (o.Closing && o.Type != OrderType.Market)
            {
                return MarketText.T(o.Type == OrderType.Stop ? TicketKeys[KindStopLoss] : TicketKeys[KindTakeProfit]);
            }
            return MarketText.T(TypeKeys[Mathf.Clamp((int)o.Type, 0, TypeKeys.Length - 1)]);
        }

        // A company's state in a few words: a bid for it, a split coming, its results' day, or its sector and size.
        private static string Status(MarketState s, Stock k)
        {
            if (k.BidCents > 0)
            {
                return MarketText.Fill("reallife_mkt_status_bid", "price", MarketText.Price(k.BidCents), "day", k.BidEndDay.ToString());
            }
            if (k.SplitDay > 0)
            {
                return MarketText.Fill("reallife_mkt_status_split", "ratio", k.SplitRatio.ToString(), "day", k.SplitDay.ToString());
            }
            if (k.ResultsDay > 0)
            {
                return MarketText.Fill("reallife_mkt_status_results", "day", k.ResultsDay.ToString());
            }
            return MarketText.Sector(k.Sector) + ", " + MarketText.Size(k.Size);
        }

        // The game's own text box for a number of shares or a price.
        private void AskNumber(Transform row, int kind, string words, string placeholderKey, bool price, Action<long> then)
        {
            UIs uis = InstanceBehavior<UIs>.Instance;
            if (uis == null || uis.textInputUi == null)
            {
                return;
            }
            UnityEngine.UI.Button button = InfoBox.Button(row, kind, "reallife_mkt_ticket_shares", () => uis.textInputUi.Show(placeholderKey, "common_confirm", text => UiClone.Safe(() =>
            {
                long value;
                if (!(price ? TryCents(text, out value) : long.TryParse((text ?? string.Empty).Trim().Replace(",", string.Empty), NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
                    || value <= 0)
                {
                    Notifications.ShowError("common_notification_invalid_amount", null, false);
                    return;
                }
                then(value);
                Refresh();
            }), null, string.Empty, false, TypedLength));
            if (button != null)
            {
                var label = button.GetComponentInChildren<TMPro.TMP_Text>(true);
                if (label != null)
                {
                    UiClone.Words(label, words);   // (the figure on the button: the key above only stands for the button's kind)
                }
            }
        }

        /// <summary>A price as typed ("12.5", "12,50", "1234") in cents.</summary>
        internal static bool TryCents(string text, out long cents)
        {
            cents = 0;
            string t = (text ?? string.Empty).Trim().Replace(',', '.');
            decimal value;
            if (!decimal.TryParse(t, NumberStyles.Number, CultureInfo.InvariantCulture, out value) || value <= 0m || value > 10000000m)
            {
                return false;
            }
            cents = (long)Math.Round(value * 100m);
            return cents > 0;
        }

        // The order, as it will be placed, put to the player before it is.
        private void Confirm(Stock k, int price)
        {
            bool closing = _kind >= KindStopLoss;
            Estimate e = _director.Estimate(k.Id, _side, _type, _qty, _type == OrderType.Limit ? price : 0, _type == OrderType.Stop ? price : 0, closing);   // (as it is now, not as it was drawn)
            if (e.Why != OrderWhy.None)
            {
                Error(e.Why, k);
                return;
            }
            OrderSide side = _side;
            OrderType type = _type;
            OrderLife life = _life;
            long qty = _qty;
            GameConfirm.Show(
                (side == OrderSide.Buy ? "reallife_mkt_confirm_buy" : "reallife_mkt_confirm_sell").Localize(new { name = k.Name }),
                "reallife_mkt_confirm_body".Localize(new
                {
                    count = Count(qty), name = k.Name, kind = MarketText.T(TicketKeys[_kind]), price = MarketText.Price(e.PriceCents), value = MarketText.Money(e.Value),
                    fee = MarketText.Money(e.Fee), total = MarketText.Money(e.Total), lasts = MarketText.T(LifeKeys[(int)life])
                }),
                () => UiClone.Safe(() => Place(k, side, type, life, qty, price, closing)), null, side == OrderSide.Buy ? "reallife_mkt_buy" : "reallife_mkt_sell", "common_cancel", false,
                side == OrderSide.Sell);
        }

        private void Place(Stock k, OrderSide side, OrderType type, OrderLife life, long qty, int price, bool closing)
        {
            OrderWhy why;
            Order order = _director.Place(k.Id, side, type, life, qty, type == OrderType.Limit ? price : 0, type == OrderType.Stop ? price : 0, false, out why, closing);
            if (order == null)
            {
                Error(why, k);
            }
            else if (order.IsOpen && order.Filled == 0)
            {
                Notifications.Show(NotificationType.Info, "reallife_mkt_note_waiting", new Dictionary<string, string> { { "name", k.Name } }, 6f, null, null, true, false);
            }
            Refresh();
        }

        private static void Error(OrderWhy why, Stock k)
        {
            Notifications.Show(NotificationType.Error, "reallife_mkt_note_refused", new Dictionary<string, string> { { "name", k != null ? k.Name : string.Empty }, { "why", MarketText.Why(why) } },
                8f, null, null, true, false);
        }
    }
}
