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
    /// <summary>
    /// The Bots tab of the Stocks app (MarketBots): a row for each firm, hired or not, with what its bot is worth, its
    /// profit, its deepest fall and its trades; picking one puts its terms, its record and its settings in the box, with
    /// the buttons to hire it, sell it up, start it again or let it go. A bot's holdings are rows of the Portfolio tab and
    /// its trades lines of the History tab, marked with its firm. Written when the app opens and when the player does
    /// something in it, never per frame.
    /// </summary>
    public sealed partial class MarketApp
    {
        public const int TabBots = 6;
        private const string BotRow = "bt:", BotHoldingRow = "bh:";
        private const int BotHoldingIds = 100000;   // a bot's holding's row: its firm times this, and the company's id

        private static readonly string[] StatusKeys = { "reallife_mkt_bot_status_running", "reallife_mkt_bot_status_stopped", "reallife_mkt_bot_status_selling" };
        private static readonly string[] StrategyKeys = { "reallife_mkt_bot_strategy_trend", "reallife_mkt_bot_strategy_reversion", "reallife_mkt_bot_strategy_momentum", "reallife_mkt_bot_strategy_income" };

        // The settings being written for the firm picked (its bot's own once it is hired).
        private int _botFirm, _botSize, _botStop, _botLoss;
        private long _botCapital;

        private TableSpec WriteBots(InfoBox box, MarketState s)
        {
            long worth = 0;
            int hired = 0;
            for (int i = 0; i < MarketBots.All.Length; i++)
            {
                BotFirm f = MarketBots.All[i];
                Bot b = MarketBots.Hired(s, f.Id);
                TableRow row = Row(BotRow, f.Id);
                row.Set(0, f.Name);
                row.Set(1, MarketText.T(StrategyKeys[(int)f.Strategy]));
                if (b == null)
                {
                    row.Set(2, string.Empty, 0.0);
                    row.Set(3, string.Empty, 0.0);
                    row.Set(4, string.Empty, 0.0);
                    row.Set(5, string.Empty, 0.0);
                    row.Set(6, string.Empty, 0.0);
                    row.Set(7, MarketText.T("reallife_mkt_bot_status_free"));
                    continue;
                }
                hired++;
                long now = MarketBots.Worth(s, b, s.Phase);
                worth += now;
                long gain = now - b.Capital;
                row.Set(2, MarketText.Money(b.Capital), b.Capital);
                row.Set(3, MarketText.Money(now), now);
                row.Set(4, MarketText.Money(gain), gain, Tone(Math.Sign(gain)));
                row.Set(5, b.DrawdownBp > 0 ? "-" + MarketText.Percent(b.DrawdownBp) : string.Empty, b.DrawdownBp, b.DrawdownBp > 0 ? TableRow.Red : TableRow.Plain);
                row.Set(6, Count(b.Trades), b.Trades);
                row.Set(7, MarketText.T(StatusKeys[(int)b.Status]), b.Status == BotStatus.Running ? TableRow.Green : b.Status == BotStatus.Stopped ? TableRow.Red : TableRow.Plain);
            }
            int id = Picked(_picked, BotRow);
            BotFirm picked = id != int.MinValue ? MarketBots.Firm(id) : null;
            if (picked != null)
            {
                WriteBotBox(box, s, picked);
            }
            else
            {
                box.Title(MarketText.T("reallife_mkt_tab_bots"));
                StatusLine(box);
                box.Line(MarketText.Fill("reallife_mkt_bot_box_hired", "count", hired.ToString(), "worth", MarketText.Money(worth)));
                box.Line(MarketText.T(Settings.MarketsBots ? "reallife_mkt_bot_box_help" : "reallife_mkt_bot_box_off"));
                Meta.Guide.Point(box, BotsGuideTitle);
            }
            return MarketTables.Bots;
        }

        private const string BotsGuideTitle = "reallife_meta_title_markets_bots";

        // The picked firm's box: its terms and settings to hire it, or its bot's record and settings.
        private void WriteBotBox(InfoBox box, MarketState s, BotFirm f)
        {
            Bot b = MarketBots.Hired(s, f.Id);
            if (_botFirm != f.Id)
            {
                _botFirm = f.Id;
                _botSize = b != null ? b.Size : f.Size;
                _botStop = b != null ? b.Stop : f.Stop;
                _botLoss = b != null ? b.Loss : f.Loss;
                _botCapital = Math.Max(_director.BotCapitalLeast, 0);
            }
            box.Title(f.Name + "  <size=70%>" + MarketText.T(StrategyKeys[(int)f.Strategy]) + "</size>");
            if (b == null)
            {
                box.Line(About(f));
                box.Line(MarketText.Fill("reallife_mkt_bot_box_terms", "fee", MarketText.Percent(f.FeeBp), "least", MarketText.Money(Prices.At(MarketBots.FeeLeastAtStart)),
                    "share", MarketText.Percent(f.ShareBp), "capital", MarketText.Money(_director.BotCapitalLeast)));
            }
            else
            {
                long now = MarketBots.Worth(s, b, s.Phase);
                int pct = b.Capital > 0 ? (int)Math.Max(-1000000L, Math.Min(1000000L, (now - b.Capital) * 10000L / b.Capital)) : 0;
                box.Line(MarketText.Fill("reallife_mkt_bot_box_worth", "status", MarketText.T(StatusKeys[(int)b.Status]), "worth", MarketText.Money(now), "capital", MarketText.Money(b.Capital),
                    "change", MarketText.Change(pct)));
                box.Line(MarketText.Fill("reallife_mkt_bot_box_record", "fall", MarketText.Percent(b.DrawdownBp), "trades", Count(b.Trades), "wins", b.Wins.ToString(), "closed", b.Closed.ToString()));
                box.Line(MarketText.Fill("reallife_mkt_bot_box_costs", "fees", MarketText.Money(b.Fees), "commission", MarketText.Money(b.Commission), "dividends", MarketText.Money(b.Dividends)));
                box.Line(MarketText.Fill("reallife_mkt_bot_box_floor", "floor", MarketText.Money(MarketBots.Floor(b)), "holdings", b.Holdings.Count.ToString()));
            }
            int size = b != null ? b.Size : _botSize, stop = b != null ? b.Stop : _botStop, loss = b != null ? b.Loss : _botLoss;
            box.Selector(MarketBots.Sizes.Length, MarketBots.IndexOf(MarketBots.Sizes, size), i => MarketText.Fill("reallife_mkt_bot_set_size", "pct", MarketBots.Sizes[i].ToString()),
                i => UiClone.Safe(() => Settle(f, b, MarketBots.Sizes[i], stop, loss)));
            box.Selector(MarketBots.Stops.Length, MarketBots.IndexOf(MarketBots.Stops, stop),
                i => MarketBots.Stops[i] == 0 ? MarketText.T("reallife_mkt_bot_set_stop_none") : MarketText.Fill("reallife_mkt_bot_set_stop", "pct", MarketBots.Stops[i].ToString()),
                i => UiClone.Safe(() => Settle(f, b, size, MarketBots.Stops[i], loss)));
            box.Selector(MarketBots.Losses.Length, MarketBots.IndexOf(MarketBots.Losses, loss), i => MarketText.Fill("reallife_mkt_bot_set_loss", "pct", MarketBots.Losses[i].ToString()),
                i => UiClone.Safe(() => Settle(f, b, size, stop, MarketBots.Losses[i])));
            Transform buttons = box.ButtonRow();
            if (b == null)
            {
                AskNumber(buttons, InfoBox.Additional, MarketText.Fill("reallife_mkt_bot_capital", "amount", MarketText.Money(_botCapital)), "reallife_mkt_bot_capital_placeholder", false,
                    value => _botCapital = value);
                InfoBox.Button(buttons, InfoBox.Positive, "reallife_mkt_bot_hire", () => UiClone.Safe(() => ConfirmHire(f)));
            }
            else
            {
                if (b.Status == BotStatus.Stopped)
                {
                    InfoBox.Button(buttons, InfoBox.Positive, "reallife_mkt_bot_restart", () => UiClone.Safe(() => Done(_director.RestartBot(f.Id, out BotWhy why), why)));
                }
                else if (b.Status == BotStatus.Running)
                {
                    InfoBox.Button(buttons, InfoBox.Additional, "reallife_mkt_bot_sell_up", () => UiClone.Safe(() => Done(_director.SellUpBot(f.Id, out BotWhy why), why)));
                }
                InfoBox.Button(buttons, InfoBox.Negative, "reallife_mkt_bot_dismiss", () => UiClone.Safe(() => ConfirmDismiss(f)));
            }
            Meta.Guide.Point(box, BotsGuideTitle);
        }

        // What a firm's strategy does, with its figures (MarketBots).
        private static string About(BotFirm f)
        {
            return MarketText.Fill(f.Key + "_about", "fast", MarketBots.TrendFast.ToString(), "slow", MarketBots.TrendSlow.ToString(), "in", MarketBots.RsiIn.ToString(),
                "out", MarketBots.RsiOut.ToString(), "filter", MarketBots.ReversionFilter.ToString(), "days", MarketBots.ReversionDaysMost.ToString(),
                "momentum", MarketBots.MomentumDays.ToString(), "quarters", MarketBots.IncomeQuarters.ToString());
        }

        // A setting changed: for a bot hired, at once (the market's next step on); for one being written, kept for the hire.
        private void Settle(BotFirm f, Bot b, int size, int stop, int loss)
        {
            if (b != null)
            {
                Done(_director.SetBot(f.Id, size, stop, loss, out BotWhy why), why);
                return;
            }
            _botSize = size;
            _botStop = stop;
            _botLoss = loss;
            Refresh();
        }

        private void ConfirmHire(BotFirm f)
        {
            int size = _botSize, stop = _botStop, loss = _botLoss;
            long capital = _botCapital;
            GameConfirm.Show(
                "reallife_mkt_bot_confirm_hire".Localize(new { firm = f.Name }),
                "reallife_mkt_bot_confirm_hire_body".Localize(new
                {
                    firm = f.Name, capital = MarketText.Money(capital), size = size.ToString(), loss = loss.ToString(), fee = MarketText.Percent(f.FeeBp), share = MarketText.Percent(f.ShareBp)
                }),
                () => UiClone.Safe(() =>
                {
                    Bot b = _director.HireBot(f.Id, capital, size, stop, loss, out BotWhy why);
                    if (b == null)
                    {
                        BotError(why);
                    }
                    else
                    {
                        Notifications.Show(NotificationType.Success, "reallife_mkt_bot_note_hired", new Dictionary<string, string> { { "firm", f.Name } }, 6f, null, null, true, false);
                    }
                    Refresh();
                }), null, "reallife_mkt_bot_hire", "common_cancel", false, false);
        }

        private void ConfirmDismiss(BotFirm f)
        {
            GameConfirm.Show(
                "reallife_mkt_bot_confirm_dismiss".Localize(new { firm = f.Name }),
                "reallife_mkt_bot_confirm_dismiss_body".Localize(new { firm = f.Name }),
                () => UiClone.Safe(() =>
                {
                    Done(_director.DismissBot(f.Id, out BotWhy why), why);
                    _botFirm = 0;
                }), null, "reallife_mkt_bot_dismiss", "common_cancel", false, true);
        }

        private void Done(bool done, BotWhy why)
        {
            if (!done)
            {
                BotError(why);
            }
            Refresh();
        }

        private static readonly string[] BotWhyKeys =
        {
            "reallife_mkt_bot_why_none", "reallife_mkt_bot_why_closed", "reallife_mkt_bot_why_held", "reallife_mkt_bot_why_behind", "reallife_mkt_bot_why_hired", "reallife_mkt_bot_why_full",
            "reallife_mkt_bot_why_too_little", "reallife_mkt_bot_why_no_cash", "reallife_mkt_bot_why_not_hired", "reallife_mkt_bot_why_not_stopped", "reallife_mkt_bot_why_too_little_left", "reallife_mkt_bot_why_off"
        };

        private static void BotError(BotWhy why)
        {
            int i = Mathf.Clamp((int)why, 0, BotWhyKeys.Length - 1);
            Notifications.Show(NotificationType.Error, "reallife_mkt_bot_note_refused", new Dictionary<string, string> { { "why", MarketText.T(BotWhyKeys[i]) } }, 8f, null, null, true, false);
        }

        // ---------------------------------------------------------------- the bots' holdings on the Portfolio tab

        private void WriteBotHoldings(MarketState s)
        {
            for (int i = 0; i < s.Bots.Count; i++)
            {
                Bot b = s.Bots[i];
                for (int j = 0; j < b.Holdings.Count; j++)
                {
                    BotHolding h = b.Holdings[j];
                    Stock k = s.Stock(h.StockId);
                    if (k == null)
                    {
                        continue;
                    }
                    Quote q = MarketTrading.QuoteOf(s, k, s.Phase);
                    long value = k.Listed ? MarketTrading.Value(h.Shares, q.Bid, false) : 0;
                    long gain = value - h.Cost;
                    TableRow row = Row(BotHoldingRow, b.Firm * BotHoldingIds + k.Id);
                    row.Kind = (int)k.Sector;
                    row.Set(0, k.Name + "  <size=80%>" + (b.Def != null ? b.Def.Name : string.Empty) + "</size>");
                    row.Set(1, Count(h.Shares), h.Shares);
                    double each = h.Shares > 0 ? h.Cost / (double)h.Shares : 0.0;
                    row.Set(2, MarketText.Price((int)Math.Round(each * 100.0)), each);
                    row.Set(3, MarketText.Price(k.Last), k.Last / 100.0);
                    row.Set(4, MarketText.Money(value), value);
                    row.Set(5, MarketText.Money(gain), gain, Tone(Math.Sign(gain)));
                    int pct = h.Cost != 0 ? (int)(gain * 10000L / Math.Abs(h.Cost)) : 0;
                    row.Set(6, MarketText.Change(pct), pct, Tone(pct));
                    row.Set(7, string.Empty, 0.0);
                }
            }
        }

        // A bot's holding's row picked: its company's page.
        private static int BotHoldingCompany(string id)
        {
            int n = Picked(id, BotHoldingRow);
            return n == int.MinValue ? int.MinValue : n % BotHoldingIds;
        }

        // The firm's name for a line of the History tab that is a bot's.
        private static string BotMark(int firm)
        {
            BotFirm f = firm > 0 ? MarketBots.Firm(firm) : null;
            return f != null ? "  <size=80%>" + f.Name + "</size>" : string.Empty;
        }
    }
}
