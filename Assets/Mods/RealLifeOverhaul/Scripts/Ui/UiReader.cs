using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Ui
{
    /// <summary>What a block of a document is.</summary>
    public enum UiReaderKind : byte
    {
        Paragraph = 0,
        Heading,       // a section: it stands in the contents
        Subheading,
        Bullet,
        Numbered,
        Note,          // a quiet aside on a band, after its info glyph
        Picture,       // a picture on a slate tile with its caption under it
        Link           // a line that opens another page: cyan, a target
    }

    /// <summary>One block of a document: its kind, its words (bold runs between &lt;b&gt; and &lt;/b&gt;), and a picture's glyph or a link's page.</summary>
    public struct UiReaderBlock
    {
        public UiReaderKind Kind;
        public string Text;
        public UiIcon Icon;      // a picture's glyph, in its own colour on its tile, until the kit carries pictures of its own
        public int Target;       // a link's page, given back on its press
        public int Number;       // a numbered line's number
    }

    /// <summary>
    /// A long document read in one column at a reading measure: its title, its blocks, the facts it stands on and the pages
    /// it leads to beside it, and how far it has scrolled. Its anchors are its headings and links in their order: the
    /// d-pad and the stick step through them, and Lay writes back where each stands and the section in view, so the
    /// contents in the window's sidebar follow the reading.
    /// </summary>
    public sealed class UiReaderModel
    {
        public string Title;                           // the document's own name over its first block ("Construction company")
        public UiReaderBlock[] Blocks = new UiReaderBlock[0];
        public UiIcon Picture;                         // the page's own picture beside the column, its topic's glyph on a large tile; None for none
        public string PictureCaption;                  // what the page is about, in a sentence under it
        public string FactsTitle;                      // "In short"
        public UiPairModel[] Facts = new UiPairModel[0];
        public string RelatedTitle;                    // "See also"
        public string[] Related = new string[0];
        public float Offset;                           // px scrolled; Lay writes back what it took
        public int Reveal = -1;                        // an anchor to bring into view on the next layout, or -1

        // Written by Lay.
        public float ContentHeight, ViewHeight;
        public float[] AnchorTops = new float[0];      // each anchor's top in the column
        public int[] SectionAnchors = new int[0];      // each section's heading, as an anchor
        public int InView;                             // the section the top of the column is in
        public UiReaderBlock[] LaidBlocks;             // the blocks and the width the heights below were found for,
        public float LaidWidth;                        // kept until either changes
        public float[] Heights = new float[0];
    }

    /// <summary>
    /// A long document, a block of an app window's page: one column at a reading measure (60 to 75 letters), its title,
    /// headings, paragraphs, lists, notes on a band, pictures on a slate tile with their captions and links in the cyan,
    /// bold names in the strong cut; beside it the facts the page stands on as labels over figures, and the pages it leads
    /// to. The column scrolls in its frame: the wheel and Page Up and Down move it, and the d-pad and the stick step
    /// through its anchors (its headings and links), scrolling as they go. Its sections stand in the window's sidebar
    /// (<see cref="LayWithContents"/>: numbered, as a document's contents read, in the sidebar's place and look) and
    /// follow the reading (<see cref="UiReaderModel.InView"/>). For the Guide, the Newsstand's articles and the game's help
    /// pages. Commands: <see cref="AnchorCommand"/> (an anchor), <see cref="RelatedCommand"/> (a page beside the column)
    /// and <see cref="ContentsCommand"/> (a section). Only the blocks in view are laid out; their heights are
    /// kept until the width changes, so the Guide's longest page scrolls at a block's cost. Game-free.
    /// </summary>
    public static class UiReader
    {
        public const int AnchorCommand = 40, RelatedCommand = 41, ContentsCommand = 42;
        public const float ContentsRow = 44f;        // px: a section in the contents, its number and its name on one line or two
        public const float RailLeast = 320f;         // px: the least the aside beside the column takes
        public const float PictureHeight = 160f;     // px: a picture's tile in the column
        public const float LeadPicture = 200f;       // px: the page's own picture beside the column
        private const float FactWidth = 180f;        // px: the least a fact's label and figure take across
        private const float LinkHeight = 32f;        // px: a link on a line of its own
        private const byte RegionColumn = 1, RegionRail = 2;

        [ThreadStatic] private static List<bool> _bold;
        [ThreadStatic] private static List<UiReaderLine> _lines;
        [ThreadStatic] private static float _viewTop, _viewBottom;   // the column's view in its content, while its blocks are laid

        /// <summary>The reading column's width in a page's room: the measure, or less where the room is narrower.</summary>
        public static float Column(float w) => Math.Min(UiReaderRules.ColumnWidth(), UiDraw.SnapDown(w - RailLeast - UiTokens.S12));

        /// <summary>Lays the document into the room an app window's Shell gave its page, and writes back where its sections stand.</summary>
        public static void Lay(UiScene s, UiAt at, UiReaderModel m, float x, float y, float w, float h, IUiKeys keys)
        {
            _bold = _bold ?? new List<bool>(512);
            _lines = _lines ?? new List<UiReaderLine>(64);
            float cw = Column(w);
            Heights(m, cw);
            // The column at its measure from the page's left; the aside after one gutter takes the rest, its facts and links
            // in columns, so the room goes to what the page stands on rather than to longer lines or empty margins.
            ColumnBlocks(s, at.InRegion(RegionColumn), m, x, y, cw, h);
            float rx = x + cw + UiTokens.S12;
            Rail(s, at.InRegion(RegionRail), m, rx, y, x + w - rx);
        }

        /// <summary>
        /// The document in an app window that Shell drew with no sections of its own: its sections numbered down the
        /// sidebar's place on its band, the one being read chosen, and the document in the room beside them.
        /// </summary>
        public static void LayWithContents(UiScene s, UiAt at, UiReaderModel m, UiFrame frame, IUiKeys keys)
        {
            frame.Window(out float wx, out float wy, out float ww, out float wh);
            float top = wy + UiFrame.Strip;
            float x = wx + UiAppWindow.Sidebar + UiTokens.S5, y = top + UiTokens.S4;
            Lay(s, at, m, x, y, wx + ww - UiTokens.S5 - x, wy + wh - UiTokens.S4 - y, keys);
            UiDraw.Band(s, at, wx, top, UiAppWindow.Sidebar, wh - UiFrame.Strip);
            int column = s.NodeCount;
            Contents(s, at.InRegion(3), m, wx + UiTokens.S2, top + UiTokens.S3, UiAppWindow.Sidebar - UiTokens.S4, wh - UiFrame.Strip - UiTokens.S3 * 2f);
            int section = s.FindNode(ContentsCommand, m.InView);
            for (int i = 0; section >= 0 && i < column; i++)
            {
                if (s.Nodes[i].Command == AnchorCommand)
                {
                    s.Nodes[i].Left = section;   // Left from the column goes to the section being read
                }
            }
        }

        // The sections numbered in their frame, the one being read chosen; focus starts there, as a sidebar's shown section.
        private static void Contents(UiScene s, UiAt at, UiReaderModel m, float x, float y, float w, float h)
        {
            int count = m.SectionAnchors.Length;
            float contentH = count * (ContentsRow + UiTokens.S1);
            float vh = UiDraw.SnapDown(h);
            float offset = Math.Max(0f, Math.Min(m.InView * (ContentsRow + UiTokens.S1) - vh / 2f, contentH - vh));
            offset = UiDraw.SnapDown(offset);
            short clip = s.AddClip(x, y, w, vh, offset);
            s.Clips[clip].ContentHeight = contentH;
            s.Clips[clip].Fills = true;   // a short contents stands at the band's top, as a sidebar's sections do
            UiDraw.ScrollThumb(s, at, x + w + UiTokens.S1 / 2f, y, vh, offset, contentH);   // more sections below than the band holds: its thumb says so
            // Whole entries only: one cut by the band's foot would read as a row with no name.
            float pitch = ContentsRow + UiTokens.S1;
            int first = Math.Max(0, (int)Math.Ceiling(offset / pitch - 0.001f)), last = Math.Min(count - 1, (int)((offset + vh - ContentsRow) / pitch));
            short list = s.AddList(count, first, Math.Max(0, last - first + 1), clip, m.InView);
            var inClip = new UiAt { Parent = -1, Clip = clip, Layer = at.Layer, Order = -1, Region = at.Region };
            float numW = UiDraw.Snap(UiTextRules.Width("99", UiStyle.BodyStrong) + UiTokens.S2);
            for (int i = 0, k = 0; i < m.Blocks.Length && k <= last; i++)
            {
                if (m.Blocks[i].Kind != UiReaderKind.Heading)
                {
                    continue;
                }
                if (k >= first)
                {
                    bool chosen = k == m.InView;
                    float ey = k * (ContentsRow + UiTokens.S1);
                    int face = UiDraw.Box(s, inClip, 0f, ey, w, ContentsRow, chosen ? UiRole.RowSel : UiRole.None, UiRole.None, UiTokens.RadiusLarge, chosen ? UiFlags.Selected : UiFlags.None);
                    UiAt inside = inClip.In(face);
                    string number = (k + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
                    UiDraw.TextMiddle(s, inside, UiTokens.S3, ey, ContentsRow, numW, UiStyle.BodyStrong, chosen ? UiRole.Text : UiRole.AccentText, number, UiFlags.None);
                    string title = UiReaderRules.Plain(m.Blocks[i].Text, _bold);
                    float tw = w - UiTokens.S3 * 2f - numW;
                    if (UiTextRules.Fits(title, UiStyle.Nav, tw))
                    {
                        UiDraw.TextMiddle(s, inside, UiTokens.S3 + numW, ey, ContentsRow, tw, UiStyle.Nav, UiRole.Text, title, UiFlags.None);
                    }
                    else
                    {
                        // A long section's name on two smaller lines rather than cut, as the settings' groups take theirs.
                        float th = UiTextRules.WrappedHeight(title, UiStyle.Nav, tw, 2);
                        UiDraw.Text(s, inside, UiTokens.S3 + numW, ey + (ContentsRow - th) / 2f, tw, UiStyle.Nav, UiRole.Text, title, UiFlags.Wrap | UiFlags.Truncate | UiFlags.Middle, 2);
                    }
                    int node = UiDraw.Node(s, inClip, 0f, ey, w, ContentsRow, ContentsCommand, k, face, false, list, k);
                    if (chosen)
                    {
                        s.FocusFirst = node;   // the keys start on the section being read, never on the close
                    }
                }
                k++;
            }
        }

        // Each block's height at the column's width, kept while the blocks and the width stay the same.
        private static void Heights(UiReaderModel m, float cw)
        {
            if (ReferenceEquals(m.LaidBlocks, m.Blocks) && m.LaidWidth == cw && m.Heights.Length == m.Blocks.Length)
            {
                return;
            }
            if (m.Heights.Length != m.Blocks.Length)
            {
                m.Heights = new float[m.Blocks.Length];
            }
            int anchors = 0, sections = 0;
            for (int i = 0; i < m.Blocks.Length; i++)
            {
                m.Heights[i] = Height(ref m.Blocks[i], cw, i == 0 && string.IsNullOrEmpty(m.Title));
                anchors += m.Blocks[i].Kind == UiReaderKind.Heading || m.Blocks[i].Kind == UiReaderKind.Link ? 1 : 0;
                sections += m.Blocks[i].Kind == UiReaderKind.Heading ? 1 : 0;
            }
            if (m.AnchorTops.Length != anchors)
            {
                m.AnchorTops = new float[anchors];
            }
            if (m.SectionAnchors.Length != sections)
            {
                m.SectionAnchors = new int[sections];
            }
            m.LaidBlocks = m.Blocks;
            m.LaidWidth = cw;
        }

        /// <summary>A block's height at a column's width, with the room after it, on the grid so every block starts on it.</summary>
        public static float Height(ref UiReaderBlock b, float cw, bool first)
        {
            switch (b.Kind)
            {
                case UiReaderKind.Heading:
                    return (first ? 0f : UiTokens.S6) + Lines(b.Text, UiStyle.Number, cw) + UiTokens.S3;
                case UiReaderKind.Subheading:
                    return (first ? 0f : UiTokens.S3) + Lines(b.Text, UiStyle.SectionHeader, cw) + UiTokens.S2;
                case UiReaderKind.Bullet:
                case UiReaderKind.Numbered:
                    return Lines(b.Text, UiStyle.Body, cw - Indent(b.Kind)) + UiTokens.S2;
                case UiReaderKind.Note:
                    return UiDraw.Snap(UiTokens.S3 * 2f + RawLines(b.Text, UiStyle.Small, cw - UiTokens.S3 * 2f - UiTokens.Icon - UiTokens.S2)) + UiTokens.S4;
                case UiReaderKind.Picture:
                    return PictureHeight + (string.IsNullOrEmpty(b.Text) ? 0f : UiTokens.S2 + Lines(b.Text, UiStyle.Small, cw)) + UiTokens.S4;
                case UiReaderKind.Link:
                    return LinkHeight + UiTokens.S2;
                default:
                    return Lines(b.Text, UiStyle.Body, cw) + UiTokens.S3;
            }
        }

        private static float Indent(UiReaderKind kind) => kind == UiReaderKind.Numbered ? UiTokens.S6 : UiTokens.S5;

        // Lines' height of a block's words at a width, on the grid.
        private static float Lines(string text, UiStyle style, float width) => UiDraw.Snap(RawLines(text, style, width));

        private static float RawLines(string text, UiStyle style, float width)
        {
            _bold = _bold ?? new List<bool>(512);
            _lines = _lines ?? new List<UiReaderLine>(64);
            string plain = UiReaderRules.Plain(text, _bold);
            int n = UiReaderRules.Wrap(plain, _bold, style, width, _lines);
            return Math.Max(1, n) * UiTextRules.LineHeight(style);
        }

        // The column in its frame: the title, then the blocks in view, anchors a list the d-pad steps through.
        private static void ColumnBlocks(UiScene s, UiAt at, UiReaderModel m, float x, float y, float cw, float h)
        {
            float titleH = string.IsNullOrEmpty(m.Title) ? 0f : Lines(m.Title, UiStyle.Number, cw) + UiTokens.S4;
            float total = titleH;
            int anchor = 0, section = 0;
            for (int i = 0; i < m.Blocks.Length; i++)
            {
                UiReaderKind kind = m.Blocks[i].Kind;
                if (kind == UiReaderKind.Heading)
                {
                    m.SectionAnchors[section++] = anchor;
                }
                if (kind == UiReaderKind.Heading || kind == UiReaderKind.Link)
                {
                    m.AnchorTops[anchor++] = total + (kind == UiReaderKind.Heading && !(i == 0 && titleH <= 0f) ? UiTokens.S6 : 0f);
                }
                total += m.Heights[i];
            }
            float vh = UiDraw.SnapDown(h);
            m.ContentHeight = total;
            m.ViewHeight = vh;
            float most = Math.Max(0f, total - vh);
            float offset = Math.Max(0f, Math.Min(m.Offset, most));
            if (m.Reveal >= 0 && m.Reveal < m.AnchorTops.Length)
            {
                float top = m.AnchorTops[m.Reveal];
                if (top < offset || top + LinkHeight > offset + vh)
                {
                    offset = Math.Max(0f, Math.Min(top - UiTokens.S4, most));   // an anchor stepped to stands near the top, its words under it in view
                }
            }
            m.Reveal = -1;
            m.Offset = offset;
            m.InView = 0;
            for (int k = 0; k < m.SectionAnchors.Length; k++)
            {
                m.InView = m.AnchorTops[m.SectionAnchors[k]] <= offset + UiTokens.S8 ? k : m.InView;
            }
            short clip = s.AddClip(x, y, cw, vh, offset);
            s.Clips[clip].ContentHeight = total;
            UiDraw.ScrollThumb(s, at, x + cw + UiTokens.S4, y, vh, offset, total);
            _viewTop = offset;
            _viewBottom = offset + vh;
            var inClip = new UiAt { Parent = -1, Clip = clip, Layer = at.Layer, Order = -1, Region = at.Region };
            int firstAnchor = -1, shown = 0;
            short list = s.AddList(m.AnchorTops.Length, 0, 0, clip, -1);
            if (titleH > 0f && titleH > offset)
            {
                Words(s, inClip, m.Title, UiStyle.Number, UiRole.AccentText, UiRole.AccentText, 0f, 0f, cw);
            }
            float by = titleH;
            anchor = 0;
            for (int i = 0; i < m.Blocks.Length; i++)
            {
                ref UiReaderBlock b = ref m.Blocks[i];
                float bh = m.Heights[i];
                bool isAnchor = b.Kind == UiReaderKind.Heading || b.Kind == UiReaderKind.Link;
                if (by + bh > offset && by < offset + vh)
                {
                    int node = Block(s, inClip, ref b, i == 0 && titleH <= 0f, by, cw, isAnchor ? anchor : -1);
                    if (node >= 0)
                    {
                        s.Nodes[node].List = list;
                        s.Nodes[node].Index = anchor;
                        firstAnchor = firstAnchor < 0 ? anchor : firstAnchor;
                        shown++;
                    }
                }
                anchor += isAnchor ? 1 : 0;
                by += bh;
            }
            s.Lists[list].First = Math.Max(0, firstAnchor);
            s.Lists[list].Shown = shown;
        }

        // A block at its top in the column. Returns its anchor's target, or -1.
        private static int Block(UiScene s, UiAt at, ref UiReaderBlock b, bool first, float y, float cw, int anchor)
        {
            switch (b.Kind)
            {
                case UiReaderKind.Heading:
                {
                    float top = y + (first ? 0f : UiTokens.S6);
                    float hh = Lines(b.Text, UiStyle.Number, cw);
                    int face = UiDraw.Box(s, at, 0f, top, cw, hh, UiRole.None, UiRole.None, UiTokens.RadiusLarge);
                    Words(s, at.In(face), b.Text, UiStyle.Number, UiRole.Text, UiRole.Text, 0f, top, cw);
                    return UiDraw.Node(s, at, 0f, top, cw, Math.Max(hh, UiTokens.MinTarget), AnchorCommand, anchor, face);
                }
                case UiReaderKind.Subheading:
                    Words(s, at, b.Text, UiStyle.SectionHeader, UiRole.Text, UiRole.Text, 0f, y + (first ? 0f : UiTokens.S3), cw);
                    return -1;
                case UiReaderKind.Bullet:
                case UiReaderKind.Numbered:
                {
                    float indent = Indent(b.Kind);
                    float lh = UiTextRules.LineHeight(UiStyle.Body);
                    if (b.Kind == UiReaderKind.Bullet)
                    {
                        float dot = UiTokens.S1 + UiTokens.Hairline * 2f;
                        UiDraw.Circle(s, at, UiTokens.S1, y + (lh - dot) / 2f, dot, dot, UiRole.Muted, UiFlags.Middle | UiFlags.Share);
                    }
                    else
                    {
                        string number = b.Number.ToString(System.Globalization.CultureInfo.InvariantCulture) + ".";
                        UiDraw.Text(s, at, 0f, y, indent - UiTokens.S1, UiStyle.BodyStrong, UiRole.AccentText, number, UiFlags.Middle);
                    }
                    Words(s, at, b.Text, UiStyle.Body, UiRole.Text2, UiRole.Text, indent, y, cw - indent);
                    return -1;
                }
                case UiReaderKind.Note:
                {
                    float nh = UiDraw.Snap(UiTokens.S3 * 2f + RawLines(b.Text, UiStyle.Small, cw - UiTokens.S3 * 2f - UiTokens.Icon - UiTokens.S2));
                    int band = UiDraw.Box(s, at, 0f, y, cw, nh, UiRole.Band, UiRole.None, UiTokens.RadiusLarge);
                    UiAt inside = at.In(band);
                    UiDraw.Icon(s, inside, UiTokens.S3, y + UiTokens.S3, UiTokens.Icon, UiIcon.Info, UiRole.None, UiFlags.Middle);
                    float tx = UiTokens.S3 + UiTokens.Icon + UiTokens.S2;
                    Words(s, inside, b.Text, UiStyle.Small, UiRole.Text2, UiRole.Text, tx, y + UiTokens.S3 + (UiTokens.Icon - UiTextRules.LineHeight(UiStyle.Small)) / 2f, cw - tx - UiTokens.S3);
                    return -1;
                }
                case UiReaderKind.Picture:
                {
                    int tile = UiDraw.Box(s, at, 0f, y, cw, PictureHeight, UiRole.Tile, UiRole.None, UiTokens.RadiusLarge);
                    float g = UiTokens.S16;
                    UiDraw.Icon(s, at.In(tile), (cw - g) / 2f, y + (PictureHeight - g) / 2f, g, b.Icon == UiIcon.None ? UiIcon.Image : b.Icon, UiRole.None, UiFlags.Middle | UiFlags.Centre, true);
                    if (!string.IsNullOrEmpty(b.Text))
                    {
                        Words(s, at, b.Text, UiStyle.Small, UiRole.Muted, UiRole.Text2, 0f, y + PictureHeight + UiTokens.S2, cw);
                    }
                    return -1;
                }
                case UiReaderKind.Link:
                    UiDraw.LinkRow(s, at, 0f, y, cw, LinkHeight, b.Text, AnchorCommand, anchor, false);
                    return s.NodeCount - 1;
                default:
                    Words(s, at, b.Text, UiStyle.Body, UiRole.Text2, UiRole.Text, 0f, y, cw);
                    return -1;
            }
        }

        // A block's words as lines, each run of plain or bold letters a text of its own beside the last, the bold in the strong
        // cut and the brighter ink, so a name reads out of a sentence as the mod's texts mark it.
        private static void Words(UiScene s, UiAt at, string text, UiStyle style, UiRole ink, UiRole strongInk, float x, float y, float w)
        {
            string plain = UiReaderRules.Plain(text, _bold);
            int lines = UiReaderRules.Wrap(plain, _bold, style, w, _lines);
            float lh = UiTextRules.LineHeight(style);
            for (int l = 0; l < lines; l++)
            {
                if (at.Clip >= 0 && (y + (l + 1) * lh <= _viewTop || y + l * lh >= _viewBottom))
                {
                    continue;   // a line of a long block out of the column's view: only what shows is laid out
                }
                UiReaderLine line = _lines[l];
                float rx = x;
                int start = line.Start;
                while (start < line.End)
                {
                    bool bold = start < _bold.Count && _bold[start];
                    int end = start;
                    while (end < line.End && (end < _bold.Count && _bold[end]) == bold)
                    {
                        end++;
                    }
                    float rw = UiReaderRules.Width(plain, _bold, style, start, end);
                    UiStyle face = bold ? UiReaderRules.Strong(style) : style;
                    // Each run a breath wider than its letters, so the engine never wraps it; the next run starts at the letters' end.
                    UiDraw.Text(s, at, rx, y + l * lh, rw + 0.4f, face, bold ? strongInk : ink, plain.Substring(start, end - start), UiFlags.Middle | (rx > x ? UiFlags.Share : UiFlags.None));
                    rx += rw;
                    start = end;
                }
            }
        }

        // The facts the page stands on as labels over figures in as many columns as fit, and the pages it leads to.
        private static void Rail(UiScene s, UiAt at, UiReaderModel m, float x, float y, float w)
        {
            float cy = y;
            if (m.Picture != UiIcon.None)
            {
                // The page's own picture as a hero band, its topic's glyph on a large tile and what the page is about beside it.
                float tile = UiAppWindow.HeroTileSize;
                float bh = tile + UiTokens.S4 * 2f;
                int band = UiDraw.Box(s, at, x, cy, w, bh, UiRole.Band, UiRole.None, UiTokens.RadiusLarge);
                UiAt inside = at.In(band);
                int t = UiDraw.Tile(s, inside, x + UiTokens.S4, cy + UiTokens.S4, tile, UiRole.None, false);
                float g = UiTokens.S10;
                UiDraw.Icon(s, inside.In(t), x + UiTokens.S4 + (tile - g) / 2f, cy + UiTokens.S4 + (tile - g) / 2f, g, m.Picture, UiRole.None, UiFlags.Middle | UiFlags.Centre, true);
                if (!string.IsNullOrEmpty(m.PictureCaption))
                {
                    float tx = x + UiTokens.S4 + tile + UiTokens.S5, tw = x + w - UiTokens.S4 - tx;
                    float ch = UiTextRules.WrappedHeight(m.PictureCaption, UiStyle.Body, tw, 3);
                    UiDraw.Text(s, inside, tx, cy + (bh - ch) / 2f, tw, UiStyle.Body, UiRole.Text2, m.PictureCaption, UiFlags.Wrap | UiFlags.Truncate | UiFlags.Middle, 3);
                }
                cy += bh + UiTokens.S6;
            }
            int facts = m.Facts.Length;
            if (facts > 0)
            {
                UiDraw.Text(s, at, x, cy, w, UiStyle.SectionHeader, UiRole.Text, m.FactsTitle, UiFlags.Label | UiFlags.Truncate);
                cy += UiDraw.Snap(UiTextRules.LineHeight(UiStyle.SectionHeader)) + UiTokens.S3;
                int columns = Math.Max(1, Math.Min(2, (int)((w - UiTokens.S4 * 2f + UiTokens.S6) / (FactWidth + UiTokens.S6))));   // two across: a label over its figure reads down the aside
                int rows = (facts + columns - 1) / columns;
                float labelH = UiDraw.Snap(UiTextRules.LineHeight(UiStyle.SectionHeader)) + UiTokens.S1, valueH = UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Title));
                float cellH = labelH + valueH;
                float bh = UiTokens.S4 * 2f + rows * cellH + (rows - 1) * UiTokens.S3;
                UiDraw.Band(s, at, x, cy, w, bh);
                float colW = UiDraw.SnapDown((w - UiTokens.S4 * 2f - (columns - 1) * UiTokens.S6) / columns);
                for (int i = 0; i < facts; i++)
                {
                    float fx = x + UiTokens.S4 + (i % columns) * (colW + UiTokens.S6), fy = cy + UiTokens.S4 + (i / columns) * (cellH + UiTokens.S3);
                    UiDraw.Text(s, at, fx, fy, colW, UiStyle.SectionHeader, UiRole.Text2, m.Facts[i].Key, UiFlags.Label | UiFlags.Truncate);
                    UiDraw.Text(s, at, fx, fy + labelH, colW, UiStyle.Title, UiRole.Text, m.Facts[i].Value, UiFlags.Truncate);
                }
                cy += bh + UiTokens.S6;
            }
            if (m.Related.Length > 0)
            {
                UiDraw.Text(s, at, x, cy, w, UiStyle.SectionHeader, UiRole.Text, m.RelatedTitle, UiFlags.Label | UiFlags.Truncate);
                cy += UiDraw.Snap(UiTextRules.LineHeight(UiStyle.SectionHeader)) + UiTokens.S2;
                const int columns = 1;   // one page a line, as a list of pages reads
                float colW = UiDraw.SnapDown((w - (columns - 1) * UiTokens.S6) / columns);
                for (int i = 0; i < m.Related.Length; i++)
                {
                    float lx = x + (i % columns) * (colW + UiTokens.S6);
                    UiDraw.LinkRow(s, at, lx, cy + (i / columns) * (LinkHeight + UiTokens.S1), colW, LinkHeight, m.Related[i], RelatedCommand, i);
                    if (i == 0)
                    {
                        s.Nodes[s.NodeCount - 1].Up = s.FindNode(UiDraw.CloseCommand, 0);   // the aside's top: Up reaches the window's close, far across the strip
                    }
                    if (i == m.Related.Length - 1)
                    {
                        s.Nodes[s.NodeCount - 1].Down = s.NodeCount - 1;   // the list's end: Down stays in the aside
                    }
                }
            }
        }
    }
}
