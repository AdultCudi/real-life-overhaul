using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Ui
{
    /// <summary>A line of a block as the reader lays it: where it starts and ends in the block's plain words.</summary>
    public struct UiReaderLine
    {
        public int Start, End;
    }

    /// <summary>
    /// The reader's words as sums, game-free: a page's text (the Guide's "# heading" and "- line", the game's help pages'
    /// markdown) turned into blocks, a block's words with their bold runs (between &lt;b&gt; and &lt;/b&gt;, the tags the
    /// mod's texts carry) as plain words and a flag a letter, and the lines those words wrap into at a width, each letter
    /// measured in its own face, so a bold name never runs a line past its column. EconomyCheck checks each against the
    /// kit's own measure. Cost: a pass over the words; the reader keeps the heights it found until the width changes.
    /// </summary>
    public static class UiReaderRules
    {
        /// <summary>The letters a reading line holds: 60 to 75 is the measure a long text reads at, its middle here.</summary>
        public const int MeasureLetters = 72;
        public const int LeastLetters = 60, MostLetters = 75;

        // An English sentence's letters in their usual mix, to take the face's average letter from.
        private const string Sample = "The licence holds for a year: renew it in its last days, and keep the plans at the office.";

        /// <summary>The width of a reading column in the body's face: the measure's letters at the face's average, on the grid.</summary>
        public static float ColumnWidth()
        {
            float average = UiTextRules.Width(Sample, UiStyle.Body) / Sample.Length;
            return UiDraw.SnapDown(average * MeasureLetters);
        }

        /// <summary>How many average letters of the body's face a width holds.</summary>
        public static float Letters(float width) => width / (UiTextRules.Width(Sample, UiStyle.Body) / Sample.Length);

        /// <summary>
        /// A page's text as blocks: "# " a heading, "## " a subheading, "- " or "* " a line of a list, "1. " a numbered line,
        /// "&gt; " a note, "![caption](Icon)" a picture, "[words](page)" a link on a line of its own, and every other run of
        /// lines a paragraph (lines joined, a blank line ending it).
        /// </summary>
        public static UiReaderBlock[] Parse(string text)
        {
            var blocks = new List<UiReaderBlock>(64);
            if (string.IsNullOrEmpty(text))
            {
                return blocks.ToArray();
            }
            string[] lines = text.Replace("\r", string.Empty).Split('\n');
            System.Text.StringBuilder para = null;
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                UiReaderBlock block;
                if (!Special(line, out block))
                {
                    if (line.Length == 0)
                    {
                        Flush(blocks, ref para);
                        continue;
                    }
                    para = para ?? new System.Text.StringBuilder(256);
                    if (para.Length > 0)
                    {
                        para.Append(' ');
                    }
                    para.Append(line);
                    continue;
                }
                Flush(blocks, ref para);
                blocks.Add(block);
            }
            Flush(blocks, ref para);
            return blocks.ToArray();
        }

        private static void Flush(List<UiReaderBlock> blocks, ref System.Text.StringBuilder para)
        {
            if (para != null && para.Length > 0)
            {
                blocks.Add(new UiReaderBlock { Kind = UiReaderKind.Paragraph, Text = para.ToString() });
                para.Clear();
            }
        }

        // A line that is a block of its own kind, read from its first marks.
        private static bool Special(string line, out UiReaderBlock block)
        {
            block = default(UiReaderBlock);
            if (line.StartsWith("## ", StringComparison.Ordinal))
            {
                block = new UiReaderBlock { Kind = UiReaderKind.Subheading, Text = line.Substring(3).Trim() };
                return true;
            }
            if (line.StartsWith("# ", StringComparison.Ordinal))
            {
                block = new UiReaderBlock { Kind = UiReaderKind.Heading, Text = line.Substring(2).Trim() };
                return true;
            }
            if (line.StartsWith("- ", StringComparison.Ordinal) || line.StartsWith("* ", StringComparison.Ordinal))
            {
                block = new UiReaderBlock { Kind = UiReaderKind.Bullet, Text = line.Substring(2).Trim() };
                return true;
            }
            if (line.StartsWith("> ", StringComparison.Ordinal))
            {
                block = new UiReaderBlock { Kind = UiReaderKind.Note, Text = line.Substring(2).Trim() };
                return true;
            }
            int dot = line.IndexOf(". ", StringComparison.Ordinal);
            if (dot > 0 && dot <= 3 && int.TryParse(line.Substring(0, dot), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out int number))
            {
                block = new UiReaderBlock { Kind = UiReaderKind.Numbered, Text = line.Substring(dot + 2).Trim(), Number = number };
                return true;
            }
            bool picture = line.StartsWith("![", StringComparison.Ordinal);
            if ((picture || line.StartsWith("[", StringComparison.Ordinal)) && line.EndsWith(")", StringComparison.Ordinal))
            {
                int close = line.IndexOf("](", StringComparison.Ordinal);
                if (close > 0)
                {
                    string words = line.Substring(picture ? 2 : 1, close - (picture ? 2 : 1));
                    string target = line.Substring(close + 2, line.Length - close - 3);
                    if (picture)
                    {
                        UiIcon icon = Enum.TryParse(target, out UiIcon named) ? named : UiIcon.Image;
                        block = new UiReaderBlock { Kind = UiReaderKind.Picture, Text = words, Icon = icon };
                        return true;
                    }
                    int.TryParse(target, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out int page);
                    block = new UiReaderBlock { Kind = UiReaderKind.Link, Text = words, Target = page };
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// A block's words without their tags, and whether each letter is bold (between &lt;b&gt; and &lt;/b&gt;); any other
        /// tag of the mod's texts is dropped, so no mark reaches the screen.
        /// </summary>
        public static string Plain(string text, List<bool> bold)
        {
            bold.Clear();
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }
            var sb = new System.Text.StringBuilder(text.Length);
            bool on = false;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c == '<')
                {
                    int close = text.IndexOf('>', i + 1);
                    if (close > i && close - i <= 8 && Tag(text, i + 1, close))
                    {
                        string tag = text.Substring(i + 1, close - i - 1);
                        on = tag == "b" ? true : tag == "/b" ? false : on;
                        i = close;
                        continue;
                    }
                }
                sb.Append(c);
                bold.Add(on);
            }
            return sb.ToString();
        }

        // A tag's inside: a slash at most, then letters only (b, /b, i, color=...: letters before any '=').
        private static bool Tag(string text, int from, int to)
        {
            int i = from;
            if (i < to && text[i] == '/')
            {
                i++;
            }
            int letters = 0;
            for (; i < to && text[i] != '='; i++)
            {
                if (!char.IsLetter(text[i]))
                {
                    return false;
                }
                letters++;
            }
            return letters > 0;
        }

        /// <summary>A letter's width in its own face: the body's, or its strong cut for a bold letter.</summary>
        public static float LetterWidth(string plain, int i, bool bold, UiStyle style) => UiTextRules.Width(plain, i, i + 1, bold ? Strong(style) : style);

        /// <summary>The strong cut of a style a bold run is drawn in.</summary>
        public static UiStyle Strong(UiStyle style) => style == UiStyle.Body ? UiStyle.BodyStrong : style == UiStyle.Small ? UiStyle.SmallStrong : style;

        /// <summary>
        /// The lines plain words wrap into at a width (greedy, at spaces, a word wider than the line broken inside it as
        /// TextMeshPro breaks it), each letter in its own face. Returns how many; <paramref name="into"/> takes them.
        /// </summary>
        public static int Wrap(string plain, List<bool> bold, UiStyle style, float width, List<UiReaderLine> into)
        {
            into.Clear();
            int n = plain.Length, i = 0;
            while (i < n)
            {
                while (i < n && plain[i] == ' ')
                {
                    i++;
                }
                if (i >= n)
                {
                    break;
                }
                float line = 0f;
                int brk = -1, j = i, end = n, next = n;
                while (j < n)
                {
                    char c = plain[j];
                    if (c == '\n')
                    {
                        end = j;
                        next = j + 1;
                        break;
                    }
                    float w = LetterWidth(plain, j, j < bold.Count && bold[j], style);
                    if (line + w > width && j > i)
                    {
                        if (c == ' ')
                        {
                            end = j;
                            next = j + 1;
                        }
                        else if (brk > i)
                        {
                            end = brk;
                            next = brk + 1;
                        }
                        else
                        {
                            end = j;
                            next = j;
                        }
                        break;
                    }
                    if (c == ' ')
                    {
                        brk = j;
                    }
                    line += w;
                    j++;
                }
                into.Add(new UiReaderLine { Start = i, End = Math.Max(i, end) });
                i = next;
            }
            return into.Count;
        }

        /// <summary>A stretch of a line's width, each letter in its own face.</summary>
        public static float Width(string plain, List<bool> bold, UiStyle style, int start, int end)
        {
            float w = 0f;
            for (int i = start; i < end; i++)
            {
                w += LetterWidth(plain, i, i < bold.Count && bold[i], style);
            }
            return w;
        }
    }
}
