using System;
using System.Collections.Generic;
using UI.Notification;

namespace RealLifeOverhaul.Markets
{
    /// <summary>Part of the market director: what the market tells the player, queued and shown once the frame's work is done.</summary>
    public sealed partial class MarketDirector
    {
        /// <summary>A piece of market news: the player hears of what touches what they hold or watch.</summary>
        internal void Heard(in MarketNews news)
        {
            MarketState s = _engine != null ? _engine.State : null;
            if (s == null || news.Kind == MarketNewsKind.Paid || OwnHeard(news))
            {
                return;   // news of the player's own company or way to the market (MarketDirector.Own.cs)
            }
            if (news.Stock <= 0 || (s.Held(news.Stock) == 0 && !s.Watching(news.Stock)))
            {
                return;
            }
            Stock k = s.Stock(news.Stock);
            string name = k != null ? k.Name : string.Empty;
            switch (news.Kind)
            {
                case MarketNewsKind.Results:
                    Queue(NotificationType.Info, news.A >= news.B ? "reallife_mkt_note_results_beat" : "reallife_mkt_note_results_miss", "name", name, "eps", MarketText.PerShare(news.A),
                        "consensus", MarketText.PerShare(news.B));
                    break;
                case MarketNewsKind.Bid:
                    Queue(NotificationType.Info, "reallife_mkt_note_bid", "name", name, "price", MarketText.Price(news.A), "day", news.B.ToString());
                    break;
                case MarketNewsKind.BidFailed:
                    Queue(NotificationType.Warning, "reallife_mkt_note_bid_failed", "name", name);
                    break;
                case MarketNewsKind.Crisis:
                    Queue(NotificationType.Warning, "reallife_mkt_note_crisis", "name", name);
                    break;
                case MarketNewsKind.Windfall:
                    Queue(NotificationType.Success, "reallife_mkt_note_windfall", "name", name);
                    break;
                case MarketNewsKind.Split:
                    Queue(NotificationType.Info, "reallife_mkt_note_split", "name", name, "ratio", news.A.ToString(), "day", news.B.ToString());
                    break;
                case MarketNewsKind.Builder:
                {
                    // A builder the player holds or watches: the market's own sentence for it, as the paper prints it.
                    string key = MarketText.NewsKey(news, s, out string[] data);
                    if (key.Length > 0)
                    {
                        Queue(news.B >= 0 ? NotificationType.Info : NotificationType.Warning, key, data);
                    }
                    break;
                }
            }
        }

        /// <summary>An order filled or refused at the fill, told once the frame's work is done.</summary>
        internal void OrderFilled(Order order, Stock stock, long qty, int cents)
        {
            if (order == null || stock == null || order.Worked)
            {
                return;   // a worked order's slices are told by the day (MarketDirector.Invest.cs)
            }
            if (order.Back)
            {
                // A dividend put back: what it bought, a part with the reason, or why it could not; the rest stays in the cash.
                if (qty <= 0)
                {
                    Queue(NotificationType.Warning, "reallife_mkt_inv_note_back_refused", "name", stock.Name, "why", MarketText.Why(order.Why, stock));
                }
                else if (order.Why != OrderWhy.None)
                {
                    Queue(NotificationType.Info, "reallife_mkt_inv_note_back_part", "name", stock.Name, "count", qty.ToString("N0"), "price", MarketText.Price(cents), "why",
                        MarketText.Why(order.Why, stock));
                }
                else
                {
                    Queue(NotificationType.Success, "reallife_mkt_inv_note_back", "name", stock.Name, "count", qty.ToString("N0"), "price", MarketText.Price(cents));
                }
                return;
            }
            if (qty <= 0)
            {
                Queue(NotificationType.Error, "reallife_mkt_note_refused", "name", stock.Name, "why", MarketText.Why(order.Why, stock));
                return;
            }
            if (order.Forced)
            {
                Queue(NotificationType.Warning, "reallife_mkt_note_forced", "name", stock.Name, "count", qty.ToString());
                return;
            }
            if (order.Closing)
            {
                // A stop loss says where it was set: a gap or a fast fall takes it under.
                Queue(order.Type == OrderType.Stop ? NotificationType.Warning : NotificationType.Success,
                    order.Type == OrderType.Stop ? "reallife_mkt_note_stop_loss" : "reallife_mkt_note_take_profit", "name", stock.Name, "count", qty.ToString(), "price", MarketText.Price(cents),
                    "stop", MarketText.Price(order.StopCents));
                return;
            }
            Queue(NotificationType.Success, order.Side == OrderSide.Buy ? "reallife_mkt_note_bought" : "reallife_mkt_note_sold", "name", stock.Name, "count", qty.ToString(),
                "price", MarketText.Price(cents));
        }

        /// <summary>Money in or out of the account: a dividend, a payout, interest, a fee, what the broker took.</summary>
        internal void MoneyMoved(TradeKind kind, Stock stock, long amount)
        {
            string name = stock != null ? stock.Name : string.Empty;
            switch (kind)
            {
                case TradeKind.Dividend:
                    _paid.Add(stock != null ? stock.Id : 0, 0, amount);   // told with any others paid at the same moment (TellPaid)
                    DividendTax(stock, amount);
                    break;
                case TradeKind.DividendPaid:
                    Queue(NotificationType.Info, "reallife_mkt_note_dividend_owed", "name", name, "amount", MarketText.Money(amount));
                    break;
                case TradeKind.Payout:
                    Queue(amount > 0 ? NotificationType.Info : NotificationType.Warning, "reallife_mkt_note_payout", "name", name, "amount", MarketText.Money(Math.Abs(amount)));
                    break;
                case TradeKind.Shortfall:
                    Queue(NotificationType.Error, "reallife_mkt_note_shortfall", "amount", MarketText.Money(amount));
                    break;
            }
        }

        internal void MarginCalled(long amount, int byDay)
        {
            Queue(NotificationType.Warning, "reallife_mkt_note_call", "amount", MarketText.Money(amount), "day", byDay.ToString());
        }

        private void Queue(NotificationType type, string key, params string[] pairs)
        {
            if (_notes.Count < 24)
            {
                _notes.Add(new Note { Type = type, Key = key, Pairs = pairs });
            }
        }

        /// <summary>A game notification of the key's text with its names filled in (name, value, ...); a click opens the app.</summary>
        private void Tell(NotificationType type, string key, string[] pairs)
        {
            Dictionary<string, string> data = null;
            if (pairs != null && pairs.Length > 1)
            {
                data = new Dictionary<string, string>(pairs.Length / 2);   // a dictionary each: the game keeps a notification's data
                for (int i = 0; i + 1 < pairs.Length; i += 2)
                {
                    data[pairs[i]] = pairs[i + 1] ?? string.Empty;
                }
            }
            Notifications.Show(type, key, data, 10f, null, Guard.Wrap(OpenApp), true, false);
        }
    }
}
