using System;
using System.Collections.Generic;
using RealLifeOverhaul.GameUi;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Markets
{
    /// <summary>
    /// Part of the Stocks app: the Futures tab. The raw materials on the exchange with this week's and the coming weeks'
    /// prices, the companies options are written on, and the player's contracts; picking one writes its ticket in the white
    /// box with the game's own selectors and buttons. Written when the tab is shown or something changes, never per frame.
    /// </summary>
    public sealed partial class MarketApp
    {
        public const int TabFutures = 8;
        private const string MaterialRow = "dm:", ShareRow = "dc:", ContractRow = "dk:";
        private const int RowMaterials = 0, RowCompanies = 1, RowContracts = 2;
        private const int QtyStart = 2, StrikeAt = 2;

        private static readonly string[] MaterialWhatKeys = { "reallife_mkt_drv_what_buy", "reallife_mkt_drv_what_sell", "reallife_mkt_drv_what_call", "reallife_mkt_drv_what_put" };
        private static readonly string[] ShareWhatKeys = { "reallife_mkt_drv_what_call", "reallife_mkt_drv_what_put" };

        private readonly int[] _drvDays = new int[DerivCatalogue.WeeksAhead];
        private readonly List<string> _placeKeys = new List<string>(8), _placeNames = new List<string>(8);
        private string _drvFor;          // the row the ticket is written for; another resets it
        private int _drvWhat, _drvWeek, _drvStrike = StrikeAt, _drvQty = QtyStart, _drvPlace;

        private TableSpec WriteFutures(InfoBox box, MarketState s)
        {
            int days = _director.ContractDays(_drvDays);
            DerivBook book = s.Derivs;
            long worth = 0, gain = 0;
            int open = 0;
            for (int i = 0; i < book.Contracts.Count; i++)
            {
                DerivContract c = book.Contracts[i];
                TableRow row = Row(ContractRow, c.Id);
                row.Kind = RowContracts;
                row.Kind2 = -1;
                row.Set(0, DerivText.Name(c));
                row.Set(1, c.IsOpen ? DerivText.Kind(c.Kind) : DerivText.End(c.End));
                row.Set(2, DerivText.Unit(c.Price), c.Price / (double)DerivCatalogue.Micro);
                row.Set(3, string.Empty, double.NaN);
                row.Set(4, string.Empty, double.NaN);
                row.Set(5, MarketText.Fill("reallife_mkt_day", "day", c.SettleDay.ToString()), c.SettleDay);
                row.Set(6, Count(c.Qty), c.Qty);
                long now = c.IsOpen ? ContractWorth(c) : 0;
                long made = c.IsOpen ? ContractGain(c, now) : c.Result / 100;
                row.Set(7, c.IsOpen ? MarketText.Money(now) : string.Empty, c.IsOpen ? now : double.NaN);
                row.Set(8, DerivText.Signed(made), made, Tone(Math.Sign(made)));
                if (c.IsOpen)
                {
                    open++;
                    worth += now;
                    gain += made;
                }
            }
            for (int i = 0; i < s.Materials.Count; i++)
            {
                RawMaterial m = s.Materials[i];
                TableRow row = Row(MaterialRow, i);
                row.Kind = RowMaterials;
                row.Kind2 = (int)m.Group;
                bool uses = _director.Uses(m.Name);
                row.Set(0, MaterialGame.Name(m.Name) + (uses ? InvestText.Mark(MarketText.T("reallife_mkt_drv_row_uses")) : string.Empty));
                row.Set(1, DerivText.Group(m.Group));
                row.Set(2, DerivText.Unit(m.Fix), m.Fix / (double)DerivCatalogue.Micro);
                int day = MaterialRules.MoveBp(m, 1), week = MaterialRules.MoveBp(m, 5);
                row.Set(3, MarketText.Change(day), day, Tone(day));
                row.Set(4, MarketText.Change(week), week, Tone(week));
                if (days > 0)
                {
                    DerivQuote q = _director.QuoteFuture(m.Name, _drvDays[0], true, 1);
                    row.Set(5, DerivText.Unit(q.Fair), q.Fair / (double)DerivCatalogue.Micro);
                }
                else
                {
                    row.Set(5, string.Empty, double.NaN);
                }
                long units = DerivRules.UnitsOn(book, m.Name);
                row.Set(6, units > 0 ? Count(units) : string.Empty, units);
                row.Set(7, string.Empty, double.NaN);
                row.Set(8, string.Empty, double.NaN);
            }
            for (int i = 0; i < s.Stocks.Count; i++)
            {
                Stock k = s.Stocks[i];
                if (DerivRules.ShareOffered(k, s.Day) != DerivWhy.None)
                {
                    continue;
                }
                TableRow row = Row(ShareRow, k.Id);
                row.Kind = RowCompanies;
                row.Kind2 = -1;
                row.Set(0, k.Name + " (" + k.Ticker + ")");
                row.Set(1, MarketText.Sector(k.Sector));
                row.Set(2, MarketText.Price(k.Last), k.Last / 100.0);
                int move = MarketHistory.DayMoveBp(k);
                row.Set(3, MarketText.Change(move), move, Tone(move));
                row.Set(4, string.Empty, double.NaN);
                row.Set(5, string.Empty, double.NaN);
                long shares = DerivRules.SharesOn(book, k.Id);
                row.Set(6, shares > 0 ? Count(shares) : string.Empty, shares);
                row.Set(7, string.Empty, double.NaN);
                row.Set(8, string.Empty, double.NaN);
            }
            int material = Picked(_picked, MaterialRow), share = Picked(_picked, ShareRow), contract = Picked(_picked, ContractRow);
            DerivContract mine = contract != int.MinValue ? book.Contract(contract) : null;
            if (material != int.MinValue && material >= 0 && material < s.Materials.Count)
            {
                WriteMaterialTicket(box, s, s.Materials[material], days);
            }
            else if (share != int.MinValue && s.Stock(share) != null)
            {
                WriteShareTicket(box, s, s.Stock(share), days);
            }
            else if (mine != null)
            {
                WriteContract(box, s, mine);
            }
            else
            {
                WriteFuturesBox(box, s, open, worth, gain);
            }
            return MarketTables.Futures;
        }

        // What an open contract is worth now in money: a future its move since the last close, an option at its bid.
        private long ContractWorth(DerivContract c)
        {
            long price = _director.ClosePrice(c);
            if (c.IsFuture)
            {
                return price > 0 ? DerivRules.Move(c.Kind, c.Qty, c.Mark, price) / 100 : 0;
            }
            return DerivRules.Money(c.Qty, price, false);
        }

        // What an open contract has made so far: its money settled, its premium and commission, and what it is worth now.
        private static long ContractGain(DerivContract c, long worth)
        {
            return c.Result / 100 + worth;
        }

        private void WriteFuturesBox(InfoBox box, MarketState s, int open, long worth, long gain)
        {
            DerivBook b = s.Derivs;
            box.Title(MarketText.T("reallife_mkt_tab_futures"));
            StatusLine(box);
            box.Section("reallife_ibox_sec_now");
            box.Line(MarketText.Fill("reallife_mkt_drv_box_exchange", "count", s.Materials.Count.ToString()));
            if (!_director.MaterialsLive)
            {
                box.Line(MarketText.T("reallife_mkt_drv_box_no_city"), CellTone.Yellow);
            }
            if (open > 0)
            {
                box.Line(MarketText.Fill("reallife_mkt_drv_box_open", "count", open.ToString(), "worth", MarketText.Money(worth), "gain", DerivText.Signed(gain)));
            }
            else
            {
                box.Line(MarketText.T("reallife_mkt_drv_box_none"));
            }
            long need = DerivRules.Needed(b);
            if (need > 0 || b.Balance != 0)
            {
                box.Line(MarketText.Fill("reallife_mkt_drv_box_account", "balance", MarketText.Money(b.Balance / 100), "need", MarketText.Money(need / 100), "keep",
                    MarketText.Money(DerivRules.Maintenance(b) / 100)));
            }
            if (b.CallDay > 0)
            {
                box.Line(MarketText.Fill("reallife_mkt_drv_box_call", "amount", MarketText.Money(b.CallAmount), "day", b.CallDay.ToString()), CellTone.Red);
                Transform buttons = box.ButtonRow();
                InfoBox.Button(buttons, InfoBox.Positive, "reallife_mkt_drv_pay_call", () => UiClone.Safe(PayCall));
            }
            box.Note(MarketText.T(Settings.MarketsDerivatives ? "reallife_mkt_drv_box_help" : "reallife_mkt_drv_box_off"));
            Meta.Guide.Point(box, "reallife_meta_title_markets_derivatives");
        }

        private void PayCall()
        {
            if (!_director.PayCall(out long paid, out DerivWhy why))
            {
                DerivError(why);
            }
            else
            {
                Notifications.Show(NotificationType.Success, "reallife_mkt_drv_note_paid", new Dictionary<string, string> { { "amount", MarketText.Money(paid) } }, 6f, null, null, true, false);
            }
            Refresh();
        }

        // A new row picked: the ticket begins again from its defaults.
        private void TicketFor(string rowId)
        {
            if (_drvFor == rowId)
            {
                return;
            }
            _drvFor = rowId;
            _drvWhat = 0;
            _drvWeek = 0;
            _drvStrike = StrikeAt;
            _drvQty = QtyStart;
            _drvPlace = 0;
        }

        private void WriteMaterialTicket(InfoBox box, MarketState s, RawMaterial m, int days)
        {
            TicketFor(_picked);
            string name = MaterialGame.Name(m.Name);
            box.Title(name);
            box.Chip(DerivText.Group(m.Group));
            StatusLine(box);
            box.Section("reallife_ibox_sec_now");
            box.Line(MarketText.Fill("reallife_mkt_drv_mat_price", "price", DerivText.Unit(m.Fix), "move", MarketText.Change(MaterialRules.MoveBp(m, 1))));
            if (_director.Uses(m.Name))
            {
                box.Line(MarketText.T("reallife_mkt_drv_mat_uses"), CellTone.Green);
            }
            for (int w = 0; w < days; w++)
            {
                DerivQuote f = _director.QuoteFuture(m.Name, _drvDays[w], true, 1);
                box.RowWords(MarketText.Fill("reallife_mkt_drv_mat_week", "day", DerivRules.WeekFrom(_drvDays[w]).ToString()), DerivText.Unit(f.Fair));
            }
            if (days == 0)
            {
                box.Line(DerivText.Why(DerivWhy.Closed), CellTone.Red);
                return;
            }
            box.Section("reallife_ibox_sec_terms");
            _drvWhat = Mathf.Clamp(_drvWhat, 0, MaterialWhatKeys.Length - 1);
            box.Selector(MaterialWhatKeys.Length, _drvWhat, i => MarketText.T(MaterialWhatKeys[i]), i => UiClone.Safe(() =>
            {
                _drvWhat = i;
                Refresh();
            }));
            WeekSelector(box, days);
            bool option = _drvWhat >= 2;
            long qty = DerivCatalogue.MaterialQuantities[Mathf.Clamp(_drvQty, 0, DerivCatalogue.MaterialQuantities.Length - 1)];
            int settle = _drvDays[Mathf.Clamp(_drvWeek, 0, days - 1)];
            ContractKind kind = (ContractKind)_drvWhat;
            DerivQuote q = option ? _director.QuoteOption(kind == ContractKind.Call, m.Name, 0, settle, StrikeStep(), qty) : _director.QuoteFuture(m.Name, settle, kind == ContractKind.Long, qty);
            if (option)
            {
                StrikeSelector(box, q.Forward);
            }
            QtySelector(box, DerivCatalogue.MaterialQuantities, "reallife_mkt_drv_qty");
            string placeKey = null, placeName = null;
            if (kind == ContractKind.Long)
            {
                _director.Places(_placeKeys, _placeNames);
                _drvPlace = Mathf.Clamp(_drvPlace, 0, _placeKeys.Count);
                box.Selector(_placeKeys.Count + 1, _drvPlace, i => i == 0 ? MarketText.T("reallife_mkt_drv_place_cash") : MarketText.Fill("reallife_mkt_drv_place", "place", _placeNames[i - 1]),
                    i => UiClone.Safe(() =>
                    {
                        _drvPlace = i;
                        Refresh();
                    }));
                if (_drvPlace > 0)
                {
                    placeKey = _placeKeys[_drvPlace - 1];
                    placeName = _placeNames[_drvPlace - 1];
                }
            }
            QuoteLines(box, kind, q, qty, settle, placeName);
            DerivWhy why = _director.CanBuy(kind, m.Name, 0, settle, qty, q);
            if (why != DerivWhy.None)
            {
                box.Line(DerivText.Why(why), CellTone.Red);
            }
            Transform buttons = box.ButtonRow();
            string material = m.Name;
            InfoBox.Button(buttons, InfoBox.Positive, kind == ContractKind.Short ? "reallife_mkt_drv_sell" : "reallife_mkt_drv_buy",
                () => UiClone.Safe(() => ConfirmContract(kind, material, 0, settle, StrikeStep(), qty, placeKey, placeName)));
            Meta.Guide.Point(box, "reallife_meta_title_markets_derivatives");
        }

        private void WriteShareTicket(InfoBox box, MarketState s, Stock k, int days)
        {
            TicketFor(_picked);
            box.Title(k.Name);
            box.Chip(k.Ticker);
            StatusLine(box);
            box.Section("reallife_ibox_sec_now");
            box.Line(MarketText.Fill("reallife_mkt_drv_share_price", "price", MarketText.Price(k.Last), "move", MarketText.Change(MarketHistory.DayMoveBp(k))));
            if (days == 0)
            {
                box.Line(DerivText.Why(DerivWhy.Closed), CellTone.Red);
                return;
            }
            box.Section("reallife_ibox_sec_terms");
            _drvWhat = Mathf.Clamp(_drvWhat, 0, ShareWhatKeys.Length - 1);
            box.Selector(ShareWhatKeys.Length, _drvWhat, i => MarketText.T(ShareWhatKeys[i]), i => UiClone.Safe(() =>
            {
                _drvWhat = i;
                Refresh();
            }));
            WeekSelector(box, days);
            ContractKind kind = _drvWhat == 0 ? ContractKind.Call : ContractKind.Put;
            long qty = DerivCatalogue.ShareQuantities[Mathf.Clamp(_drvQty, 0, DerivCatalogue.ShareQuantities.Length - 1)];
            int settle = _drvDays[Mathf.Clamp(_drvWeek, 0, days - 1)];
            DerivQuote q = _director.QuoteOption(kind == ContractKind.Call, null, k.Id, settle, StrikeStep(), qty);
            StrikeSelector(box, (long)Math.Round(k.Mid * DerivCatalogue.Micro));
            QtySelector(box, DerivCatalogue.ShareQuantities, "reallife_mkt_drv_qty_shares");
            QuoteLines(box, kind, q, qty, settle, null);
            box.Note(MarketText.Fill("reallife_mkt_drv_share_settle", "day", settle.ToString(), "minutes", (DerivCatalogue.SettleSteps * MarketCatalogue.StepMinutes).ToString()));
            DerivWhy why = _director.CanBuy(kind, null, k.Id, settle, qty, q);
            if (why != DerivWhy.None)
            {
                box.Line(DerivText.Why(why), CellTone.Red);
            }
            Transform buttons = box.ButtonRow();
            int stock = k.Id;
            InfoBox.Button(buttons, InfoBox.Positive, "reallife_mkt_drv_buy", () => UiClone.Safe(() => ConfirmContract(kind, null, stock, settle, StrikeStep(), qty, null, null)));
            Meta.Guide.Point(box, "reallife_meta_title_markets_derivatives");
        }

        private int StrikeStep()
        {
            return DerivCatalogue.StrikeStepsBp[Mathf.Clamp(_drvStrike, 0, DerivCatalogue.StrikeStepsBp.Length - 1)];
        }

        private void WeekSelector(InfoBox box, int days)
        {
            _drvWeek = Mathf.Clamp(_drvWeek, 0, days - 1);
            box.Selector(days, _drvWeek, i => MarketText.Fill("reallife_mkt_drv_week", "day", _drvDays[i].ToString(), "from", DerivRules.WeekFrom(_drvDays[i]).ToString()),
                i => UiClone.Safe(() =>
                {
                    _drvWeek = i;
                    Refresh();
                }));
        }

        private void StrikeSelector(InfoBox box, long priceMicros)
        {
            int[] steps = DerivCatalogue.StrikeStepsBp;
            _drvStrike = Mathf.Clamp(_drvStrike, 0, steps.Length - 1);
            box.Selector(steps.Length, _drvStrike, i =>
            {
                string price = DerivText.Unit(DerivRules.Strike(priceMicros, steps[i]));
                return steps[i] == 0 ? MarketText.Fill("reallife_mkt_drv_strike_at", "price", price)
                    : MarketText.Fill(steps[i] < 0 ? "reallife_mkt_drv_strike_under" : "reallife_mkt_drv_strike_over", "pct", MarketText.Percent(Math.Abs(steps[i])), "price", price);
            }, i => UiClone.Safe(() =>
            {
                _drvStrike = i;
                Refresh();
            }));
        }

        private void QtySelector(InfoBox box, long[] choices, string key)
        {
            _drvQty = Mathf.Clamp(_drvQty, 0, choices.Length - 1);
            box.Selector(choices.Length, _drvQty, i => MarketText.Fill(key, "count", Count(choices[i])), i => UiClone.Safe(() =>
            {
                _drvQty = i;
                Refresh();
            }));
        }

        // What a ticket would buy: its price, what it takes from the cash, and what it does at its day.
        private void QuoteLines(InfoBox box, ContractKind kind, in DerivQuote q, long qty, int settle, string place)
        {
            if (q.Why != DerivWhy.None && q.Fair <= 0)
            {
                return;
            }
            if (kind == ContractKind.Long || kind == ContractKind.Short)
            {
                long price = kind == ContractKind.Long ? q.Ask : q.Bid;
                box.Line(MarketText.Fill(kind == ContractKind.Long ? "reallife_mkt_drv_quote_buy" : "reallife_mkt_drv_quote_sell", "count", Count(qty), "price", DerivText.Unit(price),
                    "value", MarketText.Money(q.Value), "margin", MarketText.Money(q.Margin), "fee", MarketText.Money(q.Fee)), CellTone.Green);
                box.Note(GoingPublic.InSession && kind == ContractKind.Long ? MarketText.T("reallife_mkt_drv_quote_session")
                    : place != null ? MarketText.Fill("reallife_mkt_drv_quote_deliver", "day", settle.ToString(), "place", place, "price", DerivText.Unit(price))
                    : MarketText.Fill("reallife_mkt_drv_quote_cash", "day", settle.ToString(), "price", DerivText.Unit(price)));
            }
            else
            {
                box.Line(MarketText.Fill(kind == ContractKind.Call ? "reallife_mkt_drv_quote_call" : "reallife_mkt_drv_quote_put", "strike", DerivText.Unit(q.Strike), "day", settle.ToString(),
                    "premium", DerivText.Unit(q.Ask), "value", MarketText.Money(q.Value), "count", Count(qty), "fee", MarketText.Money(q.Fee)), CellTone.Green);
            }
            box.Note(MarketText.Fill("reallife_mkt_drv_quote_fair", "day", settle.ToString(), "forward", DerivText.Unit(q.Forward), "spread", MarketText.Percent((int)Math.Round(q.Spread * 10000.0))));
        }

        private void ConfirmContract(ContractKind kind, string material, int stock, int settle, int step, long qty, string placeKey, string placeName)
        {
            bool future = kind == ContractKind.Long || kind == ContractKind.Short;
            DerivQuote q = future ? _director.QuoteFuture(material, settle, kind == ContractKind.Long, qty) : _director.QuoteOption(kind == ContractKind.Call, material, stock, settle, step, qty);
            DerivWhy why = _director.CanBuy(kind, material, stock, settle, qty, q);   // as it is now, not as it was drawn
            if (why != DerivWhy.None)
            {
                DerivError(why);
                return;
            }
            string what = stock > 0 ? (_director.State != null && _director.State.Stock(stock) != null ? _director.State.Stock(stock).Name : string.Empty) : MaterialGame.Name(material);
            string body = future
                ? MarketText.Fill("reallife_mkt_drv_confirm_future", "kind", DerivText.Kind(kind), "count", Count(qty), "material", what, "price",
                    DerivText.Unit(kind == ContractKind.Long ? q.Ask : q.Bid), "day", settle.ToString(), "margin", MarketText.Money(q.Margin), "fee", MarketText.Money(q.Fee),
                    "place", placeName ?? MarketText.T("reallife_mkt_drv_place_cash"))
                : MarketText.Fill("reallife_mkt_drv_confirm_option", "kind", DerivText.Kind(kind), "count", Count(qty), "name", what, "strike", DerivText.Unit(q.Strike), "day", settle.ToString(),
                    "value", MarketText.Money(q.Value), "fee", MarketText.Money(q.Fee));
            GameConfirm.Show(MarketText.T("reallife_mkt_drv_confirm_title"), body, () => UiClone.Safe(() =>
            {
                DerivContract c = _director.Buy(kind, material, stock, settle, step, qty, placeKey, placeName, out DerivWhy refused);
                if (c == null)
                {
                    DerivError(refused);
                }
                else
                {
                    Notifications.Show(NotificationType.Success, "reallife_mkt_drv_note_bought", new Dictionary<string, string> { { "name", DerivText.Name(c) } }, 6f, null, null, true, false);
                    _picked = ContractRow + c.Id;
                }
                Refresh();
            }), null, future && kind == ContractKind.Short ? "reallife_mkt_drv_sell" : "reallife_mkt_drv_buy", "common_cancel", false, false);
        }

        private void WriteContract(InfoBox box, MarketState s, DerivContract c)
        {
            string name = DerivText.Name(c);
            box.Title(name);
            box.Chip(c.IsOpen ? DerivText.Kind(c.Kind) : DerivText.End(c.End), c.IsOpen ? CellTone.Plain : c.Result >= 0 ? CellTone.Green : CellTone.Red);
            StatusLine(box);
            if (!c.IsOpen)
            {
                box.Line(MarketText.Fill("reallife_mkt_drv_held_ended", "end", DerivText.End(c.End), "day", c.EndDay.ToString(), "price", DerivText.Unit(c.EndPrice), "result",
                    DerivText.Signed(c.Result / 100)), c.Result >= 0 ? CellTone.Green : CellTone.Red);
                if (c.Delivered > 0)
                {
                    box.Line(MarketText.Fill("reallife_mkt_drv_held_delivered", "count", Count(c.Delivered), "place", DerivText.Place(c)));
                }
                Meta.Guide.Point(box, "reallife_meta_title_markets_derivatives");
                return;
            }
            long price = _director.ClosePrice(c);
            long worth = ContractWorth(c);
            if (c.IsFuture)
            {
                box.Line(MarketText.Fill("reallife_mkt_drv_held_future", "count", Count(c.Qty), "price", DerivText.Unit(c.Price), "now", DerivText.Unit(c.Mark), "result",
                    DerivText.Signed(c.Result / 100), "margin", MarketText.Money(c.Margin / 100)));
                box.Note(GoingPublic.InSession ? MarketText.T("reallife_mkt_drv_quote_session") : c.Warehouse.Length > 0
                    ? MarketText.Fill("reallife_mkt_drv_quote_deliver", "day", c.SettleDay.ToString(), "place", c.PlaceName, "price", DerivText.Unit(c.Price))
                    : MarketText.Fill("reallife_mkt_drv_quote_cash", "day", c.SettleDay.ToString(), "price", DerivText.Unit(c.Price)));
            }
            else
            {
                box.Line(MarketText.Fill("reallife_mkt_drv_held_option", "count", Count(c.Qty), "strike", DerivText.Unit(c.Price), "premium", DerivText.Unit(c.Premium), "bid", DerivText.Unit(price),
                    "worth", MarketText.Money(worth), "day", c.SettleDay.ToString()));
            }
            Transform buttons = box.ButtonRow();
            int id = c.Id;
            InfoBox.Button(buttons, InfoBox.Negative, c.IsFuture ? "reallife_mkt_drv_close" : "reallife_mkt_drv_sell_back", () => UiClone.Safe(() => ConfirmClose(id)));
            Meta.Guide.Point(box, "reallife_meta_title_markets_derivatives");
        }

        private void ConfirmClose(int id)
        {
            MarketState s = _director.State;
            DerivContract c = s != null ? s.Derivs.Contract(id) : null;
            if (c == null || !c.IsOpen)
            {
                DerivError(c == null ? DerivWhy.NotHeld : DerivWhy.Ended);
                return;
            }
            long price = _director.ClosePrice(c);
            long result = ContractGain(c, ContractWorth(c));
            string name = DerivText.Name(c);
            GameConfirm.Show(MarketText.Fill("reallife_mkt_drv_confirm_close", "name", name),
                MarketText.Fill("reallife_mkt_drv_confirm_close_body", "price", DerivText.Unit(price), "result", DerivText.Signed(result)), () => UiClone.Safe(() =>
                {
                    if (!_director.CloseContract(id, out _, out DerivWhy why))
                    {
                        DerivError(why);
                    }
                    else
                    {
                        DerivContract ended = _director.State != null ? _director.State.Derivs.Contract(id) : null;
                        Notifications.Show(NotificationType.Success, "reallife_mkt_drv_note_closed",
                            new Dictionary<string, string> { { "name", name }, { "result", DerivText.Signed(ended != null ? ended.Result / 100 : result) } }, 6f, null, null, true, false);
                    }
                    Refresh();
                }), null, c.IsFuture ? "reallife_mkt_drv_close" : "reallife_mkt_drv_sell_back", "common_cancel", false, true);
        }

        private static void DerivError(DerivWhy why)
        {
            Notifications.Show(NotificationType.Error, "reallife_mkt_drv_note_refused", new Dictionary<string, string> { { "why", DerivText.Why(why) } }, 8f, null, null, true, false);
        }

        // The Portfolio tab's line for the futures and options, so what is held there is seen with the rest.
        private void DerivsLine(InfoBox box, MarketState s)
        {
            int open = DerivRules.OpenCount(s.Derivs);
            if (open == 0 && s.Derivs.Balance == 0)
            {
                return;
            }
            box.Line(MarketText.Fill("reallife_mkt_drv_account_line", "count", open.ToString(), "worth", MarketText.Money(_director.DerivWorth())));
        }
    }
}
