using System;
using RealLifeOverhaul.GameUi;
using TMPro;
using UnityEngine;

namespace RealLifeOverhaul.Markets
{
    /// <summary>Part of the chart: drawing it, the bar under the pointer, and its words.</summary>
    public sealed partial class MarketChart
    {
        private void Draw()
        {
            Rect r = _area.rect;
            float width = r.width, height = r.height;
            _mesh.Begin();
            HideLabels();
            int n = _candles.Count;
            if (n == 0 || width < 50f || height < 50f)
            {
                _drawn = false;
                _empty.gameObject.SetActive(true);
                UiClone.Words(_empty, _emptyWords);
                Place(_empty, width * 0.5f - 110f, height * 0.5f);
                _mesh.End();
                ShowHover();   // no bar
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
            var Y = new Scale(lo, hi - lo, priceBottom, mainTop - priceBottom);

            // The grid and the price scale.
            for (int g = 0; g <= Grid; g++)
            {
                double price = lo + (hi - lo) * g / Grid;
                float y = Y.Y(price);
                _mesh.Box(0f, y - 0.5f, plotW, y + 0.5f, Rule);
                TMP_Text label = _scale[g];
                label.gameObject.SetActive(true);
                UiClone.Words(label, _index ? (price).ToString("0.00") : MarketText.Price((int)Math.Round(price * 100.0)));
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
                _mesh.Box(x - 0.75f, Y.Y(_l[i]), x + 0.75f, Y.Y(_h[i]), colour);
                float top = Y.Y(Math.Max(_o[i], _c[i])), bottom = Y.Y(Math.Min(_o[i], _c[i]));
                _mesh.Box(x - body * 0.5f, bottom, x + body * 0.5f, Math.Max(top, bottom + 1f), colour);
                if (!_index)
                {
                    float vh = (float)(_v[i] / vMost) * volumeH;
                    _mesh.Box(x - body * 0.5f, mainBottom, x + body * 0.5f, mainBottom + Math.Max(0.5f, vh), up ? UpFaint : DownFaint);
                }
            }

            // The line over the candles.
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
                            _mesh.Box(x - step * 0.5f, Y.Y(_x[i]), x + step * 0.5f, Y.Y(_b[i]), Band);
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

            // The legend over the chart: its words are the candle's under the pointer.
            _legend.gameObject.SetActive(true);
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
                UiClone.Words(label, DateWords(i));
                Place(label, (i - first) * step + step * 0.5f - 110f, DateHeight * 0.5f);
            }
            _mesh.End();
            _drawn = true;
            _drawFirst = first;
            _drawLast = last;
            _drawStep = step;
            _drawTop = mainTop;
            ShowHover();
        }

        // The candle under the pointer: a bar over it in a picture of its own, and the legends' words, of it or else of the
        // last candle shown. All a move of the pointer redraws.
        private void ShowHover()
        {
            _hoverMesh.Begin();
            if (!_drawn)
            {
                _hoverMesh.End();
                return;
            }
            int first = _drawFirst, last = _drawLast;
            bool over = _hover >= first && _hover <= last;
            if (over)
            {
                float x = (_hover - first) * _drawStep + _drawStep * 0.5f;
                _hoverMesh.Box(x - 0.5f, DateHeight, x + 0.5f, _drawTop, Hover);
            }
            _hoverMesh.End();
            int shownAt = over ? _hover : last;
            string legend = OverlayLegend(Math.Max(first, _hover >= 0 ? _hover : last));
            UiClone.Words(_legend, CandleWords(shownAt) + (legend.Length > 0 ? "    " + legend : string.Empty));
            if (_lower != ChartLower.None)
            {
                UiClone.Words(_lowerLegend, LowerWords(shownAt));
            }
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

        private void Polyline(double[] series, int first, int last, float step, in Scale y, Color32 colour)
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
                var point = new Vector2((i - first) * step + step * 0.5f, y.Y(series[i]));
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
            var Y = new Scale(lo, span, bottom, top - bottom);
            if (_lower == ChartLower.Rsi || _lower == ChartLower.Stochastic)
            {
                double low = _lower == ChartLower.Rsi ? 30.0 : 20.0, high = _lower == ChartLower.Rsi ? 70.0 : 80.0;
                _mesh.Box(0f, Y.Y(low), plotW, Y.Y(high), Band);
                ScaleAt(Grid + 1, plotW, Y.Y(high), high.ToString("0"));
                ScaleAt(Grid + 2, plotW, Y.Y(low), low.ToString("0"));
            }
            if (_lower == ChartLower.Macd)
            {
                float zero = Y.Y(0.0);
                _mesh.Box(0f, zero - 0.5f, plotW, zero + 0.5f, Rule);
                for (int i = first; i <= last; i++)
                {
                    if (!double.IsNaN(_w[i]))
                    {
                        float x = (i - first) * step + step * 0.5f;
                        _mesh.Box(x - step * 0.3f, zero, x + step * 0.3f, Y.Y(_w[i]), _w[i] >= 0 ? UpFaint : DownFaint);
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
            _lowerLegend.gameObject.SetActive(true);   // its words are the candle's under the pointer
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
                UiClone.Words(text, words);
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
    }
}
