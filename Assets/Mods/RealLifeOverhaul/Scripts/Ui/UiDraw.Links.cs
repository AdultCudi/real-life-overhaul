using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// Links and the rows that lead with one or with a face, on one rhythm with the info panel's rows: a link's glyph
    /// stands <see cref="LinkGap"/> from the words as they are drawn (cut to their room first, so a long name never pushes
    /// its mark away or under it), a row that leads with a link is a pair row high, and a person's row is two pair rows,
    /// its face's left edge on the rows' labels. Game-free. Cost: a few item writes each.
    /// </summary>
    public static partial class UiDraw
    {
        /// <summary>A person's row: two pair rows, as a household's member stands among an info panel's rows.</summary>
        public const float PersonRowHeight = UiTokens.PairRow * 2f;

        /// <summary>
        /// An empty state that names what will fill its place: the thing's glyph on a tile over its words, the whole held
        /// in the middle of the room it is given, so a page with nothing yet reads as meant and says how it fills.
        /// </summary>
        public static void EmptyState(UiScene s, UiAt at, float x, float y, float w, float h, UiIcon icon, string title, string body)
        {
            const float tile = UiTokens.S12;
            float words = EmptyHeight(w, title, body) - UiTokens.S12 * 2f;
            float block = tile + UiTokens.S4 + words;
            float top = y + SnapDown(Math.Max(0f, (h - block) / 2f));
            int room = Box(s, at, x, y, w, h, UiRole.None, UiRole.None, 0f);
            UiAt inside = at.In(room);
            float tx = x + SnapDown((w - tile) / 2f);
            int t = Tile(s, inside, tx, top, tile, UiRole.None, false);
            Icon(s, inside.In(t), tx + (tile - UiTokens.S8) / 2f, top + (tile - UiTokens.S8) / 2f, UiTokens.S8, icon, UiRole.None, UiFlags.Middle | UiFlags.Centre, true);
            Empty(s, inside, x, top + tile + UiTokens.S4, w, words, title, body);
        }

        /// <summary>A target that takes no press now reads at this share of its colours, in its place, so nothing moves.</summary>
        public const float DimAlpha = 0.4f;

        /// <summary>
        /// Dims what was drawn since an item's index and takes the press from a target: it stays in its place, so a row or a
        /// bar never moves, reads quieter and is passed by the d-pad (the phone's end arrows dim the same way).
        /// </summary>
        public static void Dim(UiScene s, int fromItem, int node = -1)
        {
            for (int i = fromItem; i < s.ItemCount; i++)
            {
                s.Items[i].Flags |= UiFlags.Disabled;
                s.Items[i].Alpha = DimAlpha;
            }
            if (node >= 0)
            {
                s.Nodes[node].Disabled = true;
            }
        }

        /// <summary>
        /// A value that is a link, at the right of a row: the magnifier <see cref="LinkGap"/> before the drawn words, both in
        /// the cyan, the hover's face round the two, the words a target that opens the thing or moves the camera.
        /// </summary>
        public static void LinkValue(UiScene s, UiAt at, float x, float y, float h, float w, string value, UiStyle style, int command, int arg)
        {
            float room = w - UiTokens.IconSmall - LinkGap;
            string shown = UiTextRules.Cut(value, style, room);
            float words = UiTextRules.Width(shown, style);
            float fw = Snap(words + LinkGap + UiTokens.IconSmall + UiTokens.S1 * 2f), fx = x + w + UiTokens.S1 - fw;
            int face = Box(s, at, fx, y, fw, h, UiRole.None, UiRole.None, UiTokens.RadiusMedium, UiFlags.Right);
            UiAt inside = at.In(face);
            Icon(s, inside, x + w - words - LinkGap - UiTokens.IconSmall, y + (h - UiTokens.IconSmall) / 2f, UiTokens.IconSmall, UiIcon.Search, UiRole.AccentText,
                UiFlags.Middle | UiFlags.Decor);
            TextMiddle(s, inside, x + w - room, y, h, room, style, UiRole.AccentText, shown, UiFlags.Right | UiFlags.Truncate);
            Node(s, at, x - UiTokens.S1, y, w + UiTokens.S1 * 2f, h, command, arg, face);
        }

        /// <summary>
        /// A row that is a link: its words in the cyan at the left and the arrow <see cref="LinkGap"/> after them as drawn,
        /// the whole row a target. A pair row high among an info panel's rows; a control high under a block of a window.
        /// Returns its height.
        /// </summary>
        public static float LinkRow(UiScene s, UiAt at, float x, float y, float w, float h, string words, int command, int arg, bool bleed = true)
        {
            // The hover's fill bleeds a step past the words either side, so they stand flush with their section's label; inside
            // a scroll frame, whose edge would cut that, it keeps to the row.
            float fx = bleed ? x - UiTokens.S2 : x, fw = bleed ? w + UiTokens.S4 : w;
            int row = Box(s, at, fx, y, fw, h, UiRole.None, UiRole.None, UiTokens.RadiusLarge);
            UiAt inside = at.In(row);
            float room = w - UiTokens.IconSmall - LinkGap;
            string shown = UiTextRules.Cut(words, UiStyle.BodyStrong, room);
            float drawn = UiTextRules.Width(shown, UiStyle.BodyStrong);
            TextMiddle(s, inside, x, y, h, Math.Min(Snap(drawn + UiTokens.S1), room), UiStyle.BodyStrong, UiRole.AccentText, shown, UiFlags.Truncate);
            Icon(s, inside, x + drawn + LinkGap, y + (h - UiTokens.IconSmall) / 2f, UiTokens.IconSmall, UiIcon.Next, UiRole.AccentText, UiFlags.Middle | UiFlags.Share | UiFlags.Decor);
            Node(s, at, fx, y, fw, h, command, arg, row);
            return h;
        }

        /// <summary>
        /// Two columns of a page joined for the d-pad: Right from a target whose middle stands between <paramref name="left"/>
        /// and <paramref name="split"/> goes to the target between <paramref name="split"/> and <paramref name="right"/>
        /// nearest its height, and Left back the same way, so a list and the thread beside it (or a sidebar of groups and
        /// their rows) are crossed in one step, never by way of the window's close; <paramref name="rightCommand"/>, when given,
        /// is the only kind of target Right lands on (a group's rows, not the search over them). Targets with links of their own keep them.
        /// </summary>
        public static void JoinColumns(UiScene s, float left, float split, float right, int rightCommand = -1)
        {
            for (int i = 0; i < s.NodeCount; i++)
            {
                ref UiNode n = ref s.Nodes[i];
                s.NodeBox(i, out float nx, out float ny, out float nw, out float nh);
                float mid = nx + nw / 2f;
                bool fromLeft = mid >= left && mid < split, fromRight = mid >= split && mid < right;
                if (n.Disabled || n.Command == CloseCommand || (!fromLeft && !fromRight) || (fromLeft ? n.Right : n.Left) >= 0
                    || (fromRight && rightCommand >= 0 && n.Command != rightCommand) || Inner(s, i, fromLeft ? left : split, fromLeft ? split : right, fromLeft))
                {
                    continue;
                }
                int best = -1;
                float bestGap = float.MaxValue;
                for (int j = 0; j < s.NodeCount; j++)
                {
                    ref UiNode c = ref s.Nodes[j];
                    s.NodeBox(j, out float cx, out float cy, out float cw, out float ch);
                    float cmid = cx + cw / 2f;
                    bool other = fromLeft ? cmid >= split && cmid < right : cmid >= left && cmid < split;
                    if (!other || c.Disabled || c.Command == CloseCommand || c.Layer != n.Layer || (fromLeft && rightCommand >= 0 && c.Command != rightCommand))
                    {
                        continue;
                    }
                    if (c.Clip >= 0 && (cy + ch <= s.Clips[c.Clip].Y + 0.5f || cy >= s.Clips[c.Clip].Y + s.Clips[c.Clip].H - 0.5f))
                    {
                        continue;   // a row scrolled out of view is no place to land
                    }
                    // By height first, then by distance from the crossing, so of a field and its button the field comes first.
                    float gap = Math.Abs((cy + ch / 2f) - (ny + nh / 2f)) * 4f + Math.Abs((fromLeft ? cx : split - (cx + cw)) - (fromLeft ? split : 0f)) / 64f;
                    if (gap < bestGap)
                    {
                        bestGap = gap;
                        best = j;
                    }
                }
                if (fromLeft)
                {
                    n.Right = best;
                }
                else
                {
                    n.Left = best;
                }
            }
        }

        // Whether a target has another of its own column beside it on the side the join crosses: only the column's edge crosses.
        private static bool Inner(UiScene s, int node, float from, float to, bool towardsRight)
        {
            s.NodeBox(node, out float nx, out float ny, out float nw, out float nh);
            for (int j = 0; j < s.NodeCount; j++)
            {
                if (j == node || s.Nodes[j].Disabled || s.Nodes[j].Layer != s.Nodes[node].Layer)
                {
                    continue;
                }
                s.NodeBox(j, out float cx, out float cy, out float cw, out float ch);
                float mid = cx + cw / 2f;
                bool level = cy < ny + nh && cy + ch > ny;
                if (level && mid >= from && mid < to && (towardsRight ? mid > nx + nw / 2f + 0.5f : mid < nx + nw / 2f - 0.5f))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// A person's row, two pair rows high: their face in its ring (their mood, or none) with its left edge on the rows'
        /// labels, the name over a quiet line, and a value or a state's chip at the right. A target when it opens them.
        /// Returns the target, or -1 for a row to read.
        /// </summary>
        public static int PersonRow(UiScene s, UiAt at, float x, float y, float w, string name, float hue, UiRole ring, string sub, string value, string chip, UiRole chipTone,
            int command, int arg, bool target = true, bool chosen = false)
        {
            float h = PersonRowHeight;
            int face = Box(s, at, x - UiTokens.S2, y, w + UiTokens.S4, h, chosen ? UiRole.RowSel : UiRole.None, UiRole.None, UiTokens.RadiusLarge,
                chosen ? UiFlags.Selected : UiFlags.None);
            UiAt inside = at.In(face);
            float size = UiTokens.AvatarSmall;
            Portrait(s, inside, x, y + (h - size) / 2f, size, name, hue, ring);
            float right = x + w;
            if (!string.IsNullOrEmpty(chip))
            {
                float cw = Snap(UiTokens.S2 * 3f + UiTokens.StatusDot + UiTextRules.Width(chip, UiStyle.SmallStrong) + UiTokens.S1);
                right -= cw;
                Chip(s, inside, right, y + (h - UiTokens.Badge - UiTokens.S1) / 2f, chip, chipTone);
                right -= UiTokens.S3;
            }
            else if (!string.IsNullOrEmpty(value))
            {
                float vw = Snap(UiTextRules.Width(value, UiStyle.Body) + UiTokens.S1);
                right -= vw;
                TextMiddle(s, inside, right, y, h, vw, UiStyle.Body, chosen ? UiRole.Text : UiRole.Text2, value, UiFlags.Right);
                right -= UiTokens.S3;
            }
            float tx = x + size + UiTokens.S3, tw = right - tx;
            float nameH = UiTextRules.LineHeight(UiStyle.Title), subH = string.IsNullOrEmpty(sub) ? 0f : UiTextRules.LineHeight(UiStyle.Small);
            float top = y + (h - nameH - (subH > 0f ? UiTokens.S1 + subH : 0f)) / 2f;
            Text(s, inside, tx, top, tw, UiStyle.Title, UiRole.Text, name, UiFlags.Truncate | UiFlags.Middle);
            if (subH > 0f)
            {
                Text(s, inside, tx, top + nameH + UiTokens.S1, tw, UiStyle.Small, chosen ? UiRole.Text : UiRole.Text2, sub, UiFlags.Truncate | UiFlags.Middle);
            }
            return target ? Node(s, at, x - UiTokens.S2, y, w + UiTokens.S4, h, command, arg, face) : -1;
        }
    }
}
