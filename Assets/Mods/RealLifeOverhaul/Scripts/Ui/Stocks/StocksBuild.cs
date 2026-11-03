using System;
using System.Collections.Generic;
using RealLifeOverhaul.Markets;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The Stocks desk filled from the market the save already has. Words through the mod's keys. Built when the desk
    /// changes, not every frame. The old page stays for the way back.
    /// </summary>
    public static class StocksBuild
    {
        public static readonly string[] NavKeys =
        {
            "reallife_mkt_tab_market", "reallife_mkt_tab_companies", "reallife_mkt_tab_watch", "reallife_mkt_tab_portfolio",
            "reallife_mkt_tab_orders", "reallife_mkt_tab_history", "reallife_mkt_tab_bots", "reallife_stk_nav_listing",
            "reallife_mkt_tab_bonds", "reallife_mkt_tab_futures"
        };

        public static readonly UiIcon[] NavIcons =
        {
            UiIcon.ChartLineUp, UiIcon.Building, UiIcon.Eye, UiIcon.ChartPie, UiIcon.Receipt, UiIcon.Clock, UiIcon.Cpu,
            UiIcon.Presentation, UiIcon.Bank, UiIcon.Chart
        };

        public static readonly StocksPage[] NavPages =
        {
            StocksPage.Market, StocksPage.Companies, StocksPage.Watch, StocksPage.Portfolio, StocksPage.Orders, StocksPage.History,
            StocksPage.Bots, StocksPage.Listing, StocksPage.Bonds, StocksPage.Futures
        };

        public static int NavOf(StocksPage page)
        {
            for (int i = 0; i < NavPages.Length; i++)
            {
                if (NavPages[i] == page)
                {
                    return i;
                }
            }
            return page == StocksPage.Company || page == StocksPage.Ticket ? 1 : 0;
        }

        public static void Nav(StocksModel m, UiWords words)
        {
            m.Nav = new UiNavEntry[NavKeys.Length];
            for (int i = 0; i < NavKeys.Length; i++)
            {
                m.Nav[i] = new UiNavEntry { Icon = NavIcons[i], Tint = UiRole.None, Label = words(NavKeys[i], NavKeys[i]) };
            }
            m.Groups = new[] { words("reallife_stk_group_markets", "Markets"), words("reallife_stk_group_yours", "Yours"), words("reallife_stk_group_other", "Other markets") };
            m.GroupAt = new[] { 0, 3, 8 };
            m.NavAt = NavOf(m.Page);
            m.PromptBack = words("reallife_ui_prompt_back", "Back");
            m.PromptMove = words("reallife_ui_prompt_move", "Move");
            m.PromptSelect = words("reallife_ui_prompt_select", "Select");
        }

        public static StocksModel Page(MarketState state, StocksPage page, int stockId, int tool, bool buy, OrderType type,
            OrderLife life, long qty, int sort, int sector, int size, int focus, int rule, string find, UiWords words)
        {
            var m = new StocksModel { Title = words("reallife_mkt_app", "Stocks"), Page = page };
            Nav(m, words);
            if (state == null)
            {
                m.EmptyTitle = words("reallife_stk_empty_title", "The market is not open");
                m.EmptyBody = words("reallife_stk_empty_body", "Stocks opens with a city. Prices, orders and the tape show here.");
                return m;
            }
            switch (page)
            {
                case StocksPage.Companies: m.Table = Companies(state, words, false, sort, sector, size, find); break;
                case StocksPage.Watch: m.Table = Companies(state, words, true, sort, sector, size, find); break;
                case StocksPage.Market: Market(m, state, words, sort, sector, size, find); break;
                case StocksPage.Company: Company(m, state, stockId, tool, buy, type, life, qty, focus, words); break;
                case StocksPage.Ticket: Ticket(m, state, stockId, buy, type, life, qty, words); break;
                case StocksPage.Portfolio: Portfolio(m, state, words); break;
                case StocksPage.Orders: m.Statement = Orders(state, words, true); break;
                case StocksPage.History: m.Statement = Orders(state, words, false); break;
                case StocksPage.Bots: Bots(m, state, rule, words); break;
                case StocksPage.Listing: Listing(m, state, words); break;
                case StocksPage.Bonds: m.Table = Bonds(state, words); break;
                default: m.Table = Futures(state, words); break;
            }
            return m;
        }

        private static void Market(StocksModel m, MarketState state, UiWords words, int sort, int sector, int size, string find)
        {
            StocksMath.Today(state, out int up, out int down, out long turnover);
            m.Figures = new[]
            {
                Fig(words("reallife_mkt_col_index", "Index"), state.IndexLast.ToString(), StocksMath.ChangeBp(state.IndexPrevClose, state.IndexLast)),
                Fig(words("reallife_stk_small", "Small caps"), ((int)StocksMath.ValueOf(state.Stocks, (int)MarketSize.Small)).ToString(), 0),
                Fig(words("reallife_stk_all", "All shares"), ((int)StocksMath.ValueOf(state.Stocks, -1)).ToString(), 0),
                Fig(words("reallife_stk_today", "Market today"), up + " / " + down + " / " + MarketText.Large(turnover), 0)
            };
            m.TapeTitle = words("reallife_stk_tape", "Who is trading");
            m.Tape = Tape(state, 0, 3, words);
            m.Table = Companies(state, words, false, sort, sector, size, find);
            if (m.Table.RowCount == 0)
            {
                m.Table.EmptyTitle = words("reallife_stk_market_empty", "No companies are listed");
                m.Table.EmptyBody = words("reallife_stk_market_empty_body", "Listed companies show here with the day's move.");
            }
        }

        private static bool Matches(Stock s, string find)
        {
            if (string.IsNullOrEmpty(find))
            {
                return true;
            }
            string q = find.Trim();
            if (q.Length == 0)
            {
                return true;
            }
            return (s.Name != null && s.Name.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0)
                || (s.Ticker != null && s.Ticker.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static UiFigureModel Fig(string label, string value, int bp)
        {
            return new UiFigureModel { Label = label, Value = value, Change = bp == 0 ? null : MarketText.Change(bp), Good = bp >= 0 };
        }

        private static UiDataTableModel Companies(MarketState state, UiWords words, bool watch, int sort, int sector, int size, string find)
        {
            var table = Head(words);
            table.SortColumn = 2;
            table.SortDown = sort != (int)MarketSort.Losers;
            string[] sizes = { words("reallife_mkt_size_large", "Large"), words("reallife_mkt_size_mid", "Mid"), words("reallife_mkt_size_small", "Small") };
            string sectorWord = sector >= 0 && sector < (int)MarketSector.Count ? MarketText.Sector((MarketSector)sector) : words("reallife_stk_filter_all", "All sectors");
            string sizeWord = size >= 0 && size < sizes.Length ? sizes[size] : words("reallife_stk_all", "All shares");
            table.Filters = new[] { sectorWord, sizeWord };
            int n = 0;
            var rows = new UiDataRow[state.Stocks.Count];
            var order = new int[state.Stocks.Count];
            int listed = StocksMath.Order(state.Stocks, (MarketSort)(sort >= 0 && sort <= 3 ? sort : 0), sector, size, order);
            for (int k = 0; k < listed; k++)
            {
                Stock s = state.Stocks[order[k]];
                if (watch && !state.Watching(s.Id))
                {
                    continue;
                }
                if (!Matches(s, find))
                {
                    continue;
                }
                int bp = StocksMath.ChangeBp(s.PrevClose, s.Last);
                rows[n++] = new UiDataRow
                {
                    Cells = new[] { s.Name, MarketText.Price(s.Last), MarketText.Change(bp), s.DayVolume.ToString() },
                    Values = new[] { (int)UiIcon.Building, 0, bp > 0 ? 1 : bp < 0 ? -1 : 0, 0 },
                    Tones = new[] { UiTokens.Chart((int)s.Sector), UiRole.None, UiRole.None, UiRole.None },
                    Sub = s.Ticker
                };
            }
            table.Rows = rows;
            table.RowCount = n;
            if (n == 0)
            {
                table.EmptyTitle = words(watch ? "reallife_stk_watch_empty" : "reallife_stk_market_empty", watch ? "Nothing on the watch yet" : "No companies are listed");
                table.EmptyBody = words(watch ? "reallife_stk_watch_empty_body" : "reallife_stk_market_empty_body",
                    watch ? "Open a company and press Watch. It stays in this short list." : "Listed companies show here with the day's move.");
            }
            return table;
        }

        private static UiDataTableModel Head(UiWords words)
        {
            return new UiDataTableModel
            {
                Columns = new[]
                {
                    new UiDataColumn { Title = words("reallife_mkt_col_company", "Company"), Kind = UiCellKind.Tile, Sortable = true },
                    new UiDataColumn { Title = words("reallife_mkt_col_price", "Price"), Kind = UiCellKind.Figure, Width = 120f, Sortable = true },
                    new UiDataColumn { Title = words("reallife_mkt_col_today", "Today"), Kind = UiCellKind.Trend, Width = 140f, Sortable = true },
                    new UiDataColumn { Title = words("reallife_mkt_col_volume", "Volume"), Kind = UiCellKind.Figure, Width = 120f, Sortable = true }
                },
                Filters = new[] { words("reallife_stk_filter_all", "All sectors"), words("reallife_stk_sort_movers", "Movers") },
                SearchPlaceholder = words("reallife_stk_search", "Find a company"),
                EmptyIcon = UiIcon.ChartLineUp
            };
        }

        private static void Company(StocksModel m, MarketState state, int stockId, int tool, bool buy, OrderType type, OrderLife life, long qty, int focus, UiWords words)
        {
            Stock s = state.Stock(stockId) ?? First(state);
            if (s == null)
            {
                m.EmptyTitle = words("reallife_stk_market_empty", "No companies are listed");
                m.EmptyBody = words("reallife_stk_market_empty_body", "Listed companies show here with the day's move.");
                return;
            }
            int bp = StocksMath.ChangeBp(s.PrevClose, s.Last);
            m.Head = new UiFirmHeadModel
            {
                Name = s.Name, Glyph = UiIcon.Building, Kind = s.Ticker, KindIcon = UiIcon.Tag,
                Chips = new[] { MarketText.Sector(s.Sector) }, ChipTones = new[] { UiRole.None }
            };
            m.Note = TapeLine(state, s.Id, words);
            m.Figures = new[] { Fig(words("reallife_mkt_col_price", "Price"), MarketText.Price(s.Last), bp) };
            m.FactLabels = new[]
            {
                words("reallife_mkt_col_pe", "P/E"), words("reallife_mkt_col_yield", "Yield"), words("reallife_stk_cap", "Market cap"), words("reallife_stk_open", "Open"),
                words("reallife_stk_high", "High"), words("reallife_stk_low", "Low"), words("reallife_mkt_col_volume", "Volume")
            };
            int pe = StocksMath.PeTimes100(s.Last, s.Results.Count > 0 ? s.Results[s.Results.Count - 1].Eps : 0);
            m.FactValues = new[]
            {
                pe > 0 ? (pe / 100f).ToString("0.0") : words("reallife_stk_none", "None"),
                MarketText.Percent(StocksMath.YieldBp(s.Last, s.DpsCents)),
                MarketText.Large(MarketIndex.Value(s)),
                MarketText.Price(s.Open), MarketText.Price(s.High), MarketText.Price(s.Low), s.DayVolume.ToString()
            };
            m.Desk = Desk(state, s, focus, words);
            m.Tool = tool;
            m.Tools = new[] { " ", " ", " ", " ", " ", " ", " ", " ", " ", words("reallife_stk_clear", "Clear") };
            m.Chips = Chips(state, words);
            m.Signals = SignalLines(s, words);
            m.Ticket = TicketOf(state, s, buy, type, life, qty, words);
            m.PromptPlace = m.Ticket.Place;
            m.PromptOther = m.Ticket.Instead;
            int alerts = 1;
            for (int i = 0; i < state.Charts.Alerts.Count && alerts < 3; i++)
            {
                if (state.Charts.Alerts[i].StockId == s.Id)
                {
                    alerts++;
                }
            }
            m.Alerts = new string[alerts];
            m.Alerts[0] = words("reallife_stk_alert_add", "Add an alert");
            int n = 1;
            for (int i = 0; i < state.Charts.Alerts.Count && n < m.Alerts.Length; i++)
            {
                if (state.Charts.Alerts[i].StockId == s.Id)
                {
                    m.Alerts[n++] = words("reallife_stk_alert", "Alert") + " " + MarketText.Price(state.Charts.Alerts[i].Cents);
                }
            }
        }

        private static string[] Chips(MarketState state, UiWords words)
        {
            var list = new List<string>(8);
            list.Add(FrameWord(state.Charts.Frame, words));
            list.Add(KindWord(state.Charts.Kind, words));
            if ((state.Charts.Layers & (int)ChartLayer.Sma20) != 0)
            {
                list.Add(words("reallife_mkt_overlay_sma20", "Average of 20"));
            }
            if ((state.Charts.Layers & (int)ChartLayer.Sma50) != 0)
            {
                list.Add(words("reallife_mkt_overlay_sma50", "Average of 50"));
            }
            if ((state.Charts.Layers & (int)ChartLayer.Ema20) != 0)
            {
                list.Add(words("reallife_mkt_overlay_ema20", "Weighted average of 20"));
            }
            if ((state.Charts.Layers & (int)ChartLayer.Bollinger) != 0)
            {
                list.Add(words("reallife_mkt_overlay_bollinger", "Bollinger bands"));
            }
            if (state.Charts.FirstPane != ChartPane.None)
            {
                list.Add(Pane(state.Charts.FirstPane, words));
            }
            if (state.Charts.SecondPane != ChartPane.None)
            {
                list.Add(Pane(state.Charts.SecondPane, words));
            }
            list.Add(words("reallife_stk_add_indicator", "+ Indicator"));
            return list.ToArray();
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

        private static string Pane(ChartPane pane, UiWords words)
        {
            switch (pane)
            {
                case ChartPane.Rsi: return words("reallife_stk_rsi", "RSI");
                case ChartPane.Macd: return words("reallife_stk_macd", "MACD");
                case ChartPane.Stochastic: return words("reallife_stk_stoch", "Stochastic");
                case ChartPane.Atr: return words("reallife_stk_atr", "ATR");
                default: return words("reallife_stk_obv", "On balance volume");
            }
        }

        private static string[] SignalLines(Stock s, UiWords words)
        {
            int n = Math.Min(s.Daily.Count, 80);
            if (n < 2)
            {
                return new string[0];
            }
            var close = new double[n];
            int from = s.Daily.Count - n;
            for (int i = 0; i < n; i++)
            {
                close[i] = s.Daily[from + i].C;
            }
            var a = new double[n];
            var b = new double[n];
            var c = new double[n];
            var scratch = new double[n];
            ChartSignal signal = ChartBook.Signals(close, n, a, b, c, scratch);
            return new[]
            {
                Way(signal.Sma, words, "reallife_stk_sig_sma_up", "Price is over its 50 day average.",
                    "reallife_stk_sig_sma_down", "Price is under its 50 day average.", "reallife_stk_sig_wait", "Not enough days for a reading yet."),
                Way(signal.Rsi, words, "reallife_stk_sig_rsi_high", "RSI is overbought.",
                    "reallife_stk_sig_rsi_low", "RSI is oversold.", "reallife_stk_sig_rsi_mid", "RSI is in the middle."),
                Way(signal.Macd, words, "reallife_stk_sig_macd_up", "MACD is over its signal.",
                    "reallife_stk_sig_macd_down", "MACD is under its signal.", "reallife_stk_sig_wait", "Not enough days for a reading yet."),
                Way(signal.Band, words, "reallife_stk_sig_band_up", "Price is outside the upper band.",
                    "reallife_stk_sig_band_down", "Price is outside the lower band.", "reallife_stk_sig_band_in", "Price is inside the bands.")
            };
        }

        private static string Way(int n, UiWords words, string upKey, string up, string downKey, string down,
            string midKey, string mid)
        {
            if (n > 0)
            {
                return words(upKey, up);
            }
            if (n < 0)
            {
                return words(downKey, down);
            }
            return words(midKey, mid);
        }

        private static UiChartDeskModel Desk(MarketState state, Stock s, int focus, UiWords words)
        {
            var built = new List<Candle>(128);
            LoadCandles(state, s, state.Charts.Frame, built);
            int n = Math.Min(built.Count, 48);
            var m = new UiChartDeskModel
            {
                Kind = state.Charts.Kind, Title = s.Ticker, Focus = n > 0 ? n - 1 : -1,
                EmptyTitle = words("reallife_stk_chart_empty", "No candles yet"),
                EmptyBody = words("reallife_stk_chart_empty_body", "Candles appear as the market trades.")
            };
            if (n == 0)
            {
                return m;
            }
            m.Open = new float[n];
            m.High = new float[n];
            m.Low = new float[n];
            m.Close = new float[n];
            m.Volume = new float[n];
            int from = built.Count - n;
            var close = new double[n];
            var high = new double[n];
            var low = new double[n];
            var vol = new double[n];
            for (int i = 0; i < n; i++)
            {
                Candle c = built[from + i];
                m.Open[i] = c.O;
                m.High[i] = c.H;
                m.Low[i] = c.L;
                m.Close[i] = c.C;
                m.Volume[i] = c.V;
                close[i] = c.C;
                high[i] = c.H;
                low[i] = c.L;
                vol[i] = c.V;
            }
            int at = focus >= 0 && focus < n ? focus : n - 1;
            m.Focus = at;
            m.LastTag = MarketText.Price(s.Last);
            m.LastUp = s.Last >= s.PrevClose;
            Candle read = built[from + at];
            m.Read = words("reallife_stk_open", "Open") + " " + MarketText.Price(read.O) + "  " + words("reallife_stk_high", "High") + " " + MarketText.Price(read.H)
                + "  " + words("reallife_stk_low", "Low") + " " + MarketText.Price(read.L) + "  " + words("reallife_stk_read", "Last") + " " + MarketText.Price(read.C);
            m.Lines = Overlays(state, close, n, words);
            m.Panes = Panes(state, close, high, low, vol, n, words);
            m.Marks = Marks(state, s.Id, from);
            return m;
        }

        /// <summary>The candle's place in the frame the desk is showing, so a drawing stays on that candle.</summary>
        public static int ChartIndex(MarketState state, Stock s, int shown)
        {
            if (state == null || s == null)
            {
                return Math.Max(0, shown);
            }
            var built = new List<Candle>(128);
            LoadCandles(state, s, state.Charts.Frame, built);
            int n = Math.Min(built.Count, 48);
            int origin = Math.Max(0, built.Count - n);
            int at = shown < 0 ? Math.Max(0, n - 1) : Math.Min(shown, Math.Max(0, n - 1));
            return origin + at;
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

        private static UiChartLine[] Overlays(MarketState state, double[] close, int n, UiWords words)
        {
            var lines = new List<UiChartLine>(6);
            var a = new double[n];
            var b = new double[n];
            var c = new double[n];
            if ((state.Charts.Layers & (int)ChartLayer.Sma20) != 0)
            {
                MarketIndicators.Sma(close, n, 20, a);
                lines.Add(Line(words("reallife_mkt_overlay_sma20", "Average of 20"), a, UiTokens.Chart(0)));
            }
            if ((state.Charts.Layers & (int)ChartLayer.Sma50) != 0)
            {
                MarketIndicators.Sma(close, n, 50, a);
                lines.Add(Line(words("reallife_mkt_overlay_sma50", "Average of 50"), a, UiTokens.Chart(1)));
            }
            if ((state.Charts.Layers & (int)ChartLayer.Ema20) != 0)
            {
                MarketIndicators.Ema(close, n, 20, a);
                lines.Add(Line(words("reallife_mkt_overlay_ema20", "Weighted average of 20"), a, UiTokens.Chart(2)));
            }
            if ((state.Charts.Layers & (int)ChartLayer.Bollinger) != 0)
            {
                MarketIndicators.Bollinger(close, n, 20, 2.0, a, b, c);
                lines.Add(Line(words("reallife_mkt_overlay_bollinger", "Bollinger bands"), b, UiTokens.Chart(3)));
                lines.Add(Line(words("reallife_mkt_overlay_bollinger", "Bollinger bands"), c, UiTokens.Chart(4)));
            }
            return lines.ToArray();
        }

        private static UiChartLine[] Panes(MarketState state, double[] close, double[] high, double[] low, double[] vol, int n, UiWords words)
        {
            int count = (state.Charts.FirstPane != ChartPane.None ? 1 : 0) + (state.Charts.SecondPane != ChartPane.None ? 1 : 0);
            if (count == 0)
            {
                return new UiChartLine[0];
            }
            var panes = new UiChartLine[count];
            int at = 0;
            if (state.Charts.FirstPane != ChartPane.None)
            {
                panes[at++] = PaneLine(state.Charts.FirstPane, close, high, low, vol, n, words);
            }
            if (state.Charts.SecondPane != ChartPane.None)
            {
                panes[at] = PaneLine(state.Charts.SecondPane, close, high, low, vol, n, words);
            }
            return panes;
        }

        private static UiChartLine PaneLine(ChartPane pane, double[] close, double[] high, double[] low, double[] vol, int n, UiWords words)
        {
            var a = new double[n];
            var b = new double[n];
            var c = new double[n];
            var scratch = new double[n];
            switch (pane)
            {
                case ChartPane.Macd:
                    MarketIndicators.Macd(close, n, 12, 26, 9, a, b, c, scratch);
                    break;
                case ChartPane.Stochastic:
                    MarketIndicators.Stochastic(high, low, close, n, 14, 3, a, b);
                    break;
                case ChartPane.Atr:
                    MarketIndicators.Atr(high, low, close, n, 14, a);
                    break;
                case ChartPane.Obv:
                    MarketIndicators.Obv(close, vol, n, a);
                    break;
                default:
                    MarketIndicators.Rsi(close, n, 14, a);
                    break;
            }
            return Line(Pane(pane, words), a, UiTokens.Chart(5));
        }

        private static UiChartMark[] Marks(MarketState state, int stockId, int origin)
        {
            int count = 0;
            for (int i = 0; i < state.Charts.Drawings.Count; i++)
            {
                if (state.Charts.Drawings[i].StockId == stockId)
                {
                    count++;
                }
            }
            if (count == 0)
            {
                return new UiChartMark[0];
            }
            var marks = new List<UiChartMark>(count);
            for (int i = 0; i < state.Charts.Drawings.Count; i++)
            {
                ChartDrawing d = state.Charts.Drawings[i];
                if (d.StockId != stockId)
                {
                    continue;
                }
                float x0 = d.FromIndex - origin;
                float x1 = d.ToIndex - origin;
                if (d.Kind == ChartMark.Fibonacci)
                {
                    Fib(marks, x0, x1, d.FromCents, d.ToCents);
                }
                else
                {
                    marks.Add(new UiChartMark { FromX = x0, ToX = x1, FromY = d.FromCents, ToY = d.Kind == ChartMark.Level ? d.FromCents : d.ToCents, Tint = UiRole.Text });
                }
            }
            return marks.ToArray();
        }

        private static void Fib(List<UiChartMark> marks, float x0, float x1, int fromCents, int toCents)
        {
            int[] share = { 382, 500, 618 };
            for (int i = 0; i < share.Length; i++)
            {
                int y = fromCents + (toCents - fromCents) * share[i] / 1000;
                marks.Add(new UiChartMark { FromX = x0, ToX = x1, FromY = y, ToY = y, Tint = UiTokens.Chart(i) });
            }
        }

        private static UiChartLine Line(string name, double[] values, UiRole tint)
        {
            var v = new float[values.Length];
            for (int i = 0; i < values.Length; i++)
            {
                v[i] = double.IsNaN(values[i]) ? float.NaN : (float)values[i];
            }
            return new UiChartLine { Name = name, Values = v, Tint = tint };
        }

        private static StocksTicket TicketOf(MarketState state, Stock s, bool buy, OrderType type, OrderLife life, long qty, UiWords words)
        {
            if (qty < 1)
            {
                qty = 1;
            }
            int price = Math.Max(1, s.Last);
            long value = MarketTrading.Value(qty, price, buy);
            long fee = MarketTrading.Commission(value, MarketCatalogue.CommissionLeastAtStart);
            long cash = state.Account.Cash;
            long most = MostShares(cash, price, buy);
            long held = state.Held(s.Id);
            long afterShares = held + (buy ? qty : -qty);
            Position position = state.Position(s.Id);
            long cost = position != null ? position.Cost : 0;
            long afterCost = buy ? cost + value + fee : cost;
            long average = afterShares != 0 ? afterCost / Math.Abs(afterShares) : 0;
            return new StocksTicket
            {
                Side = buy ? words("reallife_mkt_buy", "Buy") : words("reallife_mkt_sell", "Sell"),
                Kind = type == OrderType.Limit ? words("reallife_stk_limit", "Limit") : type == OrderType.Stop ? words("reallife_stk_stop", "Stop") : words("reallife_stk_market_order", "Market"),
                Life = life == OrderLife.UntilCancelled ? words("reallife_stk_gtc", "Until cancelled") : words("reallife_stk_day", "Day"),
                Shares = qty.ToString(),
                Limit = type == OrderType.Market ? words("reallife_stk_max", "Most you can buy") : words("reallife_mkt_col_price", "Price"),
                Max = type == OrderType.Market ? most.ToString() : MarketText.Price(price),
                Cost = MarketText.Money(value),
                Fee = MarketText.Money(fee),
                Total = MarketText.Money(buy ? value + fee : Math.Max(0, value - fee)),
                CashAfter = MarketText.Money(Math.Max(0, cash - (buy ? value + fee : -(value - fee)))),
                After = words("reallife_stk_position_after", "Position after") + " " + afterShares + "  " + MarketText.Money(average),
                Place = words("reallife_stk_place", "Place order"),
                Instead = buy ? words("reallife_stk_sell_instead", "Sell instead") : words("reallife_stk_buy_instead", "Buy instead")
            };
        }

        private static long MostShares(long cash, int price, bool buy)
        {
            if (!buy || cash <= 0 || price <= 0)
            {
                return 0;
            }
            long guess = cash * 100L / price;
            for (int i = 0; i < 6 && guess > 0; i++)
            {
                long v = MarketTrading.Value(guess, price, true);
                if (v + MarketTrading.Commission(v, MarketCatalogue.CommissionLeastAtStart) <= cash)
                {
                    return guess;
                }
                long next = guess * 99L / 100L;
                guess = next < guess ? next : guess - 1;
            }
            return Math.Max(0, guess);
        }

        private static void Ticket(StocksModel m, MarketState state, int stockId, bool buy, OrderType type, OrderLife life, long qty, UiWords words)
        {
            Stock s = state.Stock(stockId) ?? First(state);
            StocksTicket t = s != null ? TicketOf(state, s, buy, type, life, qty, words) : new StocksTicket { Place = words("reallife_stk_place", "Place order") };
            m.Decision = new UiDecisionModel
            {
                Kicker = s != null ? s.Ticker : words("reallife_mkt_app", "Stocks"),
                Title = words("reallife_stk_place", "Place order"),
                Emblem = UiIcon.ChartLineUp,
                Body = s == null ? words("reallife_stk_market_empty_body", "Listed companies show here with the day's move.")
                    : t.Side + " " + t.Shares + "  " + t.Cost + "  " + t.Fee,
                Buttons = new[] { new UiActionModel { Label = t.Place, Kind = UiButtonKind.Primary, Key = UiAction.Alt } },
                Prompts = new[] { new UiPrompt { Action = UiAction.Back, Label = words("reallife_ui_prompt_back", "Back") }, new UiPrompt { Action = UiAction.Alt, Label = t.Place } },
                PromptCount = 2
            };
        }

        private static void Portfolio(StocksModel m, MarketState state, UiWords words)
        {
            if (state.Positions.Count == 0)
            {
                m.EmptyTitle = words("reallife_stk_port_empty", "You hold nothing yet");
                m.EmptyBody = words("reallife_stk_port_empty_body", "Buy shares from a company. The holding, its gain and the sector split show here.");
                m.Table = null;
                return;
            }
            long worth = 0;
            long div = 0;
            var by = new long[(int)MarketSector.Count];
            var values = new long[state.Positions.Count];
            var rows = new UiDataRow[state.Positions.Count];
            for (int i = 0; i < state.Positions.Count; i++)
            {
                Position p = state.Positions[i];
                Stock s = state.Stock(p.StockId);
                long value = s != null ? p.Shares * s.Last / 100L : 0L;
                values[i] = value;
                worth += value;
                if (s != null)
                {
                    int sector = (int)s.Sector;
                    if (sector >= 0 && sector < by.Length)
                    {
                        by[sector] += value;
                    }
                    int dps = s.DeclaredCents > 0 ? s.DeclaredCents : s.DpsCents;
                    div += p.Shares * dps / 100L;
                }
                int bp = p.Cost > 0 ? (int)((value - p.Cost) * 10000L / p.Cost) : 0;
                rows[i] = new UiDataRow
                {
                    Cells = new[] { s != null ? s.Name : words("reallife_stk_none", "None"), p.Shares.ToString(), MarketText.Money(value), "0%", MarketText.Change(bp) },
                    Values = new[] { (int)UiIcon.Building, 0, 0, 0, bp > 0 ? 1 : bp < 0 ? -1 : 0 },
                    Tones = new[] { UiRole.None, UiRole.None, UiRole.None, UiRole.None, UiRole.None },
                    Sub = s != null ? s.Ticker : string.Empty
                };
            }
            for (int i = 0; i < rows.Length; i++)
            {
                int weight = worth > 0 ? (int)(values[i] * 100L / worth) : 0;
                rows[i].Cells[3] = weight + "%";
            }
            m.Figures = new[]
            {
                Fig(words("reallife_stk_worth", "Worth"), MarketText.Money(worth), 0),
                Fig(words("reallife_stk_realised", "Realised"), MarketText.Money(state.Account.Realised), state.Account.Realised > 0 ? 1 : state.Account.Realised < 0 ? -1 : 0),
                Fig(words("reallife_stk_next_div", "Next dividends"), MarketText.Money(div), 0)
            };
            m.Chart = WorthChart(state, words);
            m.Donut = Sectors(by, worth, out string[] labels, out string[] amounts);
            m.DonutLabels = labels;
            m.DonutValues = amounts;
            m.Table = new UiDataTableModel
            {
                Columns = new[]
                {
                    new UiDataColumn { Title = words("reallife_mkt_col_company", "Company"), Kind = UiCellKind.Tile },
                    new UiDataColumn { Title = words("reallife_mkt_col_shares", "Shares"), Kind = UiCellKind.Figure, Width = 120f },
                    new UiDataColumn { Title = words("reallife_stk_worth", "Worth"), Kind = UiCellKind.Figure, Width = 140f },
                    new UiDataColumn { Title = words("reallife_stk_weight", "Weight"), Kind = UiCellKind.Figure, Width = 100f },
                    new UiDataColumn { Title = words("reallife_mkt_col_today", "Today"), Kind = UiCellKind.Trend, Width = 120f }
                },
                Rows = rows, RowCount = rows.Length, EmptyIcon = UiIcon.ChartPie
            };
        }

        private static UiChartModel WorthChart(MarketState state, UiWords words)
        {
            const int n = 32;
            var values = new float[n];
            bool any = false;
            for (int p = 0; p < state.Positions.Count; p++)
            {
                Position held = state.Positions[p];
                Stock s = state.Stock(held.StockId);
                if (s == null || s.Daily.Count < 2)
                {
                    continue;
                }
                int len = Math.Min(n, s.Daily.Count);
                int origin = s.Daily.Count - len;
                var days = new int[len];
                var closes = new int[len];
                for (int i = 0; i < len; i++)
                {
                    days[i] = s.Daily[origin + i].Day;
                    closes[i] = s.Daily[origin + i].C;
                }
                var tradeDay = new int[16];
                var tradeShares = new long[16];
                int trades = 0;
                long start = held.Shares;
                for (int t = 0; t < state.Trades.Count && trades < tradeDay.Length; t++)
                {
                    Trade trade = state.Trades[t];
                    if (trade.StockId != held.StockId || (trade.Kind != TradeKind.Bought && trade.Kind != TradeKind.Sold) || trade.Day < days[0] || trade.Day > days[len - 1])
                    {
                        continue;
                    }
                    long signed = trade.Kind == TradeKind.Sold ? -trade.Qty : trade.Qty;
                    tradeDay[trades] = trade.Day;
                    tradeShares[trades] = signed;
                    trades++;
                    start -= signed;
                }
                var into = new long[len];
                StocksMath.Mark(days, closes, len, tradeDay, tradeShares, trades, start, into);
                int pad = n - len;
                for (int i = 0; i < len; i++)
                {
                    values[pad + i] += into[i];
                }
                any = true;
            }
            if (!any)
            {
                return null;
            }
            var points = new string[n];
            for (int i = 0; i < n; i++)
            {
                points[i] = string.Empty;
            }
            return new UiChartModel
            {
                Kind = UiChartKind.Line,
                Title = words("reallife_stk_worth", "Worth"),
                Points = points,
                Series = new[] { new UiSeries { Label = words("reallife_stk_worth", "Worth"), Values = values, Tint = UiRole.Accent } }
            };
        }

        private static float[] Sectors(long[] by, long worth, out string[] labels, out string[] amounts)
        {
            int n = 0;
            for (int i = 0; i < by.Length && n < 8; i++)
            {
                if (by[i] > 0)
                {
                    n++;
                }
            }
            if (n == 0 || worth <= 0)
            {
                labels = new string[0];
                amounts = new string[0];
                return new float[0];
            }
            var share = new float[n];
            labels = new string[n];
            amounts = new string[n];
            int at = 0;
            for (int i = 0; i < by.Length && at < n; i++)
            {
                if (by[i] <= 0)
                {
                    continue;
                }
                share[at] = by[i] / (float)worth;
                labels[at] = MarketText.Sector((MarketSector)i);
                amounts[at] = (by[i] * 100L / worth) + "%";
                at++;
            }
            return share;
        }

        private static UiStatementModel Orders(MarketState state, UiWords words, bool open)
        {
            var lines = new System.Collections.Generic.List<UiStatementLine>();
            if (open)
            {
                for (int i = 0; i < state.Orders.Count; i++)
                {
                    RealLifeOverhaul.Markets.Order o = state.Orders[i];
                    if (!o.IsOpen)
                    {
                        continue;
                    }
                    Stock s = state.Stock(o.StockId);
                    lines.Add(new UiStatementLine
                    {
                        When = o.PlacedDay.ToString(), Icon = UiIcon.Receipt,
                        What = (s != null ? s.Ticker : string.Empty) + " " + o.Qty,
                        Out = MarketText.Money(o.Qty * o.LimitCents / 100L)
                    });
                }
            }
            else
            {
                for (int i = state.Trades.Count - 1; i >= 0 && lines.Count < 12; i--)
                {
                    Trade t = state.Trades[i];
                    string what = t.Kind == TradeKind.Bought ? words("reallife_mkt_buy", "Buy") : t.Kind == TradeKind.Sold ? words("reallife_mkt_sell", "Sell") : words("reallife_stk_cash", "Cash");
                    lines.Add(new UiStatementLine
                    {
                        When = t.Day.ToString(), Icon = UiIcon.Clock, What = what,
                        In = t.Value > 0 ? MarketText.Money(t.Value) : null,
                        Out = t.Value < 0 ? MarketText.Money(-t.Value) : null
                    });
                }
            }
            return new UiStatementModel
            {
                Account = words(open ? "reallife_mkt_tab_orders" : "reallife_mkt_tab_history", open ? "Orders" : "History"),
                AccountIcon = UiIcon.Receipt,
                BalanceLabel = words("reallife_stk_cash", "Cash"),
                Balance = MarketText.Money(state.Account.Cash),
                Columns = new[]
                {
                    words("reallife_mkt_col_day", "Day"), words("reallife_mkt_col_what", "What"),
                    words("reallife_stk_in", "In"), words("reallife_stk_out", "Out"), words("reallife_stk_after", "After")
                },
                Groups = lines.Count == 0 ? new UiStatementGroup[0] : new[] { new UiStatementGroup { Label = words("reallife_stk_today", "Market today"), Lines = lines.ToArray() } },
                EmptyTitle = words(open ? "reallife_stk_orders_empty" : "reallife_stk_hist_empty", open ? "No open orders" : "No trades yet"),
                EmptyBody = words(open ? "reallife_stk_orders_empty_body" : "reallife_stk_hist_empty_body",
                    open ? "An order you place and that has not filled shows here." : "Fills, dividends and cash you move show here.")
            };
        }

        private static void Bots(StocksModel m, MarketState state, int rule, UiWords words)
        {
            long budget = 0, day = 0;
            for (int i = 0; i < state.Bots.Count; i++)
            {
                budget += state.Bots[i].Capital;
                day += state.Bots[i].Realised;
            }
            for (int i = 0; i < state.Rules.Count; i++)
            {
                budget += state.Rules[i].Budget;
                day += state.Rules[i].DayResult;
            }
            m.Figures = new[]
            {
                Fig(words("reallife_stk_budget", "Budget in use"), MarketText.Money(budget), 0),
                Fig(words("reallife_stk_day_result", "Today"), MarketText.Money(day), day > 0 ? 1 : day < 0 ? -1 : 0)
            };
            int n = state.Bots.Count + state.Rules.Count;
            var rows = new UiDataRow[n];
            int at = 0;
            for (int i = 0; i < state.Bots.Count; i++)
            {
                Bot b = state.Bots[i];
                rows[at++] = BotRow(b.Def != null ? b.Def.Name : words("reallife_stk_bot", "Bot"), StocksMath.BotChip(b), words);
            }
            for (int i = 0; i < state.Rules.Count; i++)
            {
                RuleBot r = state.Rules[i];
                rows[at++] = BotRow(RuleWord((int)r.Kind, words), r.Chip, words);
            }
            m.Table = new UiDataTableModel
            {
                Columns = new[]
                {
                    new UiDataColumn { Title = words("reallife_mkt_col_company", "Company"), Kind = UiCellKind.Tile },
                    new UiDataColumn { Title = words("reallife_mkt_col_state", "State"), Kind = UiCellKind.Chip, Width = 180f }
                },
                Rows = rows, RowCount = n, EmptyIcon = UiIcon.Cpu,
                Filters = new[] { RuleWord(rule, words) },
                EmptyTitle = words("reallife_stk_bots_empty", "No bots yet"),
                EmptyBody = words("reallife_stk_bots_empty_body", "Hire a firm, or add a rule. Rivals run the same rules.")
            };
            m.PromptOther = words("reallife_stk_add_rule", "Add a rule");
        }

        private static string RuleWord(int rule, UiWords words)
        {
            string[] keys =
            {
                "reallife_stk_rule_dip", "reallife_stk_rule_rider", "reallife_stk_rule_div", "reallife_stk_rule_rebalance",
                "reallife_stk_rule_grid", "reallife_stk_rule_earn", "reallife_stk_rule_stop", "reallife_stk_rule_gap"
            };
            string[] english = { "Dip buyer", "Trend rider", "Dividend", "Rebalance", "Grid", "Earnings", "Stop guard", "Open gap" };
            int i = rule >= 0 && rule < keys.Length ? rule : 0;
            return words(keys[i], english[i]);
        }

        private static UiDataRow BotRow(string name, int chip, UiWords words)
        {
            string[] chips =
            {
                words("reallife_stk_running", "Running"), words("reallife_stk_waiting", "Waiting"), words("reallife_stk_paused", "Paused"),
                words("reallife_stk_out_budget", "Out of budget"), words("reallife_stk_stopped", "Stopped"), words("reallife_mkt_bot_status_selling", "Selling up")
            };
            UiRole[] tones = { UiRole.Ok, UiRole.None, UiRole.Warn, UiRole.Danger, UiRole.Muted, UiRole.Warn };
            int c = chip < 0 || chip >= chips.Length ? 0 : chip;
            return new UiDataRow { Cells = new[] { name, chips[c] }, Values = new[] { (int)UiIcon.Cpu, 0 }, Tones = new[] { UiRole.None, tones[c] }, Sub = string.Empty };
        }

        private static void Listing(StocksModel m, MarketState state, UiWords words)
        {
            ListingFile f = state.Listing;
            if (f == null || f.Empty)
            {
                m.EmptyTitle = words("reallife_stk_list_empty", "Your company is not listing");
                m.EmptyBody = words("reallife_stk_list_empty_body", "When you take the company to market, the stages, the book and the holders show here.");
                return;
            }
            int price = f.SetCents > 0 ? f.SetCents : f.PriceCents;
            m.Hero = new UiHeroModel
            {
                Icon = UiIcon.Presentation,
                Title = string.IsNullOrEmpty(f.Name) ? words("reallife_stk_list_empty", "Your company is not listing") : f.Name,
                Kicker = f.Ticker, Line = MarketText.Price(price),
                Progress = (int)f.Stage * 100 / (int)ListingStage.Listed
            };
            m.Note = words("reallife_stk_policy", "Pay out") + " " + (ListingCatalogue.BoardPayoutBp / 100) + "%";
            m.Track = new UiTrackModel
            {
                Title = words("reallife_stk_stages", "Stages"),
                Steps = new[]
                {
                    Step(words("reallife_lst_stage_advised", "advisers engaged"), f.Stage > ListingStage.Advised, f.Stage == ListingStage.Advised),
                    Step(words("reallife_lst_stage_auditing", "being audited"), f.Stage > ListingStage.Auditing, f.Stage == ListingStage.Auditing),
                    Step(words("reallife_lst_stage_building", "the book is open"), f.Stage > ListingStage.Building, f.Stage == ListingStage.Building),
                    Step(words("reallife_lst_stage_priced", "priced"), f.Stage > ListingStage.Priced, f.Stage == ListingStage.Priced),
                    Step(words("reallife_lst_stage_listed", "on the market"), f.Stage > ListingStage.Listed, f.Stage == ListingStage.Listed)
                },
                StateWords = new[]
                {
                    words("reallife_stk_done", "Done"), words("reallife_stk_now", "Now"), words("reallife_stk_next", "Next"),
                    words("reallife_stk_later", "Later"), words("reallife_stk_later", "Later")
                }
            };
            var rows = new UiDataRow[f.BidCount];
            for (int i = 0; i < f.BidCount; i++)
            {
                BookBid b = f.Bids[i];
                rows[i] = new UiDataRow
                {
                    Cells = new[] { words("reallife_stk_bid", "Bid"), MarketText.Price(b.Cents), b.Size.ToString(), b.Filled.ToString() },
                    Values = new[] { (int)UiIcon.Bank, 0, 0, 0 },
                    Tones = new[] { UiRole.None, UiRole.None, UiRole.None, UiRole.None }
                };
            }
            m.Table = new UiDataTableModel
            {
                Columns = new[]
                {
                    new UiDataColumn { Title = words("reallife_stk_bidder", "Bidder"), Kind = UiCellKind.Tile },
                    new UiDataColumn { Title = words("reallife_mkt_col_price", "Price"), Kind = UiCellKind.Figure, Width = 120f },
                    new UiDataColumn { Title = words("reallife_mkt_col_shares", "Shares"), Kind = UiCellKind.Figure, Width = 120f },
                    new UiDataColumn { Title = words("reallife_stk_filled", "Filled"), Kind = UiCellKind.Figure, Width = 120f }
                },
                Rows = rows, RowCount = f.BidCount, EmptyIcon = UiIcon.Presentation,
                EmptyTitle = words("reallife_stk_book_empty", "The book is not open"),
                EmptyBody = words("reallife_stk_book_empty_body", "Bids show here when the book is priced, largest first.")
            };
        }

        private static UiTrackStep Step(string label, bool done, bool now)
        {
            return new UiTrackStep { Icon = UiIcon.Check, Label = label, State = done ? UiStepState.Done : now ? UiStepState.Now : UiStepState.Later };
        }

        private static string[] Tape(MarketState state, int stockId, int most, UiWords words)
        {
            var lines = new System.Collections.Generic.List<string>(most);
            for (int i = 0; i < state.Names.Count && lines.Count < most; i++)
            {
                TradePrint p = state.Names.Newest(i);
                if (stockId != 0 && p.StockId != stockId)
                {
                    continue;
                }
                lines.Add(Who(p, state, words));
            }
            return lines.ToArray();
        }

        private static string TapeLine(MarketState state, int stockId, UiWords words)
        {
            if (!state.Names.Latest(stockId, out TradePrint p))
            {
                return null;
            }
            return Who(p, state, words);
        }

        private static string Who(TradePrint p, MarketState state, UiWords words)
        {
            string who;
            switch (p.Who)
            {
                case TradeWho.You: who = words("reallife_stk_you", "You"); break;
                case TradeWho.Hired:
                    BotFirm firm = MarketBots.Firm(p.WhoId);
                    who = firm != null ? firm.Name : words("reallife_stk_bot", "Bot");
                    break;
                case TradeWho.Rival:
                    who = p.WhoId >= 0 && p.WhoId < MarketCatalogue.RivalTemplates.Length && !string.IsNullOrEmpty(MarketCatalogue.RivalTemplates[p.WhoId].Name)
                        ? MarketCatalogue.RivalTemplates[p.WhoId].Name : words("reallife_stk_rival", "A rival");
                    break;
                case TradeWho.Fund: who = words("reallife_stk_fund", "A fund"); break;
                case TradeWho.Investor: who = words("reallife_stk_investor", "An investor"); break;
                case TradeWho.Broker: who = words("reallife_stk_broker", "A broker"); break;
                default: who = words("reallife_stk_rule", "Rule"); break;
            }
            Stock s = state.Stock(p.StockId);
            string side = p.Side == TradeSide.Buy ? words("reallife_mkt_buy", "Buy") : words("reallife_mkt_sell", "Sell");
            return who + " " + side.ToLowerInvariant() + " " + p.Size + " " + (s != null ? s.Ticker : string.Empty);
        }

        private static Stock First(MarketState state)
        {
            for (int i = 0; i < state.Stocks.Count; i++)
            {
                if (state.Stocks[i].Listed)
                {
                    return state.Stocks[i];
                }
            }
            return null;
        }

        private static UiDataTableModel Bonds(MarketState state, UiWords words)
        {
            int n = state.Bonds.Holdings.Count;
            var rows = new UiDataRow[n];
            for (int i = 0; i < n; i++)
            {
                BondHolding h = state.Bonds.Holdings[i];
                BondDef def = BondCatalogue.Find(h.Def);
                string name = !string.IsNullOrEmpty(h.Issuer) ? h.Issuer : BondName(def, words);
                rows[i] = new UiDataRow
                {
                    Cells = new[] { name, MarketText.Money(h.Face), MarketText.Percent(h.RateBp), h.EndDay.ToString() },
                    Values = new[] { (int)UiIcon.Bank, 0, 0, 0 },
                    Tones = new[] { UiRole.None, UiRole.None, UiRole.None, UiRole.None }
                };
            }
            return new UiDataTableModel
            {
                Columns = new[]
                {
                    new UiDataColumn { Title = words("reallife_mkt_tab_bonds", "Bonds"), Kind = UiCellKind.Tile },
                    new UiDataColumn { Title = words("reallife_stk_worth", "Worth"), Kind = UiCellKind.Figure, Width = 140f },
                    new UiDataColumn { Title = words("reallife_mkt_col_yield", "Yield"), Kind = UiCellKind.Figure, Width = 120f },
                    new UiDataColumn { Title = words("reallife_mkt_col_day", "Day"), Kind = UiCellKind.Figure, Width = 100f }
                },
                Rows = rows, RowCount = n, EmptyIcon = UiIcon.Bank,
                EmptyTitle = words("reallife_stk_bonds_empty", "No bonds yet"),
                EmptyBody = words("reallife_stk_bonds_empty_body", "Treasury and city bonds you buy show here, with what they pay.")
            };
        }

        private static string BondName(BondDef def, UiWords words)
        {
            if (def != null && def.Kind == BondKind.City)
            {
                return words("reallife_mkt_bond_kind_city", "City");
            }
            if (def != null && def.Kind == BondKind.Deposit)
            {
                return words("reallife_mkt_bond_kind_deposit", "Bank deposit");
            }
            return words("reallife_mkt_bond_kind_treasury", "Treasury");
        }

        private static UiDataTableModel Futures(MarketState state, UiWords words)
        {
            int n = 0;
            for (int i = 0; i < state.Derivs.Contracts.Count; i++)
            {
                if (state.Derivs.Contracts[i].IsOpen && state.Derivs.Contracts[i].IsFuture)
                {
                    n++;
                }
            }
            var rows = new UiDataRow[n];
            int at = 0;
            for (int i = 0; i < state.Derivs.Contracts.Count && at < n; i++)
            {
                DerivContract c = state.Derivs.Contracts[i];
                if (!c.IsOpen || !c.IsFuture)
                {
                    continue;
                }
                string name = !string.IsNullOrEmpty(c.Material) ? c.Material : words("reallife_mkt_tab_futures", "Futures");
                string kind = words(c.Kind == ContractKind.Short ? "reallife_mkt_drv_kind_short" : "reallife_mkt_drv_kind_long", c.Kind == ContractKind.Short ? "Future sold" : "Future bought");
                rows[at++] = new UiDataRow
                {
                    Cells = new[] { name, kind, c.Qty.ToString(), MarketText.Money(c.Margin) },
                    Values = new[] { (int)UiIcon.Chart, 0, 0, 0 },
                    Tones = new[] { UiRole.None, UiRole.None, UiRole.None, UiRole.None }
                };
            }
            return new UiDataTableModel
            {
                Columns = new[]
                {
                    new UiDataColumn { Title = words("reallife_mkt_tab_futures", "Futures"), Kind = UiCellKind.Tile },
                    new UiDataColumn { Title = words("reallife_mkt_col_what", "What"), Kind = UiCellKind.Text, Width = 160f },
                    new UiDataColumn { Title = words("reallife_mkt_col_shares", "Shares"), Kind = UiCellKind.Figure, Width = 120f },
                    new UiDataColumn { Title = words("reallife_stk_worth", "Worth"), Kind = UiCellKind.Figure, Width = 140f }
                },
                Rows = rows, RowCount = n, EmptyIcon = UiIcon.Chart,
                EmptyTitle = words("reallife_stk_fut_empty", "No futures yet"),
                EmptyBody = words("reallife_stk_fut_empty_body", "A future you open shows here until it settles.")
            };
        }

    }
}
