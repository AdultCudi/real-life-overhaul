using System;
using System.Collections.Generic;
using RealLifeOverhaul.Markets;

namespace RealLifeOverhaul.Ui
{
    /// <summary>The company page built from the market: its head, the chart with its indicators and drawings, the order and what it says.</summary>
    public static partial class StocksBuild
    {
        public const int CandlesMost = 400;       // candles of one frame the chart takes
        private static readonly Func<float, string> DollarWords = v => MarketText.Price((int)Math.Round(v * 100f));

        /// <summary>How many times a page was built this session. A hover, a pan or a zoom lays the page out again and never raises it (EconomyCheck).</summary>
        public static int Built;

        // The market's chart book for each indicator: a line over the price is a layer bit, a pane is one of two slots.
        private static ChartLayer LayerOf(StocksInd i)
        {
            switch (i)
            {
                case StocksInd.Sma20: return ChartLayer.Sma20;
                case StocksInd.Sma50: return ChartLayer.Sma50;
                case StocksInd.Ema20: return ChartLayer.Ema20;
                default: return ChartLayer.Bollinger;
            }
        }

        private static ChartPane PaneOf(StocksInd i)
        {
            switch (i)
            {
                case StocksInd.Rsi: return ChartPane.Rsi;
                case StocksInd.Macd: return ChartPane.Macd;
                case StocksInd.Stochastic: return ChartPane.Stochastic;
                case StocksInd.Atr: return ChartPane.Atr;
                default: return ChartPane.Obv;
            }
        }

        /// <summary>Whether an indicator is on the chart book.</summary>
        public static bool IndicatorOn(ChartBook book, StocksInd i)
        {
            if (StocksIndicators.IsPane(i))
            {
                ChartPane pane = PaneOf(i);
                return book.FirstPane == pane || book.SecondPane == pane;
            }
            return book.LayerOn(LayerOf(i));
        }

        /// <summary>Puts an indicator on the chart book, or takes it off. A third pane takes the older second one's place.</summary>
        public static void ToggleIndicator(ChartBook book, StocksInd i)
        {
            if (StocksIndicators.IsPane(i))
            {
                book.TogglePane(PaneOf(i));
            }
            else
            {
                book.ToggleLayer(LayerOf(i));
            }
        }

        private static void Company(StocksModel m, MarketState state, int stockId, StocksView v, bool buy, OrderLife life, long qty, UiWords words, int kind, int price, MarketDirector d)
        {
            Stock s = state.Stock(stockId) ?? First(state);
            if (s == null)
            {
                m.EmptyTitle = words("reallife_stk_market_empty", "No companies are listed");
                m.EmptyBody = words("reallife_stk_market_empty_body", "Listed companies show here with the day's move.");
                return;
            }
            var c = new StocksCompany();
            m.Co = c;
            Head(c, state, s, words);
            c.Chart = Chart(state, s, v, words);
            c.Order = TicketOf(state, s, buy, OrderType.Market, life, qty, words, kind, price, d);
            c.Order.Place = c.Order.Buying ? words("reallife_stk_place_buy", "Place buy order") : words("reallife_stk_place_sell", "Place sell order");
            Toolbar(c, state, v, words);
            c.Signals = StocksSignals.Read(c.Chart, words);
            c.SignalsTitle = words("reallife_stk_signals_title", "Signals");
            c.SignalsNone = words("reallife_stk_signals_none", "Add an indicator or draw a line to see what it says");
            Position(c, state, s, words);
            Alerts(c, state, s, words);
            m.PromptPlace = c.Order.Place;
            m.PromptOther = c.Big ? words("reallife_stk_small_chart", "Close the big chart") : words("reallife_stk_big_chart", "Open the big chart");
        }

        private static void Head(StocksCompany c, MarketState state, Stock s, UiWords words)
        {
            int bp = StocksMath.ChangeBp(s.PrevClose, s.Last);
            int move = s.Last - s.PrevClose;
            c.Name = s.Name;
            c.Ticker = s.Ticker;
            c.Sector = MarketText.Sector(s.Sector);
            string[] caps = { words("reallife_stk_cap_large", "Large cap"), words("reallife_stk_cap_mid", "Mid cap"), words("reallife_stk_cap_small", "Small cap") };
            int size = (int)s.Size;
            c.Meta = caps[size >= 0 && size < caps.Length ? size : 1] + ", " + words("reallife_stk_exchange", "NYSE");
            c.LogoTint = UiTokens.Chart((int)s.Sector);
            c.Price = MarketText.Price(s.Last);
            c.Way = move > 0 ? 1 : move < 0 ? -1 : 0;
            c.ChangeMoney = (move > 0 ? "+" : move < 0 ? "-" : string.Empty) + MarketText.Price(Math.Abs(move));
            c.ChangePercent = MarketText.Change(bp);
            int pe = StocksMath.PeTimes100(s.Last, s.Results.Count > 0 ? s.Results[s.Results.Count - 1].Eps : 0);
            c.FactLabels = new[]
            {
                words("reallife_stk_open", "Open"), words("reallife_stk_high", "High"), words("reallife_stk_low", "Low"), words("reallife_mkt_col_volume", "Volume"),
                words("reallife_stk_cap", "Market cap"), words("reallife_mkt_col_pe", "P/E"), words("reallife_mkt_col_yield", "Yield")
            };
            c.FactValues = new[]
            {
                MarketText.Price(s.Open), MarketText.Price(s.High), MarketText.Price(s.Low), UiChartDesk.Compact(s.DayVolume), MarketText.Large(MarketIndex.Value(s)),
                pe > 0 ? (pe / 100f).ToString("0.0") : words("reallife_stk_none", "None"), MarketText.Percent(StocksMath.YieldBp(s.Last, s.DpsCents))
            };
        }

        private static void Toolbar(StocksCompany c, MarketState state, StocksView v, UiWords words)
        {
            ChartBook book = state.Charts;
            c.Ranges = new string[(int)ChartFrame.Count];
            for (int i = 0; i < c.Ranges.Length; i++)
            {
                c.Ranges[i] = FrameWord(i, words);
            }
            c.Range = Math.Max(0, Math.Min(book.Frame, c.Ranges.Length - 1));
            c.Types = new[] { KindWord(0, words), KindWord(1, words), KindWord(2, words) };
            c.Type = Math.Max(0, Math.Min(book.Kind, 2));
            c.AddLabel = words("reallife_stk_add_indicator", "+ Indicator");
            c.Big = v.Big;
            var chips = new List<StocksChip>(6);
            var on = new bool[(int)StocksInd.Count];
            c.PickNames = new string[on.Length];
            c.PickHints = new string[on.Length];
            c.PickOn = on;
            for (int i = 0; i < on.Length; i++)
            {
                var ind = (StocksInd)i;
                on[i] = IndicatorOn(book, ind);
                c.PickNames[i] = words(StocksIndicators.Keys[i], StocksIndicators.English[i]);
                c.PickHints[i] = words(StocksIndicators.HintKeys[i], StocksIndicators.HintEnglish[i]);
            }
            for (int i = 0; i < on.Length; i++)
            {
                if (on[i] && !StocksIndicators.IsPane((StocksInd)i))
                {
                    chips.Add(new StocksChip { Label = c.PickNames[i], Tint = StocksIndicators.Tint((StocksInd)i), Ind = (StocksInd)i });
                }
            }
            for (int slot = 0; slot < 2; slot++)
            {
                ChartPane pane = slot == 0 ? book.FirstPane : book.SecondPane;
                for (int i = (int)StocksInd.Rsi; pane != ChartPane.None && i < on.Length; i++)
                {
                    if (PaneOf((StocksInd)i) == pane)
                    {
                        chips.Add(new StocksChip { Label = c.PickNames[i], Tint = StocksIndicators.Tint((StocksInd)i), Ind = (StocksInd)i });
                    }
                }
            }
            c.Chips = chips.ToArray();
            c.PickOpen = v.PickOpen;
        }

        private static void Position(StocksCompany c, MarketState state, Stock s, UiWords words)
        {
            c.PositionTitle = words("reallife_stk_position_title", "Your position");
            c.PositionNone = words("reallife_stk_position_none", "You hold no shares of this company");
            Position p = state.Position(s.Id);
            if (p == null || p.Shares == 0)
            {
                return;
            }
            long shares = Math.Abs(p.Shares);
            long value = p.Shares * s.Last / 100L;
            long gain = p.Shares > 0 ? value - p.Cost : p.Cost - Math.Abs(value);
            int gainBp = p.Cost > 0 ? (int)Math.Max(int.MinValue, Math.Min(int.MaxValue, gain * 10000L / p.Cost)) : 0;
            c.Position = new[]
            {
                new StocksRow { Label = words("reallife_mkt_col_shares", "Shares"), Value = p.Shares.ToString("N0") },
                new StocksRow { Label = words("reallife_stk_avg_cost", "Average cost"), Value = MarketText.Price((int)Math.Min(int.MaxValue, p.Cost * 100L / shares)) },
                new StocksRow
                {
                    Label = words("reallife_stk_gain", "Gain"), Value = (gain > 0 ? "+" : string.Empty) + MarketText.Money(gain) + " (" + MarketText.Change(gainBp) + ")",
                    Tone = gain > 0 ? UiRole.OkText : gain < 0 ? UiRole.DangerText : UiRole.None
                }
            };
        }

        private static void Alerts(StocksCompany c, MarketState state, Stock s, UiWords words)
        {
            c.AlertsTitle = words("reallife_stk_alerts_title", "Price alerts");
            c.AlertsNone = words("reallife_stk_alerts_none", "No alert for this company");
            c.AlertAbove = words("reallife_stk_alert_up", "Alert +5%");
            c.AlertBelow = words("reallife_stk_alert_down", "Alert -5%");
            c.AlertsFull = state.Charts.Alerts.Count >= ChartBook.AlertsMost;
            var rows = new List<StocksAlertRow>(3);
            for (int i = 0; i < state.Charts.Alerts.Count && rows.Count < 3; i++)
            {
                PriceAlert a = state.Charts.Alerts[i];
                if (a.StockId != s.Id)
                {
                    continue;
                }
                int cents = a.Kind == AlertKind.Support ? state.Charts.LevelOf(a.Drawing, s.Id) : a.Cents;
                string key = a.Kind == AlertKind.Above ? "reallife_stk_alert_above" : a.Kind == AlertKind.Below ? "reallife_stk_alert_below" : "reallife_stk_alert_support";
                string english = a.Kind == AlertKind.Above ? "Above {price}" : a.Kind == AlertKind.Below ? "Below {price}" : "Below the support {price}";
                rows.Add(new StocksAlertRow
                {
                    Words = words(key, english).Replace("{price}", MarketText.Price(cents)),
                    State = a.Hit ? words("reallife_stk_alert_state_hit", "hit") : words("reallife_stk_alert_state_on", "on")
                });
            }
            c.Alerts = rows.ToArray();
        }

        // The candles of the chart's range, the lines and panes, the drawings and the window.
        private static UiChartDeskModel Chart(MarketState state, Stock s, StocksView v, UiWords words)
        {
            var built = new List<Candle>(128);
            LoadCandles(state, s, state.Charts.Frame, built);
            for (int i = built.Count - 1; i >= 0; i--)
            {
                if (built[i].Empty)
                {
                    built.RemoveAt(i);   // a candle with no close is no price: never a stretch drawn at nothing
                }
            }
            int n = Math.Min(built.Count, CandlesMost);
            int from = built.Count - n;
            bool today = state.Charts.Frame <= (int)ChartFrame.Today;
            var m = new UiChartDeskModel
            {
                Kind = state.Charts.Kind, Head = s.Ticker + ", " + FrameWord(state.Charts.Frame, words), Tool = v.Tool, Tip = v.Tip, Price = DollarWords,
                EmptyTitle = words("reallife_stk_chart_empty", "No candles yet"), EmptyBody = words("reallife_stk_chart_empty_body", "Candles appear as the market trades."),
                DayLabel = words("reallife_mkt_col_day", "Day"), OpenLabel = words("reallife_stk_open", "Open"), HighLabel = words("reallife_stk_high", "High"),
                LowLabel = words("reallife_stk_low", "Low"), CloseLabel = words("reallife_stk_close", "Close"), VolumeLabel = words("reallife_mkt_col_volume", "Volume"),
                ToolNames = new string[UiChartTools.Count]
            };
            for (int i = 0; i < UiChartTools.Count; i++)
            {
                m.ToolNames[i] = words(UiChartTools.Keys[i], UiChartTools.English[i]);
            }
            m.DeleteName = v.Selected >= 0 ? words("reallife_stk_delete_one", "Delete this drawing") : words("reallife_stk_delete_all", "Delete every drawing");
            if (n == 0)
            {
                return m;
            }
            m.Open = new float[n];
            m.High = new float[n];
            m.Low = new float[n];
            m.Close = new float[n];
            m.Volume = new float[n];
            m.Day = new int[n];
            m.Stamp = new string[n];
            var close = new double[n];
            var high = new double[n];
            var low = new double[n];
            var vol = new double[n];
            for (int i = 0; i < n; i++)
            {
                Candle c = built[from + i];
                m.Open[i] = c.O / 100f;
                m.High[i] = c.H / 100f;
                m.Low[i] = c.L / 100f;
                m.Close[i] = c.C / 100f;
                m.Volume[i] = c.V;
                m.Day[i] = today ? 0 : c.Day;
                m.Stamp[i] = StampOf(state, c, i, state.Charts.Frame);
                close[i] = c.C;
                high[i] = c.H;
                low[i] = c.L;
                vol[i] = c.V;
            }
            m.LastTag = MarketText.Price(s.Last);
            m.LastUp = s.Last >= s.PrevClose;
            m.Lines = Overlays(state.Charts, close, n, words);
            m.Panes = Panes(state.Charts, close, high, low, vol, n, words);
            m.Marks = Marks(state, s, v, m, today, words);
            StocksChartControl.Window(m, v, n);
            Anchor(m, v, today);
            return m;
        }

        private static void Anchor(UiChartDeskModel m, StocksView v, bool today)
        {
            m.Anchor = -1;
            if (v.AnchorDay != 0 && !today)
            {
                int at = IndexOfDay(m.Day, v.AnchorDay);
                if (at >= 0)
                {
                    m.Anchor = at;
                    m.AnchorY = v.AnchorPrice;
                }
            }
        }

        /// <summary>The candle a game day falls in: the last whose first day is on or before it, or -1 before the first.</summary>
        public static int IndexOfDay(int[] days, int day)
        {
            if (days == null || days.Length == 0 || day < days[0])
            {
                return -1;
            }
            int lo = 0, hi = days.Length - 1;
            while (lo < hi)
            {
                int mid = (lo + hi + 1) / 2;
                if (days[mid] <= day)
                {
                    lo = mid;
                }
                else
                {
                    hi = mid - 1;
                }
            }
            return lo;
        }

        private static string StampOf(MarketState state, Candle c, int i, int frame)
        {
            if (frame <= (int)ChartFrame.Today)
            {
                int minute = MarketCatalogue.OpenMinute + i * MarketCatalogue.StepMinutes;
                return (minute / 60).GetFormattedTime(minute % 60);
            }
            int year = Math.Max(8, state.YearDays);
            switch ((ChartFrame)frame)
            {
                case ChartFrame.Months:
                {
                    int month = MarketCatalogue.MonthOf(c.Day, year);
                    int y = MarketCatalogue.FloorDiv(month, 12);
                    return MarketText.Fill("reallife_mkt_chart_month", "month", (month - y * 12 + 1).ToString(), "year", (y + 1).ToString());
                }
                case ChartFrame.Years: return MarketText.Fill("reallife_mkt_chart_year", "year", (MarketCatalogue.YearOf(c.Day, year) + 1).ToString());
                default: return MarketText.Fill("reallife_mkt_chart_day", "day", c.Day.ToString());
            }
        }

        private static string FrameWord(int frame, UiWords words)
        {
            string[] keys = { "reallife_mkt_frame_today", "reallife_mkt_frame_days", "reallife_mkt_frame_weeks", "reallife_mkt_frame_months", "reallife_mkt_frame_years" };
            string[] english = { "Today", "Days", "Weeks", "Months", "Years" };
            int i = frame >= 0 && frame < keys.Length ? frame : (int)ChartFrame.Days;
            return words(keys[i], english[i]);
        }

        private static string KindWord(int kind, UiWords words)
        {
            if (kind == 1)
            {
                return words("reallife_stk_kind_line", "Line");
            }
            if (kind == 2)
            {
                return words("reallife_stk_kind_area", "Area");
            }
            return words("reallife_stk_kind_candles", "Candles");
        }

        private static void LoadCandles(MarketState state, Stock s, int frame, List<Candle> into)
        {
            into.Clear();
            if (frame <= (int)ChartFrame.Today)
            {
                Candle[] bars = s.BarCount > 0 ? s.Bars : s.LastBars;
                int count = s.BarCount > 0 ? s.BarCount : s.LastBarCount;
                for (int i = 0; i < count; i++)
                {
                    into.Add(bars[i]);
                }
                return;
            }
            bool session = MarketCatalogue.TradingDay(state.Day) && state.Phase >= MarketCatalogue.PhaseOpen && state.Phase < MarketCatalogue.PhaseClose;
            Candle today = session ? new Candle(state.Day, s.Open, Math.Max(s.High, s.Last), Math.Min(s.Low, s.Last), s.Last, s.DayVolume) : default(Candle);
            MarketHistory.Build(s.Daily, s.Monthly, today, (Timeframe)frame, Math.Max(8, state.YearDays), into);
        }

        // The lines over the price, each over the whole set so an average has its window from the first candle it can.
        private static UiChartLine[] Overlays(ChartBook book, double[] close, int n, UiWords words)
        {
            var lines = new List<UiChartLine>(4);
            var a = new double[n];
            var b = new double[n];
            var c = new double[n];
            for (int i = 0; i < (int)StocksInd.Rsi; i++)
            {
                var ind = (StocksInd)i;
                if (!IndicatorOn(book, ind))
                {
                    continue;
                }
                string name = words(StocksIndicators.Keys[i], StocksIndicators.English[i]);
                switch (ind)
                {
                    case StocksInd.Sma20: MarketIndicators.Sma(close, n, 20, a); break;
                    case StocksInd.Sma50: MarketIndicators.Sma(close, n, 50, a); break;
                    case StocksInd.Ema20: MarketIndicators.Ema(close, n, 20, a); break;
                    default: MarketIndicators.Bollinger(close, n, 20, 2.0, a, b, c); break;
                }
                bool band = ind == StocksInd.Bollinger;
                lines.Add(new UiChartLine
                {
                    Name = name, Values = Dollars(band ? b : a), Under = band ? Dollars(c) : null, Tint = StocksIndicators.Tint(ind), Wash = band, Ind = ind
                });
            }
            return lines.ToArray();
        }

        private static UiChartPane[] Panes(ChartBook book, double[] close, double[] high, double[] low, double[] vol, int n, UiWords words)
        {
            var panes = new List<UiChartPane>(2);
            for (int slot = 0; slot < 2; slot++)
            {
                ChartPane pane = slot == 0 ? book.FirstPane : book.SecondPane;
                for (int i = (int)StocksInd.Rsi; pane != ChartPane.None && i < (int)StocksInd.Count; i++)
                {
                    if (PaneOf((StocksInd)i) == pane)
                    {
                        panes.Add(PaneOfInd((StocksInd)i, close, high, low, vol, n, words));
                    }
                }
            }
            return panes.ToArray();
        }

        private static UiChartPane PaneOfInd(StocksInd ind, double[] close, double[] high, double[] low, double[] vol, int n, UiWords words)
        {
            var a = new double[n];
            var b = new double[n];
            var c = new double[n];
            var scratch = new double[n];
            var p = new UiChartPane
            {
                Name = words(StocksIndicators.Keys[(int)ind], StocksIndicators.English[(int)ind]), Tint = StocksIndicators.Tint(ind), SecondTint = StocksIndicators.Second(ind), Ind = ind,
                Format = "0.0"
            };
            switch (ind)
            {
                case StocksInd.Macd:
                    MarketIndicators.Macd(close, n, 12, 26, 9, a, b, c, scratch);
                    p.Values = Dollars(a);
                    p.Second = Dollars(b);
                    p.Histogram = Dollars(c);
                    p.Format = "0.00";
                    break;
                case StocksInd.Stochastic:
                    MarketIndicators.Stochastic(high, low, close, n, 14, 3, a, b);
                    p.Values = Floats(a);
                    p.Second = Floats(b);
                    p.Fixed = true;
                    p.Guides = new[] { 80f, 20f };
                    break;
                case StocksInd.Atr:
                    MarketIndicators.Atr(high, low, close, n, 14, a);
                    p.Values = Dollars(a);
                    p.Format = "0.00";
                    break;
                case StocksInd.Obv:
                    MarketIndicators.Obv(close, vol, n, a);
                    p.Values = Floats(a);
                    p.Format = "0";
                    break;
                default:
                    MarketIndicators.Rsi(close, n, 14, a);
                    p.Values = Floats(a);
                    p.Fixed = true;
                    p.Guides = new[] { 70f, 30f };
                    break;
            }
            return p;
        }

        private static float[] Floats(double[] v)
        {
            var f = new float[v.Length];
            for (int i = 0; i < v.Length; i++)
            {
                f[i] = double.IsNaN(v[i]) ? float.NaN : (float)v[i];
            }
            return f;
        }

        // A price series from the market's cents to the chart's dollars.
        private static float[] Dollars(double[] v)
        {
            var f = new float[v.Length];
            for (int i = 0; i < v.Length; i++)
            {
                f[i] = double.IsNaN(v[i]) ? float.NaN : (float)(v[i] / 100.0);
            }
            return f;
        }

        /// <summary>A drawing's kind as the chart draws it.</summary>
        public static UiChartMarkKind MarkKindOf(ChartMark mark)
        {
            switch (mark)
            {
                case ChartMark.Level: return UiChartMarkKind.Level;
                case ChartMark.Fibonacci: return UiChartMarkKind.Fibonacci;
                case ChartMark.Path: return UiChartMarkKind.Ray;
                case ChartMark.Rectangle: return UiChartMarkKind.Rectangle;
                case ChartMark.Measure: return UiChartMarkKind.Measure;
                case ChartMark.Text: return UiChartMarkKind.Note;
                default: return UiChartMarkKind.Trend;
            }
        }

        // The company's drawings on the candles of this range. Each is kept by the game days of its ends, so it stays on its price and
        // day when the range or the window changes; in Today's bars only the horizontal lines, which hold for any hour.
        private static UiChartMark[] Marks(MarketState state, Stock s, StocksView v, UiChartDeskModel chart, bool today, UiWords words)
        {
            var marks = new List<UiChartMark>(4);
            int order = 0;
            for (int i = 0; i < state.Charts.Drawings.Count; i++)
            {
                ChartDrawing d = state.Charts.Drawings[i];
                if (d.StockId != s.Id)
                {
                    continue;
                }
                int ordinal = order++;
                bool selected = ordinal == v.Selected;
                UiChartMarkKind kind = MarkKindOf(d.Kind);
                if (today && kind != UiChartMarkKind.Level)
                {
                    continue;
                }
                float fromX = today ? 0f : DayX(chart.Day, d.FromIndex), toX = today ? 0f : DayX(chart.Day, d.ToIndex);
                float fromY = d.FromCents / 100f, toY = kind == UiChartMarkKind.Level || kind == UiChartMarkKind.Note ? fromY : d.ToCents / 100f;
                string label = null;
                if (kind == UiChartMarkKind.Level || kind == UiChartMarkKind.Note)
                {
                    label = DollarWords(fromY);
                }
                else if (kind == UiChartMarkKind.Measure)
                {
                    int bp = d.FromCents > 0 ? (int)Math.Max(int.MinValue, Math.Min(int.MaxValue, (long)(d.ToCents - d.FromCents) * 10000L / d.FromCents)) : 0;
                    int days = Math.Abs(d.ToIndex - d.FromIndex);
                    label = words("reallife_stk_measure", "{percent} in {days} days").Replace("{percent}", MarketText.Change(bp)).Replace("{days}", days.ToString());
                }
                marks.Add(new UiChartMark { FromX = fromX, ToX = toX, FromY = fromY, ToY = toY, Kind = kind, Label = label, Selected = selected, Ordinal = ordinal });
            }
            return marks.ToArray();
        }

        // A game day as a place on the candles: its candle, or a step before the first when the day is older than the history shown.
        private static float DayX(int[] days, int day)
        {
            int at = IndexOfDay(days, day);
            return at >= 0 ? at : -2f;
        }
    }
}
