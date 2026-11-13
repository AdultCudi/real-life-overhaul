using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// Starting a business on the kit: the name to give it and Randomise on one row, under it the kinds this building takes
    /// as a list with the game's search, and beside the list the chosen kind's detail (what the game says it is, what it sells
    /// or who it hires, where the goods come from, what it needs to open with a link to each requirement's help page, the diploma,
    /// what it costs) with Start business at its foot. Where the pane is wide the building stands in a column of its own at the
    /// pane's right: its photo, its facts with the size class, and its lease; where it is not, those rows follow the detail's.
    /// The list and the detail each scroll inside their frames, and each of the detail's headings, links and help buttons is a
    /// stop for the pad. Game-free.
    /// </summary>
    public static partial class BizManPages
    {
        private const float LeastKinds = 320f, MostKinds = 480f, LeastDetail = 280f;   // px: the list's room and the detail's least
        private const float LeastPhoto = UiTokens.S16 * 3f;                              // px: the building's photo at its smallest in the side column

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
        // last at the right, in the row the buttons of the building stand at the left. The building's column stands at the right of
        // both while the pane is wide enough to hold it and its rows whole.
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
            bool hasSide = m.StartSide.Length > 0 || !string.IsNullOrEmpty(m.StartPhoto);
            float sideW = hasSide ? SideWidth(w) : 0f;
            bool two = hasSide && BizManStartRules.TwoColumns(w, foot - y, SideNeeds(m, sideW));
            float mw = two ? w - sideW - UiTokens.S6 : w;
            if (two)
            {
                SideColumn(s, at, m, x + mw + UiTokens.S6, y, sideW, foot);
            }
            BizManStart d = m.StartDetail;
            if (d == null)
            {
                m.StartClip = m.StartList = -1;
                m.StartOffset = 0f;
                UiDraw.EmptyState(s, at, x, y, mw, foot - y, UiIcon.Store, m.StartEmptyTitle, m.StartEmptyBody, null, -1, keys);
                return;
            }
            float tile = UiTokens.S16 + UiTokens.S2;
            int box = UiDraw.Box(s, at, x, y, tile, tile, UiRole.Tile, UiRole.None, UiTokens.RadiusLarge);
            var card = new UiCatalogueCard { Picture = d.Picture, Aspect = d.Aspect, Icon = d.Icon };
            UiCatalogue.Picture(s, at.In(box), ref card, x, y, tile, tile, UiTokens.S8);
            float tx = x + tile + UiTokens.S4, tw = mw - tile - UiTokens.S4;
            float kh = UiDraw.Snap(UiTextRules.LineHeight(UiStyle.SectionHeader)), nh = UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Display));
            float ty = y + UiDraw.SnapDown((tile - kh - UiTokens.S1 - nh) / 2f);
            UiDraw.Text(s, at, tx, ty, tw, UiStyle.SectionHeader, UiRole.Text2, d.Kicker, UiFlags.Label | UiFlags.Truncate);
            UiDraw.Text(s, at, tx, ty + kh + UiTokens.S1, tw, UiStyle.Display, UiRole.Text, d.Name, UiFlags.Truncate);
            float top = y + tile + UiTokens.S4;
            // Narrow: the building's rows follow the kind's in the one scrolling region, so nothing of the game's left side is lost to a small window.
            BizManStartRow[] rows = two || m.StartSide.Length == 0 ? d.Rows : Joined(d.Rows, m.StartSide);
            m.StartOffset = StartRows(s, at, rows, x, top, mw, foot - top, m.StartOffset, ref m.StartReveal, out m.StartClip, out m.StartList, keys);
        }

        private static BizManStartRow[] Joined(BizManStartRow[] first, BizManStartRow[] second)
        {
            var all = new BizManStartRow[first.Length + second.Length];
            Array.Copy(first, all, first.Length);
            Array.Copy(second, 0, all, first.Length, second.Length);
            return all;
        }

        // The side column's width: a share of the pane between a column that reads and one that leaves the rows room.
        private static float SideWidth(float w) => UiDraw.SnapDown(Math.Max(UiTokens.S16 * 4.5f, Math.Min(UiTokens.S16 * 6.5f, w * 0.36f)));

        private static float SideRowsHeight(BizManStartRow[] rows, float w)
        {
            float total = 0f;
            for (int i = 0; i < rows.Length; i++)
            {
                total += StartRowHeight(ref rows[i], w, i == 0);
            }
            return total;
        }

        // What the side column needs to stand whole: the photo at its smallest above its rows.
        private static float SideNeeds(BizManModel m, float sideW) =>
            (string.IsNullOrEmpty(m.StartPhoto) ? 0f : LeastPhoto + UiTokens.S5) + SideRowsHeight(m.StartSide, sideW);

        // The building at the pane's right: its photo as tall as the room above its rows leaves it (never past the photo's own shape), then its
        // facts and the lease. No scroll and no stop: nothing in it is pressed.
        private static void SideColumn(UiScene s, UiAt at, BizManModel m, float x, float y, float w, float foot)
        {
            float rowsH = SideRowsHeight(m.StartSide, w), ry = y;
            if (!string.IsNullOrEmpty(m.StartPhoto))
            {
                float ph = UiDraw.SnapDown(Math.Max(LeastPhoto, Math.Min(w / PhotoAspect, foot - y - rowsH - UiTokens.S5)));
                Picture(s, at, m.StartPhoto, m.StartPhotoAspect, UiIcon.Image, x, y, w, ph);
                ry = y + ph + UiTokens.S5;
            }
            for (int i = 0; i < m.StartSide.Length; i++)
            {
                ref BizManStartRow r = ref m.StartSide[i];
                float top = ry + StartGap(r.Kind, i == 0 && string.IsNullOrEmpty(m.StartPhoto));
                if (r.Kind == BizManStartKind.Heading)
                {
                    UiDraw.Text(s, at, x, top, w, UiStyle.SectionHeader, UiRole.Text, r.Label, UiFlags.Label | UiFlags.Truncate);
                }
                else if (r.Kind == BizManStartKind.Fact)
                {
                    UiDraw.InfoRow(s, at, x, top, w, r.Label, r.Value, false, -1, 0, r.Tone == UiRole.None ? UiRole.Text : r.Tone);
                }
                ry += StartRowHeight(ref r, w, i == 0 && string.IsNullOrEmpty(m.StartPhoto));
            }
        }

        // The rows in a region that scrolls when they run past it, its thumb at its right edge; each heading, link and help button is a stop for the pad.
        private static float StartRows(UiScene s, UiAt at, BizManStartRow[] rows, float x, float y, float w, float h, float offset, ref int reveal, out short clip, out short list,
            IUiKeys keys)
        {
            float tw = w - UiTokens.S4;   // the thumb's lane
            float total = 0f, revealTop = -1f, revealH = 0f;
            int headings = 0, stops = 0;
            for (int i = 0; i < rows.Length; i++)
            {
                float rh;
                if (rows[i].Kind == BizManStartKind.Item && ItemRun(rows, i, tw, out int cols, out int n))
                {
                    float gap = StartGap(BizManStartKind.Item, i == 0);
                    for (int k = 0; k < n; k++)
                    {
                        if (rows[i + k].Arg > 0 && stops++ == reveal)
                        {
                            revealTop = total + gap + k / cols * UiTokens.PairRow;
                            revealH = UiTokens.PairRow;
                        }
                    }
                    rh = gap + (n + cols - 1) / cols * UiTokens.PairRow;
                    total += rh;
                    i += n - 1;
                    continue;
                }
                rh = StartRowHeight(ref rows[i], tw, i == 0);
                if ((rows[i].Kind == BizManStartKind.Heading || rows[i].Kind == BizManStartKind.Link || rows[i].Kind == BizManStartKind.Item && rows[i].Arg > 0) && stops++ == reveal)
                {
                    revealTop = total;
                    revealH = rh;
                }
                headings += rows[i].Kind == BizManStartKind.Heading ? 1 : 0;
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
            list = s.AddList(stops, 0, stops, clip, -1);
            var inClip = new UiAt { Parent = -1, Clip = clip, Layer = at.Layer, Order = -1, Region = at.Region };
            float ly = 0f;
            headings = 0;
            stops = 0;
            for (int i = 0; i < rows.Length; i++)
            {
                ref BizManStartRow r = ref rows[i];
                if (r.Kind == BizManStartKind.Item && ItemRun(rows, i, tw, out int cols, out int n))
                {
                    float gap = StartGap(BizManStartKind.Item, i == 0), colW = UiDraw.SnapDown((tw - UiTokens.S4 * (cols - 1)) / cols);
                    for (int k = 0; k < n; k++)
                    {
                        ItemAt(s, inClip, ref rows[i + k], (k % cols) * (colW + UiTokens.S4), ly + gap + k / cols * UiTokens.PairRow, colW, keys, list, ref stops);
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
                        UiDraw.Node(s, inClip, 0f, top - UiTokens.S1, tw, th + UiTokens.S2, Heading, StartHeading + headings, face, false, list, stops++);
                        headings++;
                        break;
                    }
                    case BizManStartKind.Fact:
                        UiDraw.InfoRow(s, inClip, 0f, top, tw, r.Label, r.Value, false, -1, 0, r.Tone == UiRole.None ? UiRole.Text : r.Tone);
                        break;
                    case BizManStartKind.Item:
                        ItemAt(s, inClip, ref r, 0f, top, tw, keys, list, ref stops);
                        break;
                    case BizManStartKind.Body:
                        UiDraw.Text(s, inClip, 0f, top, tw, UiStyle.Body, UiRole.Text, r.Label, UiFlags.Label | UiFlags.Wrap, MostBody);
                        break;
                    case BizManStartKind.Link:
                        UiDraw.LinkRow(s, inClip, 0f, top, tw, UiTokens.PairRow, r.Label, StartHelp, r.Arg, false);
                        Stop(s, list, stops++);
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

        // The node just made becomes the next stop of the region's list, so the pad that steps past the view asks for it to be scrolled to.
        private static void Stop(UiScene s, short list, int index)
        {
            int node = s.NodeCount - 1;
            s.Nodes[node].List = list;
            s.Nodes[node].Index = index;
        }

        // A line of a list: its mark and its words in a column of a width, one line high where the words fit, wrapped where they do not;
        // a line with a help page of the game's carries its "?" at the right, a stop of the region's list.
        private static void ItemAt(UiScene s, UiAt inClip, ref BizManStartRow r, float x, float top, float w, IUiKeys keys, short list, ref int stops)
        {
            float tx = x, room = w;
            if (r.Arg > 0)
            {
                room = w - UiTokens.PairRow - UiTokens.S1;
                UiDraw.Button(s, inClip, x + w - UiTokens.PairRow, top, UiTokens.PairRow, UiTokens.PairRow, UiButtonKind.Quiet, null, UiIcon.Info, UiAction.Count, keys, StartHelp, r.Arg);
                Stop(s, list, stops++);
            }
            if (r.Mark != UiIcon.None)
            {
                UiDraw.Icon(s, inClip, x, top + UiDraw.SnapDown((UiTokens.PairRow - UiTokens.IconSmall) / 2f), UiTokens.IconSmall, r.Mark, r.Tone, UiFlags.Middle, true);
                tx += UiTokens.IconSmall + UiTokens.S2;
            }
            float words = UiDraw.Snap(UiTextRules.WrappedHeight(r.Label ?? string.Empty, UiStyle.Body, x + room - tx, MostItem));
            if (words <= UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Body)))
            {
                UiDraw.TextMiddle(s, inClip, tx, top, UiTokens.PairRow, x + room - tx, UiStyle.Body, InkOf(r.Tone), r.Label, UiFlags.Label | UiFlags.Truncate);
            }
            else
            {
                UiDraw.Text(s, inClip, tx, top, x + room - tx, UiStyle.Body, InkOf(r.Tone), r.Label, UiFlags.Label | UiFlags.Wrap, MostItem);
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
                float help = rows[from + n].Arg > 0 ? UiTokens.PairRow + UiTokens.S1 : 0f;
                widest = Math.Max(widest, UiTextRules.Width(rows[from + n].Label ?? string.Empty, UiStyle.Body) + UiTokens.S1 + mark + help);
                n++;
            }
            cols = Math.Max(1, Math.Min(3, (int)((w + UiTokens.S4) / (UiDraw.Snap(widest) + UiTokens.S4))));
            return n > 1 && cols > 1;
        }

        private const int MostItem = 4;                        // lines a long line may take before it is cut
        private const int MostBody = 8;                        // lines a paragraph of the game's description may take
        public const int StartHeading = 3000;                  // a heading's argument from here, so no other column's stop is mistaken for it

        private static UiRole InkOf(UiRole tone) => tone == UiRole.None || tone == UiRole.Ok ? UiRole.Text : tone == UiRole.Warn ? UiRole.WarnText : tone == UiRole.Danger ? UiRole.DangerText : tone;

        private static float StartGap(BizManStartKind kind, bool first) =>
            first ? 0f : kind == BizManStartKind.Heading ? UiTokens.S5 : kind == BizManStartKind.Note || kind == BizManStartKind.Body ? UiTokens.S2 : UiTokens.S1;

        private static float StartRowHeight(ref BizManStartRow r, float w, bool first)
        {
            float gap = StartGap(r.Kind, first);
            switch (r.Kind)
            {
                case BizManStartKind.Heading:
                    return gap + UiDraw.Snap(UiTextRules.LineHeight(UiStyle.SectionHeader)) + UiTokens.S3;
                case BizManStartKind.Fact:
                case BizManStartKind.Link:
                    return gap + UiTokens.PairRow;
                case BizManStartKind.Item:
                {
                    float room = w - (r.Mark != UiIcon.None ? UiTokens.IconSmall + UiTokens.S2 : 0f) - (r.Arg > 0 ? UiTokens.PairRow + UiTokens.S1 : 0f);
                    return gap + Math.Max(UiTokens.PairRow, UiDraw.Snap(UiTextRules.WrappedHeight(r.Label ?? string.Empty, UiStyle.Body, room, MostItem)));
                }
                case BizManStartKind.Body:
                    return gap + UiDraw.Snap(UiTextRules.WrappedHeight(r.Label ?? string.Empty, UiStyle.Body, w, MostBody));
                default:
                    return gap + UiDraw.Snap(UiTextRules.WrappedHeight(r.Label ?? string.Empty, UiStyle.Small, w, MostItem));
            }
        }
    }
}
