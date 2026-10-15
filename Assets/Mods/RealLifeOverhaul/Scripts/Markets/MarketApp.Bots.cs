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
    /// the buttons to hire it, sell it up, start it again or let it go, and for a running bot, on its first page, to add
    /// capital or take some out of its money not in shares (nothing is sold either way). Its settings are on three pages, chosen by the
    /// first selector so that the box keeps its foot: its trades (size, stop, limit), the shares it trades (its whole
    /// market, the companies picked for it, or the player's own holdings; with a picker over the player's own company and
    /// the companies they hold or watch) and its payouts (a share of new profit, daily, weekly or monthly). A bot's
    /// holdings are rows of the Portfolio tab and its trades lines of the History tab, marked with its firm. Written when
    /// the app opens and when the player does something in it, never per frame.
    /// </summary>
    public sealed partial class MarketApp
    {
        public const int TabBots = 6;
        private const string BotRow = "bt:", BotHoldingRow = "bh:";
        private const int BotHoldingIds = 100000;   // a bot's holding's row: its firm times this, and the company's id

        private static readonly string[] StatusKeys = { "reallife_mkt_bot_status_running", "reallife_mkt_bot_status_stopped", "reallife_mkt_bot_status_selling" };
        private static readonly string[] StrategyKeys = { "reallife_mkt_bot_strategy_trend", "reallife_mkt_bot_strategy_reversion", "reallife_mkt_bot_strategy_momentum", "reallife_mkt_bot_strategy_income" };
        private static readonly string[] PageKeys = { "reallife_mkt_bot_page_trading", "reallife_mkt_bot_page_shares", "reallife_mkt_bot_page_payouts" };
        private static readonly string[] ScopeKeys = { "reallife_mkt_bot_scope_market", "reallife_mkt_bot_scope_picked", "reallife_mkt_bot_scope_held" };
        private static readonly string[] EveryKeys = { "reallife_mkt_bot_every_day", "reallife_mkt_bot_every_week", "reallife_mkt_bot_every_month" };
        private const int PageTrading = 0, PageShares = 1, PagePayouts = 2;

        // The settings being written for the firm picked (its bot's own once it is hired).
        private int _botFirm, _botSize, _botStop, _botLoss;
        private long _botCapital, _botChange;   // the capital to hire with; the sum to add to a running bot or take out of it
        private int _botPage, _botPickAt, _botPay;
        private BotScope _botScope;
        private BotPayEvery _botEvery = BotPayEvery.Monthly;
        private readonly List<int> _botPicks = new List<int>(MarketBots.PicksMost);       // a bot being written: its picks, for the hire
        private readonly List<int> _botCandidates = new List<int>(32);                   // the picker's companies, made when the box is written

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
                long gain = now + b.PaidOut - b.Capital;   // (what it paid out is profit too)
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
                box.Section("reallife_ibox_sec_now");
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
                _botChange = Math.Max(_director.BotChangeLeast, 0);
                _botPage = PageTrading;
                _botScope = BotScope.Market;
                _botPay = 0;
                _botEvery = BotPayEvery.Monthly;
                _botPickAt = 0;
                _botPicks.Clear();
            }
            box.Title(f.Name);
            box.Subtitle(MarketText.T(StrategyKeys[(int)f.Strategy]));
            if (b == null)
            {
                box.Note(About(f));
                box.Section("reallife_ibox_sec_terms");
                box.Line(MarketText.Fill("reallife_mkt_bot_box_terms", "fee", MarketText.Percent(f.FeeBp), "least", MarketText.Money(Prices.At(MarketBots.FeeLeastAtStart)),
                    "share", MarketText.Percent(f.ShareBp), "capital", MarketText.Money(_director.BotCapitalLeast)));
            }
            else
            {
                long now = MarketBots.Worth(s, b, s.Phase);
                int pct = b.Capital > 0 ? (int)Math.Max(-1000000L, Math.Min(1000000L, (now + b.PaidOut - b.Capital) * 10000L / b.Capital)) : 0;
                box.Chip(MarketText.T(StatusKeys[(int)b.Status]));
                box.Chip(MarketText.Change(pct), pct > 0 ? CellTone.Green : pct < 0 ? CellTone.Red : CellTone.Plain);
                box.Section("reallife_ibox_sec_record");
                box.Line(MarketText.Fill("reallife_mkt_bot_box_worth", "status", MarketText.T(StatusKeys[(int)b.Status]), "worth", MarketText.Money(now), "capital", MarketText.Money(b.Capital),
                    "change", MarketText.Change(pct)));
                box.Line(MarketText.Fill("reallife_mkt_bot_box_record", "fall", MarketText.Percent(b.DrawdownBp), "trades", Count(b.Trades), "wins", b.Wins.ToString(), "closed", b.Closed.ToString()));
                box.Line(MarketText.Fill("reallife_mkt_bot_box_costs", "fees", MarketText.Money(b.Fees), "commission", MarketText.Money(b.Commission), "dividends", MarketText.Money(b.Dividends)));
                box.Line(MarketText.Fill("reallife_mkt_bot_box_floor", "floor", MarketText.Money(MarketBots.Floor(b)), "holdings", b.Holdings.Count.ToString()));
                if (b.PayPct > 0 || b.PaidOut > 0 || b.PayOwed > 0)
                {
                    box.Line(MarketText.Fill("reallife_mkt_bot_box_paid", "paid", MarketText.Money(b.PaidOut), "owed", MarketText.Money(b.PayOwed),
                        "losses", MarketText.Money(MarketBots.LossesToMakeGood(b))));
                }
            }
            BotScope scope = b != null ? b.Scope : _botScope;
            int pay = b != null ? b.PayPct : _botPay;
            BotPayEvery every = b != null ? b.PayEvery : _botEvery;
            List<int> picks = b != null ? b.Picks : _botPicks;
            box.Section("reallife_ibox_sec_plan");
            box.Line(MarketText.Fill("reallife_mkt_bot_box_plan", "scope", ScopeWords(scope, picks.Count), "pay", PayWords(pay, every)));
            box.Selector(PageKeys.Length, _botPage, i => MarketText.T(PageKeys[i]), i => UiClone.Safe(() =>
            {
                _botPage = i;
                Refresh();
            }));
            if (_botPage == PageShares)
            {
                WriteBotShares(box, s, f, b, scope, picks);
            }
            else if (_botPage == PagePayouts)
            {
                box.Selector(MarketBots.PayPcts.Length, MarketBots.IndexOf(MarketBots.PayPcts, pay),
                    i => MarketBots.PayPcts[i] == 0 ? MarketText.T("reallife_mkt_bot_set_pay_none") : MarketText.Fill("reallife_mkt_bot_set_pay", "pct", MarketBots.PayPcts[i].ToString()),
                    i => UiClone.Safe(() => SettlePayout(f, b, MarketBots.PayPcts[i], every)));
                box.Selector(EveryKeys.Length, (int)every, i => MarketText.Fill("reallife_mkt_bot_set_every", "every", MarketText.T(EveryKeys[i])),
                    i => UiClone.Safe(() => SettlePayout(f, b, pay, (BotPayEvery)i)));
            }
            else
            {
                int size = b != null ? b.Size : _botSize, stop = b != null ? b.Stop : _botStop, loss = b != null ? b.Loss : _botLoss;
                box.Selector(MarketBots.Sizes.Length, MarketBots.IndexOf(MarketBots.Sizes, size), i => MarketText.Fill("reallife_mkt_bot_set_size", "pct", MarketBots.Sizes[i].ToString()),
                    i => UiClone.Safe(() => Settle(f, b, MarketBots.Sizes[i], stop, loss)));
                box.Selector(MarketBots.Stops.Length, MarketBots.IndexOf(MarketBots.Stops, stop),
                    i => MarketBots.Stops[i] == 0 ? MarketText.T("reallife_mkt_bot_set_stop_none") : MarketText.Fill("reallife_mkt_bot_set_stop", "pct", MarketBots.Stops[i].ToString()),
                    i => UiClone.Safe(() => Settle(f, b, size, MarketBots.Stops[i], loss)));
                box.Selector(MarketBots.Losses.Length, MarketBots.IndexOf(MarketBots.Losses, loss), i => MarketText.Fill("reallife_mkt_bot_set_loss", "pct", MarketBots.Losses[i].ToString()),
                    i => UiClone.Safe(() => Settle(f, b, size, stop, MarketBots.Losses[i])));
                if (b != null && b.Status == BotStatus.Running)
                {
                    WriteBotCapital(box, f, b);
                }
            }
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

        // A running bot's capital: what it has out of shares and the most that can be taken out, a sum typed in the game's
        // own text box, and the buttons to add it or take it out (each asked first in the game's own question box).
        private void WriteBotCapital(InfoBox box, BotFirm f, Bot b)
        {
            box.Line(MarketText.Fill("reallife_mkt_bot_box_capital", "cash", MarketText.Money(Math.Max(0L, b.Cash)), "most", MarketText.Money(_director.BotMostOut(f.Id))));
            Transform row = box.ButtonRow();
            AskNumber(row, InfoBox.Additional, MarketText.Fill("reallife_mkt_bot_change", "amount", MarketText.Money(_botChange)), "reallife_mkt_bot_change_placeholder", false,
                value => _botChange = value);
            InfoBox.Button(row, InfoBox.Positive, "reallife_mkt_bot_add", () => UiClone.Safe(() => ConfirmCapital(f, true)));
            InfoBox.Button(row, InfoBox.Negative, "reallife_mkt_bot_take", () => UiClone.Safe(() => ConfirmCapital(f, false)));
        }

        // Capital added to a running bot or taken out, once the player says yes; the notification says what it trades with now.
        private void ConfirmCapital(BotFirm f, bool adding)
        {
            long amount = _botChange;
            Bot b = _director.State != null ? MarketBots.Hired(_director.State, f.Id) : null;
            long after = b != null ? (adding ? b.Capital + amount : b.Capital - amount) : 0;
            GameConfirm.Show(
                (adding ? "reallife_mkt_bot_confirm_add" : "reallife_mkt_bot_confirm_take").Localize(new { firm = f.Name, amount = MarketText.Money(amount) }),
                (adding ? "reallife_mkt_bot_confirm_add_body" : "reallife_mkt_bot_confirm_take_body").Localize(new { firm = f.Name, capital = MarketText.Money(after) }),
                () => UiClone.Safe(() =>
                {
                    BotWhy why;
                    bool done = adding ? _director.AddBotCapital(f.Id, amount, out why) : _director.TakeBotCapital(f.Id, amount, out why);
                    Bot now = done && _director.State != null ? MarketBots.Hired(_director.State, f.Id) : null;
                    if (now == null)
                    {
                        BotError(done ? BotWhy.NotHired : why);
                    }
                    else
                    {
                        Notifications.Show(NotificationType.Success, adding ? "reallife_mkt_bot_note_added" : "reallife_mkt_bot_note_taken",
                            new Dictionary<string, string> { { "firm", f.Name }, { "amount", MarketText.Money(amount) }, { "capital", MarketText.Money(now.Capital) } }, 6f, null, null, true, false);
                    }
                    Refresh();
                }), null, adding ? "reallife_mkt_bot_add" : "reallife_mkt_bot_take", "common_cancel", false, false);
        }

        // What a firm's strategy does, with its figures (MarketBots).
        private static string About(BotFirm f)
        {
            return MarketText.Fill(f.Key + "_about", "fast", MarketBots.TrendFast.ToString(), "slow", MarketBots.TrendSlow.ToString(), "in", MarketBots.RsiIn.ToString(),
                "out", MarketBots.RsiOut.ToString(), "filter", MarketBots.ReversionFilter.ToString(), "days", MarketBots.ReversionDaysMost.ToString(),
                "momentum", MarketBots.MomentumDays.ToString(), "quarters", MarketBots.IncomeQuarters.ToString());
        }

        // The shares page: what it trades, and with its picks, a picker over the player's own company, their picks, what
        // they and their bots hold and what they watch, with a button to pick the one shown or take it off.
        private void WriteBotShares(InfoBox box, MarketState s, BotFirm f, Bot b, BotScope scope, List<int> picks)
        {
            box.Selector(ScopeKeys.Length, (int)scope, i => MarketText.T(ScopeKeys[i]), i => UiClone.Safe(() =>
            {
                if (b != null)
                {
                    Done(_director.SetBotScope(f.Id, (BotScope)i, out BotWhy why), why);
                    return;
                }
                _botScope = (BotScope)i;
                Refresh();
            }));
            if (scope != BotScope.Picked)
            {
                return;
            }
            Candidates(s, picks);
            if (_botCandidates.Count == 0)
            {
                box.Line(MarketText.T("reallife_mkt_bot_pick_none"));
                return;
            }
            _botPickAt = Mathf.Clamp(_botPickAt, 0, _botCandidates.Count - 1);
            box.Selector(_botCandidates.Count, _botPickAt, i => PickWords(s, _botCandidates[i], picks), i => UiClone.Safe(() =>
            {
                _botPickAt = i;
                Refresh();
            }));
            int id = _botCandidates[_botPickAt];
            bool picked = picks.Contains(id);
            Transform row = box.ButtonRow();
            InfoBox.Button(row, picked ? InfoBox.Negative : InfoBox.Positive, picked ? "reallife_mkt_bot_unpick" : "reallife_mkt_bot_pick", () => UiClone.Safe(() =>
            {
                if (b != null)
                {
                    bool done = _director.PickForBot(f.Id, id, !picked, out BotWhy why);
                    if (!done && why != BotWhy.None)
                    {
                        BotError(why);
                    }
                    Refresh();
                    return;
                }
                if (picked)
                {
                    _botPicks.Remove(id);
                }
                else if (_botPicks.Count < MarketBots.PicksMost)
                {
                    _botPicks.Add(id);
                }
                else
                {
                    BotError(BotWhy.Picks);
                }
                Refresh();
            }));
        }

        // The picker's companies, in this order: the player's own listed company, the picks, what the player and their
        // bots hold, what they watch. Listed companies only, never another player's, each once.
        private void Candidates(MarketState s, List<int> picks)
        {
            _botCandidates.Clear();
            MarketEngine engine = _director.Engine;
            Stock own = engine != null ? engine.MineListed() : null;
            AddCandidate(s, own != null ? own.Id : 0);
            for (int i = 0; i < picks.Count; i++)
            {
                AddCandidate(s, picks[i]);
            }
            for (int i = 0; i < s.Positions.Count; i++)
            {
                if (s.Positions[i].Shares > 0)
                {
                    AddCandidate(s, s.Positions[i].StockId);
                }
            }
            for (int i = 0; i < s.Bots.Count; i++)
            {
                for (int j = 0; j < s.Bots[i].Holdings.Count; j++)
                {
                    AddCandidate(s, s.Bots[i].Holdings[j].StockId);
                }
            }
            for (int i = 0; i < s.Watch.Count; i++)
            {
                AddCandidate(s, s.Watch[i]);
            }
        }

        private void AddCandidate(MarketState s, int id)
        {
            Stock k = id > 0 ? s.Stock(id) : null;
            if (k != null && k.Listed && (k.Own == null || MarketBots.Mine(s, k)) && !_botCandidates.Contains(id))
            {
                _botCandidates.Add(id);
            }
        }

        private static string PickWords(MarketState s, int id, List<int> picks)
        {
            Stock k = s.Stock(id);
            string name = k == null ? string.Empty : MarketBots.Mine(s, k) ? MarketText.Fill("reallife_mkt_bot_pick_yours", "name", k.Name) : k.Name;
            return MarketText.Fill(picks.Contains(id) ? "reallife_mkt_bot_pick_on" : "reallife_mkt_bot_pick_off", "name", name);
        }

        private static string ScopeWords(BotScope scope, int picks)
        {
            return scope == BotScope.Picked ? MarketText.Fill("reallife_mkt_bot_plan_picked", "count", picks.ToString()) : MarketText.T(ScopeKeys[(int)scope]);
        }

        private static string PayWords(int pct, BotPayEvery every)
        {
            return pct <= 0 ? MarketText.T("reallife_mkt_bot_set_pay_none") : MarketText.Fill("reallife_mkt_bot_plan_pays", "pct", pct.ToString(), "every", MarketText.T(EveryKeys[(int)every]));
        }

        // A payout setting changed: for a bot hired, at once; for one being written, kept for the hire.
        private void SettlePayout(BotFirm f, Bot b, int pct, BotPayEvery every)
        {
            if (b != null)
            {
                Done(_director.SetBotPayout(f.Id, pct, every, out BotWhy why), why);
                return;
            }
            _botPay = pct;
            _botEvery = every;
            Refresh();
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
            int size = _botSize, stop = _botStop, loss = _botLoss, pay = _botPay;
            long capital = _botCapital;
            BotScope scope = _botScope;
            BotPayEvery every = _botEvery;
            int[] picks = _botPicks.ToArray();
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
                        // The shares it trades and its payouts, as they were written for it.
                        for (int i = 0; i < picks.Length; i++)
                        {
                            _director.PickForBot(f.Id, picks[i], true, out _);
                        }
                        _director.SetBotScope(f.Id, scope, out _);
                        _director.SetBotPayout(f.Id, pay, every, out _);
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
            "reallife_mkt_bot_why_too_little", "reallife_mkt_bot_why_no_cash", "reallife_mkt_bot_why_not_hired", "reallife_mkt_bot_why_not_stopped", "reallife_mkt_bot_why_too_little_left", "reallife_mkt_bot_why_off",
            "reallife_mkt_bot_why_own_shares", "reallife_mkt_bot_why_not_listed", "reallife_mkt_bot_why_picks", "reallife_mkt_bot_why_not_running", "reallife_mkt_bot_why_change_too_little",
            "reallife_mkt_bot_why_too_much_out"
        };

        private static void BotError(BotWhy why)
        {
            int i = Mathf.Clamp((int)why, 0, BotWhyKeys.Length - 1);
            Notifications.Show(NotificationType.Error, "reallife_mkt_bot_note_refused", new Dictionary<string, string> { { "why", MarketText.T(BotWhyKeys[i]) } }, 8f, null, null, true, false);
        }

        // ---------------------------------------------------------------- the bots' holdings on the Portfolio tab (MarketApp.Portfolio.cs)

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
