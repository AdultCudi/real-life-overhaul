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
        public static int Lines(string text, UiStyle style, float width, int most, out float widest)
        {
            widest = 0f;
            if (string.IsNullOrEmpty(text))
            {
                return 0;
            }
            UiType t = UiTokens.Type(style);
            UiFace primary = UiFontMetrics.Face(t.Family, t.Weight);
            UiFace backup = null;
            int lines = 0, i = 0, n = text.Length;
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

        /// <summary>The height a text takes wrapped in a width, up to a number of lines, px.</summary>
        public static float WrappedHeight(string text, UiStyle style, float width, int most)
        {
            int lines = Lines(text, style, width, most, out float _);
            return (lines < 1 ? 1 : lines) * LineHeight(style);
        }

        /// <summary>Whether a text fits one line of a width in a style (a label that may not be cut).</summary>
        public static bool Fits(string text, UiStyle style, float width) => Width(text, style) <= width + 0.01f;

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
