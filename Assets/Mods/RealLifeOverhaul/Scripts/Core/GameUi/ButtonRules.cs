namespace RealLifeOverhaul.GameUi
{
    /// <summary>
    /// Where the buttons of a row of the white box stand: in order on as many lines as their words need, each line from the
    /// box's left edge to its right with the game's gap of 20, shared equally where they all fit, the rest shared out where
    /// not. Game-free; checked offline (ContractCheck ButtonChecks).
    /// </summary>
    public static class ButtonRules
    {
        public const float Gap = 20f;          // canvas pixels between two buttons and two lines: the game's row (MyEmployees' Buttons, spacing 20)
        public const float Height = 80f;       // canvas pixels: the game's action button (PositiveActionButton, 80 high)
        public const float RowPadding = 20f;   // canvas pixels above and below the buttons: the game's row is 120 high for buttons of 80
        public const float TextRoom = 80f;     // canvas pixels beside a button's words: the game's text stands 20 in from each side, and 20 of air a side
        public const float Least = 160f;       // canvas pixels: the narrowest button, so "OK" is no stub

        /// <summary>A button's width for words this wide: the words and the room beside them, never under <see cref="Least"/>.</summary>
        public static float Need(float wordsWidth)
        {
            float need = (wordsWidth > 0f ? wordsWidth : 0f) + TextRoom;   // NaN counts as no words
            return need > Least ? need : Least;
        }

        /// <summary>Writes each button's line, left edge and width into the arrays; returns the lines (0 for none). No width puts each on a line of its own.</summary>
        public static int Place(float width, float[] needs, int count, int[] line, float[] x, float[] w)
        {
            if (count <= 0 || needs == null || line == null || x == null || w == null)
            {
                return 0;
            }
            if (count > needs.Length || count > line.Length || count > x.Length || count > w.Length)
            {
                count = Shortest(needs.Length, line.Length, x.Length, w.Length);
            }
            float room = width > 0f ? width : 0f;
            int lines = 0, first = 0;
            while (first < count)
            {
                // As many as fit, in order, and at least one.
                float used = Needed(needs[first]);
                int last = first;
                while (last + 1 < count && used + Gap + Needed(needs[last + 1]) <= room)
                {
                    last++;
                    used += Gap + Needed(needs[last]);
                }
                Line(room, needs, first, last, lines, line, x, w);
                lines++;
                first = last + 1;
            }
            return lines;
        }

        /// <summary>The height of the buttons on this many lines, without the row's padding.</summary>
        public static float Block(int lines)
        {
            return lines > 0 ? lines * Height + (lines - 1) * Gap : 0f;
        }

        /// <summary>The height a row asks for: the game's 120 for one line, nothing for none.</summary>
        public static float RowHeight(int lines)
        {
            return lines > 0 ? Block(lines) + 2f * RowPadding : 0f;
        }

        /// <summary>A line's top from a row's top: the buttons stand in the middle of the row, as in the game's.</summary>
        public static float Top(float rowHeight, int lines, int lineIndex)
        {
            float spare = rowHeight - Block(lines);
            float start = spare > 0f ? spare * 0.5f : 0f;
            return start + lineIndex * (Height + Gap);
        }

        // One line's buttons: equal shares when each fits its share, else each its need and an equal part of the rest. The last
        // ends at the right edge exactly.
        private static void Line(float room, float[] needs, int first, int last, int index, int[] line, float[] x, float[] w)
        {
            int k = last - first + 1;
            float share = (room - (k - 1) * Gap) / k;
            bool equal = true;
            float total = 0f;
            for (int i = first; i <= last; i++)
            {
                float need = Needed(needs[i]);
                total += need;
                equal &= need <= share;
            }
            float extra = (room - (k - 1) * Gap - total) / k;
            float at = 0f;
            for (int i = first; i <= last; i++)
            {
                float width = equal ? share : Needed(needs[i]) + extra;
                if (i == last)
                {
                    width = room - at;   // no rounding left over at the right edge
                }
                if (width < 0f)
                {
                    width = 0f;
                }
                if (width > room)
                {
                    width = room;   // wider than the line: the line, its words in the game's dots
                }
                line[i] = index;
                x[i] = at;
                w[i] = width;
                at += width + Gap;
            }
        }

        private static float Needed(float need)
        {
            return need > 0f ? need : 0f;   // NaN or below nothing: nothing
        }

        private static int Shortest(int a, int b, int c, int d)
        {
            int m = a < b ? a : b;
            m = m < c ? m : c;
            return m < d ? m : d;
        }
    }
}
