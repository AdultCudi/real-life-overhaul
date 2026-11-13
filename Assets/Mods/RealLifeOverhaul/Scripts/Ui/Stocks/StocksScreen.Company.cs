using System;
using System.Globalization;
using RealLifeOverhaul.Markets;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// Part of the Stocks screen: the company page's chart and order. The pointer reads the plot through IUiTracking (one target for
    /// the whole plot), a click lands where the pointer went down (IUiDragging), and a hover, a pan, a zoom or a tip changes the view
    /// and lays the page out again from the model already built, so nothing is built from the market for them. Cost: one comparison
    /// a pointer move; a layout when the candle it reads changes.
    /// </summary>
    public sealed partial class StocksScreen
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private const float HitRadius = 9f;       // px a click may be from a drawing to select it

        private readonly StocksChartControl _ctl = new StocksChartControl();   // how the chart's view moves, and what each move needs
        private StocksView ChartView => _ctl.View;
        private StocksModel _model;               // the page as last built; a hover lays it out again
        private bool _relay;                      // laid out again from the model, not built again
        private int _typeOn = StocksLay.Shares;   // the field a press starts typing in
        private int _downCommand = -1;            // where the pointer last went down: its target and place
        private float _downX, _downY;
        private bool _plotHeld;                   // the keys hold the plot: Left and Right step the candle read, as a held slider steps

        private UiChartDeskModel Chart => _model != null && _model.Co != null ? _model.Co.Chart : null;

        private void OpenCompany(int id)
        {
            _stock = id;
            _page = StocksPage.Company;
            ResetView();
            _dirty = true;
        }

        private void ResetView()
        {
            _ctl.Reset();
            _plotHeld = false;
        }

        // What a change of the view needs: a layout from the model built, or the page built again.
        private void Did(StocksChange change)
        {
            if (change == StocksChange.Rebuild)
            {
                _dirty = true;
            }
            else if (change == StocksChange.Relay)
            {
                _relay = true;
            }
        }

        // ------------------------------------------------------------------------------------------------- pointer

        /// <summary>The plot follows the mouse.</summary>
        public bool Tracks(int command) => _page == StocksPage.Company && !_sheet.Open && command == UiChartDesk.Command;

        /// <summary>The mouse moved over the plot: the candle under it is the one read, and the page is laid out again only when that changes.</summary>
        public void Moved(int command, int arg, float x, float y)
        {
            Did(_ctl.Moved(Chart, x));
        }

        /// <summary>The mouse rests on a target, or has left it: a rail button's name shows, and the plot's reading goes when the mouse leaves it.</summary>
        public void Pointed(int command, int arg)
        {
            if (_page == StocksPage.Company && _model != null && _model.Co != null)
            {
                Did(_ctl.Pointed(Chart, command, arg));
            }
        }

        private void CompanyFocused(int command, int arg)
        {
            if (command == StocksLay.Shares || command == StocksLay.PricePlain)
            {
                _typeOn = command;
            }
            if (_page == StocksPage.Company && command == UiChartDesk.Rail)
            {
                Did(_ctl.Pointed(Chart, command, arg));
            }
        }

        /// <summary>The pointer went down on a target: where, so the click that follows can say what it landed on.</summary>
        public void PointerDown(int command, int arg, float x, float y)
        {
            _downCommand = command;
            _downX = x;
            _downY = y;
        }

        /// <summary>A drag that starts on the plot with the pointer in hand moves the chart along.</summary>
        public bool DragBegin(int command, int arg, float x, float y)
        {
            if (_page != StocksPage.Company || _sheet.Open || command != UiChartDesk.Command || !_ctl.PanBegin(Chart, x))
            {
                return false;
            }
            _downCommand = -1;   // a drag is no click
            return true;
        }

        public void DragMove(float x, float y)
        {
            Did(_ctl.PanMove(Chart, x));
        }

        public void DragEnd(bool cancelled)
        {
            _ctl.PanEnd();
            _downCommand = -1;
        }

        // The wheel over the plot zooms: in toward the newest candle, out toward all of them.
        private bool ChartWheel(int clip, float notches)
        {
            UiChartDeskModel chart = Chart;
            if (chart == null || chart.PlotClip < 0 || clip != chart.PlotClip)
            {
                return false;
            }
            Did(_ctl.Wheel(chart, notches));
            return true;
        }

        // ------------------------------------------------------------------------------------------------- keys

        // Left and Right move the focus across the page, as everywhere; the plot takes them only while the keys hold it (a first Confirm on it).
        private bool CompanySteps(int command) => _page == StocksPage.Company && _plotHeld && command == UiChartDesk.Command;

        private void CompanyStep(int command, int step)
        {
            if (command == UiChartDesk.Command)
            {
                StepFocus(step);
            }
        }

        private void StepFocus(int step) => Did(_ctl.Step(Chart, step));

        private bool CompanyBack()
        {
            StocksChange change = _ctl.Back();
            Did(change);
            if (change == StocksChange.None && _plotHeld)
            {
                _plotHeld = false;   // the keys let the plot go
                return true;
            }
            return change != StocksChange.None;
        }

        private bool ToggleBig()
        {
            if (ChartView.Figures)
            {
                return false;   // the figures have no big form
            }
            Did(_ctl.ToggleBig());
            return true;
        }

        // ------------------------------------------------------------------------------------------------- presses

        private bool CompanyPress(int command, int arg, out bool done)
        {
            done = true;
            StocksCompany co = _model != null ? _model.Co : null;
            MarketState state = _market != null ? _market.State : null;
            if (command == UiChartDesk.Command)
            {
                done = PlotClick();
                return true;
            }
            if (command == UiChartDesk.Rail)
            {
                done = RailPress(arg);
                return true;
            }
            if (command == StocksCompanyLay.AddIndicator)
            {
                Did(_ctl.TogglePicker(!ChartView.PickOpen));
                return true;
            }
            if (command == StocksCompanyLay.View)
            {
                if (co == null)
                {
                    done = false;
                    return true;
                }
                ChartView.Figures = Segment(command, co.ViewEdges, co.Views.Length, co.View) == 1;
                ChartView.Big = false;   // the big chart is the chart's; leaving it for the figures leaves it for good
                ChartView.PickOpen = false;
                _dirty = true;
                return true;
            }
            if (command == StocksCompanyLay.Big)
            {
                done = ToggleBig();
                return true;
            }
            if (command == StocksCompanyLay.PutBack)
            {
                done = CyclePutBack();
                return true;
            }
            if (command == StocksLay.Alert)
            {
                done = RemoveAlert(arg);
                return true;
            }
            if (command == StocksCompanyLay.AlertAdd)
            {
                done = OpenAlertSheet();
                return true;
            }
            if (command == StocksCompanyLay.AlertLine)
            {
                done = AlertOnLine();
                return true;
            }
            if (co == null || state == null)
            {
                done = false;
                return command >= StocksCompanyLay.Range && command <= StocksCompanyLay.Life;
            }
            switch (command)
            {
                case StocksCompanyLay.Pick:
                    if (arg < 0 || arg >= (int)StocksInd.Count)
                    {
                        done = false;
                        return true;
                    }
                    StocksBuild.ToggleIndicator(state.Charts, (StocksInd)arg);
                    _dirty = true;
                    return true;
                case StocksCompanyLay.Chip:
                    if (arg < 0 || arg >= co.Chips.Length)
                    {
                        done = false;
                        return true;
                    }
                    StocksBuild.ToggleIndicator(state.Charts, co.Chips[arg].Ind);
                    _dirty = true;
                    return true;
                case StocksCompanyLay.Range: SetRange(state.Charts, Segment(command, co.RangeEdges, co.Ranges.Length, co.Range)); return true;
                case StocksCompanyLay.Type: SetType(state.Charts, Segment(command, co.TypeEdges, co.Types.Length, co.Type)); return true;
                case StocksCompanyLay.Side:
                    done = SetSide(Segment(command, co.SideEdges, co.Order.Sides.Length, co.Order.SideAt));
                    return true;
                case StocksCompanyLay.Kind: SetKind(Segment(command, co.KindEdges, co.Order.Kinds.Length, co.Order.KindAt)); return true;
                case StocksCompanyLay.Life:
                    _life = Segment(command, co.LifeEdges, co.Order.Lives.Length, co.Order.LifeAt) == 1 ? OrderLife.UntilCancelled : OrderLife.Day;
                    _dirty = true;
                    return true;
                default: return false;
            }
        }

        // The segment a press picked: where the pointer went down along the control, or the next one when a key pressed it.
        private int Segment(int command, float[] edges, int count, int now)
        {
            int at;
            if (_downCommand == command)
            {
                at = UiSegments.At(edges, count, _downX);
            }
            else
            {
                at = (now + 1) % Math.Max(1, count);
            }
            _downCommand = -1;
            return Math.Max(0, at);
        }

        private void SetRange(ChartBook book, int frame)
        {
            if (frame != book.Frame)
            {
                book.Frame = frame;
                book.Touch();
                _ctl.ResetWindow();
            }
            _dirty = true;
        }

        private void SetType(ChartBook book, int kind)
        {
            if (kind != book.Kind)
            {
                book.Kind = kind;
                book.Touch();
            }
            _dirty = true;
        }

        // Buy or sell. A stop loss or a take profit closes what is held, so its side is not the player's to choose.
        private bool SetSide(int side)
        {
            if (_kind >= 2)
            {
                return false;
            }
            _buy = side == 0;
            _price = 0;
            _dirty = true;
            return true;
        }

        private void SetKind(int kind)
        {
            _kind = Math.Max(0, Math.Min(3, kind));
            _price = 0;
            long held = _market != null && _market.State != null ? Math.Abs(_market.State.Held(_stock)) : 0;
            if (_kind >= 2 && held > 0)
            {
                _qty = held;   // a stop loss or a take profit is on what is held: all of it, to begin with
            }
            _dirty = true;
        }

        private bool RailPress(int arg)
        {
            if (arg >= UiChartTools.Count)
            {
                return DeleteDrawing();
            }
            ChartView.Tool = Math.Max(0, arg);
            ChartView.AnchorDay = 0;
            _dirty = true;
            return true;
        }

        // The delete: the selected drawing, or every drawing of the company when none is selected.
        private bool DeleteDrawing()
        {
            MarketState state = _market != null ? _market.State : null;
            if (state == null || _stock <= 0)
            {
                return false;
            }
            bool gone = ChartView.Selected >= 0 ? state.Charts.RemoveDrawing(_stock, ChartView.Selected) : state.Charts.ClearDrawings(_stock) > 0;
            ChartView.Selected = -1;
            ChartView.AnchorDay = 0;
            _dirty = true;
            return gone;
        }

        // A click on the plot: with the pointer in hand it selects the drawing under it; with a tool it draws. A key press reads the
        // candle the keys rest on, at its close.
        private bool PlotClick()
        {
            UiChartDeskModel chart = Chart;
            MarketState state = _market != null ? _market.State : null;
            Stock stock = state != null ? state.Stock(_stock) : null;
            if (chart == null || chart.Close == null || chart.Close.Length == 0 || stock == null || chart.Slot <= 0f)
            {
                return false;
            }
            int n = chart.Close.Length;
            bool pointer = _downCommand == UiChartDesk.Command;
            _downCommand = -1;
            int at;
            float price;
            if (pointer)
            {
                at = Math.Max(chart.First, Math.Min(chart.First + chart.Shown - 1, chart.First + (int)Math.Floor((_downX - chart.PlotX) / chart.Slot)));
                float y = Math.Max(0f, Math.Min(chart.PriceH, _downY - chart.PlotY));
                price = UiChartDesk.PriceAt(y, chart.PlotLo, chart.PlotHi, chart.PriceH);
            }
            else
            {
                at = ChartView.Focus >= 0 && ChartView.Focus < n ? ChartView.Focus : n - 1;
                price = chart.Close[at];
            }
            if (!pointer && !_plotHeld)
            {
                _plotHeld = true;   // a first Confirm takes hold of the plot: Left and Right then step the candle, the next Confirm acts on it
                ChartView.Focus = at;
                _relay = true;
                return true;
            }
            int tool = ChartView.Tool;
            if (tool == (int)UiChartTool.Pointer)
            {
                int hit = pointer ? UiChartDesk.HitMark(chart, _downX, _downY, HitRadius) : -1;
                ChartView.Selected = hit >= 0 ? chart.Marks[hit].Ordinal : -1;
                ChartView.Focus = at;
                _dirty = true;
                return true;
            }
            int day = chart.Day != null && at < chart.Day.Length ? chart.Day[at] : 0;
            bool level = tool == (int)UiChartTool.Level || tool == (int)UiChartTool.Snap;
            if (day == 0 && !level)
            {
                return false;   // the day's bars have no days to keep a line on: only a price line holds there
            }
            if (tool == (int)UiChartTool.Snap)
            {
                price = Nearest(chart, at, price);
            }
            int cents = Math.Max(1, (int)Math.Round(price * 100f));
            int clicks = UiChartTools.Clicks(tool);
            if (clicks == 2 && ChartView.AnchorDay == 0)
            {
                ChartView.AnchorDay = day;
                ChartView.AnchorPrice = cents / 100f;
                ChartView.Focus = at;
                _dirty = true;
                return true;
            }
            int fromDay = clicks == 2 ? ChartView.AnchorDay : day;
            int fromCents = clicks == 2 ? (int)Math.Round(ChartView.AnchorPrice * 100f) : cents;
            ChartView.AnchorDay = 0;
            ChartMark mark = MarkOf(tool);
            bool added = state.Charts.AddDrawing(new ChartDrawing
            {
                StockId = stock.Id, Kind = mark, Frame = state.Charts.Frame, FromIndex = fromDay, ToIndex = day, FromCents = fromCents, ToCents = cents,
                Magnet = (byte)(tool == (int)UiChartTool.Snap ? 1 : 0)
            });
            if (added)
            {
                int count = 0;
                for (int i = 0; i < state.Charts.Drawings.Count; i++)
                {
                    count += state.Charts.Drawings[i].StockId == stock.Id ? 1 : 0;
                }
                ChartView.Selected = count - 1;
            }
            _dirty = true;
            return added;
        }

        private static ChartMark MarkOf(int tool)
        {
            switch ((UiChartTool)tool)
            {
                case UiChartTool.Level:
                case UiChartTool.Snap: return ChartMark.Level;
                case UiChartTool.Fibonacci: return ChartMark.Fibonacci;
                case UiChartTool.Ray: return ChartMark.Path;
                case UiChartTool.Rectangle: return ChartMark.Rectangle;
                case UiChartTool.Measure: return ChartMark.Measure;
                case UiChartTool.Note: return ChartMark.Text;
                default: return ChartMark.Trend;
            }
        }

        // The candle's own price nearest the one clicked: its open, high, low or close.
        private static float Nearest(UiChartDeskModel chart, int at, float price)
        {
            float best = chart.Close[at];
            float[][] prices = { chart.Open, chart.High, chart.Low, chart.Close };
            for (int i = 0; i < prices.Length; i++)
            {
                if (prices[i] != null && at < prices[i].Length && Math.Abs(prices[i][at] - price) < Math.Abs(best - price))
                {
                    best = prices[i][at];
                }
            }
            return best;
        }

        // The alert sheet: the level is the player's to set, above the price or below it.
        private bool OpenAlertSheet()
        {
            MarketState state = _market != null ? _market.State : null;
            if (state == null || state.Stock(_stock) == null)
            {
                return false;
            }
            OpenSheet(StocksSheets.Alert, _stock);
            return true;
        }

        // An alert for when the price falls to the selected level line: the support alert, which follows the line.
        private bool AlertOnLine()
        {
            MarketState state = _market != null ? _market.State : null;
            Stock stock = state != null ? state.Stock(_stock) : null;
            if (state == null || stock == null)
            {
                return false;
            }
            _dirty = true;
            AlertWhy why = state.Charts.SetLineAlert(stock.Id, ChartView.Selected, stock.Last);
            if (why != AlertWhy.None)
            {
                Refused(StocksBuild.AlertWhyWords(why, Words));
                return false;
            }
            return true;
        }

        // The alert of this company on the row pressed.
        private bool RemoveAlert(int row)
        {
            MarketState state = _market != null ? _market.State : null;
            if (state == null || _stock <= 0)
            {
                return false;
            }
            int seen = 0;
            for (int i = 0; i < state.Charts.Alerts.Count; i++)
            {
                if (state.Charts.Alerts[i].StockId != _stock)
                {
                    continue;
                }
                if (seen++ == row)
                {
                    _dirty = true;
                    return state.Charts.RemoveAlert(i);
                }
            }
            return false;
        }

        // ------------------------------------------------------------------------------------------------- typing

        private string CompanyTypingText()
        {
            if (_typeOn == StocksLay.PricePlain && _kind > 0)
            {
                MarketState book = _market != null ? _market.State : null;
                Stock quoted = book != null ? book.Stock(_stock) : null;
                if (quoted != null)
                {
                    return (StocksBuild.TicketPrice(book, quoted, !_buy, _kind, _price) / 100.0).ToString("0.00", Inv);
                }
            }
            return _qty.ToString();
        }

        private void CompanyTyped(string text)
        {
            if (_typeOn == StocksLay.PricePlain && _kind > 0)
            {
                if (double.TryParse((text ?? string.Empty).Replace(',', '.'), NumberStyles.Float, Inv, out double dollars) && dollars > 0.0 && dollars < 10000000.0)
                {
                    _price = Math.Max(1, (int)Math.Round(dollars * 100.0));
                }
                else if (string.IsNullOrEmpty(text))
                {
                    _price = 0;
                }
            }
            else
            {
                _qty = StocksSheetState.Typed(text, 1, 1000000000L);
            }
            _dirty = true;
        }
    }
}
