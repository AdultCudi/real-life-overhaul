using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>One line of the objectives: what to do, whether it is done, how far along, and whether it has a place to go.</summary>
    public struct UiObjectiveLine
    {
        public string Text;        // "Deliver 12 boxes to Hudson Street"
        public bool Done;          // ticked
        public string Progress;    // "4 of 12" at its right; null for none
        public bool Locate;        // the line has a place: its locate link sets the destination

        // Appended with the HUD's screens (3 Nov 2026).
        public string Sub;         // a quieter line under the words ("Murray Hill, stop 2 of 3"); null for none
        public string Close;       // the line's own way out ("Cancel" on a delivery); null for none
        public bool HasShare;      // a meter under the line, filled to Share
        public float Share;
        public bool Urgent;        // an alert the game marks high: the warning glyph in the tick's place
    }

    /// <summary>A group of the objectives on its band: the game's to-dos, goals, quests, missions and workout, the mod's construction.</summary>
    public struct UiObjectiveGroup
    {
        public string Title;       // "To do"
        public UiIcon Icon;        // the group's glyph in its own colour
        public UiObjectiveLine[] Lines;
        public bool Collapsed;     // the group folded to its band's label

        // Appended with the HUD's screens (3 Nov 2026).
        public string Note;        // the band's quiet word at its right: how many, "1 of 3 done", a mission's pay; left out where the whole name needs its room; null for none
        public int LineCount;      // the lines in use when Lines is kept between layouts; 0 for all of Lines
    }

    /// <summary>The objectives panel: its groups, and whether the whole panel is folded to its title strip.</summary>
    public sealed class UiObjectivesModel
    {
        public string Title;                                       // the game's own words for its tasks window
        public UiObjectiveGroup[] Groups = new UiObjectiveGroup[0];
        public bool Collapsed;
        public string EmptyTitle, EmptyBody;                       // a useful sentence when nothing is set
        public UiIcon EmptyIcon = UiIcon.Trophy;                   // what would fill the panel, on the empty state's tile

        // Appended with the HUD's screens (3 Nov 2026).
        public string Meta;                                        // the strip's quiet word at its right: "12 open"
        public string LocateLabel;                                 // the locate link's word ("Locate"); null for the glyph alone
        public int GroupCount = -1;                                // the groups in use when Groups is kept between layouts; -1 for all
    }

    /// <summary>
    /// The objectives: a panel docked at the left over the running game, after the game's tasks window, with its title
    /// strip (its title in capitals in the cyan, how many are open and a chevron that folds it at the right), its groups on
    /// bands (each a glyph, its name in capitals and a quiet word, folding to its band), and each line behind a tick (a
    /// green check when done, an open ring while not), its words wrapping to two lines with a quieter line under them, how far
    /// along at its right or a locate link where the line has a place, and a meter with its way out under a mission's line.
    /// Commands: <see cref="FoldCommand"/> (the panel), <see cref="GroupCommand"/> (a group's index), <see cref="LineCommand"/>,
    /// <see cref="LocateCommand"/> and <see cref="CloseCommand"/> (group * 32 + line). Game-free. Cost: a layout a change.
    /// </summary>
    public static class UiObjectives
    {
        public const int FoldCommand = 220, GroupCommand = 221, LineCommand = 222, LocateCommand = 223, CloseCommand = 224;
        public const float Width = 360f;            // px: as a side panel, so the left and right columns match
        private const float Tick = UiTokens.IconSmall;
        private const float Head = UiTokens.ControlSmall;   // px: a group's band
        private const byte Region = 220;            // the panel's own column for the d-pad

        /// <summary>The panel's height, folded or open, for its words at its width.</summary>
        public static float Height(UiObjectivesModel m)
        {
            float h = UiFrame.Strip;
            if (m.Collapsed)
            {
                return h;
            }
            int groups = Groups(m);
            if (groups == 0)
            {
                return UiDraw.Snap(h + EmptyRoom(m));
            }
            for (int g = 0; g < groups; g++)
            {
                h += GroupHeight(m, ref m.Groups[g], g == groups - 1);
            }
            return UiDraw.Snap(h + UiTokens.S1);
        }

        private static int Groups(UiObjectivesModel m) => m.GroupCount < 0 ? m.Groups.Length : Math.Min(m.GroupCount, m.Groups.Length);

        private static int Lines(ref UiObjectiveGroup group) => group.Lines == null ? 0 : group.LineCount <= 0 ? group.Lines.Length : Math.Min(group.LineCount, group.Lines.Length);

        // The empty state's room: its glyph's tile and its words with a step of room round them, so nothing floats in a hole.
        private static float EmptyRoom(UiObjectivesModel m) => UiDraw.EmptyStateHeight(Width - UiTokens.S4 * 2f, m.EmptyTitle, m.EmptyBody, null) + UiTokens.S5 * 2f;

        // A folded band keeps a step of the panel under it before the next band, so two bands' names never read as one heading.
        private const float FoldedGap = UiTokens.S3;

        private static float GroupHeight(UiObjectivesModel m, ref UiObjectiveGroup group, bool last)
        {
            float h = Head;
            if (group.Collapsed)
            {
                return h + (last ? 0f : FoldedGap);
            }
            int lines = Lines(ref group);
            for (int i = 0; i < lines; i++)
            {
                h += LineHeight(m, ref group.Lines[i]);
            }
            return h + (lines > 0 ? UiTokens.S1 : 0f);
        }

        // A line's words wrap to two lines in the room its tick, its figure and its link leave them; its quieter line and its
        // meter's row under them.
        private static float LineHeight(UiObjectivesModel m, ref UiObjectiveLine line)
        {
            float words = UiTextRules.WrappedHeight(line.Text, UiStyle.Body, TextRoom(m, ref line), 2);
            if (!string.IsNullOrEmpty(line.Sub))
            {
                words += UiTextRules.LineHeight(UiStyle.Small);
            }
            float h = Math.Max(UiTokens.MinTarget + UiTokens.S2, UiDraw.Snap(words + UiTokens.S2));
            if (!string.IsNullOrEmpty(line.Close) || line.HasShare)
            {
                h += UiTokens.ControlSmall + UiTokens.S1;
            }
            return h;
        }

        private static float TextRoom(UiObjectivesModel m, ref UiObjectiveLine line) =>
            Width - UiTokens.S4 * 2f - Tick - UiTokens.S3 - RightWidth(m, ref line) - UiTokens.S2;

        // What stands at a line's right: its figure, then its locate link.
        private static float RightWidth(UiObjectivesModel m, ref UiObjectiveLine line)
        {
            float w = 0f;
            if (!string.IsNullOrEmpty(line.Progress))
            {
                w += UiDraw.Snap(UiTextRules.Width(line.Progress, UiStyle.Small) + UiTokens.S1);
            }
            if (line.Locate)
            {
                w += (w > 0f ? UiTokens.S2 : 0f) + LocateWidth(m);
            }
            return w;
        }

        private static float LocateWidth(UiObjectivesModel m) => string.IsNullOrEmpty(m.LocateLabel)
            ? UiTokens.MinTarget
            : UiDraw.Snap(UiTokens.S1 + UiTextRules.Width(m.LocateLabel, UiStyle.Small) + UiTokens.S1 + UiTokens.IconSmall + UiTokens.S1);

        /// <summary>Lays the panel at its top left (px of the layout); returns the y under it.</summary>
        public static float Lay(UiScene s, UiAt at, UiObjectivesModel m, float x, float y)
        {
            at = at.InRegion(Region);
            float h = Height(m);
            int panel = UiDraw.Box(s, at, x, y, Width, h, UiRole.Panel, UiRole.None, UiTokens.RadiusPanel, UiFlags.Panel);
            UiAt on = at.In(panel);
            int fold = UiSidePanel.Strip(s, on, x, y, Width, m.Title, m.Meta, m.Collapsed, FoldCommand, 0);
            s.Nodes[fold].Left = s.Nodes[fold].Right = fold;   // the panel's corner: sideways leads nowhere unless the HUD joins a panel beside it
            if (m.Collapsed)
            {
                return y + h;
            }
            float cy = y + UiFrame.Strip;
            float ix = x + UiTokens.S4, iw = Width - UiTokens.S4 * 2f;
            int groups = Groups(m);
            if (groups == 0)
            {
                UiDraw.EmptyState(s, on, ix, cy, iw, EmptyRoom(m), m.EmptyIcon, m.EmptyTitle, m.EmptyBody, null, -1, UiDefaultKeys.Keyboard);
                return y + h;
            }
            for (int g = 0; g < groups; g++)
            {
                cy = Group(s, on, m, ref m.Groups[g], g, x, cy, ix, iw, g == groups - 1);
            }
            return y + h;
        }

        // A group: its band (its glyph, its name in capitals, its quiet word and the chevron, the whole band a target that
        // folds it), then its lines on the panel.
        private static float Group(UiScene s, UiAt at, UiObjectivesModel m, ref UiObjectiveGroup group, int index, float x, float y, float ix, float iw, bool last)
        {
            int band = UiDraw.Box(s, at, x, y, Width, Head, UiRole.Band, UiRole.None, 0f, UiFlags.Decor);
            UiAt inside = at.In(band);
            int face = UiDraw.Box(s, inside, x, y, Width, Head, UiRole.None, UiRole.None, 0f);
            UiAt onFace = inside.In(face);
            UiDraw.Icon(s, onFace, ix, y + (Head - UiTokens.IconSmall) / 2f, UiTokens.IconSmall, group.Icon, UiRole.None, UiFlags.Middle);
            float chevron = ix + iw - UiTokens.IconSmall;
            UiDraw.Icon(s, onFace, chevron, y + (Head - UiTokens.IconSmall) / 2f, UiTokens.IconSmall, group.Collapsed ? UiIcon.Down : UiIcon.Up, UiRole.Text2, UiFlags.Middle);
            float tx = ix + UiTokens.IconSmall + UiTokens.S2;
            float nw = string.IsNullOrEmpty(group.Note) ? 0f : Math.Min(iw * 0.45f, UiDraw.Snap(UiTextRules.Width(group.Note, UiStyle.Small) + UiTokens.S1));
            if (nw > 0f && tx + UiTextRules.Width(group.Title, UiStyle.SectionHeader) + UiTokens.S3 + nw > chevron - UiTokens.S2)
            {
                nw = 0f;   // the name wins: the quiet word stands only where the whole name leaves it room
            }
            if (nw > 0f)
            {
                UiDraw.TextMiddle(s, onFace, chevron - UiTokens.S2 - nw, y, Head, nw, UiStyle.Small, UiRole.Text2, group.Note, UiFlags.Right | UiFlags.Truncate);
            }
            float tw = chevron - UiTokens.S2 - (nw > 0f ? nw + UiTokens.S3 : 0f) - tx;
            UiDraw.TextMiddle(s, onFace, tx, y, Head, tw, UiStyle.SectionHeader, UiRole.Text2, group.Title, UiFlags.Label | UiFlags.Truncate);
            int header = UiDraw.Node(s, at, x, y, Width, Head, GroupCommand, index, face);
            s.Nodes[header].Left = s.Nodes[header].Right = header;   // a band's whole width: sideways leads nowhere
            float ly = y + Head;
            int lines = group.Collapsed ? 0 : Lines(ref group);
            for (int i = 0; i < lines; i++)
            {
                ly += Line(s, at, m, ref group.Lines[i], index * 32 + i, ix, ly, iw);
            }
            return ly + (lines > 0 ? UiTokens.S1 : group.Collapsed && !last ? FoldedGap : 0f);
        }

        // A line behind its tick: the words (a target, as the game's task opens where it leads) with the quieter line under
        // them, its figure, its locate link; under them a mission's meter and its way out.
        private static float Line(UiScene s, UiAt at, UiObjectivesModel m, ref UiObjectiveLine line, int arg, float x, float y, float w)
        {
            float h = LineHeight(m, ref line);
            bool foot = !string.IsNullOrEmpty(line.Close) || line.HasShare;
            float top = foot ? h - UiTokens.ControlSmall - UiTokens.S1 : h;   // the words' own row, the foot's under it
            float right = x + w;
            int locate = -1;
            if (line.Locate)
            {
                float lw = LocateWidth(m);
                right -= lw;
                if (string.IsNullOrEmpty(m.LocateLabel))
                {
                    locate = UiDraw.IconButton(s, at, right, y + UiDraw.SnapDown((top - UiTokens.MinTarget) / 2f), UiTokens.MinTarget, UiIcon.Locate, UiRole.None, LocateCommand, arg,
                        false, UiAction.Count, false, true);
                }
                else
                {
                    float lh = UiTokens.MinTarget, ly = y + UiDraw.SnapDown((top - lh) / 2f);
                    int face = UiDraw.Box(s, at, right, ly, lw, lh, UiRole.None, UiRole.None, UiTokens.RadiusMedium, UiFlags.Right);
                    UiAt inside = at.In(face);
                    float ww = UiTextRules.Width(m.LocateLabel, UiStyle.Small);
                    UiDraw.TextMiddle(s, inside, right + UiTokens.S1, ly, lh, UiDraw.Snap(ww + UiTokens.S1), UiStyle.Small, UiRole.AccentText, m.LocateLabel, UiFlags.Truncate);
                    UiDraw.Icon(s, inside, right + UiTokens.S1 + ww + UiDraw.LinkGap, ly + (lh - UiTokens.IconSmall) / 2f, UiTokens.IconSmall, UiIcon.Next, UiRole.AccentText,
                        UiFlags.Middle | UiFlags.Decor);
                    locate = UiDraw.Node(s, at, right, ly, lw, lh, LocateCommand, arg, face);
                }
                right -= UiTokens.S2;
            }
            if (!string.IsNullOrEmpty(line.Progress))
            {
                float pw = UiDraw.Snap(UiTextRules.Width(line.Progress, UiStyle.Small) + UiTokens.S1);
                UiDraw.TextMiddle(s, at, right - pw, y, top, pw, UiStyle.Small, UiRole.Text2, line.Progress, UiFlags.Right);
                right -= pw + UiTokens.S2;
            }
            UiDraw.Icon(s, at, x, y + (top - Tick) / 2f, Tick, line.Done ? UiIcon.Check : line.Urgent ? UiIcon.Warning : UiIcon.Ring, UiRole.None, UiFlags.Middle);
            float tx = x + Tick + UiTokens.S3;
            float tw = right - tx;
            int words = UiDraw.Box(s, at, tx - UiTokens.S1, y, tw + UiTokens.S1, top, UiRole.None, UiRole.None, UiTokens.RadiusLarge);
            UiAt on = at.In(words);
            bool sub = !string.IsNullOrEmpty(line.Sub);
            float lhWords = UiTextRules.WrappedHeight(line.Text, UiStyle.Body, tw, 2);
            float block = lhWords + (sub ? UiTextRules.LineHeight(UiStyle.Small) : 0f);
            float ty = y + (top - block) / 2f;
            UiRole ink = line.Done ? UiRole.Text2 : UiRole.Text;
            if (!sub && lhWords <= UiTextRules.LineHeight(UiStyle.Body) + 0.5f)
            {
                // One line sits on the line its tick, figure and locate share, centred as the kit centres a row's words.
                UiDraw.TextMiddle(s, on, tx, y, top, tw, UiStyle.Body, ink, line.Text, UiFlags.Truncate);
            }
            else
            {
                UiDraw.Text(s, on, tx, ty, tw, UiStyle.Body, ink, line.Text, UiFlags.Wrap | UiFlags.Middle, 2);
                if (sub)
                {
                    UiDraw.Text(s, on, tx, ty + lhWords, tw, UiStyle.Small, UiRole.Text2, line.Sub, UiFlags.Truncate | UiFlags.Middle);
                }
            }
            int node = UiDraw.Node(s, at, tx - UiTokens.S1, y, tw + UiTokens.S1, top, LineCommand, arg, words);
            s.Nodes[node].Left = node;
            s.Nodes[node].Right = locate >= 0 ? locate : node;
            if (locate >= 0)
            {
                s.Nodes[locate].Left = node;
                s.Nodes[locate].Right = locate;
            }
            if (foot)
            {
                // The mission's meter at the left and its way out at the right, under the words.
                float fy = y + top;
                float cw = string.IsNullOrEmpty(line.Close) ? 0f : UiDraw.ButtonWidth(UiButtonKind.Destructive, line.Close, UiIcon.None, UiAction.Count, null);
                if (line.HasShare)
                {
                    float mw = w - Tick - UiTokens.S3 - (cw > 0f ? cw + UiTokens.S3 : 0f);
                    float my = fy + (UiTokens.ControlSmall - UiTokens.S2) / 2f;
                    int track = UiDraw.Box(s, at, tx, my, mw, UiTokens.S2, UiRole.Track, UiRole.None, UiTokens.S1, UiFlags.Middle);
                    float share = Math.Max(0f, Math.Min(1f, line.Share));
                    if (share > 0f)
                    {
                        UiDraw.Box(s, at.In(track), tx, my, Math.Max(UiTokens.S2, mw * share), UiTokens.S2, UiRole.Accent, UiRole.None, UiTokens.S1, UiFlags.Middle | UiFlags.Share);
                    }
                }
                if (cw > 0f)
                {
                    int close = UiDraw.Button(s, at, x + w - cw, fy, cw, UiTokens.ControlSmall, UiButtonKind.Destructive, line.Close, UiIcon.None, UiAction.Count, null, CloseCommand, arg);
                    s.Nodes[close].Left = s.Nodes[close].Right = close;
                }
            }
            return h;
        }
    }
}
