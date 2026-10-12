using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RealLifeOverhaul.Markets
{
    /// <summary>The stretch of time a chart's candles stand for.</summary>
    public enum ChartFrame { Today = 0, Days = 1, Weeks = 2, Months = 3, Years = 4, Count = 5 }

    /// <summary>What is drawn over the candles.</summary>
    public enum ChartOverlay { None = 0, Sma20 = 1, Sma50 = 2, Ema20 = 3, Sma20And50 = 4, Bollinger = 5, Count = 6 }

    /// <summary>What the pane under the candles shows.</summary>
    public enum ChartLower { None = 0, Rsi = 1, Macd = 2, Stochastic = 3, Atr = 4, Obv = 5, Count = 6 }

    /// <summary>
    /// The chart's picture: boxes and lines in one mesh of the game's interface (a UI Graphic), so a whole chart is one
    /// draw however many candles it has. The chart (<see cref="MarketChart"/>) asks for its shapes and then for a new
    /// picture; nothing is drawn per frame.
    /// </summary>
    internal sealed class ChartMesh : MaskableGraphic
    {
        private struct Shape
        {
            public Vector2 A, B, C, D;
            public Color32 Colour;
        }

        private readonly List<Shape> _shapes = new List<Shape>(1024);

        public void Begin()
        {
            _shapes.Clear();
        }

        public void Box(float x0, float y0, float x1, float y1, Color32 colour)
        {
            if (x1 < x0)
            {
                float t = x0;
                x0 = x1;
                x1 = t;
            }
            if (y1 < y0)
            {
                float t = y0;
                y0 = y1;
                y1 = t;
            }
            _shapes.Add(new Shape { A = new Vector2(x0, y0), B = new Vector2(x0, y1), C = new Vector2(x1, y1), D = new Vector2(x1, y0), Colour = colour });
        }

        public void Line(Vector2 from, Vector2 to, float width, Color32 colour)
        {
            Vector2 along = to - from;
            float length = along.magnitude;
            if (length < 0.01f)
            {
                return;
            }
            Vector2 side = new Vector2(-along.y, along.x) / length * (width * 0.5f);
            _shapes.Add(new Shape { A = from - side, B = from + side, C = to + side, D = to - side, Colour = colour });
        }

        public void End()
        {
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            UIVertex v = UIVertex.simpleVert;
            for (int i = 0; i < _shapes.Count; i++)
            {
                Shape s = _shapes[i];
                int at = vh.currentVertCount;
                v.color = s.Colour;
                v.position = s.A;
                vh.AddVert(v);
                v.position = s.B;
                vh.AddVert(v);
                v.position = s.C;
                vh.AddVert(v);
                v.position = s.D;
                vh.AddVert(v);
                vh.AddTriangle(at, at + 1, at + 2);
                vh.AddTriangle(at + 2, at + 3, at);
            }
        }
    }

    /// <summary>
    /// A candle chart of a company's (or the city index's) prices, built of the game's own interface parts: the candles
    /// with their wicks, the volume under them, a line over them (moving averages, Bollinger bands) and a pane below
    /// (RSI, MACD, stochastic, average true range, on-balance volume), the price scale on the right and the dates below.
    /// The wheel zooms, a drag moves back and forth in time, and the pointer shows the candle under it. Everything is
    /// worked out when the chart is given its candles or moved, never per frame.
    /// </summary>
    public sealed class MarketChart : MonoBehaviour, IScrollHandler, IBeginDragHandler, IDragHandler, IPointerMoveHandler, IPointerExitHandler
    {
        private const float ScaleWidth = 96f, DateHeight = 30f, LegendHeight = 30f, Gap = 6f;
        private const int Grid = 5, Dates = 5, LeastShown = 8;

        private static readonly Color32 Up = new Color32(46, 160, 92, 255), Down = new Color32(214, 64, 64, 255);
        private static readonly Color32 UpFaint = new Color32(46, 160, 92, 90), DownFaint = new Color32(214, 64, 64, 90);
        private static readonly Color32 Rule = new Color32(120, 130, 140, 60), Edge = new Color32(120, 130, 140, 140);
        private static readonly Color32 First = new Color32(40, 110, 220, 255), Second = new Color32(235, 150, 30, 255), Third = new Color32(150, 90, 200, 255);
        private static readonly Color32 Band = new Color32(40, 110, 220, 40), Hover = new Color32(60, 60, 60, 120);

        private RectTransform _area;
        private ChartMesh _mesh;
        private readonly List<TMP_Text> _scale = new List<TMP_Text>(Grid + 4);
        private readonly List<TMP_Text> _dates = new List<TMP_Text>(Dates);
        private TMP_Text _legend, _lowerLegend, _empty;

        private readonly List<Candle> _candles = new List<Candle>(256);
        private double[] _o = new double[256], _h = new double[256], _l = new double[256], _c = new double[256], _v = new double[256];
        private double[] _a = new double[256], _b = new double[256], _x = new double[256], _y = new double[256], _z = new double[256], _w = new double[256], _s = new double[256];
        private ChartFrame _frame;
        private ChartOverlay _overlay;
        private ChartLower _lower;
        private int _yearDays = 60, _barStart;      // (the day the intraday bars are of)
        private bool _intraday, _index;
        private int _shown = 60, _end;              // candles shown, and one past the last of them
        private int _hover = -1;
        private float _dragCarry;
        private string _emptyWords = string.Empty;

        /// <summary>Makes a chart filling 'parent', its texts copies of 'textTemplate' (the game's own line of text).</summary>
        public static MarketChart Make(RectTransform parent, Func<Transform, TMP_Text> newText)
        {
            var go = new GameObject("MarketChart", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var area = (RectTransform)go.transform;
            area.anchorMin = Vector2.zero;
            area.anchorMax = Vector2.one;
            area.offsetMin = area.offsetMax = Vector2.zero;
            var chart = go.AddComponent<MarketChart>();
            chart._area = area;
            var meshGo = new GameObject("Picture", typeof(RectTransform));
            meshGo.transform.SetParent(area, false);
            var meshRect = (RectTransform)meshGo.transform;
            meshRect.anchorMin = Vector2.zero;
            meshRect.anchorMax = Vector2.one;
            meshRect.offsetMin = meshRect.offsetMax = Vector2.zero;
            meshRect.pivot = Vector2.zero;
            chart._mesh = meshGo.AddComponent<ChartMesh>();
            chart._mesh.raycastTarget = true;   // (the chart's own events: the wheel, a drag, the pointer)
            for (int i = 0; i < Grid + 3; i++)
            {
                chart._scale.Add(chart.Label(newText, 18f, TextAlignmentOptions.MidlineLeft));
            }
            for (int i = 0; i < Dates; i++)
            {
                chart._dates.Add(chart.Label(newText, 18f, TextAlignmentOptions.Midline));
            }
            chart._legend = chart.Label(newText, 19f, TextAlignmentOptions.MidlineLeft);
            chart._lowerLegend = chart.Label(newText, 18f, TextAlignmentOptions.MidlineLeft);
            chart._empty = chart.Label(newText, 22f, TextAlignmentOptions.Center);
            return chart;
        }

        private TMP_Text Label(Func<Transform, TMP_Text> newText, float size, TextAlignmentOptions align)
        {
            TMP_Text text = newText(_area);
            text.fontSize = size;
            text.enableAutoSizing = false;
            text.alignment = align;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;
            text.raycastTarget = false;
            var layout = text.GetComponent<LayoutElement>();
            if (layout != null)
            {
                layout.ignoreLayout = true;
            }
            var rect = (RectTransform)text.transform;
            rect.anchorMin = rect.anchorMax = Vector2.zero;
            rect.pivot = new Vector2(0f, 0.5f);
            rect.sizeDelta = new Vector2(220f, 26f);
            text.gameObject.SetActive(false);
            return text;
        }

        // ---------------------------------------------------------------- what it shows

        public ChartFrame Frame => _frame;
        public ChartOverlay Overlay => _overlay;
        public ChartLower Lower => _lower;

        /// <summary>The candles to draw, oldest first, with what they are (intraday bars of a day, or days and longer) and what to draw with them.</summary>
        public void Show(List<Candle> candles, ChartFrame frame, ChartOverlay overlay, ChartLower lower, int yearDays, bool index, string emptyWords)
        {
            bool newFrame = frame != _frame || index != _index;
            _candles.Clear();
            _candles.AddRange(candles);
            _frame = frame;
            _overlay = overlay;
            _lower = lower;
            _yearDays = Math.Max(8, yearDays);
            _intraday = frame == ChartFrame.Today;
            _index = index;
            _emptyWords = emptyWords ?? string.Empty;
            _barStart = _candles.Count > 0 ? _candles[0].Day : 0;
            int n = _candles.Count;
            if (newFrame || _end <= 0 || _end > n)
            {
                _end = n;
                _shown = _intraday ? Math.Max(LeastShown, n) : Math.Min(n, frame == ChartFrame.Days ? 60 : 48);
            }
            _shown = Mathf.Clamp(_shown, Math.Min(LeastShown, n), Math.Max(LeastShown, n));
            Series();
            Draw();
        }

        // The prices as numbers, and the series of the line and the lower pane over all of them.
        private void Series()
        {
            int n = _candles.Count;
            if (_o.Length < n)
            {
                int size = Math.Max(n, _o.Length * 2);
                _o = new double[size];
                _h = new double[size];
                _l = new double[size];
                _c = new double[size];
                _v = new double[size];
                _a = new double[size];
                _b = new double[size];
                _x = new double[size];
                _y = new double[size];
                _z = new double[size];
                _w = new double[size];
                _s = new double[size];
            }
            for (int i = 0; i < n; i++)
            {
                Candle k = _candles[i];
                _o[i] = k.O / 100.0;
                _h[i] = k.H / 100.0;
                _l[i] = k.L / 100.0;
                _c[i] = k.C / 100.0;
                _v[i] = k.V;
            }
            switch (_overlay)
            {
                case ChartOverlay.Sma20: MarketIndicators.Sma(_c, n, 20, _a); break;
                case ChartOverlay.Sma50: MarketIndicators.Sma(_c, n, 50, _a); break;
                case ChartOverlay.Ema20: MarketIndicators.Ema(_c, n, 20, _a); break;
                case ChartOverlay.Sma20And50:
                    MarketIndicators.Sma(_c, n, 20, _a);
                    MarketIndicators.Sma(_c, n, 50, _b);
                    break;
                case ChartOverlay.Bollinger: MarketIndicators.Bollinger(_c, n, 20, 2.0, _a, _b, _x); break;
            }
            switch (_lower)
            {
                case ChartLower.Rsi: MarketIndicators.Rsi(_c, n, 14, _y); break;
                case ChartLower.Macd: MarketIndicators.Macd(_c, n, 12, 26, 9, _y, _z, _w, _s); break;
                case ChartLower.Stochastic: MarketIndicators.Stochastic(_h, _l, _c, n, 14, 3, _y, _z); break;
                case ChartLower.Atr: MarketIndicators.Atr(_h, _l, _c, n, 14, _y); break;
                case ChartLower.Obv: MarketIndicators.Obv(_c, _v, n, _y); break;
            }
        }

        // ---------------------------------------------------------------- the wheel, a drag, the pointer

        public void OnScroll(PointerEventData e)
        {
            int n = _candles.Count;
            if (n <= LeastShown || _intraday)
            {
                return;
            }
            int was = _shown;
            _shown = Mathf.Clamp(e.scrollDelta.y > 0f ? Mathf.RoundToInt(_shown * 0.8f) : Mathf.RoundToInt(_shown * 1.25f) + 1, LeastShown, n);
            if (_shown != was)
            {
                UiSafe(Draw);
            }
        }

        public void OnBeginDrag(PointerEventData e)
        {
            _dragCarry = 0f;
        }

        public void OnDrag(PointerEventData e)
        {
            int n = _candles.Count;
            if (n <= _shown || _intraday)
            {
                return;
            }
            float step = PlotWidth() / Math.Max(1, _shown);
            _dragCarry += e.delta.x;
            int move = (int)(_dragCarry / step);
            if (move != 0)
            {
                _dragCarry -= move * step;
                int end = Mathf.Clamp(_end - move, _shown, n);
                if (end != _end)
                {
                    _end = end;
                    UiSafe(Draw);
                }
            }
        }

        public void OnPointerMove(PointerEventData e)
        {
            Vector2 local;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_area, e.position, e.pressEventCamera ?? e.enterEventCamera, out local))
            {
                return;
            }
            Rect r = _area.rect;
            float x = local.x - r.xMin;
            int first = Math.Max(0, _end - _shown);
            int at = first + (int)(x / (PlotWidth() / Math.Max(1, _shown)));
            int hover = x >= 0f && x < PlotWidth() && at < _end ? at : -1;
            if (hover != _hover)
            {
                _hover = hover;
                UiSafe(Draw);
            }
        }

        public void OnPointerExit(PointerEventData e)
        {
            if (_hover >= 0)
            {
                _hover = -1;
                UiSafe(Draw);
            }
        }

        private static void UiSafe(Action draw)
        {
            try
            {
                draw();
            }
            catch (Exception e)
            {
                MarketDirector.Logger?.Error(e);
            }
        }

        private float PlotWidth()
        {
            return Math.Max(10f, _area.rect.width - ScaleWidth);
        }

        // ---------------------------------------------------------------- drawing

        private void Draw()
        {
            Rect r = _area.rect;
            float width = r.width, height = r.height;
            _mesh.Begin();
            HideLabels();
            int n = _candles.Count;
            if (n == 0 || width < 50f || height < 50f)
            {
                _empty.gameObject.SetActive(true);
                UiCloneWords(_empty, _emptyWords);
                Place(_empty, width * 0.5f - 110f, height * 0.5f);
                _mesh.End();
                return;
            }
            int first = Math.Max(0, _end - _shown), last = Math.Max(first, _end - 1);
            float plotW = PlotWidth();
            float lowerH = _lower != ChartLower.None ? (height - DateHeight - LegendHeight) * 0.27f : 0f;
            float mainBottom = DateHeight + (lowerH > 0f ? lowerH + Gap : 0f);
            float mainTop = height - LegendHeight;
            float volumeH = (mainTop - mainBottom) * 0.18f;
            float priceBottom = mainBottom + volumeH + Gap;

            // The price range of what is shown, the line over it included.
            double lo = double.MaxValue, hi = double.MinValue, vMost = 1.0;
            for (int i = first; i <= last; i++)
            {
                lo = Math.Min(lo, _l[i]);
                hi = Math.Max(hi, _h[i]);
                vMost = Math.Max(vMost, _v[i]);
                Widen(ref lo, ref hi, i);
            }
            if (hi - lo < hi * 0.002)
            {
                hi += hi * 0.01 + 0.01;
                lo -= lo * 0.01 + 0.01;
            }
            double pad = (hi - lo) * 0.06;
            lo = Math.Max(0.0, lo - pad);
            hi += pad;
            float step = plotW / Math.Max(1, _shown);
            float body = Math.Max(1f, step * 0.66f);
            Func<double, float> Y = p => priceBottom + (float)((p - lo) / (hi - lo)) * (mainTop - priceBottom);

            // The grid and the price scale.
            for (int g = 0; g <= Grid; g++)
            {
                double price = lo + (hi - lo) * g / Grid;
                float y = Y(price);
                _mesh.Box(0f, y - 0.5f, plotW, y + 0.5f, Rule);
                TMP_Text label = _scale[g];
                label.gameObject.SetActive(true);
                UiCloneWords(label, _index ? (price).ToString("0.00") : MarketText.Price((int)Math.Round(price * 100.0)));
                Place(label, plotW + 6f, y);
            }
            _mesh.Box(plotW, 0f, plotW + 1f, height - LegendHeight, Edge);
            _mesh.Box(0f, mainBottom - 0.5f, plotW, mainBottom + 0.5f, Edge);

            // The candles, their wicks and the volume.
            for (int i = first; i <= last; i++)
            {
                float x = (i - first) * step + step * 0.5f;
                bool up = _c[i] >= _o[i];
                Color32 colour = up ? Up : Down;
                _mesh.Box(x - 0.75f, Y(_l[i]), x + 0.75f, Y(_h[i]), colour);
                float top = Y(Math.Max(_o[i], _c[i])), bottom = Y(Math.Min(_o[i], _c[i]));
                _mesh.Box(x - body * 0.5f, bottom, x + body * 0.5f, Math.Max(top, bottom + 1f), colour);
                if (!_index)
                {
                    float vh = (float)(_v[i] / vMost) * volumeH;
                    _mesh.Box(x - body * 0.5f, mainBottom, x + body * 0.5f, mainBottom + Math.Max(0.5f, vh), up ? UpFaint : DownFaint);
                }
            }

            // The line over the candles.
            string legend = OverlayLegend(Math.Max(first, _hover >= 0 ? _hover : last));
            switch (_overlay)
            {
                case ChartOverlay.Sma20:
                case ChartOverlay.Sma50:
                case ChartOverlay.Ema20:
                    Polyline(_a, first, last, step, Y, First);
                    break;
                case ChartOverlay.Sma20And50:
                    Polyline(_a, first, last, step, Y, First);
                    Polyline(_b, first, last, step, Y, Second);
                    break;
                case ChartOverlay.Bollinger:
                    for (int i = first; i <= last; i++)
                    {
                        if (!double.IsNaN(_b[i]) && !double.IsNaN(_x[i]))
                        {
                            float x = (i - first) * step + step * 0.5f;
                            _mesh.Box(x - step * 0.5f, Y(_x[i]), x + step * 0.5f, Y(_b[i]), Band);
                        }
                    }
                    Polyline(_a, first, last, step, Y, First);
                    Polyline(_b, first, last, step, Y, Third);
                    Polyline(_x, first, last, step, Y, Third);
                    break;
            }

            // The pane below.
            if (_lower != ChartLower.None)
            {
                float paneBottom = DateHeight, paneTop = DateHeight + lowerH;
                _mesh.Box(0f, paneTop + Gap * 0.5f - 0.5f, plotW, paneTop + Gap * 0.5f + 0.5f, Edge);
                LowerPane(first, last, step, paneBottom, paneTop, plotW);
            }

            // The candle under the pointer.
            int shownAt = _hover >= first && _hover <= last ? _hover : last;
            if (_hover >= first && _hover <= last)
            {
                float x = (_hover - first) * step + step * 0.5f;
                _mesh.Box(x - 0.5f, DateHeight, x + 0.5f, mainTop, Hover);
            }
            _legend.gameObject.SetActive(true);
            UiCloneWords(_legend, CandleWords(shownAt) + (legend.Length > 0 ? "    " + legend : string.Empty));
            Place(_legend, 4f, height - LegendHeight * 0.5f);
            ((RectTransform)_legend.transform).sizeDelta = new Vector2(width, 26f);

            // The dates.
            for (int d = 0; d < Dates; d++)
            {
                int i = first + (int)Math.Round((last - first) * (d + 0.5) / Dates);
                if (i < first || i > last)
                {
                    continue;
                }
                TMP_Text label = _dates[d];
                label.gameObject.SetActive(true);
                UiCloneWords(label, DateWords(i));
                Place(label, (i - first) * step + step * 0.5f - 110f, DateHeight * 0.5f);
            }
            _mesh.End();
        }

        // The range widened by the line over the candles.
        private void Widen(ref double lo, ref double hi, int i)
        {
            switch (_overlay)
            {
                case ChartOverlay.Sma20:
                case ChartOverlay.Sma50:
                case ChartOverlay.Ema20:
                    Take(ref lo, ref hi, _a[i]);
                    break;
                case ChartOverlay.Sma20And50:
                    Take(ref lo, ref hi, _a[i]);
                    Take(ref lo, ref hi, _b[i]);
                    break;
                case ChartOverlay.Bollinger:
                    Take(ref lo, ref hi, _b[i]);
                    Take(ref lo, ref hi, _x[i]);
                    break;
            }
        }

        private static void Take(ref double lo, ref double hi, double value)
        {
            if (!double.IsNaN(value) && value > 0.0)
            {
                lo = Math.Min(lo, value);
                hi = Math.Max(hi, value);
            }
        }

        private void Polyline(double[] series, int first, int last, float step, Func<double, float> y, Color32 colour)
        {
            bool have = false;
            Vector2 previous = default(Vector2);
            for (int i = first; i <= last; i++)
            {
                if (double.IsNaN(series[i]))
                {
                    have = false;
                    continue;
                }
                var point = new Vector2((i - first) * step + step * 0.5f, y(series[i]));
                if (have)
                {
                    _mesh.Line(previous, point, 2f, colour);
                }
                previous = point;
                have = true;
            }
        }

        private void LowerPane(int first, int last, float step, float bottom, float top, float plotW)
        {
            double lo, hi;
            switch (_lower)
            {
                case ChartLower.Rsi:
                case ChartLower.Stochastic:
                    lo = 0.0;
                    hi = 100.0;
                    break;
                default:
                    lo = double.MaxValue;
                    hi = double.MinValue;
                    for (int i = first; i <= last; i++)
                    {
                        Take2(ref lo, ref hi, _y[i]);
                        if (_lower == ChartLower.Macd)
                        {
                            Take2(ref lo, ref hi, _z[i]);
                            Take2(ref lo, ref hi, _w[i]);
                        }
                    }
                    if (lo > hi)
                    {
                        lo = 0.0;
                        hi = 1.0;
                    }
                    if (_lower == ChartLower.Macd)
                    {
                        double most = Math.Max(Math.Abs(lo), Math.Abs(hi));
                        lo = -most;
                        hi = most;
                    }
                    if (hi - lo < 1e-9)
                    {
                        hi = lo + 1.0;
                    }
                    break;
            }
            double span = hi - lo;
            Func<double, float> Y = v => bottom + (float)((v - lo) / span) * (top - bottom);
            if (_lower == ChartLower.Rsi || _lower == ChartLower.Stochastic)
            {
                double low = _lower == ChartLower.Rsi ? 30.0 : 20.0, high = _lower == ChartLower.Rsi ? 70.0 : 80.0;
                _mesh.Box(0f, Y(low), plotW, Y(high), Band);
                ScaleAt(Grid + 1, plotW, Y(high), high.ToString("0"));
                ScaleAt(Grid + 2, plotW, Y(low), low.ToString("0"));
            }
            if (_lower == ChartLower.Macd)
            {
                float zero = Y(0.0);
                _mesh.Box(0f, zero - 0.5f, plotW, zero + 0.5f, Rule);
                for (int i = first; i <= last; i++)
                {
                    if (!double.IsNaN(_w[i]))
                    {
                        float x = (i - first) * step + step * 0.5f;
                        _mesh.Box(x - step * 0.3f, zero, x + step * 0.3f, Y(_w[i]), _w[i] >= 0 ? UpFaint : DownFaint);
                    }
                }
                Polyline(_y, first, last, step, Y, First);
                Polyline(_z, first, last, step, Y, Second);
            }
            else
            {
                Polyline(_y, first, last, step, Y, First);
                if (_lower == ChartLower.Stochastic)
                {
                    Polyline(_z, first, last, step, Y, Second);
                }
            }
            int at = _hover >= first && _hover <= last ? _hover : last;
            _lowerLegend.gameObject.SetActive(true);
            UiCloneWords(_lowerLegend, LowerWords(at));
            Place(_lowerLegend, 4f, top - 12f);
            ((RectTransform)_lowerLegend.transform).sizeDelta = new Vector2(plotW, 24f);
        }

        private static void Take2(ref double lo, ref double hi, double value)
        {
            if (!double.IsNaN(value))
            {
                lo = Math.Min(lo, value);
                hi = Math.Max(hi, value);
            }
        }

        private void ScaleAt(int label, float plotW, float y, string words)
        {
            if (label < _scale.Count)
            {
                TMP_Text text = _scale[label];
                text.gameObject.SetActive(true);
                UiCloneWords(text, words);
                Place(text, plotW + 6f, y);
            }
        }

        private void HideLabels()
        {
            for (int i = 0; i < _scale.Count; i++)
            {
                _scale[i].gameObject.SetActive(false);
            }
            for (int i = 0; i < _dates.Count; i++)
            {
                _dates[i].gameObject.SetActive(false);
            }
            _legend.gameObject.SetActive(false);
            _lowerLegend.gameObject.SetActive(false);
            _empty.gameObject.SetActive(false);
        }

        private static void Place(TMP_Text text, float x, float y)
        {
            ((RectTransform)text.transform).anchoredPosition = new Vector2(x, y);
        }

        private static void UiCloneWords(TMP_Text text, string words)
        {
            GameUi.UiClone.Words(text, words);
        }

        // ---------------------------------------------------------------- words

        private string CandleWords(int i)
        {
            Candle k = _candles[i];
            string when = DateWords(i);
            if (_index)
            {
                return when + "   " + MarketText.Fill("reallife_mkt_chart_index", "open", (k.O / 100.0).ToString("0.00"), "high", (k.H / 100.0).ToString("0.00"),
                    "low", (k.L / 100.0).ToString("0.00"), "close", (k.C / 100.0).ToString("0.00"));
            }
            return when + "   " + MarketText.Fill("reallife_mkt_chart_candle", "open", MarketText.Price(k.O), "high", MarketText.Price(k.H), "low", MarketText.Price(k.L),
                "close", MarketText.Price(k.C), "volume", k.V.ToString("N0"));
        }

        private string OverlayLegend(int i)
        {
            switch (_overlay)
            {
                case ChartOverlay.Sma20: return Named("reallife_mkt_chart_sma20", _a[i]);
                case ChartOverlay.Sma50: return Named("reallife_mkt_chart_sma50", _a[i]);
                case ChartOverlay.Ema20: return Named("reallife_mkt_chart_ema20", _a[i]);
                case ChartOverlay.Sma20And50: return Named("reallife_mkt_chart_sma20", _a[i]) + "   " + Named("reallife_mkt_chart_sma50", _b[i]);
                case ChartOverlay.Bollinger: return Named("reallife_mkt_chart_bollinger", _a[i]) + " (" + Number(_x[i]) + " / " + Number(_b[i]) + ")";
                default: return string.Empty;
            }
        }

        private string LowerWords(int i)
        {
            switch (_lower)
            {
                case ChartLower.Rsi: return Named("reallife_mkt_chart_rsi", _y[i], "0.0");
                case ChartLower.Macd: return Named("reallife_mkt_chart_macd", _y[i], "0.000") + "   " + Named("reallife_mkt_chart_signal", _z[i], "0.000");
                case ChartLower.Stochastic: return Named("reallife_mkt_chart_stochastic", _y[i], "0.0") + "   " + Named("reallife_mkt_chart_signal", _z[i], "0.0");
                case ChartLower.Atr: return Named("reallife_mkt_chart_atr", _y[i]);
                case ChartLower.Obv: return Named("reallife_mkt_chart_obv", _y[i], "N0");
                default: return string.Empty;
            }
        }

        private static string Named(string key, double value, string format = "0.00")
        {
            return MarketText.T(key) + " " + (double.IsNaN(value) ? "-" : value.ToString(format));
        }

        private static string Number(double value)
        {
            return double.IsNaN(value) ? "-" : value.ToString("0.00");
        }

        private string DateWords(int i)
        {
            Candle k = _candles[i];
            if (_intraday)
            {
                int minute = MarketCatalogue.OpenMinute + i * MarketCatalogue.StepMinutes;
                return (minute / 60).GetFormattedTime(minute % 60);
            }
            switch (_frame)
            {
                case ChartFrame.Months:
                    {
                        int month = MarketCatalogue.MonthOf(k.Day, _yearDays);
                        int year = MarketCatalogue.FloorDiv(month, 12);
                        return MarketText.Fill("reallife_mkt_chart_month", "month", (month - year * 12 + 1).ToString(), "year", (year + 1).ToString());
                    }
                case ChartFrame.Years:
                    return MarketText.Fill("reallife_mkt_chart_year", "year", (MarketCatalogue.YearOf(k.Day, _yearDays) + 1).ToString());
                default:
                    return MarketText.Fill("reallife_mkt_chart_day", "day", k.Day.ToString());
            }
        }

        private void OnRectTransformDimensionsChange()
        {
            if (_mesh != null && _candles.Count > 0)
            {
                UiSafe(Draw);
            }
        }
    }
}
