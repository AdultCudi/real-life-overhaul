using System;
using System.Globalization;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// What the chart says, in plain words with the number: only about the indicators and drawings that are on the chart model,
    /// so it never speaks of an RSI that has no pane. Read off the last candle of the model. Game-free. Cost: a short walk of
    /// the lines when the desk is built, never a frame.
    /// </summary>
    public static class StocksSignals
    {
        public const int Most = 5;    // signals the card shows
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static StocksSignal[] Read(UiChartDeskModel m, UiWords words)
        {
            var list = new StocksSignal[Most];
            int count = 0;
            int n = m.Close != null ? m.Close.Length : 0;
            if (n < 2)
            {
                return new StocksSignal[0];
            }
            float price = m.Close[n - 1];
            for (int k = 0; m.Lines != null && k < m.Lines.Length && count < Most; k++)
            {
                if (Line(ref m.Lines[k], n, price, words, out StocksSignal one))
                {
                    list[count++] = one;
                }
            }
            for (int k = 0; m.Panes != null && k < m.Panes.Length && count < Most; k++)
            {
                if (Pane(ref m.Panes[k], n, price, words, out StocksSignal one))
                {
                    list[count++] = one;
                }
            }
            for (int k = (m.Marks != null ? m.Marks.Length : 0) - 1; k >= 0 && count < Most; k--)
            {
                if (Drawing(m.Marks[k], n, price, m, words, out StocksSignal one))
                {
                    list[count++] = one;
                    break;
                }
            }
            var read = new StocksSignal[count];
            Array.Copy(list, read, count);
            return read;
        }

        private static string Fill(string text, string key, string value) => text.Replace("{" + key + "}", value);

        private static float At(float[] v, int i) => v != null && i >= 0 && i < v.Length ? v[i] : float.NaN;

        private static bool Line(ref UiChartLine line, int n, float price, UiWords words, out StocksSignal one)
        {
            one = default(StocksSignal);
            float v = At(line.Values, n - 1);
            if (float.IsNaN(v))
            {
                return false;
            }
            one.Ind = line.Ind;
            if (line.Ind == StocksInd.Bollinger)
            {
                float under = At(line.Under, n - 1);
                if (float.IsNaN(under))
                {
                    return false;
                }
                one.Words = price > v ? words("reallife_stk_sig_band_over", "Above the upper Bollinger band") : price < under ? words("reallife_stk_sig_band_under", "Below the lower Bollinger band")
                    : words("reallife_stk_sig_band_inside", "Inside the Bollinger bands");
                one.Tone = price > v || price < under ? UiRole.Warn : UiRole.None;
                return true;
            }
            bool above = price >= v;
            one.Words = Fill(above ? words("reallife_stk_sig_above", "Price above {name}") : words("reallife_stk_sig_below", "Price below {name}"), "name", line.Name);
            one.Tone = above ? UiRole.Ok : UiRole.Danger;
            return true;
        }

        private static bool Pane(ref UiChartPane pane, int n, float price, UiWords words, out StocksSignal one)
        {
            one = default(StocksSignal);
            float v = At(pane.Values, n - 1);
            if (float.IsNaN(v))
            {
                return false;
            }
            one.Ind = pane.Ind;
            switch (pane.Ind)
            {
                case StocksInd.Rsi:
                case StocksInd.Stochastic:
                {
                    bool rsi = pane.Ind == StocksInd.Rsi;
                    float high = rsi ? 70f : 80f, low = rsi ? 30f : 20f;
                    string name = rsi ? words("reallife_stk_rsi", "RSI") : words("reallife_stk_stoch", "Stochastic");
                    string state = v >= high ? words("reallife_stk_sig_overbought", "overbought")
                        : v <= low ? words("reallife_stk_sig_oversold", "oversold") : words("reallife_stk_sig_neutral", "neutral");
                    one.Words = Fill(Fill(Fill(words("reallife_stk_sig_reading", "{name} {value}: {state}"), "name", name), "value", v.ToString("0", Inv)), "state", state);
                    one.Tone = v >= high ? UiRole.Danger : v <= low ? UiRole.Ok : UiRole.None;
                    return true;
                }
                case StocksInd.Macd:
                {
                    float signal = At(pane.Second, n - 1);
                    if (float.IsNaN(signal))
                    {
                        return false;
                    }
                    bool above = v >= signal;
                    one.Words = above ? words("reallife_stk_sig_macd_over", "MACD above its signal") : words("reallife_stk_sig_macd_under", "MACD below its signal");
                    one.Tone = above ? UiRole.Ok : UiRole.Danger;
                    return true;
                }
                case StocksInd.Atr:
                    one.Words = Fill(Fill(words("reallife_stk_sig_atr", "ATR {value}: {share} of the price"), "value", v.ToString("0.00", Inv)), "share",
                        (price > 0f ? v / price * 100f : 0f).ToString("0.0", Inv) + "%");
                    one.Tone = UiRole.None;
                    return true;
                default:
                {
                    float before = At(pane.Values, Math.Max(0, n - 11));
                    if (float.IsNaN(before))
                    {
                        return false;
                    }
                    bool rising = v >= before;
                    one.Words = rising ? words("reallife_stk_sig_obv_up", "Volume is backing the price up") : words("reallife_stk_sig_obv_down", "Volume is backing the price down");
                    one.Tone = rising ? UiRole.Ok : UiRole.Danger;
                    return true;
                }
            }
        }

        // A drawing the price can be held against: a trend line or ray at the last candle, or a level.
        private static bool Drawing(UiChartMark mark, int n, float price, UiChartDeskModel m, UiWords words, out StocksSignal one)
        {
            one = default(StocksSignal);
            one.Ind = StocksInd.Count;
            if (mark.Kind == UiChartMarkKind.Level)
            {
                bool above = price >= mark.FromY;
                string money = m.Price != null ? m.Price(mark.FromY) : mark.FromY.ToString("0.00", Inv);
                one.Words = Fill(above ? words("reallife_stk_sig_level_over", "Above the level {price}") : words("reallife_stk_sig_level_under", "Below the level {price}"), "price", money);
                one.Tone = above ? UiRole.Ok : UiRole.Danger;
                return true;
            }
            if ((mark.Kind != UiChartMarkKind.Trend && mark.Kind != UiChartMarkKind.Ray) || Math.Abs(mark.ToX - mark.FromX) < 0.5f)
            {
                return false;
            }
            float last = n - 1;
            if (mark.Kind == UiChartMarkKind.Trend && (last < Math.Min(mark.FromX, mark.ToX) || last > Math.Max(mark.FromX, mark.ToX) + 0.5f))
            {
                return false;   // a trend line that ended before the last candle says nothing about it
            }
            float line = mark.FromY + (mark.ToY - mark.FromY) * (last - mark.FromX) / (mark.ToX - mark.FromX);
            bool holding = price >= line;
            one.Words = holding ? words("reallife_stk_sig_trend_over", "Holding above the trend line") : words("reallife_stk_sig_trend_under", "Below the trend line");
            one.Tone = holding ? UiRole.Ok : UiRole.Danger;
            return true;
        }
    }
}
