using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The sums behind the Economy app's cards: a line only over the days that have a reading, a change as a pill's words, a meter's share of its scale. Game-free.
    /// </summary>
    public static class NewsCardRules
    {
        /// <summary>The first day of a window (oldest first) that holds a reading, or -1 when none does.</summary>
        public static int FirstKept(int[] window, int count, int noValue)
        {
            for (int i = 0; window != null && i < count && i < window.Length; i++)
            {
                if (window[i] != noValue)
                {
                    return i;
                }
            }
            return -1;
        }

        /// <summary>
        /// A line from its first reading to its last: a day without a reading after the first carries the one before, and the days before the first are left out,
        /// never drawn as if the price had stood there. Null when the window holds no reading. <paramref name="first"/> is the window's day the line starts on.
        /// </summary>
        public static float[] Kept(int[] window, int count, int noValue, Func<int, float> conv, out int first)
        {
            first = FirstKept(window, count, noValue);
            if (first < 0)
            {
                return null;
            }
            int n = Math.Min(count, window.Length);
            var line = new float[n - first];
            float carry = 0f;
            for (int i = first; i < n; i++)
            {
                if (window[i] != noValue)
                {
                    carry = conv(window[i]);
                }
                line[i - first] = carry;
            }
            return line;
        }

        /// <summary>
        /// A change over some days as a pill's size: the figure's own words (a percent under 0.05 is none), or a dash where the book has not kept so many days.
        /// <paramref name="way"/> is 1 for a rise, -1 for a fall, 0 for none or unknown.
        /// </summary>
        public static string Pill(bool known, int value, bool percent, Func<int, string> size, out int way)
        {
            way = 0;
            if (!known)
            {
                return "-";
            }
            int v = percent && Math.Abs(value) < 5 ? 0 : value;
            way = v > 0 ? 1 : v < 0 ? -1 : 0;
            return size(Math.Abs(v));
        }

        /// <summary>A value's place on a scale from <paramref name="low"/> to <paramref name="high"/>, 0 to 1.</summary>
        public static float Share(float value, float low, float high)
        {
            if (high <= low)
            {
                return 0f;
            }
            float share = (value - low) / (high - low);
            return share < 0f ? 0f : share > 1f ? 1f : share;
        }

        /// <summary>A name with its first letter capital, as the wholesale card writes a kind of goods.</summary>
        public static string Capital(string text)
        {
            return string.IsNullOrEmpty(text) || !char.IsLower(text[0]) ? text : char.ToUpperInvariant(text[0]) + text.Substring(1);
        }

        /// <summary>
        /// The columns of the districts' table that carry something: a use's demand and the market level are the mod's own districts' alone, so a city without them
        /// has no column of them.
        /// </summary>
        public static bool AnyDemand(bool[] has)
        {
            for (int i = 0; has != null && i < has.Length; i++)
            {
                if (has[i])
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>A department's step, -2 to 2, as a share of its five steps (0 to 1): its dot's place on the old card.</summary>
        public static float Step(int level)
        {
            int l = level < -2 ? -2 : level > 2 ? 2 : level;
            return (l + 2) / 4f;
        }

        /// <summary>The panels a column holds when the page has a height for them: as many rows as fit, and how many are left over.</summary>
        public static int RowsThatFit(float[] rowHeights, float room, out int left)
        {
            int fit = 0;
            float used = 0f;
            int n = rowHeights == null ? 0 : rowHeights.Length;
            while (fit < n && used + rowHeights[fit] <= room + 0.5f)
            {
                used += rowHeights[fit];
                fit++;
            }
            fit = fit == 0 && n > 0 && room > 0f ? 1 : fit;
            left = n - fit;
            return fit;
        }
    }
}
