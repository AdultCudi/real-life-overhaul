using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The kit's motion sums, game-free: the wireframe's curve (cubic-bezier .2, .9, .2, 1) as a function of time, a
    /// row's delay in an arriving list, and a figure counting up to its value written into a buffer in the figure's own
    /// form (its prefix, grouping, decimals and suffix kept), so a number ticks up without a new string each frame.
    /// Cost: a few multiplications; nothing allocated.
    /// </summary>
    public static class UiMotionRules
    {
        /// <summary>The curve's progress at a share of the time, 0 to 1 (eight Newton steps on the curve's x).</summary>
        public static float Ease(float t)
        {
            if (t <= 0f)
            {
                return 0f;
            }
            if (t >= 1f)
            {
                return 1f;
            }
            float u = t;
            for (int i = 0; i < 8; i++)
            {
                float x = Bezier(u, UiTokens.EaseX1, UiTokens.EaseX2) - t;
                float dx = Slope(u, UiTokens.EaseX1, UiTokens.EaseX2);
                if (Math.Abs(dx) < 1e-5f)
                {
                    break;
                }
                u -= x / dx;
                u = u < 0f ? 0f : u > 1f ? 1f : u;
            }
            return Bezier(u, UiTokens.EaseY1, UiTokens.EaseY2);
        }

        /// <summary>A row's delay as its list arrives, in seconds: the stagger a row, the later rows with the last that staggers.</summary>
        public static float Delay(int order, UiMotionSetting setting)
        {
            if (order < 0 || setting == UiMotionSetting.Off)
            {
                return 0f;
            }
            float d = Math.Min(order, UiTokens.StaggerMost) * UiTokens.Stagger;
            return setting == UiMotionSetting.Calm ? d * UiTokens.CalmTime : d;
        }

        /// <summary>
        /// Writes a figure at a share of its value ("$48,210" at 0.5 is "$24,105"), keeping its form, into a buffer;
        /// returns the length, or -1 when the figure holds no number to count.
        /// </summary>
        public static int CountInto(string figure, float share, char[] buffer)
        {
            if (string.IsNullOrEmpty(figure) || buffer == null)
            {
                return -1;
            }
            int start = -1, end = -1;
            for (int i = 0; i < figure.Length; i++)
            {
                if (char.IsDigit(figure[i]))
                {
                    start = start < 0 ? i : start;
                    end = i + 1;
                }
                else if (start >= 0 && !IsGroupOrPoint(figure, i))
                {
                    break;
                }
            }
            if (start < 0)
            {
                return -1;
            }
            char group = '\0', point = '\0';
            int decimals = 0;
            double value = 0;
            bool afterPoint = false;
            for (int i = start; i < end; i++)
            {
                char c = figure[i];
                if (char.IsDigit(c))
                {
                    value = value * 10 + (c - '0');
                    decimals += afterPoint ? 1 : 0;
                    continue;
                }
                int run = 0;
                for (int k = i + 1; k < end && char.IsDigit(figure[k]); k++)
                {
                    run++;
                }
                if (run == 3 && !afterPoint)
                {
                    group = c;   // thousands: "48,210", "1.234"
                }
                else
                {
                    point = c;   // decimals: "1.8", "1,5"
                    afterPoint = true;
                }
            }
            double shown = Math.Round(value * Math.Max(0f, Math.Min(1f, share)));
            int n = 0;
            for (int i = 0; i < start && n < buffer.Length; i++)
            {
                buffer[n++] = figure[i];
            }
            n = WriteNumber(shown, decimals, group, point == '\0' ? '.' : point, buffer, n);
            for (int i = end; i < figure.Length && n < buffer.Length; i++)
            {
                buffer[n++] = figure[i];
            }
            return n;
        }

        private static bool IsGroupOrPoint(string s, int i)
        {
            char c = s[i];
            return (c == ',' || c == '.' || c == ' ' || c == ' ' || c == ' ') && i + 1 < s.Length && char.IsDigit(s[i + 1]);
        }

        private static int WriteNumber(double whole, int decimals, char group, char point, char[] buffer, int n)
        {
            long all = (long)whole;
            long scale = 1;
            for (int i = 0; i < decimals; i++)
            {
                scale *= 10;
            }
            long intPart = all / scale, frac = all % scale;
            int digits = 1;
            for (long v = intPart; v >= 10; v /= 10)
            {
                digits++;
            }
            for (int i = digits - 1; i >= 0 && n < buffer.Length; i--)
            {
                long d = intPart;
                for (int k = 0; k < i; k++)
                {
                    d /= 10;
                }
                buffer[n++] = (char)('0' + d % 10);
                if (group != '\0' && i > 0 && i % 3 == 0 && n < buffer.Length)
                {
                    buffer[n++] = group;
                }
            }
            if (decimals > 0 && n < buffer.Length)
            {
                buffer[n++] = point;
                for (int i = decimals - 1; i >= 0 && n < buffer.Length; i--)
                {
                    long d = frac;
                    for (int k = 0; k < i; k++)
                    {
                        d /= 10;
                    }
                    buffer[n++] = (char)('0' + d % 10);
                }
            }
            return n;
        }

        private static float Bezier(float u, float p1, float p2)
        {
            float v = 1f - u;
            return 3f * v * v * u * p1 + 3f * v * u * u * p2 + u * u * u;
        }

        private static float Slope(float u, float p1, float p2)
        {
            float v = 1f - u;
            return 3f * v * v * p1 + 6f * v * u * (p2 - p1) + 3f * u * u * (1f - p2);
        }
    }
}
