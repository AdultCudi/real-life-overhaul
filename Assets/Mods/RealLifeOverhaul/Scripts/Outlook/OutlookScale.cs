using System;

namespace RealLifeOverhaul.Outlook
{
    /// <summary>An axis: the range it covers, the step between two lines and how many lines there are.</summary>
    public struct OutlookAxis
    {
        public double Lo, Hi, Step;
        public int Ticks;

        /// <summary>Where a value stands on the axis, 0 at the bottom to 1 at the top; held inside; the middle for an axis with no height.</summary>
        public double Fraction(double value)
        {
            double span = Hi - Lo;
            if (!(span > 0.0) || double.IsNaN(value))
            {
                return 0.5;
            }
            double f = (value - Lo) / span;
            return f < 0.0 ? 0.0 : f > 1.0 ? 1.0 : f;
        }

        /// <summary>The value of the tick at an index from the bottom.</summary>
        public double Tick(int index)
        {
            return Lo + Step * index;
        }
    }

    /// <summary>
    /// How the Economy app's charts are scaled and smoothed, game-free (EconomyCheck's OutlookChecks): nice axes for any series (a
    /// flat one, a single point, negative values, huge ones, none at all), and a smooth line through points that never swings past
    /// them. Nothing here divides by a range that can be nothing, and nothing returns NaN or infinity: what cannot be drawn is
    /// answered with a sensible axis or no points. A few array passes, run when a chart is drawn.
    /// </summary>
    public static class OutlookScale
    {
        public const double Largest = 1e15;      // a value past this is held to it: a chart is never drawn from numbers a float cannot keep
        public const int TicksMost = 12;

        public static bool Finite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }

        private static double Held(double value)
        {
            return !Finite(value) ? 0.0 : value > Largest ? Largest : value < -Largest ? -Largest : value;
        }

        /// <summary>
        /// A nice axis over a range: round ends and a round step, <paramref name="ticks"/> lines or so. A flat range (or a range
        /// of one value) is given a margin of 5% either way (one, for zero) so the line has somewhere to stand; with
        /// <paramref name="zero"/> the axis always includes zero (for bars).
        /// </summary>
        public static OutlookAxis Nice(double lo, double hi, int ticks, bool zero)
        {
            lo = Held(lo);
            hi = Held(hi);
            if (lo > hi)
            {
                double swap = lo;
                lo = hi;
                hi = swap;
            }
            if (zero)
            {
                lo = Math.Min(lo, 0.0);
                hi = Math.Max(hi, 0.0);
            }
            double scale = Math.Max(1.0, Math.Max(Math.Abs(lo), Math.Abs(hi)));
            if (hi - lo < scale * 1e-9)
            {
                double pad = Math.Abs(hi) > 0.0 ? Math.Abs(hi) * 0.05 : 1.0;
                lo -= pad;
                hi += pad;
            }
            ticks = Math.Max(2, Math.Min(TicksMost, ticks));
            double step = NiceStep((hi - lo) / (ticks - 1));
            double nlo = Math.Floor(lo / step) * step, nhi = Math.Ceiling(hi / step) * step;
            if (!(nhi > nlo))
            {
                nhi = nlo + step;
            }
            int count = (int)Math.Min(TicksMost + 2, Math.Round((nhi - nlo) / step) + 1.0);
            return new OutlookAxis { Lo = nlo, Hi = nhi, Step = step, Ticks = Math.Max(2, count) };
        }

        /// <summary>1, 2, 5 or 10 times a power of ten, the first not under <paramref name="raw"/>; 1 for anything that is not a positive number.</summary>
        public static double NiceStep(double raw)
        {
            if (!(raw > 0.0) || !Finite(raw))
            {
                return 1.0;
            }
            double power = Math.Pow(10.0, Math.Floor(Math.Log10(raw)));
            double f = raw / power;
            double nice = f <= 1.0 ? 1.0 : f <= 2.0 ? 2.0 : f <= 5.0 ? 5.0 : 10.0;
            return nice * power;
        }

        /// <summary>The smallest and largest of the readings in a window (the no-value mark is skipped, and each is scaled by <paramref name="unit"/>); false if there is none.</summary>
        public static bool Range(int[] window, int count, double unit, out double lo, out double hi, out int found)
        {
            lo = double.MaxValue;
            hi = double.MinValue;
            found = 0;
            if (window == null || !(unit > 0.0))
            {
                lo = hi = 0.0;
                return false;
            }
            for (int i = 0; i < count && i < window.Length; i++)
            {
                if (window[i] == OutlookCatalogue.NoValue)
                {
                    continue;
                }
                double v = window[i] / unit;
                lo = Math.Min(lo, v);
                hi = Math.Max(hi, v);
                found++;
            }
            if (found == 0)
            {
                lo = hi = 0.0;
            }
            return found > 0;
        }

        /// <summary>
        /// Lays a window of readings out as points: x by the day (a day with no reading leaves a gap in x, not a dip), y from the
        /// axis, in a box of <paramref name="width"/> by <paramref name="height"/> with y counted down from the top. Returns how many
        /// points; none for an empty window.
        /// </summary>
        public static int Points(int[] window, int count, double unit, OutlookAxis axis, float width, float height, float[] xs, float[] ys)
        {
            int n = 0;
            if (window == null || xs == null || ys == null || !(unit > 0.0))
            {
                return 0;
            }
            count = Math.Min(count, window.Length);
            for (int i = 0; i < count && n < xs.Length && n < ys.Length; i++)
            {
                if (window[i] == OutlookCatalogue.NoValue)
                {
                    continue;
                }
                xs[n] = count <= 1 ? width * 0.5f : width * i / (count - 1);
                ys[n] = height * (float)(1.0 - axis.Fraction(window[i] / unit));
                n++;
            }
            return n;
        }

        /// <summary>
        /// A smooth line through points that go left to right: each interval cut into <paramref name="sub"/> pieces along a
        /// monotone cubic (Fritsch and Carlson's slopes), so the line never swings past a point it passes through. Returns how
        /// many points were written to <paramref name="ox"/> and <paramref name="oy"/>, or 0 if they are too short.
        /// </summary>
        public static int Smooth(float[] x, float[] y, int n, int sub, float[] ox, float[] oy)
        {
            if (x == null || y == null || ox == null || oy == null || n <= 0)
            {
                return 0;
            }
            sub = Math.Max(1, sub);
            int total = n == 1 ? 1 : (n - 1) * sub + 1;
            if (ox.Length < total || oy.Length < total)
            {
                return 0;
            }
            if (n == 1)
            {
                ox[0] = x[0];
                oy[0] = y[0];
                return 1;
            }
            int at = 0;
            for (int k = 0; k < n - 1; k++)
            {
                double dx = x[k + 1] - x[k];
                double slope = dx > 0.0 ? (y[k + 1] - y[k]) / dx : 0.0;
                double m0 = Tangent(x, y, n, k), m1 = Tangent(x, y, n, k + 1);
                if (dx > 0.0)
                {
                    // The tangents may not be steeper than three times the interval's own slope, or the curve overshoots.
                    double limit = 3.0 * Math.Abs(slope);
                    m0 = Math.Max(-limit, Math.Min(limit, m0));
                    m1 = Math.Max(-limit, Math.Min(limit, m1));
                }
                for (int s = 0; s < sub; s++)
                {
                    double t = (double)s / sub;
                    ox[at] = (float)(x[k] + dx * t);
                    oy[at] = dx > 0.0 ? (float)Hermite(y[k], y[k + 1], m0, m1, dx, t) : y[k];
                    at++;
                }
            }
            ox[at] = x[n - 1];
            oy[at] = y[n - 1];
            return at + 1;
        }

        // The slope the curve takes at a point: the mean of the two secants if they lean the same way, nothing at a turn.
        private static double Tangent(float[] x, float[] y, int n, int k)
        {
            double before = k > 0 && x[k] > x[k - 1] ? (y[k] - y[k - 1]) / (x[k] - x[k - 1]) : double.NaN;
            double after = k < n - 1 && x[k + 1] > x[k] ? (y[k + 1] - y[k]) / (x[k + 1] - x[k]) : double.NaN;
            if (double.IsNaN(before))
            {
                return double.IsNaN(after) ? 0.0 : after;
            }
            if (double.IsNaN(after))
            {
                return before;
            }
            if (before * after <= 0.0)
            {
                return 0.0;
            }
            return 2.0 * before * after / (before + after);   // the harmonic mean: monotone
        }

        private static double Hermite(double y0, double y1, double m0, double m1, double dx, double t)
        {
            double t2 = t * t, t3 = t2 * t;
            return (2.0 * t3 - 3.0 * t2 + 1.0) * y0 + (t3 - 2.0 * t2 + t) * dx * m0 + (-2.0 * t3 + 3.0 * t2) * y1 + (t3 - t2) * dx * m1;
        }
    }
}
