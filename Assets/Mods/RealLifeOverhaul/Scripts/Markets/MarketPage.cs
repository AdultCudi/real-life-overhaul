using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using RealLifeOverhaul.GameUi;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.Markets
{
    /// <summary>
    /// A company's page (or the city index's) in the Stocks app, in place of the table on the right: its name and its
    /// state over the candle chart (<see cref="MarketChart"/>) or its figures and results, and under it the choices of
    /// what to show (the chart or the figures, the stretch of time, the line over the candles, the pane under them).
    /// Made of the game's own parts: the white box's lines, title and previous/next selectors, over a copy of the
    /// table's background.
    /// </summary>
    internal sealed class MarketPage
    {
        private const int ViewChart = 0, ViewFigures = 1;
        private static readonly string[] ViewKeys = { "reallife_mkt_view_chart", "reallife_mkt_view_figures" };
        private static readonly string[] FrameKeys = { "reallife_mkt_frame_today", "reallife_mkt_frame_days", "reallife_mkt_frame_weeks", "reallife_mkt_frame_months", "reallife_mkt_frame_years" };
        private static readonly string[] OverlayKeys =
        {
            "reallife_mkt_overlay_none", "reallife_mkt_overlay_sma20", "reallife_mkt_overlay_sma50", "reallife_mkt_overlay_ema20", "reallife_mkt_overlay_both", "reallife_mkt_overlay_bollinger"
        };
        private static readonly string[] LowerKeys =
        {
            "reallife_mkt_lower_none", "reallife_mkt_lower_rsi", "reallife_mkt_lower_macd", "reallife_mkt_lower_stochastic", "reallife_mkt_lower_atr", "reallife_mkt_lower_obv"
        };

        private readonly MarketApp _app;
        private readonly List<Candle> _candles = new List<Candle>(256);
        private readonly StringBuilder _text = new StringBuilder(1024);
        private AppShell _shell;
        private RectTransform _root, _view, _figures, _controls;
        private TMP_Text _title, _sub, _left, _right, _results;
        private MarketChart _chart;
        private int _showing = int.MinValue;   // what the page is of: a company's id or MarketApp.IndexPage
        private int _viewMode;
        private ChartFrame _frame = ChartFrame.Days;
        private ChartOverlay _overlay = ChartOverlay.Sma20And50;
        private ChartLower _lower = ChartLower.Rsi;

        private MarketPage(MarketApp app)
        {
            _app = app;
        }

        public static MarketPage Make(AppShell shell, MarketApp app)
        {
            try
            {
                var page = new MarketPage(app) { _shell = shell };
                var list = (RectTransform)shell.List;
                var go = new GameObject("CompanyPage", typeof(RectTransform));
                go.transform.SetParent(list.parent, false);
                var root = (RectTransform)go.transform;
                root.anchorMin = list.anchorMin;
                root.anchorMax = list.anchorMax;
                root.pivot = list.pivot;
                root.anchoredPosition = list.anchoredPosition;
                root.sizeDelta = list.sizeDelta;
                Transform background = list.Find("ListBackground");
                if (background != null)
                {
                    GameObject copy = UnityEngine.Object.Instantiate(background.gameObject, root, false);
                    var rect = (RectTransform)copy.transform;
                    rect.anchorMin = Vector2.zero;
                    rect.anchorMax = Vector2.one;
                    rect.offsetMin = rect.offsetMax = Vector2.zero;
                }
                else
                {
                    var image = go.AddComponent<Image>();
                    image.color = Color.white;
                }
                page._root = root;
                page._title = Free(shell.Box.Title(string.Empty, root), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(28f, -14f), new Vector2(-56f, 56f));
                page._sub = Free(shell.Box.Line(string.Empty, root), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(28f, -70f), new Vector2(-56f, 40f));
                page._view = Area(root, "View", new Vector2(18f, 104f), new Vector2(-18f, -116f));
                page._chart = MarketChart.Make(page._view, parent => shell.Box.Line(string.Empty, parent));
                page._figures = Area(page._view, "Figures", Vector2.zero, Vector2.zero);
                page._left = Block(shell, page._figures, new Vector2(0f, 0.38f), new Vector2(0.49f, 1f));
                page._right = Block(shell, page._figures, new Vector2(0.51f, 0.38f), new Vector2(1f, 1f));
                page._results = Block(shell, page._figures, new Vector2(0f, 0f), new Vector2(1f, 0.36f));
                page._controls = Area(root, "Controls", new Vector2(18f, 12f), new Vector2(-18f, 0f));
                page._controls.anchorMax = new Vector2(1f, 0f);
                page._controls.sizeDelta = new Vector2(-36f, 84f);
                page._controls.pivot = new Vector2(0.5f, 0f);
                page._controls.anchoredPosition = new Vector2(0f, 10f);
                var row = page._controls.gameObject.AddComponent<HorizontalLayoutGroup>();
                row.childControlWidth = true;
                row.childControlHeight = true;
                row.childForceExpandWidth = true;
                row.childForceExpandHeight = false;
                row.spacing = 18f;
                row.childAlignment = TextAnchor.MiddleCenter;
                go.SetActive(false);
                return page;
            }
            catch (Exception e)
            {
                MarketApp.Logger?.Error(e);
                UiClone.Fallback("the Stocks app's company page could not be built");
                return null;
            }
        }

        // A text of the box's lifted out of the box's layout and placed by hand.
        private static TMP_Text Free(TMP_Text text, Vector2 min, Vector2 max, Vector2 at, Vector2 size)
        {
            var layout = text.GetComponent<LayoutElement>();
            if (layout != null)
            {
                layout.ignoreLayout = true;
            }
            var fitter = text.GetComponent<ContentSizeFitter>();
            if (fitter != null)
            {
                fitter.enabled = false;
            }
            var rect = (RectTransform)text.transform;
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = at;
            rect.sizeDelta = size;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Ellipsis;
            return text;
        }

        private static RectTransform Area(Transform parent, string name, Vector2 offsetMin, Vector2 offsetMax)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            return rect;
        }

        private static TMP_Text Block(AppShell shell, RectTransform parent, Vector2 min, Vector2 max)
        {
            TMP_Text text = shell.Box.Line(string.Empty, parent);
            var layout = text.GetComponent<LayoutElement>();
            if (layout != null)
            {
                layout.ignoreLayout = true;
            }
            var fitter = text.GetComponent<ContentSizeFitter>();
            if (fitter != null)
            {
                fitter.enabled = false;
            }
            var rect = (RectTransform)text.transform;
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = new Vector2(10f, 6f);
            rect.offsetMax = new Vector2(-10f, -6f);
            text.alignment = TextAlignmentOptions.TopLeft;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.overflowMode = TextOverflowModes.Truncate;
            text.fontSize = Math.Min(text.fontSize, 24f);
            text.enableAutoSizing = false;
            return text;
        }

        // ---------------------------------------------------------------- showing

        public void Hide()
        {
            if (_root != null)
            {
                _root.gameObject.SetActive(false);
            }
            if (_shell != null && _shell.List != null)
            {
                _shell.List.gameObject.SetActive(true);
            }
        }

        private void Show()
        {
            _shell.List.gameObject.SetActive(false);
            _root.gameObject.SetActive(true);
        }

        public void ShowIndex(MarketState s)
        {
            Show();
            if (_showing != MarketApp.IndexPage)
            {
                _showing = MarketApp.IndexPage;
                _viewMode = ViewChart;
            }
            int today = MarketHistory.IndexDayMoveBp(s);
            UiClone.Words(_title, MarketText.T("reallife_mkt_index") + "   " + (s.IndexLast / 100.0).ToString("N2", CultureInfo.CurrentCulture) + "   " + Coloured(today));
            UiClone.Words(_sub, MarketText.Fill("reallife_mkt_page_index_sub", "count", Listed(s).ToString()));
            _viewMode = ViewChart;
            Controls(s, null);
            DrawChart(s, null);
        }

        public void ShowCompany(MarketState s, Stock k)
        {
            Show();
            if (_showing != k.Id)
            {
                _showing = k.Id;
            }
            int today = MarketHistory.DayMoveBp(k);
            UiClone.Words(_title, k.Name + "  <size=70%>" + k.Ticker + "</size>   " + MarketText.Price(k.Last) + "   " + Coloured(today));
            UiClone.Words(_sub, MarketText.Sector(k.Sector) + ", " + MarketText.Size(k.Size) + (k.BidCents > 0 ? "   " + MarketText.Fill("reallife_mkt_status_bid", "price", MarketText.Price(k.BidCents), "day", k.BidEndDay.ToString()) : string.Empty)
                                + (k.SplitDay > 0 ? "   " + MarketText.Fill("reallife_mkt_status_split", "ratio", k.SplitRatio.ToString(), "day", k.SplitDay.ToString()) : string.Empty));
            Controls(s, k);
            if (_viewMode == ViewFigures)
            {
                _chart.gameObject.SetActive(false);
                _figures.gameObject.SetActive(true);
                Figures(s, k);
            }
            else
            {
                _figures.gameObject.SetActive(false);
                _chart.gameObject.SetActive(true);
                DrawChart(s, k);
            }
        }

        private static int Listed(MarketState s)
        {
            int n = 0;
            for (int i = 0; i < s.Stocks.Count; i++)
            {
                n += s.Stocks[i].Listed ? 1 : 0;
            }
            return n;
        }

        private static string Coloured(int bp)
        {
            string colour = bp > 0 ? "#2EA05C" : bp < 0 ? "#D64040" : "#666666";
            return "<color=" + colour + ">" + MarketText.Change(bp) + "</color>";
        }

        // The choices under the page: the view, then (for the chart) the stretch of time, the line and the pane.
        private void Controls(MarketState s, Stock k)
        {
            for (int i = _controls.childCount - 1; i >= 0; i--)
            {
                GameObject child = _controls.GetChild(i).gameObject;
                child.SetActive(false);
                UnityEngine.Object.Destroy(child);
            }
            InfoBox box = _shell.Box;
            if (k != null)
            {
                box.Selector(ViewKeys.Length, _viewMode, i => MarketText.T(ViewKeys[i]), i => UiClone.Safe(() =>
                {
                    _viewMode = i;
                    _app.Refresh();
                }), _controls);
            }
            if (_viewMode == ViewChart || k == null)
            {
                box.Selector(FrameKeys.Length, (int)_frame, i => MarketText.T(FrameKeys[i]), i => UiClone.Safe(() =>
                {
                    _frame = (ChartFrame)i;
                    _app.Refresh();
                }), _controls);
                box.Selector(OverlayKeys.Length, (int)_overlay, i => MarketText.T(OverlayKeys[i]), i => UiClone.Safe(() =>
                {
                    _overlay = (ChartOverlay)i;
                    _app.Refresh();
                }), _controls);
                box.Selector(LowerKeys.Length, (int)_lower, i => MarketText.T(LowerKeys[i]), i => UiClone.Safe(() =>
                {
                    _lower = (ChartLower)i;
                    _app.Refresh();
                }), _controls);
            }
        }

        // ---------------------------------------------------------------- the chart

        private void DrawChart(MarketState s, Stock k)
        {
            _candles.Clear();
            int year = Math.Max(8, s.YearDays);
            bool session = MarketCatalogue.TradingDay(s.Day) && s.Phase >= MarketCatalogue.PhaseOpen && s.Phase < MarketCatalogue.PhaseClose;
            if (_frame == ChartFrame.Today)
            {
                Candle[] bars = k != null ? (k.BarCount > 0 ? k.Bars : k.LastBars) : (s.IndexBarCount > 0 ? s.IndexBars : s.IndexLastBars);
                int count = k != null ? (k.BarCount > 0 ? k.BarCount : k.LastBarCount) : (s.IndexBarCount > 0 ? s.IndexBarCount : s.IndexLastBarCount);
                for (int i = 0; i < count; i++)
                {
                    _candles.Add(bars[i]);
                }
            }
            else if (k != null)
            {
                Candle today = session ? new Candle(s.Day, k.Open, Math.Max(k.High, k.Last), Math.Min(k.Low, k.Last), k.Last, k.DayVolume) : default(Candle);
                MarketHistory.Build(k.Daily, k.Monthly, today, (Timeframe)(int)_frame, year, _candles);
            }
            else
            {
                Candle today = session ? new Candle(s.Day, s.IndexOpen, Math.Max(s.IndexHigh, s.IndexLast), Math.Min(s.IndexLow, s.IndexLast), s.IndexLast, 0) : default(Candle);
                MarketHistory.Build(s.IndexDaily, s.IndexMonthly, today, (Timeframe)(int)_frame, year, _candles);
            }
            _chart.Show(_candles, _frame, _overlay, _lower, year, k == null, MarketText.T(_frame == ChartFrame.Today ? "reallife_mkt_chart_no_bars" : "reallife_mkt_chart_no_candles"));
        }

        // ---------------------------------------------------------------- the figures

        private void Figures(MarketState s, Stock k)
        {
            double worth = k.Shares * (k.Last / 100.0);
            long revenue = 0, profit = 0;
            int quarters = 0;
            for (int i = Math.Max(0, k.Results.Count - 4); i < k.Results.Count; i++)
            {
                revenue += k.Results[i].RevenueK * 1000L;
                profit += k.Results[i].ProfitK * 1000L;
                quarters++;
            }
            double eps = MarketApp.TrailingEps(k), pe = MarketApp.Pe(k);
            double book = k.Shares > 0 ? k.Equity / k.Shares : 0.0;
            _text.Length = 0;
            Pair("reallife_mkt_fig_worth", MarketText.Large(worth));
            Pair("reallife_mkt_fig_shares", MarketApp.Count(k.Shares));
            Pair("reallife_mkt_fig_turnover", quarters == 4 ? MarketText.Large(revenue) : "-");
            Pair("reallife_mkt_fig_profit", quarters == 4 ? MarketText.Large(profit) : "-");
            Pair("reallife_mkt_fig_margin", quarters == 4 && revenue > 0 ? MarketText.Percent((int)(profit * 10000L / revenue)) : "-");
            Pair("reallife_mkt_fig_eps", double.IsNaN(eps) ? "-" : MarketText.PerShare((int)Math.Round(eps * 10000.0)));
            Pair("reallife_mkt_fig_pe", double.IsNaN(pe) ? "-" : pe.ToString("0.0", CultureInfo.CurrentCulture));
            Pair("reallife_mkt_fig_book", book > 0 ? MarketText.Price((int)Math.Round(book * 100.0)) + "  (" + MarketText.Fill("reallife_mkt_fig_pb", "times", (k.Last / 100.0 / book).ToString("0.0", CultureInfo.CurrentCulture)) + ")" : "-");
            Pair("reallife_mkt_fig_debt", MarketText.Large(k.Debt) + (k.Rev > 0 ? "  (" + MarketText.Fill("reallife_mkt_fig_of_turnover", "share", MarketText.Percent((int)(k.Debt * 10000.0 / k.Rev))) + ")" : string.Empty));
            UiClone.Words(_left, _text.ToString());

            _text.Length = 0;
            int yearly = k.DpsCents * 4;
            Pair("reallife_mkt_fig_dividend", yearly > 0 ? MarketText.Price(yearly) + "  (" + MarketText.Percent(MarketApp.YieldBp(k)) + ")" : MarketText.T("reallife_mkt_fig_none"));
            Pair("reallife_mkt_fig_payout", yearly > 0 && !double.IsNaN(eps) && eps > 0 ? MarketText.Percent((int)(yearly / 100.0 / eps * 10000.0)) : "-");
            if (k.DeclaredCents > 0 && k.PayDay >= s.Day)
            {
                Pair("reallife_mkt_fig_declared", MarketText.Fill("reallife_mkt_fig_declared_value", "amount", MarketText.Price(k.DeclaredCents), "record", k.RecordDay.ToString(), "pay", k.PayDay.ToString()));
            }
            Pair("reallife_mkt_fig_results", k.ResultsDay > 0 ? MarketText.Fill("reallife_mkt_day", "day", k.ResultsDay.ToString()) : "-");
            Pair("reallife_mkt_fig_beta", (k.Def.Beta / 100.0).ToString("0.00", CultureInfo.CurrentCulture));
            Pair("reallife_mkt_fig_range_day", MarketText.Fill("reallife_mkt_range", "low", MarketText.Price(k.Low), "high", MarketText.Price(k.High)));
            int lo = int.MaxValue, hi = 0;
            for (int i = 0; i < k.Daily.Count; i++)
            {
                lo = Math.Min(lo, k.Daily[i].L);
                hi = Math.Max(hi, k.Daily[i].H);
            }
            Pair("reallife_mkt_fig_range_days", hi > 0 ? MarketText.Fill("reallife_mkt_range", "low", MarketText.Price(lo), "high", MarketText.Price(hi)) : "-");
            Pair("reallife_mkt_fig_volume", MarketApp.Count((long)k.Adv));
            Pair("reallife_mkt_fig_distress", MarketText.T(k.Distress >= 0.3 ? "reallife_mkt_fig_distress_high" : k.Distress >= MarketCatalogue.DistressFrom ? "reallife_mkt_fig_distress_some" : "reallife_mkt_fig_distress_none"));
            UiClone.Words(_right, _text.ToString());

            _text.Length = 0;
            _text.Append("<b>").Append(MarketText.T("reallife_mkt_res_quarter")).Append("<pos=15%>").Append(MarketText.T("reallife_mkt_res_day")).Append("<pos=26%>")
                .Append(MarketText.T("reallife_mkt_res_turnover")).Append("<pos=44%>").Append(MarketText.T("reallife_mkt_res_profit")).Append("<pos=61%>")
                .Append(MarketText.T("reallife_mkt_res_eps")).Append("<pos=75%>").Append(MarketText.T("reallife_mkt_res_expected")).Append("<pos=89%>")
                .Append(MarketText.T("reallife_mkt_res_dividend")).Append("</b>\n");
            for (int i = k.Results.Count - 1; i >= 0 && i >= k.Results.Count - 6; i--)
            {
                QuarterResult r = k.Results[i];
                int y = MarketCatalogue.FloorDiv(r.Quarter, 4);
                bool beat = r.Eps >= r.Consensus;
                _text.Append(MarketText.Fill("reallife_mkt_res_quarter_value", "quarter", (r.Quarter - y * 4 + 1).ToString(), "year", (y + 1).ToString())).Append("<pos=15%>")
                    .Append(r.Day.ToString()).Append("<pos=26%>").Append(MarketText.Large(r.RevenueK * 1000.0)).Append("<pos=44%>").Append(MarketText.Large(r.ProfitK * 1000.0))
                    .Append("<pos=61%>").Append(beat ? "<color=#2EA05C>" : "<color=#D64040>").Append(MarketText.PerShare(r.Eps)).Append("</color><pos=75%>")
                    .Append(MarketText.PerShare(r.Consensus)).Append("<pos=89%>").Append(r.DpsCents > 0 ? MarketText.Price(r.DpsCents) : "-").Append('\n');
            }
            UiClone.Words(_results, _text.ToString());
        }

        private void Pair(string labelKey, string value)
        {
            _text.Append("<b>").Append(MarketText.T(labelKey)).Append(":</b> ").Append(value).Append('\n');
        }
    }
}
