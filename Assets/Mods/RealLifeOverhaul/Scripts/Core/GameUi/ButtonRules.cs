namespace RealLifeOverhaul.GameUi
{
    /// <summary>
    /// Where the buttons of a row of the apps' white box stand (InfoBox.ButtonRow, laid out by ButtonFlow). A row shows
    /// every button its page writes, in the order the page writes them; none is ever dropped or pushed out of sight.
    /// The buttons go on one line while their words fit it, and on as many further lines as they need when not: each
    /// line runs from the box's left edge to its right edge, as the game's own row of buttons does, with the game's gap
    /// of 20 between two buttons and between two lines. On a line whose buttons all fit an equal share of it, they share
    /// it equally (as the game's row always did); otherwise each has the room its words need and the rest of the line is
    /// shared out equally among them. A button whose words are wider than the whole line has a line of its own, and its
    /// words end in the game's dots.
    ///
    /// Plain arithmetic: no Unity or game types, nothing kept, nothing allocated. Called only when a row is laid out
    /// (a page written on a click); checked offline (ContractCheck, ButtonChecks).
    /// </summary>
    public static class ButtonRules
    {
        public const float Gap = 20f;          // canvas pixels between two buttons and between two lines: the game's row of buttons (MyEmployees' Buttons, spacing 20)
        public const float Height = 80f;       // canvas pixels: the game's action button (PositiveActionButton, 80 high)
        public const float RowPadding = 20f;   // canvas pixels above and below the buttons: the game's row is 120 high for its buttons of 80
        public const float TextRoom = 80f;     // canvas pixels a button needs beside its words: the game's text stands 20 in from each side, and 20 of air a side
        public const float Least = 160f;       // canvas pixels: the narrowest a button is, so that "OK" is not a stub

        /// <summary>The width a button needs for words of this width: its words, the room beside them, never under <see cref="Least"/>.</summary>
        public static float Need(float wordsWidth)
        {
            float need = (wordsWidth > 0f ? wordsWidth : 0f) + TextRoom;   // (NaN counts as no words)
            return need > Least ? need : Least;
        }

        /// <summary>
        /// Places <paramref name="count"/> buttons that need these widths on lines of this width: each one's line (from 0),
        /// its left edge and its width, written into the arrays given (each at least <paramref name="count"/> long). Returns
        /// the number of lines: 0 for no buttons. A width of nothing (0, below, NaN) places each button on a line of its own,
        /// of no width.
        /// </summary>
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
                // As many buttons as fit, in order, and always at least one.
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

        /// <summary>The height of the buttons on this many lines, without the row's padding: nothing for no lines.</summary>
        public static float Block(int lines)
        {
            return lines > 0 ? lines * Height + (lines - 1) * Gap : 0f;
        }

        /// <summary>The height a row asks for with this many lines of buttons: the game's 120 for one line, nothing for none.</summary>
        public static float RowHeight(int lines)
        {
            return lines > 0 ? Block(lines) + 2f * RowPadding : 0f;
        }

        /// <summary>The top of a line, from the top of a row of this height: the buttons stand in the middle of the row, as in the game's.</summary>
        public static float Top(float rowHeight, int lines, int lineIndex)
        {
            float spare = rowHeight - Block(lines);
            float start = spare > 0f ? spare * 0.5f : 0f;
            return start + lineIndex * (Height + Gap);
        }

        // One line's buttons, first to last: equal shares when every one fits its share, otherwise each its need and an
        // equal part of what is left. The last button ends at the line's right edge exactly.
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
                    width = room;   // a button wider than the line: the line, its words in the game's dots
                }
                line[i] = index;
                x[i] = at;
                w[i] = width;
                at += width + Gap;
            }
        }

        private static float Needed(float need)
        {
            return need > 0f ? need : 0f;   // (NaN or below nothing: nothing)
        }

        private static int Shortest(int a, int b, int c, int d)
        {
            int m = a < b ? a : b;
            m = m < c ? m : c;
            return m < d ? m : d;
        }
    }
}
