using System;
using System.Collections.Generic;
using RealLifeOverhaul.Markets;
using UI.Notification;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The Stocks app on the kit. The market's own page stays underneath for the way back. A layout only when the page,
    /// the price, the chart set or a sheet changed. Cost: one comparison a frame while it shows; a layout when that changes.
    /// What can be bought, opened or hired is a sheet over the page (StocksScreen.Sheet.cs): it asks the same director the old
    /// app asks, so the money and the state move as the engine says.
    /// </summary>
    public sealed partial class StocksScreen : IUiScreen, IUiTyping, IUiStepping
    {
        private readonly MarketDirector _market;
        private StocksPage _page = StocksPage.Market;
        private int _stock;
        private int _tool;
        private bool _buy = true;
        private OrderType _type = OrderType.Market;
        private OrderLife _life = OrderLife.Day;
        private long _qty = 100;
        private int _sort;
        private int _sector = -1;
        private int _size = -1;
        private int _focus = -1;
        private int _rule;
        private int _pane;
        private bool _done;
        private int _kind, _price;
        private float _offsetOne, _offsetTwo;
        private int _revealOne = -1, _revealTwo = -1;
        private int _listOne = -1, _clipTwo = -1;
        private string _find = string.Empty;
        private bool _dirty = true;
        private int _stamp = int.MinValue;
        private int _last = int.MinValue;
        private int _phase = int.MinValue;
        private static readonly UiWords Words = (key, english) => UiText.T(key);

        public StocksScreen(MarketDirector market)
        {
            _market = market;
        }

        public string Id => "stocks";

        public int TypingCommand
        {
            get
            {
                if (_sheet.Open)
                {
                    return _typedId >= 0 ? StocksSheetLay.Type : -1;
                }
                if (_page == StocksPage.Company)
                {
                    return StocksLay.Shares;
                }
                if (_page == StocksPage.Market || _page == StocksPage.Companies || _page == StocksPage.Watch)
                {
                    return UiDataTable.First + UiDataTable.Search;
                }
                return -1;
            }
        }

        public string TypingText => _sheet.Open ? SheetTypingText() : _page == StocksPage.Company ? _qty.ToString() : _find ?? string.Empty;

        public void Typed(string text)
        {
            if (_sheet.Open)
            {
                SheetTyped(text);
                return;
            }
            if (_page == StocksPage.Company)
            {
                _qty = StocksSheetState.Typed(text, 1, 1000000000L);
                _dirty = true;
                return;
            }
            _find = text ?? string.Empty;
            _offsetOne = 0f;
            _dirty = true;
        }

        public bool Dirty
        {
            get
            {
                if (_dirty)
                {
                    return true;
                }
                MarketState state = _market != null ? _market.State : null;
                if (state == null)
                {
                    return false;
                }
                int last = _stock > 0 && state.Stock(_stock) != null ? state.Stock(_stock).Last : state.IndexLast;
                if (last != _last || state.Phase != _phase || state.Charts.Stamp != _stamp)
                {
                    _last = last;
                    _phase = state.Phase;
                    _stamp = state.Charts.Stamp;
                    _dirty = true;
                }
                return _dirty;
            }
        }

        public void Lay(UiScene s, UiFrame frame, bool dark, IUiKeys keys)
        {
            MarketState state = _market != null ? _market.State : null;
            StocksModel m = StocksBuild.Page(state, _page, _stock, _tool, _buy, _type, _life, _qty, _sort, _sector, _size, _focus, _rule, _find, Words, _market, _done, _kind, _price);
            if (state != null && _sheet.Open)
            {
                m.Sheet = BuildSheet();
            }
            if (m.Table != null)
            {
                m.Table.Offset = _offsetOne;
                m.Table.Reveal = _revealOne;
            }
            if (m.Table2 != null)
            {
                m.Table2.Offset = _offsetTwo;
                m.Table2.Reveal = _revealTwo;
            }
            StocksLay.Lay(s, m, frame, dark, keys);
            if (m.Table != null)
            {
                _offsetOne = m.Table.Offset;
            }
            if (m.Table2 != null)
            {
                _offsetTwo = m.Table2.Offset;
            }
            _listOne = m.ListOne;
            _clipTwo = m.ClipTwo;
            _revealOne = -1;
            _revealTwo = -1;
            _dirty = false;
        }

        public bool Press(int command, int arg)
        {
            if (_sheet.Open)
            {
                return SheetPress(command, arg);
            }
            if (command == UiAppWindow.NavCommand && arg >= 0 && arg < StocksBuild.NavPages.Length)
            {
                _page = StocksBuild.NavPages[arg];
                _offsetOne = 0f;
                _offsetTwo = 0f;
                _dirty = true;
                return true;
            }
            if (command == StocksLay.Act)
            {
                return PageAction(arg);
            }
            if (command >= StocksLay.Second && command < StocksLay.Second + 10)
            {
                return command - StocksLay.Second == UiDataTable.Row && RowPressed(true, arg);
            }
            if (command == UiDataTable.First + UiDataTable.Head)
            {
                _sort = (_sort + 1) % 4;
                _dirty = true;
                return true;
            }
            if (command == UiDataTable.First + UiDataTable.Search)
            {
                return true;
            }
            if (command == UiDataTable.First + UiDataTable.Clear)
            {
                _find = string.Empty;
                _dirty = true;
                return true;
            }
            if (command == UiDataTable.First + UiDataTable.Filter)
            {
                if (_page == StocksPage.Orders)
                {
                    _done = !_done;
                    _offsetOne = 0f;
                }
                else if (_page == StocksPage.Bots)
                {
                    _rule = (_rule + 1) % 8;
                }
                else if (arg == 0)
                {
                    _sector = _sector + 1 >= (int)MarketSector.Count ? -1 : _sector + 1;
                }
                else
                {
                    _size = _size >= (int)MarketSize.Small ? -1 : _size + 1;
                }
                _dirty = true;
                return true;
            }
            if (command == UiChartDesk.Command)
            {
                return Draw(arg);
            }
            if (command == StocksLay.Alert)
            {
                return Alert(arg);
            }
            if (command == UiDataTable.First + UiDataTable.Row && _page != StocksPage.Company)
            {
                if (RowPressed(false, arg))
                {
                    return true;
                }
                if (_page == StocksPage.Portfolio)
                {
                    MarketState book = _market != null ? _market.State : null;
                    if (book != null && arg >= 0 && arg < book.Positions.Count)
                    {
                        _stock = book.Positions[arg].StockId;
                        _focus = -1;
                        _page = StocksPage.Company;
                        _dirty = true;
                        return true;
                    }
                }
                if (_page != StocksPage.Market && _page != StocksPage.Companies && _page != StocksPage.Watch)
                {
                    return true;
                }
                _stock = StockAt(arg);
                if (_stock > 0)
                {
                    _focus = -1;
                    _page = StocksPage.Company;
                    _dirty = true;
                    return true;
                }
            }
            if (command == StocksLay.Choice)
            {
                if (arg == 0)
                {
                    _buy = !_buy;
                    _price = 0;
                }
                else if (arg == 1)
                {
                    _kind = (_kind + 1) % 4;
                    _price = 0;
                    long held = _market != null && _market.State != null ? Math.Abs(_market.State.Held(_stock)) : 0;
                    if (_kind >= 2 && held > 0)
                    {
                        _qty = held;   // a stop loss or a take profit is on what is held: all of it, to begin with
                    }
                }
                else if (arg == 3)
                {
                    return CyclePutBack();
                }
                else
                {
                    _life = _life == OrderLife.Day ? OrderLife.UntilCancelled : OrderLife.Day;
                }
                _dirty = true;
                return true;
            }
            if (command == StocksLay.PriceLess || command == StocksLay.PriceMore)
            {
                MarketState book = _market != null ? _market.State : null;
                Stock quoted = book != null ? book.Stock(_stock) : null;
                if (quoted == null)
                {
                    return false;
                }
                int now = StocksBuild.TicketPrice(book, quoted, !_buy, _kind, _price);
                int step = Math.Max(1, now / 100);
                _price = Math.Max(1, command == StocksLay.PriceLess ? now - step : now + step);
                _dirty = true;
                return true;
            }
            if (command == StocksLay.PricePlain)
            {
                return true;
            }
            if (command == StocksLay.Watch)
            {
                return ToggleWatch();
            }
            if (command == StocksLay.Shares)
            {
                return true;
            }
            if (command == StocksLay.Less)
            {
                _qty = _qty > 10 ? _qty - 10 : 1;
                _dirty = true;
                return true;
            }
            if (command == StocksLay.More)
            {
                _qty += 10;
                _dirty = true;
                return true;
            }
            if (command == StocksLay.Instead)
            {
                _buy = !_buy;
                _dirty = true;
                return true;
            }
            if (command == StocksLay.Place || (command == UiDecision.ButtonCommand && _page == StocksPage.Ticket))
            {
                return Place();
            }
            if (command == StocksLay.Tool)
            {
                if (arg >= 9)
                {
                    ClearDrawings();
                    _tool = 0;
                }
                else
                {
                    _tool = arg;
                }
                _dirty = true;
                return true;
            }
            if (command == StocksLay.Chip)
            {
                ToggleChip(arg);
                _dirty = true;
                return true;
            }
            return false;
        }

        public void Focused(int command, int arg)
        {
        }

        public bool Back()
        {
            if (_sheet.Open)
            {
                return SheetBack();
            }
            if (_page == StocksPage.Ticket)
            {
                _page = StocksPage.Company;
                _dirty = true;
                return true;
            }
            if (_page == StocksPage.Company)
            {
                _page = StocksPage.Market;
                _dirty = true;
                return true;
            }
            return false;
        }

        public void Tab(int step)
        {
            if (_sheet.Open)
            {
                return;
            }
            int at = StocksBuild.NavOf(_page);
            int n = StocksBuild.NavPages.Length;
            at = (at + (step < 0 ? n - 1 : 1)) % n;
            _page = StocksBuild.NavPages[at];
            _offsetOne = 0f;
            _offsetTwo = 0f;
            _dirty = true;
        }

        public bool Act(UiAction action)
        {
            if (_sheet.Open)
            {
                return action == UiAction.Alt && SheetConfirm();
            }
            if (action == UiAction.Alt)
            {
                if (_page == StocksPage.Futures)
                {
                    return PayCall();
                }
                return Place();
            }
            if (action == UiAction.Option)
            {
                if (_page == StocksPage.Bots)
                {
                    return OpenRule(-1);
                }
                _buy = !_buy;
                _dirty = true;
                return true;
            }
            return false;
        }

        public void Reveal(int list, int index)
        {
            if (list == _listOne)
            {
                _revealOne = index;
            }
            else
            {
                _revealTwo = index;
            }
            _dirty = true;
        }

        public void Wheel(int clip, float notches)
        {
            if (_clipTwo >= 0 && clip == _clipTwo)
            {
                _offsetTwo = Math.Max(0f, _offsetTwo - notches * UiTokens.S16);
            }
            else
            {
                _offsetOne = Math.Max(0f, _offsetOne - notches * UiTokens.S16);
            }
            _dirty = true;
        }

        /// <summary>Opens one company. The test buttons use it.</summary>
        public void Open(int stockId)
        {
            _stock = stockId;
            _page = StocksPage.Company;
            _dirty = true;
        }

        /// <summary>Opens the order card for the company on screen.</summary>
        public void Order()
        {
            _page = StocksPage.Ticket;
            _dirty = true;
        }

        private bool Place()
        {
            if (_market == null || !_market.Running || _stock <= 0)
            {
                return false;
            }
            MarketState book = _market.State;
            Stock quoted = book.Stock(_stock);
            if (quoted == null)
            {
                return false;
            }
            if (quoted.Own != null && _market.Engine != null && _market.Engine.Mine(quoted))
            {
                OpenSheet(StocksSheets.Founder, (int)OwnAction.Deal);   // the founder deals by their own rules
                return true;
            }
            bool closing = _kind >= 2;
            bool buy = closing ? book.Held(_stock) < 0 : _buy;
            OrderType type = StocksBuild.TypeOf(_kind);
            int price = StocksBuild.TicketPrice(book, quoted, !buy, _kind, _price);
            OrderSide side = buy ? OrderSide.Buy : OrderSide.Sell;
            Estimate e = _market.Estimate(_stock, side, type, _qty, type == OrderType.Limit ? price : 0, type == OrderType.Stop ? price : 0, closing);
            OrderWhy why = e.Why;
            RealLifeOverhaul.Markets.Order placed = null;
            if (why == OrderWhy.TooLarge && type == OrderType.Market && !closing && Settings.MarketsLarge)
            {
                placed = _market.PlaceWorked(_stock, side, _qty, out why);
            }
            else if (why == OrderWhy.None)
            {
                placed = _market.Place(_stock, side, type, _life, _qty, type == OrderType.Limit ? price : 0, type == OrderType.Stop ? price : 0, false, out why, closing);
            }
            if (placed == null)
            {
                Notifications.Show(NotificationType.Error, "reallife_mkt_note_refused",
                    new Dictionary<string, string> { { "name", quoted.Name }, { "why", MarketText.Why(why, quoted) } }, 8f, null, null, true, false);
                return false;
            }
            if (placed.IsOpen && placed.Filled == 0)
            {
                Notifications.Show(NotificationType.Info, "reallife_mkt_note_waiting", new Dictionary<string, string> { { "name", quoted.Name } }, 6f, null, null, true, false);
            }
            _page = StocksPage.Orders;
            _done = false;
            _dirty = true;
            return true;
        }

        private bool ToggleWatch()
        {
            MarketState book = _market != null ? _market.State : null;
            if (book == null || _stock <= 0)
            {
                return false;
            }
            if (!_market.Watch(_stock, !book.Watching(_stock)))
            {
                Notifications.ShowError("reallife_mkt_note_watch_full", null, false);
                return false;
            }
            _dirty = true;
            return true;
        }

        // The share of each dividend put back into the holding: the next of the old app's choices.
        private bool CyclePutBack()
        {
            MarketState book = _market != null ? _market.State : null;
            Position p = book != null ? book.Position(_stock) : null;
            if (p == null || p.Shares <= 0)
            {
                return false;
            }
            int[] choices = InvestRules.ReinvestChoices;
            int at = Array.IndexOf(choices, InvestRules.ReinvestOf(p.Reinvest));
            if (!_market.SetReinvest(_stock, choices[(Math.Max(0, at) + 1) % choices.Length], out OrderWhy why))
            {
                Notifications.Show(NotificationType.Error, "reallife_mkt_note_refused", new Dictionary<string, string> { { "name", string.Empty },
                    { "why", MarketText.Why(why, book.Stock(_stock)) } }, 8f, null, null, true, false);
                return false;
            }
            _dirty = true;
            return true;
        }

        private int StockAt(int row)
        {
            MarketState state = _market != null ? _market.State : null;
            if (state == null || row < 0)
            {
                return 0;
            }
            var order = new int[state.Stocks.Count];
            int listed = StocksMath.Order(state.Stocks, (MarketSort)_sort, _sector, _size, order);
            int n = 0;
            for (int k = 0; k < listed; k++)
            {
                Stock s = state.Stocks[order[k]];
                if (_page == StocksPage.Watch && !state.Watching(s.Id))
                {
                    continue;
                }
                if (!StocksMath.Matches(s, _find))
                {
                    continue;
                }
                if (n == row)
                {
                    return s.Id;
                }
                n++;
            }
            return 0;
        }

        private void ToggleChip(int index)
        {
            MarketState state = _market != null ? _market.State : null;
            if (state == null)
            {
                return;
            }
            ChartBook book = state.Charts;
            if (index <= 0)
            {
                book.Frame = (book.Frame + 1) % (int)ChartFrame.Count;
                book.Touch();
                return;
            }
            if (index == 1)
            {
                book.Kind = (book.Kind + 1) % 3;
                book.Touch();
                return;
            }
            int slot = 2;
            ChartLayer[] layers = { ChartLayer.Sma20, ChartLayer.Sma50, ChartLayer.Ema20, ChartLayer.Bollinger };
            for (int i = 0; i < layers.Length; i++)
            {
                if (!book.LayerOn(layers[i]))
                {
                    continue;
                }
                if (slot == index)
                {
                    book.ToggleLayer(layers[i]);
                    return;
                }
                slot++;
            }
            if (book.FirstPane != ChartPane.None)
            {
                if (slot == index)
                {
                    book.TogglePane(book.FirstPane);
                    return;
                }
                slot++;
            }
            if (book.SecondPane != ChartPane.None)
            {
                if (slot == index)
                {
                    book.TogglePane(book.SecondPane);
                    return;
                }
                slot++;
            }
            AddIndicator(book);
        }

        private void AddIndicator(ChartBook book)
        {
            ChartLayer[] layers = { ChartLayer.Sma20, ChartLayer.Sma50, ChartLayer.Ema20, ChartLayer.Bollinger };
            for (int i = 0; i < layers.Length; i++)
            {
                if (!book.LayerOn(layers[i]))
                {
                    book.ToggleLayer(layers[i]);
                    return;
                }
            }
            ChartPane[] panes = { ChartPane.Rsi, ChartPane.Macd, ChartPane.Stochastic, ChartPane.Atr, ChartPane.Obv };
            book.TogglePane(panes[_pane % panes.Length]);
            _pane++;
        }

        private bool Draw(int shown)
        {
            MarketState state = _market != null ? _market.State : null;
            Stock stock = state != null ? state.Stock(_stock) : null;
            if (state == null || stock == null)
            {
                return false;
            }
            if (_tool <= 0)
            {
                _focus = shown;
                _dirty = true;
                return true;
            }
            if (_tool >= 8)
            {
                if (_tool == 8)
                {
                    return PlaceDrawing(state, stock, shown, ChartMark.Level, 1);
                }
                ClearDrawings();
                return true;
            }
            return PlaceDrawing(state, stock, shown, (ChartMark)(_tool - 1), 0);
        }

        private bool PlaceDrawing(MarketState state, Stock stock, int shown, ChartMark kind, byte magnet)
        {
            int at = StocksBuild.ChartIndex(state, stock, shown);
            int from = StocksBuild.ChartIndex(state, stock, Math.Max(0, shown - 4));
            _dirty = true;
            return state.Charts.AddDrawing(new ChartDrawing
            {
                StockId = stock.Id,
                Kind = kind,
                Frame = state.Charts.Frame,
                FromIndex = from,
                ToIndex = at,
                FromCents = stock.Last,
                ToCents = kind == ChartMark.Level ? stock.Last : Math.Max(1, stock.Last * 100 / 110),
                Magnet = magnet
            });
        }

        private void ClearDrawings()
        {
            MarketState state = _market != null ? _market.State : null;
            if (state != null && _stock > 0)
            {
                state.Charts.ClearDrawings(_stock);
            }
            _dirty = true;
        }

        private bool Alert(int arg)
        {
            MarketState state = _market != null ? _market.State : null;
            Stock stock = state != null ? state.Stock(_stock) : null;
            if (state == null || stock == null)
            {
                return false;
            }
            _dirty = true;
            if (arg <= 0)
            {
                return state.Charts.AddAlert(new PriceAlert { StockId = stock.Id, Kind = AlertKind.Above, Cents = stock.Last, Drawing = -1 });
            }
            int seen = 0;
            for (int i = 0; i < state.Charts.Alerts.Count; i++)
            {
                if (state.Charts.Alerts[i].StockId != stock.Id)
                {
                    continue;
                }
                seen++;
                if (seen == arg)
                {
                    return state.Charts.RemoveAlert(i);
                }
            }
            return false;
        }
    }
}
