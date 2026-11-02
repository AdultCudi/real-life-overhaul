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
        private static readonly float FigureRoom = UiDraw.Snap(UiTextRules.Width("+100%", UiStyle.BodyStrong) + UiTokens.S1);   // px: a signed share's words, the arrow before them
        private const float Segment = 40f;   // px: a step's segment of the bar, so three and their words fit a compact column

        /// <summary>The least a steps column takes for its bar and a short word beside it.</summary>
        public static float StepsWidth(int steps) => UiDraw.Snap(StepsBar(steps) + UiTokens.S3 + UiTokens.S16);

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
        /// A figure at the right with the arrow of its direction before it (Values: 1 rising, -1 falling, 0 none), the arrow
        /// a fixed step from the cell's right edge (a signed share's room), so a column of them keeps its arrows in one line
        /// beside their own figures, under the column's head. Arrow and figure share good or bad's colour for the player:
        /// rising is good unless the cell's tone says otherwise (Tones: Danger where a rise is bad, as a rival's is). A figure
        /// with no direction stays the row's. On the chosen row's cyan both take the step of their colour that reads there.
        /// </summary>
        public static void Trend(UiScene s, UiAt at, string words, int value, UiRole rise, float cx, float ry, float cw, float rowH, bool sel)
        {
            float g = UiTokens.IconSmall;
            float ww = Math.Min(cw - g - UiTokens.S2, UiDraw.Snap(UiTextRules.Width(words ?? string.Empty, UiStyle.BodyStrong) + UiTokens.S1));
            UiRole ink = sel ? UiRole.Text : UiRole.Text2;
            if (value != 0)
            {
                bool good = (value > 0) == (rise != UiRole.Danger && rise != UiRole.DangerText);
                // On the chosen row's cyan the bad red falls under 4.5:1: arrow and figure take the grade's step that reads there.
                UiRole tone = sel ? UiTokens.GradeOnChosen(good ? UiRole.Grade5 : UiRole.Grade1) : good ? UiRole.Ok : UiRole.Danger;
                ink = sel ? tone : good ? UiRole.OkText : UiRole.DangerText;
                float room = Math.Max(ww, FigureRoom);
                UiDraw.Icon(s, at, Math.Max(cx, cx + cw - room - UiTokens.S2 - g), ry + (rowH - g) / 2f, g, value > 0 ? UiIcon.TrendUp : UiIcon.TrendDown, tone, UiFlags.Middle);
            }
            UiDraw.TextMiddle(s, at, cx + cw - ww, ry, rowH, ww, UiStyle.BodyStrong, ink, words, UiFlags.Right | UiFlags.Truncate);
        }

        /// <summary>The bar a steps cell draws: its segments' width together, so the words after it line up down the column.</summary>
        public static float StepsBar(int steps) => steps * Segment + (steps - 1) * UiTokens.S1;

        /// <summary>
        /// A short ladder as a bar of segments, as a track of tiers (Values: the steps reached; the column's Steps: how many;
        /// the row's Share: the way through the next): those reached filled gold, as a medal is, the next filled cyan as far
        /// as it has got, the rest on the track; the words of the step reached after the bar.
        /// </summary>
        public static void Steps(UiScene s, UiAt at, string words, int reached, float share, int steps, float cx, float ry, float cw, float rowH, bool sel)
        {
            steps = Math.Max(1, steps);
            float h = UiTokens.S1, py = ry + (rowH - h) / 2f;
            for (int i = 0; i < steps; i++)
            {
                float px = cx + i * (Segment + UiTokens.S1);
                int track = UiDraw.Box(s, at, px, py, Segment, h, i < reached ? UiRole.Gold : UiRole.Track, UiRole.None, h / 2f, UiFlags.Middle | UiFlags.Chrome);
                float part = i == reached ? Math.Max(0f, Math.Min(1f, share)) : 0f;
                if (part > 0f)
                {
                    UiDraw.Box(s, at.In(track), px, py, Math.Max(h, UiDraw.Snap(Segment * part)), h, UiRole.Accent, UiRole.None, h / 2f, UiFlags.Middle | UiFlags.Share | UiFlags.Chrome);
                }
            }
            float tx = cx + StepsBar(steps) + UiTokens.S3;
            if (tx < cx + cw)
            {
                UiDraw.TextMiddle(s, at, tx, ry, rowH, cx + cw - tx, UiStyle.Body, sel ? UiRole.Text : UiRole.Text2, words, UiFlags.Truncate);
            }
        }
    }
}
