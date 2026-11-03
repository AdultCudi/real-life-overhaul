using System;
using RealLifeOverhaul.Markets;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The Stocks app on the kit. The market's own page stays underneath for the way back. A layout only when the page,
    /// the price or the chart set changed. Cost: one comparison a frame while it shows; a layout when that changes.
    /// </summary>
    public sealed class StocksScreen : IUiScreen, IUiTyping
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

        public string TypingText => _page == StocksPage.Company ? _qty.ToString() : _find ?? string.Empty;

        public void Typed(string text)
        {
            if (_page == StocksPage.Company)
            {
                long n = 0;
                if (text != null)
                {
                    for (int i = 0; i < text.Length; i++)
                    {
                        char c = text[i];
                        if (c >= '0' && c <= '9' && n < 1000000000L)
                        {
                            n = n * 10L + (c - '0');
                        }
                    }
                }
                _qty = n < 1 ? 1 : n;
                _dirty = true;
                return;
            }
            _find = text ?? string.Empty;
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
            StocksModel m = StocksBuild.Page(_market != null ? _market.State : null, _page, _stock, _tool, _buy, _type, _life, _qty, _sort, _sector, _size, _focus, _rule, _find, Words);
            StocksLay.Lay(s, m, frame, dark, keys);
            _dirty = false;
        }

        public bool Press(int command, int arg)
        {
            if (command == UiAppWindow.NavCommand && arg >= 0 && arg < StocksBuild.NavPages.Length)
            {
                _page = StocksBuild.NavPages[arg];
                _dirty = true;
                return true;
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
            if (command == UiDataTable.First + UiDataTable.Filter)
            {
                if (_page == StocksPage.Bots)
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
                }
                else if (arg == 1)
                {
                    _type = _type == OrderType.Market ? OrderType.Limit : _type == OrderType.Limit ? OrderType.Stop : OrderType.Market;
                }
                else
                {
                    _life = _life == OrderLife.Day ? OrderLife.UntilCancelled : OrderLife.Day;
                }
                _dirty = true;
                return true;
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
            int at = StocksBuild.NavOf(_page);
            int n = StocksBuild.NavPages.Length;
            at = (at + (step < 0 ? n - 1 : 1)) % n;
            _page = StocksBuild.NavPages[at];
            _dirty = true;
        }

        public bool Act(UiAction action)
        {
            if (action == UiAction.Alt)
            {
                return Place();
            }
            if (action == UiAction.Option)
            {
                if (_page == StocksPage.Bots)
                {
                    return AddRule();
                }
                _buy = !_buy;
                _dirty = true;
                return true;
            }
            return false;
        }

        public void Reveal(int list, int index)
        {
        }

        public void Wheel(int clip, float notches)
        {
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
            Stock quoted = _market.State.Stock(_stock);
            RealLifeOverhaul.Markets.Order placed = _market.Place(_stock, _buy ? OrderSide.Buy : OrderSide.Sell, _type, _life, _qty,
                quoted != null ? quoted.Last : 0, 0, false, out OrderWhy _);
            _page = StocksPage.Orders;
            _dirty = true;
            return placed != null;
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
                if (!string.IsNullOrEmpty(_find) && (s.Name == null || s.Name.IndexOf(_find.Trim(), StringComparison.OrdinalIgnoreCase) < 0)
                    && (s.Ticker == null || s.Ticker.IndexOf(_find.Trim(), StringComparison.OrdinalIgnoreCase) < 0))
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

        private bool AddRule()
        {
            MarketState state = _market != null ? _market.State : null;
            if (state == null || state.Rules.Count >= RuleBots.Most)
            {
                return false;
            }
            Stock stock = state.Stock(_stock);
            if (stock == null || !stock.Listed)
            {
                for (int i = 0; i < state.Stocks.Count; i++)
                {
                    if (state.Stocks[i].Listed)
                    {
                        stock = state.Stocks[i];
                        break;
                    }
                }
            }
            if (stock == null)
            {
                return false;
            }
            long budget = Math.Max(1000L, stock.Last * 10L);
            state.Rules.Add(new RuleBot
            {
                Id = state.Rules.Count + 1,
                Kind = (RuleKind)(_rule % 8),
                Owner = 0,
                StockId = stock.Id,
                Level = _rule % 8 == (int)RuleKind.Dip ? 30 : stock.Last,
                GainBp = 800,
                Type = OrderType.Market,
                Budget = budget,
                Cash = budget,
                PerTrade = 10,
                StopBp = 500,
                OpenOnly = true,
                Tell = true,
                MaxOpen = 1,
                Status = RuleStatus.Running
            });
            _dirty = true;
            return true;
        }
    }
}
