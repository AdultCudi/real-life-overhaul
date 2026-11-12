using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// A page written as lines (the mod's own Interior tab): the left column's lines beside the right's, the right's pages
    /// as tabs over it and its buttons at its foot. A short "label: figure" line stands as a row with its figure at the
    /// right, a score out of 100 as a graded bar, a heading in capitals as every section's title; a sentence stays a
    /// sentence. Each column scrolls on its own when its lines run past the window, and each heading is a stop for the pad,
    /// so the pad reads a long page heading by heading. Game-free.
    /// </summary>
    public static partial class BizManPages
    {
        private const int MostLines = 6;
        private const int ShortPart = 28;   // characters: a line's label and figure each at most this long stand as a row
        private static readonly System.Text.RegularExpressions.Regex OutOf100 =
            new System.Text.RegularExpressions.Regex("^([0-9]{1,3}) of 100$");   // a line's most rows before it is cut: the game's own box holds a long line whole, the column scrolls

        private static void InteriorPage(UiScene s, UiAt at, BizManModel m, float x, float y, float w, float bottom, IUiKeys keys)
        {
            float half = Half(w), rx = x + half + UiTokens.S8;
            m.LinesOffset = Column(s, at, m.Lines, x, y, half, bottom - y, m.LinesOffset, ref m.LinesReveal, 0, out m.LinesClip, out m.LinesList, true);
            float ry = y;
            bool tabs = PeriodWidth(m.Pages) <= half;   // the pages as tabs where their names fit, or else as a choice that opens their list
            if (m.Pages.Length > 1)
            {
                if (tabs)
                {
                    UiDraw.Tabs(s, at, rx, ry, PeriodWidth(m.Pages), m.Pages, m.PageShown, null, Page);
                }
                else
                {
                    UiDraw.Choice(s, at, rx, ry, half, m.PageShown >= 0 && m.PageShown < m.Pages.Length ? m.Pages[m.PageShown] : null, Pages, 0, false);
                }
                ry += UiTokens.Control + UiTokens.S4;
            }
            float foot = bottom;
            if (m.Actions.Length > 0)
            {
                foot = bottom - UiTokens.Control - UiTokens.S4;
                Buttons(s, at, m.Actions, rx + half, bottom - UiTokens.Control, keys, Action);
            }
            m.SecondOffset = Column(s, at, m.SecondLines, rx, ry, half, foot - ry, m.SecondOffset, ref m.SecondReveal, SecondColumn, out m.SecondClip, out m.SecondList, false);
            if (m.PagesOpen && !tabs && m.Pages.Length > 1)
            {
                Behind(s, x, y, w, bottom);
                UiDraw.ChoiceList(s, at, rx, y + UiTokens.Control + UiTokens.S1, half, m.Pages, m.Pages.Length, m.PageShown, 0f, Page);
            }
        }

        // A column of lines in a region that scrolls when they run past it, its thumb at its right edge. Returns the offset it took,
        // and gives its region and its headings' list, so the wheel and the pad scroll the column they are on.
        private static float Column(UiScene s, UiAt at, BizManLine[] lines, float x, float y, float w, float h, float offset, ref int reveal, int arg0, out short clip,
            out short list, bool rows)
        {
            float tw = w - UiTokens.S4;   // the thumb's lane
            float total = 0f, revealTop = -1f, revealH = 0f;
            int heading = 0;
            for (int i = 0; i < lines.Length; i++)
            {
                float lh = LineHeight(ref lines[i], tw, i == 0, rows);
                if (Stop(lines[i].Kind) && heading++ == reveal)
                {
                    revealTop = total;
                    revealH = lh;
                }
                total += lh;
            }
            float vh = UiDraw.SnapDown(h);
            float most = Math.Max(0f, total - vh);
            if (revealTop >= 0f)
            {
                offset = Math.Min(offset, revealTop);
                offset = Math.Max(offset, revealTop + revealH - vh);
            }
            reveal = -1;
            offset = Math.Max(0f, Math.Min(offset, most));
            float labelW = rows ? Labels(lines, tw) : 0f;   // the bars' labels in one column, so every bar starts at one edge
            clip = s.AddClip(x, y, tw, vh, offset);
            s.Clips[clip].ContentHeight = total;
            list = s.AddList(heading, 0, heading, clip, -1);
            var inClip = new UiAt { Parent = -1, Clip = clip, Layer = at.Layer, Order = -1, Region = at.Region };
            float ly = 0f;
            heading = 0;
            for (int i = 0; i < lines.Length; i++)
            {
                ref BizManLine line = ref lines[i];
                float lh = LineHeight(ref line, tw, i == 0, rows);
                float top = ly + Gap(line.Kind, i == 0);
                switch (line.Kind)
                {
                    case BizManLineKind.Title:
                    case BizManLineKind.Heading:
                    {
                        const UiStyle style = UiStyle.SectionHeader;
                        float th = UiDraw.Snap(UiTextRules.LineHeight(style));
                        int face = UiDraw.Box(s, inClip, 0f, top - UiTokens.S1, tw, th + UiTokens.S2, UiRole.None, UiRole.None, UiTokens.RadiusMedium);
                        UiDraw.Text(s, inClip.In(face), 0f, top, tw, style, UiRole.Text, line.Words, UiFlags.Label | UiFlags.Truncate);
                        UiDraw.Node(s, inClip, 0f, top - UiTokens.S1, tw, th + UiTokens.S2, Heading, arg0 + heading, face, false, list, heading);
                        heading++;
                        break;
                    }
                    case BizManLineKind.Note:
                        UiDraw.Text(s, inClip, 0f, top, tw, UiStyle.Small, UiRole.Text2, line.Words, UiFlags.Label | UiFlags.Wrap, MostLines);
                        break;
                    default:
                    {
                        int shape = Shape(line.Words, out string label, out string value, out int score);
                        shape = rows ? shape : 0;
                        if (shape == 2)
                        {
                            UiDraw.GradeBar(s, inClip, 0f, top, tw, label, score, labelW);
                        }
                        else if (shape == 1)
                        {
                            UiDraw.InfoRow(s, inClip, 0f, top, tw, label, value, false, -1, 0, line.Tone == UiRole.None ? UiRole.Text : line.Tone);
                        }
                        else
                        {
                            UiDraw.Text(s, inClip, 0f, top, tw, UiStyle.Body, line.Tone == UiRole.None ? UiRole.Text : line.Tone, line.Words, UiFlags.Label | UiFlags.Wrap, MostLines);
                        }
                        break;
                    }
                }
                ly += lh;
            }
            UiDraw.ScrollThumb(s, at, x + w - UiTokens.S1, y, vh, offset, total);
            return offset;
        }

        private static bool Stop(BizManLineKind kind) => kind == BizManLineKind.Title || kind == BizManLineKind.Heading;

        // The label column the column's graded bars share.
        private static float Labels(BizManLine[] lines, float w)
        {
            int n = 0;
            for (int i = 0; i < lines.Length; i++)
            {
                n += lines[i].Kind == BizManLineKind.Line && Shape(lines[i].Words, out _, out _, out _) == 2 ? 1 : 0;
            }
            var labels = new string[n];
            n = 0;
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].Kind == BizManLineKind.Line && Shape(lines[i].Words, out string label, out _, out _) == 2)
                {
                    labels[n++] = label;
                }
            }
            return n > 0 ? UiDraw.LabelColumn(w, labels) : 0f;
        }

        // A line's shape: 1 a short label and its figure (a row), 2 a score out of 100 (a graded bar), 0 a sentence.
        private static int Shape(string words, out string label, out string value, out int score)
        {
            label = value = null;
            score = 0;
            int cut = string.IsNullOrEmpty(words) ? -1 : words.IndexOf(": ", StringComparison.Ordinal);
            if (cut <= 0 || cut > ShortPart || words.Length - cut - 2 > ShortPart || words.IndexOf(": ", cut + 2, StringComparison.Ordinal) >= 0)
            {
                return 0;
            }
            label = words.Substring(0, cut);
            value = words.Substring(cut + 2);
            System.Text.RegularExpressions.Match m = OutOf100.Match(value);
            if (m.Success && int.TryParse(m.Groups[1].Value, out score) && score <= 100)
            {
                return 2;
            }
            return 1;
        }

        // The room over a line: a heading starts a block, so it stands apart from the lines before it.
        private static float Gap(BizManLineKind kind, bool first) => first ? 0f : kind == BizManLineKind.Heading || kind == BizManLineKind.Title ? UiTokens.S5 : UiTokens.S1;

        // A column of scores reads its short lines as rows and bars; a page of effects keeps every line a sentence, all alike.
        private static float LineHeight(ref BizManLine line, float w, bool first, bool rows)
        {
            float gap = Gap(line.Kind, first);
            switch (line.Kind)
            {
                case BizManLineKind.Title:
                case BizManLineKind.Heading:
                    return gap + UiDraw.Snap(UiTextRules.LineHeight(UiStyle.SectionHeader)) + UiTokens.S3;
                case BizManLineKind.Note:
                    return gap + UiDraw.Snap(UiTextRules.WrappedHeight(line.Words ?? string.Empty, UiStyle.Small, w, MostLines));
                default:
                    return gap + (rows && Shape(line.Words, out _, out _, out _) > 0 ? UiTokens.PairRow
                        : UiDraw.Snap(UiTextRules.WrappedHeight(line.Words ?? string.Empty, UiStyle.Body, w, MostLines)));
            }
        }
    }
}
