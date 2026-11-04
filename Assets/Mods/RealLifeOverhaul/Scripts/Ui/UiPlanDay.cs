using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>One thing coming up on a day's box: its hour, its kind's glyph, its words and whether it can be located.</summary>
    public struct UiPlanNext
    {
        public string Time;      // "09:30", "By 17:00"
        public UiIcon Icon;
        public UiRole Tint;      // None for the glyph's own
        public string Title;
        public string Sub;       // where it is ("Bloom and Co, Garment District"); null for none
        public bool Locate;      // a street or a point the guider can lead to
        public bool Warn;        // it clashes with another: its hour in the warning's colour
    }

    /// <summary>A stretch of free time: its hours and how long it is.</summary>
    public struct UiPlanSpan
    {
        public string Span;      // "11:00 to 12:30"
        public string Length;    // "1 h 30"
    }

    /// <summary>A day later in the week with something on it: its name and its first things.</summary>
    public struct UiPlanLater
    {
        public string Day;       // "Tue 3"
        public string Title;     // "Marketing class, Bloom and Co delivery"
    }

    /// <summary>What the day's box shows: what is next, the clash, the free time, the days after, what has no hour and the day's counts.</summary>
    public sealed class UiPlanDayModel
    {
        public string NextHead, NextMore, NextNone;           // "Up next", "4 more today", "Nothing more at a set hour"
        public UiPlanNext[] Next = new UiPlanNext[0];
        public string ClashLine;                              // "2 clashes today"; null for none
        public string FreeHead, FreeNone;                     // "Free time", "No free time left"
        public UiPlanSpan[] Free = new UiPlanSpan[0];
        public string LaterHead;                              // "Later this week"; null for none
        public UiPlanLater[] Later = new UiPlanLater[0];
        public string AllDayHead;                             // "All day"
        public string[] AllDay = new string[0];
        public string TotalsHead;                             // "The day"
        public UiPairModel[] Totals = new UiPairModel[0];     // "To do 6", "Done 1", "Missed 0", "Money due $4,200"
        public string LocateWord;                             // "Locate"
    }

    /// <summary>
    /// The Planner's day box, a block of an app window's page: what is next as rows (the hour, the kind's glyph, the name and
    /// where, the link that leads there), a line for a clash, the free time of the day as stretches of half an hour or more, the
    /// days after with their first things, what has no hour and the day's counts. Beside the board on Today and beside a day's
    /// entries on a day of the week. A section that has no room left is left out, last first. Commands: <see cref="RowCommand"/>
    /// (a next entry's index), <see cref="LocateCommand"/> (its link) and <see cref="LaterCommand"/> (a later day's index).
    /// Game-free. Cost: a layout a change.
    /// </summary>
    public static class UiPlanDay
    {
        public const int RowCommand = 1200, LocateCommand = 1201, LaterCommand = 1202;
        public const float Width = 360f;         // px: the box's column
        public const int MostNext = 5, MostLater = 3, MostAllDay = 3;
        private const float NextRow = 48f;       // px: a next entry: the hour, the glyph's tile, two lines
        private const float HourWidth = 52f;     // px: the hour column
        private const byte Region = 3;

        /// <summary>Lays the box into a column.</summary>
        public static void Lay(UiScene s, UiAt at, UiPlanDayModel m, float x, float y, float w, float h)
        {
            at = at.InRegion(Region);
            float foot = y + h, cy = y;
            float head = UiDraw.Snap(UiTextRules.LineHeight(UiStyle.SectionHeader)) + UiTokens.S2;
            cy = Header(s, at, m.NextHead, m.NextMore, x, cy, w);
            int n = Math.Min(m.Next.Length, MostNext);
            int shown = 0;
            for (int i = 0; i < n && cy + NextRow <= foot; i++)
            {
                Next(s, at.Arriving(shown++), ref m.Next[i], i, m.LocateWord, x, cy, w);
                cy += NextRow + UiTokens.S1;
            }
            if (n == 0)
            {
                cy += Quiet(s, at, m.NextNone, x, cy, w);
            }
            if (!string.IsNullOrEmpty(m.ClashLine) && cy + UiTokens.PairRow <= foot)
            {
                UiDraw.Icon(s, at, x, cy + (UiTokens.PairRow - UiTokens.IconSmall) / 2f, UiTokens.IconSmall, UiIcon.Warning, UiRole.Warn, UiFlags.Middle);
                UiDraw.TextMiddle(s, at, x + UiTokens.IconSmall + UiTokens.S2, cy, UiTokens.PairRow, w - UiTokens.IconSmall - UiTokens.S2, UiStyle.Body, UiRole.WarnText, m.ClashLine,
                    UiFlags.Label | UiFlags.Truncate);
                cy += UiTokens.PairRow + UiTokens.S2;
            }
            cy += UiTokens.S3;
            if (!string.IsNullOrEmpty(m.FreeHead) && cy + head + UiTokens.PairRow <= foot)
            {
                cy = Header(s, at, m.FreeHead, null, x, cy, w);
                if (m.Free.Length == 0)
                {
                    cy += Quiet(s, at, m.FreeNone, x, cy, w);
                }
                for (int i = 0; i < m.Free.Length && cy + UiTokens.PairRow <= foot; i++)
                {
                    cy += UiDraw.InfoRow(s, at, x, cy, w, m.Free[i].Span, m.Free[i].Length);
                }
                cy += UiTokens.S3;
            }
            if (m.Later.Length > 0 && !string.IsNullOrEmpty(m.LaterHead) && cy + head + UiTokens.PairRow <= foot)
            {
                cy = Header(s, at, m.LaterHead, null, x, cy, w);
                for (int i = 0; i < m.Later.Length && i < MostLater && cy + UiTokens.PairRow <= foot; i++)
                {
                    cy += Later(s, at, ref m.Later[i], i, x, cy, w);
                }
                cy += UiTokens.S3;
            }
            if (m.AllDay.Length > 0 && !string.IsNullOrEmpty(m.AllDayHead) && cy + head + UiTokens.PairRow <= foot)
            {
                cy = Header(s, at, m.AllDayHead, null, x, cy, w);
                for (int i = 0; i < m.AllDay.Length && i < MostAllDay && cy + UiTokens.PairRow <= foot; i++)
                {
                    UiDraw.TextMiddle(s, at, x, cy, UiTokens.PairRow, w, UiStyle.Body, UiRole.Text, m.AllDay[i], UiFlags.Label | UiFlags.Truncate);
                    cy += UiTokens.PairRow;
                }
                cy += UiTokens.S3;
            }
            if (m.Totals.Length > 0 && !string.IsNullOrEmpty(m.TotalsHead) && cy + head + UiTokens.PairRow <= foot)
            {
                cy = Header(s, at, m.TotalsHead, null, x, cy, w);
                for (int i = 0; i < m.Totals.Length && cy + UiTokens.PairRow <= foot; i++)
                {
                    cy += UiDraw.InfoRow(s, at, x, cy, w, m.Totals[i].Key, m.Totals[i].Value);
                }
            }
        }

        // A section's label in capitals, with a quiet word at its right. Returns the y under it.
        private static float Header(UiScene s, UiAt at, string label, string side, float x, float y, float w)
        {
            float h = UiDraw.Snap(UiTextRules.LineHeight(UiStyle.SectionHeader));
            float sw = string.IsNullOrEmpty(side) ? 0f : Math.Min(w * 0.5f, UiDraw.Snap(UiTextRules.Width(side, UiStyle.Small) + UiTokens.S1));
            UiDraw.Text(s, at, x, y, w - sw - (sw > 0f ? UiTokens.S3 : 0f), UiStyle.SectionHeader, UiRole.Muted, label, UiFlags.Label | UiFlags.Truncate);
            if (sw > 0f)
            {
                UiDraw.TextMiddle(s, at, x + w - sw, y, h, sw, UiStyle.Small, UiRole.Muted, side, UiFlags.Right | UiFlags.Truncate);
            }
            return y + h + UiTokens.S2;
        }

        // A line saying what there is none of. Returns its height.
        private static float Quiet(UiScene s, UiAt at, string words, float x, float y, float w)
        {
            if (string.IsNullOrEmpty(words))
            {
                return 0f;
            }
            UiDraw.TextMiddle(s, at, x, y, UiTokens.PairRow, w, UiStyle.Body, UiRole.Text2, words, UiFlags.Label | UiFlags.Truncate);
            return UiTokens.PairRow + UiTokens.S1;
        }

        // A thing coming up: its hour, its glyph on a tile, its name over where it is, and the link that leads there.
        private static void Next(UiScene s, UiAt at, ref UiPlanNext e, int index, string locateWord, float x, float y, float w)
        {
            int face = UiDraw.Box(s, at, x - UiTokens.S2, y, w + UiTokens.S4, NextRow, UiRole.None, UiRole.None, UiTokens.RadiusLarge);
            UiAt inside = at.In(face);
            UiDraw.TextMiddle(s, inside, x, y, NextRow, HourWidth, UiStyle.BodyStrong, e.Warn ? UiRole.WarnText : UiRole.Text, e.Time, UiFlags.Truncate);
            float tile = UiTokens.ControlSmall;
            float tx = x + HourWidth + UiTokens.S1;
            int t = UiDraw.Tile(s, inside, tx, y + (NextRow - tile) / 2f, tile, e.Tint, false);
            UiDraw.Icon(s, inside.In(t), tx + (tile - UiTokens.Icon) / 2f, y + (NextRow - UiTokens.Icon) / 2f, UiTokens.Icon, e.Icon, e.Tint, UiFlags.Middle | UiFlags.Centre, true);
            float textX = tx + tile + UiTokens.S3;
            bool locate = e.Locate && !string.IsNullOrEmpty(locateWord);
            float lw = locate ? Math.Max(UiTokens.MinTarget, UiDraw.Snap(UiTextRules.Width(locateWord, UiStyle.Small) + UiTokens.S3)) : 0f;
            float room = x + w - textX - lw;
            bool sub = !string.IsNullOrEmpty(e.Sub);
            float nameH = UiTextRules.LineHeight(UiStyle.BodyStrong), subH = UiTextRules.LineHeight(UiStyle.Small);
            float top = y + (NextRow - nameH - (sub ? subH : 0f)) / 2f;
            UiDraw.Text(s, inside, textX, top, room, UiStyle.BodyStrong, UiRole.Text, e.Title, UiFlags.Truncate | UiFlags.Middle);
            if (sub)
            {
                UiDraw.Text(s, inside, textX, top + nameH, room, UiStyle.Small, UiRole.Muted, e.Sub, UiFlags.Truncate | UiFlags.Middle);
            }
            if (locate)
            {
                UiDraw.TextMiddle(s, inside, x + w - lw, y, NextRow, lw, UiStyle.Small, UiRole.AccentText, locateWord, UiFlags.Right | UiFlags.Truncate);
                UiDraw.Node(s, at, x + w - lw, y, lw + UiTokens.S2, NextRow, LocateCommand, index, face);
            }
            UiDraw.Node(s, at, x - UiTokens.S2, y, w + UiTokens.S4 - lw, NextRow, RowCommand, index, face);
        }

        // A day after today: its name at the left, its first things at the right; a press opens that day.
        private static float Later(UiScene s, UiAt at, ref UiPlanLater e, int index, float x, float y, float w)
        {
            float h = UiTokens.PairRow;
            int face = UiDraw.Box(s, at, x - UiTokens.S2, y, w + UiTokens.S4, h, UiRole.None, UiRole.None, UiTokens.RadiusLarge);
            UiAt inside = at.In(face);
            float dw = UiDraw.Snap(Math.Min(w * 0.3f, UiTextRules.Width(e.Day, UiStyle.Body) + UiTokens.S2));
            UiDraw.TextMiddle(s, inside, x, y, h, dw, UiStyle.Body, UiRole.Text2, e.Day, UiFlags.Label | UiFlags.Truncate);
            UiDraw.TextMiddle(s, inside, x + dw + UiTokens.S2, y, h, w - dw - UiTokens.S2, UiStyle.Body, UiRole.Text, e.Title, UiFlags.Right | UiFlags.Truncate);
            UiDraw.Node(s, at, x - UiTokens.S2, y, w + UiTokens.S4, h, LaterCommand, index, face);
            return h;
        }
    }
}
