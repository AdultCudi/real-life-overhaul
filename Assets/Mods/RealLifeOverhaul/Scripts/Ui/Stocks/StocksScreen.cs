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
    public sealed partial class StocksScreen : IUiScreen, IUiTyping, IUiStepping, IUiPointing, IUiTracking, IUiDragging
    {
        private readonly MarketDirector _market;
        private StocksPage _page = StocksPage.Market;
        private int _stock;
        private bool _buy = true;
        private OrderType _type = OrderType.Market;
        private OrderLife _life = OrderLife.Day;
        private long _qty = 100;
        private int _sort;
        private int _sector = -1;
        private int _size = -1;
        private int _rule;
        private bool _done;
        private int _kind, _price;
        private int _view, _period;   // the Portfolio's view (held, pays, account) and the period of its chart
        private float _offsetOne, _offsetTwo;
        private int _revealOne = -1, _revealTwo = -1;
        private int _listOne = -1, _clipTwo = -1;
        private string _find = string.Empty;
        private readonly HashSet<string> _folded = new HashSet<string>(StringComparer.Ordinal);   // a statement's bands the player folded, by their words, while the app lives
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
                    return _typeOn == StocksLay.PricePlain && _kind > 0 ? StocksLay.PricePlain : StocksLay.Shares;
                }
                if (_page == StocksPage.Market || _page == StocksPage.Companies || _page == StocksPage.Watch)
                {
                    return UiDataTable.First + UiDataTable.Search;
                }
                return -1;
            }
        }

        public string TypingText => _sheet.Open ? SheetTypingText() : _page == StocksPage.Company ? CompanyTypingText() : _find ?? string.Empty;

        public void Typed(string text)
        {
            if (_sheet.Open)
            {
                SheetTyped(text);
                return;
            }
            if (_page == StocksPage.Company)
            {
                CompanyTyped(text);
                return;
            }
            _find = text ?? string.Empty;
            _offsetOne = 0f;
            _dirty = true;
        }

        private static int _wantedStock;

        /// <summary>The Company app's button, or a notice, asked for a company's page: the screen opens it the next time it lays out.</summary>
        public static void Want(int stockId)
        {
            _wantedStock = stockId;
        }

        public bool Dirty
        {
            get
            {
                if (_dirty || _relay || _wantedStock > 0)
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
                return _dirty || _relay;
            }
        }

        public void Lay(UiScene s, UiFrame frame, bool dark, IUiKeys keys)
        {
            if (_wantedStock > 0)
            {
                int asked = _wantedStock;
                _wantedStock = 0;
                OpenCompany(asked);
            }
            MarketState state = _market != null ? _market.State : null;
            // A hover, a pan, a zoom or a tip lays the page out again from the model built: only a change of what it is built from builds it again.
            if (_model == null || _dirty || _sheet.Open || _model.Page != _page)
            {
                _model = StocksBuild.Page(state, _page, _stock, ChartView, _buy, _type, _life, _qty, _sort, _sector, _size, _rule, _find, Words, _market, _done, _kind, _price, _view, _period);
                if (state != null && _sheet.Open)
                {
                    _model.Sheet = BuildSheet();
                }
            }
            StocksModel m = _model;
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
            for (int g = 0; m.Statement != null && g < m.Statement.Groups.Length; g++)
            {
                m.Statement.Groups[g].Folded = _folded.Contains(m.Statement.Groups[g].Label ?? string.Empty);   // the model is built afresh: what is folded is the screen's to keep
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
            _relay = false;
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
                _view = StocksBuild.ViewHeld;   // a page opens on its first view, never stuck in the one last left
                _offsetOne = 0f;
                _offsetTwo = 0f;
                _dirty = true;
                return true;
            }
            if (command == StocksLay.Act)
            {
                return PageAction(arg);
            }
            if (command == StocksLay.View)
            {
                return SetView();
            }
            if (command == StocksLay.OrderRow)
            {
                return OpenOrderRow(arg);
            }
            if (command == UiStatement.First + UiStatement.Group)
            {
                return FoldBand(arg);
            }
            if (command == UiStatement.First + UiStatement.Line)
            {
                return _model != null && _model.Statement != null;   // a line of a statement is read, a stop on the way down the list: a press asks nothing of the market
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
                else if (_page == StocksPage.Portfolio)
                {
                    _period = (_period + 1) % StocksBuild.Periods;   // the chart's 1 month, 3 months, 1 year, all
                }
                else if (arg == 0)
                {
                    _sort = (_sort + 1) % 4;   // the first filter is the order: movers, gainers, losers, most active
                }
                else if (arg == 1)
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
            if (_page == StocksPage.Company && CompanyPress(command, arg, out bool did))
            {
                return did;
            }
            if (command == UiDataTable.First + UiDataTable.Row && _page != StocksPage.Company)
            {
                if (RowPressed(false, arg))
                {
                    return true;
                }
                if (_page == StocksPage.Portfolio)
                {
                    return PortfolioRow(arg);
                }
                if (_page != StocksPage.Market && _page != StocksPage.Companies && _page != StocksPage.Watch)
                {
                    return true;
                }
                int picked = StockAt(arg);
                if (picked > 0)
                {
                    OpenCompany(picked);
                    return true;
                }
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
            if (command == StocksLay.Place || (command == UiDecision.ButtonCommand && _page == StocksPage.Ticket))
            {
                return Place();
            }
            return false;
        }

        // A statement's band folds its lines away and brings them back, as its mark shows. The app's pages are tables today
        // (the orders, the history), so no page builds a statement; the layout keeps the part (StocksLay.Statement), and one
        // laid answers its bands instead of the error sound.
        private bool FoldBand(int group)
        {
            UiStatementModel statement = _model != null ? _model.Statement : null;
            if (statement == null || group < 0 || group >= statement.Groups.Length)
            {
                return false;
            }
            string band = statement.Groups[group].Label ?? string.Empty;
            if (!_folded.Remove(band))
            {
                _folded.Add(band);
            }
            _relay = true;   // laid again from the model built: nothing it is built from changed
            return true;
        }

        public void Focused(int command, int arg)
        {
            CompanyFocused(command, arg);
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
            if (_page == StocksPage.Portfolio && _view != StocksBuild.ViewHeld)
            {
                _view = StocksBuild.ViewHeld;   // Back steps out of the Pays and the Account views first
                _offsetOne = 0f;
                _dirty = true;
                return true;
            }
            if (_page == StocksPage.Company)
            {
                if (CompanyBack())
                {
                    return true;
                }
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
            _view = StocksBuild.ViewHeld;
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
                if (_page == StocksPage.Company)
                {
                    return ToggleBig();
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
            if (_page == StocksPage.Company && ChartWheel(clip, notches))
            {
                return;
            }
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

        // The Portfolio's view chosen: where the pointer went down along the control, or the next one when a key pressed it.
        private bool SetView()
        {
            if (_page != StocksPage.Portfolio || _model == null || _model.ViewLabels.Length < 2)
            {
                return false;
            }
            _view = Segment(StocksLay.View, _model.ViewEdges, _model.ViewLabels.Length, _view);
            _offsetOne = 0f;
            _dirty = true;
            return true;
        }

        // A row of the Portfolio: a holding opens its company; on the Pays view a payment opens what pays it; the Account view has no rows.
        private bool PortfolioRow(int arg)
        {
            MarketState book = _market != null ? _market.State : null;
            if (book == null || arg < 0)
            {
                return false;
            }
            if (_view == StocksBuild.ViewPays)
            {
                int target = StocksBuild.PaymentTarget(_market, arg);
                if (target == 0)
                {
                    return false;
                }
                if (target < 0)
                {
                    _page = StocksPage.Bonds;   // a bond is managed on the Bonds page
                    _offsetOne = 0f;
                    _dirty = true;
                    return true;
                }
                OpenCompany(target);
                return true;
            }
            if (_view == StocksBuild.ViewHeld && arg < book.Positions.Count)
            {
                OpenCompany(book.Positions[arg].StockId);
                _dirty = true;
                return true;
            }
            return false;
        }

        // A row of the Market's open orders opens that order's sheet, as a row of the Orders page does.
        private bool OpenOrderRow(int arg)
        {
            MarketState book = _market != null ? _market.State : null;
            if (book == null || _page != StocksPage.Market || arg < 0)
            {
                return false;
            }
            var ids = new List<int>(16);
            StocksBuild.OrderRows(book, false, ids);
            if (arg >= ids.Count)
            {
                return false;
            }
            OpenSheet(StocksSheets.Order, ids[arg]);
            return true;
        }

        /// <summary>Opens one company. The test buttons use it.</summary>
        public void Open(int stockId)
        {
            OpenCompany(stockId);
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
                OpenSheet(StocksSheets.Founder, (int)OwnAction.Deal, quoted.Own.Firm ? quoted.Id : 0);   // the founder deals by their own rules, in a firm of theirs as in their company
                return true;
            }
            bool closing = _kind >= 2;
            bool buy = closing ? book.Held(_stock) < 0 : _buy;
            OrderType type = StocksBuild.TypeOf(_kind);
            int price = StocksBuild.TicketPrice(book, quoted, !buy, _kind, _price);
            OrderSide side = buy ? OrderSide.Buy : OrderSide.Sell;
            Estimate e = _market.Estimate(_stock, side, type, _qty, type == OrderType.Limit ? price : 0, type == OrderType.Stop ? price : 0, closing);
            if (e.Why != OrderWhy.None && !StocksBuild.WorkedOrder(e, type, closing))
            {
                Notifications.Show(NotificationType.Error, "reallife_mkt_note_refused",
                    new Dictionary<string, string> { { "name", quoted.Name }, { "why", MarketText.Why(e.Why, quoted) } }, 8f, null, null, true, false);
                return false;
            }
            // The old app asked before every order, with what it comes to: so does the sheet, and the order goes when it is confirmed.
            if (_page == StocksPage.Ticket)
            {
                _page = StocksPage.Company;   // the decision card has no room for a sheet
            }
            OpenSheet(StocksSheets.Ticket, 0);
            long[] v = _sheet.V;
            v[StocksSheets.TStock] = _stock;
            v[StocksSheets.TBuy] = _buy ? 1 : 0;
            v[StocksSheets.TKind] = _kind;
            v[StocksSheets.TPrice] = _price;
            v[StocksSheets.TLife] = _life == OrderLife.UntilCancelled ? 1 : 0;
            v[StocksSheets.TQty] = _qty;
            return true;
        }

        // The ticket sheet's Ok: the order it asked about is placed, as it stands now.
        private bool PlaceTicket()
        {
            StocksBuild.TicketSlots(_sheet, out int stockId, out bool wantBuy, out int kind, out int typed, out OrderLife life, out long qty);
            MarketState book = _market != null && _market.Running ? _market.State : null;
            Stock quoted = book != null ? book.Stock(stockId) : null;
            if (quoted == null)
            {
                return false;
            }
            bool closing = kind >= 2;
            bool buy = closing ? book.Held(stockId) < 0 : wantBuy;
            OrderType type = StocksBuild.TypeOf(kind);
            int price = StocksBuild.TicketPrice(book, quoted, !buy, kind, typed);
            OrderSide side = buy ? OrderSide.Buy : OrderSide.Sell;
            Estimate e = _market.Estimate(stockId, side, type, qty, type == OrderType.Limit ? price : 0, type == OrderType.Stop ? price : 0, closing);
            OrderWhy why = e.Why;
            RealLifeOverhaul.Markets.Order placed = null;
            if (StocksBuild.WorkedOrder(e, type, closing))
            {
                placed = _market.PlaceWorked(stockId, side, qty, out why);
                if (placed != null)
                {
                    Told(NotificationType.Info, "reallife_mkt_inv_note_placed", "name", quoted.Name, "count", qty.ToString("N0"));
                }
            }
            else if (why == OrderWhy.None)
            {
                placed = _market.Place(stockId, side, type, life, qty, type == OrderType.Limit ? price : 0, type == OrderType.Stop ? price : 0, false, out why, closing);
                if (placed != null && placed.IsOpen && placed.Filled == 0)
                {
                    Notifications.Show(NotificationType.Info, "reallife_mkt_note_waiting", new Dictionary<string, string> { { "name", quoted.Name } }, 6f, null, null, true, false);
                }
            }
            if (placed == null)
            {
                Notifications.Show(NotificationType.Error, "reallife_mkt_note_refused",
                    new Dictionary<string, string> { { "name", quoted.Name }, { "why", MarketText.Why(why, quoted) } }, 8f, null, null, true, false);
                return false;
            }
            _sheet.Close();
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
    }
}
