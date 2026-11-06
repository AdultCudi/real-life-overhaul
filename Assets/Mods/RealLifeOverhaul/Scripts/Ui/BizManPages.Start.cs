using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// Starting a business on the kit: the name to give it and Randomise on one row, under it the kinds this building takes
    /// as a list with the game's search, and beside the list the chosen kind's detail (what it sells or who it hires, where
    /// the goods come from, what it needs to open, the diploma, the building) with Start business at its foot. The list and
    /// the detail each scroll inside their frames, and each of the detail's headings is a stop for the pad. Game-free.
    /// </summary>
    public static partial class BizManPages
    {
        private const float LeastKinds = 320f, MostKinds = 480f, LeastDetail = 280f;   // px: the list's room and the detail's least

        private static void StartPage(UiScene s, UiAt at, BizManModel m, float x, float y, float w, float bottom, IUiKeys keys)
        {
            float lx = x;
            if (!string.IsNullOrEmpty(m.NameLabel))
            {
                float lw = UiDraw.Snap(UiTextRules.Width(m.NameLabel, UiStyle.SectionHeader) + UiTokens.S1);
                UiDraw.TextMiddle(s, at, x, y, UiTokens.Control, lw, UiStyle.SectionHeader, UiRole.Text, m.NameLabel, UiFlags.Label);
                lx = x + lw + UiTokens.S4;
            }
            float fw = UiDraw.SnapDown(Math.Min(UiTokens.S16 * 6f, (x + w - lx) * 0.5f));
            UiDraw.TextField(s, at, lx, y, fw, m.NameText, m.NamePlaceholder, Type, FieldName, false, m.Typing);
            int random = Find(m.Actions, DoRandomName);
            if (random >= 0)
            {
                ref UiActionModel a = ref m.Actions[random];
                float bw = UiDraw.ButtonWidth(a.Kind, a.Label, a.Icon, a.Key, keys);
                UiDraw.Button(s, at, lx + fw + UiTokens.S3, y, bw, UiTokens.Control, a.Kind, a.Label, a.Icon, a.Key, keys, Action, a.Arg, a.Disabled);
            }
            float cy = y + UiTokens.Control + UiTokens.S5;
            float kw = UiDraw.SnapDown(Math.Max(LeastKinds, Math.Min(MostKinds, w * 0.44f)));
            kw = Math.Min(kw, UiDraw.SnapDown(w - LeastDetail - UiTokens.S6));
            if (m.StartKinds != null)
            {
                UiDataTable.Lay(s, at, m.StartKinds, x, cy, kw, bottom - cy, keys, StartKinds);
            }
            StartDetailPane(s, at, m, x + kw + UiTokens.S6, cy, w - kw - UiTokens.S6, bottom, keys);
        }

        // The chosen kind's pane: its head, its rows in a region that scrolls, and the actions at its foot with Start business
        // last at the right, in the row the buttons of the building stand at the left.
        private static void StartDetailPane(UiScene s, UiAt at, BizManModel m, float x, float y, float w, float bottom, IUiKeys keys)
        {
            float foot = bottom, left = x, right = x + w;
            for (int i = m.Actions.Length - 1; i >= 0; i--)
            {
                ref UiActionModel a = ref m.Actions[i];
                if (a.Arg == DoRandomName)
                {
                    continue;
                }
                float bw = UiDraw.ButtonWidth(a.Kind, a.Label, a.Icon, a.Key, keys);
                if (a.Arg == DoStartHere)
                {
                    right -= bw;
                    UiDraw.Button(s, at, right, bottom - UiTokens.Control, bw, UiTokens.Control, a.Kind, a.Label, a.Icon, a.Key, keys, Action, a.Arg, a.Disabled);
                    right -= UiTokens.S3;
                }
            }
            for (int i = 0; i < m.Actions.Length; i++)
            {
                ref UiActionModel a = ref m.Actions[i];
                if (a.Arg == DoRandomName || a.Arg == DoStartHere)
                {
                    continue;
                }
                float bw = UiDraw.ButtonWidth(a.Kind, a.Label, a.Icon, a.Key, keys);
                if (left + bw > right)
                {
                    break;   // a narrow window keeps the main action and what fits beside it
                }
                UiDraw.Button(s, at, left, bottom - UiTokens.Control, bw, UiTokens.Control, a.Kind, a.Label, a.Icon, a.Key, keys, Action, a.Arg, a.Disabled);
                left += bw + UiTokens.S3;
            }
            foot = bottom - UiTokens.Control - UiTokens.S4;
            BizManStart d = m.StartDetail;
            if (d == null)
            {
                m.StartClip = m.StartList = -1;
                m.StartOffset = 0f;
                UiDraw.EmptyState(s, at, x, y, w, foot - y, UiIcon.Store, m.StartEmptyTitle, m.StartEmptyBody, null, -1, keys);
                return;
            }
            float tile = UiTokens.S16 + UiTokens.S2;
            int box = UiDraw.Box(s, at, x, y, tile, tile, UiRole.Tile, UiRole.None, UiTokens.RadiusLarge);
            var card = new UiCatalogueCard { Picture = d.Picture, Aspect = d.Aspect, Icon = d.Icon };
            UiCatalogue.Picture(s, at.In(box), ref card, x, y, tile, tile, UiTokens.S8);
            float tx = x + tile + UiTokens.S4, tw = w - tile - UiTokens.S4;
            float kh = UiDraw.Snap(UiTextRules.LineHeight(UiStyle.SectionHeader)), nh = UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Display));
            float ty = y + UiDraw.SnapDown((tile - kh - UiTokens.S1 - nh) / 2f);
            UiDraw.Text(s, at, tx, ty, tw, UiStyle.SectionHeader, UiRole.Text2, d.Kicker, UiFlags.Label | UiFlags.Truncate);
            UiDraw.Text(s, at, tx, ty + kh + UiTokens.S1, tw, UiStyle.Display, UiRole.Text, d.Name, UiFlags.Truncate);
            float top = y + tile + UiTokens.S4;
            m.StartOffset = StartRows(s, at, d.Rows, x, top, w, foot - top, m.StartOffset, ref m.StartReveal, out m.StartClip, out m.StartList);
        }

        // The rows in a region that scrolls when they run past it, its thumb at its right edge; each heading is a stop for the pad.
        private static float StartRows(UiScene s, UiAt at, BizManStartRow[] rows, float x, float y, float w, float h, float offset, ref int reveal, out short clip, out short list)
        {
            float tw = w - UiTokens.S4;   // the thumb's lane
            float total = 0f, revealTop = -1f, revealH = 0f;
            int headings = 0;
            for (int i = 0; i < rows.Length; i++)
            {
                float rh;
                if (rows[i].Kind == BizManStartKind.Item && ItemRun(rows, i, tw, out int cols, out int n))
                {
                    rh = StartGap(BizManStartKind.Item, i == 0) + (n + cols - 1) / cols * UiTokens.PairRow;
                    total += rh;
                    i += n - 1;
                    continue;
                }
                rh = StartRowHeight(ref rows[i], tw, i == 0);
                if (rows[i].Kind == BizManStartKind.Heading && headings++ == reveal)
                {
                    revealTop = total;
                    revealH = rh;
                }
                total += rh;
            }
            float vh = UiDraw.SnapDown(h);
            if (revealTop >= 0f)
            {
                offset = Math.Min(offset, revealTop);
                offset = Math.Max(offset, revealTop + revealH - vh);
            }
            reveal = -1;
            offset = Math.Max(0f, Math.Min(offset, Math.Max(0f, total - vh)));
            clip = s.AddClip(x, y, tw, vh, offset);
            s.Clips[clip].ContentHeight = total;
            list = s.AddList(headings, 0, headings, clip, -1);
            var inClip = new UiAt { Parent = -1, Clip = clip, Layer = at.Layer, Order = -1, Region = at.Region };
            float ly = 0f;
            headings = 0;
            for (int i = 0; i < rows.Length; i++)
            {
                ref BizManStartRow r = ref rows[i];
                if (r.Kind == BizManStartKind.Item && ItemRun(rows, i, tw, out int cols, out int n))
                {
                    float gap = StartGap(BizManStartKind.Item, i == 0), colW = UiDraw.SnapDown((tw - UiTokens.S4 * (cols - 1)) / cols);
                    for (int k = 0; k < n; k++)
                    {
                        ItemAt(s, inClip, ref rows[i + k], (k % cols) * (colW + UiTokens.S4), ly + gap + k / cols * UiTokens.PairRow, colW);
                    }
                    ly += gap + (n + cols - 1) / cols * UiTokens.PairRow;
                    i += n - 1;
                    continue;
                }
                float rh = StartRowHeight(ref r, tw, i == 0);
                float top = ly + StartGap(r.Kind, i == 0);
                switch (r.Kind)
                {
                    case BizManStartKind.Heading:
                    {
                        float th = UiDraw.Snap(UiTextRules.LineHeight(UiStyle.SectionHeader));
                        int face = UiDraw.Box(s, inClip, 0f, top - UiTokens.S1, tw, th + UiTokens.S2, UiRole.None, UiRole.None, UiTokens.RadiusMedium);
                        UiDraw.Text(s, inClip.In(face), 0f, top, tw, UiStyle.SectionHeader, UiRole.Text, r.Label, UiFlags.Label | UiFlags.Truncate);
                        UiDraw.Node(s, inClip, 0f, top - UiTokens.S1, tw, th + UiTokens.S2, Heading, StartHeading + headings, face, false, list, headings);
                        headings++;
                        break;
                    }
                    case BizManStartKind.Fact:
                        UiDraw.InfoRow(s, inClip, 0f, top, tw, r.Label, r.Value, false, -1, 0, r.Tone == UiRole.None ? UiRole.Text : r.Tone);
                        break;
                    case BizManStartKind.Item:
                        ItemAt(s, inClip, ref r, 0f, top, tw);
                        break;
                    default:
                        UiDraw.Text(s, inClip, 0f, top, tw, UiStyle.Small, UiRole.Text2, r.Label, UiFlags.Label | UiFlags.Wrap, MostItem);
                        break;
                }
                ly += rh;
            }
            UiDraw.ScrollThumb(s, at, x + w - UiTokens.S1, y, vh, offset, total);
            return offset;
        }

        // A line of a list: its mark and its words in a column of a width, one line high where the words fit, wrapped where they do not.
        private static void ItemAt(UiScene s, UiAt inClip, ref BizManStartRow r, float x, float top, float w)
        {
            float tx = x;
            if (r.Mark != UiIcon.None)
            {
                UiDraw.Icon(s, inClip, x, top + UiDraw.SnapDown((UiTokens.PairRow - UiTokens.IconSmall) / 2f), UiTokens.IconSmall, r.Mark, r.Tone, UiFlags.Middle, true);
                tx += UiTokens.IconSmall + UiTokens.S2;
            }
            float words = UiDraw.Snap(UiTextRules.WrappedHeight(r.Label ?? string.Empty, UiStyle.Body, x + w - tx, MostItem));
            if (words <= UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Body)))
            {
                UiDraw.TextMiddle(s, inClip, tx, top, UiTokens.PairRow, x + w - tx, UiStyle.Body, InkOf(r.Tone), r.Label, UiFlags.Label | UiFlags.Truncate);
            }
            else
            {
                UiDraw.Text(s, inClip, tx, top, x + w - tx, UiStyle.Body, InkOf(r.Tone), r.Label, UiFlags.Label | UiFlags.Wrap, MostItem);
            }
        }

        // The lines of a list that follow each other and are short stand in columns where the pane is wide; false for a lone line or a long one, which stands alone.
        private static bool ItemRun(BizManStartRow[] rows, int from, float w, out int cols, out int n)
        {
            float widest = 0f;
            n = 0;
            while (from + n < rows.Length && rows[from + n].Kind == BizManStartKind.Item)
            {
                float mark = rows[from + n].Mark != UiIcon.None ? UiTokens.IconSmall + UiTokens.S2 : 0f;
                widest = Math.Max(widest, UiTextRules.Width(rows[from + n].Label ?? string.Empty, UiStyle.Body) + UiTokens.S1 + mark);
                n++;
            }
            cols = Math.Max(1, Math.Min(3, (int)((w + UiTokens.S4) / (UiDraw.Snap(widest) + UiTokens.S4))));
            return n > 1 && cols > 1;
        }

        private const int MostItem = 4;                        // lines a long line may take before it is cut
        public const int StartHeading = 3000;                  // a heading's argument from here, so no other column's stop is mistaken for it

        private static UiRole InkOf(UiRole tone) => tone == UiRole.None || tone == UiRole.Ok ? UiRole.Text : tone == UiRole.Warn ? UiRole.WarnText : tone == UiRole.Danger ? UiRole.DangerText : tone;

        private static float StartGap(BizManStartKind kind, bool first) => first ? 0f : kind == BizManStartKind.Heading ? UiTokens.S5 : kind == BizManStartKind.Note ? UiTokens.S2 : UiTokens.S1;

        private static float StartRowHeight(ref BizManStartRow r, float w, bool first)
        {
            float gap = StartGap(r.Kind, first);
            switch (r.Kind)
            {
                case BizManStartKind.Heading:
                    return gap + UiDraw.Snap(UiTextRules.LineHeight(UiStyle.SectionHeader)) + UiTokens.S3;
                case BizManStartKind.Fact:
                    return gap + UiTokens.PairRow;
                case BizManStartKind.Item:
                {
                    float room = w - (r.Mark != UiIcon.None ? UiTokens.IconSmall + UiTokens.S2 : 0f);
                    return gap + Math.Max(UiTokens.PairRow, UiDraw.Snap(UiTextRules.WrappedHeight(r.Label ?? string.Empty, UiStyle.Body, room, MostItem)));
                }
                default:
                    return gap + UiDraw.Snap(UiTextRules.WrappedHeight(r.Label ?? string.Empty, UiStyle.Small, w, MostItem));
            }
        }
    }
}
