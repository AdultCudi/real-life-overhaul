using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Ui
{
    /// <summary>A story of the edition: its headline, a few lines of it, and its byline or its place.</summary>
    public struct UiEditionStory
    {
        public string Headline;
        public string Body;
        public string Byline;    // "Midtown", "By the business desk"; null for none
    }

    /// <summary>A section of the edition (City, Business, Around town): its glyph, its label and its stories.</summary>
    public struct UiEditionSection
    {
        public UiIcon Icon;
        public string Label;
        public UiEditionStory[] Stories;
    }

    /// <summary>A small notice at the edition's foot: its glyph and its words, with a figure at its right.</summary>
    public struct UiEditionNotice
    {
        public UiIcon Icon;
        public string Text;
        public string Value;
    }

    /// <summary>
    /// The city's newspaper as a page: the lead story as a hero (its picture tile, its section, its headline, its
    /// standfirst), the markets' figures with their lines and arrows, the stories in balanced columns under their
    /// sections' bands, and the small notices.
    /// </summary>
    public sealed class UiEditionModel
    {
        public UiIcon LeadIcon = UiIcon.Newspaper;
        public UiRole LeadTint;
        public string LeadKicker;                      // the lead's section: "City"
        public string LeadHeadline;
        public string LeadStandfirst;
        public UiFigureModel[] Markets = new UiFigureModel[0];
        public UiEditionSection[] Sections = new UiEditionSection[0];
        public string NoticesTitle;                    // "Notices"
        public UiEditionNotice[] Notices = new UiEditionNotice[0];
        public float Offset;
        public string QuietLine;                       // said under the stories on a day with few: what fills the paper and when
    }

    /// <summary>
    /// The city's newspaper as a page of an app window, in the kit's own look (never a serif or a paper's cream): the
    /// paper's name and date stand in the window's title strip; the lead story as a hero on a band (its picture on a large
    /// tile, its section, its headline large, its standfirst) beside the markets' figures with their arrows and lines; then
    /// the stories in two or three columns under their sections' bands, each story whole in one column and the columns
    /// balanced, and the small notices as rows at the end. On a quiet day a sentence says what fills the paper and when.
    /// Stories are targets that open them to read. For the mod's City Ledger (Ledger/LedgerPage) and the Newsstand.
    /// Commands: <see cref="StoryCommand"/> (section * 16 + story) and <see cref="LeadCommand"/>. Game-free.
    /// Cost: a layout a change; the columns are found with a few passes over the stories' heights.
    /// </summary>
    public static class UiEdition
    {
        public const int StoryCommand = 90, LeadCommand = 91;
        public const float LeadTile = 112f;          // px: the lead story's picture
        public const float MarketsShare = 0.34f;     // of the width, the markets beside the lead
        public const float WideColumns = 1200f;      // px of room from which the stories run in three columns, not two
        private const float BandHeight = 32f;        // px: a section's band over its stories
        private const int HeadlineLines = 3, BodyLines = 4, LeadLines = 2;

        /// <summary>A unit of the columns: a story of a section, or the notices together.</summary>
        public struct Unit
        {
            public int Section, Story;   // the notices are Section -1
            public float Height;         // without a band over it
        }

        [ThreadStatic] private static List<Unit> _units;

        /// <summary>Lays the edition into the room an app window's Shell gave its page.</summary>
        public static void Lay(UiScene s, UiAt at, UiEditionModel m, float x, float y, float w, float h, IUiKeys keys)
        {
            float mw = UiDraw.SnapDown(w * MarketsShare);
            float lw = m.Markets.Length > 0 ? w - mw - UiTokens.S6 : w;
            float lh = Math.Max(LeadHeight(m, lw), UiDraw.Snap(MarketsHeight(m)));
            Lead(s, at, m, x, y, lw, lh);
            if (m.Markets.Length > 0)
            {
                Markets(s, at, m, x + lw + UiTokens.S6, y, mw, lh);
            }
            float top = y + lh + UiTokens.S5;
            Columns(s, at, m, x, top, w, UiDraw.SnapDown(y + h - top));
        }

        private static float LeadHeight(UiEditionModel m, float w)
        {
            float tw = w - UiTokens.S5 * 2f - LeadTile - UiTokens.S5;
            float words = UiDraw.Snap(UiTextRules.LineHeight(UiStyle.SectionHeader)) + UiTokens.S1
                          + UiDraw.Snap(UiTextRules.WrappedHeight(m.LeadHeadline, UiStyle.Display, tw, LeadLines)) + UiTokens.S2
                          + (string.IsNullOrEmpty(m.LeadStandfirst) ? 0f : UiDraw.Snap(UiTextRules.WrappedHeight(m.LeadStandfirst, UiStyle.Body, tw, LeadLines)));
            return UiDraw.Snap(UiTokens.S5 * 2f + Math.Max(LeadTile, words));
        }

        // The lead story on its band: its picture on a large tile, its section, its headline in the largest words, its standfirst.
        private static void Lead(UiScene s, UiAt at, UiEditionModel m, float x, float y, float w, float h)
        {
            int band = UiDraw.Box(s, at, x, y, w, h, UiRole.Band, UiRole.None, UiTokens.RadiusLarge);
            UiAt inside = at.In(band);
            float tx = x + UiTokens.S5, ty = y + UiTokens.S5;
            int tile = UiDraw.Tile(s, inside, tx, ty, LeadTile, m.LeadTint, false);
            float g = UiTokens.S12;
            UiDraw.Icon(s, inside.In(tile), tx + (LeadTile - g) / 2f, ty + (LeadTile - g) / 2f, g, m.LeadIcon, m.LeadTint, UiFlags.Middle | UiFlags.Centre, true);
            float ix = tx + LeadTile + UiTokens.S5, iw = x + w - UiTokens.S5 - ix;
            float cy = ty;
            UiDraw.Text(s, inside, ix, cy, iw, UiStyle.SectionHeader, UiRole.Text2, m.LeadKicker, UiFlags.Label | UiFlags.Truncate);
            cy += UiDraw.Snap(UiTextRules.LineHeight(UiStyle.SectionHeader)) + UiTokens.S1;
            float hh = UiDraw.Snap(UiTextRules.WrappedHeight(m.LeadHeadline, UiStyle.Display, iw, LeadLines));
            UiDraw.Text(s, inside, ix, cy, iw, UiStyle.Display, UiRole.Text, m.LeadHeadline, UiFlags.Wrap | UiFlags.Truncate, LeadLines);
            cy += hh + UiTokens.S2;
            if (!string.IsNullOrEmpty(m.LeadStandfirst))
            {
                UiDraw.Text(s, inside, ix, cy, iw, UiStyle.Body, UiRole.Text2, m.LeadStandfirst, UiFlags.Wrap | UiFlags.Truncate, LeadLines);
            }
            UiDraw.Node(s, at, x, y, w, h, LeadCommand, 0, band);
        }

        private const float MarketRow = 32f;         // px: a market's figure, its change and its line on one row
        private const float MarketLine = 72f;        // px: a market's line across its last days

        private static float MarketsHeight(UiEditionModel m) => m.Markets.Length == 0 ? 0f : UiTokens.S3 * 2f + Math.Min(4, m.Markets.Length) * MarketRow;

        // The markets on a band as tall as the lead, a row each: its name, its figure, its change's arrow and words in good's
        // or bad's colour, and its line over the last days in the same colour.
        private static void Markets(UiScene s, UiAt at, UiEditionModel m, float x, float y, float w, float h)
        {
            UiDraw.Band(s, at, x, y, w, h);
            int count = Math.Min(4, m.Markets.Length);
            float ry = y + UiDraw.SnapDown((h - count * MarketRow) / 2f);
            float ix = x + UiTokens.S4, iw = w - UiTokens.S4 * 2f;
            for (int i = 0; i < count; i++, ry += MarketRow)
            {
                ref UiFigureModel f = ref m.Markets[i];
                bool changed = !string.IsNullOrEmpty(f.Change);
                UiRole tone = !changed ? UiRole.Accent : f.Good ? UiRole.OkText : UiRole.DangerText;
                float right = ix + iw;
                bool line = UiDraw.Drawn(f.Trend);
                float lineX = right - MarketLine;
                float cw = changed ? UiDraw.Snap(UiTextRules.Width(f.Change, UiStyle.SmallStrong) + UiTokens.S1) : 0f;
                float changeX = (line ? lineX - UiTokens.S3 : right) - cw;
                float arrowX = changeX - UiTokens.S1 - UiTokens.IconSmall;
                float vw = UiDraw.Snap(UiTextRules.Width(f.Value, UiStyle.BodyStrong) + UiTokens.S1);
                float valueX = (changed ? arrowX - UiTokens.S3 : changeX) - vw;
                UiDraw.TextMiddle(s, at, ix, ry, MarketRow, valueX - UiTokens.S3 - ix, UiStyle.Body, UiRole.Text2, f.Label, UiFlags.Truncate);
                UiDraw.TextMiddle(s, at, valueX, ry, MarketRow, vw, UiStyle.BodyStrong, UiRole.Text, f.Value, UiFlags.Right);
                if (changed)
                {
                    // The arrow by the change's sign, its colour by good or bad for the player, the line under in the same.
                    UiDraw.Icon(s, at, arrowX, ry + (MarketRow - UiTokens.IconSmall) / 2f, UiTokens.IconSmall, UiModels.Rises(ref f) ? UiIcon.TrendUp : UiIcon.TrendDown, tone,
                        UiFlags.Middle | UiFlags.Share);
                    UiDraw.TextMiddle(s, at, changeX, ry, MarketRow, cw, UiStyle.SmallStrong, tone, f.Change, UiFlags.Right);
                }
                if (line)
                {
                    UiDraw.Spark(s, at, lineX, ry + UiTokens.S2, MarketLine, MarketRow - UiTokens.S4, f.Trend, !changed ? UiRole.Accent : f.Good ? UiRole.Ok : UiRole.Danger);
                }
            }
        }

        /// <summary>A story's height in a column's width: its headline, a few lines of it and its byline, the gap under it.</summary>
        public static float StoryHeight(ref UiEditionStory story, float cw)
        {
            float tw = cw - UiTokens.S4;
            float h = UiTokens.S1 + UiDraw.Snap(UiTextRules.WrappedHeight(story.Headline, UiStyle.Title, tw, HeadlineLines));
            if (!string.IsNullOrEmpty(story.Body))
            {
                h += UiTokens.S1 + UiDraw.Snap(UiTextRules.WrappedHeight(story.Body, UiStyle.Body, tw, BodyLines));
            }
            if (!string.IsNullOrEmpty(story.Byline))
            {
                h += UiTokens.S1 + UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Small));
            }
            return h + UiTokens.S5;
        }

        private static float NoticesHeight(UiEditionModel m) => m.Notices.Length * UiTokens.PairRow + UiTokens.S3;

        /// <summary>How many columns the stories run in at a width.</summary>
        public static int ColumnCount(float w) => w >= WideColumns ? 3 : 2;

        /// <summary>
        /// The stories and the notices placed into columns in their order, each whole in one, the columns as near one height
        /// as the order allows (the least tallest column a greedy fill can keep to, found by halving): each unit's column in
        /// <paramref name="column"/>, and each column's height (a band counted over a column's first story and over a new
        /// section) in <paramref name="heights"/>.
        /// </summary>
        public static void Balance(UiEditionModel m, float cw, int columns, List<Unit> units, int[] column, float[] heights)
        {
            units.Clear();
            for (int sct = 0; sct < m.Sections.Length; sct++)
            {
                for (int st = 0; m.Sections[sct].Stories != null && st < m.Sections[sct].Stories.Length; st++)
                {
                    units.Add(new Unit { Section = sct, Story = st, Height = StoryHeight(ref m.Sections[sct].Stories[st], cw) });
                }
            }
            if (m.Notices.Length > 0)
            {
                units.Add(new Unit { Section = -1, Story = 0, Height = NoticesHeight(m) });
            }
            float lo = 0f, hi = 0f;
            for (int i = 0; i < units.Count; i++)
            {
                bool starts = i == 0 || units[i].Section != units[i - 1].Section;
                lo = starts ? Math.Max(lo, SectionHeight(units, i)) : lo;
                hi += units[i].Height + (starts ? BandHeight : 0f);
            }
            for (int pass = 0; pass < 24 && hi - lo > 1f; pass++)
            {
                float mid = (lo + hi) / 2f;
                if (Fill(units, mid, columns, column, heights) <= columns)
                {
                    hi = mid;
                }
                else
                {
                    lo = mid;
                }
            }
            // The tallest column is found; a fill that gives each column its share of what is left keeps the last from standing short.
            if (!Even(units, hi, columns, column, heights))
            {
                Fill(units, hi, columns, column, heights);
            }
        }

        // Each column filled towards an even share of the height still to place, a whole section at a time, never past the
        // tallest a fill must keep to; false when the last column would run past it (the greedy fill then stands).
        private static bool Even(List<Unit> units, float most, int columns, int[] column, float[] heights)
        {
            float left = 0f;
            for (int i = 0; i < units.Count; i++)
            {
                left += units[i].Height + (i == 0 || units[i].Section != units[i - 1].Section ? BandHeight : 0f);
            }
            for (int c = 0; c < heights.Length; c++)
            {
                heights[c] = 0f;
            }
            int col = 0;
            for (int i = 0; i < units.Count; i++)
            {
                bool starts = i == 0 || units[i].Section != units[i - 1].Section;
                if (starts && heights[col] > 0f && col < columns - 1)
                {
                    float share = left / Math.Max(1, columns - col);
                    float section = SectionHeight(units, i);
                    if (heights[col] + section > most || heights[col] + section / 2f > share)
                    {
                        left -= heights[col];
                        col++;
                    }
                }
                column[i] = col;
                heights[col] += (starts ? BandHeight : 0f) + units[i].Height;
            }
            return heights[columns - 1] <= most + 0.5f;
        }

        // A greedy fill of the units in order under a tallest column, a column breaking only where a section begins, so each
        // section stands once, whole, under its one band: how many columns it takes.
        private static int Fill(List<Unit> units, float most, int columns, int[] column, float[] heights)
        {
            for (int c = 0; c < heights.Length; c++)
            {
                heights[c] = 0f;
            }
            int col = 0;
            for (int i = 0; i < units.Count; i++)
            {
                bool starts = i == 0 || units[i].Section != units[i - 1].Section;
                float add = (starts ? BandHeight : 0f) + units[i].Height;
                if (starts && heights[Math.Min(col, heights.Length - 1)] > 0f && heights[Math.Min(col, heights.Length - 1)] + SectionHeight(units, i) > most)
                {
                    col++;
                }
                int into = Math.Min(col, heights.Length - 1);
                column[i] = into;
                heights[into] += add;
            }
            return col + 1;
        }

        // A section's height from its first unit: its band and every story of it.
        private static float SectionHeight(List<Unit> units, int first)
        {
            float h = BandHeight;
            for (int i = first; i < units.Count && units[i].Section == units[first].Section; i++)
            {
                h += units[i].Height;
            }
            return h;
        }

        private static void Columns(UiScene s, UiAt at, UiEditionModel m, float x, float y, float w, float vh)
        {
            _units = _units ?? new List<Unit>(32);
            int columns = ColumnCount(w);
            float cw = UiDraw.SnapDown((w - UiTokens.S6 * (columns - 1)) / columns);
            int stories = 0;
            for (int i = 0; i < m.Sections.Length; i++)
            {
                stories += m.Sections[i].Stories?.Length ?? 0;
            }
            var column = new int[stories + 1];
            var heights = new float[columns];
            Balance(m, cw, columns, _units, column, heights);
            float tallest = 0f;
            for (int c = 0; c < columns; c++)
            {
                tallest = Math.Max(tallest, heights[c]);
            }
            float quietH = string.IsNullOrEmpty(m.QuietLine) ? 0f : UiTokens.S2 + UiTokens.ControlSmall + UiTokens.S4;
            float contentH = tallest + quietH;
            float offset = Math.Max(0f, Math.Min(m.Offset, contentH - vh));
            m.Offset = offset;
            short clip = s.AddClip(x, y, w, vh, offset);
            s.Clips[clip].ContentHeight = contentH;
            s.Clips[clip].Fills = true;   // a quiet day's few stories stand at the top; the room under them is the paper's own
            UiDraw.ScrollThumb(s, at, x + w + UiTokens.S1, y, vh, offset, contentH);
            var inClip = new UiAt { Parent = -1, Clip = clip, Layer = at.Layer, Order = -1, Region = at.Region };
            var cy = new float[columns];
            var lastSection = new int[columns];
            var lastStory = new int[columns];
            for (int c = 0; c < columns; c++)
            {
                lastSection[c] = -2;
                lastStory[c] = -1;
            }
            for (int i = 0; i < _units.Count; i++)
            {
                Unit u = _units[i];
                int c = column[i];
                float cx = c * (cw + UiTokens.S6);
                if (cy[c] <= 0f || u.Section != lastSection[c])
                {
                    if (u.Section >= 0)
                    {
                        Band(s, inClip, m.Sections[u.Section].Icon, m.Sections[u.Section].Label, cx, cy[c], cw);
                    }
                    else
                    {
                        Band(s, inClip, UiIcon.Bell, m.NoticesTitle, cx, cy[c], cw);
                    }
                    cy[c] += BandHeight;
                }
                if (u.Section >= 0)
                {
                    int node = Story(s, inClip.Arriving(i), ref m.Sections[u.Section].Stories[u.Story], u.Section * 16 + u.Story, cx, cy[c], cw, u.Height);
                    // The d-pad walks a column to its foot, and Left and Right cross between columns.
                    if (lastStory[c] >= 0)
                    {
                        s.Nodes[lastStory[c]].Down = node;
                        s.Nodes[node].Up = lastStory[c];
                    }
                    lastStory[c] = node;
                }
                else
                {
                    Notices(s, inClip, m, cx, cy[c], cw);
                }
                cy[c] += u.Height;
                lastSection[c] = u.Section;
            }
            for (int c = 0; c < columns; c++)
            {
                if (lastStory[c] >= 0)
                {
                    s.Nodes[lastStory[c]].Down = lastStory[c];   // a column's last story: Down stays in its column
                }
            }
            if (quietH > 0f)
            {
                // What fills the paper on a quiet day, under its few stories on their text edge: its glyph's tile and the sentence.
                float qy = tallest + UiTokens.S2;
                int t = UiDraw.Tile(s, inClip, UiTokens.S2, qy, UiTokens.ControlSmall, UiRole.None, false);
                UiDraw.Icon(s, inClip.In(t), UiTokens.S2 + (UiTokens.ControlSmall - UiTokens.Icon) / 2f, qy + (UiTokens.ControlSmall - UiTokens.Icon) / 2f, UiTokens.Icon, UiIcon.Newspaper,
                    UiRole.None, UiFlags.Middle | UiFlags.Centre, true);
                float tx = UiTokens.S2 + UiTokens.ControlSmall + UiTokens.S3;
                UiDraw.TextMiddle(s, inClip, tx, qy, UiTokens.ControlSmall, w - tx, UiStyle.Body, UiRole.Text2, m.QuietLine, UiFlags.Label | UiFlags.Truncate);
            }
        }

        // A section's band: its glyph and its name in capitals, a column wide.
        private static void Band(UiScene s, UiAt at, UiIcon icon, string label, float x, float y, float w)
        {
            float bh = BandHeight - UiTokens.S2;
            int band = UiDraw.Box(s, at, x, y, w, bh, UiRole.Band, UiRole.None, UiTokens.RadiusLarge);
            UiAt inside = at.In(band);
            UiDraw.Icon(s, inside, x + UiTokens.S2, y + (bh - UiTokens.IconSmall) / 2f, UiTokens.IconSmall, icon, UiRole.None, UiFlags.Middle);
            UiDraw.TextMiddle(s, inside, x + UiTokens.S2 + UiTokens.IconSmall + UiTokens.S2, y, bh, w - UiTokens.S6 - UiTokens.IconSmall, UiStyle.SectionHeader, UiRole.Text, label,
                UiFlags.Label | UiFlags.Truncate);
        }

        // A story: its headline, a few lines of it ending at a whole word, its byline; a target that opens it to read.
        private static int Story(UiScene s, UiAt at, ref UiEditionStory story, int arg, float x, float y, float w, float h)
        {
            int face = UiDraw.Box(s, at, x, y, w, h - UiTokens.S3, UiRole.None, UiRole.None, UiTokens.RadiusLarge);
            UiAt inside = at.In(face);
            float ix = x + UiTokens.S2, iw = w - UiTokens.S4, cy = y + UiTokens.S1;
            float hh = UiDraw.Snap(UiTextRules.WrappedHeight(story.Headline, UiStyle.Title, iw, HeadlineLines));
            UiDraw.Text(s, inside, ix, cy, iw, UiStyle.Title, UiRole.Text, story.Headline, UiFlags.Wrap | UiFlags.Truncate, HeadlineLines);
            cy += hh;
            if (!string.IsNullOrEmpty(story.Body))
            {
                cy += UiTokens.S1;
                float bh = UiDraw.Snap(UiTextRules.WrappedHeight(story.Body, UiStyle.Body, iw, BodyLines));
                UiDraw.Text(s, inside, ix, cy, iw, UiStyle.Body, UiRole.Text2, story.Body, UiFlags.Wrap | UiFlags.Truncate, BodyLines);
                cy += bh;
            }
            if (!string.IsNullOrEmpty(story.Byline))
            {
                UiDraw.Text(s, inside, ix, cy + UiTokens.S1, iw, UiStyle.Small, UiRole.Muted, story.Byline, UiFlags.Truncate);
            }
            return UiDraw.Node(s, at, x, y, w, h - UiTokens.S3, StoryCommand, arg, face);
        }

        // The small notices as rows: a glyph, the words and a figure at the right.
        private static void Notices(UiScene s, UiAt at, UiEditionModel m, float x, float y, float w)
        {
            for (int i = 0; i < m.Notices.Length; i++)
            {
                float ry = y + i * UiTokens.PairRow;
                UiDraw.Icon(s, at, x + UiTokens.S2, ry + (UiTokens.PairRow - UiTokens.IconSmall) / 2f, UiTokens.IconSmall, m.Notices[i].Icon, UiRole.None, UiFlags.Middle);
                float vw = string.IsNullOrEmpty(m.Notices[i].Value) ? 0f : UiDraw.Snap(UiTextRules.Width(m.Notices[i].Value, UiStyle.BodyStrong) + UiTokens.S1);
                float tx = x + UiTokens.S2 + UiTokens.IconSmall + UiTokens.S2;
                UiDraw.TextMiddle(s, at, tx, ry, UiTokens.PairRow, x + w - UiTokens.S2 - tx - (vw > 0f ? vw + UiTokens.S3 : 0f), UiStyle.Body, UiRole.Text2, m.Notices[i].Text, UiFlags.Truncate);
                if (vw > 0f)
                {
                    UiDraw.TextMiddle(s, at, x + w - UiTokens.S2 - vw, ry, UiTokens.PairRow, vw, UiStyle.BodyStrong, UiRole.Text, m.Notices[i].Value, UiFlags.Right);
                }
            }
        }
    }
}
