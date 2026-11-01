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
    }

    /// <summary>A group of the objectives on its band: the game's to-dos, goals, quests, missions and workout, the mod's construction.</summary>
    public struct UiObjectiveGroup
    {
        public string Title;       // "To do"
        public UiIcon Icon;        // the group's glyph in its own colour
        public UiObjectiveLine[] Lines;
        public bool Collapsed;     // the group folded to its band's label
    }

    /// <summary>The objectives panel: its groups, and whether the whole panel is folded to its title strip.</summary>
    public sealed class UiObjectivesModel
    {
        public string Title;                                       // the game's own words for its tasks window
        public UiObjectiveGroup[] Groups = new UiObjectiveGroup[0];
        public bool Collapsed;
        public string EmptyTitle, EmptyBody;                       // a useful sentence when nothing is set
        public UiIcon EmptyIcon = UiIcon.Trophy;                   // what would fill the panel, on the empty state's tile
    }

    /// <summary>
    /// The objectives: a panel docked at the left over the running game, after the game's tasks window, with its title
    /// strip (a chevron folds it to the strip), its groups on bands (each a glyph and its name in capitals, folding to its
    /// band), and each line behind a tick (a green check when done, an open ring while not), its words wrapping to two lines,
    /// how far along at its right and a locate link where the line has a place. Commands: <see cref="FoldCommand"/> (the
    /// panel), <see cref="GroupCommand"/> (a group's index), <see cref="LineCommand"/> and <see cref="LocateCommand"/>
    /// (group * 32 + line). Game-free. Cost: a layout a change.
    /// </summary>
    public static class UiObjectives
    {
        public const int FoldCommand = 220, GroupCommand = 221, LineCommand = 222, LocateCommand = 223;
        public const float Width = 360f;            // px: as a side panel, so the left and right columns match
        private const float Tick = UiTokens.IconSmall;
        private const byte Region = 220;            // the panel's own column for the d-pad

        /// <summary>The panel's height, folded or open, for its words at its width.</summary>
        public static float Height(UiObjectivesModel m)
        {
            float h = UiFrame.Strip;
            if (m.Collapsed)
            {
                return h;
            }
            if (m.Groups.Length == 0)
            {
                return UiDraw.Snap(h + EmptyRoom(m));
            }
            for (int g = 0; g < m.Groups.Length; g++)
            {
                h += GroupHeight(ref m.Groups[g]) + UiTokens.S1;
            }
            return UiDraw.Snap(h + UiTokens.S2);
        }

        // The empty state's room: its glyph's tile and its words with a step of room round them, so nothing floats in a hole.
        private static float EmptyRoom(UiObjectivesModel m) =>
            UiDraw.Snap(UiTokens.S12 + UiTokens.S4 + UiDraw.EmptyHeight(Width - UiTokens.S4 * 2f, m.EmptyTitle, m.EmptyBody) - UiTokens.S12 * 2f + UiTokens.S5 * 2f);

        private static float GroupHeight(ref UiObjectiveGroup group)
        {
            float h = UiTokens.S2 + UiTokens.PairRow;
            if (group.Collapsed || group.Lines == null)
            {
                return h + UiTokens.S2;
            }
            for (int i = 0; i < group.Lines.Length; i++)
            {
                h += LineHeight(ref group.Lines[i]);
            }
            return h + UiTokens.S2;
        }

        // A line's words wrap to two lines in the room its tick, its figure and its link leave them.
        private static float LineHeight(ref UiObjectiveLine line) =>
            Math.Max(UiTokens.MinTarget + UiTokens.S2, UiDraw.Snap(UiTextRules.WrappedHeight(line.Text, UiStyle.Body, TextRoom(ref line), 2) + UiTokens.S2));

        private static float TextRoom(ref UiObjectiveLine line) =>
            Width - UiTokens.S4 * 2f - Tick - UiTokens.S3 - (string.IsNullOrEmpty(line.Progress) ? 0f : UiDraw.Snap(UiTextRules.Width(line.Progress, UiStyle.Small) + UiTokens.S1) + UiTokens.S3)
            - (line.Locate ? UiTokens.MinTarget + UiTokens.S2 : 0f);

        /// <summary>Lays the panel at its top left (px of the layout); returns the y under it.</summary>
        public static float Lay(UiScene s, UiAt at, UiObjectivesModel m, float x, float y)
        {
            at = at.InRegion(Region);
            float h = Height(m);
            int panel = UiDraw.Box(s, at, x, y, Width, h, UiRole.Panel, UiRole.None, UiTokens.RadiusPanel, UiFlags.Panel);
            UiAt on = at.In(panel);
            UiDraw.TitleStrip(s, on, x, y, Width, UiIcon.Check, m.Title, false);
            float b = UiTokens.MinTarget + UiTokens.S2;
            UiDraw.IconButton(s, on, x + Width - UiTokens.S2 - b, y + (UiFrame.Strip - b) / 2f, b, m.Collapsed ? UiIcon.Down : UiIcon.Up, UiRole.Text,
                FoldCommand, 0, false, UiAction.Count, false, true);
            if (m.Collapsed)
            {
                return y + h;
            }
            float cy = y + UiFrame.Strip;
            float ix = x + UiTokens.S4, iw = Width - UiTokens.S4 * 2f;
            if (m.Groups.Length == 0)
            {
                UiDraw.EmptyState(s, on, ix, cy, iw, EmptyRoom(m), m.EmptyIcon, m.EmptyTitle, m.EmptyBody);
                return y + h;
            }
            for (int g = 0; g < m.Groups.Length; g++)
            {
                cy = Group(s, on, ref m.Groups[g], g, x, cy, ix, iw) + UiTokens.S1;
            }
            return y + h;
        }

        // A group on its band: its glyph and name in capitals (a target that folds it), then its lines.
        private static float Group(UiScene s, UiAt at, ref UiObjectiveGroup group, int index, float x, float y, float ix, float iw)
        {
            float h = GroupHeight(ref group);
            UiDraw.Band(s, at, x, y, Width, h);
            float hy = y + UiTokens.S2;
            int face = UiDraw.Box(s, at, ix - UiTokens.S1, hy, iw + UiTokens.S2, UiTokens.PairRow, UiRole.None, UiRole.None, UiTokens.RadiusLarge);
            UiAt inside = at.In(face);
            UiDraw.Icon(s, inside, ix, hy + (UiTokens.PairRow - UiTokens.Icon) / 2f, UiTokens.Icon, group.Icon, UiRole.None, UiFlags.Middle);
            float tx = ix + UiTokens.Icon + UiTokens.S2;
            UiDraw.TextMiddle(s, inside, tx, hy, UiTokens.PairRow, iw - (tx - ix) - UiTokens.IconSmall - UiTokens.S2, UiStyle.SectionHeader, UiRole.Text, group.Title,
                UiFlags.Label | UiFlags.Truncate);
            UiDraw.Icon(s, inside, ix + iw - UiTokens.IconSmall, hy + (UiTokens.PairRow - UiTokens.IconSmall) / 2f, UiTokens.IconSmall, group.Collapsed ? UiIcon.Down : UiIcon.Up,
                UiRole.Text2, UiFlags.Middle);
            UiDraw.Node(s, at, ix - UiTokens.S1, hy, iw + UiTokens.S2, UiTokens.PairRow, GroupCommand, index, face);
            float ly = hy + UiTokens.PairRow;
            for (int i = 0; !group.Collapsed && group.Lines != null && i < group.Lines.Length; i++)
            {
                ly += Line(s, at, ref group.Lines[i], index * 32 + i, ix, ly, iw);
            }
            return y + h;
        }

        // A line behind its tick: the words (a target, as the game's task opens where it leads), its figure, its locate link.
        private static float Line(UiScene s, UiAt at, ref UiObjectiveLine line, int arg, float x, float y, float w)
        {
            float h = LineHeight(ref line);
            float right = x + w;
            if (line.Locate)
            {
                float b = UiTokens.MinTarget;
                right -= b;
                UiDraw.IconButton(s, at, right, y + UiDraw.SnapDown((h - b) / 2f), b, UiIcon.Locate, UiRole.None, LocateCommand, arg, false, UiAction.Count, false, true);
                right -= UiTokens.S2;
            }
            if (!string.IsNullOrEmpty(line.Progress))
            {
                float pw = UiDraw.Snap(UiTextRules.Width(line.Progress, UiStyle.Small) + UiTokens.S1);
                UiDraw.TextMiddle(s, at, right - pw, y, h, pw, UiStyle.Small, UiRole.Text2, line.Progress, UiFlags.Right);
                right -= pw + UiTokens.S3;
            }
            float ty = y + (h - Tick) / 2f;
            if (line.Done)
            {
                UiDraw.Icon(s, at, x, ty, Tick, UiIcon.Check, UiRole.None, UiFlags.Middle);
            }
            else
            {
                // An open ring: the line waits. A box's edge, so the tick and the ring are one size.
                UiDraw.Box(s, at, x + UiTokens.Hairline * 2f, ty + UiTokens.Hairline * 2f, Tick - UiTokens.S1, Tick - UiTokens.S1, UiRole.None, UiRole.Text2, (Tick - UiTokens.S1) / 2f,
                    UiFlags.Circle | UiFlags.Middle | UiFlags.Share);
            }
            float tx = x + Tick + UiTokens.S3;
            float tw = right - tx;
            int face = UiDraw.Box(s, at, tx - UiTokens.S1, y, tw + UiTokens.S1, h, UiRole.None, UiRole.None, UiTokens.RadiusLarge);
            float lh = UiTextRules.WrappedHeight(line.Text, UiStyle.Body, tw, 2);
            UiDraw.Text(s, at.In(face), tx, y + (h - lh) / 2f, tw, UiStyle.Body, line.Done ? UiRole.Text2 : UiRole.Text, line.Text, UiFlags.Wrap | UiFlags.Middle, 2);
            UiDraw.Node(s, at, tx - UiTokens.S1, y, tw + UiTokens.S1, h, LineCommand, arg, face);
            return h;
        }
    }
}
