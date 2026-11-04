using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>What a row of an organisation tree is.</summary>
    public enum UiOrgKind : byte
    {
        Caption = 0,    // a capitals label over the rows that follow ("Owner", "Open posts"): not a target
        Person,         // somebody at a level: their portrait in its ring, their name, their role, a figure at the right
        Tile,           // a post or a group at a level: a glyph on a tile in place of a face
        Post,           // an open post: a dim tile with a plus, what it is for and a link at the right to fill it
        More            // "and 6 more": a quiet line under the rows that were cut
    }

    /// <summary>A row of an organisation tree: its level, who or what it is, and how it stands.</summary>
    public struct UiOrgNode
    {
        public UiOrgKind Kind;
        public int Level;            // 0 the owner, 1 an executive, 2 a manager, 3 and on their crew: each a step further in
        public string Name;          // a caption's words, a person's or a post's name, "and 6 more"
        public string Sub;           // the quiet line under the name: a role, a place
        public string Right;         // at the right: "9 staff", "Cashier", the link of a post ("Hire")
        public float Hue;            // the face's hue
        public UiRole Ring;          // the face's ring (the person's mood); None for none
        public UiIcon Icon;          // a tile's glyph
        public UiRole Tint;          // None for the glyph's own colour
        public bool HasKids;         // shows the chevron, and folds
        public bool Open;            // its rows below are shown
        public string Tip;           // what the chevron does ("Fold", "Unfold"), for a screen reader and the focus ring's prompt
    }

    /// <summary>What an organisation tree shows: its title and action, its rows, the one chosen and how far it has scrolled.</summary>
    public sealed class UiOrgTreeModel
    {
        public string Title;                 // "Who decides what", in capitals
        public string Action;                // "Collapse all", at the right of the title; null for none
        public UiOrgNode[] Nodes = new UiOrgNode[0];
        public int Chosen = -1;              // the row the page beside it shows, in the cyan at a fifth: an index into Nodes
        public string FootLabel;             // the link at the foot ("Open the policy centre"); null for none
        public UiIcon FootIcon = UiIcon.Sliders;
        public float Offset;                 // px scrolled; the block writes back the offset it took
        public int Reveal = -1;              // a row (a target's index) scrolled into view as it is laid out
        public bool FoldTargets = true;      // the chevron is a target of its own; false: it only shows the state, and the chosen row pressed again folds it (a page short of targets)
    }

    /// <summary>
    /// An organisation tree, a block of an app window's page or a panel: who decides what, as rows indented by level, each a portrait in its
    /// ring (or a glyph on a tile, or a dim plus for an open post), its name and a quiet line under it, and a figure at the right; a chevron
    /// at the left of a row with rows below it folds them and unfolds them; captions part the groups. The chosen row stands in the cyan at a
    /// fifth (never a bar or a border), the page beside it shows that one. The rows are one list that scrolls inside its room, so up and
    /// down walk the persons, tiles and posts in the order they are drawn (a caption or "and 6 more" is no stop), and only the rows in view are laid out.
    /// A press on the chosen row that has rows below it folds or unfolds them, so a pad reaches the chevron too.
    /// Commands from <see cref="First"/>: <see cref="Pick"/> (a row's index in the model's rows), <see cref="Fold"/> (the same, its chevron),
    /// <see cref="All"/> (the title's action) and <see cref="Foot"/> (the foot's link). Game-free. Cost: a layout a change, the rows in view only.
    /// </summary>
    public static class UiOrgTree
    {
        // Commands 7400 to 7409 are the tree's; the Company app's own begin at 7410 (CompanyFace).
        public const int First = 7400;
        public const int Pick = 0, Fold = 1, All = 2, Foot = 3;

        public const float PersonHeight = 52f;    // px: a face of 36, two lines
        public const float CrewHeight = 44f;      // px: a face of 28, one line (those a manager has on their staff)
        public const float CaptionHeight = 32f;   // px: a label over a group, and the quiet line under a cut list
        private const float Face = 36f, FaceSmall = 28f;
        private const float Indent = 20f;         // px a level moves in
        private const float Chevron = 20f;        // px: the fold's target and its glyph's box
        private const float RowGap = 0f;

        /// <summary>A row's height: a person or a tile at a level, a crew member a little less, a caption or a "more" line a label's.</summary>
        public static float RowHeight(ref UiOrgNode n)
        {
            switch (n.Kind)
            {
                case UiOrgKind.Caption:
                case UiOrgKind.More:
                    return CaptionHeight;
                default:
                    return n.Kind == UiOrgKind.Person && n.Level >= 3 ? CrewHeight : PersonHeight;
            }
        }

        /// <summary>Whether a row is a stop for the keys and the pointer.</summary>
        public static bool Stops(ref UiOrgNode n) => n.Kind == UiOrgKind.Person || n.Kind == UiOrgKind.Tile || n.Kind == UiOrgKind.Post;

        /// <summary>The height the title takes, or 0 without one.</summary>
        public static float HeadHeight(UiOrgTreeModel m) => string.IsNullOrEmpty(m.Title) ? 0f : UiTokens.Control + UiTokens.S1;

        /// <summary>The height the foot's link takes, or 0 without one.</summary>
        public static float FootHeight(UiOrgTreeModel m) => string.IsNullOrEmpty(m.FootLabel) ? 0f : UiTokens.Control + UiTokens.S2;

        /// <summary>The height of all the rows laid end to end.</summary>
        public static float Content(UiOrgTreeModel m)
        {
            float sum = 0f;
            for (int i = 0; i < m.Nodes.Length; i++)
            {
                sum += RowHeight(ref m.Nodes[i]) + RowGap;
            }
            return Math.Max(0f, sum - RowGap);
        }

        /// <summary>The row the k-th stop is (the stops are what the keys walk); -1 past the last.</summary>
        public static int RowOfStop(UiOrgTreeModel m, int stop)
        {
            int k = 0;
            for (int i = 0; i < m.Nodes.Length; i++)
            {
                if (Stops(ref m.Nodes[i]) && k++ == stop)
                {
                    return i;
                }
            }
            return -1;
        }

        /// <summary>The stop a row is, or -1 for a row that is none.</summary>
        public static int StopOfRow(UiOrgTreeModel m, int row)
        {
            int k = 0;
            for (int i = 0; i < m.Nodes.Length && i <= row; i++)
            {
                if (Stops(ref m.Nodes[i]))
                {
                    if (i == row)
                    {
                        return k;
                    }
                    k++;
                }
            }
            return -1;
        }

        /// <summary>How many rows are stops.</summary>
        public static int StopCount(UiOrgTreeModel m)
        {
            int k = 0;
            for (int i = 0; i < m.Nodes.Length; i++)
            {
                k += Stops(ref m.Nodes[i]) ? 1 : 0;
            }
            return k;
        }

        /// <summary>Lays the tree out in a box. Returns its rows' list, or -1 when it has none.</summary>
        public static short Lay(UiScene s, UiAt at, UiOrgTreeModel m, float x, float y, float w, float h, IUiKeys keys)
        {
            float ty = y;
            if (!string.IsNullOrEmpty(m.Title))
            {
                float aw = 0f;
                if (!string.IsNullOrEmpty(m.Action))
                {
                    aw = Math.Min(UiDraw.SnapDown(w * 0.5f), UiDraw.Snap(UiTextRules.Width(m.Action, UiStyle.BodyStrong) + UiTokens.S4));
                    TextLink(s, at, x + w - aw, ty, UiTokens.Control, aw, m.Action, First + All, 0);
                }
                UiDraw.TextMiddle(s, at, x, ty, UiTokens.Control, w - aw - UiTokens.S3, UiStyle.SectionHeader, UiRole.Text, m.Title, UiFlags.Label | UiFlags.Truncate);
                ty += HeadHeight(m);
            }
            float footH = FootHeight(m), bottom = y + h - footH;
            int rows = m.Nodes.Length;
            if (rows == 0)
            {
                m.Offset = 0f;
                FootLink(s, at, m, x, bottom, w, keys);
                return -1;
            }
            float viewH = Math.Max(PersonHeight, UiDraw.SnapDown(bottom - ty));
            float content = Content(m);
            float offset = m.Offset;
            if (m.Reveal >= 0)
            {
                int row = RowOfStop(m, m.Reveal);
                if (row >= 0)
                {
                    float top = Top(m, row), height = RowHeight(ref m.Nodes[row]);
                    offset = Math.Min(offset, top);
                    offset = Math.Max(offset, top + height - viewH);
                }
            }
            offset = Math.Max(0f, Math.Min(offset, Math.Max(0f, content - viewH)));
            m.Offset = offset;
            m.Reveal = -1;
            bool scrolls = content > viewH + 0.5f;
            float rw = scrolls ? w - UiTokens.S3 : w;
            short clip = s.AddClip(x, ty, w, Math.Min(viewH, content), offset);
            s.Clips[clip].ContentHeight = content;
            int stops = StopCount(m);
            int firstRow = 0, lastRow = rows - 1;
            float run = 0f;
            for (int i = 0; i < rows; i++)
            {
                float rh = RowHeight(ref m.Nodes[i]);
                if (run + rh <= offset)
                {
                    firstRow = i + 1;
                }
                if (run < offset + viewH)
                {
                    lastRow = i;
                }
                run += rh + RowGap;
            }
            firstRow = Math.Min(firstRow, rows - 1);
            int firstStop = 0, shown = 0;
            for (int i = 0; i < rows; i++)
            {
                if (!Stops(ref m.Nodes[i]))
                {
                    continue;
                }
                if (i < firstRow)
                {
                    firstStop++;
                }
                else if (i <= lastRow)
                {
                    shown++;
                }
            }
            int chosenStop = m.Chosen >= 0 && m.Chosen < rows ? StopOfRow(m, m.Chosen) : -1;
            short list = s.AddList(stops, firstStop, shown, clip, chosenStop);
            var inClip = new UiAt { Parent = -1, Clip = clip, Layer = at.Layer, Order = -1, Region = at.Region };
            float cy = Top(m, firstRow);
            int arriving = 0;
            for (int i = firstRow; i <= lastRow; i++)
            {
                float rh = RowHeight(ref m.Nodes[i]);
                int stop = Stops(ref m.Nodes[i]) ? StopOfRow(m, i) : -1;
                Row(s, inClip.Arriving(arriving++), m, i, cy, rw, list, stop, m.FoldTargets);
                cy += rh + RowGap;
            }
            UiDraw.ScrollThumb(s, at, x + w - UiTokens.S1, ty, Math.Min(viewH, content), offset, content);
            FootLink(s, at, m, x, bottom, w, keys);
            return list;
        }

        private static float Top(UiOrgTreeModel m, int row)
        {
            float sum = 0f;
            for (int i = 0; i < row && i < m.Nodes.Length; i++)
            {
                sum += RowHeight(ref m.Nodes[i]) + RowGap;
            }
            return sum;
        }

        private static void FootLink(UiScene s, UiAt at, UiOrgTreeModel m, float x, float y, float w, IUiKeys keys)
        {
            if (string.IsNullOrEmpty(m.FootLabel))
            {
                return;
            }
            float h = UiTokens.Control, fy = y + UiTokens.S2;
            int face = UiDraw.Box(s, at, x, fy, w, h, UiRole.None, UiRole.None, UiTokens.RadiusLarge);
            UiAt inside = at.In(face);
            float gx = x + UiTokens.S3, g = UiTokens.Icon;
            UiDraw.Icon(s, inside, gx, fy + (h - g) / 2f, g, m.FootIcon, UiRole.AccentText, UiFlags.Middle | UiFlags.Decor);
            float lx = gx + g + UiTokens.S2, room = x + w - lx - UiTokens.S3 - UiTokens.IconSmall;
            string shown = UiTextRules.Cut(m.FootLabel, UiStyle.BodyStrong, room);
            float drawn = UiTextRules.Width(shown, UiStyle.BodyStrong);
            UiDraw.TextMiddle(s, inside, lx, fy, h, Math.Min(UiDraw.Snap(drawn + UiTokens.S1), room), UiStyle.BodyStrong, UiRole.AccentText, shown, UiFlags.Truncate);
            UiDraw.Icon(s, inside, lx + drawn + UiDraw.LinkGap, fy + (h - UiTokens.IconSmall) / 2f, UiTokens.IconSmall, UiIcon.Next, UiRole.AccentText, UiFlags.Middle | UiFlags.Share | UiFlags.Decor);
            UiDraw.Node(s, at, x, fy, w, h, First + Foot, 0, face);
        }

        // A text that is a link at the right of a line: cyan words on a hover's face. (The magnifier marks a link that opens a thing; this one acts on the tree.)
        private static void TextLink(UiScene s, UiAt at, float x, float y, float h, float w, string words, int command, int arg)
        {
            int face = UiDraw.Box(s, at, x, y, w, h, UiRole.None, UiRole.None, UiTokens.RadiusLarge);
            UiAt inside = at.In(face);
            UiDraw.TextMiddle(s, inside, x, y, h, w, UiStyle.BodyStrong, UiRole.AccentText, words, UiFlags.Right | UiFlags.Truncate);
            UiDraw.Node(s, at, x, y, w, h, command, arg, face);
        }

        // One row at its place in the clip.
        private static void Row(UiScene s, UiAt at, UiOrgTreeModel m, int index, float y, float w, short list, int stop, bool foldTargets)
        {
            ref UiOrgNode n = ref m.Nodes[index];
            float h = RowHeight(ref n);
            if (n.Kind == UiOrgKind.Caption)
            {
                UiAppWindow.GroupCaption(s, at, UiTokens.S3, y + h - UiAppWindow.GroupLabel, w - UiTokens.S6, n.Name);
                return;
            }
            if (n.Kind == UiOrgKind.More)
            {
                float mx = UiTokens.S3 + Indent * (n.Level + 1) + (n.Level >= 3 ? 0f : Chevron);
                UiDraw.TextMiddle(s, at, mx, y, h, Math.Max(0f, w - mx - UiTokens.S3), UiStyle.Small, UiRole.Muted, n.Name, UiFlags.Truncate);
                return;
            }
            bool chosen = index == m.Chosen, post = n.Kind == UiOrgKind.Post;
            int face = UiDraw.Box(s, at, 0f, y, w, h, chosen ? UiRole.RowSel : UiRole.None, UiRole.None, UiTokens.RadiusLarge, chosen ? UiFlags.Selected : UiFlags.None);
            UiAt inside = at.In(face);
            float x = UiTokens.S3 + Indent * n.Level;
            float cw = Chevron;
            if (n.HasKids)
            {
                float cy = y + (h - cw) / 2f;
                UiDraw.Icon(s, inside, x + (cw - UiTokens.IconSmall) / 2f, cy + (cw - UiTokens.IconSmall) / 2f, UiTokens.IconSmall, n.Open ? UiIcon.Down : UiIcon.Next, UiRole.Muted,
                    UiFlags.Middle | UiFlags.Decor);
            }
            x += cw + UiTokens.S1;
            float size = n.Level >= 3 && n.Kind == UiOrgKind.Person ? FaceSmall : Face;
            float fy = y + (h - size) / 2f;
            if (n.Kind == UiOrgKind.Person)
            {
                UiDraw.Portrait(s, inside, x, fy, size, n.Name, n.Hue, n.Ring);
            }
            else
            {
                int t = UiDraw.Tile(s, inside, x, fy, size, post ? UiRole.Muted : n.Tint, false);
                float g = size >= Face ? UiTokens.Icon : UiTokens.IconSmall;
                UiDraw.Icon(s, inside.In(t), x + (size - g) / 2f, fy + (size - g) / 2f, g, post ? UiIcon.Plus : n.Icon, post ? UiRole.Muted : n.Tint, UiFlags.Middle | UiFlags.Centre, true);
            }
            float lx = x + size + UiTokens.S3, right = w - UiTokens.S3;
            float rw = string.IsNullOrEmpty(n.Right) ? 0f : Math.Min(UiDraw.SnapDown(w * 0.34f), UiDraw.Snap(UiTextRules.Width(n.Right, post ? UiStyle.BodyStrong : UiStyle.Small) + UiTokens.S1));
            float lw = Math.Max(0f, right - rw - (rw > 0f ? UiTokens.S3 : 0f) - lx);
            bool two = !string.IsNullOrEmpty(n.Sub) && h >= PersonHeight;
            float th = UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Title)), sh = UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Small));
            if (two)
            {
                float top = y + UiDraw.SnapDown((h - th - sh) / 2f);
                UiDraw.TextMiddle(s, inside, lx, top, th, lw, UiStyle.Title, post && !chosen ? UiRole.Text2 : UiRole.Text, n.Name, UiFlags.Truncate);
                UiDraw.TextMiddle(s, inside, lx, top + th, sh, lw, UiStyle.Small, chosen ? UiRole.Text : UiRole.Text2, n.Sub, UiFlags.Truncate);
            }
            else
            {
                UiDraw.TextMiddle(s, inside, lx, y, h, lw, n.Level >= 3 ? UiStyle.Body : UiStyle.Title, UiRole.Text, n.Name, UiFlags.Truncate);
            }
            if (rw > 0f)
            {
                UiDraw.TextMiddle(s, inside, right - rw, y, h, rw, post ? UiStyle.BodyStrong : UiStyle.Small, post ? UiRole.AccentText : chosen ? UiRole.Text : UiRole.Text2, n.Right,
                    UiFlags.Right | UiFlags.Truncate);
            }
            int fold = -1;
            if (n.HasKids && foldTargets)
            {
                // The chevron is a target of its own over the row's, made first so that it keeps its own face: a press there folds, a press elsewhere on the row chooses it.
                float tw = Math.Max(cw, UiTokens.MinTarget), tx = UiTokens.S3 + Indent * n.Level - UiTokens.S1, tyy = y + UiDraw.SnapDown((h - tw) / 2f);
                int cface = UiDraw.Box(s, inside, tx, tyy, tw, tw, UiRole.None, UiRole.None, UiTokens.RadiusSmall);
                fold = UiDraw.Node(s, at, tx, tyy, tw, tw, First + Fold, index, cface);
            }
            int pick = UiDraw.Node(s, at, 0f, y, w, h, First + Pick, index, face, false, list, stop);
            if (fold >= 0)
            {
                s.Nodes[pick].Left = fold;    // the d-pad: Left from a row goes to its fold, Right from the fold back to the row
                s.Nodes[fold].Right = pick;
            }
        }

        /// <summary>
        /// The rows that stand after folding: <paramref name="folded"/> says per row of the full list whether its rows below are hidden. A row is shown
        /// when none of the rows it hangs from is folded; a caption always is. The full list is in order, each row's level telling whom it hangs from.
        /// </summary>
        public static int[] Visible(UiOrgNode[] full, bool[] folded)
        {
            var shown = new System.Collections.Generic.List<int>(full.Length);
            int hide = int.MaxValue;   // rows deeper than this level are hidden until a row at or above it
            for (int i = 0; i < full.Length; i++)
            {
                if (full[i].Kind == UiOrgKind.Caption)
                {
                    hide = int.MaxValue;
                    shown.Add(i);
                    continue;
                }
                if (full[i].Level <= hide)
                {
                    hide = int.MaxValue;
                }
                if (full[i].Level > hide)
                {
                    continue;
                }
                shown.Add(i);
                if (full[i].HasKids && i < folded.Length && folded[i])
                {
                    hide = full[i].Level;
                }
            }
            return shown.ToArray();
        }
    }
}
