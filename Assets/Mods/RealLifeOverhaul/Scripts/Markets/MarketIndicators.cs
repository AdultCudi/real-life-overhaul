using System;

namespace RealLifeOverhaul.Markets
{
    /// <summary>
    /// A chart's analysis tools over a run of candles. Game-free; each fills an array the caller keeps, with NaN until there
    /// is history enough. An EMA starts from the simple average of its first n; Bollinger bands use the window's population
    /// deviation; RSI and ATR are Wilder's, smoothed over n.
    /// </summary>
    public static class MarketIndicators
    {
        public static void Sma(double[] values, int count, int n, double[] into)
        {
            double sum = 0.0;
            for (int i = 0; i < count; i++)
            {
                sum += values[i];
                if (i >= n)
                {
                    sum -= values[i - n];
                }
                into[i] = i >= n - 1 ? sum / n : double.NaN;
            }
        }

        public static void Ema(double[] values, int count, int n, double[] into)
        {
            Ema(values, 0, count, n, into);
        }

        // From a first index on; the values before it are not used.
        private static void Ema(double[] values, int from, int count, int n, double[] into)
        {
            double alpha = 2.0 / (n + 1);
            double sum = 0.0;
            for (int i = 0; i < count; i++)
            {
                if (i < from)
                {
                    into[i] = double.NaN;
                    continue;
                }
                int k = i - from;
                if (k < n - 1)
                {
                    sum += values[i];
                    into[i] = double.NaN;
                }
                else if (k == n - 1)
                {
                    sum += values[i];
                    into[i] = sum / n;
                }
                else
                {
                    into[i] = alpha * values[i] + (1.0 - alpha) * into[i - 1];
                }
            }
        }

        public static void Bollinger(double[] values, int count, int n, double k, double[] middle, double[] upper, double[] lower)
        {
            Sma(values, count, n, middle);
            for (int i = 0; i < count; i++)
            {
                if (i < n - 1)
                {
                    upper[i] = lower[i] = double.NaN;
                    continue;
                }
                double m = middle[i], sq = 0.0;
                for (int j = i - n + 1; j <= i; j++)
                {
                    double d = values[j] - m;
                    sq += d * d;
                }
                double sd = Math.Sqrt(sq / n);
                upper[i] = m + k * sd;
                lower[i] = m - k * sd;
            }
        }

        public static void Rsi(double[] values, int count, int n, double[] into)
        {
            double gain = 0.0, loss = 0.0;
            for (int i = 0; i < count; i++)
            {
                if (i == 0)
                {
                    into[i] = double.NaN;
                    continue;
                }
                double change = values[i] - values[i - 1];
                double up = change > 0.0 ? change : 0.0, down = change < 0.0 ? -change : 0.0;
                if (i < n)
                {
                    gain += up;
                    loss += down;
                    into[i] = double.NaN;
                    continue;
                }
                if (i == n)
                {
                    gain = (gain + up) / n;
                    loss = (loss + down) / n;
                }
                else
                {
                    gain = (gain * (n - 1) + up) / n;
                    loss = (loss * (n - 1) + down) / n;
                }
                into[i] = loss <= 0.0 ? (gain <= 0.0 ? 50.0 : 100.0) : 100.0 - 100.0 / (1.0 + gain / loss);
            }
        }

        /// <summary>MACD with its signal and histogram. 'scratch' is a work array as long as the others.</summary>
        public static void Macd(double[] values, int count, int fast, int slow, int signalN, double[] macd, double[] signal, double[] histogram, double[] scratch)
        {
            Ema(values, count, fast, scratch);
            Ema(values, count, slow, macd);
            for (int i = 0; i < count; i++)
            {
                macd[i] = double.IsNaN(macd[i]) || double.IsNaN(scratch[i]) ? double.NaN : scratch[i] - macd[i];
            }
            Ema(macd, slow - 1, count, signalN, signal);
            for (int i = 0; i < count; i++)
            {
                histogram[i] = double.IsNaN(macd[i]) || double.IsNaN(signal[i]) ? double.NaN : macd[i] - signal[i];
            }
        }

        public static void Atr(double[] high, double[] low, double[] close, int count, int n, double[] into)
        {
            double avg = 0.0;
            for (int i = 0; i < count; i++)
            {
                double range = i == 0 ? high[i] - low[i] : Math.Max(high[i] - low[i], Math.Max(Math.Abs(high[i] - close[i - 1]), Math.Abs(low[i] - close[i - 1])));
                if (i < n - 1)
                {
                    avg += range;
                    into[i] = double.NaN;
                }
                else if (i == n - 1)
                {
                    avg = (avg + range) / n;
                    into[i] = avg;
                }
                else
                {
                    avg = (avg * (n - 1) + range) / n;
                    into[i] = avg;
                }
            }
        }

        public static void Stochastic(double[] high, double[] low, double[] close, int count, int n, int dN, double[] k, double[] d)
        {
            for (int i = 0; i < count; i++)
            {
                if (i < n - 1)
                {
                    k[i] = double.NaN;
                    continue;
                }
                double hi = double.MinValue, lo = double.MaxValue;
                for (int j = i - n + 1; j <= i; j++)
                {
                    hi = Math.Max(hi, high[j]);
                    lo = Math.Min(lo, low[j]);
                }
                k[i] = hi - lo <= 0.0 ? 50.0 : (close[i] - lo) / (hi - lo) * 100.0;
            }
            for (int i = 0; i < count; i++)
            {
                if (i < n - 1 + dN - 1)
                {
                    d[i] = double.NaN;
                    continue;
                }
                double sum = 0.0;
                for (int j = i - dN + 1; j <= i; j++)
                {
                    sum += k[j];
                }
                d[i] = sum / dN;
            }
        }

        public static void Obv(double[] close, double[] volume, int count, double[] into)
        {
            double total = 0.0;
            for (int i = 0; i < count; i++)
            {
                if (i > 0)
                {
                    total += close[i] > close[i - 1] ? volume[i] : close[i] < close[i - 1] ? -volume[i] : 0.0;
                }
                into[i] = total;
            }
        }
    }
}
