using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RealLifeOverhaul.Economy.Dealership
{
    /// <summary>
    /// The report chart's picture: boxes and lines in one mesh of the game's interface (a UI Graphic), as the Stocks app's
    /// chart draws its candles (Markets/ChartMesh), so a whole chart is one draw. The chart asks for its shapes and then
    /// for a new picture; nothing is drawn per frame.
    /// </summary>
    internal sealed class DealerChartMesh : MaskableGraphic
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("economy_dealer", "DealerChartMesh", typeof(DealerChartMesh));   // the game's calls of it (Core/Guard)

        private struct Shape
        {
            public Vector2 A, B, C, D;
            public Color32 Colour;
        }

        private readonly List<Shape> _shapes = new List<Shape>(256);

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
            if (FrameGuard.Off)
            {
                Guard.NoMesh(vh);   // (its feature is off: nothing drawn)
                return;
            }
            try
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
            catch (Exception fault)
            {
                Guard.FailMesh(FrameGuard, fault, vh);   // (half a mesh is not drawn)
            }
        }
    }

    /// <summary>
    /// A showroom's report as a chart of the game's own interface parts: a bar for each day kept (red under nothing, for a
    /// day's loss), the line of the last days' average over them, the scale on the right and the days below, and the
    /// pointer showing the day under it. Everything is worked out when the chart is given its days or the pointer moves
    /// over it, never per frame; while the app is closed it is not shown and nothing is drawn.
    /// </summary>
    public sealed class DealerChart : MonoBehaviour, IPointerMoveHandler, IPointerExitHandler
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("economy_dealer", "DealerChart", typeof(DealerChart));   // the game's calls of it (Core/Guard)

        private const float ScaleWidth = 110f, DateHeight = 30f, LegendHeight = 34f;
        private const int Grid = 4, Dates = 5;

        private static readonly Color32 Bar = new Color32(70, 130, 210, 255), Loss = new Color32(214, 64, 64, 255), Empty = new Color32(120, 130, 140, 40);
        private static readonly Color32 Rule = new Color32(120, 130, 140, 60), Edge = new Color32(120, 130, 140, 140), Average = new Color32(235, 150, 30, 255);
        private static readonly Color32 Hover = new Color32(60, 60, 60, 120);

        private RectTransform _area;
        private DealerChartMesh _mesh;
        private readonly List<TMP_Text> _scale = new List<TMP_Text>(Grid + 1);
        private readonly List<TMP_Text> _dates = new List<TMP_Text>(Dates);
        private TMP_Text _legend, _empty;

        private long[] _values = new long[DealerCatalogue.ReportDays], _average = new long[DealerCatalogue.ReportDays];
        private bool[] _have = new bool[DealerCatalogue.ReportDays];
        private int _n, _first, _hover = -1;
        private ReportKind _kind;
        private string _emptyWords = string.Empty, _averageWords = string.Empty;

        /// <summary>Makes a chart filling 'parent', its texts copies of the game's own line of text.</summary>
        public static DealerChart Make(RectTransform parent, Func<Transform, TMP_Text> newText)
        {
            var go = new GameObject("DealerChart", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var area = (RectTransform)go.transform;
            area.anchorMin = Vector2.zero;
            area.anchorMax = Vector2.one;
            area.offsetMin = area.offsetMax = Vector2.zero;
            var chart = go.AddComponent<DealerChart>();
            chart._area = area;
            var meshGo = new GameObject("Picture", typeof(RectTransform));
            meshGo.transform.SetParent(area, false);
            var meshRect = (RectTransform)meshGo.transform;
            meshRect.anchorMin = Vector2.zero;
            meshRect.anchorMax = Vector2.one;
            meshRect.offsetMin = meshRect.offsetMax = Vector2.zero;
            meshRect.pivot = Vector2.zero;
            chart._mesh = meshGo.AddComponent<DealerChartMesh>();
            chart._mesh.raycastTarget = true;   // (the pointer's events)
            for (int i = 0; i <= Grid; i++)
            {
                chart._scale.Add(chart.Label(newText, 18f, TextAlignmentOptions.MidlineLeft));
            }
            for (int i = 0; i < Dates; i++)
            {
                chart._dates.Add(chart.Label(newText, 18f, TextAlignmentOptions.Midline));
            }
            chart._legend = chart.Label(newText, 20f, TextAlignmentOptions.MidlineLeft);
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

        /// <summary>The days to draw, the oldest first (from <paramref name="first"/>), what they are of, and the words for the average's line and for no figures at all.</summary>
        public void Show(long[] values, bool[] have, long[] average, int n, int first, ReportKind kind, string averageWords, string emptyWords)
        {
            _n = Math.Min(n, _values.Length);
            Array.Copy(values, _values, _n);
            Array.Copy(have, _have, _n);
            Array.Copy(average, _average, _n);
            _first = first;
            _kind = kind;
            _averageWords = averageWords ?? string.Empty;
            _emptyWords = emptyWords ?? string.Empty;
            _hover = -1;
            Draw();
        }

        // ---------------------------------------------------------------- the pointer

        public void OnPointerMove(PointerEventData e)
        {
            if (FrameGuard.Off)
            {
                return;
            }
            try
            {
                if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_area, e.position, e.pressEventCamera ?? e.enterEventCamera, out Vector2 local))
                {
                    return;
                }
                float x = local.x - _area.rect.xMin;
                int at = (int)(x / (PlotWidth() / Math.Max(1, _n)));
                int hover = x >= 0f && x < PlotWidth() && at < _n ? at : -1;
                if (hover != _hover)
                {
                    _hover = hover;
                    Draw();
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
                    Draw();
                }
            }
            catch (Exception fault)
            {
                Guard.Fail(FrameGuard, fault);
            }
        }

        private void OnRectTransformDimensionsChange()
        {
            if (FrameGuard.Off || _mesh == null || _n == 0 || !isActiveAndEnabled)
            {
                return;
            }
            try
            {
                Draw();
            }
            catch (Exception fault)
            {
                Guard.Fail(FrameGuard, fault);
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
            bool any = false;
            for (int i = 0; i < _n; i++)
            {
                any |= _have[i];
            }
            if (!any || width < 50f || height < 50f)
            {
                _empty.gameObject.SetActive(true);
                GameUi.UiClone.Words(_empty, _emptyWords);
                Place(_empty, width * 0.5f - 110f, height * 0.5f);
                _mesh.End();
                return;
            }
            float plotW = PlotWidth(), bottom = DateHeight, top = height - LegendHeight;
            long lo = 0, hi = 0;
            for (int i = 0; i < _n; i++)
            {
                lo = Math.Min(lo, Math.Min(_values[i], _average[i]));
                hi = Math.Max(hi, Math.Max(_values[i], _average[i]));
            }
            if (hi - lo <= 0)
            {
                hi = lo + 1;
            }
            double span = hi - lo;
            Func<double, float> Y = v => bottom + (float)((v - lo) / span) * (top - bottom);
            float step = plotW / Math.Max(1, _n), body = Math.Max(1f, step * 0.7f);

            // The grid and the scale.
            for (int g = 0; g <= Grid; g++)
            {
                double value = lo + span * g / Grid;
                float y = Y(value);
                _mesh.Box(0f, y - 0.5f, plotW, y + 0.5f, Rule);
                TMP_Text label = _scale[g];
                label.gameObject.SetActive(true);
                GameUi.UiClone.Words(label, Words((long)Math.Round(value)));
                Place(label, plotW + 6f, y);
            }
            _mesh.Box(plotW, bottom, plotW + 1f, top, Edge);
            float zero = Y(0.0);
            _mesh.Box(0f, zero - 0.5f, plotW, zero + 0.5f, Edge);

            // The bars, and the average's line over them.
            for (int i = 0; i < _n; i++)
            {
                float x = i * step + step * 0.5f;
                if (!_have[i])
                {
                    _mesh.Box(x - body * 0.5f, zero, x + body * 0.5f, zero + 1.5f, Empty);
                    continue;
                }
                _mesh.Box(x - body * 0.5f, zero, x + body * 0.5f, Y(_values[i]) + (_values[i] == 0 ? 1f : 0f), _values[i] < 0 ? Loss : Bar);
            }
            bool have = false;
            Vector2 previous = default(Vector2);
            for (int i = 0; i < _n; i++)
            {
                if (!_have[i])
                {
                    continue;
                }
                var point = new Vector2(i * step + step * 0.5f, Y(_average[i]));
                if (have)
                {
                    _mesh.Line(previous, point, 2f, Average);
                }
                previous = point;
                have = true;
            }

            // The day under the pointer, or the last.
            int shown = _hover >= 0 && _hover < _n ? _hover : _n - 1;
            if (_hover >= 0 && _hover < _n)
            {
                float x = _hover * step + step * 0.5f;
                _mesh.Box(x - 0.5f, bottom, x + 0.5f, top, Hover);
            }
            _legend.gameObject.SetActive(true);
            GameUi.UiClone.Words(_legend, FinanceText.Day(_first + shown) + "   <b>" + (_have[shown] ? Words(_values[shown]) : "-") + "</b>    " + _averageWords + " " + Words(_average[shown]));
            Place(_legend, 4f, height - LegendHeight * 0.5f);
            ((RectTransform)_legend.transform).sizeDelta = new Vector2(width, 28f);

            // The days below.
            for (int d = 0; d < Dates; d++)
            {
                int i = (int)Math.Round((_n - 1) * (d + 0.5) / Dates);
                if (i < 0 || i >= _n)
                {
                    continue;
                }
                TMP_Text label = _dates[d];
                label.gameObject.SetActive(true);
                GameUi.UiClone.Words(label, FinanceText.Day(_first + i));
                Place(label, i * step + step * 0.5f - 110f, DateHeight * 0.5f);
            }
            _mesh.End();
        }

        // A value as the report's kind writes it: money, a share, days or a count.
        private string Words(long value)
        {
            if (DealerReport.Money(_kind))
            {
                return DealerText.Money((int)Math.Max(int.MinValue, Math.Min(int.MaxValue, value)));
            }
            if (_kind == ReportKind.Conversion)
            {
                return (value / 10.0).ToString("0.#", System.Globalization.CultureInfo.CurrentCulture) + "%";
            }
            return value.ToString("N0", System.Globalization.CultureInfo.CurrentCulture);
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
            _empty.gameObject.SetActive(false);
        }

        private static void Place(TMP_Text text, float x, float y)
        {
            ((RectTransform)text.transform).anchoredPosition = new Vector2(x, y);
        }
    }
}
