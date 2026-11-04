namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// How much room a text takes in a style, measured from the bundle's own font metrics (UiFontMetrics): a line's width,
    /// a line's height, and the lines a text wraps to in a width. The kit lays every text out with these, in the game and
    /// in EconomyCheck alike, so a label the check passes in every language is the label the game draws. A character Sora
    /// lacks is measured in Manrope of the same weight (as the bundle falls back), a CJK or Hangul one as a square (the
    /// game's Noto fallbacks), anything else as an average letter. Kerning is left out: it only narrows. Game-free.
    /// Cost: a table lookup a character; nothing allocated.
    /// </summary>
    public static class UiTextRules
    {
        public const string Ellipsis = "…";

        /// <summary>A line's height in a style, px.</summary>
        public static float LineHeight(UiStyle style)
        {
            UiType t = UiTokens.Type(style);
            return UiFontMetrics.Face(t.Family, t.Weight).LineEm * t.Size * t.Leading;
        }

        /// <summary>The height of a style's capitals, px: what a label is centred on optically.</summary>
        public static float CapHeight(UiStyle style)
        {
            UiType t = UiTokens.Type(style);
            UiFace f = UiFontMetrics.Face(t.Family, t.Weight);
            return f.CapHeight / (float)f.UnitsPerEm * t.Size;
        }

        /// <summary>A text's width on one line in a style, px; its case as the style writes it.</summary>
        public static float Width(string text, UiStyle style) => text == null ? 0f : Width(text, 0, text.Length, style);

        /// <summary>The width of part of a text, from start up to (not including) end, px.</summary>
        public static float Width(string text, int start, int end, UiStyle style)
        {
            if (string.IsNullOrEmpty(text) || end <= start)
            {
                return 0f;
            }
            UiType t = UiTokens.Type(style);
            UiFace primary = UiFontMetrics.Face(t.Family, t.Weight);
            UiFace backup = null;   // both families draw every script the bundle lists; CJK and Hangul fall to the game's font
            float em = 0f;
            int count = 0;
            for (int i = start; i < end; i++)
            {
                char c = text[i];
                if (c == '\n' || c == '\r')
                {
                    continue;
                }
                if (char.IsHighSurrogate(c))
                {
                    em += 1f;   // beyond the basic plane (a pictograph): drawn by a fallback as a square
                    count++;
                    i++;
                    continue;
                }
                em += AdvanceEm(Cased(c, t.Case), primary, backup);
                count++;
            }
            return (em + t.Tracking * count) * t.Size;
        }

        /// <summary>
        /// The lines a text takes wrapped in a width (greedy, at spaces, and between two wide characters), at most
        /// <paramref name="most"/>; <paramref name="widest"/> is the longest line's width. A word wider than the line
        /// breaks inside itself, as TextMeshPro does.
        /// </summary>
        public static int Lines(string text, UiStyle style, float width, int most, out float widest) => Wrap(text, style, width, most, -1, out widest, out int _, out int _);

        /// <summary>
        /// Set while a screen is laid that wants Chinese and Japanese wrapped as TextMeshPro draws it (a character early, so a line keeps an em in hand): the work apps, whose
        /// tiles and notes are boxes of a fixed number of lines. Off for every other screen, whose boxes follow the plain measure.
        /// </summary>
        public static bool EmInHand;

        // The greedy wrap: the lines it takes, the widest, where line number wantLine begins and where the last line begins.
        private static int Wrap(string text, UiStyle style, float width, int most, int wantLine, out float widest, out int wantStart, out int lastStart)
        {
            widest = 0f;
            wantStart = 0;
            lastStart = 0;
            if (string.IsNullOrEmpty(text))
            {
                return 0;
            }
            UiType t = UiTokens.Type(style);
            UiFace primary = UiFontMetrics.Face(t.Family, t.Weight);
            UiFace backup = null;
            int lines = 0, i = 0, n = text.Length;
            for (int k = 0; EmInHand && k < n; k++)
            {
                if ((text[k] >= '⺀' && text[k] <= '鿿') || (text[k] >= '＀' && text[k] <= '￯'))
                {
                    width -= t.Size;   // TextMeshPro breaks Chinese and Japanese (not Korean, which has spaces) a character early where punctuation may not start a line: a line of them keeps an em in hand, so no text is laid in fewer lines than it is drawn in
                    break;
                }
            }
            while (i < n && (most <= 0 || lines < most))
            {
                while (i < n && text[i] == ' ')
                {
                    i++;   // a wrapped line drops the spaces it starts with
                }
                if (i >= n)
                {
                    break;
                }
                lines++;
                lastStart = i;
                if (lines - 1 == wantLine)
                {
                    wantStart = i;
                }
                float line = 0f, atBreak = 0f;
                int brk = -1, j = i;
                bool ended = false;
                while (j < n)
                {
                    char c = text[j];
                    if (c == '\n')
                    {
                        widest = line > widest ? line : widest;
                        i = j + 1;
                        ended = true;
                        break;
                    }
                    float w = (AdvanceEm(Cased(c, t.Case), primary, backup) + t.Tracking) * t.Size;
                    if (c == ' ')
                    {
                        if (line + w > width && j > i)
                        {
                            widest = line > widest ? line : widest;   // the space ends the line: the word before it stays
                            i = j + 1;
                            ended = true;
                            break;
                        }
                        brk = j;
                        atBreak = line;
                        line += w;
                        j++;
                        continue;
                    }
                    if (line + w > width && j > i)
                    {
                        if (brk >= i)
                        {
                            widest = atBreak > widest ? atBreak : widest;
                            i = brk + 1;
                        }
                        else
                        {
                            widest = line > widest ? line : widest;
                            i = j;
                        }
                        ended = true;
                        break;
                    }
                    line += w;
                    if (Wide(c))
                    {
                        brk = j;
                        atBreak = line;
                    }
                    j++;
                }
                if (!ended)
                {
                    widest = line > widest ? line : widest;
                    i = n;
                }
            }
            return lines;
        }

        /// <summary>
        /// The end of a text that fills at most <paramref name="most"/> lines of a width, whole lines as they wrap, so a field
        /// that shows a few lines shows the ones the caret is on: the text itself when it fits. <paramref name="last"/> is
        /// the width of the last line, where the caret stands. Cost: two wraps of the text, once a layout.
        /// </summary>
        public static string LastLines(string text, UiStyle style, float width, int most, out float last)
        {
            last = 0f;
            if (string.IsNullOrEmpty(text))
            {
                return text;
            }
            int total = Wrap(text, style, width, 0, -1, out float _, out int _, out int lastStart);
            int skip = total - (most < 1 ? 1 : most);
            if (skip > 0)
            {
                Wrap(text, style, width, 0, skip, out float _, out int start, out lastStart);
                text = text.Substring(start);
                lastStart -= start;
            }
            last = System.Math.Min(width, Width(text, lastStart, text.Length, style));
            return text;
        }

        /// <summary>
        /// The end of a one line text that fits a width, nothing added: what a field shows while it is typed in, so the caret
        /// stands after the last letter as it does in a game's own field. The text itself when it fits. Cost: a few widths.
        /// </summary>
        public static string Tail(string text, UiStyle style, float width)
        {
            if (string.IsNullOrEmpty(text) || Fits(text, style, width))
            {
                return text;
            }
            int low = 0, high = text.Length;   // the first letter kept: the least start whose rest fits
            while (low < high)
            {
                int mid = (low + high) / 2;
                if (Width(text, mid, text.Length, style) <= width)
                {
                    high = mid;
                }
                else
                {
                    low = mid + 1;
                }
            }
            return text.Substring(low);
        }

        /// <summary>The height a text takes wrapped in a width, up to a number of lines, px.</summary>
        public static float WrappedHeight(string text, UiStyle style, float width, int most)
        {
            int lines = Lines(text, style, width, most, out float _);
            return (lines < 1 ? 1 : lines) * LineHeight(style);
        }

        /// <summary>Whether a text fits one line of a width in a style (a label that may not be cut).</summary>
        public static bool Fits(string text, UiStyle style, float width) => Width(text, style) <= width + 0.01f;

        /// <summary>
        /// Player words cut to one line of a width with an ellipsis: after the last whole word when that keeps most of the
        /// room, else between letters (as in a script without spaces), never with a space or a stop before the ellipsis.
        /// Cost: a width a letter, once a layout.
        /// </summary>
        public static string Cut(string text, UiStyle style, float width)
        {
            if (string.IsNullOrEmpty(text) || Fits(text, style, width))
            {
                return text;
            }
            const string dots = "…";
            float room = width - Width(dots, style);
            int fit = 0;
            while (fit < text.Length && Width(text, 0, fit + 1, style) <= room)
            {
                fit++;
            }
            int end = fit;
            int space = fit < text.Length ? text.LastIndexOf(' ', fit) : -1;
            if (space > 0 && space >= fit * 0.6f)
            {
                end = space;   // a one letter stub of the next word reads as a fault
            }
            while (end > 0 && (text[end - 1] == ' ' || text[end - 1] == ',' || text[end - 1] == '.' || text[end - 1] == ':'))
            {
                end--;
            }
            return text.Substring(0, end) + dots;
        }

        /// <summary>A character square as an ideograph: CJK, kana, Hangul and full-width forms.</summary>
        public static bool Wide(char c)
        {
            return (c >= 'ᄀ' && c <= 'ᅟ') || (c >= '⺀' && c <= '꓏') || (c >= '가' && c <= '힣')
                   || (c >= '豈' && c <= '﫿') || (c >= '︰' && c <= '﹏') || (c >= '＀' && c <= '｠')
                   || (c >= '￠' && c <= '￦');
        }

        private static char Cased(char c, UiCase textCase) => textCase == UiCase.Upper ? char.ToUpperInvariant(c) : textCase == UiCase.Lower ? char.ToLowerInvariant(c) : c;

        private static float AdvanceEm(char c, UiFace primary, UiFace backup)
        {
            int a = primary.Advance(c);
            if (a >= 0)
            {
                return a / (float)primary.UnitsPerEm;
            }
            if (backup != null)
            {
                a = backup.Advance(c);
                if (a >= 0)
                {
                    return a / (float)backup.UnitsPerEm;
                }
            }
            return Wide(c) ? 1f : primary.AverageEm;
        }
    }
}
