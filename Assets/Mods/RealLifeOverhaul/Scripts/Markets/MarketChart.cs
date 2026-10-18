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
    /// A candle chart of a company's or the index's prices: candles, volume, a line over them, a pane of an indicator below,
    /// the price scale and the dates. The wheel zooms, a drag moves through time, and the pointer shows the candle under it.
    /// Worked out when given candles or moved, never per frame; a pointer move redraws only its own bar and the legends.
    /// </summary>
    public sealed partial class MarketChart : MonoBehaviour, IScrollHandler, IBeginDragHandler, IDragHandler, IPointerMoveHandler, IPointerExitHandler
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("markets", "MarketChart", typeof(MarketChart));   // the game's calls of it

        private const float ScaleWidth = 96f, DateHeight = 30f, LegendHeight = 30f, Gap = 6f;
        private const int Grid = 5, Dates = 5, LeastShown = 8;

        private static readonly Color32 Up = new Color32(46, 160, 92, 255), Down = new Color32(214, 64, 64, 255);
        private static readonly Color32 UpFaint = new Color32(46, 160, 92, 90), DownFaint = new Color32(214, 64, 64, 90);
        private static readonly Color32 Rule = new Color32(120, 130, 140, 60), Edge = new Color32(120, 130, 140, 140);
        private static readonly Color32 First = new Color32(40, 110, 220, 255), Second = new Color32(235, 150, 30, 255), Third = new Color32(150, 90, 200, 255);
        private static readonly Color32 Band = new Color32(40, 110, 220, 40), Hover = new Color32(60, 60, 60, 120);

        private RectTransform _area;
        private ChartMesh _mesh, _hoverMesh;
        private readonly List<TMP_Text> _scale = new List<TMP_Text>(Grid + 4);
        private readonly List<TMP_Text> _dates = new List<TMP_Text>(Dates);
        private TMP_Text _legend, _lowerLegend, _empty;

        private readonly List<Candle> _candles = new List<Candle>(256);
        private double[] _o = new double[256], _h = new double[256], _l = new double[256], _c = new double[256], _v = new double[256];
        private double[] _a = new double[256], _b = new double[256], _x = new double[256], _y = new double[256], _z = new double[256], _w = new double[256], _s = new double[256];
        private ChartFrame _frame;
        private ChartOverlay _overlay;
        private ChartLower _lower;
        private int _yearDays = 60, _barStart;      // the day the intraday bars are of
        private bool _intraday, _index;
        private int _shown = 60, _end;              // candles shown, and one past the last of them
        private int _hover = -1;
        private float _dragCarry;
        private string _emptyWords = string.Empty;
        private Action _draw, _showHover;   // made once for the pointer, the wheel and a drag

        // Where the last drawing put the candles, for the bar under the pointer; _drawn false while there is nothing shown.
        private bool _drawn;
        private int _drawFirst, _drawLast;
        private float _drawStep, _drawTop;

        // A scale from values to heights, by value so a drawing makes no function of its own.
        private readonly struct Scale
        {
            private readonly double _lo, _span;
            private readonly float _bottom, _height;

            public Scale(double lo, double span, float bottom, float height)
            {
                _lo = lo;
                _span = span;
                _bottom = bottom;
                _height = height;
            }

            public float Y(double value) => _bottom + (float)((value - _lo) / _span) * _height;
        }

        /// <summary>A chart filling <paramref name="parent"/>, its texts made by <paramref name="newText"/> from the game's own line.</summary>
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
            chart._mesh.raycastTarget = true;   // the chart's own events: the wheel, a drag, the pointer
            var hoverGo = new GameObject("Hover", typeof(RectTransform));   // over the picture, under the texts made next
            hoverGo.transform.SetParent(area, false);
            var hoverRect = (RectTransform)hoverGo.transform;
            hoverRect.anchorMin = Vector2.zero;
            hoverRect.anchorMax = Vector2.one;
            hoverRect.offsetMin = hoverRect.offsetMax = Vector2.zero;
            hoverRect.pivot = Vector2.zero;
            chart._hoverMesh = hoverGo.AddComponent<ChartMesh>();
            chart._hoverMesh.raycastTarget = false;
            chart._draw = chart.Draw;
            chart._showHover = chart.ShowHover;
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

        public ChartFrame Frame => _frame;
        public ChartOverlay Overlay => _overlay;
        public ChartLower Lower => _lower;

        /// <summary>The candles to draw, oldest first, and what to draw with them.</summary>
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

        // The prices as numbers, and the line's and the lower pane's series over all of them.
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

        public void OnScroll(PointerEventData e)
        {
            if (FrameGuard.Off)
            {
                return;
            }
            try
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
                    UiSafe(_draw);
                }
            }
            catch (Exception fault)
            {
                Guard.Fail(FrameGuard, fault);
            }
        }

        public void OnBeginDrag(PointerEventData e)
        {
            _dragCarry = 0f;
        }

        public void OnDrag(PointerEventData e)
        {
            if (FrameGuard.Off)
            {
                return;
            }
            try
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
                        UiSafe(_draw);
                    }
                }
            }
            catch (Exception fault)
            {
                Guard.Fail(FrameGuard, fault);
            }
        }

        public void OnPointerMove(PointerEventData e)
        {
            if (FrameGuard.Off)
            {
                return;
            }
            try
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
                    UiSafe(_showHover);   // only the bar and the legends change
                }
            }
            catch (Exception fault)
            {
                Guard.Fail(FrameGuard, fault);
            }
        }

        public void OnPointerExit(PointerEventData e)
        {
            if (FrameGuard.Off)
            {
                return;
            }
            try
            {
                if (_hover >= 0)
                {
                    _hover = -1;
                    UiSafe(_showHover);
                }
            }
            catch (Exception fault)
            {
                Guard.Fail(FrameGuard, fault);
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

        private void OnRectTransformDimensionsChange()
        {
            if (_mesh != null && _candles.Count > 0)
            {
                UiSafe(_draw);
            }
        }
    }
}
