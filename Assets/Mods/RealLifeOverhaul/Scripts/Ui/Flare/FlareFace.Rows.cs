using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// Flare's lists: a frame that scrolls, the rows measured once at its width, only the rows in view laid out, and targets
    /// given to the rows in view while the layer has room for them (a row past the room waits for the scroll, which the d-pad
    /// asks for as it would for a row out of view). Each kind of row has its own card. Game-free.
    /// </summary>
    public static partial class FlareFace
    {
        private const int PostLines = 6, ReviewLines = 4, BubbleLines = 12, CardLines = 2;
        private const int TagsWithTargets = 2;

        /// <summary>
        /// Lays a list into a frame; returns its scroll region. The list keeps where it was laid (Clip, Index) so the screen can
        /// scroll it by the wheel and the d-pad.
        /// </summary>
        public static short List(UiScene s, UiAt at, FlareList list, float x, float y, float w, float h, IUiKeys keys)
        {
            float vh = UiDraw.SnapDown(h);
            float cw = w - UiTokens.S3;   // the room for the scroll's thumb at the right
            Measure(list, cw);
            float contentH = 0f;
            for (int i = 0; i < list.Count; i++)
            {
                contentH += list.Rows[i].Height;
            }
            float most = Math.Max(0f, contentH - vh);
            float offset = list.Offset;
            if (list.Foot && !list.Footed)
            {
                offset = most;   // a conversation opens at its newest
                list.Footed = true;
            }
            int reveal = list.Reveal;
            if (reveal >= 0 && reveal < list.Count)
            {
                float top = 0f;
                for (int i = 0; i < reveal; i++)
                {
                    top += list.Rows[i].Height;
                }
                if (top < offset || top + list.Rows[reveal].Height > offset + vh)
                {
                    offset = top;   // the row asked for at the top of the view: it is laid first and has its targets first
                }
            }
            list.Reveal = -1;
            offset = Math.Max(0f, Math.Min(offset, most));
            list.Offset = offset;
            short clip = s.AddClip(x, y, cw, vh, offset);
            s.Clips[clip].ContentHeight = contentH;
            list.Clip = clip;
            UiDraw.ScrollThumb(s, at, x + cw + UiTokens.S1, y, vh, offset, contentH);
            if (list.Count == 0)
            {
                list.Index = s.AddList(0, 0, 0, clip, -1);
                Empty(s, at, list, x, y, cw, vh);
                return clip;
            }
            int first = -1, inView = 0;
            float py = 0f;
            for (int i = 0; i < list.Count; i++)
            {
                float rh = list.Rows[i].Height;
                if (py + rh > offset && py < offset + vh)
                {
                    first = first < 0 ? i : first;
                    inView++;
                }
                py += rh;
            }
            int budget = TargetsLeft(s, at.Layer);
            int held = reveal >= first && reveal < first + inView ? Need(ref list.Rows[reveal]) : 0;
            budget -= held;
            int shown = 0;
            bool full = false;
            short index = s.AddList(list.Count, Math.Max(0, first), inView, clip, -1);
            list.Index = index;
            var inClip = new UiAt { Parent = -1, Clip = clip, Layer = at.Layer, Order = -1, Region = at.Region };
            py = 0f;
            for (int i = 0; i < list.Count; i++)
            {
                ref FlareRow r = ref list.Rows[i];
                float rh = r.Height;
                if (py + rh > offset && py < offset + vh)
                {
                    int need = Need(ref r);
                    bool targets;
                    if (i == reveal)
                    {
                        targets = true;
                        budget += held - need;
                    }
                    else
                    {
                        targets = !full && need <= budget;
                        full |= !targets;
                        budget -= targets ? need : 0;
                    }
                    shown += targets ? 1 : 0;
                    // The panel's own slate under the row: what its words stand on, also where the row runs past the view.
                    UiAt rowAt = inClip.Arriving(i - first);
                    int ground = UiDraw.Box(s, rowAt, 0f, py, cw, rh, UiRole.Panel, UiRole.None, 0f, UiFlags.Decor);
                    int nodes = s.NodeCount;
                    Row(s, rowAt.In(ground), ref r, i, index, py, cw, targets);
                    OutOfView(s, nodes, offset, vh);
                }
                py += rh;
            }
            s.Lists[index].Shown = shown;   // rows without targets wait for the scroll, which the d-pad asks for
            return clip;
        }

        // A row half in view: its targets wholly past the view's edge can be neither pressed nor reached, so they wait, held, for
        // the scroll that lays the row again (the d-pad asks for it from the row's own target in the list).
        private static void OutOfView(UiScene s, int from, float offset, float vh)
        {
            for (int n = from; n < s.NodeCount; n++)
            {
                ref UiNode node = ref s.Nodes[n];
                if (node.Y + node.H <= offset + 0.5f || node.Y >= offset + vh - 0.5f)
                {
                    node.Disabled = true;
                }
            }
            for (int n = from; n < s.NodeCount; n++)
            {
                ref UiNode node = ref s.Nodes[n];
                node.Right = node.Right >= 0 && s.Nodes[node.Right].Disabled ? -1 : node.Right;   // a way into a held target is the nearest's again
            }
        }

        private static void Empty(UiScene s, UiAt at, FlareList list, float x, float y, float w, float h)
        {
            if (string.IsNullOrEmpty(list.EmptyTitle))
            {
                return;
            }
            float eh = Math.Min(h, UiDraw.Snap(UiTokens.S16 * 3f));
            UiDraw.EmptyState(s, at, x, y + UiTokens.S8, w, eh, list.EmptyIcon, list.EmptyTitle, list.EmptyBody);
        }

        /// <summary>The rows' heights at a width, measured again only when the width or the rows changed.</summary>
        public static void Measure(FlareList list, float w)
        {
            if (Math.Abs(list.Measured - w) < 0.5f)
            {
                return;
            }
            for (int i = 0; i < list.Count; i++)
            {
                list.Rows[i].Height = HeightOf(ref list.Rows[i], w);
            }
            list.Measured = w;
        }

        // The targets a row takes in view.
        private static int Need(ref FlareRow r)
        {
            switch (r.Kind)
            {
                case FlareRowKind.Post:
                    return ((r.Flags & FlareRowFlags.Comment) != 0 ? 3 : 5) + (r.Tags != null ? Math.Min(TagsWithTargets, r.Tags.Length) : 0);
                case FlareRowKind.Account:
                    return 2;
                case FlareRowKind.Result:
                    return string.IsNullOrEmpty(r.Button) ? 1 : 2;
                case FlareRowKind.Influencer:
                    return 1 + (string.IsNullOrEmpty(r.Button) ? 0 : 1);   // the row chooses them, the button hires them
                case FlareRowKind.Review:
                case FlareRowKind.Card:
                    return string.IsNullOrEmpty(r.Button) || (r.Flags & FlareRowFlags.Answered) != 0 ? 0 : 1;
                case FlareRowKind.Section:
                case FlareRowKind.Bubble:
                    return 0;
                default:
                    return 1;
            }
        }

        private static float ContentX => UiTokens.S4 + UiTokens.AvatarRow + UiTokens.S3;

        /// <summary>A row's height at a width, as its card lays it.</summary>
        public static float HeightOf(ref FlareRow r, float w)
        {
            switch (r.Kind)
            {
                case FlareRowKind.Post:
                {
                    float cw = w - ContentX - UiTokens.S4;
                    float h = UiTokens.S3;
                    h += string.IsNullOrEmpty(r.Line) ? 0f : UiTokens.S5;
                    h += UiTokens.S6;
                    h += string.IsNullOrEmpty(r.Text) ? 0f : UiDraw.Snap(UiTextRules.WrappedHeight(r.Text, UiStyle.Body, cw, PostLines)) + UiTokens.S1;
                    h += r.Tags != null && r.Tags.Length > 0 ? UiTokens.S6 + UiTokens.S1 : 0f;
                    h += string.IsNullOrEmpty(r.Picture) ? 0f : PictureHeight(cw) + UiTokens.S2;
                    h += string.IsNullOrEmpty(r.Value) ? 0f : UiTokens.S5 + UiTokens.S1;
                    h += UiTokens.ControlSmall + UiTokens.S3;
                    return h;
                }
                case FlareRowKind.Notice:
                    return UiTokens.S16;
                case FlareRowKind.Thread:
                    return UiTokens.S16 + UiTokens.S2;
                case FlareRowKind.Bubble:
                {
                    float bw = BubbleWidth(ref r, w);
                    return BubbleHeight(ref r, bw) + UiTokens.S5 + UiTokens.S2;
                }
                case FlareRowKind.Review:
                {
                    float tw = w - ContentX - UiTokens.S4 - ActionRoom;
                    float h = UiTokens.S3 + UiTokens.S6;
                    h += string.IsNullOrEmpty(r.Text) ? 0f : UiDraw.Snap(UiTextRules.WrappedHeight(r.Text, UiStyle.Body, tw, ReviewLines)) + UiTokens.S1;
                    h += string.IsNullOrEmpty(r.Line) ? 0f : UiDraw.Snap(UiTextRules.WrappedHeight(r.Line, UiStyle.Small, tw - UiTokens.S3, 2)) + UiTokens.S2;
                    return Math.Max(h + UiTokens.S3, UiTokens.S16);
                }
                case FlareRowKind.Account:
                case FlareRowKind.Tag:
                    return UiTokens.S16;
                case FlareRowKind.Result:
                    return UiTokens.S16;
                case FlareRowKind.Section:
                    return UiTokens.S10 + UiTokens.S1;
                case FlareRowKind.Influencer:
                    return UiTokens.S16 + UiTokens.S4;
                case FlareRowKind.Card:
                {
                    float tw = w - UiTokens.S4 - UiTokens.AvatarRow - UiTokens.S3 - UiTokens.S4 - ActionRoom;
                    float h = UiTokens.S3 + UiTokens.S6;
                    h += string.IsNullOrEmpty(r.Text) ? 0f : UiDraw.Snap(UiTextRules.WrappedHeight(r.Text, UiStyle.Body, tw, CardLines)) + UiTokens.S1;
                    h += string.IsNullOrEmpty(r.Line) ? 0f : UiTokens.S5;
                    return Math.Max(h + UiTokens.S3, UiTokens.S16 + UiTokens.S4);
                }
                default:
                    return UiTokens.S16;
            }
        }

        private static float PictureHeight(float w) => UiDraw.Snap(w / PictureAspect);

        private static float ButtonRoom(string words) => string.IsNullOrEmpty(words) ? 0f : PillWidth(words) + UiTokens.S3;

        // The column a row's action stands in, and the room it takes: every row of a list has it, with or without an action, so the times and the buttons stand in a line.
        public const float ActionColumn = 96f;
        private static float ActionRoom => ActionColumn + UiTokens.S3;

        // The hire table: its button and its figure columns are the same width on every row; the columns share what the name leaves, so a row has no gap in it.
        public const float HireButton = 112f, HireColumnLeast = 96f, HireColumnMost = 176f, HireIdentity = 264f;

        /// <summary>The width of one figure column of the hire table at a row's width.</summary>
        public static float HireColumn(float rowWidth, int columns)
        {
            if (columns <= 0)
            {
                return 0f;
            }
            float room = rowWidth - ContentX - HireIdentity - UiTokens.S4 - HireButton - UiTokens.S3;
            return Math.Max(HireColumnLeast, Math.Min(HireColumnMost, UiDraw.SnapDown(room / columns)));
        }

        /// <summary>Where the figures of a hire row start, at a row's width.</summary>
        public static float HireFigureX(float rowWidth, int columns) => rowWidth - UiTokens.S4 - HireButton - UiTokens.S3 - columns * HireColumn(rowWidth, columns);

        private static float BubbleWidth(ref FlareRow r, float w)
        {
            float most = UiDraw.SnapDown(w * 0.72f);
            float want = UiDraw.Snap(UiTextRules.Width(r.Text, UiStyle.Body) + UiTokens.S6 + UiTokens.S1);
            return Math.Max(UiTokens.S16, Math.Min(most, want));
        }

        private static float BubbleHeight(ref FlareRow r, float bw) => UiDraw.Snap(UiTextRules.WrappedHeight(r.Text, UiStyle.Body, bw - UiTokens.S6, BubbleLines) + UiTokens.S4);

        // One row on its card, at its top in the list's content.
        private static void Row(UiScene s, UiAt at, ref FlareRow r, int index, short list, float y, float w, bool targets)
        {
            switch (r.Kind)
            {
                case FlareRowKind.Post: PostRow(s, at, ref r, index, list, y, w, targets); break;
                case FlareRowKind.Notice: NoticeRow(s, at, ref r, index, list, y, w, targets); break;
                case FlareRowKind.Thread: ThreadRow(s, at, ref r, index, list, y, w, targets); break;
                case FlareRowKind.Bubble: BubbleRow(s, at, ref r, y, w); break;
                case FlareRowKind.Review: ReviewRow(s, at, ref r, index, list, y, w, targets); break;
                case FlareRowKind.Account: AccountRow(s, at, ref r, index, list, y, w, targets); break;
                case FlareRowKind.Tag: TagRow(s, at, ref r, index, list, y, w, targets); break;
                case FlareRowKind.Result: ResultRow(s, at, ref r, index, list, y, w, targets); break;
                case FlareRowKind.Section:
                    UiDraw.Text(s, at, UiTokens.S4, y + UiTokens.S5, w - UiTokens.S8, UiStyle.SectionHeader, UiRole.Text2, r.Name, UiFlags.Label | UiFlags.Truncate);
                    break;
                case FlareRowKind.Influencer: InfluencerRow(s, at, ref r, index, list, y, w, targets); break;
                case FlareRowKind.Card: CardRow(s, at, ref r, index, list, y, w, targets); break;
            }
        }

        // A post: the line over it, the author's face, name, mark, handle and time, the words, the tags, the picture, what it brought, the counts.
        private const string Dot = "\u00b7";   // between a post's handle and its time

        private static void PostRow(UiScene s, UiAt at, ref FlareRow r, int index, short list, float y, float w, bool targets)
        {
            int row = UiDraw.Box(s, at, 0f, y, w, r.Height, UiRole.None, UiRole.None, 0f);
            UiAt inRow = at.In(row);
            float cx = ContentX, cw = w - cx - UiTokens.S4, cy = y + UiTokens.S3;
            if (!string.IsNullOrEmpty(r.Line))
            {
                UiDraw.Icon(s, inRow, cx, cy, UiTokens.IconSmall, r.Icon != UiIcon.None ? r.Icon : UiIcon.Megaphone, UiRole.Text2);
                UiDraw.Text(s, inRow, cx + UiTokens.S5, cy, cw - UiTokens.S5, UiStyle.Small, UiRole.Text2, r.Line, UiFlags.Truncate);
                cy += UiTokens.S5;
            }
            bool verified = (r.Flags & FlareRowFlags.Verified) != 0;
            float nameW = Math.Min(UiDraw.Snap(UiTextRules.Width(r.Name, UiStyle.Title) + UiTokens.S1), UiDraw.SnapDown(cw * 0.55f));
            float markW = verified ? UiTokens.IconSmall + UiTokens.S2 : 0f;
            string handle = r.Handle;
            // How long ago after the handle, as a feed has it ("@speedybites · 2h"): the dot and the time keep their room, the handle gives way.
            float timeW = string.IsNullOrEmpty(r.Time) ? 0f : UiDraw.Snap(UiTextRules.Width(r.Time, UiStyle.Small) + UiTokens.S1);
            float dotW = timeW > 0f ? UiDraw.Snap(UiTextRules.Width(Dot, UiStyle.Small) + UiTokens.S2) : 0f;
            float handleW = Math.Min(UiDraw.Snap(UiTextRules.Width(handle, UiStyle.Small) + UiTokens.S1), cw - nameW - markW - UiTokens.S2 - dotW - timeW);
            float headW = cx - UiTokens.S4 + nameW + markW + UiTokens.S2 + Math.Max(0f, handleW);
            int head = UiDraw.Box(s, inRow, UiTokens.S4, cy, headW, UiTokens.S6, UiRole.None, UiRole.None, UiTokens.RadiusLarge);
            UiAt inHead = inRow.In(head);
            // the face stands beside the words under the name line, as the feed has it
            Face(s, inHead, UiTokens.S4, cy, UiTokens.AvatarRow, r.Name, r.Hue, (r.Flags & FlareRowFlags.Person) != 0, r.Ring, true);
            UiDraw.TextMiddle(s, inHead, cx, cy, UiTokens.S6, nameW, UiStyle.Title, UiRole.Text, r.Name, UiFlags.Truncate);
            float hx = cx + nameW;
            if (verified)
            {
                Verified(s, inHead, hx + UiTokens.S1, cy + UiTokens.S1);
                hx += markW;
            }
            if (handleW > UiTokens.S4)
            {
                UiDraw.TextMiddle(s, inHead, hx + UiTokens.S2, cy, UiTokens.S6, handleW, UiStyle.Small, UiRole.Text2, handle, UiFlags.Truncate);
            }
            if (timeW > 0f)
            {
                float tx = hx + UiTokens.S2 + (handleW > UiTokens.S4 ? handleW : 0f);
                UiDraw.TextMiddle(s, inRow, tx, cy, UiTokens.S6, dotW, UiStyle.Small, UiRole.Text2, Dot, UiFlags.Centre);
                UiDraw.TextMiddle(s, inRow, tx + dotW, cy, UiTokens.S6, timeW, UiStyle.Small, UiRole.Text2, r.Time, UiFlags.None);
            }
            int author = targets ? UiDraw.Node(s, inRow, UiTokens.S4, cy, headW, UiTokens.S6, AuthorCommand, index, head, false, list, index) : -1;
            cy += UiTokens.S6;
            if (!string.IsNullOrEmpty(r.Text))
            {
                float th = UiDraw.Snap(UiTextRules.WrappedHeight(r.Text, UiStyle.Body, cw, PostLines));
                UiDraw.Text(s, inRow, cx, cy, cw, UiStyle.Body, UiRole.Text, r.Text, UiFlags.Wrap | UiFlags.Truncate, PostLines);
                cy += th + UiTokens.S1;
            }
            if (r.Tags != null && r.Tags.Length > 0)
            {
                float tx = cx;
                for (int t = 0; t < r.Tags.Length; t++)
                {
                    float tw = UiDraw.Snap(UiTextRules.Width(r.Tags[t], UiStyle.Body) + UiTokens.S2);
                    if (tx + tw > cx + cw)
                    {
                        break;
                    }
                    int tag = UiDraw.Box(s, inRow, tx, cy, tw, UiTokens.S6, UiRole.None, UiRole.None, UiTokens.RadiusSmall);
                    UiDraw.TextMiddle(s, inRow.In(tag), tx, cy, UiTokens.S6, tw, UiStyle.Body, UiRole.AccentText, r.Tags[t], UiFlags.None);
                    if (targets && t < TagsWithTargets)
                    {
                        UiDraw.Node(s, inRow, tx, cy, tw, UiTokens.S6, TagCommand, index * 4 + t, tag);
                    }
                    tx += tw + UiTokens.S2;
                }
                cy += UiTokens.S6 + UiTokens.S1;
            }
            if (!string.IsNullOrEmpty(r.Picture))
            {
                float ph = PictureHeight(cw);
                int tile = UiDraw.Box(s, inRow, cx, cy, cw, ph, UiRole.Tile, UiRole.None, UiTokens.RadiusLarge);
                var pic = new UiItem
                {
                    Kind = UiKind.Picture, Flags = UiFlags.Middle | UiFlags.Centre, X = cx, Y = cy, W = cw, H = ph, Text = PictureKey(r.Picture, PictureAspect), Value = PictureAspect,
                    Clip = inRow.Clip, Node = -1, Layer = inRow.Layer, Order = inRow.Order, Parent = tile, Alpha = 1f
                };
                s.Add(ref pic);
                cy += ph + UiTokens.S2;
            }
            if (!string.IsNullOrEmpty(r.Value))
            {
                UiRole ink = (r.Flags & FlareRowFlags.Bad) != 0 ? UiRole.DangerText : (r.Flags & FlareRowFlags.Good) != 0 ? UiRole.OkText : UiRole.Text2;
                UiDraw.Text(s, inRow, cx, cy, cw, UiStyle.SmallStrong, ink, r.Value, UiFlags.Truncate);
                cy += UiTokens.S5 + UiTokens.S1;
            }
            float slot = UiDraw.SnapDown(cw / 5f);
            bool liked = (r.Flags & FlareRowFlags.Liked) != 0, reposted = (r.Flags & FlareRowFlags.Reposted) != 0;
            int like = Count(s, inRow, cx - UiTokens.S2, cy, slot, UiIcon.Heart, r.Likes, liked ? UiRole.CatRetail : UiRole.Text2, liked, targets ? LikeCommand : -1, index);
            if (author >= 0 && like >= 0)
            {
                // Up and down step from post to post through the list (author to author, scrolling); right goes into the post's own
                // counts, which stand under its words where no straight line from the name reaches them.
                s.Nodes[author].Right = like;
            }
            Count(s, inRow, cx - UiTokens.S2 + slot, cy, slot, UiIcon.Message, r.Comments, UiRole.Text2, false, targets ? CommentCommand : -1, index);
            if ((r.Flags & FlareRowFlags.Comment) == 0)
            {
                Count(s, inRow, cx - UiTokens.S2 + slot * 2f, cy, slot, UiIcon.Reset, r.Reposts, reposted ? UiRole.OkText : UiRole.Text2, reposted, targets ? RepostCommand : -1, index,
                    MarkGlyph);
                Count(s, inRow, cx - UiTokens.S2 + slot * 3f, cy, slot, UiIcon.Eye, r.Views, UiRole.Text2, false, -1, index);
                Count(s, inRow, cx + cw - UiTokens.ControlSmall, cy, UiTokens.ControlSmall, UiIcon.Upload, null, UiRole.Text2, false, targets ? ShareCommand : -1, index, MarkGlyph);
            }
            UiDraw.Box(s, inRow, 0f, y + r.Height - UiTokens.Hairline, w, UiTokens.Hairline, UiRole.Line, UiRole.None, 0f, UiFlags.Decor);
        }

        // The repost's and the share's glyphs are drawn lighter in their box than the heart and the bubble, so they are cut a size larger to read as big as the others.
        private const float MarkGlyph = UiTokens.Icon + UiTokens.S1;

        // Flare's mark of an account it knows: the accent's disc with a tick.
        private static void Verified(UiScene s, UiAt at, float x, float y)
        {
            int disc = UiDraw.Circle(s, at, x, y, UiTokens.IconSmall, UiTokens.IconSmall, UiRole.Accent);
            UiDraw.Icon(s, at.In(disc), x + UiTokens.S1 / 2f, y + UiTokens.S1 / 2f, UiTokens.S3, UiIcon.Check, UiRole.OnAccent, UiFlags.Middle | UiFlags.Centre);
        }

        // A count beside its glyph in a slot: liked, the heart is filled in its pink. A target when it has a command; returns it, or -1.
        private static int Count(UiScene s, UiAt at, float x, float y, float slot, UiIcon icon, string count, UiRole tint, bool given, int command, int arg, float glyph = 0f)
        {
            float h = UiTokens.ControlSmall;
            float g = glyph > 0f ? glyph : UiTokens.Icon;
            float cw = string.IsNullOrEmpty(count) ? 0f : Math.Min(UiDraw.Snap(UiTextRules.Width(count, UiStyle.SmallStrong) + UiTokens.S1), slot - h - UiTokens.S2);
            float bw = h + (cw > 0f ? cw + UiTokens.S1 : 0f);
            int face = UiDraw.Box(s, at, x, y, bw, h, UiRole.None, UiRole.None, UiTokens.RadiusLarge, given ? UiFlags.Selected : UiFlags.None);
            UiAt inside = at.In(face);
            UiDraw.Icon(s, inside, x + (h - g) / 2f, y + (h - g) / 2f, g, icon, tint, UiFlags.Middle | UiFlags.Centre, given);
            if (cw > 0f)
            {
                UiDraw.TextMiddle(s, inside, x + h, y, h, cw, UiStyle.SmallStrong, tint, count, UiFlags.Truncate);
            }
            return command >= 0 ? UiDraw.Node(s, at, x, y, bw, h, command, arg, face) : -1;
        }

        // A notification: unread marked by the dot and strong words; its kind's glyph on a disc, the line, the detail, when.
        private static void NoticeRow(UiScene s, UiAt at, ref FlareRow r, int index, short list, float y, float w, bool targets)
        {
            bool unread = (r.Flags & FlareRowFlags.Unread) != 0;
            float h = r.Height;
            int row = UiDraw.Box(s, at, 0f, y, w, h, unread ? UiRole.Hover : UiRole.None, UiRole.None, UiTokens.RadiusLarge);
            UiAt inRow = at.In(row);
            if (unread)
            {
                UiDraw.Circle(s, inRow, UiTokens.S2, y + (h - UiTokens.StatusDot) / 2f, UiTokens.StatusDot, UiTokens.StatusDot, UiRole.Accent, UiFlags.Middle);
            }
            float gx = UiTokens.S6;
            float gy = y + (h - UiTokens.AvatarRow) / 2f;
            int disc = UiDraw.Circle(s, inRow, gx, gy, UiTokens.AvatarRow, UiTokens.AvatarRow, unread ? UiRole.Panel : UiRole.Tile);
            // a tile on every row, the unread ones on the panel's slate so it shows
            UiDraw.Icon(s, inRow.In(disc), gx + (UiTokens.AvatarRow - UiTokens.Icon) / 2f, gy + (UiTokens.AvatarRow - UiTokens.Icon) / 2f, UiTokens.Icon, r.Icon,
                r.Tint == UiRole.None ? UiRole.Text2 : r.Tint, UiFlags.Middle | UiFlags.Centre, true);
            float tx = gx + UiTokens.AvatarRow + UiTokens.S3;
            float timeW = string.IsNullOrEmpty(r.Time) ? 0f : UiDraw.Snap(UiTextRules.Width(r.Time, UiStyle.Small) + UiTokens.S1);
            float tw = w - tx - timeW - UiTokens.S6;
            bool twoLines = !string.IsNullOrEmpty(r.Line) || !string.IsNullOrEmpty(r.Value);
            float lineY = twoLines ? gy + UiTokens.S1 : gy + (UiTokens.AvatarRow - L(UiStyle.Body)) / 2f;   // one line sits on the glyph's middle, two share it
            UiDraw.Text(s, inRow, tx, lineY, tw, unread ? UiStyle.BodyStrong : UiStyle.Body, UiRole.Text, r.Text, UiFlags.Truncate);
            float valueW = string.IsNullOrEmpty(r.Value) ? 0f : Math.Min(UiDraw.Snap(UiTextRules.Width(r.Value, UiStyle.SmallStrong) + UiTokens.S1), UiDraw.SnapDown(tw * 0.5f));
            if (!string.IsNullOrEmpty(r.Line))
            {
                UiDraw.Text(s, inRow, tx, lineY + UiTokens.S6, tw - valueW - (valueW > 0f ? UiTokens.S3 : 0f), UiStyle.Small, UiRole.Text2, r.Line, UiFlags.Truncate);
            }
            if (valueW > 0f)
            {
                // What the post it is about has brought, in the good news' green, at the row's right under the time.
                UiDraw.Text(s, inRow, w - UiTokens.S4 - valueW, lineY + UiTokens.S6, valueW, UiStyle.SmallStrong, (r.Flags & FlareRowFlags.Bad) != 0 ? UiRole.DangerText : UiRole.OkText,
                    r.Value, UiFlags.Right | UiFlags.Truncate);
            }
            if (timeW > 0f)
            {
                UiDraw.Text(s, inRow, w - UiTokens.S4 - timeW, lineY, timeW, UiStyle.Small, UiRole.Text2, r.Time, UiFlags.Right);
            }
            if (targets)
            {
                UiDraw.Node(s, at, 0f, y, w, h, RowCommand, index, row, false, list, index);
            }
        }

        // A conversation: the face, the name and when, the last line, and how many are unread.
        private static void ThreadRow(UiScene s, UiAt at, ref FlareRow r, int index, short list, float y, float w, bool targets)
        {
            bool chosen = (r.Flags & FlareRowFlags.Chosen) != 0, unread = (r.Flags & FlareRowFlags.Unread) != 0;
            float h = r.Height;
            int row = UiDraw.Box(s, at, 0f, y, w, h, chosen ? UiRole.RowSel : UiRole.None, UiRole.None, UiTokens.RadiusLarge, chosen ? UiFlags.Selected : UiFlags.None);
            UiAt inRow = at.In(row);
            Face(s, inRow, UiTokens.S4, y + UiTokens.S4, UiTokens.AvatarRow, r.Name, r.Hue, (r.Flags & FlareRowFlags.Person) != 0, r.Ring, false);
            float tx = ContentX;
            float timeW = string.IsNullOrEmpty(r.Time) ? 0f : UiDraw.Snap(UiTextRules.Width(r.Time, UiStyle.Small) + UiTokens.S1);
            UiDraw.Text(s, inRow, tx, y + UiTokens.S4, w - tx - timeW - UiTokens.S6, UiStyle.Title, UiRole.Text, r.Name, UiFlags.Truncate);
            if (timeW > 0f)
            {
                UiDraw.Text(s, inRow, w - UiTokens.S4 - timeW, y + UiTokens.S4, timeW, UiStyle.Small, chosen ? UiRole.Text : UiRole.Text2, r.Time, UiFlags.Right);
            }
            float bw = 0f;
            if (unread && !string.IsNullOrEmpty(r.Value))
            {
                int n;
                int.TryParse(r.Value, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out n);
                bw = BadgeOf(s, inRow, w - UiTokens.S4, y + UiTokens.S8 + UiTokens.S1, Math.Max(1, n));
            }
            UiDraw.Text(s, inRow, tx, y + UiTokens.S10, w - tx - bw - UiTokens.S6, UiStyle.Small, unread || chosen ? UiRole.Text : UiRole.Text2, r.Text, UiFlags.Truncate);
            if (targets)
            {
                UiDraw.Node(s, at, 0f, y, w, h, RowCommand, index, row, false, list, index);
            }
        }

        // A message: theirs at the left on the bubble's slate, the player's at the right on the chosen tone, when under it.
        private static void BubbleRow(UiScene s, UiAt at, ref FlareRow r, float y, float w)
        {
            bool mine = (r.Flags & FlareRowFlags.Mine) != 0;
            float bw = BubbleWidth(ref r, w), bh = BubbleHeight(ref r, bw);
            float bx = mine ? UiDraw.SnapDown(w - UiTokens.S4 - bw) : UiTokens.S4;
            int bubble = UiDraw.Box(s, at, bx, y, bw, bh, mine ? UiRole.RowSel : UiRole.Bubble, UiRole.None, UiTokens.RadiusLarge);
            UiDraw.Text(s, at.In(bubble), bx + UiTokens.S3, y + UiTokens.S2, bw - UiTokens.S6, UiStyle.Body, UiRole.Text, r.Text, UiFlags.Wrap | UiFlags.Truncate, BubbleLines);
            if (!string.IsNullOrEmpty(r.Time))
            {
                float tw = UiDraw.Snap(UiTextRules.Width(r.Time, UiStyle.Small) + UiTokens.S1);
                UiDraw.Text(s, at, mine ? bx + bw - tw : bx, y + bh + UiTokens.S1, tw, UiStyle.Small, UiRole.Text2, r.Time, mine ? UiFlags.Right : UiFlags.None);
            }
        }

        // A review: its stars, who wrote it and when, the words, the answer when there is one, and Reply.
        private static void ReviewRow(UiScene s, UiAt at, ref FlareRow r, int index, short list, float y, float w, bool targets)
        {
            float h = r.Height;
            bool answered = (r.Flags & FlareRowFlags.Answered) != 0;
            int row = UiDraw.Box(s, at, 0f, y, w, h, UiRole.None, UiRole.None, 0f);
            UiAt inRow = at.In(row);
            float bw = ActionRoom;
            Face(s, inRow, UiTokens.S4, y + UiTokens.S3, UiTokens.AvatarRow, r.Name, r.Hue, (r.Flags & FlareRowFlags.Person) != 0, r.Ring, false);   // the reviewer's face, ringed by how they left
            float x = ContentX, tw = w - ContentX - UiTokens.S4 - bw, cy = y + UiTokens.S3;
            float sw = UiDraw.Stars(s, inRow, x, cy + UiTokens.S1, UiTokens.IconSmall, Math.Max(0, Math.Min(10, r.Stars * 2)));
            float nx = x + UiDraw.Snap(sw + UiTokens.S2);
            float timeW = string.IsNullOrEmpty(r.Time) ? 0f : UiDraw.Snap(UiTextRules.Width(r.Time, UiStyle.Small) + UiTokens.S1);
            UiDraw.TextMiddle(s, inRow, nx, cy, UiTokens.S6, Math.Max(UiTokens.S4, x + tw - nx - timeW - UiTokens.S2), UiStyle.Title, UiRole.Text, r.Name, UiFlags.Truncate);
            if (timeW > 0f)
            {
                UiDraw.TextMiddle(s, inRow, x + tw - timeW, cy, UiTokens.S6, timeW, UiStyle.Small, UiRole.Text2, r.Time, UiFlags.Right);
            }
            cy += UiTokens.S6;
            if (!string.IsNullOrEmpty(r.Text))
            {
                float th = UiDraw.Snap(UiTextRules.WrappedHeight(r.Text, UiStyle.Body, tw, ReviewLines));
                UiDraw.Text(s, inRow, x, cy, tw, UiStyle.Body, UiRole.Text, r.Text, UiFlags.Wrap | UiFlags.Truncate, ReviewLines);
                cy += th + UiTokens.S1;
            }
            if (!string.IsNullOrEmpty(r.Line))
            {
                UiDraw.Text(s, inRow, x + UiTokens.S3, cy, tw - UiTokens.S3, UiStyle.Small, UiRole.Text2, r.Line, UiFlags.Wrap | UiFlags.Truncate, 2);
            }
            if (!answered && !string.IsNullOrEmpty(r.Button))
            {
                float pw = Math.Max(ActionColumn, PillWidth(r.Button));
                Pill(s, inRow, w - UiTokens.S4 - pw, y + UiTokens.S3, pw, r.Button, PillKind.Line, targets ? ButtonCommand : -1, index, UiIcon.None, (r.Flags & FlareRowFlags.Off) != 0);
            }
            UiDraw.Box(s, inRow, 0f, y + h - UiTokens.Hairline, w, UiTokens.Hairline, UiRole.Line, UiRole.None, 0f, UiFlags.Decor);
        }

        // An account to follow: the face, the name and its mark, what it is, and Follow or Following.
        private static void AccountRow(UiScene s, UiAt at, ref FlareRow r, int index, short list, float y, float w, bool targets)
        {
            float h = r.Height;
            int row = UiDraw.Box(s, at, 0f, y, w, h, UiRole.None, UiRole.None, UiTokens.RadiusLarge);
            UiAt inRow = at.In(row);
            Face(s, inRow, UiTokens.S4, y + UiTokens.S3, UiTokens.AvatarRow, r.Name, r.Hue, (r.Flags & FlareRowFlags.Person) != 0, r.Ring, false);
            bool following = (r.Flags & FlareRowFlags.Following) != 0;
            float pw = string.IsNullOrEmpty(r.Button) ? 0f : PillWidth(r.Button);
            float tx = ContentX, tw = w - tx - pw - UiTokens.S8;
            bool verified = (r.Flags & FlareRowFlags.Verified) != 0;
            float nameW = Math.Min(UiDraw.Snap(UiTextRules.Width(r.Name, UiStyle.Title) + UiTokens.S1), tw - (verified ? UiTokens.S5 : 0f));
            UiDraw.Text(s, inRow, tx, y + UiTokens.S3, nameW, UiStyle.Title, UiRole.Text, r.Name, UiFlags.Truncate);
            if (verified)
            {
                Verified(s, inRow, tx + nameW + UiTokens.S1, y + UiTokens.S3);
            }
            UiDraw.Text(s, inRow, tx, y + UiTokens.S3 + UiTokens.S6, tw, UiStyle.Small, UiRole.Text2, r.Handle, UiFlags.Truncate);
            if (pw > 0f)
            {
                Pill(s, inRow, w - UiTokens.S4 - pw, y + (h - UiTokens.ControlSmall) / 2f, pw, r.Button, following ? PillKind.Quiet : PillKind.Light, targets ? FollowCommand : -1, index);
            }
            if (targets)
            {
                UiDraw.Node(s, at, 0f, y, w - pw - UiTokens.S6, h, RowCommand, index, row, false, list, index);
            }
        }

        // A trending tag: where and what, the tag, its posts.
        private static void TagRow(UiScene s, UiAt at, ref FlareRow r, int index, short list, float y, float w, bool targets)
        {
            float h = r.Height;
            int row = UiDraw.Box(s, at, 0f, y, w, h, UiRole.None, UiRole.None, UiTokens.RadiusLarge);
            UiAt inRow = at.In(row);
            float tx = UiTokens.S4, tw = w - UiTokens.S8;
            UiDraw.Text(s, inRow, tx, y + UiTokens.S1, tw, UiStyle.Small, UiRole.Text2, r.Handle, UiFlags.Truncate);
            UiDraw.Text(s, inRow, tx, y + UiTokens.S6, tw, UiStyle.Title, UiRole.Text, r.Name, UiFlags.Truncate);
            UiDraw.Text(s, inRow, tx, y + UiTokens.S10 + UiTokens.S1, tw, UiStyle.Small, UiRole.Text2, r.Value, UiFlags.Truncate);
            if (targets)
            {
                UiDraw.Node(s, at, 0f, y, w, h, RowCommand, index, row, false, list, index);
            }
        }

        // What a post or a campaign brought: its glyph, the words, when, and the money in its tone.
        private static void ResultRow(UiScene s, UiAt at, ref FlareRow r, int index, short list, float y, float w, bool targets)
        {
            float h = r.Height;
            int row = UiDraw.Box(s, at, 0f, y, w, h, UiRole.None, UiRole.None, UiTokens.RadiusLarge);
            UiAt inRow = at.In(row);
            int disc = UiDraw.Circle(s, inRow, UiTokens.S4, y + UiTokens.S3, UiTokens.AvatarRow, UiTokens.AvatarRow, UiRole.Tile);
            UiDraw.Icon(s, inRow.In(disc), UiTokens.S4 + (UiTokens.AvatarRow - UiTokens.Icon) / 2f, y + UiTokens.S3 + (UiTokens.AvatarRow - UiTokens.Icon) / 2f, UiTokens.Icon,
                r.Icon == UiIcon.None ? UiIcon.ChartLineUp : r.Icon, r.Tint == UiRole.None ? UiRole.Text2 : r.Tint, UiFlags.Middle | UiFlags.Centre, true);
            float bw = string.IsNullOrEmpty(r.Button) ? 0f : PillWidth(r.Button) + UiTokens.S3;
            float vw = string.IsNullOrEmpty(r.Value) ? 0f : UiDraw.Snap(UiTextRules.Width(r.Value, UiStyle.BodyStrong) + UiTokens.S1);
            float tx = ContentX, tw = w - tx - vw - bw - UiTokens.S8;
            UiDraw.Text(s, inRow, tx, y + UiTokens.S3, tw, UiStyle.Body, UiRole.Text, r.Text, UiFlags.Truncate);
            UiDraw.Text(s, inRow, tx, y + UiTokens.S3 + UiTokens.S6, tw, UiStyle.Small, UiRole.Text2, r.Line, UiFlags.Truncate);
            if (vw > 0f)
            {
                UiRole ink = (r.Flags & FlareRowFlags.Bad) != 0 ? UiRole.DangerText : (r.Flags & FlareRowFlags.Good) != 0 ? UiRole.OkText : UiRole.Text;
                UiDraw.TextMiddle(s, inRow, w - UiTokens.S4 - vw - bw, y, h, vw, UiStyle.BodyStrong, ink, r.Value, UiFlags.Right);
            }
            if (targets)
            {
                UiDraw.Node(s, at, 0f, y, w - bw, h, RowCommand, index, row, false, list, index);
            }
            if (bw > 0f)
            {
                float pw = PillWidth(r.Button);
                bool armed = (r.Flags & FlareRowFlags.Chosen) != 0;
                Pill(s, inRow, w - UiTokens.S4 - pw, y + (h - UiTokens.ControlSmall) / 2f, pw, r.Button, armed ? PillKind.Chosen : PillKind.Line, targets ? ButtonCommand : -1, index);
            }
            UiDraw.Box(s, inRow, tx, y + h - UiTokens.Hairline, w - tx - UiTokens.S4, UiTokens.Hairline, UiRole.Line, UiRole.None, 0f, UiFlags.Decor);
        }

        // An influencer for hire: the face on the ring of their record, the handle and what they post about, their figures (a bar behind the engagement and the fit),
        // and Hire. Pressed, the row chooses them: the card under the table tells more of them.
        private static void InfluencerRow(UiScene s, UiAt at, ref FlareRow r, int index, short list, float y, float w, bool targets)
        {
            float h = r.Height, rh = h - UiTokens.S2;
            bool chosen = (r.Flags & FlareRowFlags.Chosen) != 0;
            int row = UiDraw.Box(s, at, 0f, y, w, rh, chosen ? UiRole.RowSel : UiRole.Tile, UiRole.None, UiTokens.RadiusLarge, chosen ? UiFlags.Selected : UiFlags.None);
            UiAt inRow = at.In(row);
            Face(s, inRow, UiTokens.S4, y + (rh - UiTokens.AvatarRow) / 2f, UiTokens.AvatarRow, r.Name, r.Hue, true, r.Ring, false);
            float pw = string.IsNullOrEmpty(r.Button) ? 0f : HireButton;
            float tx = ContentX;
            int cols = r.Tags != null ? r.Tags.Length / 2 : 0;
            float figX = HireFigureX(w, cols), col = HireColumn(w, cols);
            float tw = figX - tx - UiTokens.S3;
            bool verified = (r.Flags & FlareRowFlags.Verified) != 0;
            float nameW = Math.Min(UiDraw.Snap(UiTextRules.Width(r.Name, UiStyle.Title) + UiTokens.S1), tw - (verified ? UiTokens.S5 : 0f));
            float ty = y + (rh - L(UiStyle.Title) - UiTokens.S1 - L(UiStyle.Small)) / 2f;
            UiDraw.Text(s, inRow, tx, UiDraw.SnapDown(ty), nameW, UiStyle.Title, UiRole.Text, r.Name, UiFlags.Truncate);
            if (verified)
            {
                Verified(s, inRow, tx + nameW + UiTokens.S1, UiDraw.SnapDown(ty));
            }
            UiDraw.Text(s, inRow, tx, UiDraw.SnapDown(ty) + L(UiStyle.Title) + UiTokens.S1, tw, UiStyle.Small, UiRole.Text2, r.Handle, UiFlags.Truncate);
            // The figures: what they reach, how many answer, what a post costs, what it should bring, right aligned in columns that are the same on every row.
            string[] figures = r.Tags;
            for (int i = 0; i < cols; i++)
            {
                float cx = figX + i * col, cw = col - UiTokens.S3;
                bool bar = r.Shares != null && i < r.Shares.Length && r.Shares[i] >= 0f;
                if (!bar)
                {
                    UiDraw.TextMiddle(s, inRow, cx, y, rh, cw, UiStyle.BodyStrong, UiRole.Text, figures[i * 2 + 1], UiFlags.Right | UiFlags.Truncate);
                    continue;
                }
                float ny = UiDraw.SnapDown(y + (rh - L(UiStyle.BodyStrong) - UiTokens.S2 - UiTokens.S1) / 2f);
                UiDraw.Text(s, inRow, cx, ny, cw, UiStyle.BodyStrong, UiRole.Text, figures[i * 2 + 1], UiFlags.Right | UiFlags.Truncate);
                float share = Math.Max(0f, Math.Min(1f, r.Shares[i])), by = ny + L(UiStyle.BodyStrong) + UiTokens.S1;
                float bw = Math.Min(cw, UiTokens.S16 + UiTokens.S4);
                int track = UiDraw.Box(s, inRow, cx + cw - bw, by, bw, UiTokens.S1, chosen ? UiRole.Panel : UiRole.Track, UiRole.None, UiTokens.S1 / 2f, UiFlags.Middle);
                UiDraw.Box(s, inRow.In(track), cx + cw - bw, by, Math.Max(UiTokens.S1, bw * share), UiTokens.S1, UiTokens.Grade((int)Math.Round(share * 100f)), UiRole.None, UiTokens.S1 / 2f,
                    UiFlags.Middle | UiFlags.Share);
            }
            if (targets)
            {
                UiDraw.Node(s, at, 0f, y, w - (pw > 0f ? pw + UiTokens.S6 : 0f), rh, RowCommand, index, row, false, list, index);
            }
            if (pw > 0f)
            {
                Orange(s, inRow, w - UiTokens.S4 - pw, y + (rh - UiTokens.ControlSmall) / 2f, pw, UiTokens.ControlSmall, r.Button, targets ? ButtonCommand : -1, index,
                    (r.Flags & FlareRowFlags.Off) != 0);   // without a target it shows, pressable once it scrolls in with room for one
            }
        }

        // A card of the city: its glyph, a title, what happened, where and when, and what to do about it.
        private static void CardRow(UiScene s, UiAt at, ref FlareRow r, int index, short list, float y, float w, bool targets)
        {
            float h = r.Height;
            int row = UiDraw.Box(s, at, 0f, y, w, h - UiTokens.S2, UiRole.Tile, UiRole.None, UiTokens.RadiusLarge);
            UiAt inRow = at.In(row);
            int disc = UiDraw.Circle(s, inRow, UiTokens.S4, y + UiTokens.S3, UiTokens.AvatarRow, UiTokens.AvatarRow, UiRole.Panel);
            UiDraw.Icon(s, inRow.In(disc), UiTokens.S4 + (UiTokens.AvatarRow - UiTokens.Icon) / 2f, y + UiTokens.S3 + (UiTokens.AvatarRow - UiTokens.Icon) / 2f, UiTokens.Icon,
                r.Icon == UiIcon.None ? UiIcon.MapPin : r.Icon, r.Tint == UiRole.None ? UiRole.Text2 : r.Tint, UiFlags.Middle | UiFlags.Centre, true);
            float pw = string.IsNullOrEmpty(r.Button) ? 0f : Math.Max(ActionColumn, PillWidth(r.Button));
            float tx = ContentX, tw = w - tx - UiTokens.S4 - ActionRoom;
            float cy = y + UiTokens.S3;
            float timeW = string.IsNullOrEmpty(r.Time) ? 0f : UiDraw.Snap(UiTextRules.Width(r.Time, UiStyle.Small) + UiTokens.S1);
            UiDraw.TextMiddle(s, inRow, tx, cy, UiTokens.S6, tw - timeW - UiTokens.S2, UiStyle.Title, UiRole.Text, r.Name, UiFlags.Truncate);
            if (timeW > 0f)
            {
                UiDraw.TextMiddle(s, inRow, tx + tw - timeW, cy, UiTokens.S6, timeW, UiStyle.Small, UiRole.Text2, r.Time, UiFlags.Right);
            }
            cy += UiTokens.S6;
            if (!string.IsNullOrEmpty(r.Text))
            {
                float th = UiDraw.Snap(UiTextRules.WrappedHeight(r.Text, UiStyle.Body, tw, CardLines));
                UiDraw.Text(s, inRow, tx, cy, tw, UiStyle.Body, UiRole.Text, r.Text, UiFlags.Wrap | UiFlags.Truncate, CardLines);
                cy += th + UiTokens.S1;
            }
            if (!string.IsNullOrEmpty(r.Line))
            {
                UiDraw.Text(s, inRow, tx, cy, tw, UiStyle.Small, UiRole.Text2, r.Line, UiFlags.Truncate);
            }
            if (pw > 0f)
            {
                Pill(s, inRow, w - UiTokens.S4 - pw, y + UiTokens.S3, pw, r.Button, PillKind.Line, targets ? ButtonCommand : -1, index, UiIcon.None, (r.Flags & FlareRowFlags.Off) != 0);
            }
        }
    }
}
