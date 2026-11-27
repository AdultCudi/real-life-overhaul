using System;
using System.Globalization;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// Where the needs' bars stand along the top of the screen when the player's option puts them there (UiStatusModel.NeedsTop): the size they are drawn at, how
    /// many rows they take and the box of one. Height is 0 when no row stands.
    /// </summary>
    public struct UiNeedsRow
    {
        public int Level;         // 1 to 4: the size the row is drawn at, the option's own or the largest one that fits two rows
        public int Rows, PerRow;  // one row, or two when the needs do not fit one; the cards on a row
        public float X;           // px: the row's left edge, where the portrait stands
        public float CellX;       // px: the first card's left edge, after the portrait
        public float CellW, CellH;
        public float Height;      // px: the rows with the gap between them; 0 for none
    }

    /// <summary>
    /// The needs' bars at a size the player asked for (the "needs bars" option of the RLO interface). Size 0 is the bar's small cells as they always were. Sizes 1 to 4 draw
    /// each need as a glyph, its figure and a thicker meter, and from size 3 its words in the bar; in a row of their own along the top of the screen (the option's second
    /// place) every size carries the words, each need a card of its own, the row as wide as the screen and on two rows where one is too short. A size too large for the room
    /// steps down to the one that fits, never off the screen. Game-free. Cost: a few measures a layout.
    /// </summary>
    public static partial class UiDraw
    {
        /// <summary>The largest size of the needs' bars; 0 is the bar's own small cells.</summary>
        public const int NeedLevels = 4;

        private const byte NeedRegion = 244;                 // the top row's targets, a column of their own for the d-pad
        private const float NeedPadX = UiTokens.S3;         // px: a top card's room at either end
        private const float NeedPadY = UiTokens.S3;         // px: ... and above and below
        private const float NeedBarGap = UiTokens.S4;       // px: between two needs in the bar
        private const float NeedRowGap = UiTokens.S2;       // px: between two cards, along a row and between the two rows

        private struct NeedLook
        {
            public float Glyph, Meter;
            public UiStyle Label, Value;
        }

        // Size 1 to 4 (index 0 is unused: size 0 is the bar's own cell): the glyph, the meter's thickness and the type of the words and the figure.
        private static readonly NeedLook[] NeedLooks =
        {
            default(NeedLook),
            new NeedLook { Glyph = 24f, Meter = 8f, Label = UiStyle.Body, Value = UiStyle.Number },
            new NeedLook { Glyph = 28f, Meter = 12f, Label = UiStyle.Number, Value = UiStyle.Number },
            new NeedLook { Glyph = 32f, Meter = 16f, Label = UiStyle.Hero, Value = UiStyle.Hero },
            new NeedLook { Glyph = 36f, Meter = 20f, Label = UiStyle.Display, Value = UiStyle.Display }
        };

        private static readonly UiIcon[] NeedIcons = { UiIcon.Lightning, UiIcon.ForkKnife, UiIcon.SmileyHappy, UiIcon.Drop, UiIcon.Brain, UiIcon.Home, UiIcon.Heartbeat };

        private static int NeedValue(UiStatusModel m, int i)
        {
            switch (i)
            {
                case 0: return m.Energy;
                case 1: return m.Hunger;
                case 2: return m.Happiness;
                case 3: return m.Hygiene;
                case 4: return m.Stress;
                case 5: return m.HomeNeed;
                default: return m.Fitness;
            }
        }

        private static string NeedLabel(UiStatusModel m, int i) => m.NeedLabels != null && i < m.NeedLabels.Length ? m.NeedLabels[i] : null;

        private static int NeedLevelOf(UiStatusModel m) => Math.Max(0, Math.Min(NeedLevels, m.NeedsSize));

        private static float NeedFigureWidth(int level) => Words("100", NeedLooks[level].Value);

        private static float NeedLineHeight(int level) =>
            Math.Max(NeedLooks[level].Glyph, Math.Max(UiTextRules.LineHeight(NeedLooks[level].Value), UiTextRules.LineHeight(NeedLooks[level].Label)));

        // The widest of the present needs' words at a size; 0 for none.
        private static float NeedWordsWidth(UiStatusModel m, int level)
        {
            float widest = 0f;
            for (int i = 0; i < NeedIcons.Length; i++)
            {
                if (NeedValue(m, i) >= 0)
                {
                    widest = Math.Max(widest, Words(NeedLabel(m, i), NeedLooks[level].Label));
                }
            }
            return widest;
        }

        // ------------------------------------------------------------------------------------------------- in the bar

        // A need's cell in the bar at a size 1 to 4, one width for all: the glyph, the figure and from size 3 the words between, the meter under all of it.
        private static float BarNeedCell(UiStatusModel m, int level)
        {
            float words = level >= 3 ? NeedWordsWidth(m, level) : 0f;
            return Snap(NeedLooks[level].Glyph + UiTokens.S1 + (words > 0f ? words + UiTokens.S2 : 0f) + NeedFigureWidth(level));
        }

        // The needs' pill's width at a size: the bar's own cells at 0 (narrow where asked), the larger cells' at 1 to 4.
        private static float NeedsWidthAt(UiStatusModel m, int level, bool tight)
        {
            if (level <= 0)
            {
                return NeedsWidth(m, tight ? NeedTight : NeedCell);
            }
            int n = NeedCount(m);
            return n == 0 ? 0f : PillPad * 2f + PortraitRoom(m) + n * BarNeedCell(m, level) + (n - 1) * NeedBarGap;
        }

        // The larger needs in the bar's pill: each a glyph and its figure, from size 3 its words too, a meter as wide under them, all in the need's own colour.
        private static void NeedsPillLarge(UiScene s, UiAt at, float x, float y, float w, int level, UiStatusModel m)
        {
            int face = PillFace(s, at, x, y, w);
            UiAt inside = at.In(face);
            float cx = x + PillPad;
            if (PortraitRoom(m) > 0f)
            {
                Portrait(s, inside, cx, y + SnapDown((BarPill - UiTokens.AvatarRow) / 2f), UiTokens.AvatarRow, m.Player, UiColour.HueOf(m.Player), UiRole.None);
                cx += PortraitRoom(m);
            }
            NeedLook look = NeedLooks[level];
            float cell = BarNeedCell(m, level), line = NeedLineHeight(level), top = y + (BarPill - line - UiTokens.S1 - look.Meter) / 2f;
            bool words = level >= 3;
            for (int i = 0; i < NeedIcons.Length; i++)
            {
                if (NeedValue(m, i) < 0)
                {
                    continue;
                }
                NeedOne(s, inside, cx, top, cell, i, m, level, words, line);
                cx += cell + NeedBarGap;
            }
            PillTarget(s, at, x, y, w, NeedsPillCommand, face, m);
        }

        // One need: its glyph, then its words if asked, its figure at the right, and the meter under them.
        private static void NeedOne(UiScene s, UiAt at, float x, float y, float w, int need, UiStatusModel m, int level, bool words, float line)
        {
            NeedLook look = NeedLooks[level];
            int value = NeedValue(m, need);
            UiIcon icon = NeedIcons[need];
            Icon(s, at, x, y + (line - look.Glyph) / 2f, look.Glyph, icon, UiRole.None, UiFlags.Middle | UiFlags.Centre | UiFlags.Chrome);
            float figure = NeedFigureWidth(level);
            float tx = x + look.Glyph + (words ? UiTokens.S2 : UiTokens.S1);
            string label = words ? NeedLabel(m, need) : null;
            if (!string.IsNullOrEmpty(label))
            {
                float lh = UiTextRules.LineHeight(look.Label);
                Text(s, at, tx, y + (line - lh) / 2f, x + w - figure - UiTokens.S2 - tx, look.Label, UiRole.Text2, label, UiFlags.Middle | UiFlags.Truncate | UiFlags.Chrome);
            }
            float vh = UiTextRules.LineHeight(look.Value);
            Text(s, at, x + w - figure, y + (line - vh) / 2f, figure, look.Value, UiRole.Text, value.ToString(CultureInfo.InvariantCulture), UiFlags.Middle | UiFlags.Right | UiFlags.Chrome);
            Meter(s, at, x, y + line + UiTokens.S1, w, look.Meter, value, UiIconTints.Of(icon));
        }

        // ------------------------------------------------------------------------------------------------- the top row

        private static float TopCardHeight(int level) => Snap(NeedPadY * 2f + NeedLineHeight(level) + UiTokens.S1 + NeedLooks[level].Meter);

        // The room a card needs at a size: the glyph, the words, the figure and a gap between each, padded.
        private static float TopCardNeed(UiStatusModel m, int level)
        {
            float words = NeedWordsWidth(m, level);
            return Snap(NeedPadX * 2f + NeedLooks[level].Glyph + UiTokens.S2 + (words > 0f ? words + UiTokens.S3 : UiTokens.S2) + NeedFigureWidth(level));
        }

        /// <summary>
        /// The needs' row along the top of a screen of a width: at the option's size if the needs fit one row or two there, else at the largest size that does, down to 1; at
        /// size 1 on a screen too narrow even for two rows the cards share the width and their words end short. Not a row (Height 0) unless the model puts the needs there.
        /// </summary>
        public static UiNeedsRow PlanNeedsRow(UiStatusModel m, float width)
        {
            int n = m != null && m.NeedsTop ? NeedCount(m) : 0;
            if (n == 0)
            {
                return default(UiNeedsRow);
            }
            float room = width - BarEdge * 2f - PortraitRoom(m);
            int want = Math.Max(1, NeedLevelOf(m));
            for (int level = want; level >= 1; level--)
            {
                float need = TopCardNeed(m, level);
                for (int rows = 1; rows <= 2; rows++)
                {
                    int per = rows == 1 ? n : (n + 1) / 2;
                    if (per * need + (per - 1) * NeedRowGap <= room)
                    {
                        return Shape(m, level, rows, per, room);
                    }
                }
            }
            return Shape(m, 1, n > 1 ? 2 : 1, n > 1 ? (n + 1) / 2 : 1, room);
        }

        private static UiNeedsRow Shape(UiStatusModel m, int level, int rows, int per, float room)
        {
            var row = new UiNeedsRow { Level = level, Rows = rows, PerRow = per, X = BarEdge, CellX = BarEdge + PortraitRoom(m) };
            row.CellW = SnapDown((room - (per - 1) * NeedRowGap) / per);
            row.CellH = TopCardHeight(level);
            row.Height = rows * row.CellH + (rows - 1) * NeedRowGap;
            return row;
        }

        /// <summary>
        /// The row drawn from <paramref name="y"/>: the player's portrait at its head, then each need on a card of its own across the width, each a target that opens the
        /// persona as the needs' pill does. Left and Right walk the cards, row by row.
        /// </summary>
        public static void NeedsRow(UiScene s, UiAt at, UiNeedsRow r, UiStatusModel m, float y)
        {
            if (r.Height <= 0f)
            {
                return;
            }
            UiAt row = at.InRegion(NeedRegion);
            int first = s.NodeCount;
            float line = NeedLineHeight(r.Level);
            if (PortraitRoom(m) > 0f)
            {
                Portrait(s, row, r.X, y + SnapDown((r.CellH - UiTokens.AvatarRow) / 2f), UiTokens.AvatarRow, m.Player, UiColour.HueOf(m.Player), UiRole.None);
            }
            int c = 0;
            for (int i = 0; i < NeedIcons.Length; i++)
            {
                if (NeedValue(m, i) < 0)
                {
                    continue;
                }
                float x = r.CellX + (c % r.PerRow) * (r.CellW + NeedRowGap), cy = y + (c / r.PerRow) * (r.CellH + NeedRowGap);
                int face = Box(s, row, x, cy, r.CellW, r.CellH, UiRole.Pill, UiRole.None, UiTokens.RadiusLarge, UiFlags.Chrome | UiFlags.Middle);
                NeedOne(s, row.In(face), x + NeedPadX, cy + NeedPadY, r.CellW - NeedPadX * 2f, i, m, r.Level, true, line);
                if (m.PillsPress)
                {
                    Node(s, row, x, cy, r.CellW, r.CellH, NeedsPillCommand, c, face);
                }
                c++;
            }
            for (int n = first; n < s.NodeCount; n++)
            {
                s.Nodes[n].Left = n > first ? n - 1 : -1;
                s.Nodes[n].Right = n < s.NodeCount - 1 ? n + 1 : -1;
            }
        }
    }
}
