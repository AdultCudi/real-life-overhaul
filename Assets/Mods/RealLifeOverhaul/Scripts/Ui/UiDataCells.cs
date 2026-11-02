using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The big table's cells that carry a picture beside their words (UiDataTable): a type's glyph on its tile with the
    /// type's name, a figure with the arrow of where it is heading, and a short ladder of steps (a goal's three tiers) as
    /// pips with the step's words. Each is a cell kind of its own (<see cref="UiCellKind.Tile"/>, <see cref="UiCellKind.Trend"/>,
    /// <see cref="UiCellKind.Steps"/>), so a table of words alone gets its pictures where the rows differ. Game-free.
    /// Cost: a few items a cell, laid out only for the rows in view.
    /// </summary>
    public static class UiDataCells
    {
        private const float Pip = 12f;   // px: a step's pip, on the grid's half step so three and their words fit a compact column

        /// <summary>The least a steps column takes for its pips and a short word beside them.</summary>
        public static float StepsWidth(int steps) => UiDraw.Snap(steps * Pip + (steps - 1) * UiTokens.S1 + UiTokens.S3 + UiTokens.S16);

        /// <summary>
        /// A type's glyph on its slate tile in the glyph's colour (Values: the UiIcon; Tones: a colour of its own, or None
        /// for the glyph's), then the type's name. A row whose glyph is None keeps the tile's room, so the names line up.
        /// </summary>
        public static void Tile(UiScene s, UiAt at, string words, int value, UiRole tint, float cx, float ry, float cw, float rowH, bool sel)
        {
            float tile = UiTokens.ControlSmall;
            var icon = (UiIcon)Math.Max(0, Math.Min((int)UiIcon.Count - 1, value));
            if (icon != UiIcon.None)
            {
                int t = UiDraw.Tile(s, at, cx, ry + (rowH - tile) / 2f, tile, tint, false);
                float g = UiTokens.Icon;
                UiDraw.Icon(s, at.In(t), cx + (tile - g) / 2f, ry + (rowH - g) / 2f, g, icon, tint, UiFlags.Middle | UiFlags.Centre, true);
            }
            float tx = cx + tile + UiTokens.S3;
            UiDraw.TextMiddle(s, at, tx, ry, rowH, cx + cw - tx, UiStyle.Body, sel ? UiRole.Text : UiRole.Text2, words, UiFlags.Truncate);
        }

        /// <summary>
        /// A figure at the right with the arrow of its direction before it (Values: 1 rising, -1 falling, 0 none): the arrow
        /// and its figure in one colour, good green up and bad red down, so the two never say different things; a figure
        /// with no direction stays the row's. On the chosen row's cyan the arrow takes the step of its colour that reads there.
        /// </summary>
        public static void Trend(UiScene s, UiAt at, string words, int value, float cx, float ry, float cw, float rowH, bool sel)
        {
            float g = UiTokens.IconSmall;
            float ww = Math.Min(cw - (value != 0 ? g + UiTokens.S1 : 0f), UiDraw.Snap(UiTextRules.Width(words ?? string.Empty, UiStyle.BodyStrong) + UiTokens.S1));
            UiRole ink = sel ? UiRole.Text : UiRole.Text2;
            if (value != 0)
            {
                // On the chosen row's cyan the bad red falls under 4.5:1: arrow and figure take the grade's step that reads there.
                UiRole tone = sel ? UiTokens.GradeOnChosen(value > 0 ? UiRole.Grade5 : UiRole.Grade1) : value > 0 ? UiRole.Ok : UiRole.Danger;
                ink = sel ? tone : value > 0 ? UiRole.OkText : UiRole.DangerText;
                UiDraw.Icon(s, at, cx + cw - ww - UiTokens.S1 - g, ry + (rowH - g) / 2f, g, value > 0 ? UiIcon.TrendUp : UiIcon.TrendDown, tone, UiFlags.Middle);
            }
            UiDraw.TextMiddle(s, at, cx + cw - ww, ry, rowH, ww, UiStyle.BodyStrong, ink, words, UiFlags.Right | UiFlags.Truncate);
        }

        /// <summary>
        /// A short ladder as pips (Values: the steps reached; the column's Steps: how many there are): those reached filled
        /// gold, as a medal is, the next one ringed in the cyan, the rest on the track; the step's words after them.
        /// </summary>
        public static void Steps(UiScene s, UiAt at, string words, int reached, int steps, float cx, float ry, float cw, float rowH, bool sel)
        {
            steps = Math.Max(1, steps);
            float py = ry + (rowH - Pip) / 2f;
            for (int i = 0; i < steps; i++)
            {
                float px = cx + i * (Pip + UiTokens.S1);
                bool done = i < reached, next = i == reached;
                UiDraw.Box(s, at, px, py, Pip, Pip, done ? UiRole.Gold : UiRole.Track, next ? UiRole.Accent : UiRole.None, Pip / 2f, UiFlags.Middle | UiFlags.Circle);
            }
            float tx = cx + steps * Pip + (steps - 1) * UiTokens.S1 + UiTokens.S3;
            if (tx < cx + cw)
            {
                UiDraw.TextMiddle(s, at, tx, ry, rowH, cx + cw - tx, UiStyle.Body, sel ? UiRole.Text : UiRole.Text2, words, UiFlags.Truncate);
            }
        }
    }
}
